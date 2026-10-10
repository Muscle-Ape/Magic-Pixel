using System;
using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>项目内统一的插屏广告位名称，便于后台和日志区分触发来源。</summary>
public static class MPInterstitialAdPlace
{
    public const string GameEnter = "game_enter";
    public const string GameExit = "game_exit";
    public const string CompletedExit = "game_completed_exit";
}

public enum MPAdsType
{
    Interstitial,
    RewardedVideo
}

/// <summary>广告回调中使用的项目层数据，避免业务代码依赖具体广告 SDK 类型。</summary>
public sealed class MPAdsInfo
{
    public string AdUnitId { get; internal set; }
    public string PlacementName { get; internal set; }
    public MPAdsType AdType { get; internal set; }

    public override string ToString()
    {
        return $"AdUnitId:{AdUnitId} Placement:{PlacementName} AdType:{AdType}";
    }
}

/// <summary>
/// 项目广告模块入口。底层统一使用 Unity LevelPlay，实际竞价广告源由 LevelPlay 后台配置。
/// 负责初始化、广告预加载、失败重试、展示回调以及插屏冷却策略。
/// </summary>
public sealed class MPAdsManager : IDisposable
{
    private const float DEFAULT_INTERSTITIAL_INTERVAL = 120.0f;
    private const float INTERSTITIAL_INTERVAL_REDUCTION = 5.0f;
    private const float MINIMUM_INTERSTITIAL_INTERVAL = 60.0f;
    private const int INTERSTITIAL_VIEWS_PER_REDUCTION = 3;
    private const float REWARDED_CLOSE_GRACE_SECONDS = 1.0f;
    private const float ADAPTER_WARMUP_SECONDS = 1.0f;

    private static MPAdsManager m_instance;

    private MPAdsAdIntervalController m_adIntervalController;
    private MPAdsAdSceneController m_adSceneController;
    private MPAdsRuntime m_runtime;
    private MPUnityAdsSettings m_settings;
    private LevelPlayInterstitialAd m_interstitialAd;
    private LevelPlayRewardedAd m_rewardedAd;
    private Action m_pendingInitializedCallback;
    private Action<bool, bool> m_rewardedCompleted;
    private string m_interstitialPlacement;
    private string m_rewardedPlacement;
    private bool m_rewardReceived;
    private bool m_rewardedClosed;
    private bool m_rewardedShowing;
    private bool m_interstitialLoading;
    private bool m_rewardedLoading;
    private bool m_interstitialReloadScheduled;
    private bool m_rewardedReloadScheduled;
    private bool m_initializing;
    private bool m_disposed;
    private int m_interstitialRetryAttempt;
    private int m_rewardedRetryAttempt;
    private int m_rewardedOperationVersion;

    private MPAdsManager()
    {
    }

    public static MPAdsManager Instance => m_instance ??= new MPAdsManager();

    public MPAdsAdIntervalController AdIntervalController => m_adIntervalController;
    public MPAdsAdSceneController AdSceneController => m_adSceneController;
    public bool IsConfigured { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsRewardedReady => IsInitialized && m_rewardedAd != null && m_rewardedAd.IsAdReady();

    public event Action Initialized;
    public event Action<string, bool, MPAdsType> AdsWillDisplay;
    public event Action<string, MPAdsInfo> AdsDidDisplay;
    public event Action<string, MPAdsInfo, bool> AdsDidDismissed;
    public event Action<string, MPAdsInfo> AdsDidFailed;

    /// <summary>初始化 LevelPlay。广告对象只会在初始化成功后创建。</summary>
    public void Initialize(string userId = null, Action onInitialized = null)
    {
        if (!MPReleaseFeatures.Ads)
            return;

        if (IsInitialized)
        {
            InvokeSafely(onInitialized, nameof(onInitialized));
            return;
        }

        if (onInitialized != null)
            m_pendingInitializedCallback += onInitialized;

        if (m_initializing)
            return;

        m_settings = MPUnityAdsSettings.Load();
        if (m_settings == null)
        {
            Debug.LogError("[MPAdsManager] 未找到 Resources/MPUnityAdsSettings 配置资源。");
            CompleteInitializationFailure();
            return;
        }

        if (!m_settings.TryValidateCurrentPlatform(out string error))
        {
            Debug.LogError($"[MPAdsManager] Unity Ads 配置无效：{error}");
            CompleteInitializationFailure();
            return;
        }

        m_disposed = false;
        m_adIntervalController = new MPAdsAdIntervalController(
            DEFAULT_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_INTERVAL_REDUCTION,
            MINIMUM_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_VIEWS_PER_REDUCTION);
        m_adSceneController = new MPAdsAdSceneController();
        m_runtime = MPAdsRuntime.Create();

        LevelPlay.OnInitSuccess += OnInitializationSuccess;
        LevelPlay.OnInitFailed += OnInitializationFailed;

        IsConfigured = true;
        m_initializing = true;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        LevelPlay.SetAdaptersDebug(true);
        if (m_settings.LaunchLevelPlayTestSuiteInDevelopment)
        {
            // 测试套件要求在 SDK 初始化前启用，并且启动前不能创建广告对象。
            LevelPlay.SetMetaData("is_test_suite", "enable");
        }
#endif

        // 当前项目未使用服务端奖励校验，不把可能含连字符的玩家 ID
        // 传给 Dynamic User ID，避免违反其仅允许 1-64 位字母数字的约束。
        Debug.Log("[MPAdsManager] 正在初始化 LevelPlay。");
        LevelPlay.Init(m_settings.CurrentAppKey);
    }

    /// <summary>按免广告权益、关卡数和冷却时间尝试展示插屏。</summary>
    public void TryShowInterstitial(string adPlace)
    {
        if (!CanAttemptInterstitial(adPlace))
            return;

        bool ready = m_interstitialAd != null && m_interstitialAd.IsAdReady();
        InvokeSafely(AdsWillDisplay, adPlace, ready, MPAdsType.Interstitial, nameof(AdsWillDisplay));

        if (!ready)
        {
            RequestInterstitialLoad();
            return;
        }

        m_interstitialPlacement = adPlace;
        try
        {
            m_interstitialAd.ShowAd(adPlace);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPAdsManager] 插屏广告调用失败 {adPlace}：{exception.Message}");
            HandleInterstitialFailure();
        }
    }

    /// <summary>
    /// 展示激励视频。回调参数依次为“调用时是否已准备”和“是否获得奖励”。
    /// </summary>
    public void ShowRewardedAd(string adPlace, Action<bool, bool> onCompleted)
    {
        bool ready = IsRewardedReady && !m_rewardedShowing;
        InvokeSafely(AdsWillDisplay, adPlace, ready, MPAdsType.RewardedVideo, nameof(AdsWillDisplay));

        if (!ready)
        {
            RequestRewardedLoad();
            InvokeSafely(onCompleted, false, false, nameof(onCompleted));
            return;
        }

        m_rewardedPlacement = adPlace;
        m_rewardedCompleted = onCompleted;
        m_rewardedShowing = true;
        m_rewardReceived = false;
        m_rewardedClosed = false;
        m_rewardedOperationVersion++;

        try
        {
            m_rewardedAd.ShowAd(adPlace);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPAdsManager] 激励广告调用失败 {adPlace}：{exception.Message}");
            HandleRewardedFailure();
        }
    }

    public void EnterGameplay()
    {
        if (MPReleaseFeatures.Ads && IsConfigured)
            m_adIntervalController?.EnterGameplay();
    }

    public void LeaveGameplay()
    {
        if (MPReleaseFeatures.Ads && IsConfigured)
            m_adIntervalController?.LeaveGameplay();
    }

    internal void Tick(float unscaledDeltaTime)
    {
        m_adIntervalController?.Tick(unscaledDeltaTime);
    }

    private void OnInitializationSuccess(LevelPlayConfiguration configuration)
    {
        UnsubscribeInitializationEvents();
        m_initializing = false;
        IsInitialized = true;
        Debug.Log("[MPAdsManager] LevelPlay 初始化成功。");
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (m_settings.LaunchLevelPlayTestSuiteInDevelopment)
        {
            Debug.Log("[MPAdsManager] 正在启动 LevelPlay Integration Test Suite。");
            LevelPlay.LaunchTestSuite();
        }
        else
#endif
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            LevelPlay.ValidateIntegration();
#endif
            Debug.Log("[MPAdsManager] 准备加载广告。");
            CreateAdObjects();
        }

        Action callback = m_pendingInitializedCallback;
        m_pendingInitializedCallback = null;
        InvokeSafely(Initialized, nameof(Initialized));
        InvokeSafely(callback, nameof(m_pendingInitializedCallback));
    }

    private void OnInitializationFailed(LevelPlayInitError error)
    {
        UnsubscribeInitializationEvents();
        Debug.LogError($"[MPAdsManager] LevelPlay 初始化失败：{error}");
        CompleteInitializationFailure();
    }

    private void CompleteInitializationFailure()
    {
        m_initializing = false;
        IsInitialized = false;
        IsConfigured = false;
        m_pendingInitializedCallback = null;

        if (m_runtime != null)
        {
            UnityEngine.Object.Destroy(m_runtime.gameObject);
            m_runtime = null;
        }

        m_adIntervalController = null;
        m_adSceneController = null;
    }

    private void UnsubscribeInitializationEvents()
    {
        LevelPlay.OnInitSuccess -= OnInitializationSuccess;
        LevelPlay.OnInitFailed -= OnInitializationFailed;
    }

    private void CreateAdObjects()
    {
        DestroyAdObjects();

        m_interstitialAd = new LevelPlayInterstitialAd(m_settings.CurrentInterstitialAdUnitId);
        m_interstitialAd.OnAdLoaded += OnInterstitialLoaded;
        m_interstitialAd.OnAdLoadFailed += OnInterstitialLoadFailed;
        m_interstitialAd.OnAdDisplayed += OnInterstitialDisplayed;
        m_interstitialAd.OnAdDisplayFailed += OnInterstitialDisplayFailed;
        m_interstitialAd.OnAdClosed += OnInterstitialClosed;

        m_rewardedAd = new LevelPlayRewardedAd(m_settings.CurrentRewardedAdUnitId);
        m_rewardedAd.OnAdLoaded += OnRewardedLoaded;
        m_rewardedAd.OnAdLoadFailed += OnRewardedLoadFailed;
        m_rewardedAd.OnAdDisplayed += OnRewardedDisplayed;
        m_rewardedAd.OnAdDisplayFailed += OnRewardedDisplayFailed;
        m_rewardedAd.OnAdRewarded += OnRewarded;
        m_rewardedAd.OnAdClosed += OnRewardedClosed;

        // LevelPlay 初始化成功时，聚合网络 Adapter 仍可能处于原生初始化收尾阶段。
        // 稍后再发起首次加载，避免 Unity Ads 在 token 尚未可用时收到请求。
        ScheduleInterstitialLoad(ADAPTER_WARMUP_SECONDS, false);
        ScheduleRewardedLoad(ADAPTER_WARMUP_SECONDS, false);
    }

    private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
    {
        m_interstitialLoading = false;
        m_interstitialReloadScheduled = false;
        m_interstitialRetryAttempt = 0;
        Debug.Log("[MPAdsManager] 插屏广告加载成功。");
    }

    private void OnInterstitialLoadFailed(LevelPlayAdError error)
    {
        m_interstitialLoading = false;
        Debug.LogWarning($"[MPAdsManager] 插屏广告加载失败：{error}");
        ScheduleInterstitialReload();
    }

    private void OnInterstitialDisplayed(LevelPlayAdInfo adInfo)
    {
        MPAdsInfo info = CreateInfo(MPAdsType.Interstitial, m_interstitialPlacement);
        m_adIntervalController?.RecordInterstitialDisplayed();
        InvokeSafely(AdsDidDisplay, m_interstitialPlacement, info, nameof(AdsDidDisplay));
    }

    private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogWarning($"[MPAdsManager] 插屏广告展示失败：{error}");
        HandleInterstitialFailure();
    }

    private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
    {
        string placement = m_interstitialPlacement;
        MPAdsInfo info = CreateInfo(MPAdsType.Interstitial, placement);
        m_interstitialPlacement = null;
        InvokeSafely(AdsDidDismissed, placement, info, false, nameof(AdsDidDismissed));
        RequestInterstitialLoad();
    }

    private void HandleInterstitialFailure()
    {
        string placement = m_interstitialPlacement;
        MPAdsInfo info = CreateInfo(MPAdsType.Interstitial, placement);
        m_interstitialPlacement = null;
        InvokeSafely(AdsDidFailed, placement, info, nameof(AdsDidFailed));
        InvokeSafely(AdsDidDismissed, placement, info, false, nameof(AdsDidDismissed));
        ScheduleInterstitialReload();
    }

    private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
    {
        m_rewardedLoading = false;
        m_rewardedReloadScheduled = false;
        m_rewardedRetryAttempt = 0;
        Debug.Log("[MPAdsManager] 激励广告加载成功。");
    }

    private void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        m_rewardedLoading = false;
        Debug.LogWarning($"[MPAdsManager] 激励广告加载失败：{error}");
        ScheduleRewardedReload();
    }

    private void OnRewardedDisplayed(LevelPlayAdInfo adInfo)
    {
        MPAdsInfo info = CreateInfo(MPAdsType.RewardedVideo, m_rewardedPlacement);
        InvokeSafely(AdsDidDisplay, m_rewardedPlacement, info, nameof(AdsDidDisplay));
    }

    private void OnRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogWarning($"[MPAdsManager] 激励广告展示失败：{error}");
        HandleRewardedFailure();
    }

    private void OnRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        m_rewardReceived = true;
        if (m_rewardedClosed)
            CompleteRewarded(true, true);
    }

    private void OnRewardedClosed(LevelPlayAdInfo adInfo)
    {
        string placement = m_rewardedPlacement;
        MPAdsInfo info = CreateInfo(MPAdsType.RewardedVideo, placement);
        m_rewardedClosed = true;
        InvokeSafely(AdsDidDismissed, placement, info, m_rewardReceived, nameof(AdsDidDismissed));
        RequestRewardedLoad();

        if (m_rewardReceived)
        {
            CompleteRewarded(true, true);
            return;
        }

        // LevelPlay 明确说明奖励和关闭回调的顺序不固定，短暂等待可避免
        // “先关闭、后奖励”时错误判定为未完成。
        int operationVersion = m_rewardedOperationVersion;
        m_runtime?.RunDelayed(REWARDED_CLOSE_GRACE_SECONDS, () =>
        {
            if (operationVersion == m_rewardedOperationVersion && m_rewardedShowing)
                CompleteRewarded(true, m_rewardReceived);
        });
    }

    private void HandleRewardedFailure()
    {
        string placement = m_rewardedPlacement;
        MPAdsInfo info = CreateInfo(MPAdsType.RewardedVideo, placement);
        InvokeSafely(AdsDidFailed, placement, info, nameof(AdsDidFailed));
        InvokeSafely(AdsDidDismissed, placement, info, false, nameof(AdsDidDismissed));
        CompleteRewarded(true, false);
        ScheduleRewardedReload();
    }

    private void CompleteRewarded(bool ready, bool success)
    {
        Action<bool, bool> callback = m_rewardedCompleted;
        m_rewardedCompleted = null;
        m_rewardedPlacement = null;
        m_rewardedShowing = false;
        m_rewardReceived = false;
        m_rewardedClosed = false;
        m_rewardedOperationVersion++;
        InvokeSafely(callback, ready, success, nameof(m_rewardedCompleted));
    }

    private void ScheduleInterstitialReload()
    {
        m_interstitialRetryAttempt++;
        float delay = Mathf.Pow(2.0f, Mathf.Min(6, m_interstitialRetryAttempt));
        ScheduleInterstitialLoad(delay, true);
    }

    private void ScheduleRewardedReload()
    {
        m_rewardedRetryAttempt++;
        float delay = Mathf.Pow(2.0f, Mathf.Min(6, m_rewardedRetryAttempt));
        ScheduleRewardedLoad(delay, true);
    }

    private void ScheduleInterstitialLoad(float delaySeconds, bool isRetry)
    {
        if (m_disposed || !IsInitialized || m_runtime == null || m_interstitialAd == null
            || m_interstitialLoading || m_interstitialReloadScheduled || m_interstitialAd.IsAdReady())
            return;

        m_interstitialReloadScheduled = true;
        if (isRetry)
            Debug.Log($"[MPAdsManager] 将在 {delaySeconds:0.#} 秒后重试加载插屏广告。");
        m_runtime.RunDelayed(delaySeconds, () =>
        {
            m_interstitialReloadScheduled = false;
            RequestInterstitialLoad();
        });
    }

    private void ScheduleRewardedLoad(float delaySeconds, bool isRetry)
    {
        if (m_disposed || !IsInitialized || m_runtime == null || m_rewardedAd == null
            || m_rewardedLoading || m_rewardedReloadScheduled || m_rewardedAd.IsAdReady())
            return;

        m_rewardedReloadScheduled = true;
        if (isRetry)
            Debug.Log($"[MPAdsManager] 将在 {delaySeconds:0.#} 秒后重试加载激励广告。");
        m_runtime.RunDelayed(delaySeconds, () =>
        {
            m_rewardedReloadScheduled = false;
            RequestRewardedLoad();
        });
    }

    private void RequestInterstitialLoad()
    {
        if (m_disposed || !IsInitialized || m_interstitialAd == null
            || m_interstitialLoading || m_interstitialReloadScheduled || m_interstitialAd.IsAdReady())
            return;

        m_interstitialLoading = true;
        try
        {
            m_interstitialAd.LoadAd();
        }
        catch (Exception exception)
        {
            m_interstitialLoading = false;
            Debug.LogWarning($"[MPAdsManager] 插屏广告加载调用异常：{exception.Message}");
            ScheduleInterstitialReload();
        }
    }

    private void RequestRewardedLoad()
    {
        if (m_disposed || !IsInitialized || m_rewardedAd == null || (m_rewardedShowing && !m_rewardedClosed)
            || m_rewardedLoading || m_rewardedReloadScheduled || m_rewardedAd.IsAdReady())
            return;

        m_rewardedLoading = true;
        try
        {
            m_rewardedAd.LoadAd();
        }
        catch (Exception exception)
        {
            m_rewardedLoading = false;
            Debug.LogWarning($"[MPAdsManager] 激励广告加载调用异常：{exception.Message}");
            ScheduleRewardedReload();
        }
    }

    private bool CanAttemptInterstitial(string adPlace)
    {
        if (!MPReleaseFeatures.Ads || !IsConfigured || !IsInitialized)
            return false;
        if (m_adSceneController == null || !m_adSceneController.IsEnabled(adPlace))
            return false;
        if (m_adIntervalController == null || !m_adIntervalController.CanAdPlay())
            return false;

        try
        {
            MPUser user = MPUser.instance;
            return user != null
                && !user.HasVipAccess()
                && !user.OwnsShopEntitlement("remove_ads")
                && user.GetCompletedMainLevelCount() >= 10;
        }
        catch
        {
            return false;
        }
    }

    private MPAdsInfo CreateInfo(MPAdsType type, string placement)
    {
        return new MPAdsInfo
        {
            AdUnitId = type == MPAdsType.Interstitial
                ? m_settings?.CurrentInterstitialAdUnitId
                : m_settings?.CurrentRewardedAdUnitId,
            PlacementName = placement,
            AdType = type
        };
    }

    public void Dispose()
    {
        if (m_disposed)
            return;

        m_disposed = true;
        UnsubscribeInitializationEvents();
        DestroyAdObjects();

        if (m_runtime != null)
        {
            UnityEngine.Object.Destroy(m_runtime.gameObject);
            m_runtime = null;
        }

        m_adIntervalController = null;
        m_adSceneController = null;
        m_pendingInitializedCallback = null;
        m_rewardedCompleted = null;
        m_rewardedShowing = false;
        m_interstitialLoading = false;
        m_rewardedLoading = false;
        m_interstitialReloadScheduled = false;
        m_rewardedReloadScheduled = false;
        IsConfigured = false;
        IsInitialized = false;
        m_initializing = false;
    }

    private void DestroyAdObjects()
    {
        m_interstitialLoading = false;
        m_rewardedLoading = false;
        m_interstitialReloadScheduled = false;
        m_rewardedReloadScheduled = false;

        if (m_interstitialAd != null)
        {
            m_interstitialAd.OnAdLoaded -= OnInterstitialLoaded;
            m_interstitialAd.OnAdLoadFailed -= OnInterstitialLoadFailed;
            m_interstitialAd.OnAdDisplayed -= OnInterstitialDisplayed;
            m_interstitialAd.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
            m_interstitialAd.OnAdClosed -= OnInterstitialClosed;
            m_interstitialAd.DestroyAd();
            m_interstitialAd = null;
        }

        if (m_rewardedAd != null)
        {
            m_rewardedAd.OnAdLoaded -= OnRewardedLoaded;
            m_rewardedAd.OnAdLoadFailed -= OnRewardedLoadFailed;
            m_rewardedAd.OnAdDisplayed -= OnRewardedDisplayed;
            m_rewardedAd.OnAdDisplayFailed -= OnRewardedDisplayFailed;
            m_rewardedAd.OnAdRewarded -= OnRewarded;
            m_rewardedAd.OnAdClosed -= OnRewardedClosed;
            m_rewardedAd.DestroyAd();
            m_rewardedAd = null;
        }
    }

    private static void InvokeSafely(Action callback, string callbackName)
    {
        if (callback == null)
            return;
        try { callback.Invoke(); }
        catch (Exception exception) { Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}"); }
    }

    private static void InvokeSafely<T1, T2>(Action<T1, T2> callback, T1 arg1, T2 arg2, string callbackName)
    {
        if (callback == null)
            return;
        try { callback.Invoke(arg1, arg2); }
        catch (Exception exception) { Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}"); }
    }

    private static void InvokeSafely<T1, T2, T3>(Action<T1, T2, T3> callback, T1 arg1, T2 arg2, T3 arg3, string callbackName)
    {
        if (callback == null)
            return;
        try { callback.Invoke(arg1, arg2, arg3); }
        catch (Exception exception) { Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}"); }
    }
}

/// <summary>仅负责低频计时和延迟重试，不承载广告业务状态。</summary>
internal sealed class MPAdsRuntime : MonoBehaviour
{
    public static MPAdsRuntime Create()
    {
        GameObject gameObject = new GameObject(nameof(MPAdsRuntime));
        DontDestroyOnLoad(gameObject);
        return gameObject.AddComponent<MPAdsRuntime>();
    }

    public void RunDelayed(float delay, Action callback)
    {
        StartCoroutine(DelayCoroutine(delay, callback));
    }

    private IEnumerator DelayCoroutine(float delay, Action callback)
    {
        yield return new WaitForSecondsRealtime(delay);
        callback?.Invoke();
    }

    private void Update()
    {
        MPAdsManager.Instance.Tick(Time.unscaledDeltaTime);
    }
}

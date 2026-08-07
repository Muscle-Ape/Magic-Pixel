using System;
using UnityEngine;

/// <summary>
/// 可选的广告策略回调接口。
/// 被 MPAdsManager 注入的策略可以实现该接口，以接收 AOAds 的生命周期回调。
/// </summary>
public interface IMPAdsCallbackReceiver
{
    void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType);
    void OnAdsDidDisplay(string adScene, AOAdsInfo info);
    void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward);
    void OnAdsDidFailed(string adScene, AOAdsInfo info);
}

/// <summary>
/// 项目广告模块的装配与回调入口。
/// 负责向 AOAds 注入控制策略、初始化 SDK，并统一接收和转发广告回调。
/// </summary>
public sealed class MPAdsManager : IDisposable
{
    private const float DEFAULT_INTERSTITIAL_INTERVAL = 120.0f;
    private const float INTERSTITIAL_INTERVAL_REDUCTION = 5.0f;
    private const float MINIMUM_INTERSTITIAL_INTERVAL = 60.0f;
    private const int INTERSTITIAL_VIEWS_PER_REDUCTION = 3;

    private static MPAdsManager m_instance;

    private MPAdsAdIntervalController m_adIntervalController;
    private AOAdsBaseAdSceneController m_adSceneController;
    private bool m_isSubscribed;

    private MPAdsManager()
    {
    }

    public static MPAdsManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPAdsManager();
            }

            return m_instance;
        }
    }

    public MPAdsAdIntervalController AdIntervalController => m_adIntervalController;
    public AOAdsBaseAdSceneController AdSceneController => m_adSceneController;
    public bool IsConfigured { get; private set; }
    public bool IsInitialized => AOAds.InitComplete;

    public event Action Initialized;
    public event Action<string, bool, AOAdsType> AdsWillDisplay;
    public event Action<string, AOAdsInfo> AdsDidDisplay;
    public event Action<string, AOAdsInfo, bool> AdsDidDismissed;
    public event Action<string, AOAdsInfo> AdsDidFailed;

    /// <summary>
    /// 创建项目广告策略、注入 AOAds 并完成初始化。
    /// 首次初始化时应在 AOAds.Start 执行前调用，确保 userId 能传递给广告 SDK。
    /// </summary>
    public void Initialize(
        string userId = null,
        Action onInitialized = null)
    {
        m_adIntervalController = new MPAdsAdIntervalController(
            DEFAULT_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_INTERVAL_REDUCTION,
            MINIMUM_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_VIEWS_PER_REDUCTION);
        m_adSceneController = new AOAdsBaseAdSceneController();

        AOAds ads = AOAds.Instance;
        ads.AdIntervalController = m_adIntervalController;
        ads.AdSceneController = m_adSceneController;

        SubscribeAOAdsCallbacks();
        IsConfigured = true;

        if (AOAds.InitComplete)
        {
            OnInitialized(onInitialized);
        }
        else
        {
            ads.Init(userId, () => OnInitialized(onInitialized));
        }
    }

    public void Dispose()
    {
        UnsubscribeAOAdsCallbacks();
        m_adIntervalController = null;
        m_adSceneController = null;
        IsConfigured = false;
    }

    private void SubscribeAOAdsCallbacks()
    {
        if (m_isSubscribed)
        {
            return;
        }

        AOAds.AdsWillDisplay += OnAdsWillDisplay;
        AOAds.AdsDidDisplay += OnAdsDidDisplay;
        AOAds.AdsDidDismissed += OnAdsDidDismissed;
        AOAds.AdsDidFailed += OnAdsDidFailed;
        m_isSubscribed = true;
    }

    private void UnsubscribeAOAdsCallbacks()
    {
        if (!m_isSubscribed)
        {
            return;
        }

        AOAds.AdsWillDisplay -= OnAdsWillDisplay;
        AOAds.AdsDidDisplay -= OnAdsDidDisplay;
        AOAds.AdsDidDismissed -= OnAdsDidDismissed;
        AOAds.AdsDidFailed -= OnAdsDidFailed;
        m_isSubscribed = false;
    }

    private void OnInitialized(Action onInitialized)
    {
        InvokeSafely(Initialized, nameof(Initialized));
        InvokeSafely(onInitialized, nameof(onInitialized));
    }

    private void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType)
    {
        NotifyStrategy(receiver => receiver.OnAdsWillDisplay(adScene, ready, adType));
        InvokeSafely(AdsWillDisplay, adScene, ready, adType, nameof(AdsWillDisplay));
    }

    private void OnAdsDidDisplay(string adScene, AOAdsInfo info)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidDisplay(adScene, info));
        InvokeSafely(AdsDidDisplay, adScene, info, nameof(AdsDidDisplay));
    }

    private void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidDismissed(adScene, info, isReceivedReward));
        InvokeSafely(AdsDidDismissed, adScene, info, isReceivedReward, nameof(AdsDidDismissed));
    }

    private void OnAdsDidFailed(string adScene, AOAdsInfo info)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidFailed(adScene, info));
        InvokeSafely(AdsDidFailed, adScene, info, nameof(AdsDidFailed));
    }

    private void NotifyStrategy(Action<IMPAdsCallbackReceiver> callback)
    {
        IMPAdsCallbackReceiver intervalReceiver = m_adIntervalController as IMPAdsCallbackReceiver;
        IMPAdsCallbackReceiver sceneReceiver = m_adSceneController as IMPAdsCallbackReceiver;

        InvokeStrategySafely(intervalReceiver, callback);
        if (!ReferenceEquals(intervalReceiver, sceneReceiver))
        {
            InvokeStrategySafely(sceneReceiver, callback);
        }
    }

    private static void InvokeStrategySafely(
        IMPAdsCallbackReceiver receiver,
        Action<IMPAdsCallbackReceiver> callback)
    {
        if (receiver == null)
        {
            return;
        }

        try
        {
            callback(receiver);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAdsManager] 广告策略回调异常：{exception}");
        }
    }

    private static void InvokeSafely(Action callback, string callbackName)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}");
        }
    }

    private static void InvokeSafely<T1, T2>(
        Action<T1, T2> callback,
        T1 arg1,
        T2 arg2,
        string callbackName)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke(arg1, arg2);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}");
        }
    }

    private static void InvokeSafely<T1, T2, T3>(
        Action<T1, T2, T3> callback,
        T1 arg1,
        T2 arg2,
        T3 arg3,
        string callbackName)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback.Invoke(arg1, arg2, arg3);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAdsManager] {callbackName} 回调异常：{exception}");
        }
    }
}

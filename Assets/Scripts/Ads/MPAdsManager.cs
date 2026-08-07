using System;
using UnityEngine;

/// <summary>
/// 可选的广告策略回调接口。
/// 被 MPAdsManager 注入的策略可以实现该接口，以接收 AOAds 的生命周期回调。
/// </summary>
public interface IMPAdsCallbackReceiver
{
    /// <summary>
    /// 广告即将展示时调用，同时返回广告是否已经准备完成。
    /// </summary>
    void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType);

    /// <summary>
    /// 广告已经成功展示时调用。
    /// </summary>
    void OnAdsDidDisplay(string adScene, AOAdsInfo info);

    /// <summary>
    /// 广告关闭时调用；激励广告可通过 isReceivedReward 判断是否获得奖励。
    /// </summary>
    void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward);

    /// <summary>
    /// 广告展示失败时调用。
    /// </summary>
    void OnAdsDidFailed(string adScene, AOAdsInfo info);
}

/// <summary>
/// 项目广告模块的装配与回调入口。
/// 负责向 AOAds 注入控制策略、初始化 SDK，并统一接收和转发广告回调。
/// </summary>
public sealed class MPAdsManager : IDisposable
{
    /// <summary>
    /// 初始插屏广告间隔，单位为秒。
    /// </summary>
    private const float DEFAULT_INTERSTITIAL_INTERVAL = 120.0f;

    /// <summary>
    /// 每次调整时减少的插屏广告间隔，单位为秒。
    /// </summary>
    private const float INTERSTITIAL_INTERVAL_REDUCTION = 5.0f;

    /// <summary>
    /// 插屏广告允许降低到的最小间隔，单位为秒。
    /// </summary>
    private const float MINIMUM_INTERSTITIAL_INTERVAL = 60.0f;

    /// <summary>
    /// 每成功展示多少次插屏广告后，减少一次间隔。
    /// </summary>
    private const int INTERSTITIAL_VIEWS_PER_REDUCTION = 3;

    /// <summary>
    /// 广告管理器单例。
    /// </summary>
    private static MPAdsManager m_instance;

    /// <summary>
    /// 当前注入 AOAds 的插屏间隔策略。
    /// </summary>
    private MPAdsAdIntervalController m_adIntervalController;

    /// <summary>
    /// 当前注入 AOAds 的广告位启用策略。
    /// </summary>
    private AOAdsBaseAdSceneController m_adSceneController;

    /// <summary>
    /// 是否已经订阅 AOAds 的静态回调，避免重复注册。
    /// </summary>
    private bool m_isSubscribed;

    private MPAdsManager()
    {
    }

    /// <summary>
    /// 获取广告管理器单例。
    /// </summary>
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

    /// <summary>
    /// 当前使用的插屏间隔控制器。
    /// </summary>
    public MPAdsAdIntervalController AdIntervalController => m_adIntervalController;

    /// <summary>
    /// 当前使用的广告位控制器。
    /// </summary>
    public AOAdsBaseAdSceneController AdSceneController => m_adSceneController;

    /// <summary>
    /// 广告策略是否已经创建并注入 AOAds。
    /// </summary>
    public bool IsConfigured { get; private set; }

    /// <summary>
    /// AOAds SDK 是否已经完成初始化。
    /// </summary>
    public bool IsInitialized => AOAds.InitComplete;

    /// <summary>
    /// AOAds 初始化完成事件。
    /// </summary>
    public event Action Initialized;

    /// <summary>
    /// 广告即将展示事件。
    /// </summary>
    public event Action<string, bool, AOAdsType> AdsWillDisplay;

    /// <summary>
    /// 广告成功展示事件。
    /// </summary>
    public event Action<string, AOAdsInfo> AdsDidDisplay;

    /// <summary>
    /// 广告关闭事件。
    /// </summary>
    public event Action<string, AOAdsInfo, bool> AdsDidDismissed;

    /// <summary>
    /// 广告展示失败事件。
    /// </summary>
    public event Action<string, AOAdsInfo> AdsDidFailed;

    /// <summary>
    /// 创建项目广告策略、注入 AOAds 并完成初始化。
    /// 首次初始化时应在 AOAds.Start 执行前调用，确保 userId 能传递给广告 SDK。
    /// </summary>
    /// <param name="userId">当前玩家 ID，可为空。</param>
    /// <param name="onInitialized">本次初始化完成后的回调。</param>
    public void Initialize(
        string userId = null,
        Action onInitialized = null)
    {
        // 间隔策略和广告位策略统一由管理器创建，外部无需持有或注入实例。
        m_adIntervalController = new MPAdsAdIntervalController(
            DEFAULT_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_INTERVAL_REDUCTION,
            MINIMUM_INTERSTITIAL_INTERVAL,
            INTERSTITIAL_VIEWS_PER_REDUCTION);
        m_adSceneController = new AOAdsBaseAdSceneController();

        // 在 AOAds 启动广告 SDK 前完成策略注入。
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

    /// <summary>
    /// 解除 AOAds 回调并释放当前策略引用。
    /// </summary>
    public void Dispose()
    {
        UnsubscribeAOAdsCallbacks();
        m_adIntervalController = null;
        m_adSceneController = null;
        IsConfigured = false;
    }

    /// <summary>
    /// 订阅 AOAds 对外回调；重复调用不会产生重复监听。
    /// </summary>
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

    /// <summary>
    /// 解除所有 AOAds 回调监听。
    /// </summary>
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

    /// <summary>
    /// 处理 AOAds 初始化完成，并依次通知全局事件和本次调用回调。
    /// </summary>
    private void OnInitialized(Action onInitialized)
    {
        InvokeSafely(Initialized, nameof(Initialized));
        InvokeSafely(onInitialized, nameof(onInitialized));
    }

    /// <summary>
    /// 接收 AOAds 的广告即将展示回调，并转发给策略和业务监听者。
    /// </summary>
    private void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType)
    {
        NotifyStrategy(receiver => receiver.OnAdsWillDisplay(adScene, ready, adType));
        InvokeSafely(AdsWillDisplay, adScene, ready, adType, nameof(AdsWillDisplay));
    }

    /// <summary>
    /// 接收 AOAds 的广告成功展示回调，并转发给策略和业务监听者。
    /// </summary>
    private void OnAdsDidDisplay(string adScene, AOAdsInfo info)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidDisplay(adScene, info));
        InvokeSafely(AdsDidDisplay, adScene, info, nameof(AdsDidDisplay));
    }

    /// <summary>
    /// 接收 AOAds 的广告关闭回调，并转发给策略和业务监听者。
    /// </summary>
    private void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidDismissed(adScene, info, isReceivedReward));
        InvokeSafely(AdsDidDismissed, adScene, info, isReceivedReward, nameof(AdsDidDismissed));
    }

    /// <summary>
    /// 接收 AOAds 的广告展示失败回调，并转发给策略和业务监听者。
    /// </summary>
    private void OnAdsDidFailed(string adScene, AOAdsInfo info)
    {
        NotifyStrategy(receiver => receiver.OnAdsDidFailed(adScene, info));
        InvokeSafely(AdsDidFailed, adScene, info, nameof(AdsDidFailed));
    }

    /// <summary>
    /// 将 AOAds 回调通知给实现了 IMPAdsCallbackReceiver 的已注入策略。
    /// </summary>
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

    /// <summary>
    /// 安全执行策略回调，避免单个策略异常中断 AOAds 的回调链。
    /// </summary>
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

    /// <summary>
    /// 安全执行无参数业务回调。
    /// </summary>
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

    /// <summary>
    /// 安全执行带两个参数的业务回调。
    /// </summary>
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

    /// <summary>
    /// 安全执行带三个参数的业务回调。
    /// </summary>
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

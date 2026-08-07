using UnityEngine;
using System;
using System.Collections;

public enum AOAdsType
{
    None,
    Interstitial,
    RewardedVideo,
    AppOpen
}
public class AOAds : MonoBehaviour
{
    /// <summary>
    /// 启用 AD BREAK页面
    /// </summary>
    public static bool EnableAdBreak = false;

    /// <summary>
    /// 启用唤醒广告 (可以理解为开屏广告:AppOpen)
    /// </summary>
    public static bool EnableWakeUpAd = false;

    /// <summary>
    /// 使用插屏广告作为唤醒广告,否则会使用开屏广告AppOpen作为唤醒广告
    /// </summary>
    public static bool UseInterWakeUpAd = false;

    /// <summary>
    /// 启用后阻止下一次唤醒广告,阻止后会自动置为false. PS:被阻止时,在应用在后台的时长不会记录
    /// </summary>
    public static bool PreventNextAdWakeUp = false;

    public static bool InitComplete { get; private set; }
    public AOAdsBaseAdIntervalController AdIntervalController { get { return _adIntervalController; } set { _adIntervalController = value; } } //插屏间隔控制器
    public AOAdsBaseAdSceneController AdSceneController { get { return _adSceneController; } set { _adSceneController = value; } } //插屏位置控制器

    public static event Action<string, bool, AOAdsType> AdsWillDisplay;//将要展示
    public static event Action<string, AOAdsInfo> AdsDidDisplay;//已展示
    public static event Action<string, AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public static event Action<string, AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)
    public event Action _onInitialized;
    private AOAdsAgentInterface _agent;
    private AOAdsAgentInterface _defaultAgent = new AOAdsDefaultAgent();
    protected static AOAds instance;
    private static bool _onApplicationQuit;
    private AOAdsBaseAdIntervalController _adIntervalController = new AOAdsBaseAdIntervalController();//插屏间隔控制器
    private AOAdsBaseAdSceneController _adSceneController = new AOAdsBaseAdSceneController();//插屏位置控制器
    private string _userId;
    private bool _adDisplaying = false;//广告展示中,防止连续调用问题
    private string _adScene;
    private Action<bool> _activeAdCompletion;
    private Coroutine _adDisplayingWatchdog;
    private const float AdDisplayingTimeout = 120.0f;
    // private bool _adDisplayingFixAudio = false;//广告展示中,处理声音没有的问题


    #region 可以用API
    //插屏准备完毕
    public static bool IsInterstitialReady(bool withLoad = true)
    {
        bool isInterstitialReady = Agent().IsInterstitialReady(withLoad);
        // AOAds.DebugLog("IsInterstitialReady:" + isInterstitialReady);
        return isInterstitialReady;
    }

    //显示插屏   PS: onDisplay回调可能不在主线程执行
    public static void ShowInterstitial(string adScene, Action<bool> interstitialOnCompleted)
    {
        Instance._ShowInterstitial(adScene, interstitialOnCompleted);

    }

    //视频准备完毕
    public static bool IsRewardedAdReady(bool withLoad = true)
    {
        bool isRewardedAdReady = Agent().IsRewardedAdReady(withLoad);
        // DebugLog("isRewardedAdReady:" + isRewardedAdReady);

        return isRewardedAdReady;
    }

    //开屏准备完毕
    public static bool IsAppOpenAdReady()
    {
        bool ready = Agent().IsAppOpenReady();
        // DebugLog("IsAppOpenReady:" + ready);
        return ready;
    }

    //显示视频    PS: onDisplay回调可能不在主线程执行
    public static void ShowRewardedVideo(string adScene, Action<bool> rewardVideoOnCompleted)
    {
        Instance._ShowRewardedVideo(adScene, rewardVideoOnCompleted);
    }


    public static void LoadInterstitial()
    {
        DebugLog("LoadInterstitial");
        Agent().LoadInterstitial();
    }

    public static void LoadRewardedVideo()
    {
        DebugLog("LoadRewardedVideo");
        Agent().LoadRewardedVideo();
    }


    public static void LoadBanner()
    {
        DebugLog("LoadBanner");
        Agent().LoadBanner();
    }

    public static void LoadAppOpen()
    {
        DebugLog("LoadAppOpen");
        Agent().LoadAppOpen();
    }

    //展示banner
    public static void ShowBanner(Action<float> bannerAdLoaded)
    {
        DebugLog("ShowBanner");
        if (!InitComplete)
        {
            return;
        }

        Agent().ShowBanner(bannerAdLoaded);
    }

    //隐藏banner
    public static void HideBanner()
    {
        DebugLog("HideBanner");
        if (InitComplete)//防止错误信息过多
        {
            Agent().HideBanner();
        }
    }

    // //插屏收入
    // public static void InterstitialAdRevenuePaid(Action<string, AOAdsInfo> revenuePaid)
    // {
    //     Agent().InterstitialAdRevenuePaid(revenuePaid);
    // }

    // //视频收入
    // public static void RewardedAdRevenuePaid(Action<string, AOAdsInfo> revenuePaid)
    // {
    //     Agent().RewardedAdRevenuePaid(revenuePaid);
    // }

    public static float GetBannerHeight()
    {
        DebugLog("GetBannerHeight");
        return Agent().GetBannerHeight();
    }

    public static bool IsBannerShowing()
    {
        DebugLog("IsBannerShowing");
        return Agent().IsBannerShowing();
    }
    #endregion

    #region 广告播放控制器

    //显示插屏: 
    // OnCompleted(参数1:返回CD与广告是否准备好, 参数2:返回播放结果)
    // launchPlay 是否是发起游戏(在主页play不能计算游戏持续时间)
    /// <summary>
    /// 检查广告是否准备好，并播放广告
    /// </summary>
    /// <param name="adScene"></param>
    /// <param name="rewardVideoOnCompleted"> Action<bool IsReady, bool IsSuccess> </param>
    public static void CheckAndShowInterstitialAd(string adScene, Action<bool, bool> interstitialOnCompleted)
    {
        Instance.checkAndShowInterstitialAd(adScene, interstitialOnCompleted);
    }

    /// <summary>
    /// 检查广告是否准备好，并播放广告
    /// </summary>
    /// <param name="adScene"></param>
    /// <param name="rewardVideoOnCompleted"> Action<bool IsReady, bool IsSuccess> </param>
    public static void CheckAndShowRewardedVideo(string adScene, Action<bool, bool> rewardVideoOnCompleted)
    {
        Instance.checkAndShowRewardedVideo(adScene, rewardVideoOnCompleted);
    }

    public static void ShowAppOpenIfReady()
    {
        DebugLog("ShowAppOpenIfReady");
        bool ready = Agent().IsAppOpenReady();
        Instance.adsWillDisplay("wake_up", ready, AOAdsType.AppOpen);
        if (ready && Instance.BeginAdDisplaying("wake_up", null))
        {
            Agent().ShowAppOpen();
        }
        else
        {
            LoadAppOpen();
        }
    }
    //进入游戏
    public static void EnterGame()
    {
        Instance._adIntervalController.EnterGame();
    }

    //离开游戏
    public static void LeaveGame()
    {
        Instance._adIntervalController.LeaveGame();
    }

    public static void ResetCooldown()
    {
        Instance._adIntervalController.ResetCooldown();
    }

    #endregion

    #region 业务逻辑

    void Awake()
    {
        if (instance != null && instance != this)
        {
            LogError("Duplicate AOAds component ignored");
            Destroy(this);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // AOAdsUtils.SetAudioFocus();
#if ao_ads_max
        _agent = AOAdsMaxAgent.CreateInstance(_userId);
#elif ao_ads_is
        _agent = AOAdsIronSourceAgent.CreateInstance(_userId);
#else
        //没有使用第三方sdk的情况下,使用EditorAgent,所有回调均为true
        _agent = new AOAdsEditorAgent();

        //没有使用第三方sdk的情况下，直接完成初始化，方便调试
        InitComplete = true;
        _onInitialized?.Invoke();
#endif
        AOAdsEvent.Init();

        _agent.OnInitialized += onInitialized;
        // _agent.AdsDidRevenuePaid += adsDidRevenuePaid;
        _agent.AdsDidDisplay += adsDidDisplay;
        _agent.AdsDidDismissed += adsDidDismissed;
        _agent.AdsDidFailed += adsDidFailed;
    }



    private static AOAdsAgentInterface Agent()
    {
        if (AOAds.InitComplete)
        {
            return AOAds.Instance._agent;
        }
        else
        {
            return AOAds.Instance._defaultAgent;
        }
    }

    bool BeginAdDisplaying(string adScene, Action<bool> completion)
    {
        if (_adDisplaying)
        {
            LogError("Ad Repeat Show! Place:" + adScene);
            return false;
        }

        _adDisplaying = true;
        _adScene = adScene;
        _activeAdCompletion = completion;
        _adIntervalController.SetFullScreenAdDisplaying(true);
        if (_adDisplayingWatchdog != null) StopCoroutine(_adDisplayingWatchdog);
        _adDisplayingWatchdog = StartCoroutine(AdDisplayingWatchdog());
        return true;
    }

    void ResetAdDisplaying()
    {
        _adDisplaying = false;
        _adIntervalController.SetFullScreenAdDisplaying(false);
        if (_adDisplayingWatchdog != null)
        {
            StopCoroutine(_adDisplayingWatchdog);
            _adDisplayingWatchdog = null;
        }
    }

    void CompleteActiveAd(bool success)
    {
        Action<bool> completion = _activeAdCompletion;
        _activeAdCompletion = null;
        ResetAdDisplaying();
        try
        {
            completion?.Invoke(success);
        }
        catch (Exception ex)
        {
            LogError("Ad completion callback exception: " + ex);
        }
    }

    IEnumerator AdDisplayingWatchdog()
    {
        yield return new WaitForSecondsRealtime(AdDisplayingTimeout);
        LogError("Ad display callback timeout. Place:" + _adScene);
        _adDisplayingWatchdog = null;
        CompleteActiveAd(false);
    }

    IEnumerator delayCloseAdBreakView()
    {
        yield return new WaitForSeconds(0.5f);
        AOAdsBreakView.CloseView();
    }

    public void Init(string userId = null, Action onInitialized = null)
    {
        DebugLog("Init UserID:" + userId);
        _userId = userId;
        _onInitialized = onInitialized;

#if UNITY_ANDROID
        EnableAdBreak = true;
#endif

    }


    void Update()
    {
        _adIntervalController.Cooldown();
    }

    void OnApplicationPause(bool isPaused)
    {
        DebugLog($"OnApplicationPause:{isPaused}");
        if (isPaused)
        {
            _adIntervalController.AppEnterBackground();
        }
        else
        {
            _adIntervalController.AppEnterForeground();
        }
        // Agent().OnApplicationPause(isPaused);
    }



    private void checkAndShowInterstitialAd(string adScene, Action<bool, bool> interstitialOnCompleted)//Action<bool IsReady, bool IsSuccess>
    {
        DebugLog("CheckAndShowInterstitialAd <1> Place:" + adScene);

        if (!InitComplete)//checkAndShowInterstitialAd是复合接口,未初始化完成不做任何操作
        {
            interstitialOnCompleted?.Invoke(false, false);
            return;
        }

        bool ready = IsInterstitialReady();
        bool placeEnabled = _adSceneController.IsEnabled(adScene); //广告位是否启用
        bool intervalAllowed = _adIntervalController.CanAdPlay(adScene); //时间间隔是否达到

        bool canPlay = InitComplete && placeEnabled && intervalAllowed;

        DebugLog(string.Format("CheckAndShowInterstitialAd <2> Init:{0} Scene:{1} PlaceEnabled:{2} Interval:{3} Reday:{4}", InitComplete, adScene, placeEnabled, intervalAllowed, ready));

        if (canPlay)
        {
            adsWillDisplay(adScene, ready, AOAdsType.Interstitial);
        }

        if (canPlay && ready)
        {
            ShowInterstitial(adScene, (bool success) =>
            {
                if (interstitialOnCompleted != null) interstitialOnCompleted(true, success);
            });
        }
        else
        {
            if (interstitialOnCompleted != null) interstitialOnCompleted(false, false);
        }
    }

    private void checkAndShowRewardedVideo(string adScene, Action<bool, bool> rewardVideoOnCompleted)//Action<bool IsReady, bool IsSuccess>
    {
        DebugLog("CheckAndShowRewardedVideo <1> Place:" + adScene);

        if (!InitComplete)//checkAndShowRewardedVideo是复合接口,未初始化完成不做任何操作
        {
            rewardVideoOnCompleted?.Invoke(false, false);
            return;
        };


        bool ready = IsRewardedAdReady();
        DebugLog(string.Format("CheckAndShowRewardedVideo <2> Init:{0} Scene:{1} Reday:{2}", InitComplete, adScene, ready));

        adsWillDisplay(adScene, ready, AOAdsType.RewardedVideo);

        if (ready)
        {
            ShowRewardedVideo(adScene, (bool success) =>
            {
                rewardVideoOnCompleted?.Invoke(true, success);
            });
        }
        else
        {
            rewardVideoOnCompleted?.Invoke(false, false);
        }
    }

    private void _ShowInterstitial(string adScene, Action<bool> interstitialOnCompleted)
    {
        DebugLog("ShowInterstitial Place:" + adScene);
        if (!BeginAdDisplaying(adScene, interstitialOnCompleted))
        {
            interstitialOnCompleted?.Invoke(false);
            return;
        }

        if (EnableAdBreak)
        {
            StartCoroutine(AdBreakShowInterstitial());
        }
        else
        {
            Agent().ShowInterstitial(CompleteActiveAd, adScene);
        }
    }

    IEnumerator AdBreakShowInterstitial()
    {
        AOAdsBreakView.ShowView();
        yield return new WaitForSeconds(0.5f);
        AOAdsBreakView.CloseView();

        Agent().ShowInterstitial(CompleteActiveAd, _adScene);
    }

    private void _ShowRewardedVideo(string adScene, Action<bool> rewardVideoOnCompleted)
    {
        DebugLog("ShowRewardedVideo Place:" + adScene);
        if (!BeginAdDisplaying(adScene, rewardVideoOnCompleted))
        {
            rewardVideoOnCompleted?.Invoke(false);
            return;
        }
        Agent().ShowRewardedVideo(CompleteActiveAd, adScene);
    }
    #endregion

    #region  agent 回调

    void onInitialized()
    {
        InitComplete = true;
        if (_onInitialized != null)
        {
            _onInitialized();
        }
    }

    void adsWillDisplay(string adScene, bool ready, AOAdsType type)
    {
        DebugLog(string.Format("adsWillDisplay adScene:{0} ready:{1} adType:{2}", adScene, ready, type));



        AOAdsEvent.WillImpression(type, adScene, ready);

        try
        {
            AdsWillDisplay?.Invoke(adScene, ready, type);
        }
        catch (System.Exception) { }
    }

    // void adsdidRevenuePaid(AOAdsInfo info)
    // {
    //     AOAdsEvent.Impression(info, _adScene);
    // }

    void adsDidDisplay(AOAdsInfo info)
    {
        string scene = SceneFor(info);
        DebugLog(string.Format("adsOnDisplay Place:{0} Info:{1}", scene, info.ToString()));
        AOAdsEvent.Impression(info, scene);
        if (info.AdType == AOAdsType.Interstitial) _adIntervalController.ResetCooldown();
        try
        {
            AdsDidDisplay?.Invoke(scene, info);
        }
        catch (System.Exception)
        {
            LogError("AdsDidDisplay Invoke Exception");
        }
    }

    void adsDidDismissed(AOAdsInfo info, bool isReceivedReward)
    {
        string scene = SceneFor(info);
        DebugLog(string.Format("adsDidDismissed Place:{0} Info:{1}", scene, info.ToString()));
        if (info.AdType == AOAdsType.AppOpen) ResetAdDisplaying();


        AOAdsEvent.Close(info, scene, isReceivedReward);


        try
        {
            AdsDidDismissed?.Invoke(scene, info, isReceivedReward);
        }
        catch (System.Exception)
        {
            LogError("AdsDidDismissed Invoke Exception");
        }

#if ao_ads_fix_sound
        if (info.NetworkName == "AppLovin" || info.NetworkName == "Google AdMob")
        {
            StartCoroutine(DelayFixAudioBug());
        }
#endif
    }


    void adsDidFailed(AOAdsInfo info)
    {
        string scene = SceneFor(info);
        DebugLog(string.Format("adsDidFailed Place:{0} Info:{1}", scene, info.ToString()));
        if (info.AdType == AOAdsType.AppOpen) ResetAdDisplaying();

        AOAdsEvent.ImpressionFail(info, scene);

        try
        {
            AdsDidFailed?.Invoke(scene, info);
        }
        catch (System.Exception)
        {
            LogError("adsDidFailed Invoke Exception");
        }
    }

    string SceneFor(AOAdsInfo info)
    {
        return info != null && info.AdType == AOAdsType.AppOpen ? "wake_up" : _adScene;
    }

    #endregion

    #region log
    public static void DebugLog(object message)
    {
        Debug.Log("[AOAds] " + message);
    }

    public static void LogError(object message)
    {
        Debug.LogError("[AOAds] " + message);
    }
    #endregion



    public static AOAds Instance
    {
        get
        {
            //避免Editor模式，OnDestroy中使用Mono单例报错
#if UNITY_EDITOR
            if (_onApplicationQuit)
            {
                return new AOAds();
            }
#endif


            if (instance == null)
            {
                instance = FindObjectOfType<AOAds>();
                if (FindObjectsOfType<AOAds>().Length > 1)
                {
                    return instance;
                }

                if (instance == null)
                {
                    string instanceName = typeof(AOAds).Name;
                    GameObject instanceGO = GameObject.Find(instanceName);

                    if (instanceGO == null)
                    {
                        instanceGO = new GameObject(instanceName);
                    }

                    instance = instanceGO.AddComponent<AOAds>();
                    DontDestroyOnLoad(instanceGO); //保证实例不会被释放                 
                }
            }

            return instance;
        }
    }

    private void OnApplicationQuit()
    {
        _onApplicationQuit = true;
    }

    private void OnDestroy()
    {
        if (instance != this) return;

        if (_agent != null)
        {
            _agent.OnInitialized -= onInitialized;
            _agent.AdsDidDisplay -= adsDidDisplay;
            _agent.AdsDidDismissed -= adsDidDismissed;
            _agent.AdsDidFailed -= adsDidFailed;
        }
        InitComplete = false;
        instance = null;
    }

    void FixAudioBug()
    {

#if UNITY_IOS
        // Log($"Date:{DateTime.Now} 1");
        AOAdsUtils.ResumeAudio();
        // Log($"Date:{DateTime.Now} 2");
        AudioConfiguration config = AudioSettings.GetConfiguration();
        // Log($"Date:{DateTime.Now} 3");
        AudioSettings.Reset(config);
        // Log($"Date:{DateTime.Now} 4");
#endif
    }

    IEnumerator DelayFixAudioBug()
    {
        yield return new WaitForEndOfFrame();
        FixAudioBug();
    }
}

#if ao_ads_is
using System;
using UnityEngine;


public class AOAdsIronSourceAgent : MonoBehaviour, AOAdsAgent
{
    public event Action OnInitialized;
    public event Action<AOAdsInfo> AdsDidDisplay;//已展示
    public event Action<AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public event Action<AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)

    private Action<bool> _rewardVideoOnCompleted;
    private Action<bool> _interstitialOnCompleted;

    private string _appkey;
    private int _interRetryAttempt = 0;
    private int _interstitialLoadFailedCount = 0;
    private string _userId;
    private bool _isReceivedReward = false;
    public static AOAdsIronSourceAgent CreateInstance(string userId = null)
    {
        AOAds.DebugLog("Agent IronSource CreateInstance");

        AOAdsIronSourceAgent instance;
        string instanceName = typeof(AOAdsIronSourceAgent).Name;
        GameObject instanceGO = GameObject.Find(instanceName);
        if (instanceGO == null)
        {
            instanceGO = new GameObject(instanceName);
        }

        instance = instanceGO.AddComponent<AOAdsIronSourceAgent>();
        DontDestroyOnLoad(instanceGO); //保证实例不会被释放 

        instance._userId = userId;

        return instance;
    }

    void Start()
    {
        AOAds.DebugLog("Agent Init Begin");
        var developerSettings = Resources.Load<AOAdsIronSourceSettings>(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME);
        if (developerSettings != null)
        {
#if UNITY_ANDROID
            _appkey = developerSettings.AppKey;
#elif UNITY_IOS
            _appkey = developerSettings.AppKey;
#endif
            AOAds.DebugLog("Agent App Key:" + _appkey);

        }
        else
        {
            AOAds.LogError("Agent AOAdsIronSourceAgent 初始化失败");
        }

        //Add Init Event
        IronSourceEvents.onSdkInitializationCompletedEvent += () =>
        {
            AOAds.DebugLog("onSdkInitializationCompletedEvent");
            OnInitialized();
        };

        IronSource.Agent.setAdaptersDebug(Debug.isDebugBuild);
        IronSource.Agent.shouldTrackNetworkState(true);
        IronSource.Agent.validateIntegration();

        //Add AdInfo Interstitial Events
        IronSourceInterstitialEvents.onAdReadyEvent += InterstitialOnAdReadyEvent;
        IronSourceInterstitialEvents.onAdLoadFailedEvent += InterstitialOnAdLoadFailed;
        IronSourceInterstitialEvents.onAdOpenedEvent += InterstitialOnAdOpenedEvent;
        IronSourceInterstitialEvents.onAdClickedEvent += InterstitialOnAdClickedEvent;
        IronSourceInterstitialEvents.onAdShowSucceededEvent += InterstitialOnAdShowSucceededEvent;
        IronSourceInterstitialEvents.onAdShowFailedEvent += InterstitialOnAdShowFailedEvent;
        IronSourceInterstitialEvents.onAdClosedEvent += InterstitialOnAdClosedEvent;

        //Add AdInfo Rewarded Video Events
        IronSourceRewardedVideoEvents.onAdOpenedEvent += RewardedVideoOnAdOpenedEvent;
        IronSourceRewardedVideoEvents.onAdClosedEvent += RewardedVideoOnAdClosedEvent;
        IronSourceRewardedVideoEvents.onAdAvailableEvent += RewardedVideoOnAdAvailable;
        IronSourceRewardedVideoEvents.onAdUnavailableEvent += RewardedVideoOnAdUnavailable;
        IronSourceRewardedVideoEvents.onAdShowFailedEvent += RewardedVideoOnAdShowFailedEvent;
        IronSourceRewardedVideoEvents.onAdRewardedEvent += RewardedVideoOnAdRewardedEvent;
        IronSourceRewardedVideoEvents.onAdClickedEvent += RewardedVideoOnAdClickedEvent;
        if (Debug.isDebugBuild)
        {
            IronSource.Agent.setMetaData("is_test_suite", "enable");
        }
        IronSource.Agent.setUserId(_userId);
        IronSource.Agent.init(_appkey);
    }

    public bool IsInterstitialReady()
    {
        bool isInterstitialReady = IronSource.Agent.isInterstitialReady();

        if (!isInterstitialReady)
        {
            LoadInterstitial();
        }
        return isInterstitialReady;
    }

    //展示插屏
    public void ShowInterstitial(Action<bool> interstitialOnCompleted)
    {
        _interstitialOnCompleted = interstitialOnCompleted;
        IronSource.Agent.showInterstitial();
    }

    //视频准备完毕
    public bool IsRewardedAdReady()
    {
        bool isRewardedAdReady = IronSource.Agent.isRewardedVideoAvailable();

        return isRewardedAdReady;
    }

    //展示视频
    public void ShowRewardedVideo(Action<bool> rewardVideoOnCompleted)
    {
        _isReceivedReward = false;

        _rewardVideoOnCompleted = rewardVideoOnCompleted;
        IronSource.Agent.showRewardedVideo();
    }

    //装载视频
    public void LoadRewardedVideo()
    {

    }

    //装载插屏
    public void LoadInterstitial()
    {
        AOAds.DebugLog("Agent LoadInterstitial");
        // if (string.IsNullOrEmpty(_interstitialAdUnitId))
        // {
        //     AOAds.LogError("Agent LoadInterstitial AdUnitId Is Null");
        //     return;
        // }

        IronSource.Agent.loadInterstitial();
    }

    public void OnApplicationPause(bool isPaused)
    {
        IronSource.Agent.onApplicationPause(isPaused);
    }

    #region banner
    //装载banner
    public void LoadBanner()
    {

    }

    //展示banner
    public void ShowBanner(Action<float> bannerAdLoaded)
    {

    }
    //隐藏banner
    public void HideBanner()
    {

    }
    //获取banner高度
    public float GetBannerHeight()
    {
        return 0;
    }

    #endregion

    #region 业务代码
    public void adsDidDisplay(AOAdsInfo info)
    {
        AdsDidDisplay?.Invoke(info);
    }

    public void adsDidDismissed(AOAdsInfo info, bool isReceivedReward = false)
    {
        AdsDidDismissed?.Invoke(info, isReceivedReward);
    }

    void interstitialOnCompleted(bool success)
    {
        AOAds.DebugLog("Agent interstitialOnCompleted Result:" + success);

        try
        {
            _interstitialOnCompleted?.Invoke(success);
        }
        catch (System.Exception)
        {
            AOAds.LogError($"Agent interstitialOnCompleted Invoke Exception: {ex.Message}\n{ex.StackTrace}");
        }


        _interstitialOnCompleted = null;
    }

    void rewardVideoOnCompleted(bool success)
    {
        AOAds.DebugLog("Agent rewardVideoOnCompleted Result:" + success);

        try
        {
            _rewardVideoOnCompleted?.Invoke(success);
        }
        catch (System.Exception)
        {
            AOAds.LogError("Agent rewardVideoOnCompleted Invoke Exception");
        }
        _rewardVideoOnCompleted = null;

    }

    #endregion

    #region  插屏回调
    /************* Interstitial AdInfo Delegates *************/
    // Invoked when the interstitial ad was loaded succesfully.
    void InterstitialOnAdReadyEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdReadyEvent");
        // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

        // Reset retry attempt
        _interRetryAttempt = 0;
    }
    // Invoked when the initialization process has failed.
    void InterstitialOnAdLoadFailed(IronSourceError ironSourceError)
    {
        AOAds.DebugLog("Agent InterstitialOnAdLoadFailed: " + ironSourceError.ToString());
        // Interstitial ad failed to load 
        // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)

        _interRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, _interRetryAttempt));

        Invoke("LoadInterstitial", (float)_interRetryAttempt);
    }
    // Invoked when the Interstitial Ad Unit has opened. This is the impression indication. 
    void InterstitialOnAdOpenedEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdOpenedEvent");
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        adsDidDisplay(info); //因为sdk原因,这里可能会在adsOnDismissed调用才会出现
    }
    // Invoked when end user clicked on the interstitial ad
    void InterstitialOnAdClickedEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdClickedEvent");
    }
    // Invoked when the ad failed to show.
    void InterstitialOnAdShowFailedEvent(IronSourceError ironSourceError, IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdShowFailedEvent: " + ironSourceError.ToString());

        // adsDidDismissed(getAdsInfo(adInfo, AOAdsType.Interstitial));
        // interstitialOnCompleted(false);
        // LoadInterstitial();
    }
    // Invoked when the interstitial ad closed and the user went back to the application screen.
    void InterstitialOnAdClosedEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdClosedEvent");
        // Interstitial ad is hidden. Pre-load the next ad.
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        adsDidDismissed(getAdsInfo(adInfo, AOAdsType.Interstitial));
        interstitialOnCompleted(true);

        LoadInterstitial();
    }
    // Invoked before the interstitial ad was opened, and before the InterstitialOnAdOpenedEvent is reported.
    // This callback is not supported by all networks, and we recommend using it only if  
    // it's supported by all networks you included in your build. 
    void InterstitialOnAdShowSucceededEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent InterstitialOnAdShowSucceededEvent");
    }

    #endregion

    #region 激励视频回调

    /************* RewardedVideo AdInfo Delegates *************/
    // Indicates that there’s an available ad.
    // The adInfo object includes information about the ad that was loaded successfully
    // This replaces the RewardedVideoAvailabilityChangedEvent(true) event
    void RewardedVideoOnAdAvailable(IronSourceAdInfo adInfo)
    {
    }
    // Indicates that no ads are available to be displayed
    // This replaces the RewardedVideoAvailabilityChangedEvent(false) event
    void RewardedVideoOnAdUnavailable()
    {
    }
    // The Rewarded Video ad view has opened. Your activity will loose focus.
    void RewardedVideoOnAdOpenedEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent RewardedVideoOnAdOpenedEvent");
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        adsDidDisplay(info);//因为sdk原因,这里可能会在adsOnDismissed调用才会出现
    }
    // The Rewarded Video ad view is about to be closed. Your activity will regain its focus.
    void RewardedVideoOnAdClosedEvent(IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent RewardedVideoOnAdClosedEvent");

        adsDidDismissed(getAdsInfo(adInfo, AOAdsType.RewardedVideo), _isReceivedReward);
        rewardVideoOnCompleted(_isReceivedReward);

        _isReceivedReward = false;
    }
    // The user completed to watch the video, and should be rewarded.
    // The placement parameter will include the reward data.
    // When using server-to-server callbacks, you may ignore this event and wait for the ironSource server callback.
    void RewardedVideoOnAdRewardedEvent(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent RewardedVideoOnAdRewardedEvent");
        _isReceivedReward = true;
    }
    // The rewarded video ad was failed to show.
    void RewardedVideoOnAdShowFailedEvent(IronSourceError error, IronSourceAdInfo adInfo)
    {
        AOAds.DebugLog("Agent RewardedVideoOnAdShowFailedEvent:" + error.ToString());
        // adsDidDismissed(getAdsInfo(adInfo, AOAdsType.RewardedVideo));
        rewardVideoOnCompleted(false);
    }

    // Invoked when the video ad was clicked.
    // This callback is not supported by all networks, and we recommend using it only if
    // it’s supported by all networks you included in your build.
    void RewardedVideoOnAdClickedEvent(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {

    }

    #endregion
    private AOAdsInfo getAdsInfo(IronSourceAdInfo adinfo, AOAdsType type)
    {
        AOAdsInfo info = new AOAdsInfo();
        info.AdType = type;
        if (adinfo != null)
        {
            info.AdUnitIdentifier = adinfo.adUnit;
            info.NetworkName = adinfo.adNetwork;
            info.Revenue = adinfo.revenue ?? 0.0f;
        }
        else
        {
            info.AdUnitIdentifier = "ad_info_null";
            info.NetworkName = "ad_info_null";
            info.NetworkPlacement = "ad_info_null";
            info.Revenue = 0;
        }

        return info;
    }
}

#endif
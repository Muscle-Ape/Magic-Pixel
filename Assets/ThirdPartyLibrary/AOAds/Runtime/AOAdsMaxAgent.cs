#if ao_ads_max
using System;
using UnityEngine;

public class AOAdsMaxAgent : MonoBehaviour, AOAdsAgentInterface
{
    public event Action OnInitialized;
    // public event Action<AOAdsInfo> AdsDidRevenuePaid;//支付收入
    public event Action<AOAdsInfo> AdsDidDisplay;//已展示
    public event Action<AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public event Action<AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)
    private Action<bool> _rewardVideoOnCompleted;
    private Action<bool> _interstitialOnCompleted;
    private Action<float> _bannerAdLoaded;


    // private int _interstitialLoadFailedCount = 0;
    private string _interstitialAdUnitId = "";
    private string _rewardedAdUnitId = "";
    private string _bannerAdUnitId = "";
    private string _appOpenUnitId = "";
    private MaxSdkBase.BannerPosition _bannerPosition;
    private const string MaxSdkKey = "KdTKTtsnw99ZmhxRKqM_Emxo_iDeH1RfpLuwg8tPn2XqOVWhbJTIRtr7ARqszzxqLJQDGNX3S1_DrK7Mnt_HlF";
    private string _maxSdkKey = MaxSdkKey;
    private int _appOpenRetryAttempt = 0;
    private int _interRetryAttempt = 0;
    private int _rewardRetryAttempt = 0;

    private bool _isBannerShowing = false;
    private bool _isBannerAdLoaded = false;//banner加载完毕
    private bool _needShowBanner = false;//banner需要展示
    private bool _isReceivedReward = false;//获得激励视频奖励
    private string _userId;

    private float _interLoadedTime = 0;
    private float _rewardLoadedTime = 0;


    public static AOAdsMaxAgent CreateInstance(string userId = null)
    {
        AOAds.DebugLog("Agent Max CreateInstance");

        AOAdsMaxAgent instance;
        string instanceName = typeof(AOAdsMaxAgent).Name;
        GameObject instanceGO = GameObject.Find(instanceName);
        if (instanceGO == null)
        {
            instanceGO = new GameObject(instanceName);
        }

        instance = instanceGO.GetComponent<AOAdsMaxAgent>();
        if (instance == null)
        {
            instance = instanceGO.AddComponent<AOAdsMaxAgent>();
        }
        DontDestroyOnLoad(instanceGO); //保证实例不会被释放 

        instance._userId = userId;

        return instance;
    }



    void Start()
    {
        var developerSettings = AOMaxAdsSettings.Load();
        if (developerSettings != null)
        {

            _interstitialAdUnitId = developerSettings.CurrentInterstitialAdUnitId;
            _rewardedAdUnitId = developerSettings.CurrentRewardedAdUnitId;
            _bannerAdUnitId = developerSettings.CurrentBannerAdUnitId;
            _appOpenUnitId = developerSettings.CurrentAppOpenAdUnitId;
            _bannerPosition = developerSettings.MaxBannerPosition;
            if (!developerSettings.TryValidateCurrentPlatform(out string validationError))
            {
                AOAds.LogError("Agent MAX configuration invalid: " + validationError);
            }
            if (!string.IsNullOrEmpty(developerSettings.MaxSdkKey))
            {
                _maxSdkKey = developerSettings.MaxSdkKey;
            }

            AOAds.DebugLog("Agent InterstitialAdUnitId:" + _interstitialAdUnitId);
            AOAds.DebugLog("Agent RewardedAdUnitId:" + _rewardedAdUnitId);
            AOAds.DebugLog("Agent BannerAdUnitId:" + _bannerAdUnitId);
            AOAds.DebugLog("Agent AppOpenUnitId:" + _appOpenUnitId);
        }
        else
        {
            AOAds.LogError("Agent [AOAds] Max AOAdsMaxAgent 初始化失败,检查广告配置");
        }
        MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;

        MaxSdk.SetSdkKey(_maxSdkKey);
        MaxSdk.SetVerboseLogging(Debug.isDebugBuild);


        if (!string.IsNullOrEmpty(_userId))
        {
            MaxSdk.SetUserId(_userId);
        }
        MaxSdk.InitializeSdk();


        // Attach callback
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaidEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;


        // Attach callback
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;

        // Attach callback
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAppOpenLoadedEvent;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAppOpenLoadFailedEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAppOpenDisplayedEvent;
        MaxSdkCallbacks.AppOpen.OnAdClickedEvent += OnAppOpenClickedEvent;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAppOpenAdRevenuePaidEvent;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAppOpenHiddenEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAppOpenAdFailedToDisplayEvent;

        // banner
        MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdFailedEvent;
    }

    private void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
    {
        OnInitialized?.Invoke();
#if UNITY_IOS && ao_fb_ads
        if (MaxSdkUtils.CompareVersions(UnityEngine.iOS.Device.systemVersion, "14.5") != MaxSdkUtils.VersionComparisonResult.Lesser)
        {
            AudienceNetwork.AdSettings.SetAdvertiserTrackingEnabled(sdkConfiguration.AppTrackingStatus == MaxSdkBase.AppTrackingStatus.Authorized);
        }
#endif
    }

    private void OnDestroy()
    {
        MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
        MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoadedEvent;
        MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailedEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnInterstitialDisplayedEvent;
        MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnInterstitialClickedEvent;
        MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnInterstitialAdRevenuePaidEvent;
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHiddenEvent;
        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialAdFailedToDisplayEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedAdLoadedEvent;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedAdLoadFailedEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRewardedAdDisplayedEvent;
        MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnRewardedAdClickedEvent;
        MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRewardedAdRevenuePaidEvent;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedAdHiddenEvent;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedAdFailedToDisplayEvent;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedRewardEvent;
        MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= OnAppOpenLoadedEvent;
        MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= OnAppOpenLoadFailedEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent -= OnAppOpenDisplayedEvent;
        MaxSdkCallbacks.AppOpen.OnAdClickedEvent -= OnAppOpenClickedEvent;
        MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent -= OnAppOpenAdRevenuePaidEvent;
        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= OnAppOpenHiddenEvent;
        MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= OnAppOpenAdFailedToDisplayEvent;
        MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerAdLoadedEvent;
        MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerAdFailedEvent;
    }

    void Update()
    {
        _interLoadedTime += Time.deltaTime;
        _rewardLoadedTime += Time.deltaTime;
        // if (_isInvokeAdsOnDisplay)
        // {
        //     adsOnDisplay(_adsOnDisplayAdInfo);
        //     _isInvokeAdsOnDisplay = false;
        //     _adsOnDisplayAdInfo = null;
        // }
    }

    #region AOAdsAgent
    public void LoadBanner()
    {
        if (string.IsNullOrEmpty(_bannerAdUnitId))
        {
            //AOAds.LogError("Agent LoadBanner AdUnitId Is Null");
            return;
        }

        MaxSdk.SetBannerExtraParameter(_bannerAdUnitId, "force_banner", "true");
        MaxSdk.SetBannerExtraParameter(_bannerAdUnitId, "adaptive_banner", "true");
        MaxSdk.SetBannerBackgroundColor(_bannerAdUnitId, Color.clear);
        MaxSdk.SetBannerWidth(_bannerAdUnitId, 320);

        MaxSdk.CreateBanner(_bannerAdUnitId, _bannerPosition);


        MaxSdk.HideBanner(_bannerAdUnitId);
        // MaxSdk.LoadBanner(_bannerAdUnitId);


    }

    //装载插屏
    public void LoadInterstitial()
    {
        AOAds.DebugLog("Agent LoadInterstitial");
        if (string.IsNullOrEmpty(_interstitialAdUnitId))
        {
            AOAds.LogError("Agent LoadInterstitial AdUnitId Is Null");
            return;
        }

        MaxSdk.LoadInterstitial(_interstitialAdUnitId);
    }

    public bool IsInterstitialReady(bool withLoad = true)
    {
        bool isInterstitialReady = MaxSdk.IsInterstitialReady(_interstitialAdUnitId);

        if (!isInterstitialReady && withLoad)
        {
            LoadInterstitial();
        }
        return isInterstitialReady;
    }

    //展示插屏
    public void ShowInterstitial(Action<bool> interstitialOnCompleted, string scene = "")
    {
        _interstitialOnCompleted = interstitialOnCompleted;
        MaxSdk.ShowInterstitial(_interstitialAdUnitId, placement: scene);
    }



    //装载视频
    public void LoadRewardedVideo()
    {
        AOAds.DebugLog("Agent LoadRewardedVideo");
        if (string.IsNullOrEmpty(_rewardedAdUnitId))
        {
            AOAds.LogError("Agent LoadRewardedVideo AdUnitId Is Null");
            return;
        }


        MaxSdk.LoadRewardedAd(_rewardedAdUnitId);
    }

    //视频准备完毕
    public bool IsRewardedAdReady(bool withLoad = true)
    {
        bool isRewardedAdReady = MaxSdk.IsRewardedAdReady(_rewardedAdUnitId);
        if (!isRewardedAdReady && withLoad)
        {
            LoadRewardedVideo();
        }
        return isRewardedAdReady;
    }

    //展示视频
    public void ShowRewardedVideo(Action<bool> rewardVideoOnCompleted, string scene = "")
    {
        _isReceivedReward = false;

        _rewardVideoOnCompleted = rewardVideoOnCompleted;
        MaxSdk.ShowRewardedAd(_rewardedAdUnitId, placement: scene);
    }

    //展示Banner
    public void ShowBanner(Action<float> bannerAdLoaded)
    {
        _bannerAdLoaded = bannerAdLoaded;

        if (_isBannerAdLoaded)
        {
            BannerDidShow();
        }
        else
        {
            _needShowBanner = true;
        }
    }


    //隐藏Banner
    public void HideBanner()
    {
        _bannerAdLoaded = null;//移除回调管理
        _isBannerShowing = false;//不在展示中
        _needShowBanner = false;//不需要展示
        MaxSdk.HideBanner(_bannerAdUnitId);
    }

    //装载视频
    public void LoadAppOpen()
    {
        AOAds.DebugLog("Agent LoadAppOpen");
        if (string.IsNullOrEmpty(_appOpenUnitId))
        {
            // AOAds.LogError("Agent LoadAppOpen AdUnitId Is Null");
            return;
        }
        MaxSdk.LoadAppOpenAd(_appOpenUnitId);
    }

    //视频准备完毕
    public bool IsAppOpenReady()
    {
        if (string.IsNullOrEmpty(_appOpenUnitId))
        {
            return false;
        }
        bool isAppOpenReady = MaxSdk.IsAppOpenAdReady(_appOpenUnitId);
        // AOAds.DebugLog("IsAppOpenReady:" + isAppOpenReady);
        return isAppOpenReady;
    }

    //展示视频
    public void ShowAppOpen()
    {
        AOAds.DebugLog("ShowAppOpen");
        if (string.IsNullOrEmpty(_appOpenUnitId))
        {
            return;
        }
        MaxSdk.ShowAppOpenAd(_appOpenUnitId, placement: "wake_up");
    }

    #endregion


    public void OnApplicationPause(bool isPaused)
    {

    }

    public void adsDidDisplay(AOAdsInfo info)
    {
        AdsDidDisplay?.Invoke(info);
    }

    public void adsDidDismissed(AOAdsInfo info, bool isReceivedReward = false)
    {
        AdsDidDismissed?.Invoke(info, isReceivedReward);
    }


    public void adsDidFailed(AOAdsInfo info, bool isReceivedReward = false)
    {
        AdsDidFailed?.Invoke(info);
    }

    #region AppOpen
    private void OnAppOpenLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenLoadedEvent");
        _appOpenRetryAttempt = 0;
    }
    private void OnAppOpenLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenLoadFailedEvent");
        _appOpenRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, _appOpenRetryAttempt));

        Invoke("LoadAppOpen", (float)retryDelay);
    }
    private void OnAppOpenDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenDisplayedEvent");

        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.AppOpen);
        adsDidDisplay(info);

    }

    private void OnAppOpenClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenClickedEvent");
    }
    private void OnAppOpenAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenAdRevenuePaidEvent");
        //!!! 将display移动到此,为了在unity进入后台前执行display - 暂时不考虑非主线程调用问题
        // AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.AppOpen);
        // adsDidDisplay(info);
        // adsDidRevenuePaid(info);
    }
    private void OnAppOpenHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenHiddenEvent");
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.AppOpen);
        adsDidDismissed(info);
        Invoke("LoadAppOpen", 1);
    }
    private void OnAppOpenAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnAppOpenAdFailedToDisplayEvent");
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.AppOpen);
        adsDidFailed(info);
        adsDidDismissed(info);
        Invoke("LoadAppOpen", 1);
    }

    #endregion

    #region 插屏
    void interstitialOnCompleted(bool success)
    {
        AOAds.DebugLog("Agent interstitialOnCompleted Result:" + success);

        try
        {
            _interstitialOnCompleted?.Invoke(success);
        }
        catch (System.Exception ex)
        {
            AOAds.LogError($"Agent interstitialOnCompleted Invoke Exception: {ex.Message}\n{ex.StackTrace}");
        }


        _interstitialOnCompleted = null;
    }

    private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialLoadedEvent");
        // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

        // Reset retry attempt
        _interRetryAttempt = 0;

        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        AOAdsEvent.Loaded(info, _interLoadedTime);
    }

    private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialLoadFailedEvent");
        // Interstitial ad failed to load 
        // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)

        _interRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, _interRetryAttempt));

        Invoke("LoadInterstitial", (float)retryDelay);
    }

    private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialDisplayedEvent");

        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        adsDidDisplay(info); //因为sdk原因,这里可能会在adsOnDismissed调用才会出现
    }

    private void OnInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialAdFailedToDisplayEvent");
        // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);

        adsDidFailed(info);
        adsDidDismissed(info);

        interstitialOnCompleted(false);
        LoadInterstitial();
    }

    private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialClickedEvent");
    }

    private void OnInterstitialAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        if (adInfo != null)
        {
            AOAds.DebugLog("Agent OnInterstitialAdRevenuePaidEvent:" + adInfo.ToString());
        }
        else
        {
            AOAds.DebugLog("Agent OnInterstitialAdRevenuePaidEvent: info is null");
        }

        // AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        // if (_interstitialRevenuePaid != null)
        // {
        //     _interstitialRevenuePaid(adUnitId, info);
        // }
        //1. OnInterstitialAdRevenuePaidEvent,在unity阻塞之前
        //2. 此处不是主线程调用 不能直接调用 adsOnDisplay(getAdsInfo(adInfo));

        //!!! 将display移动到此,为了在unity进入后台前执行display - 暂时不考虑非主线程调用问题
        // AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        // adsDidDisplay(info);
        // adsDidRevenuePaid(info);

    }

    private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnInterstitialHiddenEvent");
        // Interstitial ad is hidden. Pre-load the next ad.
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.Interstitial);
        adsDidDismissed(info);
        interstitialOnCompleted(true);

        LoadInterstitial();
    }
    #endregion

    #region  视频
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

    private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdLoadedEvent");
        // Rewarded ad is ready for you to show. MaxSdk.IsRewardedAdReady(adUnitId) now returns 'true'.

        // Reset retry attempt
        _rewardRetryAttempt = 0;

        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        AOAdsEvent.Loaded(info, _rewardLoadedTime);
    }

    private void OnRewardedAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdLoadFailedEvent");
        // Rewarded ad failed to load 
        // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds).

        _rewardRetryAttempt++;
        double retryDelay = Math.Pow(2, Math.Min(6, _rewardRetryAttempt));

        Invoke("LoadRewardedVideo", (float)retryDelay);
    }

    private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdDisplayedEvent");

        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        adsDidDisplay(info);//因为sdk原因,这里可能会在adsOnDismissed调用才会出现
    }

    private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdFailedToDisplayEvent");
        // Rewarded ad failed to display. AppLovin recommends that you load the next ad.
        AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        adsDidFailed(info);
        adsDidDismissed(info);
        rewardVideoOnCompleted(false);
        LoadRewardedVideo();
    }

    private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdClickedEvent");

        // adsOnDismissed(getAdsInfo(adInfo, AOAdsType.RewardedVideo));
        // rewardVideoOnCompleted(_isReceivedReward);
        // _isReceivedReward = false;
    }

    private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdHiddenEvent");
        // Rewarded ad is hidden. Pre-load the next ad
        adsDidDismissed(getAdsInfo(adInfo, AOAdsType.RewardedVideo), _isReceivedReward);
        rewardVideoOnCompleted(_isReceivedReward);

        LoadRewardedVideo();

        _isReceivedReward = false;
    }

    private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        AOAds.DebugLog("Agent OnRewardedAdReceivedRewardEvent");
        _isReceivedReward = true;
        // adsOnDismissed(getAdsInfo(adInfo, AOAdsType.RewardedVideo));
        // rewardVideoOnCompleted(true);
    }

    private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        if (adInfo != null)
        {
            AOAds.DebugLog("Agent OnRewardedAdRevenuePaidEvent:" + adInfo.ToString());
        }
        else
        {
            AOAds.DebugLog("Agent OnRewardedAdRevenuePaidEvent: info is null");
        }

        // AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        // if (_rewardVideoRevenuePaid != null)
        // {
        //     _rewardVideoRevenuePaid(adUnitId, info);
        // }
        //OnRewardedAdRevenuePaidEvent的回调可以在展示后立即回调,在unity阻塞之前
        // adsOnDisplay(info);
        // _isInvokeAdsOnDisplay = true;
        // _adsOnDisplayAdInfo = info;


        //!!! 将display移动到此,为了在unity进入后台前执行display - 暂时不考虑非主线程调用问题
        // AOAdsInfo info = getAdsInfo(adInfo, AOAdsType.RewardedVideo);
        // adsDidDisplay(info);
        // adsDidRevenuePaid(info);
    }

    #endregion

    #region Banner
    void BannerDidShow()
    {
        MaxSdk.ShowBanner(_bannerAdUnitId);

        if (!_isBannerShowing)
        {
            _isBannerShowing = true;
            try
            {
                _bannerAdLoaded?.Invoke(GetBannerHeight());
            }
            catch (System.Exception ex)
            {
                AOAds.LogError($"Agent BannerDidShow Invoke Exception: {ex.ToString()}");
            }
        }
    }

    public bool IsBannerShowing()
    {
        return _isBannerShowing;
    }

    public float GetBannerHeight()
    {
#if UNITY_EDITOR
        return 130;
#else


        float bannerHeight = MaxSdkUtils.GetAdaptiveBannerHeight(320);
        float adjustedHeight = 0;
        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.Android)
        {
            adjustedHeight = bannerHeight * MaxSdkUtils.GetScreenDensity();
        }
        else
        {
            adjustedHeight = bannerHeight;
        }

        AOAds.DebugLog($"Agent Banner Height:{bannerHeight} AdjustedHeight:{adjustedHeight}");
        AOAds.DebugLog($"Screen.currentResolution:({Screen.currentResolution.width},{Screen.currentResolution.height}), Screen:({Screen.width},{Screen.height})");
        AOAds.DebugLog($"MaxSdkUtils.GetScreenDensity():{MaxSdkUtils.GetScreenDensity()}");

        return adjustedHeight;
#endif
    }

    private void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        // Banner ad is ready to be shown.
        // If you have already called MaxSdk.ShowBanner(BannerAdUnitId) it will automatically be shown on the next ad refresh.
        // AOAds.DebugLog($"Agent Banner ad loaded needShow:{_needShowBanner}");
        _isBannerAdLoaded = true;

        if (_needShowBanner)
        {
            BannerDidShow();
        }
    }

    private void OnBannerAdFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        AOAds.DebugLog("Agent Banner ad load fail");
    }

    #endregion


    // public string AdUnitIdentifier;
    // public string AdFormat;
    // public string NetworkName;
    // public string NetworkPlacement;
    // public string Placement;
    // public string CreativeIdentifier;
    // public double Revenue;
    // public string RevenuePrecision;
    private AOAdsInfo getAdsInfo(MaxSdk.AdInfo maxAdinfo, AOAdsType type)
    {
        AOAdsInfo info = new AOAdsInfo();
        info.AdType = type;
        if (maxAdinfo != null)
        {
            info.AdUnitIdentifier = maxAdinfo.AdUnitIdentifier;
            info.NetworkName = maxAdinfo.NetworkName;
            info.NetworkPlacement = maxAdinfo.NetworkPlacement;
            info.CreativeIdentifier = maxAdinfo.CreativeIdentifier;
            info.Revenue = maxAdinfo.Revenue;
            // info.RevenuePrecision = maxAdinfo.RevenuePrecision;
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

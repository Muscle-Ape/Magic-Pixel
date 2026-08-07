using System.Collections.Generic;
#if ao_ads_ta_event || ao_ta_event
using ThinkingAnalytics;
#endif

#if ao_af_event
using AppsFlyerSDK;
#endif

public class AOAdsEvent
{
    /// <summary>
    /// 插屏次数
    /// </summary>
    public static int InterNum
    {
        get
        {
#if UNITY_IOS
            return AOAdsUtils.GetInterNum();
#else
            return _interNum;
#endif
        }
    }

    /// <summary>
    /// 激励次数
    /// </summary>
    public static int RewardNum
    {
        get
        {
#if UNITY_IOS  
            return AOAdsUtils.GetRewardNum();
#else
            return _rewardNum;
#endif
        }
    }
    //ta 事件
    private const string kAdLoaded = "ad_loaded";         //广告将要展示
    private const string kAdWillImpression = "ad_will_impression";         //广告将要展示
    private const string kAdImpression = "ad_impression";                  //广告展示
    private const string kAdClose = "ad_close";                            //广告关闭
    private const string kAdImpressionFail = "ad_impression_fail";         //广告失败
    private const string kAppOpenScene = "wake_up";

    //af 事件
    private const string kAFAdInterStart = "ad_inter_start";
    private const string kAFAdInterEnd = "ad_inter_end";
    private const string kAFAdRewardStart = "ad_reward_start";
    private const string kAFAdRewardEnd = "ad_reward_end";

    //广告次数 key
    private const string kAdInsertNumPrefsKey = "__ad_insert_num__";
    private const string kAdRewardNumPrefsKey = "__ad_reward_num__";

    private static bool _interUploaded = false;
    private static bool _rewardedUploaded = false;

    private static int _interNum = 0;
    private static int _rewardNum = 0;


    public static void Init()
    {
#if UNITY_IOS

        AOAdsUtils.Init();

        //同步广告次数, 同步后删除Unity本地数据,后续交给IOS存储
        if (UnityEngine.PlayerPrefs.HasKey(kAdInsertNumPrefsKey) || UnityEngine.PlayerPrefs.HasKey(kAdRewardNumPrefsKey))
        {
            int interNum = UnityEngine.PlayerPrefs.GetInt(kAdInsertNumPrefsKey, 0);
            int rewardNum = UnityEngine.PlayerPrefs.GetInt(kAdRewardNumPrefsKey, 0);

            AOAdsUtils.SyncAdsCount(interNum, rewardNum);

            UnityEngine.PlayerPrefs.DeleteKey(kAdInsertNumPrefsKey);
            UnityEngine.PlayerPrefs.DeleteKey(kAdRewardNumPrefsKey);
        }
#else
        _interNum = UnityEngine.PlayerPrefs.GetInt(kAdInsertNumPrefsKey, 0);
        _rewardNum = UnityEngine.PlayerPrefs.GetInt(kAdRewardNumPrefsKey, 0);
#endif



    }
    #region TA
    public static void Loaded(AOAdsInfo info, float loaded_time)
    {
        if (info.AdType == AOAdsType.Interstitial && _interUploaded)
        {
            return;
        }
        if (info.AdType == AOAdsType.RewardedVideo && _rewardedUploaded)
        {
            return;
        }

        Dictionary<string, object> dict = info.ToEvent();
        dict.Add("ad_loaded_time", loaded_time);
        Track(kAdLoaded, dict);

        if (info.AdType == AOAdsType.Interstitial)
        {
            _interUploaded = true;
        }

        if (info.AdType == AOAdsType.RewardedVideo)
        {
            _rewardedUploaded = true;
        }
    }

    //广告展示
    public static void Impression(AOAdsInfo info, string ad_scene)
    {
#if UNITY_ANDROID
        UpdateAdCount(info);

        Dictionary<string, object> dict = info.ToEvent();
        if (info.AdType == AOAdsType.AppOpen)
        {
            ad_scene = kAppOpenScene;
        }
        dict.Add("ad_scene", ad_scene);
        dict.Add("ad_inter_num", _interNum);
        dict.Add("ad_reward_num", _rewardNum);

        Track(kAdImpression, dict);

#if ao_ads_ta_event || ao_ta_event
        ThinkingAnalytics.ThinkingAnalyticsAPI.TimeEvent(kAdClose);
#endif

#if ao_af_event
        Dictionary<string, string> eventValues = new Dictionary<string, string>();
        eventValues.Add(AFInAppEvents.CURRENCY, "USD");
        eventValues.Add(AFInAppEvents.REVENUE, info.Revenue.ToString("f5"));
        if (info.AdType == AOAdsType.Interstitial)
        {
            AppsFlyer.sendEvent(kAFAdInterStart, eventValues);
        }
        else if (info.AdType == AOAdsType.RewardedVideo)
        {
            AppsFlyer.sendEvent(kAFAdRewardStart, eventValues);
        }
#endif

        
#endif
    }

    public static void UpdateAdCount(AOAdsInfo info)
    {
        if (info.AdType == AOAdsType.Interstitial)
        {
            _interNum++;
            UnityEngine.PlayerPrefs.SetInt(kAdInsertNumPrefsKey, _interNum);
        }
        else if (info.AdType == AOAdsType.RewardedVideo)
        {
            _rewardNum++;
            UnityEngine.PlayerPrefs.SetInt(kAdRewardNumPrefsKey, _rewardNum);
        }
    }

    //广告关闭
    public static void Close(AOAdsInfo info, string ad_scene, bool isReceivedReward)
    {
#if UNITY_ANDROID
        Dictionary<string, object> dict = info.ToEvent();

        if (info.AdType == AOAdsType.AppOpen)
        {
            ad_scene = kAppOpenScene;
        }

        dict.Add("ad_scene", ad_scene);
        dict.Add("is_complete", isReceivedReward);

        Track(kAdClose, dict);

#if ao_af_event
        Dictionary<string, string> eventValues = new Dictionary<string, string>();
        eventValues.Add(AFInAppEvents.CURRENCY, "USD");
        eventValues.Add(AFInAppEvents.REVENUE, info.Revenue.ToString("f5"));
        if (info.AdType == AOAdsType.Interstitial)
        {
            AppsFlyer.sendEvent(kAFAdInterEnd, eventValues);
        }
        else if (info.AdType == AOAdsType.RewardedVideo)
        {
            AppsFlyer.sendEvent(kAFAdRewardEnd, eventValues);
        }
#endif

#endif
    }

    //激励广告失败
    public static void ImpressionFail(AOAdsInfo info, string ad_scene)
    {
        Dictionary<string, object> dict = info.ToEvent();
        dict.Add("ad_scene", ad_scene);
        Track(kAdImpressionFail, dict);
    }

    //广告将要展示
    public static void WillImpression(AOAdsType adType, string ad_scene, bool is_ready)
    {
        // if(UnityEngine.Debug.isDebugBuild)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>()
            {
                {"is_ready", is_ready},
                {"ad_type",AOAdsInfo.AdTypeToString(adType)},
                {"ad_scene",ad_scene},
            };
            Track(kAdWillImpression, dic);
        }

    }

    private static void Track(string eventName, Dictionary<string, object> dic)
    {
        EventLog(eventName, dic);

#if ao_ads_ta_event || ao_ta_event
#if !UNITY_EDITOR
        ThinkingAnalyticsAPI.Track(eventName, dic);
#endif
#endif
    }

    private static void EventLog(string eventName, Dictionary<string, object> dic)
    {
        AOAds.DebugLog("Event: " + eventName);
    }
    #endregion    
}
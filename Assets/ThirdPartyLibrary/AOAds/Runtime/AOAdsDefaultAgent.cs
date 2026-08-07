using System;
using UnityEngine;
public class AOAdsDefaultAgent : AOAdsAgentInterface
{
    public event Action OnInitialized;
    public event Action<AOAdsInfo> AdsDidDisplay;//已展示
    public event Action<AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public event Action<AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)
    //插屏准备完毕
    public bool IsInterstitialReady(bool withLoad = true)
    {
        LogError("IsInterstitialReady");
        return false;
    }


    //展示插屏
    public void ShowInterstitial(Action<bool> interstitialOnCompleted, string scene = "")
    {

#if UNITY_EDITOR
        AOAdsInfo info = new AOAdsInfo();
        info.AdType = AOAdsType.Interstitial;
        info.AdUnitIdentifier = "ad_test";
        info.NetworkName = "ad_test";
        info.NetworkPlacement = "ad_test";
        info.Revenue = 0;

        AdsDidDisplay?.Invoke(info);
        AdsDidDismissed?.Invoke(info, true);
        interstitialOnCompleted?.Invoke(true);
#else
        LogError("ShowInterstitial");
#endif
    }

    //视频准备完毕
    public bool IsRewardedAdReady(bool withLoad = true)
    {
        LogError("IsRewardedAdReady");
        return false;
    }

    //展示视频
    public void ShowRewardedVideo(Action<bool> rewardVideoOnCompleted, string scene = "")
    {
#if UNITY_EDITOR
        AOAdsInfo info = new AOAdsInfo();
        info.AdType = AOAdsType.RewardedVideo;
        info.AdUnitIdentifier = "ad_test";
        info.NetworkName = "ad_test";
        info.NetworkPlacement = "ad_test";
        info.Revenue = 0;

        AdsDidDisplay?.Invoke(info);
        AdsDidDismissed?.Invoke(info, true);
        rewardVideoOnCompleted?.Invoke(true);
#else
        LogError("ShowRewardedVideo");
#endif

    }

    //装载视频
    public void LoadRewardedVideo()
    {
        LogError("LoadRewardedVideo");
    }

    //装载插屏
    public void LoadInterstitial()
    {
        LogError("LoadInterstitial");
    }

    //装载banner
    public void LoadBanner()
    {
        LogError("LoadBanner");
    }
    //展示banner
    public void ShowBanner(Action<float> bannerAdLoaded)
    {
        LogError("ShowBanner");
    }

    //隐藏banner
    public void HideBanner()
    {
        LogError("HideBanner");
    }


    public float GetBannerHeight()
    {
        // LogError("GetBannerHeight");
        return 0;
    }

    public bool IsBannerShowing()
    {
        return false;
    }
    public void OnApplicationPause(bool isPaused)
    {

    }

    //装载视频
    public void LoadAppOpen()
    {
        LogError("LoadAppOpen");
    }

    //视频准备完毕
    public bool IsAppOpenReady()
    {
        LogError("LoadAppOpen");
        return false;
    }

    //展示视频
    public void ShowAppOpen()
    {
        LogError("ShowAppOpen");
    }

    void LogError(object message)
    {
        Debug.LogError("[AOAds] Default " + message);
    }
}

using System;
using UnityEngine;
public class AOAdsEditorAgent : AOAdsAgentInterface
{
    public event Action OnInitialized;
    public event Action<AOAdsInfo> AdsDidDisplay;//已展示
    public event Action<AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public event Action<AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)

    //插屏准备完毕
    public bool IsInterstitialReady(bool withLoad = true)
    {
        AOAds.DebugLog("IsInterstitialReady");
        return true;
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
        AOAds.DebugLog("ShowInterstitial");
#endif
    }

    //视频准备完毕
    public bool IsRewardedAdReady(bool withLoad = true)
    {
        AOAds.DebugLog("IsRewardedAdReady");
        return true;
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
        AOAds.DebugLog("ShowRewardedVideo");
#endif

    }

    //装载视频
    public void LoadRewardedVideo()
    {
        AOAds.DebugLog("LoadRewardedVideo");
    }

    //装载插屏
    public void LoadInterstitial()
    {
        AOAds.DebugLog("LoadInterstitial");
    }

    //装载banner
    public void LoadBanner()
    {
        AOAds.DebugLog("LoadBanner");
    }


    public float GetBannerHeight()
    {
        AOAds.DebugLog("GetBannerHeight");
        return 50;
    }

    public bool IsBannerShowing()
    {
        return true;
    }

    //展示banner
    public void ShowBanner(Action<float> bannerAdLoaded)
    {
        AOAds.DebugLog("ShowBanner");
    }

    //隐藏banner
    public void HideBanner()
    {
        AOAds.DebugLog("HideBanner");
    }

    public void OnApplicationPause(bool isPaused)
    {

    }

    //装载视频
    public void LoadAppOpen()
    {
        AOAds.DebugLog("LoadAppOpen");
    }

    //视频准备完毕
    public bool IsAppOpenReady()
    {
        AOAds.DebugLog("LoadAppOpen");
        return true;
    }

    //展示视频
    public void ShowAppOpen()
    {
        AOAds.DebugLog("ShowAppOpen");
    }

}

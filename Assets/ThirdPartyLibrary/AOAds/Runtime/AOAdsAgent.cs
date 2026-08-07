using System;
public interface AOAdsAgentInterface
{
    public event Action OnInitialized;
    // public event Action<AOAdsInfo> AdsDidRevenuePaid;//支付收入
    public event Action<AOAdsInfo> AdsDidDisplay;//已展示
    public event Action<AOAdsInfo, bool> AdsDidDismissed;//已关闭 (失败是也会调用)
    public event Action<AOAdsInfo> AdsDidFailed;//已失败 (失败是会与AdsDidDismissed同时调用)

    #region 插屏

    //装载插屏
    public void LoadInterstitial();

    //插屏准备完毕
    public bool IsInterstitialReady(bool withLoad = true);

    //展示插屏
    public void ShowInterstitial(Action<bool> interstitialOnCompleted, string scene = "");

    #endregion

    #region  激励视频

    //装载视频
    public void LoadRewardedVideo();

    //视频准备完毕
    public bool IsRewardedAdReady(bool withLoad = true);

    //展示视频
    public void ShowRewardedVideo(Action<bool> rewardVideoOnCompleted, string scene = "");

    #endregion


    #region  开屏广告

    //装载视频
    public void LoadAppOpen();

    //视频准备完毕
    public bool IsAppOpenReady();

    //展示视频
    public void ShowAppOpen();

    #endregion

    #region banner
    //装载banner
    public void LoadBanner();
    //展示banner
    public void ShowBanner(Action<float> bannerAdLoaded);
    //隐藏banner
    public void HideBanner();
    //获取banner高度
    public float GetBannerHeight();

    public bool IsBannerShowing();
    #endregion




    // //插屏收入
    // public void InterstitialAdRevenuePaid(Action<string, AOAdsInfo> evenuePaid);

    // //视频收入
    // public void RewardedAdRevenuePaid(Action<string, AOAdsInfo> evenuePaid);



    public void OnApplicationPause(bool isPaused);

}
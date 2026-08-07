using UnityEngine;

/// <summary>
/// MagicPixel 的插屏广告间隔控制器。
/// 每成功展示指定次数的插屏广告后，逐步缩短下一次插屏广告的等待时间。
/// </summary>
public sealed class MPAdsAdIntervalController : AOAdsBaseAdIntervalController, IMPAdsCallbackReceiver
{
    /// <summary>
    /// 初始插屏广告间隔，单位为秒。
    /// </summary>
    public float DefaultInterstitialInterval { get; private set; }

    /// <summary>
    /// 每次调整时减少的间隔，单位为秒。
    /// </summary>
    public float InterstitialIntervalReduction { get; private set; }

    /// <summary>
    /// 插屏广告允许使用的最小间隔，单位为秒。
    /// </summary>
    public float MinimumInterstitialInterval { get; private set; }

    /// <summary>
    /// 每成功展示多少次插屏广告后调整一次间隔。
    /// </summary>
    public int ViewsPerReduction { get; private set; }

    /// <summary>
    /// 本次运行期间成功展示的插屏广告次数。
    /// </summary>
    public int InterstitialViewCount { get; private set; }

    /// <summary>
    /// 当前生效的插屏广告间隔，单位为秒。
    /// </summary>
    public float CurrentInterstitialInterval => GameInterval;

    public MPAdsAdIntervalController(
        float defaultInterstitialInterval = 120.0f,
        float interstitialIntervalReduction = 5.0f,
        float minimumInterstitialInterval = 60.0f,
        int viewsPerReduction = 3)
    {
        Configure(
            defaultInterstitialInterval,
            interstitialIntervalReduction,
            minimumInterstitialInterval,
            viewsPerReduction);
    }

    /// <summary>
    /// 更新广告间隔配置，并重新从默认间隔开始计数。
    /// </summary>
    public void Configure(
        float defaultInterstitialInterval,
        float interstitialIntervalReduction,
        float minimumInterstitialInterval,
        int viewsPerReduction)
    {
        DefaultInterstitialInterval = Mathf.Max(0.0f, defaultInterstitialInterval);
        InterstitialIntervalReduction = Mathf.Max(0.0f, interstitialIntervalReduction);
        MinimumInterstitialInterval = Mathf.Clamp(
            minimumInterstitialInterval,
            0.0f,
            DefaultInterstitialInterval);
        ViewsPerReduction = Mathf.Max(1, viewsPerReduction);

        ResetProgress();
    }

    /// <summary>
    /// 清空本次运行的观看进度，并恢复默认插屏广告间隔。
    /// </summary>
    public void ResetProgress()
    {
        InterstitialViewCount = 0;
        GameInterval = DefaultInterstitialInterval;
    }

    public void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType)
    {
    }

    public void OnAdsDidDisplay(string adScene, AOAdsInfo info)
    {
        if (info == null || info.AdType != AOAdsType.Interstitial)
        {
            return;
        }

        InterstitialViewCount++;
        if (InterstitialViewCount % ViewsPerReduction != 0)
        {
            return;
        }

        int reductionCount = InterstitialViewCount / ViewsPerReduction;
        float totalReduction = reductionCount * InterstitialIntervalReduction;
        GameInterval = Mathf.Max(
            MinimumInterstitialInterval,
            DefaultInterstitialInterval - totalReduction);

        AOAds.DebugLog(
            $"MP interval updated. Views:{InterstitialViewCount} Interval:{GameInterval:F2}s");
    }

    public void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward)
    {
    }

    public void OnAdsDidFailed(string adScene, AOAdsInfo info)
    {
    }
}

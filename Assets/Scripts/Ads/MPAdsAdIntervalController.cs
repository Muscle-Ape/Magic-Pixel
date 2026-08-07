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

    /// <summary>
    /// 创建插屏广告间隔策略，并对所有配置值进行安全修正。
    /// </summary>
    /// <param name="defaultInterstitialInterval">初始插屏间隔，单位为秒。</param>
    /// <param name="interstitialIntervalReduction">每次调整减少的间隔，单位为秒。</param>
    /// <param name="minimumInterstitialInterval">允许使用的最小间隔，单位为秒。</param>
    /// <param name="viewsPerReduction">每成功展示多少次插屏后调整一次间隔。</param>
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
    /// <param name="defaultInterstitialInterval">初始插屏间隔，单位为秒。</param>
    /// <param name="interstitialIntervalReduction">每次调整减少的间隔，单位为秒。</param>
    /// <param name="minimumInterstitialInterval">允许使用的最小间隔，单位为秒。</param>
    /// <param name="viewsPerReduction">每成功展示多少次插屏后调整一次间隔。</param>
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

    /// <summary>
    /// 当前策略不需要处理广告展示前回调。
    /// </summary>
    public void OnAdsWillDisplay(string adScene, bool ready, AOAdsType adType)
    {
    }

    /// <summary>
    /// 插屏广告成功展示后累计次数，并在达到调整次数时更新下一次插屏间隔。
    /// </summary>
    public void OnAdsDidDisplay(string adScene, AOAdsInfo info)
    {
        // 仅统计真实成功展示的插屏广告，激励广告和开屏广告不参与间隔递减。
        if (info == null || info.AdType != AOAdsType.Interstitial)
        {
            return;
        }

        InterstitialViewCount++;
        // 未达到本轮调整次数时继续保持当前间隔。
        if (InterstitialViewCount % ViewsPerReduction != 0)
        {
            return;
        }

        // 始终使用初始值重新计算，避免多次浮点减法产生累计误差。
        int reductionCount = InterstitialViewCount / ViewsPerReduction;
        float totalReduction = reductionCount * InterstitialIntervalReduction;
        GameInterval = Mathf.Max(
            MinimumInterstitialInterval,
            DefaultInterstitialInterval - totalReduction);

        AOAds.DebugLog(
            $"MP interval updated. Views:{InterstitialViewCount} Interval:{GameInterval:F2}s");
    }

    /// <summary>
    /// 当前策略不需要处理广告关闭回调。
    /// </summary>
    public void OnAdsDidDismissed(string adScene, AOAdsInfo info, bool isReceivedReward)
    {
    }

    /// <summary>
    /// 当前策略不需要处理广告展示失败回调，失败广告不会增加展示次数。
    /// </summary>
    public void OnAdsDidFailed(string adScene, AOAdsInfo info)
    {
    }
}

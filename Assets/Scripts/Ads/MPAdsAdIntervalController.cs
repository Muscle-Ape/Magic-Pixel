using UnityEngine;

/// <summary>
/// MagicPixel 的插屏广告间隔控制器。
/// 仅在游戏阶段累计时间，每成功展示指定次数后逐步缩短等待间隔。
/// </summary>
public sealed class MPAdsAdIntervalController
{
    public float DefaultInterstitialInterval { get; private set; }
    public float InterstitialIntervalReduction { get; private set; }
    public float MinimumInterstitialInterval { get; private set; }
    public int ViewsPerReduction { get; private set; }
    public int InterstitialViewCount { get; private set; }
    public float CurrentInterstitialInterval { get; private set; }
    public float GameplayDuration { get; private set; }

    private bool m_isGameplayActive;

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

    public void EnterGameplay()
    {
        m_isGameplayActive = true;
    }

    public void LeaveGameplay()
    {
        m_isGameplayActive = false;
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (m_isGameplayActive && unscaledDeltaTime > 0.0f)
            GameplayDuration += unscaledDeltaTime;
    }

    public bool CanAdPlay()
    {
        return GameplayDuration > CurrentInterstitialInterval;
    }

    public void RecordInterstitialDisplayed()
    {
        GameplayDuration = 0.0f;
        InterstitialViewCount++;

        int reductionCount = InterstitialViewCount / ViewsPerReduction;
        CurrentInterstitialInterval = Mathf.Max(
            MinimumInterstitialInterval,
            DefaultInterstitialInterval - reductionCount * InterstitialIntervalReduction);

        Debug.Log(
            $"[MPAdsManager] 插屏计数更新。Views:{InterstitialViewCount} " +
            $"Interval:{CurrentInterstitialInterval:F2}s");
    }

    public void ResetProgress()
    {
        InterstitialViewCount = 0;
        GameplayDuration = 0.0f;
        CurrentInterstitialInterval = DefaultInterstitialInterval;
    }
}

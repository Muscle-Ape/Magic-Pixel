using DG.Tweening;
using HQ.UIManager;
using UnityEngine;
using UnityEngine.UI;

public partial class MPTransitionView
{
    [TransformPath("View/Grid")]
    private CanvasGroup m_gridGroup;
    [TransformPath("View/Motion")]
    private CanvasGroup m_motionGroup;
    [TransformPath("View/Motion/Artwork/Sky")]
    private Image m_motionSky;
    [TransformPath("View/Motion/Artwork/CloudBack")]
    private RectTransform m_cloudBack;
    [TransformPath("View/Motion/Artwork/CloudMiddle")]
    private RectTransform m_cloudMiddle;
    [TransformPath("View/Motion/Artwork/CloudFront")]
    private RectTransform m_cloudFront;
    [TransformPath("View/Motion/Artwork/Orbit")]
    private RectTransform m_orbit;

    private Tween m_motionTween;
    private Vector2 m_cloudBackHome;
    private Vector2 m_cloudMiddleHome;
    private Vector2 m_cloudFrontHome;
    private Quaternion m_orbitHome;
    private bool m_motionReady;
    private bool m_motionAtEnd;
    private float m_motionCycleDuration;
    private MPTransitionGridLayout m_motionLayout;
    private Image[] m_tileImages;
    private Sprite[] m_entrySprites;

    private void CacheMotionPose()
    {
        m_motionLayout = m_grid != null ? m_grid.GetComponent<MPTransitionGridLayout>() : null;
        m_motionReady = m_gridGroup != null && m_motionGroup != null &&
            m_motionSky != null && m_motionSky.sprite != null &&
            m_cloudBack != null && m_cloudMiddle != null && m_cloudFront != null && m_orbit != null &&
            m_motionLayout != null && m_motionLayout.HasExitSprites(m_items.Count);
        if (!m_motionReady)
        {
            string reason = m_motionLayout == null
                ? "View/Grid 缺少 MPTransitionGridLayout 组件"
                : !m_motionLayout.HasExitSprites(m_items.Count)
                    ? "View/Grid 的 Exit Sprites 必须完整绑定 36 张退场切片。请执行 Tools/MagicPixel/Transition/Repair Exit Sprite References"
                    : "View/Grid、View/Motion 的 CanvasGroup 或 Artwork 下的图片/云层/Orbit 引用不完整";
            Debug.LogWarning("[MPTransitionView] 云朵和星点动画未启用：" + reason + "。本次使用静态遮挡过渡。");
            return;
        }

        m_tileImages = new Image[m_items.Count];
        m_entrySprites = new Sprite[m_items.Count];
        for (int i = 0; i < m_items.Count; i++)
        {
            m_tileImages[i] = m_items[i].GetComponent<Image>();
            if (m_tileImages[i] == null || m_tileImages[i].sprite == null)
            {
                m_motionReady = false;
                Debug.LogWarning($"[MPTransitionView] Grid 第 {i + 1} 个方块缺少 Image 或入场 Sprite，本次使用静态遮挡过渡。");
                return;
            }
            m_entrySprites[i] = m_tileImages[i].sprite;
        }

        // 全部是 836×1881 设计坐标；分辨率变化只由 Grid 统一调整 Artwork 的缩放。
        m_cloudBackHome = m_cloudBack.anchoredPosition;
        m_cloudMiddleHome = m_cloudMiddle.anchoredPosition;
        m_cloudFrontHome = m_cloudFront.anchoredPosition;
        m_orbitHome = m_orbit.localRotation;
    }

    private void PlayCoveredMotion()
    {
        if (!m_motionReady)
        {
            // 素材/节点缺失时仍保留完整方块遮挡，不让切页卡住。
            if (m_autoClose)
            {
                m_delayTween = DOVirtual.DelayedCall(m_stayDuration, () =>
                {
                    m_delayTween = null;
                    PlayCloseAnimation();
                }).SetUpdate(true).SetLink(gameObject);
            }
            return;
        }

        m_motionAtEnd = false;
        m_motionCycleDuration = m_autoClose ? m_stayDuration : DEFAULT_STAY_DURATION;

        ApplyMotionPhase(0f);
        // Sky 是不透明底图，先显示前景再隐藏拼图，避免已烘焙的云留在后面形成重影。
        m_motionGroup.alpha = 1f;
        m_gridGroup.alpha = 0f;
        PlayNextMotionCycle();
    }

    private void PlayNextMotionCycle()
    {
        if (this == null || IsDestoried || m_stage != TransitionStage.Covered)
            return;

        m_motionTween = DOVirtual.Float(0f, 1f, m_motionCycleDuration, ApplyMotionPhase)
            .SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject).OnComplete(() =>
            {
                m_motionTween = null;
                if (this == null || IsDestoried || m_stage != TransitionStage.Covered)
                    return;

                // 云保留终点位置，星点转满一圈后消除旋转误差，精确接到退场图集。
                m_motionAtEnd = true;
                ApplyMotionPhase(0f);

                if (m_closeRequested || m_autoClose)
                    PlayCloseAnimation();
                else
                    // 长时间手动等待时只继续转星点，云不倒退、不重置，也不会移出延展范围。
                    PlayNextMotionCycle();
            });
    }

    private void ApplyMotionPhase(float phase)
    {
        if (!m_motionReady || m_cloudBack == null || m_cloudMiddle == null ||
            m_cloudFront == null || m_orbit == null)
            return;

        // 每层始终向同一方向匀速移动；到终点之后不再来回摆动。
        float offset = m_motionAtEnd ? 1f : Mathf.Clamp01(phase);
        m_cloudBack.anchoredPosition = m_cloudBackHome + Vector2.right * (MPTransitionGridLayout.CLOUD_BACK_TRAVEL * offset);
        m_cloudMiddle.anchoredPosition = m_cloudMiddleHome + Vector2.right * (MPTransitionGridLayout.CLOUD_MIDDLE_TRAVEL * offset);
        m_cloudFront.anchoredPosition = m_cloudFrontHome + Vector2.right * (MPTransitionGridLayout.CLOUD_FRONT_TRAVEL * offset);
        m_orbit.localRotation = m_orbitHome * Quaternion.Euler(0f, 0f, -360f * phase);
    }

    private void ResetMotionPresentation()
    {
        m_motionAtEnd = false;
        ApplyMotionPhase(0f);
        SetTileArtwork(false);
        if (m_gridGroup != null)
            m_gridGroup.alpha = 1f;
        if (m_motionGroup != null)
            m_motionGroup.alpha = 0f;
    }

    private void PrepareMotionForExit()
    {
        // 已移动时使用终点图集；跳过中间动画时仍使用起始图集。
        SetTileArtwork(m_motionAtEnd);
        if (m_gridGroup != null)
            m_gridGroup.alpha = 1f;
        if (m_motionGroup != null)
            m_motionGroup.alpha = 0f;
    }

    private void SetTileArtwork(bool exit)
    {
        if (!m_motionReady)
            return;
        for (int i = 0; i < m_tileImages.Length; i++)
        {
            if (m_tileImages[i] == null)
                continue;
            Sprite sprite = exit ? m_motionLayout.GetExitSprite(i) : m_entrySprites[i];
            if (m_tileImages[i].sprite != sprite)
                m_tileImages[i].sprite = sprite;
        }
    }
}

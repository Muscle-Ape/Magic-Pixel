using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MPCustomBlock : MonoBehaviour
{
    private const float MARK_SCALE_ANIMATION_DURATION = 0.18f;

    /// <summary>
    /// 用于显示颜色的图片
    /// </summary>
    private Image m_colorImg;

    /// <summary>
    /// 格子外框。
    /// </summary>
    private Image m_frame;

    /// <summary>
    /// 预制体默认外框；尺寸图片缺失时用于恢复。
    /// </summary>
    private Sprite m_defaultFrameSprite;

    /// <summary>
    /// 填充状态使用的图片。
    /// </summary>
    private Image m_fillImage;
    private Sprite m_defaultColorSprite;
    private Sprite m_defaultFillSprite;

    /// <summary>
    /// 用于填色的图片
    /// </summary>
    private GameObject m_fill;

    /// <summary>
    /// 是否已经填充
    /// </summary>
    private bool m_isFill;
    public bool isFill => m_isFill;

    /// <summary>
    /// 当前是否处于填充编辑模式，用于中断动画后恢复正确显示。
    /// </summary>
    private bool m_isFillMode;

    /// <summary>
    /// 是否已经上色
    /// </summary>
    private bool m_isColor;
    public bool isColor => m_isColor;

    /// <summary>
    /// 当前的颜色
    /// </summary>
    private Color m_color;
    public Color color => m_color;

    /// <summary>
    /// 颜色渐变动画
    /// </summary>
    private Tween m_colorTween;
    private Tween m_colorScaleTween;
    private Tween m_fillScaleTween;

    public void Init()
    {
        m_colorImg = transform.Find("Color").GetComponent<Image>();
        m_frame = transform.Find("Frame")?.GetComponent<Image>();
        m_defaultFrameSprite = m_frame == null ? null : m_frame.sprite;
        m_defaultColorSprite = m_colorImg == null ? null : m_colorImg.sprite;

        Transform fill = transform.Find("Fill");
        m_fill = fill == null ? null : fill.gameObject;
        m_fillImage = fill == null ? null : fill.GetComponent<Image>();
        m_defaultFillSprite = m_fillImage == null ? null : m_fillImage.sprite;
    }

    /// <summary>
    /// 设置当前尺寸的统一外框；传空时恢复预制体默认图片。
    /// </summary>
    public void SetFrameSprite(Sprite sprite)
    {
        if (m_frame == null)
            return;

        Sprite targetSprite = sprite != null ? sprite : m_defaultFrameSprite;
        if (m_frame.sprite == targetSprite)
            return;

        m_frame.sprite = targetSprite;
    }

    /// <summary>
    /// Color 和 Fill 使用同一张当前尺寸的填充图片。
    /// 传空时分别恢复预制体中对应节点的默认图片。
    /// </summary>
    public void SetFillSprite(Sprite sprite)
    {
        SetImageSprite(m_colorImg, sprite != null ? sprite : m_defaultColorSprite);
        SetImageSprite(m_fillImage, sprite != null ? sprite : m_defaultFillSprite);
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image != null && image.sprite != sprite)
            image.sprite = sprite;
    }

    public bool ColorIsSame(Color color)
    {
        return m_isColor && m_color == color;
    }

    public void SetColor(Color color, bool playAnimation = true)
    {
        if (m_colorImg == null || (m_isColor && m_color == color))
            return;

        m_colorTween?.Kill();
        m_colorTween = null;
        m_colorScaleTween?.Kill();
        m_colorScaleTween = null;
        m_isColor = true;
        m_color = color;
        m_colorImg.color = color;

        RectTransform colorTransform = m_colorImg.rectTransform;
        if (!playAnimation)
        {
            colorTransform.localScale = Vector3.one;
            return;
        }

        colorTransform.localScale = Vector3.zero;
        m_colorScaleTween = colorTransform
            .DOScale(Vector3.one, MARK_SCALE_ANIMATION_DURATION)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject)
            .OnComplete(() => m_colorScaleTween = null);
    }

    public void ClearColor(bool playAnimation = true)
    {
        m_colorTween?.Kill();
        m_colorTween = null;
        m_colorScaleTween?.Kill();
        m_colorScaleTween = null;

        bool wasColored = m_isColor;
        m_isColor = false;
        if (m_colorImg == null)
            return;

        Color displayColor = m_colorImg.color;
        displayColor.a = 1f;
        m_colorImg.color = displayColor;

        RectTransform colorTransform = m_colorImg.rectTransform;
        if (!playAnimation || !wasColored)
        {
            colorTransform.localScale = Vector3.zero;
            return;
        }

        colorTransform.localScale = Vector3.one;
        Tween tween = colorTransform
            .DOScale(Vector3.zero, MARK_SCALE_ANIMATION_DURATION)
            .SetEase(Ease.InBack)
            .SetLink(gameObject);
        m_colorScaleTween = tween;
        tween.OnComplete(() =>
        {
            if (m_colorScaleTween == tween)
                m_colorScaleTween = null;
        });
    }

    public void Fill(bool active, bool playAnimation = true)
    {
        bool stateChanged = m_isFill != active;
        m_isFill = active;
        if (m_fill == null)
            return;

        m_fillScaleTween?.Kill();
        m_fillScaleTween = null;
        RectTransform fillTransform = m_fill.transform as RectTransform;
        if (fillTransform == null)
        {
            m_fill.SetActive(active);
            return;
        }

        if (!playAnimation || !stateChanged)
        {
            fillTransform.localScale = active ? Vector3.one : Vector3.zero;
            m_fill.SetActive(active);
            return;
        }

        m_fill.SetActive(true);
        fillTransform.localScale = active ? Vector3.zero : Vector3.one;
        Vector3 targetScale = active ? Vector3.one : Vector3.zero;
        Tween tween = fillTransform
            .DOScale(targetScale, MARK_SCALE_ANIMATION_DURATION)
            .SetEase(active ? Ease.OutBack : Ease.InBack)
            .SetLink(gameObject);
        m_fillScaleTween = tween;
        tween.OnComplete(() =>
        {
            if (m_fillScaleTween != tween)
                return;

            m_fillScaleTween = null;
            if (!m_isFill)
                m_fill.SetActive(false);
        });
    }

    /// <summary>
    /// 切换模式
    /// </summary>
    /// <param name="isFill">是否为填充模式</param>
    public void SetMode(bool isFill)
    {
        m_isFillMode = isFill;
        m_fillScaleTween?.Kill();
        m_fillScaleTween = null;
        if (m_fill != null)
        {
            m_fill.transform.localScale = m_isFill ? Vector3.one : Vector3.zero;
            m_fill.SetActive(isFill && m_isFill);
        }

        if (isFill)
        {
            if (m_isColor)
            {
                m_colorTween?.Kill();
                m_colorTween = m_colorImg.DOFade(0.5f, 0.1f);
            }
        }
        else
        {
            if (m_isColor)
            {
                m_colorTween?.Kill();
                m_colorTween = m_colorImg.DOFade(1f, 0.1f);
            }
        }
    }

    private void OnDisable()
    {
        m_colorTween?.Kill();
        m_colorTween = null;
        m_colorScaleTween?.Kill();
        m_colorScaleTween = null;
        m_fillScaleTween?.Kill();
        m_fillScaleTween = null;

        if (m_colorImg != null)
        {
            m_colorImg.rectTransform.localScale = m_isColor ? Vector3.one : Vector3.zero;
            Color displayColor = m_colorImg.color;
            displayColor.a = m_isColor && m_isFillMode ? 0.5f : 1f;
            m_colorImg.color = displayColor;
        }

        if (m_fill != null)
        {
            m_fill.transform.localScale = m_isFill ? Vector3.one : Vector3.zero;
            m_fill.SetActive(m_isFillMode && m_isFill);
        }
    }

}

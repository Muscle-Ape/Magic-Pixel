using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MPCustomBlock : MonoBehaviour
{
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
    /// 填充状态及其颜色层使用的图片。
    /// </summary>
    private Image m_fillImage;
    private Image m_fillColorImage;
    private Sprite m_defaultColorSprite;
    private Sprite m_defaultFillSprite;
    private Sprite m_defaultFillColorSprite;

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

    public void Init()
    {
        m_colorImg = transform.Find("Color").GetComponent<Image>();
        m_frame = transform.Find("Frame")?.GetComponent<Image>();
        m_defaultFrameSprite = m_frame == null ? null : m_frame.sprite;
        m_defaultColorSprite = m_colorImg == null ? null : m_colorImg.sprite;

        Transform fill = transform.Find("Fill");
        m_fill = fill == null ? null : fill.gameObject;
        m_fillImage = fill == null ? null : fill.GetComponent<Image>();
        m_fillColorImage = transform.Find("Fill/Color")?.GetComponent<Image>();
        m_defaultFillSprite = m_fillImage == null ? null : m_fillImage.sprite;
        m_defaultFillColorSprite = m_fillColorImage == null ? null : m_fillColorImage.sprite;
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
    /// Color、Fill、Fill/Color 使用同一张当前尺寸的填充图片。
    /// 传空时分别恢复预制体中对应节点的默认图片。
    /// </summary>
    public void SetFillSprite(Sprite sprite)
    {
        SetImageSprite(m_colorImg, sprite != null ? sprite : m_defaultColorSprite);
        SetImageSprite(m_fillImage, sprite != null ? sprite : m_defaultFillSprite);
        SetImageSprite(m_fillColorImage, sprite != null ? sprite : m_defaultFillColorSprite);
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

    public void SetColor(Color color)
    {
        if (!m_isColor || m_color != color)
        {
            m_colorTween?.Kill();
            m_colorTween = null;
            m_isColor = true;
            m_color = color;
            m_colorImg.color = color;
        }
    }

    public void ClearColor()
    {
        m_colorTween?.Kill();
        m_colorTween = null;
        m_isColor = false;
        m_colorImg.color = m_color = new Color(1, 1, 1, 0);

    }

    public void Fill(bool active)
    {
        m_isFill = active;
        m_fill.SetActive(active);
    }

    /// <summary>
    /// 切换模式
    /// </summary>
    /// <param name="isFill">是否为填充模式</param>
    public void SetMode(bool isFill)
    {
        if (isFill)
        {
            m_fill.SetActive(m_isFill);

            if (m_isColor)
            {
                m_colorTween?.Kill();
                m_colorTween = m_colorImg.DOFade(0.5f, 0.1f);
            }
        }
        else
        {
            m_fill.SetActive(false);

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
    }

}

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

    /// <summary>
    /// 当前的颜色
    /// </summary>
    private Color m_color;

    /// <summary>
    /// 颜色渐变动画
    /// </summary>
    private Tween m_colorTween;

    public void Init()
    {
        m_colorImg = transform.Find("Color").GetComponent<Image>();
        m_fill = transform.Find("Fill").gameObject;
    }

    public bool ColorIsSame(Color color)
    {
        return m_isColor && m_color == color;
    }

    public void SetColor(Color color)
    {
        if (m_color != color)
        {
            m_isColor = true;
            m_color = color;
            m_colorImg.color = color;
        }
    }

    public void ClearColor()
    {
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

}

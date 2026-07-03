using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPPalette : MonoBehaviour
{
    /// <summary>
    /// 色块
    /// </summary>
    private Image m_colorBlock;

    /// <summary>
    /// R Value
    /// </summary>
    private TMP_Text m_R;

    /// <summary>
    /// G Value
    /// </summary>
    private TMP_Text m_G;

    /// <summary>
    /// B Value
    /// </summary>
    private TMP_Text m_B;

    private Action<Color> m_setColor;

    /// <summary>
    /// 色相面板
    /// </summary>
    private MPHuePanel m_huePanel;

    /// <summary>
    /// 饱和度面板
    /// </summary>
    private MPStaurationPanel m_staurationPanel;

    public void Initialization(Action<Color> setColor)
    {
        m_setColor = setColor;

        m_colorBlock = transform.Find("ColorFrame/Color").GetComponent<Image>();
        m_R = transform.Find("ColorPanel/R/Value").GetComponent<TMP_Text>();
        m_G = transform.Find("ColorPanel/G/Value").GetComponent<TMP_Text>();
        m_B = transform.Find("ColorPanel/B/Value").GetComponent<TMP_Text>();

        m_staurationPanel = transform.Find("ColorPanel/Stauration").GetComponent<MPStaurationPanel>();
        m_huePanel = transform.Find("ColorPanel/Hue").GetComponent<MPHuePanel>();
        transform.Find("ColorPanel/PickColorFrame").GetComponent<MPPickColor>().Initialization(SetPaletteColor);

        m_staurationPanel.Initialization(SetColor);
        m_huePanel.Initialization(m_staurationPanel);
    }

    private void SetColor(Color color)
    {
        m_colorBlock.color = color;

        m_R.text = ((byte)(Mathf.Clamp01(color.r) * 255f)).ToString();
        m_G.text = ((byte)(Mathf.Clamp01(color.g) * 255f)).ToString();
        m_B.text = ((byte)(Mathf.Clamp01(color.b) * 255f)).ToString();

        m_setColor?.Invoke(color);
    }

    /// <summary>
    /// 根据颜色设置调色板各点的位置（取色器功能）
    /// </summary>
    /// <param name="color">目标颜色</param>
    private void SetPaletteColor(Color color)
    {
        SetColor(color);

        Color.RGBToHSV(color, out float h, out float s, out float v);
        m_huePanel.SetHueByHSV(h, s, v);
    }
}

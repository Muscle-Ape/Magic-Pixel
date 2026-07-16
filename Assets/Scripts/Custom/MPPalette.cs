using System;
using UnityEngine;
using UnityEngine.UI;

public class MPPalette : MonoBehaviour
{
    /// <summary>
    /// 色块。
    /// </summary>
    private Image m_colorBlock;

    /// <summary>
    /// 颜色变更回调。
    /// </summary>
    private Action<Color> m_setColor;

    /// <summary>
    /// 色相面板。
    /// </summary>
    private MPHuePanel m_huePanel;

    /// <summary>
    /// 饱和度面板。
    /// </summary>
    private MPStaurationPanel m_staurationPanel;

    public void Initialization(Action<Color> setColor)
    {
        m_setColor = setColor;

        m_colorBlock = transform.Find("ColorFrame/Color").GetComponent<Image>();

        m_staurationPanel = transform.Find("Stauration").GetComponent<MPStaurationPanel>();
        m_huePanel = transform.Find("Hue").GetComponent<MPHuePanel>();
        transform.Find("PickColorFrame").GetComponent<MPPickColor>().Initialization(SetPaletteColor);

        m_staurationPanel.Initialization(SetColor);
        m_huePanel.Initialization(m_staurationPanel);
    }

    private void SetColor(Color color)
    {
        m_colorBlock.color = color;

        m_setColor?.Invoke(color);
    }

    /// <summary>
    /// 根据颜色设置调色板各节点的位置。
    /// </summary>
    /// <param name="color">目标颜色。</param>
    private void SetPaletteColor(Color color)
    {
        SetColor(color);

        Color.RGBToHSV(color, out float h, out float s, out float v);
        m_huePanel.SetHueByHSV(h, s, v);
    }
}

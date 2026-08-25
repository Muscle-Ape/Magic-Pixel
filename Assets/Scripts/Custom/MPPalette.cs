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

        Transform colorBlock = transform.Find("ColorFrame/Color");
        Transform stauration = transform.Find("Stauration")
            ?? transform.Find("StaurationFrame/StaurationMask/Stauration");
        Transform hue = transform.Find("Hue")
            ?? transform.Find("HueMask/Hue");
        Transform pickColor = transform.Find("PickColorFrame");

        m_colorBlock = colorBlock == null ? null : colorBlock.GetComponent<Image>();
        m_staurationPanel = stauration == null ? null : stauration.GetComponent<MPStaurationPanel>();
        m_huePanel = hue == null ? null : hue.GetComponent<MPHuePanel>();
        MPPickColor pickColorPanel = pickColor == null ? null : pickColor.GetComponent<MPPickColor>();

        if (m_colorBlock == null || m_staurationPanel == null || m_huePanel == null || pickColorPanel == null)
        {
            Debug.LogError($"调色板节点不完整：{name}", this);
            return;
        }

        pickColorPanel.Initialization(SetPaletteColor);

        m_staurationPanel.Initialization(SetColor);
        m_huePanel.Initialization(m_staurationPanel);
    }

    private void SetColor(Color color)
    {
        if (m_colorBlock != null)
            m_colorBlock.color = color;

        m_setColor?.Invoke(color);
    }

    /// <summary>
    /// 根据颜色设置调色板各节点的位置。
    /// </summary>
    /// <param name="color">目标颜色。</param>
    public void SetPaletteColor(Color color)
    {
        SetColor(color);

        if (m_huePanel == null)
            return;

        Color.RGBToHSV(color, out float h, out float s, out float v);
        m_huePanel.SetHueByHSV(h, s, v);
    }
}

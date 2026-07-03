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


    public void Initialization(Action<Color> setColor)
    {
        m_setColor = setColor;

        m_colorBlock = transform.Find("ColorFrame/Color").GetComponent<Image>();
        m_R = transform.Find("ColorPanel/R/Value").GetComponent<TMP_Text>();
        m_G = transform.Find("ColorPanel/G/Value").GetComponent<TMP_Text>();
        m_B = transform.Find("ColorPanel/B/Value").GetComponent<TMP_Text>();

        MPStaurationPanel staurationPanel = transform.Find("ColorPanel/Stauration").GetComponent<MPStaurationPanel>();
        MPHuePanel huePanel = transform.Find("ColorPanel/Hue").GetComponent<MPHuePanel>();

        staurationPanel.Initialization(SetColor);
        huePanel.Initialization(staurationPanel);
    }

    private void SetColor(Color color)
    {
        m_colorBlock.color = color;

        m_R.text = ((byte)(Mathf.Clamp01(color.r) * 255f)).ToString();
        m_G.text = ((byte)(Mathf.Clamp01(color.g) * 255f)).ToString();
        m_B.text = ((byte)(Mathf.Clamp01(color.b) * 255f)).ToString();

        m_setColor?.Invoke(color);
    }
}

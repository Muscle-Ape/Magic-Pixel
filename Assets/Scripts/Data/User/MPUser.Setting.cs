using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用户资产
/// </summary>
public partial class MPUser
{
    #region Key
    private string m_key_isMusic = "key_isMusic";
    private string m_key_isSound = "key_isSound";
    private string m_key_isVibration = "key_isVibration";
    private const string GAME_FILL_COLOR_KEY = "key_gameFillColor";
    #endregion


    #region Fields
    /// <summary>
    /// 是否开启背景音乐
    /// </summary>
    private bool m_isMusic;
    public bool isMusic => m_isMusic;

    /// <summary>
    /// 是否开启音效
    /// </summary>
    private bool m_isSound;
    public bool isSound => m_isSound;

    /// <summary>
    /// 是否开启震动
    /// </summary>
    private bool m_isVibration;
    public bool isVibration => m_isVibration;

    private Color m_gameFillColor = Color.white;
    public Color gameFillColor => m_gameFillColor;
    #endregion


    private void InitSetting()
    {
        m_isMusic = ES3.Load<bool>(m_key_isMusic, true);
        m_isSound = ES3.Load<bool>(m_key_isSound, true);
        m_isVibration = ES3.Load<bool>(m_key_isVibration, true);
        string colorText = ES3.Load<string>(GAME_FILL_COLOR_KEY, defaultValue: "#FFFFFFFF");
        if (!ColorUtility.TryParseHtmlString(colorText, out m_gameFillColor))
            m_gameFillColor = Color.white;
        m_gameFillColor.a = 1f;
    }


    #region Method
    public void SetMusicStatus(bool isOpen)
    {
        m_isMusic = isOpen;

        ES3.Save(m_key_isMusic, m_isMusic);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Settings);
    }

    public void SetSoundStatus(bool isOpen)
    {
        m_isSound = isOpen;

        ES3.Save(m_key_isSound, m_isSound);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Settings);
    }

    public void SetVibrationStatus(bool isOpen)
    {
        m_isVibration = isOpen;

        ES3.Save(m_key_isVibration, m_isVibration);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Settings);
    }

    /// <summary>填充块着色仅是表现设置，不修改答案像素颜色。</summary>
    public void SetGameFillColor(Color color)
    {
        color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
        if (m_gameFillColor == color)
            return;
        m_gameFillColor = color;
        ES3.Save(GAME_FILL_COLOR_KEY, "#" + ColorUtility.ToHtmlStringRGBA(color));
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Settings);
    }
    #endregion
}

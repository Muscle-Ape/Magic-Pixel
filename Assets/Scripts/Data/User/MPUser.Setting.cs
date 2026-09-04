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

    /// <summary>Color1 蓝色底图的主体色（#426E99），也是首次进入游戏的默认填充色。</summary>
    public static readonly Color DefaultGameFillColor = new Color32(66, 110, 153, 255);

    private Color m_gameFillColor = DefaultGameFillColor;
    public Color gameFillColor => m_gameFillColor;
    #endregion


    private void InitSetting()
    {
        m_isMusic = ES3.Load<bool>(m_key_isMusic, true);
        m_isSound = ES3.Load<bool>(m_key_isSound, true);
        m_isVibration = ES3.Load<bool>(m_key_isVibration, true);
        string colorText = ES3.Load<string>(GAME_FILL_COLOR_KEY,
            defaultValue: "#" + ColorUtility.ToHtmlStringRGBA(DefaultGameFillColor));
        if (!ColorUtility.TryParseHtmlString(colorText, out Color savedColor))
            savedColor = DefaultGameFillColor;
        m_gameFillColor = NormalizeGameFillColor(savedColor);
        // 旧 Color1 曾错误保存为白色，只修正这一错误值，保留其他已选颜色。
        if (savedColor.r == 1f && savedColor.g == 1f && savedColor.b == 1f)
            ES3.Save(GAME_FILL_COLOR_KEY, "#" + ColorUtility.ToHtmlStringRGBA(m_gameFillColor));
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
        color = NormalizeGameFillColor(color);
        if (m_gameFillColor == color)
            return;
        m_gameFillColor = color;
        ES3.Save(GAME_FILL_COLOR_KEY, "#" + ColorUtility.ToHtmlStringRGBA(color));
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Settings);
    }

    /// <summary>统一处理本地设置与云存档颜色，避免旧 Color1 的白色值在同步后再次生效。</summary>
    private static Color NormalizeGameFillColor(Color color)
    {
        color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
        return color.r == 1f && color.g == 1f && color.b == 1f ? DefaultGameFillColor : color;
    }
    #endregion
}

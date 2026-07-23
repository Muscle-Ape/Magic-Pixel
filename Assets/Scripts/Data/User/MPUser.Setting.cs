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
    #endregion


    private void InitSetting()
    {
        m_isMusic = ES3.Load<bool>(m_key_isMusic, true);
        m_isSound = ES3.Load<bool>(m_key_isSound, true);
        m_isVibration = ES3.Load<bool>(m_key_isVibration, true);
    }


    #region Method
    public void SetMusicStatus(bool isOpen)
    {
        m_isMusic = isOpen;

        ES3.Save(m_key_isMusic, m_isMusic);
    }

    public void SetSoundStatus(bool isOpen)
    {
        m_isSound = isOpen;

        ES3.Save(m_key_isSound, m_isSound);
    }

    public void SetVibrationStatus(bool isOpen)
    {
        m_isVibration = isOpen;

        ES3.Save(m_key_isVibration, m_isVibration);
    }
    #endregion
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JSAM;

public class MPAudioManager
{
    private int m_adAudioPauseCount;
    private bool m_listenerWasPausedBeforeAd;

    #region Singleton
    private static MPAudioManager instance;
    private MPAudioManager() { }
    public static MPAudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new MPAudioManager();
            }

            return instance;
        }
    }
    #endregion

    /// <summary>
    /// 播放音效
    /// </summary>
    /// <param name="emSound"></param>
    /// <param name="isLoop"></param>
    /// <param name="replay"></param>
    public void PlaySound(MPSound emSound, bool isLoop = false, bool replay = false)
    {
        if (!MPUser.instance.isSound) return;

        if (replay)
        {
            SoundChannelHelper helper = AudioManager.PlaySound(emSound);
            helper.AudioSource.loop = isLoop;
        }
        else
        {
            if (!AudioManager.IsSoundPlaying(emSound))
            {
                SoundChannelHelper helper = AudioManager.PlaySound(emSound);
                helper.AudioSource.loop = isLoop;
            }
        }
    }

    /// <summary>
    /// 暂停音效
    /// </summary>
    /// <param name="emSound"></param>
    public void StopSound(MPSound emSound)
    {
        AudioManager.StopSound(emSound);
    }

    /// <summary>
    /// 暂停所有音效
    /// </summary>
    public void StopAllSound()
    {
        AudioManager.StopAllSounds();
    }

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="emMusic"></param>
    public void PlayBGM(MPMusic emMusic)
    {
        if (!MPUser.instance.isMusic) return;

        AudioManager.TryGetPlayingMusic(emMusic, out MusicChannelHelper helper);

        if (helper == null || !helper.AudioSource.isPlaying)
        {
            AudioManager.StopAllMusic();
            AudioManager.PlayMusic(emMusic);
        }
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    /// <param name="emMusic"></param>
    public void StopBGM(MPMusic emMusic)
    {
        AudioManager.StopMusic(emMusic);
    }

    /// <summary>
    /// 暂停所有背景音乐
    /// </summary>
    public void StopAllMusic()
    {
        AudioManager.StopAllMusic(true);
    }

    /// <summary>
    /// 广告展示前暂停游戏音频。使用 AudioListener 可以保留背景音乐当前播放位置，
    /// 并且不会改变玩家在设置中的音乐开关。
    /// </summary>
    public void PauseAudioForAd()
    {
        if (m_adAudioPauseCount == 0)
        {
            m_listenerWasPausedBeforeAd = AudioListener.pause;
            AudioListener.pause = true;
        }
        m_adAudioPauseCount++;
    }

    /// <summary>广告关闭或展示失败后恢复到广告展示前的音频暂停状态。</summary>
    public void ResumeAudioAfterAd()
    {
        if (m_adAudioPauseCount <= 0)
            return;

        m_adAudioPauseCount--;
        if (m_adAudioPauseCount == 0)
            AudioListener.pause = m_listenerWasPausedBeforeAd;
    }
}

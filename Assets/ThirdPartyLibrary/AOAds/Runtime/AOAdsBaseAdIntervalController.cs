using System;
using System.Collections;
using UnityEngine;
public class AOAdsBaseAdIntervalController
{
    public float AppOpenAdInterval = 30;
    public float GameInterval { get { return _gameInterval; } set { _gameInterval = value; } }//广告间隔
    public float GameDuration { get { return _gameDuration; } set { _gameDuration = value; } }//游戏持续时间
    private float _gameInterval = 60.0f; //游戏结束后播放广告间隔
    private float _gameDuration = 0.0f;
    private bool _gameCooldown = false;
    private bool _fullScreenAdDisplaying = false;
    private bool _backgroundCausedByAd = false;
    private DateTime _enterBackground = DateTime.UnixEpoch;
    public void Cooldown()
    {
        // if (_launchCooldown)
        // {
        //     _launchDuration += UnityEngine.Time.deltaTime;
        // }

        if (_gameCooldown)
        {
            _gameDuration += UnityEngine.Time.deltaTime;
        }
    }

    public void AppEnterForeground()
    {
        DebugLog("AppEnterForeground");
        _gameCooldown = true;

        AOAds.Instance.StartCoroutine(ShowWakeUp());
    }

    public void AppEnterBackground()
    {
        DebugLog("AppEnterBackground");
        _gameCooldown = false;

        if (_fullScreenAdDisplaying)
        {
            _backgroundCausedByAd = true;
        }

        _enterBackground = DateTime.Now;
    }

    public void ResetCooldown()
    {
        _gameDuration = 0.0f;
        _enterBackground = DateTime.UnixEpoch;
        DebugLog("ResetCooldown gameInterval:" + _gameInterval + " gameDuration:" + _gameDuration);
    }

    internal void SetFullScreenAdDisplaying(bool displaying)
    {
        _fullScreenAdDisplaying = displaying;
    }

    public void EnterGame()
    {
        DebugLog("EnterGame");
        _gameCooldown = true;
    }

    public void LeaveGame()
    {
        DebugLog("LeaveGame");
        _gameCooldown = false;
    }

    public virtual bool CanAdPlay(string adPlace = null)
    {
        DebugLog("CanGameAdPlay gameInterval:" + _gameInterval.ToString("f2") + " gameDuration:" + _gameDuration.ToString("f2"));

        if (_gameDuration > _gameInterval)
        {
            return true;
        }
        return false;
    }

    IEnumerator ShowWakeUp()
    {
        yield return new WaitForSeconds(0.5f);
        if (_enterBackground != DateTime.UnixEpoch && AOAds.EnableWakeUpAd &&
            !AOAds.PreventNextAdWakeUp && !_fullScreenAdDisplaying && !_backgroundCausedByAd)
        {

            TimeSpan span = DateTime.Now - _enterBackground;
            float seconds = (float)span.TotalSeconds;

            if (AOAds.UseInterWakeUpAd)
            {
                _gameDuration += seconds;
                AOAds.CheckAndShowInterstitialAd("wake_up", null);
            }
            else if (seconds > AppOpenAdInterval)
            {
                AOAds.ShowAppOpenIfReady();
            }
        }

        _backgroundCausedByAd = false;
        AOAds.PreventNextAdWakeUp = false;
    }
    void DebugLog(object message)
    {
        UnityEngine.Debug.Log("[AOAdsInterstitialAdConfig] " + message);
    }

}

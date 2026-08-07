#if ao_ads_max
using UnityEngine;

[CreateAssetMenu(fileName = AOAdsConstants.AOADS_MEDIATION_SETTING_NAME, menuName = "AO Ads/MAX Settings")]
public class AOMaxAdsSettings : ScriptableObject
{
    public static readonly string AOADS_SETTINGS_ASSET_PATH = AOAdsConstants.AOADS_MEDIATION_SETTING_ASSET_PATH;
    
    [Header("MAX SDK")]
    [Tooltip("Max Sdk Key")]
    public string MaxSdkKey = string.Empty;

    [Header("IOSInterstitialAdUnitId")]
    [Tooltip("Max IOS Interstitial AdUnitId")]
    public string MaxIOSInterstitialAdUnitId = string.Empty;

    [Header("IOSRewardedAdUnitId")]
    [Tooltip("Max IOS Rewarded AdUnitId")]
    public string MaxIOSRewardedAdUnitId = string.Empty;

    [Header("MaxIOSBannerAdUnitId")]
    [Tooltip("Max IOS Banner AdUnitId")]
    public string MaxIOSBannerAdUnitId = string.Empty;
    
    [Header("MaxIOSAppOpenAdUnitId")]
    [Tooltip("Max IOS App Open AdUnitId")]
    public string MaxIOSAppOpenAdUnitId = string.Empty;
    

    [Header("AndroidInterstitialAdUnitId")]
    [Tooltip("Max Android Interstitial AdUnitId")]
    public string MaxAndroidInterstitialAdUnitId = string.Empty;

    [Header("AndroidRewardedAdUnitId")]
    [Tooltip("Max Android Rewarded AdUnitId")]
    public string MaxAndroidRewardedAdUnitId = string.Empty;

    [Header("MaxAndroidBannerAdUnitId")]
    [Tooltip("Max Android Banner AdUnitId")]
    public string MaxAndroidBannerAdUnitId = string.Empty;

    [Header("MaxIOSAppOpenAdUnitId")]
    [Tooltip("Max IOS App Open AdUnitId")]
    public string MaxAndroidAppOpenAdUnitId = string.Empty;

    [Header("MaxBannerPosition")]
    [Tooltip("Max Banner Position")]
    public MaxSdkBase.BannerPosition MaxBannerPosition = MaxSdkBase.BannerPosition.BottomCenter;

    public static AOMaxAdsSettings Load()
    {
        return Resources.Load<AOMaxAdsSettings>(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME);
    }

    public string CurrentInterstitialAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return MaxAndroidInterstitialAdUnitId;
#elif UNITY_IOS
            return MaxIOSInterstitialAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public string CurrentRewardedAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return MaxAndroidRewardedAdUnitId;
#elif UNITY_IOS
            return MaxIOSRewardedAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public string CurrentBannerAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return MaxAndroidBannerAdUnitId;
#elif UNITY_IOS
            return MaxIOSBannerAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public string CurrentAppOpenAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return MaxAndroidAppOpenAdUnitId;
#elif UNITY_IOS
            return MaxIOSAppOpenAdUnitId;
#else
            return string.Empty;
#endif
        }
    }

    public bool TryValidateCurrentPlatform(out string error)
    {
        if (string.IsNullOrWhiteSpace(MaxSdkKey))
        {
            error = "MAX SDK Key is empty.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(CurrentInterstitialAdUnitId) ||
            string.IsNullOrWhiteSpace(CurrentRewardedAdUnitId))
        {
            error = "Interstitial or rewarded ad unit ID is empty for the current platform.";
            return false;
        }
        error = null;
        return true;
    }
    
}
#endif
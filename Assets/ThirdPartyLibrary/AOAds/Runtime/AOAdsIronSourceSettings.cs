#if ao_ads_is
using System.IO;
using UnityEngine;

public class AOAdsIronSourceSettings : ScriptableObject
{
    public static readonly string AOADS_SETTINGS_ASSET_PATH = Path.Combine(AOAdsConstants.AOADS_RESOURCES_PATH, AOAdsConstants.AOADS_MEDIATION_SETTING_NAME + ".asset");
    
    [Header("AppKey")]
    [Tooltip("App Key")]
    public string AppKey = string.Empty;
    
}
#endif
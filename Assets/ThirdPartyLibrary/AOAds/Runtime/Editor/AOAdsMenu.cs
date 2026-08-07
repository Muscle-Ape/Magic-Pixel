#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;
public static class AOAdsMenu
{
#if ao_ads_max
    [MenuItem("AOAds/Max Settings", false, 0)]
    public static void maxSettings()
    {
        string path = AOAdsConstants.AOADS_RESOURCES_PATH;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var adsMediationSettings = AOMaxAdsSettings.Load();
        if (adsMediationSettings == null)
        {
            Debug.LogWarning(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME + " can't be found, creating a new one...");
            adsMediationSettings = ScriptableObject.CreateInstance<AOMaxAdsSettings>();
            AssetDatabase.CreateAsset(adsMediationSettings, AOAdsConstants.AOADS_MEDIATION_SETTING_ASSET_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            adsMediationSettings = AOMaxAdsSettings.Load();
        }

        Selection.activeObject = adsMediationSettings;
        EditorGUIUtility.PingObject(adsMediationSettings);
    }
#elif ao_ads_is
    [MenuItem("AOAds/IronSource Settings", false, 0)]
    public static void maxSettings()
    {
        string path = AOAdsConstants.AOADS_RESOURCES_PATH;

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }


        var adsMediationSettings = Resources.Load<AOAdsIronSourceSettings>(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME);
        if (adsMediationSettings == null)
        {
            Debug.LogWarning(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME + " can't be found, creating a new one...");
            adsMediationSettings = ScriptableObject.CreateInstance<AOAdsIronSourceSettings>();
            AssetDatabase.CreateAsset(adsMediationSettings, AOAdsIronSourceSettings.AOADS_SETTINGS_ASSET_PATH);
            adsMediationSettings = Resources.Load<AOAdsIronSourceSettings>(AOAdsConstants.AOADS_MEDIATION_SETTING_NAME);
        }

        Selection.activeObject = adsMediationSettings;
    }
#else
#endif
#endif
}

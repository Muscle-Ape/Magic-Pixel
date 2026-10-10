using UnityEngine;

[CreateAssetMenu(
    fileName = RESOURCE_NAME,
    menuName = "Magic Pixel/Unity Ads Settings")]
public sealed class MPUnityAdsSettings : ScriptableObject
{
    public const string RESOURCE_NAME = "MPUnityAdsSettings";

    [Header("Android LevelPlay")]
    [SerializeField] private string m_androidAppKey;
    [SerializeField] private string m_androidInterstitialAdUnitId;
    [SerializeField] private string m_androidRewardedAdUnitId;

    [Header("iOS LevelPlay")]
    [SerializeField] private string m_iOSAppKey;
    [SerializeField] private string m_iOSInterstitialAdUnitId;
    [SerializeField] private string m_iOSRewardedAdUnitId;

    [Header("Development Diagnostics")]
    [Tooltip("仅在真机 Development Build 中启动 LevelPlay 官方测试套件；测试完成后应关闭。")]
    [SerializeField] private bool m_launchLevelPlayTestSuiteInDevelopment;

    public bool LaunchLevelPlayTestSuiteInDevelopment => m_launchLevelPlayTestSuiteInDevelopment;

    public string CurrentAppKey
    {
        get
        {
#if UNITY_IOS
            return m_iOSAppKey;
#else
            return m_androidAppKey;
#endif
        }
    }

    public string CurrentInterstitialAdUnitId
    {
        get
        {
#if UNITY_IOS
            return m_iOSInterstitialAdUnitId;
#else
            return m_androidInterstitialAdUnitId;
#endif
        }
    }

    public string CurrentRewardedAdUnitId
    {
        get
        {
#if UNITY_IOS
            return m_iOSRewardedAdUnitId;
#else
            return m_androidRewardedAdUnitId;
#endif
        }
    }

    public static MPUnityAdsSettings Load()
    {
        return Resources.Load<MPUnityAdsSettings>(RESOURCE_NAME);
    }

    public bool TryValidateCurrentPlatform(out string error)
    {
        if (string.IsNullOrWhiteSpace(CurrentAppKey))
        {
            error = "当前平台的 LevelPlay App Key 为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentInterstitialAdUnitId))
        {
            error = "当前平台的插屏 Ad Unit ID 为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentRewardedAdUnitId))
        {
            error = "当前平台的激励视频 Ad Unit ID 为空。";
            return false;
        }

        error = null;
        return true;
    }
}

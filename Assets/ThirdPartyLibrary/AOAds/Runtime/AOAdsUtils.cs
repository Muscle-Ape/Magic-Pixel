using System.Runtime.InteropServices;
public class AOAdsUtils
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _init();

    [DllImport("__Internal")]
    private static extern bool _SetAudioFocus();

    [DllImport("__Internal")]
    private static extern void _ResumeAudio();
    
    [DllImport("__Internal")]
    private static extern void _syncAdsCount(int interNum, int rewardNum);

    [DllImport("__Internal")]
    private static extern int _getInterNum();

    [DllImport("__Internal")]
    private static extern int _getRewardNum();
#endif

    public static void Init()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _init();
#endif
    }

    public static void SetAudioFocus()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _SetAudioFocus();
#endif
    }

    public static void ResumeAudio()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _ResumeAudio();
#endif
    }

    public static void SyncAdsCount(int interNum, int rewardNum)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _syncAdsCount(interNum, rewardNum);
#endif
    }


    public static int GetInterNum()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return _getInterNum();
#else
        return 0;
#endif
    }

    public static int GetRewardNum()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return _getRewardNum();
#else
        return 0;
#endif
    }
}
using System.Runtime.InteropServices;

public static class Helper
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool _isVPNConnected();

    [DllImport("__Internal")]
    private static extern bool _isSandbox();
#endif
    public static bool IsVPNConnected()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return _isVPNConnected();
#else
        return false;
#endif
    }

    public static bool IsSandbox()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return _isSandbox();
#else
        return false;
#endif
    }
}
#if UNITY_IOS
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;


public class UnityToastIOSBridge : IUnityToastBridge
{

    [DllImport("__Internal")]
    private static extern void _showToast(string message, double duration, int position);

    [DllImport("__Internal")]
    private static extern void _showToastActivity(int position);

    [DllImport("__Internal")]
    private static extern void _hideToastActivity();

    public void ShowToast(string message, double duration, UnityToastPosition position)
    {
#if !UNITY_EDITOR
            _showToast(message, duration, (int)position);
#endif
    }

    public void ShowToastActivity(UnityToastPosition position)
    {
#if !UNITY_EDITOR
            _showToastActivity((int)position);
#endif
    }

    public void HideToastActivity()
    {
#if !UNITY_EDITOR
            _hideToastActivity();
#endif
    }
}



#endif

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
public class UnityToastEditerBridge : IUnityToastBridge
{


    public void ShowToast(string message, double duration, UnityToastPosition position)
    {
        Debug.Log("[UnityToast] " + message);
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

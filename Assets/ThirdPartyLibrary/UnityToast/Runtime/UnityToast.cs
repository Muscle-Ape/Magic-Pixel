using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UnityToastPosition
{
    Top = 0,
    Center,
    Bottom,
}

public interface IUnityToastBridge
{
    void ShowToast(string message, double duration, UnityToastPosition position);
    void ShowToastActivity(UnityToastPosition position);
    void HideToastActivity();
}

public class UnityToast
{
    private IUnityToastBridge bridge;

    private static object _lock = new object();
    private static UnityToast _instance;
    public static UnityToast Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance != null ? _instance : _instance = new UnityToast();
            }
        }
    }

    public UnityToast()
    {
#if UNITY_IOS && !UNITY_EDITOR
            bridge = new UnityToastIOSBridge();
#elif UNITY_ANDROID && !UNITY_EDITOR
			Debug.Log("not support android platform!");
#else
        bridge = new UnityToastEditerBridge();
        // Debug.Log("not support editor platform!");
#endif
    }

    public void ShowToast(string message, double duration = 2.0f)
    {
        ShowToast(message, duration, UnityToastPosition.Center);
    }

    public void ShowToast(string message, double duration, UnityToastPosition position = UnityToastPosition.Center)
    {
        if (bridge != null)
        {
            bridge.ShowToast(message, duration, position);
        }
        else
        {
            ToastStringShow(message);
        }
    }

    public void ShowToastActivity(UnityToastPosition position = UnityToastPosition.Center)
    {
        if (bridge != null)
        {
            bridge.ShowToastActivity(position);
        }
    }

    public void HideToastActivity()
    {
        if (bridge != null)
        {
            bridge.HideToastActivity();
        }
    }

    public static void ToastStringShow(object str, AndroidJavaObject activity = null)
    {
#if UNITY_ANDROID
            if (activity == null)
            {
                AndroidJavaClass UnityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer"
                );
                activity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            AndroidJavaClass Toast = new AndroidJavaClass("android.widget.Toast");
            AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
            activity.Call(
                "runOnUiThread",
                new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject javaString = new AndroidJavaObject(
                        "java.lang.String",
                        str.ToString()
                    );
                    AndroidJavaObject toast = Toast.CallStatic<AndroidJavaObject>(
                        "makeText",
                        context,
                        javaString,
                        Toast.GetStatic<int>("LENGTH_SHORT")
                    );
                    toast.Call("setGravity", 1, 0, 0);
                    toast.Call("show");
                })
            );
#endif
    }
}


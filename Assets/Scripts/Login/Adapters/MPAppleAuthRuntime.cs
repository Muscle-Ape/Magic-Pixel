using System;
using AppleAuth;
using AppleAuth.Native;
using UnityEngine;

/// <summary>
/// Apple Authentication SDK 的常驻运行时。
/// AppleAuthManager 的原生回调必须在 Unity Update 中主动派发，因此统一由该对象维护。
/// </summary>
public sealed class MPAppleAuthRuntime : MonoBehaviour
{
    private const string RUNTIME_OBJECT_NAME = "[MPAppleAuthRuntime]";

    private static MPAppleAuthRuntime m_instance;
    private IAppleAuthManager m_authManager;

    /// <summary>Apple 凭证在系统设置中被撤销时触发；不会透传或记录 Apple UserId。</summary>
    public static event Action CredentialsRevoked;

    /// <summary>当前运行平台是否支持原生 Sign in with Apple。</summary>
    public static bool IsCurrentPlatformSupported => AppleAuthManager.IsCurrentPlatformSupported;

    /// <summary>
    /// 获取并初始化全局 AppleAuthManager。
    /// 必须从 Unity 主线程调用，因为这里会创建 GameObject。
    /// </summary>
    public static IAppleAuthManager GetOrCreateManager()
    {
        if (!IsCurrentPlatformSupported)
        {
            return null;
        }

        if (m_instance != null && m_instance.m_authManager != null)
        {
            return m_instance.m_authManager;
        }

        GameObject runtimeObject = new GameObject(RUNTIME_OBJECT_NAME);
        DontDestroyOnLoad(runtimeObject);

        m_instance = runtimeObject.AddComponent<MPAppleAuthRuntime>();
        m_instance.m_authManager = new AppleAuthManager(new PayloadDeserializer());
        m_instance.m_authManager.SetCredentialsRevokedCallback(_ => CredentialsRevoked?.Invoke());
        return m_instance.m_authManager;
    }

    private void Update()
    {
        m_authManager?.Update();
    }

    private void OnDestroy()
    {
        if (m_instance != this)
        {
            return;
        }

        m_authManager?.SetCredentialsRevokedCallback(null);
        m_authManager = null;
        m_instance = null;
    }
}

using System;
using System.Collections;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public enum MPIDFAAuthorizationStatus
{
    Unavailable = -1,
    NotDetermined = 0,
    Restricted = 1,
    Denied = 2,
    Authorized = 3
}

/// <summary>
/// iOS ATT 权限初始化器。必须在广告 SDK 初始化前执行。
/// 非 iOS 平台直接完成，不影响启动流程。
/// </summary>
public sealed class MPLaunchIDFA
{
    private const float AUTHORIZATION_TIMEOUT_SECONDS = 30f;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int MPIDFAGetTrackingAuthorizationStatus();

    [DllImport("__Internal")]
    private static extern int MPIDFAGetTrackingRequestState();

    [DllImport("__Internal")]
    private static extern void MPIDFARequestTrackingAuthorization();
#endif

    public bool IsInitialized { get; private set; }
    public MPIDFAAuthorizationStatus AuthorizationStatus { get; private set; }
        = MPIDFAAuthorizationStatus.Unavailable;
    public bool IsAuthorized => AuthorizationStatus == MPIDFAAuthorizationStatus.Authorized;

    public IEnumerator Initialize()
    {
        if (IsInitialized)
            yield break;

#if UNITY_IOS && !UNITY_EDITOR
        // Apple 仅允许应用处于 Active 状态时弹出 ATT 授权框。
        while (!Application.isFocused)
            yield return null;

        Exception initializationException = null;
        bool requestAuthorization = false;
        try
        {
            AuthorizationStatus = ToAuthorizationStatus(MPIDFAGetTrackingAuthorizationStatus());
            if (AuthorizationStatus == MPIDFAAuthorizationStatus.NotDetermined)
            {
                Debug.Log("[MPLaunchIDFA] 正在请求 ATT 跟踪权限。");
                MPIDFARequestTrackingAuthorization();
                requestAuthorization = true;
            }
        }
        catch (Exception exception)
        {
            initializationException = exception;
        }

        // 原生层会在系统授权回调结束后更新请求状态。
        bool isRequestPending = requestAuthorization;
        float requestStartedAt = Time.realtimeSinceStartup;
        while (isRequestPending && initializationException == null)
        {
            try
            {
                isRequestPending = MPIDFAGetTrackingRequestState() == 1;
            }
            catch (Exception exception)
            {
                initializationException = exception;
            }

            if (isRequestPending && initializationException == null)
            {
                if (Time.realtimeSinceStartup - requestStartedAt >= AUTHORIZATION_TIMEOUT_SECONDS)
                {
                    Debug.LogWarning("[MPLaunchIDFA] ATT 授权等待超时，将继续使用非 IDFA 广告。");
                    break;
                }

                yield return null;
            }
        }

        if (requestAuthorization && initializationException == null)
        {
            try
            {
                AuthorizationStatus = ToAuthorizationStatus(MPIDFAGetTrackingAuthorizationStatus());
            }
            catch (Exception exception)
            {
                initializationException = exception;
            }
        }

        if (initializationException == null)
        {
            LogAuthorizationResult();
        }
        else
        {
            AuthorizationStatus = MPIDFAAuthorizationStatus.Unavailable;
            Debug.LogWarning(
                $"[MPLaunchIDFA] ATT 初始化异常，将继续使用非 IDFA 广告：{initializationException.Message}");
        }
#endif

        IsInitialized = true;
    }

#if UNITY_IOS && !UNITY_EDITOR
    private static MPIDFAAuthorizationStatus ToAuthorizationStatus(int status)
    {
        switch (status)
        {
            case 0:
                return MPIDFAAuthorizationStatus.NotDetermined;
            case 1:
                return MPIDFAAuthorizationStatus.Restricted;
            case 2:
                return MPIDFAAuthorizationStatus.Denied;
            case 3:
                return MPIDFAAuthorizationStatus.Authorized;
            default:
                return MPIDFAAuthorizationStatus.Unavailable;
        }
    }

    private void LogAuthorizationResult()
    {
        switch (AuthorizationStatus)
        {
            case MPIDFAAuthorizationStatus.Authorized:
                Debug.Log("[MPLaunchIDFA] ATT 已授权，可以使用 IDFA。");
                break;
            case MPIDFAAuthorizationStatus.Denied:
                Debug.Log("[MPLaunchIDFA] ATT 已拒绝，将继续使用非 IDFA 广告。");
                break;
            case MPIDFAAuthorizationStatus.Restricted:
                Debug.Log("[MPLaunchIDFA] ATT 受系统限制，将继续使用非 IDFA 广告。");
                break;
            case MPIDFAAuthorizationStatus.NotDetermined:
                Debug.LogWarning("[MPLaunchIDFA] ATT 授权请求未完成，将继续使用非 IDFA 广告。");
                break;
            default:
                Debug.LogWarning("[MPLaunchIDFA] 无法获取 ATT 状态，将继续使用非 IDFA 广告。");
                break;
        }
    }
#endif
}

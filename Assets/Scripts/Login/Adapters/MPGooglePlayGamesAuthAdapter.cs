using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Google Play Games 登录适配器。
/// 平台 SDK 授权由 MPGooglePlayGamesAuthService 负责，本适配器把 Auth Code 转换为统一授权结果。
/// </summary>
public class MPGooglePlayGamesAuthAdapter : MPProvidedTokenAuthAdapterBase
{
    /// <summary>
    /// 当前适配器支持 Google Play Games 登录。
    /// </summary>
    public override MPLoginType LoginType => MPLoginType.GooglePlayGames;

    protected override MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request)
    {
        MPThirdPartyAuthResult invalid = RequireToken(request.authorizationCode, "Google Play Games 登录需要 Auth Code。");
        return invalid ?? MPThirdPartyAuthResult.Success(authorizationCode: request.authorizationCode, platformUserId: request.platformUserId);
    }
}

/// <summary>
/// Google Play Games 平台授权服务。
/// 只负责调用 GPGS SDK 获取一次性 Auth Code，不直接依赖 Unity Authentication 登录实现。
/// </summary>
public static class MPGooglePlayGamesAuthService
{
    /// <summary>
    /// 当前运行平台是否支持直接拉起 Google Play Games 授权。
    /// 编辑器和非 Android 平台不能调用原生 GPGS 登录。
    /// </summary>
    public static bool IsCurrentPlatformSupported
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 当前是否已经激活过 PlayGamesPlatform。
    /// GPGS 只需要激活一次，重复激活没有必要。
    /// </summary>
    private static bool s_isActivated;

    /// <summary>
    /// 请求 Google Play Games 一次性服务端授权码。
    /// 该授权码会继续交给 Unity Authentication 的 SignInWithGooglePlayGamesAsync 或 LinkWithGooglePlayGamesAsync。
    /// </summary>
    /// <param name="forceRefreshToken">是否强制 Google 返回可换取 refresh token 的授权码。Unity Authentication 通常不需要强制刷新。</param>
    /// <param name="cancellationToken">取消令牌，页面关闭时用于取消等待。</param>
    /// <returns>统一第三方授权结果，成功时 authorizationCode 有值。</returns>
    public static Task<MPThirdPartyAuthResult> RequestAuthCodeAsync(bool forceRefreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!GameInfo.ApplicationIdInitialized())
        {
            return Task.FromResult(MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.PlatformSdkNotReady,
                "尚未配置 Google Play Games Application ID，请先在 Google Play Games Setup 中完成 Android 配置。"));
        }

        if (!GameInfo.WebClientIdInitialized())
        {
            return Task.FromResult(MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.PlatformSdkNotReady,
                "尚未配置 Google Play Games Web Client ID，请先创建 Web OAuth 客户端并同步到 Unity。"));
        }

        ActivateIfNeeded();
        TaskCompletionSource<MPThirdPartyAuthResult> completion =
            new TaskCompletionSource<MPThirdPartyAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
        });

        try
        {
            // 当前方法由用户点击登录/绑定按钮触发，必须使用交互式登录。
            // Authenticate 只执行自动登录尝试，自动登录失败后不会主动弹出账号选择界面。
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                if (status != SignInStatus.Success)
                {
                    completion.TrySetResult(CreateSignInFailure(status));
                    return;
                }

                try
                {
                    RequestServerSideAccess(forceRefreshToken, completion);
                }
                catch (Exception exception)
                {
                    completion.TrySetResult(CreateUnexpectedFailure(exception));
                }
            });
        }
        catch (Exception exception)
        {
            completion.TrySetResult(CreateUnexpectedFailure(exception));
        }

        return DisposeRegistrationWhenCompletedAsync(completion.Task, registration);
#else
        return Task.FromResult(MPThirdPartyAuthResult.Failed(
            MPLoginErrorCodes.PlatformSdkNotReady,
            "Google Play Games 登录只能在已正确配置的 Android 真机或 Android 构建中使用。"));
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 激活 Google Play Games 社交平台。
    /// </summary>
    private static void ActivateIfNeeded()
    {
        if (s_isActivated)
        {
            return;
        }

        PlayGamesPlatform.Activate();
        s_isActivated = true;
    }

    /// <summary>
    /// 在 GPGS 登录成功后请求服务端授权码。
    /// </summary>
    /// <param name="forceRefreshToken">是否强制刷新 refresh token。</param>
    /// <param name="completion">异步结果完成源。</param>
    private static void RequestServerSideAccess(bool forceRefreshToken, TaskCompletionSource<MPThirdPartyAuthResult> completion)
    {
        string platformUserId = GetPlatformUserId();
        PlayGamesPlatform.Instance.RequestServerSideAccess(forceRefreshToken, authCode =>
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(authCode))
            {
                completion.TrySetResult(MPThirdPartyAuthResult.Failed(
                    MPLoginErrorCodes.ThirdPartyAuthFailed,
                    "Google Play Games 未返回有效授权码，请检查 Web Client ID 与 Play Games 配置。"));
                return;
            }

            completion.TrySetResult(MPThirdPartyAuthResult.Success(
                authorizationCode: authCode,
                platformUserId: platformUserId));
        });
    }

    /// <summary>
    /// 获取当前 Google Play Games 玩家 Id。
    /// 该字段只用于业务透传；Unity Authentication 的身份校验仍以一次性 Auth Code 为准。
    /// </summary>
    /// <returns>有效玩家 Id；SDK 未返回时为 null。</returns>
    private static string GetPlatformUserId()
    {
        string platformUserId = PlayGamesPlatform.Instance.GetUserId();
        return string.IsNullOrWhiteSpace(platformUserId) || platformUserId == "0"
            ? null
            : platformUserId;
    }

    /// <summary>
    /// 将 GPGS 登录状态转换为登录模块统一错误。
    /// </summary>
    /// <param name="status">GPGS 登录状态。</param>
    /// <returns>失败授权结果。</returns>
    private static MPThirdPartyAuthResult CreateSignInFailure(SignInStatus status)
    {
        string errorCode = status == SignInStatus.Canceled
            ? MPLoginErrorCodes.UserCancelled
            : MPLoginErrorCodes.ThirdPartyAuthFailed;
        string message = status == SignInStatus.Canceled
            ? "已取消 Google Play Games 登录。"
            : $"Google Play Games 登录失败：{status}";

        return MPThirdPartyAuthResult.Failed(errorCode, message);
    }

    /// <summary>
    /// 将 SDK 抛出的异常转换为登录模块统一错误。
    /// </summary>
    private static MPThirdPartyAuthResult CreateUnexpectedFailure(Exception exception)
    {
        return MPThirdPartyAuthResult.Failed(
            MPLoginErrorCodes.ThirdPartyAuthFailed,
            $"Google Play Games 登录异常：{exception.Message}");
    }

    /// <summary>
    /// 等待授权结果完成后释放取消注册，避免页面多次打开时泄漏回调。
    /// </summary>
    /// <param name="task">授权任务。</param>
    /// <param name="registration">取消注册句柄。</param>
    /// <returns>授权结果。</returns>
    private static async Task<MPThirdPartyAuthResult> DisposeRegistrationWhenCompletedAsync(
        Task<MPThirdPartyAuthResult> task,
        CancellationTokenRegistration registration)
    {
        try
        {
            return await task;
        }
        finally
        {
            registration.Dispose();
        }
    }
#endif
}

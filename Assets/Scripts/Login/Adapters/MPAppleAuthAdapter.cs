using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;

/// <summary>
/// Apple 登录适配器。
/// 没有外部 Identity Token 时会拉起原生 Sign in with Apple，并把结果转换为登录模块统一结构。
/// </summary>
public sealed class MPAppleAuthAdapter : IMPThirdPartyAuthAdapter
{
    /// <summary>同一时间只允许一个系统 Apple 授权请求。</summary>
    private readonly SemaphoreSlim m_authorizationGate = new SemaphoreSlim(1, 1);

    public MPLoginType LoginType => MPLoginType.Apple;

    /// <summary>当前运行平台是否支持原生 Sign in with Apple。</summary>
    public static bool IsCurrentPlatformSupported => MPAppleAuthRuntime.IsCurrentPlatformSupported;

    public async Task<MPThirdPartyAuthResult> AuthorizeAsync(
        MPThirdPartyLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request == null)
        {
            return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.InvalidRequest, "Apple 登录请求不能为空。");
        }

        // 保留服务端、自动化测试或其他平台层已经拿到 Token 时的兼容入口。
        if (!string.IsNullOrWhiteSpace(request.identityToken))
        {
            return MPThirdPartyAuthResult.Success(
                identityToken: request.identityToken,
                authorizationCode: request.authorizationCode,
                platformUserId: request.platformUserId);
        }

        if (!IsCurrentPlatformSupported)
        {
            return MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.PlatformSdkNotReady,
                "当前设备不支持 Apple 登录，请在 iOS 13 或更高版本的真机上重试。");
        }

        await m_authorizationGate.WaitAsync(cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            m_authorizationGate.Release();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return await RequestNativeAuthorizationAsync(cancellationToken);
    }

    public Task LogoutAsync()
    {
        // Apple 不提供客户端主动注销接口；Unity Authentication 的登出由登录模块统一处理。
        return Task.CompletedTask;
    }

    private Task<MPThirdPartyAuthResult> RequestNativeAuthorizationAsync(CancellationToken cancellationToken)
    {
        IAppleAuthManager authManager = MPAppleAuthRuntime.GetOrCreateManager();
        if (authManager == null)
        {
            m_authorizationGate.Release();
            return Task.FromResult(MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.PlatformSdkNotReady,
                "Apple 登录 SDK 初始化失败，请稍后重试。"));
        }

        TaskCompletionSource<MPThirdPartyAuthResult> completion =
            new TaskCompletionSource<MPThirdPartyAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        int nativeRequestCompleted = 0;

        // 页面关闭时先结束业务层等待；原生回调返回后再释放授权门，避免同时弹出多个系统授权窗口。
        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));

        Action finishNativeRequest = () =>
        {
            if (Interlocked.Exchange(ref nativeRequestCompleted, 1) == 0)
            {
                m_authorizationGate.Release();
            }
        };

        try
        {
            // 当前登录系统只需要 Identity Token，不额外申请只会在首次授权返回的邮箱和姓名。
            AppleAuthLoginArgs loginArgs = new AppleAuthLoginArgs(LoginOptions.None);
            authManager.LoginWithAppleId(
                loginArgs,
                credential =>
                {
                    finishNativeRequest();
                    completion.TrySetResult(CreateSuccessResult(credential));
                },
                error =>
                {
                    finishNativeRequest();
                    completion.TrySetResult(CreateFailureResult(error));
                });
        }
        catch (Exception exception)
        {
            finishNativeRequest();
            completion.TrySetResult(MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.ThirdPartyAuthFailed,
                $"无法启动 Apple 登录：{exception.Message}"));
        }

        return AwaitAuthorizationAsync(completion.Task, cancellationRegistration);
    }

    private static async Task<MPThirdPartyAuthResult> AwaitAuthorizationAsync(
        Task<MPThirdPartyAuthResult> authorizationTask,
        CancellationTokenRegistration cancellationRegistration)
    {
        try
        {
            return await authorizationTask;
        }
        finally
        {
            cancellationRegistration.Dispose();
        }
    }

    private static MPThirdPartyAuthResult CreateSuccessResult(ICredential credential)
    {
        if (!(credential is IAppleIDCredential appleCredential))
        {
            return MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.ThirdPartyAuthFailed,
                "Apple 登录没有返回有效的 Apple ID 凭证。");
        }

        string identityToken = DecodeUtf8(appleCredential.IdentityToken);
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            return MPThirdPartyAuthResult.Failed(
                MPLoginErrorCodes.TokenInvalid,
                "Apple 登录没有返回有效的 Identity Token。");
        }

        return MPThirdPartyAuthResult.Success(
            identityToken: identityToken,
            authorizationCode: DecodeUtf8(appleCredential.AuthorizationCode),
            platformUserId: appleCredential.User);
    }

    private static MPThirdPartyAuthResult CreateFailureResult(IAppleError error)
    {
        AuthorizationErrorCode errorCode = error == null
            ? AuthorizationErrorCode.Unknown
            : error.GetAuthorizationErrorCode();

        if (errorCode == AuthorizationErrorCode.Canceled)
        {
            return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.UserCancelled, "已取消 Apple 登录。");
        }

        string message;
        switch (errorCode)
        {
            case AuthorizationErrorCode.InvalidResponse:
                message = "Apple 登录返回了无效响应，请重试。";
                break;
            case AuthorizationErrorCode.NotHandled:
                message = "当前设备未能处理 Apple 登录请求，请检查系统账号设置。";
                break;
            case AuthorizationErrorCode.Failed:
                message = "Apple 登录授权失败，请稍后重试。";
                break;
            default:
                message = string.IsNullOrWhiteSpace(error?.LocalizedDescription)
                    ? "Apple 登录授权失败，请稍后重试。"
                    : error.LocalizedDescription;
                break;
        }

        return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.ThirdPartyAuthFailed, message);
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        return bytes == null || bytes.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }
}

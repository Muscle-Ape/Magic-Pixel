using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Facebook 登录适配器。保留外部 Token 方式，并为平台 SDK 预留异步授权入口。
/// </summary>
public class MPFacebookAuthAdapter : IMPThirdPartyAuthAdapter
{
    // 平台接入层初始化时赋值；不保存 Token，不在 UI 内依赖未安装的 SDK。
    public static Func<CancellationToken, Task<MPThirdPartyAuthResult>> RequestAuthorizationAsync { get; set; }

    /// <summary>
    /// 当前适配器支持 Facebook 登录。
    /// </summary>
    public MPLoginType LoginType => MPLoginType.Facebook;

    public async Task<MPThirdPartyAuthResult> AuthorizeAsync(MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(request?.accessToken))
            return MPThirdPartyAuthResult.Success(accessToken: request.accessToken, platformUserId: request.platformUserId);

        var authorize = RequestAuthorizationAsync;
        if (authorize == null)
            return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.PlatformSdkNotReady,
                "Facebook sign-in is not available yet. Please choose another method.");
        MPThirdPartyAuthResult result = await authorize(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result == null || (result.success && string.IsNullOrWhiteSpace(result.accessToken)))
            return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.ThirdPartyAuthFailed, "Facebook did not return an access token.");
        return result;
    }

    public Task LogoutAsync() => Task.CompletedTask;
}

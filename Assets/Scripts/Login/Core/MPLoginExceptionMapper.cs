using System;
using Unity.Services.Authentication;
using Unity.Services.Core;

/// <summary>
/// 将 Unity Services 或系统异常转换成项目内部统一错误。
/// UI 层不需要理解各种 SDK 异常类型。
/// </summary>
public static class MPLoginExceptionMapper
{
    /// <summary>
    /// 根据异常类型生成可展示、可判断是否重试的 MPLoginError。
    /// </summary>
    public static MPLoginError Map(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return MPLoginError.Create(MPLoginErrorCodes.UserCancelled, "登录已取消。", false, 0, exception);
        }

        if (exception is TimeoutException)
        {
            return MPLoginError.Create(MPLoginErrorCodes.RequestTimeout, "请求超时，请检查网络后重试。", true, 0, exception, true);
        }

        if (exception is AuthenticationException authenticationException)
        {
            if (authenticationException.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return MPLoginError.Create(
                    MPLoginErrorCodes.AccountBindingConflict,
                    "该第三方账号已绑定到其他游戏账号，请退出当前账号后直接使用该第三方账号登录。",
                    false,
                    authenticationException.ErrorCode,
                    exception);
            }

            if (authenticationException.ErrorCode == AuthenticationErrorCodes.AccountLinkLimitExceeded)
            {
                return MPLoginError.Create(
                    MPLoginErrorCodes.AccountBindingConflict,
                    "当前游戏账号已经绑定了同类型的第三方账号。",
                    false,
                    authenticationException.ErrorCode,
                    exception);
            }

            return MPLoginError.Create(
                MPLoginErrorCodes.InvalidCredentials,
                "认证失败，请检查登录信息后重试。",
                false,
                authenticationException.ErrorCode,
                exception);
        }

        if (exception is RequestFailedException requestFailedException)
        {
            return MPLoginError.Create(
                MPLoginErrorCodes.ServerError,
                requestFailedException.Message,
                true,
                requestFailedException.ErrorCode,
                exception,
                true);
        }

        if (exception is ServicesInitializationException)
        {
            return MPLoginError.Create(
                MPLoginErrorCodes.ServerError,
                exception.Message,
                true,
                0,
                exception,
                true);
        }

        return MPLoginError.Create(
            MPLoginErrorCodes.Unknown,
            exception == null ? "登录失败，请稍后重试。" : exception.Message,
            true,
            0,
            exception);
    }
}

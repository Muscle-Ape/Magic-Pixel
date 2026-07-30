/// <summary>
/// 登录模块内部错误码集合。UI 可根据这些错误码做本地化或差异化提示。
/// </summary>
public static class MPLoginErrorCodes
{
    /// <summary>请求参数不合法。</summary>
    public const string InvalidRequest = "INVALID_REQUEST";

    /// <summary>账号、密码或凭证无效。</summary>
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    /// <summary>已有登录流程正在执行。</summary>
    public const string LoginInProgress = "LOGIN_IN_PROGRESS";

    /// <summary>本地没有可复用的登录会话。</summary>
    public const string NoLocalSession = "NO_LOCAL_SESSION";

    /// <summary>匿名恢复失败。</summary>
    public const string AnonymousRecoveryFailed = "ANONYMOUS_RECOVERY_FAILED";

    /// <summary>账号被禁用、删除或暂不可用。</summary>
    public const string AccountDisabled = "ACCOUNT_DISABLED";

    /// <summary>账号绑定冲突。</summary>
    public const string AccountBindingConflict = "ACCOUNT_BINDING_CONFLICT";

    /// <summary>第三方平台 SDK 尚未接入或未初始化。</summary>
    public const string PlatformSdkNotReady = "PLATFORM_SDK_NOT_READY";

    /// <summary>用户取消登录。</summary>
    public const string UserCancelled = "USER_CANCELLED";

    /// <summary>网络不可用。</summary>
    public const string NetworkUnavailable = "NETWORK_UNAVAILABLE";

    /// <summary>请求超时。</summary>
    public const string RequestTimeout = "REQUEST_TIMEOUT";

    /// <summary>服务端或 Unity Services 返回错误。</summary>
    public const string ServerError = "SERVER_ERROR";

    /// <summary>服务维护中。</summary>
    public const string Maintenance = "MAINTENANCE";

    /// <summary>Token 已过期。</summary>
    public const string TokenExpired = "TOKEN_EXPIRED";

    /// <summary>Token 无效。</summary>
    public const string TokenInvalid = "TOKEN_INVALID";

    /// <summary>本地 Session 已确认失效。</summary>
    public const string SessionInvalid = "SESSION_INVALID";

    /// <summary>第三方平台授权失败。</summary>
    public const string ThirdPartyAuthFailed = "THIRD_PARTY_AUTH_FAILED";

    /// <summary>当前登录方式未支持或未注册。</summary>
    public const string UnsupportedLoginType = "UNSUPPORTED_LOGIN_TYPE";

    /// <summary>未知错误。</summary>
    public const string Unknown = "UNKNOWN";
}

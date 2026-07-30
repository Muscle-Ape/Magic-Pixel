/// <summary>
/// 登录模块状态。UI 可以监听 StateChanged 根据状态切换按钮、loading 和错误提示。
/// </summary>
public enum MPLoginState
{
    /// <summary>尚未初始化。</summary>
    Uninitialized = 0,
    /// <summary>未初始化别名，兼容旧代码。</summary>
    None = Uninitialized,
    /// <summary>未登录。</summary>
    LoggedOut = 1,
    /// <summary>初始化 Unity Services 中。</summary>
    Initializing = 2,
    /// <summary>认证中。</summary>
    Authenticating = 3,
    /// <summary>认证中别名，兼容旧代码。</summary>
    LoggingIn = Authenticating,
    /// <summary>已登录并拥有可用 Session。</summary>
    Authenticated = 4,
    /// <summary>已登录别名，兼容旧代码。</summary>
    LoggedIn = Authenticated,
    /// <summary>刷新认证状态中。</summary>
    RefreshingToken = 5,
    /// <summary>刷新会话中，兼容技术文档中的命名。</summary>
    RefreshingSession = RefreshingToken,
    /// <summary>加载玩家业务数据中，预留给后续接入云存档或服务器数据。</summary>
    LoadingUserData = 6,
    /// <summary>登出中。</summary>
    LoggingOut = 7,
    /// <summary>登录或绑定失败。</summary>
    Failed = 8,
    /// <summary>检查本地登录资料中。</summary>
    CheckingLocalSession = 9,
    /// <summary>恢复已有会话中。</summary>
    RestoringSession = 10,
    /// <summary>执行匿名登录中。</summary>
    LoggingInAnonymously = 11,
    /// <summary>等待玩家选择登录方式。</summary>
    WaitingForLoginSelection = 12,
    /// <summary>执行第三方认证中。</summary>
    AuthenticatingThirdParty = 13,
    /// <summary>绑定第三方或账号密码身份中。</summary>
    BindingIdentity = 14,
    /// <summary>处理账号冲突中。</summary>
    ResolvingAccountConflict = 15,
    /// <summary>网络或服务临时不可用，本地资料应保留。</summary>
    TemporaryUnavailable = 16
}

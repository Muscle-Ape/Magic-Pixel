using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 登录模块核心能力接口。
/// Facade 依赖此接口，方便后续替换实现或在测试中注入 Mock。
/// </summary>
public interface IMPLoginManager
{
    /// <summary>
    /// 当前登录模块状态。
    /// </summary>
    MPLoginState State { get; }

    /// <summary>
    /// 当前登录会话，未登录时为 null。
    /// </summary>
    MPUserSession CurrentSession { get; }

    /// <summary>
    /// 最近一次失败的错误信息。
    /// </summary>
    MPLoginError LastError { get; }

    /// <summary>
    /// 登录状态变化事件。
    /// </summary>
    event Action<MPLoginState> StateChanged;

    /// <summary>
    /// 登录或绑定成功事件。
    /// </summary>
    event Action<MPUserSession> LoginSucceeded;

    /// <summary>
    /// 登录、绑定或刷新失败事件。
    /// </summary>
    event Action<MPLoginError> LoginFailed;

    /// <summary>
    /// 认证状态刷新成功事件。
    /// </summary>
    event Action TokenRefreshed;

    /// <summary>
    /// 会话失效事件。
    /// </summary>
    event Action SessionExpired;

    /// <summary>
    /// 登出完成事件。
    /// </summary>
    event Action LoggedOut;

    /// <summary>
    /// 执行指定登录方式的登录流程。
    /// </summary>
    Task<MPLoginResult> LoginAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试复用本地会话执行自动登录。
    /// </summary>
    Task<MPLoginResult> AutoLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新当前认证状态。
    /// </summary>
    Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新拉取玩家信息并刷新本地 Session。
    /// </summary>
    Task<bool> RefreshPlayerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 给当前账号绑定新的登录方式，或执行账号密码改密。
    /// </summary>
    Task<MPLoginResult> LinkAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 登出当前账号。
    /// </summary>
    Task LogoutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 切换 Unity Authentication Profile。
    /// </summary>
    bool SwitchProfile(string profile);

    /// <summary>
    /// 清理当前 Profile 的本地 SessionToken。
    /// </summary>
    bool ClearSessionToken();
}

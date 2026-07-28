using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 认证服务 API 抽象层。
/// 当前实现是 Unity Authentication，核心流程不直接依赖 Unity SDK。
/// </summary>
public interface IMPAuthApi
{
    /// <summary>
    /// 底层认证服务当前是否已登录。
    /// </summary>
    bool IsSignedIn { get; }

    /// <summary>
    /// 底层认证服务当前是否拥有有效授权。
    /// </summary>
    bool IsAuthorized { get; }

    /// <summary>
    /// 当前 Profile 是否存在可复用的本地 SessionToken。
    /// </summary>
    bool SessionTokenExists { get; }

    /// <summary>
    /// 初始化底层认证服务。
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行游客/匿名登录。
    /// </summary>
    Task<MPUserSession> SignInAnonymouslyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用用户名和密码登录。
    /// </summary>
    Task<MPUserSession> SignInWithUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用用户名和密码注册。
    /// </summary>
    Task<MPUserSession> SignUpWithUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用第三方授权结果登录。
    /// </summary>
    Task<MPUserSession> SignInWithThirdPartyAsync(MPLoginType loginType, MPThirdPartyAuthResult authResult, bool createAccount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 给当前账号添加用户名和密码登录方式。
    /// </summary>
    Task<MPUserSession> LinkUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// 给当前账号绑定第三方登录方式。
    /// </summary>
    Task<MPUserSession> LinkThirdPartyAsync(MPLoginType loginType, MPThirdPartyAuthResult authResult, bool forceLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// 修改当前账号密码用户的密码。
    /// </summary>
    Task<MPUserSession> UpdatePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取底层认证状态并转换为项目 Session。
    /// </summary>
    Task<MPUserSession> GetCurrentSessionAsync(MPLoginType loginType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 登出底层认证服务。
    /// </summary>
    Task SignOutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 切换底层认证 Profile。
    /// </summary>
    bool SwitchProfile(string profile);

    /// <summary>
    /// 清理底层认证本地 SessionToken。
    /// </summary>
    bool ClearSessionToken();
}

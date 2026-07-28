using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 登录模块对外 Facade。外部 UI、启动流程和业务代码优先只依赖这个类。
/// 内部具体登录流程由 MPLoginManagerCore、Strategy、Adapter 和 AuthApi 分层处理。
/// </summary>
public class MPLoginManager
{
    /// <summary>
    /// 登录模块单例实例。
    /// </summary>
    private static MPLoginManager m_instance;

    /// <summary>
    /// 内部登录管理器实现，Facade 会把所有实际工作委托给它。
    /// </summary>
    private readonly IMPLoginManager m_inner;

    /// <summary>
    /// 当前正在执行的游客登录任务，用于合并并发游客登录请求。
    /// </summary>
    private Task<bool> m_guestLoginTask;

    private MPLoginManager()
    {
        m_inner = MPLoginCompositionRoot.CreateDefault();
    }

    public static MPLoginManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPLoginManager();
            }

            return m_instance;
        }
    }

    /// <summary>
    /// 当前登录状态，兼容旧代码中的 LoginState 命名。
    /// </summary>
    public MPLoginState LoginState => m_inner.State;

    /// <summary>
    /// 当前登录状态。
    /// </summary>
    public MPLoginState State => m_inner.State;

    /// <summary>
    /// 当前完整登录会话；未登录时为 null。
    /// </summary>
    public MPUserSession CurrentSession => m_inner.CurrentSession;

    /// <summary>
    /// 供旧 UI 使用的轻量用户信息。
    /// </summary>
    public MPLoginUserInfo UserInfo => MPLoginUserInfo.FromSession(m_inner.CurrentSession);

    /// <summary>
    /// 当前 Unity Authentication PlayerId。
    /// </summary>
    public string PlayerId => m_inner.CurrentSession == null ? string.Empty : m_inner.CurrentSession.userId;

    /// <summary>
    /// 当前玩家名。
    /// </summary>
    public string PlayerName => m_inner.CurrentSession == null ? string.Empty : m_inner.CurrentSession.playerName;

    /// <summary>
    /// 当前账号密码用户名。
    /// </summary>
    public string Username => m_inner.CurrentSession == null ? string.Empty : m_inner.CurrentSession.username;

    /// <summary>
    /// 当前 Unity Authentication Profile。
    /// </summary>
    public string CurrentProfile => m_inner.CurrentSession == null ? string.Empty : m_inner.CurrentSession.profile;

    /// <summary>
    /// 最近一次登录错误消息。
    /// </summary>
    public string LastError => m_inner.LastError == null ? string.Empty : m_inner.LastError.message;

    /// <summary>
    /// 最近一次 Unity Services 或底层服务错误码。
    /// </summary>
    public int LastErrorCode => m_inner.LastError == null ? 0 : m_inner.LastError.serviceErrorCode;

    /// <summary>
    /// 当前是否已经登录成功。
    /// </summary>
    public bool IsLoggedIn => m_inner.State == MPLoginState.Authenticated;

    /// <summary>
    /// 登录状态变化事件。
    /// </summary>
    public event Action<MPLoginState> StateChanged
    {
        add => m_inner.StateChanged += value;
        remove => m_inner.StateChanged -= value;
    }

    /// <summary>
    /// 登录成功事件，参数为最新 Session。
    /// </summary>
    public event Action<MPUserSession> LoginSucceeded
    {
        add => m_inner.LoginSucceeded += value;
        remove => m_inner.LoginSucceeded -= value;
    }

    /// <summary>
    /// 登录失败事件，参数为统一错误信息。
    /// </summary>
    public event Action<MPLoginError> LoginFailed
    {
        add => m_inner.LoginFailed += value;
        remove => m_inner.LoginFailed -= value;
    }

    /// <summary>
    /// 登出完成事件。
    /// </summary>
    public event Action LoggedOut
    {
        add => m_inner.LoggedOut += value;
        remove => m_inner.LoggedOut -= value;
    }

    /// <summary>
    /// 兼容旧启动流程的协程初始化入口，目前默认走游客/匿名登录。
    /// </summary>
    public IEnumerator Initialize()
    {
        Task<bool> task = LoginAsGuestAsync();
        while (!task.IsCompleted)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 通用登录入口，适合登录页按选择的登录方式统一调用。
    /// </summary>
    public Task<MPLoginResult> LoginAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        return m_inner.LoginAsync(loginType, request, cancellationToken);
    }

    /// <summary>
    /// 尝试复用本地 Unity Authentication 会话；没有本地会话时不会强行创建新账号。
    /// </summary>
    public Task<MPLoginResult> AutoLoginAsync(CancellationToken cancellationToken = default)
    {
        return m_inner.AutoLoginAsync(cancellationToken);
    }

    /// <summary>
    /// 默认游客登录入口。并发调用时复用同一个登录任务，避免重复请求。
    /// </summary>
    public Task<bool> LoginAsGuestAsync()
    {
        if (m_guestLoginTask != null && !m_guestLoginTask.IsCompleted)
        {
            return m_guestLoginTask;
        }

        m_guestLoginTask = LoginAsGuestInternalAsync();
        return m_guestLoginTask;
    }

    /// <summary>
    /// 匿名登录，当前阶段作为启动时的默认登录方式。
    /// </summary>
    public Task<MPLoginResult> LoginAnonymouslyAsync()
    {
        return m_inner.LoginAsync(MPLoginType.Guest, new MPGuestLoginRequest());
    }

    /// <summary>
    /// Unity Authentication 自带账号密码登录。
    /// </summary>
    public Task<MPLoginResult> LoginWithUsernamePasswordAsync(string username, string password)
    {
        return m_inner.LoginAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
        {
            account = username,
            password = password,
            mode = MPPasswordLoginMode.Login
        });
    }

    /// <summary>
    /// Unity Authentication 自带账号密码注册。
    /// </summary>
    public Task<MPLoginResult> RegisterWithUsernamePasswordAsync(string username, string password)
    {
        return m_inner.LoginAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
        {
            account = username,
            password = password,
            mode = MPPasswordLoginMode.Register
        });
    }

    public Task<MPLoginResult> SignUpWithUsernamePasswordAsync(string username, string password)
    {
        return RegisterWithUsernamePasswordAsync(username, password);
    }

    /// <summary>
    /// 给当前已登录游客账号绑定用户名和密码。
    /// </summary>
    public Task<MPLoginResult> AddUsernamePasswordAsync(string username, string password)
    {
        return m_inner.LinkAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
        {
            account = username,
            password = password,
            mode = MPPasswordLoginMode.AddToCurrentUser
        });
    }

    /// <summary>
    /// 使用 Google Identity Token 登录。Token 获取由外部 Google SDK 或平台层负责。
    /// </summary>
    public Task<MPLoginResult> LoginWithGoogleAsync(string idToken, bool createAccount = true)
    {
        return LoginWithThirdPartyAsync(MPLoginType.Google, identityToken: idToken, createAccount: createAccount);
    }

    /// <summary>
    /// 使用 Google Play Games Auth Code 登录。Auth Code 获取由外部 Google Play Games SDK 负责。
    /// </summary>
    public Task<MPLoginResult> LoginWithGooglePlayGamesAsync(string authCode, bool createAccount = true)
    {
        return LoginWithThirdPartyAsync(MPLoginType.GooglePlayGames, authorizationCode: authCode, createAccount: createAccount);
    }

    /// <summary>
    /// 使用 Apple Identity Token 登录。
    /// </summary>
    public Task<MPLoginResult> LoginWithAppleAsync(string idToken, bool createAccount = true)
    {
        return LoginWithThirdPartyAsync(MPLoginType.Apple, identityToken: idToken, createAccount: createAccount);
    }

    /// <summary>
    /// 使用 Facebook Access Token 登录。
    /// </summary>
    public Task<MPLoginResult> LoginWithFacebookAsync(string accessToken, bool createAccount = true)
    {
        return LoginWithThirdPartyAsync(MPLoginType.Facebook, accessToken: accessToken, createAccount: createAccount);
    }

    /// <summary>
    /// 给当前账号绑定 Google 登录方式。
    /// </summary>
    public Task<MPLoginResult> LinkGoogleAsync(string idToken, bool forceLink = false)
    {
        return LinkThirdPartyAsync(MPLoginType.Google, identityToken: idToken, forceLink: forceLink);
    }

    /// <summary>
    /// 给当前账号绑定 Google Play Games 登录方式。
    /// </summary>
    public Task<MPLoginResult> LinkGooglePlayGamesAsync(string authCode, bool forceLink = false)
    {
        return LinkThirdPartyAsync(MPLoginType.GooglePlayGames, authorizationCode: authCode, forceLink: forceLink);
    }

    /// <summary>
    /// 给当前账号绑定 Apple 登录方式。
    /// </summary>
    public Task<MPLoginResult> LinkAppleAsync(string idToken, bool forceLink = false)
    {
        return LinkThirdPartyAsync(MPLoginType.Apple, identityToken: idToken, forceLink: forceLink);
    }

    /// <summary>
    /// 给当前账号绑定 Facebook 登录方式。
    /// </summary>
    public Task<MPLoginResult> LinkFacebookAsync(string accessToken, bool forceLink = false)
    {
        return LinkThirdPartyAsync(MPLoginType.Facebook, accessToken: accessToken, forceLink: forceLink);
    }

    /// <summary>
    /// 修改 Unity Authentication 账号密码用户的密码。
    /// </summary>
    public Task<MPLoginResult> UpdatePasswordAsync(string currentPassword, string newPassword)
    {
        return m_inner.LinkAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
        {
            currentPassword = currentPassword,
            password = newPassword,
            mode = MPPasswordLoginMode.UpdatePassword
        });
    }

    /// <summary>
    /// 刷新当前认证状态；当本地会话失效时会触发登出状态。
    /// </summary>
    public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        return m_inner.RefreshTokenAsync(cancellationToken);
    }

    /// <summary>
    /// 从 Unity Authentication 重新拉取玩家信息并刷新本地 Session。
    /// </summary>
    public Task<bool> RefreshPlayerInfoAsync(CancellationToken cancellationToken = default)
    {
        return m_inner.RefreshPlayerInfoAsync(cancellationToken);
    }

    /// <summary>
    /// 登出。clearCredentials 为 true 时会清理 Unity Authentication 本地凭证。
    /// </summary>
    public Task LogoutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default)
    {
        return m_inner.LogoutAsync(clearCredentials, cancellationToken);
    }

    public void SignOut(bool clearCredentials = false)
    {
        _ = LogoutAsync(clearCredentials);
    }

    public bool SwitchProfile(string profile)
    {
        return m_inner.SwitchProfile(profile);
    }

    public bool ClearSessionToken()
    {
        return m_inner.ClearSessionToken();
    }

    private async Task<bool> LoginAsGuestInternalAsync()
    {
        MPLoginResult result = await LoginAnonymouslyAsync();
        return result.isSuccess;
    }

    /// <summary>
    /// 将第三方登录参数统一包装成请求对象，具体校验和登录由 Adapter/Strategy 完成。
    /// </summary>
    private Task<MPLoginResult> LoginWithThirdPartyAsync(
        MPLoginType provider,
        string authorizationCode = null,
        string accessToken = null,
        string identityToken = null,
        string platformUserId = null,
        bool createAccount = true)
    {
        return m_inner.LoginAsync(provider, new MPThirdPartyLoginRequest
        {
            loginType = provider,
            provider = provider,
            authorizationCode = authorizationCode,
            accessToken = accessToken,
            identityToken = identityToken,
            platformUserId = platformUserId,
            createAccount = createAccount
        });
    }

    /// <summary>
    /// 将第三方绑定参数统一包装成请求对象，方便后续替换具体平台 SDK。
    /// </summary>
    private Task<MPLoginResult> LinkThirdPartyAsync(
        MPLoginType provider,
        string authorizationCode = null,
        string accessToken = null,
        string identityToken = null,
        string platformUserId = null,
        bool forceLink = false)
    {
        return m_inner.LinkAsync(provider, new MPThirdPartyLoginRequest
        {
            loginType = provider,
            provider = provider,
            authorizationCode = authorizationCode,
            accessToken = accessToken,
            identityToken = identityToken,
            platformUserId = platformUserId,
            forceLink = forceLink
        });
    }
}

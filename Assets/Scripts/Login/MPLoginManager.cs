using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 登录模块对外 Facade。外部 UI、启动流程和业务代码优先只依赖这个类。
/// 内部具体登录流程由 MPLoginManagerCore、Strategy、Adapter 和 AuthApi 分层处理。
/// </summary>
public class MPLoginManager
{
    /// <summary>
    /// 游客登录使用的 Unity Authentication Profile 名称。
    /// </summary>
    private const string GUEST_PROFILE = "guest";

    /// <summary>
    /// 登录模块单例实例。
    /// </summary>
    private static MPLoginManager m_instance;

    /// <summary>
    /// 内部登录管理器实现，Facade 会把所有实际工作委托给它。
    /// </summary>
    private readonly IMPLoginManager m_inner;

    /// <summary>
    /// 登录启动与恢复流程控制器。
    /// </summary>
    private readonly IMPLoginFlowController m_flowController;

    /// <summary>
    /// 本地登录资料仓储。
    /// </summary>
    private readonly IMPLocalLoginRepository m_localLoginRepository;

    /// <summary>
    /// 登录模块配置。
    /// </summary>
    private readonly MPLoginConfiguration m_configuration;

    /// <summary>
    /// 当前正在执行的游客登录任务，用于合并并发游客登录请求。
    /// </summary>
    private Task<bool> m_guestLoginTask;
    private bool m_loginFlowRunning;
    public bool IsLoginFlowRunning => m_loginFlowRunning;

    private MPLoginManager()
    {
        MPLoginServiceContainer services = MPLoginCompositionRoot.CreateDefaultServices();
        m_inner = services.loginManager;
        m_flowController = services.flowController;
        m_localLoginRepository = services.localLoginRepository;
        m_configuration = services.configuration;

        if (m_configuration.EnableAppleLogin && MPAppleAuthAdapter.IsCurrentPlatformSupported)
        {
            MPAppleAuthRuntime.CredentialsRevoked += OnAppleCredentialsRevoked;
            MPAppleAuthRuntime.GetOrCreateManager();
        }
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
    public MPLoginState LoginState => State;

    /// <summary>
    /// 当前登录状态。
    /// </summary>
    public MPLoginState State => MPAccountConflictService.IsResolving ? MPLoginState.ResolvingAccountConflict : m_inner.State;

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
    public string PlayerName
    {
        get
        {
            string displayName = MPUser.instance.GetProfileName();
            return !string.IsNullOrWhiteSpace(displayName) ? displayName : m_inner.CurrentSession?.playerName ?? string.Empty;
        }
    }

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
    /// 当前登录模块配置。
    /// </summary>
    public MPLoginConfiguration Configuration => m_configuration;

    /// <summary>
    /// 最近一次启动登录流程的结果。
    /// </summary>
    public MPLoginStartupResult LastStartupResult { get; private set; }

    /// <summary>
    /// 启动流程控制器当前状态。
    /// </summary>
    public MPLoginState FlowState => m_flowController.State;

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
    /// 启动流程状态变化事件。
    /// </summary>
    public event Action<MPLoginState> FlowStateChanged
    {
        add => m_flowController.StateChanged += value;
        remove => m_flowController.StateChanged -= value;
    }

    /// <summary>
    /// 兼容旧启动流程的协程初始化入口。
    /// 新流程会先尝试恢复历史会话，只有确认首次安装时才自动创建游客账号。
    /// </summary>
    public IEnumerator Initialize()
    {
        Task<MPLoginStartupResult> task = StartLoginFlowAsync();
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogError($"[MPLogin] 启动登录流程异常：{task.Exception}");
            LastStartupResult = MPLoginStartupResult.Failed(MPLoginError.Create(MPLoginErrorCodes.Unknown, "启动登录流程异常。", true, 0, task.Exception, true));
        }
    }

    /// <summary>
    /// 通用登录入口，适合登录页按选择的登录方式统一调用。
    /// </summary>
    public async Task<MPLoginResult> LoginAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        MPLoginResult result = await m_inner.LoginAsync(loginType, request, cancellationToken);
        await SaveLoginResultAsync(result, loginType, false, cancellationToken);
        return result;
    }

    /// <summary>
    /// 尝试复用本地 Unity Authentication 会话；没有本地会话时不会强行创建新账号。
    /// </summary>
    public async Task<MPLoginResult> AutoLoginAsync(CancellationToken cancellationToken = default)
    {
        MPLocalLoginProfile profile = await m_localLoginRepository.LoadAsync(cancellationToken);
        PrepareProfileForSessionRestore(profile);

        MPLoginResult result = await m_inner.AutoLoginAsync(cancellationToken);
        await SaveLoginResultAsync(result, result == null ? MPLoginType.None : result.loginType, false, cancellationToken);
        return result;
    }

    /// <summary>
    /// 给当前账号绑定新的登录方式，或执行账号密码改密。
    /// </summary>
    public async Task<MPLoginResult> LinkAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        MPLoginResult result = await m_inner.LinkAsync(loginType, request, cancellationToken);
        await SaveLoginResultAsync(result, loginType, true, cancellationToken);
        return result;
    }

    /// <summary>
    /// 执行启动登录策略，返回明确的下一步动作。
    /// </summary>
    public Task<MPLoginStartupResult> StartLoginFlowAsync(CancellationToken cancellationToken = default)
    {
        return RunLoginFlowAsync(m_flowController.StartAsync, false, cancellationToken);
    }

    /// <summary>
    /// 重试启动登录策略，通常由网络重试页面调用。
    /// </summary>
    public Task<MPLoginStartupResult> RetryStartupAsync(CancellationToken cancellationToken = default)
    {
        return StartLoginFlowAsync(cancellationToken);
    }

    /// <summary>
    /// 用户明确选择创建新的游客账号。
    /// </summary>
    public Task<MPLoginStartupResult> ContinueAsNewGuestAsync(CancellationToken cancellationToken = default)
    {
        return RunLoginFlowAsync(m_flowController.ContinueAsNewGuestAsync, true, cancellationToken);
    }

    public async Task<MPLoginStartupResult> ContinueAsGuestAsync(CancellationToken cancellationToken = default)
    {
        MPLocalLoginProfile current = await m_localLoginRepository.LoadAsync(cancellationToken);
        MPLocalLoginProfile guest = await m_localLoginRepository.LoadGuestProfileAsync(cancellationToken);
        bool sameGuest = current?.IsIndependentGuest == true &&
            (guest == null || (guest.IsIndependentGuest && guest.playerId == current.playerId));
        // 原游客断网后重试不属于切号；必须允许先恢复它，才能继续同步它的 dirty 数据。
        return await RunLoginFlowAsync(m_flowController.ContinueAsGuestAsync, !sameGuest, cancellationToken);
    }

    /// <summary>
    /// 使用第三方登录提供方继续登录或恢复账号。
    /// </summary>
    public async Task<MPLoginStartupResult> LoginWithProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        MPLocalLoginProfile previous = await m_localLoginRepository.LoadAsync(cancellationToken);
        string reauthenticatePlayerId = CanReauthenticate(previous, loginType) ? previous.playerId : null;
        MPThirdPartyLoginRequest safeRequest = request ?? new MPThirdPartyLoginRequest();
        return await RunLoginFlowAsync(token =>
        {
            safeRequest.expectedPlayerId = MPCloudSaveManager.Instance.RequiredPlayerIdForAccountSwitch;
            return m_flowController.LoginWithProviderAsync(loginType, safeRequest, token);
        }, true, cancellationToken, reauthenticatePlayerId);
    }

    private static bool CanReauthenticate(MPLocalLoginProfile profile, MPLoginType loginType)
    {
        if (profile == null || string.IsNullOrEmpty(profile.playerId)) return false;
        if (profile.lastLoginProvider == ToProvider(loginType)) return true;
        switch (loginType)
        {
            case MPLoginType.Apple: return profile.hasAppleBinding;
            case MPLoginType.Google: return profile.hasGoogleBinding;
            case MPLoginType.GooglePlayGames: return profile.hasGooglePlayGamesBinding;
            case MPLoginType.Facebook: return profile.hasFacebookBinding;
            case MPLoginType.UsernamePassword: return profile.hasUsernamePasswordBinding;
            default: return false;
        }
    }

    private async Task<MPLoginStartupResult> RunLoginFlowAsync(
        Func<CancellationToken, Task<MPLoginStartupResult>> operation, bool switchingAccount, CancellationToken cancellationToken,
        string reauthenticatePlayerId = null)
    {
        if (m_loginFlowRunning)
            return MPLoginStartupResult.Failed(MPLoginError.Create(MPLoginErrorCodes.LoginInProgress, "登录正在进行，请稍候。"));
        m_loginFlowRunning = true;
        IDisposable switchGuard = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (switchingAccount)
            {
                switchGuard = await MPCloudSaveManager.Instance.BeginAccountSwitchAsync(cancellationToken, reauthenticatePlayerId);
                if (switchGuard == null)
                    return LastStartupResult = MPLoginStartupResult.Failed(MPLoginError.Create(MPLoginErrorCodes.ServerError,
                        "原账号还有未同步的数据，请先恢复原账号并完成同步，再切换登录。", true),
                        await m_localLoginRepository.LoadAsync(cancellationToken));
            }
            LastStartupResult = await operation(cancellationToken);
            return LastStartupResult;
        }
        finally
        {
            switchGuard?.Dispose();
            m_loginFlowRunning = false;
        }
    }

    /// <summary>
    /// 给当前已登录账号绑定第三方登录提供方。
    /// </summary>
    public Task<MPLoginResult> BindProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        return m_flowController.BindProviderAsync(loginType, request, cancellationToken);
    }

    /// <summary>
    /// 读取当前本地登录资料。
    /// </summary>
    public Task<MPLocalLoginProfile> LoadLocalProfileAsync(CancellationToken cancellationToken = default)
    {
        return m_localLoginRepository.LoadAsync(cancellationToken);
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
        return LoginAsync(MPLoginType.Guest, new MPGuestLoginRequest());
    }

    /// <summary>
    /// Unity Authentication 自带账号密码登录。
    /// </summary>
    public Task<MPLoginResult> LoginWithUsernamePasswordAsync(string username, string password)
    {
        return LoginAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
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
        return LoginAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
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
        return LinkAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
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
        return LinkAsync(MPLoginType.UsernamePassword, new MPPasswordLoginRequest
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
    public async Task LogoutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default)
    {
        await m_inner.LogoutAsync(clearCredentials, cancellationToken);
        if (clearCredentials)
            await m_localLoginRepository.ClearActiveSessionAsync(keepRecoveryData: true, cancellationToken);
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

    /// <summary>
    /// Apple 凭证被系统撤销后，如果当前会话由 Apple 登录恢复，则清理本地认证状态并返回登录态。
    /// 已通过其他身份登录的账号不会仅因 Apple 绑定失效而被强制退出。
    /// </summary>
    private async void OnAppleCredentialsRevoked()
    {
        try
        {
            if (!IsLoggedIn)
            {
                return;
            }

            MPLocalLoginProfile profile = await m_localLoginRepository.LoadAsync();
            bool isAppleSession = CurrentSession != null && CurrentSession.loginType == MPLoginType.Apple;
            bool restoredFromApple = profile != null && profile.lastLoginProvider == MPLoginProvider.Apple;
            if (!isAppleSession && !restoredFromApple)
            {
                return;
            }

            Debug.LogWarning("[MPLogin] Apple 登录授权已被撤销，正在清理当前登录凭证。");
            await LogoutAsync(clearCredentials: true);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPLogin] 处理 Apple 凭证撤销事件失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 登录或绑定成功后刷新本地登录资料。
    /// </summary>
    private async Task SaveLoginResultAsync(MPLoginResult result, MPLoginType loginType, bool markAsBound, CancellationToken cancellationToken)
    {
        if (result == null || !result.isSuccess)
        {
            return;
        }

        MPLocalLoginProfile profile = await m_localLoginRepository.LoadAsync(cancellationToken) ?? new MPLocalLoginProfile();
        if (string.IsNullOrEmpty(profile.installationId))
        {
            profile.installationId = await m_localLoginRepository.GetOrCreateInstallationIdAsync(cancellationToken);
        }

        MPLoginProvider provider = ToProvider(loginType);
        if (provider == MPLoginProvider.Anonymous && string.IsNullOrEmpty(profile.anonymousId))
        {
            profile.anonymousId = await m_localLoginRepository.GetOrCreateAnonymousIdAsync(cancellationToken);
            profile.anonymousIdempotencyKey = await m_localLoginRepository.GetOrCreateAnonymousIdempotencyKeyAsync(cancellationToken);
        }

        bool shouldMarkAsBound = markAsBound || IsBoundProvider(provider) ||
            (profile.playerId == result.playerId && profile.hasBoundIdentity);
        profile.ApplySession(result.session, provider, shouldMarkAsBound);
        await m_localLoginRepository.SaveAsync(profile, cancellationToken);
    }

    /// <summary>
    /// 自动登录前切回本地资料记录的 Unity Authentication Profile。
    /// Unity Authentication 的本地 SessionToken 按 Profile 隔离保存，游客凭证通常在 guest Profile 下。
    /// </summary>
    private void PrepareProfileForSessionRestore(MPLocalLoginProfile profile)
    {
        if (profile == null || m_inner.CurrentSession != null)
        {
            return;
        }

        string unityProfile = ResolveUnityProfileForRestore(profile);
        if (string.IsNullOrEmpty(unityProfile))
        {
            return;
        }

        if (!m_inner.SwitchProfile(unityProfile))
        {
            Debug.LogWarning($"[MPLogin] 切换 Unity Authentication Profile 失败，可能会影响本地会话恢复：{unityProfile}");
        }
    }

    /// <summary>
    /// 从本地资料推断自动恢复应使用的 Unity Authentication Profile。
    /// </summary>
    private static string ResolveUnityProfileForRestore(MPLocalLoginProfile profile)
    {
        if (profile == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(profile.unityProfile))
        {
            return profile.unityProfile;
        }

        if (profile.accountType == MPAccountType.Anonymous ||
            profile.lastLoginProvider == MPLoginProvider.Anonymous)
        {
            return GUEST_PROFILE;
        }

        return string.Empty;
    }

    /// <summary>
    /// 登录类型转本地登录提供方。
    /// </summary>
    private static MPLoginProvider ToProvider(MPLoginType loginType)
    {
        switch (loginType)
        {
            case MPLoginType.Guest:
                return MPLoginProvider.Anonymous;
            case MPLoginType.UsernamePassword:
                return MPLoginProvider.UsernamePassword;
            case MPLoginType.Google:
                return MPLoginProvider.Google;
            case MPLoginType.GooglePlayGames:
                return MPLoginProvider.GooglePlayGames;
            case MPLoginType.Apple:
                return MPLoginProvider.Apple;
            case MPLoginType.Facebook:
                return MPLoginProvider.Facebook;
            default:
                return MPLoginProvider.Unknown;
        }
    }

    /// <summary>
    /// 判断提供方是否属于正式账号身份。
    /// </summary>
    private static bool IsBoundProvider(MPLoginProvider provider)
    {
        return provider == MPLoginProvider.UsernamePassword ||
               provider == MPLoginProvider.Google ||
               provider == MPLoginProvider.GooglePlayGames ||
               provider == MPLoginProvider.Apple ||
               provider == MPLoginProvider.Facebook;
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
        return LoginAsync(provider, new MPThirdPartyLoginRequest
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
        return LinkAsync(provider, new MPThirdPartyLoginRequest
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

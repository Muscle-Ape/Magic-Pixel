using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 登录启动策略控制器。
/// 它负责根据本地资料、安装状态和登录结果决定下一步动作，不直接关心具体 UI 展示。
/// </summary>
public class MPLoginFlowController : IMPLoginFlowController
{
    /// <summary>旧游客资料未记录 Profile 时使用的兼容名称。</summary>
    private const string GUEST_PROFILE = "guest";

    /// <summary>核心登录管理器。</summary>
    private readonly IMPLoginManager m_loginManager;

    /// <summary>本地登录资料仓储。</summary>
    private readonly IMPLocalLoginRepository m_localLoginRepository;

    /// <summary>安装状态服务。</summary>
    private readonly IMPInstallationService m_installationService;

    /// <summary>登录模块配置。</summary>
    private readonly MPLoginConfiguration m_configuration;

    public MPLoginFlowController(
        IMPLoginManager loginManager,
        IMPLocalLoginRepository localLoginRepository,
        IMPInstallationService installationService,
        MPLoginConfiguration configuration)
    {
        m_loginManager = loginManager;
        m_localLoginRepository = localLoginRepository;
        m_installationService = installationService;
        m_configuration = configuration;
        State = MPLoginState.Uninitialized;
    }

    /// <summary>当前启动流程状态。</summary>
    public MPLoginState State { get; private set; }

    /// <summary>启动流程状态变化事件。</summary>
    public event Action<MPLoginState> StateChanged;

    public async Task<MPLoginStartupResult> StartAsync(CancellationToken cancellationToken = default)
    {
        ChangeState(MPLoginState.Initializing);

        ChangeState(MPLoginState.CheckingLocalSession);
        MPLocalLoginProfile profile = await m_localLoginRepository.LoadAsync(cancellationToken);

        if (profile == null || !profile.HasAnyHistory)
        {
            return await HandleMissingProfileAsync(profile, cancellationToken);
        }

        await m_installationService.MarkLoginFlowStartedAsync(cancellationToken);

        if (!m_configuration.EnableSessionRestore)
        {
            ChangeState(MPLoginState.WaitingForLoginSelection);
            return MPLoginStartupResult.ShowLoginSelection(profile, profile.lastLoginProvider, "自动恢复已关闭，请选择登录方式。");
        }

        ChangeState(MPLoginState.RestoringSession);
        PrepareProfileForSessionRestore(profile);
        MPLoginResult restoreResult = await m_loginManager.AutoLoginAsync(cancellationToken);
        if (restoreResult.isSuccess)
        {
            if (!string.IsNullOrEmpty(profile.playerId) && profile.playerId != restoreResult.playerId)
            {
                await m_loginManager.LogoutAsync(false, cancellationToken);
                return MPLoginStartupResult.Failed(MPLoginError.Create(MPLoginErrorCodes.SessionInvalid,
                    "恢复的账号与上次账号不一致，请使用原登录方式重新登录。"), profile);
            }
            // Unity 用匿名 API 恢复所有类型的 SessionToken；API 名称不代表用户改用了游客登录。
            // 仅同一 PlayerId 才沿用原登录方式，防止恢复后把 Apple/Google 等偏好覆盖成 Anonymous。
            MPLoginProvider provider = profile.playerId == restoreResult.playerId && profile.lastLoginProvider != MPLoginProvider.Unknown
                ? profile.lastLoginProvider : ToProvider(restoreResult.loginType);
            RestoreProviderMetadata(restoreResult, provider);
            await SaveSuccessfulSessionAsync(restoreResult, provider, profile.hasBoundIdentity, cancellationToken);
            ChangeState(MPLoginState.Authenticated);
            MPLocalLoginProfile updatedProfile = await m_localLoginRepository.LoadAsync(cancellationToken);
            return MPLoginStartupResult.EnterGame(restoreResult, updatedProfile);
        }

        return await HandleRestoreFailureAsync(profile, restoreResult.error, cancellationToken);
    }

    public Task<MPLoginStartupResult> ContinueAsGuestAsync(CancellationToken cancellationToken = default)
    {
        return LoginAnonymouslyForStartupAsync(false, cancellationToken);
    }

    public async Task<MPLoginStartupResult> ContinueAsNewGuestAsync(CancellationToken cancellationToken = default)
    {
        if (!m_configuration.EnableAnonymousLogin)
        {
            MPLoginError error = MPLoginError.Create(MPLoginErrorCodes.UnsupportedLoginType, "当前配置不允许游客登录。");
            return MPLoginStartupResult.Failed(error, await m_localLoginRepository.LoadAsync(cancellationToken));
        }

        if (!m_configuration.AllowCreateNewGuestAfterRecoveryFailure)
        {
            MPLoginError error = MPLoginError.Create(MPLoginErrorCodes.AnonymousRecoveryFailed, "当前配置不允许直接创建新的游客账号。");
            return MPLoginStartupResult.ShowAnonymousRecovery(error, await m_localLoginRepository.LoadAsync(cancellationToken), false);
        }

        return await LoginAnonymouslyForStartupAsync(forceNewGuest: true, cancellationToken);
    }

    public async Task<MPLoginStartupResult> LoginWithProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsProviderEnabled(loginType))
        {
            MPLoginError error = MPLoginError.Create(MPLoginErrorCodes.UnsupportedLoginType, $"当前配置未开启 {loginType} 登录。");
            return MPLoginStartupResult.Failed(error, await m_localLoginRepository.LoadAsync(cancellationToken));
        }

        ChangeState(MPLoginState.AuthenticatingThirdParty);
        await PreserveGuestHistoryAsync(cancellationToken);
        MPThirdPartyLoginRequest safeRequest = request ?? new MPThirdPartyLoginRequest();
        safeRequest.loginType = loginType;
        safeRequest.provider = loginType;

        MPLoginResult loginResult = await m_loginManager.LoginAsync(loginType, safeRequest, cancellationToken);
        if (loginResult.isSuccess)
        {
            await SaveSuccessfulSessionAsync(loginResult, ToProvider(loginType), true, cancellationToken);
            ChangeState(MPLoginState.Authenticated);
            MPLocalLoginProfile updatedProfile = await m_localLoginRepository.LoadAsync(cancellationToken);
            return MPLoginStartupResult.EnterGame(loginResult, updatedProfile);
        }

        if (IsTemporaryError(loginResult.error))
        {
            ChangeState(MPLoginState.TemporaryUnavailable);
            return MPLoginStartupResult.ShowNetworkRetry(loginResult.error, await m_localLoginRepository.LoadAsync(cancellationToken));
        }

        ChangeState(MPLoginState.WaitingForLoginSelection);
        return MPLoginStartupResult.ShowLoginSelection(
            await m_localLoginRepository.LoadAsync(cancellationToken),
            ToProvider(loginType),
            loginResult.errorMessage);
    }

    public async Task<MPLoginResult> BindProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsProviderEnabled(loginType))
        {
            return MPLoginResult.Failed(loginType, MPLoginError.Create(MPLoginErrorCodes.UnsupportedLoginType, $"当前配置未开启 {loginType} 绑定。"));
        }

        ChangeState(MPLoginState.BindingIdentity);
        MPThirdPartyLoginRequest safeRequest = request ?? new MPThirdPartyLoginRequest();
        safeRequest.loginType = loginType;
        safeRequest.provider = loginType;

        MPLoginResult result = await m_loginManager.LinkAsync(loginType, safeRequest, cancellationToken);
        if (result.isSuccess)
        {
            await SaveSuccessfulSessionAsync(result, ToProvider(loginType), true, cancellationToken);
            ChangeState(MPLoginState.Authenticated);
            return result;
        }

        ChangeState(IsTemporaryError(result.error) ? MPLoginState.TemporaryUnavailable : MPLoginState.Failed);
        return result;
    }

    /// <summary>
    /// 处理本地资料不存在的启动分支。
    /// </summary>
    private async Task<MPLoginStartupResult> HandleMissingProfileAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        MPInstallationState installationState = await m_installationService.GetInstallationStateAsync(cancellationToken);
        await m_installationService.MarkLoginFlowStartedAsync(cancellationToken);

        MPLocalLoginProfile savedGuest = await m_localLoginRepository.LoadGuestProfileAsync(cancellationToken);
        bool hasOnlyAnonymousDraft = (profile != null && !profile.HasAnyHistory) || savedGuest?.IsIndependentGuest == true;
        if ((installationState == MPInstallationState.FirstInstall || hasOnlyAnonymousDraft) &&
            m_configuration.AutoAnonymousOnFirstInstall &&
            m_configuration.EnableAnonymousLogin)
        {
            return await LoginAnonymouslyForStartupAsync(forceNewGuest: false, cancellationToken);
        }

        ChangeState(MPLoginState.WaitingForLoginSelection);
        return MPLoginStartupResult.ShowLoginSelection(null, MPLoginProvider.Unknown, "无法确认是否为首次安装，请选择登录方式。");
    }

    /// <summary>
    /// 处理恢复会话失败后的分支。
    /// </summary>
    private async Task<MPLoginStartupResult> HandleRestoreFailureAsync(MPLocalLoginProfile profile, MPLoginError error, CancellationToken cancellationToken)
    {
        if (IsMaintenanceError(error))
        {
            ChangeState(MPLoginState.TemporaryUnavailable);
            return MPLoginStartupResult.ShowMaintenance(error, profile);
        }

        if (IsAccountDisabledError(error))
        {
            ChangeState(MPLoginState.Failed);
            return MPLoginStartupResult.ShowAccountDisabled(error, profile);
        }

        if (IsTemporaryError(error))
        {
            if (m_configuration.PreserveSessionOnNetworkError)
            {
                await MarkProfileTemporaryAsync(profile, cancellationToken);
            }

            ChangeState(MPLoginState.TemporaryUnavailable);
            return MPLoginStartupResult.ShowNetworkRetry(error, profile);
        }

        if (ShouldShowAnonymousRecovery(profile, error))
        {
            ChangeState(MPLoginState.WaitingForLoginSelection);
            // 自动恢复失败不代表用户同意放弃旧账号，不能在启动失败页开放新建游客。
            return MPLoginStartupResult.ShowAnonymousRecovery(error, profile, false);
        }

        ChangeState(MPLoginState.WaitingForLoginSelection);
        MPLoginStartupResult selection = MPLoginStartupResult.ShowLoginSelection(
            profile, profile == null ? MPLoginProvider.Unknown : profile.lastLoginProvider, "暂时无法恢复登录，请重试或使用原账号绑定的登录方式。");
        selection.error = error;
        selection.canCreateNewGuest = false;
        return selection;
    }

    /// <summary>
    /// 首次安装或用户确认后执行匿名登录。
    /// </summary>
    private async Task<MPLoginStartupResult> LoginAnonymouslyForStartupAsync(bool forceNewGuest, CancellationToken cancellationToken)
    {
        MPLocalLoginProfile previous = await m_localLoginRepository.LoadAsync(cancellationToken);
        if (!m_configuration.EnableAnonymousLogin)
            return MPLoginStartupResult.Failed(MPLoginError.Create(MPLoginErrorCodes.UnsupportedLoginType, "当前配置不允许游客登录。"), previous);

        await PreserveGuestHistoryAsync(cancellationToken);
        MPLocalLoginProfile guest = await m_localLoginRepository.LoadGuestProfileAsync(cancellationToken);
        if (forceNewGuest || guest == null || !guest.IsIndependentGuest)
        {
            // 独立的新槽不会删除旧游客/第三方的 Token。请求失败或取消后重复使用此草稿槽。
            guest = new MPLocalLoginProfile
            {
                unityProfile = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 20),
                installationId = await m_localLoginRepository.GetOrCreateInstallationIdAsync(cancellationToken),
                anonymousId = "anon_" + Guid.NewGuid().ToString("N"),
                anonymousIdempotencyKey = "idem_" + Guid.NewGuid().ToString("N"),
                lastLoginProvider = MPLoginProvider.Anonymous,
                accountType = MPAccountType.Anonymous
            };
            await m_localLoginRepository.SaveGuestProfileAsync(guest, cancellationToken);
        }

        ChangeState(MPLoginState.LoggingInAnonymously);
        MPLoginResult loginResult = await m_loginManager.LoginAsync(MPLoginType.Guest, new MPGuestLoginRequest
        {
            unityProfile = guest.unityProfile,
            requireExistingAccount = !string.IsNullOrEmpty(guest.playerId) || guest.hasUnitySessionToken,
            expectedPlayerId = guest.playerId,
            anonymousId = guest.anonymousId,
            installationId = guest.installationId,
            idempotencyKey = guest.anonymousIdempotencyKey,
            deviceId = guest.installationId,
            deviceModel = SystemInfo.deviceModel,
            operatingSystem = SystemInfo.operatingSystem
        }, cancellationToken);

        if (loginResult.isSuccess)
        {
            await SaveSuccessfulSessionAsync(loginResult, MPLoginProvider.Anonymous, false, cancellationToken);
            ChangeState(MPLoginState.Authenticated);
            MPLocalLoginProfile updatedProfile = await m_localLoginRepository.LoadAsync(cancellationToken);
            return MPLoginStartupResult.EnterGame(loginResult, updatedProfile);
        }

        if (IsTemporaryError(loginResult.error))
        {
            ChangeState(MPLoginState.TemporaryUnavailable);
            return MPLoginStartupResult.ShowNetworkRetry(loginResult.error, previous);
        }

        ChangeState(MPLoginState.Failed);
        return MPLoginStartupResult.Failed(loginResult.error, previous);
    }

    private async Task PreserveGuestHistoryAsync(CancellationToken cancellationToken)
    {
        if (await m_localLoginRepository.LoadGuestProfileAsync(cancellationToken) != null) return;
        MPLocalLoginProfile previous = await m_localLoginRepository.LoadAsync(cancellationToken);
        if (previous?.IsIndependentGuest != true) return;
        if (string.IsNullOrEmpty(previous.unityProfile)) previous.unityProfile = GUEST_PROFILE;
        await m_localLoginRepository.SaveGuestProfileAsync(previous, cancellationToken);
    }

    /// <summary>
    /// 判断是否应该进入游客账号恢复页面。
    /// 当前项目还没有真正的服务端匿名恢复接口，因此 NO_LOCAL_SESSION 不能展示为“可恢复”。
    /// 否则当 Unity Authentication 本地凭证没有写入或读取不到时，会每次启动都误进恢复页。
    /// </summary>
    private bool ShouldShowAnonymousRecovery(MPLocalLoginProfile profile, MPLoginError error)
    {
        if (!m_configuration.EnableAnonymousRecovery ||
            profile == null ||
            profile.accountType != MPAccountType.Anonymous ||
            !profile.HasAnonymousRecoveryData)
        {
            return false;
        }

        if (error != null && error.code == MPLoginErrorCodes.NoLocalSession)
        {
            Debug.LogWarning("[MPLogin] 本地没有 Unity Authentication SessionToken，已跳过游客恢复页。请检查 Profile 是否为 guest，以及 Unity Authentication 是否成功持久化凭证。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 自动恢复前切回本地资料记录的 Unity Authentication Profile。
    /// Unity Authentication 的 SessionToken 按 Profile 隔离保存；游客登录写在 guest Profile 下。
    /// 如果恢复前仍停留在默认 Profile，会误判没有本地凭证，从而每次进入游客账号恢复流程。
    /// </summary>
    private void PrepareProfileForSessionRestore(MPLocalLoginProfile profile)
    {
        if (profile == null || m_loginManager.CurrentSession != null)
        {
            return;
        }

        string unityProfile = ResolveUnityProfileForRestore(profile);
        if (string.IsNullOrEmpty(unityProfile))
        {
            return;
        }

        if (!m_loginManager.SwitchProfile(unityProfile))
        {
            Debug.LogWarning($"[MPLogin] 切换 Unity Authentication Profile 失败，可能会影响本地会话恢复：{unityProfile}");
        }
    }

    /// <summary>
    /// 从本地资料推断启动恢复应使用的 Unity Authentication Profile。
    /// 新版本会直接保存 unityProfile；旧版本或异常资料缺失时，匿名历史回退到固定的 guest Profile。
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
    /// 登录成功后同步本地资料。
    /// </summary>
    private async Task SaveSuccessfulSessionAsync(MPLoginResult result, MPLoginProvider provider, bool markAsBound, CancellationToken cancellationToken)
    {
        if (result == null || !result.isSuccess)
        {
            return;
        }

        MPLocalLoginProfile profile = await m_localLoginRepository.LoadAsync(cancellationToken) ?? new MPLocalLoginProfile();
        if (provider == MPLoginProvider.Anonymous)
        {
            MPLocalLoginProfile guest = await m_localLoginRepository.LoadGuestProfileAsync(cancellationToken);
            if (guest != null && guest.unityProfile == result.session.profile &&
                (string.IsNullOrEmpty(guest.playerId) || guest.playerId == result.playerId))
                profile = guest;
        }
        if (!string.IsNullOrEmpty(profile.playerId) && profile.playerId != result.playerId)
            profile = new MPLocalLoginProfile { installationId = profile.installationId };
        if (string.IsNullOrEmpty(profile.installationId))
        {
            profile.installationId = await m_localLoginRepository.GetOrCreateInstallationIdAsync(cancellationToken);
        }

        if (provider == MPLoginProvider.Anonymous && string.IsNullOrEmpty(profile.anonymousId))
        {
            profile.anonymousId = await m_localLoginRepository.GetOrCreateAnonymousIdAsync(cancellationToken);
        }

        profile.ApplySession(result.session, provider, markAsBound);
        await m_localLoginRepository.SaveAsync(profile, cancellationToken);
        if (provider == MPLoginProvider.Anonymous && profile.IsIndependentGuest)
            await m_localLoginRepository.SaveGuestProfileAsync(profile, cancellationToken);
    }

    /// <summary>
    /// 将本地资料标记为临时不可确认，保留恢复线索。
    /// </summary>
    private async Task MarkProfileTemporaryAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        if (profile == null)
        {
            return;
        }

        profile.accountType = MPAccountType.Temporary;
        await m_localLoginRepository.SaveAsync(profile, cancellationToken);
    }

    /// <summary>
    /// 判断登录类型是否已在配置中开放。
    /// </summary>
    private bool IsProviderEnabled(MPLoginType loginType)
    {
        switch (loginType)
        {
            case MPLoginType.Guest:
                return m_configuration.EnableAnonymousLogin;
            case MPLoginType.UsernamePassword:
                return m_configuration.EnableUsernamePasswordLogin;
            case MPLoginType.Google:
                return m_configuration.EnableGoogleLogin;
            case MPLoginType.GooglePlayGames:
                return m_configuration.EnableGooglePlayGamesLogin;
            case MPLoginType.Apple:
                return m_configuration.EnableAppleLogin;
            case MPLoginType.Facebook:
                return m_configuration.EnableFacebookLogin;
            default:
                return true;
        }
    }

    /// <summary>
    /// 判断是否属于临时失败，临时失败不能清理历史账号资料。
    /// </summary>
    private static bool IsTemporaryError(MPLoginError error)
    {
        if (error == null)
        {
            return false;
        }

        // 未知但允许重试的异常也不能被当作账号永久失效；保留原 Profile 和登录凭证。
        return error.isTemporary || error.retryable ||
               error.code == MPLoginErrorCodes.NetworkUnavailable ||
               error.code == MPLoginErrorCodes.RequestTimeout ||
               (error.code == MPLoginErrorCodes.ServerError && error.retryable);
    }

    /// <summary>
    /// 判断是否是维护错误。
    /// </summary>
    private static bool IsMaintenanceError(MPLoginError error)
    {
        return error != null && error.code == MPLoginErrorCodes.Maintenance;
    }

    /// <summary>
    /// 判断是否是账号不可用错误。
    /// </summary>
    private static bool IsAccountDisabledError(MPLoginError error)
    {
        return error != null && error.code == MPLoginErrorCodes.AccountDisabled;
    }

    /// <summary>
    /// 恢复 SDK 会话后，还原业务层记录的原登录方式。
    /// </summary>
    private static void RestoreProviderMetadata(MPLoginResult result, MPLoginProvider provider)
    {
        MPLoginType loginType;
        switch (provider)
        {
            case MPLoginProvider.Anonymous: loginType = MPLoginType.Guest; break;
            case MPLoginProvider.UsernamePassword: loginType = MPLoginType.UsernamePassword; break;
            case MPLoginProvider.Google: loginType = MPLoginType.Google; break;
            case MPLoginProvider.GooglePlayGames: loginType = MPLoginType.GooglePlayGames; break;
            case MPLoginProvider.Apple: loginType = MPLoginType.Apple; break;
            case MPLoginProvider.Facebook: loginType = MPLoginType.Facebook; break;
            default: return;
        }
        result.loginType = loginType;
        if (result.session != null)
        {
            result.session.loginType = loginType;
            result.session.isGuest = loginType == MPLoginType.Guest && !result.session.hasBoundIdentity;
        }
    }

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
    /// 更新流程状态并派发事件。
    /// </summary>
    private void ChangeState(MPLoginState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(state);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 登录模块核心编排器。
/// 负责状态管理、事件分发、策略选择和 Session 更新，不直接依赖具体第三方 SDK。
/// </summary>
public class MPLoginManagerCore : IMPLoginManager
{
    /// <summary>
    /// 登录策略工厂，用于根据登录类型选择对应策略。
    /// </summary>
    private readonly IMPLoginStrategyFactory m_strategyFactory;

    /// <summary>
    /// 第三方授权适配器工厂，用于绑定或第三方登录时获取平台适配器。
    /// </summary>
    private readonly IMPThirdPartyAuthAdapterFactory m_adapterFactory;

    /// <summary>
    /// 认证服务 API 抽象，当前默认实现为 Unity Authentication。
    /// </summary>
    private readonly IMPAuthApi m_authApi;

    /// <summary>
    /// 当前 Session 存储服务。
    /// </summary>
    private readonly IMPSessionService m_sessionService;

    /// <summary>
    /// 当前登录流程的取消源，用于取消上一条未完成登录请求。
    /// </summary>
    private CancellationTokenSource m_loginCancellation;

    public MPLoginManagerCore(
        IMPLoginStrategyFactory strategyFactory,
        IMPThirdPartyAuthAdapterFactory adapterFactory,
        IMPAuthApi authApi,
        IMPSessionService sessionService)
    {
        m_strategyFactory = strategyFactory;
        m_adapterFactory = adapterFactory;
        m_authApi = authApi;
        m_sessionService = sessionService;
        ChangeState(MPLoginState.LoggedOut);
    }

    /// <summary>
    /// 当前登录模块状态。
    /// </summary>
    public MPLoginState State { get; private set; } = MPLoginState.Uninitialized;

    /// <summary>
    /// 当前登录会话。
    /// </summary>
    public MPUserSession CurrentSession => m_sessionService.CurrentSession;

    /// <summary>
    /// 最近一次登录、刷新或绑定失败的错误信息。
    /// </summary>
    public MPLoginError LastError { get; private set; }

    /// <summary>
    /// 登录状态变化事件。
    /// </summary>
    public event Action<MPLoginState> StateChanged;

    /// <summary>
    /// 登录或绑定成功事件。
    /// </summary>
    public event Action<MPUserSession> LoginSucceeded;

    /// <summary>
    /// 登录、绑定或刷新失败事件。
    /// </summary>
    public event Action<MPLoginError> LoginFailed;

    /// <summary>
    /// Token 或认证状态刷新成功事件。
    /// </summary>
    public event Action TokenRefreshed;

    /// <summary>
    /// Session 失效事件。
    /// </summary>
    public event Action SessionExpired;

    /// <summary>
    /// 登出完成事件。
    /// </summary>
    public event Action LoggedOut;

    /// <summary>
    /// 标准登录流程：检查忙碌状态、选择登录策略、执行登录并统一更新 Session 与事件。
    /// </summary>
    public async Task<MPLoginResult> LoginAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (IsBusy())
        {
            return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.LoginInProgress, "登录流程正在进行中。"));
        }

        ChangeState(GetLoginState(loginType));
        LastError = null;

        // 新登录开始时取消上一条仍未结束的登录链路，避免旧回调覆盖新状态。
        m_loginCancellation?.Cancel();
        m_loginCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        MPLoginResult result;
        try
        {
            IMPLoginStrategy strategy = m_strategyFactory.GetStrategy(loginType);
            result = await strategy.LoginAsync(request, m_loginCancellation.Token);
        }
        catch (Exception exception)
        {
            result = MPLoginResult.Failed(loginType, MPLoginExceptionMapper.Map(exception));
        }

        if (result.isSuccess)
        {
            m_sessionService.SetSession(result.session);
            ChangeState(MPLoginState.Authenticated);
            LoginSucceeded?.Invoke(result.session);
            return result;
        }

        ChangeState(MPLoginState.Failed);
        LastError = result.error;
        LoginFailed?.Invoke(result.error);
        return result;
    }

    /// <summary>
    /// 自动登录优先使用内存中的 Session；没有内存 Session 时再尝试 Unity Authentication 的本地凭证。
    /// </summary>
    public async Task<MPLoginResult> AutoLoginAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentSession != null && !CurrentSession.IsAccessTokenExpired)
        {
            ChangeState(MPLoginState.Authenticated);
            LoginSucceeded?.Invoke(CurrentSession);
            return MPLoginResult.Success(CurrentSession);
        }

        try
        {
            await m_authApi.InitializeAsync(cancellationToken);
            if (m_authApi.IsAuthorized)
            {
                MPUserSession session = await m_authApi.GetCurrentSessionAsync(MPLoginType.Guest, cancellationToken);
                m_sessionService.SetSession(session);
                ChangeState(MPLoginState.Authenticated);
                LoginSucceeded?.Invoke(session);
                return MPLoginResult.Success(session);
            }

            if (!m_authApi.SessionTokenExists)
            {
                return PublishFailure(MPLoginType.None, MPLoginError.Create(MPLoginErrorCodes.NoLocalSession, "本地不存在可用的登录凭证。"));
            }

            return await LoginAsync(MPLoginType.Guest, new MPGuestLoginRequest(), cancellationToken);
        }
        catch (Exception exception)
        {
            return PublishFailure(MPLoginType.None, MPLoginExceptionMapper.Map(exception));
        }
    }

    /// <summary>
    /// 刷新认证状态。Unity SDK 会维护实际 Token，这里负责同步本地 Session 和状态事件。
    /// </summary>
    public async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (State == MPLoginState.RefreshingToken)
        {
            return false;
        }

            ChangeState(MPLoginState.RefreshingSession);

        try
        {
            await m_authApi.InitializeAsync(cancellationToken);
            if (m_authApi.IsAuthorized)
            {
                MPLoginType loginType = CurrentSession == null ? MPLoginType.None : CurrentSession.loginType;
                MPUserSession refreshedSession = await m_authApi.GetCurrentSessionAsync(loginType, cancellationToken);
                m_sessionService.SetSession(refreshedSession);
                ChangeState(MPLoginState.Authenticated);
                TokenRefreshed?.Invoke();
                return true;
            }

            if (m_authApi.SessionTokenExists)
            {
                MPLoginResult result = await LoginAsync(MPLoginType.Guest, new MPGuestLoginRequest(), cancellationToken);
                if (result.isSuccess)
                {
                    TokenRefreshed?.Invoke();
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            LastError = MPLoginExceptionMapper.Map(exception);
            LoginFailed?.Invoke(LastError);
        }

        m_sessionService.Clear();
        ChangeState(MPLoginState.LoggedOut);
        SessionExpired?.Invoke();
        return false;
    }

    /// <summary>
    /// 重新拉取玩家信息，用于登录后更新昵称、用户名或绑定信息。
    /// </summary>
    public async Task<bool> RefreshPlayerInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await m_authApi.InitializeAsync(cancellationToken);
            if (!m_authApi.IsAuthorized)
            {
                return false;
            }

            MPLoginType loginType = CurrentSession == null ? MPLoginType.None : CurrentSession.loginType;
            MPUserSession session = await m_authApi.GetCurrentSessionAsync(loginType, cancellationToken);
            m_sessionService.SetSession(session);
            return true;
        }
        catch (Exception exception)
        {
            LastError = MPLoginExceptionMapper.Map(exception);
            LoginFailed?.Invoke(LastError);
            return false;
        }
    }

    /// <summary>
    /// 给当前已登录账号绑定新的登录方式，或执行账号密码用户的密码更新。
    /// </summary>
    public async Task<MPLoginResult> LinkAsync(MPLoginType loginType, MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (CurrentSession == null)
        {
            return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "当前没有可绑定的登录会话。"));
        }

        try
        {
            ChangeState(MPLoginState.BindingIdentity);
            MPUserSession session;

            if (loginType == MPLoginType.UsernamePassword)
            {
                if (!(request is MPPasswordLoginRequest passwordRequest))
                {
                    return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "账号密码绑定请求类型不正确。"));
                }

                if (passwordRequest.mode == MPPasswordLoginMode.UpdatePassword)
                {
                    if (string.IsNullOrWhiteSpace(passwordRequest.currentPassword) ||
                        string.IsNullOrWhiteSpace(passwordRequest.password))
                    {
                        return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "当前密码和新密码不能为空。"));
                    }

                    session = await m_authApi.UpdatePasswordAsync(passwordRequest.currentPassword, passwordRequest.password, cancellationToken);
                }
                else
                {
                    // Unity Authentication 将“给当前账号加账号密码”称为 AddUsernamePassword。
                    if (string.IsNullOrWhiteSpace(passwordRequest.account) ||
                        string.IsNullOrWhiteSpace(passwordRequest.password))
                    {
                        return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "账号和密码不能为空。"));
                    }

                    session = await m_authApi.LinkUsernamePasswordAsync(passwordRequest.account, passwordRequest.password, cancellationToken);
                }
            }
            else
            {
                if (!(request is MPThirdPartyLoginRequest thirdPartyRequest))
                {
                    return PublishFailure(loginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "第三方绑定请求类型不正确。"));
                }

                // 第三方 SDK 的接入细节留在 Adapter 内部，核心层只关心最终 Token/AuthCode。
                IMPThirdPartyAuthAdapter adapter = m_adapterFactory.GetAdapter(loginType);
                MPThirdPartyAuthResult authResult = await adapter.AuthorizeAsync(thirdPartyRequest, cancellationToken);
                if (!authResult.success)
                {
                    string errorCode = string.IsNullOrEmpty(authResult.errorCode)
                        ? MPLoginErrorCodes.ThirdPartyAuthFailed
                        : authResult.errorCode;
                    return PublishFailure(loginType, MPLoginError.Create(
                        errorCode,
                        authResult.errorMessage,
                        errorCode != MPLoginErrorCodes.UserCancelled));
                }

                session = await m_authApi.LinkThirdPartyAsync(loginType, authResult, thirdPartyRequest.forceLink, cancellationToken);
            }

            m_sessionService.SetSession(session);
            ChangeState(MPLoginState.Authenticated);
            LoginSucceeded?.Invoke(session);
            return MPLoginResult.Success(session);
        }
        catch (Exception exception)
        {
            return PublishFailure(loginType, MPLoginExceptionMapper.Map(exception));
        }
    }

    /// <summary>
    /// 登出并清理本地 Session。即使远端登出失败，也会保证本地状态被清理。
    /// </summary>
    public async Task LogoutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default)
    {
        ChangeState(MPLoginState.LoggingOut);
        m_loginCancellation?.Cancel();

        try
        {
            await m_authApi.SignOutAsync(clearCredentials, cancellationToken);
        }
        catch
        {
            // Local cleanup must still happen even if remote sign-out fails.
        }

        m_sessionService.Clear();
        ChangeState(MPLoginState.LoggedOut);
        LoggedOut?.Invoke();
    }

    /// <summary>
    /// 切换 Unity Authentication Profile。Profile 用于隔离本地登录凭证。
    /// </summary>
    public bool SwitchProfile(string profile)
    {
        try
        {
            bool result = m_authApi.SwitchProfile(profile);
            if (result)
            {
                m_sessionService.Clear();
                ChangeState(MPLoginState.LoggedOut);
            }

            return result;
        }
        catch (Exception exception)
        {
            LastError = MPLoginExceptionMapper.Map(exception);
            LoginFailed?.Invoke(LastError);
            return false;
        }
    }

    /// <summary>
    /// 清理 Unity Authentication 本地 SessionToken，通常用于完全退出或切账号。
    /// </summary>
    public bool ClearSessionToken()
    {
        try
        {
            return m_authApi.ClearSessionToken();
        }
        catch (Exception exception)
        {
            LastError = MPLoginExceptionMapper.Map(exception);
            LoginFailed?.Invoke(LastError);
            return false;
        }
    }

    private bool IsBusy()
    {
        return State == MPLoginState.Authenticating ||
               State == MPLoginState.LoggingInAnonymously ||
               State == MPLoginState.AuthenticatingThirdParty ||
               State == MPLoginState.BindingIdentity ||
               State == MPLoginState.RestoringSession ||
               State == MPLoginState.RefreshingToken ||
               State == MPLoginState.LoadingUserData ||
               State == MPLoginState.LoggingOut;
    }

    /// <summary>
    /// 根据登录方式返回更精确的流程状态。
    /// </summary>
    private static MPLoginState GetLoginState(MPLoginType loginType)
    {
        switch (loginType)
        {
            case MPLoginType.Guest:
                return MPLoginState.LoggingInAnonymously;
            case MPLoginType.Google:
            case MPLoginType.GooglePlayGames:
            case MPLoginType.Apple:
            case MPLoginType.Facebook:
                return MPLoginState.AuthenticatingThirdParty;
            default:
                return MPLoginState.Authenticating;
        }
    }

    /// <summary>
    /// 统一失败出口，保证 LastError、状态和事件保持一致。
    /// </summary>
    private MPLoginResult PublishFailure(MPLoginType loginType, MPLoginError error)
    {
        LastError = error;
        ChangeState(MPLoginState.Failed);
        LoginFailed?.Invoke(error);
        return MPLoginResult.Failed(loginType, error);
    }

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

using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Unity Authentication 的具体 API 封装。
/// 上层只通过 IMPAuthApi 使用它，便于后续替换、Mock 或隔离 Unity SDK 变化。
/// </summary>
public class MPUnityAuthenticationApi : IMPAuthApi
{
    /// <summary>
    /// 游客登录使用的 Unity Authentication Profile 名称。
    /// </summary>
    private const string GUEST_PROFILE = "guest";

    /// <summary>
    /// 是否已经提示过 Cloud Project Id 未绑定，避免重复刷日志。
    /// </summary>
    private bool m_hasWarnedCloudProjectId;

    /// <summary>
    /// Unity Authentication 当前是否处于已登录状态。
    /// </summary>
    public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

    /// <summary>
    /// Unity Authentication 当前 access token 是否仍可授权访问服务。
    /// </summary>
    public bool IsAuthorized => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsAuthorized;

    /// <summary>
    /// 当前 Profile 是否存在本地 SessionToken。
    /// </summary>
    public bool SessionTokenExists => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.SessionTokenExists;

    /// <summary>
    /// 初始化 Unity Services。当前项目未绑定 Cloud Project 时仅警告，方便本地先搭建流程。
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            return;
        }

        if (string.IsNullOrEmpty(Application.cloudProjectId) && !m_hasWarnedCloudProjectId)
        {
            m_hasWarnedCloudProjectId = true;
            Debug.LogWarning("[MPLogin] Unity Cloud Project Id is empty. Bind a Cloud Project before using online Authentication.");
        }

        await UnityServices.InitializeAsync();
    }

    /// <summary>
    /// 匿名登录。游客统一使用 guest profile，避免和后续正式账号本地凭证混在一起。
    /// </summary>
    public async Task<MPUserSession> SignInAnonymouslyAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        IAuthenticationService service = AuthenticationService.Instance;
        if (!service.IsSignedIn && service.Profile != GUEST_PROFILE)
        {
            service.SwitchProfile(GUEST_PROFILE);
        }

        if (!service.IsAuthorized)
        {
            await service.SignInAnonymouslyAsync();
        }

        return await GetCurrentSessionAsync(MPLoginType.Guest, cancellationToken);
    }

    /// <summary>
    /// 使用 Unity Authentication 用户名和密码登录。
    /// </summary>
    public async Task<MPUserSession> SignInWithUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
        return await GetCurrentSessionAsync(MPLoginType.UsernamePassword, cancellationToken);
    }

    /// <summary>
    /// 使用 Unity Authentication 用户名和密码注册。
    /// </summary>
    public async Task<MPUserSession> SignUpWithUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
        return await GetCurrentSessionAsync(MPLoginType.UsernamePassword, cancellationToken);
    }

    /// <summary>
    /// 第三方登录。不同平台需要的凭证类型不同：Google/Apple 用 IdentityToken，Google Play Games 用 AuthCode，Facebook 用 AccessToken。
    /// </summary>
    public async Task<MPUserSession> SignInWithThirdPartyAsync(MPLoginType loginType, MPThirdPartyAuthResult authResult, bool createAccount, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        SignInOptions options = new SignInOptions { CreateAccount = createAccount };

        switch (loginType)
        {
            case MPLoginType.Google:
                await AuthenticationService.Instance.SignInWithGoogleAsync(authResult.identityToken, options);
                break;
            case MPLoginType.GooglePlayGames:
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authResult.authorizationCode, options);
                break;
            case MPLoginType.Apple:
                await AuthenticationService.Instance.SignInWithAppleAsync(authResult.identityToken, options);
                break;
            case MPLoginType.Facebook:
                await AuthenticationService.Instance.SignInWithFacebookAsync(authResult.accessToken, options);
                break;
            default:
                throw new System.NotSupportedException($"Third party login is not supported: {loginType}");
        }

        return await GetCurrentSessionAsync(loginType, cancellationToken);
    }

    /// <summary>
    /// 给当前已授权玩家添加账号密码登录方式。
    /// </summary>
    public async Task<MPUserSession> LinkUsernamePasswordAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await AuthenticationService.Instance.AddUsernamePasswordAsync(username, password);
        return await GetCurrentSessionAsync(MPLoginType.UsernamePassword, cancellationToken);
    }

    /// <summary>
    /// 给当前已授权玩家绑定第三方登录方式。
    /// </summary>
    public async Task<MPUserSession> LinkThirdPartyAsync(MPLoginType loginType, MPThirdPartyAuthResult authResult, bool forceLink, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        LinkOptions options = new LinkOptions { ForceLink = forceLink };

        switch (loginType)
        {
            case MPLoginType.Google:
                await AuthenticationService.Instance.LinkWithGoogleAsync(authResult.identityToken, options);
                break;
            case MPLoginType.GooglePlayGames:
                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authResult.authorizationCode, options);
                break;
            case MPLoginType.Apple:
                await AuthenticationService.Instance.LinkWithAppleAsync(authResult.identityToken, options);
                break;
            case MPLoginType.Facebook:
                await AuthenticationService.Instance.LinkWithFacebookAsync(authResult.accessToken, options);
                break;
            default:
                throw new System.NotSupportedException($"Third party link is not supported: {loginType}");
        }

        return await GetCurrentSessionAsync(loginType, cancellationToken);
    }

    /// <summary>
    /// 修改当前账号密码用户的密码。
    /// </summary>
    public async Task<MPUserSession> UpdatePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await AuthenticationService.Instance.UpdatePasswordAsync(currentPassword, newPassword);
        return await GetCurrentSessionAsync(MPLoginType.UsernamePassword, cancellationToken);
    }

    /// <summary>
    /// 将 Unity Authentication 当前状态转换为项目内部使用的 MPUserSession。
    /// </summary>
    public async Task<MPUserSession> GetCurrentSessionAsync(MPLoginType loginType, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        IAuthenticationService service = AuthenticationService.Instance;
        if (service.IsAuthorized)
        {
            try
            {
                await service.GetPlayerInfoAsync();
            }
            catch
            {
                // Player info is optional for local startup; keep session creation resilient.
            }
        }

        PlayerInfo playerInfo = service.PlayerInfo;
        return new MPUserSession
        {
            userId = service.PlayerId,
            playerName = service.PlayerName,
            username = playerInfo == null ? string.Empty : playerInfo.Username,
            accessToken = service.AccessToken,
            sessionToken = service.SessionToken,
            // Unity SDK 内部负责 access token 刷新，这里用 MaxValue 表示不由业务层主动判过期。
            accessTokenExpiresAtUtc = System.DateTime.MaxValue,
            loginType = loginType,
            profile = service.Profile,
            isGuest = loginType == MPLoginType.Guest || loginType == MPLoginType.Anonymous
        };
    }

    /// <summary>
    /// 登出 Unity Authentication；clearCredentials 为 true 时会清理本地凭证。
    /// </summary>
    public async Task SignOutAsync(bool clearCredentials = false, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        IAuthenticationService service = AuthenticationService.Instance;
        if (service.IsSignedIn)
        {
            service.SignOut(clearCredentials);
        }
    }

    /// <summary>
    /// 切换 Unity Authentication Profile。
    /// </summary>
    public bool SwitchProfile(string profile)
    {
        AuthenticationService.Instance.SwitchProfile(profile);
        return true;
    }

    /// <summary>
    /// 清理当前 Profile 的本地 SessionToken。
    /// </summary>
    public bool ClearSessionToken()
    {
        AuthenticationService.Instance.ClearSessionToken();
        return true;
    }
}

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 游客/匿名登录策略，只负责匿名登录请求校验和调用 AuthApi。
/// </summary>
public class MPGuestLoginStrategy : IMPLoginStrategy
{
    /// <summary>
    /// 认证 API，用于执行 Unity Authentication 匿名登录。
    /// </summary>
    private readonly IMPAuthApi m_authApi;

    public MPGuestLoginStrategy(IMPAuthApi authApi)
    {
        m_authApi = authApi;
    }

    /// <summary>
    /// 当前策略支持的登录类型。
    /// </summary>
    public MPLoginType LoginType => MPLoginType.Guest;

    /// <summary>
    /// 执行游客登录。request 可为空，方便旧流程直接调用默认游客登录。
    /// </summary>
    public async Task<MPLoginResult> LoginAsync(MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request != null && !(request is MPGuestLoginRequest))
        {
            return MPLoginResult.Failed(LoginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "匿名登录请求类型不正确。"));
        }

        try
        {
            MPGuestLoginRequest guest = request as MPGuestLoginRequest;
            MPUserSession session = guest != null && !string.IsNullOrEmpty(guest.unityProfile)
                ? await m_authApi.SignInGuestAsync(guest, cancellationToken)
                : await m_authApi.SignInAnonymouslyAsync(cancellationToken);
            return MPLoginResult.Success(session);
        }
        catch (System.Exception exception)
        {
            return MPLoginResult.Failed(LoginType, MPLoginExceptionMapper.Map(exception));
        }
    }
}

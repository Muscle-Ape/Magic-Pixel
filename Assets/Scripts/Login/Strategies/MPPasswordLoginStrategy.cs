using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Unity Authentication 账号密码登录/注册策略。
/// 账号绑定和改密属于已登录账号操作，由 MPLoginManagerCore.LinkAsync 处理。
/// </summary>
public class MPPasswordLoginStrategy : IMPLoginStrategy
{
    /// <summary>
    /// 认证 API，用于执行账号密码登录或注册。
    /// </summary>
    private readonly IMPAuthApi m_authApi;

    public MPPasswordLoginStrategy(IMPAuthApi authApi)
    {
        m_authApi = authApi;
    }

    /// <summary>
    /// 当前策略支持的登录类型。
    /// </summary>
    public MPLoginType LoginType => MPLoginType.UsernamePassword;

    /// <summary>
    /// 根据请求模式执行账号密码登录或注册。
    /// </summary>
    public async Task<MPLoginResult> LoginAsync(MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!(request is MPPasswordLoginRequest passwordRequest))
        {
            return MPLoginResult.Failed(LoginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "账号密码登录请求类型不正确。"));
        }

        if (string.IsNullOrWhiteSpace(passwordRequest.account) || string.IsNullOrWhiteSpace(passwordRequest.password))
        {
            return MPLoginResult.Failed(LoginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "账号和密码不能为空。"));
        }

        try
        {
            MPUserSession session;
            if (passwordRequest.mode == MPPasswordLoginMode.Register)
            {
                session = await m_authApi.SignUpWithUsernamePasswordAsync(passwordRequest.account, passwordRequest.password, cancellationToken);
            }
            else
            {
                session = await m_authApi.SignInWithUsernamePasswordAsync(passwordRequest.account, passwordRequest.password, cancellationToken);
            }

            return MPLoginResult.Success(session);
        }
        catch (System.Exception exception)
        {
            return MPLoginResult.Failed(LoginType, MPLoginExceptionMapper.Map(exception));
        }
    }
}

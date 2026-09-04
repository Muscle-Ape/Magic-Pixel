using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 第三方登录策略。
/// 负责校验 Provider、调用对应 Adapter 获取授权结果，然后交给 AuthApi 完成 Unity 登录。
/// </summary>
public class MPThirdPartyLoginStrategy : IMPLoginStrategy
{
    /// <summary>
    /// 当前策略负责处理的第三方登录类型。
    /// </summary>
    private readonly MPLoginType m_loginType;

    /// <summary>
    /// 第三方授权适配器工厂。
    /// </summary>
    private readonly IMPThirdPartyAuthAdapterFactory m_adapterFactory;

    /// <summary>
    /// 认证 API，用于把第三方授权结果交给 Unity Authentication 登录。
    /// </summary>
    private readonly IMPAuthApi m_authApi;

    public MPThirdPartyLoginStrategy(MPLoginType loginType, IMPThirdPartyAuthAdapterFactory adapterFactory, IMPAuthApi authApi)
    {
        m_loginType = loginType;
        m_adapterFactory = adapterFactory;
        m_authApi = authApi;
    }

    /// <summary>
    /// 当前策略支持的登录类型。
    /// </summary>
    public MPLoginType LoginType => m_loginType;

    /// <summary>
    /// 执行第三方登录。真正的平台 SDK 调用被隔离在具体 Adapter 中。
    /// </summary>
    public async Task<MPLoginResult> LoginAsync(MPLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (!(request is MPThirdPartyLoginRequest thirdPartyRequest))
        {
            return MPLoginResult.Failed(LoginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "第三方登录请求类型不正确。"));
        }

        if (thirdPartyRequest.provider != LoginType)
        {
            return MPLoginResult.Failed(LoginType, MPLoginError.Create(MPLoginErrorCodes.InvalidRequest, "第三方登录 Provider 与策略类型不一致。"));
        }

        try
        {
            // Adapter 负责拉起平台 SDK，或校验外部已经提供的 token/authCode。
            IMPThirdPartyAuthAdapter adapter = m_adapterFactory.GetAdapter(thirdPartyRequest.provider);
            MPThirdPartyAuthResult authResult = await adapter.AuthorizeAsync(thirdPartyRequest, cancellationToken);
            if (!authResult.success)
            {
                string errorCode = string.IsNullOrEmpty(authResult.errorCode)
                    ? MPLoginErrorCodes.ThirdPartyAuthFailed
                    : authResult.errorCode;
                return MPLoginResult.Failed(LoginType, MPLoginError.Create(
                    errorCode,
                    authResult.errorMessage,
                    errorCode != MPLoginErrorCodes.UserCancelled));
            }

            MPUserSession session = await m_authApi.SignInWithThirdPartyAsync(LoginType, authResult, thirdPartyRequest.createAccount,
                cancellationToken, thirdPartyRequest.expectedPlayerId);
            return MPLoginResult.Success(session);
        }
        catch (System.Exception exception)
        {
            return MPLoginResult.Failed(LoginType, MPLoginExceptionMapper.Map(exception));
        }
    }
}

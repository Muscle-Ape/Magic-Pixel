using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// “外部已提供 token/authCode”的第三方授权适配器基类。
/// 当前阶段先由 UI 或平台 SDK 调用层传入凭证，后续可以替换为真正拉起 SDK 的 Adapter。
/// </summary>
public abstract class MPProvidedTokenAuthAdapterBase : IMPThirdPartyAuthAdapter
{
    /// <summary>
    /// 当前适配器支持的第三方登录类型。
    /// </summary>
    public abstract MPLoginType LoginType { get; }

    /// <summary>
    /// 校验并转换第三方凭证。这里不保存 token，也不打印 token。
    /// </summary>
    public Task<MPThirdPartyAuthResult> AuthorizeAsync(MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request == null)
        {
            return Task.FromResult(MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.InvalidRequest, "第三方登录请求不能为空。"));
        }

        return Task.FromResult(CreateResult(request));
    }

    public Task LogoutAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 由具体平台决定需要哪种凭证字段。
    /// </summary>
    protected abstract MPThirdPartyAuthResult CreateResult(MPThirdPartyLoginRequest request);

    /// <summary>
    /// 快速校验平台所需 token 是否为空。
    /// </summary>
    protected MPThirdPartyAuthResult RequireToken(string token, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return MPThirdPartyAuthResult.Failed(MPLoginErrorCodes.InvalidRequest, errorMessage);
        }

        return null;
    }
}

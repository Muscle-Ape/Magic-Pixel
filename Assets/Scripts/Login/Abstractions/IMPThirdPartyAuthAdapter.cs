using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 第三方平台授权适配器。
/// Google、Apple、Facebook 等 SDK 的差异都应隔离在具体 Adapter 中。
/// </summary>
public interface IMPThirdPartyAuthAdapter
{
    /// <summary>
    /// 当前适配器支持的第三方登录类型。
    /// </summary>
    MPLoginType LoginType { get; }

    /// <summary>
    /// 执行第三方平台授权，并返回统一授权结果。
    /// </summary>
    Task<MPThirdPartyAuthResult> AuthorizeAsync(MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 登出或清理第三方平台授权状态。
    /// </summary>
    Task LogoutAsync();
}

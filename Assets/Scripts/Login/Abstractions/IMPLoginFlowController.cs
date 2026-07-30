using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 登录启动与恢复流程控制器接口。
/// 它只返回明确的启动动作，避免 UI 层猜测下一步应该做什么。
/// </summary>
public interface IMPLoginFlowController
{
    /// <summary>
    /// 当前启动流程状态。
    /// </summary>
    MPLoginState State { get; }

    /// <summary>
    /// 启动流程状态变化事件。
    /// </summary>
    event Action<MPLoginState> StateChanged;

    /// <summary>
    /// 执行启动登录决策流程。
    /// </summary>
    Task<MPLoginStartupResult> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 用户明确选择创建新的游客账号。
    /// </summary>
    Task<MPLoginStartupResult> ContinueAsNewGuestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用第三方登录提供方继续登录或恢复账号。
    /// </summary>
    Task<MPLoginStartupResult> LoginWithProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 给当前已登录账号绑定第三方登录提供方。
    /// </summary>
    Task<MPLoginResult> BindProviderAsync(MPLoginType loginType, MPThirdPartyLoginRequest request, CancellationToken cancellationToken = default);
}

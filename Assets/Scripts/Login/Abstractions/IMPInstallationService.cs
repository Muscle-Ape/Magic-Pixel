using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 安装状态服务接口。
/// 用于判断本地资料缺失时是否可以按真正首次安装处理。
/// </summary>
public interface IMPInstallationService
{
    /// <summary>
    /// 获取当前安装状态。
    /// </summary>
    Task<MPInstallationState> GetInstallationStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 标记本次安装已经启动过登录流程。
    /// </summary>
    Task MarkLoginFlowStartedAsync(CancellationToken cancellationToken = default);
}

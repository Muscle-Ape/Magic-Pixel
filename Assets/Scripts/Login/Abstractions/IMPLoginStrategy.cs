using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 单一登录方式策略接口。
/// 每种登录方式只负责自己的参数校验和登录执行。
/// </summary>
public interface IMPLoginStrategy
{
    /// <summary>
    /// 当前策略支持的登录类型。
    /// </summary>
    MPLoginType LoginType { get; }

    /// <summary>
    /// 执行当前策略对应的登录流程。
    /// </summary>
    Task<MPLoginResult> LoginAsync(MPLoginRequest request, CancellationToken cancellationToken = default);
}

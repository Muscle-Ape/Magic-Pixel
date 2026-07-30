/// <summary>
/// 登录模块默认依赖集合。
/// 用于把 Core、Flow、Persistence 和 Config 集中装配，避免 Facade 里散落 new 逻辑。
/// </summary>
public sealed class MPLoginServiceContainer
{
    /// <summary>核心登录管理器。</summary>
    public IMPLoginManager loginManager;

    /// <summary>启动登录流程控制器。</summary>
    public IMPLoginFlowController flowController;

    /// <summary>本地登录资料仓储。</summary>
    public IMPLocalLoginRepository localLoginRepository;

    /// <summary>安装状态服务。</summary>
    public IMPInstallationService installationService;

    /// <summary>登录模块配置。</summary>
    public MPLoginConfiguration configuration;
}

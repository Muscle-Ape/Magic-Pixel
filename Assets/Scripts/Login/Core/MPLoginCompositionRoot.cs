/// <summary>
/// 登录模块默认装配入口。
/// 所有 Strategy、Adapter、AuthApi 的依赖关系集中在这里，避免散落在业务代码中。
/// </summary>
public static class MPLoginCompositionRoot
{
    /// <summary>
    /// 创建当前项目默认使用的登录管理器实例。
    /// </summary>
    public static IMPLoginManager CreateDefault()
    {
        return CreateDefaultServices().loginManager;
    }

    /// <summary>
    /// 创建登录模块默认依赖集合。
    /// </summary>
    public static MPLoginServiceContainer CreateDefaultServices()
    {
        IMPAuthApi authApi = new MPUnityAuthenticationApi();
        IMPSessionService sessionService = new MPSessionService();
        IMPLocalLoginRepository localLoginRepository = new MPEasySaveLocalLoginRepository();
        IMPInstallationService installationService = new MPEasySaveInstallationService();
        MPLoginConfiguration configuration = MPLoginConfiguration.LoadOrCreateDefault();

        IMPThirdPartyAuthAdapterFactory adapterFactory = new MPThirdPartyAuthAdapterFactory(new IMPThirdPartyAuthAdapter[]
        {
            new MPGoogleAuthAdapter(),
            new MPGooglePlayGamesAuthAdapter(),
            new MPAppleAuthAdapter(),
            new MPFacebookAuthAdapter()
        });

        IMPLoginStrategyFactory strategyFactory = new MPLoginStrategyFactory(new IMPLoginStrategy[]
        {
            new MPGuestLoginStrategy(authApi),
            new MPPasswordLoginStrategy(authApi),
            new MPThirdPartyLoginStrategy(MPLoginType.Google, adapterFactory, authApi),
            new MPThirdPartyLoginStrategy(MPLoginType.GooglePlayGames, adapterFactory, authApi),
            new MPThirdPartyLoginStrategy(MPLoginType.Apple, adapterFactory, authApi),
            new MPThirdPartyLoginStrategy(MPLoginType.Facebook, adapterFactory, authApi)
        });

        IMPLoginManager loginManager = new MPLoginManagerCore(strategyFactory, adapterFactory, authApi, sessionService);
        IMPLoginFlowController flowController = new MPLoginFlowController(loginManager, localLoginRepository, installationService, configuration);

        return new MPLoginServiceContainer
        {
            loginManager = loginManager,
            flowController = flowController,
            localLoginRepository = localLoginRepository,
            installationService = installationService,
            configuration = configuration
        };
    }
}

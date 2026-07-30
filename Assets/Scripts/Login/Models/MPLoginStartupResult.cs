/// <summary>
/// 启动登录流程的统一结果。
/// 场景管理器只需要根据 action 决定进入游戏、展示重试页或展示登录选择页。
/// </summary>
public sealed class MPLoginStartupResult
{
    /// <summary>建议 UI 或场景管理器执行的下一步动作。</summary>
    public MPLoginStartupAction action;

    /// <summary>本次启动流程产生的登录结果，未登录时可能为 null。</summary>
    public MPLoginResult loginResult;

    /// <summary>本次启动流程的错误信息，成功时为 null。</summary>
    public MPLoginError error;

    /// <summary>推荐优先展示的登录提供方。</summary>
    public MPLoginProvider preferredProvider;

    /// <summary>本地登录资料快照，供 UI 展示账号状态或恢复提示。</summary>
    public MPLocalLoginProfile localProfile;

    /// <summary>是否允许用户手动创建新的游客账号。</summary>
    public bool canCreateNewGuest;

    /// <summary>给临时登录页展示的简短说明。</summary>
    public string message;

    /// <summary>
    /// 创建进入游戏结果。
    /// </summary>
    public static MPLoginStartupResult EnterGame(MPLoginResult loginResult, MPLocalLoginProfile localProfile = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.EnterGame,
            loginResult = loginResult,
            localProfile = localProfile,
            preferredProvider = localProfile == null ? MPLoginProvider.Unknown : localProfile.lastLoginProvider,
            canCreateNewGuest = false,
            message = "登录已恢复。"
        };
    }

    /// <summary>
    /// 创建网络重试结果。
    /// </summary>
    public static MPLoginStartupResult ShowNetworkRetry(MPLoginError error, MPLocalLoginProfile localProfile = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.ShowNetworkRetry,
            error = error,
            localProfile = localProfile,
            preferredProvider = localProfile == null ? MPLoginProvider.Unknown : localProfile.lastLoginProvider,
            canCreateNewGuest = false,
            message = error == null ? "网络暂时不可用，请重试。" : error.message
        };
    }

    /// <summary>
    /// 创建登录选择结果。
    /// </summary>
    public static MPLoginStartupResult ShowLoginSelection(MPLocalLoginProfile localProfile = null, MPLoginProvider preferredProvider = MPLoginProvider.Unknown, string message = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.ShowLoginSelection,
            localProfile = localProfile,
            preferredProvider = preferredProvider,
            canCreateNewGuest = true,
            message = string.IsNullOrEmpty(message) ? "请选择登录方式。" : message
        };
    }

    /// <summary>
    /// 创建匿名恢复结果。
    /// </summary>
    public static MPLoginStartupResult ShowAnonymousRecovery(MPLoginError error, MPLocalLoginProfile localProfile, bool canCreateNewGuest)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.ShowAnonymousRecovery,
            error = error,
            localProfile = localProfile,
            preferredProvider = MPLoginProvider.Anonymous,
            canCreateNewGuest = canCreateNewGuest,
            message = error == null ? "游客账号恢复失败。" : error.message
        };
    }

    /// <summary>
    /// 创建维护提示结果。
    /// </summary>
    public static MPLoginStartupResult ShowMaintenance(MPLoginError error, MPLocalLoginProfile localProfile = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.ShowMaintenance,
            error = error,
            localProfile = localProfile,
            preferredProvider = localProfile == null ? MPLoginProvider.Unknown : localProfile.lastLoginProvider,
            canCreateNewGuest = false,
            message = error == null ? "服务器维护中，请稍后再试。" : error.message
        };
    }

    /// <summary>
    /// 创建账号不可用结果。
    /// </summary>
    public static MPLoginStartupResult ShowAccountDisabled(MPLoginError error, MPLocalLoginProfile localProfile = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.ShowAccountDisabled,
            error = error,
            localProfile = localProfile,
            preferredProvider = localProfile == null ? MPLoginProvider.Unknown : localProfile.lastLoginProvider,
            canCreateNewGuest = false,
            message = error == null ? "当前账号暂不可用。" : error.message
        };
    }

    /// <summary>
    /// 创建普通失败结果。
    /// </summary>
    public static MPLoginStartupResult Failed(MPLoginError error, MPLocalLoginProfile localProfile = null)
    {
        return new MPLoginStartupResult
        {
            action = MPLoginStartupAction.Failed,
            error = error,
            localProfile = localProfile,
            preferredProvider = localProfile == null ? MPLoginProvider.Unknown : localProfile.lastLoginProvider,
            canCreateNewGuest = false,
            message = error == null ? "登录失败，请稍后重试。" : error.message
        };
    }
}

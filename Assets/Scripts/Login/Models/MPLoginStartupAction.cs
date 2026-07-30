/// <summary>
/// 启动登录流程完成后建议 UI 或场景管理器执行的下一步动作。
/// </summary>
public enum MPLoginStartupAction
{
    /// <summary>没有明确动作。</summary>
    None = 0,
    /// <summary>已完成登录或恢复，可以进入游戏。</summary>
    EnterGame = 1,
    /// <summary>网络或临时错误，需要展示重试页面。</summary>
    ShowNetworkRetry = 2,
    /// <summary>需要展示登录选择页面。</summary>
    ShowLoginSelection = 3,
    /// <summary>匿名账号恢复失败，需要展示匿名恢复页面。</summary>
    ShowAnonymousRecovery = 4,
    /// <summary>服务维护或临时不可用，需要展示维护提示。</summary>
    ShowMaintenance = 5,
    /// <summary>账号不可用，例如封禁、删除或被服务端拒绝。</summary>
    ShowAccountDisabled = 6,
    /// <summary>流程失败且没有更具体的 UI 分支。</summary>
    Failed = 7
}

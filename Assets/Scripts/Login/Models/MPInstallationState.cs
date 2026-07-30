/// <summary>
/// 客户端安装状态，用于判断本地资料缺失时是否可以安全地自动创建新游客账号。
/// </summary>
public enum MPInstallationState
{
    /// <summary>无法确认是否为首次安装。</summary>
    Unknown = 0,
    /// <summary>没有任何历史启动或登录记录，可按首次安装处理。</summary>
    FirstInstall = 1,
    /// <summary>检测到历史启动或登录记录，不应静默创建新游客账号。</summary>
    ExistingInstall = 2
}

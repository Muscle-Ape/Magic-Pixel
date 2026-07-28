/// <summary>
/// Session 存取服务，集中管理当前内存会话。
/// 后续如需接入持久化或服务器 Session，可从这里扩展。
/// </summary>
public interface IMPSessionService
{
    /// <summary>
    /// 当前内存 Session。
    /// </summary>
    MPUserSession CurrentSession { get; }

    /// <summary>
    /// 设置当前 Session。
    /// </summary>
    void SetSession(MPUserSession session);

    /// <summary>
    /// 清理当前 Session。
    /// </summary>
    void Clear();
}

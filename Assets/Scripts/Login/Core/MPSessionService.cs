/// <summary>
/// 当前 Session 的内存存储服务。
/// 暂不把 token 写入本地存档，避免敏感信息扩散。
/// </summary>
public class MPSessionService : IMPSessionService
{
    /// <summary>
    /// 当前内存中的登录会话。
    /// </summary>
    public MPUserSession CurrentSession { get; private set; }

    public void SetSession(MPUserSession session)
    {
        CurrentSession = session;
    }

    public void Clear()
    {
        CurrentSession = null;
    }
}

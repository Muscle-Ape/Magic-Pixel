/// <summary>
/// 登录、注册、绑定等操作的统一返回结果。
/// UI 层只需要读取 isSuccess、session 或 error/errorMessage 即可展示结果。
/// </summary>
public class MPLoginResult
{
    /// <summary>操作是否成功。</summary>
    public bool isSuccess;
    /// <summary>本次操作对应的登录方式。</summary>
    public MPLoginType loginType;
    /// <summary>成功时的 PlayerId 快捷字段。</summary>
    public string playerId;
    /// <summary>成功时的玩家名快捷字段。</summary>
    public string playerName;
    /// <summary>成功时的用户名快捷字段。</summary>
    public string username;
    /// <summary>成功时的 Unity Authentication Profile。</summary>
    public string profile;
    /// <summary>失败时的错误码快捷字段。</summary>
    public int errorCode;
    /// <summary>失败时的错误消息快捷字段。</summary>
    public string errorMessage;
    /// <summary>成功时的完整会话。</summary>
    public MPUserSession session;
    /// <summary>失败时的结构化错误信息。</summary>
    public MPLoginError error;
    /// <summary>预留字段：后续接服务器时可标记是否新用户。</summary>
    public bool isNewUser;
    /// <summary>预留字段：后续可用于提示游客绑定正式账号。</summary>
    public bool requiresAccountBinding;

    /// <summary>
    /// 构造成功结果，并把常用 Session 字段复制到结果快捷字段上。
    /// </summary>
    public static MPLoginResult Success(MPUserSession session, bool isNewUser = false)
    {
        return new MPLoginResult
        {
            isSuccess = true,
            loginType = session == null ? MPLoginType.None : session.loginType,
            playerId = session == null ? string.Empty : session.userId,
            playerName = session == null ? string.Empty : session.playerName,
            username = session == null ? string.Empty : session.username,
            profile = session == null ? string.Empty : session.profile,
            session = session,
            error = null,
            errorCode = 0,
            errorMessage = string.Empty,
            isNewUser = isNewUser,
            requiresAccountBinding = false
        };
    }

    /// <summary>
    /// 构造失败结果，并保留结构化错误信息供 UI 展示或重试判断。
    /// </summary>
    public static MPLoginResult Failed(MPLoginType loginType, MPLoginError error)
    {
        return new MPLoginResult
        {
            isSuccess = false,
            loginType = loginType,
            playerId = string.Empty,
            playerName = string.Empty,
            username = string.Empty,
            profile = string.Empty,
            session = null,
            error = error,
            errorCode = error == null ? 0 : error.serviceErrorCode,
            errorMessage = error == null ? string.Empty : error.message,
            isNewUser = false,
            requiresAccountBinding = false
        };
    }
}

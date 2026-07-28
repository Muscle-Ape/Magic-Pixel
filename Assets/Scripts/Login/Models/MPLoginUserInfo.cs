/// <summary>
/// 给旧业务代码或 UI 使用的轻量用户信息视图。
/// 完整认证信息请读取 MPUserSession。
/// </summary>
public class MPLoginUserInfo
{
    /// <summary>
    /// 当前用户的登录方式。
    /// </summary>
    public MPLoginType loginType;

    /// <summary>
    /// Unity Authentication PlayerId。
    /// </summary>
    public string playerId;

    /// <summary>
    /// 当前玩家名。
    /// </summary>
    public string playerName;

    /// <summary>
    /// 账号密码用户的用户名。
    /// </summary>
    public string username;

    /// <summary>
    /// Unity Authentication Profile。
    /// </summary>
    public string profile;

    public MPLoginUserInfo(MPLoginType loginType, string playerId, string playerName, string username, string profile)
    {
        this.loginType = loginType;
        this.playerId = playerId;
        this.playerName = playerName;
        this.username = username;
        this.profile = profile;
    }

    /// <summary>
    /// 从当前 Session 转换成轻量用户信息。
    /// </summary>
    public static MPLoginUserInfo FromSession(MPUserSession session)
    {
        if (session == null)
        {
            return null;
        }

        return new MPLoginUserInfo(
            session.loginType,
            session.userId,
            session.playerName,
            session.username,
            session.profile);
    }
}

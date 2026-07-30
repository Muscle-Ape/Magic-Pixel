/// <summary>
/// 最近一次使用或推荐展示的登录提供方。
/// 与 MPLoginType 相比，它更偏向本地偏好和 UI 排序。
/// </summary>
public enum MPLoginProvider
{
    /// <summary>没有记录或无法判断。</summary>
    Unknown = 0,
    /// <summary>游客/匿名登录。</summary>
    Anonymous = 1,
    /// <summary>Unity Authentication 账号密码。</summary>
    UsernamePassword = 2,
    /// <summary>Google 登录。</summary>
    Google = 3,
    /// <summary>Google Play Games 登录。</summary>
    GooglePlayGames = 4,
    /// <summary>Apple 登录。</summary>
    Apple = 5,
    /// <summary>Facebook 登录。</summary>
    Facebook = 6
}

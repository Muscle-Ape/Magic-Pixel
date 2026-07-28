/// <summary>
/// 项目支持的登录方式。别名用于兼容旧代码里的命名。
/// </summary>
public enum MPLoginType
{
    /// <summary>未指定登录方式。</summary>
    None = 0,
    /// <summary>游客登录。</summary>
    Guest = 1,
    /// <summary>游客登录别名，兼容 Unity Authentication 的匿名登录叫法。</summary>
    Anonymous = Guest,
    /// <summary>Unity Authentication 账号密码登录。</summary>
    UsernamePassword = 2,
    /// <summary>账号密码登录别名。</summary>
    Password = UsernamePassword,
    /// <summary>Google 登录。</summary>
    Google = 3,
    /// <summary>Google Play Games 登录。</summary>
    GooglePlayGames = 4,
    /// <summary>Apple 登录。</summary>
    Apple = 5,
    /// <summary>Facebook 登录。</summary>
    Facebook = 6
}

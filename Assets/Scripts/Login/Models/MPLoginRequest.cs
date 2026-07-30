/// <summary>
/// 所有登录请求的基类。不同登录方式用派生类承载各自需要的参数。
/// </summary>
public abstract class MPLoginRequest
{
    /// <summary>
    /// 本次请求对应的登录方式。
    /// </summary>
    public MPLoginType loginType;
}

/// <summary>
/// 游客/匿名登录请求。预留设备信息，后续如需风控或埋点可在这里补充。
/// </summary>
public class MPGuestLoginRequest : MPLoginRequest
{
    /// <summary>
    /// 客户端持久化的匿名身份 Id。
    /// 当前 Unity Authentication 不直接使用，预留给后续游戏服务器匿名恢复接口。
    /// </summary>
    public string anonymousId;

    /// <summary>
    /// 当前安装实例 Id。
    /// 用于后续服务端判断同一安装上的匿名账号恢复和风控。
    /// </summary>
    public string installationId;

    /// <summary>
    /// 匿名登录幂等键。
    /// 请求超时重试时复用同一个键，避免服务端创建多个匿名账号。
    /// </summary>
    public string idempotencyKey;

    /// <summary>
    /// 设备唯一标识，当前预留，后续可用于风控或数据分析。
    /// </summary>
    public string deviceId;

    /// <summary>
    /// 设备型号，当前预留。
    /// </summary>
    public string deviceModel;

    /// <summary>
    /// 操作系统信息，当前预留。
    /// </summary>
    public string operatingSystem;

    public MPGuestLoginRequest()
    {
        loginType = MPLoginType.Guest;
    }
}

/// <summary>
/// 账号密码请求的具体操作类型。
/// </summary>
public enum MPPasswordLoginMode
{
    /// <summary>登录已有账号。</summary>
    Login,
    /// <summary>注册新账号。</summary>
    Register,
    /// <summary>给当前游客或已有账号绑定账号密码。</summary>
    AddToCurrentUser,
    /// <summary>修改当前账号密码用户的密码。</summary>
    UpdatePassword
}

/// <summary>
/// Unity Authentication 账号密码登录、注册、绑定和改密请求。
/// </summary>
public class MPPasswordLoginRequest : MPLoginRequest
{
    /// <summary>用户名或账号。</summary>
    public string account;
    /// <summary>登录/注册/绑定时的密码，改密时表示新密码。</summary>
    public string password;
    /// <summary>改密时需要提供的当前密码。</summary>
    public string currentPassword;
    /// <summary>本次账号密码请求的操作类型。</summary>
    public MPPasswordLoginMode mode;

    public MPPasswordLoginRequest()
    {
        loginType = MPLoginType.UsernamePassword;
        mode = MPPasswordLoginMode.Login;
    }
}

/// <summary>
/// 第三方登录请求。SDK 获取 token/authCode 的过程由 Adapter 或外部平台层处理。
/// </summary>
public class MPThirdPartyLoginRequest : MPLoginRequest
{
    /// <summary>第三方平台类型。</summary>
    public MPLoginType provider;
    /// <summary>授权码，例如 Google Play Games 使用 Auth Code。</summary>
    public string authorizationCode;
    /// <summary>访问令牌，例如 Facebook 使用 Access Token。</summary>
    public string accessToken;
    /// <summary>身份令牌，例如 Google 和 Apple 使用 Identity Token。</summary>
    public string identityToken;
    /// <summary>平台侧用户 Id，当前仅透传，方便后续埋点或账号展示。</summary>
    public string platformUserId;
    /// <summary>第三方登录时，如果 Unity 侧没有账号，是否自动创建。</summary>
    public bool createAccount = true;
    /// <summary>绑定第三方账号时是否强制转移绑定关系。</summary>
    public bool forceLink;

    public MPThirdPartyLoginRequest()
    {
        loginType = MPLoginType.None;
    }
}

using System;

/// <summary>
/// 登录成功后保存在内存中的用户会话。
/// 注意：token 仅用于内部认证状态同步，不要打印到日志或写入普通存档。
/// </summary>
public class MPUserSession
{
    /// <summary>Unity Authentication PlayerId。</summary>
    public string userId;
    /// <summary>Unity Player Name，可为空。</summary>
    public string playerName;
    /// <summary>账号密码用户的用户名，可为空。</summary>
    public string username;
    /// <summary>Unity Authentication AccessToken，禁止输出到日志。</summary>
    public string accessToken;
    /// <summary>Unity Authentication SessionToken，禁止输出到日志。</summary>
    public string sessionToken;
    /// <summary>业务层记录的 AccessToken 过期时间；当前由 Unity SDK 自己维护刷新。</summary>
    public DateTime accessTokenExpiresAtUtc;
    /// <summary>本次会话来源的登录方式。</summary>
    public MPLoginType loginType;
    /// <summary>Unity Authentication Profile，用于隔离本地凭证。</summary>
    public string profile;
    /// <summary>是否游客/匿名账号。</summary>
    public bool isGuest;

    /// <summary>
    /// 判断业务层记录的 AccessToken 是否过期。
    /// 当前 Unity SDK 内部刷新 token，因此 MaxValue 表示业务层不主动判过期。
    /// </summary>
    public bool IsAccessTokenExpired
    {
        get
        {
            return accessTokenExpiresAtUtc != DateTime.MinValue &&
                   accessTokenExpiresAtUtc != DateTime.MaxValue &&
                   DateTime.UtcNow >= accessTokenExpiresAtUtc;
        }
    }
}

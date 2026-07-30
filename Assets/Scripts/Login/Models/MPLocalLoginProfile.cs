using System;

/// <summary>
/// 本地登录资料。
/// 只保存账号恢复和 UI 决策需要的轻量信息，不直接保存 AccessToken 或 SessionToken 明文。
/// </summary>
[Serializable]
public sealed class MPLocalLoginProfile
{
    /// <summary>最近一次成功登录的 Unity Authentication PlayerId。</summary>
    public string playerId;

    /// <summary>客户端生成并持久化的匿名身份 Id，用于后续接入服务端匿名恢复。</summary>
    public string anonymousId;

    /// <summary>游戏服务器 RefreshToken 预留字段；当前仅接 Unity Authentication 时保持为空。</summary>
    public string refreshToken;

    /// <summary>当前 Unity Authentication Profile 名称。</summary>
    public string unityProfile;

    /// <summary>当前安装实例 Id，用于匿名恢复和幂等请求。</summary>
    public string installationId;

    /// <summary>匿名登录请求幂等键。发起匿名请求前生成并保存，重试时复用。</summary>
    public string anonymousIdempotencyKey;

    /// <summary>最近一次登录提供方。</summary>
    public MPLoginProvider lastLoginProvider;

    /// <summary>当前游戏账号类型。</summary>
    public MPAccountType accountType;

    /// <summary>是否已绑定至少一种正式登录身份。</summary>
    public bool hasBoundIdentity;

    /// <summary>是否已绑定 Unity Authentication 账号密码身份。</summary>
    public bool hasUsernamePasswordBinding;

    /// <summary>是否已绑定 Google 身份。</summary>
    public bool hasGoogleBinding;

    /// <summary>是否已绑定 Google Play Games 身份。</summary>
    public bool hasGooglePlayGamesBinding;

    /// <summary>是否已绑定 Apple 身份。</summary>
    public bool hasAppleBinding;

    /// <summary>是否已绑定 Facebook 身份。</summary>
    public bool hasFacebookBinding;

    /// <summary>是否检测到 Unity Authentication 本地 SessionToken 存在。</summary>
    public bool hasUnitySessionToken;

    /// <summary>最近一次更新本地资料的 UTC ticks。</summary>
    public long updatedAtUtcTicks;

    /// <summary>是否存在任何历史账号线索。</summary>
    public bool HasAnyHistory
    {
        get
        {
            // anonymousId 会在发起匿名登录前预生成，用于后续服务端幂等；
            // 仅有 anonymousId 不代表 Unity Authentication 已经创建过玩家账号。
            return !string.IsNullOrEmpty(playerId) ||
                   !string.IsNullOrEmpty(refreshToken) ||
                   hasUnitySessionToken ||
                   HasBoundProviderHistory ||
                   (!string.IsNullOrEmpty(unityProfile) && lastLoginProvider != MPLoginProvider.Unknown);
        }
    }

    /// <summary>是否存在可用于匿名恢复的本地资料。</summary>
    public bool HasAnonymousRecoveryData
    {
        get
        {
            // 有 PlayerId 才说明 Unity 侧已经创建过玩家；
            // 或者仍检测到 SessionToken，说明当前 Profile 里还有 SDK 可恢复的凭证。
            return !string.IsNullOrEmpty(playerId) ||
                   (hasUnitySessionToken && !string.IsNullOrEmpty(anonymousId));
        }
    }

    /// <summary>是否存在正式账号绑定历史。</summary>
    private bool HasBoundProviderHistory
    {
        get
        {
            return hasBoundIdentity ||
                   hasUsernamePasswordBinding ||
                   hasGoogleBinding ||
                   hasGooglePlayGamesBinding ||
                   hasAppleBinding ||
                   hasFacebookBinding ||
                   lastLoginProvider == MPLoginProvider.UsernamePassword ||
                   lastLoginProvider == MPLoginProvider.Google ||
                   lastLoginProvider == MPLoginProvider.GooglePlayGames ||
                   lastLoginProvider == MPLoginProvider.Apple ||
                   lastLoginProvider == MPLoginProvider.Facebook;
        }
    }

    /// <summary>
    /// 使用登录成功后的 Session 刷新本地资料。
    /// </summary>
    public void ApplySession(MPUserSession session, MPLoginProvider provider, bool markAsBound)
    {
        if (session == null)
        {
            return;
        }

        playerId = session.userId;
        unityProfile = session.profile;
        hasUnitySessionToken = !string.IsNullOrEmpty(session.sessionToken);
        lastLoginProvider = provider == MPLoginProvider.Unknown ? lastLoginProvider : provider;

        if (markAsBound)
        {
            hasBoundIdentity = true;
        }

        ApplyBindingFlag(provider);
        accountType = hasBoundIdentity ? MPAccountType.Bound : MPAccountType.Anonymous;
        updatedAtUtcTicks = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// 标记指定登录提供方已绑定到当前账号。
    /// </summary>
    public void ApplyBindingFlag(MPLoginProvider provider)
    {
        switch (provider)
        {
            case MPLoginProvider.UsernamePassword:
                hasUsernamePasswordBinding = true;
                hasBoundIdentity = true;
                break;
            case MPLoginProvider.Google:
                hasGoogleBinding = true;
                hasBoundIdentity = true;
                break;
            case MPLoginProvider.GooglePlayGames:
                hasGooglePlayGamesBinding = true;
                hasBoundIdentity = true;
                break;
            case MPLoginProvider.Apple:
                hasAppleBinding = true;
                hasBoundIdentity = true;
                break;
            case MPLoginProvider.Facebook:
                hasFacebookBinding = true;
                hasBoundIdentity = true;
                break;
        }
    }
}

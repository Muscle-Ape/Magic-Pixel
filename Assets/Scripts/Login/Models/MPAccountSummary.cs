using System;

/// <summary>
/// 账号冲突页可展示的账号摘要。
/// 当前先作为客户端模型预留，后续接游戏服务器后由服务端返回真实数据。
/// </summary>
[Serializable]
public sealed class MPAccountSummary
{
    /// <summary>账号 PlayerId。</summary>
    public string playerId;

    /// <summary>展示用昵称。</summary>
    public string displayName;

    /// <summary>玩家等级。</summary>
    public int level;

    /// <summary>累计经验，用于计算等级和级内进度；未提供时沿用 level 并展示空进度。</summary>
    public int? totalExperience;

    /// <summary>账号创建时间的 UTC ticks。</summary>
    public long createdAtUtcTicks;

    /// <summary>最近登录方式。</summary>
    public MPLoginProvider provider;
}

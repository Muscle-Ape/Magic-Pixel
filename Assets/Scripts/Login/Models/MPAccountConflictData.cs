using System;

/// <summary>
/// 第三方账号绑定冲突数据。
/// 当前作为后续服务端账号冲突流程的统一数据结构预留。
/// </summary>
[Serializable]
public sealed class MPAccountConflictData
{
    /// <summary>当前设备正在使用的账号摘要。</summary>
    public MPAccountSummary currentAccount;

    /// <summary>第三方身份已经绑定的已有账号摘要。</summary>
    public MPAccountSummary existingAccount;

    /// <summary>服务端返回的冲突处理令牌，用于用户确认切换账号时二次提交。</summary>
    public string conflictToken;
}

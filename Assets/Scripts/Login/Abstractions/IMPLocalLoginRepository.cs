using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 本地登录资料仓储接口。
/// 登录策略只依赖接口，默认实现使用 ES3，后续可以替换为系统安全存储或加密存储。
/// </summary>
public interface IMPLocalLoginRepository
{
    /// <summary>
    /// 读取本地登录资料。
    /// </summary>
    Task<MPLocalLoginProfile> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存本地登录资料。
    /// </summary>
    Task SaveAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken = default);

    /// <summary>独立游客的资料槽；切换第三方账号时仍保留，不存储明文 Token。</summary>
    Task<MPLocalLoginProfile> LoadGuestProfileAsync(CancellationToken cancellationToken = default);
    Task SaveGuestProfileAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理活动会话资料。
    /// keepRecoveryData 为 true 时保留 AnonymousId、历史 PlayerId 和最近登录方式，避免误丢账号恢复线索。
    /// </summary>
    Task ClearActiveSessionAsync(bool keepRecoveryData, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建当前安装实例 Id。
    /// </summary>
    Task<string> GetOrCreateInstallationIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建匿名账号 Id。
    /// </summary>
    Task<string> GetOrCreateAnonymousIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新生成匿名账号 Id，只有用户明确创建新游客账号时才应调用。
    /// </summary>
    Task<string> ResetAnonymousIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取或创建匿名登录幂等键。
    /// </summary>
    Task<string> GetOrCreateAnonymousIdempotencyKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新生成匿名登录幂等键，用于用户明确创建新的游客账号。
    /// </summary>
    Task<string> ResetAnonymousIdempotencyKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在任意历史登录线索。
    /// </summary>
    Task<bool> HasAnyLoginHistoryAsync(CancellationToken cancellationToken = default);
}

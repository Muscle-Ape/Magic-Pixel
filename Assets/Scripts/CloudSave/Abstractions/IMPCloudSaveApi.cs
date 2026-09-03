using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Cloud Save SDK 访问抽象，隔离 Unity SDK 具体调用方式。
/// </summary>
public interface IMPCloudSaveApi
{
    /// <summary>同一个带写锁请求提交主存档和自定义存档，避免客户端分两次覆盖。</summary>
    Task<Dictionary<string, string>> SaveSnapshotPairAsync(MPUserCloudSnapshot user, string userWriteLock,
        MPCustomLevelCloudSnapshot custom, string customWriteLock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取当前登录玩家的单个 Player Data。
    /// </summary>
    /// <typeparam name="T">目标数据类型。</typeparam>
    /// <param name="key">Cloud Save Player Data Key。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>读取结果，包含数据、是否存在以及写锁。</returns>
    Task<MPCloudSaveLoadResult<T>> LoadPlayerDataAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存当前登录玩家的单个 Player Data。
    /// </summary>
    /// <typeparam name="T">保存的数据类型。</typeparam>
    /// <param name="key">Cloud Save Player Data Key。</param>
    /// <param name="value">需要保存的数据。</param>
    /// <param name="writeLock">上一次读取或保存得到的写锁。</param>
    /// <param name="useWriteLock">是否启用写锁冲突校验。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保存成功后新的写锁。</returns>
    Task<string> SavePlayerDataAsync<T>(string key, T value, string writeLock, bool useWriteLock, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存当前登录玩家的文件数据。
    /// </summary>
    /// <param name="key">Cloud Save Files Key。</param>
    /// <param name="bytes">文件字节内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SavePlayerFileAsync(string key, byte[] bytes, CancellationToken cancellationToken = default);
}

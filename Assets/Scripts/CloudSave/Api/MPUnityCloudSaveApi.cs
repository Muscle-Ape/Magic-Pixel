using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Internal.Http;
using Unity.Services.CloudSave.Models;

/// <summary>
/// 基于 Unity Cloud Save SDK 的云存储访问实现。
/// </summary>
public class MPUnityCloudSaveApi : IMPCloudSaveApi
{
    public async Task<Dictionary<string, string>> SaveSnapshotPairAsync(MPUserCloudSnapshot user, string userWriteLock,
        MPCustomLevelCloudSnapshot custom, string customWriteLock, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem>
        {
            { MPCloudSaveConstants.USER_SNAPSHOT_KEY, new SaveItem(user, userWriteLock) },
            { MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY, new SaveItem(custom, customWriteLock) }
        });
    }

    /// <inheritdoc />
    public async Task<MPCloudSaveLoadResult<T>> LoadPlayerDataAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, Item> result = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>
        {
            key
        });

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.TryGetValue(key, out Item item) || item == null)
        {
            return MPCloudSaveLoadResult<T>.Missing();
        }

        return new MPCloudSaveLoadResult<T>
        {
            exists = true,
            // 云存档可能包含旧版本已经移除的字段。忽略未知成员可以保证
            // DTO 精简或版本升级后仍能读取已有快照，字段类型错误仍会正常抛出。
            value = item.Value.GetAs<T>(new DeserializationSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            }),
            writeLock = item.WriteLock,
            created = item.Created,
            modified = item.Modified
        };
    }

    /// <inheritdoc />
    public async Task<string> SavePlayerDataAsync<T>(string key, T value, string writeLock, bool useWriteLock, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, string> result;
        if (useWriteLock)
        {
            Dictionary<string, SaveItem> data = new Dictionary<string, SaveItem>
            {
                { key, new SaveItem(value, writeLock) }
            };
            result = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }
        else
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { key, value }
            };
            result = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.TryGetValue(key, out string newWriteLock) ? newWriteLock : string.Empty;
    }

    /// <inheritdoc />
    public async Task SavePlayerFileAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CloudSaveService.Instance.Files.Player.SaveAsync(key, bytes);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

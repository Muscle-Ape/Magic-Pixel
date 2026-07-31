using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

/// <summary>
/// 基于 Unity Cloud Save SDK 的云存储访问实现。
/// </summary>
public class MPUnityCloudSaveApi : IMPCloudSaveApi
{
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
            value = item.Value.GetAs<T>(),
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YooAsset;

/// <summary>
/// YooAsset 资源加载封装。
/// 按持有者管理资源句柄，持有者释放时必须调用 <see cref="ReleaseAll"/>。
/// </summary>
public static class MPLoad
{
    private static readonly Dictionary<int, Dictionary<string, AssetHandle>> m_ownerHandles =
        new Dictionary<int, Dictionary<string, AssetHandle>>();

    /// <summary>
    /// 同步加载资源，句柄会与持有者绑定。
    /// 同一持有者重复加载同一资源时会复用已有句柄。
    /// </summary>
    public static T Load<T>(string location, UnityEngine.Object owner) where T : UnityEngine.Object
    {
        ValidateArguments(location, owner);

        string key = CreateKey<T>(location);
        if (TryGetLoadedAsset(owner, key, out T cachedAsset))
        {
            return cachedAsset;
        }

        AssetHandle handle = YooAssets.LoadAssetSync<T>(location);
        return RegisterLoadedHandle<T>(location, owner, key, handle);
    }

    /// <summary>
    /// 异步加载资源并等待 YooAsset 操作完成。
    /// 持有者在等待期间被销毁时会释放句柄并返回 null。
    /// </summary>
    public static async Task<T> LoadAsync<T>(
        string location,
        UnityEngine.Object owner,
        CancellationToken cancellationToken = default) where T : UnityEngine.Object
    {
        ValidateArguments(location, owner);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CreateKey<T>(location);
        if (TryGetLoadedAsset(owner, key, out T cachedAsset))
        {
            return cachedAsset;
        }

        AssetHandle handle = YooAssets.LoadAssetAsync<T>(location);
        try
        {
            await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();

            if (owner == null)
            {
                handle.Release();
                return null;
            }

            return RegisterLoadedHandle<T>(location, owner, key, handle);
        }
        catch
        {
            if (handle.IsValid)
            {
                handle.Release();
            }

            throw;
        }
    }

    /// <summary>
    /// 同步加载仅在当前代码块内使用的短生命周期资源。
    /// 返回值必须通过 using 或 Dispose 释放。
    /// </summary>
    public static MPAssetLoadLease<T> LoadLease<T>(string location) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("资源地址不能为空。", nameof(location));
        }

        AssetHandle handle = YooAssets.LoadAssetSync<T>(location);
        T asset = GetLoadedAsset<T>(location, handle);
        return new MPAssetLoadLease<T>(handle, asset);
    }

    /// <summary>
    /// 释放指定持有者通过 MPLoad 加载的全部资源句柄。
    /// </summary>
    public static void ReleaseAll(UnityEngine.Object owner)
    {
        if (owner == null)
        {
            return;
        }

        int ownerId = owner.GetInstanceID();
        if (!m_ownerHandles.TryGetValue(ownerId, out Dictionary<string, AssetHandle> handles))
        {
            return;
        }

        foreach (AssetHandle handle in handles.Values)
        {
            if (handle != null && handle.IsValid)
            {
                handle.Release();
            }
        }

        handles.Clear();
        m_ownerHandles.Remove(ownerId);
    }

    private static T RegisterLoadedHandle<T>(
        string location,
        UnityEngine.Object owner,
        string key,
        AssetHandle handle) where T : UnityEngine.Object
    {
        T asset = GetLoadedAsset<T>(location, handle);
        int ownerId = owner.GetInstanceID();
        if (!m_ownerHandles.TryGetValue(ownerId, out Dictionary<string, AssetHandle> handles))
        {
            handles = new Dictionary<string, AssetHandle>();
            m_ownerHandles.Add(ownerId, handles);
        }

        if (handles.TryGetValue(key, out AssetHandle existingHandle))
        {
            T existingAsset = existingHandle != null && existingHandle.IsValid
                ? existingHandle.AssetObject as T
                : null;
            if (existingAsset != null)
            {
                handle.Release();
                return existingAsset;
            }

            if (existingHandle != null && existingHandle.IsValid)
            {
                existingHandle.Release();
            }
        }

        handles[key] = handle;
        return asset;
    }

    private static T GetLoadedAsset<T>(string location, AssetHandle handle) where T : UnityEngine.Object
    {
        if (handle == null)
        {
            throw new InvalidOperationException($"YooAsset 未返回有效句柄：{location}");
        }

        if (handle.Status != EOperationStatus.Succeed)
        {
            string error = handle.LastError;
            handle.Release();
            throw new InvalidOperationException($"资源加载失败：{location}，{error}");
        }

        T asset = handle.AssetObject as T;
        if (asset == null)
        {
            handle.Release();
            throw new InvalidOperationException($"资源类型不匹配或资源为空：{location}，期望类型 {typeof(T).Name}");
        }

        return asset;
    }

    private static bool TryGetLoadedAsset<T>(
        UnityEngine.Object owner,
        string key,
        out T asset) where T : UnityEngine.Object
    {
        asset = null;
        int ownerId = owner.GetInstanceID();
        if (!m_ownerHandles.TryGetValue(ownerId, out Dictionary<string, AssetHandle> handles) ||
            !handles.TryGetValue(key, out AssetHandle handle))
        {
            return false;
        }

        if (handle == null || !handle.IsValid)
        {
            handles.Remove(key);
            if (handles.Count == 0)
            {
                m_ownerHandles.Remove(ownerId);
            }

            return false;
        }

        asset = handle.AssetObject as T;
        return asset != null;
    }

    private static string CreateKey<T>(string location) where T : UnityEngine.Object
    {
        return $"{typeof(T).FullName}:{location}";
    }

    private static void ValidateArguments(string location, UnityEngine.Object owner)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException("资源地址不能为空。", nameof(location));
        }

        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner), "资源持有者不能为空。");
        }
    }
}

/// <summary>
/// 短生命周期 YooAsset 句柄包装。
/// </summary>
public sealed class MPAssetLoadLease<T> : IDisposable where T : UnityEngine.Object
{
    private AssetHandle m_handle;

    internal MPAssetLoadLease(AssetHandle handle, T asset)
    {
        m_handle = handle;
        Asset = asset;
    }

    public T Asset { get; }

    public void Dispose()
    {
        if (m_handle == null)
        {
            return;
        }

        if (m_handle.IsValid)
        {
            m_handle.Release();
        }

        m_handle = null;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 使用 ES3 保存云同步本地元数据。
/// </summary>
public class MPEasySaveCloudSaveLocalMetaRepository : IMPCloudSaveLocalMetaRepository
{
    /// <summary>
    /// 最近一次进入游戏的 PlayerId Key。
    /// </summary>
    private const string ACTIVE_PLAYER_ID_KEY = "MPCloudSave.ActivePlayerId";

    /// <summary>
    /// 玩家云同步元数据 Key 前缀。
    /// </summary>
    private const string META_KEY_PREFIX = "MPCloudSave.LocalMeta.";

    /// <inheritdoc />
    public Task<MPCloudSaveLocalMeta> LoadAsync(string playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Load(playerId));
    }

    /// <inheritdoc />
    public Task SaveAsync(MPCloudSaveLocalMeta meta, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Save(meta);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Save(MPCloudSaveLocalMeta meta)
    {
        if (meta == null || string.IsNullOrEmpty(meta.playerId))
        {
            return;
        }

        ES3.Save(GetMetaKey(meta.playerId), JsonConvert.SerializeObject(meta));
    }

    /// <inheritdoc />
    public string LoadActivePlayerId()
    {
        return ES3.Load<string>(ACTIVE_PLAYER_ID_KEY, defaultValue: null);
    }

    /// <inheritdoc />
    public void SaveActivePlayerId(string playerId)
    {
        if (!string.IsNullOrEmpty(playerId))
        {
            ES3.Save(ACTIVE_PLAYER_ID_KEY, playerId);
        }
    }

    /// <summary>
    /// 读取指定玩家元数据，失败时返回新对象。
    /// </summary>
    private MPCloudSaveLocalMeta Load(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return new MPCloudSaveLocalMeta();
        }

        string key = GetMetaKey(playerId);
        if (!ES3.KeyExists(key))
        {
            return new MPCloudSaveLocalMeta
            {
                playerId = playerId
            };
        }

        string json = ES3.Load<string>(key, defaultValue: null);
        if (string.IsNullOrEmpty(json))
        {
            return new MPCloudSaveLocalMeta
            {
                playerId = playerId
            };
        }

        try
        {
            MPCloudSaveLocalMeta meta = JsonConvert.DeserializeObject<MPCloudSaveLocalMeta>(json);
            if (meta == null)
            {
                meta = new MPCloudSaveLocalMeta();
            }

            meta.playerId = string.IsNullOrEmpty(meta.playerId) ? playerId : meta.playerId;
            return meta;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Local meta parse failed: {exception.Message}");
            return new MPCloudSaveLocalMeta
            {
                playerId = playerId
            };
        }
    }

    /// <summary>
    /// 获取指定玩家元数据 ES3 Key。
    /// </summary>
    private static string GetMetaKey(string playerId)
    {
        return META_KEY_PREFIX + playerId;
    }
}

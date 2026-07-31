using System;

/// <summary>
/// 保存在本地 ES3 中的云同步元数据。
/// 只保存同步状态，不保存 Unity Authentication token。
/// </summary>
[Serializable]
public class MPCloudSaveLocalMeta
{
    /// <summary>
    /// 元数据所属的 Unity Authentication PlayerId。
    /// </summary>
    public string playerId;

    /// <summary>
    /// Cloud Save 返回的用户快照写锁。
    /// </summary>
    public string snapshotWriteLock;

    /// <summary>
    /// Cloud Save 返回的自定义关卡独立快照写锁。
    /// </summary>
    public string customLevelWriteLock;

    /// <summary>
    /// 最近一次成功同步到云端或从云端应用的 UTC ticks。
    /// </summary>
    public long lastSyncedAtUtcTicks;

    /// <summary>
    /// 本地是否有尚未上传的变更。
    /// </summary>
    public bool hasDirtyData;

    /// <summary>
    /// 用户主快照是否有尚未上传的变更。
    /// </summary>
    public bool hasUserSnapshotDirtyData;

    /// <summary>
    /// 自定义关卡独立快照是否有尚未上传的变更。
    /// </summary>
    public bool hasCustomLevelDirtyData;

    /// <summary>
    /// 最近一次标记本地变更的 UTC ticks。
    /// </summary>
    public long lastDirtyAtUtcTicks;

    /// <summary>
    /// 最近一次云同步错误信息，便于排查。
    /// </summary>
    public string lastError;
}

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 云同步本地元数据仓库接口。
/// </summary>
public interface IMPCloudSaveLocalMetaRepository
{
    /// <summary>
    /// 读取指定玩家的云同步元数据。
    /// </summary>
    Task<MPCloudSaveLocalMeta> LoadAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存指定玩家的云同步元数据。
    /// </summary>
    Task SaveAsync(MPCloudSaveLocalMeta meta, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步保存指定玩家的云同步元数据，供 MarkDirty 这类非 async 入口使用。
    /// </summary>
    void Save(MPCloudSaveLocalMeta meta);

    /// <summary>
    /// 读取最近一次进入游戏的 PlayerId。
    /// </summary>
    string LoadActivePlayerId();

    /// <summary>
    /// 保存最近一次进入游戏的 PlayerId。
    /// </summary>
    void SaveActivePlayerId(string playerId);
}

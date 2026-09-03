using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 公开自定义关卡云端访问抽象。
/// UI 和业务管理器只依赖该接口，便于后续把 Cloud Code、Mock、本地测试实现互相替换。
/// </summary>
public interface IMPCustomLevelPublishApi
{
    /// <summary>
    /// 将本地自定义关卡发布到公开云端目录。
    /// </summary>
    Task<MPCustomLevelPublishResult> PublishAsync(MPCustomLevelInfo levelInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页获取公开自定义关卡列表。
    /// </summary>
    Task<MPCustomLevelListResult> GetListAsync(string sortType, int pageSize, string cursor, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取公开自定义关卡详情。
    /// </summary>
    Task<MPCustomLevelPublicRecord> GetDetailAsync(string publicLevelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取指定公开关卡的点赞数量和试玩次数。
    /// </summary>
    Task<MPCustomLevelStatsResult> GetStatsAsync(
        IReadOnlyList<string> publicLevelIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录一次体验并返回公开自定义关卡详情。
    /// </summary>
    Task<MPCustomLevelPublicRecord> PlayAsync(string publicLevelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置公开自定义关卡的点赞状态。
    /// </summary>
    Task<MPCustomLevelLikeResult> LikeAsync(
        string publicLevelId,
        bool liked,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 作者撤销公开自定义关卡。
    /// </summary>
    Task<MPCustomLevelRevokeResult> RevokeAsync(string publicLevelId, CancellationToken cancellationToken = default);
}

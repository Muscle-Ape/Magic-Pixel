using System;
using System.Collections.Generic;

/// <summary>
/// Unity Cloud 中保存的公开自定义关卡详情。
/// 客户端只读取和展示该结构，真正的写入、点赞、撤销都通过 Cloud Code 完成。
/// </summary>
[Serializable]
public class MPCustomLevelPublicRecord
{
    /// <summary>
    /// 数据结构版本，用于后续兼容旧公开关卡。
    /// </summary>
    public int schemaVersion;

    /// <summary>
    /// 云端公开关卡ID，和本地自定义关卡ID分离，避免不同玩家的本地ID冲突。
    /// </summary>
    public string publicLevelId;

    /// <summary>
    /// 作者本地关卡ID，仅用于作者设备缓存和排查问题。
    /// </summary>
    public string sourceLocalLevelId;

    /// <summary>
    /// 上传者的 Unity Authentication PlayerId。
    /// </summary>
    public string ownerPlayerId;

    /// <summary>
    /// 上传者展示名，当前优先使用 Unity PlayerName，缺失时由服务端兜底。
    /// </summary>
    public string ownerDisplayName;

    /// <summary>
    /// 公开关卡标题。
    /// </summary>
    public string title;

    /// <summary>
    /// 关卡网格尺寸，目前项目主要使用 5 或 10。
    /// </summary>
    public int size;

    /// <summary>
    /// 需要玩家填充的格子索引列表。
    /// </summary>
    public List<int> block;

    /// <summary>
    /// 每个有颜色格子的颜色配置。
    /// </summary>
    public List<MPCustomLevelColorInfo> colors;

    /// <summary>
    /// 点赞数，只能由 Cloud Code 修改。
    /// </summary>
    public int likeCount;

    /// <summary>
    /// 体验次数，只能由 Cloud Code 修改。
    /// </summary>
    public int playCount;

    /// <summary>
    /// 发布状态，对应 <see cref="MPCustomLevelPublishStatus"/>。
    /// </summary>
    public int status;

    /// <summary>
    /// 当前登录玩家是否已经点赞。
    /// 该字段由详情、体验、点赞接口根据调用者临时填充，不作为客户端可信写入来源。
    /// </summary>
    public bool likedByCurrentPlayer;

    /// <summary>
    /// 点赞过该公开关卡的玩家 ID 列表。
    /// 服务端返回给客户端时通常会置空，仅用于兼容 Cloud Code 返回结构，客户端不要依赖该字段做权限判断。
    /// </summary>
    public List<string> likedPlayerIds;

    /// <summary>
    /// 发布时间，使用 UTC Ticks，便于客户端无需时区转换即可排序。
    /// </summary>
    public long createdAtUtcTicks;

    /// <summary>
    /// 最近一次更新或撤销时间，使用 UTC Ticks。
    /// </summary>
    public long updatedAtUtcTicks;

    /// <summary>
    /// 上传时客户端版本，便于后续排查数据兼容问题。
    /// </summary>
    public string clientVersion;

    /// <summary>
    /// 上传时 Unity Services Environment 名称，例如 development 或 production。
    /// </summary>
    public string unityEnvironment;

    /// <summary>
    /// 是否仍处于可公开展示状态。
    /// </summary>
    public bool IsPublished => status == (int)MPCustomLevelPublishStatus.Published;

    /// <summary>
    /// 将公开关卡转换为当前游戏页面可直接使用的自定义关卡数据。
    /// </summary>
    public MPCustomLevelInfo ToCustomLevelInfo()
    {
        string levelId = string.IsNullOrEmpty(publicLevelId) ? sourceLocalLevelId : publicLevelId;
        string levelTitle = string.IsNullOrEmpty(title) ? "Undefined" : title;
        return new MPCustomLevelInfo(
            levelId,
            levelTitle,
            size,
            block == null ? new List<int>() : new List<int>(block),
            colors == null ? new List<MPCustomLevelColorInfo>() : new List<MPCustomLevelColorInfo>(colors),
            updatedAtUtcTicks > 0 ? updatedAtUtcTicks : createdAtUtcTicks);
    }
}

using System;
using System.Collections.Generic;

/// <summary>
/// 本地缓存的单个自定义关卡发布状态。
/// 云端状态仍以 Cloud Code 返回为准，本地缓存只用于按钮显示和快速撤销。
/// </summary>
[Serializable]
public class MPCustomLevelPublishLocalState
{
    /// <summary>
    /// 作者本地关卡ID。
    /// </summary>
    public string sourceLocalLevelId;

    /// <summary>
    /// 云端公开关卡ID。
    /// </summary>
    public string publicLevelId;

    /// <summary>
    /// 本地缓存的发布状态。
    /// </summary>
    public int status;

    /// <summary>
    /// 本地缓存最近更新时间，UTC Ticks。
    /// </summary>
    public long updatedAtUtcTicks;

    /// <summary>
    /// 最近一次从服务端同步成功的点赞数量。
    /// 页面打开时直接使用该值，不等待网络请求。
    /// </summary>
    public int cachedLikeCount;

    /// <summary>
    /// 点赞数量最近一次同步成功的时间，UTC Ticks。
    /// </summary>
    public long likeCountSyncedAtUtcTicks;

    /// <summary>
    /// 最近一次发布或撤销失败信息。
    /// </summary>
    public string lastError;

    /// <summary>
    /// 是否处于已发布状态。
    /// </summary>
    public bool IsPublished => status == (int)MPCustomLevelPublishStatus.Published;
}

/// <summary>
/// 当前玩家所有本地关卡发布状态缓存。
/// </summary>
[Serializable]
public class MPCustomLevelPublishLocalStateCollection
{
    /// <summary>
    /// 缓存所属的 Unity Authentication PlayerId。
    /// </summary>
    public string playerId;

    /// <summary>
    /// 发布状态列表。
    /// </summary>
    public List<MPCustomLevelPublishLocalState> items = new List<MPCustomLevelPublishLocalState>();
}

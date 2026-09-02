using System;
using System.Collections.Generic;

/// <summary>
/// 发布公开自定义关卡的返回结果。
/// </summary>
[Serializable]
public class MPCustomLevelPublishResult
{
    /// <summary>
    /// Cloud Code 是否成功处理本次发布请求。
    /// </summary>
    public bool success;

    /// <summary>
    /// 新生成或更新后的公开关卡ID。
    /// </summary>
    public string publicLevelId;

    /// <summary>
    /// 公开关卡状态。
    /// </summary>
    public int status;

    /// <summary>
    /// 可展示或可写入日志的服务端消息。
    /// </summary>
    public string message;

    /// <summary>
    /// 服务端保存后的完整公开关卡记录。
    /// </summary>
    public MPCustomLevelPublicRecord record;
}

/// <summary>
/// 公开自定义关卡列表返回结果。
/// </summary>
[Serializable]
public class MPCustomLevelListResult
{
    /// <summary>
    /// Cloud Code 是否成功处理本次列表请求。
    /// </summary>
    public bool success;

    /// <summary>
    /// 当前页公开关卡数据。
    /// </summary>
    public List<MPCustomLevelPublicRecord> items;

    /// <summary>
    /// 下一页游标，为空表示没有更多数据。
    /// </summary>
    public string nextCursor;

    /// <summary>
    /// 可展示或可写入日志的服务端消息。
    /// </summary>
    public string message;
}

/// <summary>
/// 公开关卡的轻量统计数据。
/// </summary>
[Serializable]
public class MPCustomLevelStatsRecord
{
    /// <summary>
    /// 公开关卡ID。
    /// </summary>
    public string publicLevelId;

    /// <summary>
    /// 服务端最新点赞数量。
    /// </summary>
    public int likeCount;
}

/// <summary>
/// 批量拉取公开关卡统计数据的返回结果。
/// </summary>
[Serializable]
public class MPCustomLevelStatsResult
{
    public bool success;
    public List<MPCustomLevelStatsRecord> items;
    public string message;
}

/// <summary>
/// 点赞公开自定义关卡的返回结果。
/// </summary>
[Serializable]
public class MPCustomLevelLikeResult
{
    /// <summary>
    /// Cloud Code 是否成功处理本次点赞请求。
    /// </summary>
    public bool success;

    /// <summary>
    /// 本次请求后，当前玩家是否处于已点赞状态。
    /// </summary>
    public bool liked;

    /// <summary>
    /// 最新点赞数。
    /// </summary>
    public int likeCount;

    /// <summary>
    /// 可展示或可写入日志的服务端消息。
    /// </summary>
    public string message;

    /// <summary>
    /// 最新公开关卡记录。
    /// </summary>
    public MPCustomLevelPublicRecord record;
}

/// <summary>
/// 撤销公开自定义关卡的返回结果。
/// </summary>
[Serializable]
public class MPCustomLevelRevokeResult
{
    /// <summary>
    /// Cloud Code 是否成功处理本次撤销请求。
    /// </summary>
    public bool success;

    /// <summary>
    /// 被撤销的公开关卡ID。
    /// </summary>
    public string publicLevelId;

    /// <summary>
    /// 撤销后的状态。
    /// </summary>
    public int status;

    /// <summary>
    /// 可展示或可写入日志的服务端消息。
    /// </summary>
    public string message;
}

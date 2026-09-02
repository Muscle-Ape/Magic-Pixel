namespace MagicPixelCustomLevelPublish;

/// <summary>
/// 客户端上传的自定义关卡结构。
/// </summary>
public class CustomLevelUploadRequest
{
    /// <summary>
    /// 作者设备上的本地关卡ID。
    /// </summary>
    public string? sourceLocalLevelId { get; set; }

    /// <summary>
    /// 关卡标题。
    /// </summary>
    public string? title { get; set; }

    /// <summary>
    /// 网格尺寸，目前只允许 5 或 10。
    /// </summary>
    public int size { get; set; }

    /// <summary>
    /// 需要玩家填充的格子索引。
    /// </summary>
    public List<int>? block { get; set; }

    /// <summary>
    /// 每个有颜色格子的颜色配置。
    /// </summary>
    public List<CustomLevelColorInfo>? colors { get; set; }
}

/// <summary>
/// 自定义关卡颜色格子配置。
/// </summary>
public class CustomLevelColorInfo
{
    /// <summary>
    /// 颜色所在格子索引。
    /// </summary>
    public int index { get; set; }

    /// <summary>
    /// #RRGGBBAA 格式颜色。
    /// </summary>
    public string? color { get; set; }
}

/// <summary>
/// 云端公开自定义关卡详情。
/// </summary>
public class CustomLevelPublicRecord
{
    public int schemaVersion { get; set; }
    public string publicLevelId { get; set; } = string.Empty;
    public string sourceLocalLevelId { get; set; } = string.Empty;
    public string ownerPlayerId { get; set; } = string.Empty;
    public string ownerDisplayName { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public int size { get; set; }
    public List<int> block { get; set; } = new();
    public List<CustomLevelColorInfo> colors { get; set; } = new();
    public int likeCount { get; set; }
    public int playCount { get; set; }
    public int status { get; set; }
    public bool likedByCurrentPlayer { get; set; }
    public long createdAtUtcTicks { get; set; }
    public long updatedAtUtcTicks { get; set; }
    public string clientVersion { get; set; } = string.Empty;
    public string unityEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// 服务端内部使用的点赞玩家列表，不返回给普通客户端。
    /// 当前开发期采用简单数组；正式高并发和大体量时可拆成独立点赞记录 Key。
    /// </summary>
    public List<string> likedPlayerIds { get; set; } = new();
}

/// <summary>
/// 公开关卡目录。
/// </summary>
public class CustomLevelCatalog
{
    public List<string> publicLevelIds { get; set; } = new();
}

public class CustomLevelPublishResult
{
    public bool success { get; set; }
    public string publicLevelId { get; set; } = string.Empty;
    public int status { get; set; }
    public string message { get; set; } = string.Empty;
    public CustomLevelPublicRecord? record { get; set; }
}

public class CustomLevelListResult
{
    public bool success { get; set; }
    public List<CustomLevelPublicRecord> items { get; set; } = new();
    public string nextCursor { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
}

/// <summary>
/// 公开关卡的轻量统计数据。
/// </summary>
public class CustomLevelStatsRecord
{
    public string publicLevelId { get; set; } = string.Empty;
    public int likeCount { get; set; }
}

/// <summary>
/// 批量拉取公开关卡统计数据的返回结果。
/// </summary>
public class CustomLevelStatsResult
{
    public bool success { get; set; }
    public List<CustomLevelStatsRecord> items { get; set; } = new();
    public string message { get; set; } = string.Empty;
}

public class CustomLevelLikeResult
{
    public bool success { get; set; }
    public bool liked { get; set; }
    public int likeCount { get; set; }
    public string message { get; set; } = string.Empty;
    public CustomLevelPublicRecord? record { get; set; }
}

public class CustomLevelRevokeResult
{
    public bool success { get; set; }
    public string publicLevelId { get; set; } = string.Empty;
    public int status { get; set; }
    public string message { get; set; } = string.Empty;
}

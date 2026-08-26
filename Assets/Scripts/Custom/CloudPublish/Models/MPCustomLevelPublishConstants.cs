/// <summary>
/// 自定义关卡公开发布模块使用的固定配置。
/// 这里的函数名必须和 Unity Dashboard 中创建的 Cloud Code 脚本名称保持一致。
/// </summary>
public static class MPCustomLevelPublishConstants
{
    /// <summary>
    /// Cloud Code C# Module 名称。
    /// 需要和服务端 .NET 工程生成并部署后的模块名保持一致。
    /// </summary>
    public const string MODULE_NAME = "MagicPixelCustomLevelPublish";

    /// <summary>
    /// 当前公开关卡数据结构版本。
    /// </summary>
    public const int SCHEMA_VERSION = 1;

    /// <summary>
    /// 发布自定义关卡的 Cloud Code 函数名。
    /// </summary>
    public const string PUBLISH_FUNCTION = "PublishCustomLevel";

    /// <summary>
    /// 拉取公开自定义关卡列表的 Cloud Code 函数名。
    /// </summary>
    public const string GET_LIST_FUNCTION = "GetPublishedCustomLevels";

    /// <summary>
    /// 拉取公开自定义关卡详情的 Cloud Code 函数名。
    /// </summary>
    public const string GET_DETAIL_FUNCTION = "GetPublishedCustomLevel";

    /// <summary>
    /// 体验公开自定义关卡的 Cloud Code 函数名。
    /// 该函数会在服务端增加 playCount，并返回完整关卡数据。
    /// </summary>
    public const string PLAY_FUNCTION = "PlayPublishedCustomLevel";

    /// <summary>
    /// 点赞公开自定义关卡的 Cloud Code 函数名。
    /// </summary>
    public const string LIKE_FUNCTION = "LikePublishedCustomLevel";

    /// <summary>
    /// 作者撤销公开自定义关卡的 Cloud Code 函数名。
    /// </summary>
    public const string REVOKE_FUNCTION = "RevokePublishedCustomLevel";

    /// <summary>
    /// 默认列表分页大小。
    /// </summary>
    public const int DEFAULT_PAGE_SIZE = 20;

    /// <summary>
    /// 单次列表请求允许的最大数量。
    /// </summary>
    public const int MAX_PAGE_SIZE = 20;

    /// <summary>
    /// 最新发布排序。
    /// </summary>
    public const string SORT_LATEST = "Latest";

    /// <summary>
    /// 热门点赞排序。
    /// </summary>
    public const string SORT_POPULAR = "Popular";

    /// <summary>
    /// 只返回当前玩家点过喜欢的公开关卡。
    /// </summary>
    public const string SORT_LIKED = "Liked";
}

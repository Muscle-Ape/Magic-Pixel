/// <summary>
/// 云存储模块使用的固定 Key 和版本号。
/// </summary>
public static class MPCloudSaveConstants
{
    /// <summary>
    /// 当前用户快照结构版本。
    /// </summary>
    public const int USER_SNAPSHOT_SCHEMA_VERSION = 1;

    /// <summary>
    /// Cloud Save Player Data 中保存完整用户快照的 Key。
    /// </summary>
    public const string USER_SNAPSHOT_KEY = "mp_user_snapshot_v1";

    /// <summary>
    /// 当前自定义关卡快照结构版本。
    /// </summary>
    public const int CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION = 1;

    /// <summary>
    /// Cloud Save Player Data 中单独保存自定义关卡数据的 Key。
    /// 自定义关卡颜色格子较多，不再塞进用户主快照，避免主快照体积过大。
    /// </summary>
    public const string CUSTOM_LEVEL_SNAPSHOT_KEY = "mp_custom_level_snapshot_v1";

    /// <summary>
    /// 自定义关卡完整图片 Cloud Save Files Key 前缀。
    /// </summary>
    public const string CUSTOM_LEVEL_IMAGE_FILE_PREFIX = "mp_custom_level_image_";

    /// <summary>
    /// Editor 中使用的 Unity Services Environment 名称。
    /// </summary>
    public const string DEVELOPMENT_ENVIRONMENT = "development";

    /// <summary>
    /// 发布包中使用的 Unity Services Environment 名称。
    /// </summary>
    public const string PRODUCTION_ENVIRONMENT = "production";
}

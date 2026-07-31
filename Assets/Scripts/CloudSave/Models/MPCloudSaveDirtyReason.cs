/// <summary>
/// 标记云同步 dirty 的来源。
/// </summary>
public enum MPCloudSaveDirtyReason
{
    /// <summary>未知来源。</summary>
    Unknown = 0,

    /// <summary>资产或道具变化。</summary>
    Assets = 1,

    /// <summary>用户设置变化。</summary>
    Settings = 2,

    /// <summary>主线关卡进度变化。</summary>
    MainLevel = 3,

    /// <summary>大图模式关卡进度变化。</summary>
    LargeImageLevel = 4,

    /// <summary>自定义关卡变化。</summary>
    CustomLevel = 5,

    /// <summary>宠物数据变化。</summary>
    Pets = 6
}

/// <summary>
/// 公开自定义关卡在云端的发布状态。
/// </summary>
public enum MPCustomLevelPublishStatus
{
    /// <summary>
    /// 已发布，普通玩家可以在公开列表中看到并体验。
    /// </summary>
    Published = 0,

    /// <summary>
    /// 作者已撤销发布，普通公开列表不再展示。
    /// </summary>
    Revoked = 1,

    /// <summary>
    /// 后续运营后台或审核系统使用的删除状态，当前客户端暂不主动写入。
    /// </summary>
    Deleted = 2
}

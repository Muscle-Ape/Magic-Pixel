using System;
using UnityEngine;

/// <summary>
/// 上传到 Cloud Save 的自定义关卡独立快照。
/// 自定义关卡颜色数据可能很大，因此从用户主快照中拆出，使用独立 Player Data Key 保存。
/// </summary>
[Serializable]
public class MPCustomLevelCloudSnapshot
{
    /// <summary>
    /// 自定义关卡快照结构版本，用于后续兼容迁移。
    /// </summary>
    public int schemaVersion = MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION;

    /// <summary>
    /// 快照所属的 Unity Authentication PlayerId。
    /// </summary>
    public string playerId;

    /// <summary>
    /// 写入快照时使用的 Unity Services Environment。
    /// </summary>
    public string unityEnvironment;

    /// <summary>
    /// 最近一次登录方式，便于后台排查游客或第三方登录问题。
    /// </summary>
    public string lastLoginProvider;

    /// <summary>
    /// 当前账号是否已经绑定正式登录身份。
    /// </summary>
    public bool hasBoundIdentity;

    /// <summary>
    /// 快照最后更新时间，UTC ticks。
    /// </summary>
    public long updatedAtUtcTicks;

    /// <summary>
    /// 写入快照时的客户端版本号。
    /// </summary>
    public string clientVersion;

    /// <summary>
    /// 自定义关卡配置和通关状态。
    /// </summary>
    public MPUserCustomLevelSnapshot customLevel = new MPUserCustomLevelSnapshot();

    /// <summary>
    /// 创建一份空的自定义关卡云快照。
    /// </summary>
    public static MPCustomLevelCloudSnapshot CreateDefault(string playerId, string unityEnvironment, string lastLoginProvider, bool hasBoundIdentity)
    {
        return new MPCustomLevelCloudSnapshot
        {
            schemaVersion = MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION,
            playerId = playerId,
            unityEnvironment = unityEnvironment,
            lastLoginProvider = lastLoginProvider,
            hasBoundIdentity = hasBoundIdentity,
            updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            clientVersion = Application.version,
            customLevel = new MPUserCustomLevelSnapshot()
        };
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 上传到 Cloud Save 的完整用户数据快照。
/// 该结构只用于云端同步，不直接替代 MPUser 的本地 ES3 存档。
/// </summary>
[Serializable]
public class MPUserCloudSnapshot
{
    /// <summary>
    /// 快照结构版本，用于后续做兼容迁移。
    /// </summary>
    public int schemaVersion = MPCloudSaveConstants.USER_SNAPSHOT_SCHEMA_VERSION;

    /// <summary>
    /// 快照所属的 Unity Authentication PlayerId。
    /// </summary>
    public string playerId;

    /// <summary>
    /// 写入快照时使用的 Unity Services Environment。
    /// </summary>
    public string unityEnvironment;

    /// <summary>
    /// 最近一次登录方式，主要用于后台排查游客或第三方登录问题。
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
    /// 金币、钻石、道具等资产数据。
    /// </summary>
    public MPUserAssetsSnapshot assets = new MPUserAssetsSnapshot();

    /// <summary>
    /// 音乐、音效、震动等用户设置。
    /// </summary>
    public MPUserSettingsSnapshot settings = new MPUserSettingsSnapshot();

    /// <summary>
    /// 主线关卡进度。
    /// </summary>
    public MPUserMainLevelSnapshot mainLevel = new MPUserMainLevelSnapshot();

    /// <summary>
    /// 大图模式关卡进度。
    /// </summary>
    public MPUserLargeImageLevelSnapshot largeImageLevel = new MPUserLargeImageLevelSnapshot();

    /// <summary>
    /// 兼容旧版本主快照中的自定义关卡数据。
    /// 新版本保存主快照时会保持为空，真实数据写入 MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY。
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public MPUserCustomLevelSnapshot customLevel;

    /// <summary>
    /// 宠物运行时数据。
    /// </summary>
    public MPUserPetsSnapshot pets = new MPUserPetsSnapshot();

    /// <summary>
    /// 创建一份新账号使用的默认云快照。
    /// </summary>
    public static MPUserCloudSnapshot CreateDefault(string playerId, string unityEnvironment, string lastLoginProvider, bool hasBoundIdentity)
    {
        return new MPUserCloudSnapshot
        {
            schemaVersion = MPCloudSaveConstants.USER_SNAPSHOT_SCHEMA_VERSION,
            playerId = playerId,
            unityEnvironment = unityEnvironment,
            lastLoginProvider = lastLoginProvider,
            hasBoundIdentity = hasBoundIdentity,
            updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            clientVersion = Application.version,
            assets = new MPUserAssetsSnapshot
            {
                coins = 200,
                diamond = 0,
                hintProps = 0,
                loveRecoverProps = 0,
                homeRewardReadyAtUtcTicks = DateTime.UtcNow.AddHours(3).Ticks
            },
            settings = new MPUserSettingsSnapshot
            {
                isMusic = true,
                isSound = true,
                isVibration = true
            },
            mainLevel = new MPUserMainLevelSnapshot(),
            largeImageLevel = new MPUserLargeImageLevelSnapshot(),
            pets = new MPUserPetsSnapshot()
        };
    }
}

/// <summary>
/// 用户资产快照。
/// </summary>
[Serializable]
public class MPUserAssetsSnapshot
{
    /// <summary>金币数量。</summary>
    public int coins;

    /// <summary>钻石数量。</summary>
    public int diamond;

    /// <summary>提示道具数量。</summary>
    public int hintProps;

    /// <summary>生命恢复道具数量。</summary>
    public int loveRecoverProps;

    /// <summary>主页定时奖励下一次可领取的 UTC ticks。</summary>
    public long homeRewardReadyAtUtcTicks;
}

/// <summary>
/// 用户设置快照。
/// </summary>
[Serializable]
public class MPUserSettingsSnapshot
{
    /// <summary>是否开启背景音乐。</summary>
    public bool isMusic = true;

    /// <summary>是否开启音效。</summary>
    public bool isSound = true;

    /// <summary>是否开启震动。</summary>
    public bool isVibration = true;
}

/// <summary>
/// 主线关卡快照。
/// </summary>
[Serializable]
public class MPUserMainLevelSnapshot
{
    /// <summary>当前通关下标。</summary>
    public int passIndex;

    /// <summary>已解锁关卡 ID 列表。</summary>
    public List<string> unlockList = new List<string>();

    /// <summary>已通关关卡 ID 列表。</summary>
    public List<string> passList = new List<string>();

    /// <summary>关卡最高星数，Key 为关卡 ID。</summary>
    public Dictionary<string, int> stars = new Dictionary<string, int>();
}

/// <summary>
/// 大图模式关卡快照。
/// </summary>
[Serializable]
public class MPUserLargeImageLevelSnapshot
{
    /// <summary>当前通关下标。</summary>
    public int passIndex;

    /// <summary>已解锁关卡 ID 列表。</summary>
    public List<string> unlockList = new List<string>();

    /// <summary>已通关关卡 ID 列表。</summary>
    public List<string> passList = new List<string>();

    /// <summary>关卡最高星数，Key 为关卡 ID。</summary>
    public Dictionary<string, int> stars = new Dictionary<string, int>();

    /// <summary>已经领取过通关金币奖励的关卡 ID 列表。</summary>
    public List<string> coinAwardClaimedList = new List<string>();
}

/// <summary>
/// 自定义关卡快照。
/// </summary>
[Serializable]
public class MPUserCustomLevelSnapshot
{
    /// <summary>自定义关卡配置列表。</summary>
    public List<MPCustomLevelInfo> levels = new List<MPCustomLevelInfo>();

    /// <summary>自定义关卡通关 ID 列表。</summary>
    public List<string> passList = new List<string>();
}

/// <summary>
/// 宠物系统快照。
/// </summary>
[Serializable]
public class MPUserPetsSnapshot
{
    /// <summary>当前选中的宠物 ID。</summary>
    public string selectedPetId;
}

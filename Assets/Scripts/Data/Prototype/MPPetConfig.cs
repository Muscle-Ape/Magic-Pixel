using Newtonsoft.Json;
using System.Collections.Generic;

/// <summary>
/// 宠物静态配置，对应 YooRes/Config/pets_config.json。
/// </summary>
public class MPPetConfig
{
    /// <summary>
    /// 宠物唯一 ID，用于配置、存档和列表选中状态的关联。
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 宠物显示名称，后续如果界面需要名字可直接使用。
    /// </summary>
    [JsonProperty]
    private string name;

    /// <summary>
    /// 宠物图标资源名，当前通过 MPLoad 按资源名加载。
    /// </summary>
    [JsonProperty]
    private string icon;

    /// <summary>
    /// 首次创建运行时数据时使用的默认等级。
    /// </summary>
    [JsonProperty]
    private int defaultLevel;

    /// <summary>
    /// 首次进入宠物系统时是否默认解锁。
    /// </summary>
    [JsonProperty]
    private bool defaultUnlocked;

    /// <summary>
    /// 解锁所需玩家等级，仅作为配置条件展示，真正解锁流程后续接弹窗时处理。
    /// </summary>
    [JsonProperty]
    private int unlockLevel;

    /// <summary>
    /// 未解锁状态下显示的条件文本。
    /// </summary>
    [JsonProperty]
    private string unlockText;

    /// <summary>
    /// 奖励生产周期，单位：秒。
    /// </summary>
    [JsonProperty]
    private int rewardIntervalSeconds;

    /// <summary>
    /// 健康度每小时自然减少值。
    /// </summary>
    [JsonProperty]
    private float healthDecayPerHour;

    /// <summary>
    /// 心情度每小时自然减少值。
    /// </summary>
    [JsonProperty]
    private float moodDecayPerHour;

    /// <summary>
    /// 宠物产出的奖励列表，界面最多展示前三个。
    /// </summary>
    [JsonProperty]
    private List<MPPetRewardConfig> rewards;

    public string ID => id;
    public string Name => name;
    public string Icon => icon;
    public int DefaultLevel => defaultLevel <= 0 ? 1 : defaultLevel;
    public bool DefaultUnlocked => defaultUnlocked;
    public int UnlockLevel => unlockLevel;
    public string UnlockText => string.IsNullOrEmpty(unlockText) ? $"Unlock at Lv.{unlockLevel}" : unlockText;
    public int RewardIntervalSeconds => rewardIntervalSeconds <= 0 ? 3600 : rewardIntervalSeconds;
    public float HealthDecayPerHour => healthDecayPerHour <= 0f ? 1f : healthDecayPerHour;
    public float MoodDecayPerHour => moodDecayPerHour <= 0f ? 1f : moodDecayPerHour;
    public List<MPPetRewardConfig> Rewards => rewards ?? new List<MPPetRewardConfig>();
}

/// <summary>
/// 单个宠物奖励配置。
/// </summary>
public class MPPetRewardConfig
{
    /// <summary>
    /// 奖励类型。金币、钻石、提示、生命恢复、食物、玩具等都通过该字段区分。
    /// </summary>
    [JsonProperty]
    private string type;

    /// <summary>
    /// 奖励图标资源名。
    /// </summary>
    [JsonProperty]
    private string icon;

    /// <summary>
    /// 食物/玩具奖励对应的物品 ID。其他奖励配置为 null 即可。
    /// </summary>
    [JsonProperty]
    private string id;

    /// <summary>
    /// 单次领取数量。
    /// </summary>
    [JsonProperty]
    private int count;

    public string Type => type;
    public string Icon => icon;
    public string ID => id;
    public int Count => count;
}

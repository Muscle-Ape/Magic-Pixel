using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MPPetRuntimeData
{
    /// <summary>
    /// 宠物 ID，对应 MPPetConfig.ID。
    /// </summary>
    public string id;

    /// <summary>
    /// 当前宠物是否已经解锁。
    /// </summary>
    public bool unlocked;

    /// <summary>
    /// 当前宠物等级。
    /// </summary>
    public int level;

    /// <summary>
    /// 当前健康度，范围 0-100。
    /// </summary>
    public float health;

    /// <summary>
    /// 当前心情度，范围 0-100。
    /// </summary>
    public float mood;

    /// <summary>
    /// 本轮奖励开始生产的 UTC ticks，用于离线倒计时计算。
    /// </summary>
    public long rewardStartTicks;

    /// <summary>
    /// 上次计算健康度和心情度衰减的 UTC ticks。
    /// </summary>
    public long lastStatusTicks;
}

public class MPPetCareRuntimeData
{
    /// <summary>
    /// 食物或玩具 ID，对应 MPPetCareItemConfig.ID。
    /// </summary>
    public string id;

    /// <summary>
    /// 当前道具是否已经解锁。
    /// </summary>
    public bool unlocked;

    /// <summary>
    /// 当前剩余数量。
    /// </summary>
    public int count;

    /// <summary>
    /// 数量是否已经用配置默认值初始化过，用于兼容旧存档。
    /// </summary>
    public bool quantityInitialized;
}

public partial class MPUser
{
    /// <summary>
    /// 健康度和心情度不需要每秒写存档，至少间隔 60 秒再落盘。
    /// </summary>
    private const int PET_STATUS_SAVE_INTERVAL_SECONDS = 60;

    /// <summary>
    /// 宠物运行时数据存档 Key。
    /// </summary>
    private string m_key_pets_json = "key_pets_json";

    /// <summary>
    /// 当前选中宠物 ID 存档 Key。
    /// </summary>
    private string m_key_selected_pet_id = "key_selected_pet_id";

    /// <summary>
    /// 非金币类宠物奖励临时背包存档 Key。
    /// </summary>
    private string m_key_pet_reward_inventory = "key_pet_reward_inventory";

    /// <summary>
    /// 食物和玩具解锁状态存档 Key。
    /// </summary>
    private string m_key_pet_care_items_json = "key_pet_care_items_json";

    /// <summary>
    /// 宠物运行时数据列表，和静态配置分离，便于热更配置和兼容旧存档。
    /// </summary>
    private List<MPPetRuntimeData> m_pet_runtime_list;

    /// <summary>
    /// 食物和玩具运行时数据列表，目前只保存解锁状态。
    /// </summary>
    private List<MPPetCareRuntimeData> m_pet_care_runtime_list;

    /// <summary>
    /// 非金币奖励累计数量，后续接入具体道具系统时可替换为正式背包。
    /// </summary>
    private Dictionary<string, int> m_pet_reward_inventory;

    /// <summary>
    /// 当前选中的宠物 ID。
    /// </summary>
    private string m_selected_pet_id;

    private void InitPets()
    {
        string petJson = ES3.Load<string>(m_key_pets_json, defaultValue: null);
        string careJson = ES3.Load<string>(m_key_pet_care_items_json, defaultValue: null);

        m_pet_runtime_list = DeserializePetRuntimeList(petJson);
        m_pet_care_runtime_list = DeserializePetCareRuntimeList(careJson);
        m_pet_reward_inventory = ES3.Load<Dictionary<string, int>>(m_key_pet_reward_inventory, new Dictionary<string, int>());
        m_selected_pet_id = ES3.Load<string>(m_key_selected_pet_id, defaultValue: null);

        MPPetsModel petsModel = MPDataManager.Instance.m_petsModel;
        SyncPetRuntimeConfigs(petsModel?.petConfigs);
        SyncPetCareRuntimeConfigs(petsModel?.foodConfigs);
        SyncPetCareRuntimeConfigs(petsModel?.toyConfigs);
    }

    public void SyncPetRuntimeConfigs(List<MPPetConfig> configs)
    {
        if (configs == null)
            return;

        if (m_pet_runtime_list == null)
        {
            m_pet_runtime_list = new List<MPPetRuntimeData>();
        }

        bool changed = false;
        long nowTicks = DateTime.UtcNow.Ticks;

        // 配置新增宠物时，只补齐缺失的运行时数据，不覆盖玩家已有进度。
        for (int i = 0; i < configs.Count; i++)
        {
            MPPetConfig config = configs[i];
            if (config == null || string.IsNullOrEmpty(config.ID))
                continue;

            MPPetRuntimeData data = GetPetRuntimeData(config.ID);
            if (data != null)
                continue;

            data = new MPPetRuntimeData()
            {
                id = config.ID,
                unlocked = config.DefaultUnlocked,
                level = config.DefaultLevel,
                health = 100f,
                mood = 100f,
                rewardStartTicks = nowTicks,
                lastStatusTicks = nowTicks,
            };
            m_pet_runtime_list.Add(data);
            changed = true;
        }

        if (string.IsNullOrEmpty(m_selected_pet_id))
        {
            // 没有历史选中记录时，默认选中第一只已解锁宠物。
            MPPetRuntimeData firstUnlocked = m_pet_runtime_list.Find(item => item != null && item.unlocked);
            if (firstUnlocked != null)
            {
                m_selected_pet_id = firstUnlocked.id;
                ES3.Save(m_key_selected_pet_id, m_selected_pet_id);
            }
        }

        if (changed)
        {
            SavePetsRuntime();
        }
    }

    public void SyncPetCareRuntimeConfigs(List<MPPetCareItemConfig> configs)
    {
        if (configs == null)
            return;

        if (m_pet_care_runtime_list == null)
        {
            m_pet_care_runtime_list = new List<MPPetCareRuntimeData>();
        }

        bool changed = false;
        for (int i = 0; i < configs.Count; i++)
        {
            MPPetCareItemConfig config = configs[i];
            if (config == null || string.IsNullOrEmpty(config.ID))
                continue;

            MPPetCareRuntimeData data = GetPetCareRuntimeData(config.ID);
            if (data != null)
            {
                if (!data.quantityInitialized)
                {
                    data.count = config.DefaultCount;
                    data.quantityInitialized = true;
                    changed = true;
                }
                continue;
            }

            m_pet_care_runtime_list.Add(new MPPetCareRuntimeData()
            {
                id = config.ID,
                unlocked = config.DefaultUnlocked,
                count = config.DefaultCount,
                quantityInitialized = true,
            });
            changed = true;
        }

        if (changed)
        {
            SavePetCareRuntime();
        }
    }

    public List<MPPetRuntimeData> GetPetRuntimeList()
    {
        if (m_pet_runtime_list == null)
        {
            InitPets();
        }

        return m_pet_runtime_list;
    }

    public MPPetRuntimeData GetPetRuntimeData(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        List<MPPetRuntimeData> list = GetPetRuntimeList();
        return list.Find(item => item != null && item.id == id);
    }

    public MPPetCareRuntimeData GetPetCareRuntimeData(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (m_pet_care_runtime_list == null)
        {
            InitPets();
        }

        return m_pet_care_runtime_list.Find(item => item != null && item.id == id);
    }

    public bool PetIsUnlock(string id)
    {
        MPPetRuntimeData data = GetPetRuntimeData(id);
        return data != null && data.unlocked;
    }

    public bool PetCareItemIsUnlock(string id)
    {
        MPPetCareRuntimeData data = GetPetCareRuntimeData(id);
        return data != null && data.unlocked;
    }

    public int GetPetCareItemCount(string id)
    {
        MPPetCareRuntimeData data = GetPetCareRuntimeData(id);
        return data == null ? 0 : Mathf.Max(0, data.count);
    }

    public void PetUnlock(string id)
    {
        MPPetRuntimeData data = GetPetRuntimeData(id);
        if (data == null || data.unlocked)
            return;

        data.unlocked = true;
        // 解锁后从当前时间重新开始计算奖励和状态衰减。
        data.lastStatusTicks = DateTime.UtcNow.Ticks;
        data.rewardStartTicks = data.lastStatusTicks;
        SavePetsRuntime();
    }

    public void PetCareItemUnlock(string id)
    {
        MPPetCareRuntimeData data = GetPetCareRuntimeData(id);
        if (data == null || data.unlocked)
            return;

        data.unlocked = true;
        SavePetCareRuntime();
    }

    public void AddPetCareItemCount(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0)
            return;

        MPPetCareRuntimeData data = GetPetCareRuntimeData(id);
        if (data == null)
            return;

        data.count = Mathf.Max(0, data.count) + count;
        data.quantityInitialized = true;
        SavePetCareRuntime();
    }

    public bool PetCareItemCanUse(string petId, MPPetCareItemConfig itemConfig)
    {
        if (itemConfig == null || !PetCareItemIsUnlock(itemConfig.ID))
            return false;

        MPPetCareRuntimeData careData = GetPetCareRuntimeData(itemConfig.ID);
        if (careData == null || careData.count <= 0)
            return false;

        MPPetRuntimeData petData = GetPetRuntimeData(petId);
        if (petData == null || !petData.unlocked)
            return false;

        // 对应状态已经满值时，不允许继续使用道具，避免无效消耗数量。
        switch (itemConfig.RestoreType)
        {
            case MPPetRestoreType.Health:
                return petData.health < 100f;
            case MPPetRestoreType.Mood:
                return petData.mood < 100f;
            default:
                return false;
        }
    }

    public string GetSelectedPetId()
    {
        if (m_pet_runtime_list == null)
        {
            InitPets();
        }

        return m_selected_pet_id;
    }

    public void SetSelectedPet(string id)
    {
        if (string.IsNullOrEmpty(id) || !PetIsUnlock(id))
            return;

        m_selected_pet_id = id;
        ES3.Save(m_key_selected_pet_id, m_selected_pet_id);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }

    public bool PetRewardIsReady(MPPetConfig config)
    {
        return GetPetRewardRemainingSeconds(config) <= 0;
    }

    public int GetPetRewardRemainingSeconds(MPPetConfig config)
    {
        if (config == null)
            return 0;

        MPPetRuntimeData data = GetPetRuntimeData(config.ID);
        if (data == null || !data.unlocked)
            return config.RewardIntervalSeconds;

        long elapsedTicks = DateTime.UtcNow.Ticks - data.rewardStartTicks;
        double elapsedSeconds = TimeSpan.FromTicks(Math.Max(0, elapsedTicks)).TotalSeconds;
        return Mathf.Max(0, config.RewardIntervalSeconds - Mathf.FloorToInt((float)elapsedSeconds));
    }

    public float GetPetRewardProgress(MPPetConfig config)
    {
        if (config == null)
            return 0f;

        int remaining = GetPetRewardRemainingSeconds(config);
        return Mathf.Clamp01(1f - (float)remaining / config.RewardIntervalSeconds);
    }

    public bool ClaimPetReward(MPPetConfig config, List<string> changedCareItemIds = null)
    {
        if (config == null || !PetRewardIsReady(config))
            return false;

        MPPetRuntimeData data = GetPetRuntimeData(config.ID);
        if (data == null || !data.unlocked)
            return false;

        List<MPPetRewardConfig> rewards = config.Rewards;
        for (int i = 0; i < rewards.Count; i++)
        {
            MPPetRewardConfig reward = rewards[i];
            string changedCareItemId = GrantPetReward(reward);
            if (!string.IsNullOrEmpty(changedCareItemId) && changedCareItemIds != null && !changedCareItemIds.Contains(changedCareItemId))
            {
                changedCareItemIds.Add(changedCareItemId);
            }
        }

        // 领取成功后重置本轮奖励倒计时。
        data.rewardStartTicks = DateTime.UtcNow.Ticks;
        SavePetsRuntime();
        return true;
    }

    /// <summary>
    /// 根据宠物奖励类型，把奖励发放到当前项目已有的用户数据中。
    /// 返回值只在食物/玩具数量变化时使用，方便界面层只刷新受影响的 Item。
    /// </summary>
    private string GrantPetReward(MPPetRewardConfig reward)
    {
        if (reward == null || reward.Count <= 0 || string.IsNullOrEmpty(reward.Type))
            return null;

        string rewardType = reward.Type.Trim();
        string rewardTypeLower = rewardType.ToLowerInvariant();

        switch (rewardTypeLower)
        {
            case "coin":
                AddCoins(reward.Count);
                return null;
            case "diamond":
                AddDiamond(reward.Count);
                return null;
            case "hint":
                AddHintProps(reward.Count);
                return null;
            case "life":
                AddLoveRecoverProps(reward.Count);
                return null;
            case "food":
            case "toy":
                return GrantPetCareReward(reward);
        }

        Debug.LogWarning($"Unknown pet reward type: {rewardType}");
        return null;
    }

    /// <summary>
    /// 发放食物/玩具奖励。配置必须填写奖励 id，避免配置错误时静默发到默认物品。
    /// </summary>
    private string GrantPetCareReward(MPPetRewardConfig reward)
    {
        if (reward == null || string.IsNullOrEmpty(reward.ID))
        {
            Debug.LogWarning($"Pet care reward id is empty. Type: {reward?.Type}");
            return null;
        }

        string careItemId = reward.ID.Trim();
        if (GetPetCareRuntimeData(careItemId) == null)
        {
            Debug.LogWarning($"Pet care reward id is not found: {careItemId}");
            return null;
        }

        AddPetCareItemCount(careItemId, reward.Count);
        return careItemId;
    }

    public bool UsePetCareItem(string petId, MPPetCareItemConfig itemConfig)
    {
        if (!PetCareItemCanUse(petId, itemConfig))
            return false;

        MPPetCareRuntimeData careData = GetPetCareRuntimeData(itemConfig.ID);
        MPPetRuntimeData petData = GetPetRuntimeData(petId);
        switch (itemConfig.RestoreType)
        {
            case MPPetRestoreType.Health:
                petData.health = Mathf.Clamp(petData.health + itemConfig.RestorePercent, 0f, 100f);
                break;
            case MPPetRestoreType.Mood:
                petData.mood = Mathf.Clamp(petData.mood + itemConfig.RestorePercent, 0f, 100f);
                break;
        }

        // 使用恢复道具后从当前时间继续做状态衰减，避免旧的 lastStatusTicks 立刻抵消恢复值。
        petData.lastStatusTicks = DateTime.UtcNow.Ticks;
        careData.count = Mathf.Max(0, careData.count - 1);
        careData.quantityInitialized = true;
        SavePetsRuntime();
        SavePetCareRuntime();
        return true;
    }

    public void ApplyPetStatusDecay(List<MPPetConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return;

        List<MPPetRuntimeData> list = GetPetRuntimeList();
        long nowTicks = DateTime.UtcNow.Ticks;
        bool changed = false;

        for (int i = 0; i < list.Count; i++)
        {
            MPPetRuntimeData data = list[i];
            if (data == null || !data.unlocked)
                continue;

            long elapsedTicks = nowTicks - data.lastStatusTicks;
            if (elapsedTicks <= TimeSpan.FromSeconds(PET_STATUS_SAVE_INTERVAL_SECONDS).Ticks)
                continue;

            MPPetConfig config = configs.Find(item => item != null && item.ID == data.id);
            if (config == null)
                continue;

            float elapsedHours = (float)TimeSpan.FromTicks(elapsedTicks).TotalHours;
            // 根据离线/在线经过时间统一扣减，避免只在界面打开期间才下降。
            data.health = Mathf.Clamp(data.health - config.HealthDecayPerHour * elapsedHours, 0f, 100f);
            data.mood = Mathf.Clamp(data.mood - config.MoodDecayPerHour * elapsedHours, 0f, 100f);
            data.lastStatusTicks = nowTicks;
            changed = true;
        }

        if (changed)
        {
            SavePetsRuntime();
        }
    }

    private void SavePetsRuntime()
    {
        ES3.Save(m_key_pets_json, JsonConvert.SerializeObject(m_pet_runtime_list));
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }

    private void SavePetCareRuntime()
    {
        ES3.Save(m_key_pet_care_items_json, JsonConvert.SerializeObject(m_pet_care_runtime_list));
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }

    public int GetPetRewardInventoryCount(string rewardType)
    {
        if (string.IsNullOrEmpty(rewardType))
            return 0;

        if (m_pet_reward_inventory == null)
        {
            m_pet_reward_inventory = ES3.Load<Dictionary<string, int>>(m_key_pet_reward_inventory, new Dictionary<string, int>());
        }

        return m_pet_reward_inventory.TryGetValue(rewardType, out int count) ? count : 0;
    }

    private void AddPetRewardInventory(string rewardType, int count)
    {
        if (string.IsNullOrEmpty(rewardType) || count <= 0)
            return;

        if (m_pet_reward_inventory == null)
        {
            m_pet_reward_inventory = ES3.Load<Dictionary<string, int>>(m_key_pet_reward_inventory, new Dictionary<string, int>());
        }

        if (!m_pet_reward_inventory.ContainsKey(rewardType))
        {
            m_pet_reward_inventory.Add(rewardType, 0);
        }

        m_pet_reward_inventory[rewardType] += count;
        ES3.Save(m_key_pet_reward_inventory, m_pet_reward_inventory);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }

    private List<MPPetRuntimeData> DeserializePetRuntimeList(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<MPPetRuntimeData>();
        }

        try
        {
            return JsonConvert.DeserializeObject<List<MPPetRuntimeData>>(json) ?? new List<MPPetRuntimeData>();
        }
        catch (Exception)
        {
            // 旧版本或异常存档无法解析时，返回空列表并由配置同步流程重新补齐。
            return new List<MPPetRuntimeData>();
        }
    }

    private List<MPPetCareRuntimeData> DeserializePetCareRuntimeList(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<MPPetCareRuntimeData>();
        }

        try
        {
            return JsonConvert.DeserializeObject<List<MPPetCareRuntimeData>>(json) ?? new List<MPPetCareRuntimeData>();
        }
        catch (Exception)
        {
            return new List<MPPetCareRuntimeData>();
        }
    }
}

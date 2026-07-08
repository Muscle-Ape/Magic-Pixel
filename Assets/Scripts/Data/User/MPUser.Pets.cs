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
    /// 宠物运行时数据列表，和静态配置分离，便于热更配置和兼容旧存档。
    /// </summary>
    private List<MPPetRuntimeData> m_pet_runtime_list;

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
        string json = ES3.Load<string>(m_key_pets_json, defaultValue: null);
        m_pet_runtime_list = DeserializePetRuntimeList(json);
        m_pet_reward_inventory = ES3.Load<Dictionary<string, int>>(m_key_pet_reward_inventory, new Dictionary<string, int>());
        m_selected_pet_id = ES3.Load<string>(m_key_selected_pet_id, defaultValue: null);

        SyncPetRuntimeConfigs(MPDataManager.Instance.m_petsModel?.petConfigs);
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

    public bool PetIsUnlock(string id)
    {
        MPPetRuntimeData data = GetPetRuntimeData(id);
        return data != null && data.unlocked;
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

    public bool ClaimPetReward(MPPetConfig config)
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
            if (reward == null)
                continue;

            if (reward.Type == "coin")
            {
                // 金币接入项目已有资产系统，其它奖励先进入宠物临时背包。
                AddCoins(reward.Count);
            }
            else
            {
                AddPetRewardInventory(reward.Type, reward.Count);
            }
        }

        // 领取成功后重置本轮奖励倒计时。
        data.rewardStartTicks = DateTime.UtcNow.Ticks;
        SavePetsRuntime();
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
}

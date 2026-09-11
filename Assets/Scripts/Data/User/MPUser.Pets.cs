using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物选择与领取数据。默认宠物初始可用，其他宠物只有主动领取成功后才允许选择和使用技能。
/// </summary>
public partial class MPUser
{
    private string m_key_selected_pet_id = "key_selected_pet_id";
    private string m_selected_pet_id;
    private HashSet<string> m_claimedPetIds;
    private string m_claimedPetOwner;

    private void InitPets()
    {
        m_selected_pet_id = ES3.Load<string>(m_key_selected_pet_id, defaultValue: null);
        SetClaimedPetsInMemory(CreateRewardProgressSnapshot(), GetRewardProgressOwner());
        SyncPetSelection(MPDataManager.Instance.m_petsModel?.petConfigs);
    }

    /// <summary>
    /// 只在默认宠物和已经领取的宠物中校验选中项；不会按主线进度自动解锁其他宠物。
    /// </summary>
    public void SyncPetSelection(List<MPPetConfig> configs)
    {
        if (configs == null)
            return;

        MPPetConfig selected = FindPetConfig(configs, m_selected_pet_id);
        EnsureClaimedPetsLoaded();
        if (selected != null && (selected.DefaultUnlocked || m_claimedPetIds.Contains(selected.ID)))
            return;

        MPPetConfig firstUnlocked = configs.Find(
            config => config != null && (config.DefaultUnlocked || m_claimedPetIds.Contains(config.ID)));
        string fallbackId = firstUnlocked?.ID;
        if (m_selected_pet_id == fallbackId)
            return;

        m_selected_pet_id = fallbackId;
        SaveSelectedPet();
    }

    public bool PetIsUnlock(string id)
    {
        MPPetConfig config = FindPetConfig(
            MPDataManager.Instance.m_petsModel?.petConfigs,
            id);
        if (config == null)
            return false;
        if (config.DefaultUnlocked)
            return true;

        EnsureClaimedPetsLoaded();
        return m_claimedPetIds.Contains(config.ID);
    }

    /// <summary>只判断是否具备领取资格，不能用于判断已拥有、自动选中或开放技能。</summary>
    public bool PetUnlockConditionIsMet(MPPetConfig config)
    {
        if (config == null)
            return false;

        if (config.DefaultUnlocked)
            return true;

        if (!config.TryGetUnlockRequirement(out string type, out int value))
            return false;

        switch (type)
        {
            case "default":
            case "free":
            case "unlocked":
                return true;
            case "mainlevel":
                return HasCompletedMainLevel(value);
            default:
                return false;
        }
    }

    /// <summary>
    /// 由奖励领取入口确认后调用。只保存一次，同一宠物重复确认不会重复发放。
    /// 打开主页、点击锁定宠物或打开/取消领取弹窗均不调用此方法。
    /// </summary>
    public bool TryClaimPet(string petId)
    {
        MPPetConfig config = FindPetConfig(MPDataManager.Instance.m_petsModel?.petConfigs, petId);
        if (config == null)
            return false;
        // 默认宠物本来就可用，不需要写入领取记录。
        if (config.DefaultUnlocked)
            return true;

        try
        {
            MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
            if (progress.claimedPetIds.Contains(config.ID))
                return true;
            if (!PetUnlockConditionIsMet(config))
                return false;

            progress.claimedPetIds.Add(config.ID);
            // 先保存再更新内存，写盘失败不会导致 UI 提前解锁；沿用账号隔离的奖励存档与云同步。
            ApplyRewardProgressSnapshot(progress);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPUser] 宠物领取未成功：{exception.Message}");
            return false;
        }
    }

    private void EnsureClaimedPetsLoaded()
    {
        string owner = GetRewardProgressOwner();
        if (m_claimedPetIds == null || m_claimedPetOwner != owner)
            SetClaimedPetsInMemory(CreateRewardProgressSnapshot(), owner);
    }

    private void SetClaimedPetsInMemory(MPRewardProgressSnapshot progress, string owner)
    {
        m_claimedPetIds = new HashSet<string>(progress?.claimedPetIds ?? new List<string>(), StringComparer.Ordinal);
        m_claimedPetOwner = owner;
    }

    public string GetSelectedPetId()
    {
        return m_selected_pet_id;
    }

    public MPPetConfig GetSelectedPetConfig()
    {
        MPPetConfig config = FindPetConfig(
            MPDataManager.Instance.m_petsModel?.petConfigs,
            m_selected_pet_id);
        return config != null && PetIsUnlock(config.ID) ? config : null;
    }

    public void SetSelectedPet(string id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id == m_selected_pet_id
            || !PetIsUnlock(id))
        {
            return;
        }

        m_selected_pet_id = id;
        SaveSelectedPet();
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }

    /// <summary>
    /// 发放携带宠物首次完成正式关卡时的额外奖励。
    /// mode 与 levelId 共同作为幂等键，避免重复结算或重复回调造成多次入账。
    /// </summary>
    public bool TryGrantPetCompletionReward(
        MPPetConfig pet,
        string mode,
        string levelId,
        out MPRewardReceipt receipt)
    {
        receipt = null;
        MPPetRewardConfig reward = pet?.CompletionReward;
        if (reward == null
            || !reward.IsValid
            || string.IsNullOrWhiteSpace(mode)
            || string.IsNullOrWhiteSpace(levelId))
        {
            return false;
        }

        string sourceId = $"{mode}:{levelId}";
        var result = new MPRewardReceipt
        {
            sourceId = sourceId,
            sourceName = "Pet level bonus",
            transactionId = $"pet_completion:{sourceId}",
            rewards = new List<MPRewardItem>
            {
                new MPRewardItem(reward.Type, reward.Count)
            }
        };
        if (!TryGrantRewards(result))
            return false;

        receipt = result;
        return true;
    }

    private void SaveSelectedPet()
    {
        if (string.IsNullOrEmpty(m_selected_pet_id))
        {
            if (ES3.KeyExists(m_key_selected_pet_id))
                ES3.DeleteKey(m_key_selected_pet_id);
            return;
        }

        ES3.Save(m_key_selected_pet_id, m_selected_pet_id);
    }

    private bool HasCompletedMainLevel(int levelNumber)
    {
        if (levelNumber <= 0)
            return true;

        List<MPMainBlockInfo> levels = MPDataManager.Instance.m_mainLevelModel?.blockInfos;
        int levelIndex = levelNumber - 1;
        if (levels != null && levelIndex >= 0 && levelIndex < levels.Count)
        {
            MPMainBlockInfo level = levels[levelIndex];
            if (level != null && MainLevelIsPass(level.ID))
                return true;
        }

        return GetMainLevlPassIndex() >= levelNumber;
    }

    private static MPPetConfig FindPetConfig(List<MPPetConfig> configs, string id)
    {
        if (configs == null || string.IsNullOrWhiteSpace(id))
            return null;

        return configs.Find(config => config != null && config.ID == id);
    }
}

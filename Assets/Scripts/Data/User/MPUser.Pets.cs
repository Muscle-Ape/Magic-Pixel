using System;
using System.Collections.Generic;

/// <summary>
/// 宠物选择与解锁数据。宠物解锁状态直接由静态配置和主线进度计算，
/// 本地及云端只需要保存当前选中的宠物 ID。
/// </summary>
public partial class MPUser
{
    private string m_key_selected_pet_id = "key_selected_pet_id";
    private string m_selected_pet_id;

    private void InitPets()
    {
        m_selected_pet_id = ES3.Load<string>(m_key_selected_pet_id, defaultValue: null);
        SyncPetSelection(MPDataManager.Instance.m_petsModel?.petConfigs);
    }

    /// <summary>
    /// 根据配置与当前解锁进度校验选中项。
    /// </summary>
    public void SyncPetSelection(List<MPPetConfig> configs)
    {
        if (configs == null)
            return;

        MPPetConfig selected = FindPetConfig(configs, m_selected_pet_id);
        if (selected != null && PetUnlockConditionIsMet(selected))
            return;

        MPPetConfig firstUnlocked = configs.Find(
            config => config != null && PetUnlockConditionIsMet(config));
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
        return PetUnlockConditionIsMet(config);
    }

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

    public string GetSelectedPetId()
    {
        return m_selected_pet_id;
    }

    public MPPetConfig GetSelectedPetConfig()
    {
        MPPetConfig config = FindPetConfig(
            MPDataManager.Instance.m_petsModel?.petConfigs,
            m_selected_pet_id);
        return PetUnlockConditionIsMet(config) ? config : null;
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

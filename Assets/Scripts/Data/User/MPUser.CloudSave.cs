using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public partial class MPUser
{
    /// <summary>
    /// 当前是否正在从本地 ES3 初始化用户数据。
    /// 初始化期间产生的默认补齐保存不应触发云端 dirty。
    /// </summary>
    private bool m_isInitializingUserData;

    /// <summary>
    /// 当前是否正在应用云端快照。
    /// 应用云端数据时会写回 ES3，但这些写入不应再次触发上传。
    /// </summary>
    private bool m_isApplyingCloudSnapshot;

    /// <summary>
    /// 从当前 MPUser 内存数据创建云端用户快照。
    /// </summary>
    public MPUserCloudSnapshot CreateCloudSnapshot()
    {
        return new MPUserCloudSnapshot
        {
            schemaVersion = MPCloudSaveConstants.USER_SNAPSHOT_SCHEMA_VERSION,
            updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            clientVersion = Application.version,
            assets = new MPUserAssetsSnapshot
            {
                coins = Mathf.Max(0, m_coins),
                diamond = Mathf.Max(0, m_diamond),
                hintProps = Mathf.Max(0, m_hintProps),
                loveRecoverProps = Mathf.Max(0, m_loveRecoverProps),
                homeRewardReadyAtUtcTicks = m_homeRewardReadyAtUtcTicks
            },
            settings = new MPUserSettingsSnapshot
            {
                isMusic = m_isMusic,
                isSound = m_isSound,
                isVibration = m_isVibration
            },
            mainLevel = new MPUserMainLevelSnapshot
            {
                passIndex = Mathf.Max(0, m_mainlevel_pass_index),
                unlockList = CopyStringList(m_mainlevel_unlocklist),
                passList = CopyStringList(m_mainlevel_passlist),
                stars = CopyIntDictionary(m_mainlevel_stars)
            },
            largeImageLevel = new MPUserLargeImageLevelSnapshot
            {
                passIndex = Mathf.Max(0, m_largeimagelevel_pass_index),
                unlockList = CopyStringList(m_largeimagelevel_unlocklist),
                passList = CopyStringList(m_largeimagelevel_passlist),
                stars = CopyIntDictionary(m_largeimagelevel_stars),
                coinAwardClaimedList = CopyStringList(m_largeimagelevel_coin_award_claimed)
            },
            pets = new MPUserPetsSnapshot
            {
                selectedPetId = m_selected_pet_id
            }
        };
    }

    /// <summary>
    /// 从当前 MPUser 内存数据创建自定义关卡独立云快照。
    /// </summary>
    public MPCustomLevelCloudSnapshot CreateCustomLevelCloudSnapshot()
    {
        return new MPCustomLevelCloudSnapshot
        {
            schemaVersion = MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION,
            updatedAtUtcTicks = DateTime.UtcNow.Ticks,
            clientVersion = Application.version,
            customLevel = new MPUserCustomLevelSnapshot
            {
                levels = NormalizeCustomLevels(CloneByJson(GetCustomLevels()) ?? new List<MPCustomLevelInfo>()),
                passList = CopyStringList(m_customlevel_passlist)
            }
        };
    }

    /// <summary>
    /// 将云端用户快照应用到当前 MPUser，并写回 ES3。
    /// </summary>
    public void ApplyCloudSnapshot(MPUserCloudSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        m_isApplyingCloudSnapshot = true;
        try
        {
            ApplyAssetsSnapshot(snapshot.assets);
            ApplySettingsSnapshot(snapshot.settings);
            ApplyMainLevelSnapshot(snapshot.mainLevel);
            ApplyLargeImageLevelSnapshot(snapshot.largeImageLevel);
            ApplyPetsSnapshot(snapshot.pets);
        }
        finally
        {
            m_isApplyingCloudSnapshot = false;
        }
    }

    /// <summary>
    /// 将云端自定义关卡独立快照应用到当前 MPUser，并写回 ES3。
    /// </summary>
    public void ApplyCustomLevelCloudSnapshot(MPCustomLevelCloudSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        m_isApplyingCloudSnapshot = true;
        try
        {
            ApplyCustomLevelSnapshot(snapshot.customLevel);
        }
        finally
        {
            m_isApplyingCloudSnapshot = false;
        }
    }

    /// <summary>
    /// 通知云同步层本地数据发生变化。
    /// </summary>
    private void NotifyCloudSaveDirty(MPCloudSaveDirtyReason reason)
    {
        if (m_isInitializingUserData || m_isApplyingCloudSnapshot)
        {
            return;
        }

        MPCloudSaveManager.Instance.MarkDirty(reason);
    }

    /// <summary>
    /// 应用资产快照。
    /// </summary>
    private void ApplyAssetsSnapshot(MPUserAssetsSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserAssetsSnapshot();
        m_coins = Mathf.Max(0, snapshot.coins);
        m_diamond = Mathf.Max(0, snapshot.diamond);
        m_hintProps = Mathf.Max(0, snapshot.hintProps);
        m_loveRecoverProps = Mathf.Max(0, snapshot.loveRecoverProps);

        // 旧版云端快照没有该字段，反序列化后为 0。
        // 此时必须保留 ES3 已加载的本地 UTC 时间，不能在每次登录同步时重新开始三小时倒计时。
        if (IsValidHomeRewardReadyAtUtcTicks(snapshot.homeRewardReadyAtUtcTicks))
        {
            // 本地或其他设备领取后都会把时间向后推进，取较晚值可避免旧云快照把倒计时回退。
            m_homeRewardReadyAtUtcTicks = IsValidHomeRewardReadyAtUtcTicks(m_homeRewardReadyAtUtcTicks)
                ? Math.Max(m_homeRewardReadyAtUtcTicks, snapshot.homeRewardReadyAtUtcTicks)
                : snapshot.homeRewardReadyAtUtcTicks;
        }
        EnsureHomeRewardCountdown();

        ES3.Save(m_key_coins, m_coins);
        ES3.Save(m_ket_diamond, m_diamond);
        ES3.Save(m_key_hint_props, m_hintProps);
        ES3.Save(m_key_love_recover_props, m_loveRecoverProps);
        ES3.Save(m_key_home_reward_ready_at_utc_ticks, m_homeRewardReadyAtUtcTicks);
    }

    /// <summary>
    /// 应用设置快照。
    /// </summary>
    private void ApplySettingsSnapshot(MPUserSettingsSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserSettingsSnapshot();
        m_isMusic = snapshot.isMusic;
        m_isSound = snapshot.isSound;
        m_isVibration = snapshot.isVibration;

        ES3.Save(m_key_isMusic, m_isMusic);
        ES3.Save(m_key_isSound, m_isSound);
        ES3.Save(m_key_isVibration, m_isVibration);
    }

    /// <summary>
    /// 应用主线关卡快照。
    /// </summary>
    private void ApplyMainLevelSnapshot(MPUserMainLevelSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserMainLevelSnapshot();
        m_mainlevel_pass_index = Mathf.Max(0, snapshot.passIndex);
        m_mainlevel_unlocklist = CopyStringList(snapshot.unlockList);
        m_mainlevel_passlist = CopyStringList(snapshot.passList);
        m_mainlevel_stars = CopyIntDictionary(snapshot.stars);

        ES3.Save(m_key_mainlevel_pass_index, m_mainlevel_pass_index);
        ES3.Save(m_key_mainlevel_unlocklist, m_mainlevel_unlocklist);
        ES3.Save(m_key_mainlevel_passlist, m_mainlevel_passlist);
        ES3.Save(m_key_mainlevel_stars, m_mainlevel_stars);

        if (MPDataManager.Instance.m_mainLevelModel?.blockInfos != null && MPDataManager.Instance.m_mainLevelModel.blockInfos.Count > 0)
        {
            MainLevelUnlock(MPDataManager.Instance.m_mainLevelModel.blockInfos[0].ID);
        }
    }

    /// <summary>
    /// 应用大图关卡快照。
    /// </summary>
    private void ApplyLargeImageLevelSnapshot(MPUserLargeImageLevelSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserLargeImageLevelSnapshot();
        m_largeimagelevel_pass_index = Mathf.Max(0, snapshot.passIndex);
        m_largeimagelevel_unlocklist = CopyStringList(snapshot.unlockList);
        m_largeimagelevel_passlist = CopyStringList(snapshot.passList);
        m_largeimagelevel_stars = CopyIntDictionary(snapshot.stars);
        m_largeimagelevel_coin_award_claimed = CopyStringList(snapshot.coinAwardClaimedList);

        ES3.Save(m_key_largeimagelevel_pass_index, m_largeimagelevel_pass_index);
        ES3.Save(m_key_largeimagelevel_unlocklist, m_largeimagelevel_unlocklist);
        ES3.Save(m_key_largeimagelevel_passlist, m_largeimagelevel_passlist);
        ES3.Save(m_key_largeimagelevel_stars, m_largeimagelevel_stars);
        ES3.Save(m_key_largeimagelevel_coin_award_claimed, m_largeimagelevel_coin_award_claimed);

        if (MPDataManager.Instance.m_largeImageModel?.blockInfos != null && MPDataManager.Instance.m_largeImageModel.blockInfos.Count > 0)
        {
            LargeImageLevelUnlock(MPDataManager.Instance.m_largeImageModel.blockInfos[0].ID);
        }
    }

    /// <summary>
    /// 应用自定义关卡快照。
    /// </summary>
    private void ApplyCustomLevelSnapshot(MPUserCustomLevelSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserCustomLevelSnapshot();
        m_customlevel_list = NormalizeCustomLevels(CloneByJson(snapshot.levels) ?? new List<MPCustomLevelInfo>());
        m_customlevel_passlist = CopyStringList(snapshot.passList);

        ES3.Save(m_key_customlevel_json, JsonConvert.SerializeObject(m_customlevel_list));
        ES3.Save(m_key_customlevel_passlist, m_customlevel_passlist);
    }

    /// <summary>
    /// 应用宠物快照。
    /// </summary>
    private void ApplyPetsSnapshot(MPUserPetsSnapshot snapshot)
    {
        snapshot = snapshot ?? new MPUserPetsSnapshot();
        m_selected_pet_id = snapshot.selectedPetId;

        if (string.IsNullOrEmpty(m_selected_pet_id))
        {
            if (ES3.KeyExists(m_key_selected_pet_id))
            {
                ES3.DeleteKey(m_key_selected_pet_id);
            }
        }
        else
        {
            ES3.Save(m_key_selected_pet_id, m_selected_pet_id);
        }

        MPPetsModel petsModel = MPDataManager.Instance.m_petsModel;
        SyncPetSelection(petsModel?.petConfigs);
    }

    /// <summary>
    /// 复制字符串列表并过滤空值。
    /// </summary>
    private static List<string> CopyStringList(List<string> source)
    {
        List<string> result = new List<string>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            string value = source[i];
            if (!string.IsNullOrEmpty(value) && !result.Contains(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    /// <summary>
    /// 复制 int 字典并过滤空 Key。
    /// </summary>
    private static Dictionary<string, int> CopyIntDictionary(Dictionary<string, int> source)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        if (source == null)
        {
            return result;
        }

        foreach (KeyValuePair<string, int> pair in source)
        {
            if (!string.IsNullOrEmpty(pair.Key))
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 使用 JSON 做一次简单深拷贝，避免云同步快照继续引用 MPUser 内部列表对象。
    /// </summary>
    private static T CloneByJson<T>(T value)
    {
        if (value == null)
        {
            return default;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Clone snapshot value failed: {exception.Message}");
            return default;
        }
    }
}

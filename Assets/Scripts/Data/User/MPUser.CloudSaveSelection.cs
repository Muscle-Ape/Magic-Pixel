using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public partial class MPUser
{
    /// <summary>
    /// 显式选择整份存档：先规范化候选，ES3File 一次提交成功后才替换内存。
    /// 不复用自动同步中的逐 Key 写入，磁盘持续失败时也无需再写盘回滚。
    /// </summary>
    public void ApplyChosenCloudSnapshots(MPUserCloudSnapshot user, MPCustomLevelCloudSnapshot custom)
    {
        if (user == null || custom == null)
            throw new ArgumentNullException("A complete save pair is required.");
        string owner = MPLoginManager.Instance.PlayerId;
        if (string.IsNullOrEmpty(owner) || (!string.IsNullOrEmpty(user.playerId) && user.playerId != owner) ||
            (!string.IsNullOrEmpty(custom.playerId) && custom.playerId != owner))
            throw new InvalidOperationException("The selected save belongs to another account.");

        // 严格深拷贝，解析失败必须终止，不能把失败当成空存档覆盖。
        MPUserCloudSnapshot candidate = JsonConvert.DeserializeObject<MPUserCloudSnapshot>(JsonConvert.SerializeObject(user));
        MPCustomLevelCloudSnapshot customCandidate = JsonConvert.DeserializeObject<MPCustomLevelCloudSnapshot>(JsonConvert.SerializeObject(custom));
        MPUserAssetsSnapshot assets = candidate.assets ?? new MPUserAssetsSnapshot();
        MPUserSettingsSnapshot settings = candidate.settings ?? new MPUserSettingsSnapshot();
        MPUserMainLevelSnapshot main = candidate.mainLevel ?? new MPUserMainLevelSnapshot();
        MPUserLargeImageLevelSnapshot large = candidate.largeImageLevel ?? new MPUserLargeImageLevelSnapshot();
        MPUserCustomLevelSnapshot levels = customCandidate.customLevel ?? new MPUserCustomLevelSnapshot();

        assets.coins = Mathf.Max(0, assets.coins);
        assets.diamond = Mathf.Max(0, assets.diamond);
        assets.hintProps = Mathf.Max(0, assets.hintProps);
        assets.loveRecoverProps = Mathf.Max(0, assets.loveRecoverProps);
        if (!IsValidHomeRewardReadyAtUtcTicks(assets.homeRewardReadyAtUtcTicks))
            assets.homeRewardReadyAtUtcTicks = IsValidHomeRewardReadyAtUtcTicks(m_homeRewardReadyAtUtcTicks)
                ? m_homeRewardReadyAtUtcTicks : DateTime.UtcNow.Ticks + HOME_REWARD_INTERVAL_TICKS;
        Color fillColor = m_gameFillColor;
        if (!string.IsNullOrEmpty(settings.gameFillColor) && ColorUtility.TryParseHtmlString(settings.gameFillColor, out Color parsedColor))
            fillColor = parsedColor;
        fillColor = NormalizeGameFillColor(fillColor);

        main.passIndex = Mathf.Max(0, main.passIndex);
        main.unlockList = CopyStringList(main.unlockList);
        main.passList = CopyStringList(main.passList);
        main.stars = CopyIntDictionary(main.stars);
        main.boxAwardClaimedList = CopyStringList(main.boxAwardClaimedList);
        large.passIndex = Mathf.Max(0, large.passIndex);
        large.unlockList = CopyStringList(large.unlockList);
        large.passList = CopyStringList(large.passList);
        large.stars = CopyIntDictionary(large.stars);
        large.coinAwardClaimedList = CopyStringList(large.coinAwardClaimedList);
        levels.levels = NormalizeCustomLevels(levels.levels ?? new List<MPCustomLevelInfo>());
        levels.passList = CopyStringList(levels.passList);
        var mainConfigs = MPDataManager.Instance.m_mainLevelModel?.blockInfos;
        if (mainConfigs != null && mainConfigs.Count > 0 && mainConfigs[0] != null && !main.unlockList.Contains(mainConfigs[0].ID))
            main.unlockList.Add(mainConfigs[0].ID);
        var largeConfigs = MPDataManager.Instance.m_largeImageModel?.blockInfos;
        if (largeConfigs != null && largeConfigs.Count > 0 && largeConfigs[0] != null && !large.unlockList.Contains(largeConfigs[0].ID))
            large.unlockList.Add(largeConfigs[0].ID);

        string selectedPetId = NormalizeChosenPet(candidate.pets?.selectedPetId, main);
        MPRewardProgressSnapshot rewards = candidate.rewardProgress;
        if (rewards == null)
        {
            // 旧云端缺少该字段时保留同账号本地记录；此处只能读取，不能触发owner补写。
            string oldRewards = ES3.Load<string>(REWARD_PROGRESS_KEY_PREFIX + owner, defaultValue: null);
            rewards = string.IsNullOrEmpty(oldRewards) ? new MPRewardProgressSnapshot()
                : JsonConvert.DeserializeObject<MPRewardProgressSnapshot>(oldRewards);
            if (rewards == null) throw new InvalidOperationException("Reward progress could not be read.");
        }
        NormalizeRewardProgress(rewards);
        string customJson = JsonConvert.SerializeObject(levels.levels);
        string rewardJson = JsonConvert.SerializeObject(rewards);

        var file = new ES3File();
        file.Save(m_key_coins, assets.coins);
        file.Save(m_ket_diamond, assets.diamond);
        file.Save(m_key_hint_props, assets.hintProps);
        file.Save(m_key_love_recover_props, assets.loveRecoverProps);
        file.Save(m_key_home_reward_ready_at_utc_ticks, assets.homeRewardReadyAtUtcTicks);
        file.Save(m_key_isMusic, settings.isMusic);
        file.Save(m_key_isSound, settings.isSound);
        file.Save(m_key_isVibration, settings.isVibration);
        file.Save(GAME_FILL_COLOR_KEY, "#" + ColorUtility.ToHtmlStringRGBA(fillColor));
        file.Save(m_key_mainlevel_pass_index, main.passIndex);
        file.Save(m_key_mainlevel_unlocklist, main.unlockList);
        file.Save(m_key_mainlevel_passlist, main.passList);
        file.Save(m_key_mainlevel_stars, main.stars);
        file.Save(m_key_mainlevel_box_award_claimed, main.boxAwardClaimedList);
        file.Save(m_key_largeimagelevel_pass_index, large.passIndex);
        file.Save(m_key_largeimagelevel_unlocklist, large.unlockList);
        file.Save(m_key_largeimagelevel_passlist, large.passList);
        file.Save(m_key_largeimagelevel_stars, large.stars);
        file.Save(m_key_largeimagelevel_coin_award_claimed, large.coinAwardClaimedList);
        file.Save(m_key_customlevel_json, customJson);
        file.Save(m_key_customlevel_passlist, levels.passList);
        file.Save(m_key_selected_pet_id, selectedPetId ?? string.Empty);
        file.Save(REWARD_PROGRESS_OWNER_KEY, owner);
        file.Save(REWARD_PROGRESS_KEY_PREFIX + owner, rewardJson);
        file.Sync();

        // 此后仅做引用/值赋值，不再序列化、执行回调或写盘。
        m_coins = assets.coins;
        m_diamond = assets.diamond;
        m_hintProps = assets.hintProps;
        m_loveRecoverProps = assets.loveRecoverProps;
        m_homeRewardReadyAtUtcTicks = assets.homeRewardReadyAtUtcTicks;
        m_isMusic = settings.isMusic;
        m_isSound = settings.isSound;
        m_isVibration = settings.isVibration;
        m_gameFillColor = fillColor;
        m_mainlevel_pass_index = main.passIndex;
        m_mainlevel_unlocklist = main.unlockList;
        m_mainlevel_passlist = main.passList;
        m_mainlevel_stars = main.stars;
        m_mainlevel_box_award_claimed = main.boxAwardClaimedList;
        m_largeimagelevel_pass_index = large.passIndex;
        m_largeimagelevel_unlocklist = large.unlockList;
        m_largeimagelevel_passlist = large.passList;
        m_largeimagelevel_stars = large.stars;
        m_largeimagelevel_coin_award_claimed = large.coinAwardClaimedList;
        m_customlevel_list = levels.levels;
        m_customlevel_passlist = levels.passList;
        m_selected_pet_id = selectedPetId;
    }

    private static string NormalizeChosenPet(string selectedPetId, MPUserMainLevelSnapshot main)
    {
        List<MPPetConfig> configs = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (configs == null) return selectedPetId;
        MPPetConfig selected = configs.Find(config => config != null && config.ID == selectedPetId && ChosenPetIsUnlocked(config, main));
        return selected != null ? selected.ID : configs.Find(config => config != null && ChosenPetIsUnlocked(config, main))?.ID;
    }

    private static bool ChosenPetIsUnlocked(MPPetConfig config, MPUserMainLevelSnapshot main)
    {
        if (config.DefaultUnlocked) return true;
        if (!config.TryGetUnlockRequirement(out string type, out int value)) return false;
        if (type == "free" || type == "default" || type == "unlocked") return true;
        if (type != "mainlevel") return false;
        if (value <= 0 || main.passIndex >= value) return true;
        var configs = MPDataManager.Instance.m_mainLevelModel?.blockInfos;
        return configs != null && value <= configs.Count && configs[value - 1] != null && main.passList.Contains(configs[value - 1].ID);
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>一次已入账的奖励结果。弹窗只展示此结果，不负责发奖。</summary>
[Serializable]
public sealed class MPRewardReceipt
{
    public string sourceId;
    public string transactionId;
    public string sourceName;
    public List<MPRewardItem> rewards = new List<MPRewardItem>();
}

[Serializable]
public sealed class MPRewardItem
{
    public string type;
    public int amount;
    public string icon;

    public MPRewardItem() { }
    public MPRewardItem(string type, int amount, string icon = null)
    {
        this.type = type;
        this.amount = amount;
        this.icon = icon;
    }
}

/// <summary>奖励与领取提示的账号级快照，旧存档缺失时使用空值。</summary>
[Serializable]
public sealed class MPRewardProgressSnapshot
{
    public List<string> transactionIds = new List<string>();
    public long signInLastClaimDay;
    public int signInClaimedDays;
    public long signInLatestObservedUtcTicks;
    // null 只用于识别没有稳定条目 ID 的旧存档，读取后会规范化为空集合。
    public List<string> signInClaimedEntryIds;
    public int signInLegacyClaimedDays;
    public int signInLegacyMappedDays;
    public List<string> unlockedPetIds = new List<string>();
    public List<string> notifiedPetIds = new List<string>();
}

public partial class MPUser
{
    private const string REWARD_PROGRESS_KEY_PREFIX = "key_reward_progress_v1_";
    private const string REWARD_PROGRESS_OWNER_KEY = "key_reward_progress_owner_v1";

    /// <summary>离线沿用最近本地账号，已登录账号各自隔离，避免切号串签到记录。</summary>
    public string GetRewardProgressOwner()
    {
        string owner = MPLoginManager.Instance.PlayerId;
        if (string.IsNullOrEmpty(owner))
            return ES3.Load<string>(REWARD_PROGRESS_OWNER_KEY, defaultValue: "offline");

        string previous = ES3.Load<string>(REWARD_PROGRESS_OWNER_KEY, defaultValue: "offline");
        if (previous != owner)
            ES3.Save(REWARD_PROGRESS_OWNER_KEY, owner);
        return owner;
    }

    public MPRewardProgressSnapshot CreateRewardProgressSnapshot()
    {
        string json = ES3.Load<string>(REWARD_PROGRESS_KEY_PREFIX + GetRewardProgressOwner(), defaultValue: null);
        if (string.IsNullOrEmpty(json))
        {
            var empty = new MPRewardProgressSnapshot();
            NormalizeRewardProgress(empty);
            return empty;
        }

        // 已存在但损坏的领取记录不能当成空记录重新发奖。
        MPRewardProgressSnapshot state = JsonConvert.DeserializeObject<MPRewardProgressSnapshot>(json);
        if (state == null)
            throw new InvalidOperationException("Reward progress could not be read.");
        NormalizeRewardProgress(state);
        return state;
    }

    public void ApplyRewardProgressSnapshot(MPRewardProgressSnapshot state)
    {
        // 老云存档没有此字段，不可清掉此账号刚刚离线提交的幂等凭据。
        if (state == null) return;
        NormalizeRewardProgress(state);
        ES3.Save(REWARD_PROGRESS_KEY_PREFIX + GetRewardProgressOwner(), JsonConvert.SerializeObject(state));
    }

    private static void NormalizeRewardProgress(MPRewardProgressSnapshot state)
    {
        state.transactionIds = state.transactionIds ?? new List<string>();
        state.unlockedPetIds = state.unlockedPetIds ?? new List<string>();
        state.notifiedPetIds = state.notifiedPetIds ?? new List<string>();
        state.signInClaimedDays = Math.Max(0, state.signInClaimedDays);
        if (state.signInClaimedEntryIds == null)
        {
            state.signInClaimedEntryIds = new List<string>();
            state.signInLegacyClaimedDays = Math.Max(state.signInLegacyClaimedDays, state.signInClaimedDays);
        }
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        state.signInClaimedEntryIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !entryIds.Add(id));
        state.signInLegacyClaimedDays = Math.Max(0, state.signInLegacyClaimedDays);
        state.signInLegacyMappedDays = Math.Max(0, state.signInLegacyMappedDays);
        state.signInLastClaimDay = Math.Max(0L, state.signInLastClaimDay);
        state.signInLatestObservedUtcTicks = Math.Max(0L,
            Math.Min(DateTime.MaxValue.Ticks, state.signInLatestObservedUtcTicks));
    }

    public bool RewardTransactionIsCommitted(string transactionId)
    {
        return !string.IsNullOrEmpty(transactionId)
            && CreateRewardProgressSnapshot().transactionIds.Contains(transactionId);
    }

    /// <summary>外部已校验的奖励入口；transactionId 必须由具体业务生成，不可每次点击随机生成。</summary>
    public bool TryGrantRewards(MPRewardReceipt receipt)
    {
        return TryCommitReward(receipt, null, null, null, MPCloudSaveDirtyReason.Assets);
    }

    private bool TryCommitReward(MPRewardReceipt receipt, MPRewardProgressSnapshot state,
        Action<ES3File> saveSourceState, Action applySourceState, MPCloudSaveDirtyReason reason)
    {
        if (receipt == null || string.IsNullOrWhiteSpace(receipt.transactionId)
            || string.IsNullOrWhiteSpace(receipt.sourceId) || receipt.rewards == null)
            return false;

        try
        {
            state = state ?? CreateRewardProgressSnapshot();
            if (state.transactionIds.Contains(receipt.transactionId))
                return false;

            var totals = new Dictionary<string, MPRewardItem>();
            foreach (MPRewardItem reward in receipt.rewards)
            {
                if (reward == null || reward.amount <= 0)
                    continue;
                string type = MPRewardPresentation.NormalizeType(reward.type);
                if (string.IsNullOrEmpty(type))
                    return false;
                if (!totals.TryGetValue(type, out MPRewardItem total))
                    totals.Add(type, total = new MPRewardItem(type, 0, reward.icon));
                total.amount = checked(total.amount + reward.amount);
            }
            if (totals.Count == 0)
                return false;

            int coins = checked(m_coins + RewardAmount(totals, "coin"));
            int diamond = checked(m_diamond + RewardAmount(totals, "diamond"));
            int hints = checked(m_hintProps + RewardAmount(totals, "hint"));
            int lives = checked(m_loveRecoverProps + RewardAmount(totals, "life"));
            state.transactionIds.Add(receipt.transactionId);

            // ES3File 将资产、业务领取标记、幂等凭据合成一次文件提交。
            // 同步失败时保留原内存值，不依赖动画或多个 ES3.Save 的先后顺序。
            var file = new ES3File();
            file.Save(m_key_coins, coins);
            file.Save(m_ket_diamond, diamond);
            file.Save(m_key_hint_props, hints);
            file.Save(m_key_love_recover_props, lives);
            file.Save(REWARD_PROGRESS_KEY_PREFIX + GetRewardProgressOwner(), JsonConvert.SerializeObject(state));
            saveSourceState?.Invoke(file);
            file.Sync();

            m_coins = coins;
            m_diamond = diamond;
            m_hintProps = hints;
            m_loveRecoverProps = lives;
            applySourceState?.Invoke();
            receipt.rewards = new List<MPRewardItem>(totals.Values);
            NotifyCloudSaveDirty(reason);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPUser] Reward was not committed: {exception.Message}");
            return false;
        }
    }

    private static int RewardAmount(Dictionary<string, MPRewardItem> rewards, string type)
    {
        return rewards.TryGetValue(type, out MPRewardItem item) ? item.amount : 0;
    }
}

public static class MPRewardPresentation
{
    public static string NormalizeType(string type)
    {
        switch ((type ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "coin": case "coins": return "coin";
            case "diamond": return "diamond";
            case "hint": case "hint_prop": return "hint";
            case "life": case "recover_life": case "love_recover": return "life";
            default: return null;
        }
    }

    public static string Name(string type)
    {
        switch (NormalizeType(type))
        {
            case "coin": return "Coins";
            case "diamond": return "Diamonds";
            case "hint": return "Hints";
            case "life": return "Life recovery";
            default: return "Reward";
        }
    }

    public static string Icon(string type)
    {
        switch (NormalizeType(type))
        {
            case "coin": return "popup_reward_coin";
            case "diamond": return "popup_reward_diamond";
            case "hint": return "popup_reward_hint";
            case "life": return "popup_reward_life";
            default: return "popup_reward_placeholder";
        }
    }
}

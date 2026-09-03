using System;
using System.Collections.Generic;

public sealed class MPSignInStatus
{
    public long day;
    public int dayIndex = -1;
    public string entryId;
    public bool claimedToday;
    public bool clockIsValid;
    public bool hasConfiguredRewards;
    public HashSet<string> claimedEntryIds = new HashSet<string>(StringComparer.Ordinal);
    public bool CanClaim => hasConfiguredRewards && dayIndex >= 0 && clockIsValid && !claimedToday;
}

public partial class MPUser
{
    // 所有设备固定采用 UTC+8 日界线。只做本地防回拨；真正防改时需服务器时间。
    private const long SIGN_IN_DAY_OFFSET_TICKS = TimeSpan.TicksPerHour * 8;
    private const long SIGN_IN_CLOCK_TOLERANCE_TICKS = TimeSpan.TicksPerMinute * 5;

    public MPSignInStatus GetSignInStatus()
    {
        MPSignInConfigService.TryLoad(out MPSignInConfig config);
        return GetSignInStatus(config);
    }

    public MPSignInStatus GetSignInStatus(MPSignInConfig config)
    {
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        bool changed = MPSignInConfigService.MigrateLegacyProgress(progress, config);
        long now = DateTime.UtcNow.Ticks;
        long day = (now + SIGN_IN_DAY_OFFSET_TICKS) / TimeSpan.TicksPerDay;
        var status = new MPSignInStatus
        {
            day = day,
            claimedToday = progress.signInLastClaimDay == day,
            clockIsValid = now + SIGN_IN_CLOCK_TOLERANCE_TICKS >= progress.signInLatestObservedUtcTicks
                && day >= progress.signInLastClaimDay,
            hasConfiguredRewards = config != null && config.AvailableEntryCount > 0,
            claimedEntryIds = new HashSet<string>(progress.signInClaimedEntryIds, StringComparer.Ordinal)
        };
        if (status.hasConfiguredRewards)
        {
            // 展示、自动弹出和实际发奖共用此范围，未凑满七条的尾组不能提前领取。
            for (int i = 0; i < config.AvailableEntryCount; i++)
            {
                if (status.claimedEntryIds.Contains(config.Entries[i].id)) continue;
                status.dayIndex = i;
                status.entryId = config.Entries[i].id;
                break;
            }
        }
        // 只在进入/操作弹窗时观察时钟，最多每分钟持久化一次，不在 Update 写存档。
        if (now > progress.signInLatestObservedUtcTicks + TimeSpan.TicksPerMinute)
        {
            progress.signInLatestObservedUtcTicks = now;
            changed = true;
        }
        if (changed)
            ApplyRewardProgressSnapshot(progress);
        return status;
    }

    public bool TryClaimSignInReward(string entryId, long expectedDay, int multiplier,
        out MPRewardReceipt receipt)
    {
        receipt = null;
        if (string.IsNullOrEmpty(entryId) || (multiplier != 1 && multiplier != 2)
            || !MPSignInConfigService.TryLoad(out MPSignInConfig config))
            return false;
        MPSignInStatus status = GetSignInStatus(config);
        if (!status.CanClaim || status.day != expectedDay || status.entryId != entryId)
            return false;

        MPSignInRewardEntry reward = config.Entries[status.dayIndex];
        MPRewardProgressSnapshot state = CreateRewardProgressSnapshot();
        if (state.signInClaimedEntryIds.Contains(entryId))
            return false;
        var result = new MPRewardReceipt
        {
            sourceId = "sign_in_entry_" + entryId,
            sourceName = "Day " + (status.dayIndex + 1) + (multiplier == 2 ? " sign-in (2x)" : " sign-in"),
            transactionId = "sign_in:" + status.day,
            rewards = new List<MPRewardItem>
            {
                new MPRewardItem(reward.type, checked(reward.amount * multiplier), reward.icon)
            }
        };
        state.signInClaimedEntryIds.Add(entryId);
        state.signInLastClaimDay = status.day;
        state.signInClaimedDays = checked(state.signInClaimedDays + 1);
        state.signInLatestObservedUtcTicks = Math.Max(state.signInLatestObservedUtcTicks, DateTime.UtcNow.Ticks);
        if (!TryCommitReward(result, state, null, null, MPCloudSaveDirtyReason.Assets))
            return false;
        receipt = result;
        return true;
    }
}

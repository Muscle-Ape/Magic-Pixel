using System;
using System.Collections.Generic;

public sealed class MPShopFreeCoinStatus
{
    public long day;
    public int claimedCount;
    public int remainingCount;
    public bool clockIsValid;
    public bool CanClaim => clockIsValid && remainingCount > 0;
}

public partial class MPUser
{
    public const int SHOP_FREE_COIN_DAILY_LIMIT = 3;
    public const int SHOP_FREE_COIN_AMOUNT = 300;

    /// <summary>获取当日广告金币次数。日界线与签到一致，统一使用 UTC+8。</summary>
    public MPShopFreeCoinStatus GetShopFreeCoinStatus()
    {
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        long now = DateTime.UtcNow.Ticks;
        long day = (now + SIGN_IN_DAY_OFFSET_TICKS) / TimeSpan.TicksPerDay;
        int claimed = progress.shopFreeCoinClaimDay == day
            ? Math.Min(SHOP_FREE_COIN_DAILY_LIMIT, progress.shopFreeCoinClaimCount)
            : 0;
        var status = new MPShopFreeCoinStatus
        {
            day = day,
            claimedCount = claimed,
            remainingCount = Math.Max(0, SHOP_FREE_COIN_DAILY_LIMIT - claimed),
            clockIsValid = now + SIGN_IN_CLOCK_TOLERANCE_TICKS >=
                progress.shopFreeCoinLatestObservedUtcTicks
                && day >= progress.shopFreeCoinClaimDay
        };

        // 只在主动打开或操作商店时记录时钟，不在 Update 中高频写存档。
        if (now > progress.shopFreeCoinLatestObservedUtcTicks + TimeSpan.TicksPerMinute)
        {
            progress.shopFreeCoinLatestObservedUtcTicks = now;
            ApplyRewardProgressSnapshot(progress);
        }
        return status;
    }

    public bool TryClaimShopFreeCoin(long expectedDay, out MPRewardReceipt receipt)
    {
        receipt = null;
        if (!MPReleaseFeatures.Ads)
            return false;

        MPShopFreeCoinStatus status = GetShopFreeCoinStatus();
        if (!status.CanClaim || status.day != expectedDay)
            return false;

        MPRewardProgressSnapshot state = CreateRewardProgressSnapshot();
        if (state.shopFreeCoinClaimDay != status.day)
        {
            state.shopFreeCoinClaimDay = status.day;
            state.shopFreeCoinClaimCount = 0;
        }
        if (state.shopFreeCoinClaimCount >= SHOP_FREE_COIN_DAILY_LIMIT)
            return false;

        int claimNumber = state.shopFreeCoinClaimCount + 1;
        var result = new MPRewardReceipt
        {
            sourceId = "shop_free_coin",
            sourceName = "Free coins",
            transactionId = $"shop_free_coin:{status.day}:{claimNumber}",
            rewards = new List<MPRewardItem>
            {
                new MPRewardItem("coin", SHOP_FREE_COIN_AMOUNT)
            }
        };
        state.shopFreeCoinClaimCount = claimNumber;
        state.shopFreeCoinLatestObservedUtcTicks = Math.Max(
            state.shopFreeCoinLatestObservedUtcTicks, DateTime.UtcNow.Ticks);
        if (!TryCommitReward(result, state, null, null, MPCloudSaveDirtyReason.Assets))
            return false;
        receipt = result;
        return true;
    }
}

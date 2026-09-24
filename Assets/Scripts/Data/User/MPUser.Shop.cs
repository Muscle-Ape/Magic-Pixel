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

public enum MPShopPetPurchaseResult
{
    Succeeded,
    AlreadyOwned,
    InsufficientCoins,
    InvalidConfig,
    Failed
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

    /// <summary>
    /// 使用金币购买配置为 coin 的宠物。金币扣除、宠物永久拥有记录和幂等事务
    /// 通过同一个 ES3File 一次提交，避免出现扣款成功但宠物未到账的中间状态。
    /// </summary>
    public MPShopPetPurchaseResult TryPurchaseShopPet(string petId)
    {
        MPPetConfig pet = MPDataManager.Instance.m_petsModel?.petConfigs?.Find(
            item => item != null && string.Equals(item.ID, petId, StringComparison.Ordinal));
        if (pet == null
            || !pet.TryGetUnlockRequirement(out string unlockType, out int price)
            || !string.Equals(unlockType, "coin", StringComparison.OrdinalIgnoreCase)
            || price <= 0)
        {
            return MPShopPetPurchaseResult.InvalidConfig;
        }

        if (PetIsUnlock(pet.ID))
            return MPShopPetPurchaseResult.AlreadyOwned;
        if (m_coins < price)
            return MPShopPetPurchaseResult.InsufficientCoins;

        try
        {
            int remainingCoins = checked(m_coins - price);
            MPRewardProgressSnapshot state = CreateRewardProgressSnapshot();
            string transactionId = "shop_pet_coin_v1:" + pet.ID;
            if (state.claimedPetIds.Contains(pet.ID)
                || state.transactionIds.Contains(transactionId))
            {
                return MPShopPetPurchaseResult.AlreadyOwned;
            }

            var receipt = new MPRewardReceipt
            {
                sourceId = "shop_pet:" + pet.ID,
                sourceName = pet.Name,
                transactionId = transactionId,
                rewards = new List<MPRewardItem>
                {
                    new MPRewardItem(pet.ID, 1, pet.Icon)
                }
            };
            bool committed = TryCommitReward(receipt, state,
                file => file.Save(m_key_coins, remainingCoins),
                () => m_coins = remainingCoins,
                MPCloudSaveDirtyReason.Pets);
            return committed
                ? MPShopPetPurchaseResult.Succeeded
                : MPShopPetPurchaseResult.Failed;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[MPUser] 商店宠物购买失败：{exception.Message}");
            return MPShopPetPurchaseResult.Failed;
        }
    }
}

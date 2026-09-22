using System;
using System.Collections.Generic;

public partial class MPUser
{
    /// <summary>VIP 提供未解锁关卡的进入权；具体关卡在真正进入时才会写入永久解锁列表。</summary>
    public bool HasVipAccess()
    {
        return MPReleaseFeatures.Vip
            && MPReleaseFeatures.InAppPurchases
            && MPIapManager.Instance.HasActiveSubscription(MPIapManager.VIP_PRODUCT_ID);
    }

    public bool CanEnterMainLevel(string levelId)
    {
        return !string.IsNullOrWhiteSpace(levelId)
            && (MainLevelIsUnlock(levelId) || HasVipAccess());
    }

    public bool CanEnterLargeImageLevel(string levelId)
    {
        return !string.IsNullOrWhiteSpace(levelId)
            && (LargeImageLevelIsUnlock(levelId) || HasVipAccess());
    }

    /// <summary>
    /// 校验主关卡进入权。VIP 首次进入锁定关卡时会立即永久解锁，
    /// 因此后续 VIP 到期也不会重新锁上该关卡。
    /// </summary>
    public bool TryUnlockMainLevelWithVip(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return false;
        if (MainLevelIsUnlock(levelId))
            return true;
        if (!HasVipAccess())
            return false;

        MainLevelUnlock(levelId);
        return MainLevelIsUnlock(levelId);
    }

    /// <summary>VIP 首次进入锁定大图关卡时永久解锁该关卡。</summary>
    public bool TryUnlockLargeImageLevelWithVip(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return false;
        if (LargeImageLevelIsUnlock(levelId))
            return true;
        if (!HasVipAccess())
            return false;

        LargeImageLevelUnlock(levelId);
        return LargeImageLevelIsUnlock(levelId);
    }

    public List<MPPetConfig> GetVipPetConfigs()
    {
        var result = new List<MPPetConfig>();
        List<MPPetConfig> configs = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (configs == null)
            return result;
        foreach (MPPetConfig pet in configs)
        {
            if (pet == null || string.IsNullOrWhiteSpace(pet.ID)
                || !pet.TryGetUnlockRequirement(out string type, out _)
                || !string.Equals(type, "vip", StringComparison.OrdinalIgnoreCase))
                continue;
            result.Add(pet);
        }
        return result;
    }

    /// <summary>
    /// 兼容商店恢复购买：只要商店确认 VIP 有效，就永久补齐所有 VIP 宠物。
    /// 每只宠物使用稳定事务 ID，同一账号不会重复发放。
    /// </summary>
    public void GrantVipPetsPermanently()
    {
        foreach (MPPetConfig pet in GetVipPetConfigs())
        {
            if (PetIsUnlock(pet.ID))
                continue;
            TryCommitReward(new MPRewardReceipt
            {
                sourceId = "vip_pet:" + pet.ID,
                sourceName = "VIP pet",
                transactionId = "vip_pet_purchase_v1:" + pet.ID,
                rewards = new List<MPRewardItem> { new MPRewardItem(pet.ID, 1, pet.Icon) }
            }, null, null, null, MPCloudSaveDirtyReason.Pets);
        }
    }
}

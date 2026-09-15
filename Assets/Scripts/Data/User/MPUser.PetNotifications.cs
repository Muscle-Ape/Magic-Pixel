using System.Collections.Generic;

public partial class MPUser
{
    public const string LEVEL_50_PET_ID = "pet_lop_rabbit";

    /// <summary>首次达到 50 级时入账；大图宠物奖励完全由关卡 box_award 配置决定。</summary>
    public void GrantLevel50Pet()
    {
        try
        {
            GrantMilestonePet(LEVEL_50_PET_ID, "Reach player level 50");
        }
        catch (System.Exception exception)
        {
            // 奖励存档异常不能中断已经成功的通关或经验入账。
            UnityEngine.Debug.LogWarning($"[MPUser] 宠物里程碑奖励未完成：{exception.Message}");
        }
    }

    private void GrantMilestonePet(string petId, string source)
    {
        // 已经拥有的宠物不重复发放；已有通知标记也不会被清除。
        if (PetIsUnlock(petId)) return;
        TryCommitReward(new MPRewardReceipt
        {
            transactionId = "pet_milestone_v1:" + petId,
            sourceId = "pet_milestone:" + petId,
            sourceName = source,
            rewards = new List<MPRewardItem> { new MPRewardItem(petId, 1) }
        }, null, null, null, MPCloudSaveDirtyReason.Pets);
    }

    public MPPetConfig GetPendingMilestonePet(string petId)
    {
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        if (!progress.claimedPetIds.Contains(petId) || progress.notifiedPetIds.Contains(petId)) return null;
        return MPDataManager.Instance.m_petsModel?.petConfigs?.Find(pet => pet != null && pet.ID == petId);
    }

    /// <summary>只查询已领取但尚未提示的宠物；查询不会解锁、领取或写入存档。</summary>
    public MPPetConfig GetPendingPetUnlockNotification()
    {
        List<MPPetConfig> configs = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (configs == null) return null;
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        foreach (MPPetConfig pet in configs)
        {
            if (pet != null && !string.IsNullOrEmpty(pet.ID) && progress.claimedPetIds.Contains(pet.ID)
                && !pet.DefaultUnlocked && !progress.notifiedPetIds.Contains(pet.ID))
                return pet;
        }
        return null;
    }

    public void MarkPetUnlockNotificationSeen(string petId)
    {
        if (string.IsNullOrEmpty(petId) || !PetIsUnlock(petId)) return;
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        if (progress.notifiedPetIds.Contains(petId)) return;
        progress.notifiedPetIds.Add(petId);
        ApplyRewardProgressSnapshot(progress);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }
}

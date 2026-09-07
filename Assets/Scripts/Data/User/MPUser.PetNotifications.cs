using System.Collections.Generic;

public partial class MPUser
{
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

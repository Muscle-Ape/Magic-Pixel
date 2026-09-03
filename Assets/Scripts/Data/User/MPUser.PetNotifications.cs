using System.Collections.Generic;

public partial class MPUser
{
    /// <summary>记录已达到解锁条件的宠物；提示已读单独存储，默认宠物不打断新手流程。</summary>
    public MPPetConfig GetPendingPetUnlockNotification()
    {
        List<MPPetConfig> configs = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (configs == null) return null;
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        bool changed = false;
        MPPetConfig pending = null;
        foreach (MPPetConfig pet in configs)
        {
            if (pet == null || string.IsNullOrEmpty(pet.ID) || !PetUnlockConditionIsMet(pet))
                continue;
            if (!progress.unlockedPetIds.Contains(pet.ID))
            {
                progress.unlockedPetIds.Add(pet.ID);
                changed = true;
            }
            if (pet.DefaultUnlocked && !progress.notifiedPetIds.Contains(pet.ID))
            {
                progress.notifiedPetIds.Add(pet.ID);
                changed = true;
            }
            if (pending == null && !progress.notifiedPetIds.Contains(pet.ID))
                pending = pet;
        }
        if (changed)
        {
            ApplyRewardProgressSnapshot(progress);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
        }
        return pending;
    }

    public void MarkPetUnlockNotificationSeen(string petId)
    {
        if (string.IsNullOrEmpty(petId) || !PetIsUnlock(petId)) return;
        MPRewardProgressSnapshot progress = CreateRewardProgressSnapshot();
        if (progress.notifiedPetIds.Contains(petId)) return;
        if (!progress.unlockedPetIds.Contains(petId)) progress.unlockedPetIds.Add(petId);
        progress.notifiedPetIds.Add(petId);
        ApplyRewardProgressSnapshot(progress);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Pets);
    }
}

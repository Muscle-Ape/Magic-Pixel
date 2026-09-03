using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用户快照冲突合并器。
/// 当前阶段采用偏保守的规则：进度类数据做并集，资产和宠物按更新时间选择整块数据。
/// </summary>
public class MPCloudSaveConflictResolver
{
    /// <summary>
    /// 合并本地快照和云端快照。
    /// </summary>
    /// <param name="local">本地快照。</param>
    /// <param name="cloud">云端快照。</param>
    /// <returns>合并后的快照。</returns>
    public MPUserCloudSnapshot Resolve(MPUserCloudSnapshot local, MPUserCloudSnapshot cloud)
    {
        if (local == null)
        {
            return cloud;
        }

        if (cloud == null)
        {
            return local;
        }

        bool localIsNewer = local.updatedAtUtcTicks >= cloud.updatedAtUtcTicks;
        MPUserCloudSnapshot result = localIsNewer ? local : cloud;

        result.assets = localIsNewer ? SafeAssets(local.assets) : SafeAssets(cloud.assets);
        result.settings = localIsNewer ? SafeSettings(local.settings) : SafeSettings(cloud.settings);
        result.mainLevel = MergeMainLevel(local.mainLevel, cloud.mainLevel);
        result.largeImageLevel = MergeLargeImageLevel(local.largeImageLevel, cloud.largeImageLevel);
        result.pets = localIsNewer ? SafePets(local.pets) : SafePets(cloud.pets);
        result.rewardProgress = MergeRewardProgress(local.rewardProgress, cloud.rewardProgress);
        result.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        result.clientVersion = Application.version;
        return result;
    }

    private static MPRewardProgressSnapshot MergeRewardProgress(MPRewardProgressSnapshot local, MPRewardProgressSnapshot cloud)
    {
        local = local ?? new MPRewardProgressSnapshot();
        cloud = cloud ?? new MPRewardProgressSnapshot();
        return new MPRewardProgressSnapshot
        {
            transactionIds = UnionList(local.transactionIds, cloud.transactionIds),
            unlockedPetIds = UnionList(local.unlockedPetIds, cloud.unlockedPetIds),
            notifiedPetIds = UnionList(local.notifiedPetIds, cloud.notifiedPetIds),
            signInLastClaimDay = Math.Max(local.signInLastClaimDay, cloud.signInLastClaimDay),
            signInClaimedDays = Math.Max(local.signInClaimedDays, cloud.signInClaimedDays),
            signInLatestObservedUtcTicks = Math.Max(local.signInLatestObservedUtcTicks, cloud.signInLatestObservedUtcTicks),
            signInClaimedEntryIds = UnionList(local.signInClaimedEntryIds, cloud.signInClaimedEntryIds),
            signInLegacyClaimedDays = Math.Max(LegacySignInClaimedDays(local), LegacySignInClaimedDays(cloud)),
            signInLegacyMappedDays = Math.Max(local.signInLegacyMappedDays, cloud.signInLegacyMappedDays)
        };
    }

    private static int LegacySignInClaimedDays(MPRewardProgressSnapshot value)
    {
        return Math.Max(value.signInLegacyClaimedDays,
            value.signInClaimedEntryIds == null ? value.signInClaimedDays : 0);
    }

    /// <summary>
    /// 合并本地自定义关卡快照和云端自定义关卡快照。
    /// </summary>
    /// <param name="local">本地自定义关卡快照。</param>
    /// <param name="cloud">云端自定义关卡快照。</param>
    /// <returns>合并后的自定义关卡快照。</returns>
    public MPCustomLevelCloudSnapshot ResolveCustomLevel(MPCustomLevelCloudSnapshot local, MPCustomLevelCloudSnapshot cloud)
    {
        if (local == null)
        {
            return cloud;
        }

        if (cloud == null)
        {
            return local;
        }

        bool localIsNewer = local.updatedAtUtcTicks >= cloud.updatedAtUtcTicks;
        MPCustomLevelCloudSnapshot result = localIsNewer ? local : cloud;
        result.customLevel = MergeCustomLevel(local.customLevel, cloud.customLevel, localIsNewer);
        result.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        result.clientVersion = Application.version;
        return result;
    }

    /// <summary>
    /// 合并主线关卡进度。
    /// </summary>
    private static MPUserMainLevelSnapshot MergeMainLevel(MPUserMainLevelSnapshot local, MPUserMainLevelSnapshot cloud)
    {
        local = SafeMainLevel(local);
        cloud = SafeMainLevel(cloud);
        return new MPUserMainLevelSnapshot
        {
            passIndex = Mathf.Max(local.passIndex, cloud.passIndex),
            unlockList = UnionList(local.unlockList, cloud.unlockList),
            passList = UnionList(local.passList, cloud.passList),
            stars = MergeMaxDictionary(local.stars, cloud.stars),
            boxAwardClaimedList = UnionList(
                local.boxAwardClaimedList,
                cloud.boxAwardClaimedList)
        };
    }

    /// <summary>
    /// 合并大图模式关卡进度。
    /// </summary>
    private static MPUserLargeImageLevelSnapshot MergeLargeImageLevel(MPUserLargeImageLevelSnapshot local, MPUserLargeImageLevelSnapshot cloud)
    {
        local = SafeLargeImageLevel(local);
        cloud = SafeLargeImageLevel(cloud);
        return new MPUserLargeImageLevelSnapshot
        {
            passIndex = Mathf.Max(local.passIndex, cloud.passIndex),
            unlockList = UnionList(local.unlockList, cloud.unlockList),
            passList = UnionList(local.passList, cloud.passList),
            stars = MergeMaxDictionary(local.stars, cloud.stars),
            coinAwardClaimedList = UnionList(local.coinAwardClaimedList, cloud.coinAwardClaimedList)
        };
    }

    /// <summary>
    /// 合并自定义关卡。
    /// 同 ID 冲突时暂时按整体快照更新时间选择一侧。
    /// </summary>
    private static MPUserCustomLevelSnapshot MergeCustomLevel(MPUserCustomLevelSnapshot local, MPUserCustomLevelSnapshot cloud, bool localIsNewer)
    {
        local = SafeCustomLevel(local);
        cloud = SafeCustomLevel(cloud);

        Dictionary<string, MPCustomLevelInfo> levels = new Dictionary<string, MPCustomLevelInfo>();
        AddCustomLevels(levels, localIsNewer ? cloud.levels : local.levels);
        AddCustomLevels(levels, localIsNewer ? local.levels : cloud.levels);

        return new MPUserCustomLevelSnapshot
        {
            levels = new List<MPCustomLevelInfo>(levels.Values),
            passList = UnionList(local.passList, cloud.passList)
        };
    }

    /// <summary>
    /// 添加自定义关卡，同 ID 后加入的覆盖先加入的。
    /// </summary>
    private static void AddCustomLevels(Dictionary<string, MPCustomLevelInfo> target, List<MPCustomLevelInfo> levels)
    {
        if (levels == null)
        {
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            MPCustomLevelInfo level = levels[i];
            if (level == null || string.IsNullOrEmpty(level.ID))
            {
                continue;
            }

            target[level.ID] = level;
        }
    }

    /// <summary>
    /// 合并字符串列表并去重。
    /// </summary>
    private static List<string> UnionList(List<string> a, List<string> b)
    {
        List<string> result = new List<string>();
        AddUnique(result, a);
        AddUnique(result, b);
        return result;
    }

    /// <summary>
    /// 向目标列表添加非空且不重复的字符串。
    /// </summary>
    private static void AddUnique(List<string> target, List<string> values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (!string.IsNullOrEmpty(value) && !target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    /// <summary>
    /// 合并字典，同 Key 取最大值。
    /// </summary>
    private static Dictionary<string, int> MergeMaxDictionary(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        AddMax(result, a);
        AddMax(result, b);
        return result;
    }

    /// <summary>
    /// 把来源字典合并到目标字典，同 Key 保留最大值。
    /// </summary>
    private static void AddMax(Dictionary<string, int> target, Dictionary<string, int> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> pair in source)
        {
            if (string.IsNullOrEmpty(pair.Key))
            {
                continue;
            }

            if (!target.TryGetValue(pair.Key, out int current) || pair.Value > current)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }

    private static MPUserAssetsSnapshot SafeAssets(MPUserAssetsSnapshot value)
    {
        return value ?? new MPUserAssetsSnapshot();
    }

    private static MPUserSettingsSnapshot SafeSettings(MPUserSettingsSnapshot value)
    {
        return value ?? new MPUserSettingsSnapshot();
    }

    private static MPUserMainLevelSnapshot SafeMainLevel(MPUserMainLevelSnapshot value)
    {
        return value ?? new MPUserMainLevelSnapshot();
    }

    private static MPUserLargeImageLevelSnapshot SafeLargeImageLevel(MPUserLargeImageLevelSnapshot value)
    {
        return value ?? new MPUserLargeImageLevelSnapshot();
    }

    private static MPUserCustomLevelSnapshot SafeCustomLevel(MPUserCustomLevelSnapshot value)
    {
        return value ?? new MPUserCustomLevelSnapshot();
    }

    private static MPUserPetsSnapshot SafePets(MPUserPetsSnapshot value)
    {
        return value ?? new MPUserPetsSnapshot();
    }
}

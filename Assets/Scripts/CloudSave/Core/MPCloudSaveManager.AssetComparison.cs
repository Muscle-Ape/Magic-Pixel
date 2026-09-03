using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public partial class MPCloudSaveManager
{
    private const string COMPARISON_BACKUP_KEY_PREFIX = "MPCloudSave.AssetComparisonBackup.v1.";
    private bool m_assetComparisonPending;
    private bool m_assetComparisonNeedsResolution;
    private CancellationTokenSource m_assetComparisonCancellation;
    private string m_assetComparisonPlayerId;

    private static async Task RefreshPlayerProfileSafeAsync()
    {
        try { await MPPlayerProfileService.RefreshAsync(); }
        catch (Exception exception) { Debug.LogWarning($"[MPProfile] 资料同步失败，保留缓存：{exception.Message}"); }
    }

    private async Task<bool?> ResolveHighRiskInitializationAsync(MPCloudSaveLoadResult<MPUserCloudSnapshot> userResult,
        MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> customResult, bool accountSwitched,
        MPLocalLoginProfile profile, CancellationToken token)
    {
        // 不把其他账号的资产带入新账号；只有同一账号双端都发生修改才需要人工决定。
        if (accountSwitched || !HasAnyDirtyData() || !userResult.exists || userResult.value == null ||
            !ValidateUserSnapshot(userResult.value, m_playerId)) return null;
        MPUserCloudSnapshot localUser = MPUser.instance.CreateCloudSnapshot();
        MPCustomLevelCloudSnapshot localCustom = MPUser.instance.CreateCustomLevelCloudSnapshot();
        MPCustomLevelCloudSnapshot cloudCustom = customResult.value ?? MPCustomLevelCloudSnapshot.CreateDefault(
            m_playerId, GetUnityEnvironmentName(), ResolveLastLoginProvider(profile), ResolveHasBoundIdentity(profile));
        if (localUser.schemaVersion != userResult.value.schemaVersion || localCustom.schemaVersion != cloudCustom.schemaVersion)
            return null;
        bool userConflict = m_meta.hasUserSnapshotDirtyData && userResult.value.updatedAtUtcTicks > m_meta.lastSyncedAtUtcTicks &&
            HasHighRiskUserDifference(localUser, userResult.value);
        bool customConflict = m_meta.hasCustomLevelDirtyData && cloudCustom.updatedAtUtcTicks > m_meta.lastSyncedAtUtcTicks &&
            HasCustomContentConflict(localCustom, cloudCustom);
        if (!userConflict && !customConflict) return null;
        if (!string.IsNullOrEmpty(cloudCustom.playerId) && cloudCustom.playerId != m_playerId) return false;
        m_assetComparisonPending = true;
        m_assetComparisonNeedsResolution = true;

        localUser.updatedAtUtcTicks = m_meta.lastDirtyAtUtcTicks;
        localCustom.updatedAtUtcTicks = m_meta.lastDirtyAtUtcTicks;
        ApplyLoginMetadata(localUser, profile);
        ApplyLoginMetadata(localCustom, profile);
        MPAssetComparisonPopUIMsgData data = new MPAssetComparisonPopUIMsgData
        {
            localUser = CopySnapshot(localUser), cloudUser = CopySnapshot(userResult.value),
            localCustom = CopySnapshot(localCustom), cloudCustom = CopySnapshot(cloudCustom)
        };
        string originalPlayerId = m_playerId;
        bool alreadyCommitted = false;
        MPUserCloudSnapshot pendingUser = null;
        MPCustomLevelCloudSnapshot pendingCustom = null;
        Dictionary<string, string> committedLocks = null;
        bool? committedUseLocal = null;
        bool requireNewChoice = false;
        Task<bool> activeCommit = null;
        data.onChoose = () => requireNewChoice = false;
        Func<bool, CancellationToken, Task<bool>> commitAsync = async (useLocal, confirmationToken) =>
        {
            if (alreadyCommitted) return true;
            if (requireNewChoice) return false;
            confirmationToken.ThrowIfCancellationRequested();
            if (MPLoginManager.Instance.PlayerId != originalPlayerId) return false;
            bool needsRemoteWrite = committedLocks == null || committedUseLocal != useLocal;
            MPUserCloudSnapshot candidateUser = pendingUser;
            MPCustomLevelCloudSnapshot candidateCustom = pendingCustom;
            if (needsRemoteWrite)
            {
                candidateUser = CopySnapshot(useLocal ? data.localUser : data.cloudUser);
                candidateCustom = CopySnapshot(useLocal ? data.localCustom : data.cloudCustom);
                ApplyLoginMetadata(candidateUser, profile);
                ApplyLoginMetadata(candidateCustom, profile);
                candidateUser.updatedAtUtcTicks = candidateCustom.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            // 单条备份保留两侧原件，网络失败、写锁冲突和本地写失败都不会丢弃它们。
            ES3.Save(COMPARISON_BACKUP_KEY_PREFIX + originalPlayerId, JsonConvert.SerializeObject(new
            {
                data.localUser, data.cloudUser, data.localCustom, data.cloudCustom
            }));
            try
            {
                if (needsRemoteWrite)
                {
                    string userWriteLock = userResult.writeLock;
                    string customWriteLock = customResult.writeLock;
                    if (committedLocks != null)
                    {
                        committedLocks.TryGetValue(MPCloudSaveConstants.USER_SNAPSHOT_KEY, out userWriteLock);
                        committedLocks.TryGetValue(MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY, out customWriteLock);
                    }
                    committedLocks = await m_cloudSaveApi.SaveSnapshotPairAsync(candidateUser, userWriteLock,
                        candidateCustom, customWriteLock, confirmationToken);
                    pendingUser = candidateUser;
                    pendingCustom = candidateCustom;
                    committedUseLocal = useLocal;
                }
                // 远端成功后不能因弹窗退出取消本地提交。
                if (MPLoginManager.Instance.PlayerId != originalPlayerId) return false;
                MPUser.instance.ApplyChosenCloudSnapshots(pendingUser, pendingCustom);
                committedLocks.TryGetValue(MPCloudSaveConstants.USER_SNAPSHOT_KEY, out string userLock);
                committedLocks.TryGetValue(MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY, out string customLock);
                MarkUserSnapshotClean(userLock);
                MarkCustomLevelSnapshotClean(customLock);
                await m_metaRepository.SaveAsync(m_meta, CancellationToken.None);
                alreadyCommitted = true;
                m_assetComparisonPending = false;
                m_assetComparisonNeedsResolution = false;
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Unity.Services.CloudSave.CloudSaveConflictException)
            {
                if (MPLoginManager.Instance.PlayerId != originalPlayerId) return false;
                MPCloudSaveLoadResult<MPUserCloudSnapshot> refreshedUser = await LoadUserSnapshotSafeAsync(confirmationToken);
                MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> refreshedCustom = await LoadCustomLevelSnapshotSafeAsync(confirmationToken);
                if (refreshedUser?.value != null && refreshedCustom?.value != null &&
                    refreshedUser.value.schemaVersion == data.localUser.schemaVersion &&
                    refreshedCustom.value.schemaVersion == data.localCustom.schemaVersion &&
                    ValidateUserSnapshot(refreshedUser.value, originalPlayerId) &&
                    ValidateCustomLevelSnapshot(refreshedCustom.value, originalPlayerId))
                {
                    // 不拿最新写锁直接覆盖用户尚未看过的数据，必须返回比较页重新选择。
                    data.cloudUser = CopySnapshot(refreshedUser.value);
                    data.cloudCustom = CopySnapshot(refreshedCustom.value);
                    userResult.writeLock = refreshedUser.writeLock;
                    customResult.writeLock = refreshedCustom.writeLock;
                    committedLocks = null;
                    committedUseLocal = null;
                    requireNewChoice = true;
                    data.onCloudRefreshed?.Invoke();
                }
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MPCloudSave] 存档选择提交失败，保留原始备份：{exception.Message}");
                return false;
            }
        };
        data.confirmAsync = (useLocal, confirmationToken) => activeCommit = commitAsync(useLocal, confirmationToken);
        CancellationTokenSource comparisonScope = CancellationTokenSource.CreateLinkedTokenSource(token);
        m_assetComparisonCancellation = comparisonScope;
        m_assetComparisonPlayerId = originalPlayerId;
        try
        {
            using (comparisonScope.Token.Register(() => data.completion.TrySetCanceled()))
                return await MPAssetComparisonPop.ShowAsync(data);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // 退出/切换账号主动撤回当前账号的比较，不让旧账号占住同步锁。
            return false;
        }
        finally
        {
            // await 回到 Unity 同步上下文后撤回两层弹窗，不在token线程操作UI。
            data.cancelPresentation?.Invoke();
            // 同账号关闭时等已发出的写入收尾；切号时旧响应有PlayerId保护，不能继续占用新账号同步锁。
            if (activeCommit != null && MPLoginManager.Instance.PlayerId == originalPlayerId)
            {
                try { await activeCommit; }
                catch (Exception) { /* 界面已撤回，原始备份仍保留。 */ }
            }
            else if (activeCommit != null)
                _ = ObserveAbandonedComparisonCommitAsync(activeCommit);
            m_assetComparisonPending = false;
            if (ReferenceEquals(m_assetComparisonCancellation, comparisonScope))
            {
                m_assetComparisonCancellation = null;
                m_assetComparisonPlayerId = null;
            }
            comparisonScope.Dispose();
        }
    }

    private static async Task ObserveAbandonedComparisonCommitAsync(Task<bool> task)
    {
        try { await task; }
        catch (Exception) { /* 旧账号请求只等待收尾，不再触碰新账号数据。 */ }
    }

    private static T CopySnapshot<T>(T source) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source));

    private static bool HasHighRiskUserDifference(MPUserCloudSnapshot local, MPUserCloudSnapshot cloud)
    {
        MPUserAssetsSnapshot a = local.assets ?? new MPUserAssetsSnapshot();
        MPUserAssetsSnapshot b = cloud.assets ?? new MPUserAssetsSnapshot();
        return a.coins != b.coins || a.diamond != b.diamond || a.hintProps != b.hintProps || a.loveRecoverProps != b.loveRecoverProps ||
            HasDivergentProgress(local.mainLevel?.passList, cloud.mainLevel?.passList) ||
            HasDivergentProgress(local.largeImageLevel?.passList, cloud.largeImageLevel?.passList);
    }

    private static bool HasDivergentProgress(List<string> local, List<string> cloud)
    {
        HashSet<string> a = new HashSet<string>(local ?? new List<string>());
        HashSet<string> b = new HashSet<string>(cloud ?? new List<string>());
        return !a.IsSubsetOf(b) && !b.IsSubsetOf(a);
    }

    private static bool HasCustomContentConflict(MPCustomLevelCloudSnapshot local, MPCustomLevelCloudSnapshot cloud)
    {
        if (local.customLevel?.levels == null || cloud.customLevel?.levels == null) return false;
        Dictionary<string, MPCustomLevelInfo> localLevels = new Dictionary<string, MPCustomLevelInfo>();
        foreach (MPCustomLevelInfo level in local.customLevel.levels)
            if (level != null && !string.IsNullOrEmpty(level.ID)) localLevels[level.ID] = level;
        foreach (MPCustomLevelInfo level in cloud.customLevel.levels)
            if (level != null && !string.IsNullOrEmpty(level.ID) && localLevels.TryGetValue(level.ID, out MPCustomLevelInfo own) &&
                JsonConvert.SerializeObject(own) != JsonConvert.SerializeObject(level)) return true;
        return false;
    }
}

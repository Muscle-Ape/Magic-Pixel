using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

/// <summary>
/// 云存储模块对外门面。
/// 负责登录后拉取云端快照、本地变更延迟上传、写锁冲突处理和生命周期 flush。
/// </summary>
public partial class MPCloudSaveManager
{
    /// <summary>
    /// 本地数据变化后延迟上传的毫秒数，避免连续多次 ES3.Save 触发多次网络请求。
    /// </summary>
    private const int DIRTY_FLUSH_DELAY_MS = 8000;

    /// <summary>
    /// 单例实例。
    /// </summary>
    private static MPCloudSaveManager m_instance;

    /// <summary>
    /// Cloud Save SDK 访问层。
    /// </summary>
    private readonly IMPCloudSaveApi m_cloudSaveApi;

    /// <summary>
    /// 云同步本地元数据仓库。
    /// </summary>
    private readonly IMPCloudSaveLocalMetaRepository m_metaRepository;

    /// <summary>
    /// 快照冲突合并器。
    /// </summary>
    private readonly MPCloudSaveConflictResolver m_conflictResolver;

    /// <summary>
    /// 同步锁，避免启动拉取、dirty 上传和退出 flush 并发写同一批 Key。
    /// </summary>
    private readonly SemaphoreSlim m_syncLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// 延迟上传任务取消源。
    /// </summary>
    private CancellationTokenSource m_debounceCancellation;

    /// <summary>
    /// 当前云同步绑定的 PlayerId。
    /// </summary>
    private string m_playerId;

    /// <summary>
    /// 当前玩家的本地云同步元数据。
    /// </summary>
    private MPCloudSaveLocalMeta m_meta;

    /// <summary>
    /// 生命周期钩子。
    /// </summary>
    private MPCloudSaveLifecycleHook m_lifecycleHook;

    /// <summary>
    /// 当前玩家是否已经完成启动阶段云同步。
    /// </summary>
    private bool m_initialized;

    /// <summary>
    /// MPUser 本地数据是否已经初始化完成。
    /// </summary>
    private bool m_isUserDataReady;
    private bool m_accountSwitchInProgress;
    public string RequiredPlayerIdForAccountSwitch { get; private set; }

    private MPCloudSaveManager()
    {
        m_cloudSaveApi = new MPUnityCloudSaveApi();
        m_metaRepository = new MPEasySaveCloudSaveLocalMetaRepository();
        m_conflictResolver = new MPCloudSaveConflictResolver();

        MPLoginManager.Instance.LoginSucceeded -= OnLoginSucceeded;
        MPLoginManager.Instance.LoginSucceeded += OnLoginSucceeded;
        MPLoginManager.Instance.LoggedOut -= OnLoggedOut;
        MPLoginManager.Instance.LoggedOut += OnLoggedOut;
    }

    /// <summary>
    /// 云存储模块单例。
    /// </summary>
    public static MPCloudSaveManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPCloudSaveManager();
            }

            return m_instance;
        }
    }

    /// <summary>
    /// 当前是否已完成云同步初始化。
    /// </summary>
    public bool IsInitialized => m_initialized;

    /// <summary>
    /// 当前云同步绑定的 PlayerId。
    /// </summary>
    public string CurrentPlayerId => m_playerId;

    /// <summary>
    /// 账号切换前保护旧存档，并等正在进行的云请求结束。持有期间禁止其它同步跨账号执行。
    /// 启动时旧账号尚未恢复且有 dirty 存档时拒绝切换，不能用新账号覆盖旧的未同步内容。
    /// </summary>
    public async Task<IDisposable> BeginAccountSwitchAsync(CancellationToken cancellationToken = default, string reauthenticatePlayerId = null)
    {
        if (m_isUserDataReady && m_initialized && MPLoginManager.Instance.IsLoggedIn &&
            m_playerId == MPLoginManager.Instance.PlayerId && !await FlushAsync(cancellationToken))
            return null;

        await m_syncLock.WaitAsync(cancellationToken);
        bool leased = false;
        try
        {
            if (m_assetComparisonPending) return null;
            string requiredPlayerId = null;
            string owner = m_metaRepository.LoadActivePlayerId();
            if (!string.IsNullOrEmpty(owner))
            {
                MPCloudSaveLocalMeta meta = await m_metaRepository.LoadAsync(owner, cancellationToken);
                if (m_assetComparisonNeedsResolution || meta.hasDirtyData || meta.hasUserSnapshotDirtyData || meta.hasCustomLevelDirtyData)
                {
                    if (owner != reauthenticatePlayerId) return null;
                    // 可以重新授权原账号，但不能因此把别的账号数据覆盖到尚未同步的本地存档。
                    requiredPlayerId = owner;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            CancelDebouncedFlush();
            m_accountSwitchInProgress = true;
            RequiredPlayerIdForAccountSwitch = requiredPlayerId;
            leased = true;
            return new AccountSwitchGuard(this);
        }
        finally
        {
            if (!leased) m_syncLock.Release();
        }
    }

    private sealed class AccountSwitchGuard : IDisposable
    {
        private MPCloudSaveManager m_owner;
        public AccountSwitchGuard(MPCloudSaveManager owner) { m_owner = owner; }
        public void Dispose()
        {
            if (m_owner == null) return;
            m_owner.m_accountSwitchInProgress = false;
            m_owner.RequiredPlayerIdForAccountSwitch = null;
            if (m_owner.m_playerId != MPLoginManager.Instance.PlayerId) m_owner.m_initialized = false;
            m_owner.m_syncLock.Release();
            m_owner = null;
        }
    }

    /// <summary>
    /// 登录且 MPUser 本地数据加载完成后调用。
    /// 会分别处理用户主快照和自定义关卡独立快照。
    /// </summary>
    public async Task<bool> InitializeAfterUserLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (m_accountSwitchInProgress) return false;
        m_isUserDataReady = true;
        EnsureLifecycleHook();

        if (!MPLoginManager.Instance.IsLoggedIn || string.IsNullOrEmpty(MPLoginManager.Instance.PlayerId))
        {
            Debug.LogWarning("[MPCloudSave] Skip initialize because player is not logged in.");
            return false;
        }

        string playerId = MPLoginManager.Instance.PlayerId;
        await m_syncLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (MPLoginManager.Instance.PlayerId != playerId)
                return false;

            m_playerId = playerId;
            m_meta = await m_metaRepository.LoadAsync(playerId, cancellationToken);
            m_meta.playerId = playerId;
            NormalizeMetaDirtyFlags();

            string previousPlayerId = m_metaRepository.LoadActivePlayerId();
            bool accountSwitched = !string.IsNullOrEmpty(previousPlayerId) && previousPlayerId != playerId;
            MPLocalLoginProfile profile = await LoadLocalLoginProfileSafeAsync(cancellationToken);

            MPCloudSaveLoadResult<MPUserCloudSnapshot> userCloudResult = await LoadUserSnapshotSafeAsync(cancellationToken);
            if (userCloudResult == null)
            {
                return false;
            }

            MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> customCloudResult = await LoadCustomLevelSnapshotSafeAsync(cancellationToken);
            if (customCloudResult == null)
            {
                return false;
            }

            bool? comparisonResult = await ResolveHighRiskInitializationAsync(
                userCloudResult, customCloudResult, accountSwitched, profile, cancellationToken);
            if (comparisonResult.HasValue)
            {
                if (comparisonResult.Value)
                {
                    m_metaRepository.SaveActivePlayerId(playerId);
                    m_initialized = true;
                    _ = RefreshPlayerProfileSafeAsync();
                }
                return comparisonResult.Value;
            }

            bool userSynced = await SyncUserSnapshotOnInitializeAsync(userCloudResult, accountSwitched, profile, cancellationToken);
            if (!userSynced)
            {
                return false;
            }

            MPUserCustomLevelSnapshot legacyCustomLevel = userCloudResult.value?.customLevel;
            long legacyCustomUpdatedAtUtcTicks = userCloudResult.value?.updatedAtUtcTicks ?? 0;
            bool customSynced = await SyncCustomLevelSnapshotOnInitializeAsync(
                customCloudResult,
                legacyCustomLevel,
                legacyCustomUpdatedAtUtcTicks,
                accountSwitched,
                profile,
                cancellationToken);

            if (!customSynced)
            {
                return false;
            }

            if (HasCustomLevelData(legacyCustomLevel))
            {
                bool pruned = await PruneLegacyCustomLevelFromUserSnapshotAsync(profile, cancellationToken);
                if (!pruned)
                {
                    return false;
                }
            }

            m_metaRepository.SaveActivePlayerId(playerId);
            m_initialized = true;
            m_assetComparisonNeedsResolution = false;
            _ = RefreshPlayerProfileSafeAsync();
            Debug.Log($"[MPCloudSave] Initialize completed. PlayerId: {playerId}");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Initialize failed: {exception}");
            RememberError(exception.Message, false);
            return false;
        }
        finally
        {
            m_syncLock.Release();
        }
    }

    /// <summary>
    /// 标记本地用户数据已经变化，云同步层会按变更类型延迟上传对应 Key。
    /// </summary>
    public void MarkDirty(MPCloudSaveDirtyReason reason)
    {
        if (!MPLoginManager.Instance.IsLoggedIn || string.IsNullOrEmpty(MPLoginManager.Instance.PlayerId))
        {
            return;
        }

        string playerId = MPLoginManager.Instance.PlayerId;
        if (m_meta == null || m_meta.playerId != playerId)
        {
            m_meta = new MPCloudSaveLocalMeta
            {
                playerId = playerId
            };
        }

        m_playerId = playerId;
        if (reason == MPCloudSaveDirtyReason.CustomLevel)
        {
            m_meta.hasCustomLevelDirtyData = true;
        }
        else if (reason == MPCloudSaveDirtyReason.Unknown)
        {
            m_meta.hasUserSnapshotDirtyData = true;
            m_meta.hasCustomLevelDirtyData = true;
        }
        else
        {
            m_meta.hasUserSnapshotDirtyData = true;
        }

        m_meta.hasDirtyData = HasAnyDirtyData();
        m_meta.lastDirtyAtUtcTicks = DateTime.UtcNow.Ticks;
        m_meta.lastError = string.Empty;
        m_metaRepository.Save(m_meta);
        ScheduleDebouncedFlush(reason);
    }

    /// <summary>
    /// 立刻尝试把 dirty 数据上传到云端。
    /// </summary>
    public async Task<bool> FlushAsync(CancellationToken cancellationToken = default)
    {
        if (m_accountSwitchInProgress) return false;
        // 人工存档选择未完成时，生命周期/延迟上传不能绕过确认覆盖任意一侧。
        if (m_assetComparisonPending)
            return false;

        if (m_assetComparisonNeedsResolution)
            return await InitializeAfterUserLoadedAsync(cancellationToken);

        if (!MPLoginManager.Instance.IsLoggedIn || string.IsNullOrEmpty(MPLoginManager.Instance.PlayerId))
        {
            return false;
        }

        string playerId = MPLoginManager.Instance.PlayerId;
        await m_syncLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_assetComparisonPending)
                return false;
            if (MPLoginManager.Instance.PlayerId != playerId) return false;
            m_playerId = playerId;
            if (m_meta == null || m_meta.playerId != playerId)
            {
                m_meta = await m_metaRepository.LoadAsync(playerId, cancellationToken);
                m_meta.playerId = playerId;
            }

            NormalizeMetaDirtyFlags();
            if (!HasAnyDirtyData())
            {
                return true;
            }

            MPLocalLoginProfile profile = await LoadLocalLoginProfileSafeAsync(cancellationToken);
            bool userUploaded = true;
            if (m_meta.hasUserSnapshotDirtyData)
            {
                userUploaded = await FlushUserSnapshotAsync(profile, cancellationToken);
            }

            bool customUploaded = true;
            if (m_meta.hasCustomLevelDirtyData)
            {
                customUploaded = await FlushCustomLevelSnapshotAsync(profile, cancellationToken);
            }

            bool uploaded = userUploaded && customUploaded;
            if (uploaded)
            {
                Debug.Log($"[MPCloudSave] Flushed local dirty data. PlayerId: {playerId}");
            }

            return uploaded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Flush failed: {exception}");
            RememberError(exception.Message);
            return false;
        }
        finally
        {
            m_syncLock.Release();
        }
    }

    /// <summary>
    /// 保存自定义关卡图片文件到 Cloud Save Files。
    /// </summary>
    public async Task<bool> SavePlayerFileAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || bytes == null || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            await m_cloudSaveApi.SavePlayerFileAsync(key, bytes, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Save file failed. Key: {key}, Error: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 登录成功事件。
    /// 游戏已初始化时，切换账号后会重新执行云同步。
    /// </summary>
    private void OnLoginSucceeded(MPUserSession session)
    {
        if (m_assetComparisonCancellation != null && m_assetComparisonPlayerId != session?.userId)
            m_assetComparisonCancellation.Cancel();
        // 登录选择流程还在保存账号元数据，页面会在整个流程完成后显式启动同步。
        if (m_isUserDataReady && !MPLoginManager.Instance.IsLoginFlowRunning)
        {
            _ = InitializeAfterUserLoadedAsync();
        }
    }

    /// <summary>
    /// 登出完成事件，清理当前内存状态。
    /// </summary>
    private void OnLoggedOut()
    {
        m_assetComparisonCancellation?.Cancel();
        m_assetComparisonPending = false;
        m_assetComparisonNeedsResolution = false;
        CancelDebouncedFlush();
        m_initialized = false;
        m_playerId = string.Empty;
        m_meta = null;
    }

    /// <summary>
    /// 初始化阶段同步用户主快照。
    /// </summary>
    private async Task<bool> SyncUserSnapshotOnInitializeAsync(
        MPCloudSaveLoadResult<MPUserCloudSnapshot> cloudResult,
        bool accountSwitched,
        MPLocalLoginProfile profile,
        CancellationToken cancellationToken)
    {
        if (!cloudResult.exists || cloudResult.value == null)
        {
            if (accountSwitched)
            {
                MPUserCloudSnapshot defaultSnapshot = MPUserCloudSnapshot.CreateDefault(
                    m_playerId,
                    GetUnityEnvironmentName(),
                    ResolveLastLoginProvider(profile),
                    ResolveHasBoundIdentity(profile));

                ApplyUserSnapshot(defaultSnapshot);
                Debug.LogWarning($"[MPCloudSave] Account changed and no user cloud snapshot exists. Created fresh user data. PlayerId: {m_playerId}");
            }

            MPUserCloudSnapshot snapshot = MPUser.instance.CreateCloudSnapshot();
            ApplyLoginMetadata(snapshot, profile);
            snapshot.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            return await UploadUserSnapshotAsync(snapshot, false, true, cancellationToken);
        }

        m_meta.snapshotWriteLock = cloudResult.writeLock;
        if (!ValidateUserSnapshot(cloudResult.value, m_playerId))
        {
            await SaveMetaAsync("User snapshot validation failed.", cancellationToken);
            return false;
        }

        if (accountSwitched)
        {
            ApplyUserSnapshot(cloudResult.value);
            MarkUserSnapshotClean(cloudResult.writeLock);
            await m_metaRepository.SaveAsync(m_meta, cancellationToken);
            Debug.Log($"[MPCloudSave] Applied user cloud snapshot after account switch. PlayerId: {m_playerId}");
            return true;
        }

        if (m_meta.hasUserSnapshotDirtyData)
        {
            return await ResolveDirtyLocalUserSnapshotAsync(cloudResult.value, profile, cancellationToken);
        }

        ApplyUserSnapshot(cloudResult.value);
        MarkUserSnapshotClean(cloudResult.writeLock);
        await m_metaRepository.SaveAsync(m_meta, cancellationToken);
        Debug.Log($"[MPCloudSave] Applied user cloud snapshot. PlayerId: {m_playerId}");
        return true;
    }

    /// <summary>
    /// 初始化阶段同步自定义关卡独立快照，并兼容迁移旧主快照中的 customLevel。
    /// </summary>
    private async Task<bool> SyncCustomLevelSnapshotOnInitializeAsync(
        MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> cloudResult,
        MPUserCustomLevelSnapshot legacyCustomLevel,
        long legacyCustomUpdatedAtUtcTicks,
        bool accountSwitched,
        MPLocalLoginProfile profile,
        CancellationToken cancellationToken)
    {
        if (!cloudResult.exists || cloudResult.value == null)
        {
            MPCustomLevelCloudSnapshot seedSnapshot = null;
            if (accountSwitched)
            {
                seedSnapshot = HasCustomLevelData(legacyCustomLevel)
                    ? CreateCustomLevelSnapshotFromLegacy(legacyCustomLevel, legacyCustomUpdatedAtUtcTicks, profile)
                    : MPCustomLevelCloudSnapshot.CreateDefault(m_playerId, GetUnityEnvironmentName(), ResolveLastLoginProvider(profile), ResolveHasBoundIdentity(profile));
            }
            else if (HasCustomLevelData(legacyCustomLevel))
            {
                MPCustomLevelCloudSnapshot legacySnapshot = CreateCustomLevelSnapshotFromLegacy(legacyCustomLevel, legacyCustomUpdatedAtUtcTicks, profile);
                if (HasLocalCustomLevelData())
                {
                    MPCustomLevelCloudSnapshot localSnapshot = MPUser.instance.CreateCustomLevelCloudSnapshot();
                    ApplyLoginMetadata(localSnapshot, profile);
                    localSnapshot.updatedAtUtcTicks = m_meta.lastDirtyAtUtcTicks > 0 ? m_meta.lastDirtyAtUtcTicks : DateTime.UtcNow.Ticks;
                    seedSnapshot = m_conflictResolver.ResolveCustomLevel(localSnapshot, legacySnapshot);
                    ApplyLoginMetadata(seedSnapshot, profile);
                }
                else
                {
                    seedSnapshot = legacySnapshot;
                }
            }

            if (seedSnapshot != null)
            {
                ApplyCustomLevelSnapshot(seedSnapshot);
            }

            MPCustomLevelCloudSnapshot snapshot = MPUser.instance.CreateCustomLevelCloudSnapshot();
            ApplyLoginMetadata(snapshot, profile);
            snapshot.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            return await UploadCustomLevelSnapshotAsync(snapshot, false, true, cancellationToken);
        }

        m_meta.customLevelWriteLock = cloudResult.writeLock;
        if (!ValidateCustomLevelSnapshot(cloudResult.value, m_playerId))
        {
            await SaveMetaAsync("Custom level snapshot validation failed.", cancellationToken);
            return false;
        }

        if (accountSwitched)
        {
            ApplyCustomLevelSnapshot(cloudResult.value);
            MarkCustomLevelSnapshotClean(cloudResult.writeLock);
            await m_metaRepository.SaveAsync(m_meta, cancellationToken);
            Debug.Log($"[MPCloudSave] Applied custom level cloud snapshot after account switch. PlayerId: {m_playerId}");
            return true;
        }

        if (m_meta.hasCustomLevelDirtyData)
        {
            return await ResolveDirtyLocalCustomLevelSnapshotAsync(cloudResult.value, profile, cancellationToken);
        }

        ApplyCustomLevelSnapshot(cloudResult.value);
        MarkCustomLevelSnapshotClean(cloudResult.writeLock);
        await m_metaRepository.SaveAsync(m_meta, cancellationToken);
        Debug.Log($"[MPCloudSave] Applied custom level cloud snapshot. PlayerId: {m_playerId}");
        return true;
    }

    /// <summary>
    /// Flush 阶段上传用户主快照。
    /// </summary>
    private async Task<bool> FlushUserSnapshotAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(m_meta.snapshotWriteLock))
        {
            MPCloudSaveLoadResult<MPUserCloudSnapshot> cloudResult = await LoadUserSnapshotSafeAsync(cancellationToken);
            if (cloudResult == null)
            {
                return false;
            }

            if (cloudResult.exists && cloudResult.value != null)
            {
                if (!ValidateUserSnapshot(cloudResult.value, m_playerId))
                {
                    await SaveMetaAsync("User snapshot validation failed.", cancellationToken);
                    return false;
                }

                m_meta.snapshotWriteLock = cloudResult.writeLock;
                return await ResolveDirtyLocalUserSnapshotAsync(cloudResult.value, profile, cancellationToken);
            }
        }

        MPUserCloudSnapshot snapshot = MPUser.instance.CreateCloudSnapshot();
        ApplyLoginMetadata(snapshot, profile);
        snapshot.updatedAtUtcTicks = Math.Max(DateTime.UtcNow.Ticks, m_meta.lastDirtyAtUtcTicks);

        bool useWriteLock = !string.IsNullOrEmpty(m_meta.snapshotWriteLock);
        return await UploadUserSnapshotAsync(snapshot, useWriteLock, true, cancellationToken);
    }

    /// <summary>
    /// Flush 阶段上传自定义关卡独立快照。
    /// </summary>
    private async Task<bool> FlushCustomLevelSnapshotAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(m_meta.customLevelWriteLock))
        {
            MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> cloudResult = await LoadCustomLevelSnapshotSafeAsync(cancellationToken);
            if (cloudResult == null)
            {
                return false;
            }

            if (cloudResult.exists && cloudResult.value != null)
            {
                if (!ValidateCustomLevelSnapshot(cloudResult.value, m_playerId))
                {
                    await SaveMetaAsync("Custom level snapshot validation failed.", cancellationToken);
                    return false;
                }

                m_meta.customLevelWriteLock = cloudResult.writeLock;
                return await ResolveDirtyLocalCustomLevelSnapshotAsync(cloudResult.value, profile, cancellationToken);
            }
        }

        MPCustomLevelCloudSnapshot snapshot = MPUser.instance.CreateCustomLevelCloudSnapshot();
        ApplyLoginMetadata(snapshot, profile);
        snapshot.updatedAtUtcTicks = Math.Max(DateTime.UtcNow.Ticks, m_meta.lastDirtyAtUtcTicks);

        bool useWriteLock = !string.IsNullOrEmpty(m_meta.customLevelWriteLock);
        return await UploadCustomLevelSnapshotAsync(snapshot, useWriteLock, true, cancellationToken);
    }

    /// <summary>
    /// 旧版本曾经把 customLevel 写在用户主快照中；迁移到独立 Key 后，需要重新写一次主快照来清理旧的大字段。
    /// </summary>
    private async Task<bool> PruneLegacyCustomLevelFromUserSnapshotAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        MPUserCloudSnapshot snapshot = MPUser.instance.CreateCloudSnapshot();
        ApplyLoginMetadata(snapshot, profile);
        snapshot.updatedAtUtcTicks = DateTime.UtcNow.Ticks;

        bool useWriteLock = !string.IsNullOrEmpty(m_meta.snapshotWriteLock);
        bool uploaded = await UploadUserSnapshotAsync(snapshot, useWriteLock, true, cancellationToken);
        if (uploaded)
        {
            Debug.Log($"[MPCloudSave] Pruned legacy customLevel from user snapshot. PlayerId: {m_playerId}");
        }

        return uploaded;
    }

    /// <summary>
    /// 本地用户主快照存在 dirty 数据且云端也有快照时进行决策。
    /// </summary>
    private async Task<bool> ResolveDirtyLocalUserSnapshotAsync(MPUserCloudSnapshot cloudSnapshot, MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        MPUserCloudSnapshot localSnapshot = MPUser.instance.CreateCloudSnapshot();
        ApplyLoginMetadata(localSnapshot, profile);
        localSnapshot.updatedAtUtcTicks = m_meta.lastDirtyAtUtcTicks > 0 ? m_meta.lastDirtyAtUtcTicks : DateTime.UtcNow.Ticks;

        if (localSnapshot.updatedAtUtcTicks > cloudSnapshot.updatedAtUtcTicks)
        {
            return await UploadUserSnapshotAsync(localSnapshot, true, true, cancellationToken);
        }

        MPUserCloudSnapshot mergedSnapshot = m_conflictResolver.Resolve(localSnapshot, cloudSnapshot);
        ApplyLoginMetadata(mergedSnapshot, profile);
        ApplyUserSnapshot(mergedSnapshot);
        return await UploadUserSnapshotAsync(mergedSnapshot, true, true, cancellationToken);
    }

    /// <summary>
    /// 本地自定义关卡存在 dirty 数据且云端也有快照时进行决策。
    /// </summary>
    private async Task<bool> ResolveDirtyLocalCustomLevelSnapshotAsync(MPCustomLevelCloudSnapshot cloudSnapshot, MPLocalLoginProfile profile, CancellationToken cancellationToken)
    {
        MPCustomLevelCloudSnapshot localSnapshot = MPUser.instance.CreateCustomLevelCloudSnapshot();
        ApplyLoginMetadata(localSnapshot, profile);
        localSnapshot.updatedAtUtcTicks = m_meta.lastDirtyAtUtcTicks > 0 ? m_meta.lastDirtyAtUtcTicks : DateTime.UtcNow.Ticks;

        if (localSnapshot.updatedAtUtcTicks > cloudSnapshot.updatedAtUtcTicks)
        {
            return await UploadCustomLevelSnapshotAsync(localSnapshot, true, true, cancellationToken);
        }

        MPCustomLevelCloudSnapshot mergedSnapshot = m_conflictResolver.ResolveCustomLevel(localSnapshot, cloudSnapshot);
        ApplyLoginMetadata(mergedSnapshot, profile);
        ApplyCustomLevelSnapshot(mergedSnapshot);
        return await UploadCustomLevelSnapshotAsync(mergedSnapshot, true, true, cancellationToken);
    }

    /// <summary>
    /// 上传用户主快照。
    /// </summary>
    private async Task<bool> UploadUserSnapshotAsync(MPUserCloudSnapshot snapshot, bool useWriteLock, bool allowConflictResolve, CancellationToken cancellationToken)
    {
        try
        {
            string writeLock = useWriteLock ? m_meta.snapshotWriteLock : null;
            string newWriteLock = await m_cloudSaveApi.SavePlayerDataAsync(
                MPCloudSaveConstants.USER_SNAPSHOT_KEY,
                snapshot,
                writeLock,
                useWriteLock && !string.IsNullOrEmpty(writeLock),
                cancellationToken);

            MarkUserSnapshotClean(newWriteLock);
            await m_metaRepository.SaveAsync(m_meta, cancellationToken);
            m_metaRepository.SaveActivePlayerId(m_playerId);
            m_initialized = true;
            return true;
        }
        catch (CloudSaveConflictException exception)
        {
            if (!allowConflictResolve)
            {
                RememberError(exception.Message);
                Debug.LogWarning($"[MPCloudSave] User snapshot write conflict unresolved: {exception.Message}");
                return false;
            }

            Debug.LogWarning("[MPCloudSave] User snapshot write conflict detected. Reloading cloud snapshot and merging.");
            return await ResolveUserWriteConflictAsync(snapshot, cancellationToken);
        }
        catch (CloudSaveException exception)
        {
            RememberError(exception.Message);
            Debug.LogWarning($"[MPCloudSave] User snapshot upload failed. Reason: {exception.Reason}, Message: {exception.Message}");
            return false;
        }
        catch (Exception exception)
        {
            RememberError(exception.Message);
            Debug.LogWarning($"[MPCloudSave] User snapshot upload failed: {exception}");
            return false;
        }
    }

    /// <summary>
    /// 上传自定义关卡独立快照。
    /// </summary>
    private async Task<bool> UploadCustomLevelSnapshotAsync(MPCustomLevelCloudSnapshot snapshot, bool useWriteLock, bool allowConflictResolve, CancellationToken cancellationToken)
    {
        try
        {
            string writeLock = useWriteLock ? m_meta.customLevelWriteLock : null;
            string newWriteLock = await m_cloudSaveApi.SavePlayerDataAsync(
                MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY,
                snapshot,
                writeLock,
                useWriteLock && !string.IsNullOrEmpty(writeLock),
                cancellationToken);

            MarkCustomLevelSnapshotClean(newWriteLock);
            await m_metaRepository.SaveAsync(m_meta, cancellationToken);
            m_metaRepository.SaveActivePlayerId(m_playerId);
            m_initialized = true;
            return true;
        }
        catch (CloudSaveConflictException exception)
        {
            if (!allowConflictResolve)
            {
                RememberError(exception.Message);
                Debug.LogWarning($"[MPCloudSave] Custom level snapshot write conflict unresolved: {exception.Message}");
                return false;
            }

            Debug.LogWarning("[MPCloudSave] Custom level snapshot write conflict detected. Reloading cloud snapshot and merging.");
            return await ResolveCustomLevelWriteConflictAsync(snapshot, cancellationToken);
        }
        catch (CloudSaveException exception)
        {
            RememberError(exception.Message);
            Debug.LogWarning($"[MPCloudSave] Custom level snapshot upload failed. Reason: {exception.Reason}, Message: {exception.Message}");
            return false;
        }
        catch (Exception exception)
        {
            RememberError(exception.Message);
            Debug.LogWarning($"[MPCloudSave] Custom level snapshot upload failed: {exception}");
            return false;
        }
    }

    /// <summary>
    /// 用户主快照写锁冲突后重读云端并合并。
    /// </summary>
    private async Task<bool> ResolveUserWriteConflictAsync(MPUserCloudSnapshot localSnapshot, CancellationToken cancellationToken)
    {
        MPCloudSaveLoadResult<MPUserCloudSnapshot> cloudResult = await LoadUserSnapshotSafeAsync(cancellationToken);
        if (cloudResult == null)
        {
            return false;
        }

        if (!cloudResult.exists || cloudResult.value == null)
        {
            m_meta.snapshotWriteLock = string.Empty;
            return await UploadUserSnapshotAsync(localSnapshot, false, false, cancellationToken);
        }

        m_meta.snapshotWriteLock = cloudResult.writeLock;
        MPUserCloudSnapshot mergedSnapshot = m_conflictResolver.Resolve(localSnapshot, cloudResult.value);
        MPLocalLoginProfile profile = await LoadLocalLoginProfileSafeAsync(cancellationToken);
        ApplyLoginMetadata(mergedSnapshot, profile);
        ApplyUserSnapshot(mergedSnapshot);

        return await UploadUserSnapshotAsync(mergedSnapshot, true, false, cancellationToken);
    }

    /// <summary>
    /// 自定义关卡独立快照写锁冲突后重读云端并合并。
    /// </summary>
    private async Task<bool> ResolveCustomLevelWriteConflictAsync(MPCustomLevelCloudSnapshot localSnapshot, CancellationToken cancellationToken)
    {
        MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot> cloudResult = await LoadCustomLevelSnapshotSafeAsync(cancellationToken);
        if (cloudResult == null)
        {
            return false;
        }

        if (!cloudResult.exists || cloudResult.value == null)
        {
            m_meta.customLevelWriteLock = string.Empty;
            return await UploadCustomLevelSnapshotAsync(localSnapshot, false, false, cancellationToken);
        }

        m_meta.customLevelWriteLock = cloudResult.writeLock;
        MPCustomLevelCloudSnapshot mergedSnapshot = m_conflictResolver.ResolveCustomLevel(localSnapshot, cloudResult.value);
        MPLocalLoginProfile profile = await LoadLocalLoginProfileSafeAsync(cancellationToken);
        ApplyLoginMetadata(mergedSnapshot, profile);
        ApplyCustomLevelSnapshot(mergedSnapshot);

        return await UploadCustomLevelSnapshotAsync(mergedSnapshot, true, false, cancellationToken);
    }

    /// <summary>
    /// 安全读取用户主快照。
    /// </summary>
    private async Task<MPCloudSaveLoadResult<MPUserCloudSnapshot>> LoadUserSnapshotSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await m_cloudSaveApi.LoadPlayerDataAsync<MPUserCloudSnapshot>(
                MPCloudSaveConstants.USER_SNAPSHOT_KEY,
                cancellationToken);
        }
        catch (CloudSaveException exception)
        {
            Debug.LogWarning($"[MPCloudSave] User snapshot load failed. Reason: {exception.Reason}, Message: {exception.Message}");
            RememberError(exception.Message, false);
            return null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] User snapshot load failed: {exception}");
            RememberError(exception.Message, false);
            return null;
        }
    }

    /// <summary>
    /// 安全读取自定义关卡独立快照。
    /// </summary>
    private async Task<MPCloudSaveLoadResult<MPCustomLevelCloudSnapshot>> LoadCustomLevelSnapshotSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await m_cloudSaveApi.LoadPlayerDataAsync<MPCustomLevelCloudSnapshot>(
                MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_KEY,
                cancellationToken);
        }
        catch (CloudSaveException exception)
        {
            Debug.LogWarning($"[MPCloudSave] Custom level snapshot load failed. Reason: {exception.Reason}, Message: {exception.Message}");
            RememberError(exception.Message, false);
            return null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Custom level snapshot load failed: {exception}");
            RememberError(exception.Message, false);
            return null;
        }
    }

    /// <summary>
    /// 安全读取本地登录资料。
    /// </summary>
    private async Task<MPLocalLoginProfile> LoadLocalLoginProfileSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await MPLoginManager.Instance.LoadLocalProfileAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCloudSave] Load local login profile failed: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 应用用户主快照到 MPUser 和 ES3。
    /// </summary>
    private void ApplyUserSnapshot(MPUserCloudSnapshot snapshot)
    {
        MPUser.instance.ApplyCloudSnapshot(snapshot);
        m_initialized = true;
    }

    /// <summary>
    /// 应用自定义关卡独立快照到 MPUser 和 ES3。
    /// </summary>
    private void ApplyCustomLevelSnapshot(MPCustomLevelCloudSnapshot snapshot)
    {
        MPUser.instance.ApplyCustomLevelCloudSnapshot(snapshot);
        m_initialized = true;
    }

    /// <summary>
    /// 给用户主快照补充当前登录信息。
    /// </summary>
    private void ApplyLoginMetadata(MPUserCloudSnapshot snapshot, MPLocalLoginProfile profile)
    {
        if (snapshot == null)
        {
            return;
        }

        snapshot.schemaVersion = MPCloudSaveConstants.USER_SNAPSHOT_SCHEMA_VERSION;
        snapshot.playerId = m_playerId;
        snapshot.unityEnvironment = GetUnityEnvironmentName();
        snapshot.lastLoginProvider = ResolveLastLoginProvider(profile);
        snapshot.hasBoundIdentity = ResolveHasBoundIdentity(profile);
        snapshot.clientVersion = Application.version;
        snapshot.customLevel = null;
    }

    /// <summary>
    /// 给自定义关卡独立快照补充当前登录信息。
    /// </summary>
    private void ApplyLoginMetadata(MPCustomLevelCloudSnapshot snapshot, MPLocalLoginProfile profile)
    {
        if (snapshot == null)
        {
            return;
        }

        snapshot.schemaVersion = MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION;
        snapshot.playerId = m_playerId;
        snapshot.unityEnvironment = GetUnityEnvironmentName();
        snapshot.lastLoginProvider = ResolveLastLoginProvider(profile);
        snapshot.hasBoundIdentity = ResolveHasBoundIdentity(profile);
        snapshot.clientVersion = Application.version;
        snapshot.customLevel = snapshot.customLevel ?? new MPUserCustomLevelSnapshot();
    }

    /// <summary>
    /// 校验用户主快照是否可被当前账号使用。
    /// </summary>
    private static bool ValidateUserSnapshot(MPUserCloudSnapshot snapshot, string playerId)
    {
        if (snapshot.schemaVersion > MPCloudSaveConstants.USER_SNAPSHOT_SCHEMA_VERSION)
        {
            Debug.LogError($"[MPCloudSave] Unsupported user snapshot schema: {snapshot.schemaVersion}");
            return false;
        }

        if (!string.IsNullOrEmpty(snapshot.playerId) && snapshot.playerId != playerId)
        {
            Debug.LogError($"[MPCloudSave] User snapshot playerId mismatch. Current: {playerId}, Snapshot: {snapshot.playerId}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 校验自定义关卡独立快照是否可被当前账号使用。
    /// </summary>
    private static bool ValidateCustomLevelSnapshot(MPCustomLevelCloudSnapshot snapshot, string playerId)
    {
        if (snapshot.schemaVersion > MPCloudSaveConstants.CUSTOM_LEVEL_SNAPSHOT_SCHEMA_VERSION)
        {
            Debug.LogError($"[MPCloudSave] Unsupported custom level snapshot schema: {snapshot.schemaVersion}");
            return false;
        }

        if (!string.IsNullOrEmpty(snapshot.playerId) && snapshot.playerId != playerId)
        {
            Debug.LogError($"[MPCloudSave] Custom level snapshot playerId mismatch. Current: {playerId}, Snapshot: {snapshot.playerId}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 将旧主快照中的 customLevel 包装成新的独立自定义关卡快照，用于兼容迁移。
    /// </summary>
    private MPCustomLevelCloudSnapshot CreateCustomLevelSnapshotFromLegacy(MPUserCustomLevelSnapshot customLevel, long updatedAtUtcTicks, MPLocalLoginProfile profile)
    {
        MPCustomLevelCloudSnapshot snapshot = new MPCustomLevelCloudSnapshot
        {
            customLevel = customLevel ?? new MPUserCustomLevelSnapshot(),
            updatedAtUtcTicks = updatedAtUtcTicks > 0 ? updatedAtUtcTicks : DateTime.UtcNow.Ticks
        };

        ApplyLoginMetadata(snapshot, profile);
        return snapshot;
    }

    /// <summary>
    /// 判断自定义关卡快照中是否包含实际数据。
    /// </summary>
    private static bool HasCustomLevelData(MPUserCustomLevelSnapshot snapshot)
    {
        return snapshot != null &&
               ((snapshot.levels != null && snapshot.levels.Count > 0) ||
                (snapshot.passList != null && snapshot.passList.Count > 0));
    }

    /// <summary>
    /// 判断本地 MPUser 中是否有自定义关卡数据。
    /// </summary>
    private static bool HasLocalCustomLevelData()
    {
        MPCustomLevelCloudSnapshot snapshot = MPUser.instance.CreateCustomLevelCloudSnapshot();
        return HasCustomLevelData(snapshot.customLevel);
    }

    /// <summary>
    /// 标记用户主快照为已同步。
    /// </summary>
    private void MarkUserSnapshotClean(string writeLock)
    {
        if (m_meta == null)
        {
            m_meta = new MPCloudSaveLocalMeta();
        }

        m_meta.playerId = m_playerId;
        m_meta.snapshotWriteLock = writeLock;
        m_meta.hasUserSnapshotDirtyData = false;
        m_meta.lastSyncedAtUtcTicks = DateTime.UtcNow.Ticks;
        m_meta.lastError = string.Empty;
        RefreshGlobalDirtyFlag();
    }

    /// <summary>
    /// 标记自定义关卡独立快照为已同步。
    /// </summary>
    private void MarkCustomLevelSnapshotClean(string writeLock)
    {
        if (m_meta == null)
        {
            m_meta = new MPCloudSaveLocalMeta();
        }

        m_meta.playerId = m_playerId;
        m_meta.customLevelWriteLock = writeLock;
        m_meta.hasCustomLevelDirtyData = false;
        m_meta.lastSyncedAtUtcTicks = DateTime.UtcNow.Ticks;
        m_meta.lastError = string.Empty;
        RefreshGlobalDirtyFlag();
    }

    /// <summary>
    /// 兼容旧版本只有 hasDirtyData 的本地元数据。
    /// </summary>
    private void NormalizeMetaDirtyFlags()
    {
        if (m_meta == null)
        {
            return;
        }

        bool hasSpecificDirty = m_meta.hasUserSnapshotDirtyData || m_meta.hasCustomLevelDirtyData;
        if (m_meta.hasDirtyData && !hasSpecificDirty)
        {
            m_meta.hasUserSnapshotDirtyData = true;
            m_meta.hasCustomLevelDirtyData = true;
        }

        RefreshGlobalDirtyFlag();
    }

    /// <summary>
    /// 刷新兼容旧版本使用的总 dirty 标记。
    /// </summary>
    private void RefreshGlobalDirtyFlag()
    {
        if (m_meta == null)
        {
            return;
        }

        m_meta.hasDirtyData = HasAnyDirtyData();
    }

    /// <summary>
    /// 当前是否存在任何未上传数据。
    /// </summary>
    private bool HasAnyDirtyData()
    {
        return m_meta != null && (m_meta.hasUserSnapshotDirtyData || m_meta.hasCustomLevelDirtyData);
    }

    /// <summary>
    /// 记录同步错误，并在需要时保留 dirty 状态。
    /// </summary>
    private void RememberError(string message, bool keepDirty = true)
    {
        if (string.IsNullOrEmpty(m_playerId))
        {
            m_playerId = MPLoginManager.Instance.PlayerId;
        }

        if (string.IsNullOrEmpty(m_playerId))
        {
            return;
        }

        if (m_meta == null)
        {
            m_meta = new MPCloudSaveLocalMeta
            {
                playerId = m_playerId
            };
        }

        m_meta.playerId = m_playerId;
        if (keepDirty)
        {
            if (!HasAnyDirtyData())
            {
                m_meta.hasUserSnapshotDirtyData = true;
            }

            if (m_meta.lastDirtyAtUtcTicks <= 0)
            {
                m_meta.lastDirtyAtUtcTicks = DateTime.UtcNow.Ticks;
            }
        }

        m_meta.lastError = message;
        RefreshGlobalDirtyFlag();
        m_metaRepository.Save(m_meta);
    }

    /// <summary>
    /// 保存元数据错误信息。
    /// </summary>
    private async Task SaveMetaAsync(string error, CancellationToken cancellationToken)
    {
        if (m_meta == null)
        {
            return;
        }

        m_meta.lastError = error;
        RefreshGlobalDirtyFlag();
        await m_metaRepository.SaveAsync(m_meta, cancellationToken);
    }

    /// <summary>
    /// 安排延迟上传。
    /// </summary>
    private void ScheduleDebouncedFlush(MPCloudSaveDirtyReason reason)
    {
        if (!m_isUserDataReady)
        {
            return;
        }

        CancelDebouncedFlush();
        m_debounceCancellation = new CancellationTokenSource();
        CancellationToken token = m_debounceCancellation.Token;
        _ = DebouncedFlushAsync(reason, token);
    }

    /// <summary>
    /// 延迟执行 flush。
    /// </summary>
    private async Task DebouncedFlushAsync(MPCloudSaveDirtyReason reason, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DIRTY_FLUSH_DELAY_MS, cancellationToken);
            Debug.Log($"[MPCloudSave] Debounced flush. Reason: {reason}");
            await FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 新的 dirty 会取消旧的延迟任务，这是正常流程。
        }
    }

    /// <summary>
    /// 取消延迟上传。
    /// </summary>
    private void CancelDebouncedFlush()
    {
        if (m_debounceCancellation == null)
        {
            return;
        }

        m_debounceCancellation.Cancel();
        m_debounceCancellation.Dispose();
        m_debounceCancellation = null;
    }

    /// <summary>
    /// 初始化生命周期钩子。
    /// </summary>
    private void EnsureLifecycleHook()
    {
        if (m_lifecycleHook != null)
        {
            return;
        }

        GameObject hookObject = new GameObject("[MPCloudSaveLifecycle]");
        UnityEngine.Object.DontDestroyOnLoad(hookObject);
        m_lifecycleHook = hookObject.AddComponent<MPCloudSaveLifecycleHook>();
    }

    /// <summary>
    /// 解析最近登录方式。
    /// </summary>
    private static string ResolveLastLoginProvider(MPLocalLoginProfile profile)
    {
        if (profile != null && profile.lastLoginProvider != MPLoginProvider.Unknown)
        {
            return profile.lastLoginProvider.ToString();
        }

        MPUserSession session = MPLoginManager.Instance.CurrentSession;
        return session == null ? MPLoginProvider.Unknown.ToString() : session.loginType.ToString();
    }

    /// <summary>
    /// 解析账号是否已经绑定正式身份。
    /// </summary>
    private static bool ResolveHasBoundIdentity(MPLocalLoginProfile profile)
    {
        if (profile != null)
        {
            return profile.hasBoundIdentity ||
                   profile.accountType == MPAccountType.Bound ||
                   profile.hasUsernamePasswordBinding ||
                   profile.hasGoogleBinding ||
                   profile.hasGooglePlayGamesBinding ||
                   profile.hasAppleBinding ||
                   profile.hasFacebookBinding;
        }

        MPUserSession session = MPLoginManager.Instance.CurrentSession;
        return session != null && !session.isGuest && session.loginType != MPLoginType.Guest;
    }

    /// <summary>
    /// 获取当前 Unity Services Environment 名称。
    /// </summary>
    private static string GetUnityEnvironmentName()
    {
#if UNITY_EDITOR
        return MPCloudSaveConstants.DEVELOPMENT_ENVIRONMENT;
#else
        return MPCloudSaveConstants.PRODUCTION_ENVIRONMENT;
#endif
    }
}

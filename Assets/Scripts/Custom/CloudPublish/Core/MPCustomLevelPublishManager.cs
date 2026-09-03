using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// 自定义关卡公开发布模块门面。
/// UI 层通过该类完成上传、列表、体验、点赞和撤销，不直接依赖 Unity Cloud Code SDK。
/// </summary>
public class MPCustomLevelPublishManager
{
    /// <summary>
    /// 本地发布状态缓存 Key 前缀，后面会拼接当前 PlayerId。
    /// </summary>
    private const string LOCAL_STATE_KEY_PREFIX = "MPCustomLevelPublish.LocalState.";

    /// <summary>
    /// 社区分页缓存有效时间。缓存只保存在当前进程中，退出游戏后自然清理。
    /// </summary>
    private static readonly long COMMUNITY_CACHE_LIFETIME_TICKS = TimeSpan.FromMinutes(10).Ticks;

    /// <summary>
    /// 单例实例。
    /// </summary>
    private static MPCustomLevelPublishManager m_instance;

    /// <summary>
    /// 云端发布 API。
    /// </summary>
    private readonly IMPCustomLevelPublishApi m_publishApi;

    /// <summary>
    /// 当前正在执行的上传任务。按本地关卡ID去重，避免页面关闭并重新打开后重复提交。
    /// </summary>
    private readonly Dictionary<string, Task<MPCustomLevelPublishResult>> m_publishOperations =
        new Dictionary<string, Task<MPCustomLevelPublishResult>>();

    /// <summary>
    /// 上传任务字典同步锁。
    /// </summary>
    private readonly object m_publishOperationsLock = new object();

    /// <summary>
    /// 当前玩家的社区关卡统一实例缓存，保证 All/Liked 两个列表共享点赞状态。
    /// </summary>
    private readonly Dictionary<string, MPCustomLevelPublicRecord> m_communityRecordCache =
        new Dictionary<string, MPCustomLevelPublicRecord>(StringComparer.Ordinal);

    /// <summary>
    /// 按排序、分页大小和游标保存的社区分页缓存。
    /// </summary>
    private readonly Dictionary<string, CommunityPageCacheEntry> m_communityPageCache =
        new Dictionary<string, CommunityPageCacheEntry>(StringComparer.Ordinal);

    private readonly Dictionary<string, CommunityLikeOperation> m_likeOperations =
        new Dictionary<string, CommunityLikeOperation>(StringComparer.Ordinal);
    private readonly object m_likeOperationsLock = new object();

    /// <summary>
    /// 当前玩家已发布关卡统计缓存的后台同步任务。同一玩家重复打开页面时复用未完成的任务。
    /// </summary>
    private readonly object m_localStatsSyncLock = new object();
    private Task m_localStatsSyncTask;
    private string m_localStatsSyncPlayerId;

    private string m_communityCachePlayerId;

    /// <summary>
    /// 当前已加载缓存所属的 PlayerId。
    /// </summary>
    private string m_loadedPlayerId;

    /// <summary>
    /// 当前玩家本地发布状态缓存。
    /// </summary>
    private MPCustomLevelPublishLocalStateCollection m_localState;

    private MPCustomLevelPublishManager()
    {
        m_publishApi = new MPUnityCloudCodeCustomLevelPublishApi();
    }

    /// <summary>
    /// 自定义关卡公开发布模块单例。
    /// </summary>
    public static MPCustomLevelPublishManager Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new MPCustomLevelPublishManager();
            }

            return m_instance;
        }
    }

    /// <summary>
    /// 本地发布状态变化事件，参数为发生变化的本地关卡发布状态。
    /// </summary>
    public event Action<MPCustomLevelPublishLocalState> PublishStateChanged;

    /// <summary>
    /// 某个本地关卡的上传任务开始或结束时触发，用于跨页面同步按钮状态。
    /// </summary>
    public event Action<string> PublishOperationChanged;

    /// <summary>
    /// 社区关卡点赞状态发生变化。
    /// isFinal 为 false 表示乐观更新，为 true 表示服务端确认或失败回滚完成。
    /// </summary>
    public event Action<MPCustomLevelPublicRecord, bool> CommunityLikeStateChanged;

    /// <summary>
    /// 将本地自定义关卡发布到公开云端目录。
    /// </summary>
    public Task<MPCustomLevelPublishResult> PublishAsync(MPCustomLevelInfo levelInfo, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        cancellationToken.ThrowIfCancellationRequested();
        MPCustomLevelInfo normalizedLevel = NormalizeLevel(levelInfo);
        if (normalizedLevel == null)
        {
            throw new ArgumentException("自定义关卡数据为空或不合法，无法上传。", nameof(levelInfo));
        }

        MPCustomLevelPublishLocalState localState = GetLocalState(normalizedLevel.ID);
        if (localState != null && localState.IsPublished && !string.IsNullOrEmpty(localState.publicLevelId))
        {
            return Task.FromResult(new MPCustomLevelPublishResult
            {
                success = true,
                publicLevelId = localState.publicLevelId,
                status = localState.status,
                message = "AlreadyPublished"
            });
        }

        Task<MPCustomLevelPublishResult> operation;
        bool operationCreated = false;
        lock (m_publishOperationsLock)
        {
            if (!m_publishOperations.TryGetValue(normalizedLevel.ID, out operation))
            {
                // 请求一旦提交给 Cloud Code，就不能再使用页面生命周期 Token 中断业务结果落盘。
                operation = PublishAndPersistAsync(normalizedLevel);
                m_publishOperations.Add(normalizedLevel.ID, operation);
                operationCreated = true;
            }
        }

        if (operationCreated)
        {
            PublishOperationChanged?.Invoke(normalizedLevel.ID);
        }

        return operation;
    }

    /// <summary>
    /// 判断指定本地关卡是否正在上传。
    /// </summary>
    public bool IsPublishPending(string sourceLocalLevelId)
    {
        if (string.IsNullOrEmpty(sourceLocalLevelId))
        {
            return false;
        }

        lock (m_publishOperationsLock)
        {
            return m_publishOperations.ContainsKey(sourceLocalLevelId);
        }
    }

    /// <summary>
    /// 执行一次不可被页面关闭中断的上传，并在服务端返回后持久化本地发布状态。
    /// </summary>
    private async Task<MPCustomLevelPublishResult> PublishAndPersistAsync(MPCustomLevelInfo normalizedLevel)
    {
        try
        {
            MPCustomLevelPublishResult result = await m_publishApi.PublishAsync(normalizedLevel, CancellationToken.None);
            if (result != null && result.success && !string.IsNullOrEmpty(result.publicLevelId))
            {
                UpsertLocalState(
                    normalizedLevel.ID,
                    result.publicLevelId,
                    result.status,
                    string.Empty,
                    result.record?.likeCount ?? 0,
                    result.record?.playCount ?? 0);
                InvalidateCommunityPageCache();
            }

            return result;
        }
        finally
        {
            lock (m_publishOperationsLock)
            {
                m_publishOperations.Remove(normalizedLevel.ID);
            }

            PublishOperationChanged?.Invoke(normalizedLevel.ID);
        }
    }

    /// <summary>
    /// 获取公开自定义关卡列表。
    /// </summary>
    public Task<MPCustomLevelListResult> GetListAsync(
        string sortType,
        int pageSize,
        string cursor,
        CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCommunityCachePlayer();
        RemoveExpiredCommunityPages();

        string cacheKey = BuildCommunityPageCacheKey(sortType, pageSize, cursor);
        if (m_communityPageCache.TryGetValue(cacheKey, out CommunityPageCacheEntry cachedPage))
            return Task.FromResult(CloneListResult(cachedPage.result));

        return GetListAndCacheAsync(sortType, pageSize, cursor, cacheKey, cancellationToken);
    }

    /// <summary>
    /// 获取公开自定义关卡详情。
    /// </summary>
    public Task<MPCustomLevelPublicRecord> GetDetailAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        EnsurePublicLevelId(publicLevelId);
        return m_publishApi.GetDetailAsync(publicLevelId, cancellationToken);
    }

    /// <summary>
    /// 获取本地关卡上一次成功同步的点赞数量。首次没有缓存时返回 0。
    /// </summary>
    public int GetCachedLocalLevelLikeCount(string sourceLocalLevelId)
    {
        MPCustomLevelPublishLocalState state = GetLocalState(sourceLocalLevelId);
        return state == null || !state.IsPublished
            ? 0
            : Mathf.Max(0, state.cachedLikeCount);
    }

    /// <summary>
    /// 获取本地关卡上一次成功同步的试玩次数。首次没有缓存时返回 0。
    /// </summary>
    public int GetCachedLocalLevelPlayCount(string sourceLocalLevelId)
    {
        MPCustomLevelPublishLocalState state = GetLocalState(sourceLocalLevelId);
        return state == null || !state.IsPublished
            ? 0
            : Mathf.Max(0, state.cachedPlayCount);
    }

    /// <summary>
    /// 在后台刷新当前玩家所有已发布本地关卡的点赞和试玩缓存。
    /// 该任务不触发 UI 事件，结果只会在下一次打开页面时展示。
    /// </summary>
    public Task RefreshPublishedLocalLevelStatsCacheAsync()
    {
        if (MPLoginManager.Instance == null ||
            !MPLoginManager.Instance.IsLoggedIn ||
            string.IsNullOrEmpty(MPLoginManager.Instance.PlayerId))
        {
            return Task.CompletedTask;
        }

        EnsureLocalStateLoaded();
        List<LocalLevelStatsSyncTarget> targets = new List<LocalLevelStatsSyncTarget>();
        if (m_localState?.items != null)
        {
            HashSet<string> publicLevelIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < m_localState.items.Count; i++)
            {
                MPCustomLevelPublishLocalState state = m_localState.items[i];
                if (state == null ||
                    !state.IsPublished ||
                    string.IsNullOrEmpty(state.sourceLocalLevelId) ||
                    string.IsNullOrEmpty(state.publicLevelId) ||
                    !publicLevelIds.Add(state.publicLevelId))
                {
                    continue;
                }

                targets.Add(new LocalLevelStatsSyncTarget
                {
                    sourceLocalLevelId = state.sourceLocalLevelId,
                    publicLevelId = state.publicLevelId,
                    likeCacheVersionUtcTicks = state.likeCountSyncedAtUtcTicks,
                    playCacheVersionUtcTicks = state.playCountSyncedAtUtcTicks,
                });
            }
        }

        if (targets.Count == 0)
            return Task.CompletedTask;

        string playerId = MPLoginManager.Instance.PlayerId;
        lock (m_localStatsSyncLock)
        {
            if (m_localStatsSyncTask != null &&
                !m_localStatsSyncTask.IsCompleted &&
                m_localStatsSyncPlayerId == playerId)
            {
                return m_localStatsSyncTask;
            }

            m_localStatsSyncPlayerId = playerId;
            m_localStatsSyncTask = RefreshPublishedLocalLevelStatsCacheCoreAsync(
                playerId,
                targets);
            return m_localStatsSyncTask;
        }
    }

    private async Task RefreshPublishedLocalLevelStatsCacheCoreAsync(
        string playerId,
        List<LocalLevelStatsSyncTarget> targets)
    {
        Dictionary<string, MPCustomLevelStatsRecord> statsByPublicLevelId =
            new Dictionary<string, MPCustomLevelStatsRecord>(StringComparer.Ordinal);
        for (int offset = 0;
             offset < targets.Count;
             offset += MPCustomLevelPublishConstants.MAX_STATS_BATCH_SIZE)
        {
            int count = Mathf.Min(
                MPCustomLevelPublishConstants.MAX_STATS_BATCH_SIZE,
                targets.Count - offset);
            List<string> publicLevelIds = new List<string>(count);
            for (int i = 0; i < count; i++)
                publicLevelIds.Add(targets[offset + i].publicLevelId);

            try
            {
                MPCustomLevelStatsResult result = await m_publishApi.GetStatsAsync(
                    publicLevelIds,
                    CancellationToken.None);
                if (result == null || !result.success)
                {
                    Debug.LogWarning(
                        $"[MPCustomLevelPublishManager] 拉取关卡统计失败：{result?.message}");
                    continue;
                }

                if (result.items == null)
                    continue;

                for (int i = 0; i < result.items.Count; i++)
                {
                    MPCustomLevelStatsRecord item = result.items[i];
                    if (item == null || string.IsNullOrEmpty(item.publicLevelId))
                        continue;

                    statsByPublicLevelId[item.publicLevelId] = item;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[MPCustomLevelPublishManager] 拉取关卡统计异常：{FormatExceptionForLog(exception)}");
            }
        }

        // 玩家已经切换时丢弃旧账号请求结果，避免写进新账号的 ES3 缓存。
        if (statsByPublicLevelId.Count == 0 || ResolvePlayerId() != playerId)
            return;

        EnsureLocalStateLoaded();
        if (m_loadedPlayerId != playerId || m_localState?.items == null)
            return;

        long syncedAtUtcTicks = DateTime.UtcNow.Ticks;
        bool shouldSave = false;
        for (int i = 0; i < targets.Count; i++)
        {
            LocalLevelStatsSyncTarget target = targets[i];
            if (!statsByPublicLevelId.TryGetValue(target.publicLevelId, out MPCustomLevelStatsRecord stats))
                continue;

            MPCustomLevelPublishLocalState state = m_localState.items.Find(item =>
                item != null &&
                item.sourceLocalLevelId == target.sourceLocalLevelId &&
                item.publicLevelId == target.publicLevelId &&
                item.IsPublished);
            if (state == null)
                continue;

            // 两项统计分别校验版本，更新的点赞确认不会被覆盖，也不会阻止试玩缓存更新。
            if (state.likeCountSyncedAtUtcTicks == target.likeCacheVersionUtcTicks)
            {
                state.cachedLikeCount = Mathf.Max(0, stats.likeCount);
                state.likeCountSyncedAtUtcTicks = syncedAtUtcTicks;
                shouldSave = true;
            }

            if (stats.playCount.HasValue &&
                state.playCountSyncedAtUtcTicks == target.playCacheVersionUtcTicks)
            {
                state.cachedPlayCount = Mathf.Max(0, stats.playCount.Value);
                state.playCountSyncedAtUtcTicks = syncedAtUtcTicks;
                shouldSave = true;
            }
        }

        if (!shouldSave)
            return;

        try
        {
            SaveLocalState();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[MPCustomLevelPublishManager] 保存关卡统计缓存失败：{FormatExceptionForLog(exception)}");
        }
    }

    /// <summary>
    /// 记录一次体验并获取公开自定义关卡详情。
    /// </summary>
    public Task<MPCustomLevelPublicRecord> PlayAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        EnsurePublicLevelId(publicLevelId);
        return m_publishApi.PlayAsync(publicLevelId, cancellationToken);
    }

    /// <summary>
    /// 设置公开自定义关卡的点赞状态。
    /// 调用时立即更新本地记录和分页缓存，服务端失败时自动回滚。
    /// </summary>
    public Task<MPCustomLevelLikeResult> LikeAsync(
        MPCustomLevelPublicRecord record,
        bool liked)
    {
        EnsureLoggedIn();
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        EnsurePublicLevelId(record.publicLevelId);
        EnsureCommunityCachePlayer();

        MPCustomLevelPublicRecord cachedRecord = CacheCommunityRecord(record);
        CommunityLikeOperation operation;
        Task<MPCustomLevelLikeResult> operationTask;
        lock (m_likeOperationsLock)
        {
            if (!m_likeOperations.TryGetValue(cachedRecord.publicLevelId, out operation))
            {
                operation = new CommunityLikeOperation
                {
                    record = cachedRecord,
                    desiredLiked = cachedRecord.likedByCurrentPlayer,
                    confirmedLiked = cachedRecord.likedByCurrentPlayer,
                    confirmedLikeCount = Mathf.Max(0, cachedRecord.likeCount),
                };
                m_likeOperations.Add(cachedRecord.publicLevelId, operation);
            }

            operation.desiredLiked = liked;
            ApplyCommunityLikeState(operation.record, liked);
            operation.operationTask ??= SynchronizeLikeOperationAsync(operation);
            operationTask = operation.operationTask;
        }

        SynchronizeLikedPageCache(operation.record);
        CommunityLikeStateChanged?.Invoke(operation.record, false);
        return operationTask;
    }

    public bool IsLikePending(string publicLevelId)
    {
        if (string.IsNullOrEmpty(publicLevelId))
            return false;

        lock (m_likeOperationsLock)
            return m_likeOperations.ContainsKey(publicLevelId);
    }

    private async Task<MPCustomLevelListResult> GetListAndCacheAsync(
        string sortType,
        int pageSize,
        string cursor,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        MPCustomLevelListResult result = await m_publishApi.GetListAsync(
            sortType,
            pageSize,
            cursor,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (result == null || !result.success)
            return result;

        EnsureCommunityCachePlayer();
        MPCustomLevelListResult cachedResult = NormalizeCommunityListResult(result);
        m_communityPageCache[cacheKey] = new CommunityPageCacheEntry
        {
            sortType = sortType ?? string.Empty,
            pageSize = pageSize,
            cursor = cursor ?? string.Empty,
            cachedAtUtcTicks = DateTime.UtcNow.Ticks,
            result = cachedResult,
        };
        return CloneListResult(cachedResult);
    }

    private async Task<MPCustomLevelLikeResult> SynchronizeLikeOperationAsync(
        CommunityLikeOperation operation)
    {
        // 确保 LikeAsync 先完成本地乐观刷新和任务登记，再开始处理服务端响应。
        await Task.Yield();

        while (true)
        {
            bool requestLiked;
            lock (m_likeOperationsLock)
            {
                if (!IsCurrentLikeOperation(operation))
                    return null;
                requestLiked = operation.desiredLiked;
            }

            MPCustomLevelLikeResult result;
            try
            {
                // 点赞属于业务状态变更，请求不跟随 Item 或页面生命周期取消。
                result = await m_publishApi.LikeAsync(
                    operation.record.publicLevelId,
                    requestLiked,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                RollbackLikeOperation(operation);
                Debug.LogError(
                    $"[MPCustomLevelPublishManager] 同步点赞状态失败：{FormatExceptionForLog(exception)}");
                return new MPCustomLevelLikeResult
                {
                    success = false,
                    liked = operation.record.likedByCurrentPlayer,
                    likeCount = operation.record.likeCount,
                    message = exception.Message,
                    record = operation.record,
                };
            }

            if (result == null || !result.success)
            {
                RollbackLikeOperation(operation);
                Debug.LogWarning(
                    $"[MPCustomLevelPublishManager] 服务端未能同步点赞状态：{result?.message}");
                return result;
            }

            bool matchesLatestTarget;
            lock (m_likeOperationsLock)
            {
                if (!IsCurrentLikeOperation(operation))
                    return result;

                operation.confirmedLiked = result.liked;
                operation.confirmedLikeCount = Mathf.Max(0, result.likeCount);
                matchesLatestTarget = operation.desiredLiked == result.liked;
            }

            if (result.record != null)
            {
                operation.record = CacheCommunityRecord(
                    result.record,
                    preservePendingLikeState: !matchesLatestTarget);
                result.record = operation.record;
            }
            else if (matchesLatestTarget)
            {
                operation.record.likedByCurrentPlayer = result.liked;
                operation.record.likeCount = Mathf.Max(0, result.likeCount);
            }

            if (!matchesLatestTarget)
                continue;

            CompleteLikeOperation(operation);
            SynchronizeLikedPageCache(operation.record);
            CacheConfirmedLocalLevelLikeCount(
                operation.record.publicLevelId,
                operation.record.likeCount);
            CommunityLikeStateChanged?.Invoke(operation.record, true);
            return result;
        }
    }

    private bool IsCurrentLikeOperation(CommunityLikeOperation operation)
    {
        return operation != null &&
               operation.record != null &&
               m_likeOperations.TryGetValue(operation.record.publicLevelId, out CommunityLikeOperation current) &&
               ReferenceEquals(current, operation);
    }

    private void CompleteLikeOperation(CommunityLikeOperation operation)
    {
        lock (m_likeOperationsLock)
        {
            if (IsCurrentLikeOperation(operation))
                m_likeOperations.Remove(operation.record.publicLevelId);
        }
    }

    private void RollbackLikeOperation(CommunityLikeOperation operation)
    {
        CompleteLikeOperation(operation);
        operation.record.likedByCurrentPlayer = operation.confirmedLiked;
        operation.record.likeCount = Mathf.Max(0, operation.confirmedLikeCount);
        SynchronizeLikedPageCache(operation.record);
        CacheConfirmedLocalLevelLikeCount(
            operation.record.publicLevelId,
            operation.record.likeCount);
        CommunityLikeStateChanged?.Invoke(operation.record, true);
    }

    private static void ApplyCommunityLikeState(MPCustomLevelPublicRecord record, bool liked)
    {
        if (record == null || record.likedByCurrentPlayer == liked)
            return;

        record.likedByCurrentPlayer = liked;
        record.likeCount = liked
            ? Mathf.Max(0, record.likeCount) + 1
            : Mathf.Max(0, record.likeCount - 1);
    }

    private MPCustomLevelListResult NormalizeCommunityListResult(MPCustomLevelListResult result)
    {
        List<MPCustomLevelPublicRecord> records = new List<MPCustomLevelPublicRecord>();
        if (result.items != null)
        {
            for (int i = 0; i < result.items.Count; i++)
            {
                MPCustomLevelPublicRecord record = result.items[i];
                if (record == null || string.IsNullOrEmpty(record.publicLevelId))
                    continue;

                records.Add(CacheCommunityRecord(record));
            }
        }

        return new MPCustomLevelListResult
        {
            success = result.success,
            items = records,
            nextCursor = result.nextCursor ?? string.Empty,
            message = result.message ?? string.Empty,
        };
    }

    private MPCustomLevelPublicRecord CacheCommunityRecord(
        MPCustomLevelPublicRecord source,
        bool preservePendingLikeState = true)
    {
        if (source == null || string.IsNullOrEmpty(source.publicLevelId))
            return source;

        if (!m_communityRecordCache.TryGetValue(source.publicLevelId, out MPCustomLevelPublicRecord cachedRecord))
        {
            m_communityRecordCache[source.publicLevelId] = source;
            return source;
        }

        if (!ReferenceEquals(cachedRecord, source))
        {
            bool shouldPreserveLikeState = false;
            bool localLiked = cachedRecord.likedByCurrentPlayer;
            int localLikeCount = cachedRecord.likeCount;
            if (preservePendingLikeState)
            {
                lock (m_likeOperationsLock)
                {
                    shouldPreserveLikeState = m_likeOperations.ContainsKey(source.publicLevelId);
                }
            }

            CopyCommunityRecord(source, cachedRecord);
            // 分页刷新或旧请求响应可能晚于用户的最新点击。点赞任务未收敛前，
            // 只合并关卡的其他字段，点赞状态始终以本地最新目标为准。
            if (shouldPreserveLikeState)
            {
                cachedRecord.likedByCurrentPlayer = localLiked;
                cachedRecord.likeCount = localLikeCount;
            }
        }

        return cachedRecord;
    }

    private static void CopyCommunityRecord(
        MPCustomLevelPublicRecord source,
        MPCustomLevelPublicRecord target)
    {
        target.schemaVersion = source.schemaVersion;
        target.publicLevelId = source.publicLevelId;
        target.sourceLocalLevelId = source.sourceLocalLevelId;
        target.ownerPlayerId = source.ownerPlayerId;
        target.ownerDisplayName = source.ownerDisplayName;
        target.title = source.title;
        target.size = source.size;
        target.block = source.block;
        target.colors = source.colors;
        target.likeCount = source.likeCount;
        target.playCount = source.playCount;
        target.status = source.status;
        target.likedByCurrentPlayer = source.likedByCurrentPlayer;
        target.likedPlayerIds = source.likedPlayerIds;
        target.createdAtUtcTicks = source.createdAtUtcTicks;
        target.updatedAtUtcTicks = source.updatedAtUtcTicks;
        target.clientVersion = source.clientVersion;
        target.unityEnvironment = source.unityEnvironment;
    }

    private void SynchronizeLikedPageCache(MPCustomLevelPublicRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.publicLevelId))
            return;

        long now = DateTime.UtcNow.Ticks;
        foreach (CommunityPageCacheEntry page in m_communityPageCache.Values)
        {
            if (!string.Equals(
                    page.sortType,
                    MPCustomLevelPublishConstants.SORT_LIKED,
                    StringComparison.OrdinalIgnoreCase) ||
                page.result?.items == null)
            {
                continue;
            }

            int recordIndex = page.result.items.FindIndex(item =>
                item != null && item.publicLevelId == record.publicLevelId);
            if (!record.likedByCurrentPlayer)
            {
                if (recordIndex >= 0)
                    page.result.items.RemoveAt(recordIndex);
            }
            else if (recordIndex < 0 && string.IsNullOrEmpty(page.cursor))
            {
                page.result.items.Insert(0, record);
            }

            page.cachedAtUtcTicks = now;
        }
    }

    private void EnsureCommunityCachePlayer()
    {
        string playerId = ResolvePlayerId();
        if (m_communityCachePlayerId == playerId)
            return;

        m_communityCachePlayerId = playerId;
        m_communityRecordCache.Clear();
        m_communityPageCache.Clear();
    }

    private void InvalidateCommunityPageCache()
    {
        EnsureCommunityCachePlayer();
        m_communityPageCache.Clear();
    }

    private void RemoveExpiredCommunityPages()
    {
        if (m_communityPageCache.Count == 0)
            return;

        long expireBefore = DateTime.UtcNow.Ticks - COMMUNITY_CACHE_LIFETIME_TICKS;
        List<string> expiredKeys = null;
        foreach (KeyValuePair<string, CommunityPageCacheEntry> pair in m_communityPageCache)
        {
            if (pair.Value.cachedAtUtcTicks >= expireBefore)
                continue;

            expiredKeys ??= new List<string>();
            expiredKeys.Add(pair.Key);
        }

        if (expiredKeys == null)
            return;

        for (int i = 0; i < expiredKeys.Count; i++)
            m_communityPageCache.Remove(expiredKeys[i]);
    }

    private static string BuildCommunityPageCacheKey(string sortType, int pageSize, string cursor)
    {
        return $"{sortType ?? string.Empty}\n{pageSize}\n{cursor ?? string.Empty}";
    }

    private static MPCustomLevelListResult CloneListResult(MPCustomLevelListResult source)
    {
        return new MPCustomLevelListResult
        {
            success = source.success,
            items = source.items == null
                ? new List<MPCustomLevelPublicRecord>()
                : new List<MPCustomLevelPublicRecord>(source.items),
            nextCursor = source.nextCursor ?? string.Empty,
            message = source.message ?? string.Empty,
        };
    }

    /// <summary>
    /// 作者撤销公开自定义关卡。
    /// </summary>
    public async Task<MPCustomLevelRevokeResult> RevokeAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        EnsurePublicLevelId(publicLevelId);
        cancellationToken.ThrowIfCancellationRequested();

        // 撤销属于服务端状态变更，请求发出后必须接收结果并同步本地缓存，不能被页面关闭打断。
        MPCustomLevelRevokeResult result = await m_publishApi.RevokeAsync(publicLevelId, CancellationToken.None);
        if (result != null && result.success)
        {
            MarkLocalStateRevoked(publicLevelId, string.Empty);
            InvalidateCommunityPageCache();
        }

        return result;
    }

    /// <summary>
    /// 撤销某个本地自定义关卡已经发布的公开版本。
    /// </summary>
    public Task<MPCustomLevelRevokeResult> RevokeLocalLevelAsync(MPCustomLevelInfo levelInfo, CancellationToken cancellationToken = default)
    {
        if (levelInfo == null)
        {
            throw new ArgumentNullException(nameof(levelInfo));
        }

        MPCustomLevelPublishLocalState state = GetLocalState(levelInfo.ID);
        if (state == null || string.IsNullOrEmpty(state.publicLevelId))
        {
            throw new InvalidOperationException("该本地关卡没有可撤销的公开发布记录。");
        }

        return RevokeAsync(state.publicLevelId, cancellationToken);
    }

    /// <summary>
    /// 获取某个本地自定义关卡的发布状态缓存。
    /// </summary>
    public MPCustomLevelPublishLocalState GetLocalState(string sourceLocalLevelId)
    {
        if (string.IsNullOrEmpty(sourceLocalLevelId))
        {
            return null;
        }

        EnsureLocalStateLoaded();
        if (m_localState == null || m_localState.items == null)
        {
            return null;
        }

        return m_localState.items.Find(item => item != null && item.sourceLocalLevelId == sourceLocalLevelId);
    }

    /// <summary>
    /// 判断本地自定义关卡是否处于已发布状态。
    /// </summary>
    public bool IsLocalLevelPublished(string sourceLocalLevelId)
    {
        MPCustomLevelPublishLocalState state = GetLocalState(sourceLocalLevelId);
        return state != null && state.IsPublished && !string.IsNullOrEmpty(state.publicLevelId);
    }

    /// <summary>
    /// 将公开关卡记录转换为游戏页面可使用的自定义关卡数据。
    /// </summary>
    public MPCustomLevelInfo ToLocalPlayableLevel(MPCustomLevelPublicRecord record)
    {
        if (record == null)
        {
            return null;
        }

        return NormalizeLevel(record.ToCustomLevelInfo());
    }

    /// <summary>
    /// 打开公开关卡的游戏体验页面。
    /// </summary>
    public void OpenPublicLevelGame(MPCustomLevelPublicRecord record, HQ.UIManager.AWindow sourceWindow = null)
    {
        MPCustomLevelInfo levelInfo = ToLocalPlayableLevel(record);
        if (levelInfo == null)
        {
            Debug.LogWarning("[MPCustomLevelPublish] 公开关卡数据不合法，无法打开游戏页面。");
            return;
        }

        MPGameViewUIMsgData data = new MPGameViewUIMsgData
        {
            customLevelInfo = levelInfo,
            isCustomLevel = true,
            index = -1,
            refresh = null
        };
        MPNewGamePop.EnterCustomLevel(data, sourceWindow);
    }

    /// <summary>
    /// 将云服务异常整理成适合 Unity Console 查看的一行日志，避免 Cloud Code 只显示 ScriptError 时缺少定位信息。
    /// </summary>
    public static string FormatExceptionForLog(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown exception";
        }

        StringBuilder builder = new StringBuilder();
        AppendExceptionForLog(builder, exception, 0);
        return builder.ToString();
    }

    /// <summary>
    /// 写入或更新本地发布状态。
    /// </summary>
    private void UpsertLocalState(
        string sourceLocalLevelId,
        string publicLevelId,
        int status,
        string lastError,
        int cachedLikeCount,
        int cachedPlayCount)
    {
        EnsureLocalStateLoaded();
        if (m_localState.items == null)
        {
            m_localState.items = new List<MPCustomLevelPublishLocalState>();
        }

        MPCustomLevelPublishLocalState state = m_localState.items.Find(item => item != null && item.sourceLocalLevelId == sourceLocalLevelId);
        if (state == null)
        {
            state = new MPCustomLevelPublishLocalState
            {
                sourceLocalLevelId = sourceLocalLevelId
            };
            m_localState.items.Add(state);
        }

        state.publicLevelId = publicLevelId;
        state.status = status;
        state.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        state.cachedLikeCount = Mathf.Max(0, cachedLikeCount);
        state.likeCountSyncedAtUtcTicks = DateTime.UtcNow.Ticks;
        state.cachedPlayCount = Mathf.Max(0, cachedPlayCount);
        state.playCountSyncedAtUtcTicks = DateTime.UtcNow.Ticks;
        state.lastError = lastError;
        SaveLocalState();
        PublishStateChanged?.Invoke(state);
    }

    /// <summary>
    /// 点赞接口确认或回滚后同步作者本地关卡缓存，不触发自定义关卡 Item 刷新。
    /// </summary>
    private void CacheConfirmedLocalLevelLikeCount(string publicLevelId, int likeCount)
    {
        if (string.IsNullOrEmpty(publicLevelId))
            return;

        EnsureLocalStateLoaded();
        MPCustomLevelPublishLocalState state = m_localState?.items?.Find(item =>
            item != null && item.IsPublished && item.publicLevelId == publicLevelId);
        if (state == null)
            return;

        state.cachedLikeCount = Mathf.Max(0, likeCount);
        state.likeCountSyncedAtUtcTicks = DateTime.UtcNow.Ticks;
        try
        {
            SaveLocalState();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[MPCustomLevelPublishManager] 保存点赞确认缓存失败：{FormatExceptionForLog(exception)}");
        }
    }

    /// <summary>
    /// 根据公开关卡ID把本地缓存标记为已撤销。
    /// </summary>
    private void MarkLocalStateRevoked(string publicLevelId, string lastError)
    {
        EnsureLocalStateLoaded();
        if (m_localState == null || m_localState.items == null)
        {
            return;
        }

        MPCustomLevelPublishLocalState state = m_localState.items.Find(item => item != null && item.publicLevelId == publicLevelId);
        if (state == null)
        {
            return;
        }

        state.status = (int)MPCustomLevelPublishStatus.Revoked;
        state.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        state.cachedLikeCount = 0;
        state.likeCountSyncedAtUtcTicks = 0;
        state.cachedPlayCount = 0;
        state.playCountSyncedAtUtcTicks = 0;
        state.lastError = lastError;
        SaveLocalState();
        PublishStateChanged?.Invoke(state);
    }

    /// <summary>
    /// 确保当前玩家的本地发布状态缓存已经加载。
    /// </summary>
    private void EnsureLocalStateLoaded()
    {
        string playerId = ResolvePlayerId();
        if (m_localState != null && m_loadedPlayerId == playerId)
        {
            return;
        }

        m_loadedPlayerId = playerId;
        string json = ES3.Load<string>(GetLocalStateKey(playerId), defaultValue: null);
        if (string.IsNullOrEmpty(json))
        {
            m_localState = CreateEmptyLocalState(playerId);
            return;
        }

        try
        {
            m_localState = JsonConvert.DeserializeObject<MPCustomLevelPublishLocalStateCollection>(json) ?? CreateEmptyLocalState(playerId);
            m_localState.playerId = playerId;
            if (m_localState.items == null)
            {
                m_localState.items = new List<MPCustomLevelPublishLocalState>();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPCustomLevelPublish] 本地发布状态缓存解析失败，将重建缓存：{exception.Message}");
            m_localState = CreateEmptyLocalState(playerId);
        }
    }

    /// <summary>
    /// 保存当前玩家本地发布状态缓存。
    /// </summary>
    private void SaveLocalState()
    {
        if (m_localState == null)
        {
            return;
        }

        ES3.Save(GetLocalStateKey(m_localState.playerId), JsonConvert.SerializeObject(m_localState));
    }

    /// <summary>
    /// 创建空的本地状态缓存。
    /// </summary>
    private static MPCustomLevelPublishLocalStateCollection CreateEmptyLocalState(string playerId)
    {
        return new MPCustomLevelPublishLocalStateCollection
        {
            playerId = playerId,
            items = new List<MPCustomLevelPublishLocalState>()
        };
    }

    /// <summary>
    /// 获取本地状态缓存 Key。
    /// </summary>
    private static string GetLocalStateKey(string playerId)
    {
        return LOCAL_STATE_KEY_PREFIX + (string.IsNullOrEmpty(playerId) ? "Unknown" : playerId);
    }

    /// <summary>
    /// 获取当前登录玩家ID。
    /// </summary>
    private static string ResolvePlayerId()
    {
        return MPLoginManager.Instance == null ? string.Empty : MPLoginManager.Instance.PlayerId;
    }

    /// <summary>
    /// 校验当前必须已经登录 Unity Authentication。
    /// </summary>
    private static void EnsureLoggedIn()
    {
        if (MPLoginManager.Instance == null || !MPLoginManager.Instance.IsLoggedIn || string.IsNullOrEmpty(MPLoginManager.Instance.PlayerId))
        {
            throw new InvalidOperationException("请先登录后再使用公开关卡云发布功能。");
        }
    }

    /// <summary>
    /// 校验公开关卡ID。
    /// </summary>
    private static void EnsurePublicLevelId(string publicLevelId)
    {
        if (string.IsNullOrEmpty(publicLevelId))
        {
            throw new ArgumentException("公开关卡ID不能为空。", nameof(publicLevelId));
        }
    }

    /// <summary>
    /// 清洗关卡数据，保证上传和游玩时不会携带重复或越界索引。
    /// </summary>
    private static MPCustomLevelInfo NormalizeLevel(MPCustomLevelInfo levelInfo)
    {
        if (levelInfo == null || string.IsNullOrEmpty(levelInfo.ID))
        {
            return null;
        }

        int size = Mathf.Clamp(levelInfo.Size, 1, 100);
        int cellCount = size * size;
        List<int> blocks = NormalizeBlockIndexes(levelInfo.Block, cellCount);
        List<MPCustomLevelColorInfo> colors = NormalizeColors(levelInfo.Colors, cellCount);
        string title = string.IsNullOrEmpty(levelInfo.Title) ? "Undefined" : levelInfo.Title;
        return new MPCustomLevelInfo(
            levelInfo.ID,
            title,
            size,
            blocks,
            colors,
            levelInfo.UpdatedAtUtcTicks);
    }

    /// <summary>
    /// 清洗填充格索引。
    /// </summary>
    private static List<int> NormalizeBlockIndexes(List<int> source, int cellCount)
    {
        List<int> result = new List<int>();
        if (source == null)
        {
            return result;
        }

        HashSet<int> indexes = new HashSet<int>();
        for (int i = 0; i < source.Count; i++)
        {
            int index = source[i];
            if (index < 0 || index >= cellCount || indexes.Contains(index))
            {
                continue;
            }

            indexes.Add(index);
            result.Add(index);
        }

        result.Sort();
        return result;
    }

    /// <summary>
    /// 清洗颜色格索引，同一个 index 重复时保留最后一次颜色。
    /// </summary>
    private static List<MPCustomLevelColorInfo> NormalizeColors(List<MPCustomLevelColorInfo> source, int cellCount)
    {
        List<MPCustomLevelColorInfo> result = new List<MPCustomLevelColorInfo>();
        if (source == null)
        {
            return result;
        }

        Dictionary<int, string> colorByIndex = new Dictionary<int, string>();
        for (int i = 0; i < source.Count; i++)
        {
            MPCustomLevelColorInfo colorInfo = source[i];
            if (colorInfo == null || colorInfo.Index < 0 || colorInfo.Index >= cellCount || string.IsNullOrEmpty(colorInfo.Color))
            {
                continue;
            }

            colorByIndex[colorInfo.Index] = colorInfo.Color;
        }

        List<int> indexes = new List<int>(colorByIndex.Keys);
        indexes.Sort();
        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            result.Add(new MPCustomLevelColorInfo(index, colorByIndex[index]));
        }

        return result;
    }

    /// <summary>
    /// 递归追加异常摘要，最多展开三层 InnerException，避免日志过长。
    /// </summary>
    private static void AppendExceptionForLog(StringBuilder builder, Exception exception, int depth)
    {
        if (exception == null || depth > 2)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(" | Inner: ");
        }

        if (exception is CloudCodeException cloudCodeException)
        {
            builder.Append("CloudCodeException")
                .Append(" Reason=").Append(cloudCodeException.Reason)
                .Append(", ErrorCode=").Append(cloudCodeException.ErrorCode)
                .Append(", Message=").Append(NormalizeLogText(cloudCodeException.Message));
        }
        else if (exception is RequestFailedException requestFailedException)
        {
            builder.Append(exception.GetType().Name)
                .Append(" ErrorCode=").Append(requestFailedException.ErrorCode)
                .Append(", Message=").Append(NormalizeLogText(requestFailedException.Message));
        }
        else
        {
            builder.Append(exception.GetType().FullName)
                .Append(": ")
                .Append(NormalizeLogText(exception.Message));
        }

        if (exception.InnerException != null)
        {
            AppendExceptionForLog(builder, exception.InnerException, depth + 1);
        }
    }

    /// <summary>
    /// 把多行异常压缩成一行，方便在 Console 中复制和搜索。
    /// </summary>
    private static string NormalizeLogText(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("\r", " ").Replace("\n", " / ");
    }

    private sealed class CommunityPageCacheEntry
    {
        public string sortType;
        public int pageSize;
        public string cursor;
        public long cachedAtUtcTicks;
        public MPCustomLevelListResult result;
    }

    /// <summary>
    /// 同一关卡只保留一个同步任务。任务执行期间可以反复修改 desiredLiked，
    /// 每个服务端响应只更新 confirmed 基线，最终以最后一次点击状态结束。
    /// </summary>
    private sealed class CommunityLikeOperation
    {
        public MPCustomLevelPublicRecord record;
        public bool desiredLiked;
        public bool confirmedLiked;
        public int confirmedLikeCount;
        public Task<MPCustomLevelLikeResult> operationTask;
    }

    private sealed class LocalLevelStatsSyncTarget
    {
        public string sourceLocalLevelId;
        public string publicLevelId;
        public long likeCacheVersionUtcTicks;
        public long playCacheVersionUtcTicks;
    }
}

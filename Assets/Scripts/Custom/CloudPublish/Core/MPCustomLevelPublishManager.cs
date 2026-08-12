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
        MPCustomLevelPublishResult result;
        try
        {
            result = await m_publishApi.PublishAsync(normalizedLevel, CancellationToken.None);
        }
        finally
        {
            lock (m_publishOperationsLock)
            {
                m_publishOperations.Remove(normalizedLevel.ID);
            }

            PublishOperationChanged?.Invoke(normalizedLevel.ID);
        }

        if (result != null && result.success && !string.IsNullOrEmpty(result.publicLevelId))
        {
            UpsertLocalState(
                normalizedLevel.ID,
                result.publicLevelId,
                result.status,
                string.Empty);
        }

        return result;
    }

    /// <summary>
    /// 获取公开自定义关卡列表。
    /// </summary>
    public Task<MPCustomLevelListResult> GetListAsync(string sortType, int pageSize, string cursor, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        return m_publishApi.GetListAsync(sortType, pageSize, cursor, cancellationToken);
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
    /// 记录一次体验并获取公开自定义关卡详情。
    /// </summary>
    public Task<MPCustomLevelPublicRecord> PlayAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        EnsurePublicLevelId(publicLevelId);
        return m_publishApi.PlayAsync(publicLevelId, cancellationToken);
    }

    /// <summary>
    /// 点赞公开自定义关卡。
    /// </summary>
    public Task<MPCustomLevelLikeResult> LikeAsync(string publicLevelId, CancellationToken cancellationToken = default)
    {
        EnsureLoggedIn();
        EnsurePublicLevelId(publicLevelId);
        return m_publishApi.LikeAsync(publicLevelId, cancellationToken);
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
        MPTransitionView.OpenWindow<MPGameView>(data, sourceWindow);
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
    private void UpsertLocalState(string sourceLocalLevelId, string publicLevelId, int status, string lastError)
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
        state.lastError = lastError;
        SaveLocalState();
        PublishStateChanged?.Invoke(state);
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
        return new MPCustomLevelInfo(levelInfo.ID, title, size, blocks, colors);
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
}

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace MagicPixelCustomLevelPublish;

/// <summary>
/// MagicPixel 自定义关卡公开发布 Cloud Code C# Module。
/// 所有会影响公共数据的操作都在服务端完成，客户端只负责发起请求和展示结果。
/// </summary>
public class CustomLevelPublishModule
{
    private const int SchemaVersion = 1;
    private const int StatusPublished = 0;
    private const int StatusRevoked = 1;
    private const string PublicCustomId = "mp_public_custom_levels";
    private const string CatalogKey = "mp_public_custom_level_catalog_v1";
    private const string RecordKeyPrefix = "mp_public_custom_level_";
    private const int MaxPageSize = 20;
    private const int RetryCount = 3;

    private static readonly Regex ColorRegex = new("^#[0-9a-fA-F]{8}$", RegexOptions.Compiled);
    private readonly ILogger<CustomLevelPublishModule> m_Logger;

    public CustomLevelPublishModule(ILogger<CustomLevelPublishModule> logger)
    {
        m_Logger = logger;
    }

    /// <summary>
    /// 发布当前玩家本地自定义关卡。
    /// </summary>
    [CloudCodeFunction("PublishCustomLevel")]
    public async Task<CustomLevelPublishResult> PublishCustomLevel(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        CustomLevelUploadRequest levelInfo,
        string? displayName = null,
        string? clientVersion = null)
    {
        try
        {
            EnsureSignedIn(context);

            var normalizedLevel = NormalizeLevel(levelInfo);
            var nowTicks = DateTime.UtcNow.Ticks;
            var record = new CustomLevelPublicRecord
            {
                schemaVersion = SchemaVersion,
                publicLevelId = CreatePublicLevelId(),
                sourceLocalLevelId = normalizedLevel.sourceLocalLevelId ?? string.Empty,
                ownerPlayerId = context.PlayerId!,
                ownerDisplayName = NormalizeDisplayName(displayName, context.PlayerId!),
                title = normalizedLevel.title ?? "Undefined",
                size = normalizedLevel.size,
                block = normalizedLevel.block ?? new List<int>(),
                colors = normalizedLevel.colors ?? new List<CustomLevelColorInfo>(),
                likeCount = 0,
                playCount = 0,
                status = StatusPublished,
                likedByCurrentPlayer = false,
                likedPlayerIds = new List<string>(),
                createdAtUtcTicks = nowTicks,
                updatedAtUtcTicks = nowTicks,
                clientVersion = clientVersion ?? string.Empty,
                unityEnvironment = context.EnvironmentName ?? string.Empty
            };

            await SetRecordAsync(context, gameApiClient, record, null);
            await AppendCatalogAsync(context, gameApiClient, record.publicLevelId);

            m_Logger.LogInformation(
                "Published custom level. PublicLevelId={PublicLevelId}, Owner={Owner}",
                record.publicLevelId,
                context.PlayerId);

            return new CustomLevelPublishResult
            {
                success = true,
                publicLevelId = record.publicLevelId,
                status = record.status,
                message = "Published",
                record = ToClientRecord(record, context.PlayerId)
            };
        }
        catch (Exception exception)
        {
            LogFunctionException(context, nameof(PublishCustomLevel), exception);
            return new CustomLevelPublishResult
            {
                success = false,
                publicLevelId = string.Empty,
                status = StatusRevoked,
                message = BuildPublicFailureMessage(exception),
                record = null
            };
        }
    }

    /// <summary>
    /// 获取公开自定义关卡列表。
    /// 开发期先通过目录 Key 分页，后续量大后可切换 Cloud Save Query 索引。
    /// </summary>
    [CloudCodeFunction("GetPublishedCustomLevels")]
    public async Task<CustomLevelListResult> GetPublishedCustomLevels(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string? sortType = null,
        int pageSize = MaxPageSize,
        string? cursor = null)
    {
        EnsureSignedIn(context);

        var catalog = await GetCatalogAsync(context, gameApiClient);
        var records = new List<CustomLevelPublicRecord>();
        foreach (var publicLevelId in catalog.publicLevelIds)
        {
            var record = await GetRecordAsync(context, gameApiClient, publicLevelId);
            if (record?.status == StatusPublished)
            {
                records.Add(ToClientRecord(record, context.PlayerId));
            }
        }

        records = SortRecords(records, sortType);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var offset = ParseCursor(cursor);
        var items = records.Skip(offset).Take(pageSize).ToList();
        var nextOffset = offset + items.Count;

        return new CustomLevelListResult
        {
            success = true,
            items = items,
            nextCursor = nextOffset < records.Count ? nextOffset.ToString() : string.Empty,
            message = string.Empty
        };
    }

    /// <summary>
    /// 获取单个公开自定义关卡详情。
    /// </summary>
    [CloudCodeFunction("GetPublishedCustomLevel")]
    public async Task<CustomLevelPublicRecord> GetPublishedCustomLevel(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string publicLevelId)
    {
        EnsureSignedIn(context);

        var record = await GetRecordAsync(context, gameApiClient, publicLevelId);
        if (record == null || record.status != StatusPublished)
        {
            throw new Exception("LEVEL_NOT_FOUND_OR_NOT_PUBLISHED");
        }

        return ToClientRecord(record, context.PlayerId);
    }

    /// <summary>
    /// 记录一次公开关卡体验并返回详情。
    /// </summary>
    [CloudCodeFunction("PlayPublishedCustomLevel")]
    public async Task<CustomLevelPublicRecord> PlayPublishedCustomLevel(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string publicLevelId)
    {
        EnsureSignedIn(context);

        return await UpdateRecordWithRetryAsync(context, gameApiClient, publicLevelId, record =>
        {
            if (record.status != StatusPublished)
            {
                throw new Exception("LEVEL_NOT_FOUND_OR_NOT_PUBLISHED");
            }

            record.playCount = Math.Max(0, record.playCount) + 1;
            record.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            return ToClientRecord(record, context.PlayerId);
        });
    }

    /// <summary>
    /// 点赞公开自定义关卡。
    /// 同一玩家重复点赞不会重复增加 likeCount。
    /// </summary>
    [CloudCodeFunction("LikePublishedCustomLevel")]
    public async Task<CustomLevelLikeResult> LikePublishedCustomLevel(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string publicLevelId)
    {
        EnsureSignedIn(context);

        return await UpdateRecordWithRetryAsync(context, gameApiClient, publicLevelId, record =>
        {
            if (record.status != StatusPublished)
            {
                throw new Exception("LEVEL_NOT_FOUND_OR_NOT_PUBLISHED");
            }

            record.likedPlayerIds ??= new List<string>();
            if (!record.likedPlayerIds.Contains(context.PlayerId!))
            {
                record.likedPlayerIds.Add(context.PlayerId!);
                record.likeCount = record.likedPlayerIds.Count;
                record.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            return new CustomLevelLikeResult
            {
                success = true,
                liked = true,
                likeCount = record.likeCount,
                message = "Liked",
                record = ToClientRecord(record, context.PlayerId)
            };
        });
    }

    /// <summary>
    /// 作者撤销自己发布的公开自定义关卡。
    /// </summary>
    [CloudCodeFunction("RevokePublishedCustomLevel")]
    public async Task<CustomLevelRevokeResult> RevokePublishedCustomLevel(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string publicLevelId)
    {
        EnsureSignedIn(context);

        var existingRecord = await GetRecordAsync(context, gameApiClient, publicLevelId);
        if (existingRecord == null)
        {
            // 支持撤销请求重试：记录已经删除时，继续清理目录并按成功返回。
            await RemoveCatalogAsync(context, gameApiClient, publicLevelId);
            return CreateRevokeResult(publicLevelId);
        }

        if (existingRecord.ownerPlayerId != context.PlayerId)
        {
            throw new Exception("ONLY_OWNER_CAN_REVOKE");
        }

        var result = await UpdateRecordWithRetryAsync(context, gameApiClient, publicLevelId, record =>
        {
            if (record.ownerPlayerId != context.PlayerId)
            {
                throw new Exception("ONLY_OWNER_CAN_REVOKE");
            }

            // 先标记撤销，确保后续目录或删除操作短暂失败时，该关卡也不会继续公开展示。
            record.status = StatusRevoked;
            record.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
            return CreateRevokeResult(record.publicLevelId);
        });

        await RemoveCatalogAsync(context, gameApiClient, publicLevelId);
        await DeleteRecordAsync(context, gameApiClient, publicLevelId);
        return result;
    }

    /// <summary>
    /// 使用写锁更新记录，遇到冲突时重新读取并重试。
    /// </summary>
    private async Task<T> UpdateRecordWithRetryAsync<T>(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string publicLevelId,
        Func<CustomLevelPublicRecord, T> update)
    {
        for (var i = 0; i < RetryCount; i++)
        {
            var item = await GetPrivateCustomItemAsync(context, gameApiClient, RecordKey(publicLevelId));
            var record = DeserializeItemValue<CustomLevelPublicRecord>(item?.Value);
            if (record == null)
            {
                throw new Exception("LEVEL_NOT_FOUND");
            }

            var result = update(record);
            try
            {
                await SetRecordAsync(context, gameApiClient, record, item?.WriteLock);
                return result;
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict && i < RetryCount - 1)
            {
                m_Logger.LogWarning("Cloud Save write conflict, retrying. PublicLevelId={PublicLevelId}", publicLevelId);
            }
        }

        throw new Exception("WRITE_CONFLICT_RETRY_EXHAUSTED");
    }

    /// <summary>
    /// 将关卡ID加入公开目录。
    /// </summary>
    private async Task AppendCatalogAsync(IExecutionContext context, IGameApiClient gameApiClient, string publicLevelId)
    {
        for (var i = 0; i < RetryCount; i++)
        {
            var item = await GetPrivateCustomItemAsync(context, gameApiClient, CatalogKey);
            var catalog = DeserializeCatalog(item?.Value);
            if (!catalog.publicLevelIds.Contains(publicLevelId))
            {
                catalog.publicLevelIds.Insert(0, publicLevelId);
            }

            try
            {
                await SetPrivateCustomItemAsync(context, gameApiClient, CatalogKey, catalog, item?.WriteLock);
                return;
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict && i < RetryCount - 1)
            {
                m_Logger.LogWarning("Catalog write conflict, retrying.");
            }
        }

        throw new Exception("CATALOG_WRITE_CONFLICT_RETRY_EXHAUSTED");
    }

    /// <summary>
    /// 将关卡ID从公开目录移除。
    /// </summary>
    private async Task RemoveCatalogAsync(IExecutionContext context, IGameApiClient gameApiClient, string publicLevelId)
    {
        for (var i = 0; i < RetryCount; i++)
        {
            var item = await GetPrivateCustomItemAsync(context, gameApiClient, CatalogKey);
            if (item == null)
            {
                return;
            }

            var catalog = DeserializeCatalog(item.Value);
            var removedCount = catalog.publicLevelIds.RemoveAll(id =>
                string.Equals(id, publicLevelId, StringComparison.Ordinal));
            if (removedCount == 0)
            {
                return;
            }

            try
            {
                await SetPrivateCustomItemAsync(context, gameApiClient, CatalogKey, catalog, item.WriteLock);
                return;
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict && i < RetryCount - 1)
            {
                m_Logger.LogWarning("Catalog remove conflict, retrying. PublicLevelId={PublicLevelId}", publicLevelId);
            }
        }

        throw new Exception("CATALOG_REMOVE_CONFLICT_RETRY_EXHAUSTED");
    }

    /// <summary>
    /// 获取公开目录。
    /// </summary>
    private async Task<CustomLevelCatalog> GetCatalogAsync(IExecutionContext context, IGameApiClient gameApiClient)
    {
        var item = await GetPrivateCustomItemAsync(context, gameApiClient, CatalogKey);
        return DeserializeCatalog(item?.Value);
    }

    /// <summary>
    /// 获取公开关卡记录。
    /// </summary>
    private async Task<CustomLevelPublicRecord?> GetRecordAsync(IExecutionContext context, IGameApiClient gameApiClient, string publicLevelId)
    {
        var item = await GetPrivateCustomItemAsync(context, gameApiClient, RecordKey(publicLevelId));
        return DeserializeItemValue<CustomLevelPublicRecord>(item?.Value);
    }

    /// <summary>
    /// 保存公开关卡记录。
    /// </summary>
    private Task SetRecordAsync(IExecutionContext context, IGameApiClient gameApiClient, CustomLevelPublicRecord record, string? writeLock)
    {
        return SetPrivateCustomItemAsync(context, gameApiClient, RecordKey(record.publicLevelId), record, writeLock);
    }

    /// <summary>
    /// 删除已经撤销的公开关卡记录，重复调用时保持幂等。
    /// </summary>
    private async Task DeleteRecordAsync(IExecutionContext context, IGameApiClient gameApiClient, string publicLevelId)
    {
        var key = RecordKey(publicLevelId);
        for (var i = 0; i < RetryCount; i++)
        {
            var item = await GetPrivateCustomItemAsync(context, gameApiClient, key);
            if (item == null)
            {
                return;
            }

            try
            {
                await gameApiClient.CloudSaveData.DeletePrivateCustomItemAsync(
                    context,
                    context.ServiceToken,
                    context.ProjectId!,
                    PublicCustomId,
                    key,
                    item.WriteLock,
                    CancellationToken.None);
                return;
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict && i < RetryCount - 1)
            {
                m_Logger.LogWarning("Record delete conflict, retrying. PublicLevelId={PublicLevelId}", publicLevelId);
            }
            catch (ApiException exception)
            {
                LogCloudSaveApiException(context, "DeletePrivateCustomItem", key, exception);
                throw;
            }
        }

        throw new Exception("RECORD_DELETE_CONFLICT_RETRY_EXHAUSTED");
    }

    /// <summary>
    /// 从 Cloud Save Private Custom Data 获取单个 Key。
    /// </summary>
    private async Task<Item?> GetPrivateCustomItemAsync(IExecutionContext context, IGameApiClient gameApiClient, string key)
    {
        try
        {
            var result = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(
                context,
                context.ServiceToken,
                context.ProjectId!,
                PublicCustomId,
                new List<string> { key });

            return result.Data.Results.FirstOrDefault(item => item.Key == key);
        }
        catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (ApiException exception)
        {
            LogCloudSaveApiException(context, "GetPrivateCustomItem", key, exception);
            throw;
        }
    }

    /// <summary>
    /// 保存单个 Cloud Save Private Custom Data Key。
    /// </summary>
    private async Task SetPrivateCustomItemAsync(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string key,
        object value,
        string? writeLock)
    {
        var body = string.IsNullOrEmpty(writeLock)
            ? new SetItemBody(key, value)
            : new SetItemBody(key, value, writeLock);

        try
        {
            await gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(
                context,
                context.ServiceToken,
                context.ProjectId!,
                PublicCustomId,
                body);
        }
        catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.Conflict)
        {
            throw;
        }
        catch (ApiException exception)
        {
            LogCloudSaveApiException(context, "SetPrivateCustomItem", key, exception);
            throw;
        }
    }

    /// <summary>
    /// 记录 Cloud Code 函数级异常，方便在 Unity Dashboard 的 Cloud Code Logs 中定位 ScriptError 原因。
    /// </summary>
    private void LogFunctionException(IExecutionContext context, string functionName, Exception exception)
    {
        if (exception is ApiException apiException)
        {
            m_Logger.LogError(
                exception,
                "Cloud Code function failed. Function={Function}, PlayerId={PlayerId}, Environment={Environment}, ApiStatusCode={ApiStatusCode}",
                functionName,
                context.PlayerId,
                context.EnvironmentName,
                apiException.Response.StatusCode);
            return;
        }

        m_Logger.LogError(
            exception,
            "Cloud Code function failed. Function={Function}, PlayerId={PlayerId}, Environment={Environment}",
            functionName,
            context.PlayerId,
            context.EnvironmentName);
    }

    /// <summary>
    /// 记录 Cloud Save API 失败细节，重点输出 CustomId、Key 和 HTTP 状态码。
    /// </summary>
    private void LogCloudSaveApiException(IExecutionContext context, string operation, string key, ApiException exception)
    {
        m_Logger.LogError(
            exception,
            "Cloud Save Private Custom Data operation failed. Operation={Operation}, StatusCode={StatusCode}, CustomId={CustomId}, Key={Key}, ProjectId={ProjectId}, PlayerId={PlayerId}, Environment={Environment}",
            operation,
            exception.Response.StatusCode,
            PublicCustomId,
            key,
            context.ProjectId,
            context.PlayerId,
            context.EnvironmentName);
    }

    /// <summary>
    /// 生成可以安全返回给客户端的失败消息，不暴露服务端堆栈。
    /// </summary>
    private static string BuildPublicFailureMessage(Exception exception)
    {
        if (exception is ApiException apiException)
        {
            return $"CLOUD_SAVE_API_ERROR_{(int)apiException.Response.StatusCode}";
        }

        if (string.IsNullOrWhiteSpace(exception.Message))
        {
            return "PUBLISH_FAILED";
        }

        var message = exception.Message.Replace("\r", " ").Replace("\n", " ").Trim();
        return message.Length > 96 ? message[..96] : message;
    }

    /// <summary>
    /// 把 Cloud Save 返回的 object/JToken/JsonElement 转回目标 DTO。
    /// Cloud Code APIs 使用 Newtonsoft.Json，不能直接用 System.Text.Json 再序列化 JToken，
    /// 否则 JValue 会被序列化为对象并导致 List&lt;string&gt; 读取失败。
    /// </summary>
    private static T? DeserializeItemValue<T>(object? value)
    {
        if (value == null)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        if (value is JToken token)
        {
            return token.ToObject<T>();
        }

        if (value is JsonElement element)
        {
            return JsonConvert.DeserializeObject<T>(element.GetRawText());
        }

        if (value is string json)
        {
            return string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json);
        }

        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    }

    private static CustomLevelCatalog DeserializeCatalog(object? value)
    {
        var catalog = DeserializeItemValue<CustomLevelCatalog>(value) ?? new CustomLevelCatalog();
        catalog.publicLevelIds = (catalog.publicLevelIds ?? new List<string>())
            .Where(publicLevelId => !string.IsNullOrWhiteSpace(publicLevelId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return catalog;
    }

    private static CustomLevelRevokeResult CreateRevokeResult(string publicLevelId)
    {
        return new CustomLevelRevokeResult
        {
            success = true,
            publicLevelId = publicLevelId,
            status = StatusRevoked,
            message = "Revoked"
        };
    }

    /// <summary>
    /// 清洗上传关卡数据。
    /// </summary>
    private static CustomLevelUploadRequest NormalizeLevel(CustomLevelUploadRequest? input)
    {
        if (input == null)
        {
            throw new Exception("LEVEL_EMPTY");
        }

        if (input.size != 5 && input.size != 10)
        {
            throw new Exception("INVALID_LEVEL_SIZE");
        }

        var cellCount = input.size * input.size;
        return new CustomLevelUploadRequest
        {
            sourceLocalLevelId = NormalizeKeyPart(input.sourceLocalLevelId ?? "local_level", 24),
            title = NormalizeTitle(input.title),
            size = input.size,
            block = NormalizeBlockIndexes(input.block, cellCount),
            colors = NormalizeColors(input.colors, cellCount)
        };
    }

    private static List<int> NormalizeBlockIndexes(List<int>? source, int cellCount)
    {
        var result = new HashSet<int>();
        foreach (var index in source ?? new List<int>())
        {
            if (index >= 0 && index < cellCount)
            {
                result.Add(index);
            }
        }

        return result.OrderBy(index => index).ToList();
    }

    private static List<CustomLevelColorInfo> NormalizeColors(List<CustomLevelColorInfo>? source, int cellCount)
    {
        var colorByIndex = new Dictionary<int, string>();
        foreach (var colorInfo in source ?? new List<CustomLevelColorInfo>())
        {
            if (colorInfo.index < 0 || colorInfo.index >= cellCount || string.IsNullOrEmpty(colorInfo.color))
            {
                continue;
            }

            if (!ColorRegex.IsMatch(colorInfo.color))
            {
                continue;
            }

            colorByIndex[colorInfo.index] = colorInfo.color;
        }

        return colorByIndex
            .OrderBy(pair => pair.Key)
            .Select(pair => new CustomLevelColorInfo { index = pair.Key, color = pair.Value })
            .ToList();
    }

    private static List<CustomLevelPublicRecord> SortRecords(List<CustomLevelPublicRecord> records, string? sortType)
    {
        if (sortType == "Popular")
        {
            return records
                .OrderByDescending(record => record.likeCount)
                .ThenByDescending(record => record.createdAtUtcTicks)
                .ToList();
        }

        return records.OrderByDescending(record => record.createdAtUtcTicks).ToList();
    }

    private static CustomLevelPublicRecord ToClientRecord(CustomLevelPublicRecord record, string? currentPlayerId)
    {
        var likedPlayerIds = record.likedPlayerIds ?? new List<string>();
        return new CustomLevelPublicRecord
        {
            schemaVersion = record.schemaVersion,
            publicLevelId = record.publicLevelId,
            sourceLocalLevelId = record.sourceLocalLevelId,
            ownerPlayerId = record.ownerPlayerId,
            ownerDisplayName = record.ownerDisplayName,
            title = record.title,
            size = record.size,
            block = record.block,
            colors = record.colors,
            likeCount = record.likeCount,
            playCount = record.playCount,
            status = record.status,
            likedByCurrentPlayer = !string.IsNullOrEmpty(currentPlayerId) && likedPlayerIds.Contains(currentPlayerId),
            createdAtUtcTicks = record.createdAtUtcTicks,
            updatedAtUtcTicks = record.updatedAtUtcTicks,
            clientVersion = record.clientVersion,
            unityEnvironment = record.unityEnvironment,
            likedPlayerIds = new List<string>()
        };
    }

    private static void EnsureSignedIn(IExecutionContext context)
    {
        if (string.IsNullOrEmpty(context.PlayerId))
        {
            throw new Exception("PLAYER_NOT_SIGNED_IN");
        }
    }

    private static int ParseCursor(string? cursor)
    {
        return int.TryParse(cursor, out var offset) ? Math.Max(0, offset) : 0;
    }

    private static string NormalizeTitle(string? title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? "Undefined" : title.Trim();
        return value.Length > 24 ? value[..24] : value;
    }

    private static string NormalizeDisplayName(string? displayName, string playerId)
    {
        var value = string.IsNullOrWhiteSpace(displayName) ? $"Player_{playerId[..Math.Min(6, playerId.Length)]}" : displayName.Trim();
        return value.Length > 24 ? value[..24] : value;
    }

    private static string CreatePublicLevelId()
    {
        return $"pl_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}_{Guid.NewGuid():N}"[..32];
    }

    private static string RecordKey(string publicLevelId)
    {
        return RecordKeyPrefix + NormalizeKeyPart(publicLevelId, 24);
    }

    private static string NormalizeKeyPart(string value, int maxLength)
    {
        var result = Regex.Replace(value, "[^A-Za-z0-9_-]", "_");
        return result.Length > maxLength ? result[..maxLength] : result;
    }
}

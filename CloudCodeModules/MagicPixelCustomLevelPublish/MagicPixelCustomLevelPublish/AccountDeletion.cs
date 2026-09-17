using System.Net;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace MagicPixelCustomLevelPublish;

public partial class CustomLevelPublishModule
{
    /// <summary>
    /// 只清理调用者自身数据，不接受客户端传入 ownerId。按 Key 分批扫描，包含未进目录的残留记录。
    /// 返回下一批游标，空字符串表示扫描结束；中断后从头重试也是幂等的。
    /// </summary>
    [CloudCodeFunction("DeleteAccountCommunityData")]
    public async Task<string> DeleteAccountCommunityData(
        IExecutionContext context, IGameApiClient gameApiClient, string after = "")
    {
        EnsureSignedIn(context);
        // 先分页扫描统一举报容器，只清理调用者自己的记录，再进入原有公开关卡清理流程。
        // 游标带阶段前缀，中断后可安全从头重试，不接受任意目标玩家 ID。
        const string reportCursorPrefix = "reports:";
        if (string.IsNullOrEmpty(after) || after.StartsWith(reportCursorPrefix, StringComparison.Ordinal))
        {
            string reportAfter = string.IsNullOrEmpty(after) ? "" : after.Substring(reportCursorPrefix.Length);
            var reportKeys = new List<string>();
            try
            {
                var reports = await gameApiClient.CloudSaveData.GetPrivateCustomKeysAsync(
                    context, context.ServiceToken, context.ProjectId!, ReportCustomId,
                    string.IsNullOrEmpty(reportAfter) ? null! : reportAfter);
                reportKeys = reports.Data.Results.Take(10).Select(item => item.Key).ToList();
            }
            catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound) { }
            foreach (string key in reportKeys)
            {
                if (!key.StartsWith("report_", StringComparison.Ordinal)) continue;
                try
                {
                    var response = await gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(
                        context, context.ServiceToken, context.ProjectId!, ReportCustomId, new List<string> { key });
                    var item = response.Data.Results.FirstOrDefault(value => value.Key == key);
                    var report = DeserializeItemValue<CommunityReport>(item?.Value);
                    // 共享容器不能按枚举结果直接删除；使用服务端保存的举报者身份校验归属。
                    if (item == null || report?.reporterPlayerId != context.PlayerId) continue;
                    await gameApiClient.CloudSaveData.DeletePrivateCustomItemAsync(
                        context, context.ServiceToken, context.ProjectId!, ReportCustomId,
                        key, item.WriteLock, CancellationToken.None);
                }
                catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound) { }
            }
            // 即使整批都是其他玩家的举报，也必须继续推进扫描游标。
            if (reportKeys.Count > 0) return reportCursorPrefix + reportKeys[^1];
            after = string.Empty;
        }
        List<string> keys;
        try
        {
            var response = await gameApiClient.CloudSaveData.GetPrivateCustomKeysAsync(
                context, context.ServiceToken, context.ProjectId!, PublicCustomId,
                string.IsNullOrEmpty(after) ? null! : after);
            keys = response.Data.Results.Take(10).Select(item => item.Key).ToList();
        }
        catch (ApiException exception) when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            // 仅忽略枚举时不存在的容器，不能吞掉后续清理失败。
            await DeleteAppleCredentialAsync(context, gameApiClient);
            return string.Empty;
        }
        foreach (var key in keys)
        {
            if (!key.StartsWith(RecordKeyPrefix, StringComparison.Ordinal) || key == CatalogKey)
                continue;
            string id = key.Substring(RecordKeyPrefix.Length);
            var record = await GetRecordAsync(context, gameApiClient, id);
            if (record == null) continue;
            if (record.ownerPlayerId == context.PlayerId)
            {
                await RevokePublishedCustomLevel(context, gameApiClient, id);
            }
            else if (record.likedPlayerIds?.Contains(context.PlayerId!) == true)
            {
                await UpdateRecordWithRetryAsync(context, gameApiClient, id, latest =>
                {
                    latest.likedPlayerIds?.RemoveAll(player => player == context.PlayerId);
                    latest.likeCount = latest.likedPlayerIds?.Count ?? 0;
                    return true;
                });
            }
        }
        if (keys.Count == 0) await DeleteAppleCredentialAsync(context, gameApiClient);
        return keys.Count == 0 ? string.Empty : keys[^1];
    }
}

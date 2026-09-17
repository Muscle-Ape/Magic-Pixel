using System.Security.Cryptography;
using System.Text;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace MagicPixelCustomLevelPublish;

public partial class CustomLevelPublishModule
{
    private const string ReportCustomId = "mp_community_reports";
    private static readonly HashSet<string> ReportReasons = new(StringComparer.Ordinal)
    {
        "dislike", "fraud", "political", "bullying", "plagiarism", "sexual", "other"
    };

    [CloudCodeFunction("ReportCustomLevel")]
    public async Task<bool> ReportCustomLevel(IExecutionContext context, IGameApiClient gameApiClient,
        string publicLevelId, string reason, string details = "")
    {
        EnsureSignedIn(context);
        if (string.IsNullOrWhiteSpace(publicLevelId) || publicLevelId.Length > 128 ||
            string.IsNullOrEmpty(reason) || !ReportReasons.Contains(reason))
            throw new ArgumentException("Invalid report.");
        details = (details ?? string.Empty).Trim();
        if (details.Length > 300 || (reason == "other" && details.Length == 0))
            throw new ArgumentException("Other reports require 1-300 characters.");
        var record = await GetRecordAsync(context, gameApiClient, publicLevelId);
        if (record == null || record.status != StatusPublished)
            throw new ArgumentException("Level is no longer available.");

        // 调用者由认证上下文获取；每人每关固定一个 Key，重试/重复点击不会产生重复记录。
        string hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(context.PlayerId + ":" + publicLevelId))).ToLowerInvariant();
        // 所有玩家共用一个私有举报容器，每人每关仍使用独立 Key，方便后台集中查看。
        await gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(
            context, context.ServiceToken, context.ProjectId!, ReportCustomId,
            new SetItemBody("report_" + hash, new CommunityReport
            {
                reporterPlayerId = context.PlayerId!, publicLevelId = publicLevelId,
                reason = reason, details = reason == "other" ? details : string.Empty,
                updatedAtUtcTicks = DateTime.UtcNow.Ticks
            }));
        return true;
    }

    private sealed class CommunityReport
    {
        public string reporterPlayerId { get; set; } = "";
        public string publicLevelId { get; set; } = "";
        public string reason { get; set; } = "";
        public string details { get; set; } = "";
        public long updatedAtUtcTicks { get; set; }
    }
}

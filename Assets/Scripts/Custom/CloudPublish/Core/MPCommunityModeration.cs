using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

/// <summary>社区屏蔽作为本机偏好持久保存；举报由 Cloud Code 验证并保存。</summary>
public static class MPCommunityModeration
{
    private const string BLOCKED_KEY = "MPCommunity.BlockedLevels.v1.";
    private const string LOCAL_BLOCKED_KEY = "BlockedLevels";
    private const string LOCAL_BLOCKED_FILE = "MPCommunityModeration.es3";
    private static readonly ES3Settings s_storage = new ES3Settings(LOCAL_BLOCKED_FILE, ES3.Location.File);
    private static string s_importedPlayerId;
    private static HashSet<string> s_blocked;
    public static event Action<string> LevelBlocked;
    public static bool IsSubmitting { get; private set; }

    private static void EnsureLoaded()
    {
        if (s_blocked == null)
        {
            var ids = ES3.Load(LOCAL_BLOCKED_KEY, new List<string>(), s_storage);
            s_blocked = new HashSet<string>(ids ?? new List<string>(), StringComparer.Ordinal);
            s_blocked.RemoveWhere(string.IsNullOrWhiteSpace);
            s_importedPlayerId = null;
        }

        // 兼容已保存的当前账号记录；新记录不再依赖登录初始化时机或账号 ID。
        string playerId = MPLoginManager.Instance.PlayerId ?? string.Empty;
        if (s_importedPlayerId == playerId) return;
        var previous = ES3.Load(BLOCKED_KEY + playerId, new List<string>());
        if (previous != null && previous.Count > 0)
        {
            var merged = new HashSet<string>(s_blocked, StringComparer.Ordinal);
            merged.UnionWith(previous);
            merged.RemoveWhere(string.IsNullOrWhiteSpace);
            ES3.Save(LOCAL_BLOCKED_KEY, new List<string>(merged), s_storage);
            s_blocked = merged;
        }
        s_importedPlayerId = playerId;
    }

    public static bool IsBlocked(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && s_blocked.Contains(id);
    }

    public static void Block(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        EnsureLoaded();
        if (s_blocked.Contains(id)) return;
        // 先落盘，失败时不让 UI 误以为已永久屏蔽。
        var next = new HashSet<string>(s_blocked, StringComparer.Ordinal) { id };
        ES3.Save(LOCAL_BLOCKED_KEY, new List<string>(next), s_storage);
        s_blocked = next;
        LevelBlocked?.Invoke(id);
    }

    public static void ClearDeletedAccount(string playerId)
    {
        ES3.DeleteKey(BLOCKED_KEY + playerId);
        // 新记录只含公共关卡 ID，属于设备偏好，退出/删除账号不恢复已屏蔽内容。
        if (s_importedPlayerId == playerId) s_importedPlayerId = null;
    }

    public static async Task SubmitAsync(string id, string reason, string details, CancellationToken token)
    {
        var login = MPLoginManager.Instance;
        if (!login.IsLoggedIn || login.IsDeletingAccount || IsSubmitting)
            throw new InvalidOperationException("Account is not ready.");
        token.ThrowIfCancellationRequested();
        string playerId = login.PlayerId;
        IsSubmitting = true;
        try
        {
            bool success = await CloudCodeService.Instance.CallModuleEndpointAsync<bool>(
                MPCustomLevelPublishConstants.MODULE_NAME, "ReportCustomLevel",
                new Dictionary<string, object>
                {
                    { "publicLevelId", id }, { "reason", reason }, { "details", details ?? string.Empty }
                });
            token.ThrowIfCancellationRequested();
            if (!success || login.PlayerId != playerId)
                throw new InvalidOperationException("Report not completed for the current account.");
        }
        finally { IsSubmitting = false; }
    }
}

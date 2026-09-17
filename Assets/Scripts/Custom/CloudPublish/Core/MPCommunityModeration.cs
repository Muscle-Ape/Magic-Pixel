using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

/// <summary>社区屏蔽按账号保存在本机；举报由 Cloud Code 验证并保存。</summary>
public static class MPCommunityModeration
{
    private const string BLOCKED_KEY = "MPCommunity.BlockedLevels.v1.";
    private static string s_playerId;
    private static HashSet<string> s_blocked;
    public static event Action<string> LevelBlocked;
    public static bool IsSubmitting { get; private set; }

    private static void EnsureLoaded()
    {
        string playerId = MPLoginManager.Instance.PlayerId ?? string.Empty;
        if (s_blocked != null && s_playerId == playerId) return;
        var ids = ES3.Load(BLOCKED_KEY + playerId, new List<string>());
        s_blocked = new HashSet<string>(ids ?? new List<string>(), StringComparer.Ordinal);
        s_playerId = playerId;
    }

    public static bool IsBlocked(string id)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(id) && s_blocked.Contains(id);
    }

    public static void Block(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        EnsureLoaded();
        if (s_blocked.Contains(id)) return;
        // 先落盘，失败时不让 UI 误以为已永久屏蔽。
        var next = new HashSet<string>(s_blocked, StringComparer.Ordinal) { id };
        ES3.Save(BLOCKED_KEY + s_playerId, new List<string>(next));
        s_blocked = next;
        LevelBlocked?.Invoke(id);
    }

    public static void ClearDeletedAccount(string playerId)
    {
        ES3.DeleteKey(BLOCKED_KEY + playerId);
        if (s_playerId == playerId) s_blocked = null;
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

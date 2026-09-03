using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>签到奖励为有限序列；更新版本可追加条目，但已经发布过的 id 不可复用。</summary>
[Serializable]
public sealed class MPSignInRewardEntry
{
    public string id;
    public string type;
    public int amount;
    public string icon;
}

public sealed class MPSignInConfig
{
    public const int DAYS_PER_ROUND = 7;

    public IReadOnlyList<MPSignInRewardEntry> Entries { get; }

    // 保留配置尾部的数据，但只有完整的七天一组才开放，不能因尾组未补齐禁用前面的完整组。
    public int AvailableEntryCount => Entries.Count - Entries.Count % DAYS_PER_ROUND;
    public bool HasIncompleteRound => AvailableEntryCount != Entries.Count;

    internal MPSignInConfig(List<MPSignInRewardEntry> entries)
    {
        Entries = entries.AsReadOnly();
    }
}

public static class MPSignInConfigService
{
    private const string CONFIG_LOCATION = "sign_in_config";
    private static string s_cachedJson;
    private static MPSignInConfig s_cachedConfig;
    private static string s_cachedError;
    private static string s_lastReportedError;

    public static bool TryLoad(out MPSignInConfig config)
    {
        config = null;
        try
        {
            // 不持有配置资源句柄，避免常驻首页将热更新前的配置锁在缓存中。
            using (MPAssetLoadLease<TextAsset> lease = MPLoad.LoadLease<TextAsset>(CONFIG_LOCATION))
            {
                string json = lease.Asset.text;
                if (!string.Equals(json, s_cachedJson, StringComparison.Ordinal))
                {
                    s_cachedJson = json;
                    TryParse(json, out s_cachedConfig, out s_cachedError);
                }
            }
            config = s_cachedConfig;
            if (config != null)
            {
                s_lastReportedError = null;
                return true;
            }
            ReportError(s_cachedError ?? "Sign-in configuration is empty.");
        }
        catch (Exception exception)
        {
            ReportError(exception.Message);
        }
        return false;
    }

    public static bool TryParse(string json, out MPSignInConfig config, out string error)
    {
        config = null;
        error = null;
        try
        {
            List<MPSignInRewardEntry> entries = JsonConvert.DeserializeObject<List<MPSignInRewardEntry>>(json);
            if (entries == null)
                throw new InvalidOperationException("Expected a sign-in reward array.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MPSignInRewardEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    throw new InvalidOperationException("Every sign-in reward requires a stable id.");
                entry.id = entry.id.Trim();
                entry.type = MPRewardPresentation.NormalizeType(entry.type);
                if (!ids.Add(entry.id))
                    throw new InvalidOperationException("Duplicate sign-in reward id: " + entry.id);
                if (entry.type == null || entry.amount <= 0 || entry.amount > int.MaxValue / 2)
                    throw new InvalidOperationException("Invalid sign-in reward: " + entry.id);
                entry.icon = string.IsNullOrWhiteSpace(entry.icon)
                    ? MPRewardPresentation.Icon(entry.type) : entry.icon.Trim();
            }
            config = new MPSignInConfig(entries);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool MigrateLegacyProgress(MPRewardProgressSnapshot progress, MPSignInConfig config)
    {
        if (config == null || config.AvailableEntryCount == 0)
            return false;
        bool changed = false;
        if (progress.signInClaimedEntryIds == null)
        {
            progress.signInClaimedEntryIds = new List<string>();
            progress.signInLegacyClaimedDays = Math.Max(progress.signInLegacyClaimedDays, progress.signInClaimedDays);
            changed = true;
        }
        int legacyCount = Math.Max(0, progress.signInLegacyClaimedDays);
        int mappedCount = Math.Max(0, progress.signInLegacyMappedDays);
        if (legacyCount <= mappedCount)
            return changed;
        for (int i = mappedCount; i < Math.Min(legacyCount, config.AvailableEntryCount); i++)
        {
            string id = config.Entries[i].id;
            if (!progress.signInClaimedEntryIds.Contains(id))
                progress.signInClaimedEntryIds.Add(id);
        }
        // 超过已开放数量的旧循环次数也标记为已迁移，不吞掉尾组补齐或未来版本新增的奖励。
        progress.signInLegacyMappedDays = legacyCount;
        return true;
    }

    private static void ReportError(string error)
    {
        if (s_lastReportedError == error) return;
        s_lastReportedError = error;
        Debug.LogWarning("[MPSignInConfig] " + error);
    }
}

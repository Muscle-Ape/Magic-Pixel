using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 基于 ES3 的本地登录资料仓储。
/// 这里只保存恢复线索和登录偏好，不保存 AccessToken 或 SessionToken 明文。
/// </summary>
public class MPEasySaveLocalLoginRepository : IMPLocalLoginRepository
{
    /// <summary>本地登录资料 JSON 的 ES3 Key。</summary>
    private const string PROFILE_KEY = "MPLogin.LocalProfileJson";
    private const string GUEST_PROFILE_KEY = "MPLogin.GuestProfileJson";

    /// <summary>安装实例 Id 的 ES3 Key。</summary>
    private const string INSTALLATION_ID_KEY = "MPLogin.InstallationId";

    /// <summary>匿名账号 Id 的 ES3 Key。</summary>
    private const string ANONYMOUS_ID_KEY = "MPLogin.AnonymousId";

    /// <summary>匿名登录幂等键的 ES3 Key。</summary>
    private const string ANONYMOUS_IDEMPOTENCY_KEY = "MPLogin.AnonymousIdempotencyKey";

    /// <summary>历史 PlayerId 的 ES3 Key。</summary>
    private const string HISTORY_PLAYER_ID_KEY = "MPLogin.HistoryPlayerId";

    /// <summary>最近登录方式的 ES3 Key。</summary>
    private const string LAST_PROVIDER_KEY = "MPLogin.LastProvider";

    public Task<MPLocalLoginProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadProfileUnsafe());
    }

    public Task SaveAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (profile == null)
        {
            return ClearActiveSessionAsync(keepRecoveryData: false, cancellationToken);
        }

        MPLocalLoginProfile guest = LoadGuestProfileUnsafe();
        profile.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        ES3.Save(PROFILE_KEY, JsonConvert.SerializeObject(profile));

        SaveIfNotEmpty(INSTALLATION_ID_KEY, profile.installationId);
        SaveIfNotEmpty(ANONYMOUS_ID_KEY, profile.anonymousId);
        SaveIfNotEmpty(ANONYMOUS_IDEMPOTENCY_KEY, profile.anonymousIdempotencyKey);
        SaveIfNotEmpty(HISTORY_PLAYER_ID_KEY, profile.playerId);
        ES3.Save(LAST_PROVIDER_KEY, (int)profile.lastLoginProvider);

        if ((guest == null && profile.IsIndependentGuest) ||
            (guest != null && ((!string.IsNullOrEmpty(profile.playerId) && guest.playerId == profile.playerId) ||
                               (!string.IsNullOrEmpty(profile.unityProfile) && guest.unityProfile == profile.unityProfile))))
        {
            // 游客后来绑定第三方时也更新此槽的绑定标记，但不删除旧 Profile 的凭证。
            ES3.Save(GUEST_PROFILE_KEY, JsonConvert.SerializeObject(profile));
        }

        return Task.CompletedTask;
    }

    public Task<MPLocalLoginProfile> LoadGuestProfileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadGuestProfileUnsafe());
    }

    public Task SaveGuestProfileAsync(MPLocalLoginProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (profile == null || string.IsNullOrEmpty(profile.unityProfile))
            throw new ArgumentException("Guest profile must have a Unity profile name.");
        ES3.Save(GUEST_PROFILE_KEY, JsonConvert.SerializeObject(profile));
        return Task.CompletedTask;
    }

    private MPLocalLoginProfile LoadGuestProfileUnsafe()
    {
        string json = ES3.Load<string>(GUEST_PROFILE_KEY, defaultValue: null);
        if (string.IsNullOrEmpty(json)) return null;
        // 损坏的游客记录不能当作“没有游客”而覆盖它，交给登录页显示可重试异常。
        MPLocalLoginProfile guest = JsonConvert.DeserializeObject<MPLocalLoginProfile>(json);
        if (guest == null || string.IsNullOrEmpty(guest.unityProfile))
            throw new InvalidOperationException("Saved guest profile is invalid.");
        return guest;
    }

    public Task ClearActiveSessionAsync(bool keepRecoveryData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!keepRecoveryData)
        {
            DeleteKeyIfExists(PROFILE_KEY);
            DeleteKeyIfExists(ANONYMOUS_ID_KEY);
            DeleteKeyIfExists(ANONYMOUS_IDEMPOTENCY_KEY);
            DeleteKeyIfExists(HISTORY_PLAYER_ID_KEY);
            DeleteKeyIfExists(LAST_PROVIDER_KEY);
            return Task.CompletedTask;
        }

        MPLocalLoginProfile profile = LoadProfileUnsafe();
        if (profile == null)
        {
            return Task.CompletedTask;
        }

        profile.refreshToken = string.Empty;
        profile.hasUnitySessionToken = false;
        profile.updatedAtUtcTicks = DateTime.UtcNow.Ticks;
        ES3.Save(PROFILE_KEY, JsonConvert.SerializeObject(profile));
        return Task.CompletedTask;
    }

    public Task<string> GetOrCreateInstallationIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetOrCreateString(INSTALLATION_ID_KEY, "install"));
    }

    public Task<string> GetOrCreateAnonymousIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetOrCreateString(ANONYMOUS_ID_KEY, "anon"));
    }

    public Task<string> ResetAnonymousIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string anonymousId = CreateStableId("anon");
        ES3.Save(ANONYMOUS_ID_KEY, anonymousId);
        return Task.FromResult(anonymousId);
    }

    public Task<string> GetOrCreateAnonymousIdempotencyKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetOrCreateString(ANONYMOUS_IDEMPOTENCY_KEY, "idem"));
    }

    public Task<string> ResetAnonymousIdempotencyKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string idempotencyKey = CreateStableId("idem");
        ES3.Save(ANONYMOUS_IDEMPOTENCY_KEY, idempotencyKey);
        return Task.FromResult(idempotencyKey);
    }

    public Task<bool> HasAnyLoginHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasHistory =
            ES3.KeyExists(PROFILE_KEY) ||
            ES3.KeyExists(ANONYMOUS_ID_KEY) ||
            ES3.KeyExists(HISTORY_PLAYER_ID_KEY) ||
            ES3.KeyExists(LAST_PROVIDER_KEY);

        return Task.FromResult(hasHistory);
    }

    /// <summary>
    /// 读取并反序列化本地登录资料。失败时返回 null，并保留原始存档等待后续排查。
    /// </summary>
    private MPLocalLoginProfile LoadProfileUnsafe()
    {
        if (!ES3.KeyExists(PROFILE_KEY))
        {
            return null;
        }

        string json = ES3.Load<string>(PROFILE_KEY, defaultValue: null);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<MPLocalLoginProfile>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPLogin] 本地登录资料解析失败，将进入登录恢复流程。{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取本地字符串值；不存在时创建一个带前缀的稳定 Id。
    /// </summary>
    private string GetOrCreateString(string key, string prefix)
    {
        string value = ES3.Load<string>(key, defaultValue: null);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = CreateStableId(prefix);
        ES3.Save(key, value);
        return value;
    }

    /// <summary>
    /// 生成本地稳定 Id。
    /// </summary>
    private static string CreateStableId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 非空时保存字符串，避免把空字符串覆盖掉已有恢复线索。
    /// </summary>
    private static void SaveIfNotEmpty(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            ES3.Save(key, value);
        }
    }

    /// <summary>
    /// Key 存在时删除，避免 ES3 抛出无意义错误。
    /// </summary>
    private static void DeleteKeyIfExists(string key)
    {
        if (ES3.KeyExists(key))
        {
            ES3.DeleteKey(key);
        }
    }
}

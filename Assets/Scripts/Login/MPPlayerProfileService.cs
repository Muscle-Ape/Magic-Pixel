using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>独立资料记录：先保存完整远端资料，再提交同一份本地资料。</summary>
public static class MPPlayerProfileService
{
    public const string CLOUD_KEY = "mp_player_profile_v1";
    public const int MAX_NAME_LENGTH = 20;
    private static readonly SemaphoreSlim s_saveLock = new SemaphoreSlim(1, 1);
    private static readonly IMPCloudSaveApi s_api = new MPUnityCloudSaveApi();

    /// <summary>用户名只接受 ASCII 英文、数字、空格、横线和下划线，不接受中文等 Unicode 字符。</summary>
    public static bool ValidateName(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
            error = "Please enter a name.";
        else if (name.Trim().Length > MAX_NAME_LENGTH)
            error = $"Use at most {MAX_NAME_LENGTH} characters.";
        else if (!Regex.IsMatch(name.Trim(), @"^[A-Za-z0-9 _\-]+$", RegexOptions.CultureInvariant))
            error = "Use English letters, numbers, spaces, - or _.";
        else
            error = null;
        return error == null;
    }

    public static bool Validate(string name, int avatarId, out string error)
    {
        if (!ValidateName(name, out error))
            return false;
        if (avatarId < 0 || avatarId >= MPPlayerProfile.AVATAR_COUNT)
        {
            error = "Please choose an avatar.";
            return false;
        }
        return true;
    }

    public static async Task SaveAsync(string name, int avatarId, CancellationToken token)
    {
        if (!Validate(name, avatarId, out string error))
            throw new InvalidOperationException(error);
        if (!MPLoginManager.Instance.IsLoggedIn)
            throw new InvalidOperationException("Connect and sign in before saving your profile.");

        string playerId = MPLoginManager.Instance.PlayerId;
        await s_saveLock.WaitAsync(token);
        try
        {
            MPCloudSaveLoadResult<MPPlayerProfile> previous = await s_api.LoadPlayerDataAsync<MPPlayerProfile>(CLOUD_KEY, token);
            token.ThrowIfCancellationRequested();
            if (MPLoginManager.Instance.PlayerId != playerId)
                throw new InvalidOperationException("Account changed. Please reopen this window.");
            MPPlayerProfile profile = new MPPlayerProfile
            {
                displayName = name.Trim(), avatarId = avatarId, updatedAtUtcTicks = DateTime.UtcNow.Ticks
            };
            // 请求发出后必须把成功结果写入本地，即使页面此时已经退出。
            await s_api.SavePlayerDataAsync(CLOUD_KEY, profile, previous.writeLock, true, CancellationToken.None);
            if (MPLoginManager.Instance.PlayerId == playerId)
                MPUser.instance.ApplyPlayerProfile(profile, playerId);
        }
        finally { s_saveLock.Release(); }
    }

    public static async Task RefreshAsync(CancellationToken token = default)
    {
        if (!MPLoginManager.Instance.IsLoggedIn)
            return;
        string playerId = MPLoginManager.Instance.PlayerId;
        await s_saveLock.WaitAsync(token);
        try
        {
            MPCloudSaveLoadResult<MPPlayerProfile> result = await s_api.LoadPlayerDataAsync<MPPlayerProfile>(CLOUD_KEY, token);
            if (result.exists && result.value != null && result.value.schemaVersion == 1 &&
                Validate(result.value.displayName, result.value.avatarId, out _) && MPLoginManager.Instance.PlayerId == playerId)
                MPUser.instance.ApplyPlayerProfile(result.value, playerId);
        }
        finally { s_saveLock.Release(); }
    }
}

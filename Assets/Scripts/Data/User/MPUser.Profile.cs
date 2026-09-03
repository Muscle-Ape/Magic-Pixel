using System;
using Newtonsoft.Json;
using UnityEngine;

public partial class MPUser
{
    private const string PROFILE_KEY_PREFIX = "MPUser.Profile.v1.";
    private MPPlayerProfile m_playerProfile;
    private string m_profileOwner;

    public static event Action ProfileChanged;

    public string GetProfileName()
    {
        EnsurePlayerProfile();
        return m_playerProfile?.displayName ?? string.Empty;
    }

    public int GetProfileAvatarId()
    {
        EnsurePlayerProfile();
        return Mathf.Clamp(m_playerProfile?.avatarId ?? 0, 0, MPPlayerProfile.AVATAR_COUNT - 1);
    }

    public string GetProfileAvatarLocation() => MPPlayerProfile.GetAvatarLocation(GetProfileAvatarId());

    public void ApplyPlayerProfile(MPPlayerProfile profile, string playerId)
    {
        if (profile == null || playerId != MPLoginManager.Instance.PlayerId)
            return;

        // 昵称和头像作为同一个存档记录提交，不能只修改其中一项。
        string json = JsonConvert.SerializeObject(profile);
        ES3.Save(PROFILE_KEY_PREFIX + playerId, json);
        m_profileOwner = playerId;
        m_playerProfile = JsonConvert.DeserializeObject<MPPlayerProfile>(json);
        ProfileChanged?.Invoke();
    }

    private void EnsurePlayerProfile()
    {
        string owner = MPLoginManager.Instance.PlayerId ?? string.Empty;
        if (m_playerProfile != null && m_profileOwner == owner)
            return;

        m_profileOwner = owner;
        m_playerProfile = new MPPlayerProfile();
        try
        {
            string json = ES3.Load<string>(PROFILE_KEY_PREFIX + owner, defaultValue: string.Empty);
            if (!string.IsNullOrEmpty(json))
                m_playerProfile = JsonConvert.DeserializeObject<MPPlayerProfile>(json) ?? new MPPlayerProfile();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPUser] 读取用户资料失败，使用默认资料：{exception.Message}");
        }
    }
}

[Serializable]
public sealed class MPPlayerProfile
{
    public const int AVATAR_COUNT = 6;
    public int schemaVersion = 1;
    public string displayName = string.Empty;
    public int avatarId;
    public long updatedAtUtcTicks;

    public static string GetAvatarLocation(int id)
    {
        // 存档继续保留 0..5 的 avatarId；更换图片只需替换同名资源。
        return "popup_avatar_" + (Mathf.Clamp(id, 0, AVATAR_COUNT - 1) + 1).ToString("00");
    }
}

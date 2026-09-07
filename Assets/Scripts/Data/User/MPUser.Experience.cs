using System;
using UnityEngine;

/// <summary>用户经验等级，与主线解锁进度独立；经验按账号保存，等级和当前级进度由总经验计算。</summary>
public partial class MPUser
{
    public const int EXPERIENCE_PER_LEVEL = 100;
    public const int MAIN_LEVEL_COMPLETION_EXPERIENCE = 30;
    public const int LARGE_IMAGE_COMPLETION_EXPERIENCE = 200;

    private const string PLAYER_EXPERIENCE_KEY_PREFIX = "MPUser.Experience.v1.";
    private int m_playerExperience;
    private string m_experienceOwner;
    private bool m_experienceLoaded;

    public static event Action ExperienceChanged;

    private void InitPlayerExperience()
    {
        m_experienceLoaded = false;
        EnsurePlayerExperience();
    }

    /// <summary>累计经验。旧存档没有经验记录时从 0 开始，不以关卡进度反推历史经验。</summary>
    public int GetPlayerExperience()
    {
        EnsurePlayerExperience();
        return m_playerExperience;
    }

    /// <summary>初始 1 级，每累计 100 点经验升一级。</summary>
    public int GetPlayerLevel() => 1 + GetPlayerExperience() / EXPERIENCE_PER_LEVEL;

    /// <summary>本级经验进度，升级后保留余数；一次奖励可连续升多级。</summary>
    public float GetPlayerLevelProgress() => (GetPlayerExperience() % EXPERIENCE_PER_LEVEL) / (float)EXPERIENCE_PER_LEVEL;

    /// <summary>通关入账时调用一次。先保存再更新显示，刷新页面、读取存档均不会发放经验。</summary>
    public void AddPlayerExperience(int amount)
    {
        if (amount <= 0)
            return;

        EnsurePlayerExperience();
        int total = (int)Math.Min(int.MaxValue, (long)m_playerExperience + amount);
        if (total == m_playerExperience)
            return;

        ES3.Save(PLAYER_EXPERIENCE_KEY_PREFIX + m_experienceOwner, total);
        m_playerExperience = total;
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.PlayerExperience);
        NotifyExperienceChanged();
    }

    private void EnsurePlayerExperience()
    {
        string owner = MPLoginManager.Instance.PlayerId ?? string.Empty;
        if (m_experienceLoaded && m_experienceOwner == owner)
            return;

        SetPlayerExperienceInMemory(ReadPlayerExperience(owner), owner);
    }

    private static int ReadPlayerExperience(string owner)
    {
        return Math.Max(0, ES3.Load<int>(PLAYER_EXPERIENCE_KEY_PREFIX + owner, defaultValue: 0));
    }

    /// <summary>旧云存档缺少该字段时仅保留当前账号本地经验，切号不会继承其他账号的经验。</summary>
    private void ApplyPlayerExperienceSnapshot(int? totalExperience)
    {
        string owner = MPLoginManager.Instance.PlayerId ?? string.Empty;
        int total = Math.Max(0, totalExperience ?? ReadPlayerExperience(owner));
        ES3.Save(PLAYER_EXPERIENCE_KEY_PREFIX + owner, total);
        SetPlayerExperienceInMemory(total, owner);
        NotifyExperienceChanged();
    }

    private void SetPlayerExperienceInMemory(int total, string owner)
    {
        m_playerExperience = total;
        m_experienceOwner = owner;
        m_experienceLoaded = true;
    }

    /// <summary>UI 回调异常不能打断经验入账、其他页面刷新或通关结算。</summary>
    private static void NotifyExperienceChanged()
    {
        if (ExperienceChanged == null)
            return;
        foreach (Action listener in ExperienceChanged.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MPUser] 刷新用户等级失败：{exception.Message}");
            }
        }
    }
}

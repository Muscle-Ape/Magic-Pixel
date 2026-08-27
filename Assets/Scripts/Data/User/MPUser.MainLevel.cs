using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主线关卡存档。
/// </summary>
public partial class MPUser
{
    #region Key
    private string m_key_mainlevel_pass_index = "key_mainlevel_pass_index";
    private string m_key_mainlevel_unlocklist = "key_mainlevel_unlocklist";
    private string m_key_mainlevel_passlist = "key_mainlevel_passlist";
    private string m_key_mainlevel_stars = "key_mainlevel_stars";
    private string m_key_mainlevel_box_award_claimed = "key_mainlevel_box_award_claimed";
    #endregion

    #region Fields
    /// <summary>
    /// 主线关卡已经解锁到的关卡下标。
    /// </summary>
    private int m_mainlevel_pass_index;

    /// <summary>
    /// 主线关卡已经解锁的关卡ID列表。
    /// </summary>
    private List<string> m_mainlevel_unlocklist;

    /// <summary>
    /// 主线关卡已经通关的关卡ID列表。
    /// </summary>
    private List<string> m_mainlevel_passlist;

    /// <summary>
    /// 主线关卡已通关星数，key为关卡ID，value为剩余生命对应的星数。
    /// </summary>
    private Dictionary<string, int> m_mainlevel_stars;

    /// <summary>
    /// 主线关卡已经领取过宝箱奖励的关卡ID列表。
    /// </summary>
    private List<string> m_mainlevel_box_award_claimed;
    #endregion

    #region Method
    /// <summary>
    /// 初始化主线关卡存档。
    /// </summary>
    private void InitMainLevel()
    {
        m_mainlevel_pass_index = ES3.Load<int>(m_key_mainlevel_pass_index, 0);
        m_mainlevel_unlocklist = ES3.Load<List<string>>(m_key_mainlevel_unlocklist, new List<string>());
        m_mainlevel_passlist = ES3.Load<List<string>>(m_key_mainlevel_passlist, new List<string>());
        m_mainlevel_stars = ES3.Load<Dictionary<string, int>>(m_key_mainlevel_stars, new Dictionary<string, int>());
        m_mainlevel_box_award_claimed = ES3.Load<List<string>>(
            m_key_mainlevel_box_award_claimed,
            new List<string>());

        MainLevelUnlock(MPDataManager.Instance.m_mainLevelModel.blockInfos[0].ID);
    }

    /// <summary>
    /// 设置主线关卡当前通关下标。
    /// </summary>
    /// <param name="index">当前通关下标。</param>
    public void SetMainLevelPassIndex(int index)
    {
        m_mainlevel_pass_index = index;

        ES3.Save(m_key_mainlevel_pass_index, m_mainlevel_pass_index);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.MainLevel);
    }

    /// <summary>
    /// 获取主线关卡当前通关下标。
    /// </summary>
    /// <returns>当前通关下标。</returns>
    public int GetMainLevlPassIndex()
    {
        return m_mainlevel_pass_index;
    }

    /// <summary>
    /// 解锁指定主线关卡。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void MainLevelUnlock(string id)
    {
        if (!m_mainlevel_unlocklist.Contains(id))
        {
            m_mainlevel_unlocklist.Add(id);

            ES3.Save(m_key_mainlevel_unlocklist, m_mainlevel_unlocklist);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.MainLevel);
        }
    }

    /// <summary>
    /// 判断指定主线关卡是否已经解锁。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>是否已经解锁。</returns>
    public bool MainLevelIsUnlock(string id)
    {
        return m_mainlevel_unlocklist.Contains(id);
    }

    /// <summary>
    /// 记录主线关卡通关，不更新星数。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void MainLevelPass(string id)
    {
        MainLevelPass(id, 0);
    }

    /// <summary>
    /// 记录主线关卡通关，并保存该关卡历史最高星数。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <param name="stars">本次通关剩余生命对应的星数。</param>
    public void MainLevelPass(string id, int stars)
    {
        bool changed = false;
        if (!m_mainlevel_passlist.Contains(id))
        {
            m_mainlevel_passlist.Add(id);

            ES3.Save(m_key_mainlevel_passlist, m_mainlevel_passlist);
            changed = true;
        }

        stars = Mathf.Max(0, stars);
        if (!m_mainlevel_stars.ContainsKey(id) || stars > m_mainlevel_stars[id])
        {
            m_mainlevel_stars[id] = stars;

            ES3.Save(m_key_mainlevel_stars, m_mainlevel_stars);
            changed = true;
        }

        if (changed)
        {
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.MainLevel);
        }
    }

    /// <summary>
    /// 判断指定主线关卡是否已经通关。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>是否已经通关。</returns>
    public bool MainLevelIsPass(string id)
    {
        return m_mainlevel_passlist.Contains(id);
    }

    /// <summary>
    /// 获取主线关卡通关星数，未通关或没有记录时返回0。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>关卡历史最高星数。</returns>
    public int GetMainLevelStars(string id)
    {
        if (m_mainlevel_stars.TryGetValue(id, out int stars))
        {
            return stars;
        }

        return 0;
    }

    /// <summary>
    /// 判断指定主线关卡的宝箱奖励是否已经领取。
    /// </summary>
    public bool MainLevelBoxAwardIsClaimed(string id)
    {
        return !string.IsNullOrEmpty(id)
            && m_mainlevel_box_award_claimed.Contains(id);
    }

    /// <summary>
    /// 尝试领取已通关主线关卡的宝箱奖励，每个关卡只允许领取一次。
    /// </summary>
    public bool TryClaimMainLevelBoxAward(MPMainBlockInfo levelInfo)
    {
        MPMainLevelBoxAward award = levelInfo?.BoxAward;
        if (levelInfo == null
            || string.IsNullOrEmpty(levelInfo.ID)
            || award == null
            || !award.IsValid
            || !MainLevelIsPass(levelInfo.ID)
            || MainLevelBoxAwardIsClaimed(levelInfo.ID))
        {
            return false;
        }

        switch (award.Type.Trim().ToLowerInvariant())
        {
            case "coin":
                m_mainlevel_box_award_claimed.Add(levelInfo.ID);
                ES3.Save(
                    m_key_mainlevel_box_award_claimed,
                    m_mainlevel_box_award_claimed);
                NotifyCloudSaveDirty(MPCloudSaveDirtyReason.MainLevel);
                AddCoins(award.Count);
                return true;
            default:
                Debug.LogWarning(
                    $"不支持的主线宝箱奖励类型：{award.Type}，关卡：{levelInfo.ID}");
                return false;
        }
    }
    #endregion
}

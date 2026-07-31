using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大图模式关卡存档。
/// </summary>
public partial class MPUser
{
    #region Key
    private string m_key_largeimagelevel_pass_index = "m_key_largeimagelevel_pass_index";
    private string m_key_largeimagelevel_unlocklist = "m_key_largeimagelevel_unlocklist";
    private string m_key_largeimagelevel_passlist = "m_key_largeimagelevel_passlist";
    private string m_key_largeimagelevel_stars = "key_largeimagelevel_stars";
    private string m_key_largeimagelevel_coin_award_claimed = "key_largeimagelevel_coin_award_claimed";
    #endregion

    #region Fields
    /// <summary>
    /// 大图模式已经解锁到的关卡下标。
    /// </summary>
    private int m_largeimagelevel_pass_index;

    /// <summary>
    /// 大图模式已经解锁的关卡ID列表。
    /// </summary>
    private List<string> m_largeimagelevel_unlocklist;

    /// <summary>
    /// 大图模式已经通关的关卡ID列表。
    /// </summary>
    private List<string> m_largeimagelevel_passlist;

    /// <summary>
    /// 大图模式关卡已通关星数，key为关卡ID，value为剩余生命对应的星数。
    /// </summary>
    private Dictionary<string, int> m_largeimagelevel_stars;

    /// <summary>
    /// 大图模式已经领取过通关金币奖励的关卡ID列表。
    /// </summary>
    private List<string> m_largeimagelevel_coin_award_claimed;
    #endregion

    #region Method
    /// <summary>
    /// 初始化大图模式关卡存档。
    /// </summary>
    private void InitLargeImageLevel()
    {
        m_largeimagelevel_pass_index = ES3.Load<int>(m_key_largeimagelevel_pass_index, 0);
        m_largeimagelevel_unlocklist = ES3.Load<List<string>>(m_key_largeimagelevel_unlocklist, new List<string>());
        m_largeimagelevel_passlist = ES3.Load<List<string>>(m_key_largeimagelevel_passlist, new List<string>());
        m_largeimagelevel_stars = ES3.Load<Dictionary<string, int>>(m_key_largeimagelevel_stars, new Dictionary<string, int>());
        m_largeimagelevel_coin_award_claimed = ES3.Load<List<string>>(m_key_largeimagelevel_coin_award_claimed, new List<string>());

        LargeImageLevelUnlock(MPDataManager.Instance.m_largeImageModel.blockInfos[0].ID);
    }

    /// <summary>
    /// 设置大图模式当前通关下标。
    /// </summary>
    /// <param name="index">当前通关下标。</param>
    public void SetLargeImageLevelPassIndex(int index)
    {
        m_largeimagelevel_pass_index = index;

        ES3.Save(m_key_largeimagelevel_pass_index, m_largeimagelevel_pass_index);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.LargeImageLevel);
    }

    /// <summary>
    /// 获取大图模式当前通关下标。
    /// </summary>
    /// <returns>当前通关下标。</returns>
    public int GetLargeImageLevlPassIndex()
    {
        return m_largeimagelevel_pass_index;
    }

    /// <summary>
    /// 解锁指定大图模式关卡。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void LargeImageLevelUnlock(string id)
    {
        if (!m_largeimagelevel_unlocklist.Contains(id))
        {
            m_largeimagelevel_unlocklist.Add(id);

            ES3.Save(m_key_largeimagelevel_unlocklist, m_largeimagelevel_unlocklist);
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.LargeImageLevel);
        }
    }

    /// <summary>
    /// 判断指定大图模式关卡是否已经解锁。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>是否已经解锁。</returns>
    public bool LargeImageLevelIsUnlock(string id)
    {
        return m_largeimagelevel_unlocklist.Contains(id);
    }

    /// <summary>
    /// 记录大图模式关卡通关，不更新星数。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    public void LargeImageLevelPass(string id)
    {
        LargeImageLevelPass(id, 0);
    }

    /// <summary>
    /// 记录大图模式关卡通关，并保存该关卡历史最高星数。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <param name="stars">本次通关剩余生命对应的星数。</param>
    public void LargeImageLevelPass(string id, int stars)
    {
        bool changed = false;
        if (!m_largeimagelevel_passlist.Contains(id))
        {
            m_largeimagelevel_passlist.Add(id);

            ES3.Save(m_key_largeimagelevel_passlist, m_largeimagelevel_passlist);
            changed = true;
        }

        stars = Mathf.Max(0, stars);
        if (!m_largeimagelevel_stars.ContainsKey(id) || stars > m_largeimagelevel_stars[id])
        {
            m_largeimagelevel_stars[id] = stars;

            ES3.Save(m_key_largeimagelevel_stars, m_largeimagelevel_stars);
            changed = true;
        }

        if (changed)
        {
            NotifyCloudSaveDirty(MPCloudSaveDirtyReason.LargeImageLevel);
        }
    }

    /// <summary>
    /// 判断指定大图模式关卡是否已经通关。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>是否已经通关。</returns>
    public bool LargeImageLevelIsPass(string id)
    {
        return m_largeimagelevel_passlist.Contains(id);
    }

    /// <summary>
    /// 获取大图模式关卡通关星数，未通关或没有记录时返回0。
    /// </summary>
    /// <param name="id">关卡ID。</param>
    /// <returns>关卡历史最高星数。</returns>
    public int GetLargeImageLevelStars(string id)
    {
        if (m_largeimagelevel_stars.TryGetValue(id, out int stars))
        {
            return stars;
        }

        return 0;
    }

    /// <summary>
    /// 尝试领取大图模式关卡通关金币奖励，每个关卡只允许领取一次。
    /// </summary>
    /// <param name="levelInfo">大图模式关卡配置。</param>
    /// <returns>本次是否成功领取奖励。</returns>
    public bool TryClaimLargeImageLevelCoinAward(MPLargeImageBlockInfo levelInfo)
    {
        if (levelInfo == null || string.IsNullOrEmpty(levelInfo.ID) || levelInfo.AwardCoin <= 0)
        {
            return false;
        }

        if (m_largeimagelevel_coin_award_claimed.Contains(levelInfo.ID))
        {
            return false;
        }

        m_largeimagelevel_coin_award_claimed.Add(levelInfo.ID);
        ES3.Save(m_key_largeimagelevel_coin_award_claimed, m_largeimagelevel_coin_award_claimed);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.LargeImageLevel);
        AddCoins(levelInfo.AwardCoin);
        return true;
    }
    #endregion
}

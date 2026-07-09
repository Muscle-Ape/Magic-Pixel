using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主游戏
/// </summary>
public partial class MPUser
{
    #region Key
    private string m_key_mainlevel_pass_index = "key_mainlevel_pass_index";
    private string m_key_mainlevel_unlocklist = "key_mainlevel_unlocklist";
    private string m_key_mainlevel_passlist = "key_mainlevel_passlist";
    private string m_key_mainlevel_stars = "key_mainlevel_stars";
    private string m_key_largeimagelevel_pass_index = "m_key_largeimagelevel_pass_index";
    private string m_key_largeimagelevel_unlocklist = "m_key_largeimagelevel_unlocklist";
    private string m_key_largeimagelevel_passlist = "m_key_largeimagelevel_passlist";
    #endregion

    #region Fields
    /// <summary>
    /// 已经解锁到的关卡下标
    /// </summary>
    private int m_mainlevel_pass_index;

    /// <summary>
    /// 已经解锁了的关卡
    /// string : id
    /// </summary>
    private List<string> m_mainlevel_unlocklist;

    /// <summary>
    /// 已经通关了的关卡
    /// string : id
    /// </summary>
    private List<string> m_mainlevel_passlist;

    /// <summary>
    /// 主线关卡已通关星数，key为关卡ID，value为剩余生命对应的星数。
    /// </summary>
    private Dictionary<string, int> m_mainlevel_stars;

    /// <summary>
    /// 已经解锁到的大图模式的关卡下标
    /// </summary>
    private int m_largeimagelevel_pass_index;

    /// <summary>
    /// 大图模式已经解锁了的关卡
    /// </summary>
    private List<string> m_largeimagelevel_unlocklist;

    /// <summary>
    /// 大图模式已经通关了的关卡
    /// </summary>
    private List<string> m_largeimagelevel_passlist;
    #endregion

    #region Method
    private void InitMainLevel()
    {
        m_mainlevel_pass_index = ES3.Load<int>(m_key_mainlevel_pass_index, 0);
        m_mainlevel_unlocklist = ES3.Load<List<string>>(m_key_mainlevel_unlocklist, new List<string>());
        m_mainlevel_passlist = ES3.Load<List<string>>(m_key_mainlevel_passlist, new List<string>());
        m_mainlevel_stars = ES3.Load<Dictionary<string, int>>(m_key_mainlevel_stars, new Dictionary<string, int>());
        m_largeimagelevel_pass_index = ES3.Load<int>(m_key_largeimagelevel_pass_index, 0);
        m_largeimagelevel_unlocklist = ES3.Load<List<string>>(m_key_largeimagelevel_unlocklist, new List<string>());
        m_largeimagelevel_passlist = ES3.Load<List<string>>(m_key_largeimagelevel_passlist, new List<string>());

        // 默认解锁第一关
        MainLevelUnlock(MPDataManager.Instance.m_mainLevelModel.blockInfos[0].ID);
        LargeImageLevelUnlock(MPDataManager.Instance.m_largeImageModel.blockInfos[0].ID);
    }

    public void SetMainLevelPassIndex(int index)
    {
        m_mainlevel_pass_index = index;

        ES3.Save(m_key_mainlevel_pass_index, m_mainlevel_pass_index);
    }

    public int GetMainLevlPassIndex()
    {
        return m_mainlevel_pass_index;
    }


    public void MainLevelUnlock(string id)
    {
        if (!m_mainlevel_unlocklist.Contains(id))
        {
            m_mainlevel_unlocklist.Add(id);

            ES3.Save(m_key_mainlevel_unlocklist, m_mainlevel_unlocklist);
        }
    }

    public bool MainLevelIsUnlock(string id)
    {
        return m_mainlevel_unlocklist.Contains(id);
    }


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
        if (!m_mainlevel_passlist.Contains(id))
        {
            m_mainlevel_passlist.Add(id);

            ES3.Save(m_key_mainlevel_passlist, m_mainlevel_passlist);
        }

        stars = Mathf.Max(0, stars);
        if (!m_mainlevel_stars.ContainsKey(id) || stars > m_mainlevel_stars[id])
        {
            m_mainlevel_stars[id] = stars;

            ES3.Save(m_key_mainlevel_stars, m_mainlevel_stars);
        }
    }

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

    public void SetLargeImageLevelPassIndex(int index)
    {
        m_largeimagelevel_pass_index = index;

        ES3.Save(m_key_largeimagelevel_pass_index, m_largeimagelevel_pass_index);
    }

    public int GetLargeImageLevlPassIndex()
    {
        return m_largeimagelevel_pass_index;
    }


    public void LargeImageLevelUnlock(string id)
    {
        if (!m_largeimagelevel_unlocklist.Contains(id))
        {
            m_largeimagelevel_unlocklist.Add(id);

            ES3.Save(m_key_largeimagelevel_unlocklist, m_largeimagelevel_unlocklist);
        }
    }

    public bool LargeImageLevelIsUnlock(string id)
    {
        return m_largeimagelevel_unlocklist.Contains(id);
    }


    public void LargeImageLevelPass(string id)
    {
        if (!m_largeimagelevel_passlist.Contains(id))
        {
            m_largeimagelevel_passlist.Add(id);

            ES3.Save(m_key_largeimagelevel_passlist, m_largeimagelevel_passlist);
        }
    }

    public bool LargeImageLevelIsPass(string id)
    {
        return m_largeimagelevel_passlist.Contains(id);
    }
    #endregion
}

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
        m_largeimagelevel_pass_index = ES3.Load<int>(m_key_mainlevel_pass_index, 0);
        m_largeimagelevel_unlocklist = ES3.Load<List<string>>(m_key_mainlevel_unlocklist, new List<string>());
        m_largeimagelevel_passlist = ES3.Load<List<string>>(m_key_mainlevel_passlist, new List<string>());

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
        if (!m_mainlevel_passlist.Contains(id))
        {
            m_mainlevel_passlist.Add(id);

            ES3.Save(m_key_mainlevel_passlist, m_mainlevel_passlist);
        }
    }

    public bool MainLevelIsPass(string id)
    {
        return m_mainlevel_passlist.Contains(id);
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

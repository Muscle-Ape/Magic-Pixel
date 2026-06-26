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
    #endregion

    #region Method
    private void InitMainLevel()
    {
        m_mainlevel_pass_index = ES3.Load<int>(m_key_mainlevel_pass_index, 0);
        m_mainlevel_unlocklist = ES3.Load<List<string>>(m_key_mainlevel_unlocklist, new List<string>());
        m_mainlevel_passlist = ES3.Load<List<string>>(m_key_mainlevel_passlist, new List<string>());

        // 默认解锁第一关
        MainLevelUnlock(MPDataManager.Instance.m_mainLevelModel.blockInfos[0].ID);
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
    #endregion
}

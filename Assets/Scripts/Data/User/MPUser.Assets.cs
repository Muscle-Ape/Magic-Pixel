using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用户资产
/// </summary>
public partial class MPUser
{
    #region Key
    private string m_key_coins = "key_coins";


    #endregion


    #region Fields
    /// <summary>
    /// 金币
    /// </summary>
    private int m_coins;


    #endregion


    private void InitAssets()
    {
        m_coins = ES3.Load<int>(m_key_coins, 0);
    }


    #region Method
    public void AddCoins(int count)
    {
        m_coins += count;

        ES3.Save(m_key_coins, m_coins);
    }

    public void UseCoins(int count)
    {
        m_coins = Mathf.Max(m_coins - count, 0);

        ES3.Save(m_key_coins, m_coins);
    }

    public int GetCoins()
    {
        return m_coins;
    }
    #endregion
}

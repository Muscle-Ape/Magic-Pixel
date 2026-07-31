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

    private string m_ket_diamond = "m_ket_diamond";

    /// <summary>
    /// 提示道具数量存档Key。
    /// </summary>
    private string m_key_hint_props = "key_hint_props";

    /// <summary>
    /// 生命恢复道具数量存档Key。
    /// </summary>
    private string m_key_love_recover_props = "key_love_recover_props";


    #endregion


    #region Fields
    /// <summary>
    /// 金币
    /// </summary>
    private int m_coins;

    /// <summary>
    /// 钻石
    /// </summary>
    private int m_diamond;

    /// <summary>
    /// 当前拥有的提示道具数量。
    /// </summary>
    private int m_hintProps;

    /// <summary>
    /// 当前拥有的生命恢复道具数量。
    /// </summary>
    private int m_loveRecoverProps;


    #endregion


    private void InitAssets()
    {
        m_coins = ES3.Load<int>(m_key_coins, 200);
        m_diamond = ES3.Load<int>(m_ket_diamond, 0);
        m_hintProps = ES3.Load<int>(m_key_hint_props, 0);
        m_loveRecoverProps = ES3.Load<int>(m_key_love_recover_props, 0);
    }


    #region Method
    public void AddCoins(int count)
    {
        m_coins += count;

        ES3.Save(m_key_coins, m_coins);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    public void UseCoins(int count)
    {
        m_coins = Mathf.Max(m_coins - count, 0);

        ES3.Save(m_key_coins, m_coins);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    public int GetCoins()
    {
        return m_coins;
    }


    public void AddDiamond(int count)
    {
        m_diamond += count;

        ES3.Save(m_ket_diamond, m_diamond);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    public void UseDiamond(int count)
    {
        m_diamond = Mathf.Max(m_diamond - count, 0);

        ES3.Save(m_ket_diamond, m_diamond);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    public int GetDiamond()
    {
        return m_diamond;
    }

    /// <summary>
    /// 增加提示道具数量。
    /// </summary>
    /// <param name="count">增加数量。</param>
    public void AddHintProps(int count)
    {
        if (count <= 0)
            return;

        m_hintProps += count;

        ES3.Save(m_key_hint_props, m_hintProps);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    /// <summary>
    /// 尝试消耗一个提示道具。
    /// </summary>
    /// <returns>消耗成功返回true，数量不足返回false。</returns>
    public bool UseHintProp()
    {
        if (m_hintProps <= 0)
            return false;

        m_hintProps--;

        ES3.Save(m_key_hint_props, m_hintProps);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);

        return true;
    }

    /// <summary>
    /// 获取当前提示道具数量。
    /// </summary>
    /// <returns>提示道具数量。</returns>
    public int GetHintProps()
    {
        return m_hintProps;
    }

    /// <summary>
    /// 增加生命恢复道具数量。
    /// </summary>
    /// <param name="count">增加数量。</param>
    public void AddLoveRecoverProps(int count)
    {
        if (count <= 0)
            return;

        m_loveRecoverProps += count;

        ES3.Save(m_key_love_recover_props, m_loveRecoverProps);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);
    }

    /// <summary>
    /// 尝试消耗一个生命恢复道具。
    /// </summary>
    /// <returns>消耗成功返回true，数量不足返回false。</returns>
    public bool UseLoveRecoverProp()
    {
        if (m_loveRecoverProps <= 0)
            return false;

        m_loveRecoverProps--;

        ES3.Save(m_key_love_recover_props, m_loveRecoverProps);
        NotifyCloudSaveDirty(MPCloudSaveDirtyReason.Assets);

        return true;
    }

    /// <summary>
    /// 获取当前生命恢复道具数量。
    /// </summary>
    /// <returns>生命恢复道具数量。</returns>
    public int GetLoveRecoverProps()
    {
        return m_loveRecoverProps;
    }
    #endregion
}

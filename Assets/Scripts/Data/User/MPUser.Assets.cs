using System;
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

    /// <summary>主页定时奖励下一次可领取的 UTC ticks。</summary>
    private string m_key_home_reward_ready_at_utc_ticks = "key_home_reward_ready_at_utc_ticks";


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

    /// <summary>主页定时奖励下一次可领取的 UTC ticks。</summary>
    private long m_homeRewardReadyAtUtcTicks;

    public const int HOME_REWARD_COIN_AMOUNT = 300;
    private const long HOME_REWARD_INTERVAL_TICKS = TimeSpan.TicksPerHour * 3;


    #endregion


    private void InitAssets()
    {
        m_coins = ES3.Load<int>(m_key_coins, 200);
        m_diamond = ES3.Load<int>(m_ket_diamond, 0);
        m_hintProps = ES3.Load<int>(m_key_hint_props, 0);
        m_loveRecoverProps = ES3.Load<int>(m_key_love_recover_props, 0);
        m_homeRewardReadyAtUtcTicks = ES3.Load<long>(m_key_home_reward_ready_at_utc_ticks, 0L);
        EnsureHomeRewardCountdown();
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

    /// <summary>获取主页定时奖励的剩余时间。</summary>
    public TimeSpan GetHomeRewardRemainingTime()
    {
        EnsureHomeRewardCountdown();
        long remainingTicks = Math.Max(0L, m_homeRewardReadyAtUtcTicks - DateTime.UtcNow.Ticks);
        return TimeSpan.FromTicks(remainingTicks);
    }

    /// <summary>
    /// 到期后领取 300 金币，并立即开始下一轮三小时倒计时。
    /// </summary>
    public bool TryClaimHomeReward()
    {
        return TryClaimHomeReward(out _);
    }

    public bool TryClaimHomeReward(out MPRewardReceipt receipt)
    {
        receipt = null;
        EnsureHomeRewardCountdown();
        long nowTicks = DateTime.UtcNow.Ticks;
        if (nowTicks < m_homeRewardReadyAtUtcTicks)
            return false;

        long nextReadyAt = nowTicks + HOME_REWARD_INTERVAL_TICKS;
        var result = new MPRewardReceipt
        {
            sourceId = "home_timed_reward",
            sourceName = "Timed reward",
            transactionId = "home_reward:" + m_homeRewardReadyAtUtcTicks,
            rewards = new List<MPRewardItem> { new MPRewardItem("coin", HOME_REWARD_COIN_AMOUNT) }
        };
        if (!TryCommitReward(result, null,
            file => file.Save(m_key_home_reward_ready_at_utc_ticks, nextReadyAt),
            () => m_homeRewardReadyAtUtcTicks = nextReadyAt, MPCloudSaveDirtyReason.Assets))
            return false;
        receipt = result;
        return true;
    }

    /// <summary>首次使用或读取到无效旧数据时，从当前时间开始一轮倒计时。</summary>
    private void EnsureHomeRewardCountdown()
    {
        if (IsValidHomeRewardReadyAtUtcTicks(m_homeRewardReadyAtUtcTicks))
            return;

        m_homeRewardReadyAtUtcTicks = DateTime.UtcNow.Ticks + HOME_REWARD_INTERVAL_TICKS;
        ES3.Save(m_key_home_reward_ready_at_utc_ticks, m_homeRewardReadyAtUtcTicks);
    }

    /// <summary>
    /// 判断持久化的 UTC 时间点是否有效。时间已经到期仍然是有效数据，不能重新计时。
    /// </summary>
    private static bool IsValidHomeRewardReadyAtUtcTicks(long ticks)
    {
        return ticks > 0L && ticks <= DateTime.MaxValue.Ticks;
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

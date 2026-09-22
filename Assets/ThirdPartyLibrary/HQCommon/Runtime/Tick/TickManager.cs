using System;
using System.Collections.Generic;
using UnityEngine;

namespace Joy
{
    public class TickManager : MonoSingleton<TickManager>
    {
        const int TICK_STEP = 66;
        const int TICK_ITEM_GROUP_NUM = 60;

        ulong m_Now;
        int m_TickIndex = 1;
        bool m_Inited = false;
        uint m_DefaultInterval;

        // ontick调度中分散tick遍历压力的容器列表
        List<TickItem> m_TickItemBack = new List<TickItem>();
        List<TickItem>[] m_TickItemGroup = new List<TickItem>[TICK_ITEM_GROUP_NUM];

        // ontick中调度updateTick的容器列表
        List<UpdateTickItem> m_UpdateTickList1 = new List<UpdateTickItem>();
        List<UpdateTickItem> m_UpdateTickList2 = new List<UpdateTickItem>();

        // tickItem存储容器
        Dictionary<int, TickItemBase> m_TickItems = new Dictionary<int, TickItemBase>();

        void Awake()
        {
            Init(Convert.ToUInt32(Time.fixedDeltaTime * 1000f));
        }

        void Update()
        {
            OnTick(Convert.ToUInt32(Time.deltaTime * 1000f));
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="interval">Fixed默认调用间隔毫秒</param>
        void Init(uint interval)
        {
            if (m_Inited)
            {
                return;
            }

            m_Now = 0;
            m_Inited = true;
            m_DefaultInterval = interval;
            for (int i = 0; i < m_TickItemGroup.Length; i++)
            {
                m_TickItemGroup[i] = new List<TickItem>(4);
            }
        }

        int GetTickID()
        {
            m_TickIndex++;
            if (m_TickIndex >= int.MaxValue)
            {
                m_TickIndex = 1;
            }
            return m_TickIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deltaTime">Tick间隔时间毫秒</param>
        void OnTick(uint deltaTime)
        {
            if (!m_Inited)
            {
                return;
            }

            ulong tickEnd = m_Now + deltaTime;
            List<UpdateTickItem> list = m_UpdateTickList1;
            m_UpdateTickList1 = m_UpdateTickList2;
            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                UpdateTickItem tick = list[i];
                if (tick.Valid)
                {
                    tick.OnTick(deltaTime, tickEnd);
                    if (tick.Valid)
                    {
                        m_UpdateTickList1.Add(tick);
                    }
                }
            }
            list.Clear();
            m_UpdateTickList2 = list;

            int nowIndex = -1;
            for (uint i = 1; i <= deltaTime; i++)
            {
                // 按间隔累进触发
                int index = GetTickGroupIdx(m_Now + i);
                if (nowIndex == index)
                    continue;
                nowIndex = index;
                TickStep(nowIndex, tickEnd, deltaTime);
            }

            m_Now = tickEnd;
        }

        int GetTickGroupIdx(ulong tick)
        {
            return (int)((tick / TICK_STEP) % TICK_ITEM_GROUP_NUM);
        }

        int Cascade(List<TickItem>[] vec, int index)
        {
            List<TickItem> list = vec[index];
            vec[index] = new List<TickItem>(4);
            foreach (TickItem tick in list)
            {
                if (tick.Valid)
                {
                    RegisterInternal(tick);
                }
            }
            return index;
        }

        void TickStep(int index, ulong tickEnd, uint deltaTime)
        {
            List<TickItem> list = m_TickItemGroup[index];
            if (list.Count == 0)
            {
                return;
            }
            m_TickItemGroup[index] = m_TickItemBack;

            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                TickItem tick = list[i];
                if (tick.Valid)
                {
                    if (tickEnd >= tick.NextTickTime)
                    {
                        tick.OnTick(deltaTime, tickEnd);
                    }

                    if (tick.Valid)
                    {
                        RegisterInternal(tick);
                    }
                }
            }
            list.Clear();
            m_TickItemBack = list;
        }

        void RegisterInternal(TickItem tick)
        {
            int index = GetTickGroupIdx(tick.NextTickTime);
            List<TickItem> list = m_TickItemGroup[index];
            list.Add(tick);
        }

        /// <summary>
        /// 注销固定间隔的tick
        /// </summary>
        /// <param name="tickID"></param>
        public void RemoveFixedTimeTick(int tickID)
        {
            m_TickItems.TryGetValue(tickID, out var tick);
            if (!(tick is TickItem))
            {
                Joy.Debug.LogError($"RemoveTick Error: cant get tickItem [{tickID}]");
                return;
            }

            var tickItem = (TickItem)tick;
            tickItem.TickAction = null;
            tickItem.Valid = false;
            m_TickItems.Remove(tickID);
        }

        /// <summary>
        /// 注册默认间隔的Tick
        /// </summary>
        /// <param name="tickFun">tick处理函数</param>
        /// <param name="nType">tick类型</param>
        /// <returns>tickId</returns>
        public int RegisterFixedTimeTick(FixedTimeTickHandler tickFun, TickType nType = TickType.Loop)
        {
            return RegisterFixedTimeTick(tickFun, m_DefaultInterval, nType);
        }

        /// <summary>
        /// 注册固定间隔的Tick
        /// </summary>
        /// <param name="tickFun">tick处理函数</param>
        /// <param name="interval">间隔</param>
        /// <param name="nType">tick类型</param>
        /// <returns>tickId</returns>
        public int RegisterFixedTimeTick(FixedTimeTickHandler tickFun, uint interval, TickType nType = TickType.Loop)
        {
            int tickID = GetTickID();
            TickItem tick = new TickItem(tickID, tickFun, interval, m_Now + interval, nType);
            m_TickItems[tickID] = tick;
            RegisterInternal(tick);
            return tickID;
        }

        /// <summary>
        /// 注销每帧调用的tick
        /// </summary>
        /// <param name="tickID"></param>
        public void RemoveUpdateTick(int tickID)
        {
            m_TickItems.TryGetValue(tickID, out var tick);
            if (!(tick is UpdateTickItem))
            {
                Joy.Debug.LogError($"RemoveUpdateTick Error: cant get tickItem [{tickID}]");
                return;
            }

            var updateTickItem = (UpdateTickItem)tick;
            updateTickItem.TickAction = null;
            updateTickItem.Valid = false;
            m_TickItems.Remove(tickID);
        }

        /// <summary>
        /// 注册每帧调用的tick
        /// </summary>
        /// <param name="tickAction">tick处理函数</param>
        /// <param name="nType">tick类型</param>
        /// <returns>tickId</returns>
        public int RegisterUpdateTick(UpdateTickHandler tickAction, TickType nType)
        {
            int tickID = GetTickID();
            var tick = new UpdateTickItem(tickID, tickAction, nType);
            m_UpdateTickList1.Add(tick);
            m_TickItems[tickID] = tick;
            return tickID;
        }
    }
}

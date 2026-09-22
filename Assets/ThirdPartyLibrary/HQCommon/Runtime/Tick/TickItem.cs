using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Joy
{
    /// <summary>
    /// Tick类型
    /// </summary>
    public enum TickType
    {
        Loop = 1,
        Once = 2
    }

    /// <summary>
    /// 帧回调
    /// </summary>
    public delegate void UpdateTickHandler(uint deltaTime, int tickID);

    /// <summary>
    /// 固定时间回调
    /// </summary>
    public delegate void FixedTimeTickHandler(int tickID);       

    internal abstract class TickItemBase
    {
        public int ID { get; set; }
        public bool Valid { get; set; }
        public TickType Type { get; set; }

        public virtual void OnTick(uint deltaTime, ulong nowTick)
        {
        }
    }

    /// <summary>
    /// Tick单位
    /// </summary>
    internal class TickItem : TickItemBase
    {
        public uint Interval { get; set; }
        public ulong NextTickTime { get; set; }
        public FixedTimeTickHandler TickAction { get; set; }

        public TickItem(int id, FixedTimeTickHandler action, uint interval, ulong expire, TickType type)
        {
            ID = id;
            Type = type;
            Valid = true;
            Interval = interval;
            TickAction = action;
            NextTickTime = expire;
        }

        public override void OnTick(uint deltaTime, ulong nowTick)
        {
            try
            {
                TickAction.Invoke(ID);
            }
            catch (Exception e)
            {
                Joy.Debug.LogException(e);
            }

            NextTickTime += Interval;
            if (NextTickTime < nowTick)
            {
                // 追帧
                NextTickTime = nowTick + 1;
            }

            if (Type == TickType.Once)
            {
                TickAction = null;
                Valid = false;
            }
        }
    }

    /// <summary>
    /// UpdateTick单位
    /// </summary>
    internal class UpdateTickItem : TickItemBase
    {
        public UpdateTickHandler TickAction { get; set; }

        public UpdateTickItem(int id, UpdateTickHandler action, TickType type)
        {
            ID = id;
            Type = type;
            Valid = true;
            TickAction = action;
        }

        public override void OnTick(uint deltaTime, ulong nowTick)
        {
            try
            {
                TickAction.Invoke(deltaTime, ID);
            }
            catch (Exception e)
            {
                Joy.Debug.LogException(e);
            }

            if (Type == TickType.Once)
            {
                Valid = false;
                TickAction = null;
            }
        }
    }
}

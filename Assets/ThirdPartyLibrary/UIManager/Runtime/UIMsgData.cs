using System;
using System.Collections.Generic;
using System.Linq;

namespace HQ.UIManager
{
    /// <summary>
    /// uimsgdata基类
    /// </summary>
    abstract public class UIMsgData
    {
        /// <summary>
        /// 获取msg实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetMsg<T>() where T : UIMsgData
        {
            return this as T;
        }
    }

    /// <summary>
    /// 通用的消息传递对象 !!复杂对象请不要使用此对象
    /// </summary>
    public class UIMsgDataGeneric : UIMsgData
    {
        public object Arg1 { get; set; }
        public object Arg2 { get; set; }
        public object Arg3 { get; set; }

        public UIMsgDataGeneric(object Arg1)
        {
            this.Arg1 = Arg1;
        }

        public UIMsgDataGeneric(object Arg1, object Arg2)
        {
            this.Arg1 = Arg1;
            this.Arg2 = Arg2;
        }

        public UIMsgDataGeneric(object Arg1, object Arg2, object Arg3)
        {
            this.Arg1 = Arg1;
            this.Arg2 = Arg2;
            this.Arg3 = Arg3;
        }
    }
}
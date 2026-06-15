using System;

namespace HQ.UIManager
{
    /// <summary>
    /// 组件属性
    /// </summary>
    public class ComponentAttribute : Attribute
    {
        /// <summary>
        /// 路径
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// 加载方式
        /// </summary>
        public bool IsAsyncLoad { get; private set; }
        public ComponentAttribute(string path, bool isAsyncLoad = false)
        {
            this.Path = path;
            this.IsAsyncLoad = isAsyncLoad;
        }
    }
}
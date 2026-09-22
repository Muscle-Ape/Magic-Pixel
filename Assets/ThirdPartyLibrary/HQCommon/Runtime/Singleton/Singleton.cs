/*
 * @Description: 单例类
 * @Author: SeanZhu 
 * @Date: 2017-10-24 09:39:49 
 * @Last Modified by: SeanZhu
 * @Last Modified time: 2017-10-30 10:23:53
 * @LastModifiedInfo: 将锁对象改为只读，防止修改
 */
namespace HQ.Singleton
{
    public class Singleton<T> where T : new()
    {
        private static readonly object _lock = new object();
        private static T _instance;

        public static T Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance != null ? _instance : _instance = new T();
                }
            }
        }
    }
}

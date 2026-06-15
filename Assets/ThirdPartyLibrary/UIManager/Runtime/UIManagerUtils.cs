using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;
using Object = UnityEngine.Object;

namespace HQ.UIManager
{
    static public partial class UIManagerUtils
    {
        #region 组件初始化

        /// <summary>
        /// 组件类缓存
        /// </summary>
        public class ComponentClassCache
        {
            public FieldInfo[] FieldInfos;
            public PropertyInfo[] PropertyInfos;
            public MethodInfo[] MethodInfos;
        }

        /// <summary>
        /// Component 类数据缓存
        /// </summary>
        static Dictionary<string, ComponentClassCache> ComponentClassCacheMap =
            new Dictionary<string, ComponentClassCache>();

        static public void InitComponent(AComponent component)
        {
            var comType = component.GetType();

            ComponentClassCache classCache = null;
#if !UNITY_EDITOR //编辑器模式下使用缓存
            //缓存各种Component的class数据
            if (!ComponentClassCacheMap.TryGetValue(comType.FullName, out classCache))
#endif
            {
                var fields = comType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var properties =
                    comType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var methodes = comType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                //筛选有属性的,且为自动赋值的
                classCache = new ComponentClassCache();
                classCache.FieldInfos = fields.Where((f) =>
                    {
                        var attrs = f.GetCustomAttributes(false);
                        for (int i = 0; i < attrs.Length; i++)
                        {
                            if (attrs[i] is AutoInitComponentAttribute)
                                return true;
                        }
                        return false;
                    })
                    .ToArray();
                classCache.PropertyInfos = properties.Where((p) =>
                    {
                        var attrs = p.GetCustomAttributes(false);
                        for (int i = 0; i < attrs.Length; i++)
                        {
                            if (attrs[i] is AutoInitComponentAttribute)
                                return true;
                        }
                        return false;
                    })
                    .ToArray();
                classCache.MethodInfos = methodes.Where((m) =>
                    {
                        var attrs = m.GetCustomAttributes(false);
                        for (int i = 0; i < attrs.Length; i++)
                        {
                            if (attrs[i] is AutoInitComponentAttribute)
                                return true;
                        }
                        return false;
                    })
                    .ToArray();
#if !UNITY_EDITOR //编辑器模式下使用缓存
                //缓存cls data
                ComponentClassCacheMap[comType.FullName] = classCache;
#endif

            }

            //开始赋值逻辑
            foreach (var f in classCache.FieldInfos)
            {
                var attrs = f.GetCustomAttributes(false);
                for (int i = 0; i < attrs.Length; i++)
                {
                    (attrs[i] as AutoInitComponentAttribute)?.AutoSetField(component, f);
                }
            }

            foreach (var p in classCache.PropertyInfos)
            {
                var attrs = p.GetCustomAttributes(false);
                for (int i = 0; i < attrs.Length; i++)
                {
                    (attrs[i] as AutoInitComponentAttribute)?.AutoSetProperty(component, p);
                }
            }

            foreach (var m in classCache.MethodInfos)
            {
                var attrs = m.GetCustomAttributes(false);
                for (int i = 0; i < attrs.Length; i++)
                {
                    (attrs[i] as AutoInitComponentAttribute)?.AutoSetMethod(component, m);
                }
            }
        }

        #endregion


        #region log

        static public void LogDev(object message)
        {
#if hq_log
            Log.Dev($"[UI] {message}");
#else
            // Debug.Log("[UI] " + message);
#endif
        }

        static public void LogInfo(object message)
        {
#if hq_log
            Log.Info($"[UI] {message}");
#else
            Debug.Log("[UI] " + message);
#endif
        }

        static public void LogError(object message)
        {
            Debug.LogError($"[UI] {message}");
#if hq_log
            Log.Error($"[UI] {message}");
#else
            Debug.Log("[UI] " + message);
#endif
        }

        #endregion
    }
}
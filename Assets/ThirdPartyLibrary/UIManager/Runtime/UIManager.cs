using System;
using System.Collections.Generic;
// using HQ.DataListener;
using UnityEngine;
// using HQ.UFlux.WindowStatus;
using System.Linq;

namespace HQ.UIManager
{
    public enum UILayer
    {
        Bottom = 0,
        Center,
        Top
    }

    public class UIManager
    {
        static private UIManager _inst;
        static public UIManager Inst
        {
            get
            {
                if (_inst == null)
                {
                    _inst = new UIManager();
                }

                return _inst;
            }
        }

        public IAssetLoader AssetLoader;

        /// <summary>
        /// ui的三个层级
        /// </summary>
        private Transform Bottom, Center, Top;

        static private int MAX_HISTORY_NUM = 50;

        // /// <summary>
        // /// 加载窗口
        // /// </summary>
        // /// <param name="uiIndexs">窗口枚举</param>
        // // public void LoadWindow(Enum uiIndex)

        /// <summary>
        /// 历史列表
        /// </summary>
        public List<AWindow> HistoryList { get; private set; } = new List<AWindow>(MAX_HISTORY_NUM);
        public UIAnimManager UIAnimManager {get; private set;}

        public void Init()
        {
            //初始化
            // windowMap = new Dictionary<int, IWindow>();
            var uiroot = GameObject.Find("UIRoot")?.transform;
            if (uiroot)
            {
                Bottom = uiroot.Find("Bottom")?.transform;
                Center = uiroot.Find("Center")?.transform;
                Top = uiroot.Find("Top")?.transform;
                //不删除uiroot
                if (Application.isPlaying)
                {
                    GameObject.DontDestroyOnLoad(uiroot.gameObject);
                }
                UIAnimManager = uiroot.gameObject.AddComponent<UIAnimManager>();
            }
            else
            {
                UIManagerUtils.LogError("UIRoot 不存在");
                //手动创建
            }


            //默认使用Resources.Load,开启yoo_asset的情况下使用YooAsset
            //或者自定义加载方式
#if yoo_asset
            AssetLoader = new YooAssetLoader();
#else
            AssetLoader = new ResourcesAssetLoader();
#endif
        }

        public GameObject LoadComponent<T>() where T : AComponent
        {
            var t = typeof(T);
            var attr = t.GetAttributeInILRuntime<ComponentAttribute>();
            if (attr == null)
            {
                UIManagerUtils.LogError("ComponentAttribute 不存在");
                return null;
            }


            var go = AssetLoader.Load<GameObject>(attr.Path);
            return go;
        }

        public T ShowComponent<T>(Transform parent, UIMsgData uiMsgData = null) where T : AComponent
        {
            Type type = typeof(T);

            UIManagerUtils.LogInfo($"ShowComponent:{type.Name}");

            //创建ui
            var prefab = LoadComponent<T>();

            if (prefab == null)
            {
                return null;
            }
            else
            {
                GameObject go = GameObject.Instantiate(prefab, parent);
                T component = go.AddComponent<T>();
                component.LoadUIMsgData(uiMsgData);
                return component;
            }
        }

        public T ShowComponent<T>(GameObject prefab, Transform parent, UIMsgData uiMsgData = null) where T : AComponent
        {
            if (prefab == null)
            {
                return null;
            }
            else
            {
                GameObject go = GameObject.Instantiate(prefab, parent);
                T component = go.AddComponent<T>();
                component.LoadUIMsgData(uiMsgData);
                return component;
            }
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <param name="index"></param>
        /// <param name="uiMsgData"></param>
        /// <param name="layer"></param>
        /// // private void ShowWindow(int uiIdx, UIMsgData uiMsgData = null, bool resetMask = true, UILayer layer = UILayer.Bottom, bool isAddToHistory = true)
        public T ShowWindow<T>(UIMsgData uiMsgData = null, bool displayLastWindow = false, UILayer layer = UILayer.Bottom) where T : AWindow
        {
            Type type = typeof(T);

            UIManagerUtils.LogInfo($"ShowWindow:{type.Name}");

            //创建ui
            var prefab = LoadComponent<T>();

            if (prefab == null)
            {
                UIManagerUtils.LogError($"Window:{type.Name} Prefab不存在");
                return null;
            }
            else
            {
                GameObject go = null;
                switch (layer)
                {
                    case UILayer.Bottom:
                        go = GameObject.Instantiate(prefab, this.Bottom);
                        break;
                    case UILayer.Center:
                        go = GameObject.Instantiate(prefab, this.Center);
                        break;
                    case UILayer.Top:
                        go = GameObject.Instantiate(prefab, this.Top);
                        break;
                }
                go.SetActive(false);//在AddToHistory中启用

                AWindow window = go.AddComponent<T>();
                window.transform.SetAsLastSibling();
                if (window is IViewEnterAnimation)
                {
                    var lastWindow = HistoryList.LastOrDefault();
                    AddToHistory(window, true);
                    UIAnimManager.DoViewEnterAnimation(window, lastWindow, displayLastWindow);
                }
                else
                {
                    AddToHistory(window, displayLastWindow);
                }
                window.LoadUIMsgData(uiMsgData);
                return window as T;
            }
        }

        public T ShowSubWindow<T>(AWindow parent, UIMsgData uiMsgData = null, bool displayLastWindow = true) where T : AWindow
        {
            Type type = typeof(T);

            UIManagerUtils.LogInfo($"ShowWindow:{type.Name}");

            //创建ui
            var prefab = LoadComponent<T>();

            if (prefab == null)
            {
                return null;
            }
            else
            {
                GameObject go = GameObject.Instantiate(prefab, parent.transform);
                go.SetActive(false);//在AddToHistory中启用
                AWindow window = go.AddComponent<T>();
                window.transform.SetAsLastSibling();
                window.Parent = parent;
                window.LoadUIMsgData(uiMsgData);
                parent.LostFocus(displayLastWindow);
                window.GetFocus();
                return window as T;
            }
        }

        public AWindow GetWindowBehindWindow(AWindow window)
        {
            for (int i = 0; i < HistoryList.Count; i++)
            {
                if (HistoryList[i] == window && i > 0)
                {
                    return HistoryList[i-1];
                }
            }
            return null;
        }

        /// <summary>
        /// 销毁指定窗口
        /// </summary>
        public void DestroyWindow(AWindow window)
        {

            if (window.Parent != null)//Parent不为空，则为SubWindow
            {
                DestroySubWindow(window);
            }
            else
            {
                RemoveHistory(window);
            }

            window.Destroy();
        }

        /// <summary>
        /// 销毁所有某一个类窗口
        /// </summary>
        public void DestroyWindow<T>() where T : AWindow
        {
            int len = HistoryList.Count;
            List<AWindow> removeWindows = new List<AWindow>();
            for (int i = len - 1; i >= 0; i--)
            {
                if (HistoryList[i] is T)
                {
                    removeWindows.Add(HistoryList[i]);
                }
            }

            foreach (var window in removeWindows)
            {
                if (window.IsFocus)//如果当前拥有焦点，则失去焦点
                {
                    window.LostFocus();
                }
                HistoryList.Remove(window);
                window.Destroy();
            }

            AWindow lastWindow = HistoryList.LastOrDefault();
            if (lastWindow != null && !lastWindow.IsFocus)
            {
                lastWindow.GetFocus();
            }
        }

        public void DestroySubWindow(AWindow window)
        {
            window.LostFocus();
            window.Parent.GetFocus();
        }

        /// <summary>
        /// 添加到历史列表
        /// </summary>
        private void AddToHistory(AWindow window, bool displayLastWindow)
        {
            if (HistoryList.Count == MAX_HISTORY_NUM)
            {
                HistoryList.RemoveAt(0);
            }

            AWindow lastWindow = HistoryList.LastOrDefault();
            if (lastWindow != null)
            {
                lastWindow.LostFocus(displayLastWindow);
            }

            window.GetFocus();
            HistoryList.Add(window);
        }

        /// <summary>
        /// 从历史列表中移除一个窗口
        /// </summary>
        private void RemoveHistory(AWindow window)
        {
            if (window.IsFocus)//如果当前拥有焦点，则失去焦点
            {
                window.LostFocus();
            }

            int index = HistoryList.IndexOf(window);
            HistoryList.Remove(window);//从历史列表中移除窗口
            if (HistoryList.Count == 0)
            {
                return;
            }
            else if (HistoryList.Count == index) //移除后长度相同，说明是最后一个
            {
                HistoryList.LastOrDefault().GetFocus();
            }
        }

        /// <summary>
        /// 从历史列表中移除一类窗口
        /// </summary>
        private void RemoveHistory<T>() where T : AWindow
        {

        }
    }
}


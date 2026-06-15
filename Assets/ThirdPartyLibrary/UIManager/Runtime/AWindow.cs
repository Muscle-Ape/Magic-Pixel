using UnityEngine;
using System.Collections.Generic;

namespace HQ.UIManager
{
    /// <summary>
    /// 窗口基类
    /// </summary>
    public class AWindow : AComponent
    {
        /// <summary>
        /// 父窗口(有此数据说明是子窗口)
        /// </summary>
        public AWindow Parent;
        public RectTransform ViewRT => _viewRT;

        public bool IsFocus { get; private set; }

        public virtual void OnFocus(bool focus) { }

        private RectTransform _viewRT;

        void Awake()
        {
            SelfRT = (RectTransform)transform;
            CheckRootNodeSettings();//检查Root节点
            CheckViewNodeExist();//检查View节点
            AdaptToNotchScreen();//适配刘海屏
            AttributesAutoBinding();//自动绑定属性
        }

        void AdaptToNotchScreen()
        {
            if (IsNotchScreen() && ShouldAdaptToNotchScreen())
            {
                float safeAreaOffsetTop = (Screen.height - Screen.safeArea.yMax);
                float offset = (1080.0f / (float)Screen.height) * safeAreaOffsetTop;
                // _viewRT.offsetMax -= new Vector2(0, 34 * (1080.0f / 414.0f));
                _viewRT.offsetMax -= new Vector2(0, offset);
            }
        }

        /// <summary>
        /// 是否需要适配刘海屏(默认开启,仅在刘海屏下有效)
        /// </summary>
        protected virtual bool ShouldAdaptToNotchScreen()
        {
            return true;
        }

        private void CheckRootNodeSettings()
        {
            if (SelfRT.offsetMin != Vector2.zero || SelfRT.offsetMax != Vector2.zero)
            {
                UIManagerUtils.LogError($"{this.GetType().Name} RectTransform 中 offsetMin 或 offsetMax 设置错误");
            }


            if (SelfRT.anchorMin != Vector2.zero || SelfRT.anchorMax != Vector2.one)
            {
                UIManagerUtils.LogError($"{this.GetType().Name} RectTransform 中 anchorMin 或 anchorMax 设置错误");
            }
        }
        private void CheckViewNodeExist()
        {
            // 在指定的根节点下查找名为 "View" 的节点
            Transform viewNode = SelfRT.Find("View");
            if (viewNode != null && viewNode.parent == SelfRT)
            {
                _viewRT = (RectTransform)viewNode;
            }
            else
            {
                UIManagerUtils.LogError("[View] 组件不存在");
            }
        }

        /// <summary>
        /// 判断是否是缺口屏
        /// </summary>
        /// <returns></returns>
        public static bool IsNotchScreen()
        {
            bool isNotchScreen = false;
#if !UNITY_EDITOR
            float safeAreaOffsetTop = (Screen.height - Screen.safeArea.yMax);
            if (safeAreaOffsetTop > 0)
            {
                isNotchScreen = true;
            }
#elif UNITY_EDITOR
            float s = (float)(Screen.height) / (float)(Screen.width);
            if (s > 2)
            {
                isNotchScreen = true;
            }
#endif
            return isNotchScreen;
        }

        private void SetFocus(bool focus)
        {
            IsFocus = focus;
            OnFocus(focus);
        }

        public void DestroyWindow()
        {
            if (this != null && m_bIsDestoried == false)
            {
                UIManager.Inst.DestroyWindow(this);
            }
        }


        /// <summary>
        /// 获得焦点
        /// </summary>
        public void GetFocus()
        {
            UIManagerUtils.LogInfo($"GetFocus:{this.GetType().Name}");
            this.gameObject.SetActive(true);
            SetFocus(true);
        }

        /// <summary>
        /// 失去焦点
        /// </summary>
        public void LostFocus(bool windowActive = false)
        {
            UIManagerUtils.LogInfo($"LostFocus:{this.GetType().Name}");
            SetFocus(false);
            this.gameObject.SetActive(windowActive);
        }

        /// <summary>
        /// 注册窗口
        /// </summary>
        /// <param name="subwin"></param>
        /// <param name="enum"></param>
        public T OpenSubWindow<T>(UIMsgData uiMsgData = null, bool hideLastWindow = true) where T : AWindow
        {
            return UIManager.Inst.ShowSubWindow<T>(this, uiMsgData, hideLastWindow);
        }

    }
}
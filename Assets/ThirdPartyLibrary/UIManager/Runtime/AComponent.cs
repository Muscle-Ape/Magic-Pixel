using UnityEngine;
using System;
using System.Reflection;
using System.Collections;


#if yoo_asset
using YooAsset;
#endif

namespace HQ.UIManager
{
    public abstract class AComponent : MonoBehaviour
    {
        /// <summary>
        /// 自身RectTransform
        /// </summary>
        public RectTransform SelfRT;

        // public HQ.EventCenter.EventCenterListener Listener = new EventCenter.EventCenterListener();

        private string resPath = null;
        void Awake()
        {
            SelfRT = (RectTransform)transform;
            AttributesAutoBinding();
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="uiMsg"></param>
        public virtual void LoadUIMsgData(UIMsgData uiMsg) { }

        public virtual void OnCreate() { }

        public virtual void OnRelease() { }


        /// <summary>
        /// 属性自动绑定
        /// </summary>
        protected void AttributesAutoBinding()
        {
            var t = this.GetType();
            var attr = t.GetAttributeInILRuntime<ComponentAttribute>();
            this.resPath = attr.Path;

            UIManagerUtils.InitComponent(this);
#if UNITY_EDITOR
            OnCreate();
#else
            try
            {
                //初始化
                OnCreate();
            }
            catch (Exception e)
            {
                UIManagerUtils.LogError($"OnCreate: {e}");
            }
#endif

        }

        protected bool m_bIsDestoried = false;
        public bool IsDestoried {get {return m_bIsDestoried;}}
        /// <summary>
        /// 销毁
        /// </summary>
        virtual public void Destroy()
        {
            if (this != null && m_bIsDestoried == false)
            {
                m_bIsDestoried = true;
                UIManagerUtils.LogInfo($"Destroy: {this.GetType().Name}");
                try
                {
                    //销毁
                    OnRelease();
                }
                catch (Exception e)
                {
                    UIManagerUtils.LogError($"OnRelease: {e}");
                }
                // HQ.EventCenter.EventCenter.RemoveListener(Listener);
                GameObject.Destroy(this.gameObject);
            }
        }

        IEnumerator OnAnimDestroyWindow()
        {
            var view = this as IViewExitAnimation;
            if (view != null)
            {
                yield return view.OnPlayExitAnimation();
            }
            GameObject.Destroy(this.gameObject);
        }

        /// <summary>
        /// 资源加载
        /// </summary>
        /// <param name="location">资源路径</param>
        public T LoadAssetSync<T>(string location) where T : UnityEngine.Object
        {
#if yoo_asset
            var handle = YooAssets.LoadAssetSync<T>(location);
            return handle.AssetObject as T;
#else
            return Resources.Load<T>(location);
#endif
        }
    }
}
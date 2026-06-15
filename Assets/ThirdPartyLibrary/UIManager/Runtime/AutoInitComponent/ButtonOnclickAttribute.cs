using System;
using System.Reflection;
using UnityEngine.UI;
using System.Collections;
using UnityEngine;
using System.Threading.Tasks;

namespace HQ.UIManager
{
    /// <summary>
    /// 自动初始化，按钮点击注册属性
    /// </summary>
    public class ButtonOnclickAttribute : AutoInitComponentAttribute
    {
        private string path;

        private bool isTriggerThisOnly = false;
        private bool disableMultipleClick = true;
        public ButtonOnclickAttribute(string path, bool isTriggerThisOnly = true, bool disableMultipleClick = true)
        {
            this.path = path;
            this.isTriggerThisOnly = isTriggerThisOnly;
            this.disableMultipleClick = disableMultipleClick;
        }

        public override void AutoSetMethod(AComponent com, MethodInfo methodInfo)
        {
            var btn = com.transform.Find(this.path)?.GetComponent<Button>();
            if (btn)
            {
                if (isTriggerThisOnly)
                {
                    btn.onClick.RemoveAllListeners();
                }
                btn.onClick.AddListener(async () =>
                {
                    //触发按钮事件
                    methodInfo.Invoke(com, new object[] { });
                    if (disableMultipleClick && btn.gameObject.activeInHierarchy)
                    {
                        btn.enabled = false;
                        await EnableButton(com, btn);
                    }
                });
            }
            else
            {
                UIManagerUtils.LogError("未找到Btn:" + this.path);
            }
        }

        async Task EnableButton(AComponent com, Button btn)
        {
            await Task.Delay(600);
            if (!com.IsDestoried && btn != null && btn.gameObject != null)
            {
                btn.enabled = true;
            }
        }
    }
}
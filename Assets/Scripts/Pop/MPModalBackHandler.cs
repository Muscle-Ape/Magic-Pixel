using HQ.UIManager;
using UnityEngine;
using UnityEngine.UI;

/// <summary>把系统返回键映射到预制体中指定的安全按钮；阻断弹窗不挂此组件。</summary>
[DisallowMultipleComponent]
public sealed class MPModalBackHandler : MonoBehaviour
{
    [SerializeField] private Button m_backButton;
    private AWindow m_window;

    private void Start()
    {
        // AWindow 由 UIManager 在实例化之后动态挂载，因此不在 Awake 获取。
        m_window = GetComponent<AWindow>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && m_window != null && m_window.IsFocus && !m_window.IsDestoried
            && m_backButton != null && m_backButton.isActiveAndEnabled && m_backButton.IsInteractable())
            m_backButton.onClick.Invoke();
    }
}

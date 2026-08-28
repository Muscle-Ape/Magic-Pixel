using System;
using HQ.UIManager;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 账号绑定冲突确认弹窗。
/// 当第三方账号已经绑定到其他 Unity PlayerId 时，用它让用户选择取消或继续冲突处理。
/// </summary>
[Component("MPAccountConflictPop")]
public class MPAccountConflictPop : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>弹窗标题文本。</summary>
    [TransformPath("View/Window/Title")]
    private TMP_Text m_titleText;

    /// <summary>冲突说明文本。</summary>
    [TransformPath("View/Window/Desc")]
    private TMP_Text m_descText;

    /// <summary>取消按钮。</summary>
    [TransformPath("View/Window/CancelBtn")]
    private Button m_cancelBtn;

    /// <summary>确认继续处理按钮。</summary>
    [TransformPath("View/Window/ConfirmBtn")]
    private Button m_confirmBtn;

    /// <summary>取消后的外部回调。</summary>
    private Action m_onCancel;

    /// <summary>确认继续处理后的外部回调。</summary>
    private Action<MPAccountConflictData> m_onConfirm;

    /// <summary>当前冲突数据。</summary>
    private MPAccountConflictData m_conflictData;

    /// <summary>
    /// 注册按钮事件。
    /// </summary>
    public override void OnCreate()
    {
        RegisterButton(m_cancelBtn, OnCancelClick);
        RegisterButton(m_confirmBtn, OnConfirmClick);
    }

    /// <summary>
    /// 接收冲突数据和按钮回调。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPAccountConflictPopUIMsgData data = uiMsg == null ? null : uiMsg.GetMsg<MPAccountConflictPopUIMsgData>();
        if (data != null)
        {
            m_conflictData = data.ConflictData;
            m_onCancel = data.OnCancel;
            m_onConfirm = data.OnConfirm;
            SetTitle(data.Title);
            SetDesc(data.Description);
            return;
        }

        SetTitle("账号冲突");
        SetDesc("当前第三方账号已经绑定到其他账号，请确认后再继续。");
    }

    /// <summary>
    /// 清理按钮事件。
    /// </summary>
    public override void OnRelease()
    {
        UnregisterButton(m_cancelBtn, OnCancelClick);
        UnregisterButton(m_confirmBtn, OnConfirmClick);
    }

    /// <summary>
    /// 取消冲突处理。
    /// </summary>
    private void OnCancelClick()
    {
        Action callback = m_onCancel;
        DestroyWindow();
        callback?.Invoke();
    }

    /// <summary>
    /// 确认继续冲突处理。
    /// </summary>
    private void OnConfirmClick()
    {
        Action<MPAccountConflictData> callback = m_onConfirm;
        MPAccountConflictData conflictData = m_conflictData;
        DestroyWindow();
        callback?.Invoke(conflictData);
    }

    /// <summary>
    /// 设置弹窗标题。
    /// </summary>
    private void SetTitle(string title)
    {
        if (m_titleText != null)
        {
            m_titleText.text = string.IsNullOrEmpty(title) ? "账号冲突" : title;
        }
    }

    /// <summary>
    /// 设置冲突说明。
    /// </summary>
    private void SetDesc(string desc)
    {
        if (m_descText != null)
        {
            m_descText.text = string.IsNullOrEmpty(desc) ? "当前第三方账号已经绑定到其他账号，请确认后再继续。" : desc;
        }
    }

    /// <summary>
    /// 注册按钮事件。
    /// </summary>
    private static void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 移除按钮事件。
    /// </summary>
    private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}

/// <summary>
/// 账号冲突确认弹窗打开参数。
/// </summary>
public sealed class MPAccountConflictPopUIMsgData : UIMsgData
{
    /// <summary>弹窗标题。</summary>
    public string Title { get; private set; }

    /// <summary>冲突说明。</summary>
    public string Description { get; private set; }

    /// <summary>账号冲突数据。</summary>
    public MPAccountConflictData ConflictData { get; private set; }

    /// <summary>取消后的回调。</summary>
    public Action OnCancel { get; private set; }

    /// <summary>确认后的回调。</summary>
    public Action<MPAccountConflictData> OnConfirm { get; private set; }

    public MPAccountConflictPopUIMsgData(
        string title,
        string description,
        MPAccountConflictData conflictData,
        Action onCancel,
        Action<MPAccountConflictData> onConfirm)
    {
        Title = title;
        Description = description;
        ConflictData = conflictData;
        OnCancel = onCancel;
        OnConfirm = onConfirm;
    }
}

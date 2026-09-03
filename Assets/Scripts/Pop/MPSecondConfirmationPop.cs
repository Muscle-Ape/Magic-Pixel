using System;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>高风险操作只在明确确认后执行，失败保持弹窗和原始数据，允许重试。</summary>
[Component("MPSecondConfirmationPop")]
public sealed class MPSecondConfirmationPop : AWindow
{
    [TransformPath("View/Window/Title")] private TMP_Text m_title;
    [TransformPath("View/Window/Desc")] private TMP_Text m_description;
    [TransformPath("View/Window/Status")] private TMP_Text m_status;
    [TransformPath("View/Window/CancelBtn")] private Button m_cancelButton;
    [TransformPath("View/Window/ConfirmBtn")] private Button m_confirmButton;

    private MPSecondConfirmationPopUIMsgData m_data;
    private CancellationTokenSource m_cancellation;
    private bool m_busy;
    private bool m_decided;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static MPSecondConfirmationPop Show(string title, string description, string confirmText,
        Func<CancellationToken, Task<bool>> confirmAsync, Action onCancel = null, string cancelText = "Cancel",
        Action onConfirmed = null)
    {
        return UIManager.Inst.ShowWindow<MPSecondConfirmationPop>(
            new MPSecondConfirmationPopUIMsgData(title, description, confirmText, confirmAsync, onCancel, cancelText, onConfirmed),
            true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_cancelButton.onClick.AddListener(Cancel);
        m_confirmButton.onClick.AddListener(Confirm);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPSecondConfirmationPopUIMsgData>();
        m_title.text = m_data?.Title ?? "Confirm action";
        m_description.text = m_data?.Description ?? "This action cannot be undone.";
        m_status.text = string.Empty;
        m_cancelButton.GetComponentInChildren<TMP_Text>(true).text = m_data?.CancelText ?? "Cancel";
        m_confirmButton.GetComponentInChildren<TMP_Text>(true).text = m_data?.ConfirmText ?? "Confirm";
        SetBusy(false);
        // 键盘/手柄的默认选项始终是安全操作。
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(m_cancelButton.gameObject);
    }

    private async void Confirm()
    {
        if (m_busy || m_decided || m_data?.ConfirmAsync == null)
            return;

        m_cancellation?.Dispose();
        var operation = new CancellationTokenSource();
        m_cancellation = operation;
        SetBusy(true);
        m_status.text = "Processing...";
        try
        {
            bool succeeded = await m_data.ConfirmAsync(operation.Token);
            if (this == null || IsDestoried || operation.IsCancellationRequested)
                return;
            if (!succeeded)
            {
                m_status.text = "Action not completed. Retry or cancel.";
                return;
            }

            m_decided = true;
            Close(m_data.OnConfirmed);
        }
        catch (OperationCanceledException)
        {
            if (this != null && !IsDestoried)
                m_status.text = "Action cancelled. You can retry.";
        }
        catch (Exception exception)
        {
            // 不把账号令牌或含敏感响应的异常文本显示到 UI。
            Debug.LogWarning($"[MPSecondConfirmationPop] 操作失败：{exception.GetType().Name}");
            if (this != null && !IsDestoried)
                m_status.text = "Action failed. Check your connection and retry, or cancel.";
        }
        finally
        {
            if (this != null && !IsDestoried && !m_decided)
                SetBusy(false);
        }
    }

    private void Cancel()
    {
        if (m_busy || m_decided)
            return;
        m_decided = true;
        SetBusy(true);
        Close(m_data?.OnCancel);
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        m_cancelButton.interactable = !busy && !m_decided;
        m_confirmButton.interactable = !busy && !m_decided && m_data?.ConfirmAsync != null;
    }

    private void Close(Action onClosed = null)
    {
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
            animation.Close(onClosed);
        else
        {
            DestroyWindow();
            onClosed?.Invoke();
        }
    }

    public override void OnRelease()
    {
        m_cancellation?.Cancel();
        m_cancellation?.Dispose();
        m_cancellation = null;
        m_cancelButton.onClick.RemoveListener(Cancel);
        m_confirmButton.onClick.RemoveListener(Confirm);
    }
}

public sealed class MPSecondConfirmationPopUIMsgData : UIMsgData
{
    public string Title { get; }
    public string Description { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public Func<CancellationToken, Task<bool>> ConfirmAsync { get; }
    public Action OnCancel { get; }
    public Action OnConfirmed { get; }
    public string ActionType { get; set; }
    public string AffectedData { get; set; }

    public MPSecondConfirmationPopUIMsgData(string title, string description, string confirmText,
        Func<CancellationToken, Task<bool>> confirmAsync, Action onCancel = null, string cancelText = "Cancel",
        Action onConfirmed = null)
    {
        Title = title;
        Description = description;
        ConfirmText = confirmText;
        CancelText = cancelText;
        ConfirmAsync = confirmAsync;
        OnCancel = onCancel;
        OnConfirmed = onConfirmed;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
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

    [TransformPath("View/Window/CurrentAccount")] private TMP_Text m_currentAccountText;
    [TransformPath("View/Window/ExistingAccount")] private TMP_Text m_existingAccountText;
    [TransformPath("View/Window/Status")] private TMP_Text m_statusText;
    private CancellationTokenSource m_lifetime;
    private Func<MPAccountConflictData, CancellationToken, Task<bool>> m_confirmAsync;
    private bool m_busy;
    private bool m_closing;

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
        m_lifetime = new CancellationTokenSource();
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
            m_confirmAsync = data.ConfirmAsync;
            SetTitle(data.Title);
            SetDesc(data.Description);
            RefreshAccounts();
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
        m_lifetime?.Cancel();
        m_lifetime?.Dispose();
        m_lifetime = null;
        UnregisterButton(m_cancelBtn, OnCancelClick);
        UnregisterButton(m_confirmBtn, OnConfirmClick);
    }

    /// <summary>
    /// 取消冲突处理。
    /// </summary>
    private void OnCancelClick()
    {
        if (m_busy || m_closing) return;
        m_closing = true;
        Action callback = m_onCancel;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(callback);
        else { DestroyWindow(); callback?.Invoke(); }
    }

    /// <summary>
    /// 确认继续冲突处理。
    /// </summary>
    private async void OnConfirmClick()
    {
        if (m_busy || m_closing) return;
        if (m_conflictData == null || string.IsNullOrWhiteSpace(m_conflictData.conflictToken))
        {
            SetStatus("This account request expired. Cancel and sign in again.");
            return;
        }
        if (m_confirmAsync == null && MPAccountConflictService.ConfirmSwitchAsync == null)
        {
            SetStatus("Account switching is not available yet. Your current account is unchanged.");
            return;
        }
        m_busy = true;
        m_cancelBtn.interactable = m_confirmBtn.interactable = false;
        SetStatus("Switching account...");
        try
        {
            bool success = await MPAccountConflictService.ResolveAsync(m_conflictData, m_confirmAsync, m_lifetime.Token);
            if (this == null || IsDestoried) return;
            if (success)
            {
                m_closing = true;
                Action callback = () => m_onConfirm?.Invoke(m_conflictData);
                MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
                if (animation != null) animation.Close(callback);
                else { DestroyWindow(); callback(); }
            }
            else SetStatus("Unable to switch accounts. Please retry or cancel.");
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPAccountConflictPop] 账号切换失败：{exception.GetType().Name}");
            if (this != null && !IsDestoried) SetStatus("Unable to switch accounts. Please retry or cancel.");
        }
        finally
        {
            m_busy = false;
            if (this != null && !IsDestoried && !m_closing)
                m_cancelBtn.interactable = m_confirmBtn.interactable = true;
        }
    }

    private void RefreshAccounts()
    {
        if (m_currentAccountText != null) m_currentAccountText.text = FormatAccount("Current account", m_conflictData?.currentAccount);
        if (m_existingAccountText != null) m_existingAccountText.text = FormatAccount("Linked account", m_conflictData?.existingAccount);
        SetStatus(string.Empty);
    }

    private static string FormatAccount(string heading, MPAccountSummary account)
    {
        if (account == null) return heading + "\nAccount details unavailable";
        string date = account.createdAtUtcTicks > 0 && account.createdAtUtcTicks <= DateTime.MaxValue.Ticks
            ? new DateTime(account.createdAtUtcTicks, DateTimeKind.Utc).ToString("yyyy-MM-dd") : "Unknown";
        string name = string.IsNullOrWhiteSpace(account.displayName) ? "Player" : account.displayName;
        return $"{heading}\n{name}\nLevel {Mathf.Max(1, account.level)}\n{account.provider}\nCreated: {date}";
    }

    private void SetStatus(string value) { if (m_statusText != null) m_statusText.text = value; }

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

    public Func<MPAccountConflictData, CancellationToken, Task<bool>> ConfirmAsync { get; private set; }

    public MPAccountConflictPopUIMsgData(
        string title,
        string description,
        MPAccountConflictData conflictData,
        Action onCancel,
        Action<MPAccountConflictData> onConfirm,
        Func<MPAccountConflictData, CancellationToken, Task<bool>> confirmAsync = null)
    {
        Title = title;
        Description = description;
        ConflictData = conflictData;
        OnCancel = onCancel;
        OnConfirm = onConfirm;
        ConfirmAsync = confirmAsync;
    }
}

/// <summary>
/// 后端接入点。服务端确认必须校验 conflictToken 并完成真实登录；严禁客户端 forceLink。
/// 当前项目没有该后端，未配置时安全失败，不伪造切换成功。
/// </summary>
public static class MPAccountConflictService
{
    public static Func<string, CancellationToken, Task<bool>> ConfirmSwitchAsync;
    public static bool IsResolving { get; private set; }

    public static async Task<bool> ResolveAsync(MPAccountConflictData data,
        Func<MPAccountConflictData, CancellationToken, Task<bool>> confirm, CancellationToken token)
    {
        if (IsResolving || data == null || string.IsNullOrWhiteSpace(data.conflictToken)) return false;
        IsResolving = true;
        try
        {
            string previousPlayerId = MPLoginManager.Instance.PlayerId;
            bool success = confirm != null
                ? await confirm(data, token)
                : ConfirmSwitchAsync != null && await ConfirmSwitchAsync(data.conflictToken, token);
            if (!success) return false;
            string expectedPlayerId = data.existingAccount?.playerId;
            if (!MPLoginManager.Instance.IsLoggedIn ||
                (!string.IsNullOrEmpty(expectedPlayerId) && MPLoginManager.Instance.PlayerId != expectedPlayerId) ||
                (string.IsNullOrEmpty(expectedPlayerId) && MPLoginManager.Instance.PlayerId == previousPlayerId))
                return false;
            return await MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync(token);
        }
        finally { IsResolving = false; }
    }
}

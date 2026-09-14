using System;
using System.Collections.Generic;
using System.Globalization;
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

    [TransformPath("View/Window/Desc")] private TMP_Text m_descText;
    [TransformPath("View/Window/CancelBtn")] private Button m_cancelBtn;
    [TransformPath("View/Window/ContinueBtn")] private Button m_confirmBtn;
    [TransformPath("View/Window/Local")] private RectTransform m_localRoot;
    [TransformPath("View/Window/Cloud")] private RectTransform m_cloudRoot;
    private AccountView m_localView;
    private AccountView m_cloudView;
    private readonly Dictionary<string, MPAssetLoadLease<Sprite>> m_iconLeases =
        new Dictionary<string, MPAssetLoadLease<Sprite>>();
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
        m_localView = new AccountView(m_localRoot);
        m_cloudView = new AccountView(m_cloudRoot);
        RegisterButton(m_cancelBtn, OnCancelClick);
        RegisterButton(m_confirmBtn, OnConfirmClick);
    }

    /// <summary>
    /// 接收冲突数据和按钮回调。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPAccountConflictPopUIMsgData data = uiMsg == null ? null : uiMsg.GetMsg<MPAccountConflictPopUIMsgData>();
        m_conflictData = data?.ConflictData;
        m_onCancel = data?.OnCancel;
        m_onConfirm = data?.OnConfirm;
        m_confirmAsync = data?.ConfirmAsync;
        SetDesc(data?.Description);
        Canvas.ForceUpdateCanvases();
        m_localView.Refresh(m_conflictData?.currentAccount, LoadPlatformIcon);
        m_cloudView.Refresh(m_conflictData?.existingAccount, LoadPlatformIcon);
        m_cancelBtn.interactable = !m_busy && !m_closing;
        m_confirmBtn.interactable = !m_busy && !m_closing && m_conflictData != null &&
            (m_onConfirm != null || m_confirmAsync != null || MPAccountConflictService.ConfirmSwitchAsync != null);
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
        m_localView?.ClearIcon();
        m_cloudView?.ClearIcon();
        foreach (var lease in m_iconLeases.Values) lease?.Dispose();
        m_iconLeases.Clear();
        m_onCancel = null;
        m_onConfirm = null;
        m_confirmAsync = null;
    }

    /// <summary>
    /// 取消冲突处理。
    /// </summary>
    private void OnCancelClick()
    {
        if (m_busy || m_closing) return;
        m_closing = true;
        m_cancelBtn.interactable = m_confirmBtn.interactable = false;
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
        // 仅提供继续回调时，由调用方在弹窗关闭后接管下一步；不在这里伪造账号切换。
        if (m_confirmAsync == null && MPAccountConflictService.ConfirmSwitchAsync == null &&
            m_onConfirm != null && m_conflictData != null)
        {
            m_closing = true;
            m_cancelBtn.interactable = m_confirmBtn.interactable = false;
            Action<MPAccountConflictData> onContinue = m_onConfirm;
            MPAccountConflictData conflict = m_conflictData;
            MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
            if (animation != null) animation.Close(() => onContinue(conflict));
            else { DestroyWindow(); onContinue(conflict); }
            return;
        }
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
                Action<MPAccountConflictData> onContinue = m_onConfirm;
                MPAccountConflictData conflict = m_conflictData;
                Action callback = () => onContinue?.Invoke(conflict);
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

    private void SetStatus(string value)
    {
        if (m_descText != null) m_descText.text = value;
    }

    private void SetDesc(string description)
    {
        SetStatus(string.IsNullOrWhiteSpace(description)
            ? "This sign-in method is already linked to another game account. Continue with the linked account? Your saves will remain separate."
            : description);
    }

    private Sprite LoadPlatformIcon(MPLoginProvider provider)
    {
        string asset;
        switch (provider)
        {
            case MPLoginProvider.Google:
            case MPLoginProvider.GooglePlayGames: asset = "loading_login_google_icon"; break;
            case MPLoginProvider.Apple: asset = "loading_login_apple_icon"; break;
            case MPLoginProvider.Facebook: asset = "loading_login_facebook_icon"; break;
            case MPLoginProvider.Anonymous: asset = "loading_login_anonymous_icon"; break;
            default: return null;
        }
        if (!m_iconLeases.TryGetValue(asset, out var lease))
        {
            try
            {
                lease = MPLoad.LoadLease<Sprite>(asset);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MPAccountConflictPop] 平台图标加载失败：{asset} ({exception.GetType().Name})");
            }
            m_iconLeases.Add(asset, lease);
        }
        return lease?.Asset;
    }

    /// <summary>两侧使用各自账号摘要，不从当前用户读取云端账号的等级信息。</summary>
    private sealed class AccountView
    {
        private readonly TMP_Text m_name, m_level, m_platform, m_createdTime;
        private readonly RectTransform m_fill;
        private readonly Image m_icon;

        public AccountView(Transform root)
        {
            m_name = root.Find("Name")?.GetComponent<TMP_Text>();
            m_level = root.Find("Level/Text")?.GetComponent<TMP_Text>();
            m_fill = root.Find("Level/Mask/Fill") as RectTransform;
            m_platform = root.Find("Platform/Info")?.GetComponent<TMP_Text>();
            m_icon = root.Find("Platform/Icon")?.GetComponent<Image>();
            m_createdTime = root.Find("CuretedTime/Info")?.GetComponent<TMP_Text>();
        }

        public void Refresh(MPAccountSummary account, Func<MPLoginProvider, Sprite> loadIcon)
        {
            if (m_name != null) m_name.text = string.IsNullOrWhiteSpace(account?.displayName) ? "Player" : account.displayName;
            int experience = Math.Max(0, account?.totalExperience ?? 0);
            int level = account?.totalExperience != null
                ? 1 + experience / MPUser.EXPERIENCE_PER_LEVEL : Math.Max(1, account?.level ?? 1);
            if (m_level != null) m_level.text = "LEVEL " + level;
            if (m_fill != null && m_fill.parent is RectTransform mask)
            {
                Vector2 position = m_fill.anchoredPosition;
                position.x = mask.rect.width * (experience % MPUser.EXPERIENCE_PER_LEVEL) / MPUser.EXPERIENCE_PER_LEVEL;
                m_fill.anchoredPosition = position;
            }
            MPLoginProvider provider = account?.provider ?? MPLoginProvider.Unknown;
            string label;
            switch (provider)
            {
                case MPLoginProvider.Anonymous: label = "Guest"; break;
                case MPLoginProvider.UsernamePassword: label = "Username"; break;
                case MPLoginProvider.Google: label = "Google"; break;
                case MPLoginProvider.GooglePlayGames: label = "Google Play Games"; break;
                case MPLoginProvider.Apple: label = "Apple"; break;
                case MPLoginProvider.Facebook: label = "Facebook"; break;
                default: label = "Unknown"; break;
            }
            if (m_platform != null)
            {
                m_platform.enableWordWrapping = false;
                m_platform.text = "Sign in\n" + label;
            }
            if (m_icon != null)
            {
                m_icon.sprite = loadIcon(provider);
                m_icon.enabled = m_icon.sprite != null;
            }
            long ticks = account?.createdAtUtcTicks ?? 0;
            string date = ticks > 0 && ticks <= DateTime.MaxValue.Ticks
                ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : "Unknown";
            if (m_createdTime != null)
            {
                m_createdTime.enableWordWrapping = false;
                m_createdTime.text = "Created on\n" + date;
            }
        }

        public void ClearIcon()
        {
            if (m_icon != null) m_icon.sprite = null;
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

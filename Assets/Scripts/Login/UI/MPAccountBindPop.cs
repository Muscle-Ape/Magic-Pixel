using System;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游客账号绑定提示弹窗。
/// 用于新手引导完成、首次付费前等时机提醒玩家绑定可恢复账号。
/// </summary>
[Component("MPAccountBindPop")]
public class MPAccountBindPop : AWindow
{
    /// <summary>弹窗标题文本。</summary>
    [TransformPath("View/Window/Title")]
    private TMP_Text m_titleText;

    /// <summary>绑定提示说明文本。</summary>
    [TransformPath("View/Window/Desc")]
    private TMP_Text m_descText;

    /// <summary>绑定状态或错误说明文本。</summary>
    [TransformPath("View/Window/Status")]
    private TMP_Text m_statusText;

    /// <summary>账号输入框，用于给当前游客添加账号密码。</summary>
    [TransformPath("View/Window/AccountInput")]
    private TMP_InputField m_accountInput;

    /// <summary>密码输入框，用于给当前游客添加账号密码。</summary>
    [TransformPath("View/Window/PasswordInput")]
    private TMP_InputField m_passwordInput;

    /// <summary>关闭或暂不绑定按钮。</summary>
    [TransformPath("View/Window/CloseBtn")]
    private Button m_closeBtn;

    /// <summary>绑定账号密码按钮。</summary>
    [TransformPath("View/Window/PasswordBindBtn")]
    private Button m_passwordBindBtn;

    /// <summary>绑定 Google 按钮。</summary>
    [TransformPath("View/Window/GoogleBindBtn")]
    private Button m_googleBindBtn;

    /// <summary>绑定 Apple 按钮。</summary>
    [TransformPath("View/Window/AppleBindBtn")]
    private Button m_appleBindBtn;

    /// <summary>绑定 Facebook 按钮。</summary>
    [TransformPath("View/Window/FacebookBindBtn")]
    private Button m_facebookBindBtn;

    /// <summary>弹窗关闭时的外部回调。</summary>
    private Action m_onClose;

    /// <summary>绑定成功时的外部回调。</summary>
    private Action<MPLoginResult> m_onBindSucceeded;

    /// <summary>当前是否正在执行绑定操作。</summary>
    private bool m_isRunning;

    /// <summary>当前绑定操作的取消源。</summary>
    private CancellationTokenSource m_operationCancellation;

    /// <summary>
    /// 注册按钮事件。
    /// </summary>
    public override void OnCreate()
    {
        RegisterButton(m_closeBtn, OnCloseClick);
        RegisterButton(m_passwordBindBtn, OnPasswordBindClick);
        RegisterButton(m_googleBindBtn, OnGoogleBindClick);
        RegisterButton(m_appleBindBtn, OnAppleBindClick);
        RegisterButton(m_facebookBindBtn, OnFacebookBindClick);
        ApplyConfiguration();
    }

    /// <summary>
    /// 接收绑定提示文案和回调。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPAccountBindPopUIMsgData data = uiMsg == null ? null : uiMsg.GetMsg<MPAccountBindPopUIMsgData>();
        if (data == null)
        {
            SetTitle("Bind Account");
            SetDesc("After binding your account, you can restore your progress when changing devices or reinstalling the game.");
            return;
        }

        m_onClose = data.OnClose;
        m_onBindSucceeded = data.OnBindSucceeded;
        SetTitle(data.Title);
        SetDesc(data.Description);
    }

    /// <summary>
    /// 清理事件和异步任务。
    /// </summary>
    public override void OnRelease()
    {
        CancelOperation();
        UnregisterButton(m_closeBtn, OnCloseClick);
        UnregisterButton(m_passwordBindBtn, OnPasswordBindClick);
        UnregisterButton(m_googleBindBtn, OnGoogleBindClick);
        UnregisterButton(m_appleBindBtn, OnAppleBindClick);
        UnregisterButton(m_facebookBindBtn, OnFacebookBindClick);
    }

    /// <summary>
    /// 根据登录配置显示可用绑定入口。
    /// </summary>
    private void ApplyConfiguration()
    {
        MPLoginConfiguration configuration = MPLoginManager.Instance.Configuration;
        SetButtonVisible(m_passwordBindBtn, configuration.EnableUsernamePasswordLogin);
        SetButtonVisible(m_googleBindBtn, configuration.EnableGoogleLogin || configuration.EnableGooglePlayGamesLogin);
        SetButtonVisible(m_appleBindBtn, configuration.EnableAppleLogin && MPAppleAuthAdapter.IsCurrentPlatformSupported);
        SetButtonVisible(m_facebookBindBtn, configuration.EnableFacebookLogin);
    }

    /// <summary>
    /// 关闭弹窗。
    /// </summary>
    private void OnCloseClick()
    {
        Action callback = m_onClose;
        DestroyWindow();
        callback?.Invoke();
    }

    /// <summary>
    /// 给当前游客账号绑定 Unity Authentication 账号密码。
    /// </summary>
    private async void OnPasswordBindClick()
    {
        string account = m_accountInput == null ? string.Empty : m_accountInput.text;
        string password = m_passwordInput == null ? string.Empty : m_passwordInput.text;
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("账号和密码不能为空。");
            return;
        }

        await RunBindOperationAsync(token => MPLoginManager.Instance.LinkAsync(
            MPLoginType.UsernamePassword,
            new MPPasswordLoginRequest
            {
                account = account,
                password = password,
                mode = MPPasswordLoginMode.AddToCurrentUser
            },
            token));
    }

    /// <summary>
    /// Google 绑定入口。
    /// 当前通过 Google Play Games SDK 获取 Auth Code，并绑定到当前 Unity Authentication 账号。
    /// </summary>
    private async void OnGoogleBindClick()
    {
        await RunBindOperationAsync(async token =>
        {
            SetStatus("正在拉起 Google Play Games 登录...");
            MPThirdPartyAuthResult authResult = await MPGooglePlayGamesAuthService.RequestAuthCodeAsync(
                forceRefreshToken: false,
                cancellationToken: token);

            if (authResult == null || !authResult.success)
            {
                return CreateThirdPartyAuthFailure(MPLoginType.GooglePlayGames, authResult);
            }

            SetStatus("正在绑定 Google Play Games...");
            return await MPLoginManager.Instance.LinkAsync(
                MPLoginType.GooglePlayGames,
                new MPThirdPartyLoginRequest
                {
                    loginType = MPLoginType.GooglePlayGames,
                    provider = MPLoginType.GooglePlayGames,
                    authorizationCode = authResult.authorizationCode,
                    platformUserId = authResult.platformUserId,
                    forceLink = false
                },
                token);
        });
    }

    /// <summary>
    /// Apple 绑定入口。Adapter 会拉起系统授权并把 Identity Token 绑定到当前 Unity Authentication 账号。
    /// </summary>
    private async void OnAppleBindClick()
    {
        await RunBindOperationAsync(async token =>
        {
            SetStatus("正在请求 Apple 授权...");
            return await MPLoginManager.Instance.LinkAsync(
                MPLoginType.Apple,
                new MPThirdPartyLoginRequest
                {
                    loginType = MPLoginType.Apple,
                    provider = MPLoginType.Apple,
                    forceLink = false
                },
                token);
        });
    }

    /// <summary>
    /// Facebook 绑定入口，后续由 SDK Adapter 提供 Access Token。
    /// </summary>
    private void OnFacebookBindClick()
    {
        SetStatus("Facebook 绑定入口已预留。接入平台 token 后调用 MPLoginManager.BindProviderAsync。");
    }

    /// <summary>
    /// 运行绑定异步操作。
    /// </summary>
    private async Task RunBindOperationAsync(Func<CancellationToken, Task<MPLoginResult>> operation)
    {
        if (m_isRunning)
        {
            return;
        }

        CancelOperation();
        m_operationCancellation = new CancellationTokenSource();
        m_isRunning = true;
        SetInteractable(false);
        SetStatus("正在绑定账号...");

        try
        {
            MPLoginResult result = await operation(m_operationCancellation.Token);
            if (m_operationCancellation == null || m_operationCancellation.IsCancellationRequested)
            {
                return;
            }

            if (result != null && result.isSuccess)
            {
                SetStatus("账号绑定成功。");
                Action<MPLoginResult> callback = m_onBindSucceeded;
                DestroyWindow();
                callback?.Invoke(result);
            }
            else
            {
                SetStatus(result == null ? "账号绑定失败，请稍后重试。" : result.errorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("绑定操作已取消。");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAccountBindPop] 绑定操作异常：{exception}");
            SetStatus($"绑定操作异常：{exception.Message}");
        }
        finally
        {
            m_isRunning = false;
            SetInteractable(true);
        }
    }

    /// <summary>
    /// 取消当前异步操作。
    /// </summary>
    private void CancelOperation()
    {
        if (m_operationCancellation == null)
        {
            return;
        }

        m_operationCancellation.Cancel();
        m_operationCancellation.Dispose();
        m_operationCancellation = null;
    }

    /// <summary>
    /// 设置标题文本。
    /// </summary>
    private void SetTitle(string title)
    {
        if (m_titleText != null)
        {
            m_titleText.text = string.IsNullOrEmpty(title) ? "绑定账号" : title;
        }
    }

    /// <summary>
    /// 设置说明文本。
    /// </summary>
    private void SetDesc(string desc)
    {
        if (m_descText != null)
        {
            m_descText.text = string.IsNullOrEmpty(desc) ? "绑定账号后可以恢复游客进度。" : desc;
        }
    }

    /// <summary>
    /// 设置状态文本。
    /// </summary>
    private void SetStatus(string status)
    {
        if (m_statusText != null)
        {
            m_statusText.text = string.IsNullOrEmpty(status) ? string.Empty : status;
        }
    }

    /// <summary>
    /// 设置所有交互控件是否可用。
    /// </summary>
    private void SetInteractable(bool interactable)
    {
        SetButtonInteractable(m_closeBtn, interactable);
        SetButtonInteractable(m_passwordBindBtn, interactable);
        SetButtonInteractable(m_googleBindBtn, interactable);
        SetButtonInteractable(m_appleBindBtn, interactable);
        SetButtonInteractable(m_facebookBindBtn, interactable);

        if (m_accountInput != null)
        {
            m_accountInput.interactable = interactable;
        }

        if (m_passwordInput != null)
        {
            m_passwordInput.interactable = interactable;
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

    /// <summary>
    /// 设置按钮显隐。
    /// </summary>
    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 设置按钮可交互状态。
    /// </summary>
    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    /// <summary>
    /// 将第三方 SDK 授权失败转换为绑定流程可展示的统一失败结果。
    /// </summary>
    /// <param name="loginType">绑定类型。</param>
    /// <param name="authResult">SDK 授权结果。</param>
    /// <returns>绑定失败结果。</returns>
    private static MPLoginResult CreateThirdPartyAuthFailure(MPLoginType loginType, MPThirdPartyAuthResult authResult)
    {
        string errorCode = authResult == null || string.IsNullOrEmpty(authResult.errorCode)
            ? MPLoginErrorCodes.ThirdPartyAuthFailed
            : authResult.errorCode;
        string errorMessage = authResult == null || string.IsNullOrEmpty(authResult.errorMessage)
            ? "第三方平台授权失败。"
            : authResult.errorMessage;

        return MPLoginResult.Failed(loginType, MPLoginError.Create(errorCode, errorMessage, errorCode != MPLoginErrorCodes.UserCancelled));
    }
}

/// <summary>
/// 游客账号绑定弹窗打开参数。
/// </summary>
public sealed class MPAccountBindPopUIMsgData : UIMsgData
{
    /// <summary>弹窗标题。</summary>
    public string Title { get; private set; }

    /// <summary>绑定原因说明。</summary>
    public string Description { get; private set; }

    /// <summary>关闭弹窗后的回调。</summary>
    public Action OnClose { get; private set; }

    /// <summary>绑定成功后的回调。</summary>
    public Action<MPLoginResult> OnBindSucceeded { get; private set; }

    public MPAccountBindPopUIMsgData(string title, string description, Action onClose, Action<MPLoginResult> onBindSucceeded)
    {
        Title = title;
        Description = description;
        OnClose = onClose;
        OnBindSucceeded = onBindSucceeded;
    }
}

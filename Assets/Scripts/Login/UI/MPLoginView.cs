using System;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录主页面。
/// 页面只负责展示登录入口与接收用户操作，启动决策由 MPLoginFlowController 处理。
/// </summary>
[Component("MPLoginView")]
public class MPLoginView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>页面标题文本。</summary>
    [TransformPath("View/Content/Title")]
    private TMP_Text m_titleText;

    /// <summary>当前登录状态或错误说明文本。</summary>
    [TransformPath("View/Content/Status")]
    private TMP_Text m_statusText;

    /// <summary>账号输入框，用于 Unity Authentication 账号密码登录和注册。</summary>
    [TransformPath("View/Content/AccountGroup/AccountInput")]
    private TMP_InputField m_accountInput;

    /// <summary>密码输入框，用于 Unity Authentication 账号密码登录和注册。</summary>
    [TransformPath("View/Content/AccountGroup/PasswordInput")]
    private TMP_InputField m_passwordInput;

    /// <summary>重试按钮，用于网络失败、维护结束后重新执行启动登录流程。</summary>
    [TransformPath("View/Content/RetryBtn")]
    private Button m_retryBtn;

    /// <summary>游客登录按钮，用于继续匿名登录或在恢复失败后创建新游客。</summary>
    [TransformPath("View/Content/GuestBtn")]
    private Button m_guestBtn;

    /// <summary>账号密码登录按钮。</summary>
    [TransformPath("View/Content/PasswordLoginBtn")]
    private Button m_passwordLoginBtn;

    /// <summary>账号密码注册按钮。</summary>
    [TransformPath("View/Content/PasswordRegisterBtn")]
    private Button m_passwordRegisterBtn;

    /// <summary>Google 登录按钮。</summary>
    [TransformPath("View/Content/GoogleBtn")]
    private Button m_googleBtn;

    /// <summary>Google Play Games 登录按钮。</summary>
    [TransformPath("View/Content/GooglePlayGamesBtn")]
    private Button m_googlePlayGamesBtn;

    /// <summary>Apple 登录按钮。</summary>
    [TransformPath("View/Content/AppleBtn")]
    private Button m_appleBtn;

    /// <summary>Facebook 登录按钮。</summary>
    [TransformPath("View/Content/FacebookBtn")]
    private Button m_facebookBtn;

    /// <summary>账号输入区域，用于统一显隐账号密码控件。</summary>
    [TransformPath("View/Content/AccountGroup")]
    private RectTransform m_accountGroup;

    /// <summary>最近一次登录启动流程结果。</summary>
    private MPLoginStartupResult m_startupResult;

    /// <summary>登录成功后的外部回调，通常由启动器进入游戏主流程。</summary>
    private Action<MPLoginResult> m_onLoginSucceeded;

    /// <summary>当前是否正在执行异步登录操作。</summary>
    private bool m_isRunning;

    /// <summary>当前登录操作的取消源，页面销毁时需要主动取消。</summary>
    private CancellationTokenSource m_operationCancellation;

    /// <summary>
    /// 初始化按钮事件。
    /// </summary>
    public override void OnCreate()
    {
        RegisterButtons();
        ApplyStartupResult(MPLoginManager.Instance.LastStartupResult);
    }

    /// <summary>
    /// 接收启动器传入的登录启动结果和成功回调。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLoginViewUIMsgData data = uiMsg == null ? null : uiMsg.GetMsg<MPLoginViewUIMsgData>();
        if (data != null)
        {
            m_startupResult = data.StartupResult;
            m_onLoginSucceeded = data.OnLoginSucceeded;
        }
        else
        {
            m_startupResult = MPLoginManager.Instance.LastStartupResult;
        }

        ApplyStartupResult(m_startupResult);
    }

    /// <summary>
    /// 清理按钮事件和异步任务，避免页面关闭后回调继续访问 UI。
    /// </summary>
    public override void OnRelease()
    {
        CancelOperation();
        UnregisterButtons();
    }

    /// <summary>
    /// 注册按钮点击事件。
    /// </summary>
    private void RegisterButtons()
    {
        RegisterButton(m_retryBtn, OnRetryClick);
        RegisterButton(m_guestBtn, OnGuestClick);
        RegisterButton(m_passwordLoginBtn, OnPasswordLoginClick);
        RegisterButton(m_passwordRegisterBtn, OnPasswordRegisterClick);
        RegisterButton(m_googleBtn, OnGoogleClick);
        RegisterButton(m_googlePlayGamesBtn, OnGooglePlayGamesClick);
        RegisterButton(m_appleBtn, OnAppleClick);
        RegisterButton(m_facebookBtn, OnFacebookClick);
    }

    /// <summary>
    /// 移除按钮点击事件。
    /// </summary>
    private void UnregisterButtons()
    {
        UnregisterButton(m_retryBtn, OnRetryClick);
        UnregisterButton(m_guestBtn, OnGuestClick);
        UnregisterButton(m_passwordLoginBtn, OnPasswordLoginClick);
        UnregisterButton(m_passwordRegisterBtn, OnPasswordRegisterClick);
        UnregisterButton(m_googleBtn, OnGoogleClick);
        UnregisterButton(m_googlePlayGamesBtn, OnGooglePlayGamesClick);
        UnregisterButton(m_appleBtn, OnAppleClick);
        UnregisterButton(m_facebookBtn, OnFacebookClick);
    }

    /// <summary>
    /// 根据启动策略结果刷新页面标题、提示和按钮显隐。
    /// </summary>
    private void ApplyStartupResult(MPLoginStartupResult result)
    {
        m_startupResult = result;

        if (result == null)
        {
            SetTitle("登录");
            SetStatus("正在准备登录模块...");
            SetLoginOptionsVisible(false);
            SetButtonVisible(m_retryBtn, true);
            return;
        }

        bool showRetry = result.action == MPLoginStartupAction.ShowNetworkRetry ||
                         result.action == MPLoginStartupAction.ShowMaintenance;
        bool showLoginOptions = result.action == MPLoginStartupAction.ShowLoginSelection ||
                                result.action == MPLoginStartupAction.ShowAnonymousRecovery ||
                                result.action == MPLoginStartupAction.ShowAccountDisabled ||
                                result.action == MPLoginStartupAction.Failed;

        SetTitle(GetTitle(result.action));
        SetStatus(result.message);
        SetButtonVisible(m_retryBtn, showRetry);
        SetLoginOptionsVisible(showLoginOptions);
        SetInteractable(!m_isRunning);
    }

    /// <summary>
    /// 显示或隐藏登录方式入口。
    /// </summary>
    private void SetLoginOptionsVisible(bool visible)
    {
        MPLoginConfiguration configuration = MPLoginManager.Instance.Configuration;
        bool showPassword = visible && configuration.EnableUsernamePasswordLogin;

        SetGameObjectVisible(m_accountGroup, showPassword);
        SetButtonVisible(m_passwordLoginBtn, showPassword);
        SetButtonVisible(m_passwordRegisterBtn, showPassword);
        SetButtonVisible(m_googleBtn, visible && configuration.EnableGoogleLogin);
        SetButtonVisible(m_googlePlayGamesBtn, visible && configuration.EnableGooglePlayGamesLogin);
        SetButtonVisible(m_appleBtn, visible && configuration.EnableAppleLogin && MPAppleAuthAdapter.IsCurrentPlatformSupported);
        SetButtonVisible(m_facebookBtn, visible && configuration.EnableFacebookLogin);
        SetButtonVisible(m_guestBtn, visible && configuration.EnableAnonymousLogin && CanCreateGuest());
    }

    /// <summary>
    /// 判断当前状态下是否允许展示游客入口。
    /// </summary>
    private bool CanCreateGuest()
    {
        return m_startupResult == null || m_startupResult.canCreateNewGuest;
    }

    /// <summary>
    /// 重新执行启动登录流程。
    /// </summary>
    private async void OnRetryClick()
    {
        await RunStartupOperationAsync(token => MPLoginManager.Instance.RetryStartupAsync(token));
    }

    /// <summary>
    /// 继续游客登录或创建新的游客账号。
    /// </summary>
    private async void OnGuestClick()
    {
        await RunStartupOperationAsync(token => MPLoginManager.Instance.ContinueAsNewGuestAsync(token));
    }

    /// <summary>
    /// 使用账号密码登录已有 Unity Authentication 用户。
    /// </summary>
    private async void OnPasswordLoginClick()
    {
        await RunPasswordOperationAsync(MPPasswordLoginMode.Login);
    }

    /// <summary>
    /// 注册新的 Unity Authentication 账号密码用户。
    /// </summary>
    private async void OnPasswordRegisterClick()
    {
        await RunPasswordOperationAsync(MPPasswordLoginMode.Register);
    }

    /// <summary>
    /// Google 登录入口。
    /// 当前项目已接入 Google Play Games SDK，因此这里复用 GPGS Auth Code 登录 Unity Authentication。
    /// </summary>
    private async void OnGoogleClick()
    {
        await RunGooglePlayGamesLoginOperationAsync();
    }

    /// <summary>
    /// Google Play Games 登录入口。
    /// 先从 GPGS SDK 获取一次性 Auth Code，再交给 Unity Authentication 登录或创建账号。
    /// </summary>
    private async void OnGooglePlayGamesClick()
    {
        await RunGooglePlayGamesLoginOperationAsync();
    }

    /// <summary>
    /// Apple 登录入口。Adapter 会拉起系统授权并把 Identity Token 交给 Unity Authentication。
    /// </summary>
    private async void OnAppleClick()
    {
        await RunLoginOperationAsync(async token =>
        {
            SetStatus("正在请求 Apple 授权...");
            return await MPLoginManager.Instance.LoginAsync(
                MPLoginType.Apple,
                new MPThirdPartyLoginRequest
                {
                    loginType = MPLoginType.Apple,
                    provider = MPLoginType.Apple,
                    createAccount = true
                },
                token);
        });
    }

    /// <summary>
    /// Facebook 登录入口，后续需要从 Facebook SDK 获取 Access Token。
    /// </summary>
    private void OnFacebookClick()
    {
        SetStatus("Facebook 登录页面已预留。接入 Facebook Access Token Adapter 后，可从这里调用 Unity Authentication 登录。");
    }

    /// <summary>
    /// 执行账号密码登录或注册。
    /// </summary>
    private async Task RunPasswordOperationAsync(MPPasswordLoginMode mode)
    {
        string account = m_accountInput == null ? string.Empty : m_accountInput.text;
        string password = m_passwordInput == null ? string.Empty : m_passwordInput.text;

        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("账号和密码不能为空。");
            return;
        }

        await RunLoginOperationAsync(token => MPLoginManager.Instance.LoginAsync(
            MPLoginType.UsernamePassword,
            new MPPasswordLoginRequest
            {
                account = account,
                password = password,
                mode = mode
            },
            token));
    }

    /// <summary>
    /// 执行 Google Play Games 登录。
    /// </summary>
    private async Task RunGooglePlayGamesLoginOperationAsync()
    {
        await RunLoginOperationAsync(async token =>
        {
            SetStatus("正在拉起 Google Play Games 登录...");
            MPThirdPartyAuthResult authResult = await MPGooglePlayGamesAuthService.RequestAuthCodeAsync(
                forceRefreshToken: false,
                cancellationToken: token);

            if (authResult == null || !authResult.success)
            {
                return CreateThirdPartyAuthFailure(MPLoginType.GooglePlayGames, authResult);
            }

            SetStatus("正在登录 Unity Authentication...");
            return await MPLoginManager.Instance.LoginAsync(
                MPLoginType.GooglePlayGames,
                new MPThirdPartyLoginRequest
                {
                    loginType = MPLoginType.GooglePlayGames,
                    provider = MPLoginType.GooglePlayGames,
                    authorizationCode = authResult.authorizationCode,
                    platformUserId = authResult.platformUserId,
                    createAccount = true
                },
                token);
        });
    }

    /// <summary>
    /// 执行启动流程相关异步操作，并根据动作决定是否进入游戏。
    /// </summary>
    private async Task RunStartupOperationAsync(Func<CancellationToken, Task<MPLoginStartupResult>> operation)
    {
        if (m_isRunning)
        {
            return;
        }

        StartOperation();

        try
        {
            MPLoginStartupResult result = await operation(m_operationCancellation.Token);
            if (m_operationCancellation == null || m_operationCancellation.IsCancellationRequested)
            {
                return;
            }

            HandleStartupResult(result);
        }
        catch (OperationCanceledException)
        {
            SetStatus("登录操作已取消。");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPLoginView] 登录操作异常：{exception}");
            SetStatus($"登录操作异常：{exception.Message}");
        }
        finally
        {
            FinishOperation();
        }
    }

    /// <summary>
    /// 执行直接登录异步操作，并根据结果决定是否进入游戏。
    /// </summary>
    private async Task RunLoginOperationAsync(Func<CancellationToken, Task<MPLoginResult>> operation)
    {
        if (m_isRunning)
        {
            return;
        }

        StartOperation();

        try
        {
            MPLoginResult result = await operation(m_operationCancellation.Token);
            if (m_operationCancellation == null || m_operationCancellation.IsCancellationRequested)
            {
                return;
            }

            HandleLoginResult(result);
        }
        catch (OperationCanceledException)
        {
            SetStatus("登录操作已取消。");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPLoginView] 登录操作异常：{exception}");
            SetStatus($"登录操作异常：{exception.Message}");
        }
        finally
        {
            FinishOperation();
        }
    }

    /// <summary>
    /// 进入异步登录处理状态。
    /// </summary>
    private void StartOperation()
    {
        CancelOperation();
        m_operationCancellation = new CancellationTokenSource();
        m_isRunning = true;
        SetInteractable(false);
        SetStatus("登录处理中...");
    }

    /// <summary>
    /// 退出异步登录处理状态。
    /// </summary>
    private void FinishOperation()
    {
        m_isRunning = false;
        SetInteractable(true);
    }

    /// <summary>
    /// 处理启动流程返回的下一步动作。
    /// </summary>
    private void HandleStartupResult(MPLoginStartupResult result)
    {
        if (result != null && result.action == MPLoginStartupAction.EnterGame)
        {
            NotifyLoginSucceeded(result.loginResult);
            return;
        }

        ApplyStartupResult(result);
    }

    /// <summary>
    /// 处理直接登录结果。
    /// </summary>
    private void HandleLoginResult(MPLoginResult result)
    {
        if (result != null && result.isSuccess)
        {
            NotifyLoginSucceeded(result);
            return;
        }

        SetStatus(result == null ? "登录失败，请稍后重试。" : result.errorMessage);
    }

    /// <summary>
    /// 通知启动器登录成功并关闭当前登录页面。
    /// </summary>
    private void NotifyLoginSucceeded(MPLoginResult result)
    {
        SetStatus("登录成功，正在进入游戏...");
        Action<MPLoginResult> callback = m_onLoginSucceeded;
        DestroyWindow();
        callback?.Invoke(result);
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
    /// 设置页面标题。
    /// </summary>
    private void SetTitle(string title)
    {
        if (m_titleText != null)
        {
            m_titleText.text = string.IsNullOrEmpty(title) ? string.Empty : title;
        }
    }

    /// <summary>
    /// 设置页面状态说明。
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
        SetButtonInteractable(m_retryBtn, interactable);
        SetButtonInteractable(m_guestBtn, interactable);
        SetButtonInteractable(m_passwordLoginBtn, interactable);
        SetButtonInteractable(m_passwordRegisterBtn, interactable);
        SetButtonInteractable(m_googleBtn, interactable);
        SetButtonInteractable(m_googlePlayGamesBtn, interactable);
        SetButtonInteractable(m_appleBtn, interactable);
        SetButtonInteractable(m_facebookBtn, interactable);

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
    /// 获取启动动作对应的页面标题。
    /// </summary>
    private static string GetTitle(MPLoginStartupAction action)
    {
        switch (action)
        {
            case MPLoginStartupAction.ShowNetworkRetry:
                return "Network connection failed"; // "网络连接失败";
            case MPLoginStartupAction.ShowMaintenance:
                return "Server under maintenance"; //"服务器维护中";
            case MPLoginStartupAction.ShowAnonymousRecovery:
                return "Guest account recovery"; // "游客账号恢复";
            case MPLoginStartupAction.ShowAccountDisabled:
                return "Account temporarily unavailable"; // "账号暂不可用";
            case MPLoginStartupAction.Failed:
                return "Login failed"; // "登录失败";
            default:
                return "Log in"; // "登录";
        }
    }

    /// <summary>
    /// 将第三方 SDK 授权失败转换成登录模块统一失败结果。
    /// </summary>
    /// <param name="loginType">登录类型。</param>
    /// <param name="authResult">SDK 授权结果。</param>
    /// <returns>登录失败结果。</returns>
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

    /// <summary>
    /// 注册单个按钮事件。
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
    /// 移除单个按钮事件。
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
    /// 设置节点显隐。
    /// </summary>
    private static void SetGameObjectVisible(Component component, bool visible)
    {
        if (component != null)
        {
            component.gameObject.SetActive(visible);
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
}

/// <summary>
/// 登录主页面打开参数。
/// </summary>
public sealed class MPLoginViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 登录启动流程计算出的首个页面状态。
    /// </summary>
    public MPLoginStartupResult StartupResult { get; private set; }

    /// <summary>
    /// 登录成功后的回调。
    /// </summary>
    public Action<MPLoginResult> OnLoginSucceeded { get; private set; }

    public MPLoginViewUIMsgData(MPLoginStartupResult startupResult, Action<MPLoginResult> onLoginSucceeded)
    {
        StartupResult = startupResult;
        OnLoginSucceeded = onLoginSucceeded;
    }
}

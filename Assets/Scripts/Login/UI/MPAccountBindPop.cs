using System;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 游客账号绑定弹窗。界面文案与图片均由预制体提供，脚本只管理六个按钮及绑定生命周期。
/// </summary>
[Component("MPAccountBindPop")]
public class MPAccountBindPop : AWindow
{
    private const string LEGAL_DOCUMENT_URL = "http://yunovagames.com:19100/";

    [TransformPath("View/Window/CloseBtn")]
    private Button m_closeBtn;

    [TransformPath("View/Window/Plattform/GoogleBindBtn")]
    private Button m_googleBindBtn;

    [TransformPath("View/Window/Plattform/AppleBindBtn")]
    private Button m_appleBindBtn;

    [TransformPath("View/Window/Plattform/FacebookBindBtn")]
    private Button m_facebookBindBtn;

    [TransformPath("View/Window/PrivacyPolicyBtn")]
    private Button m_privacyPolicyBtn;

    [TransformPath("View/Window/TermsOfServiceBtn")]
    private Button m_termsOfServiceBtn;

    private Action m_onClose;
    private Action<MPLoginResult> m_onBindSucceeded;
    private CancellationTokenSource m_operationCancellation;
    private bool m_isRunning;
    private bool m_isClosing;
    private MPSecondConfirmationPop m_conflictConfirmation;
    private bool m_conflictPromptShowing;
    private readonly MPAppleAuthAdapter m_appleAuthAdapter = new MPAppleAuthAdapter();
    // 切换账号后，必须先完成存档选择；失败时仅允许原地重试，不能进入仍显示游客数据的页面。
    private bool m_appleSignInPending;
    private string m_pendingApplePlayerId;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public override void OnCreate()
    {
        RegisterButton(m_closeBtn, OnCloseClick);
        RegisterButton(m_googleBindBtn, OnGoogleBindClick);
        RegisterButton(m_appleBindBtn, OnAppleBindClick);
        RegisterButton(m_facebookBindBtn, OnFacebookBindClick);
        RegisterButton(m_privacyPolicyBtn, OnPrivacyPolicyClick);
        RegisterButton(m_termsOfServiceBtn, OnTermsOfServiceClick);
        ApplyConfiguration();
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPAccountBindPopUIMsgData data = uiMsg?.GetMsg<MPAccountBindPopUIMsgData>();
        m_onClose = data?.OnClose;
        m_onBindSucceeded = data?.OnBindSucceeded;
    }

    public override void OnRelease()
    {
        if (m_conflictConfirmation != null && !m_conflictConfirmation.IsDestoried)
            m_conflictConfirmation.DestroyWindow();
        m_conflictConfirmation = null;
        CancelOperation();
        UnregisterButton(m_closeBtn, OnCloseClick);
        UnregisterButton(m_googleBindBtn, OnGoogleBindClick);
        UnregisterButton(m_appleBindBtn, OnAppleBindClick);
        UnregisterButton(m_facebookBindBtn, OnFacebookBindClick);
        UnregisterButton(m_privacyPolicyBtn, OnPrivacyPolicyClick);
        UnregisterButton(m_termsOfServiceBtn, OnTermsOfServiceClick);
        m_onClose = null;
        m_onBindSucceeded = null;
    }

    /// <summary>按平台与登录配置隐藏不可用的第三方绑定入口。</summary>
    private void ApplyConfiguration()
    {
        MPLoginConfiguration configuration = MPLoginManager.Instance.Configuration;
        SetButtonVisible(
            m_googleBindBtn,
            configuration.EnableGooglePlayGamesLogin && MPGooglePlayGamesAuthService.IsCurrentPlatformSupported);
        SetButtonVisible(
            m_appleBindBtn,
            configuration.EnableAppleLogin && MPAppleAuthAdapter.IsCurrentPlatformSupported);
        SetButtonVisible(m_facebookBindBtn, MPReleaseFeatures.Facebook && configuration.EnableFacebookLogin);
    }

    private void OnCloseClick()
    {
        if (m_isRunning || m_isClosing || m_conflictPromptShowing || m_appleSignInPending)
            return;

        m_isClosing = true;
        Action callback = m_onClose;
        DestroyWindow();
        callback?.Invoke();
    }

    /// <summary>先由 Google Play Games 获取 Auth Code，再绑定到当前 Unity Authentication 账号。</summary>
    private async void OnGoogleBindClick()
    {
        await RunBindOperationAsync(async token =>
        {
            MPThirdPartyAuthResult authResult = await MPGooglePlayGamesAuthService.RequestAuthCodeAsync(
                forceRefreshToken: false,
                cancellationToken: token);
            if (authResult == null || !authResult.success)
                return CreateThirdPartyAuthFailure(MPLoginType.GooglePlayGames, authResult);

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

    /// <summary>在设置页完成授权、绑定或已有账号登录，不再跳转 Loading 选择登录方式。</summary>
    private async void OnAppleBindClick()
    {
        await RunBindOperationAsync(BindOrSignInAppleAsync);
    }

    private async Task<MPLoginResult> BindOrSignInAppleAsync(CancellationToken token)
    {
        var request = new MPThirdPartyLoginRequest
        {
            loginType = MPLoginType.Apple, provider = MPLoginType.Apple,
            forceLink = false, createAccount = false
        };
        try
        {
            if (string.IsNullOrEmpty(m_pendingApplePlayerId))
            {
                MPThirdPartyAuthResult auth = await m_appleAuthAdapter.AuthorizeAsync(request, token);
                token.ThrowIfCancellationRequested();
                if (auth?.success != true) return CreateThirdPartyAuthFailure(MPLoginType.Apple, auth);
                // 令牌只用于本次绑定和紧接着的登录，不写存档、不输出日志。
                request.identityToken = auth.identityToken;
                request.platformUserId = auth.platformUserId;
                if (!m_appleSignInPending)
                {
                    MPLoginResult linked = await MPLoginManager.Instance.LinkAsync(MPLoginType.Apple, request, token);
                    token.ThrowIfCancellationRequested();
                    if (linked?.error?.code != MPLoginErrorCodes.AccountBindingConflict) return linked;

                    // 必须先取得用户同意，再保存迁移意图和切换认证账号；取消仍停留在原游客。
                    if (!await ConfirmAppleAccountConflictAsync(token))
                        return MPLoginResult.Failed(MPLoginType.Apple,
                            MPLoginError.Create(MPLoginErrorCodes.UserCancelled, "Account switch cancelled.", false));
                    token.ThrowIfCancellationRequested();
                    var profile = await MPLoginManager.Instance.LoadLocalProfileAsync(token);
                    bool saved = profile?.IsIndependentGuest == true
                        ? await MPCloudSaveManager.Instance.PrepareGuestSaveChoiceAsync(MPLoginType.Apple, token)
                        : await MPCloudSaveManager.Instance.FlushAsync(token);
                    if (!saved) return AppleFlowFailure("Current progress could not be saved. Please retry Apple sign-in.");
                    m_appleSignInPending = true;
                }

                MPLoginStartupResult login = await MPLoginManager.Instance.LoginWithProviderAsync(MPLoginType.Apple, request, token);
                token.ThrowIfCancellationRequested();
                if (login?.action != MPLoginStartupAction.EnterGame || !MPLoginManager.Instance.IsLoggedIn)
                    return MPLoginResult.Failed(MPLoginType.Apple, login?.error ??
                        MPLoginError.Create(MPLoginErrorCodes.ServerError, login?.message ?? "Apple sign-in failed. Please retry."));
                m_pendingApplePlayerId = MPLoginManager.Instance.PlayerId;
            }

            // 取消存档选择或网络失败后，再次点击 Apple 只重试数据，不重复授权或绑定。
            if (MPLoginManager.Instance.PlayerId != m_pendingApplePlayerId)
                return AppleFlowFailure("The active account changed. Please restart the game to restore your save.");
            if (!await MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync(token))
                return AppleFlowFailure("Save selection is incomplete. Tap Apple to retry.");
            token.ThrowIfCancellationRequested();
            MPVibrationManager.Instance.Initialize();
            m_appleSignInPending = false;
            m_pendingApplePlayerId = null;
            return MPLoginResult.Success(MPLoginManager.Instance.CurrentSession);
        }
        finally
        {
            request.identityToken = null;
            request.platformUserId = null;
        }
    }

    private static MPLoginResult AppleFlowFailure(string message) => MPLoginResult.Failed(
        MPLoginType.Apple, MPLoginError.Create(MPLoginErrorCodes.ServerError, message, true));

    /// <summary>仅询问是否继续，不在确认前登录、覆盖存档或创建迁移记录。</summary>
    private async Task<bool> ConfirmAppleAccountConflictAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MPSecondConfirmationPop popup = null;
        m_conflictPromptShowing = true;
        try
        {
            using (token.Register(() => completion.TrySetCanceled()))
            {
                popup = MPSecondConfirmationPop.Show(
                    "Account already linked",
                    "This Apple account is already linked to another game account. Continue to sign in? You can then choose between your current guest progress and the account's saved progress. Nothing will be replaced before you confirm your save choice.",
                    "Continue",
                    confirmationToken =>
                    {
                        confirmationToken.ThrowIfCancellationRequested();
                        token.ThrowIfCancellationRequested();
                        return Task.FromResult(this != null && !IsDestoried);
                    },
                    onCancel: () => completion.TrySetResult(false),
                    onConfirmed: () => completion.TrySetResult(true),
                    preserveButtonText: true);
                m_conflictConfirmation = popup;
                if (popup == null) return false;
                // 关闭动画完成后才继续登录，避免和后面的资产对比弹窗产生层级冲突。
                return await completion.Task;
            }
        }
        finally
        {
            if (ReferenceEquals(m_conflictConfirmation, popup)) m_conflictConfirmation = null;
            m_conflictPromptShowing = false;
            if (popup != null && !popup.IsDestoried) popup.DestroyWindow();
        }
    }

    /// <summary>通过已接入 MPFacebookAuthAdapter 的平台授权回调绑定 Facebook。</summary>
    private async void OnFacebookBindClick()
    {
        if (!MPReleaseFeatures.Facebook) return;
        await RunBindOperationAsync(token => MPLoginManager.Instance.LinkAsync(
            MPLoginType.Facebook,
            new MPThirdPartyLoginRequest
            {
                loginType = MPLoginType.Facebook,
                provider = MPLoginType.Facebook,
                forceLink = false
            },
            token));
    }

    private static void OnPrivacyPolicyClick()
    {
        Application.OpenURL(LEGAL_DOCUMENT_URL);
    }

    private static void OnTermsOfServiceClick()
    {
        Application.OpenURL(LEGAL_DOCUMENT_URL);
    }

    /// <summary>同一时间只执行一次绑定，结束前禁止六个按钮重复触发。</summary>
    private async Task RunBindOperationAsync(Func<CancellationToken, Task<MPLoginResult>> operation)
    {
        if (m_isRunning || m_isClosing || m_conflictPromptShowing || operation == null)
            return;

        CancelOperation();
        CancellationTokenSource operationCancellation = new CancellationTokenSource();
        CancellationToken operationToken = operationCancellation.Token;
        m_operationCancellation = operationCancellation;
        m_isRunning = true;
        SetInteractable(false);

        try
        {
            MPLoginResult result = await operation(operationToken);
            operationToken.ThrowIfCancellationRequested();
            if (result != null && result.isSuccess)
            {
                Action<MPLoginResult> callback = m_onBindSucceeded;
                m_isClosing = true;
                DestroyWindow();
                callback?.Invoke(result);
                return;
            }

            // 用户主动取消不是登录异常，保持绑定弹窗并恢复按钮即可。
            if (result?.error?.code == MPLoginErrorCodes.UserCancelled) return;

            // 首次绑定成功已在上面直接结束；仅服务端明确返回“关联了其他账号”才二次确认。
            // 当前玩家已有同平台绑定（10004）不是切换账号的理由，不弹出这个提示。
            if (result?.error?.code == MPLoginErrorCodes.AccountBindingConflict && result.loginType != MPLoginType.Apple)
            {
                Debug.LogWarning($"[MPAccountBindPop] 第三方身份已关联其他账号，ServiceCode={result.error.serviceErrorCode}");
                var profile = await MPLoginManager.Instance.LoadLocalProfileAsync(operationToken);
                if (profile?.IsIndependentGuest == true)
                {
                    // 先完整保存游客，再授权读取已有账号，通过资产对比页决定使用哪一份存档。
                    if (!await MPCloudSaveManager.Instance.PrepareGuestSaveChoiceAsync(result.loginType, operationToken))
                        return;
                    operationToken.ThrowIfCancellationRequested();
                    if (this == null || IsDestoried) return;
                    var loading = UIManager.Inst.ShowWindow<MPLoadingView>(new MPLoadingViewUIMsgData(
                        MPLoginStartupResult.ShowLoginSelection(null, MPLoginProvider.Unknown),
                        initialProvider: result.loginType), true, UILayer.Top);
                    if (loading == null) return;
                    m_isClosing = true;
                    Action onClose = m_onClose;
                    DestroyWindow();
                    onClose?.Invoke();
                    return;
                }
                ShowBindingConflict();
                return;
            }
            Debug.LogWarning($"[MPAccountBindPop] 账号绑定/登录未完成，Provider={result?.loginType}, Code={result?.error?.code}, ServiceCode={result?.error?.serviceErrorCode}");
        }
        catch (OperationCanceledException)
        {
            // 页面关闭时取消等待，不再访问已经释放的 UI。
        }
        catch (Exception exception)
        {
            // 授权异常正文可能包含敏感信息，只记录类型；结构化失败另行记录错误码。
            Debug.LogError($"[MPAccountBindPop] 绑定/登录操作异常：{exception.GetType().Name}");
        }
        finally
        {
            if (ReferenceEquals(m_operationCancellation, operationCancellation))
            {
                m_operationCancellation = null;
                operationCancellation.Dispose();
            }

            m_isRunning = false;
            if (this != null && !IsDestoried && !m_isClosing)
                SetInteractable(!m_conflictPromptShowing);
        }
    }

    /// <summary>绑定冲突时让用户主动选择是否保存当前账号并前往登录已有账号。</summary>
    private void ShowBindingConflict()
    {
        if (m_conflictPromptShowing || IsDestoried) return;
        m_conflictPromptShowing = true;
        SetInteractable(false);
        m_conflictConfirmation = MPSecondConfirmationPop.Show(
            "Account already linked",
            "This sign-in method is already linked to another account. Save your current progress and go to the sign-in screen to access that account? Your saves will remain separate.",
            "Continue",
            async token =>
            {
                if (this == null || IsDestoried) return false;
                if (!await MPCloudSaveManager.Instance.FlushAsync(token)) return false;
                token.ThrowIfCancellationRequested();
                await MPLoginManager.Instance.LogoutAsync(clearCredentials: false, cancellationToken: token);
                return this != null && !IsDestoried;
            },
            onCancel: () =>
            {
                if (this == null || IsDestoried) return;
                m_conflictPromptShowing = false;
                m_conflictConfirmation = null;
                SetInteractable(true);
            },
            onConfirmed: () =>
            {
                if (this == null || IsDestoried) return;
                m_conflictConfirmation = null;
                m_isClosing = true;
                Action onClose = m_onClose;
                DestroyWindow();
                onClose?.Invoke();
                MPLoginStartupResult startup = MPLoginStartupResult.ShowLoginSelection(
                    null, MPLoginProvider.Unknown, "Sign in with the account you want to use.");
                UIManager.Inst.ShowWindow<MPLoadingView>(new MPLoadingViewUIMsgData(startup), true, UILayer.Top);
            });
        if (m_conflictConfirmation == null)
        {
            m_conflictPromptShowing = false;
            SetInteractable(true);
        }
    }

    private void CancelOperation()
    {
        CancellationTokenSource cancellation = m_operationCancellation;
        m_operationCancellation = null;
        if (cancellation == null)
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void SetInteractable(bool interactable)
    {
        SetButtonInteractable(m_closeBtn, interactable && !m_appleSignInPending);
        SetButtonInteractable(m_googleBindBtn, interactable && !m_appleSignInPending);
        SetButtonInteractable(m_appleBindBtn, interactable);
        SetButtonInteractable(m_facebookBindBtn, interactable && !m_appleSignInPending);
        SetButtonInteractable(m_privacyPolicyBtn, interactable);
        SetButtonInteractable(m_termsOfServiceBtn, interactable);
    }

    private static void RegisterButton(Button button, UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnregisterButton(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private static MPLoginResult CreateThirdPartyAuthFailure(
        MPLoginType loginType,
        MPThirdPartyAuthResult authResult)
    {
        string errorCode = authResult == null || string.IsNullOrEmpty(authResult.errorCode)
            ? MPLoginErrorCodes.ThirdPartyAuthFailed
            : authResult.errorCode;
        string errorMessage = authResult == null || string.IsNullOrEmpty(authResult.errorMessage)
            ? "第三方平台授权失败。"
            : authResult.errorMessage;
        return MPLoginResult.Failed(
            loginType,
            MPLoginError.Create(errorCode, errorMessage, errorCode != MPLoginErrorCodes.UserCancelled));
    }
}

/// <summary>保留原有打开参数结构，现有调用方无需调整。</summary>
public sealed class MPAccountBindPopUIMsgData : UIMsgData
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Action OnClose { get; private set; }
    public Action<MPLoginResult> OnBindSucceeded { get; private set; }

    public MPAccountBindPopUIMsgData(
        string title,
        string description,
        Action onClose,
        Action<MPLoginResult> onBindSucceeded)
    {
        Title = title;
        Description = description;
        OnClose = onClose;
        OnBindSucceeded = onBindSucceeded;
    }
}

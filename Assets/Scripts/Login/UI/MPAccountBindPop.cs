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
        SetButtonVisible(m_facebookBindBtn, configuration.EnableFacebookLogin);
    }

    private void OnCloseClick()
    {
        if (m_isRunning || m_isClosing)
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

    /// <summary>拉起原生 Apple 授权并绑定到当前账号。</summary>
    private async void OnAppleBindClick()
    {
        await RunBindOperationAsync(token => MPLoginManager.Instance.LinkAsync(
            MPLoginType.Apple,
            new MPThirdPartyLoginRequest
            {
                loginType = MPLoginType.Apple,
                provider = MPLoginType.Apple,
                forceLink = false
            },
            token));
    }

    /// <summary>通过已接入 MPFacebookAuthAdapter 的平台授权回调绑定 Facebook。</summary>
    private async void OnFacebookBindClick()
    {
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
        if (m_isRunning || m_isClosing || operation == null)
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

            Debug.LogWarning($"[MPAccountBindPop] 账号绑定失败：{result?.errorMessage ?? "未知错误"}");
        }
        catch (OperationCanceledException)
        {
            // 页面关闭时取消等待，不再访问已经释放的 UI。
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPAccountBindPop] 绑定操作异常：{exception}");
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
        SetButtonInteractable(m_closeBtn, interactable);
        SetButtonInteractable(m_googleBindBtn, interactable);
        SetButtonInteractable(m_appleBindBtn, interactable);
        SetButtonInteractable(m_facebookBindBtn, interactable);
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

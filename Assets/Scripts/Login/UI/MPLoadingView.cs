using System;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动时由 MPLauncher 从预制体创建，资源初始化后也可由 UIManager 打开登录选择页。
/// 只控制进度和登录交互；是否完成全部启动步骤由 MPLauncher 决定。
/// </summary>
[Component("MPLoadingView")]
public sealed class MPLoadingView : AWindow
{
    // 真实初始化尚未完成时的假进度上限，剩余部分由 CompleteLoading 填满。
    private const float WAIT_PROGRESS = 0.92f;

    // 页面阶段：加载中、登录面板入场中、等待登录/重试、完成收尾、已释放。
    private enum Stage { Loading, RevealingLogin, Login, Finishing, Released }

    // 进度区域：整体淡出、填充图片和百分比文字。
    [TransformPath("View/Progress")] private CanvasGroup m_progressGroup;
    [TransformPath("View/Progress/FillMask/Fill")] private Image m_fill;
    [TransformPath("View/Progress/Text")] private TMP_Text m_progressText;

    // 登录区域：CanvasGroup 管理透明度与射线，布局组件按当前可见按钮适配高度。
    [TransformPath("View/Login")] private CanvasGroup m_loginGroup;
    [TransformPath("View/Login")] private RectTransform m_loginRect;
    [TransformPath("View/Login")] private VerticalLayoutGroup m_loginLayout;
    [TransformPath("View/Login/AnonymousLogin")] private Button m_anonymousButton;
    [TransformPath("View/Login/GoogleLogin")] private Button m_googleButton;
    [TransformPath("View/Login/AppleLogin")] private Button m_appleButton;
    [TransformPath("View/Login/FacebookLogin")] private Button m_facebookButton;

    // 异常提示兼作重试入口；文字内容由预制体维护，代码只控制显隐和可点击状态。
    [TransformPath("View/Status")] private TMP_Text m_status;
    [TransformPath("View/Status")] private Button m_statusButton;

    // 页面释放时统一取消异步等待，防止登录/同步结束后继续操作已关闭的界面。
    private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
    // 页面切换序列与假进度动画分别持有，重试、切换状态和释放时统一清理。
    private Sequence m_transition;
    private Tween m_progressTween;
    private Stage m_stage;
    private float m_progress;
    // 忙碌、登录许可、重试许可与窗口焦点共同决定交互状态。
    private bool m_busy;
    private bool m_loginAllowed;
    private bool m_statusCanRetry;
    private bool m_hasFocus = true;
    // 启动场景重试由 MPLauncher 接管；通过 UIManager 打开时改用 RetryLogin。
    private Action m_retry;
    // 启动阶段登录成功后交还 MPLauncher，继续剩余初始化步骤。
    private Action<MPLoginResult> m_startupLoginSucceeded;
    // 非启动场景在用户数据同步、进度收尾及窗口关闭后通知调用方。
    private Action<MPLoginResult> m_loginSucceeded;

    /// <summary>此页面不启用 AWindow 的刘海屏适配。</summary>
    protected override bool ShouldAdaptToNotchScreen() => false;

    /// <summary>绑定按钮并初始化进度条；所有监听在 OnRelease 中对应移除。</summary>
    public override void OnCreate()
    {
        m_anonymousButton.onClick.AddListener(OnAnonymousClick);
        m_googleButton.onClick.AddListener(OnGoogleClick);
        m_appleButton.onClick.AddListener(OnAppleClick);
        m_facebookButton.onClick.AddListener(OnFacebookClick);
        m_statusButton.onClick.AddListener(OnStatusClick);
        m_progressGroup.interactable = false;
        m_progressGroup.blocksRaycasts = false;
        m_fill.type = Image.Type.Filled;
        m_fill.fillMethod = Image.FillMethod.Horizontal;
        m_fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        SetProgress(0f);
        BeginLoading();
    }

    /// <summary>由启动器注入重试与登录成功回调，页面不自行判断整个启动流程是否结束。</summary>
    public void ConfigureStartup(Action retry, Action<MPLoginResult> onLoginSucceeded)
    {
        m_retry = retry;
        m_startupLoginSucceeded = onLoginSucceeded;
    }

    /// <summary>处理 UIManager 打开页面时传入的登录状态及成功回调。</summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLoadingViewUIMsgData data = uiMsg?.GetMsg<MPLoadingViewUIMsgData>();
        if (data == null)
            return;
        m_loginSucceeded = data.OnLoginSucceeded;
        m_retry = RetryLogin;
        // 主动打开登录选择页不是异常，不显示“已登出/请选择登录方式”等常规状态。
        ShowLogin(data.StartupResult, data.StartupResult?.action != MPLoginStartupAction.ShowLoginSelection);
    }

    /// <summary>进入不可交互的加载阶段，并启动不受 Time.timeScale 影响的假进度动画。</summary>
    public void BeginLoading()
    {
        if (m_stage == Stage.Released)
            return;
        KillTweens();
        m_stage = Stage.Loading;
        m_loginAllowed = false;
        SetLoginInteraction(false);
        m_loginGroup.alpha = 0f;
        m_loginGroup.transform.localScale = Vector3.one * 0.4f;
        m_loginGroup.gameObject.SetActive(false);
        m_progressGroup.gameObject.SetActive(true);
        m_progressGroup.alpha = 1f;
        HideStatus();
        // 假进度只向前移动，重试时不归零，也绝不自行走到 100%。
        m_progressTween = DOVirtual.Float(m_progress, Mathf.Max(m_progress, WAIT_PROGRESS),
            12f, SetProgress).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>隐藏异常提示并立即关闭点击与射线，不清空预制体中的固定文案。</summary>
    private void HideStatus()
    {
        m_statusCanRetry = false;
        m_statusButton.interactable = false;
        m_status.raycastTarget = false;
        m_status.gameObject.SetActive(false);
    }

    /// <summary>仅用 message 是否为空判断要不要展示异常提示，不将其赋给 Status 文本。</summary>
    private void ShowErrorStatus(string message)
    {
        HideStatus();
        if (string.IsNullOrWhiteSpace(message))
            return;
        m_statusCanRetry = m_retry != null;
        // 文案由预制体维护，显示和隐藏时都不能覆盖或清空。
        m_status.gameObject.SetActive(true);
        RefreshInteraction();
    }

    /// <summary>展示登录选择；主动打开页面时可通过 showError 关闭异常提示。</summary>
    public void ShowLogin(MPLoginStartupResult result, bool showError = true)
    {
        ShowFailure(showError ? result?.message ?? "Login failed. Please try again." : null, true);
    }

    /// <summary>资源或用户数据初始化失败时仅开放重试，不能绕过失败步骤直接登录/进游戏。</summary>
    public void ShowInitializationFailure(string message)
    {
        ShowFailure(message, false);
    }

    /// <summary>先淡出进度，再按需缩放并淡入登录面板；入场结束后才开放交互。</summary>
    private void ShowFailure(string message, bool allowLogin)
    {
        if (m_stage == Stage.Released)
            return;
        KillTweens();
        m_stage = Stage.RevealingLogin;
        m_loginAllowed = allowLogin;
        ShowErrorStatus(message);
        SetLoginInteraction(false);
        if (allowLogin)
        {
            m_loginGroup.alpha = 0f;
            m_loginGroup.transform.localScale = Vector3.one * 0.4f;
            m_loginGroup.gameObject.SetActive(true);
            RefreshProviders();
        }
        else
        {
            m_loginGroup.gameObject.SetActive(false);
        }

        m_transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        m_transition.Append(m_progressGroup.DOFade(0f, 0.2f));
        m_transition.AppendCallback(() => m_progressGroup.gameObject.SetActive(false));
        if (allowLogin)
        {
            m_transition.Append(m_loginGroup.transform.DOScale(1f, 0.32f).SetEase(Ease.OutBack));
            m_transition.Join(m_loginGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad));
        }
        m_transition.OnComplete(() =>
        {
            m_transition = null;
            if (m_stage == Stage.Released)
                return;
            m_stage = Stage.Login;
            RefreshInteraction();
        });
    }

    /// <summary>唯一填满进度的入口；调用者必须已经完成资源、登录、用户数据等初始化。</summary>
    public void CompleteLoading(Action onCompleted)
    {
        if (m_stage == Stage.Finishing || m_stage == Stage.Released)
            return;
        BeginLoading();
        KillTweens();
        m_stage = Stage.Finishing;
        SetLoginInteraction(false);
        m_transition = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        m_transition.Append(DOVirtual.Float(m_progress, 1f, 0.35f, SetProgress).SetEase(Ease.OutQuad));
        m_transition.AppendInterval(0.12f);
        m_transition.OnComplete(() =>
        {
            m_transition = null;
            if (m_stage != Stage.Released)
                onCompleted?.Invoke();
        });
    }

    /// <summary>统一更新进度数值、图片填充和百分比，避免显示与内部进度不一致。</summary>
    private void SetProgress(float value)
    {
        m_progress = Mathf.Clamp01(value);
        m_fill.fillAmount = m_progress;
        m_progressText.SetText("{0}%", Mathf.FloorToInt(m_progress * 100f));
    }

    /// <summary>根据配置与平台支持情况显示登录入口，再按实际可见内容调整布局高度。</summary>
    private void RefreshProviders()
    {
        MPLoginConfiguration config = MPLoginManager.Instance.Configuration;
        m_anonymousButton.gameObject.SetActive(config.EnableAnonymousLogin);
        m_googleButton.gameObject.SetActive(config.EnableGooglePlayGamesLogin && MPGooglePlayGamesAuthService.IsCurrentPlatformSupported);
        m_appleButton.gameObject.SetActive(config.EnableAppleLogin && MPAppleAuthAdapter.IsCurrentPlatformSupported);
        m_facebookButton.gameObject.SetActive(config.EnableFacebookLogin);
        RefreshLoginHeight();
    }

    /// <summary>收紧登录容器高度，避免部分平台隐藏按钮后留下过大的垂直间隔。</summary>
    private void RefreshLoginHeight()
    {
        // 必须在 Login 激活且各平台按钮显隐完成后计算，UGUI 不统计未激活的子节点。
        // 使用布局组件的首选高度，自动计入按钮尺寸、spacing 和 padding，不写死按钮数量。
        m_loginLayout.childForceExpandHeight = false;
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_loginRect);
        float height = Mathf.Max(0f, m_loginLayout.preferredHeight);
        if (!Mathf.Approximately(m_loginRect.rect.height, height))
        {
            m_loginRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_loginRect);
        }
    }

    /// <summary>跟随窗口系统的焦点变化控制交互，被其他窗口遮挡时不接受登录或重试。</summary>
    public override void OnFocus(bool focus)
    {
        m_hasFocus = focus;
        RefreshInteraction();
    }

    /// <summary>集中计算交互开关，防止动画未完成或异步请求处理中重复触发按钮。</summary>
    private void RefreshInteraction()
    {
        bool ready = m_stage == Stage.Login && !m_busy && m_hasFocus;
        SetLoginInteraction(ready && m_loginAllowed);
        bool canRetry = ready && m_statusCanRetry && m_retry != null;
        m_statusButton.interactable = canRetry;
        m_status.raycastTarget = canRetry;
    }

    /// <summary>同步设置父级 CanvasGroup 和各按钮，确保点击与射线检测一起开关。</summary>
    private void SetLoginInteraction(bool enabled)
    {
        m_loginGroup.interactable = enabled;
        m_loginGroup.blocksRaycasts = enabled;
        m_anonymousButton.interactable = enabled;
        m_googleButton.interactable = enabled;
        m_appleButton.interactable = enabled;
        m_facebookButton.interactable = enabled;
    }

    /// <summary>点击异常提示时执行当前流程的重试，不主动切换为游客账号。</summary>
    private void OnStatusClick()
    {
        if (m_stage != Stage.Login || m_busy || !m_hasFocus || !m_statusCanRetry || m_retry == null)
            return;
        m_retry();
    }

    /// <summary>已登录则继续数据初始化，否则按登录管理器记录恢复上次登录方式。</summary>
    private void RetryLogin()
    {
        _ = RunLoginAsync(token => MPLoginManager.Instance.IsLoggedIn
            ? Task.FromResult(MPLoginStartupResult.EnterGame(MPLoginResult.Success(MPLoginManager.Instance.CurrentSession)))
            : MPLoginManager.Instance.RetryStartupAsync(token));
    }

    /// <summary>主动选择本机独立游客，由管理器决定复用或新建；已有游客凭证失效时不静默重建。</summary>
    private void OnAnonymousClick()
    {
        // Status 才是恢复上次账号；主动点击游客始终选择独立游客槽，不强制重建。
        _ = RunLoginAsync(token => MPLoginManager.Instance.ContinueAsGuestAsync(token));
    }

    /// <summary>先向 Google Play Games 获取授权码，再交给登录管理器完成账号登录。</summary>
    private void OnGoogleClick()
    {
        _ = RunLoginAsync(async token =>
        {
            MPThirdPartyAuthResult auth = await MPGooglePlayGamesAuthService.RequestAuthCodeAsync(false, token);
            token.ThrowIfCancellationRequested();
            if (auth == null || !auth.success)
                return AuthorizationFailure(auth);
            return await MPLoginManager.Instance.LoginWithProviderAsync(MPLoginType.GooglePlayGames,
                new MPThirdPartyLoginRequest { authorizationCode = auth.authorizationCode, platformUserId = auth.platformUserId, createAccount = true }, token);
        });
    }

    /// <summary>选择 Apple 登录，平台授权与账号切换交由对应适配器和登录管理器处理。</summary>
    private void OnAppleClick()
    {
        _ = RunLoginAsync(token => MPLoginManager.Instance.LoginWithProviderAsync(MPLoginType.Apple,
            new MPThirdPartyLoginRequest { createAccount = true }, token));
    }

    /// <summary>选择 Facebook 登录；具体授权能力由项目中的 Facebook 适配器提供。</summary>
    private void OnFacebookClick()
    {
        _ = RunLoginAsync(token => MPLoginManager.Instance.LoginWithProviderAsync(MPLoginType.Facebook,
            new MPThirdPartyLoginRequest { createAccount = true }, token));
    }

    /// <summary>将第三方授权失败统一转换为页面可处理的登录结果，兼容空返回值。</summary>
    private static MPLoginStartupResult AuthorizationFailure(MPThirdPartyAuthResult auth)
    {
        return MPLoginStartupResult.Failed(MPLoginError.Create(
            auth?.errorCode ?? MPLoginErrorCodes.ThirdPartyAuthFailed,
            auth?.errorMessage ?? "Authorization failed. Please try again."));
    }

    /// <summary>
    /// 登录和重试共用的执行入口：防止重复请求、处理失败，再衔接启动器或用户数据同步。
    /// 页面关闭时通过生命周期令牌终止后续等待和 UI 更新。
    /// </summary>
    private async Task RunLoginAsync(Func<CancellationToken, Task<MPLoginStartupResult>> operation)
    {
        if (m_stage != Stage.Login || m_busy || !m_hasFocus)
            return;
        m_busy = true;
        RefreshInteraction();
        HideStatus();
        CancellationToken token = m_lifetime.Token;
        try
        {
            MPLoginStartupResult result = await operation(token);
            token.ThrowIfCancellationRequested();
            if (this == null || IsDestoried)
                return;
            if (result?.action != MPLoginStartupAction.EnterGame || !MPLoginManager.Instance.IsLoggedIn)
            {
                // 数据重试期间账号也可能失效，此时要重新开放登录入口，不能停在不可点击的错误文字上。
                if (!m_loginAllowed)
                    ShowLogin(result);
                else
                    ShowErrorStatus(result?.message ?? "Login failed. Please try again.");
                return;
            }

            BeginLoading();
            if (m_startupLoginSucceeded != null)
            {
                // 启动器仍需完成资源、用户数据等步骤，此处不能直接填满进度或关闭页面。
                m_startupLoginSucceeded(result.loginResult);
                return;
            }

            // 设置页登出后也使用此界面：等新账号的数据准备好再返回原页面。
            bool synced = await MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync(token);
            token.ThrowIfCancellationRequested();
            if (!synced)
            {
                ShowInitializationFailure("Could not sync your data.");
                return;
            }
            MPVibrationManager.Instance.Initialize();
            CompleteLoading(() =>
            {
                // 释放窗口会清空字段，先保存回调再关闭，保证调用方仍能收到成功结果。
                Action<MPLoginResult> callback = m_loginSucceeded;
                DestroyWindow();
                callback?.Invoke(result.loginResult);
            });
        }
        catch (OperationCanceledException)
        {
            // 页面释放触发的取消无需提示；页面仍在时的授权取消才重新开放登录选择。
            if (!token.IsCancellationRequested && this != null && !IsDestoried)
                ShowFailure("Sign-in was cancelled. Please try again.", true);
        }
        catch (Exception exception)
        {
            // 不输出可能含授权令牌的异常响应正文。
            Debug.LogWarning($"[MPLoadingView] 登录/同步失败：{exception.GetType().Name}");
            if (!token.IsCancellationRequested && this != null && !IsDestoried)
                ShowFailure("Sign-in or data sync failed.", !MPLoginManager.Instance.IsLoggedIn);
        }
        finally
        {
            if (!token.IsCancellationRequested && this != null && !IsDestoried)
            {
                m_busy = false;
                RefreshInteraction();
            }
        }
    }

    /// <summary>清理当前序列与节点动画，防止旧动画的透明度、缩放或回调干扰新状态。</summary>
    private void KillTweens()
    {
        m_transition?.Kill();
        m_transition = null;
        m_progressTween?.Kill();
        m_progressTween = null;
        if (m_loginGroup != null)
        {
            m_loginGroup.transform.DOKill();
            m_loginGroup.DOKill();
        }
        if (m_progressGroup != null)
            m_progressGroup.DOKill();
    }

    /// <summary>幂等释放：取消异步等待，停止动画，移除按钮监听并断开外部回调引用。</summary>
    public override void OnRelease()
    {
        if (m_stage == Stage.Released)
            return;
        m_stage = Stage.Released;
        m_lifetime.Cancel();
        m_lifetime.Dispose();
        KillTweens();
        m_anonymousButton?.onClick.RemoveListener(OnAnonymousClick);
        m_googleButton?.onClick.RemoveListener(OnGoogleClick);
        m_appleButton?.onClick.RemoveListener(OnAppleClick);
        m_facebookButton?.onClick.RemoveListener(OnFacebookClick);
        m_statusButton?.onClick.RemoveListener(OnStatusClick);
        m_retry = null;
        m_startupLoginSucceeded = null;
        m_loginSucceeded = null;
    }

    // 启动器直接销毁实例时也执行清理；Released 状态可避免重复释放。
    private void OnDestroy() => OnRelease();
}

/// <summary>通过 UIManager 打开加载/登录页时传入的业务参数。</summary>
public sealed class MPLoadingViewUIMsgData : UIMsgData
{
    /// <summary>决定首次展示登录选择还是异常提示的启动结果。</summary>
    public MPLoginStartupResult StartupResult { get; }
    /// <summary>账号登录、数据同步与窗口收尾全部完成后的通知回调。</summary>
    public Action<MPLoginResult> OnLoginSucceeded { get; }

    /// <summary>封装页面初始状态和可选的成功回调。</summary>
    public MPLoadingViewUIMsgData(MPLoginStartupResult startupResult, Action<MPLoginResult> onLoginSucceeded = null)
    {
        StartupResult = startupResult;
        OnLoginSucceeded = onLoginSucceeded;
    }
}

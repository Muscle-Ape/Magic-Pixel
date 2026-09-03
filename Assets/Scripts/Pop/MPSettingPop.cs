using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Component("MPSettingPop")]
public class MPSettingPop : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 开关按钮移动动画时长。
    /// </summary>
    private const float SWITCH_MOVE_DURATION = 0.18f;

    /// <summary>
    /// 开关打开状态淡入淡出时长。
    /// </summary>
    private const float SWITCH_FADE_DURATION = 0.15f;

    /// <summary>
    /// 关闭按钮。
    /// </summary>
    [TransformPath("View/Window/CloseBtn")]
    private Button m_closeBtn;

    /// <summary>
    /// 背景音乐开关。
    /// </summary>
    [TransformPath("View/Window/BGM/Switch")]
    private Button m_bgmSwitch;

    /// <summary>
    /// 背景音乐开关滑块。
    /// </summary>
    [TransformPath("View/Window/BGM/Switch/Btn")]
    private RectTransform m_bgmSwitchBtn;

    /// <summary>
    /// 背景音乐开关打开状态显示节点。
    /// </summary>
    [TransformPath("View/Window/BGM/Switch/On")]
    private RectTransform m_bgmSwitchOn;

    /// <summary>
    /// 音效开关。
    /// </summary>
    [TransformPath("View/Window/Sound/Switch")]
    private Button m_soundSwitch;

    /// <summary>
    /// 音效开关滑块。
    /// </summary>
    [TransformPath("View/Window/Sound/Switch/Btn")]
    private RectTransform m_soundSwitchBtn;

    /// <summary>
    /// 音效开关打开状态显示节点。
    /// </summary>
    [TransformPath("View/Window/Sound/Switch/On")]
    private RectTransform m_soundSwitchOn;

    /// <summary>
    /// 震动开关。
    /// </summary>
    [TransformPath("View/Window/Vibration/Switch")]
    private Button m_vibrationSwitch;

    /// <summary>
    /// 震动开关滑块。
    /// </summary>
    [TransformPath("View/Window/Vibration/Switch/Btn")]
    private RectTransform m_vibrationSwitchBtn;

    /// <summary>
    /// 震动开关打开状态显示节点。
    /// </summary>
    [TransformPath("View/Window/Vibration/Switch/On")]
    private RectTransform m_vibrationSwitchOn;

    /// <summary>
    /// 未绑定正式登录方式时显示的登录/绑定按钮。
    /// 匿名账号虽然已经通过 Unity Authentication 登录，但还不能跨设备恢复，因此这里仍展示 LogIn 引导绑定。
    /// </summary>
    [TransformPath("View/Window/LogIn")]
    private Button m_logInBtn;

    /// <summary>
    /// 已绑定正式登录方式后显示的登出按钮。
    /// 点击后会清理当前凭证，并打开登录页让玩家重新选择登录方式。
    /// </summary>
    [TransformPath("View/Window/LogOut")]
    private Button m_logOutBtn;

    [TransformPath("View/Window/ReplayBtn")]
    private Button m_replayBtn;

    [TransformPath("View/Window/AccountStatus")]
    private TMP_Text m_accountStatus;

    [TransformPath("View/Window/GameOptions")]
    private RectTransform m_gameOptions;

    [TransformPath("View/Window/GameOptions/CurrentColor")]
    private Image m_currentColor;

    [TransformPath("View/Window/GameOptions/Colors")]
    private RectTransform m_colors;

    private static readonly Color[] FILL_COLORS =
    {
        Color.white,
        new Color(1f, 0.64f, 0.25f),
        new Color(0.5f, 1f, 0.55f),
        new Color(0.83f, 0.6f, 1f),
        new Color(1f, 0.52f, 0.73f),
        new Color(0.28f, 0.3f, 0.36f),
    };

    private static readonly string[] FILL_COLOR_ICONS =
    {
        "popup_fill_blue", "popup_fill_orange", "popup_fill_green",
        "popup_fill_purple", "popup_fill_pink", "popup_fill_gray",
    };

    private readonly Dictionary<Button, UnityAction> m_colorListeners = new Dictionary<Button, UnityAction>();
    private readonly Sprite[] m_fillColorSprites = new Sprite[FILL_COLORS.Length];
    private MPSettingPopUIMsgData m_gameData;
    private bool m_isActionPromptShowing;
    private bool m_isClosing;

    /// <summary>
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    /// <summary>
    /// 登录按钮显隐刷新任务的取消源。
    /// 设置弹窗关闭或重复刷新时取消，避免异步回调访问已销毁 UI。
    /// </summary>
    private CancellationTokenSource m_loginStateCancellation;

    /// <summary>
    /// 登出操作的取消源。
    /// </summary>
    private CancellationTokenSource m_logoutCancellation;

    /// <summary>
    /// 当前是否正在执行登录相关按钮操作，用于防止重复点击。
    /// </summary>
    private bool m_isLoginActionRunning;
    private bool m_logoutCommitted;
    private MPLocalLoginProfile m_logoutProfile;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();
        m_gameData = uiMsg as MPSettingPopUIMsgData;
        m_isClosing = false;

        RegisterUI();
        RefreshGameOptions();
        RefreshAllSwitches(false);
        RefreshLoginButtons();
    }

    public override void OnRelease()
    {
        CancelLoginStateRefresh();
        CancelLogoutOperation();
        UnregisterUI();
        KillSwitchTween(m_bgmSwitchBtn, m_bgmSwitchOn);
        KillSwitchTween(m_soundSwitchBtn, m_soundSwitchOn);
        KillSwitchTween(m_vibrationSwitchBtn, m_vibrationSwitchOn);
        ClearColorListeners();
        Array.Clear(m_fillColorSprites, 0, m_fillColorSprites.Length);
        m_gameData = null;
        m_logoutProfile = null;
        MPLoad.ReleaseAll(this);
    }

    /// <summary>
    /// 注册设置弹窗按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        if (m_closeBtn != null)
        {
            m_closeBtn.onClick.RemoveListener(OnCloseClick);
            m_closeBtn.onClick.AddListener(OnCloseClick);
        }

        if (m_bgmSwitch != null)
        {
            m_bgmSwitch.onClick.RemoveListener(OnBgmSwitchClick);
            m_bgmSwitch.onClick.AddListener(OnBgmSwitchClick);
        }

        if (m_soundSwitch != null)
        {
            m_soundSwitch.onClick.RemoveListener(OnSoundSwitchClick);
            m_soundSwitch.onClick.AddListener(OnSoundSwitchClick);
        }

        if (m_vibrationSwitch != null)
        {
            m_vibrationSwitch.onClick.RemoveListener(OnVibrationSwitchClick);
            m_vibrationSwitch.onClick.AddListener(OnVibrationSwitchClick);
        }

        if (m_logInBtn != null)
        {
            m_logInBtn.onClick.RemoveListener(OnLogInClick);
            m_logInBtn.onClick.AddListener(OnLogInClick);
        }

        if (m_logOutBtn != null)
        {
            m_logOutBtn.onClick.RemoveListener(OnLogOutClick);
            m_logOutBtn.onClick.AddListener(OnLogOutClick);
        }

        if (m_replayBtn != null)
        {
            m_replayBtn.onClick.RemoveListener(OnReplayClick);
            m_replayBtn.onClick.AddListener(OnReplayClick);
        }

        MPLoginManager.Instance.LoginSucceeded -= OnLoginSucceeded;
        MPLoginManager.Instance.LoginSucceeded += OnLoginSucceeded;
        MPLoginManager.Instance.LoggedOut -= OnLoggedOut;
        MPLoginManager.Instance.LoggedOut += OnLoggedOut;
    }

    /// <summary>
    /// 移除设置弹窗按钮事件。
    /// </summary>
    private void UnregisterUI()
    {
        if (m_closeBtn != null)
        {
            m_closeBtn.onClick.RemoveListener(OnCloseClick);
        }

        if (m_bgmSwitch != null)
        {
            m_bgmSwitch.onClick.RemoveListener(OnBgmSwitchClick);
        }

        if (m_soundSwitch != null)
        {
            m_soundSwitch.onClick.RemoveListener(OnSoundSwitchClick);
        }

        if (m_vibrationSwitch != null)
        {
            m_vibrationSwitch.onClick.RemoveListener(OnVibrationSwitchClick);
        }

        if (m_logInBtn != null)
        {
            m_logInBtn.onClick.RemoveListener(OnLogInClick);
        }

        if (m_logOutBtn != null)
        {
            m_logOutBtn.onClick.RemoveListener(OnLogOutClick);
        }
        if (m_replayBtn != null)
            m_replayBtn.onClick.RemoveListener(OnReplayClick);

        MPLoginManager.Instance.LoginSucceeded -= OnLoginSucceeded;
        MPLoginManager.Instance.LoggedOut -= OnLoggedOut;
    }

    /// <summary>
    /// 根据当前用户设置刷新所有开关状态。
    /// </summary>
    /// <param name="playAnimation">是否播放动画。</param>
    private void RefreshAllSwitches(bool playAnimation)
    {
        RefreshSwitch(m_bgmSwitchBtn, m_bgmSwitchOn, MPUser.instance.isMusic, playAnimation);
        RefreshSwitch(m_soundSwitchBtn, m_soundSwitchOn, MPUser.instance.isSound, playAnimation);
        RefreshSwitch(m_vibrationSwitchBtn, m_vibrationSwitchOn, MPUser.instance.isVibration, playAnimation);
    }

    /// <summary>
    /// 点击背景音乐开关。
    /// </summary>
    private void OnBgmSwitchClick()
    {
        bool isOpen = !MPUser.instance.isMusic;
        MPUser.instance.SetMusicStatus(isOpen);

        if (isOpen)
        {
            MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
        }
        else
        {
            MPAudioManager.Instance.StopAllMusic();
        }

        RefreshSwitch(m_bgmSwitchBtn, m_bgmSwitchOn, isOpen, true);

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 点击音效开关。
    /// </summary>
    private void OnSoundSwitchClick()
    {
        bool isOpen = !MPUser.instance.isSound;
        MPUser.instance.SetSoundStatus(isOpen);

        if (isOpen)
        {
            MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
        }
        else
        {
            MPAudioManager.Instance.StopAllSound();
        }

        RefreshSwitch(m_soundSwitchBtn, m_soundSwitchOn, isOpen, true);

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 点击震动开关。
    /// </summary>
    private void OnVibrationSwitchClick()
    {
        bool isOpen = !MPUser.instance.isVibration;
        MPVibrationManager.Instance.SetEnabled(isOpen);
        RefreshSwitch(m_vibrationSwitchBtn, m_vibrationSwitchOn, isOpen, true);

        if (isOpen)
        {
            // 开启后立即给出一次反馈，让玩家确认设置已经生效。
            MPVibrationManager.Instance.PlaySelection();
        }

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 点击登录按钮。
    /// 当前已匿名登录时打开账号绑定弹窗；未登录时打开完整登录选择页。
    /// </summary>
    private void OnLogInClick()
    {
        if (m_isLoginActionRunning)
        {
            return;
        }

        m_isLoginActionRunning = true;
        SetLoginButtonsInteractable(false);

        try
        {
            // 已经匿名登录则打开绑定弹窗
            if (MPLoginManager.Instance.IsLoggedIn)
            {
                OpenAccountBindPop();
                return;
            }

            // 否则打开登录页面
            OpenLoginSelectionPage(null, "Please select your login method.");
            CloseSettingPop();
        }
        finally
        {
            if (!IsDestoried)
            {
                m_isLoginActionRunning = false;
                SetLoginButtonsInteractable(true);
            }
        }
    }

    /// <summary>
    /// 点击登出按钮。
    /// clearCredentials 使用 true，确保下一次打开登录页不会立刻自动恢复到刚退出的账号。
    /// </summary>
    private void OnLogOutClick()
    {
        if (m_isLoginActionRunning || m_isActionPromptShowing)
        {
            return;
        }

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
        m_isActionPromptShowing = true;
        m_logoutCommitted = false;
        m_logoutProfile = null;
        MPSecondConfirmationPop.Show(
            "Log Out?",
            "Your current progress will be saved to the cloud before signing out. If saving fails, you will stay signed in.",
            "Log Out",
            ConfirmLogoutAsync,
            OnActionPromptCancelled,
            "Stay Signed In",
            OnLogoutConfirmed);
    }

    private async Task<bool> ConfirmLogoutAsync(CancellationToken confirmationToken)
    {
        if (this == null || IsDestoried || m_isLoginActionRunning)
            return false;

        m_isLoginActionRunning = true;
        SetLoginButtonsInteractable(false);
        CancelLogoutOperation();
        m_logoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(confirmationToken);
        CancellationTokenSource cancellation = m_logoutCancellation;

        try
        {
            MPLocalLoginProfile profile = await MPLoginManager.Instance.LoadLocalProfileAsync(cancellation.Token);
            bool saved = await MPCloudSaveManager.Instance.FlushAsync(cancellation.Token);
            if (!saved)
                throw new InvalidOperationException("Cloud save failed. Your account is still signed in. Please retry or cancel.");
            await MPLoginManager.Instance.LogoutAsync(clearCredentials: true, cancellationToken: cancellation.Token);

            if (IsDestoried || cancellation.IsCancellationRequested)
            {
                return false;
            }

            // 此阶段只提交业务结果。等确认弹窗彻底关闭后再切换 UI 历史栈。
            m_logoutProfile = profile;
            m_logoutCommitted = true;
            return true;
        }
        finally
        {
            if (m_logoutCancellation == cancellation)
            {
                m_logoutCancellation = null;
                cancellation.Dispose();
            }

            if (this != null && !IsDestoried && !m_logoutCommitted)
            {
                m_isLoginActionRunning = false;
                SetLoginButtonsInteractable(true);
            }
        }
    }

    private void OnLogoutConfirmed()
    {
        if (this == null || IsDestoried || m_isClosing || !m_logoutCommitted)
            return;

        MPLocalLoginProfile profile = m_logoutProfile;
        CloseSettingPop(() => OpenLoginSelectionPage(profile, "已登出，请选择登录方式。"));
    }

    private void OnActionPromptCancelled()
    {
        if (this == null || IsDestoried)
            return;
        m_isActionPromptShowing = false;
    }

    /// <summary>
    /// 点击关闭按钮。
    /// </summary>
    private void OnCloseClick()
    {
        if (m_isLoginActionRunning || m_isClosing)
            return;
        CloseSettingPop();
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 监听登录或绑定成功事件，重新计算设置页登录按钮显隐。
    /// </summary>
    /// <param name="session">最新登录会话。</param>
    private void OnLoginSucceeded(MPUserSession session)
    {
        RefreshLoginButtons();
    }

    /// <summary>
    /// 监听登出完成事件，重新计算设置页登录按钮显隐。
    /// </summary>
    private void OnLoggedOut()
    {
        RefreshLoginButtons();
    }

    /// <summary>
    /// 根据当前本地登录资料刷新 LogIn 和 LogOut 的显隐。
    /// </summary>
    private async void RefreshLoginButtons()
    {
        CancelLoginStateRefresh();
        m_loginStateCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = m_loginStateCancellation;
        SetLoginButtonsInteractable(false);

        try
        {
            MPLocalLoginProfile profile = await MPLoginManager.Instance.LoadLocalProfileAsync(cancellation.Token);
            if (IsDestoried || cancellation.IsCancellationRequested)
            {
                return;
            }

            ApplyLoginButtonState(profile);
        }
        catch (OperationCanceledException)
        {
            // 弹窗关闭或新的刷新任务启动时会取消旧任务。
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPSettingPop] 刷新登录按钮状态失败：{exception.Message}");
            ApplyLoginButtonState(null);
        }
        finally
        {
            if (m_loginStateCancellation == cancellation)
            {
                m_loginStateCancellation = null;
                cancellation.Dispose();
            }

            if (!IsDestoried)
            {
                SetLoginButtonsInteractable(!m_isLoginActionRunning);
            }
        }
    }

    /// <summary>
    /// 应用登录按钮显隐。
    /// 已绑定正式身份时显示 LogOut，否则显示 LogIn。
    /// </summary>
    /// <param name="profile">本地登录资料，可能为空。</param>
    private void ApplyLoginButtonState(MPLocalLoginProfile profile)
    {
        bool showLogOut = ShouldShowLogOut(profile);
        SetButtonVisible(m_logInBtn, !showLogOut);
        SetButtonVisible(m_logOutBtn, showLogOut);
        if (m_accountStatus != null)
        {
            m_accountStatus.text = !MPLoginManager.Instance.IsLoggedIn
                ? "Offline — sign in to protect your progress."
                : showLogOut ? "Account linked — progress can be restored."
                : "Guest account — link an account before changing devices.";
        }
    }

    /// <summary>
    /// 判断当前是否应该显示登出按钮。
    /// 本地资料有绑定标记时优先相信本地资料；本地资料缺失时再回退到当前会话类型。
    /// </summary>
    /// <param name="profile">本地登录资料，可能为空。</param>
    /// <returns>需要显示 LogOut 时返回 true。</returns>
    private bool ShouldShowLogOut(MPLocalLoginProfile profile)
    {
        if (!MPLoginManager.Instance.IsLoggedIn)
        {
            return false;
        }

        if (profile != null && HasAnyBoundIdentity(profile))
        {
            return true;
        }

        MPUserSession session = MPLoginManager.Instance.CurrentSession;
        return session != null &&
               !session.isGuest &&
               session.loginType != MPLoginType.None &&
               session.loginType != MPLoginType.Guest;
    }

    /// <summary>
    /// 判断本地资料里是否已经记录过任何正式登录方式。
    /// </summary>
    /// <param name="profile">本地登录资料。</param>
    /// <returns>存在绑定方式时返回 true。</returns>
    private static bool HasAnyBoundIdentity(MPLocalLoginProfile profile)
    {
        return profile.hasBoundIdentity ||
               profile.accountType == MPAccountType.Bound ||
               profile.hasUsernamePasswordBinding ||
               profile.hasGoogleBinding ||
               profile.hasGooglePlayGamesBinding ||
               profile.hasAppleBinding ||
               profile.hasFacebookBinding;
    }

    /// <summary>
    /// 打开账号绑定弹窗。
    /// 这里不会关闭设置弹窗，绑定完成后玩家会回到设置页并看到按钮状态刷新。
    /// </summary>
    private void OpenAccountBindPop()
    {
        UIManager.Inst.ShowWindow<MPAccountBindPop>(
            new MPAccountBindPopUIMsgData(
                "Bind Account",
                "After binding your account, you can restore your current progress when changing devices or reinstalling the game.",
                null,
                OnAccountBindSucceeded),
            true,
            UILayer.Top);
    }

    /// <summary>
    /// 账号绑定成功后的回调。
    /// </summary>
    /// <param name="result">绑定结果。</param>
    private void OnAccountBindSucceeded(MPLoginResult result)
    {
        RefreshLoginButtons();
    }

    /// <summary>
    /// 打开完整登录选择页。
    /// </summary>
    /// <param name="profile">用于登录页展示偏好的本地资料。</param>
    /// <param name="message">登录页状态提示。</param>
    private static void OpenLoginSelectionPage(MPLocalLoginProfile profile, string message)
    {
        MPLoginProvider preferredProvider = profile == null ? MPLoginProvider.Unknown : profile.lastLoginProvider;
        MPLoginStartupResult startupResult = MPLoginStartupResult.ShowLoginSelection(profile, preferredProvider, message);
        UIManager.Inst.ShowWindow<MPLoginView>(new MPLoginViewUIMsgData(startupResult, null), true, UILayer.Top);
    }

    /// <summary>
    /// 关闭当前设置弹窗。
    /// </summary>
    private void CloseSettingPop(Action afterClose = null)
    {
        if (m_isClosing || IsDestoried)
            return;
        m_isClosing = true;
        if (m_popScaleAnimation != null)
        {
            m_popScaleAnimation.Close(afterClose);
            return;
        }

        DestroyWindow();
        afterClose?.Invoke();
    }

    /// <summary>
    /// 取消登录按钮显隐刷新任务。
    /// </summary>
    private void CancelLoginStateRefresh()
    {
        if (m_loginStateCancellation == null)
        {
            return;
        }

        m_loginStateCancellation.Cancel();
        m_loginStateCancellation.Dispose();
        m_loginStateCancellation = null;
    }

    /// <summary>
    /// 取消正在执行的登出任务。
    /// </summary>
    private void CancelLogoutOperation()
    {
        if (m_logoutCancellation == null)
        {
            return;
        }

        m_logoutCancellation.Cancel();
        m_logoutCancellation.Dispose();
        m_logoutCancellation = null;
    }

    /// <summary>
    /// 设置登录相关按钮是否可交互。
    /// </summary>
    /// <param name="interactable">是否可点击。</param>
    private void SetLoginButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(m_logInBtn, interactable);
        SetButtonInteractable(m_logOutBtn, interactable);
        SetButtonInteractable(m_closeBtn, !m_isLoginActionRunning && !m_isClosing);
        SetButtonInteractable(m_replayBtn, !m_isLoginActionRunning && !m_isClosing);
    }

    private void RefreshGameOptions()
    {
        bool inGame = m_gameData != null && m_gameData.isInGame;
        SetButtonVisible(m_replayBtn, inGame && m_gameData.replayAction != null);
        if (m_gameOptions != null)
            m_gameOptions.gameObject.SetActive(inGame);
        ClearColorListeners();
        if (!inGame || m_colors == null)
            return;

        for (int i = 0; i < m_colors.childCount; i++)
        {
            Transform option = m_colors.GetChild(i);
            bool valid = i < FILL_COLORS.Length;
            option.gameObject.SetActive(valid);
            if (!valid)
                continue;
            Button button = option.GetComponent<Button>();
            Image image = option.GetComponent<Image>();
            Color color = FILL_COLORS[i];
            if (m_fillColorSprites[i] == null)
                m_fillColorSprites[i] = MPRewardPopupIcons.LoadSprite(FILL_COLOR_ICONS[i], this, FILL_COLOR_ICONS[0]);
            MPRewardPopupIcons.Apply(image, m_fillColorSprites[i]);
            Transform select = option.Find("Select");
            if (select != null)
                MPRewardPopupIcons.Load(select.GetComponent<Image>(), "popup_selection_frame", this, null);
            if (button != null)
            {
                UnityAction listener = () => OnFillColorClick(color);
                m_colorListeners.Add(button, listener);
                button.onClick.AddListener(listener);
            }
        }
        RefreshColorSelection();
    }

    private void OnFillColorClick(Color color)
    {
        if (m_isClosing || m_gameData == null || !m_gameData.isInGame)
            return;
        MPUser.instance.SetGameFillColor(color);
        m_gameData.fillColorChanged?.Invoke(color);
        RefreshColorSelection();
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    private void RefreshColorSelection()
    {
        Color color = MPUser.instance.gameFillColor;
        int selectedIndex = FindFillColorIndex(color);
        MPRewardPopupIcons.Apply(m_currentColor, m_fillColorSprites[selectedIndex]);
        if (m_colors == null)
            return;
        for (int i = 0; i < m_colors.childCount && i < FILL_COLORS.Length; i++)
        {
            Transform select = m_colors.GetChild(i).Find("Select");
            if (select != null)
                select.gameObject.SetActive(i == selectedIndex);
        }
    }

    private static int FindFillColorIndex(Color color)
    {
        int selectedIndex = 0;
        float minDistance = float.MaxValue;
        for (int i = 0; i < FILL_COLORS.Length; i++)
        {
            Color difference = color - FILL_COLORS[i];
            float distance = difference.r * difference.r + difference.g * difference.g + difference.b * difference.b;
            if (distance >= minDistance)
                continue;
            selectedIndex = i;
            minDistance = distance;
        }
        // 存档转十六进制后会有颜色精度差异；只选择最接近的预览，不改写玩家的填色值。
        return selectedIndex;
    }

    private void ClearColorListeners()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in m_colorListeners)
            if (pair.Key != null)
                pair.Key.onClick.RemoveListener(pair.Value);
        m_colorListeners.Clear();
    }

    private void OnReplayClick()
    {
        if (m_gameData?.replayAction == null || m_isClosing || m_isActionPromptShowing)
            return;

        m_isActionPromptShowing = true;
        MPSecondConfirmationPop.Show(
            "Restart Level?",
            $"Restart {m_gameData.levelTitle}? All unfinished progress in this level will be discarded. Other levels are not affected.",
            "Restart",
            token =>
            {
                token.ThrowIfCancellationRequested();
                if (this == null || IsDestoried)
                    return Task.FromResult(false);
                return Task.FromResult(m_gameData?.replayAction != null);
            },
            OnActionPromptCancelled,
            "Continue Playing",
            () =>
            {
                if (this == null || IsDestoried || m_isClosing)
                    return;
                Action replay = m_gameData?.replayAction;
                if (replay != null)
                    CloseSettingPop(replay);
            });
    }

    /// <summary>
    /// 刷新单个开关的显示状态。
    /// </summary>
    /// <param name="switchBtn">开关滑块节点。</param>
    /// <param name="switchOn">开关打开状态显示节点。</param>
    /// <param name="isOpen">是否处于打开状态。</param>
    /// <param name="playAnimation">是否播放动画。</param>
    private void RefreshSwitch(RectTransform switchBtn, RectTransform switchOn, bool isOpen, bool playAnimation)
    {
        float targetX = GetSwitchTargetX(switchBtn, isOpen);
        MoveSwitchBtn(switchBtn, targetX, playAnimation);
        FadeSwitchOn(switchOn, isOpen, playAnimation);
    }

    /// <summary>
    /// 获取开关滑块的目标横向位置，右侧为打开，左侧为关闭。
    /// </summary>
    /// <param name="switchBtn">开关滑块节点。</param>
    /// <param name="isOpen">是否打开。</param>
    /// <returns>滑块目标X坐标。</returns>
    private float GetSwitchTargetX(RectTransform switchBtn, bool isOpen)
    {
        if (switchBtn == null || !(switchBtn.parent is RectTransform switchFrame))
        {
            return 0f;
        }

        float btnWidth = Mathf.Max(switchBtn.rect.width, switchFrame.rect.height);
        float openX = Mathf.Round(Mathf.Max(0f, switchFrame.rect.width * 0.5f - btnWidth * 0.5f));

        return isOpen ? openX : -openX;
    }

    /// <summary>
    /// 移动开关滑块。
    /// </summary>
    /// <param name="switchBtn">开关滑块节点。</param>
    /// <param name="targetX">目标X坐标。</param>
    /// <param name="playAnimation">是否播放动画。</param>
    private void MoveSwitchBtn(RectTransform switchBtn, float targetX, bool playAnimation)
    {
        if (switchBtn == null)
        {
            return;
        }

        switchBtn.DOKill();
        if (!playAnimation)
        {
            Vector2 pos = switchBtn.anchoredPosition;
            pos.x = targetX;
            switchBtn.anchoredPosition = pos;
            return;
        }

        switchBtn.DOAnchorPosX(targetX, SWITCH_MOVE_DURATION)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(switchBtn.gameObject);
    }

    /// <summary>
    /// 淡入或淡出开关打开状态节点。
    /// </summary>
    /// <param name="switchOn">开关打开状态显示节点。</param>
    /// <param name="isOpen">是否打开。</param>
    /// <param name="playAnimation">是否播放动画。</param>
    private void FadeSwitchOn(RectTransform switchOn, bool isOpen, bool playAnimation)
    {
        if (switchOn == null)
        {
            return;
        }

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(switchOn);
        canvasGroup.DOKill();
        switchOn.gameObject.SetActive(true);

        if (!playAnimation)
        {
            canvasGroup.alpha = isOpen ? 1f : 0f;
            switchOn.gameObject.SetActive(isOpen);
            return;
        }

        canvasGroup.DOFade(isOpen ? 1f : 0f, SWITCH_FADE_DURATION)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(switchOn.gameObject)
            .OnComplete(() =>
            {
                if (!isOpen)
                {
                    switchOn.gameObject.SetActive(false);
                }
            });
    }

    /// <summary>
    /// 获取或添加用于控制透明度的 CanvasGroup。
    /// </summary>
    /// <param name="target">目标节点。</param>
    /// <returns>目标节点上的 CanvasGroup。</returns>
    private CanvasGroup GetOrAddCanvasGroup(RectTransform target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    /// <summary>
    /// 清理单个开关正在播放的动画。
    /// </summary>
    /// <param name="switchBtn">开关滑块节点。</param>
    /// <param name="switchOn">开关打开状态显示节点。</param>
    private void KillSwitchTween(RectTransform switchBtn, RectTransform switchOn)
    {
        if (switchBtn != null)
        {
            switchBtn.DOKill();
        }

        if (switchOn != null)
        {
            CanvasGroup canvasGroup = switchOn.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
            }
        }
    }

    /// <summary>
    /// 设置按钮显隐。
    /// </summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="visible">是否显示。</param>
    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 设置按钮是否可交互。
    /// </summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="interactable">是否可点击。</param>
    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }
}

/// <summary>仅游戏页传入局内上下文；主页/关卡列表不传即隐藏局内设置。</summary>
public sealed class MPSettingPopUIMsgData : UIMsgData
{
    public bool isInGame;
    public string levelTitle;
    public Action replayAction;
    public Action<Color> fillColorChanged;
}

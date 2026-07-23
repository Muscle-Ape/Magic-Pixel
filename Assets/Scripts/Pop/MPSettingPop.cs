using DG.Tweening;
using HQ.UIManager;
using UnityEngine;
using UnityEngine.UI;

[Component("MPSettingPop")]
public class MPSettingPop : AWindow
{
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
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();

        RegisterUI();
        RefreshAllSwitches(false);
    }

    public override void OnRelease()
    {
        UnregisterUI();
        KillSwitchTween(m_bgmSwitchBtn, m_bgmSwitchOn);
        KillSwitchTween(m_soundSwitchBtn, m_soundSwitchOn);
        KillSwitchTween(m_vibrationSwitchBtn, m_vibrationSwitchOn);
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
        MPUser.instance.SetVibrationStatus(isOpen);
        RefreshSwitch(m_vibrationSwitchBtn, m_vibrationSwitchOn, isOpen, true);

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
    }

    /// <summary>
    /// 点击关闭按钮。
    /// </summary>
    private void OnCloseClick()
    {
        if (m_popScaleAnimation != null)
        {
            m_popScaleAnimation.Close(null);
            return;
        }

        DestroyWindow();

        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);
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
}

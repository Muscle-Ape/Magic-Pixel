using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 通用页面顶部栏。管理节点、数据显示、按钮监听和头像资源；页面只传入业务回调。
/// 主页仍使用自己的布局和入口，不挂载此组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class MPHead : MonoBehaviour
{
    [SerializeField] private Button m_backButton;
    [SerializeField] private Button m_settingButton;
    [SerializeField] private Button m_coinButton;
    [SerializeField] private Button m_diamondButton;
    [SerializeField] private Button[] m_openButtons;
    [SerializeField] private TMP_Text m_playerName;
    [SerializeField] private Image m_avatar;
    [SerializeField] private TMP_Text m_coinText;
    [SerializeField] private TMP_Text m_diamondText;
    [SerializeField] private TMP_Text m_level;
    [SerializeField] private Image m_levelFill;

    private UnityAction m_onBack;
    private UnityAction m_onSetting;
    private UnityAction m_onProfile;
    private UnityAction m_onCoin;
    private UnityAction m_onDiamond;
    private bool m_initialized;
    private int m_loadedAvatarId = -1;
    private Sprite m_loadedAvatar;

    /// <summary>
    /// 初始化并刷新。BackBtn 或结算页 HomeBtn 均使用 onBack，头像与昵称共用 onProfile。
    /// 可重复调用，替换回调前会解绑旧监听；未传回调的按钮不执行业务操作。
    /// </summary>
    public void Init(UnityAction onBack, UnityAction onSetting, UnityAction onProfile = null,
        UnityAction onCoin = null, UnityAction onDiamond = null)
    {
        SetListeners(false);
        m_onBack = onBack;
        m_onSetting = onSetting;
        m_onProfile = onProfile;
        m_onCoin = onCoin;
        m_onDiamond = onDiamond;
        m_initialized = true;
        SetInteractable(true);
        if (isActiveAndEnabled)
            SetListeners(true);
        Refresh();
    }

    /// <summary>页面初始化、重新获得焦点或资产变化后调用；初始化之前及释放后调用均无副作用。</summary>
    public void Refresh()
    {
        if (!m_initialized)
            return;

        if (m_coinText != null)
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        if (m_diamondText != null)
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        RefreshProfile();
        RefreshLevel();
    }

    private void RefreshProfile()
    {
        if (!m_initialized)
            return;

        if (m_playerName != null)
        {
            m_playerName.richText = false;
            string playerName = MPLoginManager.Instance.PlayerName;
            m_playerName.text = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
        }

        if (m_avatar == null)
            return;

        int avatarId = MPUser.instance.GetProfileAvatarId();
        if (m_loadedAvatarId != avatarId || m_loadedAvatar == null)
        {
            // 头像未变化时复用 Sprite；更换头像前释放旧资源，不随刷新次数累积句柄。
            ReleaseAvatar();
            m_loadedAvatar = MPRewardPopupIcons.LoadSprite(
                MPPlayerProfile.GetAvatarLocation(avatarId), this, MPPlayerProfile.GetAvatarLocation(0));
            m_loadedAvatarId = avatarId;
        }
        MPRewardPopupIcons.Apply(m_avatar, m_loadedAvatar);
    }

    private void RefreshLevel()
    {
        if (!m_initialized)
            return;

        if (m_level != null)
            m_level.text = "LEVEL " + MPUser.instance.GetPlayerLevel();
        RefreshLevelProgress(m_levelFill);
    }

    /// <summary>经验条通过移动 Fill、由父节点 Mask 裁切显示，不使用 Image.fillAmount。</summary>
    public static void RefreshLevelProgress(Image fill)
    {
        if (fill == null)
            return;

        RectTransform fillRect = fill.rectTransform;
        RectTransform maskRect = fillRect.parent as RectTransform;
        if (maskRect == null)
            return;

        // Fill 锚定在 Mask 左侧，轴心在自身右侧：从 X=0 向右移动一个遮罩宽度。
        Vector2 position = fillRect.anchoredPosition;
        position.x = maskRect.rect.width * Mathf.Clamp01(MPUser.instance.GetPlayerLevelProgress());
        fillRect.anchoredPosition = position;
    }

    /// <summary>统一控制顶部栏交互，例如通关动画期间禁用，重新初始化时恢复。</summary>
    public void SetInteractable(bool interactable)
    {
        SetButtonInteractable(m_backButton, interactable);
        SetButtonInteractable(m_settingButton, interactable);
        SetButtonInteractable(m_coinButton, interactable);
        SetButtonInteractable(m_diamondButton, interactable);
        if (m_openButtons != null)
            foreach (Button button in m_openButtons)
                SetButtonInteractable(button, interactable);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void SetListeners(bool subscribe)
    {
        // 只移除本组件注册的回调，不清空按钮上其他组件或 Inspector 配置的监听。
        MPUser.ProfileChanged -= RefreshProfile;
        MPUser.ExperienceChanged -= RefreshLevel;
        if (subscribe)
        {
            MPUser.ProfileChanged += RefreshProfile;
            MPUser.ExperienceChanged += RefreshLevel;
        }

        SetButtonListener(m_backButton, m_onBack, subscribe);
        SetButtonListener(m_settingButton, m_onSetting, subscribe);
        SetButtonListener(m_coinButton, m_onCoin, subscribe);
        SetButtonListener(m_diamondButton, m_onDiamond, subscribe);
        if (m_openButtons != null)
            foreach (Button button in m_openButtons)
                SetButtonListener(button, m_onProfile, subscribe);
    }

    private static void SetButtonListener(Button button, UnityAction callback, bool subscribe)
    {
        if (button == null || callback == null)
            return;

        button.onClick.RemoveListener(callback);
        if (subscribe)
            button.onClick.AddListener(callback);
    }

    private void OnEnable()
    {
        if (!m_initialized)
            return;

        SetListeners(true);
        Refresh();
    }

    private void OnDisable()
    {
        // 临时隐藏只解绑监听，保留回调和头像缓存，重新显示时恢复。
        SetListeners(false);
    }

    /// <summary>页面 OnRelease 调用；销毁时也会兜底执行，可重复调用。</summary>
    public void Release()
    {
        m_initialized = false;
        SetListeners(false);
        m_onBack = null;
        m_onSetting = null;
        m_onProfile = null;
        m_onCoin = null;
        m_onDiamond = null;
        SetInteractable(false);
        ReleaseAvatar();
    }

    private void ReleaseAvatar()
    {
        if (m_avatar != null && m_avatar.sprite == m_loadedAvatar)
            m_avatar.sprite = null;
        m_loadedAvatar = null;
        m_loadedAvatarId = -1;
        MPLoad.ReleaseAll(this);
    }

    private void OnDestroy()
    {
        Release();
    }
}

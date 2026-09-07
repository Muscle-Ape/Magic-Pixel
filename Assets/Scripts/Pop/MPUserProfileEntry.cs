using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>预制体头像/名字的资料入口。只订阅显示，不把资料业务分散到每个页面。</summary>
[DisallowMultipleComponent]
public sealed class MPUserProfileEntry : MonoBehaviour
{
    [SerializeField] private Button[] m_openButtons;
    [SerializeField] private TMP_Text m_playerName;
    [SerializeField] private Image m_avatar;
    [SerializeField] private TMP_Text m_level;
    [SerializeField] private Image m_levelFill;
    private int m_loadedAvatarId = -1;
    private Sprite m_loadedAvatar;

    private void Awake()
    {
        if (m_openButtons == null)
            return;
        foreach (Button button in m_openButtons)
            if (button != null)
                button.onClick.AddListener(Open);
    }

    private void OnEnable()
    {
        MPUser.ProfileChanged += Refresh;
        MPUser.ExperienceChanged += RefreshLevel;
        Refresh();
    }

    private void OnDisable()
    {
        MPUser.ProfileChanged -= Refresh;
        MPUser.ExperienceChanged -= RefreshLevel;
    }

    private void Refresh()
    {
        if (m_playerName != null)
        {
            m_playerName.richText = false;
            string playerName = MPLoginManager.Instance.PlayerName;
            m_playerName.text = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
        }
        if (m_avatar != null)
        {
            int avatarId = MPUser.instance.GetProfileAvatarId();
            // Head 会随页面反复启用。头像未变化时复用持有的 Sprite，不反复读取资源。
            if (m_loadedAvatarId != avatarId || m_loadedAvatar == null)
            {
                m_loadedAvatar = MPRewardPopupIcons.LoadSprite(
                    MPPlayerProfile.GetAvatarLocation(avatarId), this, MPPlayerProfile.GetAvatarLocation(0));
                m_loadedAvatarId = avatarId;
            }
            MPRewardPopupIcons.Apply(m_avatar, m_loadedAvatar);
        }
        RefreshLevel();
    }

    private void RefreshLevel()
    {
        if (m_level != null)
            m_level.text = "LEVEL " + MPUser.instance.GetPlayerLevel();
        MPHead.RefreshLevelProgress(m_levelFill);
    }

    private void Open()
    {
        MPUserPop.Show();
    }

    private void OnDestroy()
    {
        MPUser.ProfileChanged -= Refresh;
        MPUser.ExperienceChanged -= RefreshLevel;
        m_loadedAvatar = null;
        MPLoad.ReleaseAll(this);
        if (m_openButtons == null)
            return;
        foreach (Button button in m_openButtons)
            if (button != null)
                button.onClick.RemoveListener(Open);
    }
}

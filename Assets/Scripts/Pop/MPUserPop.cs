using System;
using System.Threading;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Component("MPUserPop")]
public class MPUserPop : AWindow
{
    [TransformPath("View/Window/Avatar")] private Image m_avatar;
    [TransformPath("View/Window/Level")] private TMP_Text m_level;
    [TransformPath("View/Window/NameInput")] private TMP_InputField m_nameInput;
    [TransformPath("View/Window/Status")] private TMP_Text m_status;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;
    [TransformPath("View/Window/SaveBtn")] private Button m_saveBtn;
    [TransformPath("View/Window/Avatars")] private RectTransform m_avatars;
    private readonly Button[] m_avatarButtons = new Button[MPPlayerProfile.AVATAR_COUNT];
    private readonly UnityAction[] m_avatarActions = new UnityAction[MPPlayerProfile.AVATAR_COUNT];
    private readonly Transform[] m_avatarSelections = new Transform[MPPlayerProfile.AVATAR_COUNT];
    private readonly Sprite[] m_avatarSprites = new Sprite[MPPlayerProfile.AVATAR_COUNT];
    private CancellationTokenSource m_lifetime;
    private int m_selectedAvatar;
    private bool m_busy;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;
    public static void Show() => UIManager.Inst.ShowWindow<MPUserPop>(null, true, UILayer.Top);

    public override void OnCreate()
    {
        m_lifetime = new CancellationTokenSource();
        m_closeBtn.onClick.AddListener(Close);
        m_saveBtn.onClick.AddListener(Save);
        for (int i = 0; i < m_avatarButtons.Length; i++)
        {
            int index = i;
            Button button = m_avatars.Find("Avatar" + (i + 1))?.GetComponent<Button>();
            m_avatarButtons[i] = button;
            m_avatarActions[i] = () => SelectAvatar(index);
            if (button == null) continue;
            button.onClick.AddListener(m_avatarActions[i]);
            m_avatarSprites[i] = MPRewardPopupIcons.LoadSprite(
                MPPlayerProfile.GetAvatarLocation(i), this, MPPlayerProfile.GetAvatarLocation(0));
            MPRewardPopupIcons.Apply(button.image, m_avatarSprites[i]);
            m_avatarSelections[i] = button.transform.Find("Select");
            if (m_avatarSelections[i] != null)
                MPRewardPopupIcons.Load(m_avatarSelections[i].GetComponent<Image>(),
                    "popup_selection_frame", this, null);
        }
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_nameInput.characterLimit = MPPlayerProfileService.MAX_NAME_LENGTH;
        m_nameInput.text = MPLoginManager.Instance.PlayerName;
        m_level.text = "LEVEL " + Mathf.Max(1, MPUser.instance.GetMainLevlPassIndex() + 1);
        m_status.text = string.Empty;
        SelectAvatar(MPUser.instance.GetProfileAvatarId());
    }

    private void SelectAvatar(int id)
    {
        if (m_busy || m_closing) return;
        m_selectedAvatar = Mathf.Clamp(id, 0, m_avatarSprites.Length - 1);
        MPRewardPopupIcons.Apply(m_avatar, m_avatarSprites[m_selectedAvatar]);
        for (int i = 0; i < m_avatarButtons.Length; i++)
            if (m_avatarSelections[i] != null)
                m_avatarSelections[i].gameObject.SetActive(i == m_selectedAvatar);
    }

    private async void Save()
    {
        if (m_busy || m_closing) return;
        if (!MPPlayerProfileService.Validate(m_nameInput.text, m_selectedAvatar, out string error))
        {
            m_status.text = error;
            return;
        }
        SetBusy(true);
        m_status.text = "Saving...";
        try
        {
            await MPPlayerProfileService.SaveAsync(m_nameInput.text, m_selectedAvatar, m_lifetime.Token);
            if (this == null || IsDestoried) return;
            SetBusy(false);
            Close();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (this != null && !IsDestoried)
                m_status.text = "Unable to save. Please check your connection and retry.";
            Debug.LogWarning($"[MPUserPop] 保存资料失败：{exception.Message}");
        }
        finally
        {
            if (this != null && !IsDestoried && !m_closing) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        m_saveBtn.interactable = !busy;
        m_closeBtn.interactable = !busy;
        m_nameInput.interactable = !busy;
        foreach (Button button in m_avatarButtons) if (button != null) button.interactable = !busy;
    }

    private void Close()
    {
        if (m_busy || m_closing) return;
        m_closing = true;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(null); else DestroyWindow();
    }

    public override void OnRelease()
    {
        m_lifetime?.Cancel(); m_lifetime?.Dispose(); m_lifetime = null;
        m_closeBtn.onClick.RemoveListener(Close);
        m_saveBtn.onClick.RemoveListener(Save);
        for (int i = 0; i < m_avatarButtons.Length; i++)
            if (m_avatarButtons[i] != null) m_avatarButtons[i].onClick.RemoveListener(m_avatarActions[i]);
        Array.Clear(m_avatarSprites, 0, m_avatarSprites.Length);
        MPLoad.ReleaseAll(this);
    }
}

using System;
using System.Threading;
using DG.Tweening;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Component("MPUserPop")]
public class MPUserPop : AWindow
{
    private const float VALIDATION_HIDE_DURATION = 0.12f;
    private const float VALIDATION_SHOW_DURATION = 0.2f;

    [TransformPath("View/Window/Avatar")] private Image m_avatar;
    [TransformPath("View/Window/Level/Text")] private TMP_Text m_level;
    [TransformPath("View/Window/Level/Mask/Fill")] private Image m_levelFill;
    [TransformPath("View/Window/NameInput")] private TMP_InputField m_nameInput;
    [TransformPath("View/Window/NameInput/Error")] private RectTransform m_nameError;
    [TransformPath("View/Window/NameInput/IconPen")] private RectTransform m_namePen;
    [TransformPath("View/Window/NameInput/IconCurrect")] private RectTransform m_nameCorrect;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;
    [TransformPath("View/Window/SaveBtn")] private Button m_saveBtn;
    [TransformPath("View/Window/Avatars")] private RectTransform m_avatars;
    private readonly Button[] m_avatarButtons = new Button[MPPlayerProfile.AVATAR_COUNT];
    private readonly UnityAction[] m_avatarActions = new UnityAction[MPPlayerProfile.AVATAR_COUNT];
    private readonly Transform[] m_avatarSelections = new Transform[MPPlayerProfile.AVATAR_COUNT];
    private readonly Sprite[] m_avatarSprites = new Sprite[MPPlayerProfile.AVATAR_COUNT];
    private CancellationTokenSource m_lifetime;
    private Sequence m_nameValidationSequence;
    private int m_selectedAvatar;
    private NameValidationState m_nameValidationState;
    private bool m_loadingData;
    private bool m_busy;
    private bool m_closing;

    private enum NameValidationState
    {
        None,
        Error,
        Correct
    }

    protected override bool ShouldAdaptToNotchScreen() => false;
    public static void Show() => UIManager.Inst.ShowWindow<MPUserPop>(null, true, UILayer.Top);

    public override void OnCreate()
    {
        m_lifetime = new CancellationTokenSource();
        m_closeBtn.onClick.AddListener(Close);
        m_saveBtn.onClick.AddListener(Save);
        m_nameInput.onValueChanged.AddListener(OnNameChanged);
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
        }
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_loadingData = true;
        m_nameInput.characterLimit = MPPlayerProfileService.MAX_NAME_LENGTH;
        SetCurrentPlayerName();
        m_loadingData = false;

        m_level.text = "LEVEL " + MPUser.instance.GetPlayerLevel();
        MPHead.RefreshLevelProgress(m_levelFill);
        SelectAvatar(MPUser.instance.GetProfileAvatarId());
        RefreshNameValidation(true, true);
    }

    /// <summary>
    /// TMP_InputField 自身保存一份 text，同时 Text Area/Text 负责实际渲染。
    /// 打开弹窗时两处都同步，避免预制体默认文案残留到第一帧或重新打开的弹窗中。
    /// </summary>
    private void SetCurrentPlayerName()
    {
        string playerName = MPLoginManager.Instance.PlayerName;
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        m_nameInput.SetTextWithoutNotify(playerName);
        if (m_nameInput.textComponent != null)
            m_nameInput.textComponent.text = playerName;
        m_nameInput.ForceLabelUpdate();
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

    private void OnNameChanged(string _)
    {
        if (m_loadingData || m_closing)
            return;

        RefreshNameValidation(true);
    }

    /// <summary>
    /// 名称结果只展示一个：错误图先缩小关闭，再将正确图缩放打开，反向切换同理。
    /// force 为 true 时允许保存校验重新播放当前结果，给用户明确反馈。
    /// </summary>
    private void RefreshNameValidation(bool animate, bool force = false)
    {
        NameValidationState state = MPPlayerProfileService.ValidateName(m_nameInput.text, out _)
            ? NameValidationState.Correct
            : NameValidationState.Error;
        SetNameValidationState(state, animate, force);
    }

    private void SetNameValidationState(NameValidationState state, bool animate, bool force)
    {
        if (!force && state == m_nameValidationState)
            return;

        m_nameValidationState = state;
        KillNameValidationAnimation();

        if (!animate)
        {
            SetValidationNodeImmediately(m_nameError, state == NameValidationState.Error);
            SetValidationNodeImmediately(m_nameCorrect, state == NameValidationState.Correct);
            RefreshPen(state);
            return;
        }

        RectTransform nodeToHide = state == NameValidationState.Correct ? m_nameError : m_nameCorrect;
        RectTransform nodeToShow = state == NameValidationState.Correct ? m_nameCorrect : m_nameError;

        // 目标节点必须等旧节点完全关闭后再打开，保证 Error 与 IconCurrect 始终互斥。
        if (nodeToShow != null)
        {
            nodeToShow.DOKill();
            nodeToShow.localScale = Vector3.zero;
            nodeToShow.gameObject.SetActive(false);
        }

        m_nameValidationSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (nodeToHide != null && nodeToHide.gameObject.activeSelf && nodeToHide.localScale.sqrMagnitude > 0.0001f)
        {
            nodeToHide.DOKill();
            m_nameValidationSequence.Append(nodeToHide
                .DOScale(Vector3.zero, VALIDATION_HIDE_DURATION)
                .SetEase(Ease.InBack));
        }
        m_nameValidationSequence.AppendCallback(() =>
        {
            if (nodeToHide != null)
            {
                nodeToHide.localScale = Vector3.zero;
                nodeToHide.gameObject.SetActive(false);
            }

            if (nodeToShow != null)
            {
                nodeToShow.gameObject.SetActive(true);
                nodeToShow.localScale = Vector3.zero;
            }
            RefreshPen(state);
        });
        if (nodeToShow != null)
        {
            m_nameValidationSequence.Append(nodeToShow
                .DOScale(Vector3.one, VALIDATION_SHOW_DURATION)
                .SetEase(Ease.OutBack));
        }
        m_nameValidationSequence.OnComplete(() => m_nameValidationSequence = null);
    }

    private void RefreshPen(NameValidationState state)
    {
        if (m_namePen != null)
            m_namePen.gameObject.SetActive(state != NameValidationState.Correct);
    }

    private static void SetValidationNodeImmediately(RectTransform node, bool visible)
    {
        if (node == null)
            return;

        node.DOKill();
        node.gameObject.SetActive(visible);
        node.localScale = visible ? Vector3.one : Vector3.zero;
    }

    private void KillNameValidationAnimation()
    {
        m_nameValidationSequence?.Kill();
        m_nameValidationSequence = null;
        m_nameError?.DOKill();
        m_nameCorrect?.DOKill();
    }

    private async void Save()
    {
        if (m_busy || m_closing) return;
        if (!MPPlayerProfileService.Validate(m_nameInput.text, m_selectedAvatar, out string error))
        {
            RefreshNameValidation(true, true);
            return;
        }
        SetBusy(true);
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
        KillNameValidationAnimation();
        m_lifetime?.Cancel(); m_lifetime?.Dispose(); m_lifetime = null;
        m_closeBtn.onClick.RemoveListener(Close);
        m_saveBtn.onClick.RemoveListener(Save);
        m_nameInput.onValueChanged.RemoveListener(OnNameChanged);
        for (int i = 0; i < m_avatarButtons.Length; i++)
            if (m_avatarButtons[i] != null) m_avatarButtons[i].onClick.RemoveListener(m_avatarActions[i]);
        Array.Clear(m_avatarSprites, 0, m_avatarSprites.Length);
        MPLoad.ReleaseAll(this);
    }
}

using HQ.UIManager;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPGameFailPop")]
public class MPGameFailPop : AWindow
{
    private const string REWARD_AD_SCENE = "game_fail_revive";

    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    [TransformPath("View/Window/QuitBtn")]
    private Button m_quitBtn;

    [TransformPath("View/Window/ReplayBtn")]
    private Button m_replayBtn;

    [TransformPath("View/Window/ReviveItemBtn")]
    private Button m_reviveItemBtn;

    [TransformPath("View/Window/ReviveItemBtn/CountFrame/Count")]
    private TMP_Text m_reviveItemCount;

    [TransformPath("View/Window/ReviveFluoriteBtn")]
    private Button m_reviveFluoriteBtn;

    [TransformPath("View/Window/ReviveAdBtn")]
    private Button m_reviveAdBtn;

    [TransformPath("View/Window/Icon/Index/Text")]
    private TMP_Text m_levelIndex;

    [TransformPath("View/Window/Icon/Index/Shadow")]
    private TMP_Text m_levelIndexShadow;

    private Action m_exitAction;
    private Action m_replayAction;
    private Func<bool> m_reviveItemAction;
    private Func<bool> m_reviveFluoriteAction;
    private Func<bool> m_reviveAdAction;

    private MPPopScaleAnimation m_popScaleAnimation;
    private MPSecondConfirmationPop m_exitConfirmation;
    private bool m_isClosing;
    private bool m_isAdRunning;
    private bool m_isReleased;
    private int m_adOperationVersion;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();
        m_isClosing = false;
        m_isAdRunning = false;
        m_isReleased = false;

        MPGameFailPopUIMsgData data = uiMsg as MPGameFailPopUIMsgData;
        if (data != null)
        {
            m_exitAction = data.exitAction;
            m_replayAction = data.replayAction;
            m_reviveItemAction = data.reviveItemAction;
            m_reviveFluoriteAction = data.reviveFluoriteAction;
            m_reviveAdAction = data.reviveAdAction;
            RefreshLevelIndex(data.levelIndex);
        }
        else
        {
            RefreshLevelIndex(1);
        }

        RefreshReviveItemCount();
        RegisterUI();
        SetButtonsInteractable(true);
    }

    private void RefreshLevelIndex(int levelIndex)
    {
        string index = Mathf.Max(1, levelIndex).ToString();
        if (m_levelIndex != null)
            m_levelIndex.text = index;
        if (m_levelIndexShadow != null)
            m_levelIndexShadow.text = index;
    }

    private void RefreshReviveItemCount()
    {
        if (m_reviveItemCount != null)
            m_reviveItemCount.text = $"X{Mathf.Max(0, MPUser.instance.GetLoveRecoverProps())}";
    }

    private void RegisterUI()
    {
        RegisterButton(m_quitBtn, OnQuitClick);
        RegisterButton(m_replayBtn, OnReplayClick);
        RegisterButton(m_reviveItemBtn, OnReviveItemClick);
        RegisterButton(m_reviveFluoriteBtn, OnReviveFluoriteClick);
        RegisterButton(m_reviveAdBtn, OnReviveAdClick);
    }

    private static void RegisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private void OnQuitClick()
    {
        if (!CanHandleClick())
            return;

        m_exitConfirmation = MPSecondConfirmationPop.Show(
            "Leave this attempt?",
            "This attempt has no lives left. Leaving will clear only this level's unfinished progress. Your other levels and rewards will not change.",
            "Leave level",
            token => Task.FromResult(!token.IsCancellationRequested && this != null && !IsDestoried && !m_isClosing),
            onCancel: () => m_exitConfirmation = null,
            cancelText: "Stay here",
            onConfirmed: () =>
            {
                m_exitConfirmation = null;
                if (this != null && !IsDestoried)
                    ClosePop(m_exitAction);
            });
    }

    private void OnReplayClick()
    {
        if (!CanHandleClick())
            return;

        ClosePop(m_replayAction);
    }

    private void OnReviveItemClick()
    {
        if (!CanHandleClick())
            return;

        TryRevive(m_reviveItemAction);
        RefreshReviveItemCount();
    }

    private void OnReviveFluoriteClick()
    {
        if (!CanHandleClick())
            return;

        TryRevive(m_reviveFluoriteAction);
    }

    private void OnReviveAdClick()
    {
        if (!CanHandleClick())
            return;

        m_isAdRunning = true;
        int operationVersion = ++m_adOperationVersion;
        SetButtonsInteractable(false);

        try
        {
            AOAds.CheckAndShowRewardedVideo(REWARD_AD_SCENE, (ready, success) =>
            {
                if (this == null || m_isReleased || IsDestoried || m_isClosing
                    || !m_isAdRunning || operationVersion != m_adOperationVersion)
                    return;

                m_isAdRunning = false;
                if (!ready || !success)
                {
                    SetButtonsInteractable(true);
                    return;
                }

                if (!TryRevive(m_reviveAdAction))
                    SetButtonsInteractable(true);
            });
        }
        catch (Exception exception)
        {
            m_isAdRunning = false;
            SetButtonsInteractable(true);
            Debug.LogWarning($"[MPGameFailPop] 激励广告播放失败：{exception.Message}");
        }
    }

    private bool TryRevive(Func<bool> reviveAction)
    {
        if (reviveAction == null || !reviveAction.Invoke())
            return false;

        ClosePop(null);
        return true;
    }

    private bool CanHandleClick()
    {
        return !m_isClosing
            && !m_isAdRunning
            && (m_exitConfirmation == null || m_exitConfirmation.IsDestoried);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(m_quitBtn, interactable);
        SetButtonInteractable(m_replayBtn, interactable);
        SetButtonInteractable(m_reviveItemBtn, interactable);
        SetButtonInteractable(m_reviveFluoriteBtn, interactable);
        SetButtonInteractable(m_reviveAdBtn, interactable);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void ClosePop(Action onClosed)
    {
        if (m_isClosing)
            return;

        m_isClosing = true;
        m_isAdRunning = false;
        ++m_adOperationVersion;
        SetButtonsInteractable(false);

        if (m_popScaleAnimation != null)
        {
            m_popScaleAnimation.Close(onClosed);
            return;
        }

        DestroyWindow();
        onClosed?.Invoke();
    }

    public override void OnRelease()
    {
        m_isReleased = true;
        m_isAdRunning = false;
        ++m_adOperationVersion;

        if (m_exitConfirmation != null && !m_exitConfirmation.IsDestoried)
            m_exitConfirmation.DestroyWindow();
        m_exitConfirmation = null;

        UnregisterButton(m_quitBtn, OnQuitClick);
        UnregisterButton(m_replayBtn, OnReplayClick);
        UnregisterButton(m_reviveItemBtn, OnReviveItemClick);
        UnregisterButton(m_reviveFluoriteBtn, OnReviveFluoriteClick);
        UnregisterButton(m_reviveAdBtn, OnReviveAdClick);

        m_exitAction = null;
        m_replayAction = null;
        m_reviveItemAction = null;
        m_reviveFluoriteAction = null;
        m_reviveAdAction = null;
    }

    private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button != null)
            button.onClick.RemoveListener(callback);
    }
}

public class MPGameFailPopUIMsgData : UIMsgData
{
    /// <summary>当前关卡下标，使用从1开始的展示值。</summary>
    public int levelIndex;

    public Action exitAction;
    public Action replayAction;

    /// <summary>使用一个复活道具，成功时返回true。</summary>
    public Func<bool> reviveItemAction;

    /// <summary>使用萤石复活，成功时返回true。</summary>
    public Func<bool> reviveFluoriteAction;

    /// <summary>激励广告成功后执行复活，成功时返回true。</summary>
    public Func<bool> reviveAdAction;
}

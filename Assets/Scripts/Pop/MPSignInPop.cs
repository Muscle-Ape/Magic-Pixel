using HQ.UIManager;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPSignInPop")]
public sealed class MPSignInPop : AWindow
{
    [TransformPath("View/Window/Days")] private RectTransform m_days;
    [TransformPath("View/Window/Status")] private TMP_Text m_status;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;
    [TransformPath("View/Window/ClaimBtn")] private Button m_claimBtn;
    [TransformPath("View/Window/DoubleBtn")] private Button m_doubleBtn;
    private MPSignInConfig m_config;
    private Vector2 m_statusDefaultPosition;
    private readonly Dictionary<string, Sprite> m_sprites = new Dictionary<string, Sprite>();
    private bool m_busy;
    private bool m_closing;
    private bool m_released;
    private int m_claimVersion;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public override void OnCreate()
    {
        m_statusDefaultPosition = m_status.rectTransform.anchoredPosition;
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        RegisterButtons();
        try
        {
            if (!MPSignInConfigService.TryLoad(out m_config))
                throw new InvalidOperationException("Sign-in configuration is unavailable.");
            RefreshDays();
        }
        catch (Exception exception)
        {
            m_config = null;
            foreach (Transform card in m_days)
                card.gameObject.SetActive(false);
            m_status.text = "Rewards are unavailable. Please try again later.";
            m_claimBtn.interactable = false;
            m_doubleBtn.interactable = false;
            Debug.LogWarning($"[MPSignInPop] {exception.Message}");
        }
    }

    private void RegisterButtons()
    {
        m_closeBtn.onClick.RemoveListener(OnClose);
        m_claimBtn.onClick.RemoveListener(OnClaim);
        m_doubleBtn.onClick.RemoveListener(OnDouble);
        m_closeBtn.onClick.AddListener(OnClose);
        m_claimBtn.onClick.AddListener(OnClaim);
        m_doubleBtn.onClick.AddListener(OnDouble);
    }

    public override void OnFocus(bool focus)
    {
        if (focus && m_config != null && !m_busy && !m_closing && !m_released)
            RefreshDays();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus && m_config != null && !m_busy && !m_closing && !m_released)
            RefreshDays();
    }

    private void RefreshDays(string message = null)
    {
        if (m_config == null || m_released || m_closing)
            return;
        MPSignInStatus status = MPUser.instance.GetSignInStatus(m_config);
        // 仅展示已开放的完整轮次；尾组不足七条时仍停留在上一组，不显示零散卡片。
        int availableCount = m_config.AvailableEntryCount;
        int currentIndex = status.dayIndex >= 0 ? status.dayIndex : availableCount - 1;
        int startIndex = Math.Max(0, currentIndex) / MPSignInConfig.DAYS_PER_ROUND * MPSignInConfig.DAYS_PER_ROUND;
        for (int i = 0; i < MPSignInConfig.DAYS_PER_ROUND; i++)
        {
            Transform card = m_days.Find("Day" + (i + 1));
            if (card == null) continue;
            int entryIndex = startIndex + i;
            bool visible = entryIndex < availableCount;
            card.gameObject.SetActive(visible);
            if (!visible) continue;
            MPSignInRewardEntry reward = m_config.Entries[entryIndex];
            card.Find("Day").GetComponent<TMP_Text>().text = "Day " + (entryIndex + 1);
            card.Find("Reward").GetComponent<TMP_Text>().text =
                MPRewardPresentation.Name(reward.type) + " x" + reward.amount;
            bool claimed = status.claimedEntryIds.Contains(reward.id);
            bool today = entryIndex == status.dayIndex && status.CanClaim;
            card.Find("State").GetComponent<TMP_Text>().text =
                claimed ? "Claimed" : today ? "Today" : "Locked";
            Image background = card.GetComponent<Image>();
            LoadSprite(background, claimed ? "popup_sign_in_claimed"
                : today ? "popup_sign_in_current" : "popup_sign_in_locked", true);
            LoadSprite(card.Find("Icon").GetComponent<Image>(), reward.icon, false);
        }
        bool awaitingNewRewards = status.dayIndex < 0;
        // 全部领完仍允许手动查看；底部领取区改为版本更新提示，不再保留无效按钮。
        m_claimBtn.gameObject.SetActive(!awaitingNewRewards);
        m_doubleBtn.gameObject.SetActive(!awaitingNewRewards);
        m_status.rectTransform.anchoredPosition = awaitingNewRewards
            ? new Vector2(m_statusDefaultPosition.x, ((RectTransform)m_claimBtn.transform).anchoredPosition.y)
            : m_statusDefaultPosition;
        m_status.text = message ?? (!status.hasConfiguredRewards
            ? (m_config.HasIncompleteRound
                ? "The next 7-day sign-in round is not ready yet.\nNew rewards will be added in future updates."
                : "No sign-in rewards available yet.\nNew rewards will be added in future updates.")
            : awaitingNewRewards
            ? "All available sign-in rewards collected!\nNew rewards will be added in future updates."
            : !status.clockIsValid
            ? "Please correct your device time to claim."
            : status.claimedToday ? "Claimed! Come back tomorrow."
            : "One reward each day - resets at 00:00 (UTC+8).");
        m_closeBtn.interactable = !m_busy && !m_closing;
        m_claimBtn.interactable = status.CanClaim && !m_busy && !m_closing;
        m_doubleBtn.interactable = status.CanClaim && !m_busy && !m_closing;
    }

    private void OnClaim()
    {
        if (m_busy || m_closing || m_config == null) return;
        MPSignInStatus status = MPUser.instance.GetSignInStatus(m_config);
        if (!status.CanClaim) { RefreshDays(); return; }
        ++m_claimVersion;
        SetBusy(true);
        CompleteClaim(status, 1);
    }

    private void OnDouble()
    {
        if (m_busy || m_closing || m_config == null) return;
        MPSignInStatus status = MPUser.instance.GetSignInStatus(m_config);
        if (!status.CanClaim) { RefreshDays(); return; }
        string owner = MPUser.instance.GetRewardProgressOwner();
        int claimVersion = ++m_claimVersion;
        SetBusy(true);
        m_status.text = "Waiting for rewarded video...";
        try
        {
            AOAds.CheckAndShowRewardedVideo("sign_in_double", (ready, success) =>
            {
                if (this == null || m_released || IsDestoried || m_closing || !m_busy
                    || claimVersion != m_claimVersion) return;
                if (!ready || !success || owner != MPUser.instance.GetRewardProgressOwner())
                {
                    SetBusy(false);
                    RefreshDays("Video not completed. Your daily reward is still available.");
                    return;
                }
                CompleteClaim(status, 2);
            });
        }
        catch (Exception exception)
        {
            SetBusy(false);
            RefreshDays("Video unavailable. Please retry or claim normally.");
            Debug.LogWarning($"[MPSignInPop] Rewarded video failed: {exception.Message}");
        }
    }

    private void CompleteClaim(MPSignInStatus status, int multiplier)
    {
        try
        {
            bool claimed = MPUser.instance.TryClaimSignInReward(status.entryId,
                status.day, multiplier, out MPRewardReceipt receipt);
            SetBusy(false);
            if (claimed)
            {
                // 先退出签到再显示已经入账的结果，防止回到首页时重入签到窗口。
                Close(() => MPRewardsClaimPop.Show(receipt));
                return;
            }
            RefreshDays("Could not claim. Please check the day and try again.");
        }
        catch (Exception exception)
        {
            SetBusy(false);
            RefreshDays("Could not save your reward. Please retry.");
            Debug.LogWarning($"[MPSignInPop] {exception.Message}");
        }
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        m_closeBtn.interactable = !busy && !m_closing;
        m_claimBtn.interactable = !busy && !m_closing;
        m_doubleBtn.interactable = !busy && !m_closing;
    }

    private void OnClose()
    {
        Close(null);
    }

    private void Close(Action onClosed)
    {
        if (m_busy || m_closing) return;
        m_closing = true;
        m_closeBtn.interactable = m_claimBtn.interactable = m_doubleBtn.interactable = false;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(onClosed);
        else
        {
            DestroyWindow();
            onClosed?.Invoke();
        }
    }

    private void LoadSprite(Image target, string location, bool panel)
    {
        if (target == null || string.IsNullOrEmpty(location)) return;
        if (!m_sprites.TryGetValue(location, out Sprite sprite))
        {
            try
            {
                sprite = MPLoad.Load<Sprite>(location, this);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MPSignInPop] Icon unavailable: {location}. {exception.Message}");
            }
            m_sprites.Add(location, sprite);
        }
        if (sprite == null) return;
        target.sprite = sprite;
        target.color = Color.white;
        target.preserveAspect = !panel;
        if (panel) target.type = Image.Type.Sliced;
    }

    public override void OnRelease()
    {
        m_released = true;
        ++m_claimVersion;
        if (m_closeBtn != null) m_closeBtn.onClick.RemoveListener(OnClose);
        if (m_claimBtn != null) m_claimBtn.onClick.RemoveListener(OnClaim);
        if (m_doubleBtn != null) m_doubleBtn.onClick.RemoveListener(OnDouble);
        MPLoad.ReleaseAll(this);
        m_sprites.Clear();
    }
}

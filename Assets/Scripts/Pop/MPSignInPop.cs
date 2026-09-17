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
    [TransformPath("View/Window/Desc")] private TMP_Text m_desc;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;
    [TransformPath("View/Window/ClaimBtn")] private Button m_claimBtn;
    [TransformPath("View/Window/DoubleBtn")] private Button m_doubleBtn;
    private MPSignInConfig m_config;
    private string m_completedDescription;
    private readonly Dictionary<string, Sprite> m_sprites = new Dictionary<string, Sprite>();
    private bool m_busy;
    private bool m_closing;
    private bool m_released;
    private int m_claimVersion;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public override void OnCreate()
    {
        // 保留预制体中“后续版本更新奖励”的文案，不再移动底部节点。
        m_completedDescription = m_desc.text;
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        if (!MPReleaseFeatures.SignIn)
        {
            DestroyWindow();
            return;
        }
        m_doubleBtn.gameObject.SetActive(MPReleaseFeatures.Ads);
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
            SetFooter(false, "Rewards are unavailable. Please try again later.");
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
            card.Find("Number").GetComponent<TMP_Text>().text =
                MPRewardPresentation.Name(reward.type) + " x" + reward.amount;
            bool claimed = status.claimedEntryIds.Contains(reward.id);
            bool today = entryIndex == status.dayIndex && status.CanClaim;
            // 第七天的宽卡片也使用自身预制的图片，仅切换节点，不覆盖其图片与尺寸。
            SetDayState(card.Find("FrameStatus"), claimed, today);
            SetDayState(card.Find("ImgStatus"), claimed, today);
            LoadSprite(card.Find("Icon").GetComponent<Image>(), reward.icon);
        }
        bool awaitingNewRewards = status.dayIndex < 0;
        // 当天已领也提供退出入口；否则隐藏 CloseBtn 会使再次打开的玩家无法退出。
        string description = !status.hasConfiguredRewards || awaitingNewRewards
            ? m_completedDescription
            : !status.clockIsValid
            ? "Please correct your device time to claim."
            : status.claimedToday ? "Claimed! Come back tomorrow."
            : string.Empty;
        SetFooter(status.CanClaim, description);
        m_closeBtn.interactable = !m_busy && !m_closing;
        m_claimBtn.interactable = status.CanClaim && !m_busy && !m_closing;
        m_doubleBtn.interactable = status.CanClaim && !m_busy && !m_closing;
        // 新界面没有 Status 节点，领取/视频异常通过现有 Toast 反馈。
        if (!string.IsNullOrEmpty(message))
            UnityToast.Instance.ShowToast(message);
    }

    private static void SetDayState(Transform root, bool claimed, bool current)
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        Transform complete = root.Find("Complete");
        Transform unlock = root.Find("Unlock");
        Transform locked = root.Find("Lock");
        if (complete != null) complete.gameObject.SetActive(claimed);
        if (unlock != null) unlock.gameObject.SetActive(!claimed && current);
        if (locked != null) locked.gameObject.SetActive(!claimed && !current);
        // ImgStatus 没有 Unlock 子节点：可领取时隐藏锁和完成遮罩即可。
    }

    private void SetFooter(bool canClaim, string description)
    {
        m_closeBtn.gameObject.SetActive(!canClaim);
        m_desc.gameObject.SetActive(!canClaim);
        m_desc.text = description;
        m_claimBtn.gameObject.SetActive(canClaim);
        m_doubleBtn.gameObject.SetActive(canClaim && MPReleaseFeatures.Ads);
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
        if (!MPReleaseFeatures.SignIn || !MPReleaseFeatures.Ads) return;
        if (m_busy || m_closing || m_config == null) return;
        MPSignInStatus status = MPUser.instance.GetSignInStatus(m_config);
        if (!status.CanClaim) { RefreshDays(); return; }
        string owner = MPUser.instance.GetRewardProgressOwner();
        int claimVersion = ++m_claimVersion;
        SetBusy(true);
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

    private void LoadSprite(Image target, string location)
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
        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
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

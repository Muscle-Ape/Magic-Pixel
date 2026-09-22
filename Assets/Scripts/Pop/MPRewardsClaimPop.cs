using HQ.UIManager;
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只展示已入账奖励，点击 Collect 不会重复发奖。</summary>
[Component("MPRewardsClaimPop")]
public sealed class MPRewardsClaimPop : AWindow
{
    private const float OPEN_DURATION = 0.3f;
    private const float CLOSE_DURATION = 0.2f;
    [TransformPath("Mask")] private CanvasGroup m_maskGroup;
    [TransformPath("View/Window")] private RectTransform m_window;
    [TransformPath("View/Window")] private CanvasGroup m_windowGroup;
    [TransformPath("View/Window/Rewards")] private RectTransform m_rewards;
    [TransformPath("View/Window/Rewards/Item")] private RectTransform m_itemTemplate;
    [TransformPath("View/Window/CollectBtn")] private Button m_collectBtn;
    private readonly List<RectTransform> m_items = new List<RectTransform>();
    private Vector2 m_windowPosition;
    private Vector3 m_windowScale;
    private float m_maskAlpha;
    private Sequence m_animation;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static void Show(MPRewardReceipt receipt)
    {
        if (receipt?.rewards == null || !receipt.rewards.Exists(IsDisplayable)) return;
        if (!MPUser.instance.RewardTransactionIsCommitted(receipt.transactionId)) return;
        UIManager.Inst.ShowWindow<MPRewardsClaimPop>(
            new MPRewardsClaimPopUIMsgData { receipt = receipt }, true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_windowPosition = m_window.anchoredPosition;
        m_windowScale = m_window.localScale;
        m_maskAlpha = m_maskGroup == null || Mathf.Approximately(m_maskGroup.alpha, 0f)
            ? 1f
            : m_maskGroup.alpha;
        m_items.Add(m_itemTemplate);
        m_itemTemplate.gameObject.SetActive(false);
        m_collectBtn.onClick.AddListener(OnCollect);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPRewardReceipt receipt = (uiMsg as MPRewardsClaimPopUIMsgData)?.receipt;
        if (m_closing) return;
        if (receipt?.rewards == null || !receipt.rewards.Exists(IsDisplayable))
        {
            DestroyWindow();
            return;
        }

        foreach (RectTransform item in m_items) item.gameObject.SetActive(false);
        int count = 0;
        foreach (MPRewardItem reward in receipt.rewards)
        {
            if (!IsDisplayable(reward)) continue;
            if (count == m_items.Count)
            {
                RectTransform item = Instantiate(m_itemTemplate, m_rewards, false);
                item.name = "Item";
                m_items.Add(item);
            }
            RectTransform row = m_items[count++];
            row.Find("Count").GetComponent<TMP_Text>().text = "x" + reward.amount;
            MPRewardPopupIcons.Load(row.Find("Icon").GetComponent<Image>(), IconFor(reward.type), this);
            row.gameObject.SetActive(true);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rewards);
        PlayOpenAnimation();
    }

    private static bool IsDisplayable(MPRewardItem reward)
    {
        return reward != null && reward.amount > 0 && IconFor(reward.type) != null;
    }

    private static string IconFor(string type)
    {
        switch (MPRewardPresentation.NormalizeType(type))
        {
            case "fluorite": return "pop_reward_icon_fluorite";
            case "hint": return "pop_reward_icon_hint";
            case "life": return "pop_reward_icon_love";
            default: return null;
        }
    }

    /// <summary>始终以预制体初始位置为终点，避免重复打开时累计偏移。</summary>
    private void PlayOpenAnimation()
    {
        KillAnimation();
        m_window.anchoredPosition = m_windowPosition + Vector2.down * 100f;
        // 本弹窗不使用通用缩放动画，每次播放前都恢复预制体缩放。
        m_window.localScale = m_windowScale;
        m_windowGroup.alpha = 0f;
        m_windowGroup.interactable = false;
        m_windowGroup.blocksRaycasts = true;
        if (m_maskGroup != null)
        {
            m_maskGroup.alpha = 0f;
            m_maskGroup.interactable = false;
            m_maskGroup.blocksRaycasts = true;
        }
        m_collectBtn.interactable = false;
        m_animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        m_animation.Join(m_window.DOAnchorPos(m_windowPosition, OPEN_DURATION).SetEase(Ease.OutCubic));
        m_animation.Join(m_windowGroup.DOFade(1f, OPEN_DURATION).SetEase(Ease.Linear));
        if (m_maskGroup != null)
            m_animation.Join(m_maskGroup.DOFade(m_maskAlpha, OPEN_DURATION).SetEase(Ease.Linear));
        m_animation.OnComplete(() =>
        {
            if (this == null || IsDestoried || m_closing) return;
            m_windowGroup.interactable = true;
            if (m_maskGroup != null)
                m_maskGroup.interactable = true;
            m_collectBtn.interactable = true;
        });
    }

    private void OnCollect()
    {
        if (m_closing || IsDestoried) return;
        m_closing = true;
        KillAnimation();
        m_collectBtn.interactable = false;
        m_windowGroup.interactable = false;
        // 保持射线阻挡直到销毁，避免淡出时误点底层页面。
        m_animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        m_animation.Join(m_windowGroup.DOFade(0f, CLOSE_DURATION).SetEase(Ease.Linear));
        if (m_maskGroup != null)
            m_animation.Join(m_maskGroup.DOFade(0f, CLOSE_DURATION).SetEase(Ease.Linear));
        m_animation.OnComplete(() =>
        {
            if (this != null && !IsDestoried) DestroyWindow();
        });
    }

    private void KillAnimation()
    {
        m_animation?.Kill();
        m_animation = null;
        m_window?.DOKill();
        m_windowGroup?.DOKill();
        m_maskGroup?.DOKill();
    }

    public override void OnRelease()
    {
        m_closing = true;
        KillAnimation();
        if (m_collectBtn != null) m_collectBtn.onClick.RemoveListener(OnCollect);
        foreach (RectTransform item in m_items)
        {
            if (item != null) item.Find("Icon").GetComponent<Image>().sprite = null;
        }
        m_items.Clear();
        MPLoad.ReleaseAll(this);
    }
}

public sealed class MPRewardsClaimPopUIMsgData : UIMsgData
{
    public MPRewardReceipt receipt;
}

/// <summary>弹窗图片通过已有资源管理器加载；缺图使用同类占位 PNG，不用 Image 染色代替图标。</summary>
public static class MPRewardPopupIcons
{
    public static void Load(Image target, string location, UnityEngine.Object owner,
        string fallbackLocation = "popup_reward_placeholder")
    {
        if (target == null)
            return;
        Apply(target, LoadSprite(location, owner, fallbackLocation));
    }

    public static void Apply(Image target, Sprite sprite)
    {
        if (target == null)
            return;
        target.sprite = sprite;
        target.preserveAspect = true;
        target.color = Color.white;
        // 占位资源也缺失时保留旁边的文字，避免显示无图的纯白矩形。
        target.enabled = sprite != null;
    }

    public static Sprite LoadSprite(string location, UnityEngine.Object owner,
        string fallbackLocation = "popup_reward_placeholder")
    {
        Sprite sprite = TryLoad(location, owner);
        if (sprite == null && !string.Equals(location, fallbackLocation, StringComparison.Ordinal))
            sprite = TryLoad(fallbackLocation, owner);
        return sprite;
    }

    private static Sprite TryLoad(string location, UnityEngine.Object owner)
    {
        if (string.IsNullOrEmpty(location) || owner == null)
            return null;
        try
        {
            return MPLoad.Load<Sprite>(location, owner);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Popup] Icon unavailable: {location}. {exception.Message}");
            return null;
        }
    }
}

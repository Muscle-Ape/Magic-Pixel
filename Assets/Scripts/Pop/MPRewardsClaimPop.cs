using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPRewardsClaimPop")]
public sealed class MPRewardsClaimPop : AWindow
{
    [TransformPath("View/Window/Source")] private TMP_Text m_source;
    [TransformPath("View/Window/Rewards")] private RectTransform m_rewards;
    [TransformPath("View/Window/CollectBtn")] private Button m_collectBtn;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static void Show(MPRewardReceipt receipt)
    {
        if (receipt == null || receipt.rewards == null || receipt.rewards.Count == 0)
            return;
        // 只有已经成功入账的结果才展示；重新展示不会再次调用任何加资产接口。
        if (!MPUser.instance.RewardTransactionIsCommitted(receipt.transactionId))
            return;
        UIManager.Inst.ShowWindow<MPRewardsClaimPop>(
            new MPRewardsClaimPopUIMsgData { receipt = receipt }, true, UILayer.Top);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPRewardReceipt receipt = (uiMsg as MPRewardsClaimPopUIMsgData)?.receipt;
        if (receipt == null)
        {
            DestroyWindow();
            return;
        }
        m_source.text = receipt.sourceName ?? "Reward";
        foreach (Transform row in m_rewards)
            row.gameObject.SetActive(false);

        foreach (MPRewardItem reward in receipt.rewards)
        {
            if (reward == null || reward.amount <= 0)
                continue;
            string rowName;
            switch (MPRewardPresentation.NormalizeType(reward.type))
            {
                case "coin": rowName = "Coin"; break;
                case "diamond": rowName = "Diamond"; break;
                case "hint": rowName = "Hint"; break;
                case "life": rowName = "Life"; break;
                default: continue;
            }
            Transform row = m_rewards.Find(rowName);
            if (row == null)
                continue;
            row.gameObject.SetActive(true);
            row.Find("Name").GetComponent<TMP_Text>().text = MPRewardPresentation.Name(reward.type);
            row.Find("Amount").GetComponent<TMP_Text>().text = "+" + reward.amount;
            MPRewardPopupIcons.Load(row.Find("Icon").GetComponent<Image>(),
                string.IsNullOrEmpty(reward.icon) ? MPRewardPresentation.Icon(reward.type) : reward.icon, this);
        }
        m_collectBtn.onClick.RemoveListener(OnCollect);
        m_collectBtn.onClick.AddListener(OnCollect);
    }

    private void OnCollect()
    {
        if (m_closing)
            return;
        m_closing = true;
        m_collectBtn.interactable = false;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null) animation.Close(null);
        else DestroyWindow();
    }

    public override void OnRelease()
    {
        if (m_collectBtn != null) m_collectBtn.onClick.RemoveListener(OnCollect);
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

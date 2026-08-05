using HQ.UIManager;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPPetRewardsPop")]
public class MPPetRewardsPop : AWindow
{
    /// <summary>
    /// 奖励列表父节点，子节点数量决定弹窗最多可展示多少个奖励。
    /// </summary>
    [TransformPath("View/Window/Awards")]
    private RectTransform m_awards;

    /// <summary>
    /// 领取流程按钮。奖励在打开弹窗前已经发放，这里只负责关闭弹窗。
    /// </summary>
    [TransformPath("View/Window/ClaimBtn")]
    private Button m_claimBtn;

    /// <summary>
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    /// <summary>
    /// 本次领取到的宠物奖励配置。
    /// </summary>
    private List<MPPetRewardConfig> m_rewards;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLoad.ReleaseAll(this);
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();

        MPPetRewardsPopUIMsgData data = uiMsg as MPPetRewardsPopUIMsgData;
        m_rewards = data == null || data.rewards == null ? new List<MPPetRewardConfig>() : data.rewards;

        RegisterUI();
        RefreshAwards();
    }

    public override void OnRelease()
    {
        if (m_claimBtn != null)
        {
            m_claimBtn.onClick.RemoveListener(OnClaimClick);
        }

        MPLoad.ReleaseAll(this);
    }

    /// <summary>
    /// 注册弹窗按钮事件，先移除再添加，避免窗口复用时重复触发。
    /// </summary>
    private void RegisterUI()
    {
        if (m_claimBtn != null)
        {
            m_claimBtn.onClick.RemoveListener(OnClaimClick);
            m_claimBtn.onClick.AddListener(OnClaimClick);
        }
    }

    /// <summary>
    /// 根据本次奖励数量显示对应 Award 节点，并刷新图标、名称和数量。
    /// </summary>
    private void RefreshAwards()
    {
        if (m_awards == null)
            return;

        int rewardCount = Mathf.Min(m_rewards.Count, m_awards.childCount);
        for (int i = 0; i < m_awards.childCount; i++)
        {
            Transform awardNode = m_awards.GetChild(i);
            bool active = i < rewardCount;
            awardNode.gameObject.SetActive(active);
            if (!active)
                continue;

            RefreshAward(awardNode, m_rewards[i]);
        }
    }

    /// <summary>
    /// 刷新单个奖励节点，节点结构来自 MPPetRewardsPop.prefab 的 Award 模板。
    /// </summary>
    private void RefreshAward(Transform awardNode, MPPetRewardConfig reward)
    {
        if (awardNode == null || reward == null)
            return;

        string icon = GetRewardIcon(reward);
        SetImageSprite(FindComponent<Image>(awardNode, "Icon"), icon);
        SetText(awardNode.Find("Name"), GetRewardName(reward));
        SetText(awardNode.Find("Count"), $"+{reward.Count}");
    }

    /// <summary>
    /// 食物/玩具优先使用奖励自己的 icon，未配置时再使用物品配置图标。
    /// </summary>
    private string GetRewardIcon(MPPetRewardConfig reward)
    {
        if (!string.IsNullOrEmpty(reward.Icon))
            return reward.Icon;

        MPPetCareItemConfig careConfig = FindCareItemConfig(reward.ID);
        return careConfig == null ? null : careConfig.Icon;
    }

    /// <summary>
    /// 普通奖励使用固定名称，食物/玩具使用 id 指向的物品名称。
    /// </summary>
    private string GetRewardName(MPPetRewardConfig reward)
    {
        if (reward == null || string.IsNullOrEmpty(reward.Type))
            return string.Empty;

        switch (reward.Type.ToLowerInvariant())
        {
            case "coin":
            case "coins":
            case "gold":
                return "Coins";
            case "diamond":
            case "diamonds":
            case "gem":
            case "gems":
                return "Diamonds";
            case "light":
            case "hint":
            case "hint_prop":
            case "hintprop":
            case "prop_hint":
                return "Hints";
            case "paw":
            case "love":
            case "life":
            case "heart":
            case "love_recover":
            case "life_recover":
            case "recover_life":
            case "loveprop":
            case "lifeprop":
                return "Life";
            case "food":
            case "toy":
                MPPetCareItemConfig careConfig = FindCareItemConfig(reward.ID);
                return careConfig == null ? reward.Type : careConfig.Name;
            default:
                return reward.Type;
        }
    }

    /// <summary>
    /// 从食物和玩具配置表中查找奖励 id 对应的物品配置。
    /// </summary>
    private MPPetCareItemConfig FindCareItemConfig(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        MPPetsModel petsModel = MPDataManager.Instance.m_petsModel;
        MPPetCareItemConfig config = FindCareItemConfig(petsModel?.foodConfigs, id);
        return config ?? FindCareItemConfig(petsModel?.toyConfigs, id);
    }

    private MPPetCareItemConfig FindCareItemConfig(List<MPPetCareItemConfig> configs, string id)
    {
        if (configs == null)
            return null;

        for (int i = 0; i < configs.Count; i++)
        {
            MPPetCareItemConfig config = configs[i];
            if (config != null && config.ID == id)
                return config;
        }

        return null;
    }

    private T FindComponent<T>(Transform root, string path) where T : Component
    {
        Transform target = root == null || string.IsNullOrEmpty(path) ? null : root.Find(path);
        return target == null ? null : target.GetComponent<T>();
    }

    private void SetImageSprite(Image image, string location)
    {
        if (image == null || string.IsNullOrEmpty(location))
            return;

        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(location, this);
            if (sprite != null)
            {
                image.sprite = sprite;
            }
        }
        catch (Exception)
        {
        }
    }

    private void SetText(Transform target, string value)
    {
        if (target == null)
            return;

        TMP_Text tmpText = target.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = value;
            return;
        }

        Text text = target.GetComponent<Text>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private void OnClaimClick()
    {
        ClosePop();
    }

    /// <summary>
    /// 关闭弹窗时优先播放通用缩放动画。
    /// </summary>
    private void ClosePop()
    {
        if (m_popScaleAnimation != null)
        {
            m_popScaleAnimation.Close(null);
            return;
        }

        DestroyWindow();
    }
}

public class MPPetRewardsPopUIMsgData : UIMsgData
{
    /// <summary>
    /// 已经发放完成、仅用于弹窗展示的宠物奖励列表。
    /// </summary>
    public List<MPPetRewardConfig> rewards;
}

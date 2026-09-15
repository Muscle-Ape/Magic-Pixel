using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPLevelUnlockPop")]
public class MPLevelUnlockPop : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 主关卡提前解锁需要消耗的萤石数量。
    /// </summary>
    private const int UNLOCK_FLUORITE_COST = 100;

    /// <summary>
    /// 关闭按钮。
    /// </summary>
    [TransformPath("View/Window/CloseBtn")]
    private Button m_closeBtn;

    /// <summary>
    /// 萤石解锁按钮。
    /// </summary>
    [TransformPath("View/Window/FluoriteBtn")]
    private Button m_fluoriteBtn;

    /// <summary>
    /// 广告解锁按钮。
    /// </summary>
    [TransformPath("View/Window/AdBtn")]
    private Button m_adBtn;

    /// <summary>
    /// VIP 按钮，点击后打开订阅弹窗。
    /// </summary>
    [TransformPath("View/Window/VipBtn")]
    private Button m_vipBtn;

    /// <summary>
    /// 萤石消耗数量文本。
    /// </summary>
    [TransformPath("View/Window/FluoriteBtn/Text")]
    private TMP_Text m_fluoriteCostText;

    /// <summary>
    /// 主关卡下标文本。
    /// </summary>
    [TransformPath("View/Window/Level/Info")]
    private TMP_Text m_levelIndexText;

    /// <summary>
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    /// <summary>
    /// 当前尝试提前解锁的主关卡数据。
    /// </summary>
    private MPMainBlockInfo m_levelInfo;

    /// <summary>
    /// 解锁成功后刷新关卡列表的回调。
    /// </summary>
    private Action m_refreshAction;
    private int m_index;
    private Action m_openSubscription;
    private bool m_busy;
    private bool m_closing;
    private bool m_released;
    private int m_operationVersion;

    public override void OnCreate()
    {
        MPReleaseFeatures.ApplyUnlock(transform);
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();
        RegisterUI();
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLevelUnlockPopUIMsgData data = uiMsg as MPLevelUnlockPopUIMsgData;
        if (data?.levelInfo == null)
        {
            ClosePop();
            return;
        }

        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();
        m_levelInfo = data.levelInfo;
        m_index = data.index;
        m_refreshAction = data.refresh;
        m_openSubscription = data.openSubscription;

        RefreshUI();
    }

    public override void OnRelease()
    {
        m_released = true;
        ++m_operationVersion;
        UnregisterUI();
        m_refreshAction = null;
        m_openSubscription = null;
    }

    /// <summary>
    /// 注册弹窗按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        if (m_closeBtn != null)
        {
            m_closeBtn.onClick.RemoveListener(OnCloseClick);
            m_closeBtn.onClick.AddListener(OnCloseClick);
        }

        if (m_fluoriteBtn != null)
        {
            m_fluoriteBtn.onClick.RemoveListener(OnFluoriteClick);
            m_fluoriteBtn.onClick.AddListener(OnFluoriteClick);
        }

        if (m_adBtn != null)
        {
            m_adBtn.onClick.RemoveListener(OnAdClick);
            m_adBtn.onClick.AddListener(OnAdClick);
        }

        if (m_vipBtn != null)
        {
            m_vipBtn.onClick.RemoveListener(OnVipClick);
            m_vipBtn.onClick.AddListener(OnVipClick);
        }
    }

    /// <summary>
    /// 移除弹窗按钮事件，避免重复注册。
    /// </summary>
    private void UnregisterUI()
    {
        if (m_closeBtn != null)
        {
            m_closeBtn.onClick.RemoveListener(OnCloseClick);
        }

        if (m_fluoriteBtn != null)
        {
            m_fluoriteBtn.onClick.RemoveListener(OnFluoriteClick);
        }

        if (m_adBtn != null)
        {
            m_adBtn.onClick.RemoveListener(OnAdClick);
        }

        if (m_vipBtn != null)
        {
            m_vipBtn.onClick.RemoveListener(OnVipClick);
        }
    }

    /// <summary>
    /// 刷新弹窗内动态文本。
    /// </summary>
    private void RefreshUI()
    {
        if (m_fluoriteCostText != null)
        {
            m_fluoriteCostText.text = $"Unlock for {UNLOCK_FLUORITE_COST}";
        }

        if (m_levelIndexText != null)
            m_levelIndexText.text = (Mathf.Max(0, m_index) + 1).ToString();
    }

    /// <summary>
    /// 点击萤石解锁按钮，萤石足够时提前解锁当前主关卡。
    /// </summary>
    private void OnFluoriteClick()
    {
        if (!CanUnlock()) return;
        if (!MPUser.instance.UseFluorite(UNLOCK_FLUORITE_COST))
        {
            Debug.Log($"Not enough Fluorite. {UNLOCK_FLUORITE_COST} Fluorite is required.");
            return;
        }
        CompleteUnlock();
    }

    private bool CanUnlock()
    {
        if (m_busy || m_closing || m_released || m_levelInfo == null || string.IsNullOrEmpty(m_levelInfo.ID))
            return false;
        if (MPUser.instance.MainLevelIsUnlock(m_levelInfo.ID))
        {
            ClosePop();
            return false;
        }
        return true;
    }

    /// <summary>仅广告准备成功且获得奖励回调时解锁，关闭或重复回调不再处理。</summary>
    private void OnAdClick()
    {
        if (!MPReleaseFeatures.Ads) return;
        if (!CanUnlock()) return;
        SetBusy(true);
        int version = ++m_operationVersion;
        try
        {
            AOAds.CheckAndShowRewardedVideo("main_level_unlock", (ready, success) =>
            {
                if (this == null || IsDestoried || m_released || m_closing || !m_busy || version != m_operationVersion) return;
                ++m_operationVersion;
                SetBusy(false);
                if (ready && success) CompleteUnlock();
            });
        }
        catch (Exception exception)
        {
            if (this == null || IsDestoried || m_released) return;
            ++m_operationVersion;
            SetBusy(false);
            Debug.LogWarning($"[MPLevelUnlockPop] 广告播放失败：{exception.GetType().Name}");
        }
    }

    /// <summary>仅通知调用方打开订阅弹窗，订阅模块负责后续购买与权益处理。</summary>
    private void OnVipClick()
    {
        if (!MPReleaseFeatures.Vip || !MPReleaseFeatures.InAppPurchases) return;
        if (m_busy || m_closing || m_released || IsDestoried || m_openSubscription == null) return;
        SetBusy(true);
        try
        {
            m_openSubscription.Invoke();
        }
        finally
        {
            if (this != null && !IsDestoried && !m_released && !m_closing) SetBusy(false);
        }
    }

    private void CompleteUnlock()
    {
        if (m_closing || m_released || m_levelInfo == null) return;
        if (!MPUser.instance.MainLevelIsUnlock(m_levelInfo.ID))
            MPUser.instance.MainLevelUnlock(m_levelInfo.ID);
        Action refresh = m_refreshAction;
        ClosePop();
        refresh?.Invoke();
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        bool enabled = !busy && !m_closing && !m_released;
        if (m_fluoriteBtn != null) m_fluoriteBtn.interactable = enabled;
        if (m_adBtn != null) m_adBtn.interactable = enabled;
        if (m_vipBtn != null) m_vipBtn.interactable = enabled;
        if (m_closeBtn != null) m_closeBtn.interactable = enabled;
    }

    private void OnCloseClick()
    {
        if (!m_busy) ClosePop();
    }

    private void ClosePop()
    {
        if (m_closing || m_released) return;
        m_closing = true;
        SetBusy(false);
        if (m_popScaleAnimation != null) m_popScaleAnimation.Close(null);
        else DestroyWindow();
    }

}

public class MPLevelUnlockPopUIMsgData : UIMsgData
{
    /// <summary>
    /// 需要提前解锁的主关卡数据。
    /// </summary>
    public MPMainBlockInfo levelInfo;

    /// <summary>
    /// 需要提前解锁的主关卡下标。
    /// </summary>
    public int index;

    /// <summary>
    /// 解锁成功后刷新关卡列表的回调。
    /// </summary>
    public Action refresh;

    /// <summary>打开订阅弹窗的回调；订阅页面制作完成后由调用方传入。</summary>
    public Action openSubscription;
}

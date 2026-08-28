using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPLargeImageLevelUnlockPop")]
public class MPLargeImageLevelUnlockPop : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 大图模式关卡提前解锁需要消耗的金币数量。
    /// </summary>
    private const int UNLOCK_COIN_COST = 800;

    /// <summary>
    /// 关闭按钮。
    /// </summary>
    [TransformPath("View/Window/CloseBtn")]
    private Button m_closeBtn;

    /// <summary>
    /// 金币解锁按钮。
    /// </summary>
    [TransformPath("View/Window/CoinBtn")]
    private Button m_coinBtn;

    /// <summary>
    /// 广告解锁按钮，具体广告逻辑后续接入。
    /// </summary>
    [TransformPath("View/Window/AdBtn")]
    private Button m_adBtn;

    /// <summary>
    /// VIP解锁按钮，具体VIP逻辑后续接入。
    /// </summary>
    [TransformPath("View/Window/VipBtn")]
    private Button m_vipBtn;

    /// <summary>
    /// 金币消耗数量文本。
    /// </summary>
    [TransformPath("View/Window/CoinBtn/Frame/Count")]
    private TMP_Text m_coinCostText;

    /// <summary>
    /// 大图尺寸文本。
    /// </summary>
    [TransformPath("View/Window/Icon/Size")]
    private TMP_Text m_sizeText;

    /// <summary>
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    /// <summary>
    /// 当前尝试提前解锁的大图模式关卡数据。
    /// </summary>
    private MPLargeImageBlockInfo m_levelInfo;

    /// <summary>
    /// 解锁成功后刷新关卡列表的回调。
    /// </summary>
    private Action m_refreshAction;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPLargeImageLevelUnlockPopUIMsgData data = uiMsg as MPLargeImageLevelUnlockPopUIMsgData;
        if (data == null)
        {
            ClosePop();
            return;
        }

        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();
        m_levelInfo = data.levelInfo;
        m_refreshAction = data.refresh;

        RegisterUI();
        RefreshUI();
    }

    public override void OnRelease()
    {
        UnregisterUI();
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

        if (m_coinBtn != null)
        {
            m_coinBtn.onClick.RemoveListener(OnCoinClick);
            m_coinBtn.onClick.AddListener(OnCoinClick);
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

        if (m_coinBtn != null)
        {
            m_coinBtn.onClick.RemoveListener(OnCoinClick);
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
        if (m_coinCostText != null)
        {
            m_coinCostText.text = UNLOCK_COIN_COST.ToString();
        }

        if (m_sizeText != null && m_levelInfo != null)
        {
            Vector2Int size = MPLargeImageLevelModel.GetLevelSize(m_levelInfo);
            m_sizeText.text = $"{size.x}x{size.y}";
        }
    }

    /// <summary>
    /// 点击金币解锁按钮，金币足够时提前解锁当前大图模式关卡。
    /// </summary>
    private void OnCoinClick()
    {
        if (m_levelInfo == null || string.IsNullOrEmpty(m_levelInfo.ID))
            return;

        if (MPUser.instance.LargeImageLevelIsUnlock(m_levelInfo.ID))
        {
            ClosePop();
            return;
        }

        if (MPUser.instance.GetCoins() < UNLOCK_COIN_COST)
        {
            Debug.Log("Coins are not enough to unlock large image level early.");
            return;
        }

        MPUser.instance.UseCoins(UNLOCK_COIN_COST);
        MPUser.instance.LargeImageLevelUnlock(m_levelInfo.ID);
        m_refreshAction?.Invoke();
        ClosePop();
    }

    /// <summary>
    /// 点击广告解锁按钮，广告逻辑后续接入。
    /// </summary>
    private void OnAdClick()
    {
        Debug.Log("Ad unlock large image level is not implemented yet.");
    }

    /// <summary>
    /// 点击VIP解锁按钮，VIP逻辑后续接入。
    /// </summary>
    private void OnVipClick()
    {
        Debug.Log("VIP unlock large image level is not implemented yet.");
    }

    /// <summary>
    /// 点击关闭按钮。
    /// </summary>
    private void OnCloseClick()
    {
        ClosePop();
    }

    /// <summary>
    /// 关闭当前弹窗，优先播放通用关闭动画。
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

public class MPLargeImageLevelUnlockPopUIMsgData : UIMsgData
{
    /// <summary>
    /// 需要提前解锁的大图模式关卡数据。
    /// </summary>
    public MPLargeImageBlockInfo levelInfo;

    /// <summary>
    /// 需要提前解锁的大图模式关卡下标。
    /// </summary>
    public int index;

    /// <summary>
    /// 解锁成功后刷新关卡列表的回调。
    /// </summary>
    public Action refresh;
}

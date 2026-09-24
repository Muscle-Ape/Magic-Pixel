using System;
using HQ.UIManager;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VIP 购买成功后的奖励展示弹窗。
/// VIP 权益和奖励在打开本弹窗前已经入账，点击按钮只负责关闭展示页面。
/// </summary>
[Component("MPVipClaimPop")]
public sealed class MPVipClaimPop : AWindow
{
    // 预制体当前节点名为 ConfirmBtn，业务上作为 Claim 按钮使用。
    [TransformPath("View/Window/ConfirmBtn")] private Button m_claimButton;

    private MPVipClaimPopUIMsgData m_data;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    /// <summary>
    /// 展示 VIP 奖励。onClosed 只会在用户点击领取且弹窗关闭动画完成后执行。
    /// </summary>
    public static MPVipClaimPop Show(Action onClosed = null)
    {
        return UIManager.Inst.ShowWindow<MPVipClaimPop>(
            new MPVipClaimPopUIMsgData(onClosed), true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_claimButton.onClick.AddListener(OnClaimClick);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg as MPVipClaimPopUIMsgData;
        m_closing = false;
        if (m_claimButton != null)
            m_claimButton.interactable = true;
    }

    private void OnClaimClick()
    {
        if (m_closing || IsDestoried)
            return;

        m_closing = true;
        m_claimButton.interactable = false;
        Action onClosed = m_data?.OnClosed;
        m_data = null;

        // 回调由关闭动画完成后触发，确保后续宠物弹窗不会和当前弹窗重叠。
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
        {
            animation.Close(onClosed);
            return;
        }

        DestroyWindow();
        onClosed?.Invoke();
    }

    public override void OnRelease()
    {
        if (m_claimButton != null)
            m_claimButton.onClick.RemoveListener(OnClaimClick);
        m_data = null;
        base.OnRelease();
    }
}

public sealed class MPVipClaimPopUIMsgData : UIMsgData
{
    public Action OnClosed { get; }

    public MPVipClaimPopUIMsgData(Action onClosed)
    {
        OnClosed = onClosed;
    }
}

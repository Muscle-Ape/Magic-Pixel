using HQ.UIManager;
using System;
using UnityEngine.UI;

[Component("MPGameFailPop")]
public class MPGameFailPop : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 退出当前游戏按钮。
    /// </summary>
    [TransformPath("View/Window/ExitBtn")]
    private Button m_exitBtn;

    /// <summary>
    /// 重玩按钮。
    /// </summary>
    [TransformPath("View/Window/ReplayBtn")]
    private Button m_replayBtn;

    /// <summary>
    /// 恢复一点生命值按钮。
    /// </summary>
    [TransformPath("View/Window/RestoreLifeBtn")]
    private Button m_restoreLifeBtn;

    /// <summary>
    /// 退出当前游戏回调。
    /// </summary>
    private Action m_exitAction;

    /// <summary>
    /// 重玩当前关卡回调。
    /// </summary>
    private Action m_replayAction;

    /// <summary>
    /// 恢复生命值回调，返回true时关闭弹窗继续游戏。
    /// </summary>
    private Func<bool> m_restoreLifeAction;

    /// <summary>
    /// 通用弹窗缩放动画组件。
    /// </summary>
    private MPPopScaleAnimation m_popScaleAnimation;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_popScaleAnimation = GetComponent<MPPopScaleAnimation>();

        MPGameFailPopUIMsgData data = uiMsg as MPGameFailPopUIMsgData;
        if (data != null)
        {
            m_exitAction = data.exitAction;
            m_replayAction = data.replayAction;
            m_restoreLifeAction = data.restoreLifeAction;
        }

        RegisterUI();
    }

    /// <summary>
    /// 注册失败弹窗按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        if (m_exitBtn != null)
        {
            m_exitBtn.onClick.RemoveListener(OnExitClick);
            m_exitBtn.onClick.AddListener(OnExitClick);
        }

        if (m_replayBtn != null)
        {
            m_replayBtn.onClick.RemoveListener(OnReplayClick);
            m_replayBtn.onClick.AddListener(OnReplayClick);
        }

        if (m_restoreLifeBtn != null)
        {
            m_restoreLifeBtn.onClick.RemoveListener(OnRestoreLifeClick);
            m_restoreLifeBtn.onClick.AddListener(OnRestoreLifeClick);
        }
    }

    /// <summary>
    /// 点击退出当前游戏按钮。
    /// </summary>
    private void OnExitClick()
    {
        ClosePop(m_exitAction);
    }

    /// <summary>
    /// 点击重玩按钮。
    /// </summary>
    private void OnReplayClick()
    {
        ClosePop(m_replayAction);
    }

    /// <summary>
    /// 点击恢复生命值按钮。
    /// </summary>
    private void OnRestoreLifeClick()
    {
        if (m_restoreLifeAction != null && m_restoreLifeAction.Invoke())
        {
            ClosePop(null);
        }
    }

    /// <summary>
    /// 关闭弹窗，如果挂载了通用弹窗动画则先播放动画。
    /// </summary>
    /// <param name="onClosed">弹窗关闭后的回调。</param>
    private void ClosePop(Action onClosed)
    {
        if (m_popScaleAnimation != null)
        {
            m_popScaleAnimation.Close(onClosed);
            return;
        }

        DestroyWindow();
        onClosed?.Invoke();
    }
}

public class MPGameFailPopUIMsgData : UIMsgData
{
    /// <summary>
    /// 退出当前游戏回调。
    /// </summary>
    public Action exitAction;

    /// <summary>
    /// 重玩当前关卡回调。
    /// </summary>
    public Action replayAction;

    /// <summary>
    /// 恢复一点生命值回调，返回true时弹窗关闭。
    /// </summary>
    public Func<bool> restoreLifeAction;
}

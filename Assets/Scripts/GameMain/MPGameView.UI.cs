using HQ.UIManager;
using UnityEngine;

public partial class MPGameView
{
    protected override string ExitProgressNotice => m_isCustomLevel
        ? "This custom/community play session cannot be resumed. Leaving will discard this session's puzzle progress; the level itself will not be deleted."
        : base.ExitProgressNotice;

    protected override void ApplyFillColorToBlocks(Color color)
    {
        if (m_blocks == null)
            return;
        foreach (MPGameBlock block in m_blocks)
            if (block != null)
                block.SetFillColor(color);
    }

    /// <summary>
    /// 自定义关卡会隐藏右侧道具区域，因此底部模式切换按钮需要居中显示。
    /// </summary>
    protected override void RefreshModeSpecificLayout()
    {
        if (m_numberFrameMoveShadow != null)
            m_numberFrameMoveShadow.gameObject.SetActive(false);

        if (!m_isCustomLevel || m_modeSwitchFrame == null)
            return;

        RectTransform modeSwitchRect = m_modeSwitchFrame.transform as RectTransform;
        if (modeSwitchRect == null)
            return;

        Vector2 anchoredPosition = modeSwitchRect.anchoredPosition;
        anchoredPosition.x = 0f;
        modeSwitchRect.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// 判断当前关卡是否还有未完成且需要填充的格子。
    /// </summary>
    protected override bool HasHintTarget()
    {
        if (m_blocks == null)
            return false;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            MPGameBlock block = m_blocks[i];
            if (block != null && !block.completed && block.isFill)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 自动完成一个可提示的格子，并同步触发行列完成检查。
    /// </summary>
    protected override void CompleteHintTarget()
    {
        MPGameBlock block = GetHintBlock();
        if (block == null)
            return;

        block.Fill();

        block.Disable();
        block.PlayHintAnimation();
        Check(block);
    }

    /// <summary>
    /// 从所有尚未完成且需要填充的格子中等概率随机选择一个。
    /// </summary>
    private MPGameBlock GetHintBlock()
    {
        if (m_blocks == null)
            return null;

        MPGameBlock selectedBlock = null;
        int candidateCount = 0;
        for (int i = 0; i < m_blocks.Count; i++)
        {
            MPGameBlock block = m_blocks[i];
            if (block == null || block.completed || !block.isFill)
                continue;

            candidateCount++;
            if (Random.Range(0, candidateCount) == 0)
            {
                selectedBlock = block;
            }
        }

        return selectedBlock;
    }

    /// <summary>切换主游戏填充/标记模式。</summary>
    protected override void ToggleInputMode()
    {
        m_isFillMode = !m_isFillMode;
    }

    /// <summary>把当前输入模式同步到全部主游戏格子。</summary>
    protected override void ApplyInputModeToBlocks()
    {
        if (m_blocks == null)
            return;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetBlankHit(!m_isFillMode);
        }
    }

    /// <summary>保留普通或自定义关卡来源信息并重新打开当前关卡。</summary>
    protected override void RestartLevel()
    {
        MPGameViewUIMsgData data = new MPGameViewUIMsgData()
        {
            blockInfo = m_blockInfo,
            customLevelInfo = m_customLevelInfo,
            isCustomLevel = m_isCustomLevel,
            index = m_index,
            refresh = m_refreshAction,
        };

        MPGameView targetWindow = null;
        MPTransitionView.Play(
            () =>
            {
                if (this == null || IsDestoried)
                    return;
                DestroyWindow();
                targetWindow = UIManager.Inst.ShowWindow<MPGameView>(data, true);
            },
            () =>
            {
                if (targetWindow != null && !targetWindow.IsDestoried)
                    targetWindow.PlayEnterAnimationAfterTransition();
            });
    }

    /// <summary>失败退出后刷新主游戏关卡列表。</summary>
    protected override void OnFailExited()
    {
        m_refreshAction?.Invoke();
    }
}

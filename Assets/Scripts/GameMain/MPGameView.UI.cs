using HQ.UIManager;
using UnityEngine;

public partial class MPGameView
{
    /// <summary>
    /// 自定义关卡会隐藏右侧道具区域，因此底部模式切换按钮需要居中显示。
    /// </summary>
    protected override void RefreshModeSpecificLayout()
    {
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
    /// 判断当前关卡是否还有未完成的格子。
    /// </summary>
    protected override bool HasHintTarget()
    {
        if (m_blocks == null)
            return false;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed)
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

        if (block.isFill)
        {
            block.Fill();
        }
        else
        {
            block.Blank();
        }

        block.Disable();
        block.PlayHintAnimation();
        Check(block);
    }

    /// <summary>
    /// 获取提示道具本次要自动完成的格子，优先选择需要填充的未完成格子。
    /// </summary>
    private MPGameBlock GetHintBlock()
    {
        if (m_blocks == null)
            return null;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed && m_blocks[i].isFill)
            {
                return m_blocks[i];
            }
        }

        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].completed)
            {
                return m_blocks[i];
            }
        }

        return null;
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

        MPTransitionView.Play(() =>
        {
            DestroyWindow();
            UIManager.Inst.ShowWindow<MPGameView>(data, true);
        });
    }

    /// <summary>失败退出后刷新主游戏关卡列表。</summary>
    protected override void OnFailExited()
    {
        m_refreshAction?.Invoke();
    }
}

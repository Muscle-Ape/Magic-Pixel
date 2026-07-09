using DG.Tweening;
using UnityEngine;

public partial class MPGameView
{
    /// <summary>
    /// 注册界面按钮事件。
    /// </summary>
    private void RegisterUI()
    {
        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);
        m_backBtn.onClick.AddListener(OnBackClick);

        if (m_hintPropBtn != null)
        {
            m_hintPropBtn.onClick.AddListener(OnHintPropClick);
        }

        if (m_loveRecoverPropBtn != null)
        {
            m_loveRecoverPropBtn.onClick.AddListener(OnLoveRecoverPropClick);
        }

        RefreshPropButtons();
    }

    /// <summary>
    /// 刷新提示道具和生命恢复道具的数量显示。
    /// </summary>
    private void RefreshPropButtons()
    {
        if (m_hintPropBtn == null || m_hintPropCountText == null || m_loveRecoverPropBtn == null || m_loveRecoverPropCountText == null)
            return;

        int hintCount = MPUser.instance.GetHintProps();
        int recoverCount = MPUser.instance.GetLoveRecoverProps();

        m_hintPropCountText.text = hintCount.ToString();
        m_loveRecoverPropCountText.text = recoverCount.ToString();
    }

    /// <summary>
    /// 扣除生命值。
    /// </summary>
    private void SubLoves()
    {
        if (m_isCustomLevel)
            return;

        m_lovesCount = Mathf.Max(0, m_lovesCount - 1);
        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.one;
        love.SetActive(false);

        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    private void AddLoves()
    {
        if (m_isCustomLevel)
            return;

        if (m_lovesCount == m_loves.Count)
            return;

        GameObject love = m_loves[m_lovesCount];
        love.transform.DOKill();
        love.transform.localScale = Vector3.zero;
        love.SetActive(true);
        love.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetLink(love);
        m_lovesCount++;

        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 判断当前关卡是否还有未完成的格子。
    /// </summary>
    /// <returns>存在未完成格子返回true，否则返回false。</returns>
    private bool HasUncompletedBlock()
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
    private void AutoCompleteOneBlock()
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
    /// <returns>可自动完成的格子，没有可用格子时返回null。</returns>
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

    /// <summary>
    /// 点击提示道具按钮，消耗一个提示道具并自动完成一个格子。
    /// </summary>
    private void OnHintPropClick()
    {
        if (m_isCustomLevel)
        {
            return;
        }

        if (!HasUncompletedBlock())
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseHintProp())
        {
            RefreshPropButtons();
            return;
        }

        AutoCompleteOneBlock();
        SaveProgressCache();
        RefreshPropButtons();
    }

    /// <summary>
    /// 点击生命恢复道具按钮，消耗一个恢复道具并恢复一颗生命。
    /// </summary>
    private void OnLoveRecoverPropClick()
    {
        if (m_isCustomLevel || m_lovesCount >= m_loves.Count)
        {
            RefreshPropButtons();
            return;
        }

        if (!MPUser.instance.UseLoveRecoverProp())
        {
            RefreshPropButtons();
            return;
        }

        AddLoves();
    }

    /// <summary>
    /// 切换模式。
    /// </summary>
    private void OnModeSwitchClick()
    {
        m_isFillMode = !m_isFillMode;

        m_modeSwitchTween?.Kill();
        m_modeSwitchTween = (m_modeSwitchBtn.transform as RectTransform).DOAnchorPosX(m_isFillMode ? 65 : -65, 0.1f).SetEase(Ease.Linear);

        m_modeSwitchFill.gameObject.SetActive(m_isFillMode);
        m_modeSwitchBlank.gameObject.SetActive(!m_isFillMode);

        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetBlankHit(!m_isFillMode);
        }
    }

    private void OnBackClick()
    {
        SaveProgressCache();
        DestroyWindow();
    }
}

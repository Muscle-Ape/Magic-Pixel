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
        m_loves[m_lovesCount].SetActive(false);

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

        m_loves[m_lovesCount].SetActive(true);
        m_lovesCount++;

        SaveProgressCache();
        RefreshPropButtons();
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

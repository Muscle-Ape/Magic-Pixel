using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MPGameView
{
    private void RegisterUI()
    {
        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);

        m_backBtn.onClick.AddListener(OnBackClick);
    }


    /// <summary>
    /// 扣除生命值
    /// </summary>
    private void SubLoves()
    {
        if (m_isCustomLevel)
            return;

        m_lovesCount = Mathf.Max(0, m_lovesCount - 1);

        m_loves[m_lovesCount].SetActive(false);
    }

    /// <summary>
    /// 恢复生命值
    /// </summary>
    private void AddLoves()
    {
        if (m_lovesCount == m_loves.Count)
            return;

        m_loves[m_lovesCount].SetActive(true);

        m_lovesCount++;
    }

    /// <summary>
    /// 切换模式
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
        DestroyWindow();
    }
}

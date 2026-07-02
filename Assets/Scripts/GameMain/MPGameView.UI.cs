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

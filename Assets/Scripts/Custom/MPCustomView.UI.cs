using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public partial class MPCustomView
{
    private void RegisterUI()
    {
        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);
        m_sizeSwitchFrame.onClick.AddListener(OnSizeSwitchClick);
        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
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
            m_blocks[i].SetMode(m_isFillMode);
        }
    }

    /// <summary>
    /// 切换大小
    /// </summary>
    private void OnSizeSwitchClick()
    {
        m_isTenSize = !m_isTenSize;

        m_sizeSwithcTween?.Kill();
        m_sizeSwithcTween = (m_sizeSwitchBtn.transform as RectTransform).DOAnchorPosX(m_isTenSize ? 65 : -65, 0.1f).SetEase(Ease.Linear);

        m_sizeSwitchTen.gameObject.SetActive(m_isTenSize);
        m_sizeSwitchFive.gameObject.SetActive(!m_isTenSize);

        if(m_isTenSize)
        {
            CreateGrid(10);
        }
        else
        {
            CreateGrid(5);
        }
    }

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {

    }
}

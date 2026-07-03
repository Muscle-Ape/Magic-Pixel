using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public partial class MPCustomView
{
    private void RegisterUI()
    {
        // 预制面板打开关闭的动画
        m_colorPanelSequence = DOTween.Sequence();
        m_colorPanelSequence.Append(m_colorPanel.DOFade(1, 0.2f).SetEase(Ease.Linear));
        m_colorPanelSequence.Join(m_colorPanel.transform.DOScale(1, 0.2f).SetEase(Ease.Linear));
        m_colorPanelSequence.SetAutoKill(false);
        m_colorPanelSequence.Pause();

        // 添加按钮回调
        m_modeSwitchFrame.onClick.AddListener(OnModeSwitchClick);
        m_sizeSwitchFrame.onClick.AddListener(OnSizeSwitchClick);
        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
        m_colorFrame.onClick.AddListener(OnColorFrameClick);

        transform.Find("View/ColorNode").GetComponent<MPPalette>().Initialization(SetColor);
    }

    private void SetColor(Color color)
    {
        m_currentColor = color;
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

        // 色块缩放出现或者消失的动画
        if(m_isFillMode)
        {
            m_colorFrameTween?.Kill();
            m_colorFrameTween = m_colorFrame.transform.DOScale(0, 0.2f).SetEase(Ease.Linear);
            m_colorFrame.interactable = false;

            m_colorPanelIsOpen = false;
            m_colorPanelSequence.Pause();
            m_colorPanelSequence.PlayBackwards();
        }
        else
        {
            m_colorFrameTween?.Kill();
            m_colorFrameTween = m_colorFrame.transform.DOScale(1, 0.2f).SetEase(Ease.Linear);
            m_colorFrame.interactable = true;
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

        if (m_isTenSize)
        {
            CreateGrid(10);
        }
        else
        {
            CreateGrid(5);
        }
    }

    private void OnColorFrameClick()
    {
        m_colorPanelIsOpen = !m_colorPanelIsOpen;

        m_colorPanelSequence.Pause();
        if (m_colorPanelIsOpen)
        {
            m_colorPanelSequence.PlayForward();
        }
        else
        {
            m_colorPanelSequence.PlayBackwards();
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

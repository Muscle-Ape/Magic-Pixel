using DG.Tweening;
using HQ.UIManager;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class MPCustomView
{
    private void RegisterUI()
    {
        m_titleInput.text = MPUser.instance.GetDefaultCustomLevelTitle();

        // 调色板动画
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
        if (m_saveBtn != null)
        {
            m_saveBtn.onClick.AddListener(OnSaveClick);
        }
        if (m_warehouseBtn != null)
        {
            m_warehouseBtn.onClick.AddListener(OnWarehouseClick);
        }

        transform.Find("View/ColorNode").GetComponent<MPPalette>().Initialization(SetColor);
    }

    private void SetColor(Color color)
    {
        m_currentColor = color;
    }


    /// <summary>
    /// 模式切换
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

        if (m_isFillMode)
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
    /// 大小切换
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


    /// <summary>
    /// 保存当前自定义关卡。
    /// </summary>
    private void OnSaveClick()
    {
        for (int i = 0; i < m_blocks.Count; i++)
        {
            if (!m_blocks[i].isColor)
                return;
        }

        string title = m_titleInput != null ? m_titleInput.text : string.Empty;
        if (string.IsNullOrEmpty(title))
        {
            title = MPUser.instance.GetDefaultCustomLevelTitle();
        }

        List<int> blocks = new List<int>();
        List<MPCustomLevelColorInfo> colors = new List<MPCustomLevelColorInfo>();
        for (int i = 0; i < m_blocks.Count; i++)
        {
            MPCustomBlock block = m_blocks[i];
            if (block.isFill)
            {
                blocks.Add(i);
            }

            if (block.isColor)
            {
                string color = "#" + ColorUtility.ToHtmlStringRGBA(block.color);
                colors.Add(new MPCustomLevelColorInfo(i, color));
            }
        }

        string id = MPUser.instance.CreateCustomLevelImageID();
        if (!SaveCustomLevelImages(id))
        {
            return;
        }

        MPCustomLevelInfo levelInfo = new MPCustomLevelInfo(
            id,
            title,
            m_currentSize,
            blocks,
            colors);

        MPUser.instance.SaveCustomLevel(levelInfo);
        m_refreshAction?.Invoke();

        // 清空当前格子的状态
        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].Fill(false);
            m_blocks[i].ClearColor();
        }
    }


    /// <summary>
    /// 将当前自定义网格保存为像素图和关卡列表图标。
    /// </summary>
    private bool SaveCustomLevelImages(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        Texture2D levelTexture = null;
        Texture2D iconTexture = null;
        try
        {
            MPUser.instance.EnsureCustomLevelImageDirectory();

            levelTexture = CreateCustomLevelTexture();
            iconTexture = CreateCustomLevelIconTexture(levelTexture);

            File.WriteAllBytes(MPUser.instance.GetCustomLevelImagePath(id), levelTexture.EncodeToPNG());
            File.WriteAllBytes(MPUser.instance.GetCustomLevelIconImagePath(id), iconTexture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save custom level images failed: {exception}");
            return false;
        }
        finally
        {
            if (levelTexture != null)
            {
                UnityEngine.Object.Destroy(levelTexture);
            }

            if (iconTexture != null)
            {
                UnityEngine.Object.Destroy(iconTexture);
            }
        }
    }


    /// <summary>
    /// 根据当前网格创建5x5或10x10的像素颜色图片。
    /// </summary>
    private Texture2D CreateCustomLevelTexture()
    {
        Texture2D texture = new Texture2D(m_currentSize, m_currentSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < m_blocks.Count; i++)
        {
            int x = i % m_currentSize;
            int y = m_currentSize - 1 - i / m_currentSize;
            texture.SetPixel(x, y, GetCustomLevelPixelColor(m_blocks[i]));
        }

        texture.Apply(false, false);
        return texture;
    }


    /// <summary>
    /// 根据像素颜色图片创建200x200的关卡列表图标。
    /// </summary>
    private Texture2D CreateCustomLevelIconTexture(Texture2D sourceTexture)
    {
        const int iconSize = 200;
        Texture2D iconTexture = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
        iconTexture.filterMode = FilterMode.Point;
        iconTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < iconSize; y++)
        {
            int sourceY = y * sourceTexture.height / iconSize;
            for (int x = 0; x < iconSize; x++)
            {
                int sourceX = x * sourceTexture.width / iconSize;
                iconTexture.SetPixel(x, y, sourceTexture.GetPixel(sourceX, sourceY));
            }
        }

        iconTexture.Apply(false, false);
        return iconTexture;
    }


    /// <summary>
    /// 获取自定义格子在保存图片中使用的像素颜色。
    /// </summary>
    private Color GetCustomLevelPixelColor(MPCustomBlock block)
    {
        if (block == null)
            return Color.white;

        if (block.isColor)
            return block.color;

        return Color.white;
    }

    /// <summary>
    /// 打开自定义关卡仓库页面。
    /// </summary>
    private void OnWarehouseClick()
    {
        UIManager.Inst.ShowWindow<MPCustomLevelView>();
    }

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {

    }
}





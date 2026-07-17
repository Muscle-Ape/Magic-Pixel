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

        // 添加按钮回调
        m_fillModeBtn.onClick.AddListener(OnFillModeClick);
        m_colorModeBtn.onClick.AddListener(OnColorModeClick);
        m_sizeFiveBtn.onClick.AddListener(OnFiveSizeClick);
        m_sizeTenBtn.onClick.AddListener(OnTenSizeClick);
        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
        if (m_saveBtn != null)
        {
            m_saveBtn.onClick.AddListener(OnSaveClick);
        }
        if (m_warehouseBtn != null)
        {
            m_warehouseBtn.onClick.AddListener(OnWarehouseClick);
        }

        MPPalette palette = transform.Find("View/ColorFrame").GetComponent<MPPalette>();
        palette.Initialization(SetColor);
        if (ColorUtility.TryParseHtmlString(DEFAULT_CUSTOM_COLOR, out Color defaultColor))
        {
            palette.SetPaletteColor(defaultColor);
        }

        RefreshModeState();
        RefreshSizeState();
    }
    private void SetColor(Color color)
    {
        m_currentColor = color;
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }


    /// <summary>
    /// 切换到填充模式。
    /// </summary>
    private void OnFillModeClick()
    {
        SetCustomMode(true);
    }


    /// <summary>
    /// 切换到上色模式。
    /// </summary>
    private void OnColorModeClick()
    {
        SetCustomMode(false);
    }

    /// <summary>
    /// 设置当前自定义编辑模式。
    /// </summary>
    private void SetCustomMode(bool isFillMode)
    {
        if (m_isFillMode == isFillMode)
        {
            RefreshModeState();
            return;
        }

        m_isFillMode = isFillMode;
        RefreshModeState();
    }

    /// <summary>
    /// 刷新当前自定义编辑模式显示。
    /// </summary>
    private void RefreshModeState()
    {
        for (int i = 0; i < m_blocks.Count; i++)
        {
            m_blocks[i].SetMode(m_isFillMode);
        }
    }


    /// <summary>
    /// 切换到5x5尺寸。
    /// </summary>
    private void OnFiveSizeClick()
    {
        SetCustomSize(false);
    }

    /// <summary>
    /// 切换到10x10尺寸。
    /// </summary>
    private void OnTenSizeClick()
    {
        SetCustomSize(true);
    }

    /// <summary>
    /// 设置当前自定义网格尺寸。
    /// </summary>
    private void SetCustomSize(bool isTenSize)
    {
        if (m_isTenSize == isTenSize)
        {
            RefreshSizeState();
            return;
        }

        m_isTenSize = isTenSize;
        CreateGrid(m_isTenSize ? 10 : 5);
        RefreshSizeState();
        RefreshModeState();
    }

    /// <summary>
    /// 刷新当前自定义网格尺寸显示。
    /// </summary>
    private void RefreshSizeState()
    {
        if (m_sizeTenOpen != null)
        {
            m_sizeTenOpen.gameObject.SetActive(m_isTenSize);
        }

        if (m_sizeFiveOpen != null)
        {
            m_sizeFiveOpen.gameObject.SetActive(!m_isTenSize);
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






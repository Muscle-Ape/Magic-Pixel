using HQ.UIManager;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        InitializeSaveAnimation();
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
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        SetCustomSize(false);
    }

    /// <summary>
    /// 切换到10x10尺寸。
    /// </summary>
    private void OnTenSizeClick()
    {
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

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
        int cellCount = m_currentSize * m_currentSize;
        if (m_blocks == null || m_blocks.Count < cellCount)
        {
            return;
        }

        for (int i = 0; i < cellCount; i++)
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
        for (int i = 0; i < cellCount; i++)
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
        UploadCustomLevelImages(id);
        m_refreshAction?.Invoke();
        PlaySaveAnimation(id);

        // 清空当前格子的状态
        for (int i = 0; i < cellCount; i++)
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
    /// 异步上传自定义关卡图片到 Cloud Save Files。
    /// 上传失败不影响本地保存和结构化云快照同步。
    /// </summary>
    private void UploadCustomLevelImages(string id)
    {
        if (string.IsNullOrEmpty(id) || !MPLoginManager.Instance.IsLoggedIn)
        {
            return;
        }

        _ = UploadCustomLevelImagesAsync(id);
    }

    /// <summary>
    /// 上传自定义关卡完整图片和列表图标。
    /// </summary>
    private async Task UploadCustomLevelImagesAsync(string id)
    {
        await UploadCustomLevelFileAsync(
            MPCloudSaveConstants.CUSTOM_LEVEL_IMAGE_FILE_PREFIX + id,
            MPUser.instance.GetCustomLevelImagePath(id));

        await UploadCustomLevelFileAsync(
            MPCloudSaveConstants.CUSTOM_LEVEL_ICON_FILE_PREFIX + id,
            MPUser.instance.GetCustomLevelIconImagePath(id));
    }

    /// <summary>
    /// 上传单个自定义关卡图片文件。
    /// </summary>
    private static async Task UploadCustomLevelFileAsync(string key, string path)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        await MPCloudSaveManager.Instance.SavePlayerFileAsync(key, bytes);
    }


    /// <summary>
    /// 根据当前网格创建5x5或10x10的像素颜色图片。
    /// </summary>
    private Texture2D CreateCustomLevelTexture()
    {
        Texture2D texture = new Texture2D(m_currentSize, m_currentSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[m_currentSize * m_currentSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);

        int cellCount = m_currentSize * m_currentSize;
        int blockCount = m_blocks == null ? 0 : Mathf.Min(m_blocks.Count, cellCount);
        for (int i = 0; i < blockCount; i++)
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
    /// 初始化保存动画节点状态。
    /// </summary>
    private void InitializeSaveAnimation()
    {
        if (m_animationNode != null)
        {
            m_animationNodeStartAnchoredPosition = m_animationNode.anchoredPosition;
            m_animationNode.localScale = Vector3.one;
            m_animationNode.gameObject.SetActive(false);
        }

        if (m_warehouseIcon != null)
        {
            m_warehouseIconStartScale = m_warehouseIcon.localScale;
        }
    }

    /// <summary>
    /// 播放保存成功后飞向仓库的动画。
    /// </summary>
    private void PlaySaveAnimation(string id)
    {
        if (m_animationNode == null || m_animationPicture == null || m_warehouseIcon == null)
            return;

        m_saveAnimationSequence?.Kill();
        m_warehouseIcon.DOKill();
        CloseSaveAnimationNode();

        if (!SetSaveAnimationPicture(id))
            return;

        m_animationNode.gameObject.SetActive(true);
        m_animationNode.anchoredPosition = m_animationNodeStartAnchoredPosition;
        m_animationNode.localScale = Vector3.one;
        m_warehouseIcon.localScale = m_warehouseIconStartScale;

        m_saveAnimationSequence = DOTween.Sequence();
        m_saveAnimationSequence.Join(m_animationNode.DOMove(m_warehouseIcon.position, 0.45f).SetEase(Ease.InOutQuad));
        m_saveAnimationSequence.Join(m_animationNode.DOScale(Vector3.zero, 0.45f).SetEase(Ease.InQuad));
        m_saveAnimationSequence.Append(m_warehouseIcon.DOScale(m_warehouseIconStartScale * 1.2f, 0.12f).SetEase(Ease.OutBack));
        m_saveAnimationSequence.Append(m_warehouseIcon.DOScale(m_warehouseIconStartScale, 0.12f).SetEase(Ease.InOutQuad));
        m_saveAnimationSequence.OnComplete(CloseSaveAnimationNode);
    }

    /// <summary>
    /// 设置保存动画图片。
    /// </summary>
    private bool SetSaveAnimationPicture(string id)
    {
        ClearSaveAnimationAsset();

        string path = MPUser.instance.GetCustomLevelIconImagePath(id);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        byte[] bytes = File.ReadAllBytes(path);
        m_saveAnimationTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!m_saveAnimationTexture.LoadImage(bytes))
        {
            ClearSaveAnimationAsset();
            return false;
        }

        m_saveAnimationTexture.filterMode = FilterMode.Point;
        m_saveAnimationTexture.wrapMode = TextureWrapMode.Clamp;
        m_saveAnimationSprite = Sprite.Create(m_saveAnimationTexture, new Rect(0, 0, m_saveAnimationTexture.width, m_saveAnimationTexture.height), new Vector2(0.5f, 0.5f), 100f);
        m_animationPicture.sprite = m_saveAnimationSprite;
        return true;
    }

    /// <summary>
    /// 关闭保存动画节点并重置显示状态。
    /// </summary>
    private void CloseSaveAnimationNode()
    {
        if (m_animationNode != null)
        {
            m_animationNode.gameObject.SetActive(false);
            m_animationNode.anchoredPosition = m_animationNodeStartAnchoredPosition;
            m_animationNode.localScale = Vector3.one;
        }

        if (m_animationPicture != null)
        {
            m_animationPicture.sprite = null;
        }

        ClearSaveAnimationAsset();
    }

    /// <summary>
    /// 清理保存动画运行时创建的图片资源。
    /// </summary>
    private void ClearSaveAnimationAsset()
    {
        if (m_saveAnimationSprite != null)
        {
            Destroy(m_saveAnimationSprite);
            m_saveAnimationSprite = null;
        }

        if (m_saveAnimationTexture != null)
        {
            Destroy(m_saveAnimationTexture);
            m_saveAnimationTexture = null;
        }
    }

    /// <summary>
    /// 清理保存动画运行状态。
    /// </summary>
    private void ClearSaveAnimation()
    {
        m_saveAnimationSequence?.Kill();
        m_saveAnimationSequence = null;

        if (m_warehouseIcon != null)
        {
            m_warehouseIcon.DOKill();
            m_warehouseIcon.localScale = m_warehouseIconStartScale;
        }

        CloseSaveAnimationNode();
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
        ClearSaveAnimation();
        DestroyWindow();
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    /// <summary>
    /// 界面销毁时清理保存动画。
    /// </summary>
    private void OnDestroy()
    {
        ClearSaveAnimation();
    }
}










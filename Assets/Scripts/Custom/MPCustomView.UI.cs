using HQ.UIManager;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MPCustomView
{
    private void RegisterUI()
    {
        m_titleInput.text = MPUser.instance.GetDefaultCustomLevelTitle();
        InitializePublishButton();

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
        if (m_publishBtn != null)
        {
            m_publishBtn.onClick.AddListener(OnUploadClick);
        }

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishStateChanged += OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged += OnPublishOperationChanged;
        if (m_warehouseBtn != null)
        {
            m_warehouseBtn.onClick.AddListener(OnWarehouseClick);
        }
        if (m_communityBtn != null)
        {
            m_communityBtn.onClick.RemoveListener(OnCommunityClick);
            m_communityBtn.onClick.AddListener(OnCommunityClick);
        }

        MPPalette palette = transform.Find("View/ColorFrame").GetComponent<MPPalette>();
        palette.Initialization(SetColor);
        if (ColorUtility.TryParseHtmlString(DEFAULT_CUSTOM_COLOR, out Color defaultColor))
        {
            palette.SetPaletteColor(defaultColor);
        }

        RefreshModeState();
        RefreshSizeState();
        RefreshPublishButtonState();
        InitializeSaveAnimation();
    }

    /// <summary>
    /// 初始化公开上传按钮。当前 Prefab 没有 UploadBtn 时，运行时从 SaveBtn 克隆一个简单按钮。
    /// </summary>
    private void InitializePublishButton()
    {
        Transform publishTransform = transform.Find("View/UploadBtn");
        if (publishTransform != null)
        {
            m_publishBtn = publishTransform.GetComponent<Button>();
        }

        if (m_publishBtn == null && m_saveBtn != null)
        {
            GameObject publishObject = Instantiate(m_saveBtn.gameObject, m_saveBtn.transform.parent);
            publishObject.name = "UploadBtn";

            RectTransform publishRect = publishObject.GetComponent<RectTransform>();
            RectTransform saveRect = m_saveBtn.GetComponent<RectTransform>();
            if (publishRect != null && saveRect != null)
            {
                Vector2 position = saveRect.anchoredPosition;
                position.x = Mathf.Abs(position.x) > 1f ? -position.x : position.x + saveRect.sizeDelta.x + 30f;
                publishRect.anchoredPosition = position;
                publishRect.sizeDelta = saveRect.sizeDelta;
            }

            m_publishBtn = publishObject.GetComponent<Button>();
        }

        m_publishText = m_publishBtn == null ? null : m_publishBtn.GetComponentInChildren<TMP_Text>(true);
        if (m_publishText != null)
        {
            m_publishText.text = "Upload";
        }
    }

    /// <summary>
    /// 判断当前是否满足公开上传条件。
    /// </summary>
    private static bool CanUseCloudPublish()
    {
        return MPLoginManager.Instance != null && MPLoginManager.Instance.IsLoggedIn;
    }

    /// <summary>
    /// 刷新编辑页公开上传按钮的交互和文本状态。
    /// </summary>
    private void RefreshPublishButtonState()
    {
        if (m_publishBtn == null)
        {
            return;
        }

        bool isPublishPending = m_pendingPublishLevelInfo != null &&
                                MPCustomLevelPublishManager.Instance.IsPublishPending(m_pendingPublishLevelInfo.ID);
        bool isPublished = m_pendingPublishLevelInfo != null &&
                           MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(m_pendingPublishLevelInfo.ID);
        bool isBusy = m_isPublishActionRunning || isPublishPending;
        m_publishBtn.interactable = !isBusy && !isPublished && CanUseCloudPublish();
        if (m_publishText == null)
        {
            return;
        }

        m_publishText.text = isBusy ? "..." : isPublished ? "Uploaded" : "Upload";
    }

    /// <summary>
    /// 当前待发布关卡状态变化时刷新编辑页上传按钮。
    /// </summary>
    private void OnPublishStateChanged(MPCustomLevelPublishLocalState state)
    {
        if (state == null || m_pendingPublishLevelInfo == null ||
            state.sourceLocalLevelId != m_pendingPublishLevelInfo.ID)
        {
            return;
        }

        RefreshPublishButtonState();
    }

    /// <summary>
    /// 当前待发布关卡的上传任务开始或结束时刷新按钮。
    /// </summary>
    private void OnPublishOperationChanged(string sourceLocalLevelId)
    {
        if (m_pendingPublishLevelInfo == null || sourceLocalLevelId != m_pendingPublishLevelInfo.ID)
        {
            return;
        }

        RefreshPublishButtonState();
    }

    /// <summary>
    /// 用户开始编辑新内容后解除上一关卡的上传按钮状态。
    /// </summary>
    private void BeginNewPublishDraft()
    {
        if (m_pendingPublishLevelInfo == null)
        {
            return;
        }

        m_pendingPublishLevelInfo = null;
        RefreshPublishButtonState();
    }

    /// <summary>
    /// 取消当前编辑页正在等待的公开上传操作。
    /// </summary>
    private void CancelPublishOperation()
    {
        if (m_publishCancellation == null)
        {
            return;
        }

        m_publishCancellation.Cancel();
        m_publishCancellation.Dispose();
        m_publishCancellation = null;
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

        BeginNewPublishDraft();
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
        SaveCurrentCustomLevel();
    }

    /// <summary>
    /// 上传当前编辑中的自定义关卡到公开关卡池。
    /// </summary>
    private async void OnUploadClick()
    {
        if (m_isPublishActionRunning)
        {
            return;
        }

        if (!CanUseCloudPublish())
        {
            Debug.LogWarning("[MPCustomView] 请先登录后再上传公开自定义关卡。");
            RefreshPublishButtonState();
            return;
        }

        m_isPublishActionRunning = true;
        RefreshPublishButtonState();
        CancelPublishOperation();
        m_publishCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = m_publishCancellation;

        try
        {
            MPCustomLevelInfo levelInfo = m_pendingPublishLevelInfo;
            if (levelInfo == null)
            {
                levelInfo = SaveCurrentCustomLevel();
                m_pendingPublishLevelInfo = levelInfo;
            }

            if (levelInfo == null)
            {
                Debug.LogWarning("[MPCustomView] 当前自定义关卡未完成，无法上传。");
                return;
            }

            if (MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(levelInfo.ID))
            {
                Debug.LogWarning("[MPCustomView] 当前自定义关卡已经上传，不能重复上传。");
                return;
            }

            MPCustomLevelPublishResult publishResult = await MPCustomLevelPublishManager.Instance.PublishAsync(levelInfo, cancellation.Token);
            if (publishResult == null || !publishResult.success)
            {
                Debug.LogWarning($"[MPCustomView] 上传公开关卡失败：{publishResult?.message}");
            }
            else
            {
                Debug.Log($"[MPCustomView] 已上传公开关卡：{publishResult.publicLevelId}");
            }
        }
        catch (OperationCanceledException)
        {
            // 界面关闭时取消异步流程，不需要额外提示。
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPCustomView] 上传公开关卡异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
        }
        finally
        {
            if (m_publishCancellation == cancellation)
            {
                m_publishCancellation = null;
                cancellation.Dispose();
            }

            if (this != null)
            {
                m_isPublishActionRunning = false;
                RefreshPublishButtonState();
            }
        }
    }

    /// <summary>
    /// 保存当前编辑中的自定义关卡并返回保存后的关卡数据。
    /// </summary>
    private MPCustomLevelInfo SaveCurrentCustomLevel()
    {
        int cellCount = m_currentSize * m_currentSize;
        if (m_blocks == null || m_blocks.Count < cellCount)
        {
            return null;
        }

        for (int i = 0; i < cellCount; i++)
        {
            if (!m_blocks[i].isColor)
                return null;
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
        if (!SaveCustomLevelImage(id))
        {
            return null;
        }

        MPCustomLevelInfo levelInfo = new MPCustomLevelInfo(
            id,
            title,
            m_currentSize,
            blocks,
            colors);

        MPUser.instance.SaveCustomLevel(levelInfo);
        UploadCustomLevelImage(id);
        m_refreshAction?.Invoke();
        PlaySaveAnimation(id);
        ClearCurrentCustomGrid(cellCount);

        return levelInfo;
    }

    /// <summary>
    /// 清空当前编辑网格，保存或上传完成后让玩家可以继续绘制下一张图。
    /// </summary>
    private void ClearCurrentCustomGrid(int cellCount)
    {
        if (m_blocks == null)
        {
            return;
        }

        for (int i = 0; i < cellCount; i++)
        {
            m_blocks[i].Fill(false);
            m_blocks[i].ClearColor();
        }
    }


    /// <summary>
    /// 将当前自定义网格保存为最小尺寸的像素数据图。
    /// </summary>
    private bool SaveCustomLevelImage(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        Texture2D levelTexture = null;
        try
        {
            MPUser.instance.EnsureCustomLevelImageDirectory();

            levelTexture = CreateCustomLevelTexture();
            File.WriteAllBytes(MPUser.instance.GetCustomLevelImagePath(id), levelTexture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save custom level image failed: {exception}");
            return false;
        }
        finally
        {
            if (levelTexture != null)
            {
                UnityEngine.Object.Destroy(levelTexture);
            }
        }
    }

    /// <summary>
    /// 异步上传自定义关卡图片到 Cloud Save Files。
    /// 上传失败不影响本地保存和结构化云快照同步。
    /// </summary>
    private void UploadCustomLevelImage(string id)
    {
        if (string.IsNullOrEmpty(id) || !MPLoginManager.Instance.IsLoggedIn)
        {
            return;
        }

        _ = UploadCustomLevelImageAsync(id);
    }

    /// <summary>
    /// 仅上传自定义关卡的最小尺寸像素数据图。
    /// </summary>
    private async Task UploadCustomLevelImageAsync(string id)
    {
        await UploadCustomLevelFileAsync(
            MPCloudSaveConstants.CUSTOM_LEVEL_IMAGE_FILE_PREFIX + id,
            MPUser.instance.GetCustomLevelImagePath(id));
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

        string path = MPUser.instance.GetCustomLevelImagePath(id);
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

    /// <summary>
    /// 打开社区公开自定义关卡页面。
    /// </summary>
    private void OnCommunityClick()
    {
        UIManager.Inst.ShowWindow<MPCommunityView>();
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
        if (m_communityBtn != null)
        {
            m_communityBtn.onClick.RemoveListener(OnCommunityClick);
        }

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
        CancelPublishOperation();
        ClearSaveAnimation();
        MPLoad.ReleaseAll(this);
    }
}





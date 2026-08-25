using DG.Tweening;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public partial class MPHomeView
{
    private static bool CanUseCustomCloudPublish()
    {
        return MPLoginManager.Instance != null && MPLoginManager.Instance.IsLoggedIn;
    }

    private void RefreshCustomPublishButtonState()
    {
        if (m_customPublishBtn == null)
            return;

        bool isPending = m_customPendingPublishLevelInfo != null
            && MPCustomLevelPublishManager.Instance.IsPublishPending(m_customPendingPublishLevelInfo.ID);
        bool isPublished = m_customPendingPublishLevelInfo != null
            && MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(m_customPendingPublishLevelInfo.ID);
        bool isBusy = m_customPublishActionRunning || isPending;

        m_customPublishBtn.interactable = !isBusy && !isPublished && CanUseCustomCloudPublish();
        if (m_customPublishText != null)
            m_customPublishText.text = isBusy ? "..." : isPublished ? "Uploaded" : "Upload";
    }

    private void OnCustomPublishStateChanged(MPCustomLevelPublishLocalState state)
    {
        if (state == null || m_customPendingPublishLevelInfo == null
            || state.sourceLocalLevelId != m_customPendingPublishLevelInfo.ID)
        {
            return;
        }

        RefreshCustomPublishButtonState();
    }

    private void OnCustomPublishOperationChanged(string sourceLocalLevelId)
    {
        if (m_customPendingPublishLevelInfo == null
            || sourceLocalLevelId != m_customPendingPublishLevelInfo.ID)
        {
            return;
        }

        RefreshCustomPublishButtonState();
    }

    private void BeginNewCustomPublishDraft()
    {
        if (m_customPendingPublishLevelInfo == null)
            return;

        m_customPendingPublishLevelInfo = null;
        RefreshCustomPublishButtonState();
    }

    private void CancelCustomPublishOperation()
    {
        if (m_customPublishCancellation == null)
            return;

        m_customPublishCancellation.Cancel();
        m_customPublishCancellation.Dispose();
        m_customPublishCancellation = null;
    }

    private void OnCustomSaveClick()
    {
        MPCustomLevelInfo levelInfo = SaveCurrentHomeCustomLevel();
        if (levelInfo == null)
        {
            Debug.LogWarning("[MPHomeView] 自定义关卡必须完成全部格子的上色后才能保存。");
            return;
        }

        m_customPendingPublishLevelInfo = levelInfo;
        RefreshCustomPublishButtonState();
    }

    private async void OnCustomUploadClick()
    {
        if (m_customPublishActionRunning)
            return;

        if (!CanUseCustomCloudPublish())
        {
            Debug.LogWarning("[MPHomeView] 请先登录后再上传公开自定义关卡。");
            RefreshCustomPublishButtonState();
            return;
        }

        m_customPublishActionRunning = true;
        RefreshCustomPublishButtonState();
        CancelCustomPublishOperation();
        m_customPublishCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = m_customPublishCancellation;

        try
        {
            MPCustomLevelInfo levelInfo = m_customPendingPublishLevelInfo;
            if (levelInfo == null)
            {
                levelInfo = SaveCurrentHomeCustomLevel();
                m_customPendingPublishLevelInfo = levelInfo;
            }

            if (levelInfo == null)
            {
                Debug.LogWarning("[MPHomeView] 当前自定义关卡未完成，无法上传。");
                return;
            }

            if (MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(levelInfo.ID))
            {
                Debug.LogWarning("[MPHomeView] 当前自定义关卡已经上传，不能重复上传。");
                return;
            }

            MPCustomLevelPublishResult publishResult = await MPCustomLevelPublishManager.Instance.PublishAsync(
                levelInfo,
                cancellation.Token);
            if (publishResult == null || !publishResult.success)
            {
                Debug.LogWarning($"[MPHomeView] 上传公开关卡失败：{publishResult?.message}");
            }
            else
            {
                Debug.Log($"[MPHomeView] 已上传公开关卡：{publishResult.publicLevelId}");
            }
        }
        catch (OperationCanceledException)
        {
            // 页面释放时只停止 UI 等待，PublishManager 会继续完成已提交的云端任务并保存结果。
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPHomeView] 上传公开关卡异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
        }
        finally
        {
            if (m_customPublishCancellation == cancellation)
            {
                m_customPublishCancellation = null;
                cancellation.Dispose();
            }

            if (this != null && m_customInitialized)
            {
                m_customPublishActionRunning = false;
                RefreshCustomPublishButtonState();
            }
        }
    }

    private MPCustomLevelInfo SaveCurrentHomeCustomLevel()
    {
        int cellCount = m_customCurrentSize * m_customCurrentSize;
        if (m_customBlocks == null || m_customBlocks.Count < cellCount)
            return null;

        for (int i = 0; i < cellCount; i++)
        {
            if (!m_customBlocks[i].isColor)
                return null;
        }

        string title = m_customTitleInput == null ? string.Empty : m_customTitleInput.text;
        if (string.IsNullOrWhiteSpace(title))
            title = MPUser.instance.GetDefaultCustomLevelTitle();

        List<int> blocks = new List<int>();
        List<MPCustomLevelColorInfo> colors = new List<MPCustomLevelColorInfo>(cellCount);
        for (int i = 0; i < cellCount; i++)
        {
            MPCustomBlock block = m_customBlocks[i];
            if (block.isFill)
                blocks.Add(i);

            colors.Add(new MPCustomLevelColorInfo(
                i,
                "#" + ColorUtility.ToHtmlStringRGBA(block.color)));
        }

        string id = MPUser.instance.CreateCustomLevelImageID();
        if (!SaveHomeCustomLevelImage(id))
            return null;

        MPCustomLevelInfo levelInfo = new MPCustomLevelInfo(
            id,
            title,
            m_customCurrentSize,
            blocks,
            colors);

        MPUser.instance.SaveCustomLevel(levelInfo);
        UploadHomeCustomLevelImage(id);
        PlayCustomSaveAnimation(id);
        ClearCurrentHomeCustomGrid(cellCount);
        return levelInfo;
    }

    private void ClearCurrentHomeCustomGrid(int cellCount)
    {
        if (m_customBlocks == null)
            return;

        int count = Mathf.Min(cellCount, m_customBlocks.Count);
        for (int i = 0; i < count; i++)
        {
            m_customBlocks[i].Fill(false);
            m_customBlocks[i].ClearColor();
        }
    }

    private bool SaveHomeCustomLevelImage(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        Texture2D levelTexture = null;
        try
        {
            MPUser.instance.EnsureCustomLevelImageDirectory();
            levelTexture = CreateHomeCustomLevelTexture();
            File.WriteAllBytes(
                MPUser.instance.GetCustomLevelImagePath(id),
                levelTexture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"保存自定义关卡图片失败：{exception}");
            return false;
        }
        finally
        {
            if (levelTexture != null)
                Destroy(levelTexture);
        }
    }

    private Texture2D CreateHomeCustomLevelTexture()
    {
        Texture2D texture = new Texture2D(
            m_customCurrentSize,
            m_customCurrentSize,
            TextureFormat.RGBA32,
            false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        int cellCount = m_customCurrentSize * m_customCurrentSize;
        for (int i = 0; i < cellCount; i++)
        {
            int x = i % m_customCurrentSize;
            int y = m_customCurrentSize - 1 - i / m_customCurrentSize;
            texture.SetPixel(x, y, m_customBlocks[i].color);
        }

        texture.Apply(false, false);
        return texture;
    }

    private void UploadHomeCustomLevelImage(string id)
    {
        if (string.IsNullOrEmpty(id) || !CanUseCustomCloudPublish())
            return;

        _ = UploadHomeCustomLevelImageAsync(id);
    }

    private async Task UploadHomeCustomLevelImageAsync(string id)
    {
        string path = MPUser.instance.GetCustomLevelImagePath(id);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            await MPCloudSaveManager.Instance.SavePlayerFileAsync(
                MPCloudSaveConstants.CUSTOM_LEVEL_IMAGE_FILE_PREFIX + id,
                bytes);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPHomeView] 上传自定义关卡像素图失败：{exception.Message}");
        }
    }

    private void InitializeCustomSaveAnimation()
    {
        if (m_customAnimationNode != null)
        {
            m_customAnimationNodeStartPosition = m_customAnimationNode.anchoredPosition;
            m_customAnimationNode.localScale = Vector3.one;
            m_customAnimationNode.gameObject.SetActive(false);
        }

        if (m_customWarehouseBtn != null)
            m_customWarehouseStartScale = m_customWarehouseBtn.transform.localScale;
    }

    private void PlayCustomSaveAnimation(string id)
    {
        if (m_customAnimationNode == null
            || m_customAnimationPicture == null
            || m_customWarehouseBtn == null)
        {
            return;
        }

        ClearCustomSaveAnimation();
        if (!SetCustomSaveAnimationPicture(id))
            return;

        Transform warehouse = m_customWarehouseBtn.transform;
        m_customAnimationNode.gameObject.SetActive(true);
        m_customAnimationNode.anchoredPosition = m_customAnimationNodeStartPosition;
        m_customAnimationNode.localScale = Vector3.one;
        warehouse.localScale = m_customWarehouseStartScale;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        sequence.Join(m_customAnimationNode.DOMove(warehouse.position, 0.45f).SetEase(Ease.InOutQuad));
        sequence.Join(m_customAnimationNode.DOScale(Vector3.zero, 0.45f).SetEase(Ease.InQuad));
        sequence.Append(warehouse.DOScale(m_customWarehouseStartScale * 1.12f, 0.12f).SetEase(Ease.OutBack));
        sequence.Append(warehouse.DOScale(m_customWarehouseStartScale, 0.12f).SetEase(Ease.InOutQuad));
        m_customSaveAnimationSequence = sequence;
        sequence.OnComplete(CloseCustomSaveAnimationNode);
        sequence.OnKill(() =>
        {
            if (m_customSaveAnimationSequence == sequence)
                m_customSaveAnimationSequence = null;
        });
    }

    private bool SetCustomSaveAnimationPicture(string id)
    {
        ClearCustomSaveAnimationAsset();
        string path = MPUser.instance.GetCustomLevelImagePath(id);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        byte[] bytes = File.ReadAllBytes(path);
        m_customSaveAnimationTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!m_customSaveAnimationTexture.LoadImage(bytes))
        {
            ClearCustomSaveAnimationAsset();
            return false;
        }

        m_customSaveAnimationTexture.filterMode = FilterMode.Point;
        m_customSaveAnimationTexture.wrapMode = TextureWrapMode.Clamp;
        m_customSaveAnimationSprite = Sprite.Create(
            m_customSaveAnimationTexture,
            new Rect(0, 0, m_customSaveAnimationTexture.width, m_customSaveAnimationTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        m_customAnimationPicture.sprite = m_customSaveAnimationSprite;
        return true;
    }

    private void CloseCustomSaveAnimationNode()
    {
        m_customSaveAnimationSequence = null;
        if (m_customAnimationNode != null)
        {
            m_customAnimationNode.gameObject.SetActive(false);
            m_customAnimationNode.anchoredPosition = m_customAnimationNodeStartPosition;
            m_customAnimationNode.localScale = Vector3.one;
        }

        if (m_customAnimationPicture != null)
            m_customAnimationPicture.sprite = null;

        ClearCustomSaveAnimationAsset();
    }

    private void ClearCustomSaveAnimationAsset()
    {
        if (m_customSaveAnimationSprite != null)
        {
            Destroy(m_customSaveAnimationSprite);
            m_customSaveAnimationSprite = null;
        }

        if (m_customSaveAnimationTexture != null)
        {
            Destroy(m_customSaveAnimationTexture);
            m_customSaveAnimationTexture = null;
        }
    }

    private void ClearCustomSaveAnimation()
    {
        Sequence sequence = m_customSaveAnimationSequence;
        m_customSaveAnimationSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill();

        if (m_customWarehouseBtn != null)
        {
            m_customWarehouseBtn.transform.DOKill();
            m_customWarehouseBtn.transform.localScale = m_customWarehouseStartScale;
        }

        CloseCustomSaveAnimationNode();
    }
}

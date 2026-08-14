using HQ.UIManager;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 社区公开自定义关卡列表项。
/// </summary>
public class MPCommunityLevelItem : MonoBehaviour
{
    private Image m_picture;
    private TMP_Text m_titleText;
    private TMP_Text m_playerNameText;
    private TMP_Text m_sizeText;
    private Button m_loveBtn;
    private Button m_playBtn;

    private MPCustomLevelPublicRecord m_record;
    private bool m_isOperationRunning;
    private int m_bindingVersion;
    private CancellationTokenSource m_operationCancellation;

    private Texture2D m_previewTexture;
    private Sprite m_previewSprite;

    public void Initialize()
    {
        m_picture = transform.Find("Picture").GetComponent<Image>();
        m_titleText = transform.Find("Title").GetComponent<TMP_Text>();
        m_playerNameText = transform.Find("PlayerName").GetComponent<TMP_Text>();
        m_sizeText = transform.Find("Size").GetComponent<TMP_Text>();
        m_loveBtn = transform.Find("LoveBtn").GetComponent<Button>();
        m_playBtn = transform.Find("PlayBtn").GetComponent<Button>();

        m_loveBtn.onClick.RemoveListener(OnLoveClick);
        m_loveBtn.onClick.AddListener(OnLoveClick);
        m_playBtn.onClick.RemoveListener(OnPlayClick);
        m_playBtn.onClick.AddListener(OnPlayClick);
    }

    public void Refresh(MPCustomLevelPublicRecord record)
    {
        CancelOperation();
        m_bindingVersion++;
        m_record = record;
        m_isOperationRunning = false;

        m_titleText.text = string.IsNullOrEmpty(record?.title) ? "Undefined" : record.title;
        m_playerNameText.text = string.IsNullOrEmpty(record?.ownerDisplayName)
            ? "Player"
            : record.ownerDisplayName;
        m_sizeText.text = record == null ? string.Empty : $"{record.size}x{record.size}";

        RefreshPreview();
        RefreshButtonState();
    }

    private void RefreshPreview()
    {
        ClearPreviewAssets();
        if (m_record == null || m_picture == null)
        {
            return;
        }

        MPCustomLevelInfo levelInfo = MPCustomLevelPublishManager.Instance.ToLocalPlayableLevel(m_record);
        if (levelInfo == null)
        {
            m_picture.sprite = null;
            return;
        }

        m_previewTexture = MPUser.instance.LoadCustomLevelImageTexture(levelInfo);
        if (m_previewTexture == null)
        {
            m_picture.sprite = null;
            return;
        }

        m_previewTexture.filterMode = FilterMode.Point;
        m_previewTexture.wrapMode = TextureWrapMode.Clamp;
        m_previewSprite = Sprite.Create(
            m_previewTexture,
            new Rect(0, 0, m_previewTexture.width, m_previewTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        m_picture.sprite = m_previewSprite;
    }

    private async void OnLoveClick()
    {
        if (!CanStartOperation() || m_record.likedByCurrentPlayer)
        {
            return;
        }

        string publicLevelId = m_record.publicLevelId;
        int bindingVersion = m_bindingVersion;
        CancellationTokenSource cancellation = BeginOperation();
        CancellationToken cancellationToken = cancellation.Token;

        try
        {
            MPCustomLevelLikeResult result = await MPCustomLevelPublishManager.Instance.LikeAsync(
                publicLevelId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCurrentBinding(publicLevelId, bindingVersion, cancellation))
            {
                return;
            }

            if (result == null || !result.success)
            {
                Debug.LogWarning($"[MPCommunityLevelItem] 点赞失败：{result?.message}");
                return;
            }

            if (result.record != null)
            {
                ApplyRecord(result.record);
            }
            else
            {
                m_record.likedByCurrentPlayer = result.liked;
                m_record.likeCount = result.likeCount;
            }
        }
        catch (OperationCanceledException)
        {
            // Item 被复用或销毁时取消 UI 回写。
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[MPCommunityLevelItem] 点赞异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
        }
        finally
        {
            EndOperation(cancellation, bindingVersion);
        }
    }

    private async void OnPlayClick()
    {
        if (!CanStartOperation())
        {
            return;
        }

        string publicLevelId = m_record.publicLevelId;
        int bindingVersion = m_bindingVersion;
        CancellationTokenSource cancellation = BeginOperation();
        CancellationToken cancellationToken = cancellation.Token;

        try
        {
            MPCustomLevelPublicRecord latestRecord = await MPCustomLevelPublishManager.Instance.PlayAsync(
                publicLevelId,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCurrentBinding(publicLevelId, bindingVersion, cancellation))
            {
                return;
            }

            if (latestRecord == null || !latestRecord.IsPublished)
            {
                Debug.LogWarning("[MPCommunityLevelItem] 公开关卡不存在或已撤销。");
                return;
            }

            ApplyRecord(latestRecord);
            AWindow sourceWindow = GetComponentInParent<AWindow>();
            MPCustomLevelPublishManager.Instance.OpenPublicLevelGame(m_record, sourceWindow);
        }
        catch (OperationCanceledException)
        {
            // Item 被复用或销毁时取消后续页面打开。
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[MPCommunityLevelItem] 打开公开关卡异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
        }
        finally
        {
            EndOperation(cancellation, bindingVersion);
        }
    }

    private bool CanStartOperation()
    {
        return m_record != null &&
               !string.IsNullOrEmpty(m_record.publicLevelId) &&
               !m_isOperationRunning;
    }

    /// <summary>
    /// 将服务端最新状态写回列表持有的记录实例，避免 Item 复用后丢失点赞等状态。
    /// </summary>
    private void ApplyRecord(MPCustomLevelPublicRecord latestRecord)
    {
        if (m_record == null || latestRecord == null)
        {
            return;
        }

        m_record.schemaVersion = latestRecord.schemaVersion;
        m_record.publicLevelId = latestRecord.publicLevelId;
        m_record.sourceLocalLevelId = latestRecord.sourceLocalLevelId;
        m_record.ownerPlayerId = latestRecord.ownerPlayerId;
        m_record.ownerDisplayName = latestRecord.ownerDisplayName;
        m_record.title = latestRecord.title;
        m_record.size = latestRecord.size;
        m_record.block = latestRecord.block;
        m_record.colors = latestRecord.colors;
        m_record.likeCount = latestRecord.likeCount;
        m_record.playCount = latestRecord.playCount;
        m_record.status = latestRecord.status;
        m_record.likedByCurrentPlayer = latestRecord.likedByCurrentPlayer;
        m_record.likedPlayerIds = latestRecord.likedPlayerIds;
        m_record.createdAtUtcTicks = latestRecord.createdAtUtcTicks;
        m_record.updatedAtUtcTicks = latestRecord.updatedAtUtcTicks;
        m_record.clientVersion = latestRecord.clientVersion;
        m_record.unityEnvironment = latestRecord.unityEnvironment;
    }

    private CancellationTokenSource BeginOperation()
    {
        CancelOperation();
        m_operationCancellation = new CancellationTokenSource();
        m_isOperationRunning = true;
        RefreshButtonState();
        return m_operationCancellation;
    }

    private void EndOperation(CancellationTokenSource cancellation, int bindingVersion)
    {
        if (m_operationCancellation != cancellation)
        {
            return;
        }

        m_operationCancellation = null;
        cancellation.Dispose();
        if (m_bindingVersion == bindingVersion && this != null)
        {
            m_isOperationRunning = false;
            RefreshButtonState();
        }
    }

    private bool IsCurrentBinding(
        string publicLevelId,
        int bindingVersion,
        CancellationTokenSource cancellation)
    {
        return m_operationCancellation == cancellation &&
               m_bindingVersion == bindingVersion &&
               m_record != null &&
               m_record.publicLevelId == publicLevelId;
    }

    private void RefreshButtonState()
    {
        bool hasRecord = m_record != null && !string.IsNullOrEmpty(m_record.publicLevelId);
        if (m_loveBtn != null)
        {
            m_loveBtn.interactable = hasRecord &&
                                     !m_isOperationRunning &&
                                     !m_record.likedByCurrentPlayer;
        }

        if (m_playBtn != null)
        {
            m_playBtn.interactable = hasRecord && !m_isOperationRunning;
        }
    }

    private void CancelOperation()
    {
        if (m_operationCancellation == null)
        {
            return;
        }

        m_operationCancellation.Cancel();
        m_operationCancellation.Dispose();
        m_operationCancellation = null;
        m_isOperationRunning = false;
    }

    private void ClearPreviewAssets()
    {
        if (m_picture != null)
        {
            m_picture.sprite = null;
        }

        if (m_previewSprite != null)
        {
            Destroy(m_previewSprite);
            m_previewSprite = null;
        }

        if (m_previewTexture != null)
        {
            Destroy(m_previewTexture);
            m_previewTexture = null;
        }
    }

    private void OnDestroy()
    {
        if (m_loveBtn != null)
        {
            m_loveBtn.onClick.RemoveListener(OnLoveClick);
        }

        if (m_playBtn != null)
        {
            m_playBtn.onClick.RemoveListener(OnPlayClick);
        }

        CancelOperation();
        ClearPreviewAssets();
    }
}

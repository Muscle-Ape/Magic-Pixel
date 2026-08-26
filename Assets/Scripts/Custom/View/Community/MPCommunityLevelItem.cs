using DG.Tweening;
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
    private const float LOVE_SCALE_DURATION = 0.28f;
    private const float FADE_START_ANCHORED_Y = 500f;

    /// <summary>
    /// 两列网格按 12 行为一组循环。相邻行偏移采用平滑过渡，避免某一列突然出现大段留白；
    /// 偏移与角度已按当前 484x771 Item、40px 横向间距校验，循环首尾也能连续衔接。
    /// </summary>
    private static readonly CommunityItemLayout[] ITEM_LAYOUTS =
    {
        new CommunityItemLayout(20f, -2.4f),
        new CommunityItemLayout(-25f, 2f),

        new CommunityItemLayout(5f, 1.4f),
        new CommunityItemLayout(-5f, 4f),

        new CommunityItemLayout(-15f, 2.5f),
        new CommunityItemLayout(20f, -1.5f),

        new CommunityItemLayout(-30f, -1.8f),
        new CommunityItemLayout(35f, 2.7f),

        new CommunityItemLayout(-10f, 0f),
        new CommunityItemLayout(15f, -2.8f),

        new CommunityItemLayout(15f, -2.5f),
        new CommunityItemLayout(-10f, 1.7f),

        new CommunityItemLayout(35f, 2.8f),
        new CommunityItemLayout(-30f, 1.3f),

        new CommunityItemLayout(25f, -1.4f),
        new CommunityItemLayout(-20f, -2.3f),

        new CommunityItemLayout(0f, 2.2f),
        new CommunityItemLayout(5f, -1.8f),

        new CommunityItemLayout(-20f, -2.7f),
        new CommunityItemLayout(25f, 2.4f),

        new CommunityItemLayout(-5f, 1.6f),
        new CommunityItemLayout(10f, 2.6f),

        new CommunityItemLayout(15f, -2.1f),
        new CommunityItemLayout(-10f, -1.2f),
    };

    private RectTransform m_node;
    private CanvasGroup m_nodeCanvasGroup;
    private Image m_picture;
    private TMP_Text m_titleText;
    private TMP_Text m_playerNameText;
    private TMP_Text m_sizeText;
    private TMP_Text m_loveCountText;
    private RectTransform m_loveOpen;
    private Button m_loveBtn;
    private Button m_playBtn;

    private MPCustomLevelPublicRecord m_record;
    private bool m_isOperationRunning;
    private bool m_initialized;
    private int m_bindingVersion;
    private CancellationTokenSource m_operationCancellation;

    private Texture2D m_previewTexture;
    private Sprite m_previewSprite;
    private readonly Vector3[] m_nodeWorldCorners = new Vector3[4];
    private Vector2 m_nodeDefaultPosition;
    private float m_nodeDefaultAngle;
    private Vector3 m_loveOpenScale = Vector3.one;
    private Tween m_loveTween;

    public void Initialize()
    {
        if (m_initialized)
            return;

        m_node = transform.Find("Node") as RectTransform;
        if (m_node != null)
        {
            m_nodeCanvasGroup = m_node.GetComponent<CanvasGroup>();
            m_nodeDefaultPosition = m_node.anchoredPosition;
            m_nodeDefaultAngle = m_node.localEulerAngles.z;
        }

        m_picture = transform.Find("Node/Mask/Picture")?.GetComponent<Image>();
        m_titleText = transform.Find("Node/Title")?.GetComponent<TMP_Text>();
        m_playerNameText = transform.Find("Node/PlayerName")?.GetComponent<TMP_Text>();
        m_sizeText = transform.Find("Node/Size")?.GetComponent<TMP_Text>();
        m_loveCountText = transform.Find("Node/LoveBtn/Count")?.GetComponent<TMP_Text>();
        m_loveOpen = transform.Find("Node/LoveBtn/Love/Open") as RectTransform;
        m_loveBtn = transform.Find("Node/LoveBtn")?.GetComponent<Button>();
        m_playBtn = transform.Find("Node/PlayBtn")?.GetComponent<Button>();
        if (m_loveOpen != null)
            m_loveOpenScale = m_loveOpen.localScale;

        if (m_loveBtn != null)
        {
            m_loveBtn.onClick.RemoveListener(OnLoveClick);
            m_loveBtn.onClick.AddListener(OnLoveClick);
        }
        if (m_playBtn != null)
        {
            m_playBtn.onClick.RemoveListener(OnPlayClick);
            m_playBtn.onClick.AddListener(OnPlayClick);
        }

        m_initialized = true;
    }

    public void Refresh(MPCustomLevelPublicRecord record)
    {
        if (!m_initialized)
            Initialize();

        CancelOperation();
        m_bindingVersion++;
        m_record = record;
        m_isOperationRunning = false;
        SetNodeAlpha(1f);

        RefreshTexts();
        RefreshPreview();
        RefreshStatusState();
    }

    /// <summary>
    /// 只调整预制体 Node 的 Y 轴位置和 Z 轴角度，Item 根节点仍由 LoopGridView 管理。
    /// </summary>
    public void ApplyLayout(int itemIndex)
    {
        if (!m_initialized)
            Initialize();
        if (m_node == null)
            return;

        int layoutIndex = itemIndex % ITEM_LAYOUTS.Length;
        if (layoutIndex < 0)
            layoutIndex += ITEM_LAYOUTS.Length;

        CommunityItemLayout layout = ITEM_LAYOUTS[layoutIndex];
        Vector2 position = m_nodeDefaultPosition;
        position.y += layout.verticalOffset;
        m_node.anchoredPosition = position;
        m_node.localRotation = Quaternion.Euler(
            0f,
            0f,
            m_nodeDefaultAngle + layout.rotationAngle);
    }

    /// <summary>
    /// 根据 Item 根节点相对 Levels 的位置更新 Node 透明度。
    /// Item 的可视 Y 小于等于 500 时完全显示，旋转后的 Node 完全退出顶部时透明度为 0。
    /// </summary>
    public void RefreshLevelsAlpha(RectTransform levelsRect)
    {
        if (!m_initialized)
            Initialize();
        if (levelsRect == null || m_node == null || m_nodeCanvasGroup == null)
            return;

        float itemAnchoredY = levelsRect.InverseTransformPoint(transform.position).y;
        if (itemAnchoredY <= FADE_START_ANCHORED_Y)
        {
            SetNodeAlpha(1f);
            return;
        }

        m_node.GetWorldCorners(m_nodeWorldCorners);
        float nodeBottomY = float.MaxValue;
        for (int i = 0; i < m_nodeWorldCorners.Length; i++)
        {
            float cornerY = levelsRect.InverseTransformPoint(m_nodeWorldCorners[i]).y;
            nodeBottomY = Mathf.Min(nodeBottomY, cornerY);
        }

        // nodeBottomY 到达 Levels 顶边时，说明包含旋转范围在内的 Node 已完全退出。
        float fullyExitedAnchoredY = itemAnchoredY + levelsRect.rect.yMax - nodeBottomY;
        float alpha = fullyExitedAnchoredY <= FADE_START_ANCHORED_Y
            ? 0f
            : 1f - Mathf.InverseLerp(
                FADE_START_ANCHORED_Y,
                fullyExitedAnchoredY,
                itemAnchoredY);
        SetNodeAlpha(alpha);
    }

    private void SetNodeAlpha(float alpha)
    {
        if (m_nodeCanvasGroup == null)
            return;

        alpha = Mathf.Clamp01(alpha);
        if (!Mathf.Approximately(m_nodeCanvasGroup.alpha, alpha))
            m_nodeCanvasGroup.alpha = alpha;
    }

    private void RefreshTexts()
    {
        if (m_titleText != null)
            m_titleText.text = string.IsNullOrEmpty(m_record?.title) ? "Undefined" : m_record.title;
        if (m_playerNameText != null)
        {
            m_playerNameText.text = string.IsNullOrEmpty(m_record?.ownerDisplayName)
                ? "Player"
                : m_record.ownerDisplayName;
        }
        if (m_sizeText != null)
            m_sizeText.text = m_record == null ? string.Empty : $"{m_record.size}x{m_record.size}";
    }

    private void RefreshStatusState(bool animateLike = false)
    {
        bool hasRecord = m_record != null && !string.IsNullOrEmpty(m_record.publicLevelId);
        bool liked = hasRecord && m_record.likedByCurrentPlayer;
        RefreshLoveState(liked, animateLike);
        if (m_loveCountText != null)
            m_loveCountText.text = hasRecord ? Mathf.Max(0, m_record.likeCount).ToString() : "0";

        RefreshButtonState();
    }

    private void RefreshPreview()
    {
        ClearPreviewAssets();
        if (m_record == null || m_picture == null)
            return;

        MPCustomLevelInfo levelInfo = MPCustomLevelPublishManager.Instance.ToLocalPlayableLevel(m_record);
        if (levelInfo == null)
            return;

        m_previewTexture = MPUser.instance.LoadCustomLevelImageTexture(levelInfo);
        if (m_previewTexture == null)
            return;

        m_previewTexture.filterMode = FilterMode.Point;
        m_previewTexture.wrapMode = TextureWrapMode.Clamp;
        m_previewSprite = Sprite.Create(
            m_previewTexture,
            new Rect(0, 0, m_previewTexture.width, m_previewTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        m_picture.sprite = m_previewSprite;
    }

    private void OnLoveClick()
    {
        if (!CanStartOperation())
            return;

        bool targetLiked = !m_record.likedByCurrentPlayer;
        try
        {
            // 点赞任务由 Manager 按关卡串行合并。这里不等待旧请求，也不在回调中二次写 UI，
            // 用户连续点击时始终直接提交最新目标状态。
            _ = MPCustomLevelPublishManager.Instance.LikeAsync(
                m_record,
                targetLiked);
            RefreshStatusState(targetLiked);
        }
        catch (Exception exception)
        {
            RefreshStatusState();
            Debug.LogError(
                $"[MPCommunityLevelItem] 修改点赞状态异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
        }
    }

    private async void OnPlayClick()
    {
        if (!CanStartOperation())
            return;

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
                return;
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
    /// 将服务端最新状态写回列表持有的记录实例，保证对象池刷新后状态不回退。
    /// </summary>
    private void ApplyRecord(MPCustomLevelPublicRecord latestRecord)
    {
        if (m_record == null || latestRecord == null)
            return;

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
        RefreshTexts();
        RefreshStatusState();
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
            return;

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
            m_loveBtn.interactable = hasRecord && !m_isOperationRunning;
        if (m_playBtn != null)
            m_playBtn.interactable = hasRecord && !m_isOperationRunning;
    }

    private void CancelOperation()
    {
        if (m_operationCancellation == null)
            return;

        m_operationCancellation.Cancel();
        m_operationCancellation.Dispose();
        m_operationCancellation = null;
        m_isOperationRunning = false;
    }

    private void ClearPreviewAssets()
    {
        if (m_picture != null)
            m_picture.sprite = null;
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

    private void OnEnable()
    {
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged -= OnCommunityLikeStateChanged;
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged += OnCommunityLikeStateChanged;
    }

    private void OnDisable()
    {
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged -= OnCommunityLikeStateChanged;
        // LoopGridView 回收 Item 时立即取消操作，避免不可见 Item 完成异步回写或打开页面。
        CancelOperation();
        KillLoveTween(true);
        SetNodeAlpha(1f);
    }

    private void OnDestroy()
    {
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged -= OnCommunityLikeStateChanged;
        if (m_loveBtn != null)
            m_loveBtn.onClick.RemoveListener(OnLoveClick);
        if (m_playBtn != null)
            m_playBtn.onClick.RemoveListener(OnPlayClick);

        CancelOperation();
        KillLoveTween(false);
        ClearPreviewAssets();
    }

    private void OnCommunityLikeStateChanged(
        MPCustomLevelPublicRecord record,
        bool _)
    {
        if (m_record == null ||
            record == null ||
            m_record.publicLevelId != record.publicLevelId)
        {
            return;
        }

        RefreshStatusState();
    }

    private void RefreshLoveState(bool liked, bool animateLike)
    {
        if (m_loveOpen == null)
            return;

        if (!liked)
        {
            KillLoveTween(true);
            if (m_loveOpen.gameObject.activeSelf)
                m_loveOpen.gameObject.SetActive(false);
            return;
        }

        bool wasActive = m_loveOpen.gameObject.activeSelf;
        if (!wasActive)
        {
            m_loveOpen.localScale = m_loveOpenScale;
            m_loveOpen.gameObject.SetActive(true);
        }

        if (!animateLike)
            return;

        KillLoveTween(false);
        m_loveOpen.localScale = Vector3.zero;
        Tween tween = m_loveOpen
            .DOScale(m_loveOpenScale, LOVE_SCALE_DURATION)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject);
        m_loveTween = tween;
        tween.OnComplete(() =>
        {
            if (m_loveTween == tween)
                m_loveTween = null;
        });
        tween.OnKill(() =>
        {
            if (m_loveTween == tween)
                m_loveTween = null;
        });
    }

    private void KillLoveTween(bool resetScale)
    {
        Tween tween = m_loveTween;
        m_loveTween = null;
        if (tween != null && tween.IsActive())
            tween.Kill();
        if (resetScale && m_loveOpen != null)
            m_loveOpen.localScale = m_loveOpenScale;
    }

    private readonly struct CommunityItemLayout
    {
        public readonly float verticalOffset;
        public readonly float rotationAngle;

        public CommunityItemLayout(float verticalOffset, float rotationAngle)
        {
            this.verticalOffset = verticalOffset;
            this.rotationAngle = rotationAngle;
        }
    }
}

using DG.Tweening;
using HQ.UIManager;
using System;
using System.IO;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPCustomLevelItem : MonoBehaviour
{
    private const float EDITOR_BUTTON_SCALE_DURATION = 0.2f;

    /// <summary>
    /// 通关后显示的图片。
    /// </summary>
    private Image m_pixel;

    /// <summary>
    /// 关卡开始按钮。
    /// </summary>
    private Button m_playBtn;

    /// <summary>
    /// 关卡上传按钮。
    /// </summary>
    private Button m_uploadBtn;

    /// <summary>
    /// 关卡上传按钮文本，用于显示 Upload、Revoke 和等待状态。
    /// </summary>
    private TMP_Text m_uploadText;

    /// <summary>
    /// 关卡删除按钮。
    /// </summary>
    private Button m_deleteBtn;

    /// <summary>
    /// 重新编辑当前未上传关卡的按钮。
    /// </summary>
    private Button m_editorBtn;

    /// <summary>
    /// 编辑按钮预制体中配置的显示缩放。
    /// </summary>
    private Vector3 m_editorVisibleScale = Vector3.one;

    /// <summary>
    /// 编辑按钮显示/隐藏动画。
    /// </summary>
    private Tween m_editorButtonTween;

    private bool m_editorStateInitialized;
    private bool m_editorShouldBeVisible;

    /// <summary>
    /// 关卡标题文本。
    /// </summary>
    private TMP_Text m_nameText;

    /// <summary>
    /// 关卡尺寸文本。
    /// </summary>
    private TMP_Text m_sizeText;

    /// <summary>
    /// 关卡最近一次创建或编辑完成时间。
    /// </summary>
    private TMP_Text m_updateTimeText;

    /// <summary>
    /// 公开关卡服务端点赞数量。
    /// </summary>
    private TMP_Text m_likedCountText;

    /// <summary>
    /// 公开关卡服务端试玩次数。
    /// </summary>
    private TMP_Text m_lookCountText;

    /// <summary>
    /// 当前自定义关卡数据。
    /// </summary>
    private MPCustomLevelInfo m_data;

    /// <summary>
    /// 当前自定义关卡索引。
    /// </summary>
    private int m_index;

    /// <summary>
    /// 关卡列表刷新回调。
    /// </summary>
    private Action m_refresh;

    /// <summary>
    /// 请求主页编辑指定自定义关卡的回调。
    /// </summary>
    private Action<MPCustomLevelInfo> m_edit;

    /// <summary>
    /// 当前列表像素预览使用的运行时贴图。
    /// </summary>
    private Texture2D m_pixelTexture;

    /// <summary>
    /// 当前列表像素预览使用的运行时精灵。
    /// </summary>
    private Sprite m_pixelSprite;

    /// <summary>
    /// 上传或撤销操作的取消源，列表项销毁时会取消异步请求后的 UI 回写。
    /// </summary>
    private CancellationTokenSource m_publishCancellation;

    /// <summary>
    /// 当前是否正在执行上传或撤销操作，避免玩家连续点击触发重复请求。
    /// </summary>
    private bool m_isPublishActionRunning;

    /// <summary>
    /// 初始化自定义关卡列表项。
    /// </summary>
    public void Initialize(Action refresh, Action<MPCustomLevelInfo> edit)
    {
        m_refresh = refresh;
        m_edit = edit;

        m_pixel = transform.Find("Completed/Pixel").GetComponent<Image>();
        m_playBtn = transform.Find("PlayBtn").GetComponent<Button>();
        m_uploadBtn = transform.Find("UploadBtn").GetComponent<Button>();
        m_deleteBtn = transform.Find("DeleteBtn").GetComponent<Button>();
        m_editorBtn = transform.Find("EditorBtn")?.GetComponent<Button>();
        m_uploadText = transform.Find("UploadBtn/Text")?.GetComponent<TMP_Text>();
        m_nameText = transform.Find("Name").GetComponent<TMP_Text>();
        m_sizeText = transform.Find("Size").GetComponent<TMP_Text>();
        m_updateTimeText = transform.Find("UpdateTime")?.GetComponent<TMP_Text>();
        m_likedCountText = transform.Find("LikedCount")?.GetComponent<TMP_Text>();
        m_lookCountText = transform.Find("LookCount")?.GetComponent<TMP_Text>();

        if (m_editorBtn != null)
        {
            m_editorVisibleScale = m_editorBtn.transform.localScale;
            if (m_editorVisibleScale.sqrMagnitude <= Mathf.Epsilon)
                m_editorVisibleScale = Vector3.one;
        }

        m_playBtn.onClick.AddListener(OnLevelClick);
        m_uploadBtn.onClick.AddListener(OnUploadClick);
        m_deleteBtn.onClick.AddListener(OnDeleteClick);
        if (m_editorBtn != null)
            m_editorBtn.onClick.AddListener(OnEditorClick);

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishStateChanged += OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged += OnPublishOperationChanged;
    }


    /// <summary>
    /// 刷新自定义关卡列表项显示。
    /// </summary>
    public void Refresh(MPCustomLevelInfo data, int index, int cachedLikeCount, int cachedPlayCount)
    {
        m_data = data;
        m_index = index;

        m_nameText.text = string.IsNullOrEmpty(m_data.Title) ? MPUser.instance.GetDefaultCustomLevelTitle() : m_data.Title;
        m_sizeText.text = $"{m_data.Size}x{m_data.Size}";
        RefreshUpdateTime();
        RefreshStatistics(cachedLikeCount, cachedPlayCount);
        RefreshCustomLevelPixel();
        RefreshUploadButtonState();
    }

    /// <summary>
    /// 使用本地持久化的最后编辑时间刷新日期。旧开发数据缺少时间时使用像素文件时间兜底。
    /// </summary>
    private void RefreshUpdateTime()
    {
        if (m_updateTimeText == null)
            return;

        long updateTicks = m_data == null ? 0 : m_data.UpdatedAtUtcTicks;
        if (updateTicks <= 0 && m_data != null)
        {
            try
            {
                string imagePath = MPUser.instance.GetCustomLevelImagePath(m_data.ID);
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    updateTicks = File.GetLastWriteTimeUtc(imagePath).Ticks;
            }
            catch (Exception)
            {
                updateTicks = 0;
            }
        }

        m_updateTimeText.text = FormatUpdateTime(updateTicks);
    }

    private static string FormatUpdateTime(long utcTicks)
    {
        if (utcTicks <= 0)
            return "--";

        try
        {
            DateTime utcTime = new DateTime(utcTicks, DateTimeKind.Utc);
            return utcTime.ToLocalTime().ToString("yyyy.MM.dd");
        }
        catch (ArgumentOutOfRangeException)
        {
            return "--";
        }
    }

    /// <summary>
    /// 显示页面打开时冻结的点赞和试玩缓存。本次页面生命周期内不接收后台同步结果。
    /// </summary>
    private void RefreshStatistics(int cachedLikeCount, int cachedPlayCount)
    {
        if (m_likedCountText != null)
            m_likedCountText.text = Mathf.Max(0, cachedLikeCount).ToString();
        if (m_lookCountText != null)
            m_lookCountText.text = Mathf.Max(0, cachedPlayCount).ToString();
    }


    /// <summary>
    /// 使用最小尺寸像素数据图刷新自定义关卡列表预览。
    /// </summary>
    private void RefreshCustomLevelPixel()
    {
        ClearCustomLevelPixelAsset();

        if (m_data == null || m_pixel == null)
            return;

        m_pixelTexture = MPUser.instance.LoadCustomLevelImageTexture(m_data);
        if (m_pixelTexture == null)
        {
            m_pixel.sprite = null;
            return;
        }

        m_pixelTexture.filterMode = FilterMode.Point;
        m_pixelTexture.wrapMode = TextureWrapMode.Clamp;
        m_pixelSprite = Sprite.Create(
            m_pixelTexture,
            new Rect(0, 0, m_pixelTexture.width, m_pixelTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        m_pixel.sprite = m_pixelSprite;
    }


    /// <summary>
    /// 清理列表项运行时创建的像素预览资源。
    /// </summary>
    private void ClearCustomLevelPixelAsset()
    {
        if (m_pixelSprite != null)
        {
            Destroy(m_pixelSprite);
            m_pixelSprite = null;
        }

        if (m_pixelTexture != null)
        {
            Destroy(m_pixelTexture);
            m_pixelTexture = null;
        }
    }


    /// <summary>
    /// 销毁列表项时移除按钮事件并释放运行时图标资源。
    /// </summary>
    private void OnDestroy()
    {
        if (m_playBtn != null)
        {
            m_playBtn.onClick.RemoveListener(OnLevelClick);
        }

        if (m_uploadBtn != null)
        {
            m_uploadBtn.onClick.RemoveListener(OnUploadClick);
        }

        if (m_deleteBtn != null)
        {
            m_deleteBtn.onClick.RemoveListener(OnDeleteClick);
        }

        if (m_editorBtn != null)
        {
            m_editorBtn.onClick.RemoveListener(OnEditorClick);
        }

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
        m_editorButtonTween?.Kill();
        m_editorButtonTween = null;
        CancelPublishOperation();
        ClearCustomLevelPixelAsset();
    }

    /// <summary>
    /// 上传自定义关卡按钮点击回调。
    /// </summary>
    private async void OnUploadClick()
    {
        if (m_data == null || m_isPublishActionRunning)
        {
            return;
        }

        m_isPublishActionRunning = true;
        RefreshUploadButtonState();
        CancelPublishOperation();
        m_publishCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = m_publishCancellation;

        try
        {
            MPCustomLevelPublishLocalState state = MPCustomLevelPublishManager.Instance.GetLocalState(m_data.ID);
            if (state != null && state.IsPublished && !string.IsNullOrEmpty(state.publicLevelId))
            {
                MPCustomLevelRevokeResult revokeResult = await MPCustomLevelPublishManager.Instance.RevokeLocalLevelAsync(m_data, cancellation.Token);
                if (revokeResult == null || !revokeResult.success)
                {
                    Debug.LogWarning($"[MPCustomLevelItem] 撤销公开关卡失败：{revokeResult?.message}");
                }
                else
                {
                    Debug.Log($"[MPCustomLevelItem] 已撤销公开关卡：{revokeResult.publicLevelId}");
                }
            }
            else
            {
                MPCustomLevelPublishResult publishResult = await MPCustomLevelPublishManager.Instance.PublishAsync(m_data, cancellation.Token);
                if (publishResult == null || !publishResult.success)
                {
                    Debug.LogWarning($"[MPCustomLevelItem] 上传公开关卡失败：{publishResult?.message}");
                }
                else
                {
                    Debug.Log($"[MPCustomLevelItem] 已上传公开关卡：{publishResult.publicLevelId}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 列表项销毁或刷新时取消异步回调，不需要额外提示。
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPCustomLevelItem] 公开关卡上传/撤销异常：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
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
                RefreshUploadButtonState();
            }
        }
    }

    /// <summary>
    /// 删除当前自定义关卡并刷新列表。
    /// </summary>
    private async void OnDeleteClick()
    {
        if (m_data == null || m_isPublishActionRunning)
        {
            return;
        }

        MPCustomLevelInfo levelInfo = m_data;
        m_isPublishActionRunning = true;
        RefreshUploadButtonState();
        CancelPublishOperation();
        m_publishCancellation = new CancellationTokenSource();
        CancellationTokenSource cancellation = m_publishCancellation;
        bool canDeleteLocalLevel = false;

        try
        {
            MPCustomLevelPublishLocalState state = MPCustomLevelPublishManager.Instance.GetLocalState(levelInfo.ID);
            if (state != null && state.IsPublished && !string.IsNullOrEmpty(state.publicLevelId))
            {
                MPCustomLevelRevokeResult revokeResult = await MPCustomLevelPublishManager.Instance.RevokeLocalLevelAsync(
                    levelInfo,
                    cancellation.Token);
                if (revokeResult == null || !revokeResult.success)
                {
                    Debug.LogWarning($"[MPCustomLevelItem] 删除前撤销公开关卡失败，已保留本地关卡：{revokeResult?.message}");
                    return;
                }
            }

            canDeleteLocalLevel = true;
        }
        catch (OperationCanceledException)
        {
            // 列表项销毁时终止后续本地删除。
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPCustomLevelItem] 删除前撤销公开关卡异常，已保留本地关卡：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
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
                RefreshUploadButtonState();
            }
        }

        if (!canDeleteLocalLevel || this == null)
        {
            return;
        }

        MPUser.instance.DeleteCustomLevel(levelInfo.ID);
        m_refresh?.Invoke();
    }

    /// <summary>
    /// 打开选中的自定义关卡游戏页面。
    /// </summary>
    private void OnLevelClick()
    {
        MPGameViewUIMsgData data = new MPGameViewUIMsgData()
        {
            customLevelInfo = m_data,
            isCustomLevel = true,
            index = m_index,
            refresh = m_refresh,
        };
        MPTransitionView.OpenWindow<MPGameView>(data, GetComponentInParent<AWindow>());
    }

    /// <summary>
    /// 请求主页加载当前未上传关卡进行编辑。
    /// </summary>
    private void OnEditorClick()
    {
        if (m_data == null || m_edit == null || m_isPublishActionRunning)
            return;

        bool isPending = MPCustomLevelPublishManager.Instance.IsPublishPending(m_data.ID);
        bool isPublished = MPCustomLevelPublishManager.Instance.IsLocalLevelPublished(m_data.ID);
        if (isPending || isPublished)
            return;

        m_edit.Invoke(m_data);
    }

    /// <summary>
    /// 发布状态变化时刷新当前列表项按钮显示。
    /// </summary>
    private void OnPublishStateChanged(MPCustomLevelPublishLocalState state)
    {
        if (state == null || m_data == null || state.sourceLocalLevelId != m_data.ID)
        {
            return;
        }

        RefreshUploadButtonState();
        if (!state.IsPublished)
            RefreshStatistics(0, 0);
    }

    /// <summary>
    /// 跨页面上传任务状态变化时刷新对应关卡按钮。
    /// </summary>
    private void OnPublishOperationChanged(string sourceLocalLevelId)
    {
        if (m_data == null || sourceLocalLevelId != m_data.ID)
        {
            return;
        }

        RefreshUploadButtonState();
    }

    /// <summary>
    /// 刷新上传按钮的交互和文本状态。
    /// </summary>
    private void RefreshUploadButtonState()
    {
        bool canUseCloudPublish = MPLoginManager.Instance != null && MPLoginManager.Instance.IsLoggedIn;
        bool isPublishPending = m_data != null && MPCustomLevelPublishManager.Instance.IsPublishPending(m_data.ID);
        bool isBusy = m_isPublishActionRunning || isPublishPending;
        MPCustomLevelPublishLocalState state = m_data == null
            ? null
            : MPCustomLevelPublishManager.Instance.GetLocalState(m_data.ID);
        bool isPublished = state != null && state.IsPublished;

        if (m_uploadBtn != null)
        {
            m_uploadBtn.interactable = !isBusy && canUseCloudPublish;
        }

        if (m_deleteBtn != null)
        {
            m_deleteBtn.interactable = !isBusy;
        }

        RefreshEditorButtonState(m_data != null && m_edit != null && !isBusy && !isPublished);

        if (m_uploadText == null)
        {
            return;
        }

        if (isBusy)
        {
            m_uploadText.text = "...";
            return;
        }

        m_uploadText.text = isPublished ? "Revoke" : "Upload";
    }

    /// <summary>
    /// 使用缩放动画切换编辑按钮。首次刷新直接应用最终状态，避免已上传关卡打开列表时闪现。
    /// </summary>
    private void RefreshEditorButtonState(bool shouldShow)
    {
        if (m_editorBtn == null)
            return;

        if (!m_editorStateInitialized)
        {
            m_editorStateInitialized = true;
            m_editorShouldBeVisible = shouldShow;
            m_editorBtn.transform.localScale = shouldShow ? m_editorVisibleScale : Vector3.zero;
            m_editorBtn.gameObject.SetActive(shouldShow);
            m_editorBtn.interactable = shouldShow;
            return;
        }

        if (m_editorShouldBeVisible == shouldShow)
            return;

        m_editorShouldBeVisible = shouldShow;
        m_editorButtonTween?.Kill();
        m_editorButtonTween = null;

        Transform editorTransform = m_editorBtn.transform;
        m_editorBtn.interactable = false;

        if (!editorTransform.gameObject.activeSelf)
            editorTransform.gameObject.SetActive(true);

        if (shouldShow)
        {
            editorTransform.localScale = Vector3.zero;
            m_editorButtonTween = editorTransform
                .DOScale(m_editorVisibleScale, EDITOR_BUTTON_SCALE_DURATION)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    m_editorButtonTween = null;
                    if (m_editorShouldBeVisible && m_editorBtn != null)
                        m_editorBtn.interactable = true;
                });
            return;
        }

        m_editorButtonTween = editorTransform
            .DOScale(Vector3.zero, EDITOR_BUTTON_SCALE_DURATION)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                m_editorButtonTween = null;
                if (!m_editorShouldBeVisible && editorTransform != null)
                    editorTransform.gameObject.SetActive(false);
            });
    }

    /// <summary>
    /// 取消当前列表项正在等待的上传或撤销操作。
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

}

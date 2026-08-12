using HQ.UIManager;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPCustomLevelItem : MonoBehaviour
{
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
    /// 关卡标题文本。
    /// </summary>
    private TMP_Text m_nameText;

    /// <summary>
    /// 关卡尺寸文本。
    /// </summary>
    private TMP_Text m_sizeText;

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
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;

        m_pixel = transform.Find("Completed/Pixel").GetComponent<Image>();
        m_playBtn = transform.Find("PlayBtn").GetComponent<Button>();
        m_uploadBtn = transform.Find("UploadBtn").GetComponent<Button>();
        m_deleteBtn = transform.Find("DeleteBtn").GetComponent<Button>();
        m_uploadText = transform.Find("UploadBtn/Text")?.GetComponent<TMP_Text>();
        m_nameText = transform.Find("Name").GetComponent<TMP_Text>();
        m_sizeText = transform.Find("Size").GetComponent<TMP_Text>();

        m_playBtn.onClick.AddListener(OnLevelClick);
        m_uploadBtn.onClick.AddListener(OnUploadClick);
        m_deleteBtn.onClick.AddListener(OnDeleteClick);

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishStateChanged += OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged += OnPublishOperationChanged;
    }


    /// <summary>
    /// 刷新自定义关卡列表项显示。
    /// </summary>
    public void Refresh(MPCustomLevelInfo data, int index)
    {
        m_data = data;
        m_index = index;

        m_nameText.text = string.IsNullOrEmpty(m_data.Title) ? MPUser.instance.GetDefaultCustomLevelTitle() : m_data.Title;
        m_sizeText.text = $"{m_data.Size}x{m_data.Size}";
        RefreshCustomLevelPixel();
        RefreshUploadButtonState();
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

        MPCustomLevelPublishManager.Instance.PublishStateChanged -= OnPublishStateChanged;
        MPCustomLevelPublishManager.Instance.PublishOperationChanged -= OnPublishOperationChanged;
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
    /// 发布状态变化时刷新当前列表项按钮显示。
    /// </summary>
    private void OnPublishStateChanged(MPCustomLevelPublishLocalState state)
    {
        if (state == null || m_data == null || state.sourceLocalLevelId != m_data.ID)
        {
            return;
        }

        RefreshUploadButtonState();
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
        if (m_uploadBtn == null)
        {
            return;
        }

        bool canUseCloudPublish = MPLoginManager.Instance != null && MPLoginManager.Instance.IsLoggedIn;
        bool isPublishPending = m_data != null && MPCustomLevelPublishManager.Instance.IsPublishPending(m_data.ID);
        bool isBusy = m_isPublishActionRunning || isPublishPending;
        m_uploadBtn.interactable = !isBusy && canUseCloudPublish;
        if (m_deleteBtn != null)
        {
            m_deleteBtn.interactable = !isBusy;
        }

        if (m_uploadText == null)
        {
            return;
        }

        if (isBusy)
        {
            m_uploadText.text = "...";
            return;
        }

        MPCustomLevelPublishLocalState state = m_data == null ? null : MPCustomLevelPublishManager.Instance.GetLocalState(m_data.ID);
        m_uploadText.text = state != null && state.IsPublished ? "Revoke" : "Upload";
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


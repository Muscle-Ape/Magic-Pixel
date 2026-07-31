using HQ.UIManager;
using System;
using System.IO;
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
    /// 当前列表图标使用的运行时贴图。
    /// </summary>
    private Texture2D m_iconTexture;

    /// <summary>
    /// 当前列表图标使用的运行时精灵。
    /// </summary>
    private Sprite m_iconSprite;

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
        RefreshCustomLevelIcon();
        RefreshUploadButtonState();
    }


    /// <summary>
    /// 刷新自定义关卡列表项的图标图片。
    /// </summary>
    private void RefreshCustomLevelIcon()
    {
        ClearCustomLevelIconAsset();

        if (m_data == null || m_pixel == null)
            return;

        string path = MPUser.instance.GetCustomLevelIconImagePath(m_data.ID);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            m_pixel.sprite = null;
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        m_iconTexture = new Texture2D(200, 200, TextureFormat.RGBA32, false);
        if (!m_iconTexture.LoadImage(bytes))
        {
            ClearCustomLevelIconAsset();
            m_pixel.sprite = null;
            return;
        }

        m_iconTexture.filterMode = FilterMode.Point;
        m_iconTexture.wrapMode = TextureWrapMode.Clamp;
        m_iconSprite = Sprite.Create(m_iconTexture, new Rect(0, 0, m_iconTexture.width, m_iconTexture.height), new Vector2(0.5f, 0.5f), 100f);
        m_pixel.sprite = m_iconSprite;
    }


    /// <summary>
    /// 清理列表项运行时创建的图标资源。
    /// </summary>
    private void ClearCustomLevelIconAsset()
    {
        if (m_iconSprite != null)
        {
            Destroy(m_iconSprite);
            m_iconSprite = null;
        }

        if (m_iconTexture != null)
        {
            Destroy(m_iconTexture);
            m_iconTexture = null;
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
        CancelPublishOperation();
        ClearCustomLevelIconAsset();
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
    private void OnDeleteClick()
    {
        if (m_data == null)
            return;

        MPUser.instance.DeleteCustomLevel(m_data.ID);
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
    /// 刷新上传按钮的交互和文本状态。
    /// </summary>
    private void RefreshUploadButtonState()
    {
        if (m_uploadBtn == null)
        {
            return;
        }

        bool canUseCloudPublish = MPLoginManager.Instance != null && MPLoginManager.Instance.IsLoggedIn;
        m_uploadBtn.interactable = !m_isPublishActionRunning && canUseCloudPublish;

        if (m_uploadText == null)
        {
            return;
        }

        if (m_isPublishActionRunning)
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






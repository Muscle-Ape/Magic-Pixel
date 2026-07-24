using HQ.UIManager;
using System;
using System.IO;
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
    /// 初始化自定义关卡列表项。
    /// </summary>
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;

        m_pixel = transform.Find("Completed/Pixel").GetComponent<Image>();
        m_playBtn = transform.Find("PlayBtn").GetComponent<Button>();
        m_uploadBtn = transform.Find("UploadBtn").GetComponent<Button>();
        m_deleteBtn = transform.Find("DeleteBtn").GetComponent<Button>();
        m_nameText = transform.Find("Name").GetComponent<TMP_Text>();
        m_sizeText = transform.Find("Size").GetComponent<TMP_Text>();

        m_playBtn.onClick.AddListener(OnLevelClick);
        m_uploadBtn.onClick.AddListener(OnUploadClick);
        m_deleteBtn.onClick.AddListener(OnDeleteClick);
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

        ClearCustomLevelIconAsset();
    }

    /// <summary>
    /// 上传自定义关卡按钮点击回调。
    /// </summary>
    private void OnUploadClick()
    {
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
}






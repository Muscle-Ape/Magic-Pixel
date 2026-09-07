using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>大图关卡列表卡片；由循环列表复用，按存档刷新锁定、可游玩和完成状态。</summary>
public class MPLargeImageLevelItem : MonoBehaviour
{
    private static readonly Color s_lockedTextColor = new Color32(0x3D, 0x59, 0x72, 0xFF);

    private Image m_frame;
    private Button m_levelBtn;
    private Image m_buttonImage;
    private TMP_Text m_buttonText;
    private GameObject m_statusLock;
    private GameObject m_statusUnlock;
    private GameObject m_statusCompleted;
    private Image m_completedPixel;
    private TMP_Text m_nameText;
    private TMP_Text m_sizeText;
    private GameObject m_award;
    private TMP_Text m_awardCount;
    private GameObject m_stars;
    private GameObject[] m_starObj;
    private MPLargeImageBlockInfo m_data;
    private int m_index;
    private Action m_refresh;
    private bool m_initialized;
    private string m_previewLevelId;

    /// <summary>节点只查找一次；重复绑定时只更新回调，不重复注册按钮事件。</summary>
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;
        if (m_initialized)
            return;

        m_frame = FindComponent<Image>("Frame");
        m_levelBtn = FindComponent<Button>("Btn");
        m_buttonImage = FindComponent<Image>("Btn");
        m_buttonText = FindComponent<TMP_Text>("Btn/Text");
        m_statusLock = FindGameObject("Status/Lock");
        m_statusUnlock = FindGameObject("Status/Unlock");
        m_statusCompleted = FindGameObject("Status/Completed");
        m_completedPixel = FindComponent<Image>("Status/Completed/Mask/Pixel");
        m_nameText = FindComponent<TMP_Text>("Name");
        m_sizeText = FindComponent<TMP_Text>("Size");
        m_award = FindGameObject("Award");
        m_awardCount = FindComponent<TMP_Text>("Award/Count");
        m_stars = FindGameObject("Stars");
        CacheStarNodes();

        if (m_levelBtn != null)
            m_levelBtn.onClick.AddListener(OnLevelClick);
        m_initialized = true;
    }

    /// <summary>更新同一关卡时保留已加载图片；绑定其他关卡时先释放旧预览，避免回收后串图。</summary>
    public void Refresh(MPLargeImageBlockInfo data, int index)
    {
        if (!m_initialized)
            Initialize(m_refresh);

        bool dataChanged = !ReferenceEquals(m_data, data);
        if (dataChanged)
            ClearCompletedPixel();
        m_data = data;
        m_index = index;
        bool valid = data != null && !string.IsNullOrWhiteSpace(data.ID);
        if (m_levelBtn != null)
            m_levelBtn.interactable = valid;
        if (!valid)
        {
            SetStatusImage(null);
            SetActive(m_award, false);
            ApplySprite(m_frame, null);
            ApplySprite(m_buttonImage, null);
            RefreshStars(false, 0);
            ClearCompletedPixel();
            if (m_nameText != null) m_nameText.text = string.Empty;
            if (m_sizeText != null) m_sizeText.text = string.Empty;
            if (m_awardCount != null) m_awardCount.text = string.Empty;
            if (m_buttonText != null) m_buttonText.text = string.Empty;
            return;
        }

        if (m_nameText != null)
            m_nameText.text = string.IsNullOrWhiteSpace(data.Name) ? data.ID : data.Name;
        if (dataChanged && m_sizeText != null)
        {
            try
            {
                Vector2Int size = MPLargeImageLevelModel.GetLevelSize(data);
                m_sizeText.text = $"{size.x}×{size.y}";
            }
            catch (Exception exception)
            {
                m_sizeText.text = string.Empty;
                Debug.LogWarning($"[MPLargeImageLevelItem] 读取关卡尺寸失败：{data.ID}，{exception.Message}");
            }
        }

        // 保留 AwardText 的预制体文案，仅更新金币数量。
        if (m_awardCount != null)
            m_awardCount.text = $"<b>{Mathf.Max(0, data.AwardCoin)}</b> coins";

        MPLargeImageLevelState state = MPLargeImageLevelModel.GetLevelState(data);
        // 每次状态刷新都同步文字颜色，避免解锁或列表复用后残留旧颜色。
        Color textColor = state == MPLargeImageLevelState.Locked ? s_lockedTextColor : Color.white;
        if (m_nameText != null)
            m_nameText.color = textColor;
        if (m_sizeText != null)
            m_sizeText.color = textColor;
        SetActive(m_award, state == MPLargeImageLevelState.Locked);
        switch (state)
        {
            case MPLargeImageLevelState.Locked:
                SetStatusImage(m_statusLock);
                RefreshStyle("large_frame_lock", "large_btn_frame_unlock", "Unlock");
                break;
            case MPLargeImageLevelState.Completed:
                SetStatusImage(m_statusCompleted);
                RefreshStyle("large_frame_completed", "large_btn_frame_replay", "Replay");
                break;
            default:
                SetStatusImage(m_statusUnlock);
                RefreshStyle("large_frame_unlock", "large_btn_frame_play", "Play");
                break;
        }

        bool completed = state == MPLargeImageLevelState.Completed;
        RefreshStars(completed, completed ? MPLargeImageLevelModel.GetLevelStars(data) : 0);
        if (completed)
            RefreshCompletedPixel();
        else
            ClearCompletedPixel();
    }

    /// <summary>样式资源由 Item 持有，同一地址通过 MPLoad 缓存复用，关闭页面时统一释放。</summary>
    private void RefreshStyle(string frameLocation, string buttonLocation, string text)
    {
        ApplySprite(m_frame, LoadSprite(frameLocation, this));
        ApplySprite(m_buttonImage, LoadSprite(buttonLocation, this));
        if (m_buttonText != null)
            m_buttonText.text = text;
    }

    private static Sprite LoadSprite(string location, UnityEngine.Object owner)
    {
        if (owner == null)
            return null;
        try
        {
            return MPLoad.Load<Sprite>(location, owner);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPLargeImageLevelItem] 图片加载失败：{location}，{exception.Message}");
            return null;
        }
    }

    private static void ApplySprite(Image target, Sprite sprite)
    {
        if (target == null)
            return;
        target.sprite = sprite;
        // 资源缺失时不显示白块，也不沿用循环列表上一关的图片。
        target.enabled = sprite != null;
    }

    /// <summary>完成预览单独使用 Pixel 作为资源持有者，以便换关时只释放预览、不重复加载卡片样式。</summary>
    private void RefreshCompletedPixel()
    {
        if (m_completedPixel == null || (m_previewLevelId == m_data.ID && m_completedPixel.sprite != null))
            return;
        ClearCompletedPixel();
        Sprite sprite = LoadSprite("icon_" + m_data.ID, m_completedPixel);
        ApplySprite(m_completedPixel, sprite);
        m_completedPixel.color = Color.white;
        m_completedPixel.preserveAspect = true;
        if (sprite != null)
            m_previewLevelId = m_data.ID;
    }

    private void ClearCompletedPixel()
    {
        if (m_completedPixel != null)
        {
            m_completedPixel.sprite = null;
            m_completedPixel.enabled = false;
            MPLoad.ReleaseAll(m_completedPixel);
        }
        m_previewLevelId = null;
    }

    private void SetStatusImage(GameObject target)
    {
        SetActive(m_statusLock, target != null && m_statusLock == target);
        SetActive(m_statusUnlock, target != null && m_statusUnlock == target);
        SetActive(m_statusCompleted, target != null && m_statusCompleted == target);
    }

    private void RefreshStars(bool completed, int stars)
    {
        SetActive(m_stars, completed);
        if (m_starObj == null)
            return;
        stars = Mathf.Clamp(stars, 0, m_starObj.Length);
        for (int i = 0; i < m_starObj.Length; i++)
            SetActive(m_starObj[i], completed && i < stars);
    }

    private void CacheStarNodes()
    {
        if (m_stars == null)
            return;
        Transform root = m_stars.transform;
        m_starObj = new GameObject[root.childCount];
        for (int i = 0; i < root.childCount; i++)
        {
            Transform light = root.GetChild(i).Find("Light");
            m_starObj[i] = light == null ? null : light.gameObject;
        }
    }

    private GameObject FindGameObject(string path)
    {
        Transform node = transform.Find(path);
        return node == null ? null : node.gameObject;
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform node = transform.Find(path);
        return node == null ? null : node.GetComponent<T>();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void OnLevelClick()
    {
        if (m_data == null || string.IsNullOrWhiteSpace(m_data.ID))
            return;
        AWindow owner = GetComponentInParent<AWindow>();
        if (owner != null && owner.IsDestoried)
            return;
        MPAudioManager.Instance.PlaySound(MPSound.MPSoundClickUI, replay: true);

        // 点击时重新读取状态，不能使用列表刷新前缓存的解锁结果。
        if (MPLargeImageLevelModel.GetLevelState(m_data) == MPLargeImageLevelState.Locked)
        {
            UIManager.Inst.ShowWindow<MPLargeImageLevelUnlockPop>(new MPLargeImageLevelUnlockPopUIMsgData
            {
                levelInfo = m_data,
                index = m_index,
                refresh = m_refresh,
            }, true, UILayer.Top);
            return;
        }

        MPNewGamePop.EnterLargeImageLevel(new MPLargeImageGameViewUIMsgData
        {
            blockInfo = m_data,
            index = m_index,
            refresh = m_refresh,
        }, owner);
    }

    private void OnDisable()
    {
        ClearCompletedPixel();
    }

    /// <summary>主页释放时可提前调用；随后 OnDestroy 再次调用也安全。</summary>
    public void ReleaseResources()
    {
        ClearCompletedPixel();
        ApplySprite(m_frame, null);
        ApplySprite(m_buttonImage, null);
        MPLoad.ReleaseAll(this);
        m_refresh = null;
        m_data = null;
        if (m_levelBtn != null)
            m_levelBtn.interactable = false;
    }

    private void OnDestroy()
    {
        if (m_levelBtn != null)
            m_levelBtn.onClick.RemoveListener(OnLevelClick);
        ReleaseResources();
    }
}

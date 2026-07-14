using HQ.UIManager;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MPLargeImageLevelItem : MonoBehaviour
{
    /// <summary>
    /// 未解锁状态底图节点。
    /// </summary>
    private GameObject m_statusLock;

    /// <summary>
    /// 已解锁未完成状态底图节点。
    /// </summary>
    private GameObject m_statusUnlock;

    /// <summary>
    /// 已完成状态底图节点。
    /// </summary>
    private GameObject m_statusCompleted;

    /// <summary>
    /// 完成状态中展示像素图的图片。
    /// </summary>
    private Image m_completedPixel;

    /// <summary>
    /// 未解锁状态的锁图标节点。
    /// </summary>
    private GameObject m_lockIcon;

    /// <summary>
    /// 未解锁状态的锁定按钮样式节点。
    /// </summary>
    private GameObject m_lock;

    /// <summary>
    /// 已解锁状态的进度条节点。
    /// </summary>
    private GameObject m_progress;

    /// <summary>
    /// 已解锁状态的进度条填充图片。
    /// </summary>
    private Image m_progressFill;

    /// <summary>
    /// 已解锁状态的开始按钮样式节点。
    /// </summary>
    private GameObject m_start;

    /// <summary>
    /// 已完成状态的星星父节点。
    /// </summary>
    private GameObject m_stars;

    /// <summary>
    /// 已完成状态的星星高亮节点数组。
    /// </summary>
    private GameObject[] m_starObj;

    /// <summary>
    /// 已完成状态的完成图标节点。
    /// </summary>
    private GameObject m_completedIcon;

    /// <summary>
    /// 关卡点击按钮。
    /// </summary>
    private Button m_levelBtn;

    /// <summary>
    /// 关卡名称文本。
    /// </summary>
    private TMP_Text m_nameText;

    /// <summary>
    /// 关卡尺寸文本。
    /// </summary>
    private TMP_Text m_sizeText;

    /// <summary>
    /// 金币奖励节点。
    /// </summary>
    private GameObject m_coinAward;

    /// <summary>
    /// 金币奖励数量文本。
    /// </summary>
    private TMP_Text m_coinAwardText;

    /// <summary>
    /// 大图关卡数据。
    /// </summary>
    private MPLargeImageBlockInfo m_data;

    /// <summary>
    /// 大图关卡数据模型。
    /// </summary>

    /// <summary>
    /// 当前关卡下标。
    /// </summary>
    private int m_index;

    /// <summary>
    /// 当前关卡是否已经解锁。
    /// </summary>
    private bool m_isUnlock;

    /// <summary>
    /// 刷新关卡列表的回调。
    /// </summary>
    private Action m_refresh;

    /// <summary>
    /// 初始化并缓存 prefab 中已经配置好的 UI 节点。
    /// </summary>
    public void Initialize(Action refresh)
    {
        m_refresh = refresh;

        m_statusLock = FindGameObject("Status/Lock");
        m_statusUnlock = FindGameObject("Status/Unlock");
        m_statusCompleted = FindGameObject("Status/Completed");
        m_completedPixel = FindComponent<Image>("Status/Completed/Pixel");
        m_lockIcon = FindGameObject("LockIcon");
        m_lock = FindGameObject("Lock");
        m_progress = FindGameObject("Progress");
        m_progressFill = FindComponent<Image>("Progress/Fill");
        m_start = FindGameObject("Start");
        m_stars = FindGameObject("Stars");
        m_completedIcon = FindGameObject("CompletedIcon");
        m_levelBtn = FindComponent<Button>("Btn");
        m_nameText = FindComponent<TMP_Text>("Name");
        m_sizeText = FindComponent<TMP_Text>("Size");
        m_coinAward = FindGameObject("CoinAward");
        m_coinAwardText = FindComponent<TMP_Text>("CoinAward/Count");

        CacheStarNodes();

        if (m_levelBtn != null)
        {
            m_levelBtn.onClick.AddListener(OnLevelClick);
        }
    }

    /// <summary>
    /// 根据关卡数据刷新当前 Item 的显示。
    /// </summary>
    public void Refresh(MPLargeImageBlockInfo data, int index)
    {
        m_data = data;
        m_index = index;

        RefreshAlwaysShowInfo();
        RefreshStateInfo();
    }

    /// <summary>
    /// 刷新所有状态下都需要显示的基础信息。
    /// </summary>
    private void RefreshAlwaysShowInfo()
    {
        if (m_nameText != null)
        {
            m_nameText.text = m_data.Name;
        }

        if (m_sizeText != null)
        {
            Vector2Int size = MPLargeImageLevelModel.GetLevelSize(m_data);
            m_sizeText.text = $"{size.x}x{size.y}";
        }

        SetActive(m_coinAward, true);
        if (m_coinAwardText != null)
        {
            m_coinAwardText.text = m_data.AwardCoin.ToString();
        }
    }

    /// <summary>
    /// 根据模型返回的关卡状态刷新对应 UI。
    /// </summary>
    private void RefreshStateInfo()
    {
        MPLargeImageLevelState state = MPLargeImageLevelModel.GetLevelState(m_data);
        m_isUnlock = state != MPLargeImageLevelState.Locked;

        switch (state)
        {
            case MPLargeImageLevelState.Locked:
                RefreshLockState();
                break;
            case MPLargeImageLevelState.Unlocked:
                RefreshUnlockState();
                break;
            case MPLargeImageLevelState.Completed:
                RefreshCompletedState();
                break;
        }
    }

    /// <summary>
    /// 刷新未解锁状态显示。
    /// </summary>
    private void RefreshLockState()
    {
        SetStatusImage(m_statusLock);
        SetActive(m_lockIcon, true);
        SetActive(m_lock, true);
        SetActive(m_progress, false);
        SetActive(m_start, false);
        RefreshStars(false, 0);
        SetActive(m_completedIcon, false);
    }

    /// <summary>
    /// 刷新已解锁但未完成状态显示。
    /// </summary>
    private void RefreshUnlockState()
    {
        SetStatusImage(m_statusUnlock);
        SetActive(m_lockIcon, false);
        SetActive(m_lock, false);
        SetActive(m_progress, true);
        SetActive(m_start, true);
        RefreshProgress();
        RefreshStars(false, 0);
        SetActive(m_completedIcon, false);
    }

    /// <summary>
    /// 刷新已完成状态显示。
    /// </summary>
    private void RefreshCompletedState()
    {
        SetStatusImage(m_statusCompleted);
        SetActive(m_lockIcon, false);
        SetActive(m_lock, false);
        SetActive(m_progress, false);
        SetActive(m_start, false);
        RefreshStars(true, MPLargeImageLevelModel.GetLevelStars(m_data));
        SetActive(m_completedIcon, true);

        if (m_completedPixel != null)
        {
            m_completedPixel.sprite = MPLoad.Load<Sprite>("icon_" + m_data.ID);
        }
    }

    /// <summary>
    /// 只显示当前状态对应的 Status 底图节点。
    /// </summary>
    /// <param name="target">当前状态需要显示的底图节点。</param>
    private void SetStatusImage(GameObject target)
    {
        SetActive(m_statusLock, m_statusLock == target);
        SetActive(m_statusUnlock, m_statusUnlock == target);
        SetActive(m_statusCompleted, m_statusCompleted == target);
    }

    /// <summary>
    /// 刷新进度条显示。
    /// </summary>
    private void RefreshProgress()
    {
        if (m_progressFill != null)
        {
            m_progressFill.fillAmount = MPLargeImageLevelModel.GetLevelProgress(m_data);
        }
    }

    /// <summary>
    /// 刷新通关星星显示。
    /// </summary>
    /// <param name="isPass">当前关卡是否已经完成。</param>
    /// <param name="stars">需要点亮的星星数量。</param>
    private void RefreshStars(bool isPass, int stars)
    {
        if (m_stars == null || m_starObj == null)
        {
            return;
        }

        m_stars.SetActive(isPass);
        stars = Mathf.Clamp(stars, 0, m_starObj.Length);
        for (int i = 0; i < m_starObj.Length; i++)
        {
            SetActive(m_starObj[i], i < stars);
        }
    }

    /// <summary>
    /// 缓存星星高亮节点。
    /// </summary>
    private void CacheStarNodes()
    {
        Transform starsTransform = transform.Find("Stars");
        if (starsTransform == null)
        {
            return;
        }

        m_starObj = new GameObject[starsTransform.childCount];
        for (int i = 0; i < starsTransform.childCount; i++)
        {
            Transform starTransform = starsTransform.GetChild(i);
            m_starObj[i] = starTransform.childCount > 0 ? starTransform.GetChild(0).gameObject : starTransform.gameObject;
        }
    }

    /// <summary>
    /// 查找指定路径的 GameObject。
    /// </summary>
    /// <param name="path">相对当前 Item 的节点路径。</param>
    /// <returns>找到的 GameObject，未找到时返回 null。</returns>
    private GameObject FindGameObject(string path)
    {
        Transform target = transform.Find(path);
        return target == null ? null : target.gameObject;
    }

    /// <summary>
    /// 查找指定路径上的组件。
    /// </summary>
    /// <param name="path">相对当前 Item 的节点路径。</param>
    /// <typeparam name="T">需要获取的组件类型。</typeparam>
    /// <returns>找到的组件，未找到时返回 null。</returns>
    private T FindComponent<T>(string path) where T : Component
    {
        Transform target = transform.Find(path);
        return target == null ? null : target.GetComponent<T>();
    }

    /// <summary>
    /// 安全设置节点显隐。
    /// </summary>
    /// <param name="target">需要设置的节点。</param>
    /// <param name="active">是否显示。</param>
    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    /// <summary>
    /// 点击已解锁关卡时进入大图游戏界面。
    /// </summary>
    private void OnLevelClick()
    {
        if (!m_isUnlock)
        {
            return;
        }

        MPLargeImageGameViewUIMsgData data = new MPLargeImageGameViewUIMsgData()
        {
            blockInfo = m_data,
            index = m_index,
            refresh = m_refresh,
        };
        UIManager.Inst.ShowWindow<MPLargeImageGameView>(data);
    }
}

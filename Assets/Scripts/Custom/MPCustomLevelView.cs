using HQ.UIManager;
using SuperScrollView;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPCustomLevelView")]
public class MPCustomLevelView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    /// <summary>
    /// 返回按钮。
    /// </summary>
    [TransformPath("View/Head/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 设置按钮。
    /// </summary>
    [TransformPath("View/Head/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 自定义关卡滚动列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopGridView m_loopGrid;

    /// <summary>
    /// 空仓库提示节点。
    /// </summary>
    [TransformPath("View/Center/EmptyTip")]
    private RectTransform m_emptyTip;

    /// <summary>
    /// 空关卡 创建按钮
    /// </summary>
    [TransformPath("View/Center/EmptyTip/CreateBtn")]
    private Button m_createBtn;

    /// <summary>
    /// 自定义关卡数据列表。
    /// </summary>
    private List<MPCustomLevelInfo> m_levelInfos;

    /// <summary>
    /// 页面打开瞬间的点赞缓存快照。
    /// 后台同步完成后不修改该快照，保证本次页面内数字不会跳变。
    /// </summary>
    private readonly Dictionary<string, int> m_likeCountSnapshot =
        new Dictionary<string, int>();

    /// <summary>
    /// 与点赞数量同步冻结的试玩次数快照，列表复用时也不读取后台的新缓存。
    /// </summary>
    private readonly Dictionary<string, int> m_playCountSnapshot =
        new Dictionary<string, int>();

    /// <summary>
    /// 将指定关卡加载到来源编辑器的回调。
    /// </summary>
    private System.Action<MPCustomLevelInfo> m_editAction;

    /// <summary>
    /// 金币数量
    /// </summary>
    [TransformPath("View/Head/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量
    /// </summary>
    [TransformPath("View/Head/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 玩家名称。
    /// </summary>
    [TransformPath("View/Head/PlayerName")]
    private TMP_Text m_playerNameText;

    /// <summary>
    /// 当前最新解锁的主线关卡。
    /// </summary>
    [TransformPath("View/Head/Level/Text")]
    private TMP_Text m_playerLevelText;

    /// <summary>
    /// 主线关卡解锁进度。
    /// </summary>
    [TransformPath("View/Head/Level/Mask/Fill")]
    private Image m_playerLevelFill;

    /// <summary>
    /// 首次 LoadUIMsgData 完成后才允许焦点回调刷新页面。
    /// </summary>
    private bool m_initialized;

    /// <summary>
    /// 加载自定义关卡列表页面数据。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPCustomLevelViewUIMsgData data = uiMsg as MPCustomLevelViewUIMsgData;
        m_editAction = data?.edit;
        m_levelInfos = MPUser.instance.GetCustomLevels();
        CaptureStatisticsSnapshot();
        m_loopGrid.InitGridView(m_levelInfos.Count, GetCustomLevelByRowColumn);
        RefreshEmptyTip();

        RegisterButtons();
        RefreshHead();
        m_initialized = true;

        // 只更新持久化缓存，不通知当前页面；下一次打开页面时才展示新数据。
        _ = MPCustomLevelPublishManager.Instance
            .RefreshPublishedLocalLevelStatsCacheAsync();
    }

    public override void OnFocus(bool focus)
    {
        if (!focus || !m_initialized)
            return;

        RefreshHead();
    }

    /// <summary>
    /// 刷新顶部栏玩家信息、主线进度和资源数量。
    /// </summary>
    private void RefreshHead()
    {
        if (m_coinText != null)
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        if (m_diamondText != null)
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();

        if (m_playerNameText != null)
        {
            string playerName = MPLoginManager.Instance.PlayerName;
            m_playerNameText.text = string.IsNullOrWhiteSpace(playerName)
                ? "Player"
                : playerName;
        }

        int levelCount = MPDataManager.Instance.m_mainLevelModel?.blockInfos?.Count ?? 0;
        int latestLevelIndex = levelCount > 0
            ? Mathf.Clamp(MPUser.instance.GetMainLevlPassIndex(), 0, levelCount - 1)
            : 0;
        if (m_playerLevelText != null)
            m_playerLevelText.text = $"LEVEL {latestLevelIndex + 1}";
        if (m_playerLevelFill != null)
        {
            m_playerLevelFill.fillAmount = levelCount <= 1
                ? 0f
                : latestLevelIndex / (float)(levelCount - 1);
        }
    }

    private void RegisterButtons()
    {
        UnregisterButtons();
        RegisterButton(m_backBtn, OnBackClick);
        RegisterButton(m_settingBtn, OnSettingClick);
        RegisterButton(m_createBtn, OnBackClick);
    }

    private void UnregisterButtons()
    {
        UnregisterButton(m_backBtn, OnBackClick);
        UnregisterButton(m_settingBtn, OnSettingClick);
        UnregisterButton(m_createBtn, OnBackClick);
    }

    private static void RegisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private static void UnregisterButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button != null)
            button.onClick.RemoveListener(callback);
    }

    /// <summary>
    /// 根据索引获取自定义关卡列表项。
    /// </summary>
    private LoopGridViewItem GetCustomLevelByRowColumn(LoopGridView view, int index, int row, int column)
    {
        if (index < 0 || index >= m_levelInfos.Count)
            return null;

        LoopGridViewItem item = m_loopGrid.NewListViewItem("MPCustomLevelItem");
        MPCustomLevelItem level = item.GetComponent<MPCustomLevelItem>();
        if (level == null)
        {
            level = item.gameObject.AddComponent<MPCustomLevelItem>();
        }

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            System.Action<MPCustomLevelInfo> edit = m_editAction == null
                ? null
                : OnEditLevel;
            level.Initialize(RefreshLevels, edit);
        }

        MPCustomLevelInfo levelInfo = m_levelInfos[index];
        int cachedLikeCount = m_likeCountSnapshot.TryGetValue(
            levelInfo.ID,
            out int snapshotLikeCount)
            ? snapshotLikeCount
            : 0;
        int cachedPlayCount = m_playCountSnapshot.TryGetValue(
            levelInfo.ID,
            out int snapshotPlayCount)
            ? snapshotPlayCount
            : 0;
        level.Refresh(levelInfo, index, cachedLikeCount, cachedPlayCount);
        return item;
    }

    /// <summary>
    /// 冻结页面本次打开时的点赞数量和试玩次数。首次没有服务端缓存时使用 0。
    /// </summary>
    private void CaptureStatisticsSnapshot()
    {
        m_likeCountSnapshot.Clear();
        m_playCountSnapshot.Clear();
        if (m_levelInfos == null)
            return;

        for (int i = 0; i < m_levelInfos.Count; i++)
        {
            MPCustomLevelInfo levelInfo = m_levelInfos[i];
            if (levelInfo == null || string.IsNullOrEmpty(levelInfo.ID))
                continue;

            m_likeCountSnapshot[levelInfo.ID] = MPCustomLevelPublishManager.Instance
                .GetCachedLocalLevelLikeCount(levelInfo.ID);
            m_playCountSnapshot[levelInfo.ID] = MPCustomLevelPublishManager.Instance
                .GetCachedLocalLevelPlayCount(levelInfo.ID);
        }
    }

    /// <summary>
    /// 刷新自定义关卡列表。
    /// </summary>
    private void RefreshLevels()
    {
        m_levelInfos = MPUser.instance.GetCustomLevels();
        m_loopGrid.SetListItemCount(m_levelInfos.Count);
        m_loopGrid.RefreshAllShownItem();
        RefreshEmptyTip();
    }

    /// <summary>
    /// 关闭仓库并把选中的关卡交给主页编辑器。
    /// </summary>
    private void OnEditLevel(MPCustomLevelInfo levelInfo)
    {
        if (levelInfo == null || m_editAction == null)
            return;

        m_editAction.Invoke(levelInfo);
        DestroyWindow();
    }

    /// <summary>
    /// 刷新空仓库提示显示。
    /// </summary>
    private void RefreshEmptyTip()
    {
        if (m_emptyTip != null)
        {
            m_emptyTip.gameObject.SetActive(m_levelInfos == null || m_levelInfos.Count == 0);
        }
    }

    /// <summary>
    /// 返回上一页面。
    /// </summary>
    private void OnBackClick()
    {
        DestroyWindow();
    }

    /// <summary>
    /// 设置按钮点击回调。
    /// </summary>
    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    public override void OnRelease()
    {
        m_initialized = false;
        UnregisterButtons();
        m_editAction = null;
        m_likeCountSnapshot.Clear();
        m_playCountSnapshot.Clear();
        base.OnRelease();
    }
}

public class MPCustomLevelViewUIMsgData : UIMsgData
{
    /// <summary>
    /// 请求编辑现有关卡的回调。
    /// </summary>
    public System.Action<MPCustomLevelInfo> edit;
}

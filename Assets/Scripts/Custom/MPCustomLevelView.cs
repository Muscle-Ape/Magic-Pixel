using HQ.UIManager;
using SuperScrollView;
using System.Collections.Generic;
using UnityEngine;

[Component("MPCustomLevelView")]
public class MPCustomLevelView : AWindow
{
    private const string LEVEL_ITEM_PREFAB = "MPCustomLevelItem";
    private const string SPACER_ITEM_PREFAB = "MPMainLevelSpacerItem";
    private const float TOP_SPACE_HEIGHT = 590f;

    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    [TransformPath("View/Head")]
    private MPHead m_head;

    /// <summary>
    /// 有关卡时显示的列表页面，包含标题和滚动列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private RectTransform m_levelsRoot;

    [TransformPath("View/Center/Levels/Levels")]
    private LoopListView2 m_loopList;

    /// <summary>
    /// 空仓库提示节点。
    /// </summary>
    [TransformPath("View/Center/EmptyTip")]
    private RectTransform m_emptyTip;

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
    /// 首次 LoadUIMsgData 完成后才允许焦点回调刷新页面。
    /// </summary>
    private bool m_initialized;
    private bool m_listInitialized;

    /// <summary>
    /// 加载自定义关卡列表页面数据。
    /// </summary>
    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        MPCustomLevelViewUIMsgData data = uiMsg as MPCustomLevelViewUIMsgData;
        m_editAction = data?.edit;
        m_levelInfos = MPUser.instance.GetCustomLevels();
        CaptureStatisticsSnapshot();
        RefreshEmptyTip();
        InitializeList();

        m_head.Init(OnBackClick, OnSettingClick, MPUserPop.Show);
        m_initialized = true;

        // 只更新持久化缓存，不通知当前页面；下一次打开页面时才展示新数据。
        _ = MPCustomLevelPublishManager.Instance
            .RefreshPublishedLocalLevelStatsCacheAsync();
    }

    public override void OnFocus(bool focus)
    {
        if (!focus || !m_initialized)
            return;

        m_head?.Refresh();
    }

    /// <summary>
    /// 使用预制体中的对象池和尺寸初始化列表，只创建可见项及缓冲项。
    /// </summary>
    private void InitializeList()
    {
        if (m_loopList == null)
            return;

        int count = GetListItemCount();
        if (m_listInitialized)
        {
            m_loopList.SetListItemCount(count, false);
            m_loopList.RefreshAllShownItem();
            return;
        }

        ItemPrefabConfData config = m_loopList.GetItemPrefabConfData(LEVEL_ITEM_PREFAB);
        if (config?.mItemPrefab == null ||
            m_loopList.GetItemPrefabConfData(SPACER_ITEM_PREFAB)?.mItemPrefab == null)
        {
            Debug.LogError("[MPCustomLevelView] Levels/Levels 缺少关卡或顶部空白占位 Item 的对象池配置。");
            return;
        }

        float height = config.mItemPrefab.GetComponent<RectTransform>().rect.height;
        float padding = config.mPadding;
        LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();
        initParam.mItemDefaultWithPaddingSize = height + padding;
        m_loopList.InitListView(count, GetCustomLevelByIndex, initParam,
            index => index == 0 ? (TOP_SPACE_HEIGHT, 0f) : (height, padding));
        m_listInitialized = true;
    }

    /// <summary>有关卡时额外添加一个顶部占位项，空仓库不生成占位。</summary>
    private int GetListItemCount()
    {
        int count = m_levelInfos?.Count ?? 0;
        return count > 0 ? count + 1 : 0;
    }

    /// <summary>
    /// 根据索引获取自定义关卡列表项。
    /// </summary>
    private LoopListViewItem2 GetCustomLevelByIndex(LoopListView2 view, int index)
    {
        if (index < 0 || index >= GetListItemCount())
            return null;

        if (index == 0)
        {
            LoopListViewItem2 spacer = view.NewListViewItem(SPACER_ITEM_PREFAB);
            if (spacer != null)
                spacer.CachedRectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical, TOP_SPACE_HEIGHT);
            return spacer;
        }

        // 列表第 0 项为空白，关卡数据索引从列表第 1 项开始。
        int levelIndex = index - 1;
        MPCustomLevelInfo levelInfo = m_levelInfos[levelIndex];
        if (levelInfo == null || string.IsNullOrEmpty(levelInfo.ID))
            return null;

        LoopListViewItem2 item = view.NewListViewItem(LEVEL_ITEM_PREFAB);
        if (item == null)
            return null;

        MPCustomLevelItem level = item.GetComponent<MPCustomLevelItem>();
        if (level == null)
        {
            level = item.gameObject.AddComponent<MPCustomLevelItem>();
        }

        // 初始化为幂等操作；复用时更新回调，不重复注册按钮事件。
        System.Action<MPCustomLevelInfo> edit = m_editAction == null ? null : OnEditLevel;
        level.Initialize(RefreshLevels, edit);
        item.IsInitHandlerCalled = true;

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
        level.Refresh(levelInfo, levelIndex, cachedLikeCount, cachedPlayCount);
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
        if (!m_initialized || IsDestoried)
            return;

        m_levelInfos = MPUser.instance.GetCustomLevels();
        RefreshEmptyTip();
        InitializeList();
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
        bool hasLevels = m_levelInfos != null && m_levelInfos.Count > 0;
        if (m_levelsRoot != null)
            m_levelsRoot.gameObject.SetActive(hasLevels);
        if (m_emptyTip != null)
            m_emptyTip.gameObject.SetActive(!hasLevels);
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
        MPNoNetworkPop.DismissLevelEntry(this);
        m_initialized = false;
        m_head?.Release();
        if (m_listInitialized && m_loopList != null)
            m_loopList.SetListItemCount(0);
        m_editAction = null;
        m_levelInfos = null;
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

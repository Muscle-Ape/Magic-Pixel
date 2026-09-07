using HQ.UIManager;
using SuperScrollView;
using UnityEngine;

public partial class MPHomeView
{
    [TransformPath("View/Center/Larger")]
    private RectTransform m_largerPage;

    [TransformPath("View/Center/Larger/Levels")]
    private LoopListView2 m_largerLevels;

    private const string LARGER_ITEM_PREFAB = "MPLargeImageLevelItem";
    private const string LARGER_SPACER_PREFAB = "MPMainLevelSpacerItem";
    private const float LARGER_TOP_SPACE = 500f;
    private const float LARGER_BOTTOM_SPACE = 275f;
    private MPLargeImageLevelModel m_largerLevelModel;
    private bool m_largerListInitialized;
    private float m_largerItemHeight;
    private float m_largerItemPadding;

    /// <summary>使用预制体已配置的对象池初始化一次，列表仅创建可见关卡和少量缓冲项。</summary>
    private void InitializeLargerPage()
    {
        if (m_largerListInitialized || m_largerLevels == null)
            return;
        m_largerLevelModel = MPDataManager.Instance.m_largeImageModel;
        ItemPrefabConfData itemConfig = m_largerLevels.GetItemPrefabConfData(LARGER_ITEM_PREFAB);
        if (itemConfig?.mItemPrefab == null || m_largerLevels.GetItemPrefabConfData(LARGER_SPACER_PREFAB)?.mItemPrefab == null)
        {
            Debug.LogError("[MPHomeView] Larger/Levels 缺少关卡或空白占位 Item 的对象池配置。");
            return;
        }

        // 预留头栏、底栏和卡片上凸图标的空间；中间关卡间距沿用预制体配置。
        m_largerItemHeight = itemConfig.mItemPrefab.GetComponent<RectTransform>().rect.height;
        m_largerItemPadding = itemConfig.mPadding;
        LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();
        initParam.mItemDefaultWithPaddingSize = m_largerItemHeight + m_largerItemPadding;
        m_largerLevels.InitListView(GetLargerListCount(), GetLargerItemByIndex, initParam, GetLargerItemSizeByIndex);
        m_largerListInitialized = true;
    }

    private int GetLargerListCount()
    {
        int count = m_largerLevelModel?.blockInfos?.Count ?? 0;
        return count > 0 ? count + 2 : 0;
    }

    private (float, float) GetLargerItemSizeByIndex(int index)
    {
        if (index == 0) return (LARGER_TOP_SPACE, 0f);
        if (index == GetLargerListCount() - 1) return (LARGER_BOTTOM_SPACE, 0f);
        return (m_largerItemHeight, m_largerItemPadding);
    }

    private LoopListViewItem2 GetLargerItemByIndex(LoopListView2 view, int index)
    {
        int count = GetLargerListCount();
        if (!m_initialized || index < 0 || index >= count)
            return null;
        if (index == 0 || index == count - 1)
        {
            LoopListViewItem2 spacer = view.NewListViewItem(LARGER_SPACER_PREFAB);
            if (spacer != null)
                spacer.CachedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    index == 0 ? LARGER_TOP_SPACE : LARGER_BOTTOM_SPACE);
            return spacer;
        }

        LoopListViewItem2 item = view.NewListViewItem(LARGER_ITEM_PREFAB);
        if (item == null)
            return null;
        MPLargeImageLevelItem level = item.GetComponent<MPLargeImageLevelItem>();
        if (level == null)
            level = item.gameObject.AddComponent<MPLargeImageLevelItem>();
        level.Initialize(RefreshLargerPage);
        item.IsInitHandlerCalled = true;
        int levelIndex = index - 1;
        level.Refresh(m_largerLevelModel.blockInfos[levelIndex], levelIndex);
        return item;
    }

    /// <summary>解锁、通关、返回主页或切换页签后只刷新可见项，保留滚动位置。</summary>
    private void RefreshLargerPage()
    {
        if (!m_initialized || IsDestoried)
            return;
        InitializeLargerPage();
        if (!m_largerListInitialized)
            return;
        m_largerLevelModel = MPDataManager.Instance.m_largeImageModel;
        m_largerLevels.SetListItemCount(GetLargerListCount(), false);
        m_largerLevels.RefreshAllShownItem();
        RefreshCurrency();
    }

    private void ReleaseLargerPage()
    {
        MPNoNetworkPop.DismissLevelEntry(this);
        if (m_largerListInitialized && m_largerLevels != null)
        {
            m_largerLevels.SetListItemCount(0);
            foreach (MPLargeImageLevelItem item in m_largerLevels.GetComponentsInChildren<MPLargeImageLevelItem>(true))
                item.ReleaseResources();
        }
        m_largerListInitialized = false;
        m_largerLevelModel = null;
    }
}

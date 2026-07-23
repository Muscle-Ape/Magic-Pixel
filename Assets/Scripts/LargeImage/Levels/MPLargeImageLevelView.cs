using HQ.UIManager;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPLargeImageLevelView")]
public class MPLargeImageLevelView : AWindow
{
    private const int EXTRA_PADDING_ITEM_COUNT = 2;
    private const float TOP_PADDING_HEIGHT = 500f;
    private const float BOTTOM_PADDING_HEIGHT = 50f;
    private const string TOP_PADDING_ITEM_NAME = "MPLargeImageLevelTopPadding";
    private const string BOTTOM_PADDING_ITEM_NAME = "MPLargeImageLevelBottomPadding";

    /// <summary>
    /// 返回按钮。
    /// </summary>
    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    /// <summary>
    /// 设置按钮。
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 关卡滚动列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopListView2 m_loopGrid;

    /// <summary>
    /// 金币数量文本。
    /// </summary>
    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量文本。
    /// </summary>
    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 关卡数据。
    /// </summary>
    private MPLargeImageLevelModel m_levelModel;

    /// <summary>
    /// 列表顶部占位节点。
    /// </summary>
    private RectTransform m_topPaddingItem;

    /// <summary>
    /// 列表底部占位节点。
    /// </summary>
    private RectTransform m_bottomPaddingItem;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelModel = MPDataManager.Instance.m_largeImageModel;

        CreatePaddingItems();
        m_loopGrid.InitListView(m_levelModel.blockInfos.Count + EXTRA_PADDING_ITEM_COUNT, OnGetItemByIndex);

        m_backBtn.onClick.AddListener(OnBackClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshUI();
        }
    }

    /// <summary>
    /// 刷新顶部资源显示。
    /// </summary>
    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    /// <summary>
    /// 根据列表索引返回对应的真实关卡 Item 或上下占位 Item。
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 view, int index)
    {
        if (index == 0)
        {
            return GetPaddingItem(TOP_PADDING_ITEM_NAME, TOP_PADDING_HEIGHT);
        }

        if (index == m_levelModel.blockInfos.Count + 1)
        {
            return GetPaddingItem(BOTTOM_PADDING_ITEM_NAME, BOTTOM_PADDING_HEIGHT);
        }

        int levelIndex = index - 1;
        if (levelIndex < 0 || levelIndex >= m_levelModel.blockInfos.Count)
        {
            return null;
        }

        MPLargeImageBlockInfo data = m_levelModel.blockInfos[levelIndex];
        LoopListViewItem2 item = m_loopGrid.NewListViewItem("MPLargeImageLevelItem");
        MPLargeImageLevelItem level = item.GetComponent<MPLargeImageLevelItem>();

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            level.Initialize(RefreshLevels);
        }

        level.Refresh(data, levelIndex);
        return item;
    }

    /// <summary>
    /// 创建并注册列表上下占位 Item 的 prefab。
    /// </summary>
    private void CreatePaddingItems()
    {
        m_topPaddingItem = CreatePaddingItem(TOP_PADDING_ITEM_NAME, TOP_PADDING_HEIGHT);
        m_bottomPaddingItem = CreatePaddingItem(BOTTOM_PADDING_ITEM_NAME, BOTTOM_PADDING_HEIGHT);

        RegisterPaddingItem(m_topPaddingItem.gameObject);
        RegisterPaddingItem(m_bottomPaddingItem.gameObject);
    }

    /// <summary>
    /// 创建一个只有 RectTransform 的占位 Item。
    /// </summary>
    /// <param name="name">占位 Item 名称。</param>
    /// <param name="height">占位高度。</param>
    /// <returns>占位 Item 的 RectTransform。</returns>
    private RectTransform CreatePaddingItem(string name, float height)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(LoopListViewItem2));
        RectTransform rectTransform = item.GetComponent<RectTransform>();
        rectTransform.SetParent(m_loopGrid.transform, false);
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(0f, height);
        item.SetActive(false);
        return rectTransform;
    }

    /// <summary>
    /// 将占位 Item 注册到 LoopListView2 的对象池配置中。
    /// </summary>
    /// <param name="item">占位 Item prefab。</param>
    private void RegisterPaddingItem(GameObject item)
    {
        if (item == null || m_loopGrid.GetItemPrefabConfData(item.name) != null)
        {
            return;
        }

        m_loopGrid.ItemPrefabDataList.Add(new ItemPrefabConfData()
        {
            mItemPrefab = item,
            mPadding = 0f,
            mInitCreateCount = 0,
            mStartPosOffset = 0f,
        });
    }

    /// <summary>
    /// 获取指定高度的占位 Item。
    /// </summary>
    /// <param name="itemName">占位 Item prefab 名称。</param>
    /// <param name="height">占位高度。</param>
    /// <returns>占位 Item。</returns>
    private LoopListViewItem2 GetPaddingItem(string itemName, float height)
    {
        LoopListViewItem2 item = m_loopGrid.NewListViewItem(itemName);
        item.CachedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        return item;
    }

    /// <summary>
    /// 刷新所有当前显示的关卡 Item。
    /// </summary>
    private void RefreshLevels()
    {
        m_loopGrid.RefreshAllShownItem();
    }

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }
}

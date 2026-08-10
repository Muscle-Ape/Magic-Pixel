using HQ.UIManager;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPHomeView")]
public class MPHomeView : AWindow
{
    private const string LEVEL_ITEM_PREFAB_NAME = "MPMainLevelItem";
    private const string SPACER_ITEM_PREFAB_NAME = "MPMainLevelSpacerItem";
    private const float LEVEL_ITEM_HEIGHT = 480f;
    private const float BOTTOM_SPACER_SCALE = 0.5f;

    /// <summary>
    /// 主关卡循环列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopListView2 m_loopList;

    /// <summary>
    /// 设置按钮
    /// </summary>
    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    /// <summary>
    /// 大图模式按钮
    /// </summary>
    [TransformPath("View/Down/Tab/LargeImage")]
    private Button m_largeImageBtn;

    /// <summary>
    /// 自定义模式按钮
    /// </summary>
    [TransformPath("View/Down/Tab/Custom")]
    private Button m_customBtn;

    /// <summary>
    /// 宠物功能按钮
    /// </summary>
    [TransformPath("View/Down/Tab/Pets")]
    private Button m_petsBtn;

    /// <summary>
    /// 3D
    /// </summary>
    [TransformPath("View/Down/Tab/ThreeD")]
    private Button m_threeDBtn;

    /// <summary>
    /// 金币数量
    /// </summary>
    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    /// <summary>
    /// 钻石数量
    /// </summary>
    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    /// <summary>
    /// 主关卡数据
    /// </summary>
    private MPMainLevelModel m_levelModel;

    /// <summary>
    /// 云层滚动视差控制器。
    /// </summary>
    private MPHomeParallaxController m_parallaxController;

    /// <summary>
    /// 列表完成初始化后才允许在 OnFocus 中刷新和定位。
    /// UIManager 首次创建窗口时会先触发 OnFocus，再调用 LoadUIMsgData。
    /// </summary>
    private bool m_listInitialized;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelModel = MPDataManager.Instance.m_mainLevelModel;
        int levelCount = m_levelModel.blockInfos.Count;
        LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();
        initParam.mItemDefaultWithPaddingSize = LEVEL_ITEM_HEIGHT;
        m_loopList.InitListView(
            levelCount + 2,
            GetMainLevelByIndex,
            initParam,
            GetListItemSizeByIndex);
        m_listInitialized = true;

        m_parallaxController = m_loopList.GetComponent<MPHomeParallaxController>();
        if (m_parallaxController != null)
        {
            m_parallaxController.Initialize(m_loopList.ScrollRect);
        }
        else
        {
            Debug.LogError("MPHomeView Prefab 的 Levels 节点缺少 MPHomeParallaxController");
        }

        m_settingBtn.onClick.RemoveListener(OnSettingClick);
        m_largeImageBtn.onClick.RemoveListener(OnLargeImageClick);
        m_customBtn.onClick.RemoveListener(OnCustomClick);
        m_petsBtn.onClick.RemoveListener(OnPetsClick);
        m_threeDBtn.onClick.RemoveListener(OnThreeDClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);
        m_largeImageBtn.onClick.AddListener(OnLargeImageClick);
        m_customBtn.onClick.AddListener(OnCustomClick);
        m_petsBtn.onClick.AddListener(OnPetsClick);
        m_threeDBtn.onClick.AddListener(OnThreeDClick);

        LocateLatestLevelAtCenter();

        // 开始播放背景音乐
        MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshUI();
            if (m_listInitialized)
            {
                RefreshLevels();
                LocateLatestLevelAtCenter();
            }
        }
    }

    private void RefreshUI()
    {
        m_coinText.text = MPUser.instance.GetCoins().ToString();
        m_diamondText.text = MPUser.instance.GetDiamond().ToString();
    }

    /// <summary>
    /// 根据列表下标获取并刷新主关卡 Item。
    /// </summary>
    private LoopListViewItem2 GetMainLevelByIndex(LoopListView2 view, int listIndex)
    {
        int levelCount = m_levelModel.blockInfos.Count;
        if (listIndex < 0 || listIndex >= levelCount + 2)
        {
            return null;
        }

        if (listIndex == 0 || listIndex == levelCount + 1)
        {
            LoopListViewItem2 spacer = view.NewListViewItem(SPACER_ITEM_PREFAB_NAME);
            spacer.CachedRectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                GetSpacerHeight(listIndex));
            return spacer;
        }

        int index = listIndex - 1;
        MPMainBlockInfo data = m_levelModel.blockInfos[index];
        LoopListViewItem2 item = view.NewListViewItem(LEVEL_ITEM_PREFAB_NAME);
        item.CachedRectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            LEVEL_ITEM_HEIGHT);
        MPMainLevelItem level = item.GetComponent<MPMainLevelItem>();

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            level.Initialize(RefreshLevels);
        }

        level.Refresh(data, index, m_levelModel.blockInfos.Count);
        return item;
    }

    /// <summary>
    /// 提前告知 LoopListView2 每个 Item 的尺寸，保证直接定位时的位置准确。
    /// </summary>
    private (float, float) GetListItemSizeByIndex(int listIndex)
    {
        int levelCount = m_levelModel.blockInfos.Count;
        bool isSpacer = listIndex == 0 || listIndex == levelCount + 1;
        return (isSpacer ? GetSpacerHeight(listIndex) : LEVEL_ITEM_HEIGHT, 0f);
    }

    /// <summary>
    /// 底部首个占位区减半，使第一关更靠近屏幕底部；顶部仍保留完整居中空间。
    /// </summary>
    private float GetSpacerHeight(int listIndex)
    {
        float centerOffset = GetCenterOffset();
        if (listIndex == 0)
            return centerOffset * BOTTOM_SPACER_SCALE;

        int levelCount = m_levelModel.blockInfos.Count;
        if (levelCount == 0)
            return centerOffset;

        float lastLevelOffsetY = MPMainLevelItem.GetLevelVerticalOffset(levelCount - 1);
        return Mathf.Max(0f, centerOffset + lastLevelOffsetY);
    }

    /// <summary>
    /// 计算关卡 Item 底边移动到屏幕中心所需的偏移。
    /// </summary>
    private float GetCenterOffset()
    {
        return Mathf.Max(0f, (m_loopList.ViewPortHeight - LEVEL_ITEM_HEIGHT) * 0.5f);
    }

    /// <summary>
    /// 将当前主线进度对应的最新关卡定位到列表视口中心。
    /// </summary>
    private void LocateLatestLevelAtCenter()
    {
        if (!m_listInitialized || m_levelModel.blockInfos.Count == 0)
            return;

        Canvas.ForceUpdateCanvases();
        int latestLevelIndex = Mathf.Clamp(
            MPUser.instance.GetMainLevlPassIndex(),
            0,
            m_levelModel.blockInfos.Count - 1);
        int targetListIndex = latestLevelIndex + 1;
        float targetOffset = GetCenterOffset()
            - MPMainLevelItem.GetLevelVerticalOffset(latestLevelIndex);
        m_loopList.MovePanelToItemIndexImmediately(targetListIndex, targetOffset);
    }

    private void RefreshLevels()
    {
        m_loopList.RefreshAllShownItem();
    }

    public override void OnRelease()
    {
        m_listInitialized = false;
        m_settingBtn.onClick.RemoveListener(OnSettingClick);
        m_largeImageBtn.onClick.RemoveListener(OnLargeImageClick);
        m_customBtn.onClick.RemoveListener(OnCustomClick);
        m_petsBtn.onClick.RemoveListener(OnPetsClick);
        m_threeDBtn.onClick.RemoveListener(OnThreeDClick);

        if (m_parallaxController != null)
        {
            m_parallaxController.Shutdown();
        }
    }


    /// <summary>
    /// 设置按钮点击回调
    /// </summary>
    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    /// <summary>
    /// 大图模式点击回调
    /// </summary>
    private void OnLargeImageClick()
    {
        UIManager.Inst.ShowWindow<MPLargeImageLevelView>();
    }

    /// <summary>
    /// 自定义模式点击回调
    /// </summary>
    private void OnCustomClick()
    {
        UIManager.Inst.ShowWindow<MPCustomView>();
    }

    /// <summary>
    /// 宠物功能点击回调
    /// </summary>
    private void OnPetsClick()
    {
        UIManager.Inst.ShowWindow<MPPetsView>();
    }

    /// <summary>
    /// 3D功能点击回调
    /// </summary>
    private void OnThreeDClick()
    {

    }
}

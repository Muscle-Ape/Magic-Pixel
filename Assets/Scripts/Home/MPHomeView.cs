using HQ.UIManager;
using SuperScrollView;
using DG.Tweening;
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
    private const float TRACK_BUTTON_SHOW_DISTANCE = LEVEL_ITEM_HEIGHT * 4f;
    private const float TRACK_BUTTON_SCALE_DURATION = 0.2f;
    private const float TRACK_SCROLL_DURATION = 0.45f;

    /// <summary>
    /// 每次启动游戏只自动定位一次；返回主页时保留用户当前的滚动位置。
    /// </summary>
    private static bool s_hasLocatedLatestLevelOnLaunch;

    /// <summary>
    /// 主关卡循环列表。
    /// </summary>
    [TransformPath("View/Center/Levels")]
    private LoopListView2 m_loopList;

    /// <summary>
    /// 快速返回最新解锁关卡的按钮。
    /// </summary>
    [TransformPath("View/Center/TrackBtn")]
    private Button m_trackBtn;

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
    /// 列表完成初始化后才允许在 OnFocus 中刷新。
    /// UIManager 首次创建窗口时会先触发 OnFocus，再调用 LoadUIMsgData。
    /// </summary>
    private bool m_listInitialized;

    /// <summary>
    /// 追踪按钮当前是否处于显示状态。
    /// </summary>
    private bool m_trackButtonVisible;

    /// <summary>
    /// 是否正在自动定位最新关卡。
    /// </summary>
    private bool m_isTrackingLatestLevel;

    private Tween m_trackButtonTween;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLaunchState()
    {
        s_hasLocatedLatestLevelOnLaunch = false;
    }

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

        InitializeTrackButton();

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

        if (!s_hasLocatedLatestLevelOnLaunch && LocateLatestLevelAtCenter())
        {
            s_hasLocatedLatestLevelOnLaunch = true;
        }

        RefreshTrackButtonVisibility();

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
                if (m_isTrackingLatestLevel)
                {
                    m_loopList.ClearAutoMoveToItemData();
                    m_isTrackingLatestLevel = false;
                }

                RefreshLevels();
                RefreshTrackButtonVisibility();
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
    private bool LocateLatestLevelAtCenter()
    {
        if (!TryGetLatestLevelTarget(
            out _,
            out int targetListIndex,
            out float targetOffset))
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        m_loopList.MovePanelToItemIndexImmediately(targetListIndex, targetOffset);
        return true;
    }

    /// <summary>
    /// 初始化追踪按钮及列表滚动监听。按钮隐藏时始终禁止点击。
    /// </summary>
    private void InitializeTrackButton()
    {
        KillTrackButtonTween();
        m_trackButtonVisible = false;
        m_isTrackingLatestLevel = false;
        m_trackBtn.transform.localScale = Vector3.zero;
        m_trackBtn.interactable = false;

        m_trackBtn.onClick.RemoveListener(OnTrackButtonClick);
        m_trackBtn.onClick.AddListener(OnTrackButtonClick);
        m_loopList.ScrollRect.onValueChanged.RemoveListener(OnLevelScrollValueChanged);
        m_loopList.ScrollRect.onValueChanged.AddListener(OnLevelScrollValueChanged);
        m_loopList.mOnSmoothMovePanelToItemFinished -= OnTrackMoveFinished;
        m_loopList.mOnSmoothMovePanelToItemFinished += OnTrackMoveFinished;
    }

    private void OnLevelScrollValueChanged(Vector2 _)
    {
        RefreshTrackButtonVisibility();
    }

    /// <summary>
    /// 只在用户向上离开最新关卡超过两个关卡高度后显示追踪按钮。
    /// </summary>
    private void RefreshTrackButtonVisibility()
    {
        if (!m_listInitialized || m_trackBtn == null)
            return;

        if (m_isTrackingLatestLevel)
        {
            SetTrackButtonVisible(false);
            return;
        }

        if (!TryGetLatestLevelTarget(
            out int latestLevelIndex,
            out _,
            out _))
        {
            SetTrackButtonVisible(false);
            return;
        }

        if (!TryGetLatestLevelViewportY(
            latestLevelIndex,
            out float latestLevelViewportY))
        {
            SetTrackButtonVisible(false);
            return;
        }

        float viewportCenterY = m_loopList.ViewPortTrans.rect.center.y;
        bool shouldShow = viewportCenterY - latestLevelViewportY
            > TRACK_BUTTON_SHOW_DISTANCE;
        SetTrackButtonVisible(shouldShow);
    }

    /// <summary>
    /// 获取最新解锁关卡的列表下标和居中偏移。
    /// </summary>
    private bool TryGetLatestLevelTarget(
        out int latestLevelIndex,
        out int targetListIndex,
        out float targetOffset)
    {
        latestLevelIndex = 0;
        targetListIndex = 0;
        targetOffset = 0f;
        if (!m_listInitialized
            || m_levelModel == null
            || m_levelModel.blockInfos.Count == 0)
        {
            return false;
        }

        latestLevelIndex = Mathf.Clamp(
            MPUser.instance.GetMainLevlPassIndex(),
            0,
            m_levelModel.blockInfos.Count - 1);
        targetListIndex = latestLevelIndex + 1;
        targetOffset = GetCenterOffset()
            - MPMainLevelItem.GetLevelVerticalOffset(latestLevelIndex);
        return true;
    }

    /// <summary>
    /// 根据首个可见 Item 的真实位置，计算最新关卡中心在视口中的 Y 坐标。
    /// LoopListView2 会重排复用 Item，不能依赖 Content 的绝对坐标判断距离。
    /// </summary>
    private bool TryGetLatestLevelViewportY(
        int latestLevelIndex,
        out float latestLevelViewportY)
    {
        latestLevelViewportY = 0f;
        LoopListViewItem2 firstShownItem = m_loopList.GetShownItemByIndex(0);
        if (firstShownItem == null)
            return false;

        float firstItemBottomY = m_loopList.GetItemCornerPosInViewPort(
            firstShownItem,
            ItemCornerEnum.LeftBottom).y;
        int targetListIndex = latestLevelIndex + 1;
        float targetItemBottomOffset = GetListItemBottomOffset(targetListIndex)
            - GetListItemBottomOffset(firstShownItem.ItemIndex);
        latestLevelViewportY = firstItemBottomY
            + targetItemBottomOffset
            + LEVEL_ITEM_HEIGHT * 0.5f
            + MPMainLevelItem.GetLevelVerticalOffset(latestLevelIndex);
        return true;
    }

    /// <summary>
    /// 获取指定列表 Item 底边到列表起点的累计距离。
    /// </summary>
    private float GetListItemBottomOffset(int listIndex)
    {
        if (listIndex <= 0)
            return 0f;

        return GetSpacerHeight(0) + (listIndex - 1) * LEVEL_ITEM_HEIGHT;
    }

    private void SetTrackButtonVisible(bool visible)
    {
        if (m_trackButtonVisible == visible)
        {
            if (!visible)
            {
                m_trackBtn.interactable = false;
            }
            else if (!m_isTrackingLatestLevel
                && (m_trackButtonTween == null
                    || !m_trackButtonTween.IsActive()))
            {
                m_trackBtn.interactable = true;
            }

            return;
        }

        m_trackButtonVisible = visible;
        KillTrackButtonTween();
        m_trackBtn.interactable = false;
        Vector3 targetScale = visible ? Vector3.one : Vector3.zero;
        Ease ease = visible ? Ease.OutBack : Ease.InBack;
        m_trackButtonTween = m_trackBtn.transform
            .DOScale(targetScale, TRACK_BUTTON_SCALE_DURATION)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(m_trackBtn.gameObject)
            .OnComplete(() =>
            {
                m_trackButtonTween = null;
                m_trackBtn.interactable = m_trackButtonVisible
                    && !m_isTrackingLatestLevel;
            });
    }

    private void OnTrackButtonClick()
    {
        if (!m_trackButtonVisible || m_isTrackingLatestLevel)
            return;

        if (!TryGetLatestLevelTarget(
            out _,
            out int targetListIndex,
            out float targetOffset))
        {
            SetTrackButtonVisible(false);
            return;
        }

        m_isTrackingLatestLevel = true;
        SetTrackButtonVisible(false);
        m_loopList.MovePanelToItemIndex(
            targetListIndex,
            targetOffset,
            TRACK_SCROLL_DURATION);
    }

    private void OnTrackMoveFinished(
        LoopListView2 _,
        int targetItemIndex,
        float targetItemOffset)
    {
        if (!m_isTrackingLatestLevel)
            return;

        m_isTrackingLatestLevel = false;
        RefreshTrackButtonVisibility();
    }

    private void KillTrackButtonTween()
    {
        if (m_trackButtonTween != null && m_trackButtonTween.IsActive())
        {
            m_trackButtonTween.Kill();
        }

        m_trackButtonTween = null;
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
        m_trackBtn.onClick.RemoveListener(OnTrackButtonClick);

        if (m_loopList != null)
        {
            m_loopList.ScrollRect.onValueChanged.RemoveListener(
                OnLevelScrollValueChanged);
            m_loopList.mOnSmoothMovePanelToItemFinished -= OnTrackMoveFinished;
            m_loopList.ClearAutoMoveToItemData();
            m_loopList.ScrollRect.StopMovement();
        }

        KillTrackButtonTween();
        m_isTrackingLatestLevel = false;
        m_trackButtonVisible = false;
        if (m_trackBtn != null)
        {
            m_trackBtn.interactable = false;
        }

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
        MPThreeDIntegration.Open();
    }
}

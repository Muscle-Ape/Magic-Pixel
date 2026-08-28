using HQ.UIManager;
using SuperScrollView;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Component("MPMainLevelView")]
public class MPMainLevelView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    private const string LEVEL_ITEM_PREFAB_NAME = "MPMainLevelItem";
    private const string SPACER_ITEM_PREFAB_NAME = "MPMainLevelSpacerItem";
    private const float LEVEL_ITEM_HEIGHT = 480f;
    private const float BOTTOM_SPACER_SCALE = 0.5f;
    private const float TRACK_BUTTON_SHOW_DISTANCE = LEVEL_ITEM_HEIGHT * 4f;
    private const float TRACK_BUTTON_SCALE_DURATION = 0.2f;
    private const float TRACK_SCROLL_DURATION = 0.45f;
    private const float CLOUD_PARALLAX_FACTOR = 0.12f;
    private const float FLASH_MIN_SCALE = 0.35f;
    private const float FLASH_MAX_SCALE = 1f;
    private const float FLASH_INITIAL_DELAY_MAX = 1.4f;
    private const int LEVEL_LINE_SEGMENTS_PER_CONNECTION = 10;
    private const float LEVEL_LINE_CONTROL_RATIO = 0.38f;

    /// <summary>
    /// 每次启动游戏只自动定位一次；返回主页时保留用户当前的滚动位置。
    /// </summary>
    private static bool s_hasLocatedLatestLevelOnLaunch;

    [TransformPath("View/Head/BackBtn")]
    private Button m_backBtn;

    [TransformPath("View/Head/SettingBtn")]
    private Button m_settingBtn;

    [TransformPath("View/Head/Coin/Count")]
    private TMP_Text m_coinText;

    [TransformPath("View/Head/Diamond/Count")]
    private TMP_Text m_diamondText;

    [TransformPath("View/Head/PlayerName")]
    private TMP_Text m_playerNameText;

    [TransformPath("View/Head/Level/Text")]
    private TMP_Text m_playerLevelText;

    [TransformPath("View/Head/Level/Mask/Fill")]
    private Image m_playerLevelFill;

    /// <summary>
    /// 主关卡循环列表。
    /// </summary>
    [TransformPath("View/Levels")]
    private LoopListView2 m_loopList;

    /// <summary>
    /// 快速返回最新解锁关卡的按钮。
    /// </summary>
    [TransformPath("View/TrackBtn")]
    private Button m_trackBtn;

    [TransformPath("View/Cloud/Cloud1")]
    private RectTransform m_cloudOne;

    [TransformPath("View/Cloud/Cloud2")]
    private RectTransform m_cloudTwo;

    [TransformPath("View/Flash")]
    private RectTransform m_flashRoot;

    [TransformPath("View/Lines")]
    private RectTransform m_levelLinesRoot;

    [TransformPath("View/Lines/Path")]
    private MPUILineGraphic m_levelPathLine;

    [TransformPath("View/Lines/Completed")]
    private MPUILineGraphic m_levelCompletedLine;

    /// <summary>
    /// 主关卡数据
    /// </summary>
    private MPMainLevelModel m_levelModel;

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

    private float m_cloudStartContentY;
    private float m_cloudHeight;
    private float m_cloudOneX;
    private float m_cloudTwoX;
    private bool m_cloudInitialized;

    private RectTransform[] m_flashPoints;
    private Image[] m_flashImages;
    private Sequence[] m_flashSequences;
    private bool m_flashRunning;

    private readonly List<LevelLineAnchor> m_levelLineAnchors =
        new List<LevelLineAnchor>();
    private readonly List<Vector2> m_levelLinePoints =
        new List<Vector2>();
    private readonly List<Vector2> m_completedLevelLinePoints =
        new List<Vector2>();
    private bool m_levelLineInitialized;

    private struct LevelLineAnchor
    {
        public int ListIndex;
        public Vector2 Position;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLaunchState()
    {
        s_hasLocatedLatestLevelOnLaunch = false;
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        RegisterHeadButtons();
        RefreshHead();

        m_loopList.gameObject.SetActive(true);
        m_trackBtn.gameObject.SetActive(true);
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

        InitializeTrackButton();

        if (!s_hasLocatedLatestLevelOnLaunch && LocateLatestLevelAtCenter())
        {
            s_hasLocatedLatestLevelOnLaunch = true;
        }

        InitializeLevelLine();
        InitializeCloudParallax();
        InitializeFlashEffects();
        RefreshTrackButtonVisibility();

        // 开始播放背景音乐
        MPAudioManager.Instance.PlayBGM(MPMusic.MPBGMMain);
    }

    public override void OnFocus(bool focus)
    {
        if (!focus)
        {
            StopFlashEffects();
            return;
        }

        if (m_listInitialized)
        {
            if (m_isTrackingLatestLevel)
            {
                m_loopList.ClearAutoMoveToItemData();
                m_isTrackingLatestLevel = false;
            }

            RefreshLevels();
            RefreshCloudParallax();
            StartFlashEffects();
            RefreshTrackButtonVisibility();
        }
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
            level.Initialize(RefreshLevels, OnBoxAwardInfoRequested);
        }

        level.Refresh(data, index);
        return item;
    }

    /// <summary>
    /// 未满足宝箱领取条件时的奖励信息展示入口。
    /// 后续可在这里打开 Toast 或奖励详情弹窗。
    /// </summary>
    private void OnBoxAwardInfoRequested(MPMainBlockInfo levelInfo)
    {
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
        RefreshLevelLine();
        RefreshCloudParallax();
        RefreshTrackButtonVisibility();
    }

    /// <summary>
    /// 初始化 UGUI 关卡连接线。Path 绘制完整路径，Completed 在其上方
    /// 绘制第一关到当前最新解锁关卡的路径。
    /// </summary>
    private void InitializeLevelLine()
    {
        m_levelLineInitialized = false;
        if (m_levelLinesRoot == null
            || m_levelPathLine == null
            || m_levelCompletedLine == null)
        {
            ClearLevelLine();
            return;
        }

        m_levelPathLine.raycastTarget = false;
        m_levelCompletedLine.raycastTarget = false;

        Transform levelsTransform = m_loopList != null
            ? m_loopList.transform
            : null;
        Transform cloudTransform = m_cloudOne != null
            ? m_cloudOne.parent
            : null;
        if (cloudTransform != null
            && m_levelLinesRoot.parent == cloudTransform.parent)
        {
            m_levelLinesRoot.SetSiblingIndex(
                cloudTransform.GetSiblingIndex() + 1);
        }
        else if (levelsTransform != null
            && m_levelLinesRoot.parent == levelsTransform.parent
            && m_levelLinesRoot.GetSiblingIndex()
            > levelsTransform.GetSiblingIndex())
        {
            m_levelLinesRoot.SetSiblingIndex(
                levelsTransform.GetSiblingIndex());
        }

        m_levelLineInitialized = true;
        Canvas.ForceUpdateCanvases();
        RefreshLevelLine();
    }

    /// <summary>
    /// 使用当前已创建的循环列表 Item 生成平滑曲线。
    /// 只处理可见区及列表预加载区，避免为全部关卡持续更新大量顶点。
    /// </summary>
    private void RefreshLevelLine()
    {
        if (!m_listInitialized
            || !m_levelLineInitialized
            || m_levelLinesRoot == null
            || m_levelPathLine == null
            || m_levelCompletedLine == null
            || m_loopList == null)
        {
            ClearLevelLine();
            return;
        }

        m_levelLineAnchors.Clear();
        int levelCount = m_levelModel?.blockInfos?.Count ?? 0;
        for (int i = 0; i < m_loopList.ShownItemCount; i++)
        {
            LoopListViewItem2 item = m_loopList.GetShownItemByIndex(i);
            if (item == null
                || item.ItemIndex <= 0
                || item.ItemIndex > levelCount)
            {
                continue;
            }

            MPMainLevelItem levelItem = item.GetComponent<MPMainLevelItem>();
            RectTransform levelRoot = levelItem != null
                ? levelItem.LevelRoot
                : null;
            if (levelRoot == null)
                continue;

            Vector2 position = m_levelLinesRoot.InverseTransformPoint(
                levelRoot.position);
            m_levelLineAnchors.Add(new LevelLineAnchor
            {
                ListIndex = item.ItemIndex,
                Position = position,
            });
        }

        if (m_levelLineAnchors.Count < 2)
        {
            ClearLevelLine();
            return;
        }

        m_levelLineAnchors.Sort(CompareLevelLineAnchor);
        BuildLevelLinePoints();
        if (m_levelLinePoints.Count < 2)
        {
            ClearLevelLine();
            return;
        }

        SetLevelLinePoints(m_levelPathLine, m_levelLinePoints);
        SetLevelLinePoints(
            m_levelCompletedLine,
            m_completedLevelLinePoints);
    }

    private void BuildLevelLinePoints()
    {
        m_levelLinePoints.Clear();
        m_completedLevelLinePoints.Clear();
        int levelCount = m_levelModel?.blockInfos?.Count ?? 0;
        int latestUnlockedListIndex = levelCount > 0
            ? Mathf.Clamp(
                MPUser.instance.GetMainLevlPassIndex(),
                0,
                levelCount - 1) + 1
            : 0;
        for (int i = 0; i < m_levelLineAnchors.Count - 1; i++)
        {
            LevelLineAnchor current = m_levelLineAnchors[i];
            LevelLineAnchor next = m_levelLineAnchors[i + 1];
            if (next.ListIndex != current.ListIndex + 1)
                continue;

            Vector2 start = current.Position;
            Vector2 end = next.Position;
            float direction = Mathf.Sign(end.y - start.y);
            if (Mathf.Approximately(direction, 0f))
                direction = 1f;

            float controlDistance = Mathf.Abs(end.y - start.y)
                * LEVEL_LINE_CONTROL_RATIO;
            Vector2 controlOffset = Vector2.up
                * direction
                * controlDistance;
            Vector2 controlOne = start + controlOffset;
            Vector2 controlTwo = end - controlOffset;
            AddBezierPoints(
                m_levelLinePoints,
                start,
                controlOne,
                controlTwo,
                end);
            if (next.ListIndex <= latestUnlockedListIndex)
            {
                AddBezierPoints(
                    m_completedLevelLinePoints,
                    start,
                    controlOne,
                    controlTwo,
                    end);
            }
        }
    }

    private static void AddBezierPoints(
        List<Vector2> points,
        Vector2 start,
        Vector2 controlOne,
        Vector2 controlTwo,
        Vector2 end)
    {
        int startSegment = points.Count == 0 ? 0 : 1;
        for (int segment = startSegment;
            segment <= LEVEL_LINE_SEGMENTS_PER_CONNECTION;
            segment++)
        {
            float progress = segment
                / (float)LEVEL_LINE_SEGMENTS_PER_CONNECTION;
            points.Add(EvaluateCubicBezier(
                start,
                controlOne,
                controlTwo,
                end,
                progress));
        }
    }

    private static void SetLevelLinePoints(
        MPUILineGraphic line,
        List<Vector2> points)
    {
        if (line == null)
            return;

        bool hasLine = points != null && points.Count >= 2;
        if (hasLine)
            line.SetPoints(points);
        else
            line.ClearPoints();

        line.enabled = hasLine;
    }

    private static Vector2 EvaluateCubicBezier(
        Vector2 start,
        Vector2 controlOne,
        Vector2 controlTwo,
        Vector2 end,
        float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * inverse * start
            + 3f * inverse * inverse * progress * controlOne
            + 3f * inverse * progress * progress * controlTwo
            + progress * progress * progress * end;
    }

    private static int CompareLevelLineAnchor(
        LevelLineAnchor left,
        LevelLineAnchor right)
    {
        return left.ListIndex.CompareTo(right.ListIndex);
    }

    private void ClearLevelLine()
    {
        m_levelLineAnchors.Clear();
        m_levelLinePoints.Clear();
        m_completedLevelLinePoints.Clear();

        if (m_levelPathLine != null)
        {
            m_levelPathLine.ClearPoints();
            m_levelPathLine.enabled = false;
        }

        if (m_levelCompletedLine != null)
        {
            m_levelCompletedLine.ClearPoints();
            m_levelCompletedLine.enabled = false;
        }
    }

    /// <summary>
    /// 记录关卡列表的初始位置，并让两张可首尾衔接的云图组成循环背景。
    /// 初次自动定位完成后再记录基准，避免启动定位造成云层跳动。
    /// </summary>
    private void InitializeCloudParallax()
    {
        m_cloudInitialized = false;
        if (m_cloudOne == null
            || m_cloudTwo == null
            || m_loopList == null
            || m_loopList.ContainerTrans == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        m_cloudHeight = Mathf.Max(
            Mathf.Abs(m_cloudOne.rect.height),
            Mathf.Abs(m_cloudOne.sizeDelta.y));
        if (m_cloudHeight <= Mathf.Epsilon)
            return;

        m_cloudStartContentY = m_loopList.ContainerTrans.anchoredPosition.y;
        m_cloudOneX = m_cloudOne.anchoredPosition.x;
        m_cloudTwoX = m_cloudTwo.anchoredPosition.x;
        Image cloudOneImage = m_cloudOne.GetComponent<Image>();
        Image cloudTwoImage = m_cloudTwo.GetComponent<Image>();
        if (cloudOneImage != null)
            cloudOneImage.raycastTarget = false;
        if (cloudTwoImage != null)
            cloudTwoImage.raycastTarget = false;
        m_cloudInitialized = true;
        ApplyCloudOffset(0f);
    }

    private void RefreshCloudParallax()
    {
        if (!m_cloudInitialized
            || m_loopList == null
            || m_loopList.ContainerTrans == null)
        {
            return;
        }

        float contentOffset = m_loopList.ContainerTrans.anchoredPosition.y
            - m_cloudStartContentY;
        ApplyCloudOffset(contentOffset * CLOUD_PARALLAX_FACTOR);
    }

    private void ApplyCloudOffset(float offsetY)
    {
        if (!m_cloudInitialized || m_cloudHeight <= Mathf.Epsilon)
            return;

        float wrappedY = offsetY
            - Mathf.Floor(offsetY / m_cloudHeight) * m_cloudHeight;
        m_cloudOne.anchoredPosition = new Vector2(m_cloudOneX, wrappedY);
        m_cloudTwo.anchoredPosition = new Vector2(
            m_cloudTwoX,
            wrappedY - m_cloudHeight);
    }

    /// <summary>
    /// 初始化闪光点。位置、移动方向、显隐时长和缩放均在每轮重新随机。
    /// </summary>
    private void InitializeFlashEffects()
    {
        StopFlashEffects();
        if (m_flashRoot == null || m_flashRoot.childCount == 0)
            return;

        int pointCount = m_flashRoot.childCount;
        m_flashPoints = new RectTransform[pointCount];
        m_flashImages = new Image[pointCount];
        m_flashSequences = new Sequence[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            RectTransform point = m_flashRoot.GetChild(i) as RectTransform;
            Image image = point != null ? point.GetComponent<Image>() : null;
            m_flashPoints[i] = point;
            m_flashImages[i] = image;
            if (image == null)
                continue;

            image.raycastTarget = false;
            SetImageAlpha(image, 0f);
        }

        StartFlashEffects();
    }

    private void StartFlashEffects()
    {
        if (m_flashRunning
            || m_flashPoints == null
            || m_flashImages == null
            || m_flashSequences == null)
        {
            return;
        }

        m_flashRunning = true;
        for (int i = 0; i < m_flashPoints.Length; i++)
        {
            StartFlashCycle(i, Random.Range(0f, FLASH_INITIAL_DELAY_MAX));
        }
    }

    private void StartFlashCycle(int index, float delay)
    {
        if (!m_flashRunning
            || index < 0
            || index >= m_flashPoints.Length)
        {
            return;
        }

        RectTransform point = m_flashPoints[index];
        Image image = m_flashImages[index];
        if (point == null || image == null)
            return;

        float scale = Random.Range(FLASH_MIN_SCALE, FLASH_MAX_SCALE);
        point.localScale = Vector3.one * scale;
        point.anchoredPosition = GetRandomFlashPosition(point, scale);
        SetImageAlpha(image, 0f);

        float fadeInDuration = Random.Range(0.45f, 0.8f);
        float visibleDuration = Random.Range(1.2f, 2.8f);
        float fadeOutDuration = Random.Range(0.55f, 0.9f);
        float movementDuration = fadeInDuration
            + visibleDuration
            + fadeOutDuration;
        Vector2 targetPosition = GetRandomFlashTargetPosition(point, scale);

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        if (delay > 0f)
            sequence.AppendInterval(delay);

        sequence.Append(
                point.DOAnchorPos(targetPosition, movementDuration)
                    .SetEase(Ease.InOutSine))
            .Join(image.DOFade(1f, fadeInDuration)
                .SetEase(Ease.InOutSine))
            .Insert(
                delay + fadeInDuration + visibleDuration,
                image.DOFade(0f, fadeOutDuration)
                    .SetEase(Ease.InOutSine))
            .OnComplete(() =>
            {
                if (m_flashSequences != null
                    && index < m_flashSequences.Length)
                {
                    m_flashSequences[index] = null;
                }

                if (m_flashRunning)
                    StartFlashCycle(index, Random.Range(0.1f, 0.6f));
            });
        m_flashSequences[index] = sequence;
    }

    private Vector2 GetRandomFlashPosition(RectTransform point, float scale)
    {
        Rect area = m_flashRoot.rect;
        float halfWidth = point.rect.width * scale * 0.5f;
        float halfHeight = point.rect.height * scale * 0.5f;
        float minX = area.xMin + halfWidth;
        float maxX = area.xMax - halfWidth;
        float minY = area.yMin + halfHeight;
        float maxY = area.yMax - halfHeight;
        float x = minX < maxX ? Random.Range(minX, maxX) : area.center.x;
        float y = minY < maxY ? Random.Range(minY, maxY) : area.center.y;
        return new Vector2(x, y);
    }

    private Vector2 GetRandomFlashTargetPosition(
        RectTransform point,
        float scale)
    {
        Rect area = m_flashRoot.rect;
        float halfWidth = point.rect.width * scale * 0.5f;
        float halfHeight = point.rect.height * scale * 0.5f;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(80f, 220f);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
            * distance;
        Vector2 target = point.anchoredPosition + offset;
        target.x = Mathf.Clamp(
            target.x,
            area.xMin + halfWidth,
            area.xMax - halfWidth);
        target.y = Mathf.Clamp(
            target.y,
            area.yMin + halfHeight,
            area.yMax - halfHeight);
        return target;
    }

    private void StopFlashEffects()
    {
        m_flashRunning = false;
        if (m_flashSequences != null)
        {
            for (int i = 0; i < m_flashSequences.Length; i++)
            {
                Sequence sequence = m_flashSequences[i];
                if (sequence != null && sequence.IsActive())
                    sequence.Kill();
                m_flashSequences[i] = null;
            }
        }

        if (m_flashImages == null)
            return;

        for (int i = 0; i < m_flashImages.Length; i++)
        {
            if (m_flashImages[i] != null)
                SetImageAlpha(m_flashImages[i], 0f);
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
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
        RefreshHead();
        m_loopList.RefreshAllShownItem();
        RefreshLevelLine();
    }

    /// <summary>
    /// 注册顶部栏按钮，重复打开页面时不会叠加监听。
    /// </summary>
    private void RegisterHeadButtons()
    {
        UnregisterHeadButtons();
        if (m_backBtn != null)
            m_backBtn.onClick.AddListener(OnBackClick);
        if (m_settingBtn != null)
            m_settingBtn.onClick.AddListener(OnSettingClick);
    }

    private void UnregisterHeadButtons()
    {
        if (m_backBtn != null)
            m_backBtn.onClick.RemoveListener(OnBackClick);
        if (m_settingBtn != null)
            m_settingBtn.onClick.RemoveListener(OnSettingClick);
    }

    /// <summary>
    /// 刷新玩家信息、主线进度和资源数量。
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

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    public override void OnRelease()
    {
        m_listInitialized = false;
        m_levelLineInitialized = false;
        ClearLevelLine();
        StopFlashEffects();
        m_cloudInitialized = false;
        UnregisterHeadButtons();
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
    }
}

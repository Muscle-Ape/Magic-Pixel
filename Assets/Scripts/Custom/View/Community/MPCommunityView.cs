using DG.Tweening;
using HQ.UIManager;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 社区公开自定义关卡列表页面。
/// </summary>
[Component("MPCommunityView")]
public class MPCommunityView : AWindow
{
    protected override bool ShouldAdaptToNotchScreen()
    {
        return false;
    }

    private const int PAGE_SIZE = 10;
    private const int PREFETCH_REMAINING_COUNT = 4;
    private const string LEVEL_ITEM_NAME = "MPCommunityLevelItem";
    private const string ALL_LEVELS_LABEL = "All Levels";
    private const string LIKED_LABEL = "Liked";
    private const float TAB_MOVE_DURATION = 0.28f;
    private const float TAB_TEXT_FADE_DURATION = 0.12f;
    private const float ALL_LEVELS_ANGLE = -5f;
    private const float LIKED_ANGLE = 5f;

    private enum CommunityTab
    {
        AllLevels,
        Liked,
    }

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

    [TransformPath("View/Tab/AllLevels")]
    private Image m_allLevelsTabImage;

    [TransformPath("View/Tab/AllLevels/Text")]
    private TMP_Text m_allLevelsTabText;

    [TransformPath("View/Tab/Liked")]
    private Image m_likedTabImage;

    [TransformPath("View/Tab/Liked/Text")]
    private TMP_Text m_likedTabText;

    [TransformPath("View/Tab/Select")]
    private RectTransform m_tabSelect;

    [TransformPath("View/Tab/Select/Text")]
    private TMP_Text m_tabSelectText;

    [TransformPath("View/Levels")]
    private LoopGridView m_levelGrid;

    [TransformPath("View/Loading")]
    private RectTransform m_loading;

    [TransformPath("View/EmptyTip")]
    private RectTransform m_emptyTip;

    [TransformPath("View/RetryBtn")]
    private Button m_retryBtn;

    private readonly List<MPCustomLevelPublicRecord> m_levelRecords =
        new List<MPCustomLevelPublicRecord>();
    private readonly HashSet<string> m_loadedLevelIds =
        new HashSet<string>(StringComparer.Ordinal);

    private Button m_allLevelsTabBtn;
    private Button m_likedTabBtn;
    private Sequence m_tabSequence;
    private CancellationTokenSource m_listCancellation;
    private CommunityTab m_selectedTab = CommunityTab.AllLevels;
    private string m_nextCursor = string.Empty;
    private float m_selectVerticalOffset;
    private bool m_hasMore = true;
    private bool m_isLoading;
    private bool m_initialRequestCompleted;
    private bool m_lastLoadFailed;
    private bool m_listInitialized;
    private bool m_initialized;
    private bool m_levelAlphaRefreshScheduled;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        InitializeTabButtons();
        RegisterButtons();
        RegisterCommunityEvents();

        if (m_levelGrid != null && !m_levelGrid.IsListViewInited)
            m_levelGrid.InitGridView(0, OnGetItemByRowColumn);
        m_listInitialized = m_levelGrid != null && m_levelGrid.IsListViewInited;
        RegisterLevelScrollFade();

        m_selectedTab = CommunityTab.AllLevels;
        m_selectVerticalOffset = GetTabSelectVerticalOffset();
        ApplyTabState(false);
        RefreshHead();
        m_initialized = true;
        ResetAndLoadFirstPage();
    }

    public override void OnFocus(bool focus)
    {
        if (!focus || !m_initialized)
            return;

        RefreshHead();
        if (m_listInitialized)
        {
            m_levelGrid.RefreshAllShownItem();
            ScheduleLevelItemAlphaRefresh();
        }
    }

    public override void OnRelease()
    {
        MPNoNetworkPop.DismissLevelEntry(this);
        m_initialized = false;
        UnregisterButtons();
        UnregisterCommunityEvents();
        UnregisterLevelScrollFade();
        CancelScheduledLevelItemAlphaRefresh();
        KillTabSequence();
        CancelListRequest();
        base.OnRelease();
    }

    private void InitializeTabButtons()
    {
        m_allLevelsTabBtn = EnsureTabButton(m_allLevelsTabImage);
        m_likedTabBtn = EnsureTabButton(m_likedTabImage);
    }

    private static Button EnsureTabButton(Image targetImage)
    {
        if (targetImage == null)
            return null;

        Button button = targetImage.GetComponent<Button>();
        if (button == null)
            button = targetImage.gameObject.AddComponent<Button>();

        button.targetGraphic = targetImage;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private void RegisterButtons()
    {
        UnregisterButtons();
        RegisterButton(m_backBtn, OnBackClick);
        RegisterButton(m_settingBtn, OnSettingClick);
        RegisterButton(m_retryBtn, OnRetryClick);
        RegisterButton(m_allLevelsTabBtn, OnAllLevelsClick);
        RegisterButton(m_likedTabBtn, OnLikedClick);
    }

    private void UnregisterButtons()
    {
        UnregisterButton(m_backBtn, OnBackClick);
        UnregisterButton(m_settingBtn, OnSettingClick);
        UnregisterButton(m_retryBtn, OnRetryClick);
        UnregisterButton(m_allLevelsTabBtn, OnAllLevelsClick);
        UnregisterButton(m_likedTabBtn, OnLikedClick);
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

    private void RefreshHead()
    {
        if (m_coinText != null)
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        if (m_diamondText != null)
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        if (m_playerNameText != null)
        {
            string playerName = MPLoginManager.Instance.PlayerName;
            m_playerNameText.text = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
        }

        int latestLevelIndex = Mathf.Max(0, MPUser.instance.GetMainLevlPassIndex());
        if (m_playerLevelText != null)
            m_playerLevelText.text = $"LEVEL {latestLevelIndex + 1}";

        if (m_playerLevelFill != null)
        {
            int levelCount = MPDataManager.Instance.m_mainLevelModel?.blockInfos?.Count ?? 0;
            m_playerLevelFill.fillAmount = levelCount <= 1
                ? 0f
                : Mathf.Clamp01(latestLevelIndex / (float)(levelCount - 1));
        }
    }

    private void OnAllLevelsClick()
    {
        SwitchTab(CommunityTab.AllLevels);
    }

    private void OnLikedClick()
    {
        SwitchTab(CommunityTab.Liked);
    }

    private void SwitchTab(CommunityTab targetTab)
    {
        if (m_selectedTab == targetTab)
            return;

        CommunityTab previousTab = m_selectedTab;
        m_selectedTab = targetTab;
        ApplyTabState(true, previousTab);
        ResetAndLoadFirstPage();
    }

    private void ApplyTabState(bool animated, CommunityTab previousTab = CommunityTab.AllLevels)
    {
        KillTabSequence();
        RectTransform targetTab = GetTabRect(m_selectedTab);
        if (targetTab == null || m_tabSelect == null)
            return;

        Vector2 targetPosition = new Vector2(
            targetTab.anchoredPosition.x,
            targetTab.anchoredPosition.y + m_selectVerticalOffset);
        float targetAngle = m_selectedTab == CommunityTab.AllLevels
            ? ALL_LEVELS_ANGLE
            : LIKED_ANGLE;
        string targetLabel = m_selectedTab == CommunityTab.AllLevels
            ? ALL_LEVELS_LABEL
            : LIKED_LABEL;

        if (!animated)
        {
            m_tabSelect.anchoredPosition = targetPosition;
            m_tabSelect.localEulerAngles = new Vector3(0f, 0f, targetAngle);
            if (m_tabSelectText != null)
            {
                m_tabSelectText.text = targetLabel;
                m_tabSelectText.alpha = 1f;
            }
            SetTabTextAlpha(m_selectedTab, 0f);
            SetTabTextAlpha(GetOtherTab(m_selectedTab), 1f);
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);
        m_tabSequence = sequence;

        sequence.Join(m_tabSelect.DOAnchorPos(targetPosition, TAB_MOVE_DURATION)
            .SetEase(Ease.Linear));
        sequence.Join(m_tabSelect.DOLocalRotate(
                new Vector3(0f, 0f, targetAngle),
                TAB_MOVE_DURATION)
            .SetEase(Ease.OutBack));
        TMP_Text selectedTabText = GetTabText(m_selectedTab);
        TMP_Text previousTabText = GetTabText(previousTab);
        if (selectedTabText != null)
            sequence.Join(selectedTabText.DOFade(0f, TAB_MOVE_DURATION * 0.75f));
        if (previousTabText != null)
            sequence.Join(previousTabText.DOFade(1f, TAB_MOVE_DURATION * 0.75f));

        if (m_tabSelectText != null)
        {
            sequence.Join(m_tabSelectText.DOFade(0f, TAB_TEXT_FADE_DURATION)
                .OnComplete(() =>
                {
                    if (m_tabSelectText != null)
                        m_tabSelectText.text = targetLabel;
                }));
            sequence.Insert(
                TAB_TEXT_FADE_DURATION,
                m_tabSelectText.DOFade(1f, TAB_TEXT_FADE_DURATION));
        }

        sequence.OnComplete(() =>
        {
            if (m_tabSequence == sequence)
                m_tabSequence = null;
        });
        sequence.OnKill(() =>
        {
            if (m_tabSequence == sequence)
                m_tabSequence = null;
        });
    }

    private float GetTabSelectVerticalOffset()
    {
        RectTransform allLevelsRect = GetTabRect(CommunityTab.AllLevels);
        if (m_tabSelect == null || allLevelsRect == null)
            return -40f;

        return m_tabSelect.anchoredPosition.y - allLevelsRect.anchoredPosition.y;
    }

    private RectTransform GetTabRect(CommunityTab tab)
    {
        Image image = tab == CommunityTab.AllLevels ? m_allLevelsTabImage : m_likedTabImage;
        return image == null ? null : image.rectTransform;
    }

    private TMP_Text GetTabText(CommunityTab tab)
    {
        return tab == CommunityTab.AllLevels ? m_allLevelsTabText : m_likedTabText;
    }

    private void SetTabTextAlpha(CommunityTab tab, float alpha)
    {
        TMP_Text text = GetTabText(tab);
        if (text != null)
            text.alpha = alpha;
    }

    private static CommunityTab GetOtherTab(CommunityTab tab)
    {
        return tab == CommunityTab.AllLevels ? CommunityTab.Liked : CommunityTab.AllLevels;
    }

    private void KillTabSequence()
    {
        Sequence sequence = m_tabSequence;
        m_tabSequence = null;
        if (sequence != null && sequence.IsActive())
            sequence.Kill();
        if (m_tabSelect != null)
            m_tabSelect.DOKill();
        if (m_tabSelectText != null)
            m_tabSelectText.DOKill();
        if (m_allLevelsTabText != null)
            m_allLevelsTabText.DOKill();
        if (m_likedTabText != null)
            m_likedTabText.DOKill();
    }

    /// <summary>
    /// 清理当前 Tab 的分页状态并从第一页开始加载。
    /// </summary>
    private void ResetAndLoadFirstPage()
    {
        CancelListRequest();
        m_listCancellation = new CancellationTokenSource();

        m_levelRecords.Clear();
        m_loadedLevelIds.Clear();
        m_nextCursor = string.Empty;
        m_hasMore = true;
        m_isLoading = false;
        m_initialRequestCompleted = false;
        m_lastLoadFailed = false;

        if (m_listInitialized)
        {
            m_levelGrid.SetListItemCount(0, true);
            m_levelGrid.RefreshAllShownItem();
        }

        RefreshLoadState();
        RequestNextPage();
    }

    private void RequestNextPage()
    {
        if (!m_listInitialized || m_isLoading || !m_hasMore || m_listCancellation == null)
            return;

        m_isLoading = true;
        m_lastLoadFailed = false;
        RefreshLoadState();
        CommunityTab requestTab = m_selectedTab;
        _ = LoadNextPageAsync(m_listCancellation, requestTab);
    }

    private async Task LoadNextPageAsync(
        CancellationTokenSource cancellation,
        CommunityTab requestTab)
    {
        CancellationToken cancellationToken = cancellation.Token;
        bool requestAnotherLikedPage = false;
        try
        {
            string sortType = requestTab == CommunityTab.Liked
                ? MPCustomLevelPublishConstants.SORT_LIKED
                : MPCustomLevelPublishConstants.SORT_LATEST;
            MPCustomLevelListResult result = await MPCustomLevelPublishManager.Instance.GetListAsync(
                sortType,
                PAGE_SIZE,
                m_nextCursor,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (m_listCancellation != cancellation || m_selectedTab != requestTab)
                return;
            if (result == null || !result.success)
            {
                throw new InvalidOperationException(result == null
                    ? "公开关卡列表返回为空。"
                    : $"公开关卡列表加载失败：{result.message}");
            }

            int previousCount = m_levelRecords.Count;
            AppendPage(result.items, requestTab);
            m_nextCursor = result.nextCursor ?? string.Empty;
            m_hasMore = !string.IsNullOrEmpty(m_nextCursor);
            m_initialRequestCompleted = true;

            m_levelGrid.SetListItemCount(m_levelRecords.Count, false);
            m_levelGrid.RefreshAllShownItem();

            // 兼容尚未部署 Liked 服务端筛选的环境：本页没有喜欢项时继续按游标查找。
            requestAnotherLikedPage = requestTab == CommunityTab.Liked &&
                                      m_levelRecords.Count == previousCount &&
                                      m_hasMore;
        }
        catch (OperationCanceledException)
        {
            // 页面关闭或切换 Tab 时取消，不回写旧页面。
        }
        catch (Exception exception)
        {
            if (m_listCancellation == cancellation && m_selectedTab == requestTab)
            {
                m_initialRequestCompleted = true;
                m_lastLoadFailed = true;
                Debug.LogError(
                    $"[MPCommunityView] 加载公开关卡失败：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
            }
        }
        finally
        {
            if (m_listCancellation == cancellation && m_selectedTab == requestTab)
            {
                m_isLoading = false;
                RefreshLoadState();
                if (requestAnotherLikedPage)
                    RequestNextPage();
            }
        }
    }

    private void AppendPage(List<MPCustomLevelPublicRecord> records, CommunityTab requestTab)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            MPCustomLevelPublicRecord record = records[i];
            if (record == null || !record.IsPublished || string.IsNullOrEmpty(record.publicLevelId))
                continue;
            if (requestTab == CommunityTab.Liked && !record.likedByCurrentPlayer)
                continue;

            if (m_loadedLevelIds.Add(record.publicLevelId))
                m_levelRecords.Add(record);
        }
    }

    private LoopGridViewItem OnGetItemByRowColumn(
        LoopGridView view,
        int index,
        int row,
        int column)
    {
        if (index < 0 || index >= m_levelRecords.Count)
            return null;

        LoopGridViewItem item = view.NewListViewItem(LEVEL_ITEM_NAME);
        if (item == null)
            return null;

        MPCommunityLevelItem levelItem = item.GetComponent<MPCommunityLevelItem>();
        if (levelItem == null)
            levelItem = item.gameObject.AddComponent<MPCommunityLevelItem>();

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            levelItem.Initialize();
        }

        levelItem.Refresh(m_levelRecords[index]);
        levelItem.ApplyLayout(index);
        // LoopGridView 会在本回调返回后才设置复用 Item 的最终位置，不能在这里读取坐标。
        ScheduleLevelItemAlphaRefresh();
        if (m_hasMore && index >= m_levelRecords.Count - PREFETCH_REMAINING_COUNT)
            RequestNextPage();

        return item;
    }

    private void RegisterLevelScrollFade()
    {
        ScrollRect scrollRect = m_levelGrid?.ScrollRect;
        if (scrollRect == null)
            return;

        scrollRect.onValueChanged.RemoveListener(OnLevelScrollValueChanged);
        scrollRect.onValueChanged.AddListener(OnLevelScrollValueChanged);
    }

    private void UnregisterLevelScrollFade()
    {
        ScrollRect scrollRect = m_levelGrid?.ScrollRect;
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnLevelScrollValueChanged);
    }

    private void OnLevelScrollValueChanged(Vector2 _)
    {
        RefreshShownLevelItemAlpha();
    }

    /// <summary>
    /// 等 LoopGridView 完成本帧的 Item 定位后、Canvas 渲染前再统一计算透明度。
    /// 多个 Item 同帧进入时只注册一次回调。
    /// </summary>
    private void ScheduleLevelItemAlphaRefresh()
    {
        if (m_levelAlphaRefreshScheduled)
            return;

        m_levelAlphaRefreshScheduled = true;
        Canvas.willRenderCanvases -= OnCanvasWillRenderForLevelAlpha;
        Canvas.willRenderCanvases += OnCanvasWillRenderForLevelAlpha;
    }

    private void OnCanvasWillRenderForLevelAlpha()
    {
        CancelScheduledLevelItemAlphaRefresh();
        if (m_initialized)
            RefreshShownLevelItemAlpha();
    }

    private void CancelScheduledLevelItemAlphaRefresh()
    {
        Canvas.willRenderCanvases -= OnCanvasWillRenderForLevelAlpha;
        m_levelAlphaRefreshScheduled = false;
    }

    /// <summary>
    /// 只遍历 LoopGridView 当前激活的池对象，滚动停止后不会产生额外刷新开销。
    /// </summary>
    private void RefreshShownLevelItemAlpha()
    {
        ScrollRect scrollRect = m_levelGrid?.ScrollRect;
        RectTransform content = m_levelGrid?.ContainerTrans;
        RectTransform levelsRect = m_levelGrid == null
            ? null
            : m_levelGrid.transform as RectTransform;
        if (scrollRect == null || content == null || levelsRect == null)
            return;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (!child.gameObject.activeInHierarchy)
                continue;

            MPCommunityLevelItem levelItem = child.GetComponent<MPCommunityLevelItem>();
            if (levelItem != null)
                levelItem.RefreshLevelsAlpha(levelsRect);
        }
    }

    private void RefreshLoadState()
    {
        int levelCount = m_levelRecords.Count;
        if (m_loading != null)
            m_loading.gameObject.SetActive(m_isLoading && levelCount == 0);
        if (m_emptyTip != null)
        {
            bool showEmpty = m_initialRequestCompleted &&
                             !m_lastLoadFailed &&
                             !m_isLoading &&
                             levelCount == 0;
            m_emptyTip.gameObject.SetActive(showEmpty);
        }
        if (m_retryBtn != null)
        {
            m_retryBtn.gameObject.SetActive(m_lastLoadFailed);
            m_retryBtn.interactable = m_lastLoadFailed && !m_isLoading;
        }
    }

    private void OnRetryClick()
    {
        RequestNextPage();
    }

    private void OnBackClick()
    {
        DestroyWindow();
    }

    private void OnSettingClick()
    {
        UIManager.Inst.ShowWindow<MPSettingPop>(null, true, UILayer.Top);
    }

    private void RegisterCommunityEvents()
    {
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged -= OnCommunityLikeStateChanged;
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged += OnCommunityLikeStateChanged;
    }

    private void UnregisterCommunityEvents()
    {
        MPCustomLevelPublishManager.Instance.CommunityLikeStateChanged -= OnCommunityLikeStateChanged;
    }

    /// <summary>
    /// 取消点赞得到服务端最终确认后，再从 Liked Tab 移除，避免 Item 回收中断请求。
    /// </summary>
    private void OnCommunityLikeStateChanged(
        MPCustomLevelPublicRecord record,
        bool isFinal)
    {
        if (!isFinal ||
            !m_initialized ||
            m_selectedTab != CommunityTab.Liked ||
            record == null)
        {
            return;
        }

        int index = m_levelRecords.FindIndex(item =>
            item != null && item.publicLevelId == record.publicLevelId);
        if (record.likedByCurrentPlayer)
        {
            // 页面可能在乐观取消点赞期间重新打开；失败回滚后需要把关卡补回列表。
            if (index >= 0)
                return;

            m_levelRecords.Insert(0, record);
            m_loadedLevelIds.Add(record.publicLevelId);
        }
        else
        {
            if (index < 0)
                return;

            m_levelRecords.RemoveAt(index);
            m_loadedLevelIds.Remove(record.publicLevelId);
        }

        if (m_listInitialized)
        {
            m_levelGrid.SetListItemCount(m_levelRecords.Count, false);
            m_levelGrid.RefreshAllShownItem();
        }

        RefreshLoadState();
    }

    private void CancelListRequest()
    {
        if (m_listCancellation == null)
            return;

        m_listCancellation.Cancel();
        m_listCancellation.Dispose();
        m_listCancellation = null;
        m_isLoading = false;
    }
}

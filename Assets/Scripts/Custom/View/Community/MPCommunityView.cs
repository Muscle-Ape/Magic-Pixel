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
    private const int PAGE_SIZE = 10;
    private const int PREFETCH_REMAINING_COUNT = 3;
    private const string LEVEL_ITEM_NAME = "MPCommunityLevelItem";

    [TransformPath("View/Up/BackBtn")]
    private Button m_backBtn;

    [TransformPath("View/Up/SettingBtn")]
    private Button m_settingBtn;

    [TransformPath("View/Up/Coin/Count")]
    private TMP_Text m_coinText;

    [TransformPath("View/Up/Diamond/Count")]
    private TMP_Text m_diamondText;

    [TransformPath("View/Levels")]
    private LoopListView2 m_levelList;

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

    private CancellationTokenSource m_listCancellation;
    private string m_nextCursor = string.Empty;
    private bool m_hasMore = true;
    private bool m_isLoading;
    private bool m_initialRequestCompleted;
    private bool m_lastLoadFailed;
    private bool m_listInitialized;

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_levelList.InitListView(0, OnGetItemByIndex);
        m_listInitialized = true;

        RegisterButtons();
        RefreshCurrency();
        ResetAndLoadFirstPage();
    }

    public override void OnFocus(bool focus)
    {
        if (focus)
        {
            RefreshCurrency();
        }
    }

    public override void OnRelease()
    {
        if (m_backBtn != null)
        {
            m_backBtn.onClick.RemoveListener(OnBackClick);
        }

        if (m_settingBtn != null)
        {
            m_settingBtn.onClick.RemoveListener(OnSettingClick);
        }

        if (m_retryBtn != null)
        {
            m_retryBtn.onClick.RemoveListener(OnRetryClick);
        }

        CancelListRequest();
        base.OnRelease();
    }

    private void RegisterButtons()
    {
        m_backBtn.onClick.RemoveListener(OnBackClick);
        m_backBtn.onClick.AddListener(OnBackClick);

        m_settingBtn.onClick.RemoveListener(OnSettingClick);
        m_settingBtn.onClick.AddListener(OnSettingClick);

        m_retryBtn.onClick.RemoveListener(OnRetryClick);
        m_retryBtn.onClick.AddListener(OnRetryClick);
    }

    private void RefreshCurrency()
    {
        if (m_coinText != null)
        {
            m_coinText.text = MPUser.instance.GetCoins().ToString();
        }

        if (m_diamondText != null)
        {
            m_diamondText.text = MPUser.instance.GetDiamond().ToString();
        }
    }

    /// <summary>
    /// 清理已有分页状态并加载最新发布的第一页。
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
            m_levelList.SetListItemCount(0, true);
        }

        RefreshLoadState();
        RequestNextPage();
    }

    /// <summary>
    /// 在没有其他分页请求时加载下一页。
    /// </summary>
    private void RequestNextPage()
    {
        if (!m_listInitialized || m_isLoading || !m_hasMore || m_listCancellation == null)
        {
            return;
        }

        m_isLoading = true;
        m_lastLoadFailed = false;
        RefreshLoadState();
        _ = LoadNextPageAsync(m_listCancellation);
    }

    private async Task LoadNextPageAsync(CancellationTokenSource cancellation)
    {
        CancellationToken cancellationToken = cancellation.Token;
        try
        {
            MPCustomLevelListResult result = await MPCustomLevelPublishManager.Instance.GetListAsync(
                MPCustomLevelPublishConstants.SORT_LATEST,
                PAGE_SIZE,
                m_nextCursor,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (m_listCancellation != cancellation)
            {
                return;
            }

            if (result == null || !result.success)
            {
                throw new InvalidOperationException(result == null
                    ? "公开关卡列表返回为空。"
                    : $"公开关卡列表加载失败：{result.message}");
            }

            AppendPage(result.items);
            m_nextCursor = result.nextCursor ?? string.Empty;
            m_hasMore = !string.IsNullOrEmpty(m_nextCursor);
            m_initialRequestCompleted = true;

            m_levelList.SetListItemCount(m_levelRecords.Count, false);
            m_levelList.RefreshAllShownItem();
        }
        catch (OperationCanceledException)
        {
            // 页面关闭或重新加载时取消，不需要显示错误。
        }
        catch (Exception exception)
        {
            if (m_listCancellation == cancellation)
            {
                m_initialRequestCompleted = true;
                m_lastLoadFailed = true;
                Debug.LogError(
                    $"[MPCommunityView] 加载公开关卡失败：{MPCustomLevelPublishManager.FormatExceptionForLog(exception)}");
            }
        }
        finally
        {
            if (m_listCancellation == cancellation)
            {
                m_isLoading = false;
                RefreshLoadState();
            }
        }
    }

    private void AppendPage(List<MPCustomLevelPublicRecord> records)
    {
        if (records == null)
        {
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            MPCustomLevelPublicRecord record = records[i];
            if (record == null || !record.IsPublished || string.IsNullOrEmpty(record.publicLevelId))
            {
                continue;
            }

            if (m_loadedLevelIds.Add(record.publicLevelId))
            {
                m_levelRecords.Add(record);
            }
        }
    }

    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 view, int index)
    {
        if (index < 0 || index >= m_levelRecords.Count)
        {
            return null;
        }

        LoopListViewItem2 item = view.NewListViewItem(LEVEL_ITEM_NAME);
        MPCommunityLevelItem levelItem = item.GetComponent<MPCommunityLevelItem>();
        if (levelItem == null)
        {
            levelItem = item.gameObject.AddComponent<MPCommunityLevelItem>();
        }

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            levelItem.Initialize();
        }

        levelItem.Refresh(m_levelRecords[index]);

        if (m_hasMore && index >= m_levelRecords.Count - PREFETCH_REMAINING_COUNT)
        {
            RequestNextPage();
        }

        return item;
    }

    private void RefreshLoadState()
    {
        int levelCount = m_levelRecords.Count;
        if (m_loading != null)
        {
            m_loading.gameObject.SetActive(m_isLoading && levelCount == 0);
        }

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

    private void CancelListRequest()
    {
        if (m_listCancellation == null)
        {
            return;
        }

        m_listCancellation.Cancel();
        m_listCancellation.Dispose();
        m_listCancellation = null;
        m_isLoading = false;
    }
}

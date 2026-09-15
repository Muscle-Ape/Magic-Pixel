using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>两份原始快照只供展示，确认回调负责带写锁提交后再应用本地。</summary>
public sealed class MPAssetComparisonPopUIMsgData : UIMsgData
{
    public MPUserCloudSnapshot localUser, cloudUser;
    public MPCustomLevelCloudSnapshot localCustom, cloudCustom;
    public bool isGuestAccountChoice;

    // VIP 与 No Ads 目前还没有接入用户存档，先由弹窗数据提供扩展入口。
    // 未赋值时默认按未拥有展示，不会影响现有云存档结构。
    public bool localVipOwned, cloudVipOwned;
    public bool localNoAdsOwned, cloudNoAdsOwned;

    public Func<bool, CancellationToken, Task<bool>> confirmAsync;
    public Action onChoose;
    public Action onCloudRefreshed;
    public Action cancelPresentation;
    public readonly TaskCompletionSource<bool> completion =
        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
}

[Component("MPAssetComparisonPop")]
public class MPAssetComparisonPop : AWindow
{
    private const int COMPARISON_ITEM_COUNT = 10;

    [TransformPath("View/Window/Panel/Local/UpdateTime")] private TMP_Text m_localUpdateTime;
    [TransformPath("View/Window/Panel/Cloud/UpdateTime")] private TMP_Text m_cloudUpdateTime;
    [TransformPath("View/Window/Panel/Content")] private RectTransform m_content;
    [TransformPath("View/Window/LocalBtn")] private Button m_localBtn;
    [TransformPath("View/Window/CloudBtn")] private Button m_cloudBtn;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeBtn;

    private readonly List<ComparisonItemView> m_itemViews = new List<ComparisonItemView>(COMPARISON_ITEM_COUNT);
    private MPAssetComparisonPopUIMsgData m_data;
    private CancellationTokenSource m_lifetime;
    private bool m_confirming;
    private bool m_resolved;
    private MPSecondConfirmationPop m_confirmation;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static Task<bool> ShowAsync(MPAssetComparisonPopUIMsgData data)
    {
        if (data == null)
        {
            return Task.FromResult(false);
        }

        MPAssetComparisonPop popup = UIManager.Inst.ShowWindow<MPAssetComparisonPop>(data, true, UILayer.Top);
        if (popup == null)
        {
            data.completion.TrySetResult(false);
        }

        return data.completion.Task;
    }

    public override void OnCreate()
    {
        m_lifetime = new CancellationTokenSource();
        BuildItemViews();
        MPReleaseFeatures.ApplyAssetComparison(transform);

        m_localBtn.onClick.RemoveListener(ChooseLocal);
        m_cloudBtn.onClick.RemoveListener(ChooseCloud);
        m_closeBtn.onClick.RemoveListener(Close);
        m_localBtn.onClick.AddListener(ChooseLocal);
        m_cloudBtn.onClick.AddListener(ChooseCloud);
        m_closeBtn.onClick.AddListener(Close);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPAssetComparisonPopUIMsgData>();
        if (m_data != null)
        {
            m_data.onCloudRefreshed = RefreshAfterCloudChange;
            m_data.cancelPresentation = CancelPresentation;
        }

        RefreshDisplay();
        if (m_data?.isGuestAccountChoice == true)
        {
            TMP_Text desc = transform.Find("View/Window/Desc")?.GetComponent<TMP_Text>();
            if (desc != null)
                desc.text = "Local: current guest progress. Cloud: existing account progress. Your guest save will be kept separately.";
        }
    }

    /// <summary>按 Content 中的预制顺序缓存十个对比 Item，不在运行时创建 UI。</summary>
    private void BuildItemViews()
    {
        m_itemViews.Clear();
        if (m_content == null)
        {
            return;
        }

        for (int i = 0; i < m_content.childCount; i++)
        {
            Transform item = m_content.GetChild(i);
            if (item == null || item.name != "Item")
            {
                continue;
            }

            ComparisonItemView itemView = ComparisonItemView.Create(item);
            if (itemView != null)
            {
                m_itemViews.Add(itemView);
            }
        }

        if (m_itemViews.Count != COMPARISON_ITEM_COUNT)
        {
            Debug.LogWarning($"[MPAssetComparisonPop] 需要 {COMPARISON_ITEM_COUNT} 个有效 Item，当前找到 {m_itemViews.Count} 个。");
        }
    }

    private void RefreshDisplay()
    {
        MPUserCloudSnapshot localUser = m_data?.localUser;
        MPUserCloudSnapshot cloudUser = m_data?.cloudUser;
        MPCustomLevelCloudSnapshot localCustom = m_data?.localCustom;
        MPCustomLevelCloudSnapshot cloudCustom = m_data?.cloudCustom;

        m_localUpdateTime.text = FormatUpdateTime(localUser, localCustom);
        m_cloudUpdateTime.text = FormatUpdateTime(cloudUser, cloudCustom);

        List<ComparisonItemData> items = CreateComparisonItems(localUser, cloudUser, localCustom, cloudCustom);
        int visibleCount = Mathf.Min(m_itemViews.Count, items.Count);
        for (int i = 0; i < m_itemViews.Count; i++)
        {
            bool visible = i < visibleCount;
            m_itemViews[i].SetActive(visible);
            if (visible)
            {
                m_itemViews[i].Refresh(items[i]);
            }
        }

        bool valid = localUser != null && cloudUser != null && localCustom != null && cloudCustom != null &&
                     localUser.schemaVersion == cloudUser.schemaVersion &&
                     localCustom.schemaVersion == cloudCustom.schemaVersion;
        SetChoiceInteractable(valid);
        m_closeBtn.interactable = !m_confirming;
    }

    private List<ComparisonItemData> CreateComparisonItems(MPUserCloudSnapshot localUser,
        MPUserCloudSnapshot cloudUser, MPCustomLevelCloudSnapshot localCustom,
        MPCustomLevelCloudSnapshot cloudCustom)
    {
        MPUserAssetsSnapshot localAssets = localUser?.assets ?? new MPUserAssetsSnapshot();
        MPUserAssetsSnapshot cloudAssets = cloudUser?.assets ?? new MPUserAssetsSnapshot();

        var items = new List<ComparisonItemData>(COMPARISON_ITEM_COUNT)
        {
            ComparisonItemData.Number("Main Level", localUser?.mainLevel?.passIndex ?? 0,
                cloudUser?.mainLevel?.passIndex ?? 0),
            ComparisonItemData.Number("Big Maps", localUser?.largeImageLevel?.passIndex ?? 0,
                cloudUser?.largeImageLevel?.passIndex ?? 0),
            ComparisonItemData.Number("Coins", localAssets.coins, cloudAssets.coins),
            ComparisonItemData.Number("Fluorite", localAssets.fluorite, cloudAssets.fluorite),
            ComparisonItemData.Number("Custom Levels", localCustom?.customLevel?.levels?.Count ?? 0,
                cloudCustom?.customLevel?.levels?.Count ?? 0),
            ComparisonItemData.Number("Pets", CountOwnedPets(localUser), CountOwnedPets(cloudUser)),
            ComparisonItemData.Number("Hints", localAssets.hintProps, cloudAssets.hintProps),
            ComparisonItemData.Number("Life Refills", localAssets.loveRecoverProps, cloudAssets.loveRecoverProps)
        };
        // 未开放的付费权益不参与首发版本的资产对比展示。
        if (MPReleaseFeatures.Vip && MPReleaseFeatures.InAppPurchases)
            items.Add(ComparisonItemData.Boolean("VIP", m_data?.localVipOwned ?? false, m_data?.cloudVipOwned ?? false));
        if (MPReleaseFeatures.Ads && MPReleaseFeatures.InAppPurchases)
            items.Add(ComparisonItemData.Boolean("No Ads", m_data?.localNoAdsOwned ?? false, m_data?.cloudNoAdsOwned ?? false));
        return items;
    }

    /// <summary>宠物数量包含默认宠物以及用户已经主动领取的宠物。</summary>
    private static int CountOwnedPets(MPUserCloudSnapshot user)
    {
        List<string> claimedIds = user?.rewardProgress?.claimedPetIds;
        List<MPPetConfig> configs = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (configs == null || configs.Count == 0)
        {
            return CountDistinctIds(claimedIds);
        }

        HashSet<string> claimed = new HashSet<string>(claimedIds ?? new List<string>(), StringComparer.Ordinal);
        int count = 0;
        foreach (MPPetConfig config in configs)
        {
            if (config != null && !string.IsNullOrEmpty(config.ID) &&
                (config.DefaultUnlocked || claimed.Contains(config.ID)))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountDistinctIds(List<string> ids)
    {
        if (ids == null)
        {
            return 0;
        }

        HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                distinct.Add(id);
            }
        }

        return distinct.Count;
    }

    /// <summary>展示两份快照中较晚的更新时间；设备信息取用户快照。</summary>
    private static string FormatUpdateTime(MPUserCloudSnapshot user, MPCustomLevelCloudSnapshot custom)
    {
        long ticks = Math.Max(user?.updatedAtUtcTicks ?? 0L, custom?.updatedAtUtcTicks ?? 0L);
        string device = string.IsNullOrWhiteSpace(user?.deviceModel) ? "Unknown Device" : user.deviceModel.Trim();
        if (ticks <= 0L || ticks > DateTime.MaxValue.Ticks)
        {
            return $"Unknown Date\nUnknown Time\n{device}";
        }

        DateTime localTime = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
        return localTime.ToString("MMM d, yyyy\nh:mm tt", CultureInfo.InvariantCulture) + "\n" + device;
    }

    private void RefreshAfterCloudChange()
    {
        if (this == null || IsDestoried)
        {
            return;
        }

        RefreshDisplay();
        // 写锁冲突后刷新比较内容，取消二次确认后才能重新选择存档。
        SetChoiceInteractable(false);
    }

    private void SetChoiceInteractable(bool interactable)
    {
        m_localBtn.interactable = interactable && !m_confirming;
        m_cloudBtn.interactable = interactable && !m_confirming;
    }

    private bool HasValidSnapshots()
    {
        return m_data?.localUser != null && m_data.cloudUser != null &&
               m_data.localCustom != null && m_data.cloudCustom != null &&
               m_data.localUser.schemaVersion == m_data.cloudUser.schemaVersion &&
               m_data.localCustom.schemaVersion == m_data.cloudCustom.schemaVersion;
    }

    private void ChooseLocal() => Choose(true);
    private void ChooseCloud() => Choose(false);

    /// <summary>确认选择后才提交存档；取消时返回比较页面。</summary>
    private void Choose(bool useLocal)
    {
        if (m_confirming || m_data?.confirmAsync == null || !HasValidSnapshots()) return;
        m_data.onChoose?.Invoke();
        m_confirming = true;
        SetChoiceInteractable(false);
        m_closeBtn.interactable = false;
        string chosen = useLocal ? "local" : "cloud";
        m_confirmation = MPSecondConfirmationPop.Show(
            "Replace saved progress?",
            m_data.isGuestAccountChoice
                ? (useLocal
                    ? "Use your guest progress for this account? The account's existing progress, currencies, items and custom levels will be replaced. Your original guest save will remain separate."
                    : "Use this account's existing progress? Your guest progress will remain saved separately and will be available when you return to guest mode.")
                : $"Use this {chosen} save? The other save's progress, currencies, items and custom levels will be replaced. The two saves cannot be merged. This cannot be undone in-game.",
            "Confirm",
            async token =>
            {
                if (this == null || IsDestoried || m_lifetime == null) return false;
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, m_lifetime.Token))
                {
                    bool success = await m_data.confirmAsync(useLocal, linked.Token);
                    return this != null && !IsDestoried && success;
                }
            },
            onCancel: RestoreChoice,
            onConfirmed: () =>
            {
                if (this == null || IsDestoried) return;
                m_confirmation = null;
                TaskCompletionSource<bool> completion = m_data.completion;
                m_resolved = true;
                DestroyWindow();
                completion.TrySetResult(true);
            });
        if (m_confirmation == null) RestoreChoice();
    }

    private void RestoreChoice()
    {
        if (this == null || IsDestoried) return;
        m_confirmation = null;
        m_confirming = false;
        SetChoiceInteractable(HasValidSnapshots());
        m_closeBtn.interactable = true;
    }

    private void Close()
    {
        if (m_confirming)
        {
            return;
        }

        SetChoiceInteractable(false);
        m_closeBtn.interactable = false;
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
        {
            animation.CloseWindow();
        }
        else
        {
            DestroyWindow();
        }
    }

    private void CancelPresentation()
    {
        if (this != null && !IsDestoried)
        {
            DestroyWindow();
        }
    }

    public override void OnRelease()
    {
        if (m_confirmation != null && !m_confirmation.IsDestoried)
            m_confirmation.DestroyWindow();
        m_confirmation = null;
        m_lifetime?.Cancel();
        m_lifetime?.Dispose();
        m_lifetime = null;

        m_localBtn?.onClick.RemoveListener(ChooseLocal);
        m_cloudBtn?.onClick.RemoveListener(ChooseCloud);
        m_closeBtn?.onClick.RemoveListener(Close);

        if (m_data != null)
        {
            m_data.onCloudRefreshed = null;
            m_data.cancelPresentation = null;
            if (!m_resolved)
            {
                m_data.completion.TrySetResult(false);
            }
        }

        m_itemViews.Clear();
    }

    private sealed class ComparisonItemView
    {
        private readonly Transform m_root;
        private readonly TMP_Text m_name;
        private readonly TMP_Text m_localValue;
        private readonly TMP_Text m_cloudValue;
        private readonly GameObject m_localPoint;
        private readonly GameObject m_cloudPoint;

        private ComparisonItemView(Transform root, TMP_Text name, TMP_Text localValue,
            TMP_Text cloudValue, GameObject localPoint, GameObject cloudPoint)
        {
            m_root = root;
            m_name = name;
            m_localValue = localValue;
            m_cloudValue = cloudValue;
            m_localPoint = localPoint;
            m_cloudPoint = cloudPoint;
        }

        public static ComparisonItemView Create(Transform root)
        {
            TMP_Text name = root.Find("Name")?.GetComponent<TMP_Text>();
            TMP_Text localValue = root.Find("Local")?.GetComponent<TMP_Text>();
            TMP_Text cloudValue = root.Find("Cloud")?.GetComponent<TMP_Text>();
            Transform localPoint = root.Find("Local/Point");
            Transform cloudPoint = root.Find("Cloud/Point");
            if (name == null || localValue == null || cloudValue == null || localPoint == null || cloudPoint == null)
            {
                Debug.LogWarning($"[MPAssetComparisonPop] Item 节点结构不完整：{root.name}");
                return null;
            }

            return new ComparisonItemView(root, name, localValue, cloudValue,
                localPoint.gameObject, cloudPoint.gameObject);
        }

        public void SetActive(bool active)
        {
            if (m_root.gameObject.activeSelf != active)
            {
                m_root.gameObject.SetActive(active);
            }
        }

        public void Refresh(ComparisonItemData data)
        {
            m_name.text = data.Name;
            m_localValue.text = data.LocalText;
            m_cloudValue.text = data.CloudText;
            m_localPoint.SetActive(data.ShowLocalPoint);
            m_cloudPoint.SetActive(data.ShowCloudPoint);
        }
    }

    private sealed class ComparisonItemData
    {
        public string Name { get; }
        public string LocalText { get; }
        public string CloudText { get; }
        public bool ShowLocalPoint { get; }
        public bool ShowCloudPoint { get; }

        private ComparisonItemData(string name, string localText, string cloudText,
            bool showLocalPoint, bool showCloudPoint)
        {
            Name = name;
            LocalText = localText;
            CloudText = cloudText;
            ShowLocalPoint = showLocalPoint;
            ShowCloudPoint = showCloudPoint;
        }

        public static ComparisonItemData Number(string name, int localValue, int cloudValue)
        {
            localValue = Mathf.Max(0, localValue);
            cloudValue = Mathf.Max(0, cloudValue);
            return new ComparisonItemData(name,
                localValue.ToString(CultureInfo.InvariantCulture),
                cloudValue.ToString(CultureInfo.InvariantCulture),
                localValue >= cloudValue,
                cloudValue >= localValue);
        }

        public static ComparisonItemData Boolean(string name, bool localValue, bool cloudValue)
        {
            return new ComparisonItemData(name,
                localValue ? "Owned" : "None",
                cloudValue ? "Owned" : "None",
                localValue,
                cloudValue);
        }
    }
}

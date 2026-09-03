using System;
using System.Collections.Generic;
using System.Text;
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
    public Func<bool, CancellationToken, Task<bool>> confirmAsync;
    public Action onChoose;
    public Action onCloudRefreshed;
    public Action cancelPresentation;
    public readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
}

[Component("MPAssetComparisonPop")]
public class MPAssetComparisonPop : AWindow
{
    [TransformPath("View/Window/Local")] private TMP_Text m_local;
    [TransformPath("View/Window/Cloud")] private TMP_Text m_cloud;
    [TransformPath("View/Window/Desc")] private TMP_Text m_desc;
    [TransformPath("View/Window/Status")] private TMP_Text m_status;
    [TransformPath("View/Window/LocalBtn")] private Button m_localBtn;
    [TransformPath("View/Window/CloudBtn")] private Button m_cloudBtn;
    private MPAssetComparisonPopUIMsgData m_data;
    private CancellationTokenSource m_lifetime;
    private bool m_confirming;
    private bool m_resolved;
    private MPSecondConfirmationPop m_confirmation;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static Task<bool> ShowAsync(MPAssetComparisonPopUIMsgData data)
    {
        if (data == null) return Task.FromResult(false);
        MPAssetComparisonPop popup = UIManager.Inst.ShowWindow<MPAssetComparisonPop>(data, true, UILayer.Top);
        if (popup == null) data.completion.TrySetResult(false);
        return data.completion.Task;
    }

    public override void OnCreate()
    {
        m_lifetime = new CancellationTokenSource();
        m_localBtn.onClick.AddListener(ChooseLocal);
        m_cloudBtn.onClick.AddListener(ChooseCloud);
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
    }

    private void RefreshDisplay()
    {
        bool valid = m_data?.localUser != null && m_data.cloudUser != null &&
                     m_data.localUser.schemaVersion == m_data.cloudUser.schemaVersion &&
                     m_data.localCustom?.schemaVersion == m_data.cloudCustom?.schemaVersion;
        m_localBtn.interactable = m_cloudBtn.interactable = valid;
        m_desc.text = "These saves contain conflicting changes. Choose one save. They will not be merged.";
        m_status.text = valid ? "Highlighted values differ. The other save will be replaced." : "Save versions differ. Update the game before syncing.";
        if (!valid) return;
        List<string> local = Describe(m_data.localUser, m_data.localCustom);
        List<string> cloud = Describe(m_data.cloudUser, m_data.cloudCustom);
        m_local.text = Format("LOCAL SAVE", local, cloud);
        m_cloud.text = Format("CLOUD SAVE", cloud, local);
    }

    private void RefreshAfterCloudChange()
    {
        if (this == null || IsDestoried) return;
        RefreshDisplay();
        m_localBtn.interactable = m_cloudBtn.interactable = false;
        m_status.text = "Cloud save changed. Cancel confirmation, review the new values, then choose again.";
    }

    private static List<string> Describe(MPUserCloudSnapshot user, MPCustomLevelCloudSnapshot custom)
    {
        MPUserAssetsSnapshot assets = user.assets ?? new MPUserAssetsSnapshot();
        int petCount = 0;
        List<MPPetConfig> pets = MPDataManager.Instance.m_petsModel?.petConfigs;
        if (pets != null)
            foreach (MPPetConfig pet in pets)
                if (pet != null && (pet.DefaultUnlocked || pet.TryGetUnlockRequirement(out string type, out int value) &&
                    ((type == "mainlevel" && (user.mainLevel?.passIndex ?? 0) >= value) || type == "free" || type == "default" || type == "unlocked")))
                    petCount++;
        return new List<string>
        {
            "Saved: " + DateLabel(user.updatedAtUtcTicks),
            "Device: " + (string.IsNullOrEmpty(user.deviceModel) ? "Unknown" : user.deviceModel),
            "Version: " + (user.clientVersion ?? "Unknown"),
            "Main levels: " + (user.mainLevel?.passIndex ?? 0),
            "Large levels: " + (user.largeImageLevel?.passIndex ?? 0),
            "Coins: " + Mathf.Max(0, assets.coins),
            "Diamonds: " + Mathf.Max(0, assets.diamond),
            "Hints: " + Mathf.Max(0, assets.hintProps),
            "Life items: " + Mathf.Max(0, assets.loveRecoverProps),
            "Custom levels: " + (custom?.customLevel?.levels?.Count ?? 0),
            "Pets: " + petCount,
            "VIP / Ad-free: Not configured"
        };
    }

    private static string DateLabel(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
        ? new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm 'UTC'") : "Unknown";

    private static string Format(string heading, List<string> own, List<string> other)
    {
        StringBuilder result = new StringBuilder(heading + "\n\n");
        for (int i = 0; i < own.Count; i++)
        {
            bool changed = own[i] != other[i];
            if (changed) result.Append("<color=#FFBB55>");
            result.Append(own[i]);
            if (changed) result.Append("</color>");
            result.Append('\n');
        }
        return result.ToString();
    }

    private void ChooseLocal() => Choose(true);
    private void ChooseCloud() => Choose(false);
    private void Choose(bool useLocal)
    {
        if (m_confirming || m_data?.confirmAsync == null) return;
        m_data.onChoose?.Invoke();
        m_confirming = true;
        m_localBtn.interactable = m_cloudBtn.interactable = false;
        string chosen = useLocal ? "local" : "cloud";
        m_confirmation = MPSecondConfirmationPop.Show("Replace saved progress?",
            $"Use the {chosen} save shown above. Progress, currencies, items and custom levels in the other save will be replaced. This cannot be merged or undone in-game.",
            "Use " + chosen + " save", async token =>
            {
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, m_lifetime.Token))
                {
                    bool success = await m_data.confirmAsync(useLocal, linked.Token);
                    if (this == null || IsDestoried) return false;
                    if (!success) return false;
                    return true;
                }
            }, () =>
            {
                if (this == null || IsDestoried) return;
                m_confirming = false;
                m_localBtn.interactable = m_cloudBtn.interactable = true;
            }, onConfirmed: () =>
            {
                if (this == null || IsDestoried) return;
                TaskCompletionSource<bool> completion = m_data.completion;
                m_resolved = true;
                DestroyWindow();
                completion.TrySetResult(true);
            });
    }

    private void CancelPresentation()
    {
        if (m_confirmation != null && !m_confirmation.IsDestoried) m_confirmation.DestroyWindow();
        if (this != null && !IsDestoried) DestroyWindow();
    }

    public override void OnRelease()
    {
        m_lifetime?.Cancel(); m_lifetime?.Dispose(); m_lifetime = null;
        m_localBtn.onClick.RemoveListener(ChooseLocal);
        m_cloudBtn.onClick.RemoveListener(ChooseCloud);
        if (m_confirmation != null && !m_confirmation.IsDestoried) m_confirmation.DestroyWindow();
        if (m_data != null)
        {
            m_data.onCloudRefreshed = null;
            m_data.cancelPresentation = null;
            if (!m_resolved) m_data.completion.TrySetResult(false);
        }
    }
}

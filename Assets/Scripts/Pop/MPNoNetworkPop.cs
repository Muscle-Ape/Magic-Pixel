using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HQ.UIManager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>进入关卡前的设备断网提示；联网并重试后才继续原来的关卡入口。</summary>
[Component("MPNoNetworkPop")]
public sealed class MPNoNetworkPop : AWindow
{
    [TransformPath("View/Window/Desc")] private TMP_Text m_description;
    [TransformPath("View/Window/Status")] private TMP_Text m_status;
    [TransformPath("View/Window/RetryBtn")] private Button m_retryButton;

    private static readonly Dictionary<string, MPNoNetworkPop> s_openPopups = new Dictionary<string, MPNoNetworkPop>();
    private MPNoNetworkPopUIMsgData m_data;
    private CancellationTokenSource m_operation;
    private bool m_busy;
    private bool m_closing;

    protected override bool ShouldAdaptToNotchScreen() => false;

    /// <summary>
    /// 检查设备是否具有网络连接，不把云服务 404、登录失败等业务错误当成设备断网。
    /// 返回 false 时调用方必须停止进入关卡；retryEntry 应保留同一目标关卡并重新校验来源。
    /// </summary>
    public static bool CheckLevelEntry(AWindow source, Action retryEntry)
    {
        if (!SourceIsAlive(source) || retryEntry == null)
            return false;
        string key = LevelEntryKey(source);
        if (s_openPopups.TryGetValue(key, out MPNoNetworkPop current) && current != null && !current.IsDestoried)
            return false;
        if (HasDeviceNetwork())
            return true;

        Show(key, "No network connection. Connect to the internet before entering this level.",
            token => Task.FromResult(!token.IsCancellationRequested && SourceIsAlive(source) && HasDeviceNetwork()),
            () =>
            {
                // 关闭动画期间可能切页或再次断网，继续入口仍需重新检查。
                if (SourceIsAlive(source))
                    retryEntry();
            });
        return false;
    }

    public static void DismissLevelEntry(AWindow source)
    {
        if (!ReferenceEquals(source, null))
            Dismiss(LevelEntryKey(source));
    }

    private static bool HasDeviceNetwork() => Application.internetReachability != NetworkReachability.NotReachable;
    private static bool SourceIsAlive(AWindow source) => ReferenceEquals(source, null) || (source != null && !source.IsDestoried);
    private static string LevelEntryKey(AWindow source) => "level-entry-" + (ReferenceEquals(source, null) ? "none" : source.GetInstanceID().ToString());

    public static MPNoNetworkPop Show(string faultKey, string description,
        Func<CancellationToken, Task<bool>> retryAsync, Action onRecovered = null)
    {
        if (string.IsNullOrWhiteSpace(faultKey))
            throw new ArgumentException("网络故障必须具有稳定的来源标识。", nameof(faultKey));
        if (retryAsync == null)
            throw new ArgumentNullException(nameof(retryAsync));
        if (s_openPopups.TryGetValue(faultKey, out MPNoNetworkPop existing) && existing != null && !existing.IsDestoried)
            return existing;
        return UIManager.Inst.ShowWindow<MPNoNetworkPop>(
            new MPNoNetworkPopUIMsgData(faultKey, description, retryAsync, onRecovered), true, UILayer.Top);
    }

    /// <summary>原页面销毁或原操作被取消时撤回其重试入口，不继续访问已销毁页面。</summary>
    public static void Dismiss(string faultKey)
    {
        if (!string.IsNullOrEmpty(faultKey) && s_openPopups.TryGetValue(faultKey, out MPNoNetworkPop popup)
            && popup != null && !popup.IsDestoried)
            popup.DestroyWindow();
    }

    public override void OnCreate()
    {
        m_retryButton.onClick.AddListener(Retry);
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPNoNetworkPopUIMsgData>();
        m_description.text = m_data?.Description ?? "No network connection. Check your connection and try again.";
        m_status.text = string.Empty;
        m_retryButton.interactable = m_data?.RetryAsync != null;
        if (m_data != null)
            s_openPopups[m_data.FaultKey] = this;
    }

    private async void Retry()
    {
        if (m_busy || m_closing || m_data?.RetryAsync == null)
            return;
        m_busy = true;
        m_retryButton.interactable = false;
        m_status.text = "Connecting...";
        m_operation?.Dispose();
        var operation = new CancellationTokenSource();
        m_operation = operation;
        try
        {
            bool recovered = await m_data.RetryAsync(operation.Token);
            if (this == null || IsDestoried || operation.IsCancellationRequested)
                return;
            if (!recovered)
            {
                m_status.text = "Still offline. Please check your connection and retry.";
                return;
            }
            m_closing = true;
            Action callback = m_data.OnRecovered;
            MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
            if (animation != null)
                animation.Close(callback);
            else
            {
                DestroyWindow();
                callback?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            if (this != null && !IsDestoried)
                m_status.text = "Connection cancelled. You can retry.";
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPNoNetworkPop] 重试失败：{exception.GetType().Name}");
            if (this != null && !IsDestoried)
                m_status.text = "Unable to connect. Please try again.";
        }
        finally
        {
            if (this != null && !IsDestoried && !m_closing)
            {
                m_busy = false;
                m_retryButton.interactable = true;
            }
        }
    }

    public override void OnRelease()
    {
        m_operation?.Cancel();
        m_operation?.Dispose();
        m_operation = null;
        m_retryButton.onClick.RemoveListener(Retry);
        if (m_data != null && s_openPopups.TryGetValue(m_data.FaultKey, out MPNoNetworkPop current) && current == this)
            s_openPopups.Remove(m_data.FaultKey);
    }
}

public sealed class MPNoNetworkPopUIMsgData : UIMsgData
{
    public string FaultKey { get; }
    public string Description { get; }
    public Func<CancellationToken, Task<bool>> RetryAsync { get; }
    public Action OnRecovered { get; }

    public MPNoNetworkPopUIMsgData(string faultKey, string description,
        Func<CancellationToken, Task<bool>> retryAsync, Action onRecovered = null)
    {
        FaultKey = faultKey;
        Description = description;
        RetryAsync = retryAsync;
        OnRecovered = onRecovered;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 项目内购模块入口。
/// 负责初始化 HQIap、发起购买、恢复购买，以及将购买内容原子写入玩家存档。
/// </summary>
public sealed class MPIapManager
{
    public const string VIP_PRODUCT_ID = "yun_vip_annual";
    private static MPIapManager m_instance;

    private readonly List<Action<HQIapStatus>> m_initializationCallbacks =
        new List<Action<HQIapStatus>>();

    private bool m_isPurchaseInProgress;
    private bool m_isRestoreInProgress;

    private MPIapManager()
    {
    }

    public static MPIapManager Instance
    {
        get
        {
            if (m_instance == null)
                m_instance = new MPIapManager();
            return m_instance;
        }
    }

    /// <summary>HQIap 是否已完成商店初始化。</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>当前是否正在等待 HQIap 初始化结果。</summary>
    public bool IsInitializing { get; private set; }

    /// <summary>最近一次初始化结果。</summary>
    public HQIapStatus InitializationStatus { get; private set; } = HQIapStatus.None;

    /// <summary>HQIap 初始化结束时触发。</summary>
    public event Action<HQIapStatus> InitializationCompleted;

    /// <summary>一次新订单成功入账时触发；重复回放的已入账订单不会再次触发。</summary>
    public event Action<HQIapProduct> ProductGranted;

    /// <summary>主动购买流程结束时触发。</summary>
    public event Action<string, HQIapPurchaseResult> PurchaseCompleted;

    /// <summary>恢复购买流程结束时触发。</summary>
    public event Action<HQIapPurchaseResult> RestoreCompleted;

    /// <summary>
    /// 初始化内购。重复调用时会合并等待中的回调，不会重复初始化 HQIap。
    /// </summary>
    public void Initialize(Action<HQIapStatus> onCompleted = null)
    {
        if (IsInitialized)
        {
            InvokeSafely(onCompleted, HQIapStatus.Succeeded, nameof(onCompleted));
            return;
        }

        if (onCompleted != null)
            m_initializationCallbacks.Add(onCompleted);

        if (IsInitializing)
            return;

        IsInitializing = true;
        InitializationStatus = HQIapStatus.None;

        try
        {
            HQIap.Init(OnInitialized, PayoutProduct);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPIapManager] Initialize failed: {exception.Message}");
            OnInitialized(HQIapStatus.ClientCodeError);
        }
    }

    /// <summary>
    /// 发起购买。productId 使用 hq_iap_data.json 中的内部 ID，不是商店 ID。
    /// </summary>
    public void Purchase(
        string productId,
        Action<string, HQIapPurchaseResult> onCompleted = null,
        string scene = null)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            CompletePurchase(productId, HQIapPurchaseResult.Failed, onCompleted);
            return;
        }

        if (!IsInitialized)
        {
            CompletePurchase(productId, HQIapPurchaseResult.NotInitialized, onCompleted);
            return;
        }

        // HQIap 内部只保存一个购买结果回调，因此不允许并发发起多个订单。
        if (m_isPurchaseInProgress)
        {
            Debug.LogWarning($"[MPIapManager] Purchase ignored because another purchase is in progress: {productId}");
            CompletePurchase(productId, HQIapPurchaseResult.Failed, onCompleted);
            return;
        }

        m_isPurchaseInProgress = true;
        try
        {
            HQIap.Purchase(productId, (id, result) =>
            {
                m_isPurchaseInProgress = false;
                CompletePurchase(id, result, onCompleted);
            }, scene);
        }
        catch (Exception exception)
        {
            m_isPurchaseInProgress = false;
            Debug.LogError($"[MPIapManager] Purchase failed: {exception.Message}");
            CompletePurchase(productId, HQIapPurchaseResult.Failed, onCompleted);
        }
    }

    /// <summary>恢复当前商店账号拥有的非消耗品和订阅。</summary>
    public void RestorePurchases(Action<HQIapPurchaseResult> onCompleted = null)
    {
        if (!IsInitialized)
        {
            CompleteRestore(HQIapPurchaseResult.NotInitialized, onCompleted);
            return;
        }

        if (m_isRestoreInProgress)
        {
            Debug.LogWarning("[MPIapManager] Restore ignored because another restore is in progress.");
            CompleteRestore(HQIapPurchaseResult.Failed, onCompleted);
            return;
        }

        if (!IsRestoreSupportedPlatform())
        {
            Debug.LogWarning($"[MPIapManager] Restore is not supported on {Application.platform}.");
            CompleteRestore(HQIapPurchaseResult.Failed, onCompleted);
            return;
        }

        m_isRestoreInProgress = true;
        try
        {
            HQIap.Restore(result =>
            {
                m_isRestoreInProgress = false;
                CompleteRestore(result, onCompleted);
            });
        }
        catch (Exception exception)
        {
            m_isRestoreInProgress = false;
            Debug.LogError($"[MPIapManager] Restore failed: {exception.Message}");
            CompleteRestore(HQIapPurchaseResult.Failed, onCompleted);
        }
    }

    public HQIapProduct GetProduct(string productId)
    {
        return string.IsNullOrWhiteSpace(productId) ? null : HQIap.GetProduct(productId);
    }

    public HQIapProduct[] GetProducts()
    {
        return HQIap.GetProducts() ?? Array.Empty<HQIapProduct>();
    }

    /// <summary>由商店票据实时判断订阅状态，不把 VIP 误保存为永久权益。</summary>
    public bool HasActiveSubscription(string productId)
    {
        if (!IsInitialized || string.IsNullOrWhiteSpace(productId))
            return false;
#if UNITY_EDITOR
        // 编辑器模拟订单没有商店订阅票据，只用于开发环境验证 VIP 权限流程。
        if (HQIap.EnableSimulatedPurchase)
            return MPUser.instance.GetShopProductPurchasedCount(productId) > 0;
#endif
        try
        {
            return HQIap.IsSubscriptionActive(productId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPIapManager] Subscription status unavailable: {exception.Message}");
            return false;
        }
    }

    private void OnInitialized(HQIapStatus status)
    {
        IsInitializing = false;
        IsInitialized = status == HQIapStatus.Succeeded;
        InitializationStatus = status;

        if (IsInitialized && HasActiveSubscription(VIP_PRODUCT_ID))
            MPUser.instance.GrantVipPetsPermanently();

        Action<HQIapStatus>[] callbacks = m_initializationCallbacks.ToArray();
        m_initializationCallbacks.Clear();

        foreach (Action<HQIapStatus> callback in callbacks)
            InvokeSafely(callback, status, nameof(Initialize));

        InvokeSafely(InitializationCompleted, status, nameof(InitializationCompleted));
    }

    /// <summary>
    /// HQIap 的持久化发货回调。只有奖励与订单幂等凭据一起成功写入 ES3 后才返回 true。
    /// </summary>
    private bool PayoutProduct(HQIapProduct product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ID))
        {
            Debug.LogError("[MPIapManager] Cannot grant an empty product.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(product.TransactionID))
        {
            Debug.LogError($"[MPIapManager] Product has no transaction ID: {product.ID}");
            return false;
        }

        MPRewardReceipt receipt = CreateRewardReceipt(product);
        if (receipt == null)
            return false;

        // Unity IAP 可能在启动或恢复购买时再次投递尚未确认的订单。
        if (MPUser.instance.RewardTransactionIsCommitted(product.TransactionID))
            return true;

        bool granted = MPUser.instance.TryGrantRewards(receipt);
        if (!granted)
        {
            // 处理极少数重复回调竞争：若另一条回调已经入账，同样可以确认订单。
            return MPUser.instance.RewardTransactionIsCommitted(product.TransactionID);
        }

        InvokeSafely(ProductGranted, product, nameof(ProductGranted));
        return true;
    }

    private static MPRewardReceipt CreateRewardReceipt(HQIapProduct product)
    {
        if (product.Payouts == null || product.Payouts.Length == 0)
        {
            Debug.LogError($"[MPIapManager] Product has no payouts: {product.ID}");
            return null;
        }

        var rewards = new List<MPRewardItem>(product.Payouts.Length);
        foreach (HQIapPayout payout in product.Payouts)
        {
            if (!TryCreateReward(payout, out MPRewardItem reward))
            {
                Debug.LogError($"[MPIapManager] Invalid payout in product: {product.ID}");
                return null;
            }

            rewards.Add(reward);
        }

        // VIP 宠物属于购买即永久赠送，与订阅后续是否到期无关，并与订单奖励原子入账。
        if (string.Equals(product.ID, VIP_PRODUCT_ID, StringComparison.Ordinal))
        {
            foreach (MPPetConfig pet in MPUser.instance.GetVipPetConfigs())
            {
                if (!rewards.Exists(item => item != null && item.type == pet.ID))
                    rewards.Add(new MPRewardItem(pet.ID, 1, pet.Icon));
            }
        }

        return new MPRewardReceipt
        {
            sourceId = "iap:" + product.ID,
            sourceName = string.IsNullOrWhiteSpace(product.DisplayName) ? product.ID : product.DisplayName,
            transactionId = product.TransactionID,
            rewards = rewards
        };
    }

    private static bool TryCreateReward(HQIapPayout payout, out MPRewardItem reward)
    {
        reward = null;
        if (payout == null || string.IsNullOrWhiteSpace(payout.AssetID) ||
            double.IsNaN(payout.Quantity) || double.IsInfinity(payout.Quantity) ||
            payout.Quantity <= 0 || payout.Quantity > int.MaxValue)
            return false;

        double roundedQuantity = Math.Round(payout.Quantity);
        if (Math.Abs(payout.Quantity - roundedQuantity) > 0.000001d)
            return false;

        reward = new MPRewardItem(payout.AssetID.Trim(), checked((int)roundedQuantity));
        return true;
    }

    private static bool IsRestoreSupportedPlatform()
    {
        return Application.platform == RuntimePlatform.IPhonePlayer ||
               Application.platform == RuntimePlatform.OSXPlayer ||
               Application.platform == RuntimePlatform.tvOS ||
               Application.platform == RuntimePlatform.Android;
    }

    private void CompletePurchase(
        string productId,
        HQIapPurchaseResult result,
        Action<string, HQIapPurchaseResult> onCompleted)
    {
        InvokeSafely(onCompleted, productId, result, nameof(onCompleted));
        InvokeSafely(PurchaseCompleted, productId, result, nameof(PurchaseCompleted));
    }

    private void CompleteRestore(
        HQIapPurchaseResult result,
        Action<HQIapPurchaseResult> onCompleted)
    {
        InvokeSafely(onCompleted, result, nameof(onCompleted));
        InvokeSafely(RestoreCompleted, result, nameof(RestoreCompleted));
    }

    private static void InvokeSafely(Action callback, string callbackName)
    {
        if (callback == null) return;
        try
        {
            callback.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPIapManager] {callbackName} callback failed: {exception.Message}");
        }
    }

    private static void InvokeSafely<T>(Action<T> callback, T value, string callbackName)
    {
        if (callback == null) return;
        try
        {
            callback.Invoke(value);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPIapManager] {callbackName} callback failed: {exception.Message}");
        }
    }

    private static void InvokeSafely<T1, T2>(
        Action<T1, T2> callback,
        T1 value1,
        T2 value2,
        string callbackName)
    {
        if (callback == null) return;
        try
        {
            callback.Invoke(value1, value2);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MPIapManager] {callbackName} callback failed: {exception.Message}");
        }
    }
}

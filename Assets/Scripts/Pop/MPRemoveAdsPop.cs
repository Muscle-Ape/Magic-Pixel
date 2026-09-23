using System;
using HQ.UIManager;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>永久移除强制广告的购买弹窗。</summary>
[Component("MPRemoveAdsPop")]
public sealed class MPRemoveAdsPop : AWindow
{
    public const string PRODUCT_ID = "remove_ads";

    [TransformPath("View/Window/Price/Text")] private TMP_Text m_priceText;
    [TransformPath("View/Window/ConfirmBtn")] private Button m_confirmButton;
    [TransformPath("View/Window/CloseBtn")] private Button m_closeButton;

    private MPRemoveAdsPopUIMsgData m_data;
    private HQIapProduct m_product;
    private HQIapProduct m_configProduct;
    private bool m_busy;
    private bool m_closing;
    private bool m_released;
    private int m_operationVersion;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static MPRemoveAdsPop Show(Action onPurchased = null)
    {
        if (!MPReleaseFeatures.Ads || !MPReleaseFeatures.InAppPurchases)
            return null;

        // 页面状态尚未来得及刷新时也不能再次打开购买流程。
        if (MPUser.instance.OwnsShopEntitlement(PRODUCT_ID))
        {
            onPurchased?.Invoke();
            return null;
        }

        return UIManager.Inst.ShowWindow<MPRemoveAdsPop>(
            new MPRemoveAdsPopUIMsgData(onPurchased), true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_confirmButton.onClick.AddListener(OnPurchaseClick);
        m_closeButton.onClick.AddListener(OnCloseClick);
        MPIapManager.Instance.InitializationCompleted += OnIapInitialized;
        MPIapManager.Instance.ProductGranted += OnProductGranted;

        m_configProduct = LoadConfiguredProduct();
        RefreshProduct();

        if (!MPIapManager.Instance.IsInitialized && !MPIapManager.Instance.IsInitializing)
            MPIapManager.Instance.Initialize();
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPRemoveAdsPopUIMsgData>();
        m_busy = false;
        m_closing = false;
        RefreshProduct();
    }

    private void RefreshProduct()
    {
        HQIapProduct runtimeProduct = MPIapManager.Instance.IsInitialized
            ? MPIapManager.Instance.GetProduct(PRODUCT_ID)
            : null;
        m_product = runtimeProduct ?? m_configProduct;

        if (m_priceText != null)
            m_priceText.text = MPShopProductUI.Price(m_product);
        RefreshInteractable();
    }

    private static HQIapProduct LoadConfiguredProduct()
    {
        try
        {
            TextAsset config = Resources.Load<TextAsset>("hq_iap_data");
            HQIapProduct[] products = config == null
                ? Array.Empty<HQIapProduct>()
                : JsonConvert.DeserializeObject<HQIapProduct[]>(config.text)
                    ?? Array.Empty<HQIapProduct>();
            return Array.Find(products, product =>
                product != null && string.Equals(product.ID, PRODUCT_ID, StringComparison.Ordinal));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPRemoveAdsPop] 读取免广告商品配置失败：{exception.Message}");
            return null;
        }
    }

    private void OnIapInitialized(HQIapStatus status)
    {
        if (!IsAvailable())
            return;
        // 初始化失败时继续保留配置价格，成功时切换为商店本地化价格。
        RefreshProduct();
    }

    private void OnProductGranted(HQIapProduct product)
    {
        if (!IsAvailable() || product == null
            || !string.Equals(product.ID, PRODUCT_ID, StringComparison.Ordinal))
            return;

        RefreshProduct();
        // 初始化或恢复购买也可能补发权益，此时直接同步主页并关闭弹窗。
        if (!m_busy && MPUser.instance.OwnsShopEntitlement(PRODUCT_ID))
            CompletePurchase();
    }

    private void OnPurchaseClick()
    {
        if (m_busy || m_closing || m_released)
            return;
        if (MPUser.instance.OwnsShopEntitlement(PRODUCT_ID))
        {
            CompletePurchase();
            return;
        }
        if (m_product == null || !MPUser.instance.ShopProductIsAvailable(m_product))
        {
            ShowToast("This product is unavailable.");
            RefreshProduct();
            return;
        }

        int version = ++m_operationVersion;
        SetBusy(true);
        if (MPIapManager.Instance.IsInitialized)
        {
            BeginPurchase(version);
            return;
        }

        MPIapManager.Instance.Initialize(status =>
        {
            if (!OperationIsCurrent(version))
                return;
            RefreshProduct();
            if (status != HQIapStatus.Succeeded)
            {
                SetBusy(false);
                ShowToast("The store is unavailable. Please try again later.");
                return;
            }
            BeginPurchase(version);
        });
    }

    private void BeginPurchase(int version)
    {
        MPIapManager.Instance.Purchase(PRODUCT_ID, (_, result) =>
        {
            if (!OperationIsCurrent(version))
                return;

            SetBusy(false);
            RefreshProduct();
            if (result != HQIapPurchaseResult.Succeeded)
            {
                ShowToast("Purchase was not completed. Please try again.");
                return;
            }

            ShowToast("Purchase completed.");
            CompletePurchase();
        }, "remove_ads_popup");
    }

    private void CompletePurchase()
    {
        if (m_closing || m_released)
            return;
        Action onPurchased = m_data?.OnPurchased;
        onPurchased?.Invoke();
        if (this == null || IsDestoried)
            return;
        Close();
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        bool available = !m_busy && !m_closing && !m_released;
        if (m_closeButton != null)
            m_closeButton.interactable = available;
        if (m_confirmButton != null)
        {
            m_confirmButton.interactable = available
                && MPReleaseFeatures.InAppPurchases
                && !MPUser.instance.OwnsShopEntitlement(PRODUCT_ID)
                && m_product != null
                && MPUser.instance.ShopProductIsAvailable(m_product);
        }
    }

    private bool OperationIsCurrent(int version)
    {
        return IsAvailable() && !m_closing && m_busy && version == m_operationVersion;
    }

    private bool IsAvailable()
    {
        return this != null && !IsDestoried && !m_released;
    }

    private void OnCloseClick()
    {
        if (!m_busy)
            Close();
    }

    private void Close()
    {
        if (m_closing || m_released)
            return;
        m_closing = true;
        ++m_operationVersion;
        RefreshInteractable();
        MPPopScaleAnimation animation = GetComponent<MPPopScaleAnimation>();
        if (animation != null)
            animation.Close(null);
        else
            DestroyWindow();
    }

    private static void ShowToast(string message)
    {
        UnityToast.Instance?.ShowToast(message);
    }

    public override void OnRelease()
    {
        if (m_released)
            return;
        m_released = true;
        ++m_operationVersion;
        m_confirmButton?.onClick.RemoveListener(OnPurchaseClick);
        m_closeButton?.onClick.RemoveListener(OnCloseClick);
        MPIapManager.Instance.InitializationCompleted -= OnIapInitialized;
        MPIapManager.Instance.ProductGranted -= OnProductGranted;
        m_data = null;
        m_product = null;
        m_configProduct = null;
        base.OnRelease();
    }
}

public sealed class MPRemoveAdsPopUIMsgData : UIMsgData
{
    public Action OnPurchased { get; }

    public MPRemoveAdsPopUIMsgData(Action onPurchased)
    {
        OnPurchased = onPurchased;
    }
}

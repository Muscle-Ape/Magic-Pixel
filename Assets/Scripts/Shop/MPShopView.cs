using System;
using System.Collections.Generic;
using System.Linq;
using HQ.UIManager;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店页面。配置负责商品内容和美元兜底价，HQIap 初始化后只覆盖为商店本地化价格。
/// </summary>
[Component("MPShopView")]
public sealed class MPShopView : AWindow
{
    private const string VIP_PRODUCT_ID = "yun_vip_annual";
    private const string REMOVE_ADS_PRODUCT_ID = "remove_ads";
    private const string FREE_COIN_AD_SCENE = "shop_free_coin";

    [TransformPath("View/CloseBtn")] private RectTransform m_closeBtnNode;
    [TransformPath("View/Coin/Count")] private TMP_Text m_coinCount;
    [TransformPath("View/Fluorite/Count")] private TMP_Text m_fluoriteCount;
    [TransformPath("View/Product/Viewport/Content/Bundles")] private RectTransform m_bundles;
    [TransformPath("View/Product/Viewport/Content/Coins/Coins")] private RectTransform m_coins;

    [TransformPath("View/Product/Viewport/Content/Vip")] private RectTransform m_vip;
    [TransformPath("View/Product/Viewport/Content/Vip/Node/PurchaseBtn")] private RectTransform m_vipButtonNode;
    [TransformPath("View/Product/Viewport/Content/RemoveAds")] private RectTransform m_removeAds;
    [TransformPath("View/Product/Viewport/Content/RemoveAds/Node/PurchaseBtn")] private RectTransform m_removeAdsButtonNode;
    [TransformPath("View/Product/Viewport/Content/Coins/Coins/FreeCoin/Node/PurchaseBtn")] private RectTransform m_freeCoinButtonNode;
    [TransformPath("View/Product/Viewport/Content/Coins/Coins/FreeCoin/Node/Count")] private TMP_Text m_freeCoinCount;

    private readonly List<MPShopBundle> m_bundleItems = new List<MPShopBundle>();
    private readonly List<MPShopCoin> m_coinItems = new List<MPShopCoin>();
    private Dictionary<string, HQIapProduct> m_products =
        new Dictionary<string, HQIapProduct>(StringComparer.Ordinal);
    private GameObject m_bundlePrefab;
    private GameObject m_coinPrefab;
    private bool m_busy;
    private bool m_released;
    private int m_operationVersion;
    private string m_bundleSignature;
    private string m_coinSignature;
    private Button m_closeBtn;
    private Button m_vipButton;
    private Button m_removeAdsButton;
    private Button m_freeCoinButton;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static MPShopView Show()
    {
        if (!MPReleaseFeatures.Shop)
            return null;
        return UIManager.Inst.ShowWindow<MPShopView>(null, true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_closeBtn = MPShopProductUI.EnsureButton(m_closeBtnNode);
        m_vipButton = MPShopProductUI.EnsureButton(m_vipButtonNode);
        m_removeAdsButton = MPShopProductUI.EnsureButton(m_removeAdsButtonNode);
        m_freeCoinButton = MPShopProductUI.EnsureButton(m_freeCoinButtonNode);
        m_closeBtn?.onClick.AddListener(OnClose);
        m_vipButton?.onClick.AddListener(OnVipPurchase);
        m_removeAdsButton?.onClick.AddListener(OnRemoveAdsPurchase);
        m_freeCoinButton?.onClick.AddListener(OnFreeCoinClick);
        MPIapManager.Instance.InitializationCompleted += OnIapInitialized;
        MPIapManager.Instance.ProductGranted += OnProductGranted;

        m_bundlePrefab = MPLoad.Load<GameObject>("MPShopBundle", this);
        m_coinPrefab = MPLoad.Load<GameObject>("MPShopCoin", this);
        RefreshProducts(GetBestAvailableProducts());
        RefreshAssets();
        RefreshFreeCoin();

        if (MPReleaseFeatures.InAppPurchases && !MPIapManager.Instance.IsInitialized
            && !MPIapManager.Instance.IsInitializing)
            MPIapManager.Instance.Initialize();
    }

    public override void OnFocus(bool focus)
    {
        if (!focus || m_released)
            return;
        RefreshProducts(GetBestAvailableProducts());
        RefreshAssets();
        RefreshFreeCoin();
    }

    private void RefreshProducts(IEnumerable<HQIapProduct> source)
    {
        HQIapProduct[] products = (source ?? Array.Empty<HQIapProduct>())
            .Where(product => product != null && !string.IsNullOrWhiteSpace(product.ID))
            .OrderBy(product => product.Sort)
            .ThenBy(product => product.ID, StringComparer.Ordinal)
            .ToArray();
        m_products = products.GroupBy(product => product.ID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        ApplyFixedProduct(m_vip, m_vipButton, VIP_PRODUCT_ID);
        ApplyFixedProduct(m_removeAds, m_removeAdsButton, REMOVE_ADS_PRODUCT_ID);

        HQIapProduct[] bundleProducts = products.Where(product =>
            product.ID.StartsWith("bundle_", StringComparison.Ordinal)
            && MPUser.instance.ShopProductIsAvailable(product)).ToArray();
        HQIapProduct[] coinProducts = products.Where(product =>
            product.ID.StartsWith("coin_", StringComparison.Ordinal)
            && MPUser.instance.ShopProductIsAvailable(product)).ToArray();
        string bundleSignature = string.Join("|", bundleProducts.Select(product => product.ID));
        string coinSignature = string.Join("|", coinProducts.Select(product => product.ID));
        if (bundleSignature != m_bundleSignature)
        {
            m_bundleSignature = bundleSignature;
            RebuildBundleItems(bundleProducts);
        }
        else
        {
            foreach (MPShopBundle item in m_bundleItems)
                if (item != null && item.ProductId != null && m_products.TryGetValue(item.ProductId, out HQIapProduct product))
                    item.RefreshPrice(product);
        }
        if (coinSignature != m_coinSignature)
        {
            m_coinSignature = coinSignature;
            RebuildCoinItems(coinProducts);
        }
        else
        {
            foreach (MPShopCoin item in m_coinItems)
                if (item != null && item.ProductId != null && m_products.TryGetValue(item.ProductId, out HQIapProduct product))
                    item.RefreshPrice(product);
        }

        Canvas.ForceUpdateCanvases();
        if (m_coins != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_coins);
        if (m_bundles != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_bundles);
        RectTransform content = m_bundles?.parent as RectTransform;
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        SetBusy(m_busy);
    }

    private void ApplyFixedProduct(RectTransform card, Button button, string productId)
    {
        if (card == null)
            return;
        if (!m_products.TryGetValue(productId, out HQIapProduct product))
        {
            card.gameObject.SetActive(false);
            return;
        }

        bool owned = (productId == REMOVE_ADS_PRODUCT_ID
                && MPUser.instance.OwnsShopEntitlement(REMOVE_ADS_PRODUCT_ID))
            || (productId == VIP_PRODUCT_ID
                && MPUser.instance.HasVipAccess());
        bool available = !owned && MPUser.instance.ShopProductIsAvailable(product);
        card.gameObject.SetActive(available);
        if (!available)
            return;

        Transform node = card.Find("Node") ?? card;
        MPShopProductUI.SetText(node, "Title", MPShopProductUI.ProductTitle(product));
        MPShopProductUI.SetText(node, "PurchaseBtn/Price",
            MPShopProductUI.Price(product));
        if (node.Find("Desc") != null && !string.IsNullOrWhiteSpace(product.Describe))
            MPShopProductUI.SetText(node, "Desc", product.Describe.Trim());
        MPShopProductUI.ApplyIcon(node.Find("Icon")?.GetComponent<Image>(), product, this);
        if (button != null)
            button.interactable = !m_busy;
    }

    private void RebuildBundleItems(IEnumerable<HQIapProduct> products)
    {
        ClearItems(m_bundleItems);
        if (m_bundlePrefab == null || m_bundles == null)
            return;
        foreach (HQIapProduct product in products)
        {
            GameObject itemObject = Instantiate(m_bundlePrefab, m_bundles, false);
            itemObject.name = "MPShopBundle_" + product.ID;
            MPShopBundle item = itemObject.GetComponent<MPShopBundle>()
                ?? itemObject.AddComponent<MPShopBundle>();
            item.Initialize(product, OnPurchaseRequested);
            m_bundleItems.Add(item);
        }
    }

    private void RebuildCoinItems(IEnumerable<HQIapProduct> products)
    {
        ClearItems(m_coinItems);
        if (m_coinPrefab == null || m_coins == null)
            return;
        foreach (HQIapProduct product in products)
        {
            GameObject itemObject = Instantiate(m_coinPrefab, m_coins, false);
            itemObject.name = "MPShopCoin_" + product.ID;
            MPShopCoin item = itemObject.GetComponent<MPShopCoin>()
                ?? itemObject.AddComponent<MPShopCoin>();
            item.Initialize(product, OnPurchaseRequested);
            m_coinItems.Add(item);
        }
    }

    private static void ClearItems<T>(List<T> items) where T : Component
    {
        foreach (T item in items)
        {
            if (item == null)
                continue;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }
        items.Clear();
    }

    private IEnumerable<HQIapProduct> GetBestAvailableProducts()
    {
        HQIapProduct[] runtimeProducts = MPIapManager.Instance.IsInitialized
            ? MPIapManager.Instance.GetProducts()
            : Array.Empty<HQIapProduct>();
        if (runtimeProducts != null && runtimeProducts.Length > 0)
            return runtimeProducts;
        try
        {
            TextAsset config = Resources.Load<TextAsset>("hq_iap_data");
            return config == null
                ? Array.Empty<HQIapProduct>()
                : JsonConvert.DeserializeObject<HQIapProduct[]>(config.text)
                    ?? Array.Empty<HQIapProduct>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPShopView] 读取商品配置失败：{exception.Message}");
            return Array.Empty<HQIapProduct>();
        }
    }

    private void OnIapInitialized(HQIapStatus status)
    {
        if (m_released || this == null)
            return;
        // 初始化失败也保留配置价格；成功后本地化价格会覆盖美元兜底价。
        RefreshProducts(GetBestAvailableProducts());
    }

    private void OnProductGranted(HQIapProduct product)
    {
        if (m_released || this == null)
            return;
        RefreshAssets();
        RefreshProducts(GetBestAvailableProducts());
    }

    private void OnVipPurchase() => OnPurchaseRequested(VIP_PRODUCT_ID);
    private void OnRemoveAdsPurchase() => OnPurchaseRequested(REMOVE_ADS_PRODUCT_ID);

    private void OnPurchaseRequested(string productId)
    {
        if (m_busy || m_released || !MPReleaseFeatures.InAppPurchases
            || string.IsNullOrEmpty(productId) || !m_products.ContainsKey(productId))
            return;

        HQIapProduct product = m_products[productId];
        if (!MPUser.instance.ShopProductIsAvailable(product))
        {
            RefreshProducts(GetBestAvailableProducts());
            ShowToast("This product is sold out.");
            return;
        }

        int version = ++m_operationVersion;
        SetBusy(true);
        if (MPIapManager.Instance.IsInitialized)
        {
            BeginPurchase(productId, version);
            return;
        }

        MPIapManager.Instance.Initialize(status =>
        {
            if (!OperationIsCurrent(version))
                return;
            if (status != HQIapStatus.Succeeded)
            {
                SetBusy(false);
                ShowToast("The store is unavailable. Please try again later.");
                return;
            }
            RefreshProducts(GetBestAvailableProducts());
            BeginPurchase(productId, version);
        });
    }

    private void BeginPurchase(string productId, int version)
    {
        MPIapManager.Instance.Purchase(productId, (_, result) =>
        {
            if (!OperationIsCurrent(version))
                return;
            SetBusy(false);
            RefreshAssets();
            // 购买回调时订阅票据已更新，再刷新一次可立即隐藏已拥有的 VIP 商品。
            RefreshProducts(GetBestAvailableProducts());
            if (result == HQIapPurchaseResult.Succeeded)
            {
                ShowToast("Purchase completed.");
                if (string.Equals(productId, VIP_PRODUCT_ID, StringComparison.Ordinal))
                    ShowVipPetNotification();
            }
            else
                ShowToast("Purchase was not completed. Please try again.");
        }, "shop");
    }

    private void ShowVipPetNotification()
    {
        foreach (MPPetConfig pet in MPUser.instance.GetVipPetConfigs())
        {
            if (MPPetClaimPop.ShowMilestoneNotification(pet.ID, this))
                return;
        }
    }

    private void OnFreeCoinClick()
    {
        if (m_busy || m_released || !MPReleaseFeatures.Ads)
            return;
        MPShopFreeCoinStatus status = MPUser.instance.GetShopFreeCoinStatus();
        if (!status.CanClaim)
        {
            ShowToast(status.remainingCount <= 0
                ? "Today's free coin rewards are complete."
                : "Please check your device time and try again.");
            RefreshFreeCoin();
            return;
        }

        string owner = MPUser.instance.GetRewardProgressOwner();
        int version = ++m_operationVersion;
        SetBusy(true);
        try
        {
            AOAds.CheckAndShowRewardedVideo(FREE_COIN_AD_SCENE, (ready, success) =>
            {
                if (!OperationIsCurrent(version))
                    return;
                SetBusy(false);
                if (!ready || !success || owner != MPUser.instance.GetRewardProgressOwner())
                {
                    ShowToast("Video not completed. Your free reward is still available.");
                    RefreshFreeCoin();
                    return;
                }
                if (!MPUser.instance.TryClaimShopFreeCoin(status.day, out _))
                {
                    ShowToast("Could not claim the reward. Please try again.");
                    RefreshFreeCoin();
                    return;
                }
                RefreshAssets();
                RefreshFreeCoin();
                ShowToast($"You received {MPUser.SHOP_FREE_COIN_AMOUNT} coins.");
            });
        }
        catch (Exception exception)
        {
            if (OperationIsCurrent(version))
            {
                SetBusy(false);
                RefreshFreeCoin();
                ShowToast("Video is unavailable. Please try again later.");
            }
            Debug.LogWarning($"[MPShopView] 激励广告打开失败：{exception.Message}");
        }
    }

    private bool OperationIsCurrent(int version)
    {
        return this != null && !m_released && !IsDestoried && version == m_operationVersion;
    }

    private void RefreshAssets()
    {
        if (m_coinCount != null)
            m_coinCount.text = MPUser.instance.GetCoins().ToString();
        if (m_fluoriteCount != null)
            m_fluoriteCount.text = MPUser.instance.GetFluorite().ToString();
    }

    private void RefreshFreeCoin()
    {
        MPShopFreeCoinStatus status = MPUser.instance.GetShopFreeCoinStatus();
        if (m_freeCoinCount != null)
            m_freeCoinCount.text = $"{status.remainingCount}/{MPUser.SHOP_FREE_COIN_DAILY_LIMIT}";
        if (m_freeCoinButton != null)
            m_freeCoinButton.interactable = !m_busy && status.CanClaim && MPReleaseFeatures.Ads;
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        if (m_closeBtn != null) m_closeBtn.interactable = !busy;
        if (m_vipButton != null)
            m_vipButton.interactable = !busy
                && ProductIsAvailable(VIP_PRODUCT_ID)
                && !MPUser.instance.HasVipAccess();
        if (m_removeAdsButton != null)
            m_removeAdsButton.interactable = !busy
                && ProductIsAvailable(REMOVE_ADS_PRODUCT_ID)
                && !MPUser.instance.OwnsShopEntitlement(REMOVE_ADS_PRODUCT_ID);
        foreach (MPShopBundle item in m_bundleItems) item?.SetInteractable(!busy);
        foreach (MPShopCoin item in m_coinItems) item?.SetInteractable(!busy);
        RefreshFreeCoin();
    }

    private bool ProductIsAvailable(string productId)
    {
        return m_products.TryGetValue(productId, out HQIapProduct product)
            && MPUser.instance.ShopProductIsAvailable(product);
    }

    private static void ShowToast(string message)
    {
        if (UnityToast.Instance != null)
            UnityToast.Instance.ShowToast(message);
    }

    private void OnClose()
    {
        if (!m_busy)
            DestroyWindow();
    }

    public override void OnRelease()
    {
        if (m_released)
            return;
        m_released = true;
        ++m_operationVersion;
        m_closeBtn?.onClick.RemoveListener(OnClose);
        m_vipButton?.onClick.RemoveListener(OnVipPurchase);
        m_removeAdsButton?.onClick.RemoveListener(OnRemoveAdsPurchase);
        m_freeCoinButton?.onClick.RemoveListener(OnFreeCoinClick);
        MPIapManager.Instance.InitializationCompleted -= OnIapInitialized;
        MPIapManager.Instance.ProductGranted -= OnProductGranted;
        ClearItems(m_bundleItems);
        ClearItems(m_coinItems);
        MPLoad.ReleaseAll(this);
        base.OnRelease();
    }
}

using System;
using System.Globalization;
using DG.Tweening;
using HQ.UIManager;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>VIP 年度订阅页面。</summary>
[Component("MPVipView")]
public sealed class MPVipView : AWindow
{
    private const string LEGAL_DOCUMENT_URL = "http://yunovagames.com:19100/";
    private const float OPEN_DURATION = 0.42f;
    private const float CLOSE_DURATION = 0.32f;
    private const float OFFSCREEN_PADDING = 24f;

    [TransformPath("View/CloseBtn")] private Button m_closeButton;
    [TransformPath("View/PurchaseBtn")] private Button m_purchaseButton;
    [TransformPath("View/PurchaseBtn/Text")] private TMP_Text m_purchaseText;
    [TransformPath("View/RestoreBtn")] private Button m_restoreButton;
    [TransformPath("View/PrivacyPolicyBtn")] private Button m_privacyPolicyButton;
    [TransformPath("View/TermsOfServiceBtn")] private Button m_termsOfServiceButton;

    private MPVipViewUIMsgData m_data;
    private RectTransform m_pageRect;
    private HQIapProduct m_configProduct;
    private HQIapProduct m_product;
    private Tween m_slideTween;
    private Vector2 m_visiblePosition;
    private float m_slideDistance;
    private bool m_opening;
    private bool m_busy;
    private bool m_closing;
    private bool m_released;
    private int m_operationVersion;

    protected override bool ShouldAdaptToNotchScreen() => false;

    public static MPVipView Show(Action onVipActivated = null)
    {
        if (!MPReleaseFeatures.Vip || !MPReleaseFeatures.InAppPurchases)
            return null;

        if (MPUser.instance.HasVipAccess())
        {
            onVipActivated?.Invoke();
            return null;
        }

        return UIManager.Inst.ShowWindow<MPVipView>(
            new MPVipViewUIMsgData(onVipActivated), true, UILayer.Top);
    }

    public override void OnCreate()
    {
        m_pageRect = transform as RectTransform;
        m_closeButton.onClick.AddListener(OnCloseClick);
        m_purchaseButton.onClick.AddListener(OnPurchaseClick);
        m_restoreButton.onClick.AddListener(OnRestoreClick);
        m_privacyPolicyButton.onClick.AddListener(OnPrivacyPolicyClick);
        m_termsOfServiceButton.onClick.AddListener(OnTermsOfServiceClick);
        MPIapManager.Instance.InitializationCompleted += OnIapInitialized;
        MPIapManager.Instance.ProductGranted += OnProductGranted;

        m_configProduct = LoadConfiguredProduct();
        RefreshProduct();
    }

    public override void LoadUIMsgData(UIMsgData uiMsg)
    {
        m_data = uiMsg?.GetMsg<MPVipViewUIMsgData>();
        m_busy = false;
        m_closing = false;
        RefreshProduct();
        PlayOpenAnimation();
        // 放在消息数据和动画状态就绪后再初始化，兼容编辑器模拟购买同步回调。
        if (!MPIapManager.Instance.IsInitialized && !MPIapManager.Instance.IsInitializing)
            MPIapManager.Instance.Initialize();
    }

    public override void OnFocus(bool focus)
    {
        if (focus && !m_released)
            RefreshProduct();
    }

    private void RefreshProduct()
    {
        HQIapProduct runtimeProduct = MPIapManager.Instance.IsInitialized
            ? MPIapManager.Instance.GetProduct(MPIapManager.VIP_PRODUCT_ID)
            : null;
        m_product = runtimeProduct ?? m_configProduct;

        if (m_purchaseText != null)
            m_purchaseText.text = $"Purchase-{GetConfiguredPrice(m_configProduct ?? m_product)}/year";
        RefreshInteractable();
    }

    /// <summary>订阅页价格由 hq_iap_data.json 提供，不在界面脚本中硬编码金额。</summary>
    private static string GetConfiguredPrice(HQIapProduct product)
    {
        string configured = product?.DisplayPrice?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            if (decimal.TryParse(configured, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out _))
                return "$" + configured;
            return configured;
        }

        string localized = product?.localizedPriceString?.Trim();
        return string.IsNullOrEmpty(localized) ? "$--" : localized;
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
            return Array.Find(products, product => product != null
                && string.Equals(product.ID, MPIapManager.VIP_PRODUCT_ID,
                    StringComparison.Ordinal));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPVipView] 读取 VIP 商品配置失败：{exception.Message}");
            return null;
        }
    }

    private void OnIapInitialized(HQIapStatus status)
    {
        if (!IsAvailable())
            return;
        RefreshProduct();
        if (status == HQIapStatus.Succeeded && !m_busy && MPUser.instance.HasVipAccess())
            CompleteVipActivation();
    }

    private void OnProductGranted(HQIapProduct product)
    {
        if (!IsAvailable() || product == null
            || !string.Equals(product.ID, MPIapManager.VIP_PRODUCT_ID,
                StringComparison.Ordinal))
            return;

        RefreshProduct();
        // 启动或恢复购买补发 VIP 时也要让当前页面及时退出。
        if (!m_busy && MPUser.instance.HasVipAccess())
            CompleteVipActivation();
    }

    private void OnPurchaseClick()
    {
        if (!CanStartStoreOperation())
            return;
        if (MPUser.instance.HasVipAccess())
        {
            CompleteVipActivation();
            return;
        }
        if (m_product == null || !MPUser.instance.ShopProductIsAvailable(m_product))
        {
            ShowToast("This subscription is unavailable.");
            return;
        }

        int version = BeginStoreOperation();
        EnsureStoreInitialized(version, BeginPurchase);
    }

    private void BeginPurchase(int version)
    {
        MPIapManager.Instance.Purchase(MPIapManager.VIP_PRODUCT_ID, (_, result) =>
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

            ShowToast("Subscription activated.");
            CompleteVipActivation();
        }, "vip_subscription");
    }

    private void OnRestoreClick()
    {
        if (!CanStartStoreOperation())
            return;
        int version = BeginStoreOperation();
        EnsureStoreInitialized(version, BeginRestore);
    }

    private void BeginRestore(int version)
    {
        MPIapManager.Instance.RestorePurchases(result =>
        {
            if (!OperationIsCurrent(version))
                return;

            SetBusy(false);
            RefreshProduct();
            if (result != HQIapPurchaseResult.Succeeded)
            {
                ShowToast("Restore was not completed. Please try again.");
                return;
            }

            if (!MPUser.instance.HasVipAccess())
            {
                ShowToast("No active VIP subscription was found.");
                return;
            }

            MPUser.instance.GrantVipPetsPermanently();
            ShowToast("Purchases restored.");
            CompleteVipActivation();
        });
    }

    private int BeginStoreOperation()
    {
        int version = ++m_operationVersion;
        SetBusy(true);
        return version;
    }

    private void EnsureStoreInitialized(int version, Action<int> onReady)
    {
        if (MPIapManager.Instance.IsInitialized)
        {
            onReady(version);
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
            if (MPUser.instance.HasVipAccess())
            {
                SetBusy(false);
                ShowToast("Subscription restored.");
                CompleteVipActivation();
                return;
            }
            onReady(version);
        });
    }

    private bool CanStartStoreOperation()
    {
        return !m_opening && !m_busy && !m_closing && !m_released
            && MPReleaseFeatures.InAppPurchases;
    }

    private void CompleteVipActivation()
    {
        if (m_closing || m_released)
            return;
        Action onVipActivated = m_data?.OnVipActivated;
        onVipActivated?.Invoke();
        if (this != null && !IsDestoried)
            Close();
    }

    private void OnPrivacyPolicyClick()
    {
        if (!m_opening && !m_busy && !m_closing)
            Application.OpenURL(LEGAL_DOCUMENT_URL);
    }

    private void OnTermsOfServiceClick()
    {
        if (!m_opening && !m_busy && !m_closing)
            Application.OpenURL(LEGAL_DOCUMENT_URL);
    }

    private void OnCloseClick()
    {
        if (!m_busy)
            Close();
    }

    private void PlayOpenAnimation()
    {
        if (m_pageRect == null || m_released)
            return;

        KillSlideTween();
        Canvas.ForceUpdateCanvases();
        m_visiblePosition = m_pageRect.anchoredPosition;
        m_slideDistance = CalculateDownwardOffscreenDistance();
        m_pageRect.anchoredPosition = m_visiblePosition + Vector2.down * m_slideDistance;
        m_opening = true;
        RefreshInteractable();
        m_slideTween = m_pageRect.DOAnchorPos(m_visiblePosition, OPEN_DURATION)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                m_slideTween = null;
                m_opening = false;
                RefreshInteractable();
            });
    }

    /// <summary>
    /// 使用页面和所属 UI 层的世界坐标边界计算移动距离，兼容不同分辨率、Canvas 缩放和安全区。
    /// </summary>
    private float CalculateDownwardOffscreenDistance()
    {
        RectTransform parent = m_pageRect.parent as RectTransform;
        if (parent == null)
            return Mathf.Max(m_pageRect.rect.height, Screen.height) + OFFSCREEN_PADDING;

        var pageCorners = new Vector3[4];
        var parentCorners = new Vector3[4];
        m_pageRect.GetWorldCorners(pageCorners);
        parent.GetWorldCorners(parentCorners);

        float pageTop = float.MinValue;
        float parentBottom = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            pageTop = Mathf.Max(pageTop, parent.InverseTransformPoint(pageCorners[i]).y);
            parentBottom = Mathf.Min(parentBottom,
                parent.InverseTransformPoint(parentCorners[i]).y);
        }
        return Mathf.Max(1f, pageTop - parentBottom + OFFSCREEN_PADDING);
    }

    private void Close()
    {
        if (m_closing || m_released)
            return;

        m_closing = true;
        m_opening = false;
        ++m_operationVersion;
        SetBusy(false);
        KillSlideTween();
        Canvas.ForceUpdateCanvases();
        // 关闭前重新计算，横竖屏或窗口尺寸变化后仍能完全移出屏幕。
        m_slideDistance = CalculateDownwardOffscreenDistance();
        Vector2 target = m_pageRect.anchoredPosition + Vector2.down * m_slideDistance;
        m_slideTween = m_pageRect.DOAnchorPos(target, CLOSE_DURATION)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                m_slideTween = null;
                if (this != null && !IsDestoried)
                    DestroyWindow();
            });
    }

    private void SetBusy(bool busy)
    {
        m_busy = busy;
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        bool available = !m_opening && !m_busy && !m_closing && !m_released;
        if (m_closeButton != null) m_closeButton.interactable = available;
        if (m_restoreButton != null) m_restoreButton.interactable = available;
        if (m_privacyPolicyButton != null) m_privacyPolicyButton.interactable = available;
        if (m_termsOfServiceButton != null) m_termsOfServiceButton.interactable = available;
        if (m_purchaseButton != null)
        {
            m_purchaseButton.interactable = available
                && !MPUser.instance.HasVipAccess()
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

    private void KillSlideTween()
    {
        if (m_slideTween != null && m_slideTween.IsActive())
            m_slideTween.Kill();
        m_slideTween = null;
        if (m_pageRect != null)
            m_pageRect.DOKill();
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
        KillSlideTween();
        m_closeButton?.onClick.RemoveListener(OnCloseClick);
        m_purchaseButton?.onClick.RemoveListener(OnPurchaseClick);
        m_restoreButton?.onClick.RemoveListener(OnRestoreClick);
        m_privacyPolicyButton?.onClick.RemoveListener(OnPrivacyPolicyClick);
        m_termsOfServiceButton?.onClick.RemoveListener(OnTermsOfServiceClick);
        MPIapManager.Instance.InitializationCompleted -= OnIapInitialized;
        MPIapManager.Instance.ProductGranted -= OnProductGranted;
        m_data = null;
        m_product = null;
        m_configProduct = null;
        base.OnRelease();
    }
}

public sealed class MPVipViewUIMsgData : UIMsgData
{
    public Action OnVipActivated { get; }

    public MPVipViewUIMsgData(Action onVipActivated)
    {
        OnVipActivated = onVipActivated;
    }
}

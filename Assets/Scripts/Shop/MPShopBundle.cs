using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>礼包商品 Item；所有内容都来自 hq_iap_data.json。</summary>
public sealed class MPShopBundle : MonoBehaviour
{
    private Button m_purchaseButton;
    private UnityAction m_purchaseAction;
    private HQIapProduct m_product;
    private Action<string> m_onPurchase;

    public string ProductId => m_product?.ID;

    public void Initialize(HQIapProduct product, Action<string> onPurchase)
    {
        ReleaseButton();
        m_product = product;
        m_onPurchase = onPurchase;

        Transform node = transform.Find("Node") ?? transform;
        MPShopProductUI.SetText(node, "Title", MPShopProductUI.ProductTitle(product));
        MPShopProductUI.SetText(node, "PurchaseBtn/Price", MPShopProductUI.Price(product));

        string tag = MPShopProductUI.Tag(product);
        MPShopProductUI.SetActive(node, "Tag", !string.IsNullOrEmpty(tag));
        MPShopProductUI.SetText(node, "Tag/Text", tag);

        ApplyAward(node, "Coin", MPShopProductUI.Payout(product, "coin"), "x");
        ApplyAward(node, "Hint", MPShopProductUI.Payout(product, "hint"), "x");
        ApplyAward(node, "Life", MPShopProductUI.Payout(product, "life"), "x");
        int removeAds = MPShopProductUI.Payout(product, "remove_ads");
        MPShopProductUI.SetActive(node, "Awards/Ads", removeAds > 0);

        MPShopProductUI.ApplyIcon(node.Find("Icon")?.GetComponent<Image>(), product, this);
        m_purchaseButton = MPShopProductUI.EnsureButton(node.Find("PurchaseBtn"));
        if (m_purchaseButton != null)
        {
            m_purchaseAction = () => m_onPurchase?.Invoke(ProductId);
            m_purchaseButton.onClick.AddListener(m_purchaseAction);
        }
    }

    public void RefreshPrice(HQIapProduct product)
    {
        if (product == null || product.ID != ProductId)
            return;
        m_product = product;
        Transform node = transform.Find("Node") ?? transform;
        MPShopProductUI.SetText(node, "PurchaseBtn/Price", MPShopProductUI.Price(product));
    }

    public void SetInteractable(bool interactable)
    {
        if (m_purchaseButton != null)
            m_purchaseButton.interactable = interactable;
    }

    private static void ApplyAward(Transform node, string awardName, int amount, string prefix)
    {
        string path = "Awards/" + awardName;
        MPShopProductUI.SetActive(node, path, amount > 0);
        if (amount > 0)
            MPShopProductUI.SetText(node, path + "/Text", prefix + amount);
    }

    private void ReleaseButton()
    {
        if (m_purchaseButton != null && m_purchaseAction != null)
            m_purchaseButton.onClick.RemoveListener(m_purchaseAction);
        m_purchaseButton = null;
        m_purchaseAction = null;
    }

    private void OnDestroy()
    {
        ReleaseButton();
        MPLoad.ReleaseAll(this);
    }
}

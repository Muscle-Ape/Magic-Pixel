using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>金币商品 Item。</summary>
public sealed class MPShopCoin : MonoBehaviour
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
        int coins = MPShopProductUI.Payout(product, "coin");
        MPShopProductUI.SetText(node, "Count", "x" + coins);
        MPShopProductUI.SetText(node, "PurchaseBtn/Price", MPShopProductUI.Price(product));
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

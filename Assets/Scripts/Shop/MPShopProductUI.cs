using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

/// <summary>商店商品公用的显示转换，避免固定卡片和动态 Item 各写一套规则。</summary>
public static class MPShopProductUI
{
    public static string Price(HQIapProduct product)
    {
        if (product == null)
            return "$ --";
        if (!string.IsNullOrWhiteSpace(product.localizedPriceString))
            return product.localizedPriceString.Trim();

        string configured = (product.DisplayPrice ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(configured))
            return "$ --";
        return configured.StartsWith("$", StringComparison.Ordinal)
            ? configured
            : "$ " + configured;
    }

    public static int Payout(HQIapProduct product, string assetId)
    {
        if (product?.Payouts == null || string.IsNullOrEmpty(assetId))
            return 0;
        int amount = 0;
        foreach (HQIapPayout payout in product.Payouts)
        {
            if (payout == null || !string.Equals(payout.AssetID, assetId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (payout.Quantity <= 0d || payout.Quantity > int.MaxValue)
                continue;
            amount += Mathf.RoundToInt((float)payout.Quantity);
        }
        return amount;
    }

    public static string ProductTitle(HQIapProduct product)
    {
        return string.IsNullOrWhiteSpace(product?.DisplayName)
            ? product?.ID ?? string.Empty
            : product.DisplayName.Trim();
    }

    /// <summary>标签内的空格强制换行，避免诸如 Best Value 挤在同一行。</summary>
    public static string Tag(HQIapProduct product)
    {
        string value = product?.Describe?.Trim();
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Join("\n", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static void SetText(Transform root, string path, string value)
    {
        TMP_Text text = root?.Find(path)?.GetComponent<TMP_Text>();
        if (text != null)
            text.text = value ?? string.Empty;
    }

    public static void SetActive(Transform root, string path, bool active)
    {
        Transform node = root?.Find(path);
        if (node != null)
            node.gameObject.SetActive(active);
    }

    public static void ApplyIcon(Image image, HQIapProduct product, UnityEngine.Object owner)
    {
        if (image == null || owner == null || string.IsNullOrWhiteSpace(product?.ImageUrl))
            return;
        string location = product.ImageUrl.Trim();
        if (!YooAssets.CheckLocationValid(location))
        {
            Debug.LogWarning($"[MPShop] 商品图片资源不存在，保留预制体占位图：{location}");
            return;
        }
        try
        {
            Sprite sprite = MPLoad.Load<Sprite>(location, owner);
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MPShop] 加载商品图片失败 {location}: {exception.Message}");
        }
    }

    /// <summary>美术预制体只放 Image 时，在业务初始化阶段补齐项目统一按钮组件。</summary>
    public static Button EnsureButton(Transform node)
    {
        if (node == null)
            return null;
        Button button = node.GetComponent<Button>();
        if (button == null)
            button = node.gameObject.AddComponent<MPButton>();
        button.targetGraphic = node.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        return button;
    }
}

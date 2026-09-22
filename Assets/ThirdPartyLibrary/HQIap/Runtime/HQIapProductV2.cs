using UnityEngine;
using System.Runtime.Versioning;

using Newtonsoft.Json;
using System.Linq;

public class HQIapProduct
{
    /// <summary>
    /// 商品id: 不是商品商店id，可以自定义
    /// </summary>
    public string ID;

    /// <summary>
    /// Dev描述,用于内部说明，方便查看
    /// </summary>
    public string DevDescribe;

    /// <summary>
    /// 商品类型
    /// </summary>
    public UnityEngine.Purchasing.ProductType ProductType;

    /// <summary>
    /// 苹果商店id
    /// </summary>
    public string AppStoreID;

    /// <summary>
    /// 谷歌商店id
    /// </summary>
    public string GooglePlayID;

    /// <summary>
    /// 默认显示名
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 默认显示价格(美元)
    /// </summary>
    public string DisplayPrice;

    /// <summary>
    /// 库存数量 -1:无限大 或者用 9999999
    /// </summary>
    public int Inventory;

    /// <summary>
    /// 描述
    /// </summary>
    public string Describe;

    /// <summary>
    /// 排序
    /// </summary>
    public int Sort;

    /// <summary>
    /// 支付给用户的
    /// </summary>
    public HQIapPayout[] Payouts;

    /// <summary>
    /// 本地图片,网络图片
    /// </summary>
    public string ImageUrl;

    /*
    * 以下是从应用商店获取到的商品信息
    */


    /// <summary>
    /// 当地商品名
    /// </summary>
    [JsonIgnore]
    public string localizedTitle;

    /// <summary>
    /// 当地价格字符串
    /// </summary>
    [JsonIgnore]
    public string localizedPriceString;

    /// <summary>
    /// 当地商品描述
    /// </summary>
    [JsonIgnore]
    public string localizedDescription;

    /// <summary>
    /// 当地货币编码
    /// </summary>
    [JsonIgnore]
    public string isoCurrencyCode;

    /// <summary>
    /// 当地价格
    /// </summary>
    [JsonIgnore]
    public decimal localizedPrice;

    /// <summary>
    /// 当地货币符号
    /// </summary>
    [JsonIgnore]
    public string localizeCurrencySymbol;

    [JsonIgnore]
    public string StoreID
    {
        get
        {
#if !UNITY_EDITOR
            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                             Application.platform == RuntimePlatform.OSXPlayer ||
                             Application.platform == RuntimePlatform.tvOS)
            {
                return AppStoreID;
            }
            else if (Application.platform == RuntimePlatform.Android)
            {
                return GooglePlayID;
            }
#elif UNITY_EDITOR && UNITY_IOS
            return AppStoreID;
#elif UNITY_EDITOR && UNITY_ANDROID
            return GooglePlayID;
#endif
            return null;

        }
    }

    /// <summary>
    /// 购买时的场景
    /// </summary>
    [JsonIgnore]
    public string PurchaseScene;

    /// <summary>
    /// 当前订单的商店交易号，仅在购买回调期间有效。
    /// </summary>
    [JsonIgnore]
    public string TransactionID;

    /// <summary>
    /// 当前订单的原始票据，仅在购买回调期间有效。
    /// </summary>
    [JsonIgnore]
    public string Receipt;

    /// <summary>
    /// 当前订单是否由恢复购买触发。
    /// </summary>
    [JsonIgnore]
    public bool IsRestored;

    public int GetGoodsNumber(string assetID)
    {
        if (string.IsNullOrEmpty(assetID)) return 0;
        HQIapPayout iapPayout = Payouts.FirstOrDefault(l => l.AssetID == assetID);
        if (iapPayout != null)
        {
            return (int)iapPayout.Quantity;
        }
        return 0;
    }

}


public class HQIapPayout
{
    public string AssetID;
    public HQIapPayoutType Type = HQIapPayoutType.Other;
    public double Quantity;
}

/// <summary>
/// HQIapPayoutType 付出的
/// Other其他, Currency货币, Item 条目, Resource 资源
/// </summary>
public enum HQIapPayoutType
{
    Other,
    Currency,
    Item,
    Resource
}





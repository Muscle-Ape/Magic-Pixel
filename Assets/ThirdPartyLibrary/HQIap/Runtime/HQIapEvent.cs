using UnityEngine;
using System.Collections.Generic;
#if hq_ta_event
using ThinkingAnalytics;
#endif

#if hq_af_event
using AppsFlyerSDK;
#endif

#if hq_adjust_event
using AdjustSdk;
#endif

public class HQIapEvent
{
    public static bool StartUnityTimer = false;
    public static bool StartStoreTimer = false;
    public static float UnityDuration = 0.0f;
    public static float StoreDuration = 0.0f;

    private static string kIapInitSucc = "iap_init_succ";
    private static string kIapInitFail = "iap_init_fail";

    private static string kIapBuySucc = "iap_buy_succ";
    private static string kIapBuyFail = "iap_buy_fail";

    private static string kIapRestoreSucc = "iap_restore_succ";
    private static string kIapRestoreFail = "iap_restore_fail";
    private static string kIapSubscriptionError = "iap_subscription_error";
    private static string kAfIapPurchase = "iap_purchase";
#if hq_adjust_event
    private static Dictionary<string, string> _adjustEventMap = new Dictionary<string, string>();
#endif
    public static void Init()
    {
#if hq_adjust_event
        TextAsset textAsset = UnityEngine.Resources.Load<TextAsset>("adjust_event");
        if (textAsset != null)
        {
            string[] lines = textAsset.text.Replace("\"", "").Split('\n');
            foreach (var line in lines)
            {
                string[] kv = line.Split(',');
                if (kv.Length == 3)
                {
                    _adjustEventMap.Add(kv[1], kv[0]);
                }
            }
        }
        else
        {
            Debug.LogError("adjust_event.csv is null");
        }
#endif
    }

    public static void IapInitSucc(int productCount, int tryCount)
    {

        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_unity_duration",UnityDuration},
            {"iap_store_duration",StoreDuration},
            {"iap_product_count",productCount},
            {"iap_try_count",tryCount},
        };
        Track(kIapInitSucc, dict);
    }
    public static void IapInitFail(HQIapStatus status, string text, int tryCount)
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_error_type",status.ToString()},
            {"iap_error_info",text},
            {"iap_try_count",tryCount},
        };
        Track(kIapInitFail, dict);
    }

    public static float String2Float(string price)
    {
        float displayPrice = 0.00f;
        try
        {
            string[] priceArr = price.Split(".");
            if (priceArr.Length == 1)
            {
                float.TryParse(priceArr[0], out displayPrice);
            }
            else if (priceArr.Length == 2)
            {
                float val1, val2;
                float.TryParse(priceArr[0], out val1);
                float.TryParse(priceArr[1], out val2);
                for (int i = 0; i < priceArr[1].Length; i++)
                {
                    val2 *= 0.1f;
                }
                displayPrice = val1 + val2;
            }
            else
            {
                Debug.LogError($"string to float error {price}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"string to float error: price = {price}, error = {ex.Message}");
        }
        return displayPrice;
    }

    public static void IapBuySucc(HQIapProduct product, bool is_restore, string transactionID, string scene)
    {
        float displayPrice = String2Float(product.DisplayPrice);
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_product_id",product.ID},
            {"iap_store_id",product.StoreID},
            {"iap_display_price",displayPrice},
            {"iap_currency_code",product.isoCurrencyCode},
            {"iap_localized_price",product.localizedPrice},
            {"iap_scene",scene},
            {"iap_is_restore",is_restore},
            {"iap_transaction_id",transactionID}
        };
        Track(kIapBuySucc, dict);

#if hq_af_event
        Dictionary<string, string> eventValues = new Dictionary<string, string>();
        eventValues.Add(AFInAppEvents.CURRENCY, product.isoCurrencyCode);
        string price = product.localizedPrice.ToString();
        price = price.Replace(",", ".");
        eventValues.Add(AFInAppEvents.REVENUE, price);
        AppsFlyer.sendEvent(kAfIapPurchase, eventValues);
#endif

#if hq_adjust_event
        if (_adjustEventMap.TryGetValue(kAfIapPurchase, out string adjustEventToken))
        {
            AdjustEvent adjustEvent = new AdjustEvent(adjustEventToken);
            adjustEvent.SetRevenue(decimal.ToDouble(product.localizedPrice), product.isoCurrencyCode);
            Adjust.TrackEvent(adjustEvent);
        }
#endif
    }

    public static void IapBuyFail(string id, string storeId, string scene, HQIapStatus status, string text)
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_product_id",id},
            {"iap_store_id",storeId},
            {"iap_error_type",status.ToString()},
            {"iap_error_info",text},
            {"iap_scene",scene}
        };
        Track(kIapBuyFail, dict);
    }

    public static void IapRestoreSucc()
    {
        Track(kIapRestoreSucc, null);
    }

    public static void IapRestoreFail(HQIapStatus status, string text)
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_error_type",status.ToString()},
            {"iap_error_info",text}
        };
        Track(kIapRestoreFail, dict);
    }

    public static void IapSubscriptionError(HQIapStatus status, string text)
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            {"iap_error_type",status.ToString()},
            {"iap_error_info",text}
        };
        Track(kIapSubscriptionError, dict);
    }



    private static void Track(string eventName, Dictionary<string, object> dict)
    {
        if (dict == null)
        {
            dict = new Dictionary<string, object>();
        }

        dict.Add("vpn_conn", Helper.IsVPNConnected());
        dict.Add("iap_sandbox", Helper.IsSandbox());

        if (Debug.isDebugBuild)
        {
            Debug.Log($"[HQIap] Event:{eventName} Dict:{Newtonsoft.Json.JsonConvert.SerializeObject(dict)}");
        }

#if hq_ta_event
        ThinkingAnalyticsAPI.Track(eventName, dict);
#endif
    }

    public static void TimeEvent(string eventName)
    {
#if hq_ta_event
        ThinkingAnalyticsAPI.TimeEvent(eventName);
#endif
    }
}
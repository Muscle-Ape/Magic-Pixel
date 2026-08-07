using System.Collections.Generic;

public class AOAdsInfo
{
    public string AdUnitIdentifier;//广告单元ID
    // public string AdFormat;//广告类型: INTER REWARDED 
    public string NetworkName;//广告平台名称: APPLOVIN/Unity
    public string NetworkPlacement;//广告位名:  interstitial_22
    public string CreativeIdentifier;//广告创意id
    public double Revenue;//收入
    public AOAdsType AdType;//广告类型(插屏,激励)
    public string AdTypeString { get { return AdTypeToString(AdType); } }//广告类型字符串

    public override string ToString()
    {
        return
        "AdUnitIdentifier:" + AdUnitIdentifier +
        " AdType:" + AdType +
        " NetworkName:" + NetworkName +
        " NetworkPlacement:" + NetworkPlacement +
        " CreativeIdentifier:" + CreativeIdentifier +
        " Revenue:" + Revenue;
        // " RevenuePrecision:" + RevenuePrecision;
    }

    public Dictionary<string, object> ToEvent()
    {
        Dictionary<string, object> dict = new Dictionary<string, object>()
        {
            { "ad_placement", this.NetworkPlacement },
            { "ad_channel", this.NetworkName },
            { "ad_type", this.AdTypeString },
            { "ad_revenue", this.Revenue },
            { "ad_creative_id", this.CreativeIdentifier }
        };
        return dict;
    }

    public static string AdTypeToString(AOAdsType adsType)
    {
        string ad_type = "none";
        if (adsType == AOAdsType.Interstitial)
        {
            ad_type = "inter";
        }
        else if (adsType == AOAdsType.RewardedVideo)
        {
            ad_type = "reward";
        }
        else if (adsType == AOAdsType.AppOpen)
        {
            ad_type = "app_open";
        }
        return ad_type;
    }
}
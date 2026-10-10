/// <summary>项目广告位策略：购买去广告后只关闭强制插屏，不影响主动观看的激励广告。</summary>
public sealed class MPAdsAdSceneController
{
    public bool IsEnabled(string adPlace)
    {
        try
        {
            return MPUser.instance == null
                || !MPUser.instance.OwnsShopEntitlement("remove_ads");
        }
        catch
        {
            // 存档尚未初始化或读取异常时不阻塞启动，最终资格仍由 MPAdsManager 再次校验。
            return true;
        }
    }
}

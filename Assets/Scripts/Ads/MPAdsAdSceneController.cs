/// <summary>项目广告位策略：购买去广告后只关闭强制插屏，不影响用户主动观看的激励广告。</summary>
public sealed class MPAdsAdSceneController : AOAdsBaseAdSceneController
{
    public override bool IsEnabled(string adPlace)
    {
        try
        {
            if (MPUser.instance.OwnsShopEntitlement("remove_ads"))
                return false;
        }
        catch
        {
            // 存档尚未初始化或读取异常时沿用原广告策略，不能影响启动流程。
        }
        return base.IsEnabled(adPlace);
    }
}

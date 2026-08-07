//
//  MAUnityAdManager+AOAdsEvent.m
//  UnityFramework
//
//  Created by 刘欢庆 on 2024/4/30.
//

#import "MAUnityAdManager+AOAdsEvent.h"
#import <objc/runtime.h>
#import "ThinkingAnalyticsSDK.h"
#import <AppsFlyerLib/AppsFlyerLib.h>

#ifdef __cplusplus
extern "C" {
#endif
    
    static bool _adsEventIsReceivedReward = false;
    static int _adsInterNum = 0;
    static int _adsRewardNum = 0;
    static NSString  * const kAdInterNumKey = @"__ios_ad_inter_num__";
    static NSString  * const kAdRewardNumKey = @"__ios_ad_reward_num__";
    
    void _init()
    {
        _adsInterNum = (int)[[NSUserDefaults standardUserDefaults] integerForKey:kAdInterNumKey];
        _adsRewardNum = (int)[[NSUserDefaults standardUserDefaults] integerForKey:kAdRewardNumKey];
    }
    
    void _syncAdsCount(int interNum, int rewardNum)
    {
        [[NSUserDefaults standardUserDefaults] setInteger:interNum forKey:kAdInterNumKey];
        [[NSUserDefaults standardUserDefaults] setInteger:rewardNum forKey:kAdRewardNumKey];
        [[NSUserDefaults standardUserDefaults] synchronize];
        
        _adsInterNum = interNum;
        _adsRewardNum = rewardNum;
    }
    
    int _getInterNum()
    {
        return _adsInterNum;
    }
    
    int _getRewardNum()
    {
        return _adsRewardNum;
    }
    
#ifdef __cplusplus
}
#endif

@implementation MAUnityAdManager (AOAdsEvent)




+ (void)load {
    [self switchDisplayAd];
    [self switchHideAd];
    [self switchRewardUserForAd];
}

- (void)swapperDidDisplayAd:(MAAd *)ad
{
    [self swapperDidDisplayAd:ad];
    _adsEventIsReceivedReward = false;
    NSLog(@"[LOG] swapperDidDisplayAd");
    
    [self UpdateAdCount:ad];
    
    NSMutableDictionary *eventProperties = [self adToDictionary:ad];
    
    
    [eventProperties setObject:ad.placement forKey:@"ad_scene"];
    [eventProperties setObject:@(_adsInterNum) forKey:@"ad_inter_num"];
    [eventProperties setObject:@(_adsRewardNum) forKey:@"ad_reward_num"];
    
    [[ThinkingAnalyticsSDK sharedInstance] timeEvent:@"ad_close"];
    [[ThinkingAnalyticsSDK sharedInstance] track:@"ad_impression" properties:eventProperties];
    
    [self afEventAdStart:ad];
    
    
}

- (void)swapperDidHideAd:(MAAd *)ad
{
    [self swapperDidHideAd:ad];
    NSLog(@"[LOG] swapperDidHideAd");
    
    NSMutableDictionary *eventProperties = [self adToDictionary:ad];
    [eventProperties setObject:ad.placement forKey:@"ad_scene"];
    [eventProperties setObject:@(_adsEventIsReceivedReward) forKey:@"is_complete"];
    
    
    [[ThinkingAnalyticsSDK sharedInstance] track:@"ad_close" properties:eventProperties];
    
    [self afEventAdEnd:ad];
}

- (void)swapperDidRewardUserForAd:(MAAd *)ad withReward:(MAReward *)reward
{
    [self swapperDidRewardUserForAd:ad withReward:reward];
    NSLog(@"[LOG] swapperDidRewardUserForAd");
    _adsEventIsReceivedReward = true;
}


- (void)afEventAdStart:(MAAd *)ad
{
    //AppsFlyer Event
    
    NSDictionary *eventValues = @{
        //广告基础数据
        AFEventParamCurrency: @"USD" ,
        AFEventParamRevenue: [NSString stringWithFormat:@"%.5f",ad.revenue]
    };
    
    
    if(MAAdFormat.interstitial == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_inter_start" withValues:eventValues];
    }
    else if(MAAdFormat.rewarded == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_reward_start" withValues:eventValues];
    }
    else if(MAAdFormat.appOpen == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_appopen_start" withValues:eventValues];
    }
}

- (void)afEventAdEnd:(MAAd *)ad
{
    //AppsFlyer Event
    
    NSDictionary *eventValues = @{
        //广告基础数据
        AFEventParamCurrency: @"USD" ,
        AFEventParamRevenue: [NSString stringWithFormat:@"%.5f",ad.revenue]
    };
    
    if(MAAdFormat.interstitial == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_inter_end" withValues:eventValues];
    }
    else if(MAAdFormat.rewarded == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_reward_end" withValues:eventValues];
    }
    else if(MAAdFormat.appOpen == ad.format)
    {
        [[AppsFlyerLib shared] logEvent:@"ad_appopen_end" withValues:eventValues];
    }
}

+ (void)switchDisplayAd
{
    Method originalMethod = class_getInstanceMethod([self class], @selector(didDisplayAd:));
    Method swizzledMethod = class_getInstanceMethod([self class], @selector(swapperDidDisplayAd:));
    method_exchangeImplementations(originalMethod, swizzledMethod);
}

+ (void)switchHideAd
{
    Method originalMethod = class_getInstanceMethod([self class], @selector(didHideAd:));
    Method swizzledMethod = class_getInstanceMethod([self class], @selector(swapperDidHideAd:));
    method_exchangeImplementations(originalMethod, swizzledMethod);
}

+ (void)switchRewardUserForAd
{
    Method originalMethod = class_getInstanceMethod([self class], @selector(didRewardUserForAd:withReward:));
    Method swizzledMethod = class_getInstanceMethod([self class], @selector(swapperDidRewardUserForAd:withReward:));
    method_exchangeImplementations(originalMethod, swizzledMethod);
}

- (NSMutableDictionary *)adToDictionary:(MAAd *)ad
{
    NSString *ad_type = @"none";
    if(MAAdFormat.interstitial == ad.format)
    {
        ad_type = @"inter";
    }
    else if(MAAdFormat.rewarded == ad.format)
    {
        ad_type = @"reward";
    }
    else if(MAAdFormat.appOpen == ad.format)
    {
        ad_type = @"app_open";
    }
   
    
    NSDictionary *eventProperties = @{
        //广告基础数据
        @"ad_placement": ad.networkPlacement ,
        @"ad_channel": ad.networkName ,
        @"ad_type": ad_type ,
        @"ad_revenue": @(ad.revenue),
        @"ad_creative_id": ad.creativeIdentifier
    };

    return [NSMutableDictionary dictionaryWithDictionary:eventProperties];
}

- (void)UpdateAdCount:(MAAd *)ad
{
    if(MAAdFormat.interstitial == ad.format)
    {
        _adsInterNum++;
        [[NSUserDefaults standardUserDefaults] setInteger:_adsInterNum forKey:kAdInterNumKey];
        [[NSUserDefaults standardUserDefaults] synchronize];
    }
    else if(MAAdFormat.rewarded == ad.format)
    {
        _adsRewardNum++;
        [[NSUserDefaults standardUserDefaults] setInteger:_adsRewardNum forKey:kAdRewardNumKey];
        [[NSUserDefaults standardUserDefaults] synchronize];
    }

}


@end

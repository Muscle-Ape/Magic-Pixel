#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

#if __has_include(<UnityAds/UnityAds-Swift.h>)
#import <UnityAds/UnityAds-Swift.h>
#define MP_HAS_UNITY_ADS_PRIVACY_API 1
#else
#define MP_HAS_UNITY_ADS_PRIVACY_API 0
#endif

static volatile int MPIDFATrackingRequestState = 0;
static const int MPIDFATrackingStatusNotDetermined = 0;
static const int MPIDFATrackingStatusAuthorized = 3;

static void MPIDFAApplyUnityAdsUserConsent(int status)
{
    if (status == MPIDFATrackingStatusNotDetermined)
    {
        return;
    }

    BOOL consentGranted = status == MPIDFATrackingStatusAuthorized;
    void (^applyConsent)(void) = ^{
#if MP_HAS_UNITY_ADS_PRIVACY_API
        // Unity Ads 4.17+ 要求在初始化前通过新 Privacy API 提供明确的 opt-in 信号。
        [UnityAds setUserConsent:consentGranted];
#endif
    };

    if ([NSThread isMainThread])
    {
        applyConsent();
    }
    else
    {
        dispatch_async(dispatch_get_main_queue(), applyConsent);
    }
}

extern "C"
{
    int MPIDFAGetTrackingAuthorizationStatus()
    {
        if (@available(iOS 14.0, *))
        {
            ATTrackingManagerAuthorizationStatus status =
                [ATTrackingManager trackingAuthorizationStatus];
            MPIDFAApplyUnityAdsUserConsent((int)status);
            return (int)status;
        }

        // iOS 14 以下没有 ATT 限制，按已授权处理。
        MPIDFAApplyUnityAdsUserConsent(MPIDFATrackingStatusAuthorized);
        return MPIDFATrackingStatusAuthorized;
    }

    int MPIDFAGetTrackingRequestState()
    {
        return MPIDFATrackingRequestState;
    }

    void MPIDFARequestTrackingAuthorization()
    {
        if (@available(iOS 14.0, *))
        {
            ATTrackingManagerAuthorizationStatus status = [ATTrackingManager trackingAuthorizationStatus];
            if (status != ATTrackingManagerAuthorizationStatusNotDetermined)
            {
                MPIDFAApplyUnityAdsUserConsent((int)status);
                MPIDFATrackingRequestState = 2;
                return;
            }

            if (MPIDFATrackingRequestState == 1)
            {
                return;
            }

            MPIDFATrackingRequestState = 1;
            void (^requestAuthorization)(void) = ^{
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:
                    ^(ATTrackingManagerAuthorizationStatus authorizationStatus)
                    {
                        MPIDFAApplyUnityAdsUserConsent((int)authorizationStatus);
                        MPIDFATrackingRequestState = 2;
                    }];
            };

            if ([NSThread isMainThread])
            {
                requestAuthorization();
            }
            else
            {
                dispatch_async(dispatch_get_main_queue(), requestAuthorization);
            }

            return;
        }

        MPIDFAApplyUnityAdsUserConsent(MPIDFATrackingStatusAuthorized);
        MPIDFATrackingRequestState = 2;
    }
}

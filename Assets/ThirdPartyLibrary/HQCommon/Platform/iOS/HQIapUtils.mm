
#import <Foundation/Foundation.h>

extern "C"
{
    bool _isVPNConnected(void)
    {
        NSDictionary *dict = CFBridgingRelease(CFNetworkCopySystemProxySettings());
            NSArray *keys = [dict[@"__SCOPED__"]allKeys];
            for (NSString *key in keys) {
                if ([key rangeOfString:@"tap"].location != NSNotFound ||
                    [key rangeOfString:@"tun"].location != NSNotFound ||
                    [key rangeOfString:@"ppp"].location != NSNotFound){
                    return YES;
                }
            }
            return NO;
    }

    bool _isSandbox(void)
    {
        NSString *path = [[NSBundle mainBundle].appStoreReceiptURL path];
        if (!path) {
            return NO;
        }
        return [path containsString:@"sandboxReceipt"];
    }
}

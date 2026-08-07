#import <AppLovinSDK/AppLovinSDK.h>
#import <AVFoundation/AVFoundation.h>
extern "C"
{
    void _SetAudioFocus(void)
    {
        ALSdkSettings *sdkSettings = [ALSdk shared].settings;
        [sdkSettings setExtraParameterForKey:@"return_audio_focus" value:@"true"];
    }

    void _ResumeAudio(void)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSError *error;
            //    BOOL isSuccess = [[AVAudioSession sharedInstance] setActive:YES error:&error];
            BOOL isSuccess = [[AVAudioSession sharedInstance] setActive:YES withFlags:AVAudioSessionSetActiveOptionNotifyOthersOnDeactivation error:&error];
            if (isSuccess) {
                // NSLog(@"恭喜你成功设置");
            } else {
                // NSLog(@"设置失败 %@",error);
            }
        });
    }

}

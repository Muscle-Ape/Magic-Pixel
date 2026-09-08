//
//  UnityToast.m
//  DCGameV3ForUnity
//
//  Created by xiabob on 2018/5/8.
//  Copyright © 2018年 Sawadee. All rights reserved.
//

#import "UnityToast.h"
#import <Toast/UIView+Toast.h>

@implementation UnityToast

typedef enum : NSUInteger {
    UnityToastPositionTop = 0,
    UnityToastPositionCenter,
    UnityToastPositionBottom,
} UnityToastPosition;

void _showToast(const char* message, double duration, int position) {
    const NSString *ctPosition = __getViewToastPosition(position);
    NSString *messageString = [NSString stringWithUTF8String:message];
    [[UIApplication sharedApplication].keyWindow makeToast:messageString duration:duration position:ctPosition];
}

void _showToastActivity(int position) {
    [[UIApplication sharedApplication].keyWindow makeToastActivity:__getViewToastPosition(position)];
}

void _hideToastActivity() {
    [[UIApplication sharedApplication].keyWindow hideToastActivity];
}

const NSString* __getViewToastPosition(UnityToastPosition position) {
    switch (position) {
        case UnityToastPositionTop:
            return CSToastPositionTop;
            
        case UnityToastPositionCenter:
            return CSToastPositionCenter;
            
        case UnityToastPositionBottom:
            return CSToastPositionBottom;
            
        default:
            return CSToastPositionCenter;
    }
    return nil;
}

@end

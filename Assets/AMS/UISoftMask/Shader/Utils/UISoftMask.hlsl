// UISoftMask by Alexandre Soria http://sorialexandre.tech
#pragma once

int _WORLDCANVAS;
float2 _RectUvSize;
float4x4 _WorldCanvasMatrix;
float4x4 _OverlayCanvasMatrix;

float4 _MaskDataSettings; // x: mode | y: opacity | z: gamma2linear | w: bleedFactor

/// MaskUV: float4 wPos
float2 MaskUV(float4 wPos)
{
    float2 rectFactor = 1.0 / _RectUvSize.xy;
    float2 worldCanvasUV = mul(_WorldCanvasMatrix, wPos).xy * rectFactor;
    float2 overlayCanvasUV = mul(_OverlayCanvasMatrix, wPos).xy * rectFactor;
    return lerp(overlayCanvasUV, worldCanvasUV, _WORLDCANVAS);
}

/// UISoftMask: float2 maskUV, sampler2D maskSampler, float alpha
float UISoftMask(float2 maskUV, sampler2D maskSampler, float alpha)
{
    float2 trimUV = saturate((1 - max(maskUV, 1 - maskUV)) * 1000);
    half2 mask = tex2D(maskSampler, maskUV).rg * lerp(trimUV.x * trimUV.y, 1, _MaskDataSettings.w);
    mask.x = lerp(mask.x, 1 - mask.x, _MaskDataSettings.x) * mask.g;
    mask.x *= _MaskDataSettings.y;
    return mask.x * alpha;
}

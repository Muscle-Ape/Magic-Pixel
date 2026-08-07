//UISoftMask (Shader Graph) by Alexandre Soria http://sorialexandre.tech
#include "UISoftMask.hlsl"

///MaskData (ShaderGraph): float3 wPos, out float3 maskUV
void MaskUV_float(float3 wPos, out float2 maskUV)
{
    #if USING_SOFT_MASK
    maskUV = MaskUV(float4(wPos, 1));
    #else
    maskUV = 0;
    #endif
}

///UISoftMask (ShaderGraph): float2 maskUV, UnityTexture2D maskSampler, float alpha, out float softMask
void UISoftMask_float(float2 maskUV, UnityTexture2D maskSampler, float alpha, out float softMask)
{
    #if USING_SOFT_MASK
    float2 trimUV = saturate((1 - max(maskUV, 1 - maskUV)) * 1000);
    half2 mask = SAMPLE_TEXTURE2D(maskSampler, maskSampler.samplerstate, maskUV).r * trimUV.x * trimUV.y;
    mask.x = lerp(mask.x, 1 - mask.x, _MaskDataSettings.x) * mask.g;
    mask.x *= _MaskDataSettings.y;
    softMask = mask.x * alpha;
    #else
    softMask = alpha;
    #endif
}

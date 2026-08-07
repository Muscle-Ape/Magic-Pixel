Shader "Hidden/AMS/UISoftMaskBlit"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("_MainTex", 2D) = "white" { }
        [PerRendererData]
        _FallOff ("_FallOff", Float) = 1
        [PerRendererData]
        _Opacity ("_Opacity", Float) = 1
        [PerRendererData]
        _AtlasData ("_AtlasData", Vector) = (1, 1, 0.14, 0)
        [PerRendererData]
        _SliceScale ("_SliceScale", Vector) = (1, 1, 0, 0)
        [PerRendererData]
        _SliceBorder ("_SliceBorder", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
        }

        Blend Off
        AlphaToMask Off
        Cull Back
        ColorMask RGBA
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Unlit"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 2.0

            #include "UnityCG.cginc"

            #pragma shader_feature_local_fragment _SLICED

            struct appdata
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionOS : SV_POSITION;
                float4 uv : TEXCOORD0;
            };

            uniform sampler2D _MainTex;
            uniform float _FallOff;

            uniform sampler2D _ParentMask;
            float4x4 _ParentMaskMatrix;
            float2 _ParentMaskScale; // xy: aspectScale
            float3 _ParentMaskData; // x: mode | y: opacity | z: blledFactor

            uniform float4 _SliceScale; // xy: texSize | z: scale
            uniform float4 _SliceBorder; // x: left | y: top | z: right | w:bottom
            uniform float4 _AtlasData; // xy: scale | wz: position

            // float2 uv, float2 scale, float4 borders;
            float2 Uv9slice(float2 uv, float2 scale, float4 borders)
            {
                half2 totalBorders = borders.xy + borders.zw;
                half2 clampedScale = max(totalBorders, scale);

                half2 t = (clampedScale * uv - borders.xy) / max(clampedScale - borders.xy - borders.zw, 0.001);
                return lerp(uv * clampedScale, 1 - clampedScale * (1 - uv), saturate(t));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv0;
                o.positionOS = UnityObjectToClipPos(v.positionOS);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv.xy;

                half2 center = .5;

                float2 parentMaskUV = uv;
                parentMaskUV = mul(_ParentMaskMatrix, float4(parentMaskUV - center, 0, 1)).xy * _ParentMaskScale.xy +
                    center;

                float2 trimUV = saturate((1 - max(parentMaskUV, 1 - parentMaskUV)) * 1000);
                float2 parentMask = tex2D(_ParentMask, parentMaskUV).rg *
                                    lerp(trimUV.x * trimUV.y, 1, _ParentMaskData.z);

                #ifdef _SLICED
                uv = Uv9slice(uv, _SliceScale, _SliceBorder);
                #endif

                float mask = tex2D(_MainTex, uv * _AtlasData.xy + _AtlasData.zw).a;
                mask = smoothstep(0, _FallOff, mask);

                parentMask.r = lerp(parentMask.r, 1 - parentMask.r, _ParentMaskData.x) * _ParentMaskData.y *
                    parentMask.g;

                return half4(mask, parentMask.r, 0, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
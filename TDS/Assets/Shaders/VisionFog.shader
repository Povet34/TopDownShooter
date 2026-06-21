Shader "TDS/VisionFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.04, 0.05, 0.07, 1)
        _MaxDarkness ("Max Darkness", Range(0,1)) = 0.8
        _BlurSize ("Edge Blur (UV)", Range(0,0.02)) = 0.005
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "VisionFog"
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionMask);
            SAMPLER(sampler_VisionMask); // 전역 RT를 강제로 bilinear-clamp 샘플(texel 블록 방지)
            float4 _VisionMaskCenterSize; // xy = 월드 중심(XZ), zw = 월드 크기(XZ)
            float4 _FogColor;
            float _MaxDarkness;
            float _BlurSize;

            float SampleMaskBlur(float2 uv)
            {
                float o = _BlurSize;
                float m = 0.0;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2(-o, -o)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2( 0, -o)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2( o, -o)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2(-o,  0)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2( o,  0)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2(-o,  o)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2( 0,  o)).r;
                m += SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv + float2( o,  o)).r;
                return m / 9.0;
            }

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 worldPos : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(IN.positionOS.xyz);
                o.worldPos = wp;
                o.positionHCS = TransformWorldToHClip(wp);
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = (IN.worldPos.xz - _VisionMaskCenterSize.xy) / _VisionMaskCenterSize.zw + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y; // RT를 카메라로 렌더하면 D3D에서 Y가 뒤집힘 — 보정
                #endif
                float m = SampleMaskBlur(uv);
                // 마스크 영역 밖 = 시야 밖(어둡게)
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) m = 0.0;
                float darkness = (1.0 - saturate(m)) * _MaxDarkness;
                return half4(_FogColor.rgb, darkness);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

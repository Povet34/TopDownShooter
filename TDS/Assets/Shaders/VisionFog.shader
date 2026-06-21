Shader "TDS/VisionFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.04, 0.05, 0.07, 1)
        _MaxDarkness ("Max Darkness", Range(0,1)) = 0.8
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
            SAMPLER(sampler_VisionMask);
            float4 _VisionMaskCenterSize; // xy = 월드 중심(XZ), zw = 월드 크기(XZ)
            float4 _FogColor;
            float _MaxDarkness;

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
                float m = SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv).r;
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

// 사막 바닥을 통짜 타일이 아니라 노이즈로 블렌딩해 자연스럽게 보이게 하는 URP 커스텀 Lit 셰이더.
// - 모래(_BaseMap)를 두 스케일로 섞어 반복(타일링) 티를 깬다(anti-tiling).
// - 월드 XZ 노이즈로 바위 패치(_SecondMap)를 군데군데 얹는다(타일링과 독립 → 거대 패턴이 자연스러움).
// - 큰 스케일 노이즈로 미세 명암(macro tint)을 줘 단조로움을 더 줄인다.
// 바닥은 평평하지만 노멀맵으로 입체감을 준다. URP 표준 PBR 라이팅/그림자 수신.
Shader "TDS/DesertGroundBlend"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sand (Base)", 2D) = "white" {}
        _BumpMap        ("Sand Normal", 2D) = "bump" {}
        _SecondMap      ("Rock (Patches)", 2D) = "white" {}
        _SecondBump     ("Rock Normal", 2D) = "bump" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness     ("Smoothness", Range(0,1)) = 0.12
        _NoiseScale     ("Macro Noise Scale (1/world units)", Float) = 0.02
        _PatchAmount    ("Rock Patch Amount", Range(0,1)) = 0.30
        _PatchSharpness ("Patch Edge Sharpness", Range(0.01,0.5)) = 0.14
        _NormalStrength ("Normal Strength", Range(0,2)) = 1.0
        _MacroTint      ("Macro Tint Strength", Range(0,1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SecondMap);  SAMPLER(sampler_SecondMap);
            TEXTURE2D(_SecondBump); SAMPLER(sampler_SecondBump);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Smoothness;
                float  _NoiseScale;
                float  _PatchAmount;
                float  _PatchSharpness;
                float  _NormalStrength;
                float  _MacroTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3; // xyz dir, w sign
                float  fogCoord    : TEXCOORD4;
            };

            // 해시 기반 값 노이즈 (외부 텍스처 불필요)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0.0, amp = 0.5;
                [unroll] for (int i = 0; i < 4; i++) { s += amp * vnoise(p); p *= 2.03; amp *= 0.5; }
                return s;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.tangentWS   = float4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 wp = IN.positionWS.xz;
                float macro = fbm(wp * _NoiseScale);
                float patchN = fbm(wp * _NoiseScale * 2.0 + 13.7);
                float patch  = smoothstep(1.0 - _PatchAmount - _PatchSharpness,
                                          1.0 - _PatchAmount + _PatchSharpness, patchN);

                // 모래는 풀디테일 유지(결을 흐리지 않음). 노이즈는 바위 패치 + 미세 명암에만 쓴다.
                float2 uvA = IN.uv;
                half3 sand  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvA).rgb;
                half3 rock  = SAMPLE_TEXTURE2D(_SecondMap, sampler_SecondMap, uvA).rgb;
                half3 albedo = lerp(sand, rock, patch);          // 부드러운 노이즈 경계로 모래↔바위
                albedo *= lerp(1.0 - _MacroTint, 1.0 + _MacroTint, macro); // 거대 스케일 명암 변주
                albedo *= _BaseColor.rgb;

                // 노멀(모래/바위 노멀을 패치로 섞음)
                half3 nSand = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvA), _NormalStrength);
                half3 nRock = UnpackNormalScale(SAMPLE_TEXTURE2D(_SecondBump, sampler_SecondBump, uvA), _NormalStrength);
                half3 tn = normalize(lerp(nSand, nRock, patch));

                float sgn = IN.tangentWS.w;
                float3 bitangent = sgn * cross(IN.normalWS.xyz, IN.tangentWS.xyz);
                float3x3 tbn = float3x3(IN.tangentWS.xyz, bitangent, IN.normalWS.xyz);
                float3 normalWS = normalize(mul(tn, tbn));

                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = IN.fogCoord;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo;
                surfaceData.alpha      = 1.0;
                surfaceData.metallic   = 0.0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion  = 1.0;
                surfaceData.normalTS   = tn;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // 폴백(URP Lit)을 쓰면 키워드 스페이스가 달라 "incompatible keyword space" assert가 난다.
        // → 폴백 대신 그림자/뎁스/노멀 패스를 직접 둔다(자족적, 충돌 없음).

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V vert(A IN)
            {
                V OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - posWS);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, ld));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = cs;
                return OUT;
            }
            half4 frag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V vert(A IN) { V OUT; OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half frag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            V vert(A IN)
            {
                V OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 frag(V IN) : SV_Target { return half4(normalize(IN.normalWS), 0.0); }
            ENDHLSL
        }
    }
}

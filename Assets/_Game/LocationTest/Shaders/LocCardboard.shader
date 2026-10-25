Shader "BuilderSim/LocationCardboard"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Shade ("Shadow tint", Color) = (0.56,0.48,0.70,1)
        _MainTex ("Texture (solid mode)", 2D) = "white" {}
        _Tex0 ("Layer 0", 2D) = "white" {}
        _Tex1 ("Layer 1", 2D) = "white" {}
        _Tex2 ("Layer 2", 2D) = "white" {}
        _Tex3 ("Layer 3", 2D) = "white" {}
        _Tex4 ("Layer 4 (rock, triplanar)", 2D) = "white" {}
        _Tex5 ("Layer 5", 2D) = "white" {}
        _Tex6 ("Layer 6", 2D) = "white" {}
        _Tex7 ("Layer 7", 2D) = "white" {}
        _Ctrl0 ("Splat 0-3", 2D) = "white" {}
        _Ctrl1 ("Splat 4-7", 2D) = "black" {}
        _Grain ("Paper grain", 2D) = "gray" {}
        _TileM ("Metres per tile", Float) = 3
        _Sharp ("Edge crispness", Float) = 3
        _Wrap ("Light wrap", Range(0,1)) = 0.35
        _Rim ("Rim light", Range(0,1)) = 0.15
        _GrainAmt ("Paper grain", Range(0,1)) = 0.6
        _ContourStep ("Cardboard sheet height, m", Float) = 0.45
        _ContourAmt ("Sheet line strength", Range(0,1)) = 0.14
        _ContourBase ("Sheet line origin (world Y), m", Float) = 0
        _Bright ("Brightness", Range(0.5,1.6)) = 1
        _Sat ("Saturation", Range(0.5,2)) = 1.2
        _MossCol ("Moss colour", Color) = (0.52,0.78,0.36,1)
        _MossAmt ("Moss on top faces", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _SPLAT_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Shade;
                float _TileM;
                float _Sharp;
                half _Wrap;
                half _Rim;
                half _GrainAmt;
                float _ContourStep;
                half _ContourAmt;
                float _ContourBase;
                half _Bright;
                half _Sat;
                half4 _MossCol;
                half _MossAmt;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Tex0); SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1);
            TEXTURE2D(_Tex2);
            TEXTURE2D(_Tex3);
            TEXTURE2D(_Tex4);
            TEXTURE2D(_Tex5);
            TEXTURE2D(_Tex6);
            TEXTURE2D(_Tex7);
            TEXTURE2D(_Ctrl0); SAMPLER(sampler_Ctrl0);
            TEXTURE2D(_Ctrl1);
            TEXTURE2D(_Grain); SAMPLER(sampler_Grain);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float3 pw = i.positionWS;
                float tile = max(_TileM, 0.1);
                float2 tuv = pw.xz / tile;

                half3 bw = pow(abs(n), 4.0);
                bw = bw / (bw.x + bw.y + bw.z + 0.0001);

            #if defined(_SPLAT_ON)
                half4 c0 = SAMPLE_TEXTURE2D(_Ctrl0, sampler_Ctrl0, i.uv);
                half4 c1 = SAMPLE_TEXTURE2D(_Ctrl1, sampler_Ctrl0, i.uv);
                float sp = max(_Sharp, 1.0);
                float w0 = pow(max(c0.r, 0.0001), sp);
                float w1 = pow(max(c0.g, 0.0001), sp);
                float w2 = pow(max(c0.b, 0.0001), sp);
                float w3 = pow(max(c0.a, 0.0001), sp);
                float w4 = pow(max(c1.r, 0.0001), sp);
                float w5 = pow(max(c1.g, 0.0001), sp);
                float w6 = pow(max(c1.b, 0.0001), sp);
                float w7 = pow(max(c1.a, 0.0001), sp);
                float wsum = w0 + w1 + w2 + w3 + w4 + w5 + w6 + w7;
                half3 acc = SAMPLE_TEXTURE2D(_Tex0, sampler_Tex0, tuv).rgb * w0;
                acc += SAMPLE_TEXTURE2D(_Tex1, sampler_Tex0, tuv).rgb * w1;
                acc += SAMPLE_TEXTURE2D(_Tex2, sampler_Tex0, tuv).rgb * w2;
                acc += SAMPLE_TEXTURE2D(_Tex3, sampler_Tex0, tuv).rgb * w3;
                acc += SAMPLE_TEXTURE2D(_Tex5, sampler_Tex0, tuv).rgb * w5;
                acc += SAMPLE_TEXTURE2D(_Tex6, sampler_Tex0, tuv).rgb * w6;
                acc += SAMPLE_TEXTURE2D(_Tex7, sampler_Tex0, tuv).rgb * w7;
                half3 rockC = SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, pw.zy / tile).rgb * bw.x
                            + SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, tuv).rgb * bw.y
                            + SAMPLE_TEXTURE2D(_Tex4, sampler_Tex0, pw.xy / tile).rgb * bw.z;
                acc += rockC * w4;
                half3 albedo = acc / max(wsum, 0.0001);
            #else
                half3 albedo = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pw.zy / tile).rgb * bw.x
                              + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tuv).rgb * bw.y
                              + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pw.xy / tile).rgb * bw.z) * _Tint.rgb;
            #endif

                // крупная вариация тона: чтобы плитка текстуры не читалась
                half macroN = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, pw.xz * 0.021).r;
                albedo *= 0.96 + 0.08 * macroN;
                half lumA = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = saturate(lerp(half3(lumA, lumA, lumA), albedo, _Sat));
                // мох на верхних гранях камней (рваный край от шума)
                half mn = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, pw.xz * 0.4).r;
                half mossK = smoothstep(0.45, 0.62, smoothstep(0.5, 0.85, n.y) + (mn - 0.5) * 0.7) * _MossAmt;
                albedo = lerp(albedo, _MossCol.rgb, mossK);

                // листы картона: тёмная линия через каждые _ContourStep метров высоты + светлая фаска под ней; на крутом сильнее
                half contour = 1.0;
                if (_ContourStep > 0.001)
                {
                    float fr = frac((pw.y - _ContourBase) / max(_ContourStep, 0.001));
                    half dark = 1.0 - smoothstep(0.0, 0.09, fr);
                    half bevel = smoothstep(0.09, 0.14, fr) * (1.0 - smoothstep(0.14, 0.26, fr));
                    half steep = saturate((1.0 - n.y) * 2.5);
                    half k = 0.15 + 0.85 * steep;
                    contour = 1.0 - _ContourAmt * dark * k + _ContourAmt * 0.55 * bevel * k;
                }

                float2 guv = pw.xz * 0.8 + pw.y * float2(0.55, 0.35);
                half g1 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, guv).r;
                half g2 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, guv * 3.7 + 0.37).r;

                float4 sc = TransformWorldToShadowCoord(pw);
                Light l = GetMainLight(sc);
                half ndl = dot(n, l.direction);
                // мультяшный свет: три чёткие полосы (цветная тень / середина / тёплый свет), без грязных градиентов
                half w = saturate((ndl + _Wrap) / (1.0 + _Wrap)) + (g1 - 0.5) * 0.05 * _GrainAmt;
                w = lerp(min(w, 0.30), w, l.shadowAttenuation);
                half b1 = smoothstep(0.28, 0.37, w);
                half b2 = smoothstep(0.60, 0.70, w);
                half3 midC = lerp(_Shade.rgb, half3(1, 1, 1), 0.66);
                half3 lit = lerp(_Shade.rgb, midC, b1);
                lit = lerp(lit, half3(1.07, 1.03, 0.92), b2);
                half3 lc = lerp(half3(1, 1, 1), saturate(l.color), 0.5);
                half hemi = lerp(0.95, 1.04, n.y * 0.5 + 0.5);
                half3 col = albedo * lit * lc * contour * _Bright * hemi;
            #if defined(_SPLAT_ON)
                col *= lerp(half3(1, 1, 1), i.color.rgb, 0.55);
            #endif
                col *= lerp(1.0, 0.97 + 0.06 * g2, _GrainAmt);

                half3 v = normalize(GetWorldSpaceViewDir(pw));
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                col += rim * lc * half3(1.0, 0.93, 0.82) * 0.3;

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertS
            #pragma fragment fragS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 vertS(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragS() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertD
            #pragma fragment fragD

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 vertD(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragD() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
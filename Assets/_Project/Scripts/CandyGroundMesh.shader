// Assets/_Project/Shaders/Candy/CandyGroundMesh.shader
// Земля для МЕША Surface из LocationBuilderTester (картонный режим): та же раскраска по 8 слоям, но без текстур.
// В отличие от Project/CandyGround (Terrain) тут обычный меш, splatmap берётся по uv0 (0..1 по острову).
//
// Порядок слоёв Tester: 0 трава, 1 трава светлая (и площадки), 2 тропа, 3 земля, 4 скала, 5 лесная подстилка, 6 песок, 7 тёмная тропа.
//  * Трава (0, 1) = лайм/мята по шуму + ворсинки из CandyGround.hlsl. Остальные слои плоские цвета (_C2.._C7), без текстур.
//  * Галька на тропе (слои 2 и 7) по шуму, затухает с расстоянием. Скала: лёгкая вариация тона по шуму вместо трипланара.
//  * Свет, тени, контурные «листы картона», рим, яркость и насыщенность: как в BuilderSim/LocationCardboard, те же свойства,
//    поэтому земля совпадает с остальными объектами Tester. Зерно «бумаги» и мох (были на текстуре _Grain) убраны.
//  * Минус по сравнению с LocCardboard: 0 текстур слоёв (было 8 + зерно), 2 выборки splatmap вместо 10+.
//  * Проходы: ForwardLit, ShadowCaster, DepthOnly. Варианты теней урезаны под мобильные.
Shader "Project/CandyGroundMesh"
{
    Properties
    {
        [NoScaleOffset] _Ctrl0 ("Splat 0-3", 2D) = "white" {}
        [NoScaleOffset] _Ctrl1 ("Splat 4-7", 2D) = "black" {}

        [Header(Layer colors)]
        _C2 ("2 Trail", Color) = (0.94, 0.79, 0.56, 1)
        _C3 ("3 Soil", Color) = (0.60, 0.42, 0.30, 1)
        _C4 ("4 Rock", Color) = (0.72, 0.68, 0.78, 1)
        _C5 ("5 Forest floor", Color) = (0.30, 0.57, 0.33, 1)
        _C6 ("6 Sand", Color) = (0.97, 0.88, 0.64, 1)
        _C7 ("7 Trail dark", Color) = (0.78, 0.60, 0.42, 1)
        _GrassLight ("Light grass (layer 1) brightness", Range(1, 1.4)) = 1.12
        _Pebbles ("Pebbles on trail (1/m)", Range(2, 20)) = 9
        _PebbleStrength ("Pebble strength", Range(0, 0.4)) = 0.14
        _PebbleFade ("Pebble fade distance (m)", Float) = 30

        [Header(Cardboard look)]
        _Shade ("Shadow tint", Color) = (0.62, 0.54, 0.86, 1)
        _Sharp ("Edge crispness", Float) = 5
        _Wrap ("Light wrap", Range(0, 1)) = 0.45
        _Rim ("Rim light", Range(0, 1)) = 0.25
        _ContourStep ("Cardboard sheet height, m", Float) = 0.45
        _ContourAmt ("Sheet line strength", Range(0, 1)) = 0.03
        _ContourBase ("Sheet line origin (world Y), m", Float) = 0
        _Bright ("Brightness", Range(0.5, 1.6)) = 1.08
        _Sat ("Saturation", Range(0.5, 2)) = 1.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _C2;
            half4 _C3;
            half4 _C4;
            half4 _C5;
            half4 _C6;
            half4 _C7;
            float _GrassLight;
            float _Pebbles;
            float _PebbleStrength;
            float _PebbleFade;
            half4 _Shade;
            float _Sharp;
            half _Wrap;
            half _Rim;
            float _ContourStep;
            half _ContourAmt;
            float _ContourBase;
            half _Bright;
            half _Sat;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/Candy/CandyGround.hlsl"

            TEXTURE2D(_Ctrl0); SAMPLER(sampler_Ctrl0);
            TEXTURE2D(_Ctrl1); SAMPLER(sampler_Ctrl1);

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
                half fog : TEXCOORD3;
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
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float3 pw = i.positionWS;
                float2 xz = pw.xz;
                float camD = distance(_WorldSpaceCameraPos, pw);

                half4 c0 = SAMPLE_TEXTURE2D(_Ctrl0, sampler_Ctrl0, i.uv);
                half4 c1 = SAMPLE_TEXTURE2D(_Ctrl1, sampler_Ctrl1, i.uv);
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

                // трава (слои 0 и 1): считаем один раз, светлая = та же трава ярче
                float3 grass = CGGrass(xz, camD, 0.0);
                float3 acc = grass * (w0 + w1 * _GrassLight);

                // тропа и тёмная тропа: плоский цвет + галька по шуму
                float3 trail = _C2.rgb;
                float3 trailD = _C7.rgb;
                float pf = 1.0 - smoothstep(_PebbleFade * 0.5, _PebbleFade, camD);
                if (pf > 0.01 && (w2 + w7) > 0.01)
                {
                    float h = CGHash(floor(xz * _Pebbles));
                    float k = 1.0 + (step(0.92, h) - step(h, 0.06) * 0.7) * _PebbleStrength * pf;
                    trail *= k;
                    trailD *= k;
                }
                acc += trail * w2 + trailD * w7;
                acc += _C3.rgb * w3 + _C5.rgb * w5 + _C6.rgb * w6;

                // скала: вариация тона по шуму (по высоте и XZ: видно и на склонах)
                float rv = CGVNoise(float2(pw.x + pw.z, pw.y * 1.7) * 0.9);
                acc += _C4.rgb * (0.9 + 0.2 * rv) * w4;
                half3 albedo = (half3)(acc / max(wsum, 0.0001));

                // крупная вариация тона, чтобы цвет не читался плиткой
                albedo *= 0.96h + 0.08h * (half)CGVNoise(xz * 0.05);
                half lumA = dot(albedo, half3(0.299, 0.587, 0.114));
                albedo = saturate(lerp(half3(lumA, lumA, lumA), albedo, _Sat));

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

                Light l = GetMainLight(TransformWorldToShadowCoord(pw));
                half ndl = dot(n, l.direction);
                // мультяшный свет: три чёткие полосы (цветная тень / середина / тёплый свет)
                half w = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                w = lerp(min(w, 0.30), w, l.shadowAttenuation);
                half b1 = smoothstep(0.28, 0.37, w);
                half b2 = smoothstep(0.60, 0.70, w);
                half3 midC = lerp(_Shade.rgb, half3(1, 1, 1), 0.66);
                half3 lit = lerp(_Shade.rgb, midC, b1);
                lit = lerp(lit, half3(1.07, 1.03, 0.92), b2);
                half3 lc = lerp(half3(1, 1, 1), saturate(l.color), 0.5);
                half hemi = lerp(0.95, 1.04, n.y * 0.5 + 0.5);
                half3 col = albedo * lit * lc * contour * _Bright * hemi;
                col *= lerp(half3(1, 1, 1), i.color.rgb, 0.55);   // затемнение во впадинах из цвета вершин

                half3 v = normalize(GetWorldSpaceViewDir(pw));
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                col += rim * lc * half3(1.0, 0.93, 0.82) * 0.3;

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertS
            #pragma fragment fragS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

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

            half4 fragS() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertD
            #pragma fragment fragD

            float4 vertD(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half fragD() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}

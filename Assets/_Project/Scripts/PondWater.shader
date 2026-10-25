// Assets/_Project/Shaders/Pond/PondWater.shader
// Мультяшная вода пруда. URP, полупрозрачная (дно, водоросли и рыбки видны сквозь неё).
//
// Что читает из меша (его строит PondBuilder):
//   цвет R   расстояние от центра: 0 центр, 1 линия воды у берега (чуть больше 1 под бортиком)
//
// Что рисует:
//   * цвет по «глубине» (мелко бирюзовый -> глубоко синий), прозрачность тоже растёт к центру
//   * мягкие тун-полосы волн, облака в отражении (медленно плывут), френель с цветом неба
//   * мерцающие искры, белая кромка пены у берега
//   * круги на воде: PondLife кладёт до 6 колец в глобальный массив _PondRipples (xy = мировой XZ, z = возраст, w = сила)
//   * тёплые блики и отсвет от фонарей (дополнительные источники света)
Shader "Project/Pond/Water"
{
    Properties
    {
        _ShallowColor ("Мелко (rgb) / прозрачность (a)", Color) = (0.52, 0.93, 0.92, 0.28)
        _DeepColor ("Глубоко (rgb) / прозрачность (a)", Color) = (0.14, 0.56, 0.90, 0.58)
        _FoamColor ("Цвет пены и кругов", Color) = (0.95, 1.0, 1.0, 1)
        _SkyColor ("Цвет неба в отражении", Color) = (0.80, 0.93, 1.0, 1)
        _HighlightColor ("Цвет полос волн", Color) = (0.85, 1.0, 1.0, 1)
        _SparkleColor ("Цвет искр", Color) = (1.0, 0.98, 0.85, 1)

        [Header(Waves and sky)]
        _WaveScale ("Частота волн, 1/м", Range(0.1, 3)) = 0.75
        _CloudCut ("Порог облаков", Range(0.3, 0.9)) = 0.52
        _CloudAmount ("Сила облаков", Range(0, 1)) = 0.32
        _FresnelPow ("Острота френеля", Range(0.5, 8)) = 3.0
        _SparkleScale ("Частота искр, 1/м", Range(1, 12)) = 4.5
        _SparkleGain ("Яркость искр", Range(0, 3)) = 1.4

        [Header(Shore)]
        _FoamStart ("Начало пены (0..1)", Range(0.5, 0.98)) = 0.84

        [Header(Light)]
        _SpecPow ("Острота блика солнца", Range(8, 256)) = 90
        _SpecGain ("Яркость блика солнца", Range(0, 2)) = 0.8
        _LightGlow ("Отсвет от фонарей", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _SkyColor;
                float4 _HighlightColor;
                float4 _SparkleColor;
                float _WaveScale;
                float _CloudCut;
                float _CloudAmount;
                float _FresnelPow;
                float _SparkleScale;
                float _SparkleGain;
                float _FoamStart;
                float _SpecPow;
                float _SpecGain;
                float _LightGlow;
            CBUFFER_END

            // круги от PondLife (глобальный массив, ставится Shader.SetGlobalVectorArray)
            float4 _PondRipples[6];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float c : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.c = v.color.r;
                o.fog = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += VNoise(p) * a;
                    p = p * 2.03 + 17.3;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float t = _Time.y;
                float c = i.c;
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // цвет и прозрачность по глубине
                float depthK = 1.0 - smoothstep(0.15, 1.0, c);
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthK);
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthK);

                // мягкие полосы волн
                float w1 = VNoise(p * _WaveScale + float2(t * 0.11, t * 0.07));
                float w2 = VNoise(p * _WaveScale * 1.9 - float2(t * 0.09, t * 0.14));
                float wave = w1 * 0.6 + w2 * 0.4;
                float lines = smoothstep(0.58, 0.63, wave) - smoothstep(0.69, 0.74, wave);
                col += _HighlightColor.rgb * (lines * 0.30);

                // облака в отражении
                float cl = Fbm(p * 0.16 + float2(t * 0.02, t * 0.012));
                float cloud = smoothstep(_CloudCut, _CloudCut + 0.14, cl) * _CloudAmount;
                col = lerp(col, _SkyColor.rgb, cloud);
                alpha = max(alpha, cloud * 0.55);

                // френель: под скользящим углом вода «зеркальнее»
                float fres = pow(1.0 - saturate(V.y), _FresnelPow);
                col = lerp(col, _SkyColor.rgb, fres * 0.55);
                alpha = lerp(alpha, 0.95, fres * 0.5);

                // искры
                float2 g = p * _SparkleScale;
                float2 gi = floor(g);
                float2 gf = frac(g) - 0.5;
                float h = Hash21(gi);
                float jitter = Hash21(gi + 7.3) - 0.5;
                float tw = pow(saturate(sin(t * (1.2 + h * 2.4) + h * 31.0)), 6.0);
                float spark = step(0.84, h) * tw * smoothstep(0.34, 0.0, length(gf + jitter * 0.4));
                col += _SparkleColor.rgb * (spark * _SparkleGain);
                alpha = max(alpha, spark * 0.9);

                // пена у берега
                float fn = VNoise(p * 3.2 + float2(t * 0.35, -t * 0.2));
                float foam = smoothstep(_FoamStart + fn * 0.05, _FoamStart + 0.13 + fn * 0.04, c);
                float foamLine = 1.0 - smoothstep(0.0, 0.018, abs(c - (_FoamStart - 0.05 + 0.02 * sin(t * 0.9 + fn * 6.0))));
                col = lerp(col, _FoamColor.rgb, saturate(foam * 0.85 + foamLine * 0.35));
                alpha = max(alpha, saturate(foam * 0.95 + foamLine * 0.4));

                // круги на воде
                float rip = 0.0;
                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    float4 r = _PondRipples[k];
                    float age = r.z;
                    float d = distance(p, r.xy);
                    float rad = age * 1.5 + 0.04;
                    float wd = 0.06 + age * 0.05;
                    float ring = 1.0 - smoothstep(0.0, wd, abs(d - rad));
                    float ring2 = 1.0 - smoothstep(0.0, wd * 0.8, abs(d - rad * 0.6));
                    rip += (ring + ring2 * 0.6) * r.w * saturate(1.0 - age / 2.3);
                }
                rip = saturate(rip);
                col = lerp(col, _FoamColor.rgb, rip * 0.75);
                alpha = max(alpha, rip * 0.9);

                // солнце: яркость и тун-блик
                Light L = GetMainLight();
                float lum = saturate(dot(L.color, float3(0.3, 0.59, 0.11)));
                col *= lerp(0.65, 1.0, lum);
                float3 N = normalize(float3((w1 - 0.5) * 0.25, 1.0, (w2 - 0.5) * 0.25));
                float3 Hv = normalize(L.direction + V);
                float spec = smoothstep(0.55, 0.62, pow(saturate(dot(N, Hv)), _SpecPow));
                col += L.color * (spec * _SpecGain);
                alpha = max(alpha, spec * 0.8);

                // фонари: тёплый отсвет и блики
                float3 add = float3(0.0, 0.0, 0.0);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS) || defined(_CLUSTER_LIGHT_LOOP)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, i.positionWS);
                        float att = al.distanceAttenuation;
                        float3 Hl = normalize(al.direction + V);
                        float sp = smoothstep(0.5, 0.7, pow(saturate(dot(N, Hl)), 60.0));
                        add += al.color * (att * (_LightGlow + sp * 1.5));
                    LIGHT_LOOP_END
                }
                #endif
                col += add;
                alpha = max(alpha, saturate(max(add.r, max(add.g, add.b))) * 0.7);

                col = MixFog(col, i.fog);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}

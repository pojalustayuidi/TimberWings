// Assets/_Project/Shaders/Pond/PondToon.shader
// Тун-шейдер всего «декора» пруда (берег, камни, мостик, камыш, цветы, фонари, рыбки). URP, forward.
//
// Что читает из меша:
//   цвет RGB   альбедо (PondBuilder кладёт уже в linear, если проект в Linear)
//   цвет A     вес покачивания на ветру (0 жёсткое, 1 свободный кончик): камыш, трава, цветы, водоросли
//   UV0.x      свечение (0 нет ... 1 стекло фонаря); яркость растёт ночью через _Glow
//
// * свет в 3 ступени (как TreeToon), тени от главного света, ambient (SH)
// * дополнительные источники (фонари, подсветка воды) красят берег и мостик тёплым светом (Forward и Forward+)
// * рыбки: _WiggleAmp > 0 включает вилянье хвостом по оси Z меша; фазу задаёт PondLife через MaterialPropertyBlock (_Phase)
Shader "Project/Pond/Toon"
{
    Properties
    {
        _Tint ("Общий оттенок", Color) = (1, 1, 1, 1)

        [Header(Toon lighting)]
        _Step1 ("Порог 1", Range(0, 1)) = 0.50
        _Step2 ("Порог 2", Range(0, 1)) = 0.80
        _StepSoft ("Мягкость ступеней", Range(0.001, 0.3)) = 0.06
        _Level0 ("Уровень 0 (тень)", Range(0, 1.5)) = 0.58
        _Level1 ("Уровень 1", Range(0, 1.5)) = 0.82
        _Level2 ("Уровень 2 (свет)", Range(0, 1.5)) = 1.0
        _ShadowTint ("Цвет тени", Color) = (0.74, 0.82, 1.0, 1)
        _ShadowStrength ("Сила теней", Range(0, 1)) = 0.8
        _ShadowNormalBias ("Смещение теневого сэмпла, м", Range(0, 0.3)) = 0.05
        _Ambient ("Ambient (SH)", Range(0, 1)) = 0.45

        [Header(Rim)]
        _RimColor ("Цвет контрового", Color) = (1.0, 0.97, 0.75, 1)
        _RimStrength ("Сила контрового", Range(0, 1)) = 0.12
        _RimPower ("Острота контрового", Range(0.5, 8)) = 3

        [Header(Glow and extra lights)]
        _Glow ("Ночное свечение 0..1 (ставит PondLife)", Range(0, 1)) = 0.6
        _EmisDay ("Свечение днём (доля от ночного)", Range(0, 1)) = 0.5
        _EmisBoost ("Яркость свечения", Range(0, 8)) = 2.5
        _LightGain ("Сила доп. источников (фонари)", Range(0, 3)) = 1.0

        [Header(Sway reeds grass flowers)]
        _SwayAmp ("Амплитуда качания, м", Range(0, 0.5)) = 0.07
        _SwaySpeed ("Скорость качания", Range(0, 6)) = 1.4

        [Header(Fish wiggle)]
        _WiggleAmp ("Амплитуда виляния (0 = выкл)", Range(0, 0.5)) = 0
        _WiggleSpeed ("Скорость виляния", Range(0, 20)) = 8
        _WiggleLen ("Длина хвоста по Z", Range(0.1, 1)) = 0.5
        _Phase ("Фаза (ставит PondLife)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Step1;
            float _Step2;
            float _StepSoft;
            float _Level0;
            float _Level1;
            float _Level2;
            float4 _ShadowTint;
            float _ShadowStrength;
            float _ShadowNormalBias;
            float _Ambient;
            float4 _RimColor;
            float _RimStrength;
            float _RimPower;
            float _Glow;
            float _EmisDay;
            float _EmisBoost;
            float _LightGain;
            float _SwayAmp;
            float _SwaySpeed;
            float _WiggleAmp;
            float _WiggleSpeed;
            float _WiggleLen;
            float _Phase;
        CBUFFER_END

        // покачивание (вес в альфе цвета) и вилянье рыбки; всё в object space
        float3 PondPosOS(float3 p, float sway)
        {
            float ph = dot(p.xz, float2(0.9, 0.6));
            float t = _Time.y * _SwaySpeed;
            p.x += sin(t + ph) * _SwayAmp * sway;
            p.z += cos(t * 0.8 + ph * 1.3) * _SwayAmp * 0.6 * sway;

            float tail = saturate(-p.z / max(_WiggleLen, 0.01));
            p.x += sin(_Time.y * _WiggleSpeed + _Phase + p.z * 6.0) * _WiggleAmp * tail * tail;
            return p;
        }
        ENDHLSL

        // ------------------------------------------------------------------ основной проход
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // доп. источники: фонари. Оба ключа Forward+ (имя зависит от версии URP), лишний просто не включится
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 color : TEXCOORD2;
                float emis : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posOS = PondPosOS(v.positionOS.xyz, v.color.a);
                VertexPositionInputs vp = GetVertexPositionInputs(posOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color.rgb;
                o.emis = v.uv.x;
                o.fog = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 albedo = i.color * _Tint.rgb;

                // главный свет, тун-ступени
                float4 sc = TransformWorldToShadowCoord(i.positionWS + N * _ShadowNormalBias);
                Light L = GetMainLight(sc);
                float ndl = dot(N, L.direction) * 0.5 + 0.5;
                float shadow = lerp(1.0, L.shadowAttenuation, _ShadowStrength);
                float x = ndl * shadow;

                float s = _StepSoft;
                float b1 = smoothstep(_Step1 - s, _Step1 + s, x);
                float b2 = smoothstep(_Step2 - s, _Step2 + s, x);
                float lvl = lerp(lerp(_Level0, _Level1, b1), _Level2, b2);

                float3 shadeTint = lerp(_ShadowTint.rgb, float3(1.0, 1.0, 1.0), b1);
                float3 ambient = max(SampleSH(N), 0.0) * _Ambient;
                float3 col = albedo * shadeTint * (L.color * lvl + ambient);

                // фонари и подсветка: мягкая тун-ступень вокруг лампы
                float3 extra = float3(0.0, 0.0, 0.0);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS) || defined(_CLUSTER_LIGHT_LOOP)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, i.positionWS);
                        float nd = dot(N, al.direction) * 0.5 + 0.5;
                        float toon = lerp(0.35, 1.0, smoothstep(0.42, 0.62, nd));
                        extra += al.color * (al.distanceAttenuation * toon);
                    LIGHT_LOOP_END
                }
                #endif
                col += albedo * extra * _LightGain;

                // контровой свет с освещённой стороны
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                rim *= smoothstep(0.45, 0.85, ndl) * shadow;
                col += _RimColor.rgb * L.color * (rim * _RimStrength);

                // самосвечение (стекло фонаря, серединки цветов, глазки рыбок): ночью ярче
                float glowK = lerp(_EmisDay, 1.0, _Glow);
                col += albedo * (i.emis * _EmisBoost * glowK);

                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ тени
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
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertShadow(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(PondPosOS(v.positionOS.xyz, v.color.a));
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #endif
                o.positionCS = cs;
                return o;
            }

            half4 fragShadow(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ глубина
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertDepth(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(PondPosOS(v.positionOS.xyz, v.color.a));
                return o;
            }

            half4 fragDepth(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

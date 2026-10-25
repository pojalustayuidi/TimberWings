// Assets/_Project/Shaders/Candy/CandyToon.shader
// Общий «конфетный» тун-шейдер (URP, классический Forward, ориентирован на мобильные).
//
//  * SRP Batcher: ВСЕ свойства материала в UnityPerMaterial, поэтому на инстанс ничего не передаём (нет _Tint/_Rand как в TreeToon).
//    Вариация цвета по инстансу считается из мировой позиции корня объекта, как раньше.
//  * GPU instancing оставлен включённым: срабатывает там, где SRP Batcher не работает (рендереры с MaterialPropertyBlock:
//    GoldenTreeFx, SilverTreeFx, ResourceHighlighter, FireSource).
//  * 3 ступени света + мягкая граница, тень от солнца сдвигает ступень вниз, тинт тени (холодный) вместо чёрного.
//  * Глянец: один тун-блик (Blinn), включается _GlossStrength > 0.
//  * Рим со стороны солнца.
//  * Vertex color: множится на цвет (_VertexColorStrength 0..1), если у меша нет цветов, подставляется белый.
//  * Дешёвые вещи считаются в вершинах: SH-ambient, туман, дополнительные огни (_ADDITIONAL_LIGHTS_VERTEX).
//    Меши граненые (flat shading), поэтому по вершинам получается то же самое, что по пикселям.
//  * Дизеринг «окна в листве» вокруг игрока: TreeToonPeek.hlsl (CameraFollow ищет свойство _PeekSupport).
//  * _BaseColor называется так же, как в TreeToon: TreeSizing / GoldenTreeFx / ResourceHighlighter красят материал через него.
//  * Проходы: ForwardLit, ShadowCaster, DepthOnly. DepthNormals нет (SSAO на мобильных не планируем).
//
// Не поддерживается намеренно: Forward+, screen-space shadows, лайтмапы, нормал-мапы. Всё это лишние варианты и ALU на телефоне.
Shader "Project/CandyToon"
{
    Properties
    {
        [Header(Color)]
        [MainColor] _BaseColor ("Color (_BaseColor)", Color) = (1, 1, 1, 1)
        _VertexColorStrength ("Vertex color strength", Range(0, 1)) = 1
        _PosVary ("Color variation by position", Range(0, 1)) = 0.25

        [Header(Toon lighting)]
        _Step1 ("Step 1 threshold", Range(0, 1)) = 0.55
        _Step2 ("Step 2 threshold", Range(0, 1)) = 0.82
        _StepSoft ("Step softness", Range(0.001, 0.3)) = 0.05
        _Level0 ("Level 0 (shadow)", Range(0, 1.5)) = 0.50
        _Level1 ("Level 1 (mid)", Range(0, 1.5)) = 0.78
        _Level2 ("Level 2 (light)", Range(0, 1.5)) = 1.0
        _ShadowTint ("Shadow tint", Color) = (0.74, 0.82, 1.0, 1)
        _ShadowStrength ("Cast shadow strength", Range(0, 1)) = 0.85
        _ShadowNormalBias ("Shadow lookup normal offset (m)", Range(0, 0.3)) = 0.05
        _Ambient ("Ambient (SH) amount", Range(0, 1)) = 0.35
        _AddLights ("Extra lights amount", Range(0, 2)) = 1

        [Header(Gloss)]
        _GlossStrength ("Gloss strength (0 = off)", Range(0, 1)) = 0
        _GlossSize ("Gloss size", Range(0.01, 0.6)) = 0.12
        _GlossSoft ("Gloss softness", Range(0.001, 0.2)) = 0.03
        _GlossColor ("Gloss color", Color) = (1, 1, 1, 1)

        [Header(Rim)]
        _RimColor ("Rim color", Color) = (1.0, 0.97, 0.70, 1)
        _RimStrength ("Rim strength (0 = off)", Range(0, 1)) = 0
        _RimPower ("Rim power", Range(0.5, 8)) = 3

        [Header(Emission)]
        // свечение ламп и окон: красит FireSource через MaterialPropertyBlock (без него стекла и фонари вечером оставались тёмными)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)

        // маркер для CameraFollow: «шейдер умеет окно в листве вокруг игрока». Не трогать
        [HideInInspector] _PeekSupport ("Peek support", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // ВСЕ свойства здесь и одинаково во всех проходах: иначе SRP Batcher отключится
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _VertexColorStrength;
            float _PosVary;
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
            float _AddLights;
            float _GlossStrength;
            float _GlossSize;
            float _GlossSoft;
            float4 _GlossColor;
            float4 _RimColor;
            float _RimStrength;
            float _RimPower;
            float _PeekSupport;
            float4 _EmissionColor;
        CBUFFER_END
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/Forest/TreeToonPeek.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half3 albedo : TEXCOORD2;     // цвет * vertex color * вариация
                half3 lightV : TEXCOORD3;     // SH ambient + доп. огни, считаны в вершине
                half fog : TEXCOORD4;
            };

            float CandyHash12(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = nWS;

                // вариация на инстанс из мировой позиции корня (без MaterialPropertyBlock)
                float3 origin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float rnd = CandyHash12(origin.xz * 1.37);
                float3 warm = float3(1.06, 1.02, 0.88);
                float3 cool = float3(0.94, 0.99, 1.05);
                float3 hue = lerp(cool, warm, rnd) * (1.0 + (rnd - 0.5) * 0.16);
                float3 vc = lerp(float3(1.0, 1.0, 1.0), v.color.rgb, _VertexColorStrength);
                o.albedo = half3(_BaseColor.rgb * vc * lerp(float3(1.0, 1.0, 1.0), hue, _PosVary));

                // ambient из SH и доп. огни: в вершине
                float3 l = max(SampleSH(nWS), 0.0) * _Ambient;
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint n = GetAdditionalLightsCount();
                for (uint i = 0u; i < n; i++)
                {
                    Light al = GetAdditionalLight(i, posWS);
                    float nd = dot(nWS, al.direction) * 0.5 + 0.5;
                    l += al.color * (al.distanceAttenuation * nd * nd * _AddLights);
                }
            #endif
                o.lightV = half3(l);

                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                PeekClip(i.positionCS, i.positionWS);   // дизеринг в окне вокруг игрока

                float3 N = normalize(i.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS + N * _ShadowNormalBias);
                Light L = GetMainLight(shadowCoord);
                float ndl = dot(N, L.direction) * 0.5 + 0.5;
                float shadow = lerp(1.0, L.shadowAttenuation, _ShadowStrength);
                float x = ndl * shadow;

                float s = _StepSoft;
                float b1 = smoothstep(_Step1 - s, _Step1 + s, x);
                float b2 = smoothstep(_Step2 - s, _Step2 + s, x);
                float lvl = lerp(lerp(_Level0, _Level1, b1), _Level2, b2);

                float3 shadeTint = lerp(_ShadowTint.rgb, float3(1.0, 1.0, 1.0), b1);
                float3 col = i.albedo * shadeTint * (L.color * lvl + i.lightV);

                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // тун-блик
                if (_GlossStrength > 0.001)
                {
                    float3 H = normalize(L.direction + V);
                    float nh = saturate(dot(N, H));
                    float g = smoothstep(1.0 - _GlossSize - _GlossSoft, 1.0 - _GlossSize + _GlossSoft, nh);
                    col += _GlossColor.rgb * L.color * (g * _GlossStrength * shadow * step(0.5, ndl));
                }

                // рим со стороны солнца
                if (_RimStrength > 0.001)
                {
                    float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                    rim *= smoothstep(0.45, 0.85, ndl) * shadow;
                    col += _RimColor.rgb * L.color * (rim * _RimStrength);
                }

                col += _EmissionColor.rgb;   // свечение ламп/окон (по умолчанию чёрный = ничего)

                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ тень
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vertShadow(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
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

            half4 fragShadow(Varyings i) : SV_Target { return 0; }
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vertDepth(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 fragDepth(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}

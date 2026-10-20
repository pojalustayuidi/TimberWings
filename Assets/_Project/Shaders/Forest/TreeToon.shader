// Assets/_Project/Shaders/Forest/TreeToon.shader
// Тунц-шейдер деревьев (URP, forward). Без скриптов: ветер, градиент по высоте и вариация цвета считаются в шейдере.
//
//  * освещение в 3 ступени (мягкая граница _StepSoft), тень от солнца и самозатенение кроны сдвигают ступень вниз
//  * градиент по высоте КРОНЫ в метрах локального меша (пивот дерева у корня): _GradMin..._GradMax, низ темнее, верх светлее
//  * ветер в вершинах: качание сильнее наверху (_WindStart/_WindHeight), мелкая дрожь листвы (_Flutter)
//  * GPU instancing. На инстанс: _Tint (цвет) и _Rand (0..1; -1 = авто по позиции дерева в мире).
//    Без MaterialPropertyBlock каждое дерево всё равно получает свой оттенок/фазу ветра из мировой позиции корня.
//  * ShadowCaster (с тем же ветром) и DepthOnly
//  * _BaseColor = цвет ВЕРХА кроны (имя важно: TreeSizing и GoldenTreeFx красят дерево именно через _BaseColor)
//
// Ветер на листве и коре должен совпадать (_WindStrength/_WindSpeed/_WindStart/_WindHeight/_WindDir), иначе крона «оторвётся» от ветвей.
Shader "Project/TreeToon"
{
    Properties
    {
        [Header(Colors)]
        [MainColor] _BaseColor ("Top color (_BaseColor)", Color) = (0.62, 0.86, 0.20, 1)
        _BottomColor ("Bottom color", Color) = (0.07, 0.30, 0.14, 1)
        _GradMin ("Gradient start height (m)", Float) = 1.6
        _GradMax ("Gradient end height (m)", Float) = 5.4
        _GradPow ("Gradient curve", Range(0.3, 3)) = 1
        _GradStrength ("Gradient strength (0 = flat top color)", Range(0, 1)) = 1

        [Header(Toon lighting)]
        _Step1 ("Step 1 threshold", Range(0, 1)) = 0.55
        _Step2 ("Step 2 threshold", Range(0, 1)) = 0.82
        _StepSoft ("Step softness", Range(0.001, 0.3)) = 0.05
        _Level0 ("Level 0 (shadow)", Range(0, 1.5)) = 0.42
        _Level1 ("Level 1 (mid)", Range(0, 1.5)) = 0.72
        _Level2 ("Level 2 (light)", Range(0, 1.5)) = 0.98
        _ShadowTint ("Shadow tint (level 0 color multiplier)", Color) = (0.78, 0.88, 1.0, 1)
        _ShadowStrength ("Cast shadow strength", Range(0, 1)) = 0.9
        _ShadowNormalBias ("Shadow lookup normal offset (m)", Range(0, 0.3)) = 0.06
        _Ambient ("Ambient (SH) amount", Range(0, 1)) = 0.35

        [Header(Rim)]
        _RimColor ("Rim color", Color) = (1.0, 0.97, 0.65, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.12
        _RimPower ("Rim power", Range(0.5, 8)) = 3

        [Header(Wind)]
        _WindDir ("Wind direction (XZ)", Vector) = (1, 0, 0.4, 0)
        _WindStrength ("Sway amplitude at top (m)", Float) = 0.12
        _WindSpeed ("Sway speed", Float) = 1.1
        _WindStart ("Wind starts at height (m)", Float) = 1.0
        _WindHeight ("Wind reaches full at +height (m)", Float) = 4.0
        _Flutter ("Leaf flutter (m)", Float) = 0.025
        _FlutterSpeed ("Flutter speed", Float) = 3.0

        [Header(Per instance variation)]
        _PosVary ("Auto color variation (by position)", Range(0, 1)) = 0.5
        _Tint ("Tint (instanced)", Color) = (1, 1, 1, 1)
        _Rand ("Random 0..1 (instanced, -1 = auto)", Float) = -1

        // маркер для CameraFollow: «шейдер умеет окно в листве вокруг игрока» (TreeToonPeek.hlsl). Не трогать
        [HideInInspector] _PeekSupport ("Peek support", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BottomColor;
            float _GradMin;
            float _GradMax;
            float _GradPow;
            float _GradStrength;
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
            float4 _WindDir;
            float _WindStrength;
            float _WindSpeed;
            float _WindStart;
            float _WindHeight;
            float _Flutter;
            float _FlutterSpeed;
            float _PosVary;
            float _PeekSupport;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_DEFINE_INSTANCED_PROP(float, _Rand)
        UNITY_INSTANCING_BUFFER_END(Props)

        float TTHash12(float2 p)
        {
            float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        // корень дерева в мире (из матрицы объекта)
        float3 TTOrigin()
        {
            return float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
        }

        float TTRand()
        {
            float r = UNITY_ACCESS_INSTANCED_PROP(Props, _Rand);
            float3 o = TTOrigin();
            return r < 0.0 ? TTHash12(o.xz * 1.37) : saturate(r);
        }

        // позиция вершины в мире с ветром; высота берётся из локального Y меша (метры при масштабе 1)
        float3 TTWindWS(float3 posOS)
        {
            float3 posWS = TransformObjectToWorld(posOS);
            float3 origin = TTOrigin();
            float sc = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            float rnd = TTRand();

            float h = saturate((posOS.y - _WindStart) / max(_WindHeight, 0.01));
            h *= h;

            float2 dir = _WindDir.xz;
            float dl = length(dir);
            dir = dl > 0.0001 ? dir / dl : float2(1.0, 0.0);
            float2 side = float2(-dir.y, dir.x);

            float ph = dot(origin.xz, float2(0.31, 0.23)) + rnd * 6.2831;
            float t = _Time.y * _WindSpeed;
            float g = sin(t + ph) * 0.6 + sin(t * 2.1 + ph * 1.7) * 0.4 + 0.35;
            float s2 = cos(t * 1.3 + ph * 0.8) * 0.35;

            float2 off = (dir * g + side * s2) * (_WindStrength * h * sc);
            posWS.xz += off;

            float fl = sin(_Time.y * _FlutterSpeed + dot(posOS, float3(3.7, 5.1, 4.3)) + ph) * _Flutter * h * sc;
            posWS += float3(fl, fl * 0.6, -fl);
            return posWS;
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "TreeToonPeek.hlsl"   // окно в листве вокруг игрока (только основной проход: тень остаётся)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 vary : TEXCOORD2;
                float height : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);

                float3 posWS = TTWindWS(v.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.height = v.positionOS.y;

                // вариация цвета на инстанс: тёплый/холодный сдвиг + яркость, плюс _Tint
                float rnd = TTRand();
                float3 warm = float3(1.10, 1.03, 0.78);
                float3 cool = float3(0.90, 0.98, 1.08);
                float3 hue = lerp(cool, warm, rnd) * (1.0 + (rnd - 0.5) * 0.30);
                o.vary = lerp(float3(1.0, 1.0, 1.0), hue, _PosVary) * UNITY_ACCESS_INSTANCED_PROP(Props, _Tint).rgb;

                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                PeekClip(i.positionCS, i.positionWS);   // дизеринг в окне вокруг игрока

                float3 N = normalize(i.normalWS);

                // градиент по высоте
                float t = saturate((i.height - _GradMin) / max(_GradMax - _GradMin, 0.001));
                t = pow(max(t, 0.0001), _GradPow);
                t = lerp(1.0, t, _GradStrength);
                float3 albedo = lerp(_BottomColor.rgb, _BaseColor.rgb, t) * i.vary;

                // свет и тень
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
                float3 ambient = max(SampleSH(N), 0.0) * _Ambient;
                float3 col = albedo * shadeTint * (L.color * lvl + ambient);

                // слабый рим со стороны солнца
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                rim *= smoothstep(0.45, 0.85, ndl) * shadow;
                col += _RimColor.rgb * L.color * (rim * _RimStrength);

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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertShadow(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);

                float3 posWS = TTWindWS(v.positionOS.xyz);
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertDepth(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TTWindWS(v.positionOS.xyz));
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

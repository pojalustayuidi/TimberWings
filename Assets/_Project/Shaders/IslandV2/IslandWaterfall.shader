// Assets/_Project/Shaders/IslandV2/IslandWaterfall.shader
// Остров v2: водопад с края острова. Отдельный полупрозрачный шейдер (река остаётся на Project/IslandV2/Water).
// Почему отдельно: прошлый водопад был непрозрачным с вырезом (clip), поэтому внизу получались «дыры», а края были жёсткими.
// Здесь края и низ растворяются плавно (альфа), полотно состоит из двух слоёв струй, сверху белая кромка гребня, снизу белая дымка.
// Меш делает IslandRiverWater.AddFall. Что читает из вершин:
//   UV.x   поперёк полотна: -1 левый край, +1 правый; UV.y = метры вдоль падения
//   цвет R  непрозрачность по высоте: 1 вверху, 0 в самом низу (растворение в облаках)
//   цвет B  пена гребня: 1 на самом пороге, падает до 0 за 0,5 м
//   цвет A  номер слоя: 0 передний, 1 задний (задний чуть уже, струи сдвинуты)
// Без ZWrite и теней: рисуется после земли и воды, один draw call.
Shader "Project/IslandV2/Waterfall"
{
    Properties
    {
        _ShallowColor ("Светлая вода", Color) = (0.55, 0.92, 0.95, 1)
        _MidColor ("Основной цвет струй", Color) = (0.30, 0.75, 0.93, 1)
        _FoamColor ("Пена", Color) = (0.96, 1.0, 1.0, 1)
        _FallSpeed ("Скорость струй", Range(0, 12)) = 4.0
        _Density ("Плотность воды (0 прозрачно, 1 плотно)", Range(0.3, 1)) = 0.9
        _LightMix ("Сколько света сцены", Range(0, 1)) = 0.35
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
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _MidColor;
                half4 _FoamColor;
                half _FallSpeed;
                half _Density;
                half _LightMix;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.normalWS = vn.normalWS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
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

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float layer = IN.color.a;                    // 0 передний, 1 задний слой
                float ph = layer * 3.7;                      // задний слой: другой рисунок
                float ax = abs(IN.uv.x);
                float y = IN.uv.y;
                float fade = IN.color.r;

                // ---------------------------------------------------- струи: вертикальные полосы разной скорости, чуть извиваются
                float w1 = sin(y * 0.50 - t * 0.55 + ph) * 0.9;
                float w2 = sin(y * 0.33 + t * 0.40 + ph * 2.0) * 1.4;
                float c1 = sin(IN.uv.x * 9.0 + ph + w1) * 0.5 + 0.5;
                float c2 = sin(IN.uv.x * 16.0 + 2.0 + ph + w2) * 0.5 + 0.5;
                float run1 = 0.5 + 0.5 * sin(y * 1.2 - t * _FallSpeed * 0.9 + c1 * 4.0 + ph);
                float run2 = 0.5 + 0.5 * sin(y * 2.0 - t * _FallSpeed * 1.35 + c2 * 5.0 + ph);
                float s1 = smoothstep(0.55, 0.85, c1) * smoothstep(0.30, 0.90, run1);
                float s2 = smoothstep(0.66, 0.92, c2) * smoothstep(0.40, 0.95, run2);
                float streak = saturate(s1 * 0.8 + s2 * 0.55);

                // ---------------------------------------------------- цвет: вода -> светлая вода -> пена
                half3 col = lerp(_MidColor.rgb, _ShallowColor.rgb, 0.35h + 0.25h * (half)c1);
                col = lerp(col, _FoamColor.rgb, (half)streak * 0.85h);

                // белая кромка гребня сверху
                col = lerp(col, _FoamColor.rgb, saturate(IN.color.b));
                // белые боковые кромки полотна
                float rim = smoothstep(0.72, 0.95, ax);
                col = lerp(col, _FoamColor.rgb, (half)rim * 0.8h);
                // внизу вода уходит в белую дымку
                half mist = 1.0h - smoothstep(0.05h, 0.65h, (half)fade);
                col = lerp(col, _FoamColor.rgb, mist * 0.9h);

                // ---------------------------------------------------- прозрачность
                // снизу плавно в ноль, края плавно в ноль (рваная каёмка от шума, а не прямая линия)
                float edgeN = VNoise(float2(y * 0.8 - t * 1.2 + ph, IN.uv.x * 2.0)) * 0.12;
                float side = 1.0 - smoothstep(0.82, 1.0, ax + edgeN);
                float bottomN = VNoise(float2(IN.uv.x * 3.0 + ph, y * 0.4 - t * 0.9)) * 0.25;
                float bottom = smoothstep(0.0, 0.55, fade - bottomN * (1.0 - fade));
                // между струями вода чуть прозрачнее: полотно «живое»
                float body = lerp(0.62, 1.0, saturate(streak + rim + IN.color.b));
                float topSolid = saturate(IN.color.b + 0.55);   // у порога плотнее
                float alpha = side * bottom * body * _Density * lerp(1.0, 1.0, topSolid);
                alpha *= lerp(1.0, 0.8, layer);                 // задний слой слабее: передний читается чётче
                alpha = saturate(alpha);

                // ---------------------------------------------------- свет: мягкий, без теней
                half3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half wrapped = saturate((dot(n, mainLight.direction) + 0.6h) / 1.6h);
                half3 lit = mainLight.color * wrapped + SampleSH(n);
                half litLum = dot(lit, half3(0.30h, 0.59h, 0.11h));
                col *= lerp(1.0h, saturate(litLum * 1.25h), _LightMix);
                col = MixFog(col, IN.fogFactor);
                return half4(col, (half)alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

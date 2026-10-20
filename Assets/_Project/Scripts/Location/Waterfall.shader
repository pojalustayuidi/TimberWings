// Шейдер падающей воды парящего острова (URP, прозрачный, без света). Рисует IslandWaterfall:
// струя (FallSheet), тонкая завеса (FallVeil) и продолжение реки у кромки (FallRunout, если река не даёт свой материал).
// Вход: UV.x = 0..1 поперёк потока, UV.y = метры пути воды (бежит вдоль потока), цвет вершины A = прозрачность ряда (внизу растворяется).
// Анимация идёт по времени в шейдере: полосы воды стекают вниз, у кромки и по бокам белая пена. _Tint ставит скрипт по времени суток.
Shader "Project/Waterfall"
{
    Properties
    {
        _Deep ("Тёмный цвет воды", Color) = (0.30,0.62,0.86,1)
        _Light ("Светлый цвет воды", Color) = (0.84,0.95,1,1)
        _Foam ("Цвет пены", Color) = (1,1,1,1)
        _Tint ("Тон (время суток)", Color) = (1,1,1,1)
        _Speed ("Скорость течения", Float) = 3.2
        _Streaks ("Число полос поперёк", Float) = 10
        _FoamLen ("Длина пены у кромки, м", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Light;
                half4 _Foam;
                half4 _Tint;
                float _Speed;
                float _Streaks;
                float _FoamLen;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
                half fogF : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = i.uv;
                o.alpha = i.color.a;
                o.fogF = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;
                float v = i.uv.y;
                float t = _Time.y * _Speed;

                // вытянутые вдоль течения полосы: шум узкий поперёк и длинный вдоль, бежит вниз со временем
                float x = u * _Streaks;
                float s1 = vnoise(float2(x, v * 0.32 - t * 0.35));
                float s2 = vnoise(float2(x * 2.1 + 7.3, v * 0.55 - t * 0.62));
                float streak = saturate(s1 * 0.65 + s2 * 0.35);

                // белые хлопья в потоке
                float fleck = smoothstep(0.78, 0.95, vnoise(float2(x * 1.7 + 3.1, v * 0.9 - t * 0.9)));

                half3 col = lerp(_Deep.rgb, _Light.rgb, smoothstep(0.25, 0.8, streak));

                // пена: у кромки (малое v) и по бокам струи
                float lipFoam = 1.0 - smoothstep(0.0, max(0.05, _FoamLen), v);
                lipFoam *= 0.55 + 0.45 * vnoise(float2(x * 1.3, v * 2.0 - t * 0.4));
                float side = min(u, 1.0 - u);
                float sideFoam = (1.0 - smoothstep(0.0, 0.14, side)) * (0.5 + 0.5 * s2);

                float foam = saturate(max(lipFoam, max(sideFoam * 0.8, fleck * 0.45)));
                col = lerp(col, _Foam.rgb, foam);
                col *= _Tint.rgb;

                // края струи прозрачнее, внизу растворяется (цвет вершины)
                float edge = smoothstep(0.0, 0.10, u) * smoothstep(0.0, 0.10, 1.0 - u);
                half a = saturate((0.78 + 0.22 * streak + foam * 0.2) * edge) * i.alpha;

                col = MixFog(col, i.fogF);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

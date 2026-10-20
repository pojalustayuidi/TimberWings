// Шейдер тумана и брызг водопада (URP, частицы-билборды). Мягкое круглое пятно с рваным краем, цвет и прозрачность берутся
// из частицы (Start Color и Color over Lifetime), _Tint ставит IslandWaterfall по времени суток.
// Нужен для IslandWaterfall (MistBase, MistMid, MistLip).
Shader "Project/WaterfallMist"
{
    Properties
    {
        _Tint ("Тон (время суток)", Color) = (1,1,1,1)
        _Softness ("Мягкость края", Range(0.1,1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Mist"
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
                half4 _Tint;
                half _Softness;
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
                half4 color : COLOR;
                half fogF : TEXCOORD1;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = i.uv;
                o.color = i.color;
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
                float2 c = i.uv * 2.0 - 1.0;
                float r = length(c);
                // рваный край: шум по углу и радиусу, у каждой частицы свой (по цвету), чтобы пятна не выглядели одинаково
                float seed = frac(i.color.r * 17.0 + i.color.g * 31.0);
                float n = vnoise(c * 2.4 + seed * 9.0);
                float radius = 0.78 + (n - 0.5) * 0.45;
                float a = 1.0 - smoothstep(radius * (1.0 - _Softness), radius, r);
                a *= a;   // центр плотнее, край воздушнее

                half3 col = i.color.rgb * _Tint.rgb;
                col = MixFog(col, i.fogF);
                return half4(col, a * i.color.a);
            }
            ENDHLSL
        }
    }
}

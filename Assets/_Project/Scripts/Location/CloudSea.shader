// Слой облаков для моря под парящим островом (URP, прозрачный). Облака считаются в шейдере по мировым координатам
// (шум не едет вместе с камерой), двухтонные: свет со стороны солнца, тень с другой. Края слоя растворяются в тумане.
// Рисуется из CloudSea.cs (Graphics.DrawMesh только для главной камеры, миникарта слой не видит).
Shader "Project/CloudSea"
{
    Properties
    {
        _Top ("Цвет света", Color) = (1,1,1,1)
        _Shade ("Цвет тени", Color) = (0.60,0.70,0.90,1)
        _Scale ("Масштаб шума", Float) = 0.03
        _Cover ("Покрытие", Range(0,1)) = 0.46
        _Soft ("Мягкость края", Range(0.01,0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Clouds"
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
                half4 _Top;
                half4 _Shade;
                float _Scale;
                half _Cover;
                half _Soft;
                float4 _Wind;   // xy: сдвиг шума (ветер), zw: направление на солнце по xz
                float4 _Fade;   // xy: центр (камера), z: радиус растворения, w: детальность 0..1
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
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

            float fbm(float2 p, float detail)
            {
                float v = 0.5 * vnoise(p);
                v += 0.25 * vnoise(p * 2.03 + 11.7);
                v += 0.125 * vnoise(p * 4.01 + 23.1);
                v += 0.0625 * detail * vnoise(p * 8.05 + 37.9);
                return v / (0.875 + 0.0625 * detail);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz * _Scale + _Wind.xy;
                float detail = _Fade.w;

                float n = fbm(p, detail);
                float n2 = fbm(p + _Wind.zw * 0.07, detail);   // тот же шум, сдвинутый к солнцу: разность даёт освещённый край

                float density = smoothstep(_Cover, _Cover + _Soft, n);
                float lit = smoothstep(0.42, 0.62, saturate((n - n2) * 7.0 + 0.55));
                float tops = smoothstep(_Cover + _Soft, 1.0, n);

                half3 col = lerp(_Shade.rgb, _Top.rgb, saturate(lit * 0.75 + tops * 0.6));

                float d = distance(i.positionWS.xz, _Fade.xy);
                float fade = 1.0 - smoothstep(_Fade.z * 0.5, _Fade.z, d);
                half alpha = density * fade * _Top.a;

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

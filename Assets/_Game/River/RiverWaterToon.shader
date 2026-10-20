
Shader "BuilderSim/CartoonWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.45, 0.92, 0.84, 0.55)
        _DeepColor ("Deep", Color) = (0.06, 0.42, 0.64, 0.93)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _FoamWidth ("Foam width (m of depth)", Range(0.02, 0.6)) = 0.14
        _DepthRange ("Depth range (m)", Float) = 2.0
        _Tiling ("Ripple tiling", Float) = 0.9
        _Speed ("Flow speed", Float) = 0.55
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.5
        _Sparkle ("Sparkle", Range(0, 1)) = 0.6
        _Caustics ("Caustics", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _FoamWidth;
                float _DepthRange;
                float _Tiling;
                float _Speed;
                float _RippleStrength;
                float _Sparkle;
                float _Caustics;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 dep : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                OUT.dep = IN.uv1;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // расстояния до ближайшей и второй ближайшей точки (F1, F2) - для сетки каустик
            float2 voro2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = hash22(i + g);
                        o = 0.5 + 0.45 * sin(_Time.y * 0.7 + 6.2831 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                }
                return float2(sqrt(d1), sqrt(d2));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float dep = IN.dep.x;
                float d01 = saturate(dep / max(0.01, _DepthRange));
                float2 p = IN.uv * _Tiling;      // x: поперёк реки, y: вдоль течения

                // вытянутые вдоль течения штрихи
                float n1 = vnoise(float2(p.x * 2.2, p.y * 0.9 - t * 1.6) * 2.0);
                float n2 = vnoise(float2(p.x * 3.4 + 7.3, p.y * 1.3 - t * 2.3) * 2.0 + n1);
                float streak = 1.0 - smoothstep(0.0, 0.07, abs(n1 - n2));

                // каустики
                float2 vc = voro2(p * 1.35 + float2(0.0, -t * 0.6));
                float caustic = 1.0 - smoothstep(0.0, 0.12, vc.y - vc.x);

                // цвет по глубине, с мягкими мультяшными ступенями
                float depthMix = smoothstep(0.0, 0.9, d01);
                depthMix = lerp(depthMix, floor(depthMix * 4.0 + 0.5) / 4.0, 0.45);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthMix);

                col += caustic * _Caustics * (1.0 - depthMix) * 0.22;
                col += streak * _RippleStrength * 0.20;

                // блик солнца ступенькой + лёгкий френель
                float3 L = normalize(_MainLightPosition.xyz);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 N = normalize(float3((n1 - 0.5) * 0.45, 1.0, (n2 - 0.5) * 0.45));
                float3 R = reflect(-L, N);
                float spec = step(0.8, pow(saturate(dot(R, V)), 28.0));
                col += spec * _Sparkle * 0.6;
                float fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                col = lerp(col, half3(0.75, 0.93, 1.0), fres * 0.18);

                float sp = vnoise(p * 9.0 + float2(t * 0.8, -t * 2.0));
                col += step(0.95, sp) * _Sparkle * 0.35;

                // пена: широкая полоса и тонкая прерывистая линия за ней
                float wob = (vnoise(p * 6.0 + float2(0.0, -t)) - 0.5) * 0.10 + sin(_Time.y * 1.7 + p.y * 5.0) * 0.012;
                float foamEdge = dep + wob;
                float foam = 1.0 - smoothstep(_FoamWidth * 0.55, _FoamWidth, foamEdge);
                float foam2 = 1.0 - smoothstep(0.0, 0.03, abs(foamEdge - _FoamWidth * 1.9));
                float dash = step(0.5, vnoise(p * 7.0 + float2(0.0, -t)));
                col = lerp(col, _FoamColor.rgb, saturate(foam + foam2 * 0.55 * dash));

                float a = lerp(_ShallowColor.a, _DeepColor.a, depthMix);
                a = saturate(max(a, foam * 0.95) + streak * 0.05);
                a *= smoothstep(-0.02, 0.02, dep);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

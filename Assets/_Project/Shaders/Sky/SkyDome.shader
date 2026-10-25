// Assets/_Project/Shaders/Sky/SkyDome.shader
// Небо: градиент по времени суток, солнце с ореолом, луна с фазами, звёзды, мультяшные облака. URP, без текстур.
// Рисуется куполом вокруг камеры (SkyDome.cs) ПОСЛЕ непрозрачных объектов и только там, где нет геометрии (ZTest LEqual),
// поэтому лишней перерисовки нет. Низ купола и «дымка» у горизонта = цвет тумана сцены: шва с краем карты не будет.
Shader "Project/SkyDome"
{
    Properties
    {
        _Fog ("Fog / horizon haze", Color) = (0.89, 0.82, 0.71, 1)
        _Horizon ("Sky horizon", Color) = (0.70, 0.85, 0.97, 1)
        _Zenith ("Sky zenith", Color) = (0.30, 0.58, 0.93, 1)
        _SunDir ("Direction TO sun", Vector) = (0, 1, 0, 0)
        _SunColor ("Sun color", Color) = (1, 0.85, 0.6, 1)
        _MoonDir ("Direction TO moon", Vector) = (0, -1, 0, 0)
        _Weights ("Day, Night, Twilight, SunVisible", Vector) = (1, 0, 0, 1)
        _Moon ("Moon: phase cut X, angular size, visibility", Vector) = (2.2, 0.045, 0, 0)
        _Cloud ("Clouds: cover, speed, scale, detail 0..1", Vector) = (0.45, 0.012, 1.1, 1)
        _Stars ("Stars: amount", Vector) = (1, 0, 0, 0)
        _CloudShade ("Cloud shade tint (day)", Color) = (0.62, 0.70, 0.90, 1)
    }

    SubShader
    {
        // Geometry+499: после обычных непрозрачных (2000), до прозрачных. ZTest LEqual: рисуется только там, где геометрии нет
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+499" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Fog;
                float4 _Horizon;
                float4 _Zenith;
                float4 _SunDir;
                float4 _SunColor;
                float4 _MoonDir;
                float4 _Weights;
                float4 _Moon;
                float4 _Cloud;
                float4 _Stars;
                float4 _CloudShade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;   // купол центрирован на камере: локальная позиция = направление взгляда
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
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

            // detail 0..1: на слабых устройствах 2 октавы, на сильных 4
            float fbm(float2 p, float detail)
            {
                float o3 = step(0.3, detail);
                float o4 = step(0.7, detail);
                float v = 0.55 * vnoise(p);
                v += 0.27 * vnoise(p * 2.03 + 17.0);
                v += 0.13 * o3 * vnoise(p * 4.1 + 31.0);
                v += 0.06 * o4 * vnoise(p * 8.3 + 5.0);
                return v / (0.55 + 0.27 + 0.13 * o3 + 0.06 * o4);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float up = saturate(h);

                float day = _Weights.x, night = _Weights.y, tw = _Weights.z, sunVis = _Weights.w;
                float3 sdir = _SunDir.xyz;

                // ---------- градиент и дымка у горизонта (= цвет тумана)
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.55));
                float haze = 1.0 - smoothstep(0.0, 0.24, up);
                col = lerp(col, _Fog.rgb, haze * 0.85);
                col = lerp(col, _Fog.rgb, 1.0 - smoothstep(-0.06, 0.0, h));   // ниже горизонта чистый туман

                // ---------- звёзды (ночью, выше горизонта)
                if (_Stars.x > 0.01 && night > 0.02 && h > 0.0)
                {
                    float3 p = d * 95.0;
                    float3 id = floor(p);
                    float3 f = frac(p) - 0.5;
                    float rnd = hash31(id);
                    float3 off = (float3(hash31(id + 7.1), hash31(id + 13.7), hash31(id + 3.3)) - 0.5) * 0.5;
                    float dist = length(f - off);
                    float sz = lerp(0.05, 0.14, hash31(id + 21.0));
                    float star = rnd > 0.94 ? (1.0 - smoothstep(0.0, sz, dist)) : 0.0;
                    float blink = 0.7 + 0.3 * sin(_Time.y * (1.0 + rnd * 4.0) + rnd * 40.0);
                    float fade = smoothstep(0.04, 0.3, h) * night * _Stars.x;
                    col += float3(0.90, 0.93, 1.0) * star * blink * fade * 1.4;
                }

                // ---------- солнце: широкое тёплое свечение, плотное ядро, диск
                float sd = saturate(dot(d, sdir));
                float nearHor = lerp(0.45, 1.0, 1.0 - smoothstep(0.0, 0.6, up));
                col += _SunColor.rgb * (pow(sd, 5.0) * 0.20 * nearHor + pow(sd, 40.0) * 0.30) * sunVis;
                float disc = smoothstep(0.99905, 0.99935, dot(d, sdir)) * step(-0.03, h);
                col = lerp(col, float3(1.0, 0.97, 0.86) * 1.15, disc * sunVis);   // чуть выше 1: на Bloom даёт мягкое сияние

                // ---------- луна: диск с фазой и слабым ореолом
                float3 mdir = _MoonDir.xyz;
                float md = dot(d, mdir);
                col += float3(0.45, 0.55, 0.90) * pow(saturate(md), 90.0) * 0.35 * _Moon.z;
                if (md > 0.9 && h > -0.03)
                {
                    float3 mr = normalize(cross(float3(0.0, 1.0, 0.0), mdir));
                    float3 mu = cross(mdir, mr);
                    float2 q = float2(dot(d, mr), dot(d, mu)) / max(_Moon.y, 0.001);
                    float moonDisc = 1.0 - smoothstep(0.92, 1.0, length(q));
                    float cut = 1.0 - smoothstep(0.92, 1.0, length(q - float2(_Moon.x, 0.0)));
                    float lit = saturate(moonDisc - cut);
                    float3 moonCol = float3(0.93, 0.96, 1.05) * lit + float3(0.12, 0.15, 0.26) * (moonDisc - lit);
                    col = lerp(col, moonCol, moonDisc * _Moon.z);
                }

                // ---------- облака: плоский слой с перспективой, двухтонная заливка как в мультфильме
                if (h > 0.01)
                {
                    float2 uv = d.xz / (h + 0.14) * _Cloud.z + _Time.y * _Cloud.y * float2(1.0, 0.35);
                    float n = fbm(uv, _Cloud.w);
                    float th = 1.0 - _Cloud.x;
                    float c = smoothstep(th, th + 0.07, n);
                    if (c > 0.001)
                    {
                        // со стороны солнца край облака светлее: сравниваем плотность в шаге к солнцу
                        float2 toSun = normalize(sdir.xz + float2(1e-4, 1e-4)) * 0.07;
                        float n2 = fbm(uv + toSun, _Cloud.w);
                        float core = smoothstep(th + 0.10, th + 0.45, n);
                        float litAmt = saturate(0.62 + (n - n2) * 5.0 - core * 0.18);

                        float3 dayLit = lerp(float3(1.0, 1.0, 1.0), _SunColor.rgb * 1.15, tw * 0.85);
                        float3 nightLit = float3(0.20, 0.25, 0.42);
                        float3 litC = lerp(nightLit, dayLit, saturate(day + tw * 0.9));
                        float3 shadeC = litC * lerp(_CloudShade.rgb, float3(0.50, 0.52, 0.75), saturate(tw * 0.7 + night * 0.5));
                        float3 cc = lerp(shadeC, litC, litAmt);
                        cc = lerp(cc, _Fog.rgb, haze * 0.4);                       // у горизонта облака тонут в дымке
                        float fadeH = smoothstep(0.015, 0.20, h);
                        col = lerp(col, cc, c * fadeH * 0.96);
                    }
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

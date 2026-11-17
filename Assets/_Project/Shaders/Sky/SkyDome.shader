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
                // The dome must sit behind every opaque object, including scenery near the far plane.
                o.positionCS.z = o.positionCS.w * UNITY_RAW_FAR_CLIP_VALUE;
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

            float cloudNoise(float2 p, float detail)
            {
                float density = 0.55 * vnoise(p) + 0.27 * vnoise(p * 2.03 + 17.0);
                float total = 0.82;
                if (detail >= 0.3)
                {
                    density += 0.13 * vnoise(p * 4.1 + 31.0);
                    total += 0.13;
                }
                if (detail >= 0.7)
                {
                    density += 0.06 * vnoise(p * 8.3 + 5.0);
                    total += 0.06;
                }
                return density / total;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float up = saturate(h);

                float day = _Weights.x, night = _Weights.y, tw = _Weights.z, sunVis = _Weights.w;
                float3 sdir = _SunDir.xyz;
                if (h < -0.065) return half4(_Fog.rgb, 1.0h);

                float sd = saturate(dot(d, sdir));
                float horizonAtmosphere = 1.0 - smoothstep(0.015, 0.28, up);
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.63));
                float gold = horizonAtmosphere * tw * (0.12 + pow(sd, 6.0) * 0.82);
                col = lerp(col, _SunColor.rgb * float3(1.12, 0.93, 0.76), gold * 0.42);
                float haze = 1.0 - smoothstep(-0.065, 0.20, h);
                col = lerp(col, _Fog.rgb, haze * lerp(0.78, 0.53, tw));
                col = lerp(col, _Fog.rgb, 1.0 - smoothstep(-0.065, 0.012, h));

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
                    float blink = 0.86 + 0.14 * sin(_Time.y * (0.9 + rnd * 2.0) + rnd * 40.0);
                    float fade = smoothstep(0.07, 0.35, h) * night * _Stars.x;
                    col += float3(0.77, 0.84, 1.0) * star * blink * fade * 1.18;
                }

                // ---------- солнце: широкое тёплое свечение, плотное ядро, диск
                float glowVisibility = smoothstep(-0.065, 0.028, h);
                float nearHor = lerp(0.47, 1.0, 1.0 - smoothstep(0.0, 0.58, up));
                col += _SunColor.rgb * (pow(sd, 5.0) * 0.18 * nearHor + pow(sd, 42.0) * 0.27) * (sunVis * glowVisibility);
                float disc = smoothstep(0.99905, 0.99935, dot(d, sdir)) * step(-0.03, h);
                col = lerp(col, float3(1.0, 0.97, 0.86) * 1.15, disc * sunVis);   // чуть выше 1: на Bloom даёт мягкое сияние

                // ---------- луна: диск с фазой и слабым ореолом
                float3 mdir = _MoonDir.xyz;
                float md = dot(d, mdir);
                col += float3(0.45, 0.55, 0.90) * pow(saturate(md), 90.0) * 0.35 * _Moon.z;
                if (md > 0.9 && h > -0.03)
                {
                    float3 pole = abs(mdir.y) > 0.99 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
                    float3 mr = normalize(cross(pole, mdir));
                    float3 mu = cross(mdir, mr);
                    float2 q = float2(dot(d, mr), dot(d, mu)) / max(_Moon.y, 0.001);
                    float moonDisc = 1.0 - smoothstep(0.92, 1.0, length(q));
                    float cut = 1.0 - smoothstep(0.92, 1.0, length(q - float2(_Moon.x, 0.0)));
                    float lit = saturate(moonDisc - cut);
                    float3 moonCol = float3(0.93, 0.96, 1.05) * lit + float3(0.12, 0.15, 0.26) * (moonDisc - lit);
                    col = lerp(col, moonCol, moonDisc * _Moon.z);
                }

                if (h > 0.004)
                {
                    float2 drift = _Time.y * _Cloud.y * float2(1.0, 0.35);
                    float2 uv = d.xz / (h + 0.21) * _Cloud.z * 0.86 + drift;
                    float3 warmCloud = lerp(float3(0.91, 0.97, 1.0), _SunColor.rgb * float3(1.11, 0.91, 0.88), tw * 0.88);
                    float3 litCloud = lerp(float3(0.060, 0.079, 0.144), warmCloud, saturate(day + tw * 0.9));

                    // Thin high streaks appear through gaps in the lower cloud deck.
                    float2 highUV = (d.xz / (h + 0.52)) * _Cloud.z * float2(2.45, 6.0)
                        - drift * 0.34 + float2(19.4, 27.3);
                    highUV.y += 0.22 * sin(highUV.x * 1.5);
                    float wisps = smoothstep(0.57, 0.83, vnoise(highUV));
                    float highFade = smoothstep(0.025, 0.17, h) * (1.0 - smoothstep(0.62, 0.98, h));
                    col = lerp(col, litCloud, wisps * highFade * _Cloud.x * lerp(0.64, 0.27, night));

                    float threshold = lerp(0.76, 0.31, _Cloud.x);
                    float density = cloudNoise(uv, _Cloud.w);
                    float cloud = smoothstep(threshold, threshold + 0.105, density);
                    if (cloud > 0.001)
                    {
                        float2 lightDir = normalize(sdir.xz + float2(1e-4, 1e-4));
                        float towardLight = cloudNoise(uv + lightDir * 0.10, _Cloud.w);
                        float core = smoothstep(threshold + 0.08, threshold + 0.37, density);
                        float shade = smoothstep(0.21, 0.78, saturate(0.62 + (density - towardLight) * 5.0 - core * 0.23));
                        float3 darkCloud = litCloud * lerp(_CloudShade.rgb, float3(0.64, 0.62, 0.82), tw * 0.63 + night * 0.38);
                        float3 cloudColor = lerp(darkCloud, litCloud, shade);
                        float silver = (1.0 - core) * pow(sd, 9.0) * sunVis * shade * 0.21;
                        cloudColor += _SunColor.rgb * silver;
                        cloudColor = lerp(cloudColor, _Fog.rgb, horizonAtmosphere * 0.48);
                        float fade = smoothstep(0.004, 0.16, h);
                        col = lerp(col, cloudColor, cloud * fade * 0.97);
                    }
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

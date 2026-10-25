Shader "BuilderSim/LocationToonWater"
{
    Properties
    {
        _Deep ("Deep colour", Color) = (0.16,0.50,0.80,1)
        _Shallow ("Shallow colour", Color) = (0.42,0.82,0.92,1)
        _Foam ("Foam colour", Color) = (0.95,0.99,1,1)
        _Alpha ("Alpha", Range(0,1)) = 0.82
        _Speed ("Flow speed", Float) = 1.4
        _Freq ("Stripes per unit of flow", Float) = 0.7
        _Lines ("Stripe strength", Range(0,1)) = 0.5
        _FoamDepth ("Shore foam depth, u", Float) = 0.35
        _DepthScale ("Depth for deep colour, u", Float) = 1.2
        _Caustic ("Caustic light on the bed", Range(0,1)) = 0
        _Ribbons ("Flow ribbons along the stream", Range(0,1)) = 0
        _ShallowAlpha ("Alpha in the shallows (-1 = off)", Range(-1,1)) = -1
        _WakeMap ("Wake map: R edge dist, G V-wake, B speed-up", 2D) = "black" {}
        _WakeRect ("Wake map rect (x0 z0 1/w 1/h, object space)", Vector) = (0,0,1,1)
        _WakeAmt ("Foam around rocks and V-wakes", Range(0,2)) = 0
        _Sparkle ("Sun glints on the water", Range(0,1)) = 0
        _Streak ("Waterfall streaks (uv1 = lateral pos, half width)", Range(0,1)) = 0
        _FallLen ("Waterfall path length, u (foam at the foot)", Float) = 0
        _FallFade ("Fade the sheet into mist at the bottom", Range(0,1)) = 0
        _Pulse ("Soft pulsing of the alpha (mist)", Range(0,1)) = 0
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
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Shallow;
                half4 _Foam;
                half _Alpha;
                float _Speed;
                float _Freq;
                half _Lines;
                float _FoamDepth;
                float _DepthScale;
                half _Caustic;
                half _Ribbons;
                half _ShallowAlpha;
                float4 _WakeRect;
                half _WakeAmt;
                half _Sparkle;
                half _Streak;
                float _FallLen;
                half _FallFade;
                half _Pulse;
            CBUFFER_END

            TEXTURE2D(_WakeMap);
            SAMPLER(sampler_WakeMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 posOS : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.uv2 = i.uv2;
                o.posOS = i.positionOS.xz;
                return o;
            }

            float FHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // мягкий value-noise 0..1 без ступенек: из него сделаны струи водопада
            float FNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                fp = fp * fp * (3.0 - 2.0 * fp);
                float na = FHash(ip);
                float nb = FHash(ip + float2(1.0, 0.0));
                float nc = FHash(ip + float2(0.0, 1.0));
                float nd = FHash(ip + float2(1.0, 1.0));
                return lerp(lerp(na, nb, fp.x), lerp(nc, nd, fp.x), fp.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float depth = i.uv.x;
                float flow = i.uv.y;
                float d01 = saturate(depth / max(_DepthScale, 0.01));
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, d01);
                half3 colSmooth = col;
                col = floor(col * 8.0 + 0.5) / 8.0;
                if (_Streak > 0.001) col = colSmooth;   // водопад: плавный градиент без ступенек цвета

                float wob = sin(flow * _Freq * 6.2831 - _Time.y * _Speed + sin(i.positionWS.x * 1.3 + i.positionWS.z * 0.9) * 1.6);
                float strip = smoothstep(0.64, 0.80, wob);
                col = lerp(col, lerp(col, _Foam.rgb, 0.5), strip * _Lines * (0.4 + 0.6 * d01) * (_Streak > 0.001 ? 0.0 : 1.0));

                float ew = 0.5 + 0.5 * sin(flow * 3.1 + _Time.y * 0.8 + i.positionWS.x * 2.0);
                float shore = 1.0 - smoothstep(_FoamDepth * 0.6 * (0.6 + 0.4 * ew), _FoamDepth * (0.8 + 0.5 * ew), depth);
                shore *= step(0.001, _FoamDepth);
                col = lerp(col, _Foam.rgb, shore);

                float foamExtra = 0.0;
                if (_Ribbons > 0.001 || _WakeAmt > 0.001)
                {
                    float2 wuv = (i.posOS - _WakeRect.xy) * _WakeRect.zw;
                    float4 wk = SAMPLE_TEXTURE2D_LOD(_WakeMap, sampler_WakeMap, wuv, 0);

                    if (_Ribbons > 0.001)
                    {
                        float lat = i.uv2.x;
                        float hwr = max(i.uv2.y, 0.5);
                        float wl = lat + sin(flow * 0.35 + lat * 0.8) * 0.22;
                        float lane = floor(wl / 1.05);
                        float fl = frac(wl / 1.05) - 0.5;
                        float h = frac(sin(lane * 12.9898) * 43758.5453);
                        float al = frac(flow * 0.22 - _Time.y * _Speed * 0.16 + h);
                        float ln = 0.22 + 0.30 * h + 0.25 * wk.b;
                        float tt = saturate(al / ln);
                        float wd = lerp(0.04, 0.17 + 0.07 * h, sin(3.14159 * tt));
                        float rb = (1.0 - step(ln, al)) * (1.0 - smoothstep(wd * 0.75, wd, abs(fl) * 1.05));
                        rb *= 1.0 - smoothstep(0.65, 1.0, abs(lat) / hwr);
                        rb *= smoothstep(0.06, 0.2, depth);
                        col = lerp(col, lerp(_Shallow.rgb, _Foam.rgb, 0.4), rb * _Ribbons);
                    }

                    if (_WakeAmt > 0.001)
                    {
                        float edge = wk.r * 1.2;
                        float thick = (0.28 + 0.12 * ew) * _WakeAmt;
                        float ring = 1.0 - smoothstep(thick * 0.55, thick, edge);
                        float vk = smoothstep(0.30, 0.50, wk.g) * (0.75 + 0.25 * sin(flow * 2.2 - _Time.y * _Speed * 1.6)) * _WakeAmt;
                        foamExtra = saturate(max(ring, vk * 0.85));
                        col = lerp(col, _Foam.rgb, foamExtra);
                    }
                }

                if (_Caustic > 0.001)
                {
                    float2 cuv = i.positionWS.xz * 0.9;
                    float2 cip = floor(cuv);
                    float2 cfp = frac(cuv);
                    float m1 = 8.0;
                    float m2 = 8.0;
                    for (int cy = -1; cy <= 1; cy++)
                    {
                        for (int cx = -1; cx <= 1; cx++)
                        {
                            float2 cg = float2(cx, cy);
                            float2 cc = cip + cg;
                            float2 ch = frac(sin(float2(dot(cc, float2(127.1, 311.7)), dot(cc, float2(269.5, 183.3)))) * 43758.5453);
                            ch = 0.5 + 0.5 * sin(_Time.y * 0.7 + 6.2831 * ch);
                            float2 cr = cg + ch - cfp;
                            float cd = dot(cr, cr);
                            if (cd < m1) { m2 = m1; m1 = cd; }
                            else if (cd < m2) { m2 = cd; }
                        }
                    }
                    float cl = 1.0 - smoothstep(0.0, 0.10, sqrt(m2) - sqrt(m1));
                    col = lerp(col, _Shallow.rgb + 0.25, cl * _Caustic * (1.0 - 0.6 * d01));
                }

                float fallFoam = 0.0;
                float fallEdge = 1.0;
                if (_Streak > 0.001)
                {
                    // водопад: мягкие струи вдоль падения (три слоя шума, едут вниз с разной скоростью), без ступенек и резких границ.
                    // Вверху вода гладкая и прозрачно-голубая, чем ниже по падению, тем белее и рыхлее; у края лист плавно тает.
                    float flc = i.uv2.x;
                    float fhw = max(i.uv2.y, 0.5);
                    float fside = saturate(abs(flc) / fhw);
                    float ftime = _Time.y * _Speed;
                    float s1 = FNoise(float2(flc * 1.6, flow * 0.16 - ftime * 0.30));
                    float s2 = FNoise(float2(flc * 3.4 + 7.3, flow * 0.28 - ftime * 0.55));
                    float s3 = FNoise(float2(flc * 7.0 + 19.1, flow * 0.55 - ftime * 0.95));
                    float sn = s1 * 0.50 + s2 * 0.32 + s3 * 0.18;
                    float fstreak = smoothstep(0.46, 0.74, sn);
                    float fdark = 1.0 - smoothstep(0.30, 0.50, sn);
                    float faer = smoothstep(0.0, max(_FallLen, 1.0) * 0.9, flow);
                    col = lerp(col, col * 0.86, fdark * 0.45 * _Streak);
                    col = lerp(col, _Foam.rgb, saturate(fstreak * (0.30 + 0.45 * faer) + fside * fside * 0.35) * _Streak);
                    float ftop = (1.0 - smoothstep(0.0, 1.4, flow)) * (0.55 + 0.45 * FNoise(float2(flc * 2.0, ftime * 0.5)));
                    float fbot = 0.0;
                    if (_FallLen > 0.01 && _FallFade < 0.5)
                        fbot = smoothstep(_FallLen - 2.2, _FallLen - 0.2, flow) * (0.65 + 0.35 * FNoise(float2(flc * 2.5, ftime * 0.8)));
                    fallFoam = saturate(max(ftop * 0.8, fbot)) * _Streak;
                    col = lerp(col, _Foam.rgb, fallFoam);
                    float fedgeN = FNoise(float2(flc * 2.3, flow * 0.35 - ftime * 0.2));
                    float fedge = 1.0 - smoothstep(0.62 + 0.18 * fedgeN, 1.0, fside);
                    fallEdge = lerp(1.0, fedge, _Streak);
                    fallEdge *= lerp(1.0, 0.78 + 0.22 * saturate(sn * 1.4), _Streak * 0.6);
                    if (_FallFade > 0.5 && _FallLen > 0.01)
                        fallEdge *= 1.0 - smoothstep(_FallLen * 0.55, _FallLen * 0.98, flow);
                }

                float glint = 0.0;
                if (_Sparkle > 0.001)
                {
                    // две сетки мерцающих искр, плывущих по течению: на глубине ярче, у берега и в пене гаснут
                    for (int gk = 0; gk < 2; gk++)
                    {
                        float gsc = gk == 0 ? 2.6 : 4.1;
                        float2 gp = i.positionWS.xz * gsc + float2(_Time.y * 0.12, -_Time.y * 0.18 * _Speed) * (1.0 + gk);
                        float2 gi = floor(gp);
                        float2 gf = frac(gp);
                        float gh = frac(sin(dot(gi, float2(41.3, 289.1)) + gk * 17.0) * 43758.5453);
                        float2 gc = 0.2 + 0.6 * float2(frac(gh * 7.3), frac(gh * 13.7));
                        float gtw = pow(saturate(sin(_Time.y * (2.0 + gh * 3.0) + gh * 40.0) * 0.5 + 0.5), 6.0);
                        float gd = length(gf - gc);
                        glint = max(glint, (1.0 - smoothstep(0.05, 0.12, gd)) * gtw * step(0.6, gh));
                    }
                    glint *= smoothstep(0.15, 0.6, d01) * (1.0 - foamExtra) * (1.0 - shore) * _Sparkle;
                    col = lerp(col, half3(1.0, 1.0, 1.0), glint);
                }

                col = lerp(col, col * half3(1.05, 1.0, 0.91), 0.6);   // золотой час: вода чуть теплее
                half aBase = _Alpha;
                if (_ShallowAlpha >= 0.0) aBase = lerp(_ShallowAlpha, _Alpha, saturate(d01 * 1.6));
                half a = aBase * smoothstep(-0.02, 0.06, depth);
                a = max(a, shore * 0.95 * smoothstep(-0.02, 0.02, depth));
                a = max(a, foamExtra * 0.95 * smoothstep(-0.02, 0.02, depth));
                a = max(a, glint * 0.9 * smoothstep(-0.02, 0.02, depth));
                a = max(a, fallFoam * 0.95);
                a *= fallEdge;
                a *= 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 1.3 + i.positionWS.x * 0.7 + i.positionWS.z * 0.9));

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
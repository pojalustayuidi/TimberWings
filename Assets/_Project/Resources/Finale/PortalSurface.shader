Shader "TimberWings/PortalSurface"
{
    Properties
    {
        _Activation ("Activation", Range(0, 1)) = 0
        _Motion ("Motion", Range(0, 1)) = 1
        _DrawIn ("Draw in", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+25" }
        Pass
        {
            Name "Portal"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Activation;
                float _Motion;
                float _DrawIn;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(231.11, 133.87));
                p += dot(p, p + 37.42);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float x = uv.x * 2.0 - 1.0;
                float crown = max(0.0, (uv.y - 0.449) / 0.551);
                float border = length(float2(x, crown));
                float mask = (1.0 - smoothstep(.979, 1.0, border)) * smoothstep(0.0, .065, uv.y);
                float t = _Time.y * _Motion * .64;
                float2 p = float2(x, (uv.y - .50) * 1.90);
                float r = length(p);
                float angle = atan2(p.y, p.x + 0.0001);
                float warp = sin(p.x * 5.1 + t) * .56 + cos(p.y * 7.0 - t * .71) * .40;
                float phase = 3.0 * angle + 17.0 * r + t * 4.1 + warp + _DrawIn * 3.0;
                float ribbons = pow(saturate(.5 + .5 * sin(phase)), 3.0);
                float filament = pow(saturate(.5 + .5 * sin(phase * 2.0 + warp)), 16.0);
                float mist = .5 + .5 * sin(r * 12.0 + angle * 2.0 + t * 2.0 + warp * 2.1);
                float inner = smoothstep(.87, .968, border) * (1.0 - smoothstep(.968, 1.0, border));
                float edgeFlow = .66 + .34 * sin(angle * 5.0 + t * 3.0);
                float throat = exp(-r * r * 24.0);

                float2 grid = float2(angle * 5.0, (r + t * .26) * 11.0);
                float2 cell = floor(grid);
                float rand = Hash(cell);
                float2 dotCenter = float2(.29 + rand * .41, .29 + Hash(cell + 7.19) * .42);
                float spark = (1.0 - smoothstep(.025, .115, length(frac(grid) - dotCenter))) * step(.82, rand);

                float3 ink = float3(.019, .033, .091);
                float3 mint = float3(.16, .91, .71);
                float3 lilac = float3(.54, .43, 1.0);
                float3 flow = lerp(mint, lilac, .47 + .40 * sin(angle + .8 * r - t * .44));
                float energy = _Activation * (.50 + .50 * _DrawIn);
                float3 color = ink + flow * mist * (.10 + .15 * energy);
                color += flow * (ribbons * .29 + filament * .14 + spark * .68) * (.23 + energy);
                color += lerp(flow, float3(.71, 1.0, .94), .35) * inner * edgeFlow * (.22 + energy * 1.48);
                color += float3(.43, .92, .90) * throat * (.10 + _DrawIn * 1.17);
                float alpha = mask * saturate(.045 + .70 * _Activation + .20 * _DrawIn +
                    inner * (.18 + energy * .30) + ribbons * (.09 + energy * .14) + throat * .22 * _DrawIn);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}

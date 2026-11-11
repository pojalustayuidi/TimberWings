Shader "TimberWings/PortalSurface"
{
    Properties
    {
        _Activation ("Activation", Range(0, 1)) = 0
        _Motion ("Motion", Range(0, 1)) = 1
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
                float mask = (1.0 - smoothstep(.976, 1.0, border)) * smoothstep(0.0, .055, uv.y);
                float t = _Time.y * _Motion * (.32 + _Activation * .38);
                float2 p = float2(x, (uv.y - .53) * 1.83);
                float r = length(p);
                float angle = atan2(p.y, p.x + 0.0001);
                float warp = sin(p.x * 4.7 + t) * .42 + cos(p.y * 6.0 - t * .75) * .39;
                float twist = angle * 3.0 + r * 12.0 + warp - t * 2.8;
                float current = pow(saturate(.5 + .5 * sin(twist)), 4.0);
                float filaments = pow(saturate(.5 + .5 * sin(twist * 2.0 + warp)), 13.0);
                float mist = .5 + .5 * sin(p.y * 8.0 - p.x * 2.7 + sin(p.x * 4.0 + t) * 2.0 - t);
                float rim = pow(saturate(border), 9.0) * (.75 + .25 * cos(angle * 5.0 - t * 2.0));

                float2 tiles = uv * float2(24.0, 29.0) - float2(0.0, t * 1.5);
                float2 cell = floor(tiles);
                float pick = Hash(cell);
                float2 sparkOffset = float2(.24 + pick * .45, .29 + Hash(cell + 3.17) * .42);
                float spark = (1.0 - smoothstep(.025, .12, length(frac(tiles) - sparkOffset))) * step(.82, pick);

                float3 color = lerp(float3(.012, .050, .089), float3(.035, .27, .29), mist * .68);
                color += float3(.05, .55, .49) * (current * .55 + filaments * .15);
                color += float3(.21, .97, .81) * (rim * (.15 + .9 * _Activation) + spark * .65);
                float alpha = mask * saturate(.04 + .76 * _Activation + rim * .32 + current * .14 + spark * .14);
                return half4(MixFog(color, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}

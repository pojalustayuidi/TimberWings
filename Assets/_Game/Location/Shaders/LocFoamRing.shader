
Shader "BuilderSim/LocFoamRing"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
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
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = IN.uv.x;
                float ang = IN.uv.y;
                float ph = frac(sin(dot(floor(IN.positionWS.xz * 0.5), float2(12.9898, 78.233))) * 43758.5453) * 6.2831;
                float pulse = 0.5 + 0.5 * sin(_Time.y * 1.6 + ph);
                float edge = smoothstep(0.0, 0.3, r) * (1.0 - smoothstep(0.35 + 0.4 * pulse, 1.0, r));
                float dash = 0.65 + 0.35 * sin(ang * 6.2831 * 9.0 + _Time.y * 0.8 + ph);
                float a = edge * dash * 0.75 * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

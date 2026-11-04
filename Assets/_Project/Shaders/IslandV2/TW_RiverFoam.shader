// Lightweight grounded highlight material. Not a second river; uses RiverSurfaceAt + ~8cm in editor mesh.
// URP forward, fogged and ambient-lit for Cloudy Candy Bright daytime AND night.
Shader "Project/IslandV2/TW_RiverFoam"
{
    Properties { _Tint ("Foam tint", Color) = (0.95, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                half fog : TEXCOORD0;
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings v) : SV_Target
            {
                half3 ambient = SampleSH(half3(0,1,0));
                // Per-scene ambient varies with DayNightCycle; foam is never emissive.
                half brightness = saturate(0.26h + dot(ambient, half3(0.24h,0.56h,0.20h)) * 1.45h);
                half3 rgb = _Tint.rgb * v.color.rgb * brightness;
                rgb = MixFog(rgb, v.fog);
                return half4(rgb, _Tint.a * v.color.a);
            }
            ENDHLSL
        }
    }
}

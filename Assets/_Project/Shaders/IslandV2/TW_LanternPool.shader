Shader "Project/IslandV2/TW_LanternPool"
{
    Properties
    {
        [HDR] _EmissionColor ("Light colour", Color) = (1.3, 0.77, 0.31, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+8" }
        Pass
        {
            Name "SoftGroundLight"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _EmissionColor;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float alpha : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.alpha = input.color.a;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float4 frag(Varyings input) : SV_Target
            {
                float brightness = max(_EmissionColor.r, max(_EmissionColor.g, _EmissionColor.b));
                float3 colour = _EmissionColor.rgb / max(brightness, 0.001);
                colour = MixFogColor(colour, float3(0.0, 0.0, 0.0), input.fogFactor);
                return float4(colour, input.alpha * min(0.26, brightness * 0.11));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

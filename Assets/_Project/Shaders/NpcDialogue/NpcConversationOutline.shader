Shader "TimberWings/NPC Conversation Outline"
{
    Properties
    {
        _NpcColor ("Color",Color)=(1,.91,.62,1)
        _NpcWidth ("Width in pixels",Float)=2.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Name "NpcEdge"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _NpcColor;
                float _NpcWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
                float2 direction=mul((float3x3)UNITY_MATRIX_V,normalWS).xy;
                direction/=max(length(direction),.0001);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.xy+=direction*(2*_NpcWidth/_ScaledScreenParams.xy)*output.positionCS.w;
                return output;
            }
            half4 frag(Varyings input):SV_Target { return _NpcColor; }
            ENDHLSL
        }
    }
}

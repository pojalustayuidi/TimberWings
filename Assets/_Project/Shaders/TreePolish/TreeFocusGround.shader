Shader "TimberWings/Tree Focus Ground"
{
    Properties { _FocusColor ("Color", Color) = (1,.83,.4,.6) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FocusColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output; }
            half4 frag(Varyings input) : SV_Target { return _FocusColor; }
            ENDHLSL
        }
    }
}

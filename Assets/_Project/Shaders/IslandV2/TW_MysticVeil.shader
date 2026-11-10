Shader "Project/IslandV2/TW_MysticVeil"
{
    Properties
    {
        _BaseMap("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Alpha("Opacity",Range(0,1))=1
        _Intensity("Glow",Range(0,4))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Alpha;
            float _Intensity;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Varyings Vert(Attributes input)
            {
                Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;output.color=input.color;return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                float4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_Color*input.color;
                c.rgb*=_Intensity;c.a*=_Alpha;return c;
            }
            ENDHLSL
        }
    }
}

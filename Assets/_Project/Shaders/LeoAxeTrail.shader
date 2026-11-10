Shader "TimberWings/LeoAxeTrail"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input {float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Output {float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            Output Vert(Input i){Output o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color;o.uv=i.uv;return o;}
            half4 Frag(Output i):SV_Target {half edge=smoothstep(0,.25,i.uv.y)*smoothstep(0,.25,1-i.uv.y);return half4(i.color.rgb,i.color.a*edge);}
            ENDHLSL
        }
    }
}

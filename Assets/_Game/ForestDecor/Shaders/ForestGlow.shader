Shader "ForestDecor/Glow"
{
    Properties { _Color("Color", Color) = (1,1,1,1) _Intensity("Intensity", Float) = 1 _Power("Falloff", Float) = 2 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Power;
            CBUFFER_END
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), _Power);
                half4 c = _Color * i.col;
                return half4(c.rgb, c.a * a * _Intensity);
            }
            ENDHLSL
        }
    }
}
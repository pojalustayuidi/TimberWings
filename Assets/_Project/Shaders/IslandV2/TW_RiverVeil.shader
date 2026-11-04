// Lightweight stylized waterfall veil. Decorative only: does not replace native River_Fall logic.
Shader "Project/IslandV2/TW_RiverVeil"
{
    Properties
    {
        _Tint ("Water tint", Color) = (0.65, 0.85, 1, 1)
        _Foam ("Foam tint", Color) = (0.95, 0.99, 1, 1)
        _FlowSpeed ("Flow speed", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+3" }
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
                half4 _Foam;
                half _FlowSpeed;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(Varyings v) : SV_Target
            {
                half flow = frac(v.uv.y * 5.2h - _Time.y * max(0.15h, _FlowSpeed) * 0.42h);
                half streak = 0.55h + 0.45h * sin((v.uv.y * 18.0h - _Time.y * _FlowSpeed * 2.0h) + v.uv.x * 5.2h);
                half edge = saturate(1.0h - abs(v.uv.x * 2.0h - 1.0h));
                half foam = saturate(streak * 0.65h + edge * 0.45h + (1.0h - flow) * 0.15h);
                half alpha = saturate(v.color.a * (0.62h + foam * 0.45h));
                half3 ambient = SampleSH(half3(0, 1, 0));
                half brightness = saturate(0.24h + dot(ambient, half3(0.24h, 0.56h, 0.20h)) * 1.35h);
                half3 rgb = lerp(_Tint.rgb, _Foam.rgb, foam * 0.58h) * v.color.rgb * brightness;
                rgb = MixFog(rgb, v.fog);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

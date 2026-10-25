// Assets/_Project/Shaders/Pond/PondGlow.shader
// Мягкое аддитивное свечение: светлячки, ореолы вокруг фонарей, брызги. URP, без глубины.
// Работает и на частицах (цвет частицы = цвет вершины), и на обычном квадрате (ореол фонаря).
Shader "Project/Pond/Glow"
{
    Properties
    {
        _Tint ("Цвет (rgb) и сила (a)", Color) = (1.0, 0.85, 0.5, 1)
        _Soft ("Мягкость края", Range(0.5, 6)) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Soft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r = saturate(1.0 - length(d));
                float a = pow(r, _Soft) + 0.8 * pow(r, _Soft * 3.0);
                float3 rgb = _Tint.rgb * i.color.rgb;
                return half4(rgb, saturate(a) * _Tint.a * i.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

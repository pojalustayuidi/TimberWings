// Assets/_Project/Shaders/Candy/CandyParticle.shader
// Мягкая белая «капля пены» для частиц воды: круг считается в шейдере по uv, текстур нет. Цвет и прозрачность берутся из частицы.
Shader "Project/CandyParticle"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge softness", Range(0.05, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Tint;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;
                half a = (half)(1.0 - smoothstep(1.0 - _Softness, 1.0, r));
                half3 col = MixFog(i.color.rgb, i.fog);
                return half4(col, i.color.a * a);
            }
            ENDHLSL
        }
    }
}

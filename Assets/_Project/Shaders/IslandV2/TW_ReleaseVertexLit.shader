// TIMBER WINGS - cheap faceted scene props / URP. No CandyToon variants, no emission.
Shader "Project/IslandV2/TW_ReleaseVertexLit"
{
    Properties { _Brightness("Art brightness", Range(0.5,1.25)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Brightness;
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
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS);
                o.positionCS = p.positionCS;
                o.normalWS = n.normalWS;
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 ambient = SampleSH(n);
                Light sun = GetMainLight();
                half direct = saturate(dot(n, sun.direction)) * 0.44h;
                half3 lightTerm = saturate(ambient * 1.05h + direct * sun.color.rgb + 0.13h);
                half3 rgb = i.color.rgb * lightTerm * _Brightness;
                rgb = MixFog(rgb, i.fog);
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}

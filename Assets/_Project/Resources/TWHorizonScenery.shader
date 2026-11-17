Shader "Project/TimberWings/HorizonScenery"
{
    Properties
    {
        _Haze ("Horizon haze", Color) = (0.67, 0.81, 0.94, 1)
        _Atmosphere ("Night, twilight, camera range, cloud level", Vector) = (0, 0, 150, -20)
        _SunDirection ("Light direction", Vector) = (0, 1, 0, 0)
        _LightTint ("Light tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "Horizon"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Haze;
                float4 _Atmosphere;
                float4 _SunDirection;
                half4 _LightTint;
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
                half3 shaded : TEXCOORD0;
                half haze : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;

                half3 baseColor = input.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    baseColor = SRGBToLinear(baseColor);
                #endif

                half3 normal = TransformObjectToWorldNormal(input.normalOS);
                half windward = smoothstep(-0.35h, 0.75h, dot(normal, _SunDirection.xyz));
                half light = lerp(0.53h, 1.08h, windward);
                half night = _Atmosphere.x;
                half twilight = _Atmosphere.y;
                half3 tint = lerp(half3(0.76h, 0.85h, 1.0h), _LightTint.rgb, windward * 0.64h);
                half3 ambient = lerp(half3(1.0h, 1.0h, 1.0h), half3(0.41h, 0.57h, 0.85h), night);
                half lantern = saturate(1.0h - input.color.a) * (0.1h + 1.65h * night + 0.38h * twilight);
                output.shaded = baseColor * (tint * ambient * light + lantern);

                float range = _Atmosphere.z;
                float distanceToCamera = distance(vertex.positionWS, _WorldSpaceCameraPos);
                half farHaze = smoothstep(range * 0.25, range * 0.92, distanceToCamera) * 0.52h;
                half clipHaze = smoothstep(range * 0.81, range, -vertex.positionVS.z) * 0.83h;
                half cloudHaze = (1.0h - smoothstep(_Atmosphere.w - 5.0, _Atmosphere.w + 10.0, vertex.positionWS.y)) * 0.48h;
                output.haze = 1.0h - (1.0h - farHaze) * (1.0h - cloudHaze) * (1.0h - clipHaze);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(lerp(input.shaded, _Haze.rgb, input.haze), 1.0h);
            }
            ENDHLSL
        }
    }
}

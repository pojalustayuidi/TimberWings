// Assets/_Project/Shaders/IslandV2/IslandHaze.shader
// Остров v2, этап 3: дымка. Мягкие круглые карточки, повёрнутые к камере. Рисует IslandLook.Haze одним instanced draw call.
// Свойства от кода: _Tint (цвет и альфа, ведёт IslandLook по времени суток), _Fade (x = near start, y = near end, z = far start, w = far end, метры от камеры).
// Вблизи карточки растворяются (игрок и тропа чистые), вдали тоже. Без теней, без тумана сцены (дымка сама цвета тумана).
Shader "Project/IslandV2/Haze"
{
    Properties
    {
        _Tint ("Цвет и альфа", Color) = (0.8, 0.9, 1, 0.3)
        _Fade ("Затухание: near start, near end, far start, far end", Vector) = (8, 20, 150, 220)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Haze"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fade : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // центр и размер карточки из матрицы экземпляра
                float4x4 m = UNITY_MATRIX_M;
                float3 center = float3(m[0][3], m[1][3], m[2][3]);
                float size = length(float3(m[0][0], m[1][0], m[2][0]));

                // поворот к камере: оси камеры в мировых координатах
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 posWS = center + (right * IN.positionOS.x + up * IN.positionOS.y) * size;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;

                float dist = distance(_WorldSpaceCameraPos, center);
                float nearK = smoothstep(_Fade.x, max(_Fade.y, _Fade.x + 0.01), dist);
                float farK = 1.0 - smoothstep(_Fade.z, max(_Fade.w, _Fade.z + 0.01), dist);
                OUT.fade = (half)(nearK * farK);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float2 d = IN.uv * 2.0 - 1.0;
                float r = saturate(1.0 - dot(d, d));   // круглое пятно, мягкий край
                half a = (half)(r * r) * _Tint.a * IN.fade;
                return half4(_Tint.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

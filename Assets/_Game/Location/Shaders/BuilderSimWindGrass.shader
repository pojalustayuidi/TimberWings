Shader "BuilderSim/WindGrass"
{
    Properties
    {
        _Sway ("Sway (m)", Float) = 0.12
        _Speed ("Speed", Float) = 1.7
        _Bend ("Player bend (m)", Float) = 0.45
        _Wrap ("Light wrap", Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Sway;
                float _Speed;
                float _Bend;
                float _Wrap;
            CBUFFER_END

            // глобальная позиция игрока (xyz) и радиус (w, 0 = выключено); в своём cbuffer, чтобы не ломать SRP Batcher
            CBUFFER_START(LocGlobals)
                float4 _LocPlayer;
            CBUFFER_END

            struct Attr
            {
                float4 pos : POSITION;
                float4 col : COLOR;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 col : TEXCOORD0;
                float4 sc : TEXCOORD1;
            };

            Varyings vert(Attr v)
            {
                Varyings o;
                float3 pw = TransformObjectToWorld(v.pos.xyz);
                float w = v.col.a;
                w = w * w;

                float t = _Time.y * _Speed;
                float ph = dot(pw.xz, float2(0.42, 0.31));
                float g = sin(t + ph) * 0.65 + sin(t * 2.3 + ph * 2.1) * 0.35;
                pw.x += (g + 0.35) * _Sway * w;
                pw.z += g * 0.45 * _Sway * w;

                float2 dp = pw.xz - _LocPlayer.xz;
                float d = length(dp);
                float push = saturate(1.0 - d / max(_LocPlayer.w, 0.001)) * step(0.001, _LocPlayer.w);
                push = push * push;
                pw.xz += dp / max(d, 0.05) * push * _Bend * w;
                pw.y -= push * _Bend * 0.5 * w;

                VertexPositionInputs vi = (VertexPositionInputs)0;
                vi.positionWS = pw;
                vi.positionVS = TransformWorldToView(pw);
                vi.positionCS = TransformWorldToHClip(pw);
                float4 ndc = vi.positionCS * 0.5f;
                vi.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                vi.positionNDC.zw = vi.positionCS.zw;

                o.posCS = vi.positionCS;
                o.col = v.col.rgb;
                o.sc = GetShadowCoord(vi);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light l = GetMainLight(i.sc);
                float ndl = saturate(l.direction.y * (1.0 - _Wrap) + _Wrap);
                float3 amb = SampleSH(float3(0, 1, 0));
                float3 c = i.col * (amb + l.color * ndl * l.distanceAttenuation * l.shadowAttenuation);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
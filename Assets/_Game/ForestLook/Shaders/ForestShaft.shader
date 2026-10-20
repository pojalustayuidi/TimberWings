Shader "ForestLook/Shaft"
{
    Properties
    {
        _Color("Color", Color) = (1,0.88,0.58,1)
        _Intensity("Intensity", Float) = 0.16
        _Alpha("Alpha", Float) = 1
        _FadeNear("Fade near", Float) = 3
        _FadeFar("Fade far", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Shaft"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Alpha;
                float _FadeNear;
                float _FadeFar;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                // мягкие бока
                float x = abs(i.uv.x * 2.0 - 1.0);
                float side = 1.0 - x * x;
                side = side * side;
                // у земли плавно появляется, к кроне тает
                float v = i.uv.y;
                float along = smoothstep(0.0, 0.12, v) * (1.0 - smoothstep(0.35, 1.0, v));
                // лёгкое мерцание, бегущее вдоль луча
                float flick = 0.78 + 0.22 * sin(_Time.y * 0.9 + i.wp.x * 1.3 + i.wp.z * 1.7 + v * 3.0);
                // не режем кадр жёстко, когда луч близко к камере
                float camD = distance(_WorldSpaceCameraPos, i.wp);
                float camFade = saturate((camD - _FadeNear) / max(0.01, _FadeFar - _FadeNear));
                float a = side * along * flick * camFade * _Alpha * _Intensity * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
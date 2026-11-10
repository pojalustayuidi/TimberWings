Shader "Project/IslandV2/TW_RiverRibbon"
{
    Properties
    {
        _DeepColor ("Clear blue water", Color) = (0.035, 0.52, 0.84, 1)
        _ShallowColor ("Turquoise shallows", Color) = (0.28, 0.86, 0.91, 1)
        _FoamColor ("Pearl foam", Color) = (0.94, 0.99, 1.0, 1)
        _FlowSpeed ("Flow metres per second", Range(0, 4)) = 1.15
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _DeepColor;
        half4 _ShallowColor;
        half4 _FoamColor;
        float _FlowSpeed;
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
        float3 positionWS : TEXCOORD0;
        half3 normalWS : TEXCOORD1;
        float2 uv : TEXCOORD2;
        half4 data : COLOR;
        half fog : TEXCOORD3;
        float4 shadowCoord : TEXCOORD4;
    };
    Varyings vert(Attributes v)
    {
        Varyings o;
        VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
        o.positionCS = p.positionCS;
        o.positionWS = p.positionWS;
        o.normalWS = TransformObjectToWorldNormal(v.normalOS);
        o.uv = v.uv; o.data = v.color;
        o.fog = ComputeFogFactor(p.positionCS.z);
        o.shadowCoord = GetShadowCoord(p);
        return o;
    }
    float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
    float noise21(float2 p)
    {
        float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
        return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                    lerp(hash21(i + float2(0,1)), hash21(i + 1), f.x), f.y);
    }
    void TailClip(Varyings i)
    {
        // Stable opaque depth. Fade only the final mist section, never the lip.
        float fade = i.data.b;
        if (fade < 0.999)
        {
            float dither = frac(52.9829189 * frac(dot(floor(i.positionCS.xy), float2(0.06711056, 0.00583715))));
            clip(fade - dither - 0.001);
        }
    }
    ENDHLSL
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 frag(Varyings i) : SV_Target
            {
                TailClip(i);
                float u = i.uv.x, edge = abs(u), fall = i.data.g;
                float flow = i.uv.y - _Time.y * _FlowSpeed;
                float broad = noise21(float2(u * 2.5, flow * 0.13));
                float shallow = smoothstep(0.55, 1.0, edge) * 0.58 + (1 - i.data.r) * 0.10;
                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(shallow));
                col *= 0.98 + broad * 0.04;
                // Tiny broken highlights, not metre-long white hooks.
                float2 cell = float2(u * 8.0, flow * 0.75);
                float2 cellOffset = frac(cell) - 0.5;
                cellOffset.y *= 2.8;
                float glint = (1.0 - smoothstep(0.025, 0.065, dot(cellOffset, cellOffset))) *
                              step(0.82, hash21(floor(cell)));
                float bank = smoothstep(0.90, 0.99, edge + (broad - 0.5) * 0.045);
                bank *= smoothstep(0.40, 0.74, noise21(float2(u * 4, flow * 0.28)));
                float lanes = noise21(float2(u * 6.5 + sin(flow * 0.08) * 0.25, flow * 0.22));
                float streak = smoothstep(0.48, 0.85, lanes) * fall;
                float crest = smoothstep(0.02, 0.15, fall) * (1 - smoothstep(0.45, 0.95, fall));
                float mist = fall * (1 - i.data.b);
                float foam = bank * 0.30 * (1 - fall) + glint * 0.12 * (1 - fall) + streak * 0.62 + crest * 0.28;
                col = lerp(col, _ShallowColor.rgb, fall * 0.42);
                col = lerp(col, _FoamColor.rgb, saturate(foam + mist * 0.65));
                col *= 1 - i.data.a * 0.48;
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight(i.shadowCoord);
                half wrapped = saturate((dot(n, sun.direction) + 0.55) / 1.55);
                float ambient = dot(SampleSH(n), float3(0.30, 0.59, 0.11));
                float sunlight = dot(sun.color, float3(0.30, 0.59, 0.11));
                // Preserve the blue hue while still responding to night and bridge shadows.
                float brightness = ambient * 0.65 + sunlight * (0.55 + wrapped * 0.45);
                brightness *= 0.72 + sun.shadowAttenuation * 0.28;
                col *= clamp(brightness, 0.20, 1.12);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment depthFrag
            half4 depthFrag(Varyings i) : SV_Target { TailClip(i); return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

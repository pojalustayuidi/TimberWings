Shader "TimberWings/Tree Focus Outline"
{
    Properties
    {
        _FocusColor ("Focus color", Color) = (1,.83,.4,.9)
        _FocusWidth ("Width in pixels", Float) = 1.8
        _FocusRadial ("Radial fallback", Float) = 0
        _FocusCenter ("Mesh center", Vector) = (0,0,0,0)
        _FocusTreeWind ("TreeToon wind", Float) = 0
        _Rand ("Wind seed", Float) = -1
        _WindDir ("Wind direction", Vector) = (1,0,.4,0)
        _WindStrength ("Wind strength", Float) = .12
        _WindSpeed ("Wind speed", Float) = 1.1
        _WindStart ("Wind start", Float) = 1
        _WindHeight ("Wind height", Float) = 4
        _Flutter ("Flutter", Float) = 0
        _FlutterSpeed ("Flutter speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "FocusOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _FocusColor, _FocusCenter, _WindDir;
                float _FocusWidth, _FocusRadial, _FocusTreeWind, _Rand;
                float _WindStrength, _WindSpeed, _WindStart, _WindHeight, _Flutter, _FlutterSpeed;
            CBUFFER_END
            float4 _PeekData, _PeekParams;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            float Hash12(float2 p) { float3 q = frac(float3(p.x,p.y,p.x) * .1031); q += dot(q,q.yzx + 33.33); return frac((q.x + q.y) * q.z); }
            float3 WindPosition(float3 p)
            {
                float3 world = TransformObjectToWorld(p);
                if (_FocusTreeWind < .5) return world;
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float scale = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float seed = _Rand < 0 ? Hash12(origin.xz * 1.37) : saturate(_Rand);
                float h = saturate((p.y - _WindStart) / max(_WindHeight, .01)); h *= h;
                float2 dir = _WindDir.xz; float len = length(dir); dir = len > .0001 ? dir / len : float2(1,0);
                float2 side = float2(-dir.y, dir.x);
                float phase = dot(origin.xz, float2(.31,.23)) + seed * 6.2831;
                float t = _Time.y * _WindSpeed;
                float g = sin(t + phase) * .6 + sin(t * 2.1 + phase * 1.7) * .4 + .35;
                float s = cos(t * 1.3 + phase * .8) * .35;
                world.xz += (dir * g + side * s) * (_WindStrength * h * scale);
                float flutter = sin(_Time.y * _FlutterSpeed + dot(p, float3(3.7,5.1,4.3)) + phase) * _Flutter * h * scale;
                return world + float3(flutter, flutter * .6, -flutter);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normal = input.normalOS;
                if (_FocusRadial > .5) normal = input.positionOS.xyz - _FocusCenter.xyz;
                normal = TransformObjectToWorldNormal(normal);
                float2 projected = mul((float3x3)UNITY_MATRIX_V, normal).xy;
                projected /= max(length(projected), .0001);
                output.positionWS = WindPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.positionCS.xy += projected * (2 * _FocusWidth / _ScaledScreenParams.xy) * output.positionCS.w;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                // Match the existing canopy opening around the player; never fill that opening with a glowing shell.
                if (_FocusTreeWind > .5 && _PeekParams.z > .001)
                {
                    float4 clipPoint = TransformWorldToHClip(input.positionWS);
                    float2 uv = clipPoint.xy / max(clipPoint.w, .0001) * .5 + .5;
                    if (_ProjectionParams.x < 0) uv.y = 1 - uv.y;
                    float depth = -TransformWorldToView(input.positionWS).z;
                    float distance = length((uv - _PeekData.xy) * float2(_PeekParams.y,1));
                    float radius = max(_PeekData.z, .0001);
                    float hole = 1 - smoothstep(radius * (1 - _PeekData.w), radius, distance);
                    float front = smoothstep(0, .6, _PeekParams.x - depth);
                    float alpha = lerp(1, _PeekParams.w, hole * front * _PeekParams.z);
                    float noise = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(.06711056,.00583715))));
                    clip(alpha - noise);
                }
                return _FocusColor;
            }
            ENDHLSL
        }
    }
}

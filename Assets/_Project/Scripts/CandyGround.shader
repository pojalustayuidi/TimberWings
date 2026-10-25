// Assets/_Project/Shaders/Candy/CandyGround.shader
// Шейдер материала Terrain (URP, классический Forward, под мобильные). Ставится как Terrain.materialTemplate.
//
//  * Слои Terrain — плоские цвета, поэтому текстур слоёв нет: цвет слоёв лежит в свойствах материала (_L2.._L8),
//    а трава (слои 0, 1, 9 «гребень», 10 «влажная») считается шумом: лайм/мята + ворсинки (см. CandyGround.hlsl).
//  * Тропы по splatmap: края вырезаются порогом, поэтому splatmap можно держать низкого разрешения (256 вместо 512),
//    а граница остаётся чёткой. Тёмная кромка/колеи (слой 6) и галька на тропе по шуму.
//  * Splatmap читается напрямую из alphamap-текстур (_Ctrl0.._Ctrl2, 4 слоя на текстуру, порядок как в TerrainData),
//    поэтому не нужен ни basemap, ни дополнительный проход для слоёв > 4: ОДИН проход на все 11 слоёв.
//    Привязку текстур и _TerrainRect делает CandyGroundParams.
//  * Геометрия: инстансинг патчей Terrain (heightmap в вершинном шейдере), как у URP Terrain/Lit.
//  * Свет: мягкие два тона + холодный тинт (CGGroundShade, тот же, что у IslandRock), SH-ambient и доп. огни в вершине.
//  * Проходы: ForwardLit, ShadowCaster, DepthOnly.
Shader "Project/CandyGround"
{
    Properties
    {
        [NoScaleOffset] _Ctrl0 ("Splat 0 (слои 0-3)", 2D) = "black" {}
        [NoScaleOffset] _Ctrl1 ("Splat 1 (слои 4-7)", 2D) = "black" {}
        [NoScaleOffset] _Ctrl2 ("Splat 2 (слои 8-10)", 2D) = "black" {}
        _TerrainRect ("Terrain rect (x0, z0, 1/sizeX, 1/sizeZ)", Vector) = (0, 0, 0.001, 0.001)

        [Header(Layer colors)]
        _L2 ("2 Trail", Color) = (0.80, 0.66, 0.45, 1)
        _L3 ("3 Pad", Color) = (0.52, 0.76, 0.36, 1)
        _L4 ("4 Beach", Color) = (0.90, 0.82, 0.58, 1)
        _L5 ("5 Forest floor", Color) = (0.17, 0.46, 0.17, 1)
        _L6 ("6 Trail dark (edge, ruts)", Color) = (0.67, 0.53, 0.35, 1)
        _L7 ("7 Rock", Color) = (0.50, 0.50, 0.54, 1)
        _L8 ("8 Soil", Color) = (0.42, 0.33, 0.21, 1)

        [Header(Trail)]
        _TrailEdge ("Trail threshold", Range(0.2, 0.8)) = 0.5
        _TrailSoft ("Trail edge softness", Range(0.02, 0.4)) = 0.12
        _Pebbles ("Pebbles on trail (1/m)", Range(2, 20)) = 9
        _PebbleStrength ("Pebble strength", Range(0, 0.4)) = 0.16
        _PebbleFade ("Pebble fade distance (m)", Float) = 30
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TerrainRect;
            half4 _L2;
            half4 _L3;
            half4 _L4;
            half4 _L5;
            half4 _L6;
            half4 _L7;
            half4 _L8;
            float _TrailEdge;
            float _TrailSoft;
            float _Pebbles;
            float _PebbleStrength;
            float _PebbleFade;
        CBUFFER_END

        // данные патчей Terrain: Unity кладёт их сама (имена фиксированы движком)
        TEXTURE2D(_TerrainHeightmapTexture);
        TEXTURE2D(_TerrainNormalmapTexture);
        float4 _TerrainHeightmapRecipSize;   // 1/w, 1/h, 1/(w-1), 1/(h-1)
        float4 _TerrainHeightmapScale;       // sizeX/(w-1)-ish, sizeY/kMax, sizeZ/(h-1)-ish, 0

        UNITY_INSTANCING_BUFFER_START(Terrain)
            UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)   // xBase, yBase, skipScale, ~
        UNITY_INSTANCING_BUFFER_END(Terrain)

        // Если на этой функции компилятор ругается на UnpackHeightmap, сравни с TerrainInstancing в
        // Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl вашей версии URP (10 строк) и поправь.
        void CGTerrainInstancing(inout float4 positionOS, inout float3 normalOS)
        {
        #ifdef UNITY_INSTANCING_ENABLED
            float2 patchVertex = positionOS.xy;
            float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
            float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
            float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
            positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
            positionOS.y = height * _TerrainHeightmapScale.y;
            normalOS = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2.0 - 1.0;
        #endif
        }
        ENDHLSL

        // ------------------------------------------------------------------ основной проход
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/Candy/CandyGround.hlsl"

            TEXTURE2D(_Ctrl0); SAMPLER(sampler_Ctrl0);
            TEXTURE2D(_Ctrl1); SAMPLER(sampler_Ctrl1);
            TEXTURE2D(_Ctrl2); SAMPLER(sampler_Ctrl2);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half3 ambient : TEXCOORD2;    // SH + доп. огни в вершине
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                CGTerrainInstancing(v.positionOS, v.normalOS);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = nWS;

                float3 amb = max(SampleSH(nWS), float3(0.06, 0.055, 0.074));
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint cnt = GetAdditionalLightsCount();
                for (uint k = 0u; k < cnt; k++)
                {
                    Light al = GetAdditionalLight(k, posWS);
                    float nd = dot(nWS, al.direction) * 0.5 + 0.5;
                    amb += al.color * (al.distanceAttenuation * nd * nd);
                }
            #endif
                o.ambient = half3(amb);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = (i.positionWS.xz - _TerrainRect.xy) * _TerrainRect.zw;
                half4 c0 = SAMPLE_TEXTURE2D(_Ctrl0, sampler_Ctrl0, suv);
                half4 c1 = SAMPLE_TEXTURE2D(_Ctrl1, sampler_Ctrl1, suv);
                half4 c2 = SAMPLE_TEXTURE2D(_Ctrl2, sampler_Ctrl2, suv);
                float camD = distance(_WorldSpaceCameraPos, i.positionWS);
                float2 xz = i.positionWS.xz;

                // веса слоёв: 0 GrassA, 1 GrassB, 2 Trail, 3 Pad, 4 Beach, 5 Forest, 6 TrailDark, 7 Rock, 8 Soil, 9 Crest, 10 Wet
                float gw = c0.r + c0.g;
                float mintBias = saturate(c0.g / max(gw, 1e-3) * 0.6 + c2.b * 0.5);
                float3 grass = CGGrass(xz, camD, mintBias);
                grass *= 1.0 + c2.g * 0.14;                          // гребни светлее

                // всё, кроме тропы: нормируем без слоёв 2 и 6, чтобы края тропы потом вырезать порогом
                float rest = gw + c0.a + c1.r + c1.g + c1.a + c2.r + c2.g + c2.b;
                float3 baseCol = grass * (gw + c2.g + c2.b)
                               + (float3)_L3.rgb * c0.a
                               + (float3)_L4.rgb * c1.r
                               + (float3)_L5.rgb * c1.g
                               + (float3)_L7.rgb * c1.a
                               + (float3)_L8.rgb * c2.r;
                baseCol /= max(rest, 1e-3);

                // тропа: чёткий край по порогу + галька
                float trailM = smoothstep(_TrailEdge - _TrailSoft, _TrailEdge + _TrailSoft, c0.b);
                float3 trailCol = _L2.rgb;
                float pf = 1.0 - smoothstep(_PebbleFade * 0.5, _PebbleFade, camD);
                if (pf > 0.01 && trailM > 0.01)
                {
                    float h = CGHash(floor(xz * _Pebbles));
                    trailCol *= 1.0 + (step(0.92, h) * 1.0 - step(h, 0.06) * 0.7) * _PebbleStrength * pf;
                }
                float3 albedo = lerp(baseCol, trailCol, trailM);
                float edgeM = smoothstep(0.18, 0.50, c1.b);          // тёмная кромка и колеи
                albedo = lerp(albedo, _L6.rgb, edgeM);

                float3 N = normalize(i.normalWS);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 col = albedo * CGGroundShade(N, L, i.ambient);

                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ тень
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vertShadow(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                CGTerrainInstancing(v.positionOS, v.normalOS);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE * cs.w);
            #endif
                o.positionCS = cs;
                return o;
            }

            half4 fragShadow(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ глубина
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma exclude_renderers gles
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vertDepth(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                CGTerrainInstancing(v.positionOS, v.normalOS);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 fragDepth(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}

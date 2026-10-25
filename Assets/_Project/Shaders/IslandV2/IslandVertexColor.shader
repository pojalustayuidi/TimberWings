// Assets/_Project/Shaders/IslandV2/IslandVertexColor.shader
// Остров v2: земля и серые заглушки блокаута. Заглушки (альфа вершины 1): цвет вершины. Земля (альфа вершины 0): текстура цвета _GroundTex по мировым XZ (запекает IslandBlockout.GroundTex).
// URP, один материал на всё (SRP Batcher: все свойства в UnityPerMaterial).
//   * Свет: главный свет с тенями (каскады и мягкие тени по настройкам URP), мягкая «обёртка» света вокруг терминатора, ambient через SH.
//   * Тон теней: глобальная переменная _IslandShadowTint (rgb = цвет, a = сила), её ставит IslandLook (этап 3). Нет IslandLook: a = 0, тона нет.
//   * Туман сцены: линейный, экспоненциальный (multi_compile_fog), земля уходит в дымку вместе со всем остальным.
//   * Candy-слой земли (_CandyOn = 1, включает запекание IslandBlockout.GroundTex, только для земли: альфа вершины 0): объём тропы и мягкие холмики
//     через подмену нормали (маска _GroundMask: R,G = уклон тропы, B = плотность посыпки, A = глянец), мягкие мультяшные ступени света, блик «мармелада»
//     на скруглённых краях, сахарные искры, посыпка, воздушная подсветка теней, медленные тени облаков. Геометрия и коллайдеры не меняются.
//   * Проходы: ForwardLit, ShadowCaster, DepthOnly. Для заглушек зданий и деревьев тот же материал: у них цвет вершин серый.
Shader "Project/IslandV2/VertexColor"
{
    Properties
    {
        _Tint ("Множитель цвета", Color) = (1, 1, 1, 1)
        _Bright ("Яркость", Range(0.5, 1.6)) = 1.0
        _Wrap ("Обёртка света (0 = жёсткий терминатор, 1 = мягкий)", Range(0, 1)) = 0.35
        _AmbientMul ("Сила ambient", Range(0, 2)) = 1.0
        [NoScaleOffset] _GroundTex ("Цвет земли (запекает IslandBlockout, G)", 2D) = "white" {}
        _GroundRect ("Рамка текстуры земли: xy = угол (x, z), zw = размер, метры", Vector) = (0, 0, 1, 1)

        [Header(Candy ground)]
        [Toggle] _CandyOn ("Candy-эффекты земли (ставит запекание)", Float) = 0
        [NoScaleOffset] _GroundMask ("Маска земли: RG уклон тропы, B посыпка, A глянец (запекает IslandBlockout)", 2D) = "gray" {}
        _PathBump ("Объём тропы, м (высота подушки)", Range(0, 0.8)) = 0.25
        _GrassBump ("Мягкие холмики травы", Range(0, 1)) = 0.5
        _GrassBumpSize ("Размер холмиков, м", Range(2, 24)) = 5
        _Toon ("Мультяшные ступени света", Range(0, 1)) = 0.5
        _Gloss ("Блеск мармелада", Range(0, 2)) = 0.6
        _GlossPower ("Острота блика", Range(4, 96)) = 24
        _Sparkle ("Сахарные искры", Range(0, 1)) = 0.35
        _Sprinkles ("Посыпка (0 = нет)", Range(0, 1)) = 0.6
        _SprinkleSize ("Густота посыпки (ячеек на метр)", Range(0.5, 4)) = 1.5
        _Airy ("Воздушная подсветка теней", Range(0, 0.4)) = 0.06
        _CloudShade ("Тени облаков", Range(0, 0.4)) = 0.08
        _CloudSize ("Размер облаков, м", Range(10, 120)) = 46
        _CloudSpeed ("Скорость облаков", Range(0, 2)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Bright;
            half _Wrap;
            half _AmbientMul;
            float4 _GroundRect;
            half _CandyOn;
            half _PathBump;
            half _GrassBump;
            float _GrassBumpSize;
            half _Toon;
            half _Gloss;
            half _GlossPower;
            half _Sparkle;
            half _Sprinkles;
            float _SprinkleSize;
            half _Airy;
            half _CloudShade;
            float _CloudSize;
            float _CloudSpeed;
        CBUFFER_END
        TEXTURE2D(_GroundTex);
        SAMPLER(sampler_GroundTex);
        TEXTURE2D(_GroundMask);
        SAMPLER(sampler_GroundMask);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // глобальный тон теней земли (этап 3): rgb = цвет, a = сила; задаётся через Shader.SetGlobalColor, в CBUFFER материала не входит
            float4 _IslandShadowTint;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            // ================================================================ Candy-помощники (все с префиксом Cd, чтобы не пересечься с URP)
            float CdHash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 CdHash22(float2 p)
            {
                float h = CdHash21(p);
                return float2(h, CdHash21(p + h + 17.17));
            }

            float CdNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = CdHash21(i);
                float b = CdHash21(i + float2(1.0, 0.0));
                float c = CdHash21(i + float2(0.0, 1.0));
                float d = CdHash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // sRGB -> линейный (приближение Unity): цвета посыпки задаём «как в инспекторе»
            half3 CdSrgbToLin(half3 c)
            {
                return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
            }

            // уклон мягких холмиков: сумма трёх синусов с искажением, уклон считается формулой (без текстур и соседних выборок), d/dx и d/dz на метр
            float2 CdBumpGrad(float2 posXZ, float size)
            {
                float2 p = posXZ / max(size, 0.5);
                float a = 1.7 * p.x + 1.3 * sin(1.3 * p.y + 0.7);
                float2 g1 = cos(a) * float2(1.7, 1.69 * cos(1.3 * p.y + 0.7));
                float b = 1.9 * p.x - 1.1 * p.y + 2.1;
                float2 g2 = 0.6 * cos(b) * float2(1.9, -1.1);
                float c = 0.8 * p.x + 2.3 * p.y + 0.4 * sin(2.1 * p.x);
                float2 g3 = 0.45 * cos(c) * float2(0.8 + 0.84 * cos(2.1 * p.x), 2.3);
                return (g1 + g2 + g3) / max(size, 0.5);
            }

            // посыпка: короткие цветные «палочки» в ячейках по мировым XZ. rgb = цвет (sRGB), a = покрытие пикселя
            half4 CdSprinkle(float2 posXZ, float density, float cellsPerMeter)
            {
                float2 sc = posXZ * cellsPerMeter;
                float2 cell = floor(sc);
                float2 f = frac(sc);
                float roll = CdHash21(cell + 3.7);
                float has = step(roll, saturate(density) * 0.5);
                float2 ctr = 0.22 + 0.56 * CdHash22(cell + 11.3);
                float ang = CdHash21(cell + 29.1) * 3.14159;
                float2 dirv = float2(cos(ang), sin(ang));
                float2 dd = f - ctr;
                float along = clamp(dot(dd, dirv), -0.12, 0.12);
                float dist = length(dd - dirv * along);
                float px = max(length(fwidth(sc)), 0.0005);
                float cover = saturate((0.065 - dist) / px + 0.5) * has;
                cover *= saturate(1.0 - (px - 0.02) / 0.05);
                float k = CdHash21(cell + 53.9) * 5.0;
                half3 col = k < 1.0 ? half3(1.0, 0.62, 0.78)
                          : k < 2.0 ? half3(1.0, 0.97, 0.92)
                          : k < 3.0 ? half3(1.0, 0.89, 0.48)
                          : k < 4.0 ? half3(0.76, 0.66, 1.0)
                          :           half3(0.55, 0.90, 1.0);
                return half4(col, cover);
            }

            // сахарные искры: редкие точки, мерцают по времени, дальше 28 м гаснут (иначе дрожат)
            float CdSparkle(float2 posXZ, float camDist)
            {
                float2 sc = posXZ * 6.0;
                float2 cell = floor(sc);
                float roll = CdHash21(cell + 71.3);
                float2 ctr = 0.2 + 0.6 * CdHash22(cell + 5.1);
                float r = length(frac(sc) - ctr);
                float tw = 0.5 + 0.5 * sin(_Time.y * (1.5 + roll * 3.0) + roll * 40.0);
                float has = step(0.95, roll);
                float s = has * tw * saturate(1.0 - r / 0.14);
                return s * s * saturate(1.0 - camDist / 28.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = vn.normalWS;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // альфа цвета вершины: 0 = земля (цвет из текстуры по мировым XZ), 1 = заглушки зданий и леса (цвет вершины)
                float2 groundUV = (IN.positionWS.xz - _GroundRect.xy) / max(_GroundRect.zw, float2(0.001, 0.001));
                half3 groundCol = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, groundUV).rgb;
                half3 baseCol = lerp(groundCol, IN.color.rgb, IN.color.a);

                // ---- Candy-слой: g = 1 только на земле и только если землю запекли с маской (_CandyOn); иначе всё ниже ничего не меняет
                half g = (1.0 - IN.color.a) * saturate(_CandyOn);
                half4 gm = SAMPLE_TEXTURE2D(_GroundMask, sampler_GroundMask, groundUV);
                half pathness = saturate((gm.a - 0.25) / 0.5);
                float2 pathTilt = (gm.rg - 0.5) * 4.0 * _PathBump;
                float2 grassTilt = CdBumpGrad(IN.positionWS.xz, _GrassBumpSize) * (_GrassBump * 0.5) * (1.0 - pathness * 0.85);
                float2 tilt = (pathTilt + grassTilt) * g;

                half3 albedo = baseCol * _Tint.rgb * _Bright;
                half4 spr = CdSprinkle(IN.positionWS.xz, _Sprinkles * gm.b, _SprinkleSize);
                albedo = lerp(albedo, CdSrgbToLin(spr.rgb) * _Tint.rgb * _Bright, spr.a * g);

                half3 n = normalize(IN.normalWS);
                n = normalize(n + half3(-tilt.x, 0.0, -tilt.y));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = dot(n, mainLight.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));

                // мягкие мультяшные ступени: плоская земля остаётся на своей яркости (flatW), наклон усиливается S-образной кривой
                half flatW = saturate((mainLight.direction.y + _Wrap) / (1.0 + _Wrap));
                half rel = (wrapped - flatW) * 2.0 + 0.5;
                half bandI = floor(rel);
                half banded = flatW + (bandI + smoothstep(0.2, 0.8, rel - bandI) - 0.5) * 0.5;
                wrapped = lerp(wrapped, saturate(banded), saturate(_Toon) * g);

                half atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half direct = wrapped * atten;

                half3 ambient = SampleSH(n) * _AmbientMul;

                // доля «в тени»: 0 на свету, 1 в полной тени. Тон теней красит только затенённые места
                half shade = saturate(1.0 - direct);
                half3 tint = lerp(half3(1.0, 1.0, 1.0), (half3)_IslandShadowTint.rgb, saturate(_IslandShadowTint.a) * shade);

                half3 col = albedo * (mainLight.color * direct + ambient + _Airy * g) * tint;

                // блик «мармелада»: только там, где нормаль наклонена (края тропы, холмики), на ровном месте блика нет
                half3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDir);
                half tiltAmt = saturate(length(tilt) * 3.0);
                half spec = pow(saturate(dot(n, halfDir)), _GlossPower) * gm.a * _Gloss * tiltAmt * g;
                col += mainLight.color * spec * mainLight.shadowAttenuation;

                // сахарные искры (только на свету)
                float camDist = distance(_WorldSpaceCameraPos, IN.positionWS);
                half sparkle = CdSparkle(IN.positionWS.xz, camDist) * _Sparkle * g;
                col += half3(1.0, 0.98, 0.92) * sparkle * saturate(direct * 1.5);

                // медленные тени облаков, чуть холодные
                float2 cloudUV = IN.positionWS.xz / max(_CloudSize, 1.0) + float2(0.021, 0.013) * (_Time.y * _CloudSpeed);
                half cloud = smoothstep(0.42, 0.78, CdNoise(cloudUV)) * _CloudShade * g;
                col *= half3(1.0 - cloud * 0.85, 1.0 - cloud * 0.75, 1.0 - cloud * 0.45);

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}

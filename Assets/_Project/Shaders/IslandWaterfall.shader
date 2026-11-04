// Assets/_Project/Shaders/IslandV2/IslandWaterfall.shader
// Остров v2: водопад с края острова. Отдельный полупрозрачный шейдер (река остаётся на Project/IslandV2/Water).
// Версия «одно полотно»: раньше было два слоя (передний и задний), которые смешивались по альфе и давали кашу, а верх был полупрозрачным
// на фоне непрозрачной реки: отсюда шов. Теперь:
//   * слой один, струи рисуются шумом, вытянутым вдоль падения;
//   * на первых метрах (_TopSolid) полотно НЕПРОЗРАЧНОЕ и цвет/пена считаются как у реки (мелководье + полосы пены): стык с River_Water незаметен;
//   * ниже полотно светлеет, края и низ растворяются в облаках.
// Меш делает IslandRiverWater.AddFall. Что читает из вершин:
//   UV.x   поперёк полотна: -1 левый край, +1 правый; UV.y = метры вдоль падения от порога
//   цвет R  непрозрачность по высоте: 1 вверху, 0 в самом низу (растворение в облаках)
//   цвет B  пена гребня: 1 на самом пороге, падает до 0 за 0,45 м
//   цвет A  не используется (слой один)
// Без ZWrite и теней: рисуется после земли и воды, один draw call.
Shader "Project/IslandV2/Waterfall"
{
    Properties
    {
        _ShallowColor ("Светлая вода", Color) = (0.55, 0.92, 0.95, 1)
        _MidColor ("Основной цвет струй", Color) = (0.30, 0.75, 0.93, 1)
        _FoamColor ("Пена", Color) = (0.96, 1.0, 1.0, 1)
        _FallSpeed ("Скорость струй", Range(0, 12)) = 4.0
        _Density ("Плотность воды (0 прозрачно, 1 плотно)", Range(0.3, 1)) = 0.9
        _LightMix ("Сколько света сцены", Range(0, 1)) = 0.35
        _TopSolid ("Метры у порога, где полотно непрозрачно (стык с рекой)", Range(0.3, 4)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _MidColor;
                half4 _FoamColor;
                half _FallSpeed;
                half _Density;
                half _LightMix;
                half _TopSolid;
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
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.normalWS = vn.normalWS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float u = IN.uv.x;
                float ax = abs(u);
                float y = IN.uv.y;                                   // метры от порога
                float fade = IN.color.r;
                float crest = saturate(IN.color.b);
                float topSolid = 1.0 - smoothstep(0.0, _TopSolid, y);   // 1 у порога (как река), 0 ниже
                float below = 1.0 - topSolid;

                // ---------------------------------------------------- струи: шум вытянут вдоль падения, две частоты, бегут вниз с разной скоростью
                float n1 = VNoise(float2(u * 5.5, y * 0.30 - t * _FallSpeed * 0.35));
                float n2 = VNoise(float2(u * 12.0 + 5.0, y * 0.55 - t * _FallSpeed * 0.60));
                float streak = saturate(smoothstep(0.58, 0.80, n1) * 0.85 + smoothstep(0.66, 0.88, n2) * 0.55) * below;

                // ---------------------------------------------------- цвет
                // у порога: мелководье реки; ниже: основной цвет струй; в самом низу: дымка
                half3 col = lerp(_ShallowColor.rgb, _MidColor.rgb, smoothstep(0.3, 2.6, y));
                col = lerp(col, lerp(_ShallowColor.rgb, _FoamColor.rgb, 0.40h), 0.35h * (half)below);   // полотно светлее реки
                col = lerp(col, _FoamColor.rgb, (half)streak * 0.85h);

                // пена у порога: та же формула полос, что у реки (IslandWater: thr * bands), поэтому на стыке рисунок продолжается
                float thr = smoothstep(0.5, 1.0, crest);
                float bands = 0.55 + 0.45 * sin(u * 7.0 + t * 2.0 + y * 0.6);
                col = lerp(col, _FoamColor.rgb, (half)saturate(thr * saturate(bands + thr * 0.5)));

                // белые боковые кромки (ниже порога; у самого порога берега реки и так с пеной)
                float rim = smoothstep(0.74, 0.96, ax) * below;
                col = lerp(col, _FoamColor.rgb, (half)rim * 0.8h);

                // внизу вода уходит в белую дымку
                half mist = 1.0h - smoothstep(0.05h, 0.65h, (half)fade);
                col = lerp(col, _FoamColor.rgb, mist * 0.9h);

                // ---------------------------------------------------- прозрачность
                // низ плавно в ноль, края плавно в ноль (рваная каёмка от шума, а не прямая линия). У порога всё это выключено: там непрозрачно, как река
                float edgeN = VNoise(float2(y * 0.8 - t * 1.2, u * 2.0)) * 0.12;
                float side = 1.0 - smoothstep(0.82, 1.0, ax + edgeN * below);
                float bottomN = VNoise(float2(u * 3.0, y * 0.4 - t * 0.9)) * 0.25;
                float bottom = smoothstep(0.0, 0.55, fade - bottomN * (1.0 - fade));
                float body = lerp(0.72, 1.0, saturate(streak + rim + crest));      // между струями чуть прозрачнее: полотно «живое»
                float alpha = side * bottom * body * _Density;
                alpha = saturate(lerp(alpha, 1.0, topSolid));

                // ---------------------------------------------------- свет: мягкий, без теней
                half3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half wrapped = saturate((dot(n, mainLight.direction) + 0.6h) / 1.6h);
                half3 lit = mainLight.color * wrapped + SampleSH(n);
                half litLum = dot(lit, half3(0.30h, 0.59h, 0.11h));
                col *= lerp(1.0h, saturate(litLum * 1.25h), _LightMix);
                col = MixFog(col, IN.fogFactor);
                return half4(col, (half)alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// Assets/_Project/Shaders/IslandV2/IslandWater.shader
// Остров v2, задача E: вода реки и водопад одним материалом. Рисует IslandRiverWater (меш River_Water).
// Версия «мультяшная» (под референс: яркая бирюзово-голубая вода, ступенчатая глубина, белые штрихи течения, блики, пена с волнистой кромкой).
// URP, без прозрачности и сортировки: непрозрачная вода плюс вырез (clip) там, где водопад растворяется в облаках.
// SRP Batcher: все свойства в UnityPerMaterial. Теней вода не бросает и не принимает, поэтому проходов ShadowCaster и DepthOnly нет.
//
// Что читает из вершин меша (его задаёт IslandRiverWater, договор тот же, что был раньше):
//   UV.x   поперёк потока: -1 левый берег, 0 ось, +1 правый (чуть больше 1 на кромке под откосом); UV.y = метры вдоль течения
//   цвет R  поверхность: доля глубины воды (0 мелко, 1 глубоко); водопад: непрозрачность (1 вверху, 0 в самом низу)
//   цвет G  режим: 0 = поверхность реки, 1 = водопад
//   цвет B  пена: у порога (поверхность: пенится при B > 0,5) и в гребне водопада
//   цвет A  «ущелье» (только у поверхности): 0 на броде, 1 у высоких стен. Чем выше, тем темнее вода у стен и медленнее течение
Shader "Project/IslandV2/Water"
{
    Properties
    {
        _ShallowColor ("Цвет мелководья", Color) = (0.55, 0.92, 0.95, 1)
        _MidColor ("Цвет средней глубины (ставит IslandRiverWater)", Color) = (0.30, 0.75, 0.93, 1)
        _DeepColor ("Цвет глубины", Color) = (0.12, 0.58, 0.92, 1)
        _FoamColor ("Цвет пены", Color) = (0.96, 1.0, 1.0, 1)
        _FlowSpeed ("Скорость течения реки", Range(0, 4)) = 1.2
        _FallSpeed ("Скорость водопада", Range(0, 12)) = 4.5
        _StripeScale ("Частота штрихов, 1/м", Range(0.1, 3)) = 0.8
        _HalfWidth ("Полуширина воды, м (ставит IslandRiverWater)", Float) = 5
        _FoamMeters ("Ширина пены у берега, м", Range(0.1, 2)) = 0.7
        _Gloom ("Затемнение у стен ущелья", Range(0, 1)) = 0.4
        _LightMix ("Сколько света сцены в цвете воды", Range(0, 1)) = 0.45
        _LineAmt ("Яркость белых штрихов течения", Range(0, 1)) = 0.8
        _SparkleAmt ("Яркость бликов", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

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
                half4 _DeepColor;
                half4 _FoamColor;
                half _FlowSpeed;
                half _FallSpeed;
                half _StripeScale;
                half _HalfWidth;
                half _FoamMeters;
                half _Gloom;
                half _LightMix;
                half _LineAmt;
                half _SparkleAmt;
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
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = vn.normalWS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            // ---------------------------------------------------------------- шум
            float Hash11(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // гладкий шум 0..1
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

            // ---------------------------------------------------------------- рисунок поверхности
            // Белые «штрихи течения»: короткие изогнутые чёрточки, которые бегут по течению. p = (метры поперёк, метры вдоль), t = время.
            float FlowDashes(float2 p, float speed, float scale)
            {
                float2 q = p;
                q.x += sin(q.y * 0.33 + _Time.y * 0.35) * 0.7;                         // русло слегка виляет
                float v = q.y * scale - _Time.y * speed * scale;
                v += sin(q.x * 1.25) * 0.30 + sin(q.x * 0.47 + q.y * 0.21) * 0.45;    // линии не прямые, а волнистые
                float cell = floor(v);
                float h = Hash11(cell + 3.7);
                float s = abs(frac(v) - 0.5) * 2.0;                                    // 0 в центре штриха
                float line_ = 1.0 - smoothstep(0.05, 0.22, s);
                float seg = abs(frac(q.x * 0.26 + h * 7.0) - 0.5);                     // штрих не на всю ширину, а отрезком
                float dash = 1.0 - smoothstep(0.17, 0.27, seg);
                return line_ * dash * step(0.28, h);                                   // часть штрихов пропускаем: нет «решётки»
            }

            // Блики: мелкие звёздочки-точки, мерцают
            float Sparkle(float2 p)
            {
                float2 g = p * 1.15;
                g.y -= _Time.y * 0.25;
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float h = Hash21(id);
                float on = step(0.90, h) * (0.5 + 0.5 * sin(_Time.y * 3.2 + h * 40.0));
                float2 o = float2(Hash21(id + 7.1), Hash21(id + 3.3)) - 0.5;
                float d = length(f - o * 0.55);
                return on * (1.0 - smoothstep(0.05, 0.14, d));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half isFall = step(0.5h, IN.color.g);
                float ax = abs(IN.uv.x);
                float2 pm = float2(IN.uv.x * _HalfWidth, IN.uv.y);   // метры: поперёк и вдоль течения

                // ============================================================ водопад: растворение внизу
                // R = непрозрачность. Край растворения рваный и гладкий (не «песок»): шум растянут вдоль струи
                float fade = IN.color.r;
                float rag = VNoise(float2(IN.uv.x * 3.2, IN.uv.y * 0.55 - _Time.y * 1.4)) * 0.6
                          + VNoise(float2(IN.uv.x * 7.0 + 4.0, IN.uv.y * 1.1 - _Time.y * 2.0)) * 0.4;
                float keep = lerp(1.0, fade - 0.42 * rag, isFall);
                clip(keep - 0.02);

                // ============================================================ цвет воды по глубине (три ступени, как в мультфильме)
                half profile = 1.0h - smoothstep(0.45h, 1.0h, (half)ax);
                half depth = saturate(profile * lerp(IN.color.r, 1.0h, isFall));
                half canyon = IN.color.a * (1.0h - isFall);
                half3 water = _ShallowColor.rgb;
                water = lerp(water, _MidColor.rgb, smoothstep(0.20h, 0.30h, depth));
                water = lerp(water, _DeepColor.rgb, smoothstep(0.58h, 0.68h, depth));
                // мягкая подкраска внутри ступени: цвет не плоский
                water *= (0.94h + 0.10h * (half)VNoise(pm * 0.45 + float2(0.0, -_Time.y * 0.2)));

                // у стен ущелья вода в тени
                half wallShade = canyon * _Gloom * smoothstep(0.30h, 1.0h, (half)ax);
                water *= (1.0h - wallShade * 0.55h);
                water = lerp(water, water * half3(0.82h, 0.92h, 1.08h), canyon * 0.4h);

                // ============================================================ узор
                half foam = 0.0h;
                half sparkle = 0.0h;
                if (isFall < 0.5h)
                {
                    // река: штрихи течения (в глубине медленнее) и блики
                    float speed = _FlowSpeed * lerp(1.0, 0.6, saturate(depth * canyon));
                    float d1 = FlowDashes(pm, speed, _StripeScale);
                    float d2 = FlowDashes(pm * float2(1.0, 1.0) + float2(3.1, 11.0), speed * 0.8, _StripeScale * 0.62);
                    foam = (half)saturate(max(d1, d2 * 0.7)) * _LineAmt * (0.35h + 0.65h * (1.0h - depth * 0.5h));
                    sparkle = (half)Sparkle(pm) * _SparkleAmt * (1.0h - canyon * 0.7h);

                    // пена у порога: только в последних метрах перед краем, полосами по течению
                    float thr = smoothstep(0.5, 1.0, IN.color.b);
                    float bands = 0.55 + 0.45 * sin(IN.uv.x * 7.0 + _Time.y * 2.0 + IN.uv.y * 0.6);
                    foam = max(foam, (half)(thr * saturate(bands + thr * 0.5)));
                }
                else
                {
                    // водопад: вертикальные струи двух слоёв (разные скорости), цвет светлее реки
                    float t = _Time.y;
                    float flowY = IN.uv.y;
                    float c1 = sin(IN.uv.x * 11.0 + sin(flowY * 0.45 - t * 0.6) * 1.2) * 0.5 + 0.5;
                    float c2 = sin(IN.uv.x * 19.0 + 2.0 + sin(flowY * 0.30 + t * 0.4) * 1.6) * 0.5 + 0.5;
                    float run1 = 0.5 + 0.5 * sin(flowY * 1.3 - t * _FallSpeed * 0.9 + c1 * 4.0);
                    float run2 = 0.5 + 0.5 * sin(flowY * 2.1 - t * _FallSpeed * 1.3 + c2 * 5.0);
                    float streak1 = smoothstep(0.62, 0.85, c1) * smoothstep(0.35, 0.9, run1);
                    float streak2 = smoothstep(0.70, 0.90, c2) * smoothstep(0.45, 0.95, run2);
                    water = lerp(water, lerp(_ShallowColor.rgb, _FoamColor.rgb, 0.42h), 0.75h);   // полотно светлее реки
                    foam = (half)saturate(streak1 * 0.75 + streak2 * 0.55);
                    // белые края полотна и белая кромка гребня
                    half sideFoam = (half)smoothstep(0.78, 0.98, ax);
                    foam = max(foam, sideFoam * 0.9h);
                    foam = max(foam, IN.color.b);
                }

                // ============================================================ кромка у берега: две волнистые линии пены
                half shoreStart = 1.0h - _FoamMeters / max(_HalfWidth, 0.5h);
                float wave = sin(IN.uv.y * 2.1 + _Time.y * 1.3) * 0.025 + sin(IN.uv.y * 5.3 - _Time.y * 0.9) * 0.012;
                half shore = (half)smoothstep(shoreStart + wave, shoreStart + wave + 0.06, (float)ax);
                half gap = (half)smoothstep(shoreStart - 0.075 + wave, shoreStart - 0.05 + wave, (float)ax)
                         * (1.0h - (half)smoothstep(shoreStart - 0.03 + wave, shoreStart + wave, (float)ax));
                half shoreFoam = saturate(shore + gap * 0.6h) * (1.0h - isFall);

                half foamAll = saturate(max(foam, shoreFoam));
                half3 albedo = lerp(water, _FoamColor.rgb, foamAll);
                albedo = lerp(albedo, _FoamColor.rgb, saturate(sparkle));

                // низ водопада: растворяется в белую дымку (мазки пены вместо серого)
                half mist = isFall * (1.0h - smoothstep(0.0h, 0.5h, (half)fade));
                albedo = lerp(albedo, _FoamColor.rgb, mist * 0.85h);

                // ============================================================ свет: мягкий, мультяшный (вода яркая и не чернеет в тени)
                half3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half ndl = dot(n, mainLight.direction);
                half wrapped = saturate((ndl + 0.5h) / 1.5h);
                half3 ambient = SampleSH(n);
                half3 lit = mainLight.color * wrapped * mainLight.distanceAttenuation + ambient;
                half litLum = dot(lit, half3(0.30h, 0.59h, 0.11h));
                half lightMul = lerp(1.0h, saturate(litLum * 1.25h), _LightMix);
                half3 col = albedo * lightMul;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

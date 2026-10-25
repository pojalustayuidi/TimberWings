// Шейдер скалы и обрыва парящего острова (URP). Цвет берётся из цвета вершин, свет мультяшный в три тона:
// тень, полутень, свет. Принимает тени, туман и свет SH. Тени отбрасывает через ShadowCaster от Lit.
// Нужен для FloatingIsland (обрыв, скала снизу, корни, кристаллы).
Shader "Project/IslandRock"
{
    Properties
    {
        _Tint ("Тон", Color) = (1,1,1,1)
        _Shade ("Цвет тени", Color) = (0.50,0.46,0.62,1)
        _Wrap ("Мягкость света", Range(0,1)) = 0.35
        _Rim ("Подсветка по краю", Range(0,1)) = 0.2
        _GlowIntensity ("Сила свечения кристаллов", Range(0,6)) = 2.5
        // Текстура земли (та же, что у Terrain) на травяной кромке обрыва и на поле. Вес травы берётся из UV0.x вершины, UV0.y = затемнение
        _GroundTex ("Текстура земли", 2D) = "white" {}
        _GroundMeters ("Метров на повтор текстуры", Float) = 4
        _GroundTint ("Тон земли", Color) = (1,1,1,1)
        _AllGround ("Вся поверхность = земля (поле)", Range(0,1)) = 0
        // 1 = земля считается тем же шейдерным шумом, что у Terrain (Project/CandyGround, CandyGround.hlsl): лайм/мята + ворсинки, без текстуры.
        // 0 = старый путь через _GroundTex (нужен, если Terrain остался на URP Lit)
        _UseCandyGround ("Земля как у CandyGround", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Shaders/Candy/CandyGround.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Shade;
                half _Wrap;
                half _Rim;
                half _GlowIntensity;
                half4 _GroundTint;
                float _GroundMeters;
                half _AllGround;
                half _UseCandyGround;
            CBUFFER_END

            TEXTURE2D(_GroundTex);
            SAMPLER(sampler_GroundTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light l = GetMainLight(sc);

                half ndl = dot(n, l.direction);
                half w = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half lit = smoothstep(0.30h, 0.55h, w) * l.shadowAttenuation;   // три тона: тень / полутень / свет

                half3 amb = max(SampleSH(n), 0.12h * _Shade.rgb);
                half3 vcolRock = i.color.rgb * _Tint.rgb;   // цвета вершин задаются как обычные sRGB-цвета
                #if !defined(UNITY_COLORSPACE_GAMMA)
                vcolRock = SRGBToLinear(vcolRock);
                #endif

                half gW = saturate(lerp(i.uv.x, 1.0h, _AllGround));
                half gS = lerp(i.uv.y, 1.0h, _AllGround);

                // скала: мультяшный свет в три тона
                half3 directRock = l.color * lit;
                half3 shadeRock = lerp(_Shade.rgb, half3(1, 1, 1), saturate(lit + 0.25h));

                half3 col;
                half3 vcol;
                if (_UseCandyGround > 0.5h)
                {
                    // земля (трава кромки и поле): цвет и свет из CandyGround.hlsl, как у Terrain, поэтому на стыке шва нет
                    half3 g = (half3)CGGrass(i.positionWS.xz, distance(_WorldSpaceCameraPos, i.positionWS), 0.0) * gS;
                    half3 colRock = vcolRock * (directRock + amb) * shadeRock;
                    half3 colGround = g * (half3)CGGroundShade(n, l, amb);
                    col = lerp(colRock, colGround, gW);
                    vcol = lerp(vcolRock, g, gW);
                }
                else
                {
                    // старый путь: текстура Terrain по мировым XZ. Земля: обычный ламберт, как у Terrain/Lit
                    half3 gTex = SAMPLE_TEXTURE2D(_GroundTex, sampler_GroundTex, i.positionWS.xz / max(0.1, _GroundMeters)).rgb * _GroundTint.rgb * gS;
                    vcol = lerp(vcolRock, gTex, gW);
                    half3 directGround = l.color * (saturate(ndl) * l.shadowAttenuation);
                    half3 direct = lerp(directRock, directGround, gW);
                    half3 shadeTint = lerp(shadeRock, half3(1, 1, 1), gW);
                    col = vcol * (direct + amb) * shadeTint;
                }

                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * _Rim;
                col += rim * l.color * 0.35h * lit * (1.0h - gW);

                // свечение: прозрачность вершины 1 = нет, 0 = полное (кристаллы)
                half glow = saturate(1.0h - i.color.a);
                col += vcol * glow * _GlowIntensity;

                half fogF = ComputeFogFactor(i.positionCS.z);
                col = MixFog(col, fogF);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

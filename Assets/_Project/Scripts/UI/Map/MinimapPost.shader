// Assets/_Project/Scripts/UI/MinimapPost.shader
// Мягкая обводка для снимка мини-карты. Снимок состоит из плоских цветов (трава, вода, лес, здания...),
// поэтому границу между слоями находим по разнице цвета у соседних пикселей и слегка затемняем её.
// Заодно получаются береговая линия, кромка леса и силуэты зданий с обводкой.
// Тон (Candy): в конце картинка слегка осветляется (_ToneGamma меньше 1) и смешивается с холодным небесным цветом (_ToneColor, _ToneAmount).
// По умолчанию тон выключен (Gamma 1, Amount 0): HudMinimap.ApplyPostTone включает его при candyStyle.
// Вызывается из HudMinimap через Graphics.Blit. Работает в любом пайплайне (URP тоже): это обычный «полноэкранный» проход.
//
// Земля: камера карты рисует только объекты на прозрачном фоне (чёрный, альфа 0), а землю (_GroundTex, её рисует
// HudMinimap.Ground.cs по данным Terrain) шейдер подкладывает под них. Объекты уже «премультиплицированы» фоном 0,
// поэтому края после MSAA ложатся на землю без голубой каймы: итог = земля * (1 - альфа) + цвет объектов.
//
// ВАЖНО: чтобы шейдер попал в сборку, положи его в Always Included Shaders (Project Settings > Graphics)
// или перетащи в поле Post Shader у HudMinimap. Без него карта рисуется без обводки (всё остальное работает).
Shader "Hidden/BuilderSim/MinimapPost"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.08, 0.12, 0.18, 1)
        _Width ("Width (px)", Float) = 1.2
        _Strength ("Strength", Range(0, 1)) = 0.5
        _Gain ("Gain", Float) = 4
        _GroundTex ("Ground", 2D) = "black" {}
        _UseGround ("Use Ground", Float) = 0
        _ToneColor ("Tone Color", Color) = (0.81, 0.91, 1, 1)
        _ToneAmount ("Tone Amount", Range(0, 0.5)) = 0
        _ToneGamma ("Tone Gamma (<1 lighter)", Range(0.6, 1.2)) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _Width;
            float _Strength;
            float _Gain;
            sampler2D _GroundTex;
            float _UseGround;
            fixed4 _ToneColor;
            float _ToneAmount;
            float _ToneGamma;

            // цвет пикселя с подложенной землёй (если земли нет, снимок как есть)
            fixed3 Comp(float2 uv)
            {
                fixed4 s = tex2D(_MainTex, uv);
                if (_UseGround > 0.5)
                {
                    fixed3 g = tex2D(_GroundTex, uv).rgb;
                    return g * (1.0 - s.a) + s.rgb;
                }
                return s.rgb;
            }

            // насколько пиксели снимка различаются (0 одинаковые). Считаем по сырому снимку: объекты и их альфа, без земли,
            // поэтому обводка идёт по кромке рек, леса, зданий, камней, но не по каждому пятну раскраски земли
            float Diff(fixed4 a, fixed4 b)
            {
                fixed4 d = abs(a - b);
                return d.r + d.g + d.b + d.a;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 px = _MainTex_TexelSize.xy;
                fixed4 s0 = tex2D(_MainTex, i.uv);
                fixed4 c = fixed4(Comp(i.uv), 1);

                // два кольца по 4 соседа: ближнее даёт линию, дальнее (шире и слабее) мягкий ореол
                float2 w1 = px * _Width;
                float2 w2 = px * _Width * 2.2;
                float e1 = 0;
                e1 = max(e1, Diff(s0, tex2D(_MainTex, i.uv + float2( w1.x, 0))));
                e1 = max(e1, Diff(s0, tex2D(_MainTex, i.uv + float2(-w1.x, 0))));
                e1 = max(e1, Diff(s0, tex2D(_MainTex, i.uv + float2(0,  w1.y))));
                e1 = max(e1, Diff(s0, tex2D(_MainTex, i.uv + float2(0, -w1.y))));
                float e2 = 0;
                e2 = max(e2, Diff(s0, tex2D(_MainTex, i.uv + float2( w2.x,  w2.y))));
                e2 = max(e2, Diff(s0, tex2D(_MainTex, i.uv + float2(-w2.x,  w2.y))));
                e2 = max(e2, Diff(s0, tex2D(_MainTex, i.uv + float2( w2.x, -w2.y))));
                e2 = max(e2, Diff(s0, tex2D(_MainTex, i.uv + float2(-w2.x, -w2.y))));

                float e = saturate(e1 * _Gain) * 0.75 + saturate(e2 * _Gain) * 0.25;
                c.rgb = lerp(c.rgb, c.rgb * _OutlineColor.rgb * 2.2, e * _Strength);
                // тон: светлее и холоднее (при Gamma 1 и Amount 0 ничего не меняется)
                c.rgb = pow(saturate(c.rgb), _ToneGamma);
                c.rgb = lerp(c.rgb, _ToneColor.rgb, _ToneAmount);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

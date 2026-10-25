// Assets/_Project/Shaders/Candy/CandyGround.hlsl
// Общая «земля»: цвет травы (лайм/мята по шуму + ворсинки) и мягкий свет земли.
// Подключают Project/CandyGround (Terrain) и Project/IslandRock (кромка обрыва и поле острова), поэтому шва на стыке нет:
// цвет считается из мировых XZ одной и той же функцией с одними и теми же параметрами.
//
// Параметры глобальные (Shader.SetGlobal*), их ставит CandyGroundParams. Если компонента нет (все нули), берутся запасные значения.
//   _CGLime, _CGMint   rgb = цвет (уже в цветовом пространстве проекта), a > 0.5 = «задан»
//   _CGNoise           x = масштаб пятен (1/м), y = сила мяты 0..1, z = смещение к мяте 0..1, w = полуширина границы пятен
//   _CGTuft            x = плотность ворсинок (1/м), y = сила, z = дистанция начала затухания (м), w = дистанция конца (м)
// Подключать ПОСЛЕ Core.hlsl и Lighting.hlsl (нужны SRGBToLinear, Light).
#ifndef CANDY_GROUND_INCLUDED
#define CANDY_GROUND_INCLUDED

float4 _CGLime;
float4 _CGMint;
float4 _CGNoise;
float4 _CGTuft;

float CGHash(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float CGVNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = CGHash(i);
    float b = CGHash(i + float2(1.0, 0.0));
    float c = CGHash(i + float2(0.0, 1.0));
    float d = CGHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float3 CGSrgb(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return SRGBToLinear(c);
#endif
}

// camD = расстояние до камеры (м), mintBias 0..1 = подталкивание к мяте (слой «влажная» / пятна из splatmap)
float3 CGGrass(float2 xz, float camD, float mintBias)
{
    float3 lime = _CGLime.a > 0.5 ? _CGLime.rgb : CGSrgb(float3(0.60, 0.85, 0.22));
    float3 mint = _CGMint.a > 0.5 ? _CGMint.rgb : CGSrgb(float3(0.42, 0.82, 0.55));
    float4 nz = _CGNoise.x > 0.0 ? _CGNoise : float4(0.06, 1.0, 0.0, 0.14);
    float4 tf = _CGTuft.x > 0.0 ? _CGTuft : float4(5.5, 0.22, 18.0, 45.0);

    // крупные пятна лайма и мяты: два октава value-noise
    float n = CGVNoise(xz * nz.x) * 0.62 + CGVNoise(xz * (nz.x * 2.9) + 11.7) * 0.38;
    float t = smoothstep(0.5 - nz.w, 0.5 + nz.w, n);
    t = saturate(t * nz.y + (nz.z + mintBias) * 0.5);
    float3 col = lerp(lime, mint, t);

    // ворсинки: мелкий шум, светлые кончики и тёмные просветы, затухают с расстоянием (экономия и нет мерцания вдали)
    float fade = 1.0 - smoothstep(tf.z, tf.w, camD);
    if (fade > 0.01 && tf.y > 0.001)
    {
        float2 p = xz * tf.x;
        float a = CGVNoise(p);
        float b = CGVNoise(p * 2.3 + 5.1);
        float tuft = smoothstep(0.62, 0.80, a);
        float gap = smoothstep(0.30, 0.12, b);
        col *= 1.0 + (tuft * tf.y - gap * tf.y * 0.6) * fade;
    }
    return col;
}

// Мягкий свет земли в два тона с холодным тинтом тени. Возвращает множитель на альбедо (прямой свет + ambient).
float3 CGGroundShade(float3 n, Light l, float3 amb)
{
    float x = (dot(n, l.direction) * 0.5 + 0.5) * lerp(1.0, l.shadowAttenuation, 0.85);
    float b = smoothstep(0.40, 0.62, x);
    float lvl = lerp(0.62, 1.0, b);
    float3 tint = lerp(float3(0.80, 0.88, 1.0), float3(1.0, 1.0, 1.0), b);
    return (l.color * lvl + amb) * tint;
}

#endif

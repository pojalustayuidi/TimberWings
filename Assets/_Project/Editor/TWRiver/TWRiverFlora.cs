// Assets/_Project/Editor/TWRiver/TWRiverFlora.cs
// Умеренные растения: камыш у воды на мягком берегу, кусты и тафты на верхней кромке, мох на камнях и крыше истока.
// Растения стоят на IslandField.Height, обходят брод, мост, тропы и край острова. Попадают в тот же меш и материал, что и камень (один draw call).
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverFlora
    {
        const int MaxReeds = 70, MaxBushes = 55;

        static void Reed(TWRiverCtx x, Vector3 baseP, int seed)
        {
            int blades = 5 + (int)(TWNoise.Hash(1, 1, seed) * 4f);
            for (int b = 0; b < blades; b++)
            {
                float ang = TWNoise.Hash(b, 2, seed) * Mathf.PI * 2f;
                float rad = 0.05f + 0.22f * TWNoise.Hash(b, 3, seed);
                float h = 0.9f + 0.8f * TWNoise.Hash(b, 4, seed);
                float lean = 0.12f + 0.25f * TWNoise.Hash(b, 5, seed);
                float la = TWNoise.Hash(b, 6, seed) * Mathf.PI * 2f;
                Vector3 bp = baseP + new Vector3(Mathf.Cos(ang) * rad, -0.05f, Mathf.Sin(ang) * rad);
                Vector3 tip = bp + new Vector3(Mathf.Cos(la) * lean, h, Mathf.Sin(la) * lean);
                float w = 0.045f;
                Vector3 p0 = bp + new Vector3(w, 0f, 0f), p1 = bp + new Vector3(-w * 0.5f, 0f, w * 0.87f), p2 = bp + new Vector3(-w * 0.5f, 0f, -w * 0.87f);
                Vector3 inside = bp + Vector3.up * (h * 0.3f);
                float g = TWNoise.Hash(b, 7, seed);
                Color col = x.pal.Green(g, 0.5f);
                x.rocks.TriAway(p0, p1, tip, col, inside);
                x.rocks.TriAway(p1, p2, tip, col, inside);
                x.rocks.TriAway(p2, p0, tip, col, inside);
            }
        }

        static void Bush(TWRiverCtx x, Vector3 p, float r, float flat, int seed)
        {
            TWStone.ColorFn green = (wy, n, h) => x.pal.Green(h, Mathf.Clamp01(n.y));
            TWStone.Build(x.rocks, p + Vector3.up * (r * flat * 0.45f), new Vector3(r, r * flat, r * 1.1f), TWNoise.Hash(seed, 1, x.seed) * 3f, seed, green, 0.3f);
        }

        static bool Allowed(TWRiverCtx x, Vector3 p)
        {
            if (!x.f.Inside(p.x, p.z)) return false;
            if (x.f.EdgeDistance(p.x, p.z) < 1.8f) return false;
            Vector2 q = new Vector2(p.x, p.z);
            if (x.ExclusionWeight(q, 0.5f) > 0.01f) return false;
            if (x.f.PathWeight(p.x, p.z) > 0.12f) return false;
            return true;
        }

        public static void Build(TWRiverCtx x)
        {
            var f = x.f;
            float len = x.ax.Length;
            int reeds = 0, bushes = 0;
            float s = (x.hasSource ? TWRiverSpec.MouthDist + 2f : 2f);
            int idx = 0;
            while (s < len - 3f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    bool rock = side == x.rockSide;
                    float cover = TWRiverBanks.Cover(x, s, side);
                    float h1 = TWNoise.Hash(idx, side, x.seed + 501);
                    // камыш у воды: мягкий берег и тихие места
                    if (reeds < MaxReeds && cover < 0.45f && h1 < (rock ? 0.30f : 0.60f))
                    {
                        float off = x.half + 0.15f + 0.9f * TWNoise.Hash(idx, side, x.seed + 502);
                        Vector3 p = x.OnGround(s + (TWNoise.Hash(idx, side, x.seed + 503) - 0.5f) * 1.2f, side * off, 0f);
                        if (Allowed(x, p)) { Reed(x, p, x.seed + 600 + idx * 3 + side); reeds++; }
                    }
                    // кусты и тафты у верхней кромки берега
                    float h2 = TWNoise.Hash(idx, side, x.seed + 511);
                    if (bushes < MaxBushes && h2 < (rock ? 0.22f : 0.5f))
                    {
                        float off = x.half + 3.6f + 3.0f * TWNoise.Hash(idx, side, x.seed + 512);
                        Vector3 p = x.OnGround(s, side * off, 0f);
                        if (Allowed(x, p))
                        {
                            float r = 0.45f + 0.55f * TWNoise.Hash(idx, side, x.seed + 513);
                            bool tuft = TWNoise.Hash(idx, side, x.seed + 514) > 0.5f;
                            Bush(x, p, r, tuft ? 0.45f : 0.75f, x.seed + 700 + idx * 5 + side);
                            bushes++;
                        }
                    }
                }
                s += 2.4f + 1.6f * TWNoise.Hash(idx, 0, x.seed + 515);
                idx++;
            }

            // вокруг истока: плотнее и выше, чтобы массив «вырастал» из травы
            if (x.hasSource)
            {
                float lm = TWRiverSpec.MouthDist;
                for (int i = 0; i < 10; i++)
                {
                    int side = (i % 2 == 0) ? 1 : -1;
                    float ss = Mathf.Lerp(-0.5f, lm + 2.5f, TWNoise.Hash(i, 1, x.seed + 520));
                    float off = side * (x.half + 6.6f + 1.4f * TWNoise.Hash(i, 2, x.seed + 521));
                    Vector3 p = x.OnGround(ss, off, 0f);
                    if (!Allowed(x, p)) continue;
                    Bush(x, p, 0.55f + 0.6f * TWNoise.Hash(i, 3, x.seed + 522), 0.6f, x.seed + 740 + i);
                }
            }

            // мох на камнях и на крыше истока: плоские низкие куполки
            TWStone.ColorFn moss = (wy, n, h) => x.pal.Green(0.2f + 0.5f * h, 0.3f);
            for (int i = 0; i < x.mossSpots.Count; i++)
            {
                Vector4 m = x.mossSpots[i];
                TWStone.Build(x.rocks, new Vector3(m.x, m.y - 0.06f, m.z), new Vector3(m.w, 0.16f, m.w * 0.9f), TWNoise.Hash(i, 2, x.seed + 530) * 3f, x.seed + 800 + i, moss, 0.1f);
            }
        }
    }
}

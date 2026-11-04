// Assets/_Project/Editor/TWRiver/TWRiverFoam.cs
// Пена и блики-следы. ВСЕ полосы строятся по оси реки (ax.At / ax.Side), поэтому всегда идут по течению и огибают камни.
// Один меш и один материал (Project/IslandV2/TW_RiverFoam, без записи глубины). Высота = поверхность воды + 7 см.
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverFoam
    {
        const float Up = 0.07f;

        static Vector3 FP(TWRiverCtx x, float s, float off)
        {
            return x.P(s, off, x.Yw(s) + Up);
        }

        // полоса вдоль течения: от (s0, off0) до (s1, off1), края и концы растворяются
        static void Streak(TWRiverCtx x, float s0, float s1, float off0, float off1, float hw0, float hw1, float a0, float a1, int steps, float wiggle, int seed)
        {
            float len = x.ax.Length;
            if (s1 > len - 0.25f) s1 = len - 0.25f;
            if (s1 - s0 < 0.6f) return;
            var p = new List<Vector3>(); var hw = new List<float>(); var a = new List<float>();
            for (int j = 0; j <= steps; j++)
            {
                float t = (float)j / steps;
                float s = Mathf.Lerp(s0, s1, t);
                float off = Mathf.Lerp(off0, off1, t) + Mathf.Sin(t * 6.1f + TWNoise.Hash(seed, 1, x.seed) * 6f) * wiggle;
                p.Add(FP(x, s, off));
                hw.Add(Mathf.Lerp(hw0, hw1, t));
                a.Add(Mathf.Lerp(a0, a1, t) * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), 0.6f));
            }
            x.foam.Strip(p, hw, a);
        }

        static void Wake(TWRiverCtx x, Vector4 st, int seed)
        {
            float s = st.x, off = st.y, r = st.w;
            float lenW = 2.2f + 2.4f * r;
            float s0 = s + r * 0.6f;
            for (int arm = -1; arm <= 1; arm += 2)
                Streak(x, s0, s0 + lenW, off + arm * r * 0.8f, off + arm * (r * 0.8f + lenW * 0.22f), 0.10f, 0.20f, 0.75f, 0.2f, 5, 0.04f, seed + arm);
            Streak(x, s0, s0 + lenW * 0.75f, off, off + (TWNoise.Hash(seed, 2, x.seed) - 0.5f) * 0.3f, 0.12f, 0.30f, 0.55f, 0.1f, 4, 0.05f, seed + 5);
            Vector2 tg = x.ax.Tangent(Mathf.Clamp(s, 0f, x.ax.Length));
            x.foam.Ring(FP(x, s, off), new Vector3(tg.x, 0f, tg.y), r * 1.05f, r * 0.95f, 0.30f + 0.10f * r, 0.85f);
        }

        public static void Build(TWRiverCtx x)
        {
            float len = x.ax.Length;
            float scale = x.half / 5f;

            // следы за каждым камнем в воде
            for (int i = 0; i < x.stones.Count; i++) Wake(x, x.stones[i], x.seed + 31 * i);

            // устье грота: веер струй из тёмного проёма вдоль течения
            if (x.hasSource)
            {
                float lm = TWRiverSpec.MouthDist;
                float[] offs = { -2.5f, -1.2f, 0f, 1.3f, 2.6f };
                for (int i = 0; i < offs.Length; i++)
                {
                    float sA = lm - 0.4f + 0.3f * i;
                    float sB = sA + 4.5f + 2.0f * TWNoise.Hash(i, 3, x.seed + 4);
                    Streak(x, sA, sB, offs[i] * 0.8f, offs[i] * 1.15f, 0.13f, 0.22f, 0.65f, 0.15f, 6, 0.06f, 100 + i);
                }
                // пена по линии воды у столбов проёма
                for (int sd = -1; sd <= 1; sd += 2)
                    Streak(x, lm - 2.0f, lm + 3.2f, sd * (TWRiverSpec.MouthHalf + 0.15f), sd * (TWRiverSpec.MouthHalf + 0.9f), 0.12f, 0.18f, 0.6f, 0.2f, 6, 0.03f, 120 + sd);
            }

            // перед порогом вода ускоряется: полосы сходятся к струям водопада и густеют к кромке
            float[] lanes = { -3.4f, -1.7f, 0.2f, 1.9f, 3.5f };
            for (int i = 0; i < lanes.Length; i++)
            {
                float sB = len - 0.3f;
                float sA = sB - (5f + 5f * TWNoise.Hash(i, 7, x.seed + 8));
                float o = lanes[i] * scale;
                Streak(x, sA, sB, o * 1.05f, o * 0.93f, 0.12f, 0.26f, 0.15f, 0.75f, 8, 0.05f, 140 + i);
            }

            // по воде вдоль скалистого берега: тонкие следы на линии воды
            int rs = x.rockSide;
            float s = (x.hasSource ? TWRiverSpec.MouthDist + 4f : 3f);
            int idx = 0;
            while (s < len - 6f)
            {
                float cover = TWRiverBanks.Cover(x, s, rs);
                if (cover > 0.55f)
                {
                    float l = 3f + 3f * TWNoise.Hash(idx, 11, x.seed + 9);
                    Streak(x, s, s + l, rs * (x.half - 0.35f), rs * (x.half - 0.55f), 0.09f, 0.14f, 0.45f, 0.1f, 5, 0.03f, 160 + idx);
                }
                s += 4.5f + 3f * TWNoise.Hash(idx, 12, x.seed + 9);
                idx++;
            }
        }
    }
}

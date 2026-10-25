#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/CandyCampfire.Hearth.cs (Editor)
// Часть partial-класса CandyCampfire: «центр v2», читаемость костра и сидения вокруг него.
// Файл НОВЫЙ, лежит рядом с CandyCampfire.cs (тот стал partial). Ничего не создаёт сам: методы зовёт CandyCampfire.cs при сборке.
//
// Что здесь:
//   HearthGround   тёмная копчёная чаша (3 диска) под камнями: на тёмном фоне и холодные поленья, и пламя видны сверху
//   FlameMesh      крупное пламя: угольная подушка с жаром, большой язык, 6 боковых, жёлтая и белая сердцевина, 2 розовых огонька
//   PotLift        на сколько поднят котелок и макушка треноги (раньше котелок висел прямо над огнём и закрывал его с камеры)
//   PlankBench     лавка-доска на двух пнях (30 и 210 градусов); лавки-бревна (120 и 300) остались в CandyCampfire.BenchLog
//   StumpSeats     4 пня-сиденья между лавками (165 и 345 с 1-го этапа, 75 и 255 со 2-го); на 165 градусах пень с топором у поленницы
//   StumpCols      коллайдеры пней (чтобы герой не ходил сквозь них)
using UnityEngine;

namespace IslandV2
{
    public static partial class CandyCampfire
    {
        // ---------------------------------------------------------------- числа и цвета
        const float PotLift = 0.55f;
        const float StumpR = 2.7f;
        static readonly float[] StumpDeg = { 75f, 165f, 255f, 345f };
        static readonly int[] StumpStage = { 2, 1, 2, 1 };

        static readonly Color HearthCol = SoftMesh.C(0x5A4252), HearthDeep = SoftMesh.C(0x483442), CoalCol = SoftMesh.C(0x33242E),
            LogMid = SoftMesh.C(0xA86C44), LogDark = SoftMesh.C(0x7A4A32), SteelCol = SoftMesh.C(0xB9AEC9);

        // ---------------------------------------------------------------- чаша костра
        static void DiscFan(SoftMesh s, float r, float y, Color col, int n)
        {
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                pts[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            s.Fan(pts, col, Vector3.up, false);
        }

        // копоть: широкий диск под камнями (закрывает зазор между камнями и плитами), ближе к огню темнее
        static void HearthGround(SoftMesh s)
        {
            DiscFan(s, 1.42f, 0.040f, HearthCol, 32);
            DiscFan(s, 1.14f, 0.048f, HearthDeep, 28);
            DiscFan(s, 0.88f, 0.056f, CoalCol, 24);
        }

        // ---------------------------------------------------------------- пламя
        // Сетка в локальных координатах объекта «Flames» (он стоит на 0,22 над землёй костра и масштабируется мерцанием).
        static void FlameMesh(Ctx c, SoftMesh f)
        {
            Color oA = SoftMesh.C(0xFF4A18), oB = SoftMesh.C(0xFF8A2E), yA = SoftMesh.C(0xFFB52E), yB = SoftMesh.C(0xFFDD6A),
                  kA = SoftMesh.C(0xFFE08A), kB = SoftMesh.C(0xFFF6C8), pA = SoftMesh.C(0xFF8FB1), pB = SoftMesh.C(0xFFC4D6);

            // угли: красная подушка и яркие жаринки (по ним огонь читается даже без языков)
            f.Puff(new Vector3(0f, -0.15f, 0f), new Vector3(0.80f, 0.05f, 0.80f), SoftMesh.C(0xE8502D), 0f, 12, 3, 0.1f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f + c.Jf(0.25f), r = c.Rf(0.28f, 0.68f);
                f.Puff(new Vector3(Mathf.Cos(a) * r, -0.11f, Mathf.Sin(a) * r), new Vector3(0.10f, 0.045f, 0.10f),
                       i % 2 == 0 ? SoftMesh.C(0xFFB04A) : SoftMesh.C(0xFF8A3D), 0f, 6, 3, 0.05f);
            }

            // большой язык по центру
            f.Teardrop(Vector3.zero, 1.28f, 0.42f, new Vector3(c.Jf(0.05f), 0f, c.Jf(0.05f)), oA, oB, 9, 8);
            // шесть боковых языков вокруг
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + c.Jf(0.25f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                f.Teardrop(dir * 0.27f + new Vector3(0f, 0.02f, 0f), c.Rf(0.62f, 0.92f), 0.15f, dir * 0.16f, oA, oB, 7, 5);
            }
            // жёлтая и белая сердцевина
            f.Teardrop(new Vector3(0f, 0.03f, 0f), 0.95f, 0.30f, new Vector3(c.Jf(0.04f), 0f, c.Jf(0.04f)), yA, yB, 8, 7);
            f.Teardrop(new Vector3(0f, 0.04f, 0f), 0.58f, 0.18f, Vector3.zero, kA, kB, 7, 5);
            // два розовых огонька (конфетная нотка)
            for (int i = 0; i < 2; i++)
            {
                float a = 0.8f + i * Mathf.PI + c.Jf(0.4f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                f.Teardrop(dir * 0.30f, c.Rf(0.30f, 0.42f), 0.06f, dir * 0.08f, pA, pB, 6, 4);
            }
        }

        // ---------------------------------------------------------------- лавка-доска на пнях
        static void PlankBench(SoftMesh s, float a, int i)
        {
            Vector3 p = BenchPos(a);
            Quaternion q = Quaternion.Euler(0f, TanYaw(a), 0f);   // локальный X вдоль окружности, Z к центру
            Vector3 tan = q * Vector3.right;
            Color top = i == 0 ? WoodL : SoftMesh.Shade(WoodL, 0.93f);
            // две доски сиденья с щелью
            for (int k = 0; k < 2; k++)
            {
                Vector3 off = q * new Vector3(0f, 0f, (k - 0.5f) * 0.235f);
                s.Box(p + off + Vector3.up * 0.30f, new Vector3(1.55f, 0.08f, 0.22f), q, k == 0 ? top : SoftMesh.Shade(top, 0.95f), 1.1f);
            }
            // две ножки-пенька
            for (int k = 0; k < 2; k++)
            {
                Vector3 b = p + tan * (k == 0 ? -0.55f : 0.55f);
                s.Cyl(b, b + Vector3.up * 0.27f, 0.15f, 0.12f, LogMid, WoodCap, true, true, 8);
            }
        }

        // ---------------------------------------------------------------- пни-сиденья
        static float StumpAngle(Ctx c, int i) { return FreeAngle(c, StumpR, StumpDeg[i], 1.4f); }

        static Vector3 StumpPos(float a) { return new Vector3(Mathf.Cos(a) * StumpR, Gy(StumpR) - 0.03f, Mathf.Sin(a) * StumpR); }

        static void StumpSeats(Ctx c, SoftMesh s, int stage)
        {
            for (int i = 0; i < StumpDeg.Length; i++)
            {
                if (StumpStage[i] != stage) continue;
                float a = StumpAngle(c, i);
                Vector3 p = StumpPos(a);
                const float h = 0.36f, r = 0.27f;
                // корни-лапки
                for (int k = 0; k < 3; k++)
                {
                    float ra = a * 2f + k * Mathf.PI * 2f / 3f;
                    s.Puff(p + new Vector3(Mathf.Cos(ra) * r * 0.95f, 0.05f, Mathf.Sin(ra) * r * 0.95f), new Vector3(0.09f, 0.06f, 0.09f), LogDark, 0f, 6, 3, 0.15f);
                }
                // тело и срез с годовыми кольцами
                s.Cyl(p, p + Vector3.up * h, r * 1.1f, r, i % 2 == 0 ? LogMid : LogDark, WoodCap, true, true, 10);
                s.Puff(p + Vector3.up * (h + 0.004f), new Vector3(r * 0.62f, 0.012f, r * 0.62f), WoodL, 0f, 9, 3, 0.05f);
                s.Puff(p + Vector3.up * (h + 0.009f), new Vector3(r * 0.22f, 0.012f, r * 0.22f), WoodD, 0f, 7, 3, 0.05f);

                if (i == 1) Axe(s, p + Vector3.up * h, a);
                else if (stage == 2) s.Puff(p + Vector3.up * (h + 0.045f), new Vector3(0.21f, 0.05f, 0.21f), Pastels[(i + 1) % Pastels.Length], 0f, 8, 4, 0.22f);   // мягкая подушка на пне
            }
        }

        // топор воткнут в колоду: рукоять наклонена наружу от костра
        static void Axe(SoftMesh s, Vector3 top, float a)
        {
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 d = (radial * 0.45f + Vector3.up).normalized;
            Vector3 head = top + Vector3.up * 0.06f;
            s.Cyl(head, head + d * 0.62f, 0.028f, 0.022f, WoodL, WoodCap, false, true, 6);
            s.Box(head + d * 0.03f, new Vector3(0.05f, 0.16f, 0.24f), Quaternion.FromToRotation(Vector3.up, d), SteelCol, 1.1f);
            // щепки у колоды
            for (int k = 0; k < 3; k++)
            {
                float ca = a + 1.4f + k * 0.9f;
                s.Puff(new Vector3(top.x + Mathf.Cos(ca) * 0.42f, top.y - 0.33f, top.z + Mathf.Sin(ca) * 0.42f), new Vector3(0.06f, 0.015f, 0.03f), WoodCap, ca * Mathf.Rad2Deg, 5, 3, 0.05f);
            }
        }

        static void StumpCols(Ctx c, Transform stage, int st)
        {
            for (int i = 0; i < StumpDeg.Length; i++)
            {
                if (StumpStage[i] != st) continue;
                Vector3 p = StumpPos(StumpAngle(c, i));
                AddCol(c, stage, new Vector3(p.x, 0.2f, p.z), new Vector3(0.6f, 0.4f, 0.6f), 0f);
            }
        }
    }
}
#endif

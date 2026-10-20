#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Builder/LocationGen.ForestTrails.cs
// Раскладка троп леса на MakeTrail / trailMeander из LocationGen.Core.cs.
// Работает в МЕТРАХ: временные Settings с worldScale = 1 (1 u = 1 м), настройки острова и сцену не трогает.
// Ничего не создаёт в сцене: возвращает ForestTrailPlan (его собирает ForestTrails.cs, часть 2).
//
// Раскладка:
//   Main         арка (ForestGate) -> поляна лесопилки. Широкая у арки, сужается в коридор, воронка у поляны.
//   Branch_Birch ответвление в сторону берёзовой подзоны, открытый конец (тропа тает среди деревьев)
//   Branch_Find  ответвление в сторону осенней подзоны, ТУПИКОВАЯ ПОЛЯНА (место для находки / NPC)
//   Branch_Spur  короткий отросток (только если forestTrailBranches = 3 и лес не тесный)
// Лесопилка и поляна находки берутся из объектов сцены ForestSawmill / ForestFindSpot, если они есть (их можно двигать руками).
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class LocationBuilder
{
    internal static ForestTrailPlan PlanForestTrails(ForestSettings fs, ForestTrailInput inp)
    {
        var cs = new Settings();
        cs.worldScale = 1f;
        cs.seed = unchecked(fs.seed * 31 + fs.forestTrailVariant * 101 + 7);
        cs.trailMeander = fs.forestTrailMeander;
        cs.trailWidth = fs.forestTrailBranchWidth;
        cs.trailWidthAtPad = fs.forestTrailBranchWidth;
        var g = new Gen(cs);
        return g.PlanForest(fs, inp);
    }

    sealed partial class Gen
    {
        static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        Pad FtPad(Vector2 pos, float rad, float entryDist, Vector2 face, bool round)
        {
            var p = new Pad { round = round };
            p.pos = pos; p.rad = rad; p.hw = rad; p.hd = entryDist;
            p.Orient(face);
            return p;
        }

        // ближайшая точка ломаной; возвращает расстояние
        static float FtPolyDist(List<Vector2> poly, Vector2 p, out Vector2 nearest, out int seg)
        {
            float best = float.MaxValue; nearest = poly[0]; seg = 0;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                Vector2 a = poly[i], ab = poly[i + 1] - a;
                float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                Vector2 q = a + ab * t;
                float d = (p - q).magnitude;
                if (d < best) { best = d; nearest = q; seg = i; }
            }
            return best;
        }

        static Vector2 FtPush(Vector2 p, Vector2 from, float minDist)
        {
            Vector2 v = p - from;
            float m = v.magnitude;
            if (m >= minDist) return p;
            return from + (m < 1e-3f ? Vector2.up : v / m) * minDist;
        }

        internal ForestTrailPlan PlanForest(ForestSettings fs, ForestTrailInput inp)
        {
            var plan = new ForestTrailPlan();
            Vector2 C = inp.center, G = inp.gate;
            float R = Mathf.Max(6f, inp.radius);
            // маленькая зона: ширины и поляны ужимаются, чтобы тропы не слипались (R >= 20 м = полный размер)
            float wk = Mathf.Clamp(R / 20f, 0.55f, 1f);
            plan.center = C; plan.radius = R; plan.gate = G;

            Func<Vector2, float> depth = inp.depth != null ? inp.depth : (Func<Vector2, float>)(p => R - (p - C).magnitude);

            Vector2 InsideZone(Vector2 p, float margin)
            {
                for (int i = 0; i < 30 && depth(p) < margin; i++) p = Vector2.Lerp(p, C, 0.1f);
                return p;
            }

            Vector2 d = C - G;
            float dist0 = d.magnitude;
            d = dist0 < 1e-3f ? Vector2.up : d / dist0;          // от арки вглубь
            Vector2 left = new Vector2(-d.y, d.x);
            float side = rng.NextDouble() < 0.5 ? 1f : -1f;

            // ---------------------------------------------------- лесопилка
            float sawR; Vector2 S;
            if (inp.sawmillFixed)
            {
                S = inp.sawmill; sawR = Mathf.Max(1.5f, inp.sawmillRadius);
                if (depth(S) < sawR * 0.5f) plan.warnings.Add("Лесопилка (объект ForestSawmill) стоит у самого края зоны леса.");
            }
            else
            {
                sawR = Mathf.Clamp(0.30f * R, 4f * wk, Mathf.Max(4f * wk, fs.sawmillRadius));
                S = InsideZone(G + d * (dist0 * 0.88f), sawR + 1.5f);
            }
            plan.sawmill = S; plan.sawmillRadius = sawR; plan.sawmillExisting = inp.sawmillFixed;
            if ((S - G).magnitude < sawR + 8f)
                plan.warnings.Add("Лесопилка слишком близко к арке: главная тропа короткая. Подвинь объект ForestSawmill глубже в лес.");

            // ---------------------------------------------------- главная тропа
            var padS = FtPad(S, sawR, Mathf.Max(1.0f, sawR * 0.75f), G - S, true);
            pads.Add(padS);
            float Lsg = (S - G).magnitude;
            Vector2? via = null;
            if (Lsg > 14f) via = InsideZone(Vector2.Lerp(G, S, 0.5f) + left * side * Mathf.Min(0.16f * Lsg, 6f), 3f);
            Trail tm = MakeTrail(padS, G, d, false, "Main", via);

            // зона леса не должна кончаться посреди тропы: изгибы не выпускаем за край
            float run = 0f;
            for (int i = 1; i < tm.p.Count; i++)
            {
                run += (tm.p[i] - tm.p[i - 1]).magnitude;
                if (run > 4f) tm.p[i] = InsideZone(tm.p[i], 1.4f);
            }
            plan.paths.Add(FtToPath(tm, "Main", true, true, S, sawR, fs, wk));

            if (plan.paths[0].len < 6f || tm.p.Count < 7)
            {
                plan.warnings.Add("Главная тропа слишком короткая для ответвлений: увеличь зону леса или отодвинь лесопилку.");
                return plan;
            }
            float usable = Mathf.Max(6f, plan.paths[0].len - sawR);   // длина главной тропы до поляны лесопилки

            Vector2 ForkAt(float distAlong, out Vector2 tan)
            {
                float acc = 0f; int fi = 2;
                for (int i = 1; i < tm.p.Count - 1; i++)
                {
                    acc += (tm.p[i] - tm.p[i - 1]).magnitude;
                    fi = i;
                    if (acc >= distAlong) break;
                }
                fi = Mathf.Clamp(fi, 2, Mathf.Max(2, tm.p.Count - 3));
                tan = (tm.p[fi + 1] - tm.p[fi - 1]).normalized;
                return tm.p[fi];
            }

            // уводит точку от главной тропы, лесопилки, развилки и других концов; держит внутри леса
            Vector2 Settle(Vector2 T, Vector2 F, float minLen, float minMain, float minSaw, List<Vector2> others, float minOther, float margin)
            {
                for (int it = 0; it < 4; it++)
                {
                    T = FtPush(T, F, minLen);
                    T = FtPush(T, S, minSaw);
                    Vector2 nn; int sg;
                    float dm = FtPolyDist(tm.p, T, out nn, out sg);
                    if (dm < minMain) T = nn + (T - nn).normalized * minMain;
                    for (int o = 0; o < others.Count; o++) T = FtPush(T, others[o], minOther);
                    T = InsideZone(T, margin);
                }
                return T;
            }

            var ends = new List<Vector2>();

            // ---------------------------------------------------- Branch_Birch: открытый конец
            Vector2 tan1; Vector2 F1 = ForkAt(usable * 0.50f, out tan1);
            Vector2 T1 = F1 + (inp.birch - F1) * 0.65f;
            T1 = Settle(T1, F1, 5f * wk, 3.5f * wk, sawR + 2f, ends, 6f, 2.5f);
            ends.Add(T1);

            // ---------------------------------------------------- Branch_Find: тупиковая поляна
            float gR = Mathf.Max(1.2f, fs.forestFindGladeRadius * wk);
            Vector2 tan2; Vector2 F2 = ForkAt(usable * 0.82f, out tan2);
            Vector2 T2;
            if (inp.findFixed)
            {
                T2 = inp.findSpot;
                Vector2 nn; int sg;
                FtPolyDist(tm.p, T2, out nn, out sg);
                int fi2 = Mathf.Clamp(sg, 2, Mathf.Max(2, tm.p.Count - 3));
                if ((tm.p[fi2] - S).magnitude > sawR + 1f)
                {
                    F2 = tm.p[fi2];
                    tan2 = (tm.p[fi2 + 1] - tm.p[fi2 - 1]).normalized;
                }
                plan.findExisting = true;
            }
            else
            {
                T2 = F2 + (inp.autumn - F2) * 0.7f;
                T2 = Settle(T2, F2, 7f * wk, gR + 2f, sawR + gR + 2.5f, ends, gR + 6f, gR + 1.5f);
            }
            ends.Add(T2);
            {
                Vector2 nn; int sg;
                if (FtPolyDist(tm.p, T2, out nn, out sg) < gR + 1.5f)
                    plan.warnings.Add("Тупиковая поляна почти упирается в главную тропу: зона леса тесная, подвинь ForestFindSpot вручную и пересобери тропы.");
            }
            plan.findSpot = T2; plan.findRadius = gR; plan.hasFind = true;

            var padB = FtPad(T1, 1.0f, 0.6f, F1 - T1, false);
            pads.Add(padB);
            Trail tb = MakeTrail(padB, F1, tan1, true, "Branch_Birch");
            plan.paths.Add(FtToPath(tb, "Branch_Birch", false, false, T1, 0f, fs, wk));

            var padF = FtPad(T2, gR, Mathf.Max(0.8f, gR * 0.55f), F2 - T2, true);
            pads.Add(padF);
            Trail tf = MakeTrail(padF, F2, tan2, true, "Branch_Find");
            plan.paths.Add(FtToPath(tf, "Branch_Find", false, true, T2, gR, fs, wk));

            // ---------------------------------------------------- Branch_Spur: короткий отросток
            if (fs.forestTrailBranches >= 3)
            {
                float sideB = Mathf.Sign(Cross2(d, inp.birch - C));
                if (sideB == 0f) sideB = 1f;
                bool ok3 = false;
                for (int attempt = 0; attempt < 2 && !ok3 && R >= 16f; attempt++)
                {
                    Vector2 tan3; Vector2 F3 = ForkAt(usable * (attempt == 0 ? 0.22f : 0.30f), out tan3);
                    Vector2 lat = left * (attempt == 0 ? sideB : -sideB);
                    Vector2 T3 = F3 + lat * (0.36f * R) + d * (0.08f * R);
                    T3 = Settle(T3, F3, 6f * wk, 3.5f * wk, sawR + 2f, ends, 6f, 2.5f);
                    if (Vector2.Distance(T3, T1) < 5.5f || Vector2.Distance(T3, T2) < 5.5f || Vector2.Distance(F3, F1) < 3.5f) continue;
                    var padP = FtPad(T3, 1.0f, 0.6f, F3 - T3, false);
                    pads.Add(padP);
                    Trail tp = MakeTrail(padP, F3, tan3, true, "Branch_Spur");
                    plan.paths.Add(FtToPath(tp, "Branch_Spur", false, false, T3, 0f, fs, wk));
                    ends.Add(T3);
                    ok3 = true;
                }
                if (!ok3 && R >= 16f) plan.warnings.Add("Третий отросток не поместился (зона тесная): остались Branch_Birch и Branch_Find.");
            }
            return plan;
        }

        // Trail из MakeTrail -> путь плана с нашими ширинами (вместо авто-расширения у площадки)
        ForestTrailPath FtToPath(Trail t, string name, bool main, bool glade, Vector2 gladeC, float gladeR, ForestSettings fs, float wk)
        {
            var path = new ForestTrailPath { name = name, main = main, glade = glade, end = gladeC, endRadius = gladeR };
            int n = t.p.Count;
            var cum = new float[n];
            for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + (t.p[i] - t.p[i - 1]).magnitude;
            path.len = cum[n - 1];
            for (int i = 0; i < n; i++)
            {
                float dist = cum[i], toEnd = path.len - cum[i];
                float w;
                if (main)
                    w = Mathf.Lerp(fs.forestTrailMouthWidth, fs.forestTrailCorridorWidth, SS(0f, Mathf.Max(1f, fs.forestTrailNarrowLength * wk), dist)) * wk;
                else
                    w = fs.forestTrailBranchWidth * wk * Mathf.Lerp(1.35f, 1f, SS(0f, 1.6f, dist));   // у развилки чуть шире
                float nz = Perlin(dist * 0.17f + (main ? 11f : 47f), 3.7f, 0f) * 2f - 1f;
                w *= 1f + 0.14f * nz;
                if (glade) w += 2f * fs.forestTrailFlare * wk * (1f - SS(0f, 2.8f, toEnd));          // воронка у поляны
                else w *= Mathf.Lerp(0.55f, 1f, SS(0f, 3f, toEnd));                                   // открытый конец тает
                path.p.Add(t.p[i]);
                path.half.Add(Mathf.Max(0.35f, w * 0.5f));
            }
            return path;
        }
    }
}
#endif

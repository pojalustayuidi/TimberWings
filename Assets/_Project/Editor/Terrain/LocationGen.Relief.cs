#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Terrain/LocationGen.Relief.cs
// «Выразительный» рельеф (Settings.reliefStyle = 1): большие запроектированные формы вместо одного шума.
// Старый рельеф (reliefStyle = 0) живёт в LocationGen.Terrain.cs без изменений.
//
// Из чего собирается высота (единицы u, 1 u = worldScale м):
//   1. Форма B(x,z), всегда >= 0 (ниже нуля только русло и озеро: модули считают h < -0.05..-0.12 водой):
//        луга: пологие холмы с ровными лощинами между ними (шум с доменным сдвигом; meadowSwellFlat = доля площади без холмов)
//        чаша: плавный подъём от центра к краю (bowlHeight) + кромка по контуру острова (rimHeight, rimWidth)
//        лес: массив, который плавно поднимается от лагеря к площадке Forest (рампа), ярус 1 (плато), ярус 2 (дальняя часть)
//        долина: у реки формы затухают к руслу на valleyWidth, озеро (если включено) — чаша с мелким берегом
//   2. Тропы: профиль высоты вдоль каждой тропы (сглаженная форма, концы привязаны к центру / площадке / воротам леса,
//      уклон не больше trailMaxGrade), земля вокруг плавно сходится к профилю на trailBank.
//   3. Площадки и центр: ровные террасы на Y = 0 (как раньше), переход в склон на padSkirt.
//   4. Русло реки: как раньше (-riverDepth).
// Раскраска (BuildSplat в Terrain.cs зовёт ReliefPaintA / ReliefPaintB): светлая трава на гребнях, влажный оттенок в низинах,
// тёмная земля и скала на крутых склонах.
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class LocationBuilder
{
    sealed partial class Gen
    {
        const int L_ROCK = 7, L_SOIL = 8, L_CREST = 9, L_WET = 10;
        const float PaintLowpassU = 9f;   // радиус сглаживания для «гребень / ложбина», u

        const float ForestFoot = 1.5f;    // подъём массива начинается за 1.5 радиуса леса от его центра (на краю центральной площади)

        bool Expressive { get { return s.reliefStyle == 1; } }
        public int LayerCount { get { return LayerCountFor(s); } }
        internal static int LayerCountFor(Settings st) { return st.reliefStyle == 1 ? 11 : 7; }

        // ---------------------------------------------------------- край острова

        // контур FloatingIsland в u (null, если острова в сцене нет)
        List<Vector2> IslandLoopU()
        {
            var fi = UnityEngine.Object.FindFirstObjectByType<FloatingIsland>();
            if (fi == null) return null;
            var loop = fi.GetEdgeLoop();
            if (loop == null || loop.Count < 3) return null;
            var res = new List<Vector2>(loop.Count);
            for (int i = 0; i < loop.Count; i++)
                res.Add(new Vector2((loop[i].x - s.origin.x) / ws, (loop[i].y - s.origin.z) / ws));
            return res;
        }

        static float PolySegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }

        static bool PolyInside(List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            }
            return inside;
        }

        // расстояние до края острова в u: > 0 внутри. Без острова в сцене или при rimFollowIsland = false: до края Terrain
        float[] ComputeEdgeDistance(out Vector2? gateU)
        {
            gateU = null;
            var res = new float[N * N];
            List<Vector2> poly = s.rimFollowIsland ? IslandLoopU() : null;
            if (poly == null)
            {
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                        res[j * N + i] = half - Mathf.Max(Mathf.Abs(-half + i * cell), Mathf.Abs(-half + j * cell));
                return res;
            }

            var fi = UnityEngine.Object.FindFirstObjectByType<FloatingIsland>();
            var g = fi.GetGatePosition();
            if (g.HasValue) gateU = new Vector2((g.Value.x - s.origin.x) / ws, (g.Value.y - s.origin.z) / ws);

            const int M = 97;
            float cs = s.terrainSize / (M - 1);
            var coarse = new float[M * M];
            for (int j = 0; j < M; j++)
                for (int i = 0; i < M; i++)
                {
                    var q = new Vector2(-half + i * cs, -half + j * cs);
                    float best = float.MaxValue;
                    for (int k = 0; k < poly.Count; k++) best = Mathf.Min(best, PolySegDist(q, poly[k], poly[(k + 1) % poly.Count]));
                    coarse[j * M + i] = PolyInside(poly, q) ? best : -best;
                }
            for (int j = 0; j < N; j++)
            {
                float fz = Mathf.Clamp(j * cell / cs, 0f, M - 1.001f);
                int j0 = (int)fz; float tz = fz - j0;
                for (int i = 0; i < N; i++)
                {
                    float fx = Mathf.Clamp(i * cell / cs, 0f, M - 1.001f);
                    int i0 = (int)fx; float tx = fx - i0;
                    float a = coarse[j0 * M + i0], b = coarse[j0 * M + i0 + 1], c = coarse[(j0 + 1) * M + i0], d = coarse[(j0 + 1) * M + i0 + 1];
                    res[j * N + i] = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
                }
            }
            return res;
        }

        // ---------------------------------------------------------- большие формы

        // шум лугов: 0..1, порог для холмов берём по квантилям (не зависит от того, как именно распределён Perlin)
        float MeadowNoise(float x, float z)
        {
            float f = s.meadowSwellScale;
            float wx = x + (Perlin(x * 0.011f, z * 0.011f, 710f) - 0.5f) * 30f;
            float wz = z + (Perlin(x * 0.011f, z * 0.011f, 740f) - 0.5f) * 30f;
            return 0.65f * Perlin(wx * f, wz * f, 700f) + 0.35f * Perlin(wx * f * 2.3f, wz * f * 2.3f, 760f);
        }

        static float Quantile01(float[] v, float q)
        {
            const int B = 512;
            var hist = new int[B];
            for (int i = 0; i < v.Length; i++) hist[Mathf.Clamp((int)(v[i] * B), 0, B - 1)]++;
            int target = Mathf.Max(1, (int)(Mathf.Clamp01(q) * v.Length)), acc = 0;
            for (int b = 0; b < B; b++)
            {
                acc += hist[b];
                if (acc >= target) return (b + 1f) / B;
            }
            return 1f;
        }

        float ReliefBase(float x, float z, float edgeIn, Vector2? gateU, Vector2 fc, Vector2 axis, Vector2 axisR, float meadowN, float mLo, float mHi)
        {
            float hw = s.riverWidth * 0.5f;
            float rC = Mathf.Sqrt(x * x + z * z);
            float riverZ = RiverMask(z);
            float valley = Mathf.Lerp(1f, SS(hw + s.riverBank, hw + s.riverBank + s.valleyWidth, Mathf.Abs(x - RiverX(z))), riverZ);

            // лесной массив: рампа от лагеря (along < 0) к площадке леса, ярус 1 на плато, ярус 2 в дальней от лагеря части
            float massif = 0f, massifW = 0f;
            if (forest != null)
            {
                Vector2 rel = new Vector2(x, z) - fc;
                float d = rel.magnitude;
                float rad = forest.rad;
                float along = Vector2.Dot(rel, axis);                  // от центра леса прочь от лагеря
                float lateral = Mathf.Abs(Vector2.Dot(rel, axisR));
                // со стороны лагеря склон сходит по боковому расстоянию (там его формирует рампа), с остальных сторон по радиусу
                float dEff = Mathf.Lerp(lateral, d, SS(-0.25f * rad, 0.25f * rad, along));
                float mask = 1f - SS(rad * 0.9f, rad * 0.9f + s.forestSlope, dEff);
                float ramp = SS(-ForestFoot * rad, -0.1f * rad, along);
                float t2 = SS(rad * 0.25f, rad * 0.25f + s.forestTierSlope, along) * (1f - SS(rad * 0.7f, rad * 1.3f, lateral));
                float body = mask * ramp;
                massif = body * Mathf.Lerp(s.forestTier1, s.forestTier2, t2);
                massif *= 0.94f + 0.12f * Perlin(x * 0.05f, z * 0.05f, 770f);
                massif += s.forestGladeRelief * (Perlin(x * 0.08f, z * 0.08f, 300f) * 2f - 1f) * body;
                massif = Mathf.Max(0f, massif);
                massifW = mask;
            }

            // луга: холмы с ровными лощинами между ними
            float meadow = s.meadowSwell * SS(mLo, mHi, meadowN) * (1f - 0.75f * massifW);

            // чаша: подъём к краю и кромка по контуру острова
            float bowl = s.bowlHeight * SS(s.centerRadius + 6f, half * 0.85f, rC);
            float rim = 0f;
            if (s.rimHeight > 0.01f)
            {
                float rt = 1f - Mathf.Clamp01(Mathf.Max(0f, edgeIn - s.rimFlatWidth) / Mathf.Max(1f, s.rimWidth));
                float prof = rt * rt * (3f - 2f * rt);
                float rn = (0.65f + 0.7f * Perlin(x * 0.05f, z * 0.05f, 520f)) * (1f + 0.25f * (Perlin(x * 0.11f, z * 0.11f, 260f) * 2f - 1f));
                float gateF = gateU.HasValue ? SS(s.rimGateClear * 0.6f, s.rimGateClear * 1.6f, Vector2.Distance(new Vector2(x, z), gateU.Value)) : 1f;
                if (s.newRiver && bridge != null)   // Р3: у подхода к Dock кромка снижается, как у ворот (без крутого подъёма на тропе)
                    gateF *= SS(s.rimGateClear * 0.6f, s.rimGateClear * 1.6f, Vector2.Distance(new Vector2(x, z), DockApproachU()));
                rim = s.rimHeight * rn * prof * gateF * Mathf.Lerp(1f, RiverRingMask(x, z), riverZ);
            }

            float shape = Mathf.Max(rim, massif) + 0.35f * Mathf.Min(rim, massif);
            float h = (meadow + bowl) * valley + shape * Mathf.Lerp(1f, valley, 0.7f);

            if (s.lakeEnabled)
            {
                float dl = Vector2.Distance(new Vector2(x, z), s.lakePos);
                h *= SS(s.lakeRadius, s.lakeRadius + s.lakeShore, dl);
                if (dl < s.lakeRadius) h = LakeBowl(dl);   // Р4: чистая чаша (дно, стенка, мелкий берег), LocationGen.Lake.cs
            }
            return h;
        }

        // ---------------------------------------------------------- тропы по склону

        float AvgField(float[] f, Vector2 c, float r)
        {
            float sum = FieldAt(f, c.x, c.y);
            sum += FieldAt(f, c.x + r, c.y); sum += FieldAt(f, c.x - r, c.y);
            sum += FieldAt(f, c.x, c.y + r); sum += FieldAt(f, c.x, c.y - r);
            return sum / 5f;
        }

        // профиль высоты каждой тропы: сглаженная форма, концы привязаны, уклон ограничен
        void GradeTrails(float[] B)
        {
            foreach (var tr in trails)
            {
                int cnt = tr.p.Count;
                var h = new float[cnt];
                var cum = new float[cnt];
                for (int i = 1; i < cnt; i++) cum[i] = cum[i - 1] + (tr.p[i] - tr.p[i - 1]).magnitude;
                float L = cum[cnt - 1];
                for (int i = 0; i < cnt; i++) h[i] = FieldAt(B, tr.p[i].x, tr.p[i].y);

                int r = Mathf.Max(1, Mathf.RoundToInt(s.trailGradeSmooth * 0.25f));
                for (int pass = 0; pass < 3; pass++)
                {
                    var t = (float[])h.Clone();
                    for (int i = 0; i < cnt; i++)
                    {
                        float sum = 0f;
                        for (int k = -r; k <= r; k++) sum += t[Mathf.Clamp(i + k, 0, cnt - 1)];
                        h[i] = sum / (2 * r + 1);
                    }
                }

                // концы: тропа от центра начинается на 0, ответвление на высоте родительской тропы в развилке,
                // тропа к площадке кончается на 0, к воротам леса на высоте яруса у ворот
                float startVal = 0f;
                Pad tp = tr.target;
                if (tp != null && tp.branch)
                {
                    Pad par = pads.Find(q => q.def.name == tp.def.parent && q != tp);
                    if (par != null && par.trail != null && par.trail.h != null)
                    {
                        float bd = float.MaxValue; int bi = 0;
                        for (int i = 0; i < par.trail.p.Count; i++)
                        {
                            float dd = (par.trail.p[i] - tp.fork).sqrMagnitude;
                            if (dd < bd) { bd = dd; bi = i; }
                        }
                        startVal = par.trail.h[bi];
                    }
                }
                float endVal = (tp != null && tp.round) ? AvgField(B, tr.p[cnt - 1], 3f) : 0f;

                float bl = Mathf.Min(14f, L * 0.45f);
                float d0 = startVal - h[0], d1 = endVal - h[cnt - 1];
                for (int i = 0; i < cnt; i++)
                    h[i] += d0 * (1f - SS(0f, bl, cum[i])) + d1 * (1f - SS(0f, bl, L - cum[i]));

                float g = Mathf.Max(0.05f, s.trailMaxGrade);
                for (int it = 0; it < 4; it++)
                {
                    h[0] = startVal;
                    for (int i = 1; i < cnt; i++)
                    {
                        float st = cum[i] - cum[i - 1];
                        h[i] = Mathf.Clamp(h[i], h[i - 1] - g * st, h[i - 1] + g * st);
                    }
                    h[cnt - 1] = endVal;
                    for (int i = cnt - 2; i >= 0; i--)
                    {
                        float st = cum[i + 1] - cum[i];
                        h[i] = Mathf.Clamp(h[i], h[i + 1] - g * st, h[i + 1] + g * st);
                    }
                }
                for (int i = 0; i < cnt; i++) h[i] = Mathf.Max(0f, h[i]);

                float worst = 0f;
                for (int i = 1; i < cnt; i++)
                    worst = Mathf.Max(worst, Mathf.Abs(h[i] - h[i - 1]) / Mathf.Max(0.01f, cum[i] - cum[i - 1]));
                if (worst > g * 1.25f)
                    warnings.Add("Тропа " + tr.name + ": уклон " + worst.ToString("0.00") + " больше лимита " + g.ToString("0.00") + " (слишком короткая для такого подъёма: подвинь площадку или уменьши ярус леса).");
                tr.h = h;
            }
        }

        // для каждой клетки: высота профиля ближайшей тропы
        float[] StampTrailHeights()
        {
            var tH = new float[N * N];
            var best = new float[N * N];
            for (int i = 0; i < best.Length; i++) best[i] = float.MaxValue;
            float reach = s.trailBank + s.trailFlatWidth + 6f;
            foreach (var tr in trails)
            {
                if (tr.h == null) continue;
                for (int k = 0; k < tr.p.Count - 1; k++)
                {
                    Vector2 a = tr.p[k], b = tr.p[k + 1];
                    float ha = tr.h[k], hb = tr.h[k + 1];
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + half) / cell));
                    int i1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + half) / cell));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + half) / cell));
                    int j1 = Mathf.Min(N - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + half) / cell));
                    Vector2 ab = b - a;
                    float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                    for (int j = j0; j <= j1; j++)
                    {
                        float z = -half + j * cell;
                        for (int i = i0; i <= i1; i++)
                        {
                            var p = new Vector2(-half + i * cell, z);
                            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                            float dc = (p - (a + ab * t)).magnitude;
                            int idx = j * N + i;
                            if (dc < best[idx]) { best[idx] = dc; tH[idx] = Mathf.Lerp(ha, hb, t); }
                        }
                    }
                }
            }
            return tH;
        }

        // ---------------------------------------------------------- сборка высот

        void BlurHeights()
        {
            var tmp = new float[N * N];
            for (int pass = 0; pass < s.blurPasses; pass++)
            {
                for (int j = 0; j < N; j++)
                {
                    int jm = Mathf.Max(j - 1, 0), jp = Mathf.Min(j + 1, N - 1);
                    for (int i = 0; i < N; i++)
                    {
                        int im = Mathf.Max(i - 1, 0), ip = Mathf.Min(i + 1, N - 1);
                        float sum = hu[jm * N + im] + 2f * hu[jm * N + i] + hu[jm * N + ip]
                                  + 2f * hu[j * N + im] + 4f * hu[j * N + i] + 2f * hu[j * N + ip]
                                  + hu[jp * N + im] + 2f * hu[jp * N + i] + hu[jp * N + ip];
                        tmp[j * N + i] = sum / 16f;
                    }
                }
                var sw = hu; hu = tmp; tmp = sw;
            }
        }

        void ComputeHeightsRelief()
        {
            hu = new float[N * N];
            dFlatArr = new float[N * N];
            Vector2? gateU;
            float[] edgeD = ComputeEdgeDistance(out gateU);
            edgeField = edgeD;
            float flatExtra = (s.trailFlatWidth - s.trailWidth) * 0.5f;
            float hw = s.riverWidth * 0.5f;
            Vector2 fc = forest != null ? forest.pos : Vector2.zero;
            Vector2 axis = (forest != null && forest.pos.magnitude > 1e-3f) ? forest.pos.normalized : Vector2.up;
            Vector2 axisR = new Vector2(axis.y, -axis.x);

            if (s.lakeEnabled)
                foreach (var q in pads)
                    if (q.def.name != "Pond" && Vector2.Distance(q.pos, s.lakePos) < s.lakeRadius + s.lakeShore + q.rad)   // Pond = пирс на берегу, ему на берегу и стоять
                        warnings.Add("Озеро заходит на площадку " + q.def.name + ": подвинь озеро (Рельеф → Река и озеро).");

            // 1) большие формы. Сначала шум лугов целиком: порог холмов берётся по квантилям
            var nArr = new float[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    nArr[j * N + i] = MeadowNoise(-half + i * cell, -half + j * cell);
            float mLo = Quantile01(nArr, s.meadowSwellFlat);
            float mHi = Mathf.Max(mLo + 0.03f, Quantile01(nArr, Mathf.Min(0.97f, s.meadowSwellFlat + 0.4f)));

            var B = new float[N * N];
            var dPadA = new float[N * N];
            for (int j = 0; j < N; j++)
            {
                float z = -half + j * cell;
                for (int i = 0; i < N; i++)
                {
                    float x = -half + i * cell;
                    int idx = j * N + i;
                    float dTr = sdTrail[idx] - flatExtra;
                    float dPad = Mathf.Min(sdPad[idx], new Vector2(x, z).magnitude - s.centerRadius);
                    dPadA[idx] = dPad;
                    dFlatArr[idx] = Mathf.Min(dTr, dPad);
                    B[idx] = ReliefBase(x, z, edgeD[idx], gateU, fc, axis, axisR, nArr[idx], mLo, mHi);
                }
            }

            // 2) профили троп и привязка земли к ним
            GradeTrails(B);
            float[] tH = StampTrailHeights();

            for (int j = 0; j < N; j++)
            {
                float z = -half + j * cell;
                for (int i = 0; i < N; i++)
                {
                    float x = -half + i * cell;
                    int idx = j * N + i;
                    float h = B[idx];

                    float lk = LakeKeep(x, z);   // Р4: площадка и тропа не поднимают дно чаши внутри водного радиуса
                    float wT = (1f - SS(0f, s.trailBank, sdTrail[idx] - flatExtra)) * lk;
                    if (wT > 0f) h = Mathf.Lerp(h, tH[idx], wT);

                    float wP = (1f - SS(0.4f, 0.4f + s.padSkirt, dPadA[idx])) * lk;
                    if (wP > 0f) h = Mathf.Lerp(h, 0f, wP);

                    h = CarveRiver(x, z, h, Mathf.Max(wT, wP), 1f - SS(0f, 0.8f, dPadA[idx]));

                    hu[idx] = Mathf.Min(h, s.maxHeight);
                }
            }

            BlurHeights();

            // площадки и центр: Y = 0 точно (тропы ровными не делаем: они идут по профилю)
            for (int idx = 0; idx < hu.Length; idx++)
            {
                float d = dPadA[idx];
                if (d <= 0.4f) hu[idx] = 0f;
                else if (d < 2f)
                {
                    // Р4: у воды озера переход к площадке не поднимает дно (LakeKeep = 0 внутри водного радиуса)
                    float lk2 = s.lakeEnabled ? LakeKeep(-half + (idx % N) * cell, -half + (idx / N) * cell) : 1f;
                    hu[idx] = Mathf.Lerp(hu[idx], Mathf.Lerp(0f, hu[idx], SS(0.4f, 2f, d)), lk2);
                }
            }

            float mn = float.MaxValue, mx = float.MinValue;
            for (int idx = 0; idx < hu.Length; idx++)
            {
                if (hu[idx] < mn) mn = hu[idx];
                if (hu[idx] > mx) mx = hu[idx];
            }
            minM = mn * ws;
            hmM = Mathf.Max(mx * ws - minM, 1f);

            if (mx >= s.maxHeight - 0.01f)
                warnings.Add("Рельеф упирается в «Макс. высоту» (" + s.maxHeight.ToString("0.#") + " u): вершины срезаны. Подними maxHeight или уменьши ярусы леса / кромку.");
            if (cell * ws > 0.2f)
                warnings.Add("Heightmap грубый: шаг " + (cell * ws * 100f).ToString("0") + " см. Подними Heightmap res (Рельеф → Terrain).");
            Debug.Log("[LocationBuilder] Рельеф «Выразительный»: высоты " + mn.ToString("0.0") + " .. " + mx.ToString("0.0") + " u (" + (mx * ws).ToString("0.0") + " м), шаг heightmap " + (cell * ws * 100f).ToString("0") + " см.");
        }

        // для перекраски троп леса: высоты берём из уже собранного Terrain (рельеф не пересчитываем, ничего не двигается)
        string LoadHeightsFromTerrain(Terrain t)
        {
            var td = t.terrainData;
            if (td.heightmapResolution != N)
                return "разрешение heightmap Terrain (" + td.heightmapResolution + ") не совпадает с настройками (" + N + "): пересоберите локацию";
            var hm = td.GetHeights(0, 0, N, N);
            float y0 = t.transform.localPosition.y, sy = td.size.y;
            hu = new float[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    hu[j * N + i] = (y0 + hm[j, i] * sy) / ws;
            return null;
        }

        // ---------------------------------------------------------- раскраска

        float[] BlurField(float[] src, int r)
        {
            var tmp = new float[N * N];
            var dst = new float[N * N];
            float inv = 1f / (2 * r + 1);
            for (int j = 0; j < N; j++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += src[j * N + Mathf.Clamp(k, 0, N - 1)];
                for (int i = 0; i < N; i++)
                {
                    tmp[j * N + i] = sum * inv;
                    sum += src[j * N + Mathf.Min(i + r + 1, N - 1)] - src[j * N + Mathf.Max(i - r, 0)];
                }
            }
            for (int i = 0; i < N; i++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += tmp[Mathf.Clamp(k, 0, N - 1) * N + i];
                for (int j = 0; j < N; j++)
                {
                    dst[j * N + i] = sum * inv;
                    sum += tmp[Mathf.Min(j + r + 1, N - 1) * N + i] - tmp[Mathf.Max(j - r, 0) * N + i];
                }
            }
            return dst;
        }

        float SlopeAt(float x, float z)
        {
            const float d = 0.6f;
            float gx = (HeightU(x + d, z) - HeightU(x - d, z)) / (2f * d);
            float gz = (HeightU(x, z + d) - HeightU(x, z - d)) / (2f * d);
            return Mathf.Sqrt(gx * gx + gz * gz);
        }

        // светлая трава на гребнях и влажный оттенок в низинах (до леса и берега: их они не перекрывают)
        void ReliefPaintA(float[] w, float x, float z, float[] lowU)
        {
            float h = HeightU(x, z);
            float conc = FieldAt(lowU, x, z) - h;                       // > 0 ложбина, < 0 гребень
            float sl = SlopeAt(x, z);
            float flat = 1f - SS(0.12f, 0.30f, sl);
            float dg = Mathf.Min(FieldAt(sdTrail, x, z), FieldAt(sdPad, x, z));
            float away = SS(1.5f, 7f, dg);                              // у троп и площадок свои цвета

            float crest = SS(0.20f, 0.80f, -conc) * SS(0.3f, 1.2f, h) * flat * away;
            Over(w, L_CREST, crest * 0.85f * s.paintCrest);

            float hw = s.riverWidth * 0.5f;
            float nearRiver = (1f - SS(hw + s.riverBank, hw + s.riverBank + s.valleyWidth * 0.7f, Mathf.Abs(x - RiverX(z)))) * RiverMask(z);
            float lake = s.lakeEnabled ? 1f - SS(s.lakeRadius, s.lakeRadius + s.lakeShore, Vector2.Distance(new Vector2(x, z), s.lakePos)) : 0f;
            float low = (1f - SS(0.5f, 1.6f, h)) * SS(0.10f, 0.60f, conc) * flat;
            float wet = Mathf.Max(low, Mathf.Max(nearRiver * 0.8f, lake)) * away;
            Over(w, L_WET, wet * 0.75f * s.paintWet);
        }

        // тёмная земля и скала на крутых склонах (после леса: уступы массива читаются и под деревьями)
        void ReliefPaintB(float[] w, float x, float z)
        {
            float sl = SlopeAt(x, z) + (Perlin(x * 0.35f, z * 0.35f, 810f) - 0.5f) * 0.10f;
            float dg = Mathf.Min(FieldAt(sdTrail, x, z), FieldAt(sdPad, x, z));
            float k = SS(1.0f, 4.0f, dg);
            Over(w, L_SOIL, SS(s.paintSoilSlope, s.paintSoilSlope + 0.16f, sl) * 0.9f * k);
            Over(w, L_ROCK, SS(s.paintRockSlope, s.paintRockSlope + 0.18f, sl) * 0.95f * k);
        }
    }
}
#endif

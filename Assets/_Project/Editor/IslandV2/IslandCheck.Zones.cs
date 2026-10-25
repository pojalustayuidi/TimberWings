// Assets/_Project/Editor/IslandV2/IslandCheck.Zones.cs
// Остров v2, A3/A4: расширение IslandCheck под зоны и тропы. Часть partial-класса IslandCheck (основной файл: IslandCheck.cs).
// Вызывается из IslandCheck.Run (пункт 3). Проходимость до каждого нового якоря уже считает CheckWalk (он идёт по всем L.anchors); здесь добавлено:
//   CheckZones       все якоря зон есть в раскладке; 6 мест рабочих у палатки не слиплись; кейс питомцев рядом с палаткой
//   CheckTrails      длина каждой тропы, длина пути по тропам до моста и время при 4 м/с, примыкание тропы к другой тропе
//   ReportDistances  кратчайшая ходьба по сетке от SPAWN_Player до зон; если до моста по сетке заметно короче, чем по тропам, лес не держит змейку
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandCheck
    {
        const float WalkSpeed = 4f;   // м/с, как в GDD

        static readonly string[] RequiredAnchors =
        {
            "CAMPFIRE_Point", "SPAWN_Player", "PAD_Hut", "PAD_Market", "PAD_QuestBoard", "PAD_SiteA", "PAD_SiteB",
            "PAD_Tent", "PAD_PetCase", "PAD_Worker1", "PAD_Worker2", "PAD_Worker3", "PAD_Worker4", "PAD_Worker5", "PAD_Worker6",
            "PAD_Forest", "ForestGate", "PAD_Grandpa", "PAD_Lighthouse", "PAD_GunnarGlade", "PAD_GunnarShore", "PAD_GroveSouth", "DEFERRED_Bridge"
        };

        static readonly string[] DistTargets =
        {
            "PAD_Hut", "PAD_Market", "PAD_SiteA", "PAD_SiteB", "PAD_Tent", "PAD_Forest", "PAD_Grandpa", "PAD_Lighthouse",
            "PAD_GroveSouth", "PAD_GunnarGlade", "PAD_GunnarShore", "DEFERRED_Bridge"
        };

        static float trailToBridge;   // длина пути по тропам костёр -> мост, м (0 = не посчитано)

        // ---------------------------------------------------------------- зоны (A3)
        static void CheckZones(IslandField f)
        {
            var L = f.L;
            var missing = new List<string>();
            foreach (var n in RequiredAnchors) if (L.FindAnchor(n) == null) missing.Add(n);
            if (missing.Count > 0) Fail("нет якорей зон: " + string.Join(", ", missing) + " (меню «Зоны: применить A3-A4»)");
            else Ok("все " + RequiredAnchors.Length + " якорей зон есть в раскладке");

            // 6 мест рабочих
            var w = new List<Vector2>();
            for (int i = 1; i <= 6; i++)
            {
                var a = L.FindAnchor("PAD_Worker" + i);
                if (a != null) w.Add(f.AnchorWorld(a));
            }
            var tent = L.FindAnchor("PAD_Tent");
            if (w.Count == 6 && tent != null)
            {
                float minD = float.MaxValue, maxT = 0f;
                Vector2 tp = f.AnchorWorld(tent);
                for (int i = 0; i < w.Count; i++)
                {
                    maxT = Mathf.Max(maxT, Vector2.Distance(w[i], tp));
                    for (int j = i + 1; j < w.Count; j++) minD = Mathf.Min(minD, Vector2.Distance(w[i], w[j]));
                }
                string s = "6 мест рабочих: между соседними не меньше " + minD.ToString("0.0") + " м, от палатки не дальше " + maxT.ToString("0.0") + " м";
                if (minD < 1.8f) Warn(s + " (слишком тесно, нужно от 1,8 м)");
                else if (maxT > 12f) Warn(s + " (далеко от палатки, нужно до 12 м)");
                else Ok(s);
            }

            // кейс питомцев рядом с палаткой
            var pet = L.FindAnchor("PAD_PetCase");
            if (pet != null && tent != null)
            {
                float d = Vector2.Distance(f.AnchorWorld(pet), f.AnchorWorld(tent));
                string s = "кейс питомцев в " + d.ToString("0.0") + " м от палатки";
                if (d <= 8f) Ok(s); else Warn(s + " (по плану рядом, до 8 м)");
            }

            // брод должен перекрывать реку от берега до берега: иначе по его краям остаётся полоска воды и западный берег не достижим
            foreach (var cr in L.crossings)
            {
                float need = L.riverWidth * 0.5f + 1f;
                string s = "брод (" + cr.pos.x.ToString("0") + ", " + cr.pos.y.ToString("0") + "): halfLength " + cr.halfLength.ToString("0.#") + " м, река шириной " + L.riverWidth.ToString("0") + " м";
                if (cr.halfLength >= need) Ok(s + ": зона брода перекрывает реку от берега до берега");
                else Fail(s + ": зона брода уже реки, берега не соединены (меню «Зоны: применить A3-A4» поправит)");
            }
        }

        // ---------------------------------------------------------------- тропы (A4)
        static float PolyLen(Vector2[] pl)
        {
            float l = 0f;
            for (int i = 0; i + 1 < pl.Length; i++) l += Vector2.Distance(pl[i], pl[i + 1]);
            return l;
        }

        static float DistToPoly(Vector2[] pl, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < pl.Length; i++)
            {
                Vector2 a = pl[i], ab = pl[i + 1] - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static void CheckTrails(IslandField f)
        {
            var L = f.L;
            var lens = new Dictionary<string, float>();
            var line = new System.Text.StringBuilder();
            for (int i = 0; i < L.paths.Count; i++)
            {
                var pl = f.PathPolyline(i);
                float len = PolyLen(pl);
                lens[L.paths[i].name] = len;
                line.Append(L.paths[i].name + " " + len.ToString("0") + "; ");
                if (pl.Length == 0) continue;
                bool joined = pl[0].magnitude < 3f;   // начало у костра
                for (int j = 0; j < L.paths.Count && !joined; j++)
                    if (j != i && DistToPoly(f.PathPolyline(j), pl[0]) < 2.5f) joined = true;
                if (!joined) Warn("тропа «" + L.paths[i].name + "» начинается в (" + pl[0].x.ToString("0") + ", " + pl[0].y.ToString("0") + ") и не примыкает ни к костру, ни к другой тропе");
            }
            sb.AppendLine("  длины троп, м: " + line);

            float a, b;
            trailToBridge = 0f;
            if (lens.TryGetValue("Рынок", out a) && lens.TryGetValue("Мост", out b))
            {
                trailToBridge = a + b;
                float t = trailToBridge / WalkSpeed;
                string s = "путь по тропам костёр -> мост: " + trailToBridge.ToString("0") + " м, ~" + t.ToString("0") + " с при " + WalkSpeed.ToString("0") + " м/с (в GDD 40-50 с до дальней точки)";
                if (t >= 28f) Ok(s); else Warn(s + ": короче ~30 с, тропу «Мост» нужно вытянуть");
            }
        }

        // ---------------------------------------------------------------- дистанции ходьбы (A3/A4)
        sealed class DistCmp : IComparer<KeyValuePair<float, int>>
        {
            public int Compare(KeyValuePair<float, int> x, KeyValuePair<float, int> y)
            {
                int c = x.Key.CompareTo(y.Key);
                return c != 0 ? c : x.Value.CompareTo(y.Value);
            }
        }

        // кратчайшие расстояния по проходимым клеткам от (si, sj), 8 направлений, как в Flood
        static float[] Dijkstra(Grid g, int si, int sj)
        {
            var dist = new float[g.walk.Length];
            for (int k = 0; k < dist.Length; k++) dist[k] = float.MaxValue;
            var open = new SortedSet<KeyValuePair<float, int>>(new DistCmp());
            int s0 = g.Idx(si, sj);
            dist[s0] = 0f;
            open.Add(new KeyValuePair<float, int>(0f, s0));
            while (open.Count > 0)
            {
                var top = open.Min;
                open.Remove(top);
                int k = top.Value;
                if (top.Key > dist[k]) continue;
                int i = k % g.nx, j = k / g.nx;
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= g.nx || nj >= g.nz) continue;
                        int nk = g.Idx(ni, nj);
                        if (!g.walk[nk]) continue;
                        if (di != 0 && dj != 0 && (!g.walk[g.Idx(i + di, j)] || !g.walk[g.Idx(i, j + dj)])) continue;
                        float nd = top.Key + (di != 0 && dj != 0 ? 1.4142f : 1f);
                        if (nd < dist[nk]) { dist[nk] = nd; open.Add(new KeyValuePair<float, int>(nd, nk)); }
                    }
                }
            }
            return dist;
        }

        static void ReportDistances(IslandField f, Grid g, int si, int sj)
        {
            var dist = Dijkstra(g, si, sj);
            sb.AppendLine("  ходьба от SPAWN_Player по сетке 1 м (кратчайшая, лес считается проходимым между стволами), " + WalkSpeed.ToString("0") + " м/с:");
            foreach (var n in DistTargets)
            {
                var a = f.L.FindAnchor(n);
                if (a == null) continue;
                int ci, cj;
                if (!Nearest(g, Approach(f, a), 2.0f, out ci, out cj)) continue;   // про отсутствие подхода уже написал CheckWalk
                float d = dist[g.Idx(ci, cj)];
                if (d >= float.MaxValue) { sb.AppendLine("    " + n + ": недостижимо"); continue; }
                sb.AppendLine("    " + n + ": " + d.ToString("0") + " м, ~" + (d / WalkSpeed).ToString("0") + " с");
                if (n == "DEFERRED_Bridge" && trailToBridge > 0f)
                {
                    string s = "до моста по сетке " + d.ToString("0") + " м против " + trailToBridge.ToString("0") + " м по тропам";
                    if (d >= trailToBridge * 0.75f) Ok(s + ": змейку не обойти");
                    else Warn(s + ": лес между петлями проходим, игрок срежет (густота леса между петлями решается в A5)");
                }
            }
        }
    }
}

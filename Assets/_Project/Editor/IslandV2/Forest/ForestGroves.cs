#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestGroves.cs (Editor)  НОВЫЙ ФАЙЛ
// «Большой лес из рубимых деревьев»: рощи с полянами и проходами вместо равномерной россыпи. Отдельного пункта меню нет:
// вызывается из «4. Лес: расставить рабочие и фоновые деревья» (ForestBackdrop.Run) вместо старой сетки фоновых Decor_*.
// Результат лежит в IslandV2/_Manual/ForestChop (рядом с рабочими деревьями полян и ZoneTree_*), имена Chop_<номер>.
//
// Правила расстановки (всё детерминировано по Seed, повторный запуск даёт тот же лес):
//   1. Поляны: до MaxClearings «чистых полян» радиусом 4.5-7 м в густой части леса (вес леса от ClearWeightMin). Деревья на них не ставятся.
//   2. Рощи: центры бросаются случайно, принимаются с вероятностью, которая растёт вместе с ForestWeight (чаща = много рощ, у троп и на лугах мало).
//      Радиус рощи 3.2-7 м, зависит от веса. Между рощами держим проход не уже PassageGap (деревья там не растут), поэтому лес читается как
//      «рощи и проходы», а не сплошной ковёр. Центр рощи не ближе 3.0 м к тропе, 2.5 м к воде, 2.5 м к обрыву (Ok из ForestBackdrop).
//   3. Деревья рощи: плотнее к центру (степень 0.65 вместо 0.5 у равномерного диска), не ближе TreeGap друг к другу. Вокруг троп и построек
//      часть деревьев пропускается (у тропы 20-85% остаётся, у зданий до 50%): «у троп и построек пореже».
//   4. Вид: пятнами по шуму Перлина (кедры, дубы, «случайные», берёзы, осень), 75% рощи свой вид, остальное «случайные» и берёзы.
//   5. Размер (ResourceItem.overrideSize + forcedSize): в чаще больше старых, у края молодых, ядро рощи чуть старше края.
//   6. Не ставит: в радиусе зоны рубки PAD_GroveSouth (там ChopZone), у PAD_Forest (маленькая достопримечательность, 5-6 своих деревьев), у деревьев-хранителей
//      и лёгких хранителей, которые уже стоят в сцене (читает их полянки), на крутых склонах, воде, тропах, зданиях.
//   Общий потолок деревьев MaxTrees: у каждого дерева ResourceItem, коллайдер и земля под ним (TreeGround), это самое дорогое, что есть в лесу.
// Сводка (сколько рощ, полян, деревьев по размерам, что не нашло места) возвращается строкой и печатается в Console из ForestBackdrop.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ForestGroves
    {
        public const int Seed = 8803;

        // ---- числа расстановки (м, если не сказано иное)
        public const int MaxTrees = 380;          // потолок рубимых деревьев рощ (без деревьев полян и ZoneTree_): баланс «большой лес» против стоимости ResourceItem
        const int MaxGroves = 80;                 // потолок рощ: защита от бесконечного цикла на большом острове
        const int MaxClearings = 6;               // чистых полян в чаще
        const float ClearingSeparation = 24f;     // между центрами полян: поляны разбросаны по лесу, а не слиплись
        const float ClearWeightMin = 0.55f;       // поляны только там, где лес уже густой (вес леса)
        const float PassageGap = 3.8f;            // минимальный проход между рощами: игрок и подлесок проходят без обхода деревьев
        public const float TreeGap = 2.7f;        // ближе друг к другу рубимые деревья не ставятся (как ChopGap в ForestBackdrop)
        const float GroveCoreExp = 0.65f;         // степень радиуса: меньше 0.5 = плотнее в центре
        const float AreaPerTreeThick = 13f;       // м² на дерево в чаще (плотно, но с воздухом: минимум по TreeGap это ~6.5 м²)
        const float AreaPerTreeThin = 22f;        // м² на дерево на краю леса (реже)
        const float PathThinDist = 7f;            // ближе этого к тропе деревья прореживаются
        const float PathMin = 2.6f;               // ближе этого к оси тропы деревьев нет совсем
        const float BuildingThinDist = 9f;        // ближе этого к зданию деревья прореживаются
        const float EdgeThinDist = 6f;            // у обрыва реже: там стоят крупные нерубимые «кулисы»

        public sealed class Result
        {
            public int groves, clearings, trees, capped;
            public int rejOk, rejNear, rejKeep, rejThin, rejClearing, rejGlade;
            public int groveMiss;                 // рощ, где не набралось и половины деревьев
            public int young, mature, old;
            public string report;
        }

        // ============================================================ главное
        // noGo: (x, z, радиус): сюда не ставим (зона рубки, поляна). all: уже стоящие стволы (общий хеш 3 м), сюда же добавляем свои.
        public static Result Place(IslandField f, IslandLayout L, Transform chopRoot, Dictionary<string, List<GameObject>> sp,
            Dictionary<long, List<Vector2>> all, List<Vector3> noGo, int firstIndex)
        {
            var res = new Result();
            var rng = new System.Random(Seed);
            var keep = ReadKeepOut();
            int idx = firstIndex;

            // ---- 1. поляны
            var clearings = new List<Vector3>();
            var cand = new List<Vector2>();
            for (float gx = f.Min.x; gx <= f.Max.x; gx += 5f)
                for (float gz = f.Min.y; gz <= f.Max.y; gz += 5f)
                {
                    if (!f.Inside(gx, gz) || f.ForestWeight(gx, gz) < ClearWeightMin) continue;
                    if (!ForestBackdrop.Ok(f, L, gx, gz, 6f, 4f, 6f)) continue;
                    if (InCircles(noGo, gx, gz, 4f)) continue;
                    cand.Add(new Vector2(gx, gz));
                }
            for (int i = cand.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = cand[i]; cand[i] = cand[j]; cand[j] = t; }
            for (int i = 0; i < cand.Count && clearings.Count < MaxClearings; i++)
            {
                float cr = 4.5f + 2.5f * (float)rng.NextDouble();
                bool far = true;
                for (int k = 0; k < clearings.Count && far; k++)
                    if (Vector2.Distance(new Vector2(clearings[k].x, clearings[k].y), cand[i]) < ClearingSeparation) far = false;
                if (!far || InCircles(keep, cand[i].x, cand[i].y, cr + 1f)) continue;
                clearings.Add(new Vector3(cand[i].x, cand[i].y, cr));
            }
            res.clearings = clearings.Count;

            // ---- 2. центры рощ
            var groves = new List<Vector3>();
            for (int tries = 0; tries < 6000 && groves.Count < MaxGroves; tries++)
            {
                float x = Mathf.Lerp(f.Min.x, f.Max.x, (float)rng.NextDouble());
                float z = Mathf.Lerp(f.Min.y, f.Max.y, (float)rng.NextDouble());
                double roll = rng.NextDouble();
                double sizeRoll = rng.NextDouble();
                if (!f.Inside(x, z)) continue;
                float w = f.ForestWeight(x, z);
                if (roll > 0.05f + 0.95f * Mathf.Pow(w, 1.3f)) continue;   // лес растёт к чаще, на лугах редкие рощицы
                float gr = Mathf.Lerp(3.2f, 7f, w) * (0.8f + 0.4f * (float)sizeRoll);
                if (!ForestBackdrop.Ok(f, L, x, z, 2.5f, 2.5f, PathMin + 0.4f)) { res.rejOk++; continue; }
                if (InCircles(noGo, x, z, gr * 0.5f)) { res.rejGlade++; continue; }
                if (InCircles(keep, x, z, gr * 0.5f)) { res.rejKeep++; continue; }
                if (InCircles(clearings, x, z, gr * 0.5f)) { res.rejClearing++; continue; }
                bool tight = false;
                for (int k = 0; k < groves.Count && !tight; k++)
                    if (Vector2.Distance(new Vector2(groves[k].x, groves[k].y), new Vector2(x, z)) < gr + groves[k].z + PassageGap) tight = true;
                if (tight) { res.rejNear++; continue; }
                groves.Add(new Vector3(x, z, gr));
            }
            res.groves = groves.Count;

            // ---- 3. деревья в рощах
            for (int g = 0; g < groves.Count && res.trees < MaxTrees; g++)
            {
                Vector2 c = new Vector2(groves[g].x, groves[g].y);
                float gr = groves[g].z;
                float w = f.ForestWeight(c.x, c.y);
                float perTree = Mathf.Lerp(AreaPerTreeThin, AreaPerTreeThick, w);
                int target = Mathf.Max(3, Mathf.RoundToInt(Mathf.PI * gr * gr / perTree));

                // вид-доминанта по шуму: пятна одного вида на несколько рощ подряд
                float n = Mathf.PerlinNoise(c.x / 21f + 5.1f, c.y / 21f + 9.7f);
                string dom = n < 0.22f ? "Cedar" : n < 0.42f ? "Oak" : n < 0.62f ? "Random" : n < 0.82f ? "Birch" : "Autumn";

                int placed = 0;
                for (int tries = 0; tries < target * 14 && placed < target && res.trees < MaxTrees; tries++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rho = gr * Mathf.Pow((float)rng.NextDouble(), GroveCoreExp);
                    float x = c.x + Mathf.Cos(ang) * rho, z = c.y + Mathf.Sin(ang) * rho;
                    double thin = rng.NextDouble(), pick = rng.NextDouble(), sizeRoll = rng.NextDouble();
                    int variant = rng.Next(1000);

                    if (!ForestBackdrop.Ok(f, L, x, z, 2.0f, 2.0f, PathMin)) { res.rejOk++; continue; }
                    if (InCircles(noGo, x, z, 0f)) { res.rejGlade++; continue; }
                    if (InCircles(keep, x, z, 0f)) { res.rejKeep++; continue; }
                    if (InCircles(clearings, x, z, 0f)) { res.rejClearing++; continue; }
                    if (ForestBackdrop.Near(all, new Vector2(x, z), TreeGap)) { res.rejNear++; continue; }

                    // «у троп, построек и обрыва пореже»
                    float keepP = 1f;
                    float pd = f.PathDistance(x, z);
                    if (pd < PathThinDist) keepP *= 0.2f + 0.65f * Mathf.InverseLerp(PathMin, PathThinDist, pd);
                    float bd = BuildingDist(f, L, x, z);
                    if (bd < BuildingThinDist) keepP *= 0.5f + 0.5f * Mathf.InverseLerp(4f, BuildingThinDist, bd);
                    float ed = f.EdgeDistance(x, z);
                    if (ed < EdgeThinDist) keepP *= 0.5f + 0.5f * Mathf.InverseLerp(2f, EdgeThinDist, ed);
                    if (thin > keepP) { res.rejThin++; continue; }

                    string key = pick < 0.75 ? dom : (pick < 0.88 ? "Random" : "Birch");
                    List<GameObject> list;
                    if (!sp.TryGetValue(key, out list) || list.Count == 0) { key = "Random"; list = sp[key]; }
                    var prefab = list[variant % list.Count];

                    // размер: чаща старше, край молодее, ядро рощи чуть старше края
                    float core = 1f - Mathf.Clamp01(rho / gr);
                    float wOld = Mathf.Clamp01(w * 0.8f + core * 0.25f);
                    TreeSize size = sizeRoll < Mathf.Lerp(0.55f, 0.08f, wOld) ? TreeSize.Young
                                  : sizeRoll < Mathf.Lerp(0.95f, 0.50f, wOld) ? TreeSize.Mature : TreeSize.Old;

                    var go = ForestBackdrop.SpawnTree(prefab, chopRoot, f, x, z, rng, "Chop_" + idx);
                    var ri = go.GetComponent<ResourceItem>();
                    if (ri != null)
                    {
                        ri.overrideSize = true;
                        ri.forcedSize = size;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(ri);
                    }
                    ForestBackdrop.AddTrunk(all, new Vector2(x, z));
                    if (size == TreeSize.Young) res.young++; else if (size == TreeSize.Mature) res.mature++; else res.old++;
                    idx++; placed++; res.trees++;
                }
                if (placed < target / 2) res.groveMiss++;
            }
            if (res.trees >= MaxTrees) res.capped = 1;

            var sb = new StringBuilder();
            sb.AppendLine("  большой лес: рощ " + res.groves + ", чистых полян " + res.clearings + ", рубимых деревьев " + res.trees + " (Young " + res.young + ", Mature " + res.mature + ", Old " + res.old + ")" + (res.capped == 1 ? " [!] упёрлось в потолок " + MaxTrees + ", подними MaxTrees в ForestGroves.cs, если нужно больше" : ""));
            sb.AppendLine("  не нашло места: роща без половины деревьев " + res.groveMiss + "; отказов: вода/обрыв/тропа/здание/склон " + res.rejOk + ", слишком близко к соседу или роще " + res.rejNear
                + ", зона рубки и поляны " + res.rejGlade + ", хранители " + res.rejKeep + ", чистые поляны " + res.rejClearing + ", прореживание у троп/построек/обрыва " + res.rejThin);
            res.report = sb.ToString();
            return res;
        }

        // ============================================================ помощники
        // (x, z, радиус): уже стоящие в сцене нерубимые деревья с полянкой. Читаются из корней SacredTrees и GuardianLite.
        public static List<Vector3> ReadKeepOut()
        {
            var l = new List<Vector3>();
            var man = GameObject.Find("IslandV2/_Manual");
            if (man == null) return l;
            var s = man.transform.Find("SacredTrees");
            if (s != null)
                for (int i = 0; i < s.childCount; i++) { var p = s.GetChild(i).position; l.Add(new Vector3(p.x, p.z, 4.2f)); }
            var g = man.transform.Find(ForestGuardLite.RootName);
            if (g != null)
                for (int i = 0; i < g.childCount; i++)
                {
                    var t = g.GetChild(i);
                    var lg = t.GetComponent<LiteGuardian>();
                    l.Add(new Vector3(t.position.x, t.position.z, lg != null ? lg.clearRadius : 4.2f));
                }
            return l;
        }

        // внутри любого круга (x, z, r) с запасом pad
        static bool InCircles(List<Vector3> circles, float x, float z, float pad)
        {
            for (int i = 0; i < circles.Count; i++)
            {
                float dx = circles[i].x - x, dz = circles[i].y - z, r = circles[i].z + pad;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        public static float BuildingDist(IslandField f, IslandLayout L, float x, float z)
        {
            float bd = float.MaxValue;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                if (L.anchors[i].kind != IslandLayout.AnchorKind.Building) continue;
                bd = Mathf.Min(bd, f.RectDistance(L.anchors[i], x, z));
            }
            return bd;
        }
    }
}
#endif

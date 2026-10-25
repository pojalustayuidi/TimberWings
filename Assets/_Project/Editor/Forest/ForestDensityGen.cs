#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/ForestDensityGen.cs (Editor)
// Раскладка леса картой плотности (вторая половина класса ForestSettings). Старый режим рощиц остаётся в ForestSettings.cs,
// переключатель: ForestSettings.useDensityMap.
//
// Как это работает:
//   1. Карта плотности D(p) 0..1 = крупный шум (чаща ↔ редколесье) + естественные поляны (шум) + ручные маски
//      (чаща, редколесье, поляна, опушка: круги из списка masks и объекты-маркеры «ForestMask_Thicket / _Sparse / _Clearing / _Fringe»).
//   2. Вырезы (множитель 0..1, мягкий край): тропы, река, постройки, арка (маркер ForestGate: круг + коридор вглубь леса),
//      поляна лесопилки (круг: sawmillCenter/sawmillRadius, либо объект «ForestSawmill» в сцене: радиус = масштаб/2).
//   3. Слои высоты (layers): крупные, средние, подрост. Каждый слой: свои кандидаты (Poisson), свой отбор по плотности,
//      свой масштаб и своя минимальная дистанция. Расстояние между двумя стволами = сумма их «радиусов» (minDistance/2),
//      поэтому подрост втискивается между крупными, а крупные между собой стоят по своей дистанции.
//      В чаще дистанция сжимается (thicketSpacing), кроны перекрываются и земли почти не видно.
//   4. Виды пятнами: ячейки Вороного (размер standScale, края искривлены шумом); каждая ячейка = один вид.
//      standStrength = доля деревьев, взявших вид пятна (остальные жребием по весам).
//      speciesRules: какой вид в каких слоях живёт (дуб только крупный, ива только средняя и подрост).
//   5. Детерминизм: seed + положение зон. Порядок зон сортируется, у каждой пары (слой, биом, зона) свой System.Random.
//      Один seed = один лес. Деревья обычные объекты, префабы из ForestTrees/<Биом>. Одно действие Ctrl+Z.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public enum ForestMaskKind
{
    [InspectorName("Чаща")] Thicket = 0,
    [InspectorName("Редколесье")] Sparse = 1,
    [InspectorName("Поляна")] Clearing = 2,
    [InspectorName("Опушка")] Fringe = 3
}

public enum ForestShotTarget
{
    [InspectorName("Центр леса")] ForestCenter = 0,
    [InspectorName("Арка (ForestGate)")] Gate = 1,
    [InspectorName("Лесопилка")] Sawmill = 2,
    [InspectorName("Первая чаща")] Thicket = 3,
    [InspectorName("Центр Scene View")] SceneView = 4
}

// поле, которое видно в инспекторе только в режиме карты плотности
[AttributeUsage(AttributeTargets.Field)]
public sealed class DensityOnlyAttribute : Attribute { }

[Serializable]
public class ForestMask
{
    public ForestMaskKind kind = ForestMaskKind.Thicket;
    [Tooltip("Центр в мировых координатах: X и Z")]
    public Vector2 center;
    [Min(1f)] public float radius = 12f;
    [Tooltip("Ширина мягкого края внутрь радиуса, м")]
    [Min(0f)] public float feather = 5f;
    [Range(0f, 1f)] public float strength = 1f;
}

[Serializable]
public class ForestLayer
{
    public string name = "Слой";
    [Tooltip("Минимальная дистанция между стволами этого слоя, м (в чаще сжимается на thicketSpacing)")]
    [Min(0.3f)] public float minDistance = 3f;
    [Tooltip("Масштаб дерева в слое (поверх рантайм-размера 0.7 / 1 / 1.3 из GameBalance.TreeScaleMul)")]
    public Vector2 scale = new Vector2(0.9f, 1.15f);
    [Tooltip("Отбор по плотности: шанс = D^power × share. Больше 1 = слой живёт в чаще; меньше 1 = и в редколесье тоже")]
    [Min(0.1f)] public float densityPower = 1f;
    [Range(0f, 1f)] public float share = 1f;
    [Tooltip("Свободная полоса вдоль тропы, м: в неё не заходят стволы этого слоя (кроны крупных нависают над тропой)")]
    [Min(0f)] public float pathClearance = 1.5f;

    public ForestLayer() { }
    public ForestLayer(string name, float minDist, float sMin, float sMax, float power, float share, float pathClear)
    {
        this.name = name; minDistance = minDist; scale = new Vector2(sMin, sMax);
        densityPower = power; this.share = share; pathClearance = pathClear;
    }
}

[Serializable]
public class SpeciesRule
{
    [Tooltip("Имя семейства префабов: Tree_Oak, Willow_Dead, Tree_Autumn…")]
    public string family = "Tree_Oak";
    public bool large = true, medium = true, understory = true;
    [Tooltip("Множитель масштаба вида (дуб крупнее)")]
    [Range(0.5f, 1.6f)] public float scaleMul = 1f;
}

public struct ForestMaskInst
{
    public ForestMaskKind kind;
    public Vector3 c;
    public float r, feather, strength;
}

public partial class ForestSettings
{
    // ------------------------------------------------------------ режим
    [Header("Режим раскладки")]
    [Tooltip("true: карта плотности (чаща, редколесье, поляны, слои высоты). false: старый режим рощиц (поля ниже)")]
    public bool useDensityMap = true;

    // ------------------------------------------------------------ карта плотности
    [Header("Карта плотности: шум")] [DensityOnly]
    [Tooltip("Размер пятен чащи и редколесья, м (больше = крупнее и реже)")]
    [Min(8f)] public float densityNoiseScale = 42f;
    [DensityOnly] [Tooltip("Контраст шума: больше = резче граница между чащей и редколесьем")]
    [Range(0.5f, 4f)] public float noiseContrast = 1.7f;
    [DensityOnly] [Tooltip("Сдвиг шума: больше 0 = леса гуще в среднем, меньше 0 = реже")]
    [Range(-0.4f, 0.4f)] public float densityBias = 0.08f;
    [DensityOnly] [Tooltip("Плотность в редколесье (0..1)")]
    [Range(0f, 1f)] public float sparseDensity = 0.22f;
    [DensityOnly] [Tooltip("Плотность в чаще (0..1)")]
    [Range(0f, 1f)] public float thicketDensity = 1f;
    [DensityOnly] [Tooltip("Во сколько раз сжимается дистанция между стволами в самой чаще: 0.78 = кроны перекрываются, земли почти не видно")]
    [Range(0.4f, 1f)] public float thicketSpacing = 0.78f;
    [DensityOnly] [Tooltip("Деревья в чаще крупнее во столько раз (чаща читается сплошной шапкой)")]
    [Range(1f, 1.4f)] public float thicketScaleBoost = 1.1f;
    [DensityOnly] [Tooltip("Естественные поляны от шума: 0 = нет, 1 = очень много")]
    [Range(0f, 1f)] public float naturalGladeAmount = 0.22f;
    [DensityOnly] [Tooltip("Размер естественных полян, м")]
    [Min(6f)] public float naturalGladeScale = 24f;

    [Header("Карта плотности: ручные маски")] [DensityOnly]
    [Tooltip("Круги на карте: чаща, редколесье, поляна, опушка. Дополнительно подхватываются объекты сцены «ForestMask_Thicket/_Sparse/_Clearing/_Fringe» (радиус = масштаб/2)")]
    public List<ForestMask> masks = new List<ForestMask>();
    [DensityOnly] [Tooltip("Подхватывать объекты-маркеры ForestMask_* из сцены")]
    public bool useSceneMasks = true;
    [DensityOnly] [Tooltip("Подрост на опушке: прибавка к шансу, 0.5 = на 50% чаще")]
    [Range(0f, 2f)] public float fringeUnderstoryBoost = 0.6f;

    [Header("Вырезы: тропы, река, постройки")] [DensityOnly]
    [Tooltip("Длина мягкого края вдоль троп, м (сама полоса clearance задаётся в слое)")]
    [Min(0.1f)] public float pathFeather = 2.5f;
    [DensityOnly] [Min(0f)] public float riverClearance = 1.5f;
    [DensityOnly] [Min(0.1f)] public float riverFeather = 2f;
    [DensityOnly] [Min(0f)] public float buildingClearance = 2.5f;
    [DensityOnly] [Min(0.1f)] public float buildingFeather = 3f;

    [Header("Вырезы: арка и лесопилка")] [DensityOnly]
    [Tooltip("Радиус свободного круга вокруг маркера ForestGate, м")]
    [Min(0f)] public float gateRadius = 7f;
    [DensityOnly] [Tooltip("Длина коридора от арки вглубь леса, м (0 = только круг)")]
    [Min(0f)] public float gateCorridor = 9f;
    [DensityOnly] [Min(0.1f)] public float gateFeather = 3f;
    [DensityOnly] [Tooltip("Заглушка поляны лесопилки: круг без деревьев. Выключено = нет выреза (если в сцене есть объект «ForestSawmill», он включает вырез сам)")]
    public bool sawmillEnabled = false;
    [DensityOnly] [Tooltip("Центр в мировых координатах: X и Z")]
    public Vector2 sawmillCenter = Vector2.zero;
    [DensityOnly] [Min(1f)] public float sawmillRadius = 11f;
    [DensityOnly] [Min(0.1f)] public float sawmillFeather = 3f;

    [Header("Слои высоты")] [DensityOnly]
    [Tooltip("Первый слой = самые крупные деревья, последний = подрост. Слои сажаются по очереди, мелкие заполняют просветы между крупными")]
    public List<ForestLayer> layers = new List<ForestLayer>
    {
        new ForestLayer("Крупные", 4.0f, 1.15f, 1.45f, 1.3f, 0.9f, 2.6f),
        new ForestLayer("Средние", 3.0f, 0.90f, 1.15f, 0.9f, 1.0f, 1.8f),
        new ForestLayer("Подрост", 1.9f, 0.55f, 0.80f, 0.5f, 0.8f, 1.0f)
    };
    [DensityOnly] [Tooltip("Слой задаёт размер дерева в игре: крупные = старые, средние = зрелые, подрост = молодые (ResourceItem.overrideSize). Выключено = размер выбирает рантайм по плотности соседей")]
    public bool layerSetsTreeSize = false;
    [DensityOnly] [Tooltip("Радиус ствола уже стоящих в сцене деревьев для дистанции, м")]
    [Min(0.2f)] public float existingTreeRadius = 1.2f;

    [Header("Виды пятнами")] [DensityOnly]
    [Tooltip("Размер пятен одного вида задаёт standScale, доля деревьев пятна задаёт standStrength. Здесь: насколько границы пятен кривые")]
    [Range(0f, 0.8f)] public float patchWarp = 0.35f;
    [DensityOnly] [Tooltip("Правила видов по слоям. Вида нет в списке = живёт во всех слоях")]
    public List<SpeciesRule> speciesRules = new List<SpeciesRule>
    {
        new SpeciesRule { family = "Tree_Oak", large = true, medium = false, understory = false, scaleMul = 1.15f },
        new SpeciesRule { family = "Willow_Dead", large = false, medium = true, understory = true, scaleMul = 1f }
    };

    [Header("Проверка и скриншоты")] [DensityOnly]
    [Tooltip("Рисовать в Scene View круги масок, арки и лесопилки")]
    public bool drawGizmos = true;
    [DensityOnly] [Tooltip("Радиус кроны при масштабе 1, м: нужен только для отчёта о покрытии кронами")]
    [Min(0.5f)] public float crownRadius = 1.8f;
    [DensityOnly] public ForestShotTarget shotTarget = ForestShotTarget.ForestCenter;
    [DensityOnly] [Range(30f, 80f)] public float shotPitch = 55f;
    [DensityOnly] public float shotYaw = 45f;
    [DensityOnly] [Range(15f, 60f)] public float shotFov = 35f;
    [DensityOnly] [Tooltip("Расстояние камеры до точки взгляда, м")]
    [Min(5f)] public float shotDistance = 26f;

    // ------------------------------------------------------------ внутренние типы
    class BiomeSet
    {
        public ForestBiome biome; public int bi;
        public BiomeTrees bs;
        public List<ForestZone> zones;
        public Transform parent;
        public List<List<Fam>> famsByLayer = new List<List<Fam>>();
    }

    struct Sample { public float dens, thick, fringe, carve; }

    class DCtx
    {
        public ForestPlacementFilter filter;
        public List<ForestMaskInst> masks;
        public float zk = 1f, pk = 1f;     // масштаб вырезов арки (R/30 м) и полос вдоль троп (R/20 м) для маленького леса
        public bool hasGate; public Vector2 gate, gateEnd;
        public bool hasSaw; public Vector2 saw; public float sawR;
        public List<ForestGladeInst> glades;   // поляны рубки (A5): разрежение вокруг маркеров PAD_Forest, PAD_GroveSouth
        public float n1x, n1z, n2x, n2z, gx, gz, wx, wz;
        public float cell = 4f;
        public Dictionary<long, List<Vector3>> hash = new Dictionary<long, List<Vector3>>();   // x, z, радиус
        public List<Vector3> crowns = new List<Vector3>();                                       // x, z, радиус кроны
    }

    // ------------------------------------------------------------ вход
    int GenerateDensityMap(ForestBiome? only, Ctx c, StringBuilder report)
    {
        if (layers == null || layers.Count == 0) { report.Append("\nСлои высоты не заданы (список layers пуст)."); return 0; }

        var sets = new List<BiomeSet>();
        for (int bi = 0; bi < Biomes.Length; bi++)
        {
            ForestBiome b = Biomes[bi];
            if (only.HasValue && only.Value != b) continue;

            var zones = new List<ForestZone>();
            for (int i = 0; i < ForestZone.All.Count; i++)
            {
                var z = ForestZone.All[i];
                if (z != null && z.isActiveAndEnabled && z.biome == b && !z.IsNeutral && z.gameObject.scene.IsValid()) zones.Add(z);
            }
            if (zones.Count == 0) { report.Append("\n" + b + ": зона не найдена (Tools → Forest → Лес → «Добавить подзоны»)."); continue; }
            // стабильный порядок зон: от порядка OnEnable результат зависеть не должен
            zones.Sort((x, y) =>
            {
                Vector3 px = x.transform.position, py = y.transform.position;
                int r = Mathf.RoundToInt(px.x * 10f).CompareTo(Mathf.RoundToInt(py.x * 10f));
                if (r == 0) r = Mathf.RoundToInt(px.z * 10f).CompareTo(Mathf.RoundToInt(py.z * 10f));
                return r != 0 ? r : string.CompareOrdinal(x.name, y.name);
            });

            BiomeTrees bs = Get(b);
            var entries = new List<TreeEntry>();
            for (int i = 0; i < bs.prefabs.Count; i++)
            {
                var e = bs.prefabs[i];
                if (e == null || e.prefab == null || e.weight <= 0f) continue;
                if (PrefabUtility.GetPrefabAssetType(e.prefab) == PrefabAssetType.NotAPrefab) { report.Append("\n" + b + ": «" + e.prefab.name + "» не префаб-ассет, пропущен."); continue; }
                entries.Add(e);
            }
            if (entries.Count == 0) { report.Append("\n" + b + ": не задано ни одного префаба."); continue; }

            var set = new BiomeSet { biome = b, bi = bi, bs = bs, zones = zones, parent = Container(b, true) };
            var fams = BuildFamilies(entries);
            for (int li = 0; li < layers.Count; li++) set.famsByLayer.Add(FamsForLayer(fams, li));
            sets.Add(set);
        }
        if (sets.Count == 0) return 0;

        // контекст поля
        var d = new DCtx { filter = c.filter };
        d.n1x = c.noiseX; d.n1z = c.noiseZ;
        d.n2x = c.noiseX * 0.61f + 211.3f; d.n2z = c.noiseZ * 0.47f + 83.9f;
        d.gx = c.noiseX * 0.29f + 517.1f; d.gz = c.noiseZ * 0.83f + 301.7f;
        d.wx = c.noiseX * 0.37f + 91.7f; d.wz = c.noiseZ * 0.53f + 47.3f;
        d.masks = GatherMasks(sets[0].zones[0].transform.position.y);
        {
            var mz = ForestZones.Find(ForestBiome.Forest);
            float zr = 30f;
            if (mz != null) zr = mz.Map.shape == MapZone.Shape.Circle ? mz.Map.radius : Mathf.Min(mz.Map.size.x, mz.Map.size.y) * 0.5f;
            d.zk = Mathf.Clamp(zr / 30f, 0.35f, 1f);
            d.pk = Mathf.Clamp(zr / 20f, 0.55f, 1f);
        }
        PrepareCuts(d, sets);

        float maxRad = existingTreeRadius;
        for (int li = 0; li < layers.Count; li++) maxRad = Mathf.Max(maxRad, layers[li].minDistance * 0.5f);
        d.cell = maxRad * 2f + 0.05f;
        foreach (var kv in c.hash)
            for (int i = 0; i < kv.Value.Count; i++) AddR(d, kv.Value[i].x, kv.Value[i].y, existingTreeRadius);

        var perLayer = new int[layers.Count];
        var perBiome = new Dictionary<ForestBiome, int>();
        int placedAll = 0;
        bool capped = false;

        for (int li = 0; li < layers.Count && !capped; li++)
        {
            ForestLayer L = layers[li];
            float rCand = Mathf.Max(0.5f, L.minDistance * Mathf.Min(1f, thicketSpacing));
            for (int si = 0; si < sets.Count && !capped; si++)
            {
                BiomeSet set = sets[si];
                for (int zi = 0; zi < set.zones.Count && !capped; zi++)
                {
                    ForestZone zone = set.zones[zi];
                    EditorUtility.DisplayProgressBar("Лесные деревья", L.name + ": " + set.biome + " (" + zone.Map.zoneName + ")",
                        (li + (si + (zi + 1f) / set.zones.Count) / sets.Count) / layers.Count);

                    var rng = new System.Random(unchecked(seed * 31 + li * 1000003 + set.bi * 7919 + zi * 104729 + 17));
                    var pts = Poisson(ZoneRect(zone.Map), rCand, rng);
                    Shuffle(pts, rng);
                    float y0 = zone.transform.position.y;
                    List<Fam> fams = set.famsByLayer[li];
                    int placed = 0;

                    for (int k = 0; k < pts.Count; k++)
                    {
                        if (c.total >= maxTrees) { capped = true; break; }
                        Vector3 p = new Vector3(pts[k].x, y0, pts[k].y);
                        if (ForestZone.At(p) != zone) continue;

                        Sample sm = Eval(p, d);
                        if (sm.carve <= 0.001f) continue;
                        float wEdge = EdgeWeight(p);
                        float acc = Mathf.Pow(Mathf.Max(0.0001f, sm.dens), L.densityPower) * L.share * sm.carve * Mathf.Lerp(edgeDensityMin, 1f, wEdge);
                        if (li >= 2) acc *= 1f + fringeUnderstoryBoost * sm.fringe;
                        acc *= ObstacleFactor(L, p, d);
                        if (rng.NextDouble() > acc) continue;

                        float spacing = L.minDistance * Mathf.Lerp(1f, thicketSpacing, sm.thick);
                        float rad = spacing * 0.5f;
                        if (TooCloseR(d, p, rad)) continue;
                        if (!c.filter.CanSpawnAt(p, set.bs.trunkClearance)) continue;

                        // вид: пятно (Вороной) или жребий по весам
                        Fam fam = rng.NextDouble() < standStrength ? PatchFam(fams, p, d) : PickFam(fams, rng);
                        TreeEntry e = Pick(fam.items, fam.total, rng);
                        SpeciesRule rule = FindRule(fam.name);

                        float scale = Mathf.Lerp(L.scale.x, L.scale.y, (float)rng.NextDouble())
                                      * Mathf.Lerp(edgeScaleMin, 1f, wEdge)
                                      * Mathf.Lerp(1f, thicketScaleBoost, sm.thick)
                                      * Mathf.Lerp(1f, 0.88f, sm.fringe)
                                      * (rule != null ? rule.scaleMul : 1f);

                        if (!PlaceLayered(set, e, p, scale, li, rad, c, d, rng)) continue;
                        placed++; c.total++;
                    }
                    perLayer[li] += placed; placedAll += placed;
                    int prev; perBiome.TryGetValue(set.biome, out prev); perBiome[set.biome] = prev + placed;
                }
            }
        }

        // отчёт
        var lay = new StringBuilder();
        for (int li = 0; li < layers.Count; li++) lay.Append((li > 0 ? ", " : "") + layers[li].name + " " + perLayer[li]);
        report.Append("\nСлои: " + lay);
        foreach (var kv in perBiome) report.Append("\n" + kv.Key + ": " + kv.Value + " деревьев");
        if (capped) report.Append("\nДостигнут потолок maxTrees = " + maxTrees + ": лес обрезан, подними maxTrees.");
        report.Append("\n" + Coverage(d, sets));
        report.Append("\nМаски: " + d.masks.Count + (useSceneMasks ? " (с маркерами сцены)" : "") +
                      ", арка: " + (d.hasGate ? "есть" : "нет маркера ForestGate") +
                      ", лесопилка: " + (d.hasSaw ? "вырез R=" + d.sawR.ToString("0.#") + " м" : "выключена") +
                      ", поляны рубки: " + GladesReport(d));
        return placedAll;
    }

    // ------------------------------------------------------------ размещение
    bool PlaceLayered(BiomeSet set, TreeEntry e, Vector3 p, float scale, int li, float rad, Ctx c, DCtx d, System.Random rng)
    {
        Vector3 g;
        if (!c.filter.GroundPoint(p.x, p.z, out g)) g = new Vector3(p.x, p.y, p.z);
        else
        {
            Vector3 gx, gz;
            if (c.filter.GroundPoint(p.x + 1f, p.z, out gx) && c.filter.GroundPoint(p.x, p.z + 1f, out gz))
            {
                float slope = Mathf.Atan(Mathf.Max(Mathf.Abs(gx.y - g.y), Mathf.Abs(gz.y - g.y))) * Mathf.Rad2Deg;
                if (slope > maxSlope) return false;
            }
        }

        var go = PrefabUtility.InstantiatePrefab(e.prefab, set.parent) as GameObject;
        if (go == null) return false;
        Undo.RegisterCreatedObjectUndo(go, "Generate forest trees");

        float yaw = (float)rng.NextDouble() * 360f;
        float tx = ((float)rng.NextDouble() * 2f - 1f) * tiltMax;
        float tz = ((float)rng.NextDouble() * 2f - 1f) * tiltMax;
        go.transform.SetPositionAndRotation(g + Vector3.up * set.bs.yOffset, Quaternion.Euler(tx, yaw, tz) * e.prefab.transform.rotation);
        go.transform.localScale = e.prefab.transform.localScale * scale;

        if (layerSetsTreeSize)
        {
            var ri = go.GetComponentInChildren<ResourceItem>(true);
            if (ri != null)
            {
                ri.overrideSize = true;
                ri.forcedSize = li == 0 ? TreeSize.Old : (li >= layers.Count - 1 ? TreeSize.Young : TreeSize.Mature);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ri);
            }
        }

        AddR(d, g.x, g.z, rad);
        d.crowns.Add(new Vector3(g.x, g.z, crownRadius * scale));
        return true;
    }

    // ------------------------------------------------------------ карта плотности
    Sample Eval(Vector3 p, DCtx d)
    {
        var s = new Sample { carve = 1f };
        float n = Mathf.PerlinNoise(p.x / densityNoiseScale + d.n1x, p.z / densityNoiseScale + d.n1z) * 0.7f
                + Mathf.PerlinNoise(p.x / (densityNoiseScale * 0.37f) + d.n2x, p.z / (densityNoiseScale * 0.37f) + d.n2z) * 0.3f;
        n = Mathf.Clamp01((n - 0.5f) * noiseContrast + 0.5f + densityBias);
        float dens = Mathf.Lerp(sparseDensity, thicketDensity, Smooth01(0.25f, 0.75f, n));

        if (naturalGladeAmount > 0f)
        {
            float g = Mathf.PerlinNoise(p.x / naturalGladeScale + d.gx, p.z / naturalGladeScale + d.gz);
            float thr = Mathf.Lerp(0.12f, 0.42f, naturalGladeAmount);
            s.carve *= Smooth01(thr - 0.03f, thr + 0.07f, g);
        }

        float boost = 0f, fringe = 0f;
        for (int i = 0; i < d.masks.Count; i++)
        {
            ForestMaskInst m = d.masks[i];
            float dx = p.x - m.c.x, dz = p.z - m.c.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist >= m.r) continue;
            float k = (1f - Smooth01(Mathf.Max(0f, m.r - m.feather), m.r, dist)) * m.strength;
            switch (m.kind)
            {
                case ForestMaskKind.Thicket: dens = Mathf.Lerp(dens, 1f, k); boost = Mathf.Max(boost, k); break;
                case ForestMaskKind.Sparse: dens *= Mathf.Lerp(1f, 0.3f, k); break;
                case ForestMaskKind.Clearing: s.carve *= 1f - k; break;
                case ForestMaskKind.Fringe: dens *= Mathf.Lerp(1f, 0.5f, k); fringe = Mathf.Max(fringe, k); break;
            }
        }

        // коридор: за свободной полосой вдоль троп (pathClearance) лес гуще, тропа идёт «стеной» деревьев
        if (forestTrailWallBoost > 0.001f && layers.Count > 0)
        {
            float pd = d.filter.PathDistance(p);
            if (pd < 1e5f)
            {
                float c0 = layers[0].pathClearance * d.pk;
                float wk = (1f - Smooth01(c0, c0 + forestTrailWallBand * d.pk, pd)) * forestTrailWallBoost;
                if (wk > 0f) { dens = Mathf.Lerp(dens, 1f, wk); boost = Mathf.Max(boost, wk * 0.6f); }
            }
        }

        Vector2 p2 = new Vector2(p.x, p.z);
        if (d.hasGate)
        {
            float gr = gateRadius * d.zk, gf = gateFeather * d.zk;
            s.carve *= Smooth01(gr - gf, gr, (p2 - d.gate).magnitude);
            if (gateCorridor > 0.5f)
            {
                float rc = gr * 0.7f;
                s.carve *= Smooth01(rc - gf, rc, SegDist(p2, d.gate, d.gateEnd));
            }
        }
        if (d.hasSaw) s.carve *= Smooth01(d.sawR - Mathf.Min(sawmillFeather, d.sawR * 0.35f), d.sawR, (p2 - d.saw).magnitude);

        // поляны рубки (A5): вокруг маркера лес разрежен (не убран: деревья нужны под топор), в центре свободное место, где стоит игрок
        if (d.glades != null)
            for (int gi = 0; gi < d.glades.Count; gi++)
            {
                ForestGladeInst gl = d.glades[gi];
                float gd = (p2 - gl.c).magnitude;
                if (gd >= gl.r) continue;
                float gk = 1f - Smooth01(Mathf.Max(0f, gl.r - gl.feather), gl.r, gd);
                dens *= Mathf.Lerp(1f, gl.thin, gk);
                boost *= 1f - gk;
                s.carve *= Smooth01(gl.stand * 0.6f, gl.stand, gd);
            }

        s.dens = Mathf.Clamp01(dens);
        s.fringe = fringe;
        s.thick = Mathf.Max(Smooth01(0.6f, 0.95f, s.dens), boost);
        return s;
    }

    // тропы, река, постройки: мягкий вырез по расстоянию от краёв (маска ForestPlacementFilter)
    float ObstacleFactor(ForestLayer L, Vector3 p, DCtx d)
    {
        float pc = L.pathClearance * d.pk;
        float f = Smooth01(pc, pc + pathFeather * d.pk, d.filter.PathDistance(p));
        if (f <= 0f) return 0f;
        f *= Smooth01(riverClearance, riverClearance + riverFeather, d.filter.RiverDistance(p));
        f *= Smooth01(buildingClearance, buildingClearance + buildingFeather, d.filter.BuildingDistance(p));
        return f;
    }

    static float Smooth01(float a, float b, float x)
    {
        if (b <= a) return x >= b ? 1f : 0f;
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 1e-6f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return (p - (a + ab * t)).magnitude;
    }

    // ------------------------------------------------------------ маски, арка, лесопилка
    public List<ForestMaskInst> GatherMasks(float refY)
    {
        var res = new List<ForestMaskInst>();
        if (masks != null)
            for (int i = 0; i < masks.Count; i++)
            {
                var m = masks[i];
                if (m == null || m.radius <= 0.1f) continue;
                res.Add(new ForestMaskInst { kind = m.kind, c = new Vector3(m.center.x, refY, m.center.y), r = m.radius, feather = Mathf.Min(m.feather, m.radius), strength = m.strength });
            }
        if (useSceneMasks)
        {
            var found = new List<ForestMaskInst>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || !t.gameObject.scene.IsValid() || !t.name.StartsWith("ForestMask_", StringComparison.OrdinalIgnoreCase)) continue;
                string n = t.name.Substring("ForestMask_".Length).ToLowerInvariant();
                ForestMaskKind k;
                if (n.StartsWith("thicket")) k = ForestMaskKind.Thicket;
                else if (n.StartsWith("sparse")) k = ForestMaskKind.Sparse;
                else if (n.StartsWith("clearing")) k = ForestMaskKind.Clearing;
                else if (n.StartsWith("fringe")) k = ForestMaskKind.Fringe;
                else continue;
                float r = Mathf.Max(t.lossyScale.x, t.lossyScale.z) * 0.5f;
                if (r < 0.5f) continue;
                found.Add(new ForestMaskInst { kind = k, c = t.position, r = r, feather = r * 0.35f, strength = 1f });
            }
            // детерминированный порядок: от порядка объектов в сцене результат зависеть не должен
            found.Sort((a, b) =>
            {
                int r = Mathf.RoundToInt(a.c.x * 10f).CompareTo(Mathf.RoundToInt(b.c.x * 10f));
                if (r == 0) r = Mathf.RoundToInt(a.c.z * 10f).CompareTo(Mathf.RoundToInt(b.c.z * 10f));
                return r != 0 ? r : ((int)a.kind).CompareTo((int)b.kind);
            });
            res.AddRange(found);
        }
        AddIslandMeadows(res, refY);   // поляны острова v2 (лагерь, «Дедушка», поляна и берег Гуннара, участок B): в лесу тоже без деревьев
        return res;
    }

    void PrepareCuts(DCtx d, List<BiomeSet> sets)
    {
        d.glades = GatherGlades();
        var gate = GameObject.Find("ForestGate");
        if (gate != null && gateRadius > 0.1f)
        {
            d.hasGate = true;
            d.gate = new Vector2(gate.transform.position.x, gate.transform.position.z);
            // коридор ведёт к центру главной зоны леса (или первой найденной)
            Vector3 target = sets[0].zones[0].transform.position;
            var master = ForestZones.Find(ForestBiome.Forest);
            if (master != null) target = master.transform.position;
            Vector2 dir = new Vector2(target.x, target.z) - d.gate;
            dir = dir.sqrMagnitude < 1f ? Vector2.up : dir.normalized;
            d.gateEnd = d.gate + dir * gateCorridor * d.zk;
        }

        var saw = GameObject.Find("ForestSawmill");
        if (saw != null)
        {
            d.hasSaw = true;
            d.saw = new Vector2(saw.transform.position.x, saw.transform.position.z);
            float r = Mathf.Max(saw.transform.lossyScale.x, saw.transform.lossyScale.z) * 0.5f;
            d.sawR = r >= 1f ? r : sawmillRadius;
        }
        else if (sawmillEnabled)
        {
            d.hasSaw = true; d.saw = sawmillCenter; d.sawR = sawmillRadius;
        }
    }

    // ------------------------------------------------------------ виды: слои и пятна
    SpeciesRule FindRule(string family)
    {
        if (speciesRules == null) return null;
        for (int i = 0; i < speciesRules.Count; i++)
            if (speciesRules[i] != null && string.Equals(speciesRules[i].family, family, StringComparison.OrdinalIgnoreCase)) return speciesRules[i];
        return null;
    }

    List<Fam> FamsForLayer(List<Fam> fams, int li)
    {
        var res = new List<Fam>();
        for (int i = 0; i < fams.Count; i++)
        {
            SpeciesRule r = FindRule(fams[i].name);
            bool ok = r == null || (li == 0 ? r.large : (li == 1 ? r.medium : r.understory));
            if (ok) res.Add(fams[i]);
        }
        return res.Count > 0 ? res : fams;   // правила выключили всё: лучше ошибиться видом, чем оставить слой пустым
    }

    // вид пятна: ячейки Вороного, границы искривлены шумом; каждой ячейке свой вид по весам
    Fam PatchFam(List<Fam> fams, Vector3 p, DCtx d)
    {
        if (fams.Count == 1) return fams[0];
        float s = Mathf.Max(8f, standScale);
        float wx = (Mathf.PerlinNoise(p.x / (s * 0.8f) + d.wx, p.z / (s * 0.8f) + d.wz) - 0.5f) * 2f * patchWarp * s;
        float wz = (Mathf.PerlinNoise(p.x / (s * 0.8f) + d.wx + 31.7f, p.z / (s * 0.8f) + d.wz + 17.3f) - 0.5f) * 2f * patchWarp * s;
        float qx = (p.x + wx) / s, qz = (p.z + wz) / s;
        int ix = Mathf.FloorToInt(qx), iz = Mathf.FloorToInt(qz);
        float best = float.MaxValue; int bx = ix, bz = iz;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx, cz = iz + dz;
                float fx = cx + 0.15f + 0.7f * Hash01(cx, cz, 1);
                float fz = cz + 0.15f + 0.7f * Hash01(cx, cz, 2);
                float dd = (fx - qx) * (fx - qx) + (fz - qz) * (fz - qz);
                if (dd < best) { best = dd; bx = cx; bz = cz; }
            }
        float total = 0f;
        for (int i = 0; i < fams.Count; i++) total += fams[i].total;
        float v = Hash01(bx, bz, 3) * total;
        for (int i = 0; i < fams.Count; i++) { v -= fams[i].total; if (v <= 0f) return fams[i]; }
        return fams[fams.Count - 1];
    }

    float Hash01(int x, int z, int salt)
    {
        unchecked
        {
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)salt * 83492791u ^ (uint)seed * 2654435761u;
            h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0x1000000;
        }
    }

    static void Shuffle(List<Vector2> l, System.Random rng)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2 t = l[i]; l[i] = l[j]; l[j] = t;
        }
    }

    // ------------------------------------------------------------ дистанция между стволами с радиусами
    static long KeyR(DCtx d, int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

    void AddR(DCtx d, float x, float z, float rad)
    {
        long k = KeyR(d, Mathf.FloorToInt(x / d.cell), Mathf.FloorToInt(z / d.cell));
        List<Vector3> l;
        if (!d.hash.TryGetValue(k, out l)) { l = new List<Vector3>(4); d.hash[k] = l; }
        l.Add(new Vector3(x, z, rad));
    }

    bool TooCloseR(DCtx d, Vector3 p, float rad)
    {
        int cx = Mathf.FloorToInt(p.x / d.cell), cz = Mathf.FloorToInt(p.z / d.cell);
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                List<Vector3> l;
                if (!d.hash.TryGetValue(KeyR(d, cx + dx, cz + dz), out l)) continue;
                for (int i = 0; i < l.Count; i++)
                {
                    float ex = l[i].x - p.x, ez = l[i].y - p.z, need = rad + l[i].z;
                    if (ex * ex + ez * ez < need * need) return true;
                }
            }
        return false;
    }

    // ------------------------------------------------------------ проверка: покрытие кронами
    // Сетка 1.5 м по зонам леса. Считаем только там, где лес должен быть (не поляна, не тропа, не кромка).
    // Чаща должна давать 85–95% и выше, редколесье заметно меньше: по этим числам видно, что карта плотности работает
    string Coverage(DCtx d, List<BiomeSet> sets)
    {
        const float cs = 6f;
        var grid = new Dictionary<long, List<Vector3>>();
        for (int i = 0; i < d.crowns.Count; i++)
        {
            Vector3 cr = d.crowns[i];
            long k = ((long)Mathf.FloorToInt(cr.x / cs) << 32) ^ (uint)Mathf.FloorToInt(cr.y / cs);
            List<Vector3> l;
            if (!grid.TryGetValue(k, out l)) { l = new List<Vector3>(); grid[k] = l; }
            l.Add(cr);
        }
        int thickN = 0, thickC = 0, sparseN = 0, sparseC = 0, allN = 0, allC = 0;
        for (int si = 0; si < sets.Count; si++)
            for (int zi = 0; zi < sets[si].zones.Count; zi++)
            {
                ForestZone z = sets[si].zones[zi];
                Rect a = ZoneRect(z.Map);
                for (float x = a.xMin; x < a.xMax; x += 1.5f)
                    for (float zz = a.yMin; zz < a.yMax; zz += 1.5f)
                    {
                        Vector3 p = new Vector3(x, z.transform.position.y, zz);
                        if (ForestZone.At(p) != z) continue;
                        if (EdgeWeight(p) < 0.8f) continue;
                        Sample sm = Eval(p, d);
                        if (sm.carve < 0.95f) continue;
                        if (d.filter.PathDistance(p) < 3f || d.filter.RiverDistance(p) < 3f || d.filter.BuildingDistance(p) < 4f) continue;
                        bool cov = false;
                        int cx = Mathf.FloorToInt(x / cs), cz = Mathf.FloorToInt(zz / cs);
                        for (int dz = -1; dz <= 1 && !cov; dz++)
                            for (int dx = -1; dx <= 1 && !cov; dx++)
                            {
                                List<Vector3> l;
                                if (!grid.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out l)) continue;
                                for (int i = 0; i < l.Count; i++)
                                {
                                    float ex = l[i].x - x, ez = l[i].y - zz;
                                    if (ex * ex + ez * ez < l[i].z * l[i].z) { cov = true; break; }
                                }
                            }
                        allN++; if (cov) allC++;
                        if (sm.thick > 0.6f) { thickN++; if (cov) thickC++; }
                        else if (sm.thick < 0.25f) { sparseN++; if (cov) sparseC++; }
                    }
            }
        Func<int, int, string> pct = (n, c) => n == 0 ? "нет участков" : Mathf.RoundToInt(100f * c / n) + "%";
        return "Покрытие кронами (оценка при crownRadius " + crownRadius.ToString("0.0") + " м, без рантайм-размера): чаща " + pct(thickN, thickC) +
               ", редколесье " + pct(sparseN, sparseC) + ", всего " + pct(allN, allC);
    }
}

// ---------------------------------------------------------------- инспектор: прячет поля чужого режима
[CustomEditor(typeof(ForestSettings))]
public class ForestSettingsEditor : Editor
{
    static readonly string[] GroveOnly =
    {
        "groveMin", "groveMax", "groveRadius", "gladeWidth", "clearingAmount", "clearingScale", "groveUniformity", "groveLeaderScale"
    };
    static string[] densityOnly;

    static string[] DensityOnlyNames()
    {
        if (densityOnly != null) return densityOnly;
        var l = new List<string>();
        foreach (var f in typeof(ForestSettings).GetFields())
            if (f.IsDefined(typeof(DensityOnlyAttribute), false)) l.Add(f.Name);
        densityOnly = l.ToArray();
        return densityOnly;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var mode = serializedObject.FindProperty("useDensityMap");
        EditorGUILayout.PropertyField(mode);
        EditorGUILayout.HelpBox(mode.boolValue
            ? "Карта плотности: шум + маски, вырезы под тропы/арку/лесопилку, слои высоты, виды пятнами. Поля рощиц скрыты."
            : "Старый режим: рощицы (Poisson). Поля карты плотности скрыты.", MessageType.None);

        var skip = new List<string> { "m_Script", "useDensityMap" };
        skip.AddRange(mode.boolValue ? (IEnumerable<string>)GroveOnly : DensityOnlyNames());
        DrawPropertiesExcluding(serializedObject, skip.ToArray());
        serializedObject.ApplyModifiedProperties();
    }
}

// ---------------------------------------------------------------- маркеры масок и круги в Scene View
[InitializeOnLoad]
public static class ForestMaskTools
{
    static double lastScan;
    static List<ForestMaskInst> cache = new List<ForestMaskInst>();
    static Vector3 gate; static bool hasGate;
    static Vector3 saw; static float sawR; static bool hasSaw;
    static List<ForestGladeInst> gladeCache = new List<ForestGladeInst>();

    static ForestMaskTools() { SceneView.duringSceneGui += OnScene; }

    static string Suffix(ForestMaskKind k)
    {
        switch (k)
        {
            case ForestMaskKind.Sparse: return "Sparse";
            case ForestMaskKind.Clearing: return "Clearing";
            case ForestMaskKind.Fringe: return "Fringe";
            default: return "Thicket";
        }
    }

    // маркер в центре Scene View: двигай и масштабируй (радиус = масштаб/2), генератор подхватит сам
    public static string CreateMarker(ForestMaskKind kind, float radius)
    {
        var root = GameObject.Find("ForestMasks");
        if (root == null)
        {
            root = new GameObject("ForestMasks");
            Undo.RegisterCreatedObjectUndo(root, "Create ForestMasks");
        }
        var go = new GameObject("ForestMask_" + Suffix(kind));
        Undo.RegisterCreatedObjectUndo(go, "Create forest mask");
        go.transform.SetParent(root.transform, false);
        var sv = SceneView.lastActiveSceneView;
        Vector3 pos = sv != null ? sv.pivot : Vector3.zero;
        var master = ForestZones.Find(ForestBiome.Forest);
        if (master != null) pos.y = master.transform.position.y;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
        Selection.activeGameObject = go;
        return "Создана маска «" + go.name + "» R=" + radius + " м. Двигай и масштабируй объект: радиус = масштаб/2.";
    }

    static Color KindColor(ForestMaskKind k)
    {
        switch (k)
        {
            case ForestMaskKind.Sparse: return new Color(1f, 0.85f, 0.2f);
            case ForestMaskKind.Clearing: return new Color(0.4f, 0.9f, 1f);
            case ForestMaskKind.Fringe: return new Color(1f, 0.55f, 0.15f);
            default: return new Color(0.1f, 0.7f, 0.2f);
        }
    }

    static void OnScene(SceneView sv)
    {
        if (Event.current.type != EventType.Repaint) return;
        var s = AssetDatabase.LoadAssetAtPath<ForestSettings>(ForestSettings.AssetPath);
        if (s == null || !s.useDensityMap || !s.drawGizmos) return;

        if (EditorApplication.timeSinceStartup - lastScan > 0.7)
        {
            lastScan = EditorApplication.timeSinceStartup;
            var master = ForestZones.Find(ForestBiome.Forest);
            cache = s.GatherMasks(master != null ? master.transform.position.y : 0f);
            gladeCache = s.GatherGlades();
            var g = GameObject.Find("ForestGate");
            hasGate = g != null; if (hasGate) gate = g.transform.position;
            var w = GameObject.Find("ForestSawmill");
            if (w != null) { hasSaw = true; saw = w.transform.position; sawR = Mathf.Max(w.transform.lossyScale.x, w.transform.lossyScale.z) * 0.5f; if (sawR < 1f) sawR = s.sawmillRadius; }
            else if (s.sawmillEnabled) { hasSaw = true; saw = new Vector3(s.sawmillCenter.x, master != null ? master.transform.position.y : 0f, s.sawmillCenter.y); sawR = s.sawmillRadius; }
            else hasSaw = false;
        }

        for (int i = 0; i < cache.Count; i++)
        {
            var m = cache[i];
            Handles.color = KindColor(m.kind);
            Handles.DrawWireDisc(m.c, Vector3.up, m.r);
            Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.5f);
            Handles.DrawWireDisc(m.c, Vector3.up, Mathf.Max(0.1f, m.r - m.feather));
            Handles.Label(m.c + Vector3.up * 0.5f, m.kind == ForestMaskKind.Thicket ? "чаща" : m.kind == ForestMaskKind.Sparse ? "редколесье" : m.kind == ForestMaskKind.Clearing ? "поляна" : "опушка");
        }
        if (hasGate)
        {
            Handles.color = new Color(0.3f, 0.5f, 1f);
            Handles.DrawWireDisc(gate, Vector3.up, s.gateRadius);
            Handles.Label(gate + Vector3.up * 0.5f, "арка: вырез");
        }
        if (hasSaw)
        {
            Handles.color = new Color(0.7f, 0.45f, 0.2f);
            Handles.DrawWireDisc(saw, Vector3.up, sawR);
            Handles.Label(saw + Vector3.up * 0.5f, "лесопилка: вырез");
        }
        for (int i = 0; i < gladeCache.Count; i++)
        {
            var gl = gladeCache[i];
            Vector3 gc = new Vector3(gl.c.x, gl.y, gl.c.y);
            Handles.color = new Color(0.95f, 0.75f, 0.25f);
            Handles.DrawWireDisc(gc, Vector3.up, gl.r);
            Handles.color = new Color(0.95f, 0.75f, 0.25f, 0.5f);
            Handles.DrawWireDisc(gc, Vector3.up, Mathf.Max(0.1f, gl.r - gl.feather));
            Handles.DrawWireDisc(gc, Vector3.up, gl.stand);
            Handles.Label(gc + Vector3.up * 0.5f, "поляна рубки: " + gl.name);
        }
    }
}
#endif

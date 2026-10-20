#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestSettings.cs (Editor)
// Настройки и генерация деревьев леса. Это ассет (Assets/_Project/Editor/Forest/ForestSettings.asset), а не компонент в сцене.
// Запускается из окна Tools → Forest → Лес. Деревья кладутся в ForestTrees/<Биом>; ForestTrees/Manual генератор не трогает.
//
//   Два режима: карта плотности (useDensityMap, ForestDensityGen.cs) и старые рощицы (ниже).
//   Рощицы (Poisson disc) вместо шума, просветы gladeWidth, крупные поляны (clearingAmount), куртины видов (standScale),
//   опушка (вес края из ForestZone.Weight), фильтр ForestPlacementFilter (тропы, вода, постройки, земля), уклон maxSlope.
//   Вся генерация = одно действие Ctrl+Z. Детерминирована: тот же seed и те же зоны дают тот же лес.
// Пивот префаба должен стоять у основания ствола.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public class TreeEntry
{
    public GameObject prefab;
    [Tooltip("Относительная частота: 2 встречается вдвое чаще, чем 1")]
    [Min(0f)] public float weight = 1f;
}

[Serializable]
public class BiomeTrees
{
    [Tooltip("Префабы деревьев этого биома (обычные, берёзы, кедры, осенние, сухие ивы…)")]
    public List<TreeEntry> prefabs = new List<TreeEntry>();
    [Tooltip("Деревьев на 100 м² там, где они растут (в рощицах). Реальнее меньше: поляны и опушка прореживают")]
    [Min(0f)] public float density = 5f;
    [Tooltip("Минимальное расстояние между стволами, м")]
    [Min(0.3f)] public float minDistance = 2.4f;
    [Tooltip("Радиус ствола для фильтра троп, реки и построек, м (дерево не встанет ближе)")]
    [Min(0f)] public float trunkClearance = 0.6f;
    [Tooltip("Сдвиг по высоте от земли, м (если пивот префаба не у основания)")]
    public float yOffset = 0f;

    public BiomeTrees() { }
    public BiomeTrees(float density, float minDistance) { this.density = density; this.minDistance = minDistance; }
}

public partial class ForestSettings : ScriptableObject
{
    [Header("Деревья по зонам")]
    public BiomeTrees forest = new BiomeTrees(4.5f, 2.4f);
    public BiomeTrees cedar = new BiomeTrees(6.5f, 2.0f);
    public BiomeTrees birch = new BiomeTrees(3.5f, 2.6f);
    public BiomeTrees autumn = new BiomeTrees(4.5f, 2.4f);

    [Header("Рощицы и поляны")]
    [Min(1)] public int groveMin = 2;
    [Min(1)] public int groveMax = 4;
    [Tooltip("Радиус рощицы, м: в нём деревья группы")]
    [Min(0.5f)] public float groveRadius = 2.2f;
    [Tooltip("Минимальная ширина свободного прохода между рощицами, м")]
    [Min(0f)] public float gladeWidth = 3.5f;
    [Tooltip("Крупные поляны: 0 = нет, 1 = очень много")]
    [Range(0f, 1f)] public float clearingAmount = 0.35f;
    [Tooltip("Размер пятен-полян, м (больше = реже и крупнее)")]
    [Min(5f)] public float clearingScale = 28f;
    [Tooltip("Доля деревьев в рощице того же вида, что и главное дерево рощицы")]
    [Range(0f, 1f)] public float groveUniformity = 0.8f;
    [Tooltip("Размер куртин, м: пятен, где преобладает один вид дерева. Больше = реже и крупнее")]
    [Min(8f)] public float standScale = 32f;
    [Tooltip("0 = виды в рощицах выбираются жребием, 1 = рощица всегда берёт вид куртины")]
    [Range(0f, 1f)] public float standStrength = 0.75f;
    [Tooltip("Главное дерево рощицы крупнее (остальные чуть мельче): рощица читается группой, а не горстью одинаковых")]
    [Range(1f, 1.5f)] public float groveLeaderScale = 1.12f;

    [Header("Состав видов")]
    [Tooltip("true: при каждой генерации списки префабов перезаписываются пресетом (3 вариации × 5 видов с весами по биомам). Выключи, чтобы задавать списки руками; пустые списки заполняются всегда")]
    public bool useSpeciesPreset = true;

    [Header("Опушка (у внешней границы леса)")]
    [Tooltip("0 = ширина опушки равна feather зоны (z.Weight). Больше 0 = своя ширина полосы, м")]
    [Min(0f)] public float edgeWidth = 0f;
    [Tooltip("Доля деревьев, оставшаяся на самой кромке")]
    [Range(0f, 1f)] public float edgeDensityMin = 0.2f;
    [Tooltip("Масштаб деревьев на самой кромке (в глубине леса 1)")]
    [Range(0.3f, 1f)] public float edgeScaleMin = 0.75f;

    [Header("Вариация каждого дерева")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.2f);
    [Tooltip("Наклон по X и Z: от -tiltMax до +tiltMax, градусов")]
    [Range(0f, 15f)] public float tiltMax = 3f;

    [Header("Правила")]
    [Range(5f, 80f)] public float maxSlope = 35f;
    [Tooltip("Не ставить ближе minDistance к уже стоящим в сцене деревьям (ResourceItem вне контейнеров)")]
    public bool avoidExistingTrees = true;
    [Tooltip("Перед генерацией очищать папки ForestTrees/<Биом> (папка Manual не трогается никогда)")]
    public bool clearBeforeGenerate = true;
    [Tooltip("Потолок деревьев за один запуск (защита от ошибки в плотности)")]
    [Min(100)] public int maxTrees = 8000;
    public int seed = 12345;

    public static readonly ForestBiome[] Biomes = { ForestBiome.Forest, ForestBiome.Cedar, ForestBiome.Birch, ForestBiome.Autumn };

    public BiomeTrees Get(ForestBiome b)
    {
        switch (b)
        {
            case ForestBiome.Cedar: return cedar;
            case ForestBiome.Birch: return birch;
            case ForestBiome.Autumn: return autumn;
            default: return forest;
        }
    }

    public static string ContainerName(ForestBiome b) { return b.ToString(); }

    // ------------------------------------------------------------ ассет настроек
    public const string AssetDir = "Assets/_Project/Editor/Forest";
    public const string AssetPath = AssetDir + "/ForestSettings.asset";

    public static ForestSettings Load()
    {
        var s = AssetDatabase.LoadAssetAtPath<ForestSettings>(AssetPath);
        if (s != null) return s;
        if (!AssetDatabase.IsValidFolder(AssetDir))
        {
            string cur = "Assets";
            foreach (var part in AssetDir.Substring("Assets/".Length).Split('/'))
            {
                string next = cur + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, part);
                cur = next;
            }
        }
        s = CreateInstance<ForestSettings>();
        AssetDatabase.CreateAsset(s, AssetPath);
        AssetDatabase.SaveAssets();
        return s;
    }

    // ------------------------------------------------------------ папки деревьев в сцене: ForestTrees/<Биом>
    public Transform Container(ForestBiome b, bool create)
    {
        Transform root = ForestZones.TreesRoot(create);
        if (root == null) return null;
        Transform t = root.Find(ContainerName(b));
        if (t != null || !create) return t;
        var go = new GameObject(ContainerName(b));
        Undo.RegisterCreatedObjectUndo(go, "Create tree container");
        go.transform.SetParent(root, false);
        return go.transform;
    }

    public int CountTrees(ForestBiome b)
    {
        var c = Container(b, false);
        return c == null ? 0 : c.childCount;
    }

    // удалить деревья папок биомов (папки остаются); only = null: все четыре. Manual не трогается
    public int Clear(ForestBiome? only)
    {
        int removed = 0;
        foreach (var b in Biomes)
        {
            if (only.HasValue && only.Value != b) continue;
            var c = Container(b, false);
            if (c == null) continue;
            for (int i = c.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(c.GetChild(i).gameObject);
                removed++;
            }
        }
        return removed;
    }

    // ------------------------------------------------------------ состав видов
    const string SpeciesDir = "Assets/_Project/Prefabs/Trees/Generated";

    static readonly Dictionary<ForestBiome, KeyValuePair<string, float>[]> Mix = new Dictionary<ForestBiome, KeyValuePair<string, float>[]>
    {
        { ForestBiome.Forest, new[] { P("Tree_Random", 0.38f), P("Tree_Cedar", 0.25f), P("Tree_Birch", 0.22f), P("Willow_Dead", 0.04f), P("Tree_Autumn", 0.04f), P("Tree_Oak", 0.04f) } },
        { ForestBiome.Cedar,  new[] { P("Tree_Cedar", 0.85f), P("Willow_Dead", 0.10f), P("Tree_Random", 0.05f) } },
        { ForestBiome.Birch,  new[] { P("Tree_Birch", 0.70f), P("Tree_Random", 0.25f), P("Willow_Dead", 0.05f) } },
        { ForestBiome.Autumn, new[] { P("Tree_Autumn", 0.60f), P("Tree_Birch", 0.15f), P("Willow_Dead", 0.10f), P("Tree_Oak", 0.15f) } }
    };
    static KeyValuePair<string, float> P(string family, float w) { return new KeyValuePair<string, float>(family, w); }

    bool NeedsSpecies()
    {
        foreach (var b in Biomes)
        {
            var bs = Get(b);
            bool any = false;
            for (int i = 0; i < bs.prefabs.Count; i++) if (bs.prefabs[i] != null && bs.prefabs[i].prefab != null) { any = true; break; }
            if (!any) return true;
        }
        return false;
    }

    static Dictionary<string, List<GameObject>> LoadFamilies()
    {
        var res = new Dictionary<string, List<GameObject>>();
        if (!AssetDatabase.IsValidFolder(SpeciesDir)) return res;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SpeciesDir }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null || go.name.StartsWith("Stump_")) continue;
            string fam = FamilyName(go);
            List<GameObject> l;
            if (!res.TryGetValue(fam, out l)) { l = new List<GameObject>(); res[fam] = l; }
            l.Add(go);
        }
        foreach (var l in res.Values) l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return res;
    }

    // списки префабов по биомам из Prefabs/Trees/Generated (если их нет, собираются TreeSpeciesBuilder.BuildAll)
    public string ApplySpeciesPreset()
    {
        var fams = LoadFamilies();
        if (fams.Count == 0)
        {
            TreeSpeciesBuilder.BuildAll();
            fams = LoadFamilies();
            if (fams.Count == 0) return "Префабов деревьев нет в " + SpeciesDir + ": запусти Tools → Forest → Trees → Build Species.";
        }
        Undo.RecordObject(this, "Apply species preset");
        var rep = new System.Text.StringBuilder("Состав видов:");
        foreach (var b in Biomes)
        {
            var bs = Get(b);
            bs.prefabs.Clear();
            var parts = new List<string>();
            foreach (var kv in Mix[b])
            {
                List<GameObject> l;
                if (!fams.TryGetValue(kv.Key, out l) || l.Count == 0) continue;
                for (int i = 0; i < l.Count; i++) bs.prefabs.Add(new TreeEntry { prefab = l[i], weight = kv.Value / l.Count });
                parts.Add(kv.Key.Replace("Tree_", "") + " " + Mathf.RoundToInt(kv.Value * 100f) + "%");
            }
            rep.Append("\n" + b + ": " + (parts.Count == 0 ? "ничего не найдено" : string.Join(", ", parts)));
        }
        EditorUtility.SetDirty(this);
        return rep.ToString();
    }

    // ------------------------------------------------------------ генерация
    class Ctx
    {
        public ForestPlacementFilter filter;
        public System.Random rng;
        public Dictionary<long, List<Vector2>> hash = new Dictionary<long, List<Vector2>>();
        public float cell = 4f;
        public float noiseX, noiseZ;
        public int total;
    }

    // only = null: все четыре зоны. Возвращает отчёт
    public string Generate(ForestBiome? only)
    {
        if (Application.isPlaying) return "Во время Play генерация отключена: выйди из Play.";
        if (groveMax < groveMin) groveMax = groveMin;

        var report = new System.Text.StringBuilder();
        string fill = null;
        if (useSpeciesPreset || NeedsSpecies()) fill = ApplySpeciesPreset();
        var filter = ForestPlacementFilter.Get();
        if (!filter.Rebuild()) return "ForestPlacementFilter не смог собрать маску: в сцене нет земли, троп, реки и построек по заданным именам.";

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate forest trees");

        try
        {
            if (clearBeforeGenerate)
            {
                int removed = Clear(only);
                if (removed > 0) report.Append("Удалено прежних деревьев: " + removed + ". ");
            }

            var c = new Ctx { filter = filter };
            float maxMin = 0f;
            foreach (var b in Biomes) maxMin = Mathf.Max(maxMin, Get(b).minDistance);
            c.cell = Mathf.Max(4f, maxMin + 0.01f);
            c.noiseX = ((seed * 7919) % 1000) + 0.37f;
            c.noiseZ = ((seed * 104729) % 1000) + 0.71f;
            if (avoidExistingTrees) CollectExisting(c);

            int placedAll = 0;
            // режим карты плотности (ForestDensityGen.cs) или старый режим рощиц
            if (useDensityMap) placedAll = GenerateDensityMap(only, c, report);
            else
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

                BiomeTrees bs = Get(b);
                var entries = new List<TreeEntry>();
                float totalW = 0f;
                for (int i = 0; i < bs.prefabs.Count; i++)
                {
                    var e = bs.prefabs[i];
                    if (e == null || e.prefab == null || e.weight <= 0f) continue;
                    if (PrefabUtility.GetPrefabAssetType(e.prefab) == PrefabAssetType.NotAPrefab) { report.Append("\n" + b + ": «" + e.prefab.name + "» не префаб-ассет, пропущен."); continue; }
                    entries.Add(e); totalW += e.weight;
                }
                if (entries.Count == 0) { report.Append("\n" + b + ": не задано ни одного префаба."); continue; }

                Transform parent = Container(b, true);
                var fams = BuildFamilies(entries);
                int placed = 0;
                for (int zi = 0; zi < zones.Count; zi++)
                {
                    c.rng = new System.Random(seed * 31 + bi * 7919 + zi * 104729);
                    EditorUtility.DisplayProgressBar("Лесные деревья", b + " (" + zones[zi].Map.zoneName + ")", (bi + (zi + 1f) / zones.Count) / Biomes.Length);
                    placed += GenerateZone(zones[zi], bs, entries, totalW, fams, parent, c);
                    if (c.total >= maxTrees) { report.Append("\nДостигнут потолок maxTrees = " + maxTrees + ", остальное не создано."); break; }
                }
                placedAll += placed;
                report.Append("\n" + b + ": " + placed + " деревьев (" + zones.Count + " зон., префабов " + entries.Count + ")");
            }
            report.Insert(0, "Готово: " + placedAll + " деревьев. ");
            if (fill != null && fill.StartsWith("Префабов")) report.Append("\n" + fill);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(group);
        }
        Debug.Log("[ForestSettings] " + report);
        return report.ToString();
    }

    int GenerateZone(ForestZone zone, BiomeTrees bs, List<TreeEntry> entries, float totalW, List<Fam> fams, Transform parent, Ctx c)
    {
        MapZone m = zone.Map;
        Rect area = ZoneRect(m);

        // центры рощиц: плотность = деревья/100 м² делим на среднюю группу; Poisson даёт ~0.85/r² точек на м²
        float avg = (groveMin + groveMax) * 0.5f;
        float groves100 = Mathf.Max(0.01f, bs.density / avg);
        float r = Mathf.Sqrt(100f * 0.85f / groves100);
        r = Mathf.Max(r, groveRadius * 2f + gladeWidth);   // между рощицами всегда проходимый просвет
        var centers = Poisson(area, r, c.rng);

        float thr = Mathf.Lerp(0.20f, 0.58f, clearingAmount);   // Perlin 0..1: ниже порога = поляна
        int placed = 0;
        for (int i = 0; i < centers.Count && c.total < maxTrees; i++)
        {
            Vector3 cp = new Vector3(centers[i].x, m.transform.position.y, centers[i].y);
            if (zone.Depth(cp) < -groveRadius) continue;

            float noise = Mathf.PerlinNoise(cp.x / clearingScale + c.noiseX, cp.z / clearingScale + c.noiseZ);
            if (clearingAmount > 0f && noise < thr) continue;

            float w = EdgeWeight(cp);
            if (c.rng.NextDouble() > Mathf.Lerp(edgeDensityMin, 1f, w)) continue;

            int count = c.rng.Next(groveMin, groveMax + 1);
            if (count > 1 && w < 0.5f && c.rng.NextDouble() < (1f - w)) count--;   // у кромки группы меньше
            // вид рощицы: куртина (шумовое поле) с вероятностью standStrength, иначе жребий по весам
            Fam fam = c.rng.NextDouble() < standStrength ? FamilyAt(fams, cp, c) : PickFam(fams, c.rng);
            TreeEntry leader = Pick(fam.items, fam.total, c.rng);

            for (int t = 0; t < count; t++)
            {
                TreeEntry e;
                if (t == 0) e = leader;
                else if (c.rng.NextDouble() < groveUniformity) e = PickOther(fam, leader, c.rng);
                else e = Pick(entries, totalW, c.rng);
                float sizeMul = t == 0 ? groveLeaderScale : Mathf.Lerp(0.82f, 1f, (float)c.rng.NextDouble());

                for (int attempt = 0; attempt < 12; attempt++)
                {
                    double ang = c.rng.NextDouble() * Math.PI * 2.0;
                    float dist = groveRadius * (t == 0 ? 0.35f : 1f) * Mathf.Sqrt((float)c.rng.NextDouble());   // главное дерево ближе к центру
                    Vector3 p = new Vector3(cp.x + (float)Math.Cos(ang) * dist, cp.y, cp.z + (float)Math.Sin(ang) * dist);
                    if (TryPlace(zone, bs, e, p, parent, c, sizeMul)) { placed++; c.total++; break; }
                }
            }
        }
        return placed;
    }

    bool TryPlace(ForestZone zone, BiomeTrees bs, TreeEntry e, Vector3 p, Transform parent, Ctx c, float sizeMul)
    {
        if (ForestZone.At(p) != zone) return false;   // точка принадлежит другой, более приоритетной зоне
        float w = EdgeWeight(p);
        if (c.rng.NextDouble() > Mathf.Lerp(edgeDensityMin, 1f, w)) return false;
        if (!c.filter.CanSpawnAt(p, bs.trunkClearance)) return false;
        if (TooClose(c, p, bs.minDistance)) return false;

        Vector3 g;
        if (!c.filter.GroundPoint(p.x, p.z, out g)) g = new Vector3(p.x, zone.transform.position.y, p.z);
        else
        {
            // уклон по двум соседним точкам в метре
            Vector3 gx, gz;
            if (c.filter.GroundPoint(p.x + 1f, p.z, out gx) && c.filter.GroundPoint(p.x, p.z + 1f, out gz))
            {
                float slope = Mathf.Atan(Mathf.Max(Mathf.Abs(gx.y - g.y), Mathf.Abs(gz.y - g.y))) * Mathf.Rad2Deg;
                if (slope > maxSlope) return false;
            }
        }

        var go = PrefabUtility.InstantiatePrefab(e.prefab, parent) as GameObject;
        if (go == null) return false;
        Undo.RegisterCreatedObjectUndo(go, "Generate forest trees");

        float yaw = (float)c.rng.NextDouble() * 360f;
        float tx = ((float)c.rng.NextDouble() * 2f - 1f) * tiltMax;
        float tz = ((float)c.rng.NextDouble() * 2f - 1f) * tiltMax;
        float s = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)c.rng.NextDouble()) * Mathf.Lerp(edgeScaleMin, 1f, w) * sizeMul;

        go.transform.SetPositionAndRotation(g + Vector3.up * bs.yOffset, Quaternion.Euler(tx, yaw, tz) * e.prefab.transform.rotation);
        go.transform.localScale = e.prefab.transform.localScale * s;

        AddToHash(c, g);
        return true;
    }

    // вес опушки: максимум z.Weight(p) по всем лесным зонам, куда попала точка.
    // Внешняя кромка леса: все веса малы. Стык зон внутри леса: вес соседа 1, швов нет
    float EdgeWeight(Vector3 p)
    {
        float best = 0f;
        for (int i = 0; i < ForestZone.All.Count; i++)
        {
            var z = ForestZone.All[i];
            if (z == null || !z.isActiveAndEnabled || z.IsNeutral || !z.Map.Contains(p)) continue;
            float wgt = edgeWidth > 0.01f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(z.Depth(p) / edgeWidth)) : z.Weight(p);
            if (wgt > best) best = wgt;
        }
        return best;
    }

    // ------------------------------------------------------------ виды (семейства префабов)
    class Fam { public string name; public List<TreeEntry> items = new List<TreeEntry>(); public float total; }

    // «Tree_Cedar_A_s1» → «Tree_Cedar», «Willow_Dead_2_s2» → «Willow_Dead»: срезаем _sN и одну букву или цифру вариации
    static string FamilyName(GameObject prefab)
    {
        string n = System.Text.RegularExpressions.Regex.Replace(prefab.name, @"_s\d+$", "");
        return System.Text.RegularExpressions.Regex.Replace(n, @"_[A-Za-z0-9]$", "");
    }

    static List<Fam> BuildFamilies(List<TreeEntry> entries)
    {
        var res = new List<Fam>();
        for (int i = 0; i < entries.Count; i++)
        {
            string key = FamilyName(entries[i].prefab);
            Fam f = res.Find(x => x.name == key);
            if (f == null) { f = new Fam { name = key }; res.Add(f); }
            f.items.Add(entries[i]); f.total += entries[i].weight;
        }
        return res;
    }

    static Fam PickFam(List<Fam> fams, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < fams.Count; i++) total += fams[i].total;
        float v = (float)rng.NextDouble() * total;
        for (int i = 0; i < fams.Count; i++) { v -= fams[i].total; if (v <= 0f) return fams[i]; }
        return fams[fams.Count - 1];
    }

    // вид куртины в точке: крупный шум режется на доли по весам семейств
    Fam FamilyAt(List<Fam> fams, Vector3 p, Ctx c)
    {
        if (fams.Count == 1) return fams[0];
        float n = Mathf.PerlinNoise(p.x / standScale + c.noiseX * 0.37f + 91.7f, p.z / standScale + c.noiseZ * 0.53f + 47.3f);
        float t = Mathf.Clamp01((n - 0.25f) / 0.5f);   // Perlin держится около 0.5: растягиваем, чтобы виды были примерно в долях весов
        float total = 0f;
        for (int i = 0; i < fams.Count; i++) total += fams[i].total;
        float v = t * total;
        for (int i = 0; i < fams.Count; i++) { v -= fams[i].total; if (v <= 0f) return fams[i]; }
        return fams[fams.Count - 1];
    }

    // другая вариация того же вида (A/B/C не повторяются подряд); если вариация одна, она и берётся
    static TreeEntry PickOther(Fam fam, TreeEntry not, System.Random rng)
    {
        if (fam.items.Count < 2) return fam.items[0];
        float total = 0f;
        for (int i = 0; i < fam.items.Count; i++) if (fam.items[i] != not) total += fam.items[i].weight;
        float v = (float)rng.NextDouble() * total;
        for (int i = 0; i < fam.items.Count; i++)
        {
            if (fam.items[i] == not) continue;
            v -= fam.items[i].weight;
            if (v <= 0f) return fam.items[i];
        }
        return fam.items[0];
    }

    static TreeEntry Pick(List<TreeEntry> list, float total, System.Random rng)
    {
        float v = (float)rng.NextDouble() * total;
        for (int i = 0; i < list.Count; i++) { v -= list[i].weight; if (v <= 0f) return list[i]; }
        return list[list.Count - 1];
    }

    static Rect ZoneRect(MapZone m)
    {
        Vector3 c = m.transform.position;
        if (m.shape == MapZone.Shape.Circle) return new Rect(c.x - m.radius, c.z - m.radius, m.radius * 2f, m.radius * 2f);
        Quaternion q = Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
        float hx = m.size.x * 0.5f, hz = m.size.y * 0.5f;
        Vector3 a = q * new Vector3(-hx, 0f, -hz), b = q * new Vector3(hx, 0f, -hz), d = q * new Vector3(hx, 0f, hz), e = q * new Vector3(-hx, 0f, hz);
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(d.x, e.x)), maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(d.x, e.x));
        float minZ = Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(d.z, e.z)), maxZ = Mathf.Max(Mathf.Max(a.z, b.z), Mathf.Max(d.z, e.z));
        return new Rect(c.x + minX, c.z + minZ, maxX - minX, maxZ - minZ);
    }

    // равномерно рассеянные точки (Bridson): расстояние между любыми двумя не меньше r
    static List<Vector2> Poisson(Rect area, float r, System.Random rng)
    {
        var pts = new List<Vector2>();
        float cs = r / 1.41421356f;
        int gw = Mathf.CeilToInt(area.width / cs) + 1, gh = Mathf.CeilToInt(area.height / cs) + 1;
        var grid = new int[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;
        var active = new List<int>();

        Vector2 first = new Vector2(area.x + (float)rng.NextDouble() * area.width, area.y + (float)rng.NextDouble() * area.height);
        pts.Add(first); active.Add(0);
        grid[Mathf.Clamp((int)((first.y - area.y) / cs), 0, gh - 1) * gw + Mathf.Clamp((int)((first.x - area.x) / cs), 0, gw - 1)] = 0;

        while (active.Count > 0)
        {
            int ai = rng.Next(active.Count);
            Vector2 b = pts[active[ai]];
            bool found = false;
            for (int k = 0; k < 30 && !found; k++)
            {
                double ang = rng.NextDouble() * Math.PI * 2.0;
                float rad = r * (1f + (float)rng.NextDouble());
                Vector2 q = new Vector2(b.x + (float)Math.Cos(ang) * rad, b.y + (float)Math.Sin(ang) * rad);
                if (!area.Contains(q)) continue;
                int gx = Mathf.Clamp((int)((q.x - area.x) / cs), 0, gw - 1), gy = Mathf.Clamp((int)((q.y - area.y) / cs), 0, gh - 1);
                bool ok = true;
                for (int dy = -2; dy <= 2 && ok; dy++)
                    for (int dx = -2; dx <= 2 && ok; dx++)
                    {
                        int nx = gx + dx, ny = gy + dy;
                        if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                        int idx = grid[ny * gw + nx];
                        if (idx >= 0 && (pts[idx] - q).sqrMagnitude < r * r) ok = false;
                    }
                if (!ok) continue;
                pts.Add(q);
                active.Add(pts.Count - 1);
                grid[gy * gw + gx] = pts.Count - 1;
                found = true;
            }
            if (!found) { active[ai] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
        }
        return pts;
    }

    // ------------------------------------------------------------ минимальная дистанция между стволами (сетка-хэш)
    long Key(Ctx c, float x, float z) { return ((long)Mathf.FloorToInt(x / c.cell) << 32) ^ (uint)Mathf.FloorToInt(z / c.cell); }

    void AddToHash(Ctx c, Vector3 p)
    {
        long k = Key(c, p.x, p.z);
        List<Vector2> l;
        if (!c.hash.TryGetValue(k, out l)) { l = new List<Vector2>(4); c.hash[k] = l; }
        l.Add(new Vector2(p.x, p.z));
    }

    bool TooClose(Ctx c, Vector3 p, float minDist)
    {
        int cx = Mathf.FloorToInt(p.x / c.cell), cz = Mathf.FloorToInt(p.z / c.cell);
        float m2 = minDist * minDist;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                List<Vector2> l;
                if (!c.hash.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out l)) continue;
                for (int i = 0; i < l.Count; i++)
                {
                    float ex = l[i].x - p.x, ez = l[i].y - p.z;
                    if (ex * ex + ez * ez < m2) return true;
                }
            }
        return false;
    }

    // уже стоящие деревья: игровые ResourceItem вне контейнеров и сохранённые деревья из контейнеров, которые не очищались
    void CollectExisting(Ctx c)
    {
        Transform root = ForestZones.TreesRoot(false);
        var items = FindObjectsByType<ResourceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
            if (root == null || !items[i].transform.IsChildOf(root)) AddToHash(c, items[i].transform.position);
        foreach (var b in Biomes)
        {
            var cont = Container(b, false);
            if (cont == null) continue;
            for (int i = 0; i < cont.childCount; i++) AddToHash(c, cont.GetChild(i).position);
        }
    }
}
#endif

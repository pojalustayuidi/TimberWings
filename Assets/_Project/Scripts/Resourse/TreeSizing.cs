// Assets/_Project/Scripts/Resource/TreeSizing.cs (НЕ в Editor)
// Размеры деревьев: молодое / зрелое / старое. Размер выбирается шумом по ПОЗИЦИИ дерева (+ seed), поэтому не меняется между запусками.
// Все числа лежат в GameBalance (блок «размеры деревьев»). Здесь только выбор размера и тон листвы.
//
// Тон листвы: материалы листвы подменяются на закэшированные копии (3 тона на исходный материал, не на дерево),
// поэтому SRP Batcher и батчинг не страдают, MaterialPropertyBlock не нужен.
using System.Collections.Generic;
using UnityEngine;

public enum TreeSize { Young = 0, Mature = 1, Old = 2 }

public static class TreeSizing
{
    static readonly Dictionary<int, Material> toned = new Dictionary<int, Material>(16);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { toned.Clear(); sizeMap.Clear(); anchor = null; }

    // ------------------------------------------------------------ размер по позиции
    // позиция квантуется до 0.1 м: микро-сдвиги сцены размер не меняют
    public static uint Hash(Vector3 p)
    {
        unchecked
        {
            int x = Mathf.RoundToInt(p.x * 10f), z = Mathf.RoundToInt(p.z * 10f);
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ GameBalance.TreeSizeSeed;
            h ^= h >> 16; h *= 0x85EBCA6Bu;
            h ^= h >> 13; h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }

    public static float Value01(Vector3 p) { return (Hash(p) & 0xFFFFFFu) / (float)0x1000000; }

    // ------------------------------------------------------------ размер по шуму позиции
    // Раньше размер выбирал чистый хэш: старые и молодые деревья перемешивались как конфетти. Теперь у каждого дерева «балл»:
    //   0.50 · гладкий шум позиции (пятна по ~10 м и мелкая добавка по ~4 м) — крупные куски леса растут вместе
    //   0.20 · близость к центру острова                                   — молодые к краям
    //   0.20 · плотность деревьев вокруг (соседи в 5 м)                    — старые в чаще, молодые на полянах
    //   0.10 · хэш позиции                                                 — чтобы границы пятен не были слишком ровными
    // Деревья сортируются по баллу, и первые 30% (GameBalance.TreeChance) становятся молодыми, следующие 45% зрелыми, остальные старыми.
    // Так доли в лесу точные при любой раскладке, а при том же GameBalance.TreeSizeSeed лес одинаковый при каждом запуске.
    // Позиции деревьев в сцене не читаются из LocationBuilder: берутся сами ResourceItem (всё считается один раз при первом Roll).
    static readonly Dictionary<long, TreeSize> sizeMap = new Dictionary<long, TreeSize>(128);
    static ResourceItem anchor;                 // любое дерево сцены: если его уже нет, сцена сменилась и раскладку надо строить заново
    public static Vector3 IslandCenter { get; private set; }

    const float DensityRadius = 5f;

    static long Key(Vector3 p)
    {
        int x = Mathf.RoundToInt(p.x * 10f), z = Mathf.RoundToInt(p.z * 10f);
        return ((long)x << 32) ^ (uint)z;
    }

    static uint Lattice(int x, int z)
    {
        unchecked
        {
            uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ (GameBalance.TreeSizeSeed * 2654435761u);
            h ^= h >> 16; h *= 0x85EBCA6Bu;
            h ^= h >> 13; h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }

    // гладкий value-noise 0..1 (билинейная интерполяция узлов решётки со сглаживанием)
    static float Noise2(float x, float z)
    {
        int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
        float fx = x - xi, fz = z - zi;
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);
        float a = (Lattice(xi, zi) & 0xFFFF) / 65535f;
        float b = (Lattice(xi + 1, zi) & 0xFFFF) / 65535f;
        float c = (Lattice(xi, zi + 1) & 0xFFFF) / 65535f;
        float d = (Lattice(xi + 1, zi + 1) & 0xFFFF) / 65535f;
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
    }

    static float Fbm(Vector3 p)
    {
        return 0.65f * Noise2(p.x * 0.09f, p.z * 0.09f) + 0.35f * Noise2(p.x * 0.23f + 17f, p.z * 0.23f - 9f);
    }

    static void Build()
    {
        sizeMap.Clear();
        var items = Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int n = items.Length;
        anchor = n > 0 ? items[0] : null;
        if (n == 0) return;

        var pos = new Vector3[n];
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < n; i++) { pos[i] = items[i].transform.position; sum += pos[i]; }
        Vector3 c = sum / n;
        c.y = 0f;
        IslandCenter = c;

        float rMax = 1f;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = pos[i] - c; d.y = 0f;
            rMax = Mathf.Max(rMax, d.magnitude);
        }

        // соседи в DensityRadius (n² один раз за сцену: для сотен деревьев это доли миллисекунды)
        var near = new int[n];
        int maxNear = 1;
        float lim = DensityRadius * DensityRadius;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float dx = pos[i].x - pos[j].x, dz = pos[i].z - pos[j].z;
                if (dx * dx + dz * dz <= lim) { near[i]++; near[j]++; }
            }
            maxNear = Mathf.Max(maxNear, near[i]);
        }

        var score = new float[n];
        var keys = new long[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 d = pos[i] - c; d.y = 0f;
            float centre = 1f - Mathf.Clamp01(d.magnitude / rMax);
            float dens = near[i] / (float)maxNear;
            score[i] = 0.50f * Fbm(pos[i]) + 0.20f * centre + 0.20f * dens + 0.10f * Value01(pos[i]);
            keys[i] = Key(pos[i]);
        }
        System.Array.Sort(score, keys);   // по возрастанию балла

        float[] ch = GameBalance.TreeChance;
        int young = Mathf.RoundToInt(n * ch[0]);
        int mature = Mathf.RoundToInt(n * (ch[0] + ch[1]));
        for (int i = 0; i < n; i++)
            sizeMap[keys[i]] = i < young ? TreeSize.Young : (i < mature ? TreeSize.Mature : TreeSize.Old);
    }

    public static TreeSize Roll(Vector3 p)
    {
        if (anchor == null) Build();   // первый вызов в сцене (или сцена сменилась)

        TreeSize s;
        if (sizeMap.TryGetValue(Key(p), out s)) return s;

        // дерево, которого не было в сцене при расчёте (появилось позже): прежний выбор по хэшу позиции
        float v = Value01(p);
        float[] ch = GameBalance.TreeChance;
        float acc = 0f;
        for (int i = 0; i < ch.Length - 1; i++)
        {
            acc += ch[i];
            if (v < acc) return (TreeSize)i;
        }
        return (TreeSize)(ch.Length - 1);
    }

    // ------------------------------------------------------------ тон листвы
    static bool IsRenderSkip(string n)
    {
        return n == "Outline" || n == "Hull" || n == "HighlightRing";
    }

    static bool IsFoliage(Material m)
    {
        if (m == null) return false;
        string n = m.name.ToLowerInvariant();
        if (n.Contains("bark") || n.Contains("trunk") || n.Contains("wood") || n.Contains("stump") || n.Contains("кора")) return false;
        if (n.Contains("leaf") || n.Contains("foliage") || n.Contains("crown") || n.Contains("needle") || n.Contains("листв")) return true;

        string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null);
        if (prop == null) return false;
        Color c = m.GetColor(prop);
        return c.g >= c.r * 0.95f && c.g >= c.b;   // зелёный по цвету
    }

    static Color Tone(Color c, TreeSize s)
    {
        if (s == TreeSize.Mature) return c;
        float h, sat, v;
        Color.RGBToHSV(c, out h, out sat, out v);
        if (s == TreeSize.Young)
        {
            h = Mathf.Repeat(h - 0.03f, 1f);          // чуть жёлтее
            sat *= 0.85f;
            v = Mathf.Min(1f, v * 1.18f);              // светлее
        }
        else
        {
            h = Mathf.Repeat(h + 0.02f, 1f);          // чуть холоднее
            sat = Mathf.Min(1f, sat * 1.10f);
            v *= 0.68f;                                // заметно темнее
        }
        Color o = Color.HSVToRGB(h, sat, v);
        o.a = c.a;
        return o;
    }

    public static Material TonedMaterial(Material src, TreeSize s)
    {
        if (src == null || s == TreeSize.Mature || !IsFoliage(src)) return src;
        int key = src.GetInstanceID() * 4 + (int)s;
        Material m;
        if (toned.TryGetValue(key, out m) && m != null) return m;

        m = new Material(src) { name = src.name + "_" + s };
        m.enableInstancing = src.enableInstancing;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Tone(src.GetColor("_BaseColor"), s));
        if (m.HasProperty("_Color")) m.SetColor("_Color", Tone(src.GetColor("_Color"), s));
        toned[key] = m;
        return m;
    }

    // красит листву всех мешей дерева (включая выключенные модели RandomModel). Контур и кольцо подсветки не трогает
    public static void ApplyTone(GameObject tree, TreeSize s)
    {
        if (s == TreeSize.Mature) return;
        var rs = tree.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null || IsRenderSkip(r.gameObject.name)) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int j = 0; j < mats.Length; j++)
            {
                var t = TonedMaterial(mats[j], s);
                if (t != mats[j]) { mats[j] = t; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}

// Assets/_Project/Scripts/Resource/TreeGround.cs (НЕ в Editor)
// Пятно земли под деревом, которое можно рубить: трава вокруг, голая земля под «рабочими» деревьями.
// Декоративные деревья (без ResourceItem) пятна не получают, так что с первого взгляда видно, какое дерево твоё.
//
// ТЕПЕРЬ У КАЖДОГО ВИДА СВОЯ ЗЕМЛЯ (ключ = TreeSpeciesInfo.species: Cedar, Birch, Autumn, Willow_Dead, Random, Oak):
//   Cedar    ковёр хвои: тёмная красно-бурая подстилка, штрихи иголок, пятно шире (ель сбрасывает хвою широким кругом)
//   Birch    светлая песчаная почва, редкие белые листики, пятно меньше
//   Autumn   листопад: оранжево-жёлтый ковёр, крупные листья, самое большое пятно
//   Willow   сухая серая земля с трещинами и пятно чуть вытянуто (мёртвая ива)
//   Oak      тёмный перегной с мхом по краю, большое пятно
//   Random   как было: тёплая коричневая земля (для деревьев без вида)
// Все числа в таблице Styles ниже: радиус (множитель к размеру молодое/зрелое/старое), цвета, «крапинки», рваность края.
//
// Пятно отдельный объект (не ребёнок дерева): когда дерево срублено и прячется, земля остаётся вокруг пня.
// Меш подгоняется под рельеф лучами вниз один раз при создании (3 кольца по 18 точек), поэтому на склоне пятно не зарывается в землю.
// Текстура и материал рисуются в коде ОДИН раз на вид (6 текстур 128×128 на весь лес), ассеты не нужны.
using System.Collections.Generic;
using UnityEngine;

public static class TreeGround
{
    //                                       молодое  зрелое  старое
    static readonly float[] Radius = { 0.95f, 1.35f, 1.90f };

    const int Segs = 18;
    const float Lift = 0.03f;       // над землёй, чтобы не мерцало (z-fighting)

    // ------------------------------------------------------------ стили видов
    sealed class Style
    {
        public string key;
        public float radius = 1f;        // множитель к Radius[размер]
        public float stretch = 1f;       // вытянутость пятна (1 = круг)
        public Color dark, light, fleck; // центр, светлая кромка, крапинки
        public float fleckAmt = 0.6f;    // насколько крапинки перекрашивают землю 0..1
        public float fleckThr = 0.62f;   // порог шума: выше = реже
        public float fleckScale = 0.22f; // размер крапинок (больше = мельче)
        public bool needles;             // крапинки штрихами (хвоя), а не пятнами
        public float edgeRough = 0.18f;  // рваность края
        public float soft = 0.28f;       // мягкость края
        public float alpha = 0.94f;      // непрозрачность в центре
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // меняй числа здесь: радиус, цвет, густота крапинок. Новый вид: добавь строку и ключ из TreeSpeciesInfo.species
    static readonly Style[] Styles =
    {
        new Style { key = "Random",
            dark = C(0.39f, 0.27f, 0.17f), light = C(0.57f, 0.43f, 0.27f), fleck = C(0.30f, 0.21f, 0.13f),
            radius = 1.00f, fleckAmt = 0.35f, fleckThr = 0.64f },

        new Style { key = "Cedar",
            dark = C(0.27f, 0.18f, 0.12f), light = C(0.43f, 0.33f, 0.19f), fleck = C(0.60f, 0.44f, 0.24f),
            radius = 1.18f, needles = true, fleckAmt = 0.75f, fleckThr = 0.62f, edgeRough = 0.22f, soft = 0.30f },

        new Style { key = "Birch",
            dark = C(0.55f, 0.43f, 0.30f), light = C(0.66f, 0.58f, 0.40f), fleck = C(0.90f, 0.86f, 0.58f),
            radius = 0.85f, fleckAmt = 0.65f, fleckThr = 0.70f, fleckScale = 0.30f, edgeRough = 0.14f, alpha = 0.90f },

        new Style { key = "Autumn",
            dark = C(0.46f, 0.26f, 0.12f), light = C(0.78f, 0.46f, 0.14f), fleck = C(0.97f, 0.76f, 0.20f),
            radius = 1.28f, fleckAmt = 0.85f, fleckThr = 0.57f, fleckScale = 0.17f, edgeRough = 0.24f, soft = 0.34f },

        new Style { key = "Willow",
            dark = C(0.47f, 0.42f, 0.37f), light = C(0.66f, 0.62f, 0.54f), fleck = C(0.24f, 0.21f, 0.19f),
            radius = 1.05f, stretch = 1.12f, fleckAmt = 0.70f, fleckThr = 0.67f, fleckScale = 0.34f, edgeRough = 0.26f, soft = 0.22f, alpha = 0.92f },

        new Style { key = "Oak",
            dark = C(0.32f, 0.23f, 0.12f), light = C(0.37f, 0.47f, 0.20f), fleck = C(0.42f, 0.58f, 0.22f),
            radius = 1.30f, fleckAmt = 0.70f, fleckThr = 0.58f, fleckScale = 0.14f, edgeRough = 0.20f, soft = 0.32f },
    };

    static readonly Dictionary<Style, Material> mats = new Dictionary<Style, Material>(8);
    static readonly Dictionary<Style, Texture2D> texs = new Dictionary<Style, Texture2D>(8);
    static readonly RaycastHit[] hits = new RaycastHit[8];

    // цвет земли по умолчанию (оставлен для совместимости с прежним кодом)
    public static Color Dirt = new Color(0.46f, 0.32f, 0.20f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { mats.Clear(); texs.Clear(); }

    // ключ вида (TreeSpeciesInfo.species или null) → стиль. Willow_Dead = Willow; неизвестное = Random
    static Style Find(string species)
    {
        if (!string.IsNullOrEmpty(species))
        {
            for (int i = 0; i < Styles.Length; i++)
                if (species.StartsWith(Styles[i].key, System.StringComparison.OrdinalIgnoreCase)) return Styles[i];
        }
        return Styles[0];
    }

    public static GameObject Create(Vector3 basePos, int size, int layer, string species = null)
    {
        Style st = Find(species);
        Material mat = GetMat(st);
        if (mat == null) return null;
        size = Mathf.Clamp(size, 0, 2);
        float R = Radius[size] * st.radius;

        uint h = TreeSizing.Hash(basePos);
        float rot = (h & 0xFFFF) / 65535f * Mathf.PI * 2f;     // каждое пятно повёрнуто по-своему: одинаковых не видно
        float stretch = st.stretch * (0.9f + ((h >> 16) & 0xFF) / 255f * 0.2f);

        var go = new GameObject("TreeDirt");
        go.layer = layer;
        go.transform.position = basePos;

        // вершины: центр + 3 кольца
        var verts = new Vector3[1 + 3 * Segs];
        var uvs = new Vector2[verts.Length];
        verts[0] = new Vector3(0f, GroundY(basePos, basePos.y) - basePos.y + Lift, 0f);
        uvs[0] = new Vector2(0.5f, 0.5f);
        float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
        for (int r = 1; r <= 3; r++)
        {
            float k = r / 3f;
            for (int i = 0; i < Segs; i++)
            {
                float a = i / (float)Segs * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * k, z = Mathf.Sin(a) * k * stretch;
                var w = new Vector3(basePos.x + x * R, basePos.y, basePos.z + z * R);
                int idx = 1 + (r - 1) * Segs + i;
                verts[idx] = new Vector3(x * R, GroundY(w, basePos.y) - basePos.y + Lift, z * R);
                // UV повёрнут на rot: рваный край текстуры смотрит в разные стороны
                float ux = x * c - (z / stretch) * s, uz = x * s + (z / stretch) * c;
                uvs[idx] = new Vector2(0.5f + ux * 0.5f, 0.5f + uz * 0.5f);
            }
        }

        var tris = new List<int>(Segs * 3 * 5);
        for (int i = 0; i < Segs; i++)   // центр → кольцо 1
            tris.AddRange(new[] { 0, 1 + (i + 1) % Segs, 1 + i });
        for (int r = 1; r < 3; r++)      // кольцо r → кольцо r+1
        {
            int a0 = 1 + (r - 1) * Segs, b0 = 1 + r * Segs;
            for (int i = 0; i < Segs; i++)
            {
                int n = (i + 1) % Segs;
                tris.AddRange(new[] { a0 + i, a0 + n, b0 + i });
                tris.AddRange(new[] { a0 + n, b0 + n, b0 + i });
            }
        }

        var mesh = new Mesh { name = "TreeDirt" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    // убрать пятно вместе с его мешем (дерево уничтожено)
    public static void Destroy(GameObject go)
    {
        if (go == null) return;
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) Object.Destroy(mf.sharedMesh);
        Object.Destroy(go);
    }

    // высота земли под точкой: луч вниз, игнорируем триггеры, сами деревья, пни и всё, что не похоже на землю (стены, крыши)
    static float GroundY(Vector3 p, float fallback)
    {
        int n = Physics.RaycastNonAlloc(new Vector3(p.x, fallback + 3f, p.z), Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, y = fallback;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.normal.y < 0.5f) continue;
            if (h.collider.GetComponentInParent<ResourceItem>() != null) continue;
            if (h.collider.attachedRigidbody != null) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        return y;
    }

    // ------------------------------------------------------------ материал и текстура на стиль
    static Material GetMat(Style st)
    {
        Material m;
        if (mats.TryGetValue(st, out m) && m != null) return m;

        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) return null;

        Texture2D tex;
        if (!texs.TryGetValue(st, out tex) || tex == null) { tex = MakeTexture(st); texs[st] = tex; }

        m = new Material(sh) { name = "TreeDirt_" + st.key };
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        m.renderQueue = 2999;   // раньше кольца подсветки (3000): кольцо рисуется поверх земли
        mats[st] = m;
        return m;
    }

    static float SeedOf(string key)
    {
        int h = 17;
        for (int i = 0; i < key.Length; i++) h = h * 31 + key[i];
        return (h & 0xFF) * 0.37f;   // сдвиг шума: у видов разные узоры
    }

    // 128×128: мягкий рваный край, пятна темнее к центру, светлая кромка и крапинки вида (хвоя / листики / трещины / мох)
    static Texture2D MakeTexture(Style st)
    {
        const int N = 128;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "TreeDirtTex_" + st.key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        float off = SeedOf(st.key);
        Color dark = st.dark, light = st.light;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float edge = 0.78f + st.edgeRough * Mathf.PerlinNoise(Mathf.Cos(a) * 1.4f + 5f + off, Mathf.Sin(a) * 1.4f + 5f);
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(edge - st.soft, edge, r)) * st.alpha;

                float spots = Mathf.PerlinNoise(x * 0.09f + 11f + off, y * 0.09f + 3f);
                float rim = Mathf.InverseLerp(0.35f, edge, r);
                Color col = Color.Lerp(dark, light, Mathf.Clamp01(rim * 0.8f + (spots - 0.5f) * 0.5f));

                // крапинки вида
                float f;
                if (st.needles)
                {
                    // штрихи: шум, растянутый по двум осям; пересечение даёт короткие «иголки» в разные стороны
                    float s1 = Mathf.PerlinNoise(x * 0.42f + off, y * 0.07f + off * 0.5f);
                    float s2 = Mathf.PerlinNoise(x * 0.07f + off * 2f, y * 0.42f + off);
                    f = Mathf.Max(s1 * s2 * 1.9f, 0f);
                }
                else f = Mathf.PerlinNoise(x * st.fleckScale + off * 3f, y * st.fleckScale + off * 5f);

                float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(st.fleckThr, st.fleckThr + 0.10f, f)) * st.fleckAmt;
                m *= Mathf.Clamp01(1.15f - r * 0.35f);   // к самому краю крапинок чуть меньше
                col = Color.Lerp(col, st.fleck, m);

                px[y * N + x] = new Color(col.r, col.g, col.b, alpha);
            }
        }
        t.SetPixels(px);
        t.Apply(false, true);
        return t;
    }
}

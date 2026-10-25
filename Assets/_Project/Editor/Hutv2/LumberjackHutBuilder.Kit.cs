// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Kit.cs   (ТОЛЬКО в папке Editor!)
// Набор кирпичиков генератора: палитра Cloud Candy, материалы-ассеты, примитивы (коробка, бревно, жердь, клякса, конус),
// крыша из рядов досок и «ткань на цепочке» для флагов. Остальные части partial-класса строят сами стадии.
//
// Материалы: Project/CandyToon (как деревья), если шейдера нет — URP Lit. Лежат в Assets/_Project/Art/HutV2/Materials.
// Имена важны для твоих систем света (BuildingLightLink / FireLightScan):
//   «HutWindowGlass»  в имени есть «glass»  -> окно, светится ночью (glow-пятно с наружной стороны)
//   «HutLamp»         в имени есть «lamp»   -> стекло фонаря, emissive следует за огнём рядом
//   Light «LanternLight…» (Point) -> становится FireSource (мерцание, плавный пул настоящих светов)
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class LumberjackHutBuilder
{
    // ---------------------------------------------------------------- палитра
    // ключ -> цвет (sRGB). Тёплое дерево, жёлтая «соломенная» кровля, лавандовые тени достаёт сам стиль/шейдер.
    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "WoodDark",  new Color(0.49f, 0.29f, 0.14f) },
        { "WoodMid",   new Color(0.69f, 0.42f, 0.18f) },
        { "WoodLight", new Color(0.88f, 0.60f, 0.29f) },
        { "LogEnd",    new Color(0.96f, 0.80f, 0.52f) },
        { "Plank",     new Color(0.94f, 0.78f, 0.50f) },
        { "PlankDark", new Color(0.80f, 0.60f, 0.34f) },
        { "Roof",      new Color(0.98f, 0.81f, 0.25f) },
        { "RoofShade", new Color(0.88f, 0.66f, 0.18f) },
        { "Stone",     new Color(0.82f, 0.80f, 0.78f) },
        { "StoneDark", new Color(0.63f, 0.61f, 0.62f) },
        { "Moss",      new Color(0.47f, 0.79f, 0.23f) },
        { "Grass",     new Color(0.55f, 0.86f, 0.30f) },
        { "Leaf",      new Color(0.30f, 0.70f, 0.32f) },
        { "Dirt",      new Color(0.90f, 0.78f, 0.53f) },
        { "Red",       new Color(0.90f, 0.32f, 0.25f) },
        { "Cream",     new Color(0.99f, 0.95f, 0.85f) },
        { "Rope",      new Color(0.86f, 0.74f, 0.52f) },
        { "Metal",     new Color(0.68f, 0.71f, 0.76f) },
        { "MetalDark", new Color(0.42f, 0.44f, 0.50f) },
        { "Tarp",      new Color(0.76f, 0.86f, 0.93f) },
        { "Lilac",     new Color(0.74f, 0.55f, 0.92f) },
        { "Pink",      new Color(1.00f, 0.63f, 0.75f) },
        { "Water",     new Color(0.42f, 0.86f, 0.84f) },
        { "HutLamp",   new Color(1.00f, 0.83f, 0.46f) },
        { "HutWindowGlass", new Color(1.00f, 0.87f, 0.55f) },
        { "HutSmoke",  new Color(1.00f, 0.98f, 0.96f) },
        { "HutDust",   new Color(0.93f, 0.82f, 0.60f) },
    };

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Shader toon;
    static bool toonTried;

    static Shader ToonShader()
    {
        if (toonTried) return toon;
        toonTried = true;
        var sh = Shader.Find("Project/CandyToon");
        toon = (sh != null && sh.isSupported) ? sh : null;
        if (toon == null)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) toon = rp.defaultMaterial.shader;
            if (toon == null) toon = Shader.Find("Universal Render Pipeline/Lit");
            if (toon == null) toon = Shader.Find("Standard");
            Debug.LogWarning("[HutV2] Project/CandyToon не найден: материалы на запасном шейдере. Вид можно потом переназначить в папке " + HutMeshLib.MatDir);
        }
        return toon;
    }

    static Material Mat(string key)
    {
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) return m;

        HutMeshLib.EnsureFolders();
        string path = HutMeshLib.MatDir + "/" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Color c;
        if (!Palette.TryGetValue(key, out c)) c = Color.magenta;
        if (m == null)
        {
            m = new Material(ToonShader()) { name = key };
            m.enableInstancing = true;
            SetCol(m, c);
            if (m.HasProperty("_GlossStrength")) m.SetFloat("_GlossStrength", 0f);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", key == "HutSmoke" ? 0.2f : 0.05f);
            bool glow = key == "HutLamp" || key == "HutWindowGlass";
            if (glow && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.6f);
            }
            AssetDatabase.CreateAsset(m, path);
        }
        mats[key] = m;
        return m;
    }

    static void SetCol(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ---------------------------------------------------------------- случайность (одно зерно = одна и та же хижина)
    static System.Random rng = new System.Random(7);
    static float R() { return (float)rng.NextDouble(); }
    static float R(float a, float b) { return a + (b - a) * R(); }
    static int RI(int a, int b) { return rng.Next(a, b); }

    // ---------------------------------------------------------------- базовое создание объекта
    static GameObject Obj(Transform p, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        return go;
    }

    static GameObject Part(Transform p, string name, Mesh mesh, Material m, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = new GameObject(name);
        var t = go.transform;
        t.SetParent(p, false);
        t.localPosition = pos;
        t.localRotation = rot;
        t.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.On;
        r.receiveShadows = true;
        return go;
    }

    // коробка с фаской (центр pos, размеры size, углы euler)
    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, string mat, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.SoftCube, Mat(mat), pos, size, Quaternion.Euler(euler));
    }

    // вертикальный цилиндр
    static GameObject Cyl(Transform p, string name, Vector3 pos, float radius, float height, string mat, int sides = 8, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.Cyl(sides), Mat(mat), pos, new Vector3(radius * 2f, height, radius * 2f), Quaternion.Euler(euler));
    }

    static GameObject ConeP(Transform p, string name, Vector3 pos, float radius, float height, string mat, int sides = 7, Vector3 euler = default(Vector3))
    {
        return Part(p, name, HutMeshLib.Cone(sides), Mat(mat), pos, new Vector3(radius * 2f, height, radius * 2f), Quaternion.Euler(euler));
    }

    static GameObject Blob(Transform p, string name, Vector3 pos, Vector3 size, string mat, int seed = -1)
    {
        if (seed < 0) seed = RI(0, 4);
        return Part(p, name, HutMeshLib.Blob(seed), Mat(mat), pos, size, Quaternion.Euler(0f, R(0f, 360f), 0f));
    }

    // балка/жердь от точки a до точки b (квадратное сечение thick)
    static GameObject Beam(Transform p, string name, Vector3 a, Vector3 b, float thick, string mat)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) len = 0.001f;
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return Part(p, name, HutMeshLib.SoftCube, Mat(mat), (a + b) * 0.5f, new Vector3(thick, len, thick), rot);
    }

    // круглая жердь (верёвка, ось, труба)
    static GameObject Rod(Transform p, string name, Vector3 a, Vector3 b, float radius, string mat, int sides = 6)
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        return Part(p, name, HutMeshLib.Cyl(sides), Mat(mat), (a + b) * 0.5f, new Vector3(radius * 2f, len, radius * 2f), rot);
    }

    // бревно со светлыми торцами: ОДИН Renderer с двумя материалами (ствол, торец), поэтому при сборке оно падает целиком
    static GameObject Log(Transform p, string name, Vector3 a, Vector3 b, float radius, string wood = "WoodMid", string end = "LogEnd")
    {
        Vector3 d = b - a;
        float len = Mathf.Max(0.001f, d.magnitude);
        var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        var go = Part(p, name, HutMeshLib.LogCyl(8), Mat(wood), (a + b) * 0.5f, new Vector3(radius * 2f, len, radius * 2f), rot);
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat(wood), Mat(end) };
        return go;
    }

    // невидимая коллизия (в локальных координатах родителя)
    static GameObject Solid(Transform p, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.localPosition = center;
        var bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        return go;
    }

    // ---------------------------------------------------------------- «ткань на цепочке» (флаги, вымпелы)
    // Каждый сегмент висит на своём шарнире-пустышке, шарнир ребёнок предыдущего: HutLife качает шарниры волной.
    // topLocal: точка крепления сверху; сегменты идут вниз. tail: другой материал для последнего сегмента (вымпел с «ласточкой»).
    static HutLife.ClothChain Cloth(Transform p, string name, Vector3 topLocal, float width, float segLen, int segs, string mat, float amp = 10f, float phase = 0f, string tail = null)
    {
        var chain = new HutLife.ClothChain { amp = amp, phase = phase, segs = new Transform[segs] };
        Transform parent = p;
        for (int i = 0; i < segs; i++)
        {
            var hinge = Obj(parent, name + "_h" + i, i == 0 ? topLocal : new Vector3(0f, -segLen, 0f));
            bool last = i == segs - 1;
            Box(hinge.transform, name + "_" + i, new Vector3(0f, -segLen * 0.5f, 0f),
                new Vector3(last && tail != null ? width * 0.8f : width, segLen * 1.03f, 0.025f), (last && tail != null) ? tail : mat);
            chain.segs[i] = hinge.transform;
            parent = hinge.transform;
        }
        return chain;
    }

    // ---------------------------------------------------------------- крыша из рядов досок (ось конька вдоль локальной X родителя)
    // center: центр по X/Z, baseY: высота карниза, rise: подъём до конька, halfSpan: полуширина по Z (с учётом свеса), length: длина вдоль X
    static void GableRoof(Transform p, string name, Vector3 center, float baseY, float rise, float halfSpan, float length, int rows, bool withRidge, bool mossy)
    {
        var root = Obj(p, name, center);
        float slopeLen = Mathf.Sqrt(halfSpan * halfSpan + rise * rise);
        float rowLen = slopeLen / rows;
        for (int side = 0; side < 2; side++)
        {
            float sgn = side == 0 ? 1f : -1f;
            Vector3 eave = new Vector3(0f, baseY, sgn * halfSpan);
            Vector3 ridge = new Vector3(0f, baseY + rise, 0f);
            Vector3 up = (ridge - eave).normalized;                               // вдоль ската вверх
            Vector3 normal = new Vector3(0f, halfSpan, sgn * rise).normalized;     // наружу и вверх
            var rot = Quaternion.LookRotation(up, normal);
            for (int i = 0; i < rows; i++)
            {
                float t = (i + 0.5f) / rows;
                Vector3 pos = Vector3.Lerp(eave, ridge, t);
                // ряд из 2–3 досок разной длины с зазорами и ступенчатой раскладкой
                int n = RI(2, 4);
                float x = -length * 0.5f;
                float step = length / n;
                for (int k = 0; k < n; k++)
                {
                    float w = step - R(0.02f, 0.07f);
                    float cx = -length * 0.5f + step * (k + 0.5f) + ((i % 2 == 0) ? 0.18f : -0.18f) * (k == 0 ? 0.3f : 0.1f);
                    var go = Part(root.transform, "RoofPlank", HutMeshLib.SoftCube, Mat(((i + k) % 3 == 0) ? "RoofShade" : "Roof"),
                        new Vector3(cx, pos.y + 0.035f, pos.z), new Vector3(w, 0.085f, rowLen * 1.22f), rot);
                    go.transform.localPosition += normal * (0.01f * (i % 2));
                    // лёгкий случайный перекос, чтобы доски жили
                    go.transform.localRotation *= Quaternion.Euler(R(-1.2f, 1.2f), 0f, R(-1.5f, 1.5f));
                }
            }
            if (mossy)
            {
                int nm = 2;
                for (int m = 0; m < nm; m++)
                {
                    float t = R(0.25f, 0.85f);
                    Vector3 pos = Vector3.Lerp(eave, ridge, t) + normal * 0.07f;
                    Blob(root.transform, "RoofMoss", new Vector3(R(-length * 0.4f, length * 0.4f), pos.y, pos.z), new Vector3(R(0.7f, 1.3f), 0.14f, R(0.5f, 0.8f)), "Moss");
                }
            }
        }
        if (withRidge)
        {
            Log(root.transform, "RidgeLog", new Vector3(-length * 0.5f - 0.12f, baseY + rise + 0.07f, 0f), new Vector3(length * 0.5f + 0.12f, baseY + rise + 0.07f, 0f), 0.15f, "WoodMid");
        }
    }

    // ---------------------------------------------------------------- мелочи оформления
    static void GrassTuft(Transform p, Vector3 pos, float size)
    {
        var g = Obj(p, "Grass", pos);
        int blades = RI(3, 6);
        for (int i = 0; i < blades; i++)
        {
            float a = i * 360f / blades + R(-20f, 20f);
            float h = size * R(0.7f, 1.2f);
            var b = ConeP(g.transform, "Blade", new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f, h * 0.5f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f), 0.045f, h, R() < 0.4f ? "Moss" : "Grass", 4,
                new Vector3(R(-14f, 14f), a, R(-14f, 14f)));
            b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void Flower(Transform p, Vector3 pos, string petal)
    {
        var f = Obj(p, "Flower", pos);
        float h = R(0.14f, 0.26f);
        Cyl(f.transform, "Stem", new Vector3(0, h * 0.5f, 0), 0.012f, h, "Leaf", 4).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Blob(f.transform, "Bloom", new Vector3(0, h + 0.02f, 0), new Vector3(0.11f, 0.07f, 0.11f), petal, 1).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Blob(f.transform, "Core", new Vector3(0, h + 0.05f, 0), new Vector3(0.04f, 0.03f, 0.04f), "RoofShade", 2).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void Rock(Transform p, Vector3 pos, float size, string mat = "StoneDark")
    {
        Blob(p, "Rock", pos + Vector3.up * size * 0.22f, new Vector3(size, size * 0.62f, size * R(0.8f, 1.1f)), mat);
    }

    static void Mushroom(Transform p, Vector3 pos, float s)
    {
        var m = Obj(p, "Mushroom", pos);
        Cyl(m.transform, "Stem", new Vector3(0, s * 0.35f, 0), s * 0.18f, s * 0.7f, "Cream", 6);
        Blob(m.transform, "Cap", new Vector3(0, s * 0.78f, 0), new Vector3(s * 0.95f, s * 0.55f, s * 0.95f), "Red", 3);
    }

    // стопка досок: n слоёв, каждый слой чуть смещён
    static GameObject PlankStack(Transform p, string name, Vector3 pos, float len, float wid, int layers, bool tied, float yaw)
    {
        var s = Obj(p, name, pos);
        s.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        float h = 0.075f;
        for (int i = 0; i < layers; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                float w = wid / 3f - 0.015f;
                Box(s.transform, "Board", new Vector3(R(-0.02f, 0.02f), h * (i + 0.5f), -wid * 0.5f + wid / 3f * (k + 0.5f)),
                    new Vector3(len * R(0.96f, 1.0f), h * 0.92f, w), ((i + k) % 2 == 0) ? "Plank" : "PlankDark",
                    new Vector3(0f, R(-1.2f, 1.2f), 0f));
            }
        }
        if (tied)
        {
            float H = h * layers;
            for (int k = -1; k <= 1; k += 2)
                Box(s.transform, "Strap", new Vector3(k * len * 0.28f, H * 0.5f, 0f), new Vector3(0.05f, H + 0.03f, wid + 0.03f), "Rope");
        }
        return s;
    }
}
#endif

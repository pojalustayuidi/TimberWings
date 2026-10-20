// Assets/_Project/Editor/Pets/PetCrateBuilder.cs (в папке Editor)
// Процедурная модель клети питомцев (мультяшная, low-poly, плоские грани). Unity 6, URP.
// Требует HutBuilder.cs (класс HutMesh) и Pets/PetCrateSite.cs.
//
// Из чего собрана клеть (всё внутри объекта Visual):
//   Mesh_*      корпус: ножки, угловые стойки, обвязка, доски 3 оттенков, шляпки гвоздей, верёвочные ручки по бокам,
//               выжженное клеймо с дирижаблем и облаками на передней и задней стенках
//   Lid         крышка на петлях (ось сзади). PetCrateSite открывает её, когда игрок открывает клеть
//   Hay         сено (у Небесной клети это пушистое облако) внутри
//   OpenGlow    мягкий свет изнутри, разгорается при открытии
// Лесная клеть: тёплое дерево, латунь. Небесная: крашеные голубые доски, кремовая обвязка, золото с лёгким свечением.
// Ассеты (меши и материалы) лежат в Assets/_Game/PetCrate. Перестроить: Tools > BuilderSim > Питомцы > Пересобрать модель клетей.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PetCrateBuilder
{
    const string Root = "Assets/_Game";
    const string Dir = "Assets/_Game/PetCrate";

    const float W = 1.3f, D = 1.0f;       // ширина (x) и глубина (z) корпуса, метры
    const float Pivot = 0.87f;            // высота верхней кромки: ось крышки

    enum MatId { WoodA, WoodB, WoodC, Frame, Metal, Rope, HayA, HayB, Stamp, Inner }

    class Pal
    {
        public Color a, b, c, frame, metal, rope, hayA, hayB, stamp, inner, glow;
        public float metalEmis;
    }

    static Pal GetPal(CrateType t)
    {
        var p = new Pal();
        switch (t)
        {
            case CrateType.Sky:
                p.a = new Color(0.45f, 0.66f, 0.86f); p.b = new Color(0.38f, 0.58f, 0.80f); p.c = new Color(0.53f, 0.73f, 0.91f);
                p.frame = new Color(0.97f, 0.91f, 0.76f);
                p.metal = new Color(1.00f, 0.82f, 0.30f); p.metalEmis = 0.5f;
                p.rope = new Color(0.95f, 0.88f, 0.70f);
                p.hayA = new Color(0.98f, 0.98f, 1.00f); p.hayB = new Color(0.82f, 0.90f, 1.00f);
                p.stamp = new Color(0.99f, 0.93f, 0.72f);
                p.inner = new Color(0.17f, 0.27f, 0.45f);
                p.glow = new Color(0.65f, 0.85f, 1.00f);
                break;
            case CrateType.River:
                p.a = new Color(0.36f, 0.62f, 0.58f); p.b = new Color(0.30f, 0.54f, 0.52f); p.c = new Color(0.44f, 0.70f, 0.64f);
                p.frame = new Color(0.40f, 0.27f, 0.15f);
                p.metal = new Color(0.82f, 0.74f, 0.55f); p.metalEmis = 0f;
                p.rope = new Color(0.90f, 0.82f, 0.58f);
                p.hayA = new Color(0.97f, 0.82f, 0.32f); p.hayB = new Color(0.88f, 0.68f, 0.22f);
                p.stamp = new Color(0.16f, 0.30f, 0.30f);
                p.inner = new Color(0.12f, 0.20f, 0.20f);
                p.glow = new Color(0.60f, 1.00f, 0.90f);
                break;
            default:   // Forest
                p.a = new Color(0.74f, 0.48f, 0.25f); p.b = new Color(0.66f, 0.42f, 0.21f); p.c = new Color(0.81f, 0.55f, 0.30f);
                p.frame = new Color(0.45f, 0.28f, 0.14f);
                p.metal = new Color(0.80f, 0.68f, 0.36f); p.metalEmis = 0f;
                p.rope = new Color(0.91f, 0.80f, 0.55f);
                p.hayA = new Color(0.98f, 0.81f, 0.29f); p.hayB = new Color(0.90f, 0.68f, 0.20f);
                p.stamp = new Color(0.31f, 0.18f, 0.08f);
                p.inner = new Color(0.20f, 0.12f, 0.07f);
                p.glow = new Color(1.00f, 0.85f, 0.45f);
                break;
        }
        return p;
    }

    static Color ColorOf(MatId id, Pal p)
    {
        switch (id)
        {
            case MatId.WoodA: return p.a;
            case MatId.WoodB: return p.b;
            case MatId.WoodC: return p.c;
            case MatId.Frame: return p.frame;
            case MatId.Metal: return p.metal;
            case MatId.Rope: return p.rope;
            case MatId.HayA: return p.hayA;
            case MatId.HayB: return p.hayB;
            case MatId.Stamp: return p.stamp;
            default: return p.inner;
        }
    }

    // ============================================================ точка входа
    // Строит (или перестраивает) модель внутри site.visual. Если Visual нет, создаёт его. Заполняет site.lid и site.glow.
    public static void Build(PetCrateSite site)
    {
        EnsureDirs();
        CrateType type = site.crate;
        Pal pal = GetPal(type);
        var rng = new System.Random(4242 + (int)type * 97);
        var mats = new Dictionary<MatId, Material>();

        Transform vis;
        if (site.visual != null) vis = site.visual.transform;
        else
        {
            var go = new GameObject("Visual");
            Undo.RegisterCreatedObjectUndo(go, "Модель клети");
            go.transform.SetParent(site.transform, false);
            vis = go.transform;
            site.visual = go;
        }
        vis.localScale = Vector3.one;
        for (int i = vis.childCount - 1; i >= 0; i--) Object.DestroyImmediate(vis.GetChild(i).gameObject);
        var oldCols = vis.GetComponents<Collider>();
        for (int i = 0; i < oldCols.Length; i++) Object.DestroyImmediate(oldCols[i]);

        // корпус
        var body = new Dictionary<MatId, HutMesh>();
        BuildBody(body, rng, pal);
        Flush(body, vis, type, "Body", pal, mats);

        // крышка (ось на задней кромке)
        var lidGo = new GameObject("Lid");
        lidGo.transform.SetParent(vis, false);
        lidGo.transform.localPosition = new Vector3(0f, Pivot, -D * 0.5f);
        var lid = new Dictionary<MatId, HutMesh>();
        BuildLid(lid, rng);
        Flush(lid, lidGo.transform, type, "Lid", pal, mats);

        // сено / облако
        var hayGo = new GameObject("Hay");
        hayGo.transform.SetParent(vis, false);
        var hay = new Dictionary<MatId, HutMesh>();
        BuildHay(hay, rng);
        Flush(hay, hayGo.transform, type, "Hay", pal, mats);

        // свет изнутри
        var lgo = new GameObject("OpenGlow");
        lgo.transform.SetParent(vis, false);
        lgo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var li = lgo.AddComponent<Light>();
        li.type = LightType.Point;
        li.color = pal.glow;
        li.range = 3.5f;
        li.intensity = 0f;
        li.shadows = LightShadows.None;

        // коллайдер: игрок упирается в клеть
        var col = vis.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.46f, 0f);
        col.size = new Vector3(W + 0.1f, 0.92f, D + 0.1f);

        site.lid = lidGo.transform;
        site.glow = li;
        EditorUtility.SetDirty(site);
        AssetDatabase.SaveAssets();
    }

    // ============================================================ корпус
    static void BuildBody(Dictionary<MatId, HutMesh> d, System.Random rng, Pal pal)
    {
        HutMesh fr = M(d, MatId.Frame, rng);
        HutMesh mt = M(d, MatId.Metal, rng);
        HutMesh rp = M(d, MatId.Rope, rng);
        HutMesh inn = M(d, MatId.Inner, rng);
        MatId[] tone = { MatId.WoodA, MatId.WoodB, MatId.WoodC, MatId.WoodB };
        float hx = W * 0.5f, hz = D * 0.5f;
        Quaternion q0 = Quaternion.identity;

        for (int ix = 0; ix < 2; ix++)
        {
            float sx = ix == 0 ? -1f : 1f;
            for (int iz = 0; iz < 2; iz++)
            {
                float sz = iz == 0 ? -1f : 1f;

                // ножка и стойка
                fr.Box(new Vector3(sx * (hx - 0.08f), 0.035f, sz * (hz - 0.08f)), new Vector3(0.12f, 0.07f, 0.12f), q0, 0.05f);
                fr.Box(new Vector3(sx * (hx - 0.055f), 0.49f, sz * (hz - 0.055f)), new Vector3(0.11f, 0.84f, 0.11f), q0, 0.04f);

                // шляпки гвоздей снизу и сверху стойки, на двух гранях
                float[] ys = { 0.2f, 0.78f };
                for (int k = 0; k < ys.Length; k++)
                {
                    Stud(mt, new Vector3(sx * (hx - 0.055f), ys[k], sz * hz), new Vector3(0f, 0f, sz));
                    Stud(mt, new Vector3(sx * hx, ys[k], sz * (hz - 0.055f)), new Vector3(sx, 0f, 0f));
                }
            }
        }

        // доски: 4 ряда на каждой стенке, зазор между ними, лёгкая кривизна
        for (int i = 0; i < 4; i++)
        {
            float y = 0.13f + 0.175f * (i + 0.5f);
            for (int iz = 0; iz < 2; iz++)
            {
                float sz = iz == 0 ? -1f : 1f;
                M(d, tone[(i + iz) % 4], rng).Box(new Vector3(0f, y, sz * (hz - 0.045f)), new Vector3(1.10f, 0.163f, 0.06f), q0, 0.03f);
            }
            for (int ix = 0; ix < 2; ix++)
            {
                float sx = ix == 0 ? -1f : 1f;
                M(d, tone[(i + ix + 1) % 4], rng).Box(new Vector3(sx * (hx - 0.045f), y, 0f), new Vector3(0.06f, 0.163f, 0.80f), q0, 0.03f);
            }
        }

        // верхняя и нижняя обвязка
        float[] ry = { 0.835f, 0.135f };
        for (int k = 0; k < 2; k++)
        {
            for (int iz = 0; iz < 2; iz++)
            {
                float sz = iz == 0 ? -1f : 1f;
                fr.Box(new Vector3(0f, ry[k], sz * (hz - 0.02f)), new Vector3(W - 0.02f, 0.07f, 0.06f), q0, 0.03f);
            }
            for (int ix = 0; ix < 2; ix++)
            {
                float sx = ix == 0 ? -1f : 1f;
                fr.Box(new Vector3(sx * (hx - 0.02f), ry[k], 0f), new Vector3(0.06f, 0.07f, D - 0.02f), q0, 0.03f);
            }
        }

        // дно внутри
        inn.Box(new Vector3(0f, 0.14f, 0f), new Vector3(W - 0.2f, 0.04f, D - 0.2f), q0, 0f);

        // верёвочные ручки по бокам
        RopeHandle(rp, mt, -1f);
        RopeHandle(rp, mt, 1f);

        // клеймо с дирижаблем на передней (+z) и задней (-z) стенках
        Stamp(d, rng, hz - 0.015f, 1f);
        Stamp(d, rng, -(hz - 0.015f), -1f);
    }

    static void Stud(HutMesh m, Vector3 p, Vector3 dir)
    {
        m.Cyl(p, p + dir * 0.02f, 0.02f, 0.014f, 5, 0f, true);
    }

    // петля из верёвки на боковой стенке: дуга из 6 сегментов, узлы и железные ушки на концах
    static void RopeHandle(HutMesh rp, HutMesh mt, float sx)
    {
        float hx = W * 0.5f;
        const int N = 6;
        Vector3 prev = Vector3.zero;
        for (int k = 0; k <= N; k++)
        {
            float t = k / (float)N * Mathf.PI;
            var p = new Vector3(sx * (hx + 0.01f + 0.17f * Mathf.Sin(t)), 0.52f - 0.04f * Mathf.Sin(t), -0.2f * Mathf.Cos(t));
            if (k > 0) rp.Cyl(prev, p, 0.03f, 0.03f, 5, 0.05f, true);
            prev = p;
        }
        for (int e = 0; e < 2; e++)
        {
            float z = e == 0 ? -0.2f : 0.2f;
            var a = new Vector3(sx * (hx + 0.01f), 0.52f, z);
            mt.Cyl(a, a + new Vector3(sx * 0.025f, 0f, 0f), 0.05f, 0.05f, 6, 0f, true);
            rp.Ico(a + new Vector3(sx * 0.04f, 0f, 0f), 0.045f, 0.12f);
        }
    }

    // ============================================================ клеймо
    static Vector2[] Ellipse(float cx, float cy, float a, float b, int n)
    {
        var r = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float ang = i * Mathf.PI * 2f / n;
            r[i] = new Vector2(cx + Mathf.Cos(ang) * a, cy + Mathf.Sin(ang) * b);
        }
        return r;
    }

    // плоский многоугольник на стенке: смотрит в сторону flip по оси z, x зеркалится, чтобы с обеих сторон рисунок читался
    static void Poly(HutMesh m, Vector2[] pts, float cx, float cy, float z, float flip, float s)
    {
        Vector2 cen = Vector2.zero;
        for (int i = 0; i < pts.Length; i++) cen += pts[i];
        cen /= pts.Length;
        Vector3 inside = new Vector3(cx, cy, z - flip * 0.2f);
        Vector3 a = new Vector3(cx + cen.x * s * flip, cy + cen.y * s, z);
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 p = pts[i], q = pts[(i + 1) % pts.Length];
            Vector3 b = new Vector3(cx + p.x * s * flip, cy + p.y * s, z);
            Vector3 c = new Vector3(cx + q.x * s * flip, cy + q.y * s, z);
            m.TriOut(a, b, c, inside);
        }
    }

    static void Stamp(Dictionary<MatId, HutMesh> d, System.Random rng, float faceZ, float flip)
    {
        HutMesh st = M(d, MatId.Stamp, rng);
        HutMesh lt = M(d, MatId.WoodC, rng);
        float z1 = faceZ + flip * 0.006f, z2 = faceZ + flip * 0.012f;
        const float cx = 0f, cy = 0.5f, s = 1.3f;

        // корпус дирижабля, хвостовые стабилизаторы, гондола
        Poly(st, Ellipse(0f, 0.04f, 0.18f, 0.065f, 12), cx, cy, z1, flip, s);
        Poly(st, new[] { new Vector2(-0.13f, 0.08f), new Vector2(-0.22f, 0.14f), new Vector2(-0.19f, 0.05f) }, cx, cy, z1, flip, s);
        Poly(st, new[] { new Vector2(-0.13f, 0.00f), new Vector2(-0.22f, -0.06f), new Vector2(-0.19f, 0.03f) }, cx, cy, z1, flip, s);
        Poly(st, new[] { new Vector2(-0.04f, -0.02f), new Vector2(0.06f, -0.02f), new Vector2(0.05f, -0.065f), new Vector2(-0.03f, -0.065f) }, cx, cy, z1, flip, s);

        // две светлые полосы на корпусе
        Poly(lt, new[] { new Vector2(-0.065f, -0.01f), new Vector2(-0.04f, -0.01f), new Vector2(-0.04f, 0.09f), new Vector2(-0.065f, 0.09f) }, cx, cy, z2, flip, s);
        Poly(lt, new[] { new Vector2(0.00f, -0.01f), new Vector2(0.025f, -0.01f), new Vector2(0.025f, 0.09f), new Vector2(0.00f, 0.09f) }, cx, cy, z2, flip, s);

        // облачка под ним
        Poly(st, Ellipse(-0.13f, -0.13f, 0.10f, 0.032f, 8), cx, cy, z1, flip, s);
        Poly(st, Ellipse(0.05f, -0.16f, 0.12f, 0.038f, 8), cx, cy, z1, flip, s);
        Poly(st, Ellipse(0.18f, -0.12f, 0.07f, 0.028f, 8), cx, cy, z1, flip, s);
    }

    // ============================================================ крышка (локальные координаты от оси)
    static void BuildLid(Dictionary<MatId, HutMesh> d, System.Random rng)
    {
        HutMesh fr = M(d, MatId.Frame, rng);
        HutMesh mt = M(d, MatId.Metal, rng);
        MatId[] tone = { MatId.WoodA, MatId.WoodB, MatId.WoodC, MatId.WoodB, MatId.WoodA };
        Quaternion q0 = Quaternion.identity;

        // 5 досок вдоль x, уложены по глубине
        for (int i = 0; i < 5; i++)
        {
            float z = -0.02f + 0.212f * (i + 0.5f);
            M(d, tone[i], rng).Box(new Vector3(0f, 0.025f, z), new Vector3(W + 0.04f, 0.05f, 0.2f), q0, 0.03f);
        }

        // две поперечные планки сверху и передний/задний борт
        float bx = W * 0.5f - 0.12f;
        fr.Box(new Vector3(-bx, 0.065f, 0.51f), new Vector3(0.09f, 0.04f, D + 0.06f), q0, 0.03f);
        fr.Box(new Vector3(bx, 0.065f, 0.51f), new Vector3(0.09f, 0.04f, D + 0.06f), q0, 0.03f);
        fr.Box(new Vector3(0f, 0.035f, 1.045f), new Vector3(W + 0.06f, 0.07f, 0.05f), q0, 0.03f);
        fr.Box(new Vector3(0f, 0.035f, -0.015f), new Vector3(W + 0.06f, 0.07f, 0.05f), q0, 0.03f);

        // гвозди на планках
        for (int s = 0; s < 2; s++)
        {
            float x = s == 0 ? -bx : bx;
            Stud(mt, new Vector3(x, 0.085f, 0.1f), Vector3.up);
            Stud(mt, new Vector3(x, 0.085f, 0.92f), Vector3.up);
        }

        // петли сзади: стальная накладка и ствол по оси
        for (int s = 0; s < 2; s++)
        {
            float x = s == 0 ? -0.4f : 0.4f;
            mt.Box(new Vector3(x, 0.09f, 0.1f), new Vector3(0.16f, 0.015f, 0.2f), q0, 0.02f);
            mt.Cyl(new Vector3(x - 0.07f, 0f, 0f), new Vector3(x + 0.07f, 0f, 0f), 0.03f, 0.03f, 6, 0f, true);
        }

        // замок спереди: пластина и кольцо
        mt.Box(new Vector3(0f, 0.03f, 1.075f), new Vector3(0.14f, 0.12f, 0.02f), q0, 0.02f);
        mt.Cyl(new Vector3(0f, -0.04f, 1.085f), new Vector3(0f, -0.04f, 1.10f), 0.035f, 0.035f, 6, 0f, true);
    }

    // ============================================================ сено (у Небесной клети пушистое облако)
    static void BuildHay(Dictionary<MatId, HutMesh> d, System.Random rng)
    {
        HutMesh ha = M(d, MatId.HayA, rng);
        HutMesh hb = M(d, MatId.HayB, rng);

        hb.Box(new Vector3(0f, 0.40f, 0f), new Vector3(W - 0.2f, 0.5f, D - 0.2f), Quaternion.identity, 0.02f);
        for (int i = 0; i < 6; i++)
            ha.Ico(new Vector3(R(rng, -0.4f, 0.4f), 0.64f, R(rng, -0.25f, 0.25f)), 0.16f, 0.25f);

        for (int i = 0; i < 64; i++)
        {
            HutMesh m = (i % 3 == 0) ? hb : ha;
            Vector3 p0 = new Vector3(R(rng, -0.5f, 0.5f), R(rng, 0.56f, 0.66f), R(rng, -0.36f, 0.36f));
            Vector3 dir = new Vector3(R(rng, -0.7f, 0.7f), R(rng, 0.5f, 1f), R(rng, -0.7f, 0.7f)).normalized;
            float len = R(rng, 0.16f, 0.3f);
            len = Mathf.Min(len, (0.83f - p0.y) / Mathf.Max(0.3f, dir.y));   // не выше кромки: крышка закрывается чисто
            Vector3 p1 = p0 + dir * len;
            p1.x = Mathf.Clamp(p1.x, -0.56f, 0.56f);
            p1.z = Mathf.Clamp(p1.z, -0.40f, 0.40f);
            m.Beam(p0, p1, 0.018f, 0.018f, 0.1f);
        }
    }

    // ============================================================ утилиты
    static float R(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    static HutMesh M(Dictionary<MatId, HutMesh> d, MatId id, System.Random rng)
    {
        HutMesh m;
        if (!d.TryGetValue(id, out m)) { m = new HutMesh(rng); d[id] = m; }
        return m;
    }

    static void EnsureDirs()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "_Game");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder(Root, "PetCrate");
    }

    static void Flush(Dictionary<MatId, HutMesh> d, Transform parent, CrateType type, string part, Pal pal, Dictionary<MatId, Material> mats)
    {
        foreach (var kv in d)
        {
            if (kv.Value.Count == 0) continue;
            string nm = "PetCrate_" + type + "_" + part + "_" + kv.Key;
            var go = new GameObject("Mesh_" + kv.Key);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = SaveMesh(kv.Value.ToMesh(nm), nm);
            mr.sharedMaterial = GetMat(type, kv.Key, pal, mats);
        }
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = Dir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material GetMat(CrateType type, MatId id, Pal pal, Dictionary<MatId, Material> cache)
    {
        Material m;
        if (cache.TryGetValue(id, out m)) return m;

        Color col = ColorOf(id, pal);
        float emis = id == MatId.Metal ? pal.metalEmis : 0f;
        string path = Dir + "/PetCrate_" + type + "_" + id + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "PetCrate_" + type + "_" + id };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", id == MatId.Metal ? 0.35f : 0.05f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", id == MatId.Metal ? 0.35f : 0.05f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emis > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * emis);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        cache[id] = m;
        return m;
    }
}

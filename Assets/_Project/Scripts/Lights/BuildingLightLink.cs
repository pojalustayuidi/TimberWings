// Assets/_Project/Scripts/Lights/BuildingLightLink.cs (НЕ в Editor)
// Огни у построек. Создаётся сам: FireLightPool после загрузки сцены вызывает FireLightScan.Run().
//
//  Хижина   : фонарь у двери (уже есть в сцене: Light «LanternLight» в Stage3) + светящиеся окна (меши со стеклом «Glass»);
//  Рынок    : гирлянда лампочек вдоль «передней» стороны крыши (строится в рантайме под Stage3 лавки);
//  Палатка  : фонари OuterLantern / InnerLantern (уже есть в сцене, у TentStages.lights);
//  Мост     : 4 фонаря на столбиках по углам, только после постройки (под объектом стадии 3 RiverBridge);
//  Костёр   : CampLight (уже есть в сцене) + мерцание пламени.
// Все Light из сцены с именами camp / lantern / lamp / torch / фонар превращаются в FireSource: сам Light остаётся в сцене
// ВЫКЛЮЧЕННЫМ (все ссылки на него живы), а настоящий свет рядом с игроком даёт пул (FireLightPool).
//
// Привязка к стадиям (BuildingLightLink на объекте с BuildingController):
//   • StageBuilt / Finished  -> огни разгораются плавно;
//   • GameEvents.BuildingRestored(id, stage) -> огни встают в нужное состояние сразу, без анимации (загрузка сохранения).
// Свои точки для мостовых фонарей: добавь в модель моста пустые объекты с именем «LightAnchor…», тогда процедурные столбики не строятся.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingLightLink : MonoBehaviour
{
    class Entry { public FireSource src; public int minStage; }

    BuildingController build;
    BuildSiteKind kind;
    Transform root;
    bool propsBuilt, subscribed, inited;
    readonly List<Entry> entries = new List<Entry>();

    public Transform Root { get { return root; } }

    // номер вида 1..3: так же считает BuildStageLink
    int ViewStage()
    {
        if (build == null) return 1;
        return build.StageTotal > 1 ? build.StageCurrent : (build.IsFinished ? 3 : 1);
    }

    internal void Init(BuildingController b)
    {
        if (inited) return;
        inited = true;
        build = b;
        kind = b.SiteKind;
        root = ResolveRoot();
        Subscribe();
        BuildProps();
        Refresh(false);
    }

    void OnEnable() { Subscribe(); }

    void OnDisable()
    {
        if (!subscribed) return;
        if (build != null) { build.StageBuilt -= OnBuilt; build.Finished -= OnBuilt; }
        GameEvents.BuildingRestored -= OnRestored;
        subscribed = false;
    }

    void Subscribe()
    {
        if (subscribed || build == null) return;
        build.StageBuilt += OnBuilt;
        build.Finished += OnBuilt;
        GameEvents.BuildingRestored += OnRestored;
        subscribed = true;
    }

    // сдан этап: огни, которые должны гореть на этой стадии, разгораются плавно
    void OnBuilt() { Refresh(true); }

    // загрузка сохранения: без анимации
    void OnRestored(string id, int stage)
    {
        if (build == null || id != build.SaveId) return;
        Refresh(false);
    }

    internal void Add(FireSource s, int minStage)
    {
        if (s == null) return;
        for (int i = 0; i < entries.Count; i++) if (entries[i].src == s) return;
        var e = new Entry { src = s, minStage = minStage };
        entries.Add(e);
        s.SetOn(ViewStage() >= minStage, false);
    }

    void Refresh(bool animate)
    {
        int vs = ViewStage();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.src == null) continue;
            e.src.SetOn(vs >= e.minStage, animate);
        }
    }

    // ---------------------------------------------------------------- корень здания
    Transform ResolveRoot()
    {
        switch (kind)
        {
            case BuildSiteKind.Market:
                { var s = Nearest<ShopStages>(); if (s != null) return s.transform; break; }
            case BuildSiteKind.Tent:
                { var t = Nearest<TentStages>(); if (t != null) return t.transform; break; }
            case BuildSiteKind.Bridge:
                {
                    RiverBridge rb = (StoryFlow.Instance != null && StoryFlow.Instance.riverBridge != null) ? StoryFlow.Instance.riverBridge : Nearest<RiverBridge>();
                    if (rb != null) return rb.transform;
                    break;
                }
        }
        if (build.stages != null) return build.stages.transform;
        var hs = build.GetComponentInParent<HutStages>(true);
        if (hs != null) return hs.transform;
        return build.transform.parent != null ? build.transform.parent : build.transform;
    }

    T Nearest<T>() where T : Component
    {
        T best = null;
        float bd = float.MaxValue;
        foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            float d = (c.transform.position - build.transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    internal int RootDepth()
    {
        int d = 0;
        for (var t = root; t != null; t = t.parent) d++;
        return d;
    }

    // ---------------------------------------------------------------- процедурные огни
    void BuildProps()
    {
        if (propsBuilt || root == null) return;
        propsBuilt = true;
        switch (kind)
        {
            case BuildSiteKind.Hut: BuildWindows(); break;
            case BuildSiteKind.Market: BuildGarland(); break;
            case BuildSiteKind.Bridge: BuildBridgeLamps(); break;
        }
    }

    // светящиеся окна: каждый меш со стеклом получает emissive и glow-пятно с наружной стороны
    void BuildWindows()
    {
        Bounds rb;
        if (!FireProps.LocalBounds(root, out rb)) return;
        Vector3 center = root.TransformPoint(rb.center);

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = r.sharedMaterial;
            if (m == null || m.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (r.GetComponent<MeshFilter>() == null) continue;

            Bounds wb = FireProps.WorldBounds(r);
            Vector3 c = wb.center, size = wb.size;
            // тонкая ось стекла и есть нормаль окна
            Vector3 outward = size.x < size.z
                ? new Vector3(Mathf.Sign(c.x - center.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(c.z - center.z));

            var go = new GameObject("WindowGlow");
            go.transform.SetParent(r.transform, true);
            go.transform.position = c + outward * 0.12f;
            var fs = go.AddComponent<FireSource>();
            fs.preset = FlickerPreset.Window;
            fs.simOnly = true;
            fs.color = new Color(1f, 0.8f, 0.45f);
            fs.emissive = new Renderer[] { r };
            fs.emissiveStrength = 1.3f;
            fs.glowSize = Mathf.Clamp(Mathf.Max(size.y, Mathf.Max(size.x, size.z)) * 1.8f, 0.6f, 2.2f);
            Add(fs, 0);
        }
    }

    // гирлянда: по «передней» стороне лавки (той, что ближе к площадке сдачи), под навесом
    void BuildGarland()
    {
        var shop = root.GetComponent<ShopStages>();
        GameObject s3 = shop != null ? shop.stage3 : null;
        if (s3 == null) return;
        Transform p = s3.transform;

        Bounds b;
        if (!FireProps.LocalBounds(p, out b)) return;
        Material wire = FireProps.Lit(new Color(0.18f, 0.13f, 0.09f), false, "GarlandWire");
        Material bulb = FireProps.Lit(new Color(1f, 0.85f, 0.5f), true, "GarlandBulb");
        if (wire == null || bulb == null) return;

        Vector3 ctrl = p.InverseTransformPoint(build.transform.position) - b.center;
        bool alongX = Mathf.Abs(ctrl.z) >= Mathf.Abs(ctrl.x);   // фронт смотрит по z: гирлянда идёт вдоль x
        float sign = (alongX ? ctrl.z : ctrl.x) >= 0f ? 1f : -1f;
        float y = b.min.y + b.size.y * 0.7f;
        Vector3 a, c2;
        if (alongX)
        {
            float face = (sign > 0f ? b.max.z : b.min.z) + sign * 0.12f;
            a = new Vector3(b.min.x + 0.25f, y, face);
            c2 = new Vector3(b.max.x - 0.25f, y, face);
        }
        else
        {
            float face = (sign > 0f ? b.max.x : b.min.x) + sign * 0.12f;
            a = new Vector3(face, y, b.min.z + 0.25f);
            c2 = new Vector3(face, y, b.max.z - 0.25f);
        }
        float len = Vector3.Distance(a, c2);
        if (len < 1f) return;

        int n = Mathf.Clamp(Mathf.RoundToInt(len / 0.55f), 5, 14);
        float sag = Mathf.Min(0.35f, len * 0.06f);

        var garland = new GameObject("Garland");
        garland.transform.SetParent(p, false);

        var wb = new FireProps.MeshBuilder();
        var bb = new FireProps.MeshBuilder();
        const int segs = 14;
        Vector3 prev = a;
        for (int i = 1; i <= segs; i++)
        {
            Vector3 cur = GarlandPoint(a, c2, i / (float)segs, sag);
            wb.AddBeam(prev, cur, 0.025f);
            prev = cur;
        }
        var pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 pt = GarlandPoint(a, c2, (i + 0.5f) / n, sag) + Vector3.down * 0.06f;
            pts[i] = pt;
            bb.AddBox(pt, Quaternion.identity, new Vector3(0.09f, 0.11f, 0.09f));
        }
        FireProps.MeshObject(garland.transform, "Wire", wb.ToMesh("GarlandWire"), wire);
        var bulbs = FireProps.MeshObject(garland.transform, "Bulbs", bb.ToMesh("GarlandBulbs"), bulb);

        var fs = garland.AddComponent<FireSource>();
        fs.preset = FlickerPreset.Garland;
        fs.simOnly = true;
        fs.color = new Color(1f, 0.85f, 0.5f);
        fs.emissive = new Renderer[] { bulbs.GetComponent<Renderer>() };
        fs.emissiveStrength = 2.4f;
        fs.glowSize = 0.5f;
        fs.glowPoints = pts;   // garland стоит в нуле локальной системы Stage3, поэтому точки совпадают
        Add(fs, 3);
    }

    static Vector3 GarlandPoint(Vector3 a, Vector3 b, float t, float sag)
    {
        return Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
    }

    // фонари моста: свои точки (LightAnchor…) или 4 столбика по углам габарита стадии 3
    void BuildBridgeLamps()
    {
        var rb = root.GetComponent<RiverBridge>();
        Transform parent = (rb != null && rb.stages != null && rb.stages.Length >= 3 && rb.stages[2] != null) ? rb.stages[2].transform : root;

        var anchors = new List<Transform>();
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("LightAnchor", System.StringComparison.OrdinalIgnoreCase)) anchors.Add(t);
        if (anchors.Count > 0)
        {
            foreach (var t in anchors)
            {
                if (t.GetComponent<FireSource>() != null) continue;
                var fs = t.gameObject.AddComponent<FireSource>();
                FillLantern(fs);
                Add(fs, 3);
            }
            return;
        }

        Bounds b;
        if (!FireProps.LocalBounds(parent, out b)) return;
        Material wood = FireProps.Lit(new Color(0.40f, 0.27f, 0.15f), false, "LampPost");
        Material lamp = FireProps.Lit(new Color(1f, 0.82f, 0.42f), true, "LampGlass");
        if (wood == null || lamp == null) return;

        const float inset = 0.25f;
        float y0 = b.max.y - 0.05f;
        float[] xs = { b.min.x + inset, b.max.x - inset };
        float[] zs = { b.min.z + inset, b.max.z - inset };
        int k = 0;
        for (int ix = 0; ix < 2; ix++)
            for (int iz = 0; iz < 2; iz++)
            {
                var go = new GameObject("BridgeLamp_" + (k++));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(xs[ix], y0, zs[iz]);

                var pb = new FireProps.MeshBuilder();
                pb.AddBox(new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(0.09f, 1.0f, 0.09f));   // столбик
                pb.AddBox(new Vector3(0f, 1.2f, 0f), Quaternion.identity, new Vector3(0.24f, 0.04f, 0.24f));   // крышка
                pb.AddBox(new Vector3(0f, 0.98f, 0f), Quaternion.identity, new Vector3(0.22f, 0.03f, 0.22f));  // донце
                FireProps.MeshObject(go.transform, "Post", pb.ToMesh("LampPost"), wood);

                var lb = new FireProps.MeshBuilder();
                lb.AddBox(new Vector3(0f, 1.09f, 0f), Quaternion.identity, new Vector3(0.17f, 0.2f, 0.17f));
                var lampGo = FireProps.MeshObject(go.transform, "Lantern", lb.ToMesh("LampGlass"), lamp);

                var fs = go.AddComponent<FireSource>();
                FillLantern(fs);
                fs.lightOffset = new Vector3(0f, 1.09f, 0f);
                fs.glowPoints = new[] { new Vector3(0f, 1.09f, 0f) };
                fs.emissive = new Renderer[] { lampGo.GetComponent<Renderer>() };
                Add(fs, 3);
            }
    }

    static void FillLantern(FireSource fs)
    {
        fs.preset = FlickerPreset.Lantern;
        fs.color = new Color(1f, 0.76f, 0.42f);
        fs.intensity = 2.2f;
        fs.range = 5f;
        fs.glowSize = 0.9f;
    }
}

// ====================================================================================== сканер сцены
public static class FireLightScan
{
    static readonly string[] LightNames = { "camp", "lantern", "lamp", "torch", "фонар" };

    public static void Run()
    {
        // 1) связи со стройками (на повторном проходе уже созданные пропускаются)
        var links = new List<BuildingLightLink>();
        foreach (var b in Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var link = b.GetComponent<BuildingLightLink>();
            if (link == null)
            {
                link = b.gameObject.AddComponent<BuildingLightLink>();
                link.Init(b);
            }
            links.Add(link);
        }

        // 2) меши ламп: их emissive будет следовать за огнём рядом
        var lamps = new List<MeshRenderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material m = r.sharedMaterial;
            if (m != null && m.name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0) lamps.Add(r);
        }

        // 3) Light из сцены -> FireSource (Light остаётся, но выключается: ссылки на него целы)
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) continue;
            if (l.GetComponent<FirePoolLight>() != null || l.GetComponent<FireSource>() != null) continue;
            string n = l.name.ToLowerInvariant();
            if (!NameMatches(n)) continue;
            Convert(l, n.Contains("camp"), lamps, links);
        }
    }

    static bool NameMatches(string lower)
    {
        for (int i = 0; i < LightNames.Length; i++) if (lower.Contains(LightNames[i])) return true;
        return false;
    }

    static void Convert(Light l, bool campfire, List<MeshRenderer> lamps, List<BuildingLightLink> links)
    {
        var s = l.gameObject.AddComponent<FireSource>();
        s.authoredLight = l;
        s.preset = campfire ? FlickerPreset.Campfire : FlickerPreset.Lantern;
        s.color = l.color;
        s.intensity = l.intensity;
        s.range = l.range;
        s.glowSize = campfire ? 2.2f : 0.9f;
        if (!campfire) s.emissive = FindLampRenderers(l.transform.position, lamps);   // у костра пламя светится своим материалом
        l.enabled = false;

        // к какой стройке относится: самая «глубокая» из тех, у кого этот Light среди потомков
        BuildingLightLink best = null;
        int bestDepth = -1;
        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            if (link == null || link.Root == null || !l.transform.IsChildOf(link.Root)) continue;
            int d = link.RootDepth();
            if (d > bestDepth) { bestDepth = d; best = link; }
        }
        if (best != null) best.Add(s, 0);
    }

    static Renderer[] FindLampRenderers(Vector3 pos, List<MeshRenderer> lamps)
    {
        var found = new List<Renderer>(2);
        for (int i = 0; i < lamps.Count && found.Count < 3; i++)
        {
            var r = lamps[i];
            if (r == null || r.GetComponent<MeshFilter>() == null) continue;
            if (FireProps.WorldBounds(r).SqrDistance(pos) <= 0.35f * 0.35f) found.Add(r);
        }
        return found.ToArray();
    }
}

// ====================================================================================== процедурные меши и материалы
public static class FireProps
{
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { mats.Clear(); }

    // простой Lit-материал; emissive=true включает _EMISSION (цвет задаёт FireSource через MaterialPropertyBlock)
    public static Material Lit(Color c, bool emissive, string name)
    {
        Material m;
        if (mats.TryGetValue(name, out m) && m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null)
        {
            Debug.LogWarning("[FireProps] Не найден шейдер Lit (вырезан из билда?): " + name + " не создан. Добавь Universal Render Pipeline/Lit в Always Included Shaders.");
            return null;
        }
        m = new Material(sh) { name = "Fire_" + name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", Color.black);
        }
        mats[name] = m;
        return m;
    }

    public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.AddComponent<FireMeshOwner>().mesh = mesh;
        return go;
    }

    // габарит всех мешей под root в ЛОКАЛЬНЫХ координатах root (работает и для выключенных объектов, в отличие от Renderer.bounds)
    public static bool LocalBounds(Transform root, out Bounds result)
    {
        bool has = false;
        result = new Bounds();
        Matrix4x4 toLocal = root.worldToLocalMatrix;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || mf.GetComponent<FireMeshOwner>() != null) continue;
            Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
            Bounds mb = mesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                else result.Encapsulate(p);
            }
        }
        return has;
    }

    public static Bounds WorldBounds(MeshRenderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return r.bounds;
        Matrix4x4 m = r.transform.localToWorldMatrix;
        Bounds mb = mf.sharedMesh.bounds;
        Vector3 c = mb.center, e = mb.extents;
        Bounds res = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            res.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return res;
    }

    // минимальный сборщик мешей из коробок (плоские нормали)
    public class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<int> tri = new List<int>();
        static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

        public void AddBox(Vector3 center, Quaternion rot, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            for (int ax = 0; ax < 3; ax++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                    Vector3 N = Axes[ax] * sg, U = Axes[a1], V = Axes[a2];
                    float hN = h[ax], hU = h[a1], hV = h[a2];
                    Vector3 p0 = N * hN - U * hU - V * hV;
                    Vector3 p1 = N * hN + U * hU - V * hV;
                    Vector3 p2 = N * hN + U * hU + V * hV;
                    Vector3 p3 = N * hN - U * hU + V * hV;
                    int b = v.Count;
                    Vector3 wn = rot * N;
                    v.Add(center + rot * p0); v.Add(center + rot * p1); v.Add(center + rot * p2); v.Add(center + rot * p3);
                    for (int i = 0; i < 4; i++) nrm.Add(wn);
                    // порядок обхода выбираем так, чтобы геометрическая нормаль смотрела наружу (Unity: front = cross(b-a, c-a))
                    Vector3 geo = Vector3.Cross(rot * (p1 - p0), rot * (p2 - p0));
                    if (Vector3.Dot(geo, wn) >= 0f) { tri.Add(b); tri.Add(b + 1); tri.Add(b + 2); tri.Add(b); tri.Add(b + 2); tri.Add(b + 3); }
                    else { tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2); }
                }
        }

        // тонкая коробка вдоль отрезка a-b
        public void AddBeam(Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            AddBox((a + b) * 0.5f, Quaternion.LookRotation(d / len, Vector3.up), new Vector3(thickness, thickness, len));
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(nrm);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}

// владелец процедурного меша: при уничтожении объекта (смена сцены) освобождает меш
public class FireMeshOwner : MonoBehaviour
{
    public Mesh mesh;
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}

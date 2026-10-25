// Assets/_Project/Scripts/Forest/MysticGlade/MysticGladeBuilder.cs (НЕ в Editor)
// «Таинственная поляна» вокруг Дедушки (LandmarkTree) на метке PAD_Forest. Заменяет старую поляну рубки.
// По паттерну ChopGroveBuilder: вешается на метку сам (MysticGladeBootstrap), собирает кодом в двух-четырёх мешах (GroveMesh, цвет вершин, Project/CandyToon).
//
// Состав (локальные оси: мир, центр = ствол Дедушки; вход со стороны костра CAMPFIRE_Point):
//   • 6 скамеек кольцом, лицом к дереву; в кольце оставлен проход к входу
//   • 6 РУБИМЫХ деревьев за скамейками (тег Resource + ResourceItem, всё как у обычных: рубка, пень, респаун)
//   • кольцо рунных плит вокруг дерева, 5 менгиров со светящимися рунами и кристаллами, «ворота» из двух кристальных столбов у входа
//   • светящиеся грибы, кристаллы у корней деревьев (свечение: FireSource, ваша система огней)
//   • эффекты: MysticGladeVibe (пульсирующий свет, споры, светлячки, лепестки, туман, кружащие огоньки; ночью ярче)
// Части: MysticGladeBuilder.cs (сборка, земля, деревья), .Props.cs (скамейки, менгиры, кристаллы, грибы), MysticGladeVibe.cs (живость).
//
// Старое (в Play Mode, SetActive(false), сцена не меняется): корни ChopZone (забор, арки, ZoneTree_*), ChopCamp, ChopGladeDecor, GrandpaGrove
// из IslandV2/_Manual, ChopGroveBuilder рядом и GladeVibe. Центр = Дедушка (PAD_Grandpa, GrandpaVibe), он стоит в ~10 м от PAD_Forest.
// Деревья (рубимые и декор на ToonSway) внутри clearingRadius вытесняются за его границу. LandmarkTree не трогаем.
// В Edit Mode собирается только превью без деревьев (DontSave, в сцену не пишется): ПКМ по компоненту → «Собрать зону».
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MysticPal
{
    public static readonly Color Wood = new Color(0.64f, 0.46f, 0.58f);
    public static readonly Color WoodLight = new Color(0.84f, 0.66f, 0.74f);
    public static readonly Color Stone = new Color(0.66f, 0.60f, 0.84f);
    public static readonly Color StoneDark = new Color(0.48f, 0.43f, 0.68f);
    public static readonly Color Moss = new Color(0.45f, 0.76f, 0.58f);
    public static readonly Color Violet = new Color(0.78f, 0.55f, 1.00f);
    public static readonly Color Cyan = new Color(0.45f, 1.00f, 0.92f);
    public static readonly Color Pink = new Color(1.00f, 0.62f, 0.80f);
    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.88f);
    public static readonly Color[] Crystals = { Violet, Cyan, Pink };
}

public static class MysticGladeBootstrap
{
    public static readonly string[] padNames = { "PAD_Forest" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { Attach(); }

    static void Attach()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || System.Array.IndexOf(padNames, t.name) < 0) continue;
            if (t.GetComponent<MysticGladeBuilder>() != null) continue;
            t.gameObject.AddComponent<MysticGladeBuilder>();
        }
    }
}

[DefaultExecutionOrder(-100)]   // раньше Start/Awake деревьев: сдвинутые деревья запомнят уже новое место
[DisallowMultipleComponent]
public partial class MysticGladeBuilder : MonoBehaviour
{
    public const string RootName = "_MysticGlade";

    [Header("Сборка")]
    public bool buildOnStart = true;
    public int seed = 5309;

    [Header("Центр")]
    [Tooltip("Центр зоны = Дедушка (GrandpaVibe), иначе ближайший LandmarkTree, в этом радиусе от метки")]
    public bool snapToLandmark = true;
    public float landmarkSearch = 30f;

    [Header("Вход")]
    public bool autoEntry = true;
    [Tooltip("Если autoEntry выключен: направление ОТ центра К входу, градусы мира (0 = +Z, 90 = +X)")]
    public float entryYaw = 180f;

    [Header("Раскладка (метры от ствола)")]
    public float runeRadius = 4.8f;
    public float benchRadius = 7.2f;
    public int benchCount = 6;
    public float treeRadius = 12.5f;
    public int treeCount = 6;
    public float clearingRadius = 15f;

    [Header("Детали")]
    [Range(0f, 2f)] public float scatterDensity = 1f;
    public bool addVibe = true;

    [Header("Старое")]
    [Tooltip("Сдвинуть деревья из зоны за её границу (только в Play Mode)")]
    public bool relocateTrees = true;
    [Tooltip("Выключить старую поляну рубки: ChopGroveBuilder рядом, ChopGladeDecor, GladeVibe (только в Play Mode)")]
    public bool replaceOldGlade = true;
    public string oldDecorName = "ChopGladeDecor";

    // ---------------------------------------------------------------- состояние сборки
    Transform root, collidersRoot, liveRoot;
    GroveMesh props, glowCrystal, glowRune, glowShroom;
    readonly List<Vector3> crystalPts = new List<Vector3>();
    readonly List<Vector3> shroomPts = new List<Vector3>();
    readonly List<Vector3> foot = new List<Vector3>();   // x, z, радиус занятых мест
    Vector3 center;
    float entryDeg;
    static readonly RaycastHit[] hitBuf = new RaycastHit[24];
    static Material mat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { mat = null; }

    void Awake()
    {
        if (buildOnStart && Application.isPlaying && transform.Find(RootName) == null) Build();
    }

    // ---------------------------------------------------------------- центр и вход
    Vector3 FindCenter()
    {
        Vector3 c = transform.position;
        if (!snapToLandmark) return c;
        // сначала Дедушка (у него GrandpaVibe), иначе любое LandmarkTree; рядом стоят ещё деревья-хранители, их центром не берём
        Transform best = null;
        float bd = landmarkSearch * landmarkSearch;
        foreach (var g in FindObjectsByType<GrandpaVibe>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            float d = (g.transform.position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = g.transform; }
        }
        if (best == null)
        {
            bd = landmarkSearch * landmarkSearch;
            foreach (var t in FindObjectsByType<LandmarkTree>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                float d = (t.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = t.transform; }
            }
        }
        if (best != null) c = best.position;
        return c;
    }

    float ResolveEntryDeg()
    {
        float yaw = entryYaw;
        if (autoEntry)
        {
            var cp = GameObject.Find("CAMPFIRE_Point");
            if (cp != null)
            {
                Vector3 d = cp.transform.position - center; d.y = 0f;
                if (d.sqrMagnitude > 4f) yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
        }
        return yaw;
    }

    // (x, z) в локальных метрах от центра; deg: 0 = +Z, 90 = +X
    static Vector2 Pos(float deg, float r)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
    }

    // ---------------------------------------------------------------- главный метод
    [ContextMenu("Собрать зону")]
    public void Build()
    {
        Clear();
        center = FindCenter();
        if (Application.isPlaying && replaceOldGlade)
        {
            DisableOldChopGrove();
            HideOldGlade();   // до сборки: коллайдеры старого забора и лавок не должны попасть под лучи GroundY
        }
        Physics.SyncTransforms();
        var rg = new GameObject(RootName);
        root = rg.transform;
        root.SetParent(transform, false);
        root.position = center;
        root.rotation = Quaternion.identity;
        Vector3 ls = transform.lossyScale;
        if (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f || Mathf.Abs(ls.z - 1f) > 0.01f)
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
        entryDeg = ResolveEntryDeg();

        collidersRoot = NewChild("Colliders");
        liveRoot = NewChild("Live");
        props = new GroveMesh(seed);
        glowCrystal = new GroveMesh(seed + 1);
        glowRune = new GroveMesh(seed + 2);
        glowShroom = new GroveMesh(seed + 3);
        foot.Clear(); crystalPts.Clear(); shroomPts.Clear();
        foot.Add(new Vector3(0f, 0f, 2.6f));   // ствол и корни Дедушки

        BuildRuneRing();
        BuildMenhirs();
        BuildGate();
        BuildBenches();
        BuildTreeRootCrystals();
        BuildShrooms();
        Finish();

        if (Application.isPlaying)
        {
            SpawnTrees();
            if (relocateTrees) RelocateTrees();
        }
        if (addVibe) AddVibe();
        if (!Application.isPlaying) MarkDontSave(root);
        Debug.Log("MysticGlade: зона собрана (" + (props.TriCount + glowCrystal.TriCount + glowRune.TriCount + glowShroom.TriCount) + " треугольников). Центр: " + center, this);
    }

    [ContextMenu("Удалить собранную зону")]
    public void Clear()
    {
        var old = transform.Find(RootName);
        if (old == null) return;
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
    }

    Transform NewChild(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(root, false);
        return go.transform;
    }

    static void MarkDontSave(Transform t)
    {
        t.gameObject.hideFlags = HideFlags.DontSave;
        foreach (Transform c in t) MarkDontSave(c);
    }

    // ---------------------------------------------------------------- земля и место
    float Y(float lx, float lz) { return GroundY(lx, lz); }

    public float GroundY(float lx, float lz)
    {
        Vector3 w = root.TransformPoint(new Vector3(lx, 0f, lz));
        Vector3 origin = new Vector3(w.x, center.y + 40f, w.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuf, 90f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hitBuf[i].collider;
            if (c.transform.IsChildOf(root)) continue;
            if (c.GetComponentInParent<ToonSway>() != null || c.GetComponentInParent<LandmarkTree>() != null) continue;   // стволы деревьев землёй не считаем
            if (IsOldGlade(c.transform)) continue;
            if (hitBuf[i].distance < best) { best = hitBuf[i].distance; found = true; }
        }
        if (!found) return 0f;
        return (origin.y - best) - root.position.y;
    }

    float Clearance(float lx, float lz)
    {
        float best = float.MaxValue;
        for (int i = 0; i < foot.Count; i++)
            best = Mathf.Min(best, Vector2.Distance(new Vector2(lx, lz), new Vector2(foot[i].x, foot[i].y)) - foot[i].z);
        return best;
    }

    void Occupy(float x, float z, float r) { foot.Add(new Vector3(x, z, r)); }

    // случайное свободное место в кольце; проход к входу всегда пустой
    bool Spot(float minR, float maxR, float clr, out float x, out float z)
    {
        for (int t = 0; t < 30; t++)
        {
            float a = props.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(props.Range(minR * minR, maxR * maxR));
            x = Mathf.Cos(a) * r; z = Mathf.Sin(a) * r;
            if (Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(x, z) * Mathf.Rad2Deg, entryDeg)) < 11f) continue;
            if (Clearance(x, z) >= clr) return true;
        }
        x = z = 0f;
        return false;
    }

    // ---------------------------------------------------------------- материал и меши
    static Material GroveMat()
    {
        if (mat != null) return mat;
        Shader sh = Shader.Find("Project/CandyToon");
        bool candy = sh != null;
        if (!candy) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        mat = new Material(sh) { name = "MysticGlade_Mat" };
        if (candy)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_VertexColorStrength")) mat.SetFloat("_VertexColorStrength", 1f);
            if (mat.HasProperty("_PosVary")) mat.SetFloat("_PosVary", 0.06f);
        }
        return mat;
    }

    GameObject MeshObject(string n, Transform parent, Mesh mesh, bool cast, bool receive = true)
    {
        if (!Application.isPlaying) mesh.hideFlags = HideFlags.DontSave;
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = GroveMat();
        r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = receive;
        return go;
    }

    // светящийся меш: FireSource даёт эмиссию и мягкие ореолы (ваша система FireLightPool/FireSource)
    void GlowObject(string n, GroveMesh gm, Color tint, List<Vector3> halos, float haloSize, float strength)
    {
        var go = MeshObject(n, root, gm.ToMesh("MysticGlade_" + n), false, false);
        go.SetActive(false);   // FireSource читает поля при старте
        var fire = go.AddComponent<FireSource>();
        fire.preset = FlickerPreset.Steady;
        fire.color = tint;
        fire.simOnly = true;
        fire.emissive = new Renderer[] { go.GetComponent<Renderer>() };
        fire.emissiveStrength = strength;
        fire.glow = halos != null && halos.Count > 0;
        fire.glowSize = haloSize;
        fire.glowPoints = halos != null ? halos.ToArray() : new Vector3[0];
        go.SetActive(true);
    }

    void Finish()
    {
        MeshObject("Props", root, props.ToMesh("MysticGlade_Props"), true);
        GlowObject("GlowCrystals", glowCrystal, MysticPal.Violet, crystalPts, 1.5f, 2.4f);
        GlowObject("GlowRunes", glowRune, MysticPal.Cyan, null, 0f, 2.8f);
        GlowObject("GlowShrooms", glowShroom, MysticPal.Pink, shroomPts, 0.7f, 2.0f);
    }

    // ---------------------------------------------------------------- коллайдеры
    void AddBoxCollider(string n, Vector3 c, Vector3 size, float yaw)
    {
        var go = new GameObject(n);
        go.transform.SetParent(collidersRoot, false);
        go.transform.localPosition = c;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.AddComponent<BoxCollider>().size = size;
    }

    void AddCapsuleCollider(string n, Vector3 c, float radius, float height)
    {
        var go = new GameObject(n);
        go.transform.SetParent(collidersRoot, false);
        go.transform.localPosition = c;
        var cc = go.AddComponent<CapsuleCollider>();
        cc.radius = radius; cc.height = height;
    }

    // ---------------------------------------------------------------- рубимые деревья (только Play Mode)
    void SpawnTrees()
    {
        var holder = NewChild("ChopTrees");
        int layer = 0;
        var any = FindFirstObjectByType<ResourceItem>();
        if (any != null) layer = any.gameObject.layer;

        float step = 360f / Mathf.Max(1, treeCount);
        for (int i = 0; i < treeCount; i++)
        {
            float deg = entryDeg + step * 0.5f + step * i + props.Range(-5f, 5f);
            Vector2 p = Pos(deg, treeRadius + props.Range(-0.8f, 1.2f));
            float y = Y(p.x, p.y);

            var go = new GameObject("ChopTree_" + i);
            go.SetActive(false);   // сначала собираем, потом Awake у ResourceItem
            go.transform.SetParent(holder, false);
            go.transform.localPosition = new Vector3(p.x, y, p.y);
            go.transform.localRotation = Quaternion.Euler(0f, props.Range(0f, 360f), 0f);
            go.layer = layer;
            go.tag = "Resource";
            var sway = go.AddComponent<ToonSway>();
            sway.angle = 1.2f; sway.speed = 0.9f;
            go.AddComponent<ResourceItem>();   // модель, ствол и зону выбора ResourceItem строит сам (useGeneratedModel)
            go.SetActive(true);
            Occupy(p.x, p.y, 1.6f);
        }
    }

    // ---------------------------------------------------------------- вытеснить чужие деревья
    void RelocateTrees()
    {
        var list = new List<Transform>();
        foreach (var ri in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None)) if (ri != null) list.Add(ri.transform);
        foreach (var sw in FindObjectsByType<ToonSway>(FindObjectsSortMode.None))
        {
            if (sw == null || list.Contains(sw.transform)) continue;
            if (sw.GetComponent<FireSource>() != null || sw.GetComponentInParent<LandmarkTree>() != null) continue;
            list.Add(sw.transform);
        }
        int moved = 0;
        foreach (var t in list)
        {
            if (t == null || t.IsChildOf(root) || !t.gameObject.activeInHierarchy) continue;
            Vector3 d = t.position - center; d.y = 0f;
            if (d.magnitude >= clearingRadius) continue;
            if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
            d.Normalize();
            float r = clearingRadius + 1f + props.Range(0f, 3f);
            Vector3 np = center + d * r;
            float dy = GroundY(d.x * r, d.z * r) - GroundY((t.position - center).x, (t.position - center).z);
            t.position = new Vector3(np.x, t.position.y + dy, np.z);
            moved++;
        }
        if (moved > 0) Debug.Log("MysticGlade: вытеснено деревьев из зоны: " + moved, this);
    }

    // ---------------------------------------------------------------- старая поляна
    void DisableOldChopGrove()
    {
        foreach (var g in FindObjectsByType<ChopGroveBuilder>(FindObjectsSortMode.None))
        {
            if (g == null || (g.transform.position - transform.position).sqrMagnitude > (clearingRadius + 12f) * (clearingRadius + 12f)) continue;
            g.buildOnStart = false;
            g.Clear();
            g.enabled = false;
        }
    }

    // корни, которые собирают Editor-генераторы (IslandV2/_Manual) и которые эта зона заменяет целиком
    static readonly string[] OldRoots = { "ChopZone", "ChopCamp", "ChopGladeDecor", "GrandpaGrove" };

    static bool IsOldGlade(Transform t)
    {
        for (; t != null; t = t.parent)
            for (int i = 0; i < OldRoots.Length; i++)
                if (t.name == OldRoots[i]) return true;
        return false;
    }

    // Выключаются объекты в сцене (SetActive(false)), ничего не удаляется: каждый Play делает это заново, сцену трогать не нужно.
    // Если запустишь «Красота леса → 0. Всё сразу», старое соберётся снова, но на старте игры опять спрячется.
    void HideOldGlade()
    {
        int off = 0;
        var manual = GameObject.Find("IslandV2/_Manual");
        foreach (var n in OldRoots)
        {
            Transform t = manual != null ? manual.transform.Find(n) : null;
            if (t == null) { var g = GameObject.Find(n); if (g != null) t = g.transform; }
            if (t != null && t.gameObject.activeSelf) { t.gameObject.SetActive(false); off++; }
        }
        // рубимые деревья старой зоны рубки (ChopZone.TreePrefix): их заменяют 6 новых
        foreach (var ri in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None))
        {
            if (ri == null || !ri.name.StartsWith("ZoneTree_")) continue;
            if ((ri.transform.position - center).sqrMagnitude > (clearingRadius + 25f) * (clearingRadius + 25f)) continue;
            ri.gameObject.SetActive(false); off++;
        }
        // золотая пыль GladeVibe спорит с мистической палитрой
        float lim = clearingRadius * 2f;
        foreach (var g in FindObjectsByType<GladeVibe>(FindObjectsSortMode.None))
        {
            if (g == null || (g.transform.position - center).sqrMagnitude > lim * lim) continue;
            g.enabled = false;
            foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
        }
        if (off > 0) Debug.Log("MysticGlade: выключено старых объектов поляны: " + off, this);
    }

    void AddVibe()
    {
        var go = new GameObject("MysticVibe");
        go.transform.SetParent(root, false);
        var v = go.AddComponent<MysticGladeVibe>();
        v.radius = clearingRadius;
        v.ringRadius = benchRadius;
    }

    // ---------------------------------------------------------------- гизмо
    void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying && root != null ? center : transform.position;
        Gizmos.color = new Color(0.78f, 0.55f, 1f, 0.9f);
        DrawCircle(c, benchRadius); DrawCircle(c, treeRadius); DrawCircle(c, clearingRadius);
    }

    static void DrawCircle(Vector3 c, float r)
    {
        c.y += 0.2f;
        Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 cur = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}

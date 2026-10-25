#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/ForestDecor.cs (Editor)
// Декор леса: Tools → Forest → Декор леса.
//
//   Арка          деревянная арка с каменными основаниями, крышей, вывеской, фонарями, гирляндой флажков и лампочек, лозой и цветами.
//                 Ставится на маркер ForestGate, смотрит вдоль главной тропы.
//   Фонари        столбы с фонарями вдоль главной тропы, по кругу у поляны лесопилки и у тупиковой поляны, фонари на деревьях.
//   Пропсы        брёвна, пни с топорами, телега с дровами, заборчик у арки, указатель на развилке. Центр поляны лесопилки не занимает.
//   Природа       грибы у стволов, папоротники, цветы, кусты с ягодами, мшистые камни, поваленные брёвна.
//   Свет          пятна солнца на земле, лучи, светлячки (рантайм: ForestAtmosphere меняет их яркость днём и ночью).
//
// Что берёт из сцены: ForestSettings → ForestTrailPlanner (тропы, поляны), ForestGate, ForestTrees, ForestPlacementFilter (земля, тропы).
// Что создаёт: корень ForestDecor (детерминировано по seed). Перед сборкой старый корень удаляется, так что кнопку можно жать сколько угодно.
// Всё, что руками положено ВНЕ корня ForestDecor, не трогается.
// Ассеты (меши, материалы, шейдер): Assets/_Game/ForestDecor. Папку Location Builder не стирает.
// Все огни = FireSource (рядом с игроком настоящий свет даёт FireLightPool, остальные светятся имитацией).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// ====================================================================================== настройки и окно
[Serializable]
public sealed class ForestDecorSettings
{
    public int seed = 7;
    [Tooltip("0 = авто по высоте игрока в сцене (игрок = 1.0), иначе множитель размера всего декора")]
    public float size = 0f;

    public bool arch = true, archRoof = true, archLights = true, archBunting = true, archVines = true;
    public bool lamps = true;
    public float lampSpacing = 7f;
    public int treeLanterns = 5;

    public bool props = true;
    public float propsK = 1f;
    public bool nature = true;
    public float natureK = 1f;

    public bool fireflies = true;
    public int fireflyVolumes = 8;
    public bool patches = true;
    public int patchCount = 26;
    public bool shafts = true;
    public int shaftCount = 7;

    public bool colliders = true;
    public float treeClear = 1.0f;
    public Color glowTint = new Color(1f, 0.86f, 0.5f, 1f);
}

public class ForestDecorWindow : EditorWindow
{
    const string PrefsKey = "BuilderSim.ForestDecor.v1";
    ForestDecorSettings s = new ForestDecorSettings();
    Vector2 scroll;
    string log = "";

    [MenuItem("Tools/BuilderSim/Лес/Декор леса", false, 200)]
    static void Open()
    {
        var w = GetWindow<ForestDecorWindow>("Декор леса");
        w.minSize = new Vector2(360f, 560f);
    }

    void OnEnable()
    {
        s = new ForestDecorSettings();
        string json = EditorPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, s); } catch (Exception) { s = new ForestDecorSettings(); }
        }
    }

    void OnDisable() { EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(s)); }

    void Run(Func<string> action)
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(s));
        try { log = action(); }
        catch (Exception e) { Debug.LogException(e); log = "Ошибка, см. Console."; }
        finally { EditorUtility.ClearProgressBar(); }
        SceneView.RepaintAll();
        Repaint();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Сначала зона леса, деревья и тропы (окно «Лес»), потом этот инструмент: он ставит декор по тропам и полянам.", MessageType.None);

        s.seed = EditorGUILayout.IntField("Seed", s.seed);
        s.size = EditorGUILayout.FloatField(new GUIContent("Размер (0 = по игроку)", "Множитель размера всего декора. 1 = игрок высотой 1 м"), s.size);
        s.treeClear = EditorGUILayout.Slider("Зазор от стволов, м", s.treeClear, 0.4f, 2.5f);
        s.colliders = EditorGUILayout.Toggle("Коллайдеры (столбы, пни)", s.colliders);

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("Арка", EditorStyles.boldLabel);
        s.arch = EditorGUILayout.Toggle("Арка на ForestGate", s.arch);
        if (s.arch)
        {
            EditorGUI.indentLevel++;
            s.archRoof = EditorGUILayout.Toggle("Крыша и вывеска", s.archRoof);
            s.archLights = EditorGUILayout.Toggle("Фонари и лампочки", s.archLights);
            s.archBunting = EditorGUILayout.Toggle("Флажки", s.archBunting);
            s.archVines = EditorGUILayout.Toggle("Лоза и цветы", s.archVines);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("Фонари", EditorStyles.boldLabel);
        s.lamps = EditorGUILayout.Toggle("Столбы с фонарями", s.lamps);
        s.lampSpacing = EditorGUILayout.Slider("Шаг вдоль тропы, м", s.lampSpacing, 4f, 14f);
        s.treeLanterns = EditorGUILayout.IntSlider("Фонари на деревьях", s.treeLanterns, 0, 14);

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("Пропсы и природа", EditorStyles.boldLabel);
        s.props = EditorGUILayout.Toggle("Пропсы (дрова, пни, телега, забор)", s.props);
        s.propsK = EditorGUILayout.Slider("Количество пропсов", s.propsK, 0f, 2f);
        s.nature = EditorGUILayout.Toggle("Природа (грибы, цветы, камни)", s.nature);
        s.natureK = EditorGUILayout.Slider("Количество природы", s.natureK, 0f, 2.5f);

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("Свет и атмосфера", EditorStyles.boldLabel);
        s.patches = EditorGUILayout.Toggle("Пятна солнца на земле", s.patches);
        if (s.patches) s.patchCount = EditorGUILayout.IntSlider("Пятен", s.patchCount, 4, 80);
        s.shafts = EditorGUILayout.Toggle("Лучи света", s.shafts);
        if (s.shafts) s.shaftCount = EditorGUILayout.IntSlider("Лучей", s.shaftCount, 1, 24);
        s.fireflies = EditorGUILayout.Toggle("Светлячки", s.fireflies);
        if (s.fireflies) s.fireflyVolumes = EditorGUILayout.IntSlider("Облаков светлячков", s.fireflyVolumes, 1, 20);
        s.glowTint = EditorGUILayout.ColorField("Цвет света", s.glowTint);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(4f);
        if (GUILayout.Button("Собрать декор", GUILayout.Height(34f))) Run(() => ForestDecor.Build(s, false));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Только арка", GUILayout.Height(24f))) Run(() => ForestDecor.Build(s, true));
        if (GUILayout.Button("Убрать декор", GUILayout.Height(24f))) Run(ForestDecor.Remove);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(log)) EditorGUILayout.HelpBox(log, MessageType.Info);
    }
}

// ====================================================================================== ассеты
static class FD
{
    public const string Dir = "Assets/_Game/ForestDecor";
    public const string MatDir = Dir + "/Materials";
    public const string MeshDir = Dir + "/Meshes";
    public const string ShaderDir = Dir + "/Shaders";
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static int meshN;

    const string GlowSrc = @"Shader `ForestDecor/Glow`
{
    Properties { _Color(`Color`, Color) = (1,1,1,1) _Intensity(`Intensity`, Float) = 1 _Power(`Falloff`, Float) = 2 }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Power;
            CBUFFER_END
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), _Power);
                half4 c = _Color * i.col;
                return half4(c.rgb, c.a * a * _Intensity);
            }
            ENDHLSL
        }
    }
}";

    public static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        string parent = path.Substring(0, i);
        Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
    }

    // вызывается один раз перед сборкой: сцена уже очищена от старых мешей
    public static void Begin()
    {
        cache.Clear();
        meshN = 0;
        Folder(Dir); Folder(MatDir); Folder(ShaderDir);
        AssetDatabase.DeleteAsset(MeshDir);
        Folder(MeshDir);
    }

    public static Material Lit(string name, Color c, float smooth = 0.08f, bool emissive = false)
    {
        Material m;
        if (cache.TryGetValue(name, out m) && m != null) return m;
        string path = MatDir + "/FD_" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh) { name = "FD_" + name };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", Color.black);   // яркость ведёт FireSource через MaterialPropertyBlock
        }
        EditorUtility.SetDirty(m);
        cache[name] = m;
        return m;
    }

    public static Shader GlowShader()
    {
        string path = ShaderDir + "/ForestGlow.shader";
        string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        string src = GlowSrc.Replace('`', '"');
        if (!File.Exists(full) || File.ReadAllText(full) != src)
        {
            File.WriteAllText(full, src);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<Shader>(path);
    }

    public static Material Glow(string name, Color c, float power, float intensity = 1f)
    {
        Material m;
        if (cache.TryGetValue(name, out m) && m != null) return m;
        string path = MatDir + "/FD_" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = GlowShader();
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (m == null)
        {
            m = new Material(sh) { name = "FD_" + name };
            AssetDatabase.CreateAsset(m, path);
        }
        else m.shader = sh;
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Power")) m.SetFloat("_Power", power);
        if (m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        cache[name] = m;
        return m;
    }

    public static Mesh Save(Mesh m, string name)
    {
        string safe = name.Replace(' ', '_').Replace('/', '_');
        m.name = safe;
        AssetDatabase.CreateAsset(m, MeshDir + "/" + safe + "_" + (meshN++) + ".asset");
        return m;
    }
}

// ====================================================================================== геометрия
sealed class Prims : IDisposable
{
    public Mesh box, cyl6, cyl8, cyl12, cone6, cone8, octa;
    public Mesh[] ico = new Mesh[3], blob = new Mesh[3];

    public Prims()
    {
        box = LocKit.Box();
        cyl6 = LocKit.Prism("FDc6", 6, 0.5f, 0.5f);
        cyl8 = LocKit.Prism("FDc8", 8, 0.5f, 0.5f);
        cyl12 = LocKit.Prism("FDc12", 12, 0.5f, 0.5f);
        cone6 = LocKit.Prism("FDk6", 6, 0.5f, 0f);
        cone8 = LocKit.Prism("FDk8", 8, 0.5f, 0f);
        octa = LocKit.Octa();
        for (int i = 0; i < 3; i++)
        {
            ico[i] = LocKit.Ico("FDrock" + i, i + 11, LocKit.RockDeform);
            blob[i] = LocKit.Ico("FDblob" + i, i + 3, BlobDeform);
        }
    }

    static Vector3 BlobDeform(Vector3 p, int seed)
    {
        float n = Mathf.PerlinNoise(p.x * 2f + seed * 3.1f + 7f, p.y * 2f + p.z * 2f + 7f);
        return p * (0.5f * (0.9f + 0.2f * n));
    }

    public void Dispose()
    {
        Mesh[] all = { box, cyl6, cyl8, cyl12, cone6, cone8, octa, ico[0], ico[1], ico[2], blob[0], blob[1], blob[2] };
        for (int i = 0; i < all.Length; i++) if (all[i] != null) Object.DestroyImmediate(all[i]);
    }
}

// набор граней по материалам; сборка в один Mesh с подмешами. Все плоские грани (low poly)
sealed class Part
{
    readonly Prims P;
    readonly Dictionary<Material, LocMB> mb = new Dictionary<Material, LocMB>();
    readonly List<Material> order = new List<Material>();

    public Part(Prims p) { P = p; }
    public bool Empty { get { return order.Count == 0; } }

    LocMB Get(Material m)
    {
        LocMB b;
        if (!mb.TryGetValue(m, out b)) { b = new LocMB(); mb[m] = b; order.Add(m); }
        return b;
    }

    // масштаб только положительный: порядок вершин сохраняется
    public void Add(Mesh src, Material m, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var b = Get(m);
        var M = Matrix4x4.TRS(pos, rot, scale);
        var vs = src.vertices;
        var ts = src.triangles;
        for (int i = 0; i < ts.Length; i++)
        {
            b.t.Add(b.v.Count);
            b.v.Add(M.MultiplyPoint3x4(vs[ts[i]]));
        }
    }

    public void Box(Material m, Vector3 c, Vector3 size) { Add(P.box, m, c, Quaternion.identity, size); }
    public void Box(Material m, Vector3 c, Quaternion r, Vector3 size) { Add(P.box, m, c, r, size); }

    // тонкая балка от a до b: w = ширина, h = толщина (по умолчанию квадрат)
    public void Beam(Material m, Vector3 a, Vector3 b, float w, float h = -1f)
    {
        Vector3 d = b - a;
        float l = d.magnitude;
        if (l < 1e-4f) return;
        Vector3 f = d / l;
        Vector3 up = Mathf.Abs(f.y) > 0.98f ? Vector3.forward : Vector3.up;
        Add(P.box, m, (a + b) * 0.5f, Quaternion.LookRotation(f, up), new Vector3(w, h < 0f ? w : h, l));
    }

    // цилиндр от земли baseP высотой h, диаметром d
    public void Cyl(Material m, Vector3 baseP, float h, float d, Mesh mesh = null)
    {
        Add(mesh != null ? mesh : P.cyl8, m, baseP + Vector3.up * (h * 0.5f), Quaternion.identity, new Vector3(d, h, d));
    }

    public void Tube(Material m, Vector3 a, Vector3 b, float r0, float r1, int n = 8) { LocKit.AddTube(Get(m), a, b, r0, r1, n); }

    // диск-торец на конце бревна: ось Z
    public void Cap(Material m, Vector3 c, float d, float th = 0.02f) { Add(P.cyl8, m, c, Quaternion.Euler(90f, 0f, 0f), new Vector3(d, th, d)); }

    public void Tri2(Material m, Vector3 a, Vector3 b, Vector3 c)
    {
        var bb = Get(m);
        Vector3 n = Vector3.Cross(b - a, c - a);
        bb.Tri(a, b, c, n);
        bb.Tri(a, c, b, -n);
    }

    public Mesh Build(string name, out Material[] mats)
    {
        mats = order.ToArray();
        var verts = new List<Vector3>();
        var tris = new List<int[]>();
        for (int k = 0; k < order.Count; k++)
        {
            var b = mb[order[k]];
            int off = verts.Count;
            verts.AddRange(b.v);
            var t = new int[b.t.Count];
            for (int i = 0; i < t.Length; i++) t[i] = b.t[i] + off;
            tris.Add(t);
        }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(verts);
        m.subMeshCount = Mathf.Max(1, tris.Count);
        for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return FD.Save(m, name);
    }
}

sealed class TreeGrid
{
    const float C = 4f;
    readonly Dictionary<long, List<Vector3>> h = new Dictionary<long, List<Vector3>>();
    public int Count;

    static long K(int x, int z) { return ((long)x << 32) ^ (long)(uint)z; }

    public void Add(Vector3 p)
    {
        long k = K(Mathf.FloorToInt(p.x / C), Mathf.FloorToInt(p.z / C));
        List<Vector3> l;
        if (!h.TryGetValue(k, out l)) { l = new List<Vector3>(); h[k] = l; }
        l.Add(p);
        Count++;
    }

    // расстояние до ближайшего ствола (до maxR; дальше = maxR)
    public float Nearest(Vector2 p, float maxR, out Vector3 tp)
    {
        tp = Vector3.zero;
        float best = maxR;
        int cx = Mathf.FloorToInt(p.x / C), cz = Mathf.FloorToInt(p.y / C);
        int rc = Mathf.CeilToInt(maxR / C);
        for (int x = cx - rc; x <= cx + rc; x++)
            for (int z = cz - rc; z <= cz + rc; z++)
            {
                List<Vector3> l;
                if (!h.TryGetValue(K(x, z), out l)) continue;
                for (int i = 0; i < l.Count; i++)
                {
                    float d = (new Vector2(l[i].x, l[i].z) - p).magnitude;
                    if (d < best) { best = d; tp = l[i]; }
                }
            }
        return best;
    }

    public bool Clear(Vector2 p, float r) { Vector3 t; return Nearest(p, r, out t) >= r; }
}

// ====================================================================================== сборка
public static class ForestDecor
{
    public const string RootName = "ForestDecor";

    public static string Remove()
    {
        if (Application.isPlaying) return "Во время Play декор не меняется: выйди из Play.";
        bool had = RemoveRoot();
        return had ? "Декор убран (ассеты в Assets/_Game/ForestDecor остались, их можно удалить руками)." : "Декора в сцене нет.";
    }

    static bool RemoveRoot()
    {
        bool had = false;
        GameObject r;
        while ((r = GameObject.Find(RootName)) != null) { Undo.DestroyObjectImmediate(r); had = true; }
        return had;
    }

    public static string Build(ForestDecorSettings s, bool archOnly)
    {
        if (Application.isPlaying) return "Во время Play декор не меняется: выйди из Play.";
        var fs = ForestSettings.Load();
        string err;
        var plan = ForestTrailPlanner.Plan(fs, out err);
        if (plan == null) return "Декор: " + err + "\nСначала собери тропы в окне «Лес».";

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build forest decor");
        int group = Undo.GetCurrentGroup();

        RemoveRoot();
        FD.Begin();
        var prims = new Prims();
        try
        {
            var b = new DecorBuilder(s, plan, prims);
            string rep = b.Run(archOnly);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            if (b.root != null) { Selection.activeGameObject = b.root.gameObject; UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(b.root.gameObject.scene); }
            return rep;
        }
        finally { prims.Dispose(); }
    }
}

sealed class DecorBuilder
{
    readonly ForestDecorSettings s;
    readonly ForestTrailPlan plan;
    readonly Prims P;
    readonly System.Random rng;
    readonly StringBuilder rep = new StringBuilder();
    readonly TreeGrid trees = new TreeGrid();
    ForestPlacementFilter f;
    bool fOk;
    float y0;
    float sz = 1f;
    public Transform root;

    // материалы
    Material mWood, mWoodL, mWoodEnd, mBark, mIron, mStone, mStoneD, mMoss, mLeaf, mLeaf2, mRope, mGlass, mBulb;
    Material mRed, mCream, mSpot, mFern, mFlowW, mFlowY, mFlowP, mFlowV, mBerry, mShelf, mRoof;
    Material[] flag;
    Mesh lanIron, lanGlass, unitBox;
    ForestTrailPath main;
    float[] mainCum;
    readonly List<Renderer> rPatch = new List<Renderer>(), rShaft = new List<Renderer>(), rFly = new List<Renderer>();
    readonly List<Vector2> lampPts = new List<Vector2>();
    int nLamps, nLantern, nProps, nNature;

    public DecorBuilder(ForestDecorSettings settings, ForestTrailPlan pl, Prims prims)
    {
        s = settings; plan = pl; P = prims;
        rng = new System.Random(unchecked(s.seed * 7919 + 13));
    }

    // ---------------------------------------------------------------- утилиты
    float R() { return (float)rng.NextDouble(); }
    float R(float a, float b) { return a + (b - a) * (float)rng.NextDouble(); }
    static Vector3 V3(Vector2 v, float y) { return new Vector3(v.x, y, v.y); }
    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }
    static Quaternion Yaw(Vector2 dir) { return dir.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up); }

    float Gy(Vector2 p)
    {
        Vector3 g;
        if (fOk && f.GroundPoint(p.x, p.y, out g)) return g.y;
        return y0;
    }

    Vector3 G(Vector2 p) { return new Vector3(p.x, Gy(p), p.y); }

    // можно ли ставить: земля, не на тропе, не в воде, не у ствола, вне центров полян
    bool Spot(Vector2 p, float r, float trailGap = 0.15f, float treeR = -1f)
    {
        if (treeR < 0f) treeR = s.treeClear;
        if (fOk)
        {
            Vector3 g;
            if (!f.GroundPoint(p.x, p.y, out g)) return false;
            if (f.RiverDistance(g) < 1f) return false;
            if (f.PathDistance(g) < trailGap) return false;
            if (f.BuildingDistance(g) < 1.2f) return false;
        }
        if (Vector2.Distance(p, plan.sawmill) < plan.sawmillRadius * 0.55f) return false;
        if (plan.hasFind && Vector2.Distance(p, plan.findSpot) < plan.findRadius * 0.45f) return false;
        if (!trees.Clear(p, treeR + r)) return false;
        return true;
    }

    GameObject AddMesh(GameObject go, Mesh m, Material mat, bool shadows)
    {
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    GameObject Obj(Part p, string name, Transform parent, Vector3 pos, Quaternion rot, bool shadows = true)
    {
        Material[] mats;
        Mesh mesh = p.Build(name, out mats);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    void Col(GameObject go, Vector3 center, Vector3 size)
    {
        if (!s.colliders) return;
        var c = go.AddComponent<BoxCollider>();
        c.center = center; c.size = size;
    }

    // ---------------------------------------------------------------- запуск
    public string Run(bool archOnly)
    {
        var master = ForestZones.Find(ForestBiome.Forest);
        y0 = master != null ? master.transform.position.y : 0f;
        f = ForestPlacementFilter.Get();
        fOk = f.Rebuild();

        string how = "задан вручную";
        if (s.size > 0.05f) sz = s.size;
        else sz = AutoSize(out how);
        sz = Mathf.Clamp(sz, 0.4f, 3f);

        main = plan.paths.Find(x => x.main);
        if (main == null && plan.paths.Count > 0) main = plan.paths[0];
        if (main != null) mainCum = Cum(main);

        CollectTrees();
        InitMaterials();
        InitLantern();

        var rootGo = new GameObject(ForestDecor.RootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Forest decor");
        root = rootGo.transform;

        rep.Append("Декор леса. Размер ").Append(sz.ToString("0.##")).Append(" (").Append(how).Append("), деревьев в сцене: ").Append(trees.Count).Append('.');
        if (!fOk) rep.Append("\n! Земля (Ground_Auto / Terrain) не найдена: всё стоит на высоте зоны леса.");
        if (main == null) { rep.Append("\n! В плане нет главной тропы."); return rep.ToString(); }

        EditorUtility.DisplayProgressBar("Декор леса", "Арка", 0.1f);
        if (s.arch) BuildArch();
        if (archOnly) return rep.ToString();

        if (s.lamps) { EditorUtility.DisplayProgressBar("Декор леса", "Фонари", 0.3f); BuildLamps(); }
        if (s.treeLanterns > 0) BuildTreeLanterns();
        if (s.props) { EditorUtility.DisplayProgressBar("Декор леса", "Пропсы", 0.5f); BuildProps(); }
        if (s.nature) { EditorUtility.DisplayProgressBar("Декор леса", "Природа", 0.7f); BuildNature(); }
        EditorUtility.DisplayProgressBar("Декор леса", "Свет", 0.9f);
        BuildFx();

        rep.Append("\nФонарей: ").Append(nLamps).Append(" на столбах, ").Append(nLantern).Append(" на деревьях. Пропсов: ").Append(nProps).Append(". Природы: ").Append(nNature).Append('.');
        rep.Append("\nПроверь ночью: ПКМ по _FireLights в Play → «Тест: ночь для огней», или ПКМ по ForestDecor/Fx → «Тест: ночь».");
        return rep.ToString();
    }

    float AutoSize(out string how)
    {
        how = "игрока в сцене нет, 1.0";
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            var rs = pm.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                float h = b.size.y;
                if (h > 0.3f && h < 3f) { how = "по игроку, высота " + h.ToString("0.##") + " м"; return h; }
            }
        }
        return 1f;
    }

    void CollectTrees()
    {
        var tr = GameObject.Find("ForestTrees");
        if (tr == null) return;
        foreach (Transform biome in tr.transform)
            foreach (Transform t in biome)
                trees.Add(t.position);
    }

    // ---------------------------------------------------------------- материалы и фонарь
    void InitMaterials()
    {
        mWood = FD.Lit("Wood", new Color(0.38f, 0.25f, 0.15f));
        mWoodL = FD.Lit("WoodL", new Color(0.58f, 0.40f, 0.23f));
        mWoodEnd = FD.Lit("WoodEnd", new Color(0.76f, 0.58f, 0.36f));
        mBark = FD.Lit("Bark", new Color(0.31f, 0.22f, 0.15f));
        mIron = FD.Lit("Iron", new Color(0.18f, 0.18f, 0.22f), 0.25f);
        mStone = FD.Lit("Stone", new Color(0.56f, 0.56f, 0.60f));
        mStoneD = FD.Lit("StoneD", new Color(0.42f, 0.42f, 0.47f));
        mMoss = FD.Lit("Moss", new Color(0.36f, 0.56f, 0.20f));
        mLeaf = FD.Lit("Leaf", new Color(0.28f, 0.54f, 0.18f));
        mLeaf2 = FD.Lit("Leaf2", new Color(0.50f, 0.68f, 0.22f));
        mRope = FD.Lit("Rope", new Color(0.78f, 0.68f, 0.45f));
        mGlass = FD.Lit("LanternGlass", new Color(1f, 0.82f, 0.45f), 0.2f, true);
        mBulb = FD.Lit("Bulb", new Color(1f, 0.88f, 0.55f), 0.2f, true);
        mRed = FD.Lit("ShroomRed", new Color(0.86f, 0.18f, 0.15f));
        mCream = FD.Lit("ShroomStem", new Color(0.95f, 0.90f, 0.80f));
        mSpot = FD.Lit("ShroomSpot", new Color(1f, 0.97f, 0.9f));
        mFern = FD.Lit("Fern", new Color(0.22f, 0.50f, 0.26f));
        mFlowW = FD.Lit("FlowerW", new Color(0.98f, 0.96f, 0.92f));
        mFlowY = FD.Lit("FlowerY", new Color(1f, 0.82f, 0.22f));
        mFlowP = FD.Lit("FlowerP", new Color(0.95f, 0.55f, 0.72f));
        mFlowV = FD.Lit("FlowerV", new Color(0.62f, 0.50f, 0.90f));
        mBerry = FD.Lit("Berry", new Color(0.85f, 0.15f, 0.20f));
        mShelf = FD.Lit("ShelfShroom", new Color(0.92f, 0.62f, 0.28f));
        mRoof = FD.Lit("Roof", new Color(0.48f, 0.32f, 0.20f));
        flag = new[]
        {
            FD.Lit("FlagY", new Color(0.96f, 0.78f, 0.24f)),
            FD.Lit("FlagO", new Color(0.92f, 0.52f, 0.22f)),
            FD.Lit("FlagG", new Color(0.50f, 0.72f, 0.32f)),
            FD.Lit("FlagC", new Color(0.96f, 0.91f, 0.72f))
        };
    }

    // фонарь: начало координат в центре стекла, высота ~0.62 (кольцо сверху на +0.31)
    void InitLantern()
    {
        var iron = new Part(P);
        iron.Add(P.cyl6, mIron, new Vector3(0f, 0.145f, 0f), Quaternion.identity, new Vector3(0.25f, 0.03f, 0.25f));
        iron.Add(P.cyl6, mIron, new Vector3(0f, -0.15f, 0f), Quaternion.identity, new Vector3(0.24f, 0.04f, 0.24f));
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f;
            iron.Add(P.box, mIron, new Vector3(Mathf.Cos(a) * 0.1f, 0f, Mathf.Sin(a) * 0.1f), Quaternion.identity, new Vector3(0.024f, 0.27f, 0.024f));
        }
        iron.Add(P.cone6, mIron, new Vector3(0f, 0.225f, 0f), Quaternion.identity, new Vector3(0.32f, 0.12f, 0.32f));
        iron.Add(P.cyl6, mIron, new Vector3(0f, 0.30f, 0f), Quaternion.identity, new Vector3(0.07f, 0.06f, 0.07f));
        var glass = new Part(P);
        glass.Add(P.cyl6, mGlass, Vector3.zero, Quaternion.identity, new Vector3(0.2f, 0.26f, 0.2f));
        Material[] mm;
        lanIron = iron.Build("Lantern_Iron", out mm);
        lanGlass = glass.Build("Lantern_Glass", out mm);
        var ub = new Part(P);
        ub.Box(mIron, Vector3.zero, Vector3.one);
        unitBox = ub.Build("UnitBox", out mm);
    }

    // подвесной фонарь: pivot в точке подвеса (качается через ToonSway), цепь вниз, фонарь на конце
    Transform Hang(Transform parent, string name, Vector3 attachLocal, float chain, float sc, bool swing, bool sim = false)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = attachLocal;
        if (swing)
        {
            var sw = pivot.gameObject.AddComponent<ToonSway>();
            sw.angle = 2.2f;
            sw.speed = R(0.6f, 1.1f);
        }
        if (chain > 0.01f)
        {
            var c = new GameObject("Chain");
            c.transform.SetParent(pivot, false);
            c.transform.localPosition = new Vector3(0f, -chain * 0.5f, 0f);
            c.transform.localScale = new Vector3(0.016f * sc, chain, 0.016f * sc);
            AddMesh(c, unitBox, mIron, false);
        }
        var lan = new GameObject("Lantern");
        lan.transform.SetParent(pivot, false);
        lan.transform.localPosition = new Vector3(0f, -(chain + 0.31f * sc), 0f);
        lan.transform.localScale = Vector3.one * sc;
        var ir = new GameObject("Iron"); ir.transform.SetParent(lan.transform, false);
        AddMesh(ir, lanIron, mIron, false);
        var gl = new GameObject("Glass"); gl.transform.SetParent(lan.transform, false);
        AddMesh(gl, lanGlass, mGlass, false);

        var fsrc = lan.AddComponent<FireSource>();
        fsrc.preset = FlickerPreset.Lantern;
        fsrc.color = new Color(1f, 0.77f, 0.42f);
        fsrc.intensity = 2.2f;
        fsrc.range = 4.5f * Mathf.Max(0.8f, sc);
        fsrc.simOnly = sim;
        fsrc.emissive = new Renderer[] { gl.GetComponent<Renderer>() };
        fsrc.emissiveStrength = 2.4f;
        fsrc.glowSize = 1.0f * sc;
        return pivot;
    }

    // ---------------------------------------------------------------- тропы
    static float[] Cum(ForestTrailPath t)
    {
        var c = new float[t.p.Count];
        for (int i = 1; i < c.Length; i++) c[i] = c[i - 1] + (t.p[i] - t.p[i - 1]).magnitude;
        return c;
    }

    static void At(ForestTrailPath t, float[] cum, float sdist, out Vector2 pos, out Vector2 nrm, out float half)
    {
        int n = t.p.Count, i = 0;
        while (i < n - 2 && cum[i + 1] < sdist) i++;
        float seg = Mathf.Max(1e-4f, cum[i + 1] - cum[i]);
        float u = Mathf.Clamp01((sdist - cum[i]) / seg);
        pos = Vector2.Lerp(t.p[i], t.p[i + 1], u);
        Vector2 tan = (t.p[i + 1] - t.p[i]).normalized;
        nrm = new Vector2(-tan.y, tan.x);
        half = Mathf.Lerp(t.half[i], t.half[i + 1], u);
    }

    Vector2 MainDir()
    {
        int n = main.p.Count;
        Vector2 d = main.p[Mathf.Min(n - 1, 5)] - main.p[0];
        return d.sqrMagnitude < 1e-4f ? Vector2.up : d.normalized;
    }

    // ---------------------------------------------------------------- арка
    void BuildArch()
    {
        var gate = GameObject.Find("ForestGate");
        Vector2 gp = gate != null ? XZ(gate.transform.position) : plan.gate;
        if (gate == null) rep.Append("\n! Маркера ForestGate нет: арка поставлена на край зоны со стороны центра острова.");
        Vector2 dir = MainDir();
        float half0 = main.half.Count > 0 ? main.half[0] : 1.7f;

        float W = Mathf.Max(2.2f, half0 * 2f + 0.7f);       // свободная ширина прохода
        float H = Mathf.Max(W * 0.85f, 2.3f * sz);          // высота до нижней перекладины
        float pr = 0.17f * sz;
        float px = W * 0.5f + pr;                           // ось стойки по X
        float Hp = H + 0.6f * sz;                           // верх стойки

        var archRoot = Group(root, "ForestArch");
        Vector3 pos = G(gp);
        archRoot.SetPositionAndRotation(pos, Yaw(dir));

        // ---- каркас: стойки, балки, упоры, камни
        var body = new Part(P);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            float x = sg * px;
            body.Tube(mWood, new Vector3(x, -0.45f, 0f), new Vector3(x, Hp, 0f), pr * 1.2f, pr * 0.95f, 8);
            body.Cyl(mRope, new Vector3(x, H * 0.46f, 0f), 0.08f * sz, pr * 2.7f, P.cyl8);
            body.Cyl(mRope, new Vector3(x, H * 0.52f, 0f), 0.08f * sz, pr * 2.7f, P.cyl8);
            body.Cyl(mStoneD, new Vector3(x, 0f, 0f), 0.14f * sz, pr * 5.2f, P.cyl8);
            body.Add(P.cone8, mWoodL, new Vector3(x, Hp + 0.07f * sz, 0f), Quaternion.identity, new Vector3(pr * 2.6f, 0.2f * sz, pr * 2.6f));
            // камни вокруг основания
            for (int k = 0; k < 4; k++)
            {
                float a = (k + R()) * Mathf.PI * 0.5f;
                float rr = R(0.26f, 0.42f) * sz;
                float ks = R(0.28f, 0.5f) * sz;
                body.Add(P.ico[k % 3], k % 2 == 0 ? mStone : mStoneD,
                    new Vector3(x + Mathf.Cos(a) * rr, 0.04f * sz, Mathf.Sin(a) * rr), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(ks, ks * 0.8f, ks));
            }
            body.Add(P.blob[1], mMoss, new Vector3(x + sg * 0.1f * sz, 0.2f * sz, 0.12f * sz), Quaternion.identity, new Vector3(0.42f * sz, 0.12f * sz, 0.36f * sz));
            // косые упоры к нижней перекладине
            body.Beam(mWoodL, new Vector3(x - sg * pr * 0.9f, H - 0.72f * sz, 0f), new Vector3(x - sg * (pr + 0.62f * sz), H - 0.02f * sz, 0f), 0.12f * sz, 0.14f * sz);
        }
        float span = px * 2f;
        body.Box(mWoodL, new Vector3(0f, H, 0f), new Vector3(span + 0.2f * sz, 0.26f * sz, 0.26f * sz));
        body.Box(mWood, new Vector3(0f, H + 0.42f * sz, 0f), new Vector3(span + 1.1f * sz, 0.2f * sz, 0.34f * sz));
        for (int sg = -1; sg <= 1; sg += 2)
            body.Box(mWoodEnd, new Vector3(sg * (span * 0.5f + 0.55f * sz), H + 0.42f * sz, 0f), new Vector3(0.03f * sz, 0.2f * sz, 0.34f * sz));
        // стропильные выпуски над верхней балкой
        for (int k = -2; k <= 2; k++)
            body.Box(mWoodL, new Vector3(k * span * 0.22f, H + 0.22f * sz, 0f), new Vector3(0.1f * sz, 0.18f * sz, 0.42f * sz));

        if (s.archRoof)
        {
            // маленькая двускатная крыша над центром + коньковая балка
            float rw = Mathf.Min(W * 0.56f, 2.2f * sz + 0.3f);
            float ang = 24f;
            float yy = H + 0.62f * sz;
            float hw = rw * 0.5f;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var q = Quaternion.Euler(0f, 0f, -sg * ang);
                body.Add(P.box, mRoof, new Vector3(sg * hw * 0.48f, yy + 0.16f * sz, 0f), q, new Vector3(hw * 1.08f, 0.07f * sz, 0.62f * sz));
                body.Add(P.box, mWood, new Vector3(sg * hw * 0.48f, yy + 0.2f * sz, 0.3f * sz), q, new Vector3(hw * 1.1f, 0.05f * sz, 0.04f * sz));
                body.Add(P.box, mWood, new Vector3(sg * hw * 0.48f, yy + 0.2f * sz, -0.3f * sz), q, new Vector3(hw * 1.1f, 0.05f * sz, 0.04f * sz));
            }
            body.Box(mWood, new Vector3(0f, yy + 0.4f * sz, 0f), new Vector3(0.14f * sz, 0.08f * sz, 0.68f * sz));
            body.Add(P.blob[2], mMoss, new Vector3(-hw * 0.4f, yy + 0.34f * sz, 0.05f * sz), Quaternion.identity, new Vector3(0.5f * sz, 0.11f * sz, 0.4f * sz));
            body.Add(P.blob[0], mMoss, new Vector3(hw * 0.5f, yy + 0.3f * sz, -0.05f * sz), Quaternion.identity, new Vector3(0.38f * sz, 0.1f * sz, 0.34f * sz));
        }

        // ---- лоза и цветы
        if (s.archVines)
        {
            for (int k = 0; k < 9; k++)
            {
                float x = Mathf.Lerp(-span * 0.5f, span * 0.5f, (k + R(0.1f, 0.9f)) / 9f);
                float bs = R(0.22f, 0.42f) * sz;
                body.Add(P.blob[k % 3], k % 2 == 0 ? mLeaf : mLeaf2, new Vector3(x, H + 0.55f * sz, R(-0.12f, 0.12f) * sz), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(bs * 1.3f, bs * 0.8f, bs));
                if (R() < 0.45f) AddBloom(body, new Vector3(x + R(-0.1f, 0.1f) * sz, H + 0.62f * sz, R(0.05f, 0.18f) * sz), 0.06f * sz);
            }
            for (int sg = -1; sg <= 1; sg += 2)
            {
                int cnt = sg < 0 ? 7 : 4;                         // слева лоза плотнее: асимметрия живее
                for (int k = 0; k < cnt; k++)
                {
                    float t = (k + 0.5f) / cnt;
                    float a = t * 9f + (sg < 0 ? 0f : 2f);
                    float y = Mathf.Lerp(0.35f * sz, Hp - 0.1f * sz, t);
                    float bs = R(0.2f, 0.34f) * sz;
                    Vector3 c = new Vector3(sg * px + Mathf.Cos(a) * pr * 1.15f, y, Mathf.Sin(a) * pr * 1.15f);
                    body.Add(P.blob[k % 3], k % 2 == 0 ? mLeaf : mLeaf2, c, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(bs, bs * 0.9f, bs));
                    if (R() < 0.4f) AddBloom(body, c + new Vector3(Mathf.Cos(a), 0.04f, Mathf.Sin(a)) * bs * 0.55f, 0.055f * sz);
                }
            }
        }

        // ---- флажки: между верхними концами и по нижней перекладине спереди
        if (s.archBunting)
        {
            float zf = -0.3f * sz;
            Bunting(body, new Vector3(-px - 0.45f * sz, Hp - 0.08f * sz, zf), new Vector3(px + 0.45f * sz, Hp - 0.08f * sz, zf), 0.55f * sz, 0.3f * sz);
            float zb = 0.3f * sz;
            Bunting(body, new Vector3(-px - 0.45f * sz, Hp - 0.08f * sz, zb), new Vector3(px + 0.45f * sz, Hp - 0.08f * sz, zb), 0.5f * sz, 0.28f * sz);
        }

        var bodyGo = Obj(body, "ArchBody", archRoot, pos, archRoot.rotation);
        bodyGo.transform.SetParent(archRoot, true);
        if (s.colliders)
            for (int sg = -1; sg <= 1; sg += 2)
                Col(bodyGo, new Vector3(sg * px, Hp * 0.5f, 0f), new Vector3(pr * 2.6f, Hp, pr * 2.6f));

        // ---- вывеска (отдельный объект: заменишь на свою или повесишь TextMeshPro)
        if (s.archRoof)
        {
            var sign = new Part(P);
            float sw = Mathf.Min(W * 0.55f, 1.7f * sz), sh = 0.4f * sz;
            sign.Box(mWoodL, new Vector3(0f, 0f, 0f), new Vector3(sw, sh, 0.07f * sz));
            sign.Box(mWood, new Vector3(0f, sh * 0.5f, 0f), new Vector3(sw + 0.06f * sz, 0.05f * sz, 0.1f * sz));
            sign.Box(mWood, new Vector3(0f, -sh * 0.5f, 0f), new Vector3(sw + 0.06f * sz, 0.05f * sz, 0.1f * sz));
            for (int k = -1; k <= 1; k++)
                sign.Box(mWood, new Vector3(k * sw * 0.26f, 0f, 0.045f * sz), new Vector3(sw * 0.18f, 0.06f * sz, 0.02f * sz));
            sign.Beam(mRope, new Vector3(-sw * 0.4f, sh * 0.5f, 0f), new Vector3(-sw * 0.4f, sh * 0.5f + 0.28f * sz, 0f), 0.025f * sz);
            sign.Beam(mRope, new Vector3(sw * 0.4f, sh * 0.5f, 0f), new Vector3(sw * 0.4f, sh * 0.5f + 0.28f * sz, 0f), 0.025f * sz);
            var pivot = new GameObject("SignPivot").transform;
            pivot.SetParent(archRoot, false);
            pivot.localPosition = new Vector3(0f, H - 0.14f * sz, 0f);
            var sw1 = pivot.gameObject.AddComponent<ToonSway>();
            sw1.angle = 1.4f; sw1.speed = 0.55f;
            var signGo = Obj(sign, "Sign", pivot, Vector3.zero, Quaternion.identity);
            signGo.transform.localPosition = new Vector3(0f, -0.5f * sz - sh * 0.5f + 0.28f * sz, 0f);
            signGo.transform.localRotation = Quaternion.identity;
        }

        // ---- фонари и лампочки
        if (s.archLights)
        {
            float lx = W * 0.5f - 0.18f * sz;
            Hang(archRoot, "LanternL", new Vector3(-lx, H - 0.13f * sz, 0f), 0.34f * sz, sz, true);
            Hang(archRoot, "LanternR", new Vector3(lx, H - 0.13f * sz, 0f), 0.34f * sz, sz, true);
            nLamps += 2;
            // кронштейны на лицевой стороне стоек (-Z: со стороны игрока, входящего в лес)
            var br = new Part(P);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 a0 = new Vector3(sg * px, Hp * 0.72f, -pr * 0.6f);
                Vector3 a1 = new Vector3(sg * px, Hp * 0.72f, -0.55f * sz);
                br.Beam(mIron, a0, a1, 0.05f * sz);
                br.Beam(mWoodL, new Vector3(sg * px, Hp * 0.72f - 0.3f * sz, -pr * 0.6f), new Vector3(sg * px, Hp * 0.72f - 0.02f * sz, -0.4f * sz), 0.045f * sz);
                Hang(archRoot, sg < 0 ? "BracketL" : "BracketR", new Vector3(sg * px, Hp * 0.72f - 0.03f * sz, -0.55f * sz), 0.14f * sz, 0.9f * sz, true);
                nLamps++;
            }
            Obj(br, "Brackets", archRoot, pos, archRoot.rotation).transform.SetParent(archRoot, true);

            // гирлянда лампочек под верхней балкой
            var bulbs = new Part(P);
            var pts = new List<Vector3>();
            Vector3 ga = new Vector3(-px, H + 0.2f * sz, -0.17f * sz), gb = new Vector3(px, H + 0.2f * sz, -0.17f * sz);
            int bn = Mathf.Max(6, Mathf.RoundToInt(span / (0.42f * sz)));
            float sag = 0.22f * sz;
            Vector3 prevP = ga;
            var wire = new Part(P);
            for (int i = 1; i <= bn; i++)
            {
                float t = i / (float)bn;
                Vector3 q = Vector3.Lerp(ga, gb, t); q.y -= sag * 4f * t * (1f - t);
                wire.Beam(mIron, prevP, q, 0.018f * sz);
                prevP = q;
                if (i < bn)
                {
                    Vector3 bp = q + Vector3.down * 0.06f * sz;
                    bulbs.Add(P.octa, mBulb, bp, Quaternion.identity, new Vector3(0.09f, 0.12f, 0.09f) * sz);
                    pts.Add(bp);
                }
            }
            Obj(wire, "BulbWire", archRoot, pos, archRoot.rotation, false).transform.SetParent(archRoot, true);
            var bgo = Obj(bulbs, "Bulbs", archRoot, pos, archRoot.rotation, false);
            bgo.transform.SetParent(archRoot, true);
            var fsrc = archRoot.gameObject.AddComponent<FireSource>();
            fsrc.preset = FlickerPreset.Garland;
            fsrc.simOnly = true;
            fsrc.color = new Color(1f, 0.85f, 0.5f);
            fsrc.emissive = new Renderer[] { bgo.GetComponent<Renderer>() };
            fsrc.emissiveStrength = 2.6f;
            fsrc.glowSize = 0.5f * sz;
            fsrc.glowPoints = pts.ToArray();
        }

        rep.Append("\nАрка: проход ").Append(W.ToString("0.0")).Append(" м, высота ").Append(Hp.ToString("0.0")).Append(" м, на ForestGate. Вывеска ForestArch/SignPivot/Sign пустая: повесь текст или замени.");
    }

    void AddBloom(Part p, Vector3 c, float r)
    {
        Material m;
        switch (rng.Next(4)) { case 0: m = mFlowW; break; case 1: m = mFlowY; break; case 2: m = mFlowP; break; default: m = mFlowV; break; }
        p.Add(P.octa, m, c, Quaternion.Euler(R(0f, 60f), R(0f, 360f), 0f), new Vector3(r * 2.2f, r * 1.8f, r * 2.2f));
    }

    // верёвка с флажками по цепной линии; a и b в локальных координатах Part
    void Bunting(Part p, Vector3 a, Vector3 b, float sag, float fl)
    {
        float len = Vector3.Distance(a, b);
        int seg = Mathf.Max(8, Mathf.RoundToInt(len / (0.3f * sz)));
        Vector3 prev = a;
        Vector3 along = (b - a).normalized;
        int ci = rng.Next(flag.Length);
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 q = Vector3.Lerp(a, b, t); q.y -= sag * 4f * t * (1f - t);
            p.Beam(mRope, prev, q, 0.022f * sz);
            if (i % 2 == 0 && i < seg)
            {
                float t0 = (i - 0.5f) / seg;
                Vector3 c = Vector3.Lerp(a, b, t0); c.y -= sag * 4f * t0 * (1f - t0);
                Vector3 l = c - along * fl * 0.42f, r = c + along * fl * 0.42f;
                Vector3 tip = c + Vector3.down * fl * 0.9f + new Vector3(0f, 0f, R(-0.03f, 0.03f));
                p.Tri2(flag[ci % flag.Length], l, r, tip);
                ci++;
            }
            prev = q;
        }
    }

    // ---------------------------------------------------------------- столбы с фонарями
    GameObject LampPost(Transform parent, Vector2 p, Vector2 face)
    {
        float Hl = 1.8f * sz;
        var part = new Part(P);
        part.Add(P.ico[1], mStone, new Vector3(0f, 0.03f * sz, 0f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(0.52f, 0.28f, 0.52f) * sz);
        part.Tube(mWood, new Vector3(0f, -0.2f, 0f), new Vector3(0f, Hl, 0f), 0.075f * sz, 0.058f * sz, 8);
        part.Cyl(mRope, new Vector3(0f, 0.55f * sz, 0f), 0.07f * sz, 0.2f * sz, P.cyl8);
        part.Add(P.cone8, mWoodL, new Vector3(0f, Hl + 0.05f * sz, 0f), Quaternion.identity, new Vector3(0.2f, 0.14f, 0.2f) * sz);
        float ya = Hl - 0.12f * sz;
        part.Beam(mIron, new Vector3(0f, ya, 0f), new Vector3(0f, ya, 0.55f * sz), 0.05f * sz);
        part.Beam(mWoodL, new Vector3(0f, ya - 0.38f * sz, 0f), new Vector3(0f, ya - 0.02f * sz, 0.4f * sz), 0.045f * sz);
        part.Add(P.blob[0], mMoss, new Vector3(0.08f * sz, 0.14f * sz, 0.1f * sz), Quaternion.identity, new Vector3(0.34f, 0.14f, 0.3f) * sz);
        if (R() < 0.6f) AddBloom(part, new Vector3(0.12f * sz, 0.22f * sz, 0.2f * sz), 0.05f * sz);
        Vector3 pos = G(p);
        var go = Obj(part, "LampPost", parent, pos, Yaw(face));
        Col(go, new Vector3(0f, Hl * 0.5f, 0f), new Vector3(0.24f * sz, Hl, 0.24f * sz));
        Hang(go.transform, "Swing", new Vector3(0f, ya, 0.55f * sz), 0.16f * sz, sz, true);
        nLamps++;
        lampPts.Add(p);
        return go;
    }

    bool LampOk(Vector2 p, bool glade = false) { return Spot(p, 0.3f * sz, glade ? -1f : 0.2f, s.treeClear * 0.6f); }

    void BuildLamps()
    {
        var g = Group(root, "Lamps");
        // главная тропа: по очереди слева и справа
        float len = main.len;
        float stop = Mathf.Max(5f, len - plan.sawmillRadius * 0.9f);
        float step = s.lampSpacing;
        int side = rng.Next(2) == 0 ? 1 : -1;
        for (float d = 3.2f; d < stop; d += step * R(0.85f, 1.15f))
        {
            Vector2 pos, nrm; float half;
            At(main, mainCum, d, out pos, out nrm, out half);
            bool placed = false;
            for (int attempt = 0; attempt < 4 && !placed; attempt++)
            {
                int sd = attempt < 2 ? side : -side;
                float off = half + (attempt % 2 == 0 ? 0.7f : 1.05f) * sz;
                Vector2 pp = pos + nrm * (sd * off);
                if (!LampOk(pp)) continue;
                LampPost(g, pp, -nrm * sd);
                placed = true;
            }
            side = -side;
        }
        // поляна лесопилки: дуга лампочек на дальней от тропы стороне
        Vector2 awayS = main.p.Count > 3 ? (main.p[main.p.Count - 1] - main.p[main.p.Count - 4]).normalized : Vector2.up;
        GladeLamps(g, plan.sawmill, plan.sawmillRadius, awayS, new[] { -105f, -55f, 0f, 55f, 105f });
        // тупиковая поляна
        if (plan.hasFind)
        {
            var fp = plan.paths.Find(x => x.name == "Branch_Find");
            Vector2 awayF = fp != null && fp.p.Count > 3 ? (fp.p[fp.p.Count - 1] - fp.p[fp.p.Count - 4]).normalized : Vector2.up;
            GladeLamps(g, plan.findSpot, plan.findRadius, awayF, new[] { -70f, 0f, 70f });
        }
    }

    void GladeLamps(Transform parent, Vector2 c, float r, Vector2 away, float[] angles)
    {
        float baseAng = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
        for (int i = 0; i < angles.Length; i++)
        {
            float a = (baseAng + angles[i]) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            for (int k = 0; k < 3; k++)
            {
                Vector2 pp = c + dir * (r * (0.86f - 0.08f * k));
                if (!LampOk(pp, true)) continue;
                LampPost(parent, pp, -dir);
                break;
            }
        }
    }

    // фонари на стволах у тропы: жердь от ствола и фонарь на цепи
    void BuildTreeLanterns()
    {
        if (trees.Count == 0) return;
        var g = Group(root, "TreeLanterns");
        int want = s.treeLanterns;
        float len = main.len;
        int placed = 0;
        for (int i = 0; i < want * 3 && placed < want; i++)
        {
            float d = Mathf.Lerp(6f, Mathf.Max(7f, len - plan.sawmillRadius), (i + R(0.2f, 0.8f)) / (want * 3f));
            Vector2 pos, nrm; float half;
            At(main, mainCum, d, out pos, out nrm, out half);
            int sd = placed % 2 == 0 ? 1 : -1;
            Vector2 probe = pos + nrm * (sd * (half + 2.2f));
            Vector3 tp;
            float dist = trees.Nearest(probe, 3.2f, out tp);
            if (dist >= 3.2f) continue;
            Vector2 tx = XZ(tp);
            Vector2 toTrail = pos - tx;
            float tl = toTrail.magnitude;
            if (tl < 0.9f || tl > 5f) continue;
            bool near = false;
            for (int k = 0; k < lampPts.Count; k++) if (Vector2.Distance(lampPts[k], tx) < 3f) { near = true; break; }
            if (near) continue;
            toTrail /= tl;
            Vector3 root3 = G(tx);
            float yy = 1.85f * sz;
            var part = new Part(P);
            Vector3 a0 = new Vector3(-toTrail.x * 0.15f, yy, -toTrail.y * 0.15f);
            Vector3 a1 = new Vector3(toTrail.x * 0.95f * sz, yy + 0.06f * sz, toTrail.y * 0.95f * sz);
            part.Beam(mWood, a0, a1, 0.07f * sz);
            part.Beam(mWoodL, new Vector3(toTrail.x * 0.15f, yy - 0.3f * sz, toTrail.y * 0.15f), new Vector3(toTrail.x * 0.7f * sz, yy + 0.03f * sz, toTrail.y * 0.7f * sz), 0.04f * sz);
            part.Add(P.blob[1], mLeaf2, a1 + new Vector3(0f, 0.04f * sz, 0f), Quaternion.identity, new Vector3(0.2f, 0.1f, 0.2f) * sz);
            var go = Obj(part, "TreeLanternArm", g, root3, Quaternion.identity);
            Hang(go.transform, "Swing", a1 + new Vector3(0f, -0.02f * sz, 0f), 0.3f * sz, 0.95f * sz, true);
            nLantern++; placed++;
            lampPts.Add(tx);
        }
    }

    // ---------------------------------------------------------------- пропсы
    void BuildProps()
    {
        var g = Group(root, "Props");
        float k = s.propsK;

        // поляна лесопилки: пни, дрова, телега по краю (центр свободен под лесопилку)
        Vector2 awayS = main.p.Count > 3 ? (main.p[main.p.Count - 1] - main.p[main.p.Count - 4]).normalized : Vector2.up;
        float baseAng = Mathf.Atan2(awayS.y, awayS.x) * Mathf.Rad2Deg;
        float sr = plan.sawmillRadius;
        Action<float, float, Action<Vector2, Vector2>> onRim = (ang, rr, act) =>
        {
            float a = (baseAng + ang) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 p = plan.sawmill + dir * (sr * rr);
            if (!Spot(p, 0.9f * sz, -1f, 0.6f)) return;
            act(p, dir);
        };
        if (k > 0.2f)
        {
            onRim(38f, 0.72f, (p, d) => LogPile(g, p, new Vector2(-d.y, d.x)));
            onRim(-42f, 0.74f, (p, d) => Cart(g, p, new Vector2(-d.y, d.x) * (R() < 0.5f ? 1 : -1)));
            onRim(80f, 0.78f, (p, d) => Stump(g, p, true));
            onRim(-84f, 0.8f, (p, d) => Stump(g, p, true));
        }
        if (k > 0.9f)
        {
            onRim(12f, 0.82f, (p, d) => Stump(g, p, false));
            onRim(-18f, 0.84f, (p, d) => Stump(g, p, R() < 0.5f));
            onRim(120f, 0.8f, (p, d) => LogPile(g, p, new Vector2(-d.y, d.x)));
        }

        // вдоль тропы: пни с топорами и поленницы изредка
        int extra = Mathf.RoundToInt(main.len / 16f * k);
        for (int i = 0; i < extra; i++)
        {
            float d = R(6f, Mathf.Max(7f, main.len - sr));
            Vector2 pos, nrm; float half;
            At(main, mainCum, d, out pos, out nrm, out half);
            int sd = rng.Next(2) == 0 ? 1 : -1;
            Vector2 p = pos + nrm * (sd * (half + R(0.8f, 1.8f)));
            if (!Spot(p, 0.4f * sz, 0.2f)) continue;
            if (R() < 0.7f) Stump(g, p, R() < 0.6f); else LogPile(g, p, new Vector2(nrm.y, -nrm.x));
        }

        // заборчик по бокам арки
        var arch = GameObject.Find(ForestDecor.RootName + "/ForestArch");
        if (arch != null && s.arch)
        {
            Vector2 dir = MainDir();
            Vector2 lat = new Vector2(-dir.y, dir.x);
            float W = Mathf.Max(2.2f, (main.half.Count > 0 ? main.half[0] : 1.7f) * 2f + 0.7f);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector2 start = plan.gate;
                var gt = GameObject.Find("ForestGate");
                if (gt != null) start = XZ(gt.transform.position);
                start += lat * (sg * (W * 0.5f + 0.9f * sz + 0.3f));
                Fence(g, start, lat * sg, 3, k);
            }
        }

        // указатель на развилке
        var br = plan.paths.Find(x => x.name == "Branch_Find") ?? (plan.paths.Count > 1 ? plan.paths.Find(x => !x.main) : null);
        if (br != null && br.p.Count > 4)
        {
            Vector2 fork = br.p[0];
            Vector2 mdir = Vector2.up; float mh = 1.5f;
            float bestD = float.MaxValue;
            for (int i = 0; i < main.p.Count - 1; i++)
            {
                float dd = (main.p[i] - fork).sqrMagnitude;
                if (dd < bestD) { bestD = dd; mdir = (main.p[i + 1] - main.p[i]).normalized; mh = main.half[i]; }
            }
            Vector2 side = new Vector2(-mdir.y, mdir.x);
            Vector2 bdir = (br.p[Mathf.Min(6, br.p.Count - 1)] - br.p[0]).normalized;
            float sgn = Vector2.Dot(bdir, side) >= 0f ? -1f : 1f;    // знак ставим на противоположной ветке стороне, чтобы не мешать
            Vector2 sp = fork + side * (sgn * (mh + 1.0f * sz));
            if (Spot(sp, 0.4f * sz, 0.2f))
                SignPost(g, sp, new[] { mdir, bdir });
        }
    }

    void Stump(Transform parent, Vector2 p, bool axe)
    {
        var part = new Part(P);
        float h = R(0.32f, 0.5f) * sz, r = R(0.28f, 0.4f) * sz;
        part.Tube(mBark, new Vector3(0f, -0.12f, 0f), new Vector3(0f, h, 0f), r * 1.15f, r * 0.92f, 8);
        part.Add(P.cyl8, mWoodEnd, new Vector3(0f, h, 0f), Quaternion.identity, new Vector3(r * 1.82f, 0.025f, r * 1.82f));
        part.Add(P.cyl8, mWoodL, new Vector3(0f, h + 0.01f, 0f), Quaternion.identity, new Vector3(r * 1.0f, 0.025f, r * 1.0f));
        for (int i = 0; i < 4; i++)
        {
            float a = (i + R(-0.2f, 0.2f)) * Mathf.PI * 0.5f;
            part.Beam(mBark, new Vector3(Mathf.Cos(a) * r * 0.8f, 0.1f * sz, Mathf.Sin(a) * r * 0.8f), new Vector3(Mathf.Cos(a) * r * 1.55f, -0.02f, Mathf.Sin(a) * r * 1.55f), 0.1f * sz, 0.07f * sz);
        }
        if (R() < 0.5f) part.Add(P.blob[1], mMoss, new Vector3(r * 0.7f, 0.06f * sz, -r * 0.5f), Quaternion.identity, new Vector3(0.28f, 0.1f, 0.24f) * sz);
        if (axe)
        {
            Vector3 hb = new Vector3(r * 0.2f, h + 0.06f * sz, 0f);
            Vector3 ht = hb + new Vector3(r * 0.25f, 0.62f * sz, 0.02f);
            part.Beam(mWoodL, hb, ht, 0.045f * sz);
            part.Box(mIron, hb + new Vector3(-0.065f * sz, 0.03f * sz, 0f), new Vector3(0.17f, 0.13f, 0.035f) * sz);
            part.Box(mStone, hb + new Vector3(-0.15f * sz, 0.03f * sz, 0f), new Vector3(0.02f, 0.15f, 0.03f) * sz);
        }
        var go = Obj(part, axe ? "StumpAxe" : "Stump", parent, G(p), Quaternion.Euler(0f, R(0f, 360f), 0f));
        Col(go, new Vector3(0f, h * 0.5f, 0f), new Vector3(r * 1.8f, h, r * 1.8f));
        nProps++;
    }

    void LogPile(Transform parent, Vector2 p, Vector2 along)
    {
        var part = new Part(P);
        float rr = 0.14f * sz, L = R(1.0f, 1.3f) * sz;
        int[] rows = { 3, 2, 1 };
        for (int row = 0; row < rows.Length; row++)
            for (int i = 0; i < rows[row]; i++)
            {
                float x = (i - (rows[row] - 1) * 0.5f) * rr * 2.05f;
                float y = rr + row * rr * 1.75f;
                float jitter = R(-0.05f, 0.05f) * sz;
                Vector3 a = new Vector3(x, y, -L * 0.5f + jitter), b = new Vector3(x, y, L * 0.5f + jitter);
                part.Tube(mBark, a, b, rr, rr * 0.97f, 8);
                part.Cap(mWoodEnd, b + Vector3.forward * 0.004f, rr * 1.9f);
                part.Cap(mWoodEnd, a - Vector3.forward * 0.004f, rr * 1.9f);
            }
        for (int sg = -1; sg <= 1; sg += 2)
            part.Tube(mWood, new Vector3(sg * rr * 3.4f, -0.1f, 0f), new Vector3(sg * rr * 3.4f, rr * 5.2f, 0f), 0.05f * sz, 0.04f * sz, 6);
        var go = Obj(part, "LogPile", parent, G(p), Yaw(along));
        Col(go, new Vector3(0f, rr * 2.5f, 0f), new Vector3(rr * 7.5f, rr * 5f, L));
        nProps++;
    }

    void Cart(Transform parent, Vector2 p, Vector2 face)
    {
        var part = new Part(P);
        float w = 1.2f * sz, d = 0.7f * sz;
        part.Box(mWoodL, new Vector3(0f, 0.45f * sz, 0f), new Vector3(w, 0.07f * sz, d));
        for (int sg = -1; sg <= 1; sg += 2)
        {
            part.Box(mWood, new Vector3(0f, 0.57f * sz, sg * d * 0.5f), new Vector3(w, 0.22f * sz, 0.05f * sz));
            part.Box(mWood, new Vector3(sg * w * 0.5f, 0.57f * sz, 0f), new Vector3(0.05f * sz, 0.22f * sz, d));
            part.Cap(mWoodL, new Vector3(0.08f * sz, 0.28f * sz, sg * (d * 0.5f + 0.07f * sz)), 0.56f * sz, 0.07f * sz);
            part.Cap(mWood, new Vector3(0.08f * sz, 0.28f * sz, sg * (d * 0.5f + 0.1f * sz)), 0.12f * sz);
            part.Beam(mWood, new Vector3(0.08f * sz, 0.28f * sz, sg * (d * 0.5f + 0.075f * sz)), new Vector3(0.08f * sz + 0.26f * sz, 0.28f * sz + 0.1f * sz, sg * (d * 0.5f + 0.075f * sz)), 0.04f * sz, 0.02f * sz);
            part.Beam(mWood, new Vector3(0.08f * sz, 0.28f * sz, sg * (d * 0.5f + 0.075f * sz)), new Vector3(0.08f * sz - 0.2f * sz, 0.28f * sz + 0.2f * sz, sg * (d * 0.5f + 0.075f * sz)), 0.04f * sz, 0.02f * sz);
            part.Beam(mWoodL, new Vector3(-w * 0.5f, 0.5f * sz, sg * d * 0.36f), new Vector3(-w * 0.5f - 0.8f * sz, 0.28f * sz, sg * d * 0.32f), 0.06f * sz);
        }
        part.Beam(mIron, new Vector3(0.08f * sz, 0.28f * sz, -d * 0.5f), new Vector3(0.08f * sz, 0.28f * sz, d * 0.5f), 0.04f * sz);
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.3f * sz;
            part.Tube(mBark, new Vector3(x, 0.66f * sz, -0.28f * sz), new Vector3(x, 0.66f * sz, 0.28f * sz), 0.1f * sz, 0.095f * sz, 8);
            part.Cap(mWoodEnd, new Vector3(x, 0.66f * sz, 0.284f * sz), 0.19f * sz);
        }
        part.Tube(mBark, new Vector3(-0.15f * sz, 0.84f * sz, -0.2f * sz), new Vector3(-0.15f * sz, 0.84f * sz, 0.22f * sz), 0.1f * sz, 0.095f * sz, 8);
        var go = Obj(part, "WoodCart", parent, G(p), Yaw(face) * Quaternion.Euler(0f, 90f, 0f));
        Col(go, new Vector3(0f, 0.55f * sz, 0f), new Vector3(w + 0.8f * sz, 0.7f * sz, d + 0.2f * sz));
        nProps++;
    }

    void Fence(Transform parent, Vector2 start, Vector2 dir, int posts, float k)
    {
        if (k < 0.3f) return;
        var part = new Part(P);
        float gap = 1.1f * sz;
        for (int i = 0; i < posts; i++)
        {
            float x = i * gap;
            float h = R(0.8f, 0.92f) * sz;
            Quaternion tilt = Quaternion.Euler(R(-3f, 3f), 0f, R(-3f, 3f));
            part.Add(P.box, mWood, new Vector3(x, h * 0.5f - 0.05f * sz, 0f), tilt, new Vector3(0.1f, h, 0.1f) * sz);
            part.Add(P.cone6, mWoodL, new Vector3(x, h + 0.04f * sz, 0f), tilt, new Vector3(0.14f, 0.12f, 0.14f) * sz);
            if (i < posts - 1)
            {
                float n = (i % 2 == 0) ? 1f : -1f;
                part.Beam(mWoodL, new Vector3(x, 0.38f * sz, 0.06f * sz), new Vector3(x + gap, 0.4f * sz + 0.02f * sz * n, 0.06f * sz), 0.05f * sz, 0.08f * sz);
                part.Beam(mWoodL, new Vector3(x, 0.66f * sz, -0.06f * sz), new Vector3(x + gap, 0.64f * sz - 0.02f * sz * n, -0.06f * sz), 0.05f * sz, 0.08f * sz);
            }
        }
        if (R() < 0.7f) part.Add(P.blob[2], mLeaf, new Vector3(gap * 0.5f, 0.1f * sz, 0.1f * sz), Quaternion.identity, new Vector3(0.5f, 0.28f, 0.34f) * sz);
        Vector2 ob = dir.normalized;
        var go = Obj(part, "Fence", parent, G(start), Quaternion.LookRotation(new Vector3(-ob.y, 0f, ob.x), Vector3.up));
        Col(go, new Vector3(gap, 0.45f * sz, 0f), new Vector3(gap * (posts - 1) + 0.2f * sz, 0.9f * sz, 0.14f * sz));
        nProps++;
    }

    void SignPost(Transform parent, Vector2 p, Vector2[] dirs)
    {
        var part = new Part(P);
        float h = 1.5f * sz;
        part.Tube(mWood, new Vector3(0f, -0.2f, 0f), new Vector3(0f, h, 0f), 0.06f * sz, 0.05f * sz, 8);
        part.Add(P.cone8, mWoodL, new Vector3(0f, h + 0.05f * sz, 0f), Quaternion.identity, new Vector3(0.14f, 0.12f, 0.14f) * sz);
        part.Add(P.ico[0], mStone, new Vector3(0.1f * sz, 0.04f * sz, 0.06f * sz), Quaternion.identity, new Vector3(0.42f, 0.24f, 0.4f) * sz);
        for (int i = 0; i < dirs.Length; i++)
        {
            float yaw = Mathf.Atan2(-dirs[i].y, dirs[i].x) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            float y = h - 0.22f * sz - i * 0.3f * sz;
            float len = 0.8f * sz;
            part.Add(P.box, i == 0 ? mWoodL : mWoodEnd, q * new Vector3(len * 0.5f, y, 0f), q, new Vector3(len, 0.17f * sz, 0.04f * sz));
            part.Add(P.box, i == 0 ? mWoodL : mWoodEnd, q * new Vector3(len + 0.02f * sz, y, 0f), q * Quaternion.Euler(0f, 0f, 45f), new Vector3(0.12f * sz, 0.12f * sz, 0.04f * sz));
            part.Add(P.box, mWood, q * new Vector3(len * 0.45f, y, 0.025f * sz), q, new Vector3(len * 0.5f, 0.03f * sz, 0.012f * sz));
        }
        var go = Obj(part, "SignPost", parent, G(p), Quaternion.identity);
        Col(go, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.18f * sz, h, 0.18f * sz));
        nProps++;
    }

    // ---------------------------------------------------------------- природа
    void BuildNature()
    {
        var g = Group(root, "Nature");
        var shroom = new Part(P); var fern = new Part(P); var flowers = new Part(P); var rocks = new Part(P); var bush = new Part(P); var logs = new Part(P);
        float k = s.natureK;

        // грибы кольцом у стволов вдоль троп
        int budget = Mathf.RoundToInt(22f * k);
        for (int pi = 0; pi < plan.paths.Count && budget > 0; pi++)
        {
            var path = plan.paths[pi];
            var cum = Cum(path);
            for (float d = 2f; d < path.len && budget > 0; d += R(3f, 6f) / Mathf.Max(0.3f, k))
            {
                Vector2 pos, nrm; float half;
                At(path, cum, d, out pos, out nrm, out half);
                int sd = rng.Next(2) == 0 ? 1 : -1;
                Vector2 probe = pos + nrm * (sd * (half + 2.5f));
                Vector3 tp;
                if (trees.Nearest(probe, 4f, out tp) >= 4f) continue;
                Vector2 tx = XZ(tp);
                Vector2 away = (probe - tx);
                if (away.sqrMagnitude < 0.01f) away = nrm * sd;
                away.Normalize();
                int n = rng.Next(3, 6);
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.Atan2(away.y, away.x) + R(-1.1f, 1.1f);
                    Vector2 q = tx + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R(0.65f, 1.15f) * (0.6f + 0.4f * sz);
                    if (!Spot(q, 0.1f, 0.2f, 0.35f)) continue;
                    Shroom(shroom, G(q), R(0.1f, 0.2f) * sz);
                    any = true;
                }
                if (any) { budget--; nNature++; }
            }
        }

        // папоротники, цветы, кусты, камни: вдоль троп, у полян
        for (int pi = 0; pi < plan.paths.Count; pi++)
        {
            var path = plan.paths[pi];
            var cum = Cum(path);
            for (float d = 1.5f; d < path.len; d += R(1.6f, 3.2f) / Mathf.Max(0.3f, k))
            {
                Vector2 pos, nrm; float half;
                At(path, cum, d, out pos, out nrm, out half);
                int sd = rng.Next(2) == 0 ? 1 : -1;
                Vector2 p = pos + nrm * (sd * (half + R(0.3f, 2.6f)));
                float roll = R();
                if (roll < 0.34f)
                {
                    if (!Spot(p, 0.3f, 0.2f, 0.5f)) continue;
                    FernClump(fern, G(p), R(0.7f, 1.25f) * sz);
                }
                else if (roll < 0.62f)
                {
                    if (!Spot(p, 0.3f, 0.2f, 0.4f)) continue;
                    FlowerPatch(flowers, p, R(0.35f, 0.7f) * sz);
                }
                else if (roll < 0.8f)
                {
                    if (!Spot(p, 0.5f, 0.2f, 0.7f)) continue;
                    BerryBush(bush, G(p), R(0.55f, 0.9f) * sz);
                }
                else
                {
                    if (!Spot(p, 0.4f, 0.2f, 0.6f)) continue;
                    MossRock(rocks, G(p), R(0.35f, 0.95f) * sz);
                }
                nNature++;
            }
        }

        // поваленные брёвна
        int nl = Mathf.RoundToInt(main.len / 22f * k);
        for (int i = 0; i < nl; i++)
        {
            float d = R(8f, Mathf.Max(9f, main.len - plan.sawmillRadius));
            Vector2 pos, nrm; float half;
            At(main, mainCum, d, out pos, out nrm, out half);
            int sd = rng.Next(2) == 0 ? 1 : -1;
            Vector2 p = pos + nrm * (sd * (half + R(1.6f, 3.2f)));
            if (!Spot(p, 0.8f * sz, 0.2f, 0.7f)) continue;
            FallenLog(logs, G(p), Quaternion.Euler(0f, R(0f, 360f), 0f));
            nNature++;
        }

        if (!shroom.Empty) Obj(shroom, "Mushrooms", g, Vector3.zero, Quaternion.identity, false);
        if (!fern.Empty) Obj(fern, "Ferns", g, Vector3.zero, Quaternion.identity, false);
        if (!flowers.Empty) Obj(flowers, "Flowers", g, Vector3.zero, Quaternion.identity, false);
        if (!rocks.Empty) Obj(rocks, "MossRocks", g, Vector3.zero, Quaternion.identity);
        if (!bush.Empty) Obj(bush, "Bushes", g, Vector3.zero, Quaternion.identity);
        if (!logs.Empty) Obj(logs, "FallenLogs", g, Vector3.zero, Quaternion.identity);
    }

    void Shroom(Part p, Vector3 pos, float h)
    {
        Quaternion q = Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
        p.Add(P.cyl6, mCream, pos + q * new Vector3(0f, h * 0.5f, 0f), q, new Vector3(h * 0.36f, h, h * 0.36f));
        float cw = h * 1.15f;
        Vector3 cp = pos + q * new Vector3(0f, h * 0.95f, 0f);
        p.Add(P.blob[0], mRed, cp, q, new Vector3(cw * 1.7f, cw * 0.9f, cw * 1.7f));
        for (int i = 0; i < 5; i++)
        {
            float a = R(0f, Mathf.PI * 2f), fr = R(0.25f, 0.7f);
            float x = Mathf.Cos(a) * fr * cw * 0.85f, z = Mathf.Sin(a) * fr * cw * 0.85f;
            float y = cw * 0.45f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - fr * fr)) * 0.95f;
            p.Add(P.blob[1], mSpot, cp + q * new Vector3(x, y, z), q, new Vector3(cw * 0.3f, cw * 0.16f, cw * 0.3f));
        }
    }

    void FernClump(Part p, Vector3 pos, float scale)
    {
        int n = rng.Next(6, 10);
        float off = R(0f, 6.28f);
        for (int i = 0; i < n; i++)
        {
            float a = off + i * Mathf.PI * 2f / n + R(-0.25f, 0.25f);
            float L = R(0.38f, 0.6f) * scale;
            Vector3 b = pos + new Vector3(Mathf.Cos(a) * 0.05f, 0.02f, Mathf.Sin(a) * 0.05f);
            Vector3 mid = pos + new Vector3(Mathf.Cos(a) * L * 0.6f, L * 0.55f, Mathf.Sin(a) * L * 0.6f);
            Vector3 tip = pos + new Vector3(Mathf.Cos(a) * L * 1.05f, L * 0.4f, Mathf.Sin(a) * L * 1.05f);
            p.Beam(mFern, b, mid, 0.09f * scale, 0.018f);
            p.Beam(i % 2 == 0 ? mFern : mLeaf2, mid, tip, 0.08f * scale, 0.016f);
        }
    }

    void FlowerPatch(Part p, Vector2 c, float rad)
    {
        int n = rng.Next(4, 9);
        Material m;
        switch (rng.Next(4)) { case 0: m = mFlowW; break; case 1: m = mFlowY; break; case 2: m = mFlowP; break; default: m = mFlowV; break; }
        for (int i = 0; i < n; i++)
        {
            float a = R(0f, 6.28f), d = R(0f, rad);
            Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if (!Spot(q, 0.05f, 0.15f, 0.3f)) continue;
            Vector3 b = G(q);
            float h = R(0.16f, 0.3f) * sz;
            p.Beam(mLeaf, b, b + new Vector3(R(-0.03f, 0.03f), h, R(-0.03f, 0.03f)), 0.022f);
            p.Add(P.octa, (i % 5 == 4) ? mFlowY : m, b + Vector3.up * h, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(0.1f, 0.07f, 0.1f) * sz);
        }
    }

    void BerryBush(Part p, Vector3 pos, float scale)
    {
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f + R(0f, 1f);
            float bs = scale * R(0.5f, 0.7f);
            p.Add(P.blob[i % 3], i == 1 ? mLeaf2 : mLeaf, pos + new Vector3(Mathf.Cos(a) * scale * 0.22f, bs * 0.4f, Mathf.Sin(a) * scale * 0.22f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(bs, bs * 0.85f, bs));
        }
        int berries = rng.Next(3, 8);
        bool flow = R() < 0.4f;
        for (int i = 0; i < berries; i++)
        {
            float a = R(0f, 6.28f), el = R(0.2f, 0.9f);
            Vector3 d = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
            p.Add(P.octa, flow ? (i % 2 == 0 ? mFlowW : mFlowP) : mBerry, pos + new Vector3(0f, scale * 0.3f, 0f) + d * scale * 0.42f, Quaternion.identity, Vector3.one * 0.07f * sz);
        }
    }

    void MossRock(Part p, Vector3 pos, float scale)
    {
        int i = rng.Next(3);
        p.Add(P.ico[i], rng.Next(2) == 0 ? mStone : mStoneD, pos + Vector3.up * (scale * 0.08f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(scale, scale * 0.8f, scale * R(0.7f, 1f)));
        p.Add(P.blob[i], mMoss, pos + new Vector3(scale * R(-0.1f, 0.1f), scale * 0.26f, scale * R(-0.1f, 0.1f)), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(scale * 0.7f, scale * 0.22f, scale * 0.6f));
        if (R() < 0.5f) p.Add(P.ico[(i + 1) % 3], mStoneD, pos + new Vector3(scale * 0.7f, scale * 0.05f, scale * 0.2f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(scale * 0.45f, scale * 0.35f, scale * 0.4f));
    }

    void FallenLog(Part p, Vector3 pos, Quaternion rot)
    {
        float L = R(1.6f, 2.6f) * sz, r = R(0.15f, 0.22f) * sz;
        Vector3 a = rot * new Vector3(-L * 0.5f, r * 0.95f, 0f) + pos, b = rot * new Vector3(L * 0.5f, r * 1.05f, 0f) + pos;
        p.Tube(mBark, a, b, r, r * 0.9f, 8);
        Vector3 axis = (b - a).normalized;
        p.Add(P.cyl8, mWoodEnd, b + axis * 0.004f, Quaternion.FromToRotation(Vector3.up, axis), new Vector3(r * 1.8f, 0.02f, r * 1.8f));
        p.Add(P.cyl8, mWoodEnd, a - axis * 0.004f, Quaternion.FromToRotation(Vector3.up, -axis), new Vector3(r * 1.8f, 0.02f, r * 1.8f));
        for (int i = 0; i < 3; i++)
        {
            float t = R(0.1f, 0.9f);
            Vector3 c = Vector3.Lerp(a, b, t) + Vector3.up * r * 0.85f;
            p.Add(P.blob[i], mMoss, c, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(r * 2.2f, r * 0.7f, r * 1.8f));
        }
        Vector3 side = rot * Vector3.forward;
        if (R() < 0.8f)
        {
            Vector3 c = Vector3.Lerp(a, b, R(0.3f, 0.7f)) + side * r * 0.95f + Vector3.up * r * 0.2f;
            p.Add(P.blob[0], mShelf, c, Quaternion.LookRotation(side), new Vector3(r * 1.1f, r * 0.35f, r * 1.1f));
            p.Add(P.blob[1], mShelf, c + new Vector3(0f, r * 0.7f, 0f) + side * r * 0.1f, Quaternion.LookRotation(side), new Vector3(r * 0.8f, r * 0.28f, r * 0.8f));
        }
    }

    // ---------------------------------------------------------------- свет: пятна, лучи, светлячки
    struct Pt { public Vector2 p; public Vector2 nrm; public float half; }

    Pt RandomTrailPoint(float lateral)
    {
        // доля точек на главной, остальные на ветках
        var path = (plan.paths.Count > 1 && R() < 0.3f) ? plan.paths[rng.Next(plan.paths.Count)] : main;
        var cum = (path == main) ? mainCum : Cum(path);
        Vector2 pos, nrm; float half;
        At(path, cum, R(0.5f, Mathf.Max(1f, path.len - 0.5f)), out pos, out nrm, out half);
        return new Pt { p = pos + nrm * R(-1f, 1f) * (half + lateral), nrm = nrm, half = half };
    }

    void BuildFx()
    {
        var fx = Group(root, "Fx");
        Color tint = s.glowTint;
        var atm = fx.gameObject.AddComponent<ForestAtmosphere>();
        var groups = new List<ForestAtmosphere.Group>();

        if (s.patches)
        {
            var matP = FD.Glow("SunPatch", new Color(tint.r, tint.g, tint.b, 0.20f), 1.4f);
            var mesh = PatchMesh();
            if (mesh != null)
            {
                var go = new GameObject("SunPatches");
                go.transform.SetParent(fx, false);
                AddMesh(go, mesh, matP, false);
                rPatch.Add(go.GetComponent<Renderer>());
                groups.Add(new ForestAtmosphere.Group { name = "Пятна солнца", renderers = rPatch.ToArray(), day = 1f, night = 0.2f });
            }
        }
        if (s.shafts)
        {
            var matS = FD.Glow("SunShaft", new Color(tint.r, tint.g, tint.b, 0.10f), 1.6f);
            var mesh = ShaftMesh();
            if (mesh != null)
            {
                var go = new GameObject("SunShafts");
                go.transform.SetParent(fx, false);
                AddMesh(go, mesh, matS, false);
                rShaft.Add(go.GetComponent<Renderer>());
                groups.Add(new ForestAtmosphere.Group { name = "Лучи", renderers = rShaft.ToArray(), day = 1f, night = 0f });
            }
        }
        if (s.fireflies)
        {
            var matF = FD.Glow("Firefly", Color.white, 2f);
            int flyMade = 0;
            for (int tries = 0; tries < s.fireflyVolumes * 8 && flyMade < s.fireflyVolumes; tries++)
            {
                Pt t = RandomTrailPoint(2.5f);
                if (!Spot(t.p, 0.2f, 0f, 0.3f)) continue;
                var ps = ToonKit.MakeGlow(fx, "Fireflies_" + (flyMade++), G(t.p) + Vector3.up * 1.1f * sz, new Vector3(5f, 1.6f * sz, 5f),
                    new Color(1f, 0.95f, 0.45f, 1f), new Color(0.7f, 1f, 0.45f, 1f), 0.16f * sz, 22, 7f, 0.04f, matF);
                rFly.Add(ps.GetComponent<ParticleSystemRenderer>());
            }
            // облако у арки: встречает игрока
            var gt = GameObject.Find("ForestGate");
            if (gt != null)
            {
                var ps = ToonKit.MakeGlow(fx, "Fireflies_Gate", gt.transform.position + MainDirV3() * 2.5f + Vector3.up * 1.2f * sz, new Vector3(5f, 1.6f * sz, 4f),
                    new Color(1f, 0.95f, 0.45f, 1f), new Color(0.7f, 1f, 0.45f, 1f), 0.16f * sz, 18, 7f, 0.04f, matF);
                rFly.Add(ps.GetComponent<ParticleSystemRenderer>());
            }
            groups.Add(new ForestAtmosphere.Group { name = "Светлячки", renderers = rFly.ToArray(), day = 0.3f, night = 1.3f });
        }
        atm.groups = groups.ToArray();
    }

    Vector3 MainDirV3() { Vector2 d = MainDir(); return new Vector3(d.x, 0f, d.y); }

    // эллиптические пятна, лежащие на земле (сетка 5x5 повторяет рельеф)
    Mesh PatchMesh()
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var cols = new List<Color>(); var tri = new List<int>();
        const int N = 5;
        int made = 0;
        for (int i = 0; i < s.patchCount * 4 && made < s.patchCount; i++)
        {
            Pt t = RandomTrailPoint(3.2f);
            if (!Spot(t.p, 0.3f, 0f, 0.2f)) continue;
            float rx = R(0.9f, 1.9f) * Mathf.Max(0.8f, sz), rz = R(0.7f, 1.4f) * Mathf.Max(0.8f, sz);
            float yaw = R(0f, 6.28f), cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            float a = R(0.55f, 1f);
            int b0 = vs.Count;
            for (int zi = 0; zi < N; zi++)
                for (int xi = 0; xi < N; xi++)
                {
                    float u = xi / (float)(N - 1) * 2f - 1f, v = zi / (float)(N - 1) * 2f - 1f;
                    float ox = u * rx, oz = v * rz;
                    Vector2 w = t.p + new Vector2(ox * cy - oz * sy, ox * sy + oz * cy);
                    vs.Add(new Vector3(w.x, Gy(w) + 0.06f, w.y));
                    uv.Add(new Vector2(u * 0.5f + 0.5f, v * 0.5f + 0.5f));
                    cols.Add(new Color(1f, 1f, 1f, a));
                }
            for (int zi = 0; zi < N - 1; zi++)
                for (int xi = 0; xi < N - 1; xi++)
                {
                    int i0 = b0 + zi * N + xi, i1 = i0 + 1, i2 = i0 + N, i3 = i2 + 1;
                    tri.Add(i0); tri.Add(i2); tri.Add(i1);
                    tri.Add(i1); tri.Add(i2); tri.Add(i3);
                }
            made++;
        }
        if (made == 0) return null;
        var m = new Mesh { name = "SunPatches", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cols); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return FD.Save(m, "SunPatches");
    }

    // лучи: пары скрещённых вертикальных квадов, наклон по направлению солнца
    Mesh ShaftMesh()
    {
        Vector3 dirDown = new Vector3(0.35f, -1f, 0.25f);
        if (RenderSettings.sun != null) dirDown = RenderSettings.sun.transform.forward;
        if (dirDown.y > -0.2f) dirDown = new Vector3(dirDown.x, -0.5f, dirDown.z);
        dirDown.Normalize();
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var cols = new List<Color>(); var tri = new List<int>();
        int made = 0;
        for (int i = 0; i < s.shaftCount * 4 && made < s.shaftCount; i++)
        {
            Pt t = RandomTrailPoint(2.5f);
            if (!Spot(t.p, 0.3f, 0f, 0.2f)) continue;
            float H = R(5f, 8f) * Mathf.Max(0.8f, sz);
            float w = R(0.9f, 1.8f) * Mathf.Max(0.8f, sz);
            Vector3 b = G(t.p) + Vector3.down * 0.2f;
            Vector3 top = b - dirDown * (H / -dirDown.y);
            Vector3 s1 = Vector3.Cross(Vector3.up, dirDown).normalized;
            Vector3 s2 = Vector3.Cross(s1, dirDown).normalized;
            float a = R(0.6f, 1f);
            for (int q = 0; q < 2; q++)
            {
                Vector3 sd = q == 0 ? s1 : s2;
                int b0 = vs.Count;
                vs.Add(b - sd * w * 0.5f); uv.Add(new Vector2(0f, 0f));
                vs.Add(b + sd * w * 0.5f); uv.Add(new Vector2(1f, 0f));
                vs.Add(top + sd * w * 0.9f); uv.Add(new Vector2(1f, 1f));
                vs.Add(top - sd * w * 0.9f); uv.Add(new Vector2(0f, 1f));
                for (int c = 0; c < 4; c++) cols.Add(new Color(1f, 1f, 1f, a));
                tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 1);
                tri.Add(b0); tri.Add(b0 + 3); tri.Add(b0 + 2);
            }
            made++;
        }
        if (made == 0) return null;
        var m = new Mesh { name = "SunShafts", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cols); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return FD.Save(m, "SunShafts");
    }
}
#endif

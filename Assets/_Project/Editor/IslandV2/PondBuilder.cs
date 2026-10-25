// Tools > BuilderSim > Природа > Pond Builder
// Процедурный сказочный пруд: бортик с травой и цветами, вода, кувшинки, камыш, камни, мостик для рыбалки с
// удочкой и фонарями, рыбки, светлячки. Unity 6 (6000.3), URP.
// Кладётся в Assets/_Project/Editor/Nature/. Рантайм-компонент PondLife.cs кладётся в Assets/_Project/Scripts/ (НЕ в Editor).
// Шейдеры Project/Pond/Toon, Project/Pond/Water, Project/Pond/Glow лежат в Assets/_Project/Shaders/Pond/.
// Самодостаточный: не требует HutMesh / EditorAssets.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// ============================================================================
//  Копилка геометрии: одна вершина = позиция + цвет (A = вес покачивания) + UV.x (свечение)
//  Всё, что добавлено через Tri/Quad/Box/Tube/Ico, не делит вершины => плоские грани (low-poly).
//  Сетки с общими вершинами (берег, тело рыбки) сглаживаются сами, RecalculateNormals учитывает индексы.
// ============================================================================
class PondMB
{
    public List<Vector3> P = new List<Vector3>();
    public List<Color> C = new List<Color>();
    public List<Vector2> U = new List<Vector2>();
    public List<int> T = new List<int>();

    public int Count { get { return P.Count; } }

    public int V(Vector3 p, Color c, float e)
    {
        P.Add(p); C.Add(c); U.Add(new Vector2(e, 0f));
        return P.Count - 1;
    }

    public void I(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }

    public static Color Mul(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }

    // треугольник, переворачивается так, чтобы нормаль смотрела в сторону hint
    public void Tri3(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float e, Vector3 hint)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0f)
        {
            Vector3 tp = b; b = c; c = tp;
            Color tc = cb; cb = cc; cc = tc;
        }
        int i0 = V(a, ca, e), i1 = V(b, cb, e), i2 = V(c, cc, e);
        I(i0, i1, i2);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, float e, Vector3 hint) { Tri3(a, b, c, col, col, col, e, hint); }

    // двусторонний треугольник (лезвия травы, лепестки, плавники)
    public void Tri2(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float e)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        Tri3(a, b, c, ca, cb, cc, e, n);
        Tri3(a, b, c, ca, cb, cc, e, -n);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float e, Vector3 hint)
    {
        Tri3(a, b, c, col, col, col, e, hint);
        Tri3(a, c, d, col, col, col, e, hint);
    }

    public void Box(Vector3 center, Vector3 size, Quaternion rot, Color col, float e)
    {
        Vector3 h = size * 0.5f;
        System.Func<float, float, float, Vector3> cn = (sx, sy, sz) => center + rot * new Vector3(h.x * sx, h.y * sy, h.z * sz);
        Quad(cn(1, -1, -1), cn(1, 1, -1), cn(1, 1, 1), cn(1, -1, 1), Mul(col, 0.92f), e, rot * Vector3.right);
        Quad(cn(-1, -1, 1), cn(-1, 1, 1), cn(-1, 1, -1), cn(-1, -1, -1), Mul(col, 0.88f), e, rot * Vector3.left);
        Quad(cn(-1, 1, -1), cn(-1, 1, 1), cn(1, 1, 1), cn(1, 1, -1), Mul(col, 1.08f), e, rot * Vector3.up);
        Quad(cn(-1, -1, 1), cn(-1, -1, -1), cn(1, -1, -1), cn(1, -1, 1), Mul(col, 0.70f), e, rot * Vector3.down);
        Quad(cn(-1, -1, 1), cn(1, -1, 1), cn(1, 1, 1), cn(-1, 1, 1), Mul(col, 0.96f), e, rot * Vector3.forward);
        Quad(cn(1, -1, -1), cn(-1, -1, -1), cn(-1, 1, -1), cn(1, 1, -1), Mul(col, 0.84f), e, rot * Vector3.back);
    }

    // цилиндр / конус между двумя точками. sw0/sw1 = вес покачивания у концов a и b
    public void Tube(Vector3 a, Vector3 b, float r0, float r1, int sides, Color c0, Color c1, float e, bool capA, bool capB, float sw0, float sw1)
    {
        Vector3 ax = b - a;
        float len = ax.magnitude;
        if (len < 1e-5f) return;
        ax /= len;
        Vector3 hlp = Mathf.Abs(ax.y) < 0.95f ? Vector3.up : Vector3.right;
        Vector3 e1 = Vector3.Cross(ax, hlp).normalized;
        Vector3 e2 = Vector3.Cross(ax, e1);
        c0.a = sw0; c1.a = sw1;
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2f / sides, t1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = e1 * Mathf.Cos(t0) + e2 * Mathf.Sin(t0);
            Vector3 d1 = e1 * Mathf.Cos(t1) + e2 * Mathf.Sin(t1);
            Vector3 pa0 = a + d0 * r0, pa1 = a + d1 * r0, pb0 = b + d0 * r1, pb1 = b + d1 * r1;
            Vector3 hint = d0 + d1;
            Tri3(pa0, pa1, pb1, c0, c0, c1, e, hint);
            Tri3(pa0, pb1, pb0, c0, c1, c1, e, hint);
            if (capA) Tri3(a, pa1, pa0, c0, c0, c0, e, -ax);
            if (capB) Tri3(b, pb0, pb1, c1, c1, c1, e, ax);
        }
    }

    public void Tube(Vector3 a, Vector3 b, float r, int sides, Color c)
    {
        Tube(a, b, r, r, sides, c, c, 0f, true, true, 0f, 0f);
    }

    // ---- low-poly сфера (икосаэдр)
    static readonly Dictionary<int, List<Vector3[]>> icoCache = new Dictionary<int, List<Vector3[]>>();

    static List<Vector3[]> IcoFaces(int sub)
    {
        List<Vector3[]> cached;
        if (icoCache.TryGetValue(sub, out cached)) return cached;

        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        var faces = new List<Vector3[]>();
        for (int i = 0; i < f.Length; i += 3) faces.Add(new[] { v[f[i]], v[f[i + 1]], v[f[i + 2]] });
        for (int s = 0; s < sub; s++)
        {
            var nf = new List<Vector3[]>();
            foreach (var q in faces)
            {
                Vector3 a = q[0], b = q[1], c = q[2];
                Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                nf.Add(new[] { a, ab, ca });
                nf.Add(new[] { b, bc, ab });
                nf.Add(new[] { c, ca, bc });
                nf.Add(new[] { ab, bc, ca });
            }
            faces = nf;
        }
        icoCache[sub] = faces;
        return faces;
    }

    static float Hsh3(Vector3 u, int seed)
    {
        float h = Mathf.Sin(Vector3.Dot(u, new Vector3(12.9898f, 78.233f, 37.719f)) + seed * 0.731f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // hard = верх/низ по центру грани без плавного перехода (поплавок)
    public void Ico(Vector3 center, Vector3 radii, Quaternion rot, Color top, Color bottom, int sub, float noise, float e, int seed, bool hard)
    {
        foreach (var q in IcoFaces(sub))
        {
            var p = new Vector3[3];
            var col = new Color[3];
            float fv = 0.94f + 0.12f * Hsh3(q[0] + q[1] + q[2], seed + 13);
            float faceY = (q[0].y + q[1].y + q[2].y) / 3f;
            for (int k = 0; k < 3; k++)
            {
                Vector3 u = q[k];
                float dsp = 1f + noise * (Hsh3(u, seed) - 0.5f) * 2f;
                p[k] = center + rot * Vector3.Scale(u * dsp, radii);
                float tt = hard ? (faceY >= 0f ? 1f : 0f) : u.y * 0.5f + 0.5f;
                col[k] = Mul(Color.Lerp(bottom, top, tt), fv);
            }
            Vector3 cen = (p[0] + p[1] + p[2]) / 3f;
            Tri3(p[0], p[1], p[2], col[0], col[1], col[2], e, cen - center);
        }
    }

    public void Ico(Vector3 center, float r, Color c, int sub, float noise, float e, int seed)
    {
        Ico(center, new Vector3(r, r, r), Quaternion.identity, c, Mul(c, 0.8f), sub, noise, e, seed, false);
    }

    // изогнутое лезвие травы/камыша: двустороннее, вес покачивания растёт к кончику
    public void Blade(Vector3 b, Vector3 bendDir, float h, float w, float bend, Color c0, Color c1, int segs, float swayTop)
    {
        Vector3 side = Vector3.Cross(Vector3.up, bendDir).normalized;
        var L = new Vector3[segs + 1];
        var R = new Vector3[segs + 1];
        var cc = new Color[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            Vector3 c = b + Vector3.up * (h * t) + bendDir * (bend * t * t);
            float hw = (i == segs) ? 0f : w * 0.5f * (1f - 0.8f * t);
            L[i] = c - side * hw;
            R[i] = c + side * hw;
            Color col = Color.Lerp(c0, c1, t);
            col.a = swayTop * t * Mathf.Sqrt(t);
            cc[i] = col;
        }
        for (int i = 0; i < segs; i++)
        {
            if (i == segs - 1)
            {
                Tri2(L[i], R[i], L[i + 1], cc[i], cc[i], cc[i + 1], 0f);
            }
            else
            {
                Tri2(L[i], R[i], R[i + 1], cc[i], cc[i], cc[i + 1], 0f);
                Tri2(L[i], R[i + 1], L[i + 1], cc[i], cc[i + 1], cc[i + 1], 0f);
            }
        }
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name };
        if (P.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(P);
        m.SetColors(C);
        m.SetUVs(0, U);
        m.SetTriangles(T, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}

public class PondBuilder : EditorWindow
{
    const string AssetDir = "Assets/_Game/Pond";
    const string PrefKey = "BuilderSim.PondBuilder.v1";
    const string RootName = "Pond";
    const int N = 96;                                   // сегментов контура берега
    const float TwoPi = Mathf.PI * 2f;

    [System.Serializable]
    public class Settings
    {
        public int seed = 2025;
        public float radiusX = 5.5f, radiusZ = 4.3f, irregular = 0.16f;
        public float lipHeight = 0.55f, lipWidth = 2.4f;

        public bool dock = true;
        public float dockAngle = 205f, dockLength = 3.8f, dockWidth = 1.5f;

        public int fish = 9, pads = 9, reeds = 11, rocks = 13, bushes = 8, flowers = 45, grass = 55, mushrooms = 4, seaweed = 12;

        public bool lights = true, fireflies = true, halos = true;
        public float lightIntensity = 1f;
        public float glow = 0.65f;

        public Color shallow = new Color(0.52f, 0.93f, 0.92f, 0.28f);
        public Color deep = new Color(0.14f, 0.56f, 0.90f, 0.58f);
        public Color sand = new Color(0.96f, 0.86f, 0.62f);
        public Color grassTop = new Color(0.62f, 0.86f, 0.28f);
        public Color grassDark = new Color(0.34f, 0.66f, 0.26f);
        public Color woodLight = new Color(0.74f, 0.52f, 0.30f);
        public Color woodDark = new Color(0.46f, 0.30f, 0.17f);
        public Color rock = new Color(0.72f, 0.67f, 0.82f);
        public Color lantern = new Color(1f, 0.80f, 0.42f);

        public Vector3 position;
        public float yaw;
    }

    Settings S = new Settings();
    Vector2 scroll;

    // ---- форма и высоты
    float rx, rz, H, W, F, Ys, lipW, sW;
    readonly float[] amp = new float[3];
    readonly float[] phs = new float[3];
    static readonly int[] Harm = { 2, 3, 5 };
    readonly float[] shoreR = new float[N];

    // ---- мостик
    bool hasDock;
    Vector3 dockStart, dockIn, dockSide, outDir, bobberPos, rodTipPos;
    float dockL, dockW, DY;

    // ---- состояние сборки
    System.Random rng;
    Transform root;
    PondMB decor;
    Material matDecor, matFish, matWater, matGlow;
    Mesh[] fishMeshes;
    readonly List<Vector3> lanternPos = new List<Vector3>();

    [MenuItem("Tools/BuilderSim/Природа/Pond Builder", false, 400)]
    static void Open()
    {
        var w = GetWindow<PondBuilder>(false, "Пруд");
        w.minSize = new Vector2(390, 640);
        w.Show();
    }

    // ========================================================================
    //  Сохранение настроек
    // ========================================================================
    void OnEnable()
    {
        if (EditorPrefs.HasKey(PrefKey)) JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(PrefKey), S);
        if (S.position == Vector3.zero && SceneView.lastActiveSceneView != null) S.position = SceneView.lastActiveSceneView.pivot;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Save();
    }

    void Save() { EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(S)); }

    // ========================================================================
    //  UI окна
    // ========================================================================
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Пруд с рыбалкой", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ручку в окне сцены можно двигать и вращать. Нажмите «Построить»: пруд встанет на землю (коллайдер «Ground»). " +
                                "Шейдеры Project/Pond/* должны лежать в проекте.", MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Форма", EditorStyles.boldLabel);
        S.seed = EditorGUILayout.IntField("Seed", S.seed);
        S.radiusX = EditorGUILayout.Slider("Радиус по X (м)", S.radiusX, 3f, 12f);
        S.radiusZ = EditorGUILayout.Slider("Радиус по Z (м)", S.radiusZ, 3f, 12f);
        S.irregular = EditorGUILayout.Slider("Кривизна берега", S.irregular, 0f, 0.3f);
        S.lipHeight = EditorGUILayout.Slider("Высота бортика (м)", S.lipHeight, 0.35f, 1.2f);
        S.lipWidth = EditorGUILayout.Slider("Ширина склона (м)", S.lipWidth, 1f, 4f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Мостик для рыбалки", EditorStyles.boldLabel);
        S.dock = EditorGUILayout.Toggle("Мостик с удочкой", S.dock);
        using (new EditorGUI.DisabledScope(!S.dock))
        {
            S.dockAngle = EditorGUILayout.Slider("Направление (°)", S.dockAngle, 0f, 360f);
            S.dockLength = EditorGUILayout.Slider("Длина (м)", S.dockLength, 2.5f, 8f);
            S.dockWidth = EditorGUILayout.Slider("Ширина (м)", S.dockWidth, 1f, 2.6f);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Жизнь и декор", EditorStyles.boldLabel);
        S.fish = EditorGUILayout.IntSlider("Рыбок", S.fish, 0, 24);
        S.pads = EditorGUILayout.IntSlider("Кувшинок", S.pads, 0, 24);
        S.seaweed = EditorGUILayout.IntSlider("Водорослей на дне", S.seaweed, 0, 30);
        S.reeds = EditorGUILayout.IntSlider("Камышей (кустов)", S.reeds, 0, 30);
        S.rocks = EditorGUILayout.IntSlider("Камней", S.rocks, 0, 40);
        S.bushes = EditorGUILayout.IntSlider("Кустов вокруг", S.bushes, 0, 24);
        S.flowers = EditorGUILayout.IntSlider("Цветов на траве", S.flowers, 0, 150);
        S.grass = EditorGUILayout.IntSlider("Пучков травы", S.grass, 0, 200);
        S.mushrooms = EditorGUILayout.IntSlider("Грибов", S.mushrooms, 0, 16);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Свет", EditorStyles.boldLabel);
        S.lights = EditorGUILayout.Toggle("Фонари и подсветка воды", S.lights);
        S.halos = EditorGUILayout.Toggle("Ореолы вокруг фонарей", S.halos);
        S.fireflies = EditorGUILayout.Toggle("Светлячки", S.fireflies);
        S.lightIntensity = EditorGUILayout.Slider("Сила света", S.lightIntensity, 0f, 3f);
        S.glow = EditorGUILayout.Slider("Свечение (0 день ... 1 ночь)", S.glow, 0f, 1f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Цвета", EditorStyles.boldLabel);
        S.shallow = EditorGUILayout.ColorField("Вода: мелко (a = прозрачность)", S.shallow);
        S.deep = EditorGUILayout.ColorField("Вода: глубоко (a = прозрачность)", S.deep);
        S.sand = EditorGUILayout.ColorField("Песок у воды", S.sand);
        S.grassTop = EditorGUILayout.ColorField("Трава (светлая)", S.grassTop);
        S.grassDark = EditorGUILayout.ColorField("Трава (у подножия)", S.grassDark);
        S.woodLight = EditorGUILayout.ColorField("Светлое дерево", S.woodLight);
        S.woodDark = EditorGUILayout.ColorField("Тёмное дерево", S.woodDark);
        S.rock = EditorGUILayout.ColorField("Камни", S.rock);
        S.lantern = EditorGUILayout.ColorField("Огонёк фонарей", S.lantern);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Положение", EditorStyles.boldLabel);
        S.position = EditorGUILayout.Vector3Field("Позиция", S.position);
        S.yaw = EditorGUILayout.Slider("Поворот (°)", S.yaw, 0f, 360f);

        if (EditorGUI.EndChangeCheck())
        {
            Save();
            MoveExisting();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        if (GUILayout.Button("Построить / перестроить пруд", GUILayout.Height(32))) Build();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("В центр вида сцены"))
        {
            if (SceneView.lastActiveSceneView != null)
            {
                S.position = SceneView.lastActiveSceneView.pivot;
                Save();
                MoveExisting();
            }
        }
        if (GUILayout.Button("Удалить пруд")) Delete();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // ========================================================================
    //  Ручки в сцене
    // ========================================================================
    void OnSceneGUI(SceneView sv)
    {
        ShapeInit();
        var rot = Quaternion.Euler(0, S.yaw, 0);

        var pts = new Vector3[N + 1];
        for (int k = 0; k <= N; k++)
        {
            float phi = (k % N) * TwoPi / N;
            pts[k] = S.position + rot * (Dir(phi) * shoreR[k % N]) + Vector3.up * 0.05f;
        }
        Handles.color = new Color(0.45f, 0.95f, 1f, 0.95f);
        Handles.DrawAAPolyLine(4f, pts);
        Handles.Label(S.position + Vector3.up * 1.5f, "Пруд");

        EditorGUI.BeginChangeCheck();
        Vector3 np = Handles.PositionHandle(S.position, Quaternion.identity);
        float size = HandleUtility.GetHandleSize(S.position);
        Quaternion nr = Handles.Disc(Quaternion.Euler(0, S.yaw, 0), S.position, Vector3.up, size * 1.2f, false, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            S.position = np;
            S.yaw = nr.eulerAngles.y;
            Save();
            MoveExisting();
            Repaint();
        }
    }

    static GameObject FindPond()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == RootName) return go;
        return null;
    }

    void MoveExisting()
    {
        var p = FindPond();
        if (p == null) return;
        ShapeInit();
        float y;
        if (!AverageGround(out y)) y = p.transform.position.y;
        Undo.RecordObject(p.transform, "Переместить пруд");
        p.transform.SetPositionAndRotation(new Vector3(S.position.x, y, S.position.z), Quaternion.Euler(0, S.yaw, 0));
    }

    void Delete()
    {
        var p = FindPond();
        if (p != null) Undo.DestroyObjectImmediate(p);
    }

    // ========================================================================
    //  Земля (понимает и «Ground», и чанки Ground/G_x_z); если не нашли, берёт любой не-триггерный коллайдер
    // ========================================================================
    static bool IsGround(Collider c)
    {
        var t = c.transform;
        if (t.name == "Ground") return true;
        return t.parent != null && t.parent.name == "Ground";
    }

    static bool GroundY(Vector3 wp, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(wp.x, wp.y + 500f, wp.z), Vector3.down, 1500f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, bestAny = float.MaxValue, yAny = 0f;
        bool ok = false, any = false;
        foreach (var h in hits)
        {
            var pond = FindPond();
            if (pond != null && h.collider.transform.IsChildOf(pond.transform)) continue;
            if (h.distance < bestAny) { bestAny = h.distance; yAny = h.point.y; any = true; }
            if (!IsGround(h.collider)) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
        }
        if (!ok && any) { y = yAny; ok = true; }
        return ok;
    }

    bool AverageGround(out float y)
    {
        var q = Quaternion.Euler(0, S.yaw, 0);
        float sum = 0f; int cnt = 0;
        for (int i = -1; i < 8; i++)
        {
            Vector3 o = i < 0 ? Vector3.zero : Dir(i * TwoPi / 8f) * (Mathf.Min(rx, rz) * 0.9f);
            float gy;
            if (GroundY(S.position + q * o, out gy)) { sum += gy; cnt++; }
        }
        y = cnt > 0 ? sum / cnt : S.position.y;
        return cnt > 0;
    }

    // ========================================================================
    //  Форма пруда и профиль берега
    // ========================================================================
    static Vector3 Dir(float phi) { return new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi)); }

    float ShapeF(float phi)
    {
        float f = 1f;
        for (int i = 0; i < 3; i++) f += amp[i] * Mathf.Sin(Harm[i] * phi + phs[i]);
        return f;
    }

    float Shore(float phi)
    {
        float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
        float e = rx * rz / Mathf.Sqrt(rz * rz * c * c + rx * rx * s * s);
        return e * ShapeF(phi);
    }

    // профиль: s = доля радиуса от центра (0..1) внутри, d = метры от берега наружу
    float BedY(float s)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, s));
        return Mathf.Lerp(F, Ys, k);
    }

    float OutY(float d)
    {
        const float peakD = 0.45f;
        if (d <= peakD) return Mathf.Lerp(Ys, H, Mathf.SmoothStep(0f, 1f, d / peakD));
        float t = Mathf.Clamp01((d - peakD) / lipW);
        return Mathf.Lerp(H, -0.30f, Mathf.SmoothStep(0f, 1f, t));
    }

    void ShapeInit()
    {
        var r = new System.Random(S.seed * 7919 + 17);
        rx = S.radiusX; rz = S.radiusZ;
        amp[0] = S.irregular * 0.9f; amp[1] = S.irregular * 0.55f; amp[2] = S.irregular * 0.3f;
        for (int i = 0; i < 3; i++) phs[i] = (float)r.NextDouble() * TwoPi;
        H = S.lipHeight; W = H - 0.14f; F = 0.02f; Ys = W + 0.04f; lipW = S.lipWidth;
        for (int k = 0; k < N; k++) shoreR[k] = Shore(k * TwoPi / N);

        float lo = 0.5f, hi = 1f;                                   // где дно пересекает уровень воды
        for (int i = 0; i < 30; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (BedY(mid) < W) lo = mid; else hi = mid;
        }
        sW = Mathf.Min(lo, 0.96f);
    }

    // точка на берегу: d = метры от кромки (d < 0 в воду)
    Vector3 PBank(float phi, float d)
    {
        float R = Shore(phi);
        float dist = R + d;
        float y = d >= 0f ? OutY(d) : BedY(Mathf.Clamp01(dist / R));
        return Dir(phi) * dist + Vector3.up * y;
    }

    bool NearDock(Vector3 p, float margin)
    {
        if (!hasDock) return false;
        Vector3 a3 = dockStart + outDir * 3f;
        Vector3 b3 = dockStart + dockIn * (dockL + 1.2f);
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), q = new Vector2(p.x, p.z);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
        return (q - (a + ab * t)).magnitude < margin + dockW * 0.5f;
    }

    // ========================================================================
    //  Утилиты
    // ========================================================================
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    static float Nz(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = H2(ix, iy), b = H2(ix + 1, iy), c = H2(ix, iy + 1), d = H2(ix + 1, iy + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float H2(float x, float y)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // цвета для вершин: в Linear-проекте переводим, как это делает материал
    static Color Vc(Color c, float sway)
    {
        if (PlayerSettings.colorSpace == ColorSpace.Linear) c = c.linear;
        c.a = sway;
        return c;
    }

    static Color Vc(Color c) { return Vc(c, 0f); }

    static Color Mul(Color c, float k) { return PondMB.Mul(c, k); }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    Mesh SaveMesh(Mesh m, string name)
    {
        string path = AssetDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(m, existing);
            DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    Material GetMat(string name, Shader sh, System.Action<Material> setup)
    {
        string path = AssetDir + "/Pond_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh) { name = "Pond_" + name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        if (setup != null) setup(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    GameObject MeshGO(Transform parent, string name, Mesh mesh, Material mat, bool castShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    // ========================================================================
    //  СБОРКА
    // ========================================================================
    void Build()
    {
        var shToon = Shader.Find("Project/Pond/Toon");
        var shWater = Shader.Find("Project/Pond/Water");
        var shGlow = Shader.Find("Project/Pond/Glow");
        if (shToon == null || shWater == null || shGlow == null)
        {
            EditorUtility.DisplayDialog("Пруд", "Не найдены шейдеры Project/Pond/Toon, Water, Glow.\nПоложите папку Shaders/Pond в проект и дождитесь импорта.", "OK");
            return;
        }

        EnsureFolder(AssetDir);
        var old = FindPond();
        if (old != null) { Undo.DestroyObjectImmediate(old); Physics.SyncTransforms(); }

        rng = new System.Random(S.seed);
        ShapeInit();
        float gy;
        if (!AverageGround(out gy))
        {
            gy = S.position.y;
            Debug.LogWarning("PondBuilder: коллайдер земли под прудом не найден, высота взята из позиции ручки.");
        }

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Построить пруд");
        root = go.transform;
        root.SetPositionAndRotation(new Vector3(S.position.x, gy, S.position.z), Quaternion.Euler(0, S.yaw, 0));

        MakeMaterials(shToon, shWater, shGlow);
        fishMeshes = new Mesh[5];
        for (int k = 0; k < 5; k++) fishMeshes[k] = SaveMesh(MakeFish(k), "Pond_Fish" + k);

        var life = go.AddComponent<PondLife>();
        life.waterY = W;
        life.shoreR = (float[])shoreR.Clone();
        life.glow = S.glow;

        decor = new PondMB();
        lanternPos.Clear();
        DockInit();

        BuildBank();
        BuildSeabed();
        if (hasDock) BuildDock();
        BuildRocks();
        BuildReeds();
        BuildBushes();
        BuildMeadow();
        BuildMushrooms();

        var decorGO = MeshGO(root, "Decor", SaveMesh(decor.ToMesh("Pond_Decor"), "Pond_Decor"), matDecor, true);
        life.decorRenderer = decorGO.GetComponent<Renderer>();

        MeshGO(root, "Water", SaveMesh(MakeWater(), "Pond_Water"), matWater, false);

        BuildFish(life);
        BuildPads(life);
        if (hasDock) BuildFishing(life);
        BuildLights(life);
        BuildFx(life);

        Physics.SyncTransforms();
        Selection.activeGameObject = go;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    void MakeMaterials(Shader toon, Shader water, Shader glow)
    {
        matDecor = GetMat("Decor", toon, m => { m.SetFloat("_Glow", S.glow); });
        matFish = GetMat("Fish", toon, m =>
        {
            m.SetFloat("_WiggleAmp", 0.13f);
            m.SetFloat("_SwayAmp", 0f);
            m.SetFloat("_EmisDay", 1f);
            m.SetFloat("_Ambient", 0.6f);
        });
        matWater = GetMat("Water", water, m =>
        {
            m.SetColor("_ShallowColor", S.shallow);
            m.SetColor("_DeepColor", S.deep);
        });
        matGlow = GetMat("Glow", glow, m =>
        {
            Color c = S.lantern; c.a = 0.55f;
            m.SetColor("_Tint", c);
        });
    }

    // ========================================================================
    //  Берег и дно (одна гладкая сетка с общими вершинами)
    // ========================================================================
    Color BankColor(bool inside, float s, float d, float phi)
    {
        float n = 0.94f + 0.12f * Nz(phi * 14f, (inside ? s : d) * 2.5f);
        if (inside)
        {
            Color mud = new Color(0.26f, 0.52f, 0.62f), teal = new Color(0.42f, 0.74f, 0.74f);
            Color c = Color.Lerp(mud, teal, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.6f, s)));
            c = Color.Lerp(c, S.sand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.9f, s)));
            return Vc(Mul(c, n));
        }
        float edge = 0.10f + 0.14f * Nz(phi * 6f, 1.7f);               // волнистая граница песок/трава
        float g = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge, edge + 0.12f, d));
        Color gr = Color.Lerp(S.grassTop, S.grassDark, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.45f + lipW, d)) * 0.85f);
        return Vc(Mul(Color.Lerp(S.sand, gr, g), n));
    }

    void BuildBank()
    {
        float[] ringS = { 0f, 0.3f, 0.5f, 0.62f, 0.72f, 0.8f, 0.87f, 0.93f, 0.97f, 1f };
        float[] ringD = { 0.12f, 0.25f, 0.45f, 0.45f + lipW * 0.18f, 0.45f + lipW * 0.36f, 0.45f + lipW * 0.55f, 0.45f + lipW * 0.75f, 0.45f + lipW };
        int rings = ringS.Length + ringD.Length;
        var idx = new int[rings, N];

        int center = decor.V(new Vector3(0, BedY(0f), 0), BankColor(true, 0f, 0f, 0f), 0f);
        for (int j = 0; j < rings; j++)
            for (int k = 0; k < N; k++)
            {
                float phi = k * TwoPi / N;
                float R = shoreR[k];
                Vector3 dir = Dir(phi);
                if (j == 0) { idx[j, k] = center; continue; }
                Vector3 pos; Color col;
                if (j < ringS.Length)
                {
                    float s = ringS[j];
                    pos = dir * (R * s) + Vector3.up * BedY(s);
                    col = BankColor(true, s, 0f, phi);
                }
                else
                {
                    float d = ringD[j - ringS.Length];
                    pos = dir * (R + d) + Vector3.up * OutY(d);
                    col = BankColor(false, 1f, d, phi);
                }
                idx[j, k] = decor.V(pos, col, 0f);
            }

        for (int j = 0; j < rings - 1; j++)
            for (int k = 0; k < N; k++)
            {
                int k2 = (k + 1) % N;
                int a = idx[j, k], b = idx[j + 1, k], c = idx[j + 1, k2], d = idx[j, k2];
                decor.I(a, c, b);
                if (a != d) decor.I(a, d, c);
            }
    }

    // водоросли и камушки на дне: видны сквозь воду
    void BuildSeabed()
    {
        for (int i = 0; i < S.seaweed; i++)
        {
            float phi = Rf(0f, TwoPi), s = Rf(0.18f, 0.74f);
            Vector3 p = Dir(phi) * (Shore(phi) * s);
            if (NearDock(p, 0.6f)) continue;
            p.y = BedY(s) - 0.01f;
            int blades = 3 + rng.Next(3);
            for (int b = 0; b < blades; b++)
            {
                float a = Rf(0f, TwoPi);
                Vector3 bd = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 bp = p + new Vector3(Rf(-0.1f, 0.1f), 0f, Rf(-0.1f, 0.1f));
                decor.Blade(bp, bd, Rf(0.22f, 0.42f), 0.07f, Rf(0.04f, 0.14f), Vc(new Color(0.18f, 0.55f, 0.48f)), Vc(new Color(0.45f, 0.86f, 0.55f)), 3, 1.4f);
            }
        }
        for (int i = 0; i < 12; i++)
        {
            float phi = Rf(0f, TwoPi), s = Rf(0.1f, 0.78f);
            Vector3 p = Dir(phi) * (Shore(phi) * s);
            p.y = BedY(s) + 0.01f;
            float r = Rf(0.05f, 0.12f);
            Color c = Color.Lerp(new Color(0.78f, 0.78f, 0.86f), new Color(0.95f, 0.85f, 0.7f), (float)rng.NextDouble());
            decor.Ico(p, new Vector3(r, r * 0.55f, r), Quaternion.Euler(0f, Rf(0f, 360f), 0f), Vc(Mul(c, 1.05f)), Vc(Mul(c, 0.75f)), 0, 0.15f, 0f, rng.Next(999), false);
        }
    }

    // ========================================================================
    //  Вода
    // ========================================================================
    Mesh MakeWater()
    {
        float[] ringC = { 0f, 0.3f, 0.55f, 0.75f, 0.88f, 0.96f, 1f, 1.04f };
        var mb = new PondMB();
        var idx = new int[ringC.Length, N];
        int center = mb.V(new Vector3(0, W, 0), new Color(0, 0, 0, 1), 0f);
        for (int j = 0; j < ringC.Length; j++)
            for (int k = 0; k < N; k++)
            {
                if (j == 0) { idx[j, k] = center; continue; }
                float phi = k * TwoPi / N;
                float s = ringC[j] * sW;
                idx[j, k] = mb.V(Dir(phi) * (shoreR[k] * s) + Vector3.up * W, new Color(ringC[j], 0, 0, 1), 0f);
            }
        for (int j = 0; j < ringC.Length - 1; j++)
            for (int k = 0; k < N; k++)
            {
                int k2 = (k + 1) % N;
                int a = idx[j, k], b = idx[j + 1, k], c = idx[j + 1, k2], d = idx[j, k2];
                mb.I(a, c, b);
                if (a != d) mb.I(a, d, c);
            }
        return mb.ToMesh("Pond_Water");
    }

    // ========================================================================
    //  Мостик
    // ========================================================================
    void DockInit()
    {
        hasDock = S.dock;
        if (!hasDock) return;
        float phi = S.dockAngle * Mathf.Deg2Rad;
        float R = Shore(phi);
        outDir = Dir(phi);
        dockIn = -outDir;
        dockSide = new Vector3(-outDir.z, 0f, outDir.x);
        dockStart = outDir * (R + 0.30f);
        dockL = Mathf.Min(S.dockLength, R * 1.1f);
        dockW = S.dockWidth;
        DY = H + 0.14f;
    }

    float HalfW(float t) { return t > dockL - 1.5f ? dockW * 0.5f + 0.35f : dockW * 0.5f; }

    Vector3 DockPt(float t, float side, float y)
    {
        Vector3 p = dockStart + dockIn * t + dockSide * side;
        p.y = y;
        return p;
    }

    void Rope(Vector3 a, Vector3 b, float sag, Color col)
    {
        const int segs = 7;
        Vector3 prev = a;
        for (int i = 1; i <= segs; i++)
        {
            float t = i / (float)segs;
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            decor.Tube(prev, p, 0.022f, 0.022f, 5, col, col, 0f, false, false, 0f, 0f);
            prev = p;
        }
    }

    // фонарь: стекло светится, сверху крышка. Возвращает центр стекла
    Vector3 Lantern(Vector3 top)
    {
        Color wood = Vc(S.woodDark);
        Color glass = Vc(S.lantern);
        Vector3 c = top + Vector3.down * 0.45f;
        decor.Tube(top, c + Vector3.up * 0.30f, 0.012f, 0.012f, 4, wood, wood, 0f, false, false, 0f, 0f);
        decor.Box(c, new Vector3(0.20f, 0.28f, 0.20f), Quaternion.identity, glass, 1f);
        decor.Box(c + Vector3.down * 0.15f, new Vector3(0.24f, 0.04f, 0.24f), Quaternion.identity, wood, 0f);
        decor.Tube(c + Vector3.up * 0.14f, c + Vector3.up * 0.30f, 0.17f, 0.02f, 4, wood, wood, 0f, true, false, 0f, 0f);
        decor.Box(c + Vector3.up * 0.31f, new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity, wood, 0f);
        lanternPos.Add(c);
        return c;
    }

    void BuildDock()
    {
        Color wL = S.woodLight, wD = S.woodDark;
        var rotD = Quaternion.LookRotation(dockIn, Vector3.up);
        Color rope = Vc(new Color(0.84f, 0.72f, 0.46f));

        // ступени-плиты со склона на гребень
        Color st = S.rock;
        for (int i = 0; i < 3; i++)
        {
            float d = 0.75f + i * 0.65f;
            Vector3 p = dockStart + outDir * (d - 0.3f);
            float y = OutY(d) + 0.04f;
            decor.Box(new Vector3(p.x, y, p.z), new Vector3(dockW * 0.85f, 0.14f, 0.62f), rotD * Quaternion.Euler(0f, Rf(-3f, 3f), 0f), Vc(Mul(st, 1.0f - i * 0.05f)), 0f);
        }

        // доски настила
        int n = Mathf.FloorToInt(dockL / 0.25f);
        for (int i = 0; i < n; i++)
        {
            float t = 0.12f + i * 0.25f;
            float len = (t > dockL - 1.5f ? dockW + 0.7f : dockW) + Rf(-0.04f, 0.04f);
            Color c = Color.Lerp(wL, wD, Mathf.Clamp01(0.15f + 0.7f * (float)rng.NextDouble()));
            Vector3 pc = DockPt(t, 0f, DY + Rf(-0.006f, 0.006f));
            decor.Box(pc, new Vector3(len, 0.07f, 0.21f), rotD * Quaternion.Euler(0f, Rf(-1.5f, 1.5f), 0f), Vc(c), 0f);
        }

        // балки под настилом
        Color beam = Vc(Mul(wD, 0.85f));
        float beamL = dockL;
        decor.Box(DockPt(beamL * 0.5f + 0.06f, dockW * 0.5f - 0.12f, DY - 0.10f), new Vector3(0.10f, 0.12f, beamL), rotD, beam, 0f);
        decor.Box(DockPt(beamL * 0.5f + 0.06f, -(dockW * 0.5f - 0.12f), DY - 0.10f), new Vector3(0.10f, 0.12f, beamL), rotD, beam, 0f);
        decor.Box(DockPt(dockL - 0.75f, 0f, DY - 0.10f), new Vector3(dockW + 0.7f, 0.10f, 0.12f), rotD, beam, 0f);

        // столбы и перила
        var posts = new List<float>();
        for (float t = 0.35f; t < dockL - 1.7f; t += 1.5f) posts.Add(t);
        posts.Add(dockL - 1.35f);
        posts.Add(dockL - 0.12f);
        Color postC = Vc(wD);
        Color capC = Vc(Mul(wL, 1.05f));
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            Vector3 prevTop = Vector3.zero;
            for (int i = 0; i < posts.Count; i++)
            {
                float t = posts[i];
                bool endPost = i == posts.Count - 1;
                float off = (HalfW(t) + 0.02f) * sgn;
                float topY = DY + (endPost ? 1.75f : 0.80f);
                Vector3 b = DockPt(t, off, F - 0.05f);
                Vector3 tp = DockPt(t, off, topY);
                decor.Tube(b, tp, 0.085f, 0.075f, 7, postC, postC, 0f, true, true, 0f, 0f);
                decor.Ico(tp + Vector3.up * 0.02f, new Vector3(0.11f, 0.08f, 0.11f), Quaternion.identity, capC, Mul(capC, 0.8f), 0, 0.05f, 0f, rng.Next(999), false);

                Vector3 ropeTop = DockPt(t, off, DY + 0.62f);
                if (i > 0) Rope(prevTop, ropeTop, 0.10f, rope);
                prevTop = ropeTop;

                if (endPost)
                {
                    Vector3 armEnd = tp + dockSide * (-sgn * 0.42f);
                    decor.Tube(tp - Vector3.up * 0.06f, armEnd - Vector3.up * 0.06f, 0.03f, 0.025f, 5, postC, postC, 0f, true, true, 0f, 0f);
                    Lantern(armEnd - Vector3.up * 0.06f);
                }
            }
        }
        // поперечная верёвка на конце
        Rope(DockPt(dockL - 0.12f, -(HalfW(dockL) + 0.02f), DY + 0.62f), DockPt(dockL - 0.12f, HalfW(dockL) + 0.02f, DY + 0.62f), 0.14f, rope);

        // фонарный столб на берегу у входа
        {
            Vector3 b = dockStart + dockSide * (dockW * 0.5f + 1.0f) + outDir * 0.9f;
            b.y = OutY(Vector3.Distance(new Vector3(b.x, 0, b.z), Vector3.zero) - Shore(Mathf.Atan2(b.z, b.x))) - 0.05f;
            Vector3 tp = b + Vector3.up * 2.0f;
            decor.Tube(b, tp, 0.09f, 0.07f, 7, postC, postC, 0f, true, true, 0f, 0f);
            Vector3 armEnd = tp - dockSide * 0.45f;
            decor.Tube(tp - Vector3.up * 0.05f, armEnd - Vector3.up * 0.05f, 0.03f, 0.025f, 5, postC, postC, 0f, true, true, 0f, 0f);
            Lantern(armEnd - Vector3.up * 0.05f);
        }

        // удочка: подставка + бамбуковое удилище, вынесено над водой
        {
            float tR = dockL - 0.75f;
            float side = HalfW(tR) - 0.14f;
            Vector3 hb = DockPt(tR, side, DY);
            Vector3 ht = DockPt(tR, side, DY + 0.34f);
            decor.Tube(hb, ht, 0.04f, 0.035f, 6, postC, postC, 0f, true, true, 0f, 0f);
            Vector3 tip = ht + dockIn * 1.7f - dockSide * (side * 0.8f) + Vector3.up * 1.05f;
            Color bamboo = Vc(new Color(0.80f, 0.62f, 0.30f));
            decor.Tube(ht, tip, 0.028f, 0.009f, 5, bamboo, Vc(new Color(0.55f, 0.38f, 0.18f)), 0f, true, true, 0f, 0f);
            Vector3 reel = ht + dockIn * 0.28f + Vector3.up * 0.06f;
            decor.Tube(reel - dockSide * 0.04f, reel + dockSide * 0.04f, 0.055f, 0.055f, 8, Vc(new Color(0.35f, 0.38f, 0.45f)), Vc(new Color(0.55f, 0.6f, 0.68f)), 0f, true, true, 0f, 0f);
            rodTipPos = tip;
            bobberPos = new Vector3(tip.x, W + 0.02f, tip.z) + dockIn * 0.5f;
        }

        // ведёрко с рыбкой и ящик
        {
            Vector3 bp = DockPt(dockL - 1.15f, -(HalfW(dockL - 1.15f) - 0.28f), DY + 0.035f);
            Color wood = Vc(Mul(wL, 0.9f)), band = Vc(new Color(0.55f, 0.58f, 0.66f));
            decor.Tube(bp, bp + Vector3.up * 0.34f, 0.17f, 0.21f, 10, wood, wood, 0f, true, false, 0f, 0f);
            decor.Tube(bp + Vector3.up * 0.10f, bp + Vector3.up * 0.13f, 0.185f, 0.19f, 10, band, band, 0f, false, false, 0f, 0f);
            decor.Tube(bp + Vector3.up * 0.335f, bp + Vector3.up * 0.34f, 0.19f, 0.19f, 10, Vc(new Color(0.30f, 0.55f, 0.72f)), Vc(new Color(0.30f, 0.55f, 0.72f)), 0f, true, true, 0f, 0f);
            bucketFishPos = bp + Vector3.up * 0.32f;

            Vector3 cp = DockPt(0.9f, -(dockW * 0.5f - 0.36f), DY + 0.035f);
            decor.Box(cp + Vector3.up * 0.20f, new Vector3(0.5f, 0.4f, 0.5f), rotD * Quaternion.Euler(0f, 12f, 0f), Vc(Mul(wL, 0.95f)), 0f);
            decor.Box(cp + Vector3.up * 0.41f, new Vector3(0.54f, 0.05f, 0.54f), rotD * Quaternion.Euler(0f, 12f, 0f), Vc(wD), 0f);
        }
    }

    Vector3 bucketFishPos;

    // ========================================================================
    //  Камни, камыш, кусты, цветы, грибы
    // ========================================================================
    void BuildRocks()
    {
        Color[] pal = { S.rock, Mul(S.rock, 0.9f), Mul(S.rock, 1.1f), new Color(0.78f, 0.72f, 0.74f) };
        int placed = 0, tries = 0;
        while (placed < S.rocks && tries++ < 400)
        {
            float phi = Rf(0f, TwoPi);
            float d = Rf(-0.35f, 0.9f);
            Vector3 p = PBank(phi, d);
            if (NearDock(p, 1.2f)) continue;
            float size = Rf(0.28f, 0.8f) * (rng.NextDouble() < 0.2 ? 1.5f : 1f);
            Color c = pal[rng.Next(pal.Length)];
            Vector3 cen = p + Vector3.up * (size * 0.18f);
            decor.Ico(cen, new Vector3(size, size * 0.62f, size * Rf(0.8f, 1.1f)), Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f)),
                      Vc(Mul(c, 1.1f)), Vc(Mul(c, 0.72f)), 1, 0.16f, 0f, rng.Next(9999), false);
            if (rng.NextDouble() < 0.5)
            {
                float a = Rf(0f, TwoPi), sr = size * Rf(0.25f, 0.4f);
                Vector3 sp = PBank(phi + 0.02f * Mathf.Cos(a), d + Mathf.Sin(a) * size * 1.1f);
                decor.Ico(sp + Vector3.up * (sr * 0.2f), new Vector3(sr, sr * 0.65f, sr), Quaternion.Euler(0f, Rf(0f, 360f), 0f), Vc(Mul(c, 1.05f)), Vc(Mul(c, 0.75f)), 0, 0.15f, 0f, rng.Next(9999), false);
            }
            placed++;
        }
    }

    void ReedCluster(Vector3 c, float scale)
    {
        Color g0 = Vc(new Color(0.22f, 0.52f, 0.24f)), g1 = Vc(new Color(0.62f, 0.84f, 0.30f));
        int blades = 5 + rng.Next(4);
        for (int b = 0; b < blades; b++)
        {
            float a = Rf(0f, TwoPi), off = Rf(0f, 0.25f), a2 = Rf(0f, TwoPi);
            Vector3 bp = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * off;
            decor.Blade(bp, new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)), Rf(0.9f, 1.5f) * scale, 0.075f * scale, Rf(0.05f, 0.3f), g0, g1, 4, 1f);
        }
        int cats = 1 + rng.Next(2);
        Color brown = Vc(new Color(0.46f, 0.28f, 0.15f));
        for (int k = 0; k < cats; k++)
        {
            float a = Rf(0f, TwoPi);
            Vector3 bp = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Rf(0.05f, 0.22f);
            float h = Rf(1.2f, 1.9f) * scale;
            Vector3 lean = new Vector3(Rf(-0.12f, 0.12f), 0f, Rf(-0.12f, 0.12f));
            Vector3 top = bp + Vector3.up * h + lean;
            decor.Tube(bp, top, 0.018f, 0.012f, 4, g0, g1, 0f, false, false, 0f, 0.9f);
            decor.Tube(top - Vector3.up * 0.30f, top + Vector3.up * 0.0f, 0.052f, 0.052f, 6, brown, brown, 0f, true, true, 0.9f, 0.95f);
        }
    }

    void BuildReeds()
    {
        int placed = 0, tries = 0;
        while (placed < S.reeds && tries++ < 300)
        {
            float phi = Rf(0f, TwoPi);
            Vector3 p = PBank(phi, Rf(-0.45f, 0.25f));
            if (NearDock(p, 1.5f)) continue;
            ReedCluster(p + Vector3.down * 0.02f, Rf(0.8f, 1.2f));
            placed++;
        }
    }

    void BuildBushes()
    {
        Color[] pal = { new Color(0.55f, 0.80f, 0.30f), new Color(0.40f, 0.72f, 0.35f), new Color(0.30f, 0.62f, 0.34f) };
        Color[] fl = { new Color(1f, 0.98f, 0.9f), new Color(1f, 0.6f, 0.75f), new Color(1f, 0.86f, 0.3f) };
        int placed = 0, tries = 0;
        while (placed < S.bushes && tries++ < 300)
        {
            float phi = Rf(0f, TwoPi);
            float d = Rf(1.4f, 2.8f);
            Vector3 p = PBank(phi, d);
            if (NearDock(p, 2.2f)) continue;
            Color c = pal[rng.Next(pal.Length)];
            int blobs = 2 + rng.Next(2);
            for (int b = 0; b < blobs; b++)
            {
                float r = Rf(0.55f, 1.0f);
                Vector3 bc = p + new Vector3(Rf(-0.7f, 0.7f), 0f, Rf(-0.7f, 0.7f)) + Vector3.up * (r * 0.45f);
                decor.Ico(bc, new Vector3(r, r * 0.85f, r), Quaternion.Euler(0f, Rf(0f, 360f), 0f), Vc(Mul(c, 1.12f)), Vc(Mul(c, 0.68f)), 1, 0.10f, 0f, rng.Next(9999), false);
                if (rng.NextDouble() < 0.6)
                    for (int k = 0; k < 5; k++)
                    {
                        Vector3 u = new Vector3(Rf(-1f, 1f), Rf(0.15f, 1f), Rf(-1f, 1f)).normalized;
                        decor.Ico(bc + Vector3.Scale(u, new Vector3(r, r * 0.85f, r)) * 0.98f, 0.07f, Vc(fl[rng.Next(fl.Length)]), 0, 0f, 0.12f, rng.Next(999));
                    }
            }
            placed++;
        }
    }

    void BuildMeadow()
    {
        Color[] bloom =
        {
            new Color(1f, 0.98f, 0.9f), new Color(1f, 0.85f, 0.25f), new Color(1f, 0.55f, 0.7f), new Color(0.75f, 0.6f, 0.95f)
        };
        Color stem0 = Vc(new Color(0.30f, 0.60f, 0.26f)), stem1 = Vc(new Color(0.50f, 0.78f, 0.30f));
        int placed = 0, tries = 0;
        while (placed < S.flowers && tries++ < 800)
        {
            float phi = Rf(0f, TwoPi);
            float d = Rf(0.4f, 2.4f);
            Vector3 p = PBank(phi, d);
            if (NearDock(p, 0.9f)) continue;
            float h = Rf(0.22f, 0.45f);
            Vector3 top = p + Vector3.up * h + new Vector3(Rf(-0.05f, 0.05f), 0f, Rf(-0.05f, 0.05f));
            decor.Tube(p - Vector3.up * 0.02f, top, 0.012f, 0.009f, 4, stem0, stem1, 0f, false, false, 0f, 0.8f);
            float br = Rf(0.06f, 0.095f);
            decor.Ico(top, br, Vc(bloom[rng.Next(bloom.Length)], 0.8f), 0, 0.05f, 0.14f, rng.Next(999));
            placed++;
        }
        placed = 0; tries = 0;
        while (placed < S.grass && tries++ < 800)
        {
            float phi = Rf(0f, TwoPi);
            float d = Rf(0.3f, 2.6f);
            Vector3 p = PBank(phi, d);
            if (NearDock(p, 0.9f)) continue;
            int blades = 3 + rng.Next(3);
            for (int b = 0; b < blades; b++)
            {
                float a = Rf(0f, TwoPi);
                Vector3 bp = p + new Vector3(Rf(-0.08f, 0.08f), -0.02f, Rf(-0.08f, 0.08f));
                decor.Blade(bp, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), Rf(0.22f, 0.48f), 0.07f, Rf(0.06f, 0.2f),
                            Vc(Mul(S.grassDark, 0.9f)), Vc(Mul(S.grassTop, 1.08f)), 2, 1f);
            }
            placed++;
        }
    }

    void BuildMushrooms()
    {
        Color cap = Vc(new Color(0.93f, 0.28f, 0.30f)), stem = Vc(new Color(1f, 0.94f, 0.80f)), dot = Vc(new Color(1f, 1f, 0.96f));
        int placed = 0, tries = 0;
        while (placed < S.mushrooms && tries++ < 300)
        {
            float phi = Rf(0f, TwoPi);
            Vector3 p = PBank(phi, Rf(0.7f, 1.8f));
            if (NearDock(p, 1.2f)) continue;
            int cnt = 1 + rng.Next(3);
            for (int m = 0; m < cnt; m++)
            {
                float s = Rf(0.7f, 1.3f) * (m == 0 ? 1f : 0.7f);
                Vector3 mp = p + new Vector3(Rf(-0.25f, 0.25f), 0f, Rf(-0.25f, 0.25f));
                decor.Tube(mp - Vector3.up * 0.01f, mp + Vector3.up * 0.16f * s, 0.05f * s, 0.04f * s, 6, stem, stem, 0f, true, false, 0f, 0f);
                decor.Ico(mp + Vector3.up * (0.17f * s), new Vector3(0.17f * s, 0.11f * s, 0.17f * s), Quaternion.identity, Mul(cap, 1.1f), Mul(cap, 0.82f), 1, 0.05f, 0f, rng.Next(999), false);
                for (int k = 0; k < 4; k++)
                {
                    float a = Rf(0f, TwoPi), rr = Rf(0.02f, 0.09f) * s;
                    decor.Ico(mp + new Vector3(Mathf.Cos(a) * rr, 0.255f * s - rr * 0.5f, Mathf.Sin(a) * rr), 0.028f * s, dot, 0, 0f, 0f, rng.Next(999));
                }
            }
            placed++;
        }
    }

    // ========================================================================
    //  Рыбки, кувшинки, удочка
    // ========================================================================
    void BuildFish(PondLife life)
    {
        var fr = NewChild(root, "Fish");
        var list = new List<Transform>();
        for (int i = 0; i < S.fish; i++)
        {
            float phi = Rf(0f, TwoPi), rr = Mathf.Sqrt((float)rng.NextDouble()) * 0.7f;
            Vector3 p = Dir(phi) * (Shore(phi) * rr);
            var go = MeshGO(fr, "Fish_" + i.ToString("00"), fishMeshes[i % 5], matFish, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
            go.transform.localPosition = new Vector3(p.x, W - 0.17f, p.z);
            go.transform.localRotation = Quaternion.Euler(0f, Rf(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * Rf(0.5f, 0.85f);
            list.Add(go.transform);
        }
        life.fish = list.ToArray();
    }

    void BuildPads(PondLife life)
    {
        var pr = NewChild(root, "LilyPads");
        var padMeshes = new Mesh[3];
        Color[] pc =
        {
            new Color(0.40f, 0.78f, 0.34f), new Color(0.30f, 0.70f, 0.38f), new Color(0.50f, 0.82f, 0.30f)
        };
        for (int i = 0; i < 3; i++) padMeshes[i] = SaveMesh(MakePad(pc[i]), "Pond_Pad" + i);

        var flMeshes = new Mesh[3];
        flMeshes[0] = SaveMesh(MakeFlower(new Color(1f, 0.58f, 0.76f), new Color(1f, 0.90f, 0.95f)), "Pond_Flower0");
        flMeshes[1] = SaveMesh(MakeFlower(new Color(1f, 0.96f, 0.92f), new Color(1f, 0.82f, 0.88f)), "Pond_Flower1");
        flMeshes[2] = SaveMesh(MakeFlower(new Color(0.74f, 0.58f, 1f), new Color(0.95f, 0.90f, 1f)), "Pond_Flower2");

        var list = new List<Transform>();
        var placed = new List<Vector3>();
        var sizes = new List<float>();
        int tries = 0;
        while (list.Count < S.pads && tries++ < 500)
        {
            float phi = Rf(0f, TwoPi), rr = Rf(0.12f, 0.80f);
            Vector3 p = Dir(phi) * (Shore(phi) * rr);
            if (NearDock(p, 0.8f)) continue;
            float sc = Rf(0.6f, 1.15f);
            bool ok = true;
            for (int j = 0; j < placed.Count; j++)
                if ((placed[j] - p).magnitude < (sc + sizes[j]) * 0.5f + 0.15f) { ok = false; break; }
            if (!ok) continue;

            var g = MeshGO(pr, "Pad_" + list.Count.ToString("00"), padMeshes[rng.Next(3)], matDecor, false);
            g.transform.localPosition = new Vector3(p.x, W + 0.012f, p.z);
            g.transform.localRotation = Quaternion.Euler(0f, Rf(0f, 360f), 0f);
            g.transform.localScale = Vector3.one * sc;
            if (rng.NextDouble() < 0.4)
            {
                var fl = MeshGO(g.transform, "Flower", flMeshes[rng.Next(3)], matDecor, false);
                fl.transform.localPosition = new Vector3(Rf(-0.1f, 0.1f), 0.01f, Rf(-0.1f, 0.1f));
                fl.transform.localRotation = Quaternion.Euler(0f, Rf(0f, 360f), 0f);
            }
            list.Add(g.transform); placed.Add(p); sizes.Add(sc);
        }
        life.pads = list.ToArray();
    }

    void BuildFishing(PondLife life)
    {
        var fr = NewChild(root, "Fishing");

        var tip = NewChild(fr, "RodTip");
        tip.localPosition = rodTipPos;

        var bm = new PondMB();
        bm.Ico(Vector3.zero, new Vector3(0.095f, 0.115f, 0.095f), Quaternion.identity, Vc(new Color(0.95f, 0.22f, 0.22f)), Vc(new Color(1f, 1f, 0.98f)), 1, 0f, 0.1f, 5, true);
        bm.Tube(Vector3.up * 0.10f, Vector3.up * 0.20f, 0.012f, 0.012f, 4, Vc(new Color(0.3f, 0.3f, 0.3f)), Vc(new Color(0.3f, 0.3f, 0.3f)), 0f, true, true, 0f, 0f);
        var bob = MeshGO(fr, "Bobber", SaveMesh(bm.ToMesh("Pond_Bobber"), "Pond_Bobber"), matDecor, false);
        bob.transform.localPosition = bobberPos;

        var lm = new PondMB();
        Color lc = Vc(new Color(0.96f, 0.96f, 0.92f));
        lm.Tube(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f), 0.5f, 0.5f, 4, lc, lc, 0f, false, false, 0f, 0f);
        var line = MeshGO(fr, "Line", SaveMesh(lm.ToMesh("Pond_Line"), "Pond_Line"), matDecor, false);
        PondLife.PlaceLine(line.transform, root.TransformPoint(rodTipPos), root.TransformPoint(bobberPos), life.lineThickness);

        var fp = NewChild(fr, "FishingPoint");
        fp.localPosition = DockPt(dockL - 0.5f, 0f, DY);
        fp.localRotation = Quaternion.LookRotation(dockIn, Vector3.up);

        var zone = NewChild(fr, "FishingZone");
        zone.localPosition = DockPt(dockL - 0.8f, 0f, DY + 1f);
        zone.localRotation = Quaternion.LookRotation(dockIn, Vector3.up);
        var zc = zone.gameObject.AddComponent<BoxCollider>();
        zc.isTrigger = true;
        zc.size = new Vector3(dockW + 1.2f, 2f, 2.2f);

        // коллайдеры мостика
        var cols = NewChild(fr, "Colliders");
        var deck = NewChild(cols, "Deck");
        deck.localPosition = DockPt(dockL * 0.5f + 0.06f, 0f, DY - 0.03f);
        deck.localRotation = Quaternion.LookRotation(dockIn, Vector3.up);
        deck.gameObject.AddComponent<BoxCollider>().size = new Vector3(dockW, 0.14f, dockL);
        var plat = NewChild(cols, "Platform");
        plat.localPosition = DockPt(dockL - 0.75f, 0f, DY - 0.03f);
        plat.localRotation = Quaternion.LookRotation(dockIn, Vector3.up);
        plat.gameObject.AddComponent<BoxCollider>().size = new Vector3(dockW + 0.7f, 0.14f, 1.5f);

        // рыбка торчит из ведра
        var bf = MeshGO(fr, "BucketFish", fishMeshes[0], matFish, true);
        bf.transform.localPosition = bucketFishPos;
        bf.transform.localRotation = Quaternion.Euler(-72f, 200f, 0f);
        bf.transform.localScale = Vector3.one * 0.42f;

        life.bobber = bob.transform;
        life.rodTip = tip;
        life.line = line.transform;
    }

    // ========================================================================
    //  Свет и эффекты
    // ========================================================================
    Light MakeLight(Transform parent, string name, Vector3 localPos, Color col, float range, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    void BuildLights(PondLife life)
    {
        var lr = NewChild(root, "Lights");
        var lights = new List<Light>();
        var halos = new List<Transform>();

        Mesh quad = null;
        if (S.halos)
        {
            var qm = new Mesh { name = "Pond_Halo" };
            qm.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            qm.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            qm.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            qm.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            qm.RecalculateNormals();
            quad = SaveMesh(qm, "Pond_Halo");
        }

        if (S.lights)
        {
            for (int i = 0; i < lanternPos.Count; i++)
            {
                lights.Add(MakeLight(lr, "Lantern_" + i, lanternPos[i], S.lantern, 7.5f, 2.2f * S.lightIntensity));
                if (quad != null)
                {
                    var h = MeshGO(lr, "Halo_" + i, quad, matGlow, false);
                    h.transform.localPosition = lanternPos[i];
                    h.transform.localScale = Vector3.one * 1.8f;
                    halos.Add(h.transform);
                }
            }
            var pool = MakeLight(lr, "PoolGlow", new Vector3(0f, W - 0.05f, 0f), new Color(0.35f, 0.9f, 1f), Mathf.Max(rx, rz) * 1.35f, 1.4f * S.lightIntensity);
            life.poolLight = pool;
        }
        life.lights = lights.ToArray();
        life.halos = halos.ToArray();
    }

    void BuildFx(PondLife life)
    {
        if (S.fireflies)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, H + 1.0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.45f, 1f), new Color(1f, 0.92f, 0.5f, 1f));
            main.maxParticles = 60;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.rateOverTime = 7f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(rx * 2f, 1.2f, rz * 2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.2f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = matGlow;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            life.fireflies = ps;
        }

        var sp = new GameObject("Splash");
        sp.transform.SetParent(root, false);
        sp.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);       // конус смотрит вверх
        var sps = sp.AddComponent<ParticleSystem>();
        var sm = sps.main;
        sm.loop = true;
        sm.playOnAwake = true;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        sm.startColor = new Color(0.85f, 0.96f, 1f, 0.9f);
        sm.gravityModifier = 1.6f;
        sm.maxParticles = 60;
        var sem = sps.emission;
        sem.enabled = false;                                                // частицы выпускает PondLife через Emit
        var ssh = sps.shape;
        ssh.enabled = true;
        ssh.shapeType = ParticleSystemShapeType.Cone;
        ssh.angle = 28f;
        ssh.radius = 0.08f;
        var sr = sp.GetComponent<ParticleSystemRenderer>();
        sr.sharedMaterial = matGlow;
        sr.renderMode = ParticleSystemRenderMode.Billboard;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        life.splash = sps;
    }

    // ========================================================================
    //  Меши: рыбка, кувшинка, цветок
    // ========================================================================
    Mesh MakeFish(int kind)
    {
        Color back, belly, fin;
        switch (kind)
        {
            case 0: back = new Color(1.00f, 0.50f, 0.16f); belly = new Color(1.00f, 0.84f, 0.52f); fin = new Color(1.00f, 0.68f, 0.32f); break;
            case 1: back = new Color(0.20f, 0.62f, 0.96f); belly = new Color(0.80f, 0.96f, 1.00f); fin = new Color(0.45f, 0.80f, 1.00f); break;
            case 2: back = new Color(1.00f, 0.82f, 0.22f); belly = new Color(1.00f, 0.96f, 0.70f); fin = new Color(1.00f, 0.60f, 0.64f); break;
            case 3: back = new Color(0.98f, 0.97f, 0.95f); belly = new Color(1.00f, 0.99f, 0.97f); fin = new Color(1.00f, 0.90f, 0.85f); break;
            default: back = new Color(0.68f, 0.48f, 0.95f); belly = new Color(0.96f, 0.88f, 1.00f); fin = new Color(0.85f, 0.62f, 1.00f); break;
        }
        var mb = new PondMB();
        const float emis = 0.12f;
        const int rings = 11, sides = 10;
        const float zTail = -0.42f, zNose = 0.50f;
        var idx = new int[rings, sides];
        for (int i = 0; i < rings; i++)
        {
            float u = i / (float)(rings - 1);
            float z = Mathf.Lerp(zTail, zNose, u);
            float g = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.8f)), 0.65f);
            g = Mathf.Max(g, 0.10f);
            if (i == rings - 1) g = 0f;
            for (int j = 0; j < sides; j++)
            {
                float ang = j * TwoPi / sides;
                Vector3 p = new Vector3(Mathf.Cos(ang) * 0.17f * g, Mathf.Sin(ang) * 0.21f * g, z);
                float tt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.7f, 0.5f + 0.5f * Mathf.Sin(ang)));
                Color c = Color.Lerp(belly, back, tt);
                if (kind == 3)                                              // кои: пятна
                {
                    float n = Nz(z * 7f, ang * 1.3f + 3f);
                    if (n > 0.80f) c = new Color(0.22f, 0.22f, 0.28f);
                    else if (n > 0.60f) c = new Color(1f, 0.46f, 0.16f);
                }
                idx[i, j] = mb.V(p, Vc(c), emis);
            }
        }
        for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int j2 = (j + 1) % sides;
                int a = idx[i, j], b = idx[i + 1, j], c2 = idx[i + 1, j2], d = idx[i, j2];
                mb.I(a, c2, b);
                mb.I(a, d, c2);
            }
        // заглушка у хвоста
        int tc = mb.V(new Vector3(0, 0, zTail), Vc(belly), emis);
        for (int j = 0; j < sides; j++) mb.I(tc, idx[0, j], idx[0, (j + 1) % sides]);

        Color f0 = Vc(fin), f1 = Vc(Mul(fin, 1.12f));
        // хвост (раздвоенный) и плавники
        Vector3 t0 = new Vector3(0, 0.04f, -0.36f), t1 = new Vector3(0, 0.28f, -0.70f), t2 = new Vector3(0, 0f, -0.55f), t3 = new Vector3(0, -0.28f, -0.70f), t4 = new Vector3(0, -0.04f, -0.36f);
        mb.Tri2(t0, t1, t2, f0, f1, f0, emis);
        mb.Tri2(t0, t2, t4, f0, f0, f0, emis);
        mb.Tri2(t4, t2, t3, f0, f0, f1, emis);
        mb.Tri2(new Vector3(0, 0.17f, 0.18f), new Vector3(0, 0.34f, -0.12f), new Vector3(0, 0.17f, -0.15f), f0, f1, f0, emis);
        for (int s = -1; s <= 1; s += 2)
            mb.Tri2(new Vector3(0.13f * s, -0.05f, 0.18f), new Vector3(0.30f * s, -0.14f, 0.02f), new Vector3(0.12f * s, -0.08f, 0.05f), f0, f1, f0, emis);

        // глазки и румянец
        Color white = Vc(new Color(1f, 1f, 1f)), black = Vc(new Color(0.08f, 0.08f, 0.12f)), blush = Vc(new Color(1f, 0.55f, 0.62f));
        for (int s = -1; s <= 1; s += 2)
        {
            mb.Ico(new Vector3(0.10f * s, 0.075f, 0.33f), 0.07f, white, 1, 0f, 0.1f, 3);
            mb.Ico(new Vector3(0.14f * s, 0.08f, 0.355f), 0.04f, black, 0, 0f, 0f, 4);
            mb.Ico(new Vector3(0.115f * s, -0.04f, 0.31f), 0.035f, blush, 0, 0f, 0.08f, 5);
        }
        return mb.ToMesh("Pond_Fish" + kind);
    }

    // круглый лист с вырезом, куполом и мягким краем
    Mesh MakePad(Color baseC)
    {
        var mb = new PondMB();
        const int seg = 24;
        const float r = 0.5f;
        float half = 0.22f;                                                 // половина выреза, рад
        Color cIn = Vc(Mul(baseC, 1.25f)), cMid = Vc(baseC), cOut = Vc(Mul(baseC, 0.78f));
        int ic = mb.V(new Vector3(0, 0.035f, 0), cIn, 0f);
        var mid = new int[seg + 1];
        var rim = new int[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float a = half + i * (TwoPi - 2f * half) / seg;
            Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            mid[i] = mb.V(d * (r * 0.55f) + Vector3.up * 0.026f, cMid, 0f);
            rim[i] = mb.V(d * r + Vector3.up * (-0.004f + 0.006f * Mathf.Sin(i * 1.7f)), cOut, 0f);
        }
        for (int i = 0; i < seg; i++)
        {
            mb.I(ic, mid[i + 1], mid[i]);
            mb.I(mid[i], mid[i + 1], rim[i + 1]);
            mb.I(mid[i], rim[i + 1], rim[i]);
        }
        return mb.ToMesh("Pond_Pad");
    }

    // лотос: два венчика лепестков и жёлтая серединка
    Mesh MakeFlower(Color petal, Color tip)
    {
        var mb = new PondMB();
        Color pb = Vc(petal), pt = Vc(tip);
        for (int layer = 0; layer < 2; layer++)
        {
            int n = layer == 0 ? 8 : 6;
            float rTip = layer == 0 ? 0.20f : 0.11f;
            float yTip = layer == 0 ? 0.16f : 0.21f;
            float rMid = rTip * 0.55f, yMid = yTip * 0.55f + 0.01f;
            float off = layer == 0 ? 0f : Mathf.PI / n;
            for (int i = 0; i < n; i++)
            {
                float a = off + i * TwoPi / n;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 s = new Vector3(-d.z, 0, d.x);
                Vector3 bs = d * 0.035f + Vector3.up * 0.03f;
                Vector3 l = d * rMid + s * 0.045f + Vector3.up * yMid;
                Vector3 r = d * rMid - s * 0.045f + Vector3.up * yMid;
                Vector3 t = d * rTip + Vector3.up * yTip;
                mb.Tri2(bs, l, t, pb, pb, pt, 0.25f);
                mb.Tri2(bs, t, r, pb, pt, pb, 0.25f);
            }
        }
        mb.Ico(new Vector3(0, 0.085f, 0), 0.05f, Vc(new Color(1f, 0.85f, 0.25f)), 1, 0.05f, 0.7f, 9);
        return mb.ToMesh("Pond_Flower");
    }
}

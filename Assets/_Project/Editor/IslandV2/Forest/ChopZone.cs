#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ChopZone.cs (Editor)  НОВЫЙ ФАЙЛ
// «Зона рубки»: поляна вокруг метки PAD_Forest превращается в читаемое место: низкий забор с арками у троп, внутри только рубимые деревья
// (кластерами с проходами, молодые у входа, старые в глубине), снаружи аккуратные клумбы и кусты у забора и кольцо старых дубов.
// Меню: Tools/BuilderSim/Лес/Красота леса/11. Зона рубки: забор, деревья, окружение (240), 12. Убрать зону рубки (241).
// Безопасно жать повторно: корень ChopZone в IslandV2/_Manual пересоздаётся целиком, рабочие деревья зоны (ZoneTree_*) тоже.
// «4. Лес» теперь вызывает этот пункт сам в конце (на PAD_Forest он больше не рассыпает случайные рабочие деревья).
//
// Что строит (процедурно, TreeMB, материалы TreeToon, пастель как у остального леса):
//   • забор: столбы через ~2.1 м, две перекладины, на каждом третьем столбе тёплый фонарик (свечение ночью, без настоящего света);
//     форма не круг, а мягкий овал (радиус 11-15 м). Забор сплошной (коллайдеры), открыт только у троп;
//   • арка у каждой тропы, которая пересекает забор: два высоких столба, дуга-балка с гирляндой (розовые и тёплые огоньки),
//     табличка с топором (видна и снаружи, и изнутри), стоит настоящий фонарь над главной аркой;
//   • деревья: 8-9 групп по 3-6 штук, между группами проходы. Рядом с аркой молодые, дальше зрелые, в глубине старые (ResourceItem.forcedSize).
//     У каждой группы свой преобладающий вид: поляна читается как «рощицы», а не россыпь. Нерубимых деревьев внутри нет (всё в радиусе ClearR вычищается);
//   • вокруг арок каменные плиты дорожкой, утоптанная земля; внутри пятна земли, чтобы поляна не была ровным ковром;
//   • снаружи вдоль забора группы кустов (лаймовые, тёмные, цветущие), кустики цветов и камни; внутри у забора редкие цветы.
// Порядок: после «4. Лес» (она вызывает этот пункт), до «8. Поляна рубки: уют» (вещи поляны обходят деревья и ставятся внутри забора).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ChopZone
    {
        public const string RootName = "ChopZone";
        public const string PadName = "PAD_GroveSouth";   // метка, вокруг которой строится зона рубки (забор, арки, рабочие деревья). PAD_Forest теперь маленькая достопримечательность, см. ForestBackdrop
        public const string TreePrefix = "ZoneTree_";
        public const float BaseR = 13f;        // средний радиус забора, м
        public const float ClearR = 15.5f;     // дальше этого радиуса начинается обычный фоновый лес (внутри всё нерубимое вычищается)

        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const string SrcDir = "Assets/_Project/Prefabs/Trees/Generated";
        const float PostStep = 2.1f;
        const bool FenceSolid = true;          // false = забор без коллайдеров (проходимый)
        const int Seed = 9931;
        const int Tn = 720;

        sealed class Post { public Vector2 w; public Vector3 l; public float ang, h; public bool exists, gate; }

        static IslandField F;
        static IslandLayout L;
        static Vector3 O;
        static Vector2 C2;
        static Transform R, Cols;
        static TreeMB A, B, Cm, D;     // A стройка (0 дерево, 1 тёмное, 2 тёплое, 3 розовое), B кусты (0 лайм, 1 тёмный, 2 низ, 3 розовые бусины),
                                       // Cm цветы (0 стебель, 1 жёлтые, 2 розовые, 3 белые), D земля (0 утоптанная земля, 1 камень)
        static float[] cum;
        static float perim;
        static Post[] posts;
        static readonly List<Vector2> gates = new List<Vector2>();
        static readonly List<Vector3> lanternPts = new List<Vector3>();
        static readonly List<Vector2> treesW = new List<Vector2>();
        static int nCol, nTrees, nBush, nTuft, nRock;

        // угол (в плоскости x,z) на главную арку, нужен ChopCamp и ForestGladeBeauty, чтобы расставлять вещи одной композицией от входа.
        // Та же логика, что в FindGates: пересечения троп с забором, дубли ближе 6 м отбрасываются, главная = ближайшая к центру плана.
        public static float MainGateAngle(IslandField f, IslandLayout lay, Vector2 c2)
        {
            var found = new List<Vector2>();
            for (int i = 0; i < lay.paths.Count; i++)
            {
                var pl = f.PathPolyline(i);
                if (pl == null || pl.Length < 2) continue;
                for (int j = 0; j < pl.Length - 1; j++)
                {
                    Vector2 d0 = pl[j] - c2, d1 = pl[j + 1] - c2;
                    float s0 = d0.magnitude - Rb(AngOf(d0)), s1 = d1.magnitude - Rb(AngOf(d1));
                    if (s0 * s1 >= 0f) continue;
                    Vector2 p = Vector2.Lerp(pl[j], pl[j + 1], s0 / (s0 - s1));
                    bool dup = false;
                    for (int g = 0; g < found.Count; g++) if (Vector2.Distance(found[g], p) < 6f) dup = true;
                    if (!dup) found.Add(p);
                }
            }
            if (found.Count == 0)
            {
                Vector2 d = Vector2.zero - c2;
                if (d.sqrMagnitude < 0.01f) d = Vector2.down;
                return AngOf(d);
            }
            found.Sort((p, q) => p.sqrMagnitude.CompareTo(q.sqrMagnitude));
            return AngOf(found[0] - c2);
        }

        // ============================================================ форма
        public static float Rb(float a)
        {
            return BaseR * (1f + 0.07f * Mathf.Sin(2f * a + 0.6f) + 0.045f * Mathf.Sin(3f * a + 2.1f) + 0.025f * Mathf.Sin(5f * a + 1.2f));
        }
        static Vector2 Dir2(float a) { return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        static float AngOf(Vector2 d) { return Mathf.Atan2(d.y, d.x); }
        static float AngDist(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad; }
        static Vector3 Loc(Vector2 w) { return new Vector3(w.x - O.x, F.Height(w.x, w.y) - O.y, w.y - O.z); }
        static Color Rgb(float r, float g, float b) { return new Color(r, g, b); }

        // ============================================================ для других инструментов (подлесок): сюда ничего «случайного» не сыпать
        static Vector2 avC;
        static bool avOn;
        static readonly List<Vector3> avSacred = new List<Vector3>();   // (x, z, радиус полянки)

        public static void PrepareAvoid()
        {
            avSacred.Clear();
            var pad = GameObject.Find(PadName);
            avOn = pad != null && GameObject.Find("IslandV2/_Manual/" + RootName) != null;
            if (avOn) avC = new Vector2(pad.transform.position.x, pad.transform.position.z);
            var sacred = GameObject.Find("IslandV2/_Manual/SacredTrees");
            if (sacred != null)
                foreach (Transform t in sacred.transform) avSacred.Add(new Vector3(t.position.x, t.position.z, 4.2f));
            // лёгкие хранители: у каждого своя полянка (крупные с кольцом шире, мелкие уже)
            var lite = GameObject.Find("IslandV2/_Manual/" + ForestGuardLite.RootName);
            if (lite != null)
                foreach (Transform t in lite.transform)
                {
                    var lg = t.GetComponent<LiteGuardian>();
                    avSacred.Add(new Vector3(t.position.x, t.position.z, lg != null ? lg.clearRadius : 4.2f));
                }
            // кулисные деревья: у каждой свой венок из камней, внутри него кустов и цветов нет
            var wings = GameObject.Find("IslandV2/_Manual/" + ForestWings.RootName);
            if (wings != null)
                foreach (Transform t in wings.transform)
                    avSacred.Add(new Vector3(t.position.x, t.position.z, ForestWings.ClearRadius(t)));
        }

        // true: тут подлеску ставить нельзя (внутри забора и в полосе озеленения снаружи, у деревьев-хранителей)
        public static bool Avoid(float x, float z)
        {
            if (avOn)
            {
                Vector2 d = new Vector2(x, z) - avC;
                if (d.magnitude < Rb(AngOf(d)) + 3.2f) return true;
            }
            for (int i = 0; i < avSacred.Count; i++)
            {
                float dx = avSacred[i].x - x, dz = avSacred[i].y - z, r = avSacred[i].z;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/11. Зона рубки: забор, деревья, окружение", false, 240)]
        static void Run()
        {
            string rep = Build();
            if (rep != null) Debug.Log(rep);
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/12. Убрать зону рубки (забор и окружение)", false, 241)]
        static void Clear()
        {
            var manual = GameObject.Find("IslandV2/_Manual");
            if (manual == null) { Debug.LogWarning("Зона рубки: нет IslandV2/_Manual."); return; }
            var t = manual.transform.Find(RootName);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            Debug.Log("Зона рубки: забор и окружение убраны. Деревья ZoneTree_* остались в ForestChop (убери «4. Лес» ещё раз или руками).");
        }

        // ============================================================ главное
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Зона рубки: останови Play Mode."); return null; }
            var pad = GameObject.Find(PadName);
            if (pad == null) { Debug.LogError("Зона рубки: в сцене нет метки " + PadName + " (Остров v2 → 2. Собрать блокаут)."); return null; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Зона рубки: в сцене нет IslandV2."); return null; }
            L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Зона рубки: нет раскладки " + IslandLayout.AssetPath); return null; }
            F = new IslandField(L);
            O = pad.transform.position;
            C2 = new Vector2(O.x, O.z);
            var manualGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = manualGo != null ? manualGo.transform : rootGo.transform;

            // ---- материалы
            var wood = ForestMats.GroveWood(false); var woodD = ForestMats.GroveWoodDark(false);
            var warm = ForestMats.GroveWarm(false); var pink = ForestMats.FlowerPink(false);
            var lime = ForestMats.BushLime(false); var dark = ForestMats.BushDark(false); var under = ForestMats.BushUnder(false);
            var stem = ForestMats.Stem(false); var yellow = ForestMats.FlowerYellow(false); var white = ForestMats.FlowerWhite(false);
            var rock = ForestMats.Rock(false);
            var dirt = ForestMats.Get("Zone_Dirt", Rgb(0.86f, 0.74f, 0.54f), Rgb(0.68f, 0.54f, 0.38f), 0f, 1f, 0f, 0.02f, 0.2f, false);
            foreach (var m in new[] { wood, woodD, warm, pink, lime, dark, under, stem, yellow, white, rock, dirt })
                if (m == null) { Debug.LogError("Зона рубки: не создался материал (проверь шейдер TreeToon: Лес → Tree Toon)."); return null; }

            // ---- префабы деревьев
            var sp = new Dictionary<string, List<GameObject>>();
            foreach (var key in new[] { "Cedar", "Birch", "Autumn", "Random" })
            {
                sp[key] = LoadSpecies(key);
                if (sp[key].Count == 0) { Debug.LogError("Зона рубки: нет префабов Tree_" + key + "_* в " + SrcDir + ". Сначала Tools/BuilderSim/Лес/Деревья/Build Species."); return null; }
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // ---- корни
            var old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var chopT = parent.Find("ForestChop");
            if (chopT == null) chopT = NewRoot(parent, "ForestChop");
            var rootObj = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootObj, RootName);
            rootObj.transform.SetParent(parent, false);
            rootObj.transform.position = O;
            rootObj.transform.rotation = Quaternion.identity;
            R = rootObj.transform;
            var colGo = new GameObject("Colliders");
            colGo.transform.SetParent(R, false);
            Cols = colGo.transform;

            A = new TreeMB(); B = new TreeMB(); Cm = new TreeMB(); D = new TreeMB();
            gates.Clear(); lanternPts.Clear(); nCol = 0; nTrees = 0; nBush = 0; nTuft = 0; nRock = 0;

            // ---- 1. вычистить всё нерубимое и старое внутри ClearR
            int removed = ClearOld(parent);

            // ---- 2. забор, арки
            FindGates();
            BuildArc();
            BuildPosts();
            int nPosts = 0, nSegs = 0, nGates = 0;
            BuildFence(out nPosts, out nSegs);
            nGates = BuildGates();

            // ---- 3. деревья
            var rng = new System.Random(Seed);
            PlaceTrees(sp, chopT, rng);

            // ---- 4. окружение
            GatherTrees(parent);
            Ground(rng);
            Plants();

            // ---- 5. меши
            EditorAssets.EnsureFolder(Dir);
            AddMesh("Structure", A, "ChopZone_Structure", new[] { wood, woodD, warm, pink }, true);
            AddMesh("Bushes", B, "ChopZone_Bushes", new[] { lime, dark, under, pink }, true);
            AddMesh("Flowers", Cm, "ChopZone_Flowers", new[] { stem, yellow, pink, white }, false);
            AddMesh("Ground", D, "ChopZone_Ground", new[] { dirt, rock }, false);

            // ---- 6. свет: огоньки фонариков (без настоящего света) и фонарь над главной аркой
            if (lanternPts.Count > 0)
            {
                var go = new GameObject("Light_FenceLanterns");
                go.transform.SetParent(R, false);
                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Garland;
                fs.color = new Color(1f, 0.80f, 0.48f);
                fs.intensity = 1f; fs.range = 4f; fs.simOnly = true;
                fs.glow = true; fs.glowSize = 0.75f;
                fs.glowPoints = lanternPts.ToArray();
            }
            if (gates.Count > 0)
            {
                var go = new GameObject("Light_Gate");
                go.transform.SetParent(R, false);
                Vector3 gl = Loc(gates[0]) + Vector3.up * 2.9f;
                go.transform.localPosition = gl;
                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Lantern;
                fs.color = new Color(1f, 0.78f, 0.45f);
                fs.intensity = 2.0f; fs.range = 7.5f; fs.simOnly = false;
                fs.glow = true; fs.glowSize = 1.1f;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(R.gameObject.scene);

            var sb = new StringBuilder("Зона рубки (" + PadName + " " + O.x.ToString("0.#") + ", " + O.z.ToString("0.#") + "):\n");
            sb.AppendLine("  убрано старых деревьев в радиусе " + ClearR.ToString("0.#") + " м: " + removed);
            sb.AppendLine("  забор: столбов " + nPosts + ", пролётов " + nSegs + ", арок " + nGates + (nGates == 0 ? " [!] ни одна тропа не пересекла забор: вход открыт без арки" : "") + (nPosts < posts.Length * 0.85f ? " [!] столбов " + nPosts + " из " + posts.Length + ": забор рвётся у края острова/воды, подвинь " + PadName + " внутрь или уменьши BaseR" : "") + ", фонариков " + lanternPts.Count + (FenceSolid ? ", коллайдеров " + nCol : ", без коллайдеров"));
            sb.AppendLine("  рабочих деревьев внутри: " + nTrees + (nTrees > zoneTreeStrict ? " (строгих " + zoneTreeStrict + ", остальные мягким проходом)" : "") + (nTrees < MinTrees ? " [!] мало, цель " + MinTrees : "") + " (молодые у арки, старые в глубине), кустов " + nBush + ", кустиков цветов " + nTuft + ", камней " + nRock);
            sb.AppendLine("  Корень: IslandV2/_Manual/" + RootName + ", деревья ForestChop/" + TreePrefix + "*. Дальше: «8. Поляна рубки: уют» (вещи встанут внутри забора), «2. Подлесок» (обойдёт зону).");
            if (nTrees == 0) Debug.LogError("Зона рубки: деревьев 0. Отбраковка: " + RejectReport() + ". Проверь, что есть префабы Tree_* и что " + PadName + " не у самого обрыва.");
            else if (nTrees < MinTrees) sb.AppendLine("  отбраковка точек: " + RejectReport());
            return sb.ToString();
        }

        // ============================================================ чистка
        static int ClearOld(Transform parent)
        {
            var cand = new List<Transform>();
            foreach (var rootName in new[] { "ForestChop", "ForestDecor", ForestGuardLite.RootName, ForestWings.RootName })   // ForestDecor: корень старых версий
            {
                var t = parent.Find(rootName);
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++) cand.Add(t.GetChild(i));
            }
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) cand.Add(ri.transform);

            var done = new HashSet<GameObject>();
            int n = 0;
            for (int i = 0; i < cand.Count; i++)
            {
                var t = cand[i];
                if (t == null) continue;
                Vector2 q = new Vector2(t.position.x, t.position.z);
                if ((q - C2).sqrMagnitude >= ClearR * ClearR) continue;
                var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                GameObject target = inst != null ? inst : t.gameObject;
                if (!done.Add(target)) continue;
                Undo.DestroyObjectImmediate(target);
                n++;
            }
            return n;
        }

        // ============================================================ тропы и ворота
        static float SignedD(Vector2 p)
        {
            Vector2 d = p - C2;
            return d.magnitude - Rb(AngOf(d));
        }

        static void FindGates()
        {
            for (int i = 0; i < L.paths.Count; i++)
            {
                var pl = F.PathPolyline(i);
                if (pl == null || pl.Length < 2) continue;
                for (int j = 0; j < pl.Length - 1; j++)
                {
                    float s0 = SignedD(pl[j]), s1 = SignedD(pl[j + 1]);
                    if (s0 * s1 >= 0f) continue;
                    Vector2 p = Vector2.Lerp(pl[j], pl[j + 1], s0 / (s0 - s1));
                    bool dup = false;
                    for (int g = 0; g < gates.Count; g++) if (Vector2.Distance(gates[g], p) < 6f) dup = true;
                    if (!dup) gates.Add(p);
                }
            }
            if (gates.Count == 0)
            {
                // ни одна тропа не пересекает границу: открываем сторону к центру острова
                Vector2 d = Vector2.zero - C2;
                if (d.sqrMagnitude < 0.01f) d = Vector2.down;
                float a = AngOf(d);
                gates.Add(C2 + Dir2(a) * Rb(a));
            }
            // главная арка: ближайшая к лагерю (центру плана)
            gates.Sort((p, q) => p.sqrMagnitude.CompareTo(q.sqrMagnitude));
        }

        static float GateAngDist(float a)
        {
            float best = Mathf.PI;
            for (int i = 0; i < gates.Count; i++) best = Mathf.Min(best, AngDist(a, AngOf(gates[i] - C2)));
            return best;
        }

        // расстояние от точки до отрезков «арка → центр» (проход к игроку должен оставаться свободным)
        static bool InGateLane(Vector2 p, float halfW)
        {
            for (int i = 0; i < gates.Count; i++)
            {
                Vector2 a = gates[i], b = C2, ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                if (Vector2.Distance(p, a + ab * t) < halfW) return true;
            }
            return false;
        }

        // ============================================================ столбы
        static void BuildArc()
        {
            cum = new float[Tn + 1];
            Vector2 prev = C2 + Dir2(0f) * Rb(0f);
            for (int i = 1; i <= Tn; i++)
            {
                float a = i * Mathf.PI * 2f / Tn;
                Vector2 p = C2 + Dir2(a) * Rb(a);
                cum[i] = cum[i - 1] + Vector2.Distance(p, prev);
                prev = p;
            }
            perim = cum[Tn];
        }

        static float AngleAt(float s)
        {
            s = Mathf.Repeat(s, perim);
            int lo = 0, hi = Tn;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (cum[m] <= s) lo = m; else hi = m; }
            float t = (s - cum[lo]) / Mathf.Max(1e-4f, cum[lo + 1] - cum[lo]);
            return (lo + t) * Mathf.PI * 2f / Tn;
        }

        static bool ValidPost(Vector2 w)
        {
            return F.Inside(w.x, w.y) && F.EdgeDistance(w.x, w.y) >= 1.6f && !F.IsWater(w.x, w.y) && !F.IsFord(w.x, w.y) && F.WaterDistance(w.x, w.y) >= 0.8f;
        }

        static void BuildPosts()
        {
            int n = Mathf.Max(12, Mathf.RoundToInt(perim / PostStep));
            posts = new Post[n];
            for (int k = 0; k < n; k++)
            {
                float a = AngleAt(k * perim / n);
                var p = new Post { ang = a, w = C2 + Dir2(a) * Rb(a) };
                p.l = Loc(p.w);
                p.h = 1.05f + 0.10f * (TreeMB.H(601, k) - 0.5f);
                p.exists = ValidPost(p.w);
                posts[k] = p;
            }
            // проёмы у арок: убираем ближайший к арке столб, соседние становятся арочными (высокие)
            var gateIdx = new List<int>();
            for (int g = 0; g < gates.Count; g++)
            {
                int kg = 0; float best = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    float d = Vector2.Distance(posts[k].w, gates[g]);
                    if (d < best) { best = d; kg = k; }
                }
                gates[g] = posts[kg].w;                       // арка встаёт ровно на проём
                posts[kg].exists = false;
                var a = posts[(kg + n - 1) % n]; var b = posts[(kg + 1) % n];
                if (a.exists && b.exists) { a.gate = true; b.gate = true; a.h = 2.7f; b.h = 2.7f; }
                gateIdx.Add(kg);
            }
            // остальные столбы: не на самой тропе (тропа, идущая вдоль забора, оставит естественный проём)
            for (int k = 0; k < n; k++)
                if (posts[k].exists && !posts[k].gate && F.PathDistance(posts[k].w.x, posts[k].w.y) < 1.0f) posts[k].exists = false;
        }

        static void BuildFence(out int nPosts, out int nSegs)
        {
            nPosts = 0; nSegs = 0;
            int n = posts.Length;
            int lantern = 0;
            for (int k = 0; k < n; k++)
            {
                var p = posts[k];
                if (!p.exists) continue;
                nPosts++;
                float r = p.gate ? 0.11f : 0.065f;
                float yaw = TreeMB.H(602, k) * 90f;
                float top = p.l.y + p.h, bottom = p.l.y - 0.14f;
                GroveGeo.Box(A, 0, new Vector3(p.l.x, (top + bottom) * 0.5f, p.l.z), new Vector3(r, (top - bottom) * 0.5f, r), yaw);
                GroveGeo.Box(A, 1, new Vector3(p.l.x, top + 0.03f, p.l.z), new Vector3(r * 1.5f, 0.03f, r * 1.5f), yaw);
                if (p.gate)
                {
                    AddCol("Col_GatePost", new Vector3(p.l.x, p.l.y + p.h * 0.5f, p.l.z), Quaternion.identity, new Vector3(0.32f, p.h, 0.32f));
                    lanternPts.Add(new Vector3(p.l.x, top + 0.22f, p.l.z));
                    A.Blob(2, new Vector3(p.l.x, top + 0.22f, p.l.z), new Vector3(0.10f, 0.13f, 0.10f), 620 + k, 0, 0.1f, -1);
                    GroveGeo.Box(A, 1, new Vector3(p.l.x, top + 0.40f, p.l.z), new Vector3(0.13f, 0.02f, 0.13f), yaw);
                }
                else if (lantern++ % 3 == 1)
                {
                    Vector3 lp = new Vector3(p.l.x, top + 0.20f, p.l.z);
                    A.Blob(2, lp, new Vector3(0.10f, 0.13f, 0.10f), 620 + k, 0, 0.1f, -1);
                    GroveGeo.Box(A, 1, lp + Vector3.up * 0.18f, new Vector3(0.13f, 0.02f, 0.13f), yaw);
                    lanternPts.Add(lp);
                }
            }
            for (int k = 0; k < n; k++)
            {
                var a = posts[k]; var b = posts[(k + 1) % n];
                if (!a.exists || !b.exists) continue;
                if (Vector3.Distance(a.l, b.l) > 3.4f) continue;
                nSegs++;
                for (int i = 0; i < 2; i++)
                {
                    float h = i == 0 ? 0.42f : 0.80f;
                    Vector3 pa = a.l + Vector3.up * h, pb = b.l + Vector3.up * h;
                    Vector3 d = pb - pa;
                    GroveGeo.Box(A, 0, (pa + pb) * 0.5f, new Vector3(0.022f, 0.04f, d.magnitude * 0.5f), Quaternion.LookRotation(d.normalized, Vector3.up));
                }
                Vector3 dd = b.l - a.l; dd.y = 0f;
                if (FenceSolid && dd.sqrMagnitude > 0.01f)
                    AddCol("Col_Fence", (a.l + b.l) * 0.5f + Vector3.up * 0.55f, Quaternion.LookRotation(dd.normalized, Vector3.up), new Vector3(0.22f, 1.1f, dd.magnitude));
            }
        }

        // ============================================================ арки
        static int BuildGates()
        {
            int built = 0, n = posts.Length;
            for (int g = 0; g < gates.Count; g++)
            {
                // арочные столбы: соседи самого проёма
                int kg = 0; float best = float.MaxValue;
                for (int k = 0; k < n; k++) { float d = Vector2.Distance(posts[k].w, gates[g]); if (d < best) { best = d; kg = k; } }
                var a = posts[(kg + n - 1) % n]; var b = posts[(kg + 1) % n];
                if (!a.exists || !b.exists || !a.gate || !b.gate) continue;

                Vector3 ta = a.l + Vector3.up * a.h, tb = b.l + Vector3.up * b.h;
                const int segs = 10;
                var pts = new Vector3[segs + 1];
                var rad = new float[segs + 1];
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    float arch = 0.55f * (1f - (2f * t - 1f) * (2f * t - 1f));
                    pts[i] = Vector3.Lerp(ta, tb, t) + Vector3.up * (0.10f + arch);
                    rad[i] = 0.085f;
                }
                A.Tube(1, pts, rad, 6, false);

                // гирлянда по дуге: розовые и тёплые огоньки
                for (int i = 1; i < segs; i++)
                    A.Blob(i % 2 == 0 ? 3 : 2, pts[i] + Vector3.down * 0.07f, new Vector3(0.14f, 0.10f, 0.14f), 640 + g * 20 + i, 0, 0.2f, -1);
                lanternPts.Add(pts[segs / 2] + Vector3.down * 0.07f);

                // табличка с топором (смотрит наружу, видна с обеих сторон)
                Vector2 n2 = (gates[g] - C2).normalized;
                Vector3 nrm = new Vector3(n2.x, 0f, n2.y);
                Quaternion q = Quaternion.LookRotation(nrm, Vector3.up);
                Vector3 bc = pts[segs / 2] + Vector3.down * 1.0f;
                GroveGeo.Box(A, 0, bc, new Vector3(0.70f, 0.33f, 0.026f), q);                    // рамка
                GroveGeo.Box(A, 3, bc, new Vector3(0.62f, 0.26f, 0.034f), q);                    // розовая доска
                for (int s = -1; s <= 1; s += 2)
                {
                    float zc = s * 0.045f;
                    Quaternion q2 = q * Quaternion.Euler(0f, 0f, -28f);
                    Vector3 hc = bc + q * new Vector3(-0.07f, -0.03f, zc);
                    GroveGeo.Box(A, 1, hc, new Vector3(0.028f, 0.19f, 0.012f), q2);                                  // рукоять
                    Vector3 top = hc + q2 * new Vector3(0f, 0.15f, 0f);
                    GroveGeo.Box(A, 2, top + q2 * new Vector3(0.09f, 0.0f, 0f), new Vector3(0.11f, 0.065f, 0.012f), q2);   // лезвие
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 up = pts[Mathf.Clamp(segs / 2 + s * 3, 0, segs)];
                    Vector3 corner = bc + q * new Vector3(s * 0.52f, 0.33f, 0f);
                    A.Tube(1, new[] { up, corner }, new[] { 0.014f, 0.014f }, 4, false);
                }

                // плиты дорожкой от арки к центру и пятно утоптанной земли
                Vector2 u = (C2 - gates[g]).normalized, perp = new Vector2(-u.y, u.x);
                float dist = Vector2.Distance(gates[g], C2);
                int si = 0;
                for (float t = 0.5f; t < dist - 3.6f; t += 1.05f, si++)
                {
                    Vector2 w = gates[g] + u * t + perp * ((TreeMB.H(650 + g, si) - 0.5f) * 0.7f);
                    Vector3 l = Loc(w);
                    D.Blob(1, l + Vector3.up * 0.05f, new Vector3(0.32f + 0.12f * TreeMB.H(660 + g, si), 0.06f, 0.27f + 0.10f * TreeMB.H(670 + g, si)), 680 + si, 0, 0.3f, -1);
                }
                D.Blob(0, Loc(gates[g] + u * 1.8f) + Vector3.up * 0.045f, new Vector3(2.0f, 0.03f, 2.2f), 690 + g, 1, 0.35f, -1);
                built++;
            }
            return built;
        }

        // ============================================================ деревья
        static List<GameObject> LoadSpecies(string key)
        {
            var res = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("Tree_" + key + "_ t:Prefab", new[] { SrcDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string nm = System.IO.Path.GetFileNameWithoutExtension(p);
                if (!nm.StartsWith("Tree_" + key + "_")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) res.Add(go);
            }
            res.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            return res;
        }

        static bool relax;                      // второй проход: мягче к краю, воде, склону (если строгий не набрал деревьев)
        static readonly int[] rej = new int[8]; // почему отбраковано: 0 центр/край забора, 1 край острова, 2 вода, 3 тропа, 4 склон, 5 проход к арке, 6 якорь/здание, 7 близко к своему дереву
        static readonly string[] rejName = { "центр/забор", "край острова", "вода", "тропа", "склон", "проход у арки", "якорь/здание", "соседнее дерево" };

        static bool TreeOk(Vector2 p, List<Vector2> mine)
        {
            Vector2 d = p - C2;
            float dist = d.magnitude;
            if (dist < (relax ? 5.5f : 7.0f) || dist > Rb(AngOf(d)) - (relax ? 1.5f : 1.9f)) { rej[0]++; return false; }
            if (!F.Inside(p.x, p.y) || F.EdgeDistance(p.x, p.y) < (relax ? 1.0f : 2f)) { rej[1]++; return false; }
            if (F.WaterDistance(p.x, p.y) < (relax ? 1.0f : 2f) || F.IsFord(p.x, p.y)) { rej[2]++; return false; }
            if (F.PathDistance(p.x, p.y) < (relax ? 1.8f : 2.4f)) { rej[3]++; return false; }
            if (F.Slope(p.x, p.y) > (relax ? 40f : 28f)) { rej[4]++; return false; }
            if (InGateLane(p, relax ? 1.8f : 2.3f)) { rej[5]++; return false; }
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind == IslandLayout.AnchorKind.Building) { if (F.RectDistance(a, p.x, p.y) < 4f) { rej[6]++; return false; } }
                else if (a.name != PadName && Vector2.Distance(F.AnchorWorld(a), p) < 3f) { rej[6]++; return false; }
            }
            float gap = relax ? 2.3f : 2.7f;
            for (int i = 0; i < mine.Count; i++) if ((mine[i] - p).sqrMagnitude < gap * gap) { rej[7]++; return false; }
            return true;
        }

        const int MinTrees = 26;   // GroveSouth: основной лес, деревьев должно быть много (как на референсе, кольцо вокруг поляны)

        static void SpawnZoneTree(Vector2 p, string key, TreeSize size, Dictionary<string, List<GameObject>> sp, Transform chopT, System.Random rng, List<Vector2> mine)
        {
            var list = sp[key];
            var prefab = list[rng.Next(list.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, chopT);
            Undo.RegisterCreatedObjectUndo(go, TreePrefix + nTrees);
            go.name = TreePrefix + nTrees;
            go.transform.position = new Vector3(p.x, F.Height(p.x, p.y), p.y);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var ri = go.GetComponent<ResourceItem>();
            if (ri != null)
            {
                Undo.RecordObject(ri, "Зона рубки: размер дерева");
                ri.overrideSize = true;
                ri.forcedSize = size;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ri);
            }
            mine.Add(p);
            nTrees++;
        }

        // th = угловое расстояние от арки, рад: 0 у входа, pi в глубине
        static TreeSize SizeAt(float th, System.Random rng)
        {
            double r = rng.NextDouble();
            if (th < 0.95f) return TreeSize.Young;                                  // у входа только молодые: вид внутрь открыт
            if (th < 2.0f) return r < 0.15 ? TreeSize.Young : r < 0.85 ? TreeSize.Mature : TreeSize.Old;
            return r < 0.45 ? TreeSize.Mature : TreeSize.Old;                       // в глубине старые
        }

        static string SpeciesAt(float th, int k)
        {
            string[] side = { "Random", "Birch", "Autumn", "Random" };
            string[] back = { "Autumn", "Random", "Autumn", "Birch", "Random" };
            if (th < 0.95f) return k % 3 == 2 ? "Cedar" : (k % 2 == 0 ? "Birch" : "Random");   // у входа светлые молодые деревья, ёлки только акцентом (тёмные ёлки закрывали вид)
            if (th < 2.0f) return k % 9 == 4 ? "Cedar" : side[(k / 3) % side.Length];
            return k % 9 == 2 ? "Cedar" : back[(k / 3) % back.Length];
        }

        static int zoneTreeStrict;

        // композиция: открытый центр, вокруг него два ряда деревьев в шахматку (внешний полный, внутренний только по бокам и в глубине),
        // у арки низкие молодые ёлочки, по бокам круглые лиственные группами по 3, в глубине старые золотые и оранжевые
        static void PlaceTrees(Dictionary<string, List<GameObject>> sp, Transform chopT, System.Random rng)
        {
            var mine = new List<Vector2>();
            System.Array.Clear(rej, 0, rej.Length);
            relax = false;
            float g0 = AngOf(gates[0] - C2);
            const float gap = 0.40f;              // свободный проход у арки, рад
            float[] rowF = { 0.84f, 0.74f };
            for (int row = 0; row < 2; row++)
            {
                float a = g0 + gap + (row == 1 ? 0.5f * 3.0f / (Rb(g0) * rowF[1]) : 0f);
                int k = 0;
                float end = g0 + Mathf.PI * 2f - gap;
                while (a < end)
                {
                    float rr = Rb(a) * rowF[row];
                    float th = Mathf.Clamp(AngDist(a, g0), 0f, Mathf.PI);
                    float step = (th < 0.95f ? 3.4f : 3.0f) / rr;
                    if (row == 1 && th < 0.9f) { a += step; continue; }           // внутреннего ряда у входа нет: вид внутрь открыт
                    float jr = ((float)rng.NextDouble() - 0.5f) * 0.7f, ja = ((float)rng.NextDouble() - 0.5f) * 0.10f;
                    Vector2 p = C2 + Dir2(a + ja) * (rr + jr);
                    if (TreeOk(p, mine)) SpawnZoneTree(p, SpeciesAt(th, k), SizeAt(th, rng), sp, chopT, rng, mine);
                    k++;
                    a += step;
                }
            }
            // ---- если места мало (край острова, вода, тропы), мягче правила и добираем по кольцу до MinTrees
            int strict = nTrees;
            if (nTrees < MinTrees)
            {
                relax = true;
                for (int tries = 0; tries < 1500 && nTrees < MinTrees; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rho = Rb(a) * Mathf.Lerp(0.66f, 0.90f, (float)rng.NextDouble());
                    Vector2 p = C2 + Dir2(a) * rho;
                    if (!TreeOk(p, mine)) continue;
                    float th = Mathf.Clamp(AngDist(a, g0), 0f, Mathf.PI);
                    SpawnZoneTree(p, SpeciesAt(th, nTrees), SizeAt(th, rng), sp, chopT, rng, mine);
                }
                relax = false;
            }
            zoneTreeStrict = strict;
        }

        static string RejectReport()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rej.Length; i++) if (rej[i] > 0) sb.Append((sb.Length > 0 ? ", " : "") + rejName[i] + " " + rej[i]);
            return sb.ToString();
        }

        // ============================================================ окружение
        static void GatherTrees(Transform parent)
        {
            treesW.Clear();
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                treesW.Add(new Vector2(ri.transform.position.x, ri.transform.position.z));
            foreach (var rn in new[] { "ForestDecor", "SacredTrees", ForestGuardLite.RootName, ForestWings.RootName })
            {
                var t = parent.Find(rn);
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++) { var p = t.GetChild(i).position; treesW.Add(new Vector2(p.x, p.z)); }
            }
        }

        static bool NearTree(Vector2 w, float r)
        {
            for (int i = 0; i < treesW.Count; i++) if ((treesW[i] - w).sqrMagnitude < r * r) return true;
            return false;
        }

        static bool PlantOk(Vector2 w, float treeClear)
        {
            if (!F.Inside(w.x, w.y) || F.EdgeDistance(w.x, w.y) < 1.2f) return false;
            if (F.IsWater(w.x, w.y) || F.IsFord(w.x, w.y) || F.WaterDistance(w.x, w.y) < 0.5f) return false;
            if (F.PathDistance(w.x, w.y) < 1.3f) return false;
            if (F.Slope(w.x, w.y) > 30f) return false;
            return !NearTree(w, treeClear);
        }

        // утоптанная земля как на референсе: открытая поляна в центре и тропы от арок к центру (плюс две боковые тропки), без случайных пятен
        static bool DirtValid(Vector2 w)
        {
            Vector2 d = w - C2;
            if (d.magnitude > Rb(AngOf(d)) - 1.0f) return false;
            if (!F.Inside(w.x, w.y) || F.EdgeDistance(w.x, w.y) < 1.2f) return false;
            if (F.IsWater(w.x, w.y) || F.IsFord(w.x, w.y) || F.WaterDistance(w.x, w.y) < 0.6f) return false;
            return F.Slope(w.x, w.y) <= 22f;
        }

        static int dirtSeed;
        static void DirtBlob(Vector2 w, float rx, float rz, bool avoidTrees)
        {
            if (!DirtValid(w)) return;
            if (avoidTrees && NearTree(w, rx * 0.8f + 0.5f)) return;
            dirtSeed += 3;
            D.Blob(0, Loc(w) + Vector3.up * 0.05f, new Vector3(rx, 0.03f, rz), 3300 + dirtSeed, 1, 0.22f, -1);
        }

        static void DirtPath(Vector2 from, Vector2 to, float width, bool avoidTrees)
        {
            float len = Vector2.Distance(from, to);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.4f));
            Vector2 dir = (to - from).normalized, side = new Vector2(-dir.y, dir.x);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float taper = i == n ? 0.7f : 1f;                                    // конец тропки сужается
                Vector2 w = Vector2.Lerp(from, to, t) + side * Mathf.Sin(t * 5.2f + from.x) * 0.45f;
                float k = width * taper * (0.85f + 0.3f * TreeMB.H(3400, i + Mathf.RoundToInt(from.x * 7f)));
                DirtBlob(w, k, k * 0.9f, avoidTrees);
                // кромка: мелкие плоские камешки по краям тропы
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float h = TreeMB.H(3500 + sd, i + Mathf.RoundToInt(from.y * 5f));
                    if (h > 0.55f) continue;
                    Vector2 q = w + side * sd * (k * (0.95f + 0.25f * h));
                    if (!DirtValid(q) || NearTree(q, 0.9f)) continue;
                    float sz = 0.10f + 0.12f * TreeMB.H(3510 + sd, i);
                    D.Blob(1, Loc(q) + Vector3.up * (sz * 0.35f), new Vector3(sz * 1.3f, sz * 0.6f, sz), 3600 + dirtSeed++, 0, 0.30f, -1);
                }
            }
        }

        // тропа, ведущая к вещи: углы совпадают с местами в ChopCamp (навес, поленница, телега, брёвна)
        static readonly float[] BranchTh = { 160f, -150f, -62f, 110f, -100f };
        static readonly float[] BranchEnd = { 6.3f, 7.0f, 4.4f, 6.8f, 6.6f };

        static void Ground(System.Random rng)
        {
            dirtSeed = 0;
            // центральная поляна: три перекрывающихся овала
            DirtBlob(C2 + new Vector2(0.4f, 0.2f), 3.7f, 3.2f, false);
            DirtBlob(C2 + new Vector2(-2.4f, 1.6f), 2.6f, 2.3f, true);
            DirtBlob(C2 + new Vector2(2.2f, -1.8f), 2.7f, 2.3f, true);
            // главные тропы от арок к центру
            for (int g = 0; g < gates.Count; g++) DirtPath(gates[g], C2, 1.6f, false);
            // ответвления к навесу, поленнице, телеге и брёвнам (как в референсе: от центра веером)
            float g0 = AngOf(gates[0] - C2);
            for (int k = 0; k < BranchTh.Length; k++)
            {
                float a = g0 + BranchTh[k] * Mathf.Deg2Rad;
                DirtPath(C2 + Dir2(a) * 3.0f, C2 + Dir2(a) * BranchEnd[k], 1.05f, true);
            }
        }

        static void Bush(Vector2 w, float k, int seed, int kind)
        {
            Vector3 p = Loc(w);
            int main = kind == 1 ? 1 : 0;
            B.Blob(main, p + new Vector3(0f, 0.42f * k, 0f), new Vector3(0.85f, 0.62f, 0.85f) * k, seed, 1, 0.30f, 2);
            B.Blob(main, p + new Vector3(0.58f * k, 0.30f * k, 0.25f * k), new Vector3(0.50f, 0.40f, 0.50f) * k, seed + 1, 1, 0.30f, 2);
            B.Blob(main, p + new Vector3(-0.48f * k, 0.28f * k, -0.32f * k), new Vector3(0.45f, 0.36f, 0.45f) * k, seed + 2, 1, 0.30f, 2);
            if (kind == 2)
                for (int i = 0; i < 9; i++)
                {
                    float a = TreeMB.H(seed + 7, i) * Mathf.PI * 2f, up = 0.35f + 0.55f * TreeMB.H(seed + 9, i);
                    float r = 0.78f * Mathf.Sqrt(1f - up * up) * k;
                    B.Blob(3, p + new Vector3(Mathf.Cos(a) * r, 0.18f * k + up * 0.62f * k, Mathf.Sin(a) * r), new Vector3(0.10f, 0.08f, 0.10f), seed + 20 + i, 0, 0.3f, -1);
                }
            nBush++;
        }

        static void Tuft(Vector2 w, int seed, int color)
        {
            Vector3 p = Loc(w);
            for (int k = 0; k < 6; k++)
            {
                float a = TreeMB.H(seed, k) * Mathf.PI * 2f, rr = 0.32f * Mathf.Sqrt(TreeMB.H(seed + 1, k));
                Vector3 q = p + new Vector3(Mathf.Sin(a) * rr, 0f, Mathf.Cos(a) * rr);
                float h = 0.22f + 0.12f * TreeMB.H(seed + 2, k);
                Cm.Tube(0, new[] { q + Vector3.down * 0.02f, q + Vector3.up * h }, new[] { 0.022f, 0.016f }, 3, false);
                Cm.Blob(1 + color, q + Vector3.up * (h + 0.03f), new Vector3(0.11f, 0.08f, 0.11f) * (0.8f + 0.5f * TreeMB.H(seed + 3, k)), seed + 4 + k, 0, 0.25f, -1);
            }
            nTuft++;
        }

        static void MossyRock(Vector2 w, float size, int seed)
        {
            Vector3 p = Loc(w);
            float h = size * 0.7f;
            D.Blob(1, p + Vector3.up * (h * 0.5f), new Vector3(size, h, size * 0.9f), seed, 1, 0.30f, -1);
            D.Blob(1, p + new Vector3(size * 0.9f, h * 0.2f, size * 0.3f), new Vector3(size * 0.45f, h * 0.4f, size * 0.4f), seed + 1, 0, 0.35f, -1);
            AddCol("Col_ZoneRock", p + Vector3.up * (h * 0.5f), Quaternion.identity, new Vector3(size * 1.7f, h * 1.1f, size * 1.6f));
            nRock++;
        }

        static void Plants()
        {
            int n = posts.Length;
            for (int k = 0; k < n; k++)
            {
                var p = posts[k];
                if (!p.exists) continue;
                bool nearGate = false;
                for (int g = 0; g < gates.Count; g++) if (Vector2.Distance(p.w, gates[g]) < 3.4f) nearGate = true;
                if (nearGate) continue;
                Vector2 outv = (p.w - C2).normalized, tan = new Vector2(-outv.y, outv.x);

                // снаружи: группа «куст + цветы (+ камень)»
                if (TreeMB.H(710, k) < 0.55f)
                {
                    Vector2 bp = p.w + outv * (1.0f + 0.9f * TreeMB.H(711, k)) + tan * ((TreeMB.H(712, k) - 0.5f) * 1.6f);
                    if (PlantOk(bp, 1.6f))
                    {
                        int nb = 1 + (TreeMB.H(713, k) < 0.45f ? 1 : 0);
                        for (int i = 0; i < nb; i++)
                        {
                            Vector2 q = bp + tan * (i * 1.15f);
                            if (i > 0 && !PlantOk(q, 1.6f)) continue;
                            float rr = TreeMB.H(714 + i, k);
                            Bush(q, 0.8f + 0.5f * TreeMB.H(716 + i, k), 730 + k * 7 + i * 3, rr < 0.35f ? 2 : rr < 0.68f ? 0 : 1);
                        }
                        int col = k % 3;
                        int nt = 3 + (TreeMB.H(718, k) < 0.5f ? 1 : 0);
                        for (int i = 0; i < nt; i++)
                        {
                            float a = TreeMB.H(720 + i, k) * Mathf.PI * 2f, r = 0.55f + 0.9f * TreeMB.H(725 + i, k);
                            Vector2 q = bp + Dir2(a) * r;
                            if (!PlantOk(q, 0.9f)) continue;
                            Tuft(q, 800 + k * 11 + i * 3, col);
                        }
                        if (TreeMB.H(740, k) < 0.20f)
                        {
                            Vector2 q = bp - tan * 1.4f + outv * 0.5f;
                            if (PlantOk(q, 1.4f)) MossyRock(q, 0.38f + 0.26f * TreeMB.H(741, k), 760 + k);
                        }
                    }
                }

                // внутри у забора: редкие цветы, чтобы забор не стоял «голым»
                if (TreeMB.H(750, k) < 0.35f)
                {
                    Vector2 ip = p.w - outv * (0.85f + 0.5f * TreeMB.H(751, k));
                    int col = (k + 1) % 3;
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 q = ip + tan * (i * 0.7f);
                        if (PlantOk(q, 1.0f)) Tuft(q, 900 + k * 7 + i * 3, col);
                    }
                }
            }
        }

        // ============================================================ сцена
        static void AddCol(string name, Vector3 local, Quaternion q, Vector3 size)
        {
            if (!FenceSolid && name != "Col_ZoneRock" && name != "Col_GatePost") return;
            var go = new GameObject(name);
            go.transform.SetParent(Cols, false);
            go.transform.localPosition = local;
            go.transform.localRotation = q;
            go.AddComponent<BoxCollider>().size = size;
            nCol++;
        }

        static void AddMesh(string name, TreeMB mb, string asset, Material[] mats, bool shadows)
        {
            if (mb.TotalTris == 0) return;
            Mesh mesh = ForestMats.SaveMesh(mb.Build(asset), Dir + "/" + asset + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(R, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            int subs = Mathf.Max(1, mesh.subMeshCount);
            var arr = new Material[subs];
            for (int i = 0; i < subs; i++) arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
            mr.sharedMaterials = arr;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        static Transform NewRoot(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}
#endif

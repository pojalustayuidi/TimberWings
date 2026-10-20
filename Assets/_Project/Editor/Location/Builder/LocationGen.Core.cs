#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Builder/LocationGen.Core.cs
// Генератор, часть 1: поля, запуск, раскладка площадок, тропы, поля расстояний.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EditorAssets;

public partial class LocationBuilder
{
    sealed partial class Gen
    {
        readonly Settings s;
        readonly float ws, half, cell;
        readonly int N;
        readonly System.Random rng;
        readonly float ox, oz;

        readonly List<Pad> pads = new List<Pad>();
        readonly List<Trail> trails = new List<Trail>();
        readonly List<string> warnings = new List<string>();
        readonly HashSet<string> touched = new HashSet<string>();
        readonly Dictionary<int, MeshBatch> batches = new Dictionary<int, MeshBatch>();
        readonly List<KeyValuePair<Vector3, float>> bigRocks = new List<KeyValuePair<Vector3, float>>();

        Pad forest, bridge;
        float[] sdTrail, sdPad, hu, dFlatArr, tCenter;
        float riverBase;
        float waterLevel;
        float minM, hmM;
        Transform root;
        Material[] envMats;

        Geo box, cyl6, cone4, cone5;
        Geo[] ico, icoS;

        public Gen(Settings settings)
        {
            s = settings;
            ws = s.worldScale;
            half = s.terrainSize * 0.5f;
            N = s.heightmapRes;
            cell = s.terrainSize / (N - 1);
            rng = new System.Random(s.seed);
            ox = 1000f + (float)rng.NextDouble() * 5000f;
            oz = 1000f + (float)rng.NextDouble() * 5000f;
            box = Prim.Box();
            cyl6 = Prim.Cyl(6);
            cone4 = Prim.Cone(4);
            cone5 = Prim.Cone(5);
            ico = new[] { Prim.Ico(0), Prim.Ico(1), Prim.Ico(2) };
            icoS = new[] { Prim.IcoSub(0, 0.22f), Prim.IcoSub(1, 0.26f), Prim.IcoSub(2, 0.20f) };
        }

        // ---------------------------------------------------------- utils

        static float SS(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        float Perlin(float x, float z, float off)
        {
            return Mathf.PerlinNoise(x + ox + off, z + oz + off);
        }

        float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        Vector3 W(Vector2 p, float y) { return new Vector3(p.x * ws, y * ws, p.y * ws); }

        float FieldAt(float[] f, float x, float z)
        {
            float fx = Mathf.Clamp((x + half) / cell, 0f, N - 1.001f);
            float fz = Mathf.Clamp((z + half) / cell, 0f, N - 1.001f);
            int i0 = (int)fx, j0 = (int)fz;
            float tx = fx - i0, tz = fz - j0;
            float a = f[j0 * N + i0], b = f[j0 * N + i0 + 1], c = f[(j0 + 1) * N + i0], d = f[(j0 + 1) * N + i0 + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        float HeightU(float x, float z) { return FieldAt(hu, x, z); }

        void Progress(string msg, float t) { EditorUtility.DisplayProgressBar("Location Builder", msg, t); }

        static void MarkStatic(GameObject go)
        {
            // геометрия уже слита в чанки, статик-батчинг не нужен (иначе дубль памяти)
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        // контекст для модулей (река, скалы, центр, лес, поляна)
        LocationCtx MakeCtx()
        {
            var c = new LocationCtx();
            c.s = s; c.root = root; c.ws = ws; c.half = half;
            c.centerRadius = s.centerRadius; c.terrainSize = s.terrainSize;
            c.maxHeight = s.maxHeight; c.seed = s.seed;
            c.waterLevel = waterLevel; c.riverWidth = s.riverWidth;
            c.riverBank = s.riverBank; c.riverDepth = s.riverDepth;
            c.riverX = RiverX; c.heightU = HeightU; c.distFlat = DistFlat; c.inGlade = InGlade;
            c.edgeIn = EdgeIn; c.headMask = HeadMask; c.riverSrcZ = RiverSrcZ();
            if (bridge != null && s.hasBridge)   // v9: без hasBridge мост не строится, маркеры не создаются
            {
                c.hasBridge = true;
                c.bridgePos = bridge.pos;
                c.bridgeSide = Mathf.Sign(RiverX(bridge.pos.y) - bridge.pos.x);
            }
            foreach (var p in pads)
            {
                if (p.branch) continue;
                c.trailDirs.Add(p.pos.normalized);
                c.trailColors.Add(p.def.gizmoColor);
            }
            if (forest != null) { c.hasForest = true; c.forestPos = forest.pos; c.forestRad = forest.rad; }
            return c;
        }

        // ---------------------------------------------------------- run

        public void Run()
        {
            // пакетный режим: ассеты импортируются один раз в конце, а не на каждый CreateAsset
            AssetDatabase.StartAssetEditing();
            try { RunInner(); }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LocationModules.Run(MakeCtx());
            if (GameObject.Find("ForestTrails") != null)
                Debug.Log("[LocationBuilder] Локация пересобрана: тропы леса стёрты с Terrain. Tools → Forest → Лес → «1. Создать зону леса», затем «Тропы + пересобрать лес».");
        }

        void RunInner()
        {
            Progress("Подготовка", 0.02f);
            RemoveRoot();
            EnsureFolder(AssetDir + "/Meshes");
            EnsureFolder(AssetDir + "/Materials");
            EnsureFolder(AssetDir + "/Textures");

            SolveLayout();
            Progress("Тропы", 0.08f);
            BuildTrails();
            Progress("Поля расстояний", 0.14f);
            ComputeFields();
            Progress("Рельеф", 0.28f);
            ComputeHeights();

            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Build Location");
            root = rootGo.transform;
            root.position = s.origin;

            Progress("Terrain и раскраска", 0.45f);
            BuildTerrain();
            CreateMaterials();
            Progress("Маркеры", 0.6f);
            BuildMarkers();
            Progress("Окружение", 0.75f);
            BuildEnvironment();
            Progress("Сборка мешей", 0.92f);
            FlushChunks();
            BuildBoundary();
            SetupScene();
            CleanupMeshes();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            foreach (var w in warnings) Debug.LogWarning("[LocationBuilder] " + w);
            Debug.Log("[LocationBuilder] Готово. seed=" + s.seed + ", scale=" + ws + ", троп: " + trails.Count);
        }

        // ---------------------------------------------------------- layout

        void SolveLayout()
        {
            pads.Clear();
            foreach (var d in s.pads)
            {
                var p = new Pad { def = d, round = d.round };
                p.hw = d.round ? d.size.x : d.size.x * 0.5f;
                p.hd = d.round ? d.size.x : d.size.y * 0.5f;
                p.rad = d.round ? d.size.x : Mathf.Max(p.hw, p.hd);
                pads.Add(p);
            }

            for (int i = 0; i < pads.Count; i++)
            {
                var p = pads[i];
                float a = p.def.angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float dist = Mathf.Max(p.def.distance, s.centerRadius + 0.5f + p.hd);
                float maxR = half - p.rad - 3f;

                for (int it = 0; it < 120; it++)
                {
                    Vector2 pos = dir * dist;
                    bool ok = true;
                    for (int j = 0; j < i; j++)
                    {
                        if ((pos - pads[j].pos).magnitude < p.rad + pads[j].rad + s.minPadGap) { ok = false; break; }
                    }
                    if (ok) break;
                    dist += 0.5f;
                }
                while (dist > s.centerRadius + 1f && Mathf.Max(Mathf.Abs(dir.x * dist), Mathf.Abs(dir.y * dist)) > maxR)
                    dist -= 0.5f;

                if (Mathf.Abs(dist - p.def.distance) > 0.6f)
                    warnings.Add("Площадка " + p.def.name + ": расстояние скорректировано " + p.def.distance.ToString("0.#") + " -> " + dist.ToString("0.#"));

                p.pos = dir * dist;
                p.Orient(-dir);

                if (p.def.round && forest == null) forest = p;
                if (p.def.name == "Bridge") bridge = p;
            }

            float hw = s.riverWidth * 0.5f;
            if (bridge != null)
                riverBase = bridge.pos.x - bridge.hd - 0.8f - s.riverBank - hw;   // площадка Bridge остаётся (её ждут StoryFlow/BuildStageLink)
            else
                riverBase = -half * 0.85f;
            riverBase += s.riverOffsetX;
            waterLevel = (s.riverGorge ? -0.62f : -0.35f) * s.riverDepth;   // ущелье: вода глубоко под бровкой
        }

        float RiverX(float z)
        {
            // мягкий меандр: гасится у z=0 (площадка Bridge), растёт к северу и югу
            float mea = s.riverMeander * Mathf.Sin(z * 0.027f + 0.4f) * SS(8f, 30f, Mathf.Abs(z));
            return riverBase + s.riverCurve * Mathf.Sin(z * 0.05f) + 0.4f * s.riverCurve * Mathf.Sin(z * 0.11f) + mea;
        }

        // 1 = река есть на этом z (v9: по всей длине острова)
        // расстояние до края острова внутрь, u (заполняет рельеф «Выразительный»; без него считаем от южного края террейна)
        float[] edgeField;
        float EdgeIn(float x, float z) { return edgeField != null ? FieldAt(edgeField, x, z) : half + z; }

        float RiverSrcZ() { return Mathf.Min(s.riverSourceZ, half - 8f); }
        float RiverMask(float z) { return s.riverFullLength ? 1f - SS(RiverSrcZ() - 0.5f, RiverSrcZ() + ((s.riverGorge && s.riverHead) ? 2.6f : 5f), z) : 1f - SS(48f, 60f, Mathf.Abs(z)); }
        // у истока ручей узкий (40%), к 28 u ниже по течению доходит до полной ширины
        float RiverTaper(float z) { return s.riverFullLength ? Mathf.Lerp(0.4f, 1f, SS(RiverSrcZ(), RiverSrcZ() - 28f, z)) : 1f; }

        // Прорезает долину: широкое пологое понижение + русло с U-профилем и неровным дном.
        // keepValley 0..1 - насколько защитить землю от общего понижения (тропы, площадки); padCore 0..1 - ядро площадки, которое русло не трогает.
        // Дальний склон (dx<0, к кромке острова) пологий (+riverSlope), склон со стороны площадок - как раньше.
        float CarveRiver(float x, float z, float h, float keepValley, float padCore)
        {
            h = CarveRiverCore(x, z, h, keepValley, padCore);
            if (s.riverGorge && s.riverHead && s.riverFullLength) h = HeadRidge(x, z, h, padCore);
            return h;
        }

        // Гора у истока. Здесь только «ядро» в высотах Terrain: узкий массив с крутыми боками, который держит коллизию и
        // скрыт внутри слоистой меш-скалы (LocationHeadMountain). Без меш-скалы (buildHeadMountain=false) ядро растёт до headHeight.
        // Массив идёт вдоль русла от истока на север до края острова и замыкает его с краем: исток обойти нельзя, реку перейти можно только по мосту.
        float HeadRidge(float x, float z, float h, float padCore)
        {
            float z0 = RiverSrcZ() + 0.6f;                         // подножие лба (вода кончается примерно здесь)
            if (z < z0 - 1f) return h;
            float dxr = Mathf.Abs(x - RiverX(z));
            float inner = s.buildHeadMountain ? 1.3f : 0f;         // ядро уже меша: бока не торчат из скалы
            float rw = (s.headHalfWidth - inner) * (0.85f + 0.3f * Perlin(x * 0.10f, z * 0.10f, 670f));
            float jag = (Perlin(x * 0.25f, z * 0.25f, 675f) - 0.5f) * 1.2f * s.gorgeRough;
            float lat = 1f - SS(rw, rw + 1.6f, dxr + jag);         // бока почти отвесные (~1.6 u по ширине)
            float run = SS(z0 - 1f, z0 + 1.6f, z);                 // лоб: подъём на ~2.6 u
            float w = lat * run * (1f - padCore);
            if (w <= 0f) return h;
            float want = s.buildHeadMountain ? s.headHeight * s.headCoreFrac : Mathf.Min(s.headHeight, s.maxHeight - 0.5f);
            float top = want * (0.9f + 0.2f * Perlin(x * 0.08f, z * 0.08f, 680f));
            return Mathf.Lerp(h, Mathf.Max(h, top), w);
        }

        // 0..1: след горы (с запасом под контрфорсы). Нужен деревьям, разметке слоёв и модулю меш-скалы.
        float HeadMask(float x, float z)
        {
            if (!(s.riverGorge && s.riverHead && s.riverFullLength)) return 0f;
            float z0 = RiverSrcZ() + 0.6f;
            if (z < z0 - 3f) return 0f;
            float hw = s.headHalfWidth + 1.4f;
            float dxr = Mathf.Abs(x - RiverX(z));
            return (1f - SS(hw, hw + 2f, dxr)) * SS(z0 - 3f, z0 - 1f, z);
        }

        float CarveRiverCore(float x, float z, float h, float keepValley, float padCore)
        {
            float m = RiverMask(z);
            if (m <= 0f) return h;
            float taper = RiverTaper(z);
            float hw = s.riverWidth * 0.5f * taper;
            float dx = x - RiverX(z);
            float d = Mathf.Abs(dx);
            if (s.riverGorge) return CarveGorge(x, z, h, keepValley, padCore, m, taper, hw, dx, d);
            float bank = s.riverBank + (dx < 0f ? s.riverSlope : 0f);
            float v = (1f - SS(hw + bank, hw + bank + s.valleyReach, d)) * s.valleyCut * m * (1f - keepValley);
            float floorH = -0.2f * s.riverDepth;                 // общее понижение не уходит ниже уровня воды
            if (h > floorH) h = Mathf.Max(floorH, h - v);
            float c = (1f - SS(hw, hw + bank, d)) * m * (1f - padCore);
            if (c > 0f)
            {
                float bed = -s.riverDepth * (0.45f + 0.55f * taper) * (1f - 0.35f * SS(0f, hw, d)) * (0.9f + 0.2f * Perlin(x * 0.12f, z * 0.12f, 610f));
                h = Mathf.Lerp(h, bed, c);
            }
            return h;
        }

        // Ущелье: плоское скруглённое дно, отвесные стенки (двойной smoothstep), неровная бровка.
        // Дно лежит на 0.4..1.7 u ниже воды, вода на 0.62*riverDepth ниже берега, поэтому берега видны как обрыв и мост нужен.
        // У края острова (юг) дно поднимается к воде и стенки расходятся воронкой: получается порог, с которого вода падает (IslandWaterfall).
        const float GorgeFunnel = 1.5f;

        float CarveGorge(float x, float z, float h, float keepValley, float padCore, float m, float taper, float hw, float dx, float d)
        {
            float rough = s.gorgeRough;
            float nWall = Perlin(x * 0.17f, z * 0.17f, 640f);                    // 0..1, меняет ширину стенки вдоль русла
            float nLip = Perlin(x * 0.31f, z * 0.31f, 655f) - 0.5f;             // мелкая неровность бровки
            float lip = 1f - SS(2.5f, s.gorgeLipLength, EdgeIn(x, z));          // 1 у ближайшего края острова (по его контуру)
            float wallW = (dx < 0f ? s.gorgeFarWall : s.gorgeWall) * (1f - 0.35f * rough + 0.7f * rough * nWall);
            wallW *= Mathf.Lerp(1f, GorgeFunnel, lip);                           // небольшое расширение перед водопадом

            // лёгкое понижение земли вокруг ущелья, чтобы бровка читалась (как valleyCut, но от внешней кромки стенки)
            float v = (1f - SS(hw + wallW, hw + wallW + s.valleyReach, d)) * s.valleyCut * m * (1f - keepValley);
            float floorH = -0.2f * s.riverDepth;
            if (h > floorH) h = Mathf.Max(floorH, h - v);

            float t01 = Mathf.Clamp01((taper - 0.4f) / 0.6f);
            float depthAtBed = Mathf.Lerp(0.4f, 1.7f, t01);                      // толща воды над дном, u
            float bed = waterLevel - depthAtBed * (0.9f + 0.2f * Perlin(x * 0.12f, z * 0.12f, 610f));
            bed = Mathf.Lerp(bed, waterLevel - 0.4f, lip * 0.9f);                // порог: вода остаётся (>0.05), дно у края мелкое
            bed += 0.5f * SS(hw * 0.5f, hw, d);                                  // дно скруглено к подножью стенки

            float dd = d + nLip * 0.8f * rough;                                  // рваная бровка
            float t = Mathf.Clamp01((dd - hw) / Mathf.Max(0.3f, wallW));
            float w = SS(0f, 1f, SS(0f, 1f, t));                                 // 0 на дне, 1 на бровке, середина крутая
            float k = (1f - w) * m * (1f - padCore);
            if (k > 0f) h = Mathf.Lerp(h, bed, k);
            return h;
        }

        // ---------------------------------------------------------- trails

        static Vector2 CR(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void BuildTrails()
        {
            trails.Clear();
            Vector2 centerStartDir;
            // тропы от центра
            foreach (var p in pads)
            {
                bool isBranch = !string.IsNullOrEmpty(p.def.parent) && pads.Exists(q => q.def.name == p.def.parent && q != p);
                if (isBranch) continue;
                centerStartDir = p.pos.normalized;
                Vector2 start = centerStartDir * (s.centerRadius - 1f);
                p.trail = MakeTrail(p, start, centerStartDir, false, "Trail_" + p.def.name);
                trails.Add(p.trail);
            }
            // ответвления
            foreach (var p in pads)
            {
                if (string.IsNullOrEmpty(p.def.parent)) continue;
                Pad par = pads.Find(q => q.def.name == p.def.parent && q != p);
                if (par == null || par.trail == null) continue;
                var pt = par.trail;
                int fi = Mathf.RoundToInt(p.def.forkDistance - (s.centerRadius - 1f));
                fi = Mathf.Clamp(fi, 2, Mathf.Max(2, pt.p.Count - 8));
                Vector2 fork = pt.p[fi];
                Vector2 tan = (pt.p[Mathf.Min(fi + 1, pt.p.Count - 1)] - pt.p[fi - 1]).normalized;
                p.branch = true;
                p.fork = fork;
                p.forkTan = tan;
                p.Orient(fork - p.pos);
                p.trail = MakeTrail(p, fork, tan, true, "Trail_" + p.def.name);
                trails.Add(p.trail);
            }
        }

        // via (необязательно): промежуточная точка, через которую тропа обязана пройти (изгиб главной тропы леса)
        Trail MakeTrail(Pad target, Vector2 start, Vector2 startDir, bool branch, string nm, Vector2? via = null)
        {
            var tr = new Trail { name = nm, target = target };
            Vector2 entry = target.entry, fwd = target.fwd;
            var ctrl = new List<Vector2> { start };
            float direct = Vector2.Distance(start, entry);
            if (direct > 12f)
            {
                Vector2 a = start;
                if (branch) { a = start + startDir * 6f; ctrl.Add(a); }
                float appr = Mathf.Min(5f, direct * 0.3f);
                Vector2 b = entry + fwd * appr;
                if (via.HasValue)
                {
                    Vector2 v = via.Value;
                    int n1 = Mathf.FloorToInt(Vector2.Distance(a, v) / 8f);
                    for (int k = 1; k <= n1; k++) ctrl.Add(Vector2.Lerp(a, v, k / (float)(n1 + 1)));
                    ctrl.Add(v);
                    int n2 = Mathf.FloorToInt(Vector2.Distance(v, b) / 8f);
                    for (int k = 1; k <= n2; k++) ctrl.Add(Vector2.Lerp(v, b, k / (float)(n2 + 1)));
                }
                else
                {
                    int n = Mathf.FloorToInt(Vector2.Distance(a, b) / 8f);
                    for (int k = 1; k <= n; k++) ctrl.Add(Vector2.Lerp(a, b, k / (float)(n + 1)));
                }
                ctrl.Add(b);
            }
            ctrl.Add(entry);

            int m = ctrl.Count;
            Vector2 g0 = ctrl[0] - (ctrl[1] - ctrl[0]);
            Vector2 gN = ctrl[m - 1] + (ctrl[m - 1] - ctrl[m - 2]);
            var dense = new List<Vector2>();
            for (int k = 0; k < m - 1; k++)
            {
                Vector2 p0 = k == 0 ? g0 : ctrl[k - 1];
                Vector2 p1 = ctrl[k], p2 = ctrl[k + 1];
                Vector2 p3 = k + 2 < m ? ctrl[k + 2] : gN;
                for (int st = 0; st < 12; st++) dense.Add(CR(p0, p1, p2, p3, st / 12f));
            }
            dense.Add(ctrl[m - 1]);

            // равномерная пересэмплировка шагом 1 u
            var pts = new List<Vector2> { dense[0] };
            float acc = 0f, nextS = 1f;
            for (int k = 1; k < dense.Count; k++)
            {
                float seg = (dense[k] - dense[k - 1]).magnitude;
                if (seg < 1e-5f) continue;
                while (acc + seg >= nextS)
                {
                    float t = (nextS - acc) / seg;
                    pts.Add(Vector2.Lerp(dense[k - 1], dense[k], t));
                    nextS += 1f;
                }
                acc += seg;
            }
            if (Vector2.Distance(pts[pts.Count - 1], entry) > 0.3f || pts.Count < 2) pts.Add(entry);
            else pts[pts.Count - 1] = entry;

            int cnt = pts.Count;
            var cum = new float[cnt];
            for (int i = 1; i < cnt; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            float len = cum[cnt - 1];

            // изгибы
            float seedOff = (float)rng.NextDouble() * 500f;
            var np = new Vector2[cnt];
            for (int i = 0; i < cnt; i++)
            {
                Vector2 tan = (pts[Mathf.Min(i + 1, cnt - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 nrm = new Vector2(-tan.y, tan.x);
                float env = SS(0f, branch ? 10f : 6f, cum[i]) * SS(0f, 6f, len - cum[i]);
                float off = (Perlin(cum[i] * 0.07f, seedOff, 0f) * 2f - 1f) * s.trailMeander * env;
                np[i] = pts[i] + nrm * off;
            }

            // отталкивание от чужих площадок
            for (int i = 0; i < cnt; i++)
            {
                Vector2 p = np[i];
                foreach (var q in pads)
                {
                    if (q == target) continue;
                    float R = q.rad + 3.5f;
                    Vector2 dv = p - q.pos;
                    float dm = dv.magnitude;
                    if (dm >= R || dm < 1e-3f) continue;
                    float w = SS(0f, 8f, cum[i]) * SS(0f, 8f, len - cum[i]);
                    if (branch) w *= SS(0.05f * len, 0.3f * len, cum[i]);
                    p += dv / dm * (R - dm) * w;
                }
                np[i] = p;
            }

            // сглаживание, концы фиксированы
            for (int pass = 0; pass < 3; pass++)
            {
                var tmp = (Vector2[])np.Clone();
                for (int i = 1; i < cnt - 1; i++) np[i] = 0.25f * tmp[i - 1] + 0.5f * tmp[i] + 0.25f * tmp[i + 1];
            }

            float L = 0f;
            for (int i = 1; i < cnt; i++) L += (np[i] - np[i - 1]).magnitude;
            float run = 0f;
            float baseHalf = s.trailWidth * 0.5f;
            float padHalf = target.round ? 3.0f : s.trailWidthAtPad * 0.5f;
            float widen = target.round ? 14f : 8f;
            for (int i = 0; i < cnt; i++)
            {
                if (i > 0) run += (np[i] - np[i - 1]).magnitude;
                tr.p.Add(np[i]);
                tr.half.Add(Mathf.Lerp(baseHalf, padHalf, SS(L - widen, L, run)));
            }
            tr.len = L;
            return tr;
        }

        // ---------------------------------------------------------- fields

        float PadSD(Pad q, Vector2 p)
        {
            Vector2 d = p - q.pos;
            float lx = Mathf.Abs(Vector2.Dot(d, q.right)), lz = Mathf.Abs(Vector2.Dot(d, q.fwd));
            float rc = Mathf.Min(1f, Mathf.Min(q.hw, q.hd) * 0.5f);
            float qx = lx - (q.hw - rc), qz = lz - (q.hd - rc);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qz), 0f) - rc;
        }

        void Stamp(Vector2 a, Vector2 b, float ha, float hb, float reach)
        {
            float minx = Mathf.Min(a.x, b.x) - reach, maxx = Mathf.Max(a.x, b.x) + reach;
            float minz = Mathf.Min(a.y, b.y) - reach, maxz = Mathf.Max(a.y, b.y) + reach;
            int i0 = Mathf.Max(0, Mathf.FloorToInt((minx + half) / cell));
            int i1 = Mathf.Min(N - 1, Mathf.CeilToInt((maxx + half) / cell));
            int j0 = Mathf.Max(0, Mathf.FloorToInt((minz + half) / cell));
            int j1 = Mathf.Min(N - 1, Mathf.CeilToInt((maxz + half) / cell));
            Vector2 ab = b - a;
            float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int j = j0; j <= j1; j++)
            {
                float z = -half + j * cell;
                for (int i = i0; i <= i1; i++)
                {
                    float x = -half + i * cell;
                    Vector2 p = new Vector2(x, z);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float dc = (p - (a + ab * t)).magnitude;
                    float d = dc - Mathf.Lerp(ha, hb, t);
                    int idx = j * N + i;
                    if (d < sdTrail[idx]) { sdTrail[idx] = d; tCenter[idx] = dc; }
                }
            }
        }

        void ComputeFields()
        {
            ComputeFieldsCore();
            StampForestDiscs();   // поляны лесных троп (LocationGen.ForestPaint.cs)
        }

        void ComputeFieldsCore()
        {
            sdTrail = new float[N * N];
            sdPad = new float[N * N];
            tCenter = new float[N * N];
            for (int i = 0; i < sdTrail.Length; i++) { sdTrail[i] = 999f; sdPad[i] = 999f; tCenter[i] = 999f; }

            foreach (var tr in trails)
                for (int k = 0; k < tr.p.Count - 1; k++)
                    Stamp(tr.p[k], tr.p[k + 1], tr.half[k], tr.half[k + 1], 30f);

            foreach (var q in pads)
            {
                if (q.round) continue;
                for (int j = 0; j < N; j++)
                {
                    float z = -half + j * cell;
                    for (int i = 0; i < N; i++)
                    {
                        float d = PadSD(q, new Vector2(-half + i * cell, z));
                        int idx = j * N + i;
                        if (d < sdPad[idx]) sdPad[idx] = d;
                    }
                }
            }
        }
    }
}
#endif

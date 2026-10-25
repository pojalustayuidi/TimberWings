#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestGladeBeauty.cs (Editor)  НОВЫЙ ФАЙЛ
// A5 v3: красивая поляна рубки у метки PAD_Forest. Tools/BuilderSim/Лес/Красота леса/8. Поляна рубки: уют и красота (приоритет 237).
// Безопасно жать повторно: корень «ChopGladeDecor» в IslandV2/_Manual пересоздаётся целиком.
//
// Что ставит (всё процедурно, TreeMB, материалы TreeToon, как у остального леса):
//   • поленница под навесом (2 столба, скатная крыша), колода с воткнутым топором и щепой, козлы с брёвнами и спилами;
//   • 2 лавки из доски на пнях, 4 пенька-«сиденья», круг плоских камней вокруг места игрока (место игрока не занимается);
//   • 3 столба-фонаря с тёплым светом (FireSource, ночью светятся, рядом с игроком дают настоящий свет);
//   • мшистые камни, папоротники, цветочные кустики (розовые, жёлтые, белые), красные грибочки, пятна мха на земле;
//   • рантайм-эффекты: пыль в солнечных лучах, мягкая дымка света, светлячки ночью, листья (Scripts/Forest/GladeVibe.cs).
// Деревья обходит сам: берёт позиции из ForestChop и ForestDecor, а также всех ResourceItem сцены. Не ставит на воду, тропу (ближе 1.4 м),
// ближе 1.5 м к краю острова. Не нашло место для вещи = пропущена (счёт в отчёте).
// Порядок: сначала «4. Лес: расставить рабочие деревья», потом этот пункт (кнопка «0. Всё сразу» делает это сама).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestGladeBeauty
    {
        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const string RootName = "ChopGladeDecor";

        static TreeMB A, B, C;          // A дерево: 0 бревно, 1 торец и ножки грибов, 2 тёмное дерево, 3 железо
                                        // B уют: 0 тёплое стекло, 1 розовый, 2 жёлтый, 3 белый
                                        // C земля: 0 папоротник, 1 мох, 2 красные шляпки, 3 камень
        static IslandField F;
        static Transform R;
        static Vector3 O;               // мировая позиция PAD_Forest; корень стоит тут без поворота, локальные координаты = мировые минус O
        static int skipped, placed, seed;
        static float gateA;             // угол главной арки (x,z), вещи расставляются от входа
        static readonly List<Vector2> trees = new List<Vector2>();   // мировые x, z всех деревьев
        static readonly List<Vector3> used = new List<Vector3>();    // локальные x, z, радиус занятого места
        static readonly List<Vector3> lamps = new List<Vector3>();   // локальные точки фонарей

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/8. Поляна рубки: уют и красота", false, 237)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Поляна рубки: останови Play Mode."); return; }
            var pad = GameObject.Find(ChopZone.PadName);
            if (pad == null) { Debug.LogError("Поляна рубки: в сцене нет метки " + ChopZone.PadName + " (Остров v2 → 2. Собрать блокаут)."); return; }
            var manualGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = manualGo != null ? manualGo.transform : null;

            O = pad.transform.position;
            var L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            F = L != null ? new IslandField(L) : null;
            gateA = (F != null && L != null) ? ChopZone.MainGateAngle(F, L, new Vector2(O.x, O.z)) : 0f;

            // ---- материалы
            var log = ForestMats.GladeLog(false); var logEnd = ForestMats.GladeLogEnd(false);
            var woodD = ForestMats.GroveWoodDark(false); var iron = ForestMats.GroveIron(false);
            var warm = ForestMats.GroveWarm(false); var pink = ForestMats.FlowerPink(false);
            var yellow = ForestMats.FlowerYellow(false); var white = ForestMats.FlowerWhite(false);
            var fern = ForestMats.GladeFern(false); var moss = ForestMats.BushLime(false);
            var red = ForestMats.GrandpaDetail(false)[2]; var rock = ForestMats.Rock(false);
            foreach (var m in new[] { log, logEnd, woodD, iron, warm, pink, yellow, white, fern, moss, red, rock })
                if (m == null) { Debug.LogError("Поляна рубки: не создался материал (проверь шейдер TreeToon: Лес → Tree Toon)."); return; }

            // ---- деревья, которые надо обойти
            trees.Clear();
            foreach (var rootName in new[] { "ForestChop", "ForestDecor", ForestGuardLite.RootName })
            {
                var t = parent != null ? parent.Find(rootName) : null;
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++) { var p = t.GetChild(i).position; trees.Add(new Vector2(p.x, p.z)); }
            }
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsSortMode.None))
            {
                var p = ri.transform.position;
                trees.Add(new Vector2(p.x, p.z));
            }

            // ---- корень
            var old = parent != null ? parent.Find(RootName) : null;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Поляна рубки");
            if (parent != null) rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = O;
            rootGo.transform.rotation = Quaternion.identity;
            R = rootGo.transform;

            A = new TreeMB(); B = new TreeMB(); C = new TreeMB();
            used.Clear(); lamps.Clear(); skipped = 0; placed = 0; seed = 3000;

            // ---- вещи: крупное первым, мелкое потом (мелкое обходит крупное)
            float x, z, y;
            // поленницу и навес даёт ChopCamp (на референсе они одни), тут не дублируем
            if (SpotAt(38f, 5.0f, 1.3f, 110, 1.6f, out x, out z, out y)) ChopBlock(x, y, z);
            if (SpotAt(-34f, 5.6f, 1.6f, 120, 1.6f, out x, out z, out y)) Sawhorse(x, y, z, 25f);
            for (int i = 0; i < 0; i++)   // лавок на референсе нет
                if (Spot(3.6f, 6.2f, 1.3f, 130 + i * 11, 1.6f, out x, out z, out y)) Bench(x, y, z, Mathf.Atan2(-x, -z) * Mathf.Rad2Deg + 90f);
            for (int i = 0; i < 0; i++)   // столбы с фонарями и гирлянды даёт ChopCamp
                if (Spot(4.0f, 6.5f, 0.8f, 150 + i * 13, 1.5f, out x, out z, out y)) LampPost(x, y, z);
            for (int i = 0; i < 2; i++)
                if (SpotAt(i == 0 ? 88f : -118f, i == 0 ? 6.2f : 6.8f, 0.7f, 170 + i * 9, 1.5f, out x, out z, out y)) StumpSeat(x, y, z, 0.30f + 0.04f * (i % 2), 0.34f + 0.06f * (i % 3));
            for (int i = 0; i < 3; i++)
                if (SpotAt(i == 0 ? 58f : i == 1 ? -60f : 178f, 9.0f, 1.0f, 200 + i * 7, 1.4f, out x, out z, out y)) MossyRock(x, y, z, 0.38f + 0.28f * TreeMB.H(205, i), i);
            for (int i = 0; i < 2; i++)
                if (SpotAt(i == 0 ? 120f : -140f, 8.8f, 0.55f, 240 + i * 5, 1.2f, out x, out z, out y)) RedShrooms(x, y, z, i);
            for (int i = 0; i < 6; i++)
                if (SpotAt(i == 0 ? 50f : i == 1 ? -50f : i == 2 ? 100f : i == 3 ? -100f : i == 4 ? 150f : -150f, 9.2f, 0.5f, 280 + i * 3, 1.0f, out x, out z, out y)) Fern(x, y, z, i);
            for (int i = 0; i < 5; i++)
                if (SpotAt(i == 0 ? 22f : i == 1 ? -22f : i == 2 ? 66f : i == 3 ? -70f : 130f, 7.2f, 0.45f, 340 + i * 3, 1.0f, out x, out z, out y)) Flowers(x, y, z, i);
            // круг камней убран: на референсе вокруг места игрока открытая земля
            MossPatches();

            // ---- меши
            EditorAssets.EnsureFolder(Dir);
            AddMesh("Furniture", ForestMats.SaveMesh(A.Build("Glade_Furniture"), Dir + "/Glade_Furniture.asset"), new[] { log, logEnd, woodD, iron }, true);
            AddMesh("Cozy", ForestMats.SaveMesh(B.Build("Glade_Cozy"), Dir + "/Glade_Cozy.asset"), new[] { warm, pink, yellow, white }, false);
            AddMesh("Ground", ForestMats.SaveMesh(C.Build("Glade_Ground"), Dir + "/Glade_Ground.asset"), new[] { fern, moss, red, rock }, false);

            // ---- свет
            int li = 0;
            foreach (var lp in lamps)
            {
                var go = new GameObject("Light_Lantern" + (li++));
                go.transform.SetParent(R, false);
                go.transform.localPosition = lp;
                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Lantern;
                fs.color = new Color(1f, 0.78f, 0.45f);
                fs.intensity = 2.2f; fs.range = 6.5f; fs.simOnly = false;
                fs.glow = true; fs.glowSize = 1.3f;
            }

            // ---- рантайм: пыль, дымка, светлячки, листья
            var vibe = rootGo.AddComponent<GladeVibe>();
            vibe.radius = 9f;

            EditorSceneManager.MarkSceneDirty(R.gameObject.scene);
            Selection.activeGameObject = rootGo;
            var sb = new StringBuilder("Поляна рубки: корень " + RootName + " в (" + O.x.ToString("0.#") + ", " + O.z.ToString("0.#") + "), деревьев обойдено " + trees.Count + ".\n");
            sb.AppendLine("  поставлено вещей: " + placed + ", не нашлось места (деревья, вода, тропа, край): " + skipped + ", фонарей: " + lamps.Count + ".");
            sb.AppendLine("  меши Furniture/Cozy/Ground в " + Dir + ", коллайдеры-коробки у поленницы, колоды, лавок, камней. Место игрока (3 м вокруг метки) свободно.");
            sb.AppendLine("  Дальше: «Остров v2 → 3. Проверить блокаут», посмотри на закате в Play Mode (фонари, светлячки). Если мало вещей: сначала «4. Лес: расставить...» с меньшим числом деревьев или двигай руками.");
            Debug.Log(sb.ToString());
        }

        // ============================================================ помощники
        static bool GY(float x, float z, out float y, float edge, float pathMin)
        {
            Vector3 w = O + new Vector3(x, 0f, z);
            if (F != null)
            {
                if (!F.Inside(w.x, w.z) || F.EdgeDistance(w.x, w.z) < edge || F.IsWater(w.x, w.z) || F.PathDistance(w.x, w.z) < pathMin) { y = 0f; return false; }
                y = F.Height(w.x, w.z) - O.y;
                return true;
            }
            RaycastHit h;
            if (Physics.Raycast(new Vector3(w.x, w.y + 60f, w.z), Vector3.down, out h, 200f, ~0, QueryTriggerInteraction.Ignore)) { y = h.point.y - O.y; return true; }
            y = 0f; return true;
        }

        static bool Blocked(float x, float z, float clear)
        {
            float wx = O.x + x, wz = O.z + z;
            for (int i = 0; i < trees.Count; i++)
            {
                float dx = trees[i].x - wx, dz = trees[i].y - wz, rr = clear + 0.9f;   // 0.9 = запас на ствол
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            for (int i = 0; i < used.Count; i++)
            {
                float dx = used[i].x - x, dz = used[i].y - z, rr = clear + used[i].z;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        // ищет свободное место в кольце rMin..rMax вокруг метки (детерминировано по tag). clear = радиус, который займёт вещь
        // авторское место: угол от арки (градусы) и радиус; рядом ищет с расширением, потом обычный поиск по кольцу
        static bool SpotAt(float thDeg, float r, float clear, int tag, float pathMin, out float sx, out float sz, out float sy)
        {
            for (int i = 0; i < 60; i++)
            {
                float k = i / 60f;
                float da = i == 0 ? 0f : (TreeMB.H(tag, i * 2) - 0.5f) * 2f * Mathf.Lerp(4f, 24f, k);
                float dr = i == 0 ? 0f : (TreeMB.H(tag + 7, i * 2 + 1) - 0.5f) * 2f * Mathf.Lerp(0.4f, 1.8f, k);
                float a = gateA + (thDeg + da) * Mathf.Deg2Rad, rr = r + dr;
                float x = Mathf.Cos(a) * rr, z = Mathf.Sin(a) * rr, y;
                if (!GY(x, z, out y, 1.5f, pathMin)) continue;
                if (Blocked(x, z, clear)) continue;
                used.Add(new Vector3(x, z, clear));
                sx = x; sz = z; sy = y;
                return true;
            }
            return Spot(Mathf.Max(3.3f, r - 2.5f), r + 2.5f, clear, tag, pathMin, out sx, out sz, out sy);
        }

        static bool Spot(float rMin, float rMax, float clear, int tag, float pathMin, out float sx, out float sz, out float sy)
        {
            for (int i = 0; i < 80; i++)
            {
                float a = TreeMB.H(tag, i * 2) * Mathf.PI * 2f;
                float r = Mathf.Lerp(rMin, rMax, TreeMB.H(tag + 7, i * 2 + 1));
                float x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r, y;
                if (!GY(x, z, out y, 1.5f, pathMin)) continue;
                if (Blocked(x, z, clear)) continue;
                used.Add(new Vector3(x, z, clear));
                sx = x; sz = z; sy = y;
                return true;
            }
            skipped++;
            sx = 0f; sz = 0f; sy = 0f;
            return false;
        }

        static void Col(string name, Vector3 local, Quaternion q, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(R, false);
            go.transform.localPosition = local;
            go.transform.localRotation = q;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void AddMesh(string name, Mesh mesh, Material[] mats, bool shadows)
        {
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
            if (name == "Furniture") go.AddComponent<MeshCollider>().sharedMesh = mesh;   // колоды, козлы, пеньки: сквозь них не пройти
        }

        // пенёк: ствол + светлый спил сверху
        static void Stump(Vector3 p, float r, float h)
        {
            A.Tube(0, new[] { p + Vector3.down * 0.10f, p + Vector3.up * h }, new[] { r * 1.1f, r }, 8, false);
            A.Cap(1, p + Vector3.up * (h + 0.004f), r * 0.97f, 8);
        }

        // двусторонний листок (папоротник): основание, изгиб, кончик
        static void Blade(TreeMB m, int sub, Vector3 b, Vector3 dir, float len, float w)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 p1 = b + dir * (len * 0.5f) + Vector3.up * (len * 0.42f);
            Vector3 p2 = b + dir * len + Vector3.up * (len * 0.30f);
            Vector3 a0 = b - side * w, a1 = b + side * w, c0 = p1 - side * (w * 0.9f), c1 = p1 + side * (w * 0.9f);
            Vector3 dn = b + Vector3.down, up = b + Vector3.up;
            m.Tri(sub, a0, a1, c0, dn); m.Tri(sub, a1, c1, c0, dn); m.Tri(sub, c0, c1, p2, dn);
            m.Tri(sub, a0, a1, c0, up); m.Tri(sub, a1, c1, c0, up); m.Tri(sub, c0, c1, p2, up);
        }

        // ============================================================ поленница под навесом (брёвна вдоль X)
        static void Woodpile(float x, float y, float z)
        {
            Vector3 p = new Vector3(x, y, z);
            const float r = 0.17f, len = 1.7f;
            int[] counts = { 5, 4, 3 };
            for (int row = 0; row < counts.Length; row++)
                for (int i = 0; i < counts[row]; i++)
                {
                    float lz = (i - (counts[row] - 1) * 0.5f) * (r * 2.05f);
                    float ly = r + row * r * 1.75f;
                    float jit = (TreeMB.H(401 + row, i) - 0.5f) * 0.14f;
                    Vector3 a = p + new Vector3(-len * 0.5f + jit, ly, lz), b = p + new Vector3(len * 0.5f + jit, ly, lz);
                    A.Tube(0, new[] { a, b }, new[] { r, r }, 7, false);
                    seed += 2;
                    A.Blob(1, a, new Vector3(0.04f, r * 0.95f, r * 0.95f), seed, 0, 0.12f, -1);
                    A.Blob(1, b, new Vector3(0.04f, r * 0.95f, r * 0.95f), seed + 1, 0, 0.12f, -1);
                }
            // навес: 2 столба, скатная крыша, бока-распорки
            for (int s = -1; s <= 1; s += 2)
                GroveGeo.Box(A, 2, p + new Vector3(s * 0.95f, 0.62f, 1.05f), new Vector3(0.06f, 0.62f, 0.06f), 0f);
            GroveGeo.Box(A, 2, p + new Vector3(0f, 1.30f, 0.05f), new Vector3(1.15f, 0.045f, 1.35f), Quaternion.Euler(14f, 0f, 0f));
            GroveGeo.Box(A, 3, p + new Vector3(0f, 1.40f, -0.50f), new Vector3(1.15f, 0.02f, 0.08f), Quaternion.Euler(14f, 0f, 0f));   // жестяная кромка
            C.Blob(1, p + new Vector3(0.1f, 1.40f, 0.1f), new Vector3(0.55f, 0.06f, 0.45f), seed + 3, 0, 0.35f, -1);                    // мох на крыше
            Col("Col_Woodpile", p + new Vector3(0f, 0.6f, 0.1f), Quaternion.identity, new Vector3(len + 0.3f, 1.2f, 2.0f));
            placed++;
        }

        // ============================================================ колода с топором
        static void ChopBlock(float x, float y, float z)
        {
            Vector3 p = new Vector3(x, y, z);
            Stump(p, 0.40f, 0.58f);
            Quaternion q = Quaternion.Euler(0f, 30f, 18f);
            Vector3 hc = p + new Vector3(0.05f, 0.98f, 0f);
            GroveGeo.Box(A, 2, hc, new Vector3(0.03f, 0.42f, 0.03f), q);                                        // рукоять
            Vector3 top = hc + q * new Vector3(0f, 0.36f, 0f);
            GroveGeo.Box(A, 3, top + q * new Vector3(0.10f, 0f, 0f), new Vector3(0.13f, 0.09f, 0.022f), q);     // лезвие
            // щепа вокруг
            for (int i = 0; i < 5; i++)
            {
                float a = TreeMB.H(420, i) * Mathf.PI * 2f, rr = 0.55f + 0.5f * TreeMB.H(421, i);
                seed += 2;
                A.Blob(1, p + new Vector3(Mathf.Sin(a) * rr, 0.04f, Mathf.Cos(a) * rr), new Vector3(0.12f, 0.03f, 0.05f), seed, 0, 0.3f, -1);
            }
            Col("Col_ChopBlock", p + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.85f, 0.6f, 0.85f));
            placed++;
        }

        // ============================================================ козлы с бревном и спилами
        static void Sawhorse(float x, float y, float z, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = new Vector3(x, y, z);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 fx = new Vector3(s * 0.45f, 0f, 0f);
                GroveGeo.Box(A, 2, p + q * (fx + new Vector3(0f, 0.42f, 0.18f)), new Vector3(0.04f, 0.45f, 0.04f), q * Quaternion.Euler(14f, 0f, 0f));
                GroveGeo.Box(A, 2, p + q * (fx + new Vector3(0f, 0.42f, -0.18f)), new Vector3(0.04f, 0.45f, 0.04f), q * Quaternion.Euler(-14f, 0f, 0f));
                GroveGeo.Box(A, 2, p + q * (fx + new Vector3(0f, 0.52f, 0f)), new Vector3(0.05f, 0.03f, 0.22f), q);
            }
            Vector3 a = p + q * new Vector3(-0.95f, 0.98f, 0f), b = p + q * new Vector3(0.95f, 0.98f, 0f);
            A.Tube(0, new[] { a, b }, new[] { 0.17f, 0.17f }, 8, false);
            Vector3 axis = (b - a).normalized;
            seed += 2;
            A.Blob(1, a, new Vector3(0.04f + Mathf.Abs(axis.z) * 0.12f, 0.16f, 0.04f + Mathf.Abs(axis.x) * 0.12f), seed, 0, 0.1f, -1);
            A.Blob(1, b, new Vector3(0.04f + Mathf.Abs(axis.z) * 0.12f, 0.16f, 0.04f + Mathf.Abs(axis.x) * 0.12f), seed + 1, 0, 0.1f, -1);
            // спилы на земле
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = p + q * new Vector3(0.6f + i * 0.28f, 0f, 0.75f + 0.1f * (i % 2));
                A.Tube(0, new[] { c, c + Vector3.up * 0.11f }, new[] { 0.16f, 0.16f }, 8, false);
                A.Cap(1, c + Vector3.up * 0.114f, 0.155f, 8);
            }
            Col("Col_Sawhorse", p + q * new Vector3(0f, 0.6f, 0f), q, new Vector3(2.0f, 1.0f, 0.8f));
            placed++;
        }

        // ============================================================ лавка: доска на двух пеньках, смотрит на центр
        static void Bench(float x, float y, float z, float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 p = new Vector3(x, y, z);
            for (int s = -1; s <= 1; s += 2)
                Stump(p + q * new Vector3(s * 0.72f, 0f, 0f), 0.22f, 0.36f);
            GroveGeo.Box(A, 0, p + q * new Vector3(0f, 0.41f, 0f), new Vector3(1.0f, 0.045f, 0.22f), q);
            GroveGeo.Box(A, 2, p + q * new Vector3(0f, 0.375f, 0f), new Vector3(0.94f, 0.02f, 0.18f), q);
            Col("Col_GladeBench", p + q * new Vector3(0f, 0.3f, 0f), q, new Vector3(2.2f, 0.6f, 0.6f));
            placed++;
        }

        static void StumpSeat(float x, float y, float z, float r, float h)
        {
            Stump(new Vector3(x, y, z), r, h);
            Col("Col_Seat", new Vector3(x, y + h * 0.5f, z), Quaternion.identity, new Vector3(r * 1.9f, h, r * 1.9f));
            placed++;
        }

        // ============================================================ столб-фонарь
        static void LampPost(float x, float y, float z)
        {
            Vector3 p = new Vector3(x, y, z);
            float s = x > 0f ? -1f : 1f;
            GroveGeo.Box(A, 2, p + Vector3.up * 1.15f, new Vector3(0.06f, 1.2f, 0.06f), 0f);
            GroveGeo.Box(A, 3, p + Vector3.up * 0.06f, new Vector3(0.11f, 0.06f, 0.11f), 0f);                         // железная пята
            GroveGeo.Box(A, 3, p + new Vector3(s * 0.22f, 2.22f, 0f), new Vector3(0.24f, 0.025f, 0.025f), 0f);       // рука
            Vector3 lp = p + new Vector3(s * 0.40f, 1.96f, 0f);
            seed += 2;
            B.Blob(0, lp, new Vector3(0.17f, 0.22f, 0.17f), seed, 0, 0.1f, -1);                                       // тёплое стекло
            A.Blob(3, lp + Vector3.up * 0.27f, new Vector3(0.20f, 0.06f, 0.20f), seed + 1, 0, 0.1f, -1);              // крыша фонаря
            lamps.Add(lp);
            Col("Col_GladeLamp", p + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.25f, 2.4f, 0.25f));
            placed++;
        }

        // ============================================================ камни, грибы, папоротники, цветы
        static void MossyRock(float x, float y, float z, float w, int i)
        {
            Vector3 p = new Vector3(x, y, z);
            seed += 4;
            float h = w * (0.65f + 0.3f * TreeMB.H(450, i));
            C.Blob(3, p + Vector3.up * (h * 0.55f), new Vector3(w, h, w * 0.9f), seed, 1, 0.30f, -1);
            C.Blob(1, p + Vector3.up * (h * 1.2f), new Vector3(w * 0.65f, h * 0.30f, w * 0.6f), seed + 1, 0, 0.35f, -1);   // мох сверху
            C.Blob(3, p + new Vector3(w * 1.0f, h * 0.2f, w * 0.3f), new Vector3(w * 0.45f, h * 0.35f, w * 0.4f), seed + 2, 0, 0.35f, -1);
            if (w > 0.55f) Col("Col_GladeRock", p + Vector3.up * (h * 0.6f), Quaternion.identity, new Vector3(w * 1.7f, h * 1.2f, w * 1.6f));
            placed++;
        }

        static void RedShrooms(float x, float y, float z, int i)
        {
            Vector3 p = new Vector3(x, y, z);
            for (int k = 0; k < 3; k++)
            {
                float a = TreeMB.H(470 + i, k) * Mathf.PI * 2f, rr = 0.06f + 0.22f * TreeMB.H(480 + i, k);
                Vector3 q = p + new Vector3(Mathf.Sin(a) * rr, 0f, Mathf.Cos(a) * rr);
                float h = 0.14f + 0.20f * TreeMB.H(490 + i, k);
                A.Tube(1, new[] { q + Vector3.down * 0.03f, q + new Vector3(0.01f, h, 0f) }, new[] { 0.05f, 0.035f }, 5, false);
                seed += 2;
                C.Blob(2, q + new Vector3(0.01f, h + 0.04f, 0f), new Vector3(0.14f, 0.08f, 0.14f) * (0.8f + 0.7f * TreeMB.H(495 + i, k)), seed, 0, 0.25f, -1);
            }
            placed++;
        }

        static void Fern(float x, float y, float z, int i)
        {
            Vector3 p = new Vector3(x, y, z);
            int n = 7;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f + TreeMB.H(500 + i, k) * 0.5f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Blade(C, 0, p + d * 0.04f, d, 0.50f + 0.35f * TreeMB.H(510 + i, k), 0.09f);
            }
            placed++;
        }

        static void Flowers(float x, float y, float z, int i)
        {
            Vector3 p = new Vector3(x, y, z);
            for (int k = 0; k < 6; k++)
            {
                float a = TreeMB.H(530 + i, k) * Mathf.PI * 2f, rr = 0.30f * Mathf.Sqrt(TreeMB.H(540 + i, k));
                Vector3 q = p + new Vector3(Mathf.Sin(a) * rr, 0f, Mathf.Cos(a) * rr);
                float h = 0.20f + 0.10f * TreeMB.H(550 + i, k);
                C.Tube(0, new[] { q + Vector3.down * 0.02f, q + Vector3.up * h }, new[] { 0.022f, 0.016f }, 3, false);
                seed += 2;
                int kind = (i + k) % 3;
                B.Blob(1 + kind, q + Vector3.up * (h + 0.03f), new Vector3(0.10f, 0.07f, 0.10f) * (0.8f + 0.5f * TreeMB.H(560 + i, k)), seed, 0, 0.25f, -1);
                C.Blob(1, q + new Vector3(0.09f, 0.06f, 0f), new Vector3(0.11f, 0.035f, 0.065f), seed + 1, 0, 0.3f, -1);
            }
            placed++;
        }

        // ============================================================ круг плоских камней вокруг места игрока (не мешают, это просто красота)
        static void StoneCircle()
        {
            int n = 11;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + 0.2f;
                float rr = 2.35f + 0.18f * (TreeMB.H(600, i) - 0.5f);
                float x = Mathf.Sin(a) * rr, z = Mathf.Cos(a) * rr, y;
                if (!GY(x, z, out y, 1.5f, 0.9f)) { skipped++; continue; }
                if (Blocked(x, z, 0.35f)) { skipped++; continue; }
                seed += 2;
                C.Blob(3, new Vector3(x, y + 0.04f, z), new Vector3(0.40f + 0.12f * TreeMB.H(610, i), 0.07f, 0.34f), seed, 0, 0.35f, -1);
                if (i % 3 == 1) C.Blob(1, new Vector3(x + 0.1f, y + 0.08f, z - 0.05f), new Vector3(0.17f, 0.04f, 0.14f), seed + 1, 0, 0.35f, -1);
            }
            placed++;
        }

        // ============================================================ пятна мха на земле: поляна выглядит «зелёнее», чем голая земля
        static void MossPatches()
        {
            for (int i = 0; i < 4; i++)
            {
                float a = TreeMB.H(650, i) * Mathf.PI * 2f, rr = 3.6f + 5.0f * TreeMB.H(660, i);
                float x = Mathf.Sin(a) * rr, z = Mathf.Cos(a) * rr, y;
                if (!GY(x, z, out y, 1.5f, 1.0f)) { skipped++; continue; }
                seed += 2;
                C.Blob(1, new Vector3(x, y + 0.03f, z), new Vector3(1.0f + 0.7f * TreeMB.H(670, i), 0.05f, 0.8f + 0.5f * TreeMB.H(680, i)), seed, 1, 0.3f, -1);
            }
            placed++;
        }
    }
}
#endif

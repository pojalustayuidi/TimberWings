#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ChopCamp.cs (Editor)  НОВЫЙ ФАЙЛ
// «Лагерь лесорубов»: добавляет на поляну рубки (PAD_Forest) то, что есть на референсе и чего ещё нет в ChopZone и ForestGladeBeauty:
//   • открытый навес-пилорама: 4 столба, скатная кровля из досок, под ней штабель досок на поддоне, козлы с брёвном, опилки, фонарь на цепи;
//   • повозка с брёвнами (4 колеса со спицами, борта из досок, оглобли, три ряда брёвен на платформе);
//   • две поленницы-пирамиды на стойках, три группы лежащих брёвен (с сучками и мхом), пеньки с фонариками;
//   • 6 столбов с фонарями и гирлянды между ними: флажки (розовые, жёлтые, бирюзовые, оранжевые) и тёплые лампочки;
//   • большие пятна утоптанной земли и щепа, сиреневые валуны, бирюзовые и лаймовые кусты.
// Меню: Tools/BuilderSim/Лес/Красота леса/8b. Лагерь лесорубов (238), 8c. Убрать лагерь лесорубов (239).
// Безопасно жать повторно: корень ChopCamp в IslandV2/_Manual пересоздаётся целиком. Другие корни не трогаются.
// Порядок: после «4. Лес» (там зона рубки: забор, арка, деревья) и после «8. Поляна рубки: уют и красота». Лагерь обходит деревья, тропы,
// ворота, воду, край острова, коллайдеры ChopGladeDecor и место игрока (3 м вокруг метки). Не нашлось место для вещи = она пропущена (счёт в отчёте).
// Всё процедурно (TreeMB, материалы TreeToon, пастель как у остального леса). 6 мешей = 6 draw calls (без теней у флажков, земли и огоньков).
// Свет: огоньки (флажки, лампочки, фонари) это один FireSource simOnly со спрайтами, настоящий свет дают только 2 фонаря (пул берёт ближайшие к игроку).
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ChopCamp
    {
        public const string RootName = "ChopCamp";
        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const float PlayerR = 3.0f;       // место игрока вокруг метки свободно
        const float PoleH = 3.1f;

        struct Fp { public float dx, dz, r; public Fp(float dx, float dz, float r) { this.dx = dx; this.dz = dz; this.r = r; } }
        struct Fr
        {
            public Vector3 p; public Quaternion q;
            public Vector3 T(float x, float y, float z) { return p + q * new Vector3(x, y, z); }
        }

        // следы вещей в локальных координатах вещи (x вправо, z вперёд), r = радиус круга занятого места
        static readonly Fp[] FpShed = { new Fp(-1.1f, 0f, 1.3f), new Fp(0f, 0f, 1.3f), new Fp(1.1f, 0f, 1.3f) };
        static readonly Fp[] FpWagon = { new Fp(-0.5f, 0f, 0.85f), new Fp(0.5f, 0f, 0.85f), new Fp(1.6f, 0f, 0.45f) };
        static readonly Fp[] FpPile = { new Fp(-0.55f, 0f, 0.75f), new Fp(0.55f, 0f, 0.75f) };
        static readonly Fp[] FpLogs = { new Fp(-0.8f, 0f, 0.8f), new Fp(0.8f, 0f, 0.8f) };
        static readonly Fp[] FpPole = { new Fp(0f, 0f, 0.45f) };
        static readonly Fp[] FpStump = { new Fp(0f, 0f, 0.55f) };
        static readonly Fp[] FpBarrel = { new Fp(0f, 0f, 0.50f) };
        static readonly Fp[] FpCrate = { new Fp(0f, 0f, 0.95f) };

        static IslandField F;
        static IslandLayout L;
        static Vector3 O;
        static Vector2 C2;
        static Transform R;
        static TreeMB W, P, Fl, G, D, S;
        // W дерево: 0 кора бревна, 1 торец (светлый спил), 2 тёмное дерево, 3 железо
        // P доски: 0 светлая доска, 1 кровля, 2 верёвка, 3 тёмная прокладка
        // Fl флажки: 0 розовый, 1 жёлтый, 2 бирюзовый, 3 оранжевый
        // G свет: 0 тёплое стекло и лампочки
        // D земля: 0 утоптанная земля, 1 щепа, 2 камень, 3 мох
        // S кусты: 0 лайм, 1 бирюзовый, 2 низ, 3 жёлтые цветочки
        static int seed, placed, skipped;
        static float gateA;                    // угол главной арки: все вещи расставляются «от входа» (0° = к арке, 180° = в глубине)
        static int nShed, nWagon, nPile, nLogs, nPole, nGarland, nStumpLamp, nRock, nBush, nPatch;
        static readonly List<Vector2> treesW = new List<Vector2>();       // мировые x, z деревьев
        static readonly List<Vector3> used = new List<Vector3>();         // локальные x, z, радиус занятого места
        static readonly List<Vector2> gates = new List<Vector2>();        // мировые точки, где тропы пересекают забор
        static readonly List<Vector3> glows = new List<Vector3>();        // локальные точки огоньков (простое свечение)
        static readonly List<Vector3> realLamps = new List<Vector3>();    // локальные точки настоящего света

        // ============================================================ меню
        [MenuItem("Tools/BuilderSim/Лес/Красота леса/8b. Лагерь лесорубов: навес, повозка, флажки", false, 238)]
        static void Run()
        {
            string rep = Build();
            if (rep != null) Debug.Log(rep);
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/8c. Убрать лагерь лесорубов", false, 239)]
        static void Clear()
        {
            var manual = GameObject.Find("IslandV2/_Manual");
            if (manual == null) { Debug.LogWarning("Лагерь лесорубов: нет IslandV2/_Manual."); return; }
            var t = manual.transform.Find(RootName);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
            Debug.Log("Лагерь лесорубов: убран.");
        }

        // ============================================================ главное
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Лагерь лесорубов: останови Play Mode."); return null; }
            var pad = GameObject.Find(ChopZone.PadName);
            if (pad == null) { Debug.LogError("Лагерь лесорубов: в сцене нет метки " + ChopZone.PadName + " (Остров v2 → 2. Собрать блокаут)."); return null; }
            var rootGo = GameObject.Find("IslandV2");
            if (rootGo == null) { Debug.LogError("Лагерь лесорубов: в сцене нет IslandV2."); return null; }
            L = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (L == null) { Debug.LogError("Лагерь лесорубов: нет раскладки " + IslandLayout.AssetPath); return null; }
            F = new IslandField(L);
            O = pad.transform.position;
            C2 = new Vector2(O.x, O.z);
            var manualGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = manualGo != null ? manualGo.transform : rootGo.transform;

            // ---- материалы (имена Zone_Dirt, Glade_*, Grove_*, Under_* общие с ChopZone и поляной, остальные свои Camp_*)
            var log = ForestMats.GladeLog(false); var logEnd = ForestMats.GladeLogEnd(false);
            var woodD = ForestMats.GroveWoodDark(false); var iron = ForestMats.GroveIron(false);
            var plank = ForestMats.GroveWood(false);
            var roof = ForestMats.Get("Camp_Roof", Rgb(0.62f, 0.40f, 0.26f), Rgb(0.30f, 0.18f, 0.12f), 0f, 3.2f, 0f, 0.05f, 0.60f, false);
            var rope = ForestMats.Get("Camp_Rope", Rgb(0.86f, 0.74f, 0.52f), Rgb(0.62f, 0.50f, 0.34f), 0f, 4f, 0f, 0.02f, 0.20f, false);
            var fPink = ForestMats.FlowerPink(false); var fYellow = ForestMats.FlowerYellow(false);
            var fTeal = ForestMats.Get("Camp_FlagTeal", Rgb(0.35f, 0.82f, 0.78f), Rgb(0.22f, 0.62f, 0.62f), 0f, 4f, 0.02f, 0.10f, 0f, false);
            var fOrange = ForestMats.Get("Camp_FlagOrange", Rgb(1.00f, 0.62f, 0.28f), Rgb(0.92f, 0.46f, 0.20f), 0f, 4f, 0.02f, 0.10f, 0f, false);
            var warm = ForestMats.GroveWarm(false);
            var dirt = ForestMats.Get("Zone_Dirt", Rgb(0.86f, 0.74f, 0.54f), Rgb(0.68f, 0.54f, 0.38f), 0f, 1f, 0f, 0.02f, 0.2f, false);
            var chips = ForestMats.Get("Camp_Chips", Rgb(0.96f, 0.82f, 0.58f), Rgb(0.80f, 0.64f, 0.42f), 0f, 1f, 0f, 0.02f, 0.2f, false);
            var rock = ForestMats.Rock(false); var moss = ForestMats.BushLime(false);
            var bLime = ForestMats.BushLime(false); var bTeal = ForestMats.BushDark(false); var bUnder = ForestMats.BushUnder(false);
            var bloom = ForestMats.FlowerYellow(false);
            foreach (var m in new[] { log, logEnd, woodD, iron, plank, roof, rope, fPink, fYellow, fTeal, fOrange, warm, dirt, chips, rock, moss, bLime, bTeal, bUnder, bloom })
                if (m == null) { Debug.LogError("Лагерь лесорубов: не создался материал (проверь шейдер TreeToon: Лес → Tree Toon)."); return null; }

            // ---- что обходить
            GatherTrees(parent);
            FindGates();
            gateA = ChopZone.MainGateAngle(F, L, C2);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // ---- корень
            var old = parent.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var rootObj = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootObj, RootName);
            rootObj.transform.SetParent(parent, false);
            rootObj.transform.position = O;
            rootObj.transform.rotation = Quaternion.identity;
            R = rootObj.transform;

            W = new TreeMB(); P = new TreeMB(); Fl = new TreeMB(); G = new TreeMB(); D = new TreeMB(); S = new TreeMB();
            used.Clear(); glows.Clear(); realLamps.Clear();
            seed = 5000; placed = 0; skipped = 0;
            nShed = nWagon = nPile = nLogs = nPole = nGarland = nStumpLamp = nRock = nBush = nPatch = 0;
            ReserveGlade(parent);

            float x, z, yaw;

            // ---- 1. крупное первым (мелкое обходит крупное)
            if (SpotAt(160f, 7.9f, 11, FpShed, 0, 5f, 1.6f, 10f, out x, out z, out yaw)) { Shed(x, z, yaw); nShed++; } else skipped++;
            if (SpotAt(-62f, 5.9f, 21, FpWagon, 1, 10f, 1.6f, 10f, out x, out z, out yaw)) { Wagon(x, z, yaw); nWagon++; } else skipped++;
            for (int i = 0; i < 1; i++)
                if (SpotAt(-150f, 8.5f, 31 + i * 9, FpPile, 0, 10f, 1.5f, 11f, out x, out z, out yaw)) { LogPile(x, z, yaw, i); nPile++; } else skipped++;
            for (int i = 0; i < 2; i++)
                if (SpotAt(i == 0 ? -100f : 110f, 8.3f, 51 + i * 11, FpLogs, 1, 12f, 1.5f, 12f, out x, out z, out yaw)) { FallenLogs(x, z, yaw, i); nLogs++; } else skipped++;

            // ---- 2. столбы с фонарями и гирлянды между ними
            PolesAndGarlands();

            // ---- 3. пеньки с фонариками, камни, кусты
            for (int i = 0; i < 2; i++)
                if (SpotAt(i == 0 ? 42f : -30f, i == 0 ? 6.2f : 8.0f, 80 + i * 7, FpStump, 2, 0f, 1.3f, 14f, out x, out z, out yaw)) { StumpLamp(x, z, i); nStumpLamp++; } else skipped++;
            for (int i = 0; i < 4; i++)
            {
                float size = 0.40f + 0.34f * TreeMB.H(205, i);
                var fp = new[] { new Fp(0f, 0f, size * 1.3f) };
                if (SpotAt(i == 0 ? 75f : i == 1 ? -80f : i == 2 ? 140f : -128f, 9.0f, 110 + i * 5, fp, 2, 0f, 1.2f, 18f, out x, out z, out yaw)) { Rocks(x, z, size, i); nRock++; } else skipped++;
            }
            for (int i = 0; i < 6; i++)
            {
                var fp = new[] { new Fp(0f, 0f, 0.85f) };
                if (SpotAt(i == 0 ? 30f : i == 1 ? -30f : i == 2 ? 95f : i == 3 ? -95f : i == 4 ? 135f : -135f, 8.5f, 150 + i * 5, fp, 2, 0f, 1.2f, 22f, out x, out z, out yaw)) { Bush(x, z, 0.8f + 0.5f * TreeMB.H(160, i), i); nBush++; } else skipped++;
            }

            // ---- 3b. мелочи для вайба: бочки у навеса, ящики с мешком у тропы
            for (int i = 0; i < 2; i++)
                if (SpotAt(i == 0 ? 148f : 176f, 8.6f, 190 + i * 7, FpBarrel, 2, 0f, 1.3f, 14f, out x, out z, out yaw)) Barrel(x, z, i); else skipped++;
            if (SpotAt(-46f, 7.3f, 205, FpCrate, 2, 0f, 1.3f, 14f, out x, out z, out yaw)) Crates(x, z, 0); else skipped++;

            // ---- 4. утоптанная земля большими пятнами и щепа
            Patches();

            // ---- 5. меши
            EditorAssets.EnsureFolder(Dir);
            AddMesh("Wood", W, "ChopCamp_Wood", new[] { log, logEnd, woodD, iron }, true, true);
            AddMesh("Planks", P, "ChopCamp_Planks", new[] { plank, roof, rope, woodD }, true, true);
            AddMesh("Flags", Fl, "ChopCamp_Flags", new[] { fPink, fYellow, fTeal, fOrange }, false);
            AddMesh("Glow", G, "ChopCamp_Glow", new[] { warm }, false);
            AddMesh("Ground", D, "ChopCamp_Ground", new[] { dirt, chips, rock, moss }, false);
            AddMesh("Bushes", S, "ChopCamp_Bushes", new[] { bLime, bTeal, bUnder, bloom }, true);

            // ---- 6. свет
            if (glows.Count > 0)
            {
                var go = new GameObject("Light_CampGlow");
                go.transform.SetParent(R, false);
                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Garland;
                fs.color = new Color(1f, 0.80f, 0.48f);
                fs.intensity = 1f; fs.range = 4f; fs.simOnly = true;
                fs.glow = true; fs.glowSize = 0.70f;
                fs.glowPoints = glows.ToArray();
            }
            for (int i = 0; i < realLamps.Count; i++)
            {
                var go = new GameObject("Light_CampLantern" + i);
                go.transform.SetParent(R, false);
                go.transform.localPosition = realLamps[i];
                var fs = go.AddComponent<FireSource>();
                fs.preset = FlickerPreset.Lantern;
                fs.color = new Color(1f, 0.78f, 0.45f);
                fs.intensity = 2.0f; fs.range = 7.0f; fs.simOnly = false;
                fs.glow = true; fs.glowSize = 1.1f;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(R.gameObject.scene);
            Selection.activeGameObject = rootObj;

            var sb = new StringBuilder("Лагерь лесорубов (" + ChopZone.PadName + " " + O.x.ToString("0.#") + ", " + O.z.ToString("0.#") + "), деревьев обойдено " + treesW.Count + ":\n");
            sb.AppendLine("  навес-пилорама " + nShed + "/1, повозка " + nWagon + "/1, поленницы " + nPile + "/2, лежащие брёвна " + nLogs + "/3, пеньки с фонариками " + nStumpLamp + "/3");
            sb.AppendLine("  столбов с фонарями " + nPole + "/6, гирлянд с флажками " + nGarland + ", камней " + nRock + "/7, кустов " + nBush + "/9, пятен земли " + nPatch);
            sb.AppendLine("  не нашлось места (деревья, тропа, вода, край, забор): " + skipped + ". Огоньков (простое свечение) " + glows.Count + ", настоящих фонарей " + realLamps.Count + ".");
            sb.AppendLine("  меши ChopCamp_* в " + Dir + " (6 draw calls). Корень IslandV2/_Manual/" + RootName + ". Место игрока (3 м вокруг метки) свободно.");
            if (skipped > 6) sb.AppendLine("  [!] Много пропусков: поляна забита деревьями. Нажми «4. Лес» (меньше деревьев в зоне) или сдвинь вещи руками, потом «8b» ещё раз.");
            return sb.ToString();
        }

        // ============================================================ что обходить
        static void GatherTrees(Transform parent)
        {
            treesW.Clear();
            foreach (var ri in Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                treesW.Add(new Vector2(ri.transform.position.x, ri.transform.position.z));
            foreach (var rn in new[] { "ForestChop", "ForestDecor", "SacredTrees", ForestGuardLite.RootName, ForestWings.RootName })
            {
                var t = parent.Find(rn);
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++) { var p = t.GetChild(i).position; treesW.Add(new Vector2(p.x, p.z)); }
            }
        }

        // вещи ChopGladeDecor (поленница, лавки, фонари...): по их коллайдерам-коробкам занимаем место
        static void ReserveGlade(Transform parent)
        {
            var g = parent.Find("ChopGladeDecor");
            if (g == null) return;
            foreach (var bc in g.GetComponentsInChildren<BoxCollider>())
            {
                Vector3 p = bc.transform.position;
                Vector3 s = bc.size;
                float r = 0.5f * Mathf.Sqrt(s.x * s.x + s.z * s.z);
                used.Add(new Vector3(p.x - O.x, p.z - O.z, Mathf.Max(0.3f, r)));
            }
        }

        static float SignedD(Vector2 p)
        {
            Vector2 d = p - C2;
            return d.magnitude - ChopZone.Rb(Mathf.Atan2(d.y, d.x));
        }

        // те же ворота, что находит ChopZone: тропы, пересекающие забор. От ворот к центру остаётся свободный проход
        static void FindGates()
        {
            gates.Clear();
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
        }

        static bool InLane(float wx, float wz, float halfW)
        {
            Vector2 p = new Vector2(wx, wz);
            for (int i = 0; i < gates.Count; i++)
            {
                Vector2 a = gates[i], ab = C2 - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                if (Vector2.Distance(p, a + ab * t) < halfW) return true;
            }
            return false;
        }

        static bool NearOtherAnchor(float wx, float wz, float margin)
        {
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.name == ChopZone.PadName) continue;
                if (a.kind == IslandLayout.AnchorKind.Building) { if (F.RectDistance(a, wx, wz) < margin) return true; }
                else if (Vector2.Distance(F.AnchorWorld(a), new Vector2(wx, wz)) < margin) return true;
            }
            return false;
        }

        // ============================================================ поиск места
        static float Hy(float lx, float lz) { return F.Height(O.x + lx, O.z + lz) - O.y; }
        static Color Rgb(float r, float g, float b) { return new Color(r, g, b); }

        static bool BlockedBy(float cx, float cz, float r)
        {
            float wx = O.x + cx, wz = O.z + cz;
            for (int i = 0; i < treesW.Count; i++)
            {
                float dx = treesW[i].x - wx, dz = treesW[i].y - wz, rr = r + 0.9f;   // 0.9 = запас на ствол
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            for (int i = 0; i < used.Count; i++)
            {
                float dx = used[i].x - cx, dz = used[i].y - cz, rr = r + used[i].z;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        static void Circle(Fp c, float x, float z, float yaw, out float cx, out float cz)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            cx = x + c.dx * cs + c.dz * sn;
            cz = z - c.dx * sn + c.dz * cs;
        }

        static bool Fits(float x, float z, float yaw, Fp[] fp, float pathMin, float maxSlope)
        {
            for (int i = 0; i < fp.Length; i++)
            {
                float cx, cz;
                Circle(fp[i], x, z, yaw, out cx, out cz);
                float r = fp[i].r;
                float wx = O.x + cx, wz = O.z + cz;
                if (!F.Inside(wx, wz) || F.EdgeDistance(wx, wz) < 1.5f + r * 0.3f) return false;
                if (F.IsWater(wx, wz) || F.IsFord(wx, wz) || F.WaterDistance(wx, wz) < 0.6f + r * 0.5f) return false;
                if (F.PathDistance(wx, wz) < pathMin) return false;
                if (F.Slope(wx, wz) > maxSlope) return false;
                float d = Mathf.Sqrt(cx * cx + cz * cz);
                if (d < PlayerR + r) return false;
                if (d > ChopZone.Rb(Mathf.Atan2(cz, cx)) - 1.5f - r) return false;      // внутри забора
                if (InLane(wx, wz, 1.5f + r)) return false;
                if (NearOtherAnchor(wx, wz, 2.5f + r)) return false;
                if (BlockedBy(cx, cz, r)) return false;
            }
            return true;
        }

        static void Reserve(float x, float z, float yaw, Fp[] fp)
        {
            for (int i = 0; i < fp.Length; i++)
            {
                float cx, cz;
                Circle(fp[i], x, z, yaw, out cx, out cz);
                used.Add(new Vector3(cx, cz, fp[i].r));
            }
        }

        // yawMode: 0 = фронт (локальный +z) смотрит на центр, 1 = длинная ось (локальный +x) вдоль окружности, 2 = случайный поворот
        static bool Spot(float rMin, float rMax, int tag, Fp[] fp, int yawMode, float yawJit, float pathMin, float maxSlope, out float sx, out float sz, out float syaw)
        {
            for (int i = 0; i < 140; i++)
            {
                float a = TreeMB.H(tag, i * 2) * Mathf.PI * 2f;
                float r = Mathf.Lerp(rMin, rMax, TreeMB.H(tag + 7, i * 2 + 1));
                float cx = Mathf.Cos(a) * r, cz = Mathf.Sin(a) * r;
                float yw;
                if (yawMode == 0) yw = Mathf.Atan2(-cx, -cz) * Mathf.Rad2Deg;
                else if (yawMode == 1) yw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
                else yw = TreeMB.H(tag + 13, i) * 360f;
                yw += (TreeMB.H(tag + 19, i) - 0.5f) * 2f * yawJit;
                if (!Fits(cx, cz, yw, fp, pathMin, maxSlope)) continue;
                Reserve(cx, cz, yw, fp);
                sx = cx; sz = cz; syaw = yw;
                placed++;
                return true;
            }
            sx = 0f; sz = 0f; syaw = 0f;
            return false;
        }

        // место по авторской композиции: угол thDeg от арки и радиус r; если занято, ищет рядом (угол и радиус расширяются), потом общий случайный поиск
        static bool SpotAt(float thDeg, float r, int tag, Fp[] fp, int yawMode, float yawJit, float pathMin, float maxSlope, out float sx, out float sz, out float syaw)
        {
            for (int i = 0; i < 90; i++)
            {
                float k = i / 90f;
                float da = i == 0 ? 0f : (TreeMB.H(tag, i * 2) - 0.5f) * 2f * Mathf.Lerp(4f, 24f, k);
                float dr = i == 0 ? 0f : (TreeMB.H(tag + 7, i * 2 + 1) - 0.5f) * 2f * Mathf.Lerp(0.4f, 1.8f, k);
                float a = gateA + (thDeg + da) * Mathf.Deg2Rad, rr = r + dr;
                float cx = Mathf.Cos(a) * rr, cz = Mathf.Sin(a) * rr;
                float yw;
                if (yawMode == 0) yw = Mathf.Atan2(-cx, -cz) * Mathf.Rad2Deg;
                else if (yawMode == 1) yw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
                else yw = TreeMB.H(tag + 13, i) * 360f;
                yw += (TreeMB.H(tag + 19, i) - 0.5f) * 2f * yawJit;
                if (!Fits(cx, cz, yw, fp, pathMin, maxSlope)) continue;
                Reserve(cx, cz, yw, fp);
                sx = cx; sz = cz; syaw = yw;
                placed++;
                return true;
            }
            return Spot(Mathf.Max(3.6f, r - 2.5f), r + 2.5f, tag, fp, yawMode, yawJit, pathMin, maxSlope, out sx, out sz, out syaw);
        }

        // ============================================================ геометрия: общие помощники
        // балка по двум точкам: hw горизонтальная половина сечения, hh вертикальная
        static void Beam(TreeMB m, int sub, Vector3 a, Vector3 b, float hw, float hh)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector3 dir = d / len;
            Vector3 up = Mathf.Abs(dir.y) > 0.97f ? Vector3.forward : Vector3.up;
            GroveGeo.Box(m, sub, (a + b) * 0.5f, new Vector3(hw, hh, len * 0.5f), Quaternion.LookRotation(dir, up));
        }

        // плоский торец: нормаль по axis (наружу)
        static void Disc(TreeMB m, int sub, Vector3 c, Vector3 axis, float r, int sides)
        {
            Vector3 refv = Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, refv).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            Vector3 c2 = c + axis * 0.002f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = c2 + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * r;
                Vector3 p1 = c2 + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * r;
                m.Tri(sub, c2, p0, p1, c2 - axis);
            }
        }

        // бревно между двумя точками: кора, светлые торцы
        static void LogTube(Vector3 a, Vector3 b, float r)
        {
            W.Tube(0, new[] { a, (a + b) * 0.5f, b }, new[] { r * 0.97f, r * 1.03f, r * 0.97f }, 8, false);
            Vector3 ax = (b - a).normalized;
            Disc(W, 1, a, -ax, r * 0.97f, 8);
            Disc(W, 1, b, ax, r * 0.97f, 8);
        }

        // бревно лежит на земле: высота концов берётся с рельефа (ya, yb игнорируются в f.T)
        static void LogOnGround(Fr f, float ax, float az, float bx, float bz, float r)
        {
            Vector3 a = f.T(ax, 0f, az), b = f.T(bx, 0f, bz);
            a.y = Hy(a.x, a.z) + r * 0.82f;
            b.y = Hy(b.x, b.z) + r * 0.82f;
            LogTube(a, b, r);
        }

        static void Stump(Vector3 p, float r, float h)
        {
            W.Tube(0, new[] { p + Vector3.down * 0.10f, p + Vector3.up * h }, new[] { r * 1.12f, r }, 8, false);
            W.Cap(1, p + Vector3.up * (h + 0.004f), r * 0.97f, 8);
        }

        static void Col(string name, Vector3 local, Quaternion q, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(R, false);
            go.transform.localPosition = local;
            go.transform.localRotation = q;
            go.AddComponent<BoxCollider>().size = size;
        }

        // фонарь: тёплое стекло, железная рамка, крышка. c = центр стекла. Огонёк (простое свечение) добавляется сам
        static void Lantern(Vector3 c)
        {
            seed += 2;
            G.Blob(0, c, new Vector3(0.085f, 0.11f, 0.085f), seed, 0, 0.1f, -1);
            GroveGeo.Box(W, 3, c + Vector3.down * 0.13f, new Vector3(0.10f, 0.014f, 0.10f), 0f);
            GroveGeo.Box(W, 3, c + Vector3.up * 0.14f, new Vector3(0.115f, 0.016f, 0.115f), 0f);
            GroveGeo.Box(W, 3, c + Vector3.up * 0.175f, new Vector3(0.04f, 0.02f, 0.04f), 0f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    GroveGeo.Box(W, 3, c + new Vector3(sx * 0.09f, 0.0f, sz * 0.09f), new Vector3(0.008f, 0.125f, 0.008f), 0f);
            glows.Add(c);
        }

        // ============================================================ навес-пилорама
        static void Shed(float x, float z, float yaw)
        {
            var f = new Fr { p = new Vector3(x, 0f, z), q = Quaternion.Euler(0f, yaw, 0f) };
            // базовая высота: среднее по углам
            float hs = 0f;
            var corners = new[] { new Vector2(-1.55f, -1f), new Vector2(1.55f, -1f), new Vector2(-1.55f, 1f), new Vector2(1.55f, 1f) };
            for (int k = 0; k < corners.Length; k++) { Vector3 w = f.T(corners[k].x, 0f, corners[k].y); hs += Hy(w.x, w.z); }
            f.p.y = hs / corners.Length;

            // столбы: задние выше, кровля скатывается к фронту
            float[] ph = { 2.05f, 2.05f, 2.45f, 2.45f };
            var pc = new[] { new Vector2(-1.55f, 1f), new Vector2(1.55f, 1f), new Vector2(-1.55f, -1f), new Vector2(1.55f, -1f) };
            for (int k = 0; k < 4; k++)
            {
                Vector3 top = f.T(pc[k].x, ph[k], pc[k].y);
                Vector3 b0 = f.T(pc[k].x, 0f, pc[k].y);
                float gy = Hy(b0.x, b0.z) - 0.2f;
                Beam(W, 2, new Vector3(top.x, gy, top.z), top + Vector3.up * 0.04f, 0.09f, 0.09f);
                seed += 3;
                D.Blob(2, new Vector3(top.x, gy + 0.28f, top.z), new Vector3(0.24f, 0.16f, 0.22f), seed, 0, 0.3f, -1);   // камень у основания
                Col("Col_ShedPost", new Vector3(top.x, gy + (top.y - gy) * 0.5f, top.z), f.q, new Vector3(0.22f, top.y - gy, 0.22f));
            }
            // обвязка
            Beam(W, 2, f.T(-1.65f, 2.0f, 1f), f.T(1.65f, 2.0f, 1f), 0.07f, 0.07f);
            Beam(W, 2, f.T(-1.65f, 2.4f, -1f), f.T(1.65f, 2.4f, -1f), 0.07f, 0.07f);
            for (int k = 0; k < 4; k++)
            {
                float rx = -1.55f + k * 1.03333f;
                Beam(W, 2, f.T(rx, 2.08f, 1.2f), f.T(rx, 2.50f, -1.2f), 0.04f, 0.05f);
            }
            // кровля: доски вдоль ската, через одну светлее
            float pitch = Mathf.Atan2(0.42f, 2.4f) * Mathf.Rad2Deg;
            Quaternion rq = f.q * Quaternion.Euler(pitch, 0f, 0f);
            for (int i = 0; i < 9; i++)
            {
                float bx = -1.6f + i * 0.4f;
                float jy = (TreeMB.H(1100, i) - 0.5f) * 0.024f;
                GroveGeo.Box(P, i % 2 == 0 ? 1 : 0, f.T(bx, 2.35f + jy, 0f), new Vector3(0.19f, 0.02f, 1.35f), rq);
            }
            for (int k = 0; k < 3; k++)
            {
                float bz = 0.9f - k * 0.9f;
                GroveGeo.Box(W, 2, f.T(0f, 2.35f - bz * 0.175f + 0.04f, bz), new Vector3(1.82f, 0.018f, 0.03f), rq);
            }
            seed += 2;
            D.Blob(3, f.T(-0.9f, 2.52f, -0.2f), new Vector3(0.55f, 0.05f, 0.45f), seed, 0, 0.35f, -1);   // мох на кровле

            // штабель досок на поддоне у задней стенки
            for (int k = 0; k < 3; k++) Beam(W, 2, f.T(-1.05f + k * 1.0f, 0.04f, -0.95f), f.T(-1.05f + k * 1.0f, 0.04f, -0.15f), 0.05f, 0.04f);
            for (int layer = 0; layer < 12; layer++)
            {
                float ly = 0.12f + layer * 0.048f;
                float jx = (TreeMB.H(1110, layer) - 0.5f) * 0.14f;
                Quaternion lq = f.q * Quaternion.Euler(0f, (TreeMB.H(1111, layer) - 0.5f) * 3f, 0f);
                for (int b = -1; b <= 1; b++)
                    GroveGeo.Box(P, 0, f.T(-0.1f + jx, ly, -0.55f + b * 0.255f), new Vector3(1.05f, 0.02f, 0.115f), lq);
                if (layer % 2 == 1)
                    for (int s = -1; s <= 1; s += 2)
                        GroveGeo.Box(P, 3, f.T(-0.1f + s * 0.72f, ly + 0.026f, -0.55f), new Vector3(0.04f, 0.012f, 0.40f), lq);
            }
            Col("Col_ShedStack", f.T(-0.1f, 0.35f, -0.55f), f.q, new Vector3(2.2f, 0.7f, 0.9f));

            // козлы с брёвнышком слева у фронта
            Fr sh = new Fr { p = f.T(-0.95f, 0f, 0.5f), q = f.q * Quaternion.Euler(0f, 18f, 0f) };
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 fx = new Vector3(s * 0.42f, 0f, 0f);
                GroveGeo.Box(W, 2, sh.T(fx.x, 0.38f, 0.17f), new Vector3(0.04f, 0.42f, 0.04f), sh.q * Quaternion.Euler(14f, 0f, 0f));
                GroveGeo.Box(W, 2, sh.T(fx.x, 0.38f, -0.17f), new Vector3(0.04f, 0.42f, 0.04f), sh.q * Quaternion.Euler(-14f, 0f, 0f));
                GroveGeo.Box(W, 2, sh.T(fx.x, 0.50f, 0f), new Vector3(0.05f, 0.03f, 0.22f), sh.q);
            }
            LogTube(sh.T(-0.85f, 0.70f, 0f), sh.T(0.85f, 0.70f, 0f), 0.15f);
            Col("Col_ShedHorse", sh.T(0f, 0.45f, 0f), sh.q, new Vector3(1.8f, 0.9f, 0.6f));

            // фонарь на цепи под передней балкой
            Beam(W, 3, f.T(0.9f, 2.0f, 1.0f), f.T(0.9f, 1.74f, 1.0f), 0.008f, 0.008f);
            Vector3 lp = f.T(0.9f, 1.60f, 1.0f);
            Lantern(lp);
            realLamps.Add(lp);

            // земля под навесом и опилки
            Dirt(f.T(-0.9f, 0f, 0.3f).x, f.T(-0.9f, 0f, 0.3f).z, 1.05f, 0.9f, 1200);
            Dirt(f.T(0.9f, 0f, 0.0f).x, f.T(0.9f, 0f, 0.0f).z, 1.05f, 0.95f, 1210);
            Dirt(f.T(0f, 0f, 1.35f).x, f.T(0f, 0f, 1.35f).z, 1.2f, 0.6f, 1220);
            Chips(f.T(-0.4f, 0f, 0.5f).x, f.T(-0.4f, 0f, 0.5f).z, 1.6f, 22, 1230);
        }

        // ============================================================ повозка с брёвнами
        static void Wheel(Vector3 c, Quaternion q, float rad)
        {
            Vector3 zA = q * Vector3.forward;
            Vector3 a = c - zA * 0.045f, b = c + zA * 0.045f;
            W.Tube(2, new[] { a, b }, new[] { rad, rad }, 12, false);
            Disc(W, 2, a, -zA, rad, 12);
            Disc(W, 2, b, zA, rad, 12);
            for (int sp = 0; sp < 4; sp++)
            {
                Quaternion sq = q * Quaternion.Euler(0f, 0f, sp * 45f);
                GroveGeo.Box(P, 0, c + zA * 0.056f, new Vector3(rad * 0.9f, 0.022f, 0.011f), sq);
                GroveGeo.Box(P, 0, c - zA * 0.056f, new Vector3(rad * 0.9f, 0.022f, 0.011f), sq);
            }
            W.Tube(3, new[] { c - zA * 0.08f, c + zA * 0.08f }, new[] { 0.055f, 0.055f }, 6, false);
        }

        static void Wagon(float x, float z, float yaw)
        {
            var f = new Fr { p = new Vector3(x, Hy(x, z), z), q = Quaternion.Euler(0f, yaw, 0f) };
            const float Rw = 0.30f;
            // рама, борта из досок, стойки
            for (int s = -1; s <= 1; s += 2)
            {
                Beam(W, 2, f.T(-1.0f, 0.42f, s * 0.36f), f.T(1.0f, 0.42f, s * 0.36f), 0.045f, 0.04f);
                Beam(P, 0, f.T(-0.95f, 0.66f, s * 0.50f), f.T(0.95f, 0.66f, s * 0.50f), 0.015f, 0.075f);
                for (int k = 0; k < 4; k++)
                {
                    float px = -0.82f + k * 0.55f;
                    Beam(W, 2, f.T(px, 0.50f, s * 0.52f), f.T(px, 1.12f, s * 0.52f), 0.035f, 0.035f);
                }
            }
            GroveGeo.Box(W, 2, f.T(0f, 0.50f, 0f), new Vector3(0.97f, 0.03f, 0.50f), f.q);
            for (int e = -1; e <= 1; e += 2) GroveGeo.Box(P, 0, f.T(e * 0.97f, 0.70f, 0f), new Vector3(0.02f, 0.17f, 0.50f), f.q);
            // оси и колёса
            for (int ax = -1; ax <= 1; ax += 2)
            {
                Beam(W, 3, f.T(ax * 0.60f, Rw, -0.62f), f.T(ax * 0.60f, Rw, 0.62f), 0.035f, 0.035f);
                for (int s = -1; s <= 1; s += 2) Wheel(f.T(ax * 0.60f, Rw, s * 0.66f), f.q, Rw);
            }
            // оглобли и поперечина
            for (int s = -1; s <= 1; s += 2) Beam(W, 2, f.T(0.95f, 0.50f, s * 0.32f), f.T(2.35f, 0.09f, s * 0.26f), 0.03f, 0.03f);
            Beam(W, 2, f.T(2.35f, 0.09f, -0.26f), f.T(2.35f, 0.09f, 0.26f), 0.03f, 0.03f);
            // брёвна: три ряда
            const float r = 0.15f;
            int[] rowN = { 3, 2, 1 };
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < rowN[row]; i++)
                {
                    float lz = (i - (rowN[row] - 1) * 0.5f) * (r * 2.1f);
                    float ly = 0.53f + r + row * r * 1.78f;
                    float jit = (TreeMB.H(1300 + row, i) - 0.5f) * 0.16f;
                    float rr = r * (0.94f + 0.12f * TreeMB.H(1310 + row, i));
                    LogTube(f.T(-0.88f + jit, ly, lz), f.T(0.88f + jit, ly, lz), rr);
                }
            Col("Col_Wagon", f.T(0.2f, 0.7f, 0f), f.q, new Vector3(2.1f, 1.4f, 1.4f));
            Dirt(f.T(0f, 0f, 0f).x, f.T(0f, 0f, 0f).z, 1.5f, 1.1f, 1320);
            Chips(f.T(0.2f, 0f, 0.9f).x, f.T(0.2f, 0f, 0.9f).z, 1.1f, 8, 1330);
        }

        // ============================================================ поленница-пирамида на стойках
        static void LogPile(float x, float z, float yaw, int variant)
        {
            var f = new Fr { p = new Vector3(x, Hy(x, z), z), q = Quaternion.Euler(0f, yaw, 0f) };
            const float r = 0.19f;
            int[] rowN = variant == 0 ? new[] { 4, 3, 2 } : new[] { 3, 3, 2, 1 };
            for (int row = 0; row < rowN.Length; row++)
                for (int i = 0; i < rowN[row]; i++)
                {
                    float lz = (i - (rowN[row] - 1) * 0.5f) * (r * 2.04f);
                    float ly = r * 0.9f + row * r * 1.75f;
                    float jit = (TreeMB.H(1400 + variant * 9 + row, i) - 0.5f) * 0.2f;
                    float rr = r * (0.94f + 0.12f * TreeMB.H(1420 + variant, row * 5 + i));
                    LogTube(f.T(-0.95f + jit, ly, lz), f.T(0.95f + jit, ly, lz), rr);
                }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Beam(W, 2, f.T(sx * 1.08f, -0.1f, sz * 0.84f), f.T(sx * 1.08f, 0.95f, sz * 0.84f), 0.04f, 0.04f);
            Col("Col_LogPile", f.T(0f, 0.5f, 0f), f.q, new Vector3(2.2f, 1.0f, 1.2f));
            Chips(x, z, 1.5f, 8, 1440 + variant);
        }

        // ============================================================ лежащие брёвна: два рядом и одно сверху наискосок
        static void FallenLogs(float x, float z, float yaw, int variant)
        {
            var f = new Fr { p = new Vector3(x, Hy(x, z), z), q = Quaternion.Euler(0f, yaw, 0f) };
            float r1 = 0.30f, r2 = 0.26f;
            LogOnGround(f, -1.2f, 0f, 1.2f, 0.05f, r1);
            LogOnGround(f, -0.9f, 0.62f, 1.1f, 0.56f, r2);
            // верхнее наискосок
            Vector3 a = f.T(-0.7f, 0f, 0.55f), b = f.T(0.8f, 0f, 0.05f);
            a.y = Hy(a.x, a.z) + r2 * 1.9f; b.y = Hy(b.x, b.z) + r1 * 1.8f;
            LogTube(a, b, 0.19f);
            // сучки и мох
            for (int i = 0; i < 3; i++)
            {
                float t = 0.2f + 0.3f * i + 0.08f * TreeMB.H(1500 + variant, i);
                Vector3 pt = Vector3.Lerp(f.T(-1.2f, 0f, 0f), f.T(1.2f, 0f, 0.05f), t);
                pt.y = Hy(pt.x, pt.z) + r1 * 1.0f;
                Vector3 dir = (Vector3.up + f.q * new Vector3(0.4f * (i - 1), 0f, -0.9f)).normalized;
                W.Tube(0, new[] { pt, pt + dir * 0.28f }, new[] { 0.055f, 0.035f }, 5, false);
                Disc(W, 1, pt + dir * 0.28f, dir, 0.035f, 5);
            }
            seed += 3;
            Vector3 mc = f.T(0.2f, 0f, 0.05f);
            D.Blob(3, new Vector3(mc.x, Hy(mc.x, mc.z) + r1 * 1.55f, mc.z), new Vector3(0.45f, 0.05f, 0.14f), seed, 0, 0.35f, -1);
            Col("Col_FallenLogs", f.T(0f, 0.35f, 0.3f), f.q, new Vector3(2.6f, 0.7f, 1.3f));
            Chips(x, z, 1.4f, 6, 1520 + variant);
        }

        // ============================================================ пенёк с фонариком
        static void StumpLamp(float x, float z, int i)
        {
            float y = Hy(x, z);
            float r = 0.30f + 0.04f * (i % 2), h = 0.40f + 0.06f * (i % 3);
            Stump(new Vector3(x, y, z), r, h);
            Vector3 top = new Vector3(x + 0.05f, y + h, z);
            Lantern(top + Vector3.up * 0.15f);
            if (i == 0) realLamps.Add(top + Vector3.up * 0.2f);
            Col("Col_StumpLamp", new Vector3(x, y + h * 0.5f, z), Quaternion.identity, new Vector3(r * 1.9f, h, r * 1.9f));
            // щепа вокруг
            Chips(x, z, 0.9f, 5, 1600 + i);
        }

        // ============================================================ бочка и ящики
        static void Barrel(float x, float z, int i)
        {
            float y = Hy(x, z), h = 0.80f, r = 0.30f;
            Vector3 b = new Vector3(x, y, z);
            W.Tube(2, new[] { b, b + Vector3.up * h * 0.5f, b + Vector3.up * h }, new[] { r * 0.9f, r * 1.1f, r * 0.9f }, 10, false);
            Disc(W, 1, b + Vector3.up * h, Vector3.up, r * 0.88f, 10);
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? 0.20f : 0.80f;
                float rr = r * 0.98f + 0.014f;
                Vector3 c = b + Vector3.up * (h * t);
                W.Tube(3, new[] { c + Vector3.down * 0.025f, c + Vector3.up * 0.025f }, new[] { rr, rr }, 10, false);
            }
            Col("Col_Barrel", b + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(r * 2.1f, h, r * 2.1f));
        }

        static void Crates(float x, float z, int i)
        {
            float y = Hy(x, z);
            Vector3 b = new Vector3(x, y, z);
            Quaternion q = Quaternion.Euler(0f, TreeMB.H(1900, i) * 180f, 0f);
            GroveGeo.Box(P, 0, b + Vector3.up * 0.30f, new Vector3(0.34f, 0.30f, 0.34f), q);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    GroveGeo.Box(P, 3, b + q * new Vector3(sx * 0.34f, 0.30f, sz * 0.34f), new Vector3(0.035f, 0.31f, 0.035f), q);
            for (int s2 = -1; s2 <= 1; s2 += 2)
                GroveGeo.Box(P, 3, b + q * new Vector3(0f, 0.612f, s2 * 0.22f), new Vector3(0.36f, 0.012f, 0.04f), q);
            Quaternion q2 = q * Quaternion.Euler(0f, 24f, 0f);
            GroveGeo.Box(P, 0, b + q * new Vector3(0.04f, 0.83f, 0.02f), new Vector3(0.24f, 0.22f, 0.24f), q2);
            seed += 3;
            P.Blob(0, b + q * new Vector3(0.72f, 0.20f, 0.12f), new Vector3(0.28f, 0.22f, 0.24f), seed, 1, 0.30f, -1);   // мешок рядом
            Col("Col_Crates", b + Vector3.up * 0.5f, q, new Vector3(0.75f, 1.0f, 0.75f));
        }

        // ============================================================ камни и кусты
        static void Rocks(float x, float z, float w, int i)
        {
            float y = Hy(x, z);
            Vector3 p = new Vector3(x, y, z);
            seed += 4;
            float h = w * (0.65f + 0.3f * TreeMB.H(1700, i));
            D.Blob(2, p + Vector3.up * (h * 0.55f), new Vector3(w, h, w * 0.9f), seed, 1, 0.30f, -1);
            D.Blob(3, p + Vector3.up * (h * 1.2f), new Vector3(w * 0.65f, h * 0.30f, w * 0.6f), seed + 1, 0, 0.35f, -1);
            D.Blob(2, p + new Vector3(w * 1.0f, h * 0.2f, w * 0.3f), new Vector3(w * 0.45f, h * 0.35f, w * 0.4f), seed + 2, 0, 0.35f, -1);
            if (w > 0.55f) Col("Col_CampRock", p + Vector3.up * (h * 0.6f), Quaternion.identity, new Vector3(w * 1.7f, h * 1.2f, w * 1.6f));
        }

        static void Bush(float x, float z, float k, int i)
        {
            float y = Hy(x, z);
            Vector3 p = new Vector3(x, y, z);
            int main = (i % 3 == 1) ? 0 : 1;                // чаще бирюзовые, как на референсе
            seed += 4;
            S.Blob(main, p + new Vector3(0f, 0.42f * k, 0f), new Vector3(0.85f, 0.62f, 0.85f) * k, seed, 1, 0.30f, 2);
            S.Blob(main, p + new Vector3(0.58f * k, 0.30f * k, 0.25f * k), new Vector3(0.50f, 0.40f, 0.50f) * k, seed + 1, 1, 0.30f, 2);
            S.Blob(main, p + new Vector3(-0.48f * k, 0.28f * k, -0.32f * k), new Vector3(0.45f, 0.36f, 0.45f) * k, seed + 2, 1, 0.30f, 2);
            if (i % 2 == 0)
                for (int b = 0; b < 6; b++)
                {
                    float a = TreeMB.H(seed + 7, b) * Mathf.PI * 2f, up = 0.35f + 0.55f * TreeMB.H(seed + 9, b);
                    float rr = 0.78f * Mathf.Sqrt(1f - up * up) * k;
                    S.Blob(3, p + new Vector3(Mathf.Cos(a) * rr, 0.18f * k + up * 0.62f * k, Mathf.Sin(a) * rr), new Vector3(0.09f, 0.07f, 0.09f), seed + 20 + b, 0, 0.3f, -1);
                }
        }

        // ============================================================ столбы с фонарями и гирлянды
        static void PolesAndGarlands()
        {
            const int want = 6;
            var ok = new bool[want];
            var tops = new Vector3[want];
            for (int k = 0; k < want; k++)
            {
                float[] poleTh = { -150f, -95f, -42f, 42f, 95f, 150f };   // кольцом от глубины через вход: гирлянды образуют цепочку, над аркой тоже
                for (int i = 0; i < 50; i++)
                {
                    float a = gateA + (poleTh[k] + (i == 0 ? 0f : (TreeMB.H(900 + k, i * 2) - 0.5f) * 30f)) * Mathf.Deg2Rad;
                    float r = i == 0 ? 8.8f : Mathf.Lerp(7.0f, 9.0f, TreeMB.H(920 + k, i * 2 + 1));
                    float cx = Mathf.Cos(a) * r, cz = Mathf.Sin(a) * r;
                    if (!Fits(cx, cz, 0f, FpPole, 1.2f, 25f)) continue;
                    Reserve(cx, cz, 0f, FpPole);
                    tops[k] = Pole(cx, cz, k);
                    ok[k] = true;
                    nPole++;
                    placed++;
                    break;
                }
                if (!ok[k]) skipped++;
            }
            for (int k = 0; k < want; k++)
            {
                int n = (k + 1) % want;
                if (!ok[k] || !ok[n]) continue;
                Vector3 a = tops[k], b = tops[n];
                float len = Vector3.Distance(a, b);
                if (len < 2.5f || len > 13f) continue;
                if (!SegClear(new Vector2(a.x, a.z), new Vector2(b.x, b.z))) continue;
                Garland(a, b, 0.06f * len + 0.2f, 40 + k * 3);
                nGarland++;
            }
        }

        // столб: кривоватый, с каменным основанием, рукой и фонарём; возвращает точку крепления гирлянды (локальные координаты)
        static Vector3 Pole(float x, float z, int tag)
        {
            float y0 = Hy(x, z);
            float la = TreeMB.H(930, tag) * Mathf.PI * 2f;
            Vector3 b = new Vector3(x, y0 - 0.15f, z);
            Vector3 t = new Vector3(x + Mathf.Cos(la) * 0.10f, y0 + PoleH, z + Mathf.Sin(la) * 0.10f);
            W.Tube(2, new[] { b, (b + t) * 0.5f, t }, new[] { 0.085f, 0.07f, 0.06f }, 6, false);
            seed += 3;
            W.Blob(2, t + Vector3.up * 0.03f, new Vector3(0.09f, 0.055f, 0.09f), seed, 0, 0.1f, -1);
            D.Blob(2, new Vector3(x + 0.18f, y0 + 0.08f, z), new Vector3(0.22f, 0.15f, 0.19f), seed + 1, 0, 0.3f, -1);
            D.Blob(2, new Vector3(x - 0.14f, y0 + 0.06f, z + 0.12f), new Vector3(0.17f, 0.12f, 0.16f), seed + 2, 0, 0.3f, -1);
            Vector2 inv = new Vector2(-x, -z);
            if (inv.sqrMagnitude < 0.01f) inv = Vector2.up;
            inv.Normalize();
            Vector3 din = new Vector3(inv.x, 0f, inv.y);
            Vector3 at = t + Vector3.down * 0.20f;
            Vector3 ae = at + din * 0.36f;
            Beam(W, 3, at, ae, 0.012f, 0.012f);
            Beam(W, 3, ae, ae + Vector3.down * 0.10f, 0.006f, 0.006f);
            Vector3 lp = ae + Vector3.down * 0.30f;
            Lantern(lp);
            if (tag == 0 || tag == 3) realLamps.Add(lp);
            Col("Col_CampPole", new Vector3(x, y0 + PoleH * 0.5f, z), Quaternion.identity, new Vector3(0.25f, PoleH, 0.25f));
            return t + Vector3.down * 0.10f;
        }

        // гирлянда не должна проходить через деревья и над навесом, повозкой и поленницами
        static bool SegClear(Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = Mathf.Max(0.01f, ab.sqrMagnitude);
            for (int i = 0; i < treesW.Count; i++)
            {
                Vector2 p = new Vector2(treesW[i].x - O.x, treesW[i].y - O.z);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                if (Vector2.Distance(p, a + ab * t) < 1.5f) return false;
            }
            for (int i = 0; i < used.Count; i++)
            {
                if (used[i].z < 1.0f) continue;
                Vector2 p = new Vector2(used[i].x, used[i].y);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                if (Vector2.Distance(p, a + ab * t) < used[i].z * 0.85f) return false;
            }
            return true;
        }

        // верёвка с провисом, на ней флажки четырёх цветов, между флажками тёплые лампочки
        static void Garland(Vector3 a, Vector3 b, float sag, int tag)
        {
            const int segs = 14;
            var pts = new Vector3[segs + 1];
            var rad = new float[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                pts[i] = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                rad[i] = 0.012f;
            }
            P.Tube(2, pts, rad, 3, false);
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 0.55f));
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float t2 = Mathf.Min(1f, t + 0.02f);
                Vector3 pos = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                Vector3 pos2 = Vector3.Lerp(a, b, t2) + Vector3.down * (sag * 4f * t2 * (1f - t2));
                Vector3 dir = (pos2 - pos).normalized;
                Vector3 nrm = Vector3.Cross(dir, Vector3.up).normalized;
                float w = 0.12f, h = 0.24f + 0.06f * TreeMB.H(tag, i);
                float sw = (TreeMB.H(tag + 3, i) - 0.5f) * 0.06f;
                Vector3 p0 = pos - dir * w, p1 = pos + dir * w, p2 = pos + Vector3.down * h + nrm * sw;
                Vector3 mid = (p0 + p1 + p2) / 3f;
                int sub = (i + tag) % 4;
                Fl.Tri(sub, p0, p1, p2, mid + nrm);   // двусторонний флажок
                Fl.Tri(sub, p0, p1, p2, mid - nrm);
                if (i > 0)
                {
                    float tb = i / (float)n;
                    Vector3 bp = Vector3.Lerp(a, b, tb) + Vector3.down * (sag * 4f * tb * (1f - tb) + 0.045f);
                    seed += 2;
                    G.Blob(0, bp, new Vector3(0.04f, 0.05f, 0.04f), seed, 0, 0.1f, -1);
                    if (i % 4 == 2) glows.Add(bp);
                }
            }
        }

        // ============================================================ земля
        static bool DirtOk(float lx, float lz)
        {
            float wx = O.x + lx, wz = O.z + lz;
            if (!F.Inside(wx, wz) || F.EdgeDistance(wx, wz) < 1.2f) return false;
            if (F.IsWater(wx, wz) || F.IsFord(wx, wz) || F.WaterDistance(wx, wz) < 0.5f) return false;
            if (F.Slope(wx, wz) > 14f) return false;
            float d = Mathf.Sqrt(lx * lx + lz * lz);
            return d <= ChopZone.Rb(Mathf.Atan2(lz, lx)) - 1.0f;
        }

        // пятно утоптанной земли из трёх блобов (форма не круг)
        static void Dirt(float lx, float lz, float rx, float rz, int tag)
        {
            if (!DirtOk(lx, lz)) return;
            for (int i = 0; i < 3; i++)
            {
                float a = TreeMB.H(tag, i) * Mathf.PI * 2f;
                float ox = i == 0 ? 0f : Mathf.Cos(a) * rx * 0.45f, oz = i == 0 ? 0f : Mathf.Sin(a) * rz * 0.45f;
                float cx = lx + ox, cz = lz + oz;
                if (!DirtOk(cx, cz)) continue;
                seed += 2;
                float sc = i == 0 ? 1f : 0.7f;
                D.Blob(0, new Vector3(cx, Hy(cx, cz) + 0.048f, cz), new Vector3(rx * sc, 0.03f, rz * sc), seed, 1, 0.35f, -1);
            }
            nPatch++;
        }

        // щепа: маленькие плоские дощечки вразброс
        static void Chips(float lx, float lz, float radius, int count, int tag)
        {
            for (int i = 0; i < count; i++)
            {
                float a = TreeMB.H(tag, i * 2) * Mathf.PI * 2f, r = radius * Mathf.Sqrt(TreeMB.H(tag + 1, i * 2 + 1));
                float cx = lx + Mathf.Cos(a) * r, cz = lz + Mathf.Sin(a) * r;
                if (!DirtOk(cx, cz)) continue;
                GroveGeo.Box(D, 1, new Vector3(cx, Hy(cx, cz) + 0.062f, cz), new Vector3(0.09f, 0.010f, 0.04f), TreeMB.H(tag + 2, i) * 180f);
            }
        }

        static void Patches()
        {
            // земля (центральная поляна и тропы) строится в ChopZone.Ground; тут только щепа вокруг места игрока
            Chips(0f, 0f, 3.4f, 10, 1890);
        }

        // ============================================================ сцена
        static void AddMesh(string name, TreeMB mb, string asset, Material[] mats, bool shadows, bool collide = false)
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
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;   // сквозь доски, брёвна и столбы пройти нельзя (точный коллайдер по модели)
        }
    }
}
#endif

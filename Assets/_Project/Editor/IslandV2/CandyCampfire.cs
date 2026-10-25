#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/CandyCampfire.cs (Editor)
// Костёр в стиле Candy / Cloud: пухлые камни-пончики с мордашками, бревёнышки с кремовыми срезами, лавки с подушками, тренога с бантом,
// палатка-«мороженка», котелок с сердечком, зефирки на палочках, пламя из мягких капель, искры, дым-облачка и редкие сердечки.
//
// Меню: Tools/BuilderSim/Здания/Костёр Candy (облачный): собрать   (приоритет 303; старый «Campfire Builder» остаётся, 304)
// Контракт тот же, что у старого CampfireBuilder, поэтому игра, стройка и сохранения ничего не замечают:
//   Campfire (корень сцены, HutStages)
//     Stage1   «первое здание» (видно с самого начала): чаша из камней-пончиков с мордашками, холодный шалаш из поленьев (ColdLogs),
//              две лавки-бревна, поленница, колышки с гирляндами флажков (BuildMarkers, стройплощадка) и табличка с тремя звёздочками (горит одна)
//     Stage2   (после 1-й сдачи брёвен) стройплощадка уходит: ещё две лавки и подушки на всех четырёх, тренога с бантом, вторая звезда (палатку убрали)
//     Stage3   шалаш из холодных поленьев схлопывается, загорается пламя (Flames), CampLight, котелок, зефирки, искры, дым, сердечки, третья звезда
//     BuildPoint   куда встаёт игрок при стройке
//     BuildZone    невидимый триггер + BuildingController (цены этапов 4 и 4 бревна, как раньше)
// Детали, которые уходят при смене стадии (ColdLogs, BuildMarkers), висят под StageVisibility и не помечаются статичными.
// Посмотреть стадии без игры: меню Tools/BuilderSim/Здания/Костёр Candy: показать стадию 1 / 2 / 3 (305-307).
// Свет: объект «CampLight» FireSource делает сам в рантайме (BuildingLightLink), мерцание пламени: CampfireFlicker.
// Материалы (Assets/_Game/Campfire): Candy_Camp (Project/CandyToon, цвет в вершинах), Candy_Flame (свечение), Candy_Heart, Candy_Motes (Project/CandyParticle).
// ЦЕНТР v2 (читаемость): тёмная копчёная чаша под огнём (HearthGround), тёмные поленья, крупное пламя с углями (FlameMesh),
//   котелок поднят над огнём (PotLift), сидения: 2 лавки-бревна, 2 лавки-доски на ножках, 4 пня (один с топором). Новый код: CandyCampfire.Hearth.cs.
// Положение: метка IslandV2/Markers/CAMPFIRE_Point (или где стоял прежний «Campfire»), высота по лучу в Terrain-коллайдер.
// Углы лавок, палатки и поленьев подбираются так, чтобы не перекрывать тропы (если в проекте есть раскладка острова).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static partial class CandyCampfire
    {
        public const string Dir = "Assets/_Game/Campfire";
        public const string RootName = "Campfire";
        const float R = 0.95f;   // радиус кольца камней

        // ---------------------------------------------------------------- палитра (sRGB hex -> линейный)
        static readonly Color Cream = SoftMesh.C(0xFFF1CF), Peach = SoftMesh.C(0xFFC9A8), Pink = SoftMesh.C(0xFFB5CC), Lav = SoftMesh.C(0xCDB8F0),
            Mint = SoftMesh.C(0xA6E8CB), White = SoftMesh.C(0xFFFAF0), Ash = SoftMesh.C(0xDDD3EA), WoodL = SoftMesh.C(0xE8B67E), WoodD = SoftMesh.C(0xB97F52),
            WoodCap = SoftMesh.C(0xFFE9C4), Char = SoftMesh.C(0x6E4B5C), Eye = SoftMesh.C(0x4A3550), Blush = SoftMesh.C(0xFF9DB5), Plum = SoftMesh.C(0x7A5B8C),
            GrassA = SoftMesh.C(0x8EE0A8), GrassB = SoftMesh.C(0xC4F29A), Gold = SoftMesh.C(0xF3C77A), Toast = SoftMesh.C(0xD9955A);
        static readonly Color[] Stones = { SoftMesh.C(0xE9DEF7), SoftMesh.C(0xFFE8D6), SoftMesh.C(0xD8F2E6), SoftMesh.C(0xF7D4E4) };
        static readonly Color[] Flowers = { SoftMesh.C(0xFFFFFF), SoftMesh.C(0xFFE066), SoftMesh.C(0xFF9EC4), SoftMesh.C(0xB79BFF) };
        static readonly Color[] Pastels = { SoftMesh.C(0xFFB5CC), SoftMesh.C(0xA6E8CB), SoftMesh.C(0xCDB8F0), SoftMesh.C(0xFFC9A8) };

        sealed class Ctx
        {
            public System.Random rng;
            public IslandField field;
            public Vector2 center;
            public Material camp, flame, heart, motes;
            public Transform colRoot;
            public float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
            public float Jf(float a) { return Rf(-a, a); }
        }

        // ================================================================ меню
        [MenuItem("Tools/BuilderSim/Здания/Костёр Candy (облачный): собрать", false, 303)]
        static void MenuBuild() { Build(true); }

        // высота пола на радиусе r от костра: земля у костра, дальше плиты площадки (+0.07) и мозаика (+0.10)
        static float Gy(float r) { return r < 1.45f ? 0f : (r < 4.0f ? 0.10f : 0.07f); }

        public static GameObject Build(bool select)
        {
            if (Application.isPlaying) { Debug.LogError("Костёр Candy: собирается вне Play Mode."); return null; }
            IslandLayout.EnsureFolder(Dir);

            Vector3 pos = FindCenter();
            var ctx = new Ctx { rng = new System.Random(777) };
            try { ctx.field = new IslandField(IslandLayout.GetOrCreate()); } catch { ctx.field = null; }
            ctx.center = new Vector2(pos.x, pos.z);
            GetMaterials(ctx);

            // старый костёр (любой «Campfire» в корне сцены) убираем: второго быть не должно
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == RootName) Undo.DestroyObjectImmediate(go);

            Physics.SyncTransforms();
            float gy;
            if (!GroundY(pos, null, out gy)) { gy = pos.y; Debug.LogWarning("Костёр Candy: земля под костром не найдена (сначала «2. Собрать блокаут»), высота взята из метки."); }

            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Костёр Candy");
            rootGo.transform.SetPositionAndRotation(new Vector3(pos.x, gy, pos.z), Quaternion.identity);
            Transform root = rootGo.transform;

            var s1 = NewChild(root, "Stage1");
            var s2 = NewChild(root, "Stage2");
            var s3 = NewChild(root, "Stage3");
            int t1 = Stage1(ctx, s1), t2 = Stage2(ctx, s2), t3 = Stage3(ctx, s3);

            var bp = NewChild(root, "BuildPoint");
            float by;
            Vector3 bw = new Vector3(pos.x, gy + 50f, pos.z - R - 2.6f);
            bp.position = GroundY(bw, root, out by) ? new Vector3(bw.x, by, bw.z) : new Vector3(bw.x, gy, bw.z);
            bp.rotation = Quaternion.identity;

            var stages = rootGo.AddComponent<HutStages>();
            stages.Configure(s1.gameObject, s2.gameObject, s3.gameObject, bp, 1);
            stages.SetStage(3);
            BuildZone(root, stages);
            ApplyStatic(root);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            if (select) Selection.activeGameObject = rootGo;
            Debug.Log("Костёр Candy собран: Tris этап 1 = " + t1 + ", этап 2 = " + t2 + ", этап 3 = " + t3 + ". Позиция " + rootGo.transform.position
                      + ". В игре стартует с этапа 1; посмотреть этапы: компонент HutStages на «Campfire».");
            return rootGo;
        }

        // ================================================================ позиция и земля
        static Vector3 FindCenter()
        {
            var m = GameObject.Find("IslandV2/Markers/CAMPFIRE_Point");
            if (m != null) return m.transform.position;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == RootName) return go.transform.position;
            return Vector3.zero;
        }

        static bool GroundY(Vector3 wp, Transform ignore, out float y)
        {
            y = 0f;
            var hits = Physics.RaycastAll(new Vector3(wp.x, Mathf.Max(wp.y, 0f) + 50f, wp.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool ok = false;
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.collider.transform.name == RootName || (h.collider.transform.parent != null && h.collider.transform.IsChildOf(FindOldRoot()))) continue;
                string n = h.collider.gameObject.name;
                if (!(h.collider is TerrainCollider) && n != "Ground" && !n.StartsWith("Terrain")) continue;
                if (h.distance < best) { best = h.distance; y = h.point.y; ok = true; }
            }
            return ok;
        }

        static Transform FindOldRoot()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == RootName) return go.transform;
            return new GameObject("__none").transform;   // сразу удалится ниже: см. Cleanup
        }

        static float FreeAngle(Ctx c, float r, float deg, float minPath)
        {
            if (c.field == null) return deg * Mathf.Deg2Rad;
            for (int k = 0; k < 24; k++)
            {
                float a = (deg + k * 8f) * Mathf.Deg2Rad;
                if (c.field.PathAxisDistance(c.center.x + Mathf.Cos(a) * r, c.center.y + Mathf.Sin(a) * r) >= minPath) return a;
            }
            return deg * Mathf.Deg2Rad;
        }

        // ================================================================ материалы
        static void GetMaterials(Ctx c)
        {
            Shader toon = Shader.Find("Project/CandyToon");
            if (toon == null)
            {
                Debug.LogWarning("Костёр Candy: нет шейдера Project/CandyToon (дождись компиляции), беру URP/Lit.");
                toon = Shader.Find("Universal Render Pipeline/Lit");
            }
            c.camp = Mat("Candy_Camp", toon, m =>
            {
                Set(m, "_BaseColor", Color.white); Set(m, "_VertexColorStrength", 1f); Set(m, "_PosVary", 0.08f);
                Set(m, "_Level0", 0.66f); Set(m, "_Level1", 0.86f); Set(m, "_Level2", 1.0f); Set(m, "_StepSoft", 0.12f);
                Set(m, "_ShadowTint", new Color(0.80f, 0.74f, 1f, 1f)); Set(m, "_ShadowStrength", 0.7f);
                Set(m, "_GlossStrength", 0.35f); Set(m, "_GlossSize", 0.16f); Set(m, "_GlossSoft", 0.05f);
                Set(m, "_RimColor", new Color(1f, 0.93f, 0.82f, 1f)); Set(m, "_RimStrength", 0.35f); Set(m, "_RimPower", 2.6f);
            });
            c.flame = Mat("Candy_Flame", toon, m =>
            {
                Set(m, "_BaseColor", Color.white); Set(m, "_VertexColorStrength", 1f); Set(m, "_PosVary", 0f);
                Set(m, "_Level0", 1.0f); Set(m, "_Level1", 1.0f); Set(m, "_Level2", 1.0f);
                Set(m, "_ShadowStrength", 0f); Set(m, "_GlossStrength", 0f); Set(m, "_RimStrength", 0f);
                Set(m, "_EmissionColor", new Color(0.95f, 0.45f, 0.22f, 1f) * 0.9f);
            });
            c.heart = Mat("Candy_Heart", toon, m =>
            {
                Set(m, "_BaseColor", Color.white); Set(m, "_VertexColorStrength", 1f); Set(m, "_PosVary", 0f);
                Set(m, "_Level0", 1.1f); Set(m, "_Level1", 1.1f); Set(m, "_Level2", 1.1f);
                Set(m, "_ShadowStrength", 0f); Set(m, "_GlossStrength", 0f); Set(m, "_RimStrength", 0f);
                Set(m, "_EmissionColor", new Color(1f, 0.35f, 0.55f, 1f) * 0.8f);
            });
            Shader part = Shader.Find("Project/CandyParticle");
            if (part == null) { Debug.LogWarning("Костёр Candy: нет шейдера Project/CandyParticle: искр и дыма не будет."); c.motes = null; }
            else c.motes = Mat("Candy_Motes", part, m => { Set(m, "_Tint", Color.white); Set(m, "_Softness", 0.7f); });
        }

        // общий материал «Candy_Camp» для украшений площадки (IslandBlockout.PlazaCandy.cs): те же настройки, что у костра
        public static Material CampMaterial()
        {
            IslandLayout.EnsureFolder(Dir);
            var c = new Ctx { rng = new System.Random(1) };
            GetMaterials(c);
            AssetDatabase.SaveAssets();
            return c.camp;
        }

        static Material Mat(string name, Shader sh, System.Action<Material> setup)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            setup(m);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        static void Set(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }

        // ================================================================ утилиты
        static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
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

        // меш в сцену: один объект, один материал
        static GameObject Flush(SoftMesh sm, Transform parent, string objName, string meshName, Material mat)
        {
            if (sm.Tris == 0) return null;
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(sm.ToMesh(meshName), meshName);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        static void AddCol(Ctx c, Transform stage, Vector3 center, Vector3 size, float yaw)
        {
            var colRoot = stage.Find("Colliders");
            if (colRoot == null) colRoot = NewChild(stage, "Colliders");
            var go = new GameObject("Col");
            go.transform.SetParent(colRoot, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<BoxCollider>().size = size;
        }

        // ================================================================ меню: посмотреть стадии в редакторе
        [MenuItem("Tools/BuilderSim/Здания/Костёр Candy: показать стадию 1 (первое здание)", false, 305)]
        static void ShowS1() { ShowStage(1); }
        [MenuItem("Tools/BuilderSim/Здания/Костёр Candy: показать стадию 2 (лагерь)", false, 306)]
        static void ShowS2() { ShowStage(2); }
        [MenuItem("Tools/BuilderSim/Здания/Костёр Candy: показать стадию 3 (горит)", false, 307)]
        static void ShowS3() { ShowStage(3); }

        static void ShowStage(int n)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name != RootName) continue;
                var hs = go.GetComponent<HutStages>();
                if (hs == null) { Debug.LogWarning("Костёр Candy: на «Campfire» нет HutStages."); return; }
                hs.SetStage(n);
                EditorSceneManager.MarkSceneDirty(go.scene);
                return;
            }
            Debug.LogWarning("Костёр Candy: в сцене нет «Campfire». Сначала Tools/BuilderSim/Здания/Костёр Candy (облачный): собрать.");
        }

        static float TanYaw(float a) { return Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg; }

        // ================================================================ ЭТАП 1: «первое здание»
        // Читается с первого взгляда: каменная чаша с холодным шалашом из поленьев (после 2-го этапа он «схлопывается»), две лавки-бревна,
        // поленница, колышки с флажками (стройплощадка, уходят на 2-м этапе), табличка с тремя звёздочками (горит одна).
        static int Stage1(Ctx c, Transform parent)
        {
            int tris = 0;
            var s = new SoftMesh();
            // зола: мягкая лепёшка
            HearthGround(s);   // тёмная копчёная чаша: на ней и холодный шалаш, и пламя читаются с первого взгляда

            // чаша из камней-пончиков, у части мордашки
            int n = 12;
            int[] faceAt = { 1, 5, 9 };
            for (int i = 0; i < n; i++)
            {
                float a = (i + c.Jf(0.1f)) * Mathf.PI * 2f / n, r = R + c.Jf(0.03f);
                float rx = c.Rf(0.23f, 0.30f), rz = c.Rf(0.18f, 0.23f), ry = c.Rf(0.15f, 0.22f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, ry * 0.6f - 0.02f, Mathf.Sin(a) * r);
                float yaw = TanYaw(a) + c.Jf(8f);
                s.Puff(p, new Vector3(rx, ry, rz), Stones[i % Stones.Length], yaw, 9, 5, 0.22f);
                foreach (int fi in faceAt) if (fi == i) Face(s, p, a, rx, ry, rz, yaw);
            }

            // две лавки-бревна сразу (остальные две и подушки на всех четырёх добавит 2-й этап)
            for (int i = 0; i < 4; i++)
                if (BenchStage[i] == 1) BenchLog(s, BenchAngle(c, i), i);
            StumpSeats(c, s, 1);

            // поленница между лавками: два внизу и одно сверху, торцы кремовые
            float la = FreeAngle(c, 1.95f, 150f, 1.9f);
            Vector3 lc = new Vector3(Mathf.Cos(la) * 1.95f, 0f, Mathf.Sin(la) * 1.95f);
            Vector3 along = new Vector3(-Mathf.Sin(la), 0f, Mathf.Cos(la)), side = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la));
            float by = Gy(1.95f) - 0.02f;
            for (int k = 0; k < 2; k++)
            {
                Vector3 o = lc + side * ((k - 0.5f) * 0.22f) + Vector3.up * (by + 0.1f);
                s.Cyl(o - along * 0.42f, o + along * 0.42f, 0.105f, 0.1f, k == 0 ? WoodL : WoodD, WoodCap);
            }
            Vector3 top = lc + Vector3.up * (by + 0.27f);
            s.Cyl(top - along * 0.4f, top + along * 0.4f, 0.1f, 0.095f, WoodL, WoodCap);

            // веточки у чаши
            for (int i = 0; i < 4; i++)
            {
                float a = c.Rf(0f, Mathf.PI * 2f), r = R + c.Rf(0.35f, 0.7f);
                Vector3 b = new Vector3(Mathf.Cos(a) * r, Gy(r) - 0.01f + 0.02f, Mathf.Sin(a) * r);
                Vector3 d = new Vector3(Mathf.Cos(a + 1.2f), 0f, Mathf.Sin(a + 1.2f)) * c.Rf(0.25f, 0.4f);
                s.Cyl(b, b + d + Vector3.up * 0.02f, 0.016f, 0.012f, WoodD, WoodD, true, true, 5);
            }

            // травка-тюфячки и цветочки вокруг чаши
            for (int i = 0; i < 6; i++)
            {
                float a = c.Rf(0f, Mathf.PI * 2f), r = R + c.Rf(0.45f, 0.95f);
                Vector3 b = new Vector3(Mathf.Cos(a) * r, Gy(r) - 0.03f, Mathf.Sin(a) * r);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 o = b + new Vector3(c.Jf(0.07f), 0f, c.Jf(0.07f));
                    s.Teardrop(o, c.Rf(0.16f, 0.26f), 0.04f, new Vector3(c.Jf(0.07f), 0f, c.Jf(0.07f)), GrassA, GrassB, 5, 4);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                float a = c.Rf(0f, Mathf.PI * 2f), r = R + c.Rf(0.6f, 1.1f);
                Vector3 b = new Vector3(Mathf.Cos(a) * r, Gy(r) - 0.03f, Mathf.Sin(a) * r);
                float hgt = c.Rf(0.16f, 0.26f);
                s.Cyl(b, b + Vector3.up * hgt, 0.012f, 0.01f, GrassA, GrassA, false, false, 4);
                s.Puff(b + Vector3.up * (hgt + 0.02f), new Vector3(0.05f, 0.04f, 0.05f), Flowers[i % Flowers.Length], 0f, 6, 3, 0.15f);
                s.Puff(b + Vector3.up * (hgt + 0.045f), new Vector3(0.022f, 0.02f, 0.022f), Cream, 0f, 5, 3, 0.1f);
            }

            Flush(s, parent, "Mesh_Solid", "CandyCamp_S1", c.camp);
            tris += s.Tris;
            AddCol(c, parent, new Vector3(0f, 0.15f, 0f), new Vector3(R * 2f + 0.5f, 0.3f, R * 2f + 0.5f), 0f);
            for (int i = 0; i < 4; i++)
                if (BenchStage[i] == 1) BenchCol(c, parent, BenchAngle(c, i));
            StumpCols(c, parent, 1);

            // табличка «Костёр»: три звёздочки по числу этапов, на 1-м горит одна
            var sg = new SoftMesh();
            SignMesh(sg, c);
            Flush(sg, parent, "Sign", "CandyCamp_S1Sign", c.camp);
            tris += sg.Tris;

            // холодный шалаш из светлых поленьев в чаше: на 3-м этапе «схлопывается», и загорается огонь
            var cold = NewChild(parent, "ColdLogs");
            var cl = new SoftMesh();
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f + 0.2f;
                Vector3 outer = new Vector3(Mathf.Cos(a) * 0.68f, 0.10f, Mathf.Sin(a) * 0.68f);
                Vector3 inner = new Vector3(Mathf.Cos(a) * 0.07f, 0.82f, Mathf.Sin(a) * 0.07f);
                cl.Cyl(outer, inner, 0.10f, 0.075f, i % 2 == 0 ? LogMid : LogDark, WoodCap, true, true, 7);
            }
            for (int i = 0; i < 5; i++)   // лучинки внутри
            {
                float a = i * Mathf.PI * 2f / 5f + 0.9f;
                cl.Cyl(new Vector3(Mathf.Cos(a) * 0.3f, 0.07f, Mathf.Sin(a) * 0.3f), new Vector3(Mathf.Cos(a) * 0.04f, 0.3f, Mathf.Sin(a) * 0.04f), 0.02f, 0.014f, WoodCap, WoodCap, false, false, 5);
            }
            Flush(cl, cold, "Mesh_Logs", "CandyCamp_S1Logs", c.camp);
            tris += cl.Tris;
            var vCold = cold.gameObject.AddComponent<StageVisibility>();
            vCold.minStage = 1; vCold.maxStage = 2; vCold.exitStyle = StageVisibility.ExitStyle.Pop; vCold.exitTime = 1.0f;

            // колышки с бантиками и гирляндой флажков: «здесь строят». Проход (южная сторона, где встаёт игрок) свободен. Уходят на 2-м этапе
            var mk = NewChild(parent, "BuildMarkers");
            tris += BuildMarkers(c, mk);
            var vMk = mk.gameObject.AddComponent<StageVisibility>();
            vMk.minStage = 1; vMk.maxStage = 1; vMk.exitStyle = StageVisibility.ExitStyle.Pop; vMk.exitTime = 1.2f;
            return tris;
        }

        // ---------------------------------------------------------------- стройплощадка: колышки и гирлянды флажков
        const float StakeR = 3.15f;

        static int BuildMarkers(Ctx c, Transform mk)
        {
            int tris = 0;
            Vector3[] sb = new Vector3[6];
            float[] sh = new float[6];
            bool[] on = new bool[6];
            for (int k = 0; k < 6; k++)
            {
                if (k == 4) continue;   // 270 градусов: проход
                float a = (30f + 60f * k) * Mathf.Deg2Rad;
                bool gate = (k == 3 || k == 5);
                Vector3 b = new Vector3(Mathf.Cos(a) * StakeR, Gy(StakeR) - 0.02f, Mathf.Sin(a) * StakeR);
                float h = gate ? 1.35f : 1.05f;
                sb[k] = b; sh[k] = h; on[k] = true;
                var st = new SoftMesh();
                st.Cyl(b, b + Vector3.up * h, 0.045f, 0.035f, WoodD, WoodCap, true, true, 7);
                st.Puff(b + Vector3.up * (h + 0.03f), new Vector3(0.07f, 0.065f, 0.07f), Pastels[k % 4], 0f, 7, 4, 0.2f);
                if (gate)
                {
                    Vector3 tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    st.Heart(b + Vector3.up * (h + 0.30f), tang, Vector3.up, 0.22f, Pink);
                }
                FlushAt(st, mk, "Stake" + k, "CandyCamp_S1_Stake" + k, c.camp, b);
                tris += st.Tris;
            }
            int[,] pairs = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 5, 0 } };
            for (int q = 0; q < pairs.GetLength(0); q++)
            {
                int k0 = pairs[q, 0], k1 = pairs[q, 1];
                if (!on[k0] || !on[k1]) continue;
                Vector3 P0 = sb[k0] + Vector3.up * (sh[k0] - 0.12f), P1 = sb[k1] + Vector3.up * (sh[k1] - 0.12f);
                var rs = new SoftMesh();
                const int m = 8;
                Vector3 prev = P0;
                for (int j = 1; j <= m; j++)
                {
                    float t = (float)j / m;
                    Vector3 p = Vector3.Lerp(P0, P1, t);
                    p.y -= 0.16f * 4f * t * (1f - t);
                    rs.Cyl(prev, p, 0.012f, 0.012f, Cream, Cream, false, false, 4);
                    prev = p;
                }
                Vector3 dir = P1 - P0; dir.y = 0f; dir.Normalize();
                Vector3 nrm = Vector3.Cross(dir, Vector3.up);
                for (int j = 0; j < 6; j++)
                {
                    float t = (j + 0.5f) / 6f;
                    Vector3 p = Vector3.Lerp(P0, P1, t);
                    p.y -= 0.16f * 4f * t * (1f - t) + 0.01f;
                    Vector3 l = p - dir * 0.10f, r = p + dir * 0.10f, tip = p + Vector3.down * 0.21f;
                    Color col = Pastels[(j + k0) % 4];
                    rs.Tri(l, r, tip, col, nrm);
                    rs.Tri(l, r, tip, col, -nrm);
                }
                Vector3 pivot = (P0 + P1) * 0.5f; pivot.y = sb[k0].y;
                FlushAt(rs, mk, "Bunting" + k0, "CandyCamp_S1_Bunting" + k0, c.camp, pivot);
                tris += rs.Tris;
            }
            return tris;
        }

        // ---------------------------------------------------------------- лавки-бревна: 120 и 300 градусов с 1-го этапа, 30 и 210 со 2-го
        static readonly float[] BenchDeg = { 30f, 120f, 210f, 300f };
        static readonly int[] BenchStage = { 2, 1, 2, 1 };
        const float SeatR = 2.25f;

        static float BenchAngle(Ctx c, int i) { return FreeAngle(c, SeatR, BenchDeg[i], 1.9f); }

        static Vector3 BenchPos(float a) { return new Vector3(Mathf.Cos(a) * SeatR, Gy(SeatR) - 0.03f, Mathf.Sin(a) * SeatR); }

        static void BenchLog(SoftMesh s, float a, int i)
        {
            Vector3 p = BenchPos(a);
            if ((i & 1) == 0) { PlankBench(s, a, i); return; }   // 30 и 210 градусов: лавка-доска на пнях, 120 и 300: бревно
            Vector3 tan = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            s.Cyl(p - tan * 0.68f + Vector3.up * 0.19f, p + tan * 0.68f + Vector3.up * 0.19f, 0.19f, 0.19f, LogMid, WoodCap, true, true, 9);
            s.Cyl(p - tan * 0.30f + Vector3.up * 0.37f, p + tan * 0.30f + Vector3.up * 0.37f, 0.03f, 0.03f, LogDark, LogDark, false, false, 5);   // сучок-бугорок
            for (int k = 0; k < 2; k++)   // камешки-упоры по концам
                s.Puff(p + tan * (k == 0 ? -0.80f : 0.80f) + Vector3.up * 0.09f, new Vector3(0.13f, 0.10f, 0.13f), Stones[(i + k) % Stones.Length], 0f, 7, 4, 0.2f);
        }

        static void BenchCushion(SoftMesh s, float a, int i)
        {
            Vector3 p = BenchPos(a);
            float yaw = TanYaw(a);
            Color cc = Pastels[i % Pastels.Length];
            s.Puff(p + Vector3.up * 0.37f, new Vector3(0.52f, 0.1f, 0.23f), cc, yaw, 9, 5, 0.22f);
            s.Puff(p + Vector3.up * 0.46f, new Vector3(0.035f, 0.02f, 0.035f), SoftMesh.Shade(cc, 0.7f), yaw, 6, 3, 0.1f);
        }

        static void BenchCol(Ctx c, Transform stage, float a)
        {
            Vector3 p = BenchPos(a);
            AddCol(c, stage, new Vector3(p.x, 0.25f, p.z), new Vector3(1.65f, 0.5f, 0.6f), TanYaw(a));
        }

        // ---------------------------------------------------------------- табличка «Костёр»: три звёздочки = три этапа
        // Стоит на краю площадки (60 градусов, севернее игрока) лицом к центру, камера с юга видит лицевую сторону.
        static void SignPose(Ctx c, out Vector3 pos, out Vector3 f)
        {
            float a = FreeAngle(c, 3.95f, 60f, 1.9f);
            pos = new Vector3(Mathf.Cos(a) * 3.95f, Gy(3.95f) - 0.02f, Mathf.Sin(a) * 3.95f);
            f = new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a));
        }

        static void SignMesh(SoftMesh s, Ctx c)
        {
            Vector3 pos, f; SignPose(c, out pos, out f);
            Quaternion q = Quaternion.LookRotation(f, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, f);
            Vector3 bc = pos + Vector3.up * 0.95f;
            s.Cyl(pos, pos + Vector3.up * 1.25f, 0.05f, 0.042f, WoodD, WoodCap, true, true, 7);
            s.Box(bc - f * 0.012f, new Vector3(0.96f, 0.46f, 0.05f), q, WoodD, 1.05f);
            s.Box(bc + f * 0.012f, new Vector3(0.86f, 0.36f, 0.04f), q, WoodCap, 1.05f);
            // огонёк-топпер над табличкой
            s.Puff(pos + Vector3.up * 1.27f, new Vector3(0.07f, 0.04f, 0.07f), Cream, 0f, 7, 3, 0.2f);
            s.Teardrop(pos + Vector3.up * 1.29f, 0.24f, 0.075f, Vector3.zero, SoftMesh.C(0xFF8447), SoftMesh.C(0xFFD45E), 7, 5);
            // три пустых гнезда и первая золотая звезда
            Color slot = SoftMesh.C(0xCBBFE0);
            for (int k = 0; k < 3; k++)
                s.Star(bc + right * ((k - 1) * 0.27f) + f * 0.04f, right, Vector3.up, 0.11f, 0.05f, 5, slot);
            SignStar(s, c, 0);
        }

        // золотая звезда номер k (0..2) поверх серого гнезда: 2-й этап зажигает вторую, 3-й третью
        static void SignStar(SoftMesh s, Ctx c, int k)
        {
            Vector3 pos, f; SignPose(c, out pos, out f);
            Vector3 right = Vector3.Cross(Vector3.up, f);
            Vector3 bc = pos + Vector3.up * 0.95f;
            s.Star(bc + right * ((k - 1) * 0.27f) + f * 0.052f, right, Vector3.up, 0.115f, 0.052f, 5, SoftMesh.C(0xFFD45E));
        }

        // меш с собственной точкой вращения (нужен деталям, которые уходят при смене стадии: StageVisibility двигает их вокруг pivot)
        static GameObject FlushAt(SoftMesh sm, Transform parent, string objName, string meshName, Material mat, Vector3 pivot)
        {
            if (sm.Tris == 0) return null;
            for (int i = 0; i < sm.v.Count; i++) sm.v[i] -= pivot;
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(sm.ToMesh(meshName), meshName);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        // мордашка на камне: глазки-бусинки и румянец
        static void Face(SoftMesh s, Vector3 stone, float a, float rx, float ry, float rz, float yaw)
        {
            Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), t = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 f = stone + o * (rz * 0.92f) + Vector3.up * (ry * 0.18f);
            s.Puff(f - t * (rx * 0.30f), new Vector3(0.026f, 0.03f, 0.016f), Eye, yaw, 6, 3, 0.1f);
            s.Puff(f + t * (rx * 0.30f), new Vector3(0.026f, 0.03f, 0.016f), Eye, yaw, 6, 3, 0.1f);
            s.Puff(f - t * (rx * 0.58f) - Vector3.up * 0.035f, new Vector3(0.04f, 0.022f, 0.012f), Blush, yaw, 6, 3, 0.05f);
            s.Puff(f + t * (rx * 0.58f) - Vector3.up * 0.035f, new Vector3(0.04f, 0.022f, 0.012f), Blush, yaw, 6, 3, 0.05f);
            s.Puff(f - Vector3.up * 0.05f, new Vector3(0.022f, 0.008f, 0.012f), Eye, yaw, 6, 3, 0.05f);   // ротик
        }

        // ================================================================ ЭТАП 2: лагерь
        static int Stage2(Ctx c, Transform parent)
        {
            var s = new SoftMesh();

            // лавки: две недостающие брёвна (30 и 210 градусов) + пастельные подушки с пуговкой на всех четырёх
            for (int i = 0; i < 4; i++)
            {
                float a = BenchAngle(c, i);
                if (BenchStage[i] == 2) { BenchLog(s, a, i); BenchCol(c, parent, a); }
                BenchCushion(s, a, i);
            }
            StumpSeats(c, s, 2);
            StumpCols(c, parent, 2);

            // тренога с бантом: три ножки сходятся над огнём
            Vector3 apex = new Vector3(0f, 2.2f + PotLift, 0f);   // выше, чтобы котелок не закрывал пламя сверху
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                Vector3 b = new Vector3(Mathf.Cos(a) * 1.4f, Gy(1.4f) - 0.02f, Mathf.Sin(a) * 1.4f);
                s.Cyl(b, apex + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.05f, 0.05f, 0.036f, WoodL, WoodCap, true, false, 7);
            }
            s.Puff(apex + Vector3.up * 0.01f, new Vector3(0.1f, 0.08f, 0.1f), Pink, 0f, 8, 4, 0.2f);
            s.Puff(apex + new Vector3(0.1f, 0.0f, 0f), new Vector3(0.08f, 0.055f, 0.045f), Pink, 20f, 7, 4, 0.2f);   // бант: два лепестка
            s.Puff(apex + new Vector3(-0.1f, 0.0f, 0f), new Vector3(0.08f, 0.055f, 0.045f), Pink, -20f, 7, 4, 0.2f);
            s.Teardrop(apex + new Vector3(0.05f, -0.03f, 0.03f), -0.16f, 0.03f, new Vector3(0.03f, 0f, 0f), Pink, Pink, 5, 3);

            SignStar(s, c, 1);   // вторая звезда на табличке
            Flush(s, parent, "Mesh_Solid", "CandyCamp_S2", c.camp);
            return s.Tris;
        }

        static void TentMesh(SoftMesh s, Ctx c, Vector3 b, Vector3 front)
        {
            const int sec = 8;
            const float rBase = 1.0f, rMid = 0.62f, hMid = 0.72f, hTop = 1.55f;
            Vector3 apex = b + Vector3.up * hTop;
            Color[] stripe = { SoftMesh.C(0xFFB5CC), SoftMesh.C(0xFFF3D6) };
            for (int i = 0; i < sec; i++)
            {
                float a0 = Mathf.PI * 2f * i / sec, a1 = Mathf.PI * 2f * (i + 1) / sec, am = (a0 + a1) * 0.5f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)), dm = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                Color col = stripe[i & 1];
                Vector3 b0 = b + d0 * rBase, b1 = b + d1 * rBase;
                Vector3 m0 = b + d0 * rMid + Vector3.up * hMid, m1 = b + d1 * rMid + Vector3.up * hMid;
                s.Quad(b0, b1, m1, m0, SoftMesh.Shade(col, 0.96f), dm + Vector3.up * 0.4f);
                s.Tri(m0, m1, apex, col, dm + Vector3.up * 0.9f);
            }
            // облачка у подножия
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * 2f * i / 7f + 0.2f;
                Vector3 p = b + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (rBase + 0.05f) + Vector3.up * 0.1f;
                float r = c.Rf(0.17f, 0.24f);
                s.Puff(p, new Vector3(r, r * 0.8f, r), White, c.Rf(0f, 360f), 7, 4, 0.18f);
            }
            // дверка-арка + сердечко + звезда на макушке
            Vector3 right = Vector3.Cross(Vector3.up, front).normalized;
            Vector3 door = b + front * (rBase - 0.12f) + Vector3.up * 0.34f;
            s.Puff(door, new Vector3(0.24f, 0.34f, 0.04f), Plum, Quaternion.LookRotation(front, Vector3.up), 8, 5, 0.1f);
            s.Heart(b + front * 0.66f + Vector3.up * 0.98f, right, Vector3.up, 0.11f, SoftMesh.C(0xFF7FA8));
            s.Puff(apex + Vector3.up * 0.02f, new Vector3(0.09f, 0.09f, 0.09f), Lav, 0f, 7, 4, 0.2f);
            s.Star(apex + Vector3.up * 0.2f, right, Vector3.up, 0.13f, 0.06f, 5, SoftMesh.C(0xFFD45E));
        }

        // ================================================================ ЭТАП 3: костёр горит
        static int Stage3(Ctx c, Transform parent)
        {
            int tris = 0;

            // ---- пламя: мягкие капли, отдельный не статичный объект (его масштабирует мерцание)
            var flames = NewChild(parent, "Flames");
            flames.localPosition = new Vector3(0f, 0.22f, 0f);
            var f = new SoftMesh();
            FlameMesh(c, f);   // крупное пламя с углями: CandyCampfire.Hearth.cs
            var fgo = Flush(f, flames, "Mesh_Flame", "CandyCamp_S3Flame", c.flame);
            tris += f.Tris;

            // ---- твёрдое: шалашик из поленьев, котелок, зефирки
            var s = new SoftMesh();
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                Vector3 outer = new Vector3(Mathf.Cos(a) * 0.5f, 0.1f, Mathf.Sin(a) * 0.5f);
                Vector3 inner = new Vector3(Mathf.Cos(a) * 0.07f, 0.42f, Mathf.Sin(a) * 0.07f);
                s.Cyl(outer, inner, 0.075f, 0.06f, i % 2 == 0 ? LogMid : LogDark, Char, true, true, 7);
            }
            Pot(s);
            Marshmallows(s, c);
            SignStar(s, c, 2);   // третья звезда на табличке
            Flush(s, parent, "Mesh_Solid", "CandyCamp_S3", c.camp);
            tris += s.Tris;

            // ---- свет (FireSource из него сделает BuildingLightLink в рантайме) и мерцание
            var lg = new GameObject("CampLight");
            lg.transform.SetParent(parent, false);
            lg.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = 4f;
            l.color = new Color(1f, 0.64f, 0.42f);
            l.shadows = LightShadows.None;
            parent.gameObject.AddComponent<CampfireFlicker>().Configure(l, flames);

            // ---- частицы: искры, дым-облачка, сердечки
            if (c.motes != null)
            {
                var sp = MakePS(parent, "Sparks", new Vector3(0f, 0.9f, 0f), c.motes, null);
                var m = sp.main;
                m.loop = true; m.prewarm = true; m.startLifetime = 1.7f; m.startSpeed = 1.0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.45f, 1f), new Color(1f, 0.6f, 0.78f, 1f));
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.05f; m.maxParticles = 48;
                var e = sp.emission; e.rateOverTime = 14f;
                var sh = sp.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.12f;
                FadeOut(sp, 0.1f);
                Shrink(sp, 1f, 0.2f);
                sp.Play();

                var sm = MakePS(parent, "Smoke", new Vector3(0f, 2.7f, 0f), c.motes, null);
                var m2 = sm.main;
                m2.loop = true; m2.prewarm = true; m2.startLifetime = 3.4f; m2.startSpeed = 0.5f;
                m2.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                m2.startColor = new ParticleSystem.MinMaxGradient(new Color(0.96f, 0.93f, 1f, 0.5f), new Color(1f, 0.93f, 0.95f, 0.5f));
                m2.simulationSpace = ParticleSystemSimulationSpace.World; m2.gravityModifier = -0.01f; m2.maxParticles = 16;
                var e2 = sm.emission; e2.rateOverTime = 2.4f;
                var sh2 = sm.shape; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 12f; sh2.radius = 0.1f;
                FadeOut(sm, 0.25f);
                Shrink(sm, 0.5f, 1.7f);
                sm.Play();
            }
            // сердечки: крест из двух плоских сердец, видно со всех сторон
            var hs = new SoftMesh();
            Color hp = SoftMesh.C(0xFF7FA8);
            hs.Heart(Vector3.zero, Vector3.right, Vector3.up, 0.5f, hp);
            hs.Heart(Vector3.zero, Vector3.forward, Vector3.up, 0.5f, hp);
            var hmesh = SaveMesh(hs.ToMesh("CandyCamp_Heart"), "CandyCamp_Heart");
            var hps = MakePS(parent, "Hearts", new Vector3(0f, 1.6f, 0f), c.heart, hmesh);
            {
                var m = hps.main;
                m.loop = true; m.prewarm = true; m.startLifetime = 3.2f; m.startSpeed = 0.55f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
                m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.02f; m.maxParticles = 6;
                var e = hps.emission; e.rateOverTime = 0.5f;
                var sh = hps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 24f; sh.radius = 0.25f;
                Shrink(hps, 1f, 0.0f);
                var psr = hps.GetComponent<ParticleSystemRenderer>();
                psr.enableGPUInstancing = false;
                psr.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
                hps.Play();
            }
            return tris;
        }

        static void Pot(SoftMesh s)
        {
            Color body = SoftMesh.C(0x9ED9C0), lid = Lav, steel = SoftMesh.C(0xB9AEC9);
            float L = PotLift;
            // цепь с кольцами от макушки треноги
            s.Cyl(new Vector3(0f, 2.2f + L, 0f), new Vector3(0f, 1.98f + L, 0f), 0.012f, 0.012f, steel, steel, false, false, 5);
            for (int i = 0; i < 3; i++) s.Puff(new Vector3(0f, 2.12f + L - i * 0.07f, 0f), new Vector3(0.022f, 0.03f, 0.022f), steel, i * 60f, 6, 3, 0.1f);
            // дужка
            Vector3 pPrev = new Vector3(-0.27f, 1.66f + L, 0f);
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f, a = Mathf.PI * (1f - t);
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.27f, 1.66f + L + Mathf.Sin(a) * 0.3f, 0f);
                s.Cyl(pPrev, p, 0.011f, 0.011f, steel, steel, false, false, 4);
                pPrev = p;
            }
            // тело, крышка, кнопка, сердечко на боку
            s.Puff(new Vector3(0f, 1.5f + L, 0f), new Vector3(0.3f, 0.25f, 0.3f), body, 0f, 10, 6, 0.22f);
            s.Puff(new Vector3(0f, 1.71f + L, 0f), new Vector3(0.25f, 0.075f, 0.25f), lid, 0f, 10, 4, 0.2f);
            s.Puff(new Vector3(0f, 1.8f + L, 0f), new Vector3(0.045f, 0.04f, 0.045f), Pink, 0f, 6, 3, 0.1f);
            s.Heart(new Vector3(0f, 1.47f + L, 0.298f), Vector3.right, Vector3.up, 0.09f, SoftMesh.C(0xFF7FA8));
            s.Heart(new Vector3(0f, 1.47f + L, -0.298f), Vector3.right, Vector3.up, 0.09f, SoftMesh.C(0xFF7FA8));
        }

        static void Marshmallows(SoftMesh s, Ctx c)
        {
            float[] deg = { 55f, 175f, 305f };
            for (int i = 0; i < 3; i++)
            {
                float a = FreeAngle(c, 1.7f, deg[i], 1.7f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 b = dir * 1.75f + Vector3.up * (Gy(1.75f) - 0.01f);
                Vector3 tip = dir * 0.68f + Vector3.up * 0.7f;
                s.Cyl(b, tip, 0.011f, 0.008f, WoodL, WoodL, true, true, 4);
                Vector3 mdir = (tip - b).normalized;
                Quaternion q = Quaternion.FromToRotation(Vector3.up, mdir);
                Color mm = i == 1 ? SoftMesh.C(0xFFC4D6) : White;
                s.Puff(tip + mdir * 0.06f, new Vector3(0.05f, 0.07f, 0.05f), mm, q, 8, 5, 0.12f);
                s.Puff(tip + mdir * 0.075f - dir * 0.0f, new Vector3(0.052f, 0.04f, 0.052f), i == 0 ? Toast : Gold, q, 8, 4, 0.05f);   // подрумяненный бочок
            }
        }

        // ================================================================ частицы
        static ParticleSystem MakePS(Transform parent, string name, Vector3 pos, Material mat, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // эмиссия вдоль +Z -> вверх
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (mesh != null) { psr.renderMode = ParticleSystemRenderMode.Mesh; psr.mesh = mesh; }
            else psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            return ps;
        }

        static void FadeOut(ParticleSystem ps, float fadeIn)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(fadeIn, 0.02f, 0.9f)), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Shrink(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from), new Keyframe(0.4f, Mathf.Max(from, to) * 0.9f + 0.1f), new Keyframe(1f, to)));
        }

        // ================================================================ зона сдачи и стройка (как в старом CampfireBuilder)
        static void BuildZone(Transform rootT, HutStages stages)
        {
            var zg = new GameObject("BuildZone");
            zg.transform.SetParent(rootT, false);
            float side = R * 2f + 6f;
            var col = zg.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(side, 3f, side);
            col.center = new Vector3(0f, 1.5f, 0f);

            var bc = zg.AddComponent<BuildingController>();
            bc.stages = stages;
            bc.startStage = 1;
            bc.stageCosts = new[] { 4, 4 };
            bc.woodRequired = bc.stageCosts[0];
            bc.useRing = true;
            bc.ringOffset = new Vector3(0f, 3f, 0f);
            bc.rewardXp = 10;
        }

        static void ApplyStatic(Transform t)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (var tr in t.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                if (n == "Smoke" || n == "Sparks" || n == "Hearts" || n == "CampLight" || n == "Flames" || n == "BuildZone" || n == "CostTag") continue;
                if (tr.parent != null && tr.parent.name == "Flames") continue;
                if (tr.GetComponentInParent<StageVisibility>(true) != null) continue;   // уходящие детали должны двигаться: не статичные
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, flags);
            }
        }
    }
}
#endif

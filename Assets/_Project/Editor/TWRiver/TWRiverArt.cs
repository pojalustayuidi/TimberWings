// Assets/_Project/Editor/TWRiver/TWRiverArt.cs
// Timber Wings / ISL-004: ЕДИНЫЙ запуск новой системы «исток - русло - водопад».
// Собирает Source + Banks + Rapids + FallFrame + Flora + Foam в один корень IslandV2/_Manual/TW_RIVER_ART (2 меша, 2 материала, 0 коллайдеров).
// Водопад: авторитетная геометрия остаётся River_Fall (одно полотно, IslandRiverWater.AddFall). Здесь второго полотна НЕТ,
// только скальная рамка по бокам порога, камни-разделители струй и пена. Стык River_Water / River_Fall проверяет пункт QA.
// Безопасность: НЕ вызывает IslandBlockout.Build / IslandAll, не трогает IslandLayout, Terrain, коллайдеры, мост, WaterVolume, Nav, сейвы.
// Перед Apply делается копия сцены и IslandLayout в Library/TW_RiverArt/Backups. Откат: пункт 3 (удаляет только этот арт и его ассеты).
// Старые TW_RIVER_OVERHAUL_V1..V3 в сцене удаляются в том же Apply, но только ПОСЛЕ успешной сборки нового арта.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace IslandV2
{
    public static class TWRiverArt
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/RIVER ART/";
        const string RocksAsset = TWRiverSpec.AssetFolder + "/TW_RiverRocks.asset";
        const string FoamAsset = TWRiverSpec.AssetFolder + "/TW_RiverFoamMesh.asset";
        const string FoamMatAsset = TWRiverSpec.AssetFolder + "/TW_RiverFoam.mat";
        const string FoamShader = "Project/IslandV2/TW_RiverFoam";
        const int Seed = 4004;

        sealed class Ctx
        {
            public Scene scene;
            public GameObject root;
            public Transform gen, man;
            public IslandLayout layout;
            public IslandField field;
            public List<Vector2> piece;
            public IslandField.RiverEdge outlet;
            public bool hasInlet;
            public IslandField.RiverEdge inlet;
        }

        static string[] AssetPaths() { return new[] { RocksAsset, FoamAsset, FoamMatAsset }; }
        static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }

        // ================================================================ 1. аудит
        [MenuItem(Menu + "1. Audit (без изменений)", false, 181)]
        public static void Audit()
        {
            Ctx c; string report;
            bool ok = Inspect(out c, out report);
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Library/TW_RiverArt"));
            File.WriteAllText(Path.Combine(ProjectRoot, "Library/TW_RiverArt/Audit.txt"), report, Encoding.UTF8);
            if (ok) Debug.Log("TW RIVER ART AUDIT: ГОТОВО к Apply.\n" + report);
            else Debug.LogError("TW RIVER ART AUDIT: Apply запрещён.\n" + report);
        }

        // ================================================================ 2. Apply
        [MenuItem(Menu + "2. Apply (исток + русло + рамка водопада, убрать старые V1-V3)", false, 182)]
        public static void Apply()
        {
            Ctx c; string report;
            if (!Inspect(out c, out report)) { Debug.LogError("TW RIVER ART: Apply остановлен.\n" + report); return; }
            if (c.man.Find(TWRiverSpec.RootName) != null) { Debug.LogError("TW RIVER ART: арт уже установлен. Сначала пункт 3 (Rollback)."); return; }
            foreach (var p in AssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(p) != null) { Debug.LogError("TW RIVER ART: конфликт ассета " + p + ". Ничего не заменено."); return; }

            var legacy = TWRiverSpec.LegacyRoots.Select(n => c.man.Find(n)).Where(t => t != null).ToList();
            string legacyText = legacy.Count == 0 ? "старых слоёв V1-V3 в сцене нет" : "будут удалены старые слои: " + string.Join(", ", legacy.Select(t => t.name).ToArray());
            if (!EditorUtility.DisplayDialog("TW RIVER ART — Apply",
                "Сцена: " + c.scene.name + "\nИсток: " + (c.hasInlet ? "скальный массив с гротом у края острова" : "не нужен (река начинается внутри острова)") + "\n" +
                "Создаётся только IslandV2/_Manual/" + TWRiverSpec.RootName + " (2 меша, 2 материала, без коллайдеров).\n" +
                "River_Water / River_Fall, Terrain, мост, WaterVolume, IslandLayout не меняются.\n" + legacyText + " (после успешной сборки).\n\n" +
                "Сначала будет сделана копия сцены и IslandLayout в Library/TW_RiverArt/Backups.", "Backup + Apply", "Отмена")) return;

            string backup;
            try { backup = Backup(c); }
            catch (Exception ex) { Debug.LogError("TW RIVER ART: бэкап не создан, Apply отменён. " + ex); return; }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("TW River Art: Apply");
            int group = Undo.GetCurrentGroup();
            GameObject made = null;
            try
            {
                var rockMat = AssetDatabase.LoadAssetAtPath<Material>(IslandCliff.MaterialPath);
                var foamSh = Shader.Find(FoamShader);
                IslandLayout.EnsureFolder(TWRiverSpec.AssetFolder);

                var x = MakeCtx(c);
                if (x.hasSource) TWRiverSource.Build(x);
                TWRiverBanks.Build(x);
                TWRiverBanks.BuildRapids(x);
                TWRiverBanks.BuildFallFrame(x);
                TWRiverFlora.Build(x);     // после камней: мох ставится на уже известные точки
                TWRiverFoam.Build(x);      // после камней: следы за каждым камнем в воде

                // заземление: якоря обязаны лежать в земле, воде или обрыве
                int hanging = 0; var bad = new List<Vector3>();
                foreach (var a in x.rocks.anchors) if (!x.Grounded(a)) { hanging++; if (bad.Count < 6) bad.Add(a); }
                int total = Mathf.Max(1, x.rocks.anchors.Count);
                if (hanging * 4 > total)
                    throw new InvalidOperationException("слишком много висящих точек (" + hanging + " из " + total + "): ось реки или сторона берега определены неверно");
                if (hanging > 0)
                    Debug.LogWarning("TW RIVER ART: висящих опорных точек " + hanging + " из " + total + ". Первые: " + string.Join("; ", bad.Select(v => v.ToString("F1")).ToArray()));

                made = new GameObject(TWRiverSpec.RootName);
                Undo.RegisterCreatedObjectUndo(made, "TW River Art root");
                made.transform.SetParent(c.man, false);

                var rocksMesh = x.rocks.ToMesh("TW_RiverRocks");
                AssetDatabase.CreateAsset(rocksMesh, RocksAsset);
                var rgo = new GameObject("River_Rocks_Source_Banks_FallFrame");
                rgo.transform.SetParent(made.transform, false);
                rgo.isStatic = true;
                rgo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(RocksAsset);
                var rmr = rgo.AddComponent<MeshRenderer>();
                rmr.sharedMaterial = rockMat;
                rmr.shadowCastingMode = ShadowCastingMode.On;
                rmr.receiveShadows = true;

                int foamTris = x.foam.Tris;
                if (foamTris > 0)
                {
                    var foamMat = new Material(foamSh) { name = "TW_RiverFoam" };
                    foamMat.SetColor("_Tint", c.layout.riverFoam);
                    AssetDatabase.CreateAsset(foamMat, FoamMatAsset);
                    var foamMesh = x.foam.ToMesh("TW_RiverFoamMesh");
                    AssetDatabase.CreateAsset(foamMesh, FoamAsset);
                    var fgo = new GameObject("River_Foam_Flow");
                    fgo.transform.SetParent(made.transform, false);
                    fgo.isStatic = true;
                    fgo.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FoamAsset);
                    var fmr = fgo.AddComponent<MeshRenderer>();
                    fmr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FoamMatAsset);
                    fmr.shadowCastingMode = ShadowCastingMode.Off;
                    fmr.receiveShadows = false;
                    fmr.lightProbeUsage = LightProbeUsage.Off;
                    fmr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }

                // старые слои убираем только теперь, когда новый арт собран
                foreach (var t in legacy) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(c.scene);
                Selection.activeGameObject = made;
                Undo.CollapseUndoOperations(group);
                Debug.Log("TW RIVER ART APPLY: скал " + x.rocks.Tris + " tris, пены " + foamTris + " tris, камней в воде " + x.stones.Count +
                          ", источник " + (x.hasSource ? "да" : "нет") + ", скалистый берег " + (x.rockSide > 0 ? "левый" : "правый") + " по течению, " +
                          "убрано старых слоёв " + legacy.Count + ". Бэкап: " + backup + ". Дальше: Ctrl+S, пункт 4 (QA), снимки Game View.");
            }
            catch (Exception ex)
            {
                if (made != null) Undo.DestroyObjectImmediate(made);
                foreach (var p in AssetPaths()) if (AssetDatabase.LoadMainAssetAtPath(p) != null) AssetDatabase.DeleteAsset(p);
                Debug.LogError("TW RIVER ART: Apply прерван, созданное очищено, старые слои НЕ тронуты. Бэкап: " + backup + ". " + ex);
            }
        }

        // ================================================================ 3. Rollback
        [MenuItem(Menu + "3. Rollback (удалить только этот арт)", false, 183)]
        public static void Rollback()
        {
            if (Application.isPlaying) { Debug.LogError("TW RIVER ART: выйди из Play Mode."); return; }
            var root = IslandBlockout.FindRoot();
            var art = root != null ? root.transform.Find(IslandBlockout.ManualName + "/" + TWRiverSpec.RootName) : null;
            if (art == null) { Debug.LogWarning("TW RIVER ART: арт в сцене не найден, ничего не изменено."); return; }
            if (!EditorUtility.DisplayDialog("TW RIVER ART — Rollback",
                "Удалить ТОЛЬКО " + TWRiverSpec.RootName + " и его 3 ассета?\nСтарые слои V1-V3 это не вернёт: для них есть копия сцены в Library/TW_RiverArt/Backups.", "Rollback", "Отмена")) return;
            Undo.DestroyObjectImmediate(art.gameObject);
            foreach (var p in AssetPaths()) if (AssetDatabase.LoadMainAssetAtPath(p) != null) AssetDatabase.DeleteAsset(p);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("TW RIVER ART ROLLBACK: арт и ассеты удалены. Сохрани сцену (Ctrl+S).");
        }

        // ================================================================ 4. QA
        [MenuItem(Menu + "4. QA (дубли, шов вода-водопад, коллайдеры)", false, 184)]
        public static void QA()
        {
            var root = IslandBlockout.FindRoot();
            if (root == null) { Debug.LogError("TW RIVER ART QA: нет IslandV2 в сцене."); return; }
            var sb = new StringBuilder();
            int problems = 0;
            Action<string> bad = s => { problems++; sb.AppendLine("ПРОБЛЕМА: " + s); };
            Action<string> ok = s => sb.AppendLine("ок: " + s);

            var man = root.transform.Find(IslandBlockout.ManualName);
            var gen = root.transform.Find(IslandBlockout.GeneratedName);
            var art = man != null ? man.Find(TWRiverSpec.RootName) : null;
            if (art == null) bad("TW_RIVER_ART не установлен (пункт 2)");
            else
            {
                var mrs = art.GetComponentsInChildren<MeshRenderer>(true);
                int tris = 0;
                foreach (var m in mrs) { var mf = m.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3); }
                ok("арт: рендереров " + mrs.Length + ", треугольников " + tris + ", коллайдеров " + art.GetComponentsInChildren<Collider>(true).Length + ", Light " + art.GetComponentsInChildren<Light>(true).Length);
                if (art.GetComponentsInChildren<Collider>(true).Length > 0) bad("в арте появились коллайдеры");
            }

            foreach (var n in TWRiverSpec.LegacyRoots)
            {
                var all = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == n).ToList();
                if (all.Count > 0) bad("остался старый слой " + n + " (Apply уберёт, либо FINAL CLEANUP пункт 2)");
            }
            int veils = root.GetComponentsInChildren<Transform>(true).Count(t => t.name.IndexOf("Veil", StringComparison.OrdinalIgnoreCase) >= 0);
            if (veils > 0) bad("найдено объектов с «Veil» в имени: " + veils + " (второе полотно водопада)"); else ok("лишних полотен водопада (Veil) нет");

            Transform water = gen != null ? gen.Find(IslandRiverWater.ObjectName) : null;
            Transform fall = gen != null ? gen.Find(IslandRiverWater.FallObjectName) : null;
            if (water == null || fall == null) bad("нет River_Water или River_Fall");
            else
            {
                var wm = water.GetComponent<MeshFilter>().sharedMesh;
                var fm = fall.GetComponent<MeshFilter>().sharedMesh;
                int per = IslandRiverWater.FallRows * IslandRiverWater.FallCols;
                if (fm == null || wm == null) bad("у River_Water или River_Fall нет меша");
                else if (fm.vertexCount % per != 0) bad("число вершин River_Fall (" + fm.vertexCount + ") не кратно одному полотну (" + per + ")");
                else
                {
                    int falls = fm.vertexCount / per;
                    ok("River_Fall: полотен " + falls + " (одно на выход реки)");
                    var wv = wm.vertices; var fv = fm.vertices;
                    float worst = 0f;
                    for (int k = 0; k < falls; k++)
                    {
                        int a = k * per, b = k * per + IslandRiverWater.FallCols - 1;   // два крайних угла верхнего ряда полотна
                        foreach (int vi in new[] { a, b })
                        {
                            Vector3 p = fall.TransformPoint(fv[vi]);
                            float best = float.MaxValue;
                            for (int i = 0; i < wv.Length; i++) { float d = (water.TransformPoint(wv[i]) - p).sqrMagnitude; if (d < best) best = d; }
                            worst = Mathf.Max(worst, Mathf.Sqrt(best));
                        }
                    }
                    if (worst > 0.05f) bad("шов River_Water / River_Fall: зазор до " + worst.ToString("F3") + " м (допуск 0.05)");
                    else ok("шов River_Water / River_Fall: зазор " + worst.ToString("F3") + " м");
                }
            }
            var tcs = root.GetComponentsInChildren<TerrainCollider>(true).Length;
            if (tcs == 0) bad("нет TerrainCollider"); else ok("TerrainCollider " + tcs + ", Collider на острове " + root.GetComponentsInChildren<Collider>(true).Length);
            var vols = root.GetComponentsInChildren<WaterVolume>(true).Length;
            if (vols == 0) bad("нет WaterVolume (плавание)"); else ok("WaterVolume " + vols);

            string text = "TW RIVER ART QA: проблем " + problems + "\n" + sb;
            if (problems == 0) Debug.Log(text + "\nДальше: запусти «Река: проверить стык воды и водопада» и сними Game View (день/ночь).");
            else Debug.LogWarning(text);
        }

        // ================================================================ 5-8. ракурсы Scene View
        [MenuItem(Menu + "5. Frame: вся река", false, 185)]
        static void FrameOverview()
        {
            Ctx c; string r; Inspect(out c, out r);
            if (c == null || c.piece == null) { Debug.LogError(r); return; }
            Vector2 a = c.piece[0], b = c.piece[c.piece.Count - 1], m = (a + b) * 0.5f;
            Look(new Vector3(m.x, 0f, m.y), new Vector3(0.35f, -0.82f, 0.45f), Mathf.Max(70f, Vector2.Distance(a, b) * 0.95f));
        }

        [MenuItem(Menu + "6. Frame: исток спереди", false, 186)]
        static void FrameSource()
        {
            Ctx c; string r; Inspect(out c, out r);
            if (c == null || c.piece == null) { Debug.LogError(r); return; }
            var ax = new TWRiverAxis(c.piece, 0.8f);
            Vector2 p = ax.At(TWRiverSpec.MouthDist), t = ax.Tangent(TWRiverSpec.MouthDist);
            float y = c.field.RiverSurfaceAt(p.x, p.y) + 1.6f;
            Look(new Vector3(p.x, y, p.y), new Vector3(-t.x, -0.30f, -t.y), 13f);   // камера ниже по течению, смотрит против течения в проём
        }

        [MenuItem(Menu + "7. Frame: порог водопада сверху", false, 187)]
        static void FrameFallTop() { FrameFall(-0.62f, 1f, 14f); }

        [MenuItem(Menu + "8. Frame: водопад сбоку", false, 188)]
        static void FrameFallSide() { FrameFall(-0.10f, 0f, 22f); }

        static void FrameFall(float pitch, float alongWeight, float size)
        {
            Ctx c; string r; Inspect(out c, out r);
            if (c == null || c.piece == null) { Debug.LogError(r); return; }
            Vector2 e = c.outlet.pos, o = c.outlet.outward.normalized, side = new Vector2(-o.y, o.x);
            Vector2 dir = Vector2.Lerp(side, o, alongWeight).normalized;
            float y = c.field.RiverSurfaceAt(e.x, e.y) - 2.2f;
            Look(new Vector3(e.x + o.x * 1.0f, y, e.y + o.y * 1.0f), new Vector3(dir.x, pitch, dir.y), size);
        }

        static void Look(Vector3 pivot, Vector3 forward, float size)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) { Debug.LogWarning("TW RIVER ART: нет открытого окна Scene."); return; }
            sv.LookAt(pivot, Quaternion.LookRotation(forward.normalized, Vector3.up), size);
            sv.Repaint();
        }

        // ================================================================ контекст сборки
        static TWRiverCtx MakeCtx(Ctx c)
        {
            var L = c.layout; var f = c.field;
            var x = new TWRiverCtx();
            x.f = f; x.L = L; x.seed = Seed;
            x.half = L.riverWidth * 0.5f;
            x.ax = new TWRiverAxis(c.piece, 0.8f);
            x.pal = new TWRiverPalette(L);
            x.cliff = TWCliffProbe.Find(c.gen);
            x.hasSource = TWRiverSpec.HasSource(f);
            x.outletEdgeS = x.ax.Length;
            x.outletTail = c.outlet.outward;
            x.rockSide = PickRockSide(x);
            if (L.crossings != null)
                foreach (var cr in L.crossings)
                    x.exclusions.Add(new Vector3(cr.pos.x, cr.pos.y, cr.halfLength + x.half + 3f));   // брод и мост: ничего не ставим
            return x;
        }

        // скалистым делаем более высокий берег: скала продолжает естественный рельеф, мягкий берег остаётся травяным
        static int PickRockSide(TWRiverCtx x)
        {
            float sum = 0f;
            float off = x.half + 5f;
            for (float s = 4f; s < x.ax.Length - 4f; s += 3f)
            {
                Vector2 a = x.ax.At(s), cs = x.ax.Side(s);
                sum += x.f.Height(a.x + cs.x * off, a.y + cs.y * off) - x.f.Height(a.x - cs.x * off, a.y - cs.y * off);
            }
            return sum >= 0f ? 1 : -1;
        }

        // ================================================================ проверка сцены
        static bool Inspect(out Ctx c, out string report)
        {
            c = new Ctx();
            var lines = new List<string>();
            bool blocked = false;
            Action<string> Fail = s => { lines.Add("BLOCK: " + s); blocked = true; };
            Action<string> Note = s => lines.Add("INFO: " + s);

            c.scene = SceneManager.GetActiveScene();
            if (Application.isPlaying) Fail("Play Mode");
            if (!c.scene.IsValid() || string.IsNullOrEmpty(c.scene.path)) Fail("сцена не сохранена в Assets");
            if (c.scene.isDirty) Fail("в сцене несохранённые изменения: сначала Ctrl+S");

            c.root = IslandBlockout.FindRoot();
            if (c.root == null || c.root.scene != c.scene) Fail("в активной сцене нет IslandV2");
            else
            {
                if (c.root.transform.position.sqrMagnitude > 1e-5f || Quaternion.Angle(c.root.transform.rotation, Quaternion.identity) > 0.01f ||
                    Vector3.Distance(c.root.transform.lossyScale, Vector3.one) > 0.001f)
                    Fail("IslandV2 не в нуле / не в единичном масштабе: меши считаются в координатах IslandField");
                c.gen = c.root.transform.Find(IslandBlockout.GeneratedName);
                c.man = c.root.transform.Find(IslandBlockout.ManualName);
                if (c.gen == null || c.man == null) Fail("нет _Generated или _Manual");
                else
                {
                    if (c.man.localPosition.sqrMagnitude > 1e-5f || c.man.localRotation != Quaternion.identity || c.man.localScale != Vector3.one)
                        Fail("_Manual смещён: арт ляжет не туда");
                    foreach (string n in new[] { IslandCliff.ObjectName, IslandRiverWater.ObjectName, IslandRiverWater.FallObjectName, IslandBlockout.BridgeName })
                        if (c.gen.Find(n) == null) Fail("нет нативного узла _Generated/" + n + " (полную регенерацию НЕ запускаем)");
                    foreach (var n in TWRiverSpec.LegacyRoots)
                        if (c.man.Find(n) != null) Note("найден старый слой " + n + ": Apply удалит его после успешной сборки");
                    if (c.man.Find(TWRiverSpec.RootName) != null) Note("арт уже установлен: Apply запрещён, доступен Rollback");
                }
                if (c.root.GetComponentsInChildren<TerrainCollider>(true).Length == 0) Fail("нет TerrainCollider: география не подтверждена");
            }

            c.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (c.layout == null) Fail("нет " + IslandLayout.AssetPath);
            else
            {
                if (!c.layout.buildRiverWater || !c.layout.buildCliff) Fail("выключены buildRiverWater или buildCliff");
                if (c.layout.riverPoints == null || c.layout.riverPoints.Count < 2) Fail("нет riverPoints");
                else
                {
                    c.field = new IslandField(c.layout);
                    var pieces = c.field.RiverInsidePieces();
                    var edges = c.field.RiverEdges;
                    var outs = edges.Where(e => e.isOutlet).ToList();
                    var ins = edges.Where(e => !e.isOutlet).ToList();
                    Note("кусков реки " + pieces.Count + ", входов " + ins.Count + ", выходов " + outs.Count);
                    if (pieces.Count != 1 || outs.Count != 1 || ins.Count > 1) Fail("нужна одна непрерывная река: один кусок, один выход, не более одного входа");
                    else
                    {
                        c.piece = pieces[0]; c.outlet = outs[0];
                        c.hasInlet = ins.Count == 1; if (c.hasInlet) c.inlet = ins[0];
                        float len = 0f; for (int i = 1; i < c.piece.Count; i++) len += Vector2.Distance(c.piece[i - 1], c.piece[i]);
                        Note("длина реки внутри острова " + len.ToString("F1") + " м, ширина " + c.layout.riverWidth.ToString("F1"));
                        if (len < 25f) Fail("река слишком короткая");
                        if (c.hasInlet && len < TWRiverSpec.MouthDist + 20f) Fail("слишком мало места между истоком и водопадом");
                    }
                }
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(IslandCliff.MaterialPath) == null) Fail("нет материала обрыва " + IslandCliff.MaterialPath);
            if (Shader.Find(FoamShader) == null) Fail("нет шейдера " + FoamShader);

            if (c.gen != null)
            {
                var fall = c.gen.Find(IslandRiverWater.FallObjectName);
                var mf = fall != null ? fall.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null && c.field != null)
                {
                    int expect = c.field.RiverEdges.Count(e => e.isOutlet) * IslandRiverWater.FallRows * IslandRiverWater.FallCols;
                    if (mf.sharedMesh.vertexCount != expect) Fail("River_Fall имеет " + mf.sharedMesh.vertexCount + " вершин, ожидалось " + expect + " (лишний слой водопада?). Пересобери воду.");
                    else Note("River_Fall: одно полотно, " + expect + " вершин");
                }
            }
            report = "TW RIVER ART / Unity " + Application.unityVersion + " / " + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", lines.ToArray()) + "\n";
            return !blocked;
        }

        // ================================================================ бэкап
        static string Backup(Ctx c)
        {
            string dir = Path.Combine(ProjectRoot, "Library/TW_RiverArt/Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dir);
            Copy(c.scene.path, dir, true); Copy(c.scene.path + ".meta", dir, false);
            Copy(IslandLayout.AssetPath, dir, true); Copy(IslandLayout.AssetPath + ".meta", dir, false);
            return dir;
        }

        static void Copy(string assetPath, string dir, bool required)
        {
            string src = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src)) { if (required) throw new FileNotFoundException("для бэкапа нет " + src); return; }
            File.Copy(src, Path.Combine(dir, Path.GetFileName(src)), false);
        }
    }
}

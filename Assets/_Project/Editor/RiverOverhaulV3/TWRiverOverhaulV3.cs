// Timber Wings — ISL-004 River Overhaul v0.3, safe scene-only pass for Unity 6 / URP.
// Uses existing IslandLayout -> IslandField -> IslandBlockout / IslandRiverWater / IslandCliff.
// NEVER invokes IslandBlockout.Build, IslandAll, changes riverPoints, or modifies colliders.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace IslandV2
{
    public static class TWRiverOverhaulV3
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/RIVER OVERHAUL v0.3/";
        internal const string RootName = "TW_RIVER_OVERHAUL_V3";
        internal const string LegacyRootName = "TW_RIVER_OVERHAUL_V1";
        internal const string LegacyRootName2 = "TW_RIVER_OVERHAUL_V2";
        internal const string AssetFolder = "Assets/_Project/IslandV2/RiverOverhaul_v0_3";
        internal const string MeshFolder = AssetFolder + "/Generated";
        internal const string RocksAsset = MeshFolder + "/TW_RiverRockFaces.asset";
        internal const string PlantsAsset = MeshFolder + "/TW_RiverBankPlants.asset";
        internal const string FoamAsset = MeshFolder + "/TW_RiverCurrentHighlights.asset";
        internal const string FoamMaterialAsset = MeshFolder + "/TW_RiverFoam.mat";
        const string FoamShader = "Project/IslandV2/TW_RiverFoam";
        
        sealed class Inspection
        {
            public Scene scene;
            public GameObject root;
            public Transform generated, manual;
            public IslandLayout layout;
            public IslandField field;
            public List<List<Vector2>> pieces;
            public IslandField.RiverEdge inlet, outlet;
            public string scenePath, sceneHash, layoutHash;
        }

        [MenuItem(Menu + "1. Audit (без изменений)", false, 170)]
        public static void Audit()
        {
            Inspection i;
            string reason, report;
            bool valid = Inspect(out i, out reason, out report);
            string file = AuditPath();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, report, Encoding.UTF8);
            if (valid)
                Debug.Log("TW RIVER AUDIT v0.3: ГОТОВО для локального Apply. Отчёт: " + file + "\n" + report);
            else
                Debug.LogError("TW RIVER AUDIT v0.3: Apply ЗАПРЕЩЁН. " + reason + "\nОтчёт: " + file + "\n" + report);
        }

        [MenuItem(Menu + "2. Apply (единый водопад + исправленный грот)", false, 171)]
        public static void Apply()
        {
            Inspection i;
            string reason, report;
            if (!Inspect(out i, out reason, out report))
            {
                Debug.LogError("TW RIVER v0.3: Apply остановлен: " + reason + ". Сначала Audit.");
                return;
            }
            string auditFile = AuditPath();
            if (!File.Exists(auditFile) || !File.ReadAllText(auditFile).Contains(AuditToken(i)))
            {
                Debug.LogError("TW RIVER v0.3: сначала запусти Audit для этой сохранённой сцены и текущего IslandLayout.asset.");
                return;
            }
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("TW RIVER v0.3: сначала сохрани сцену вручную (Ctrl+S), затем повтори Audit и Apply.");
                return;
            }
            if (i.manual.Find(LegacyRootName2) != null)
            {
                Debug.LogError("TW RIVER v0.3: сначала запусти Rollback в меню v0.2, чтобы убрать двойной водопад. Старый " + LegacyRootName2 + " ещё установлен.");
                return;
            }
            if (i.manual.Find(LegacyRootName) != null)
            {
                Debug.LogError("TW RIVER v0.3: обнаружен установленный пакет " + LegacyRootName + ". Сначала удали/откати версию v0.1, чтобы не дублировать арт.");
                return;
            }
            if (i.manual.Find(RootName) != null)
            {
                Debug.LogError("TW RIVER v0.3: пакет уже установлен. Сначала Rollback — повторный Apply не создаёт дубли.");
                return;
            }
            foreach (var p in AssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(p) != null)
                {
                    Debug.LogError("TW RIVER v0.3: конфликт существующего ассета " + p + "; ничего не заменено.");
                    return;
                }
            if (!EditorUtility.DisplayDialog("TW River Overhaul v0.3 — Apply",
                "Сцена: " + i.scene.name + "\nРека: " + i.pieces.Count + " связанный участок, 1 вход и 1 выход.\n" +
                "Будет создан только арт под IslandV2/_Manual/" + RootName + ".\n" +
                "Восстанавливается единое нативное полотно River_Fall (без дубля v0.2). Добавляются проходимый скальный исток, связные берега и устье без висящих колонн.\n" +
                "IslandLayout, _Generated, TerrainCollider, мост, игровые объекты, вода и Nav не меняются.\n\n" +
                "Перед изменением будет сделана файловая копия сцены и IslandLayout в Library/. Продолжить?", "Backup + Apply", "Отмена")) return;

            string backup = "";
            try { backup = Backup(i); }
            catch (Exception ex) { Debug.LogError("TW RIVER v0.3: резервная копия не создана; Apply отменён. " + ex); return; }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("TW River Overhaul v0.3: Apply scene art");
            int group = Undo.GetCurrentGroup();
            Transform made = null;
            try
            {
                EnsureFolder(AssetFolder);
                EnsureFolder(MeshFolder);
                Material rockMat = AssetDatabase.LoadAssetAtPath<Material>(IslandCliff.MaterialPath);
                if (rockMat == null) throw new InvalidOperationException("Отсутствует Mat_IslandRock. Сначала восстанови существующий обрыв; автоматическая сборка запрещена.");
                Shader foamShader = Shader.Find(FoamShader);
                if (foamShader == null || !foamShader.isSupported) throw new InvalidOperationException("Не найден совместимый шейдер " + FoamShader);

                var foamMat = new Material(foamShader) { name = "TW_RiverFoam" };
                foamMat.SetColor("_Tint", i.layout.riverFoam);
                AssetDatabase.CreateAsset(foamMat, FoamMaterialAsset);


                var parent = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(parent, "TW River Overhaul v0.3 root");
                parent.transform.SetParent(i.manual, false);
                made = parent.transform;

                var result = TWRiverOverhaulGeometryV3.Make(i.field, i.inlet, i.outlet);
                int renderers = 0;
                if (SaveMesh(result.rocks, RocksAsset, made, "Grotto_Channel_Fall_Rocks", rockMat)) renderers++;
                if (SaveMesh(result.plants, PlantsAsset, made, "Riverbank_Soft_Growth", rockMat)) renderers++;
                if (SaveMesh(result.foam, FoamAsset, made, "Spring_Rapids_Fall_Crests", foamMat)) renderers++;
                EditorSceneManager.MarkSceneDirty(i.scene);
                Selection.activeGameObject = parent;
                Undo.CollapseUndoOperations(group);
                Debug.Log("TW RIVER APPLY v0.3: создано " + renderers + " MeshRenderer, " + result.triangles + " треугольников, 0 Collider, 0 Light, 0 ParticleSystem; River_Fall без изменений. " +
                          "Источник " + i.inlet.pos.ToString("F1") + ", устье " + i.outlet.pos.ToString("F1") + ". " +
                          "Снимок до Apply: " + backup + ". Сохрани сцену вручную и проверь Game/Scene (день/ночь, Frame Debugger, FPS). Не выполняй IslandAll.Build.");
            }
            catch (Exception ex)
            {
                if (made != null) Undo.DestroyObjectImmediate(made.gameObject);
                foreach (var p in AssetPaths()) if (AssetDatabase.LoadMainAssetAtPath(p) != null) AssetDatabase.DeleteAsset(p);
                Debug.LogError("TW RIVER v0.3: Apply прерван, созданные объекты и ассеты очищены. Исходная сцена сохранена в " + backup + ". " + ex);
            }
        }

        [MenuItem(Menu + "3. Rollback (удалить только этот арт)", false, 172)]
        public static void Rollback()
        {
            if (Application.isPlaying) { Debug.LogError("TW RIVER v0.3: выйди из Play Mode."); return; }
            var root = IslandBlockout.FindRoot();
            var art = root != null ? root.transform.Find(IslandBlockout.ManualName + "/" + RootName) : null;
            if (art == null) { Debug.LogWarning("TW RIVER v0.3: пакет не найден в открытой сцене; ничего не изменено."); return; }
            if (!EditorUtility.DisplayDialog("TW River Overhaul v0.3 — Rollback",
                "Удалить ТОЛЬКО " + RootName + " и известные сгенерированные ассеты?\nИсходные _Generated, IslandLayout и мост останутся нетронутыми.\n" +
                "Полная файловая копия сцены до Apply хранится в Library/TW_RiverOverhaul/Backups.", "Rollback", "Отмена")) return;
            Undo.DestroyObjectImmediate(art.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            foreach (var p in AssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(p) != null) AssetDatabase.DeleteAsset(p);
            AssetDatabase.SaveAssets();
            Debug.Log("TW RIVER ROLLBACK v0.3: арт и четыре созданных ассета удалены. Сцена требует сохранения. Нативные генераторы и коллайдеры не менялись.");
        }

        [MenuItem(Menu + "4. Frame: вся река с обзорного ракурса", false, 173)]
        static void FrameOverview()
        {
            Inspection i; string reason, report;
            if (!Inspect(out i, out reason, out report)) { Debug.LogError(reason); return; }
            Vector2 middle = (i.inlet.pos + i.outlet.pos) * 0.5f;
            float size = Mathf.Max(68f, Vector2.Distance(i.inlet.pos, i.outlet.pos) * 0.95f);
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) { sv.LookAt(new Vector3(middle.x, 0f, middle.y), Quaternion.Euler(57f, 20f, 0f), size); sv.Repaint(); }
        }
        [MenuItem(Menu + "5. Frame: исток в Scene View", false, 174)]
        static void FrameSource() { Frame(true); }
        [MenuItem(Menu + "6. Frame: водопад в Scene View", false, 175)]
        static void FrameFall() { Frame(false); }
        static void Frame(bool source)
        {
            Inspection i; string reason, report;
            if (!Inspect(out i, out reason, out report)) { Debug.LogError(reason); return; }
            var target = source ? i.inlet.pos : i.outlet.pos;
            var outward = (source ? i.inlet.outward : i.outlet.outward).normalized;
            float back = source ? 11f : 15f;
            Vector3 pivot = new Vector3(target.x, source ? i.field.RiverSurfaceAt(target.x, target.y) + 2.4f : i.field.RiverSurfaceAt(target.x, target.y) - 2.3f, target.y);
            Quaternion rot = source ? Quaternion.LookRotation(new Vector3(-outward.x, -0.55f, -outward.y).normalized, Vector3.up)
                                    : Quaternion.LookRotation(new Vector3(outward.x, -0.72f, outward.y).normalized, Vector3.up);
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) { sv.LookAt(pivot, rot, back); sv.Repaint(); }
        }

        static string[] AssetPaths() { return new[] { RocksAsset, PlantsAsset, FoamAsset, FoamMaterialAsset }; }
        static string AuditPath() { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/TW_RiverOverhaul/Audit.txt"); }
        static string AuditToken(Inspection i)
        {
            return "AUDIT_TOKEN: scene=" + i.sceneHash + ";layout=" + i.layoutHash + ";path=" + i.scene.path;
        }
        static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            string current = parts[0];
            for (int x = 1; x < parts.Length; x++)
            {
                string next = current + "/" + parts[x];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[x]);
                current = next;
            }
        }
        static bool SaveMesh(Mesh mesh, string path, Transform parent, string name, Material mat)
        {
            if (mesh == null || mesh.vertexCount == 0) return false;
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return true;
        }
        static string FileHash(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }
        static string Backup(Inspection i)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string dir = Path.Combine(project, "Library/TW_RiverOverhaul/Backups", stamp);
            Directory.CreateDirectory(dir);
            CopyForBackup(project, i.scene.path, dir, Path.GetFileName(i.scene.path), true);
            CopyForBackup(project, i.scene.path + ".meta", dir, Path.GetFileName(i.scene.path) + ".meta", false);
            CopyForBackup(project, IslandLayout.AssetPath, dir, "IslandLayout.asset", true);
            CopyForBackup(project, IslandLayout.AssetPath + ".meta", dir, "IslandLayout.asset.meta", false);
            return dir;
        }
        static void CopyForBackup(string root, string path, string targetDir, string newName, bool required)
        {
            string src = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src)) { if (required) throw new FileNotFoundException("Для бэкапа отсутствует " + src); return; }
            File.Copy(src, Path.Combine(targetDir, newName), false);
        }
        static string GitReport(string project)
        {
            try
            {
                string head = Git(project, "rev-parse --short HEAD");
                string status = Git(project, "status --porcelain --untracked-files=normal");
                return "Git HEAD: " + head.Trim() + "\nGit status: " + (string.IsNullOrWhiteSpace(status) ? "clean" : status.Trim());
            }
            catch (Exception ex) { return "Git not verifiable: " + ex.Message; }
        }
        static string Git(string project, string args)
        {
            using (var p = new Process())
            {
                p.StartInfo = new ProcessStartInfo("git", "-C \"" + project + "\" " + args)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(4000) || p.ExitCode != 0) throw new InvalidOperationException(stderr.Trim());
                return stdout;
            }
        }
        static float AxisDistance(List<Vector2> axis)
        {
            float length = 0f;
            for (int k = 1; k < axis.Count; k++) length += Vector2.Distance(axis[k - 1], axis[k]);
            return length;
        }
        static bool Inspect(out Inspection i, out string reason, out string report)
        {
            i = new Inspection(); reason = "";
            var checks = new List<string>();
            string firstFailure = "";
            void Fail(string msg) { checks.Add("BLOCK: " + msg); if (firstFailure.Length == 0) firstFailure = msg; }
            void Note(string msg) { checks.Add("INFO: " + msg); }
            i.scene = SceneManager.GetActiveScene();
            i.scenePath = i.scene.path;
            if (Application.isPlaying) Fail("Play Mode — нельзя применять");
            if (!i.scene.IsValid() || string.IsNullOrEmpty(i.scene.path)) Fail("сцена не сохранена в Assets");
            else
            {
                string sceneFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, i.scene.path);
                if (!File.Exists(sceneFile)) Fail("отсутствует файл активной сцены для резервного копирования");
                else i.sceneHash = FileHash(sceneFile);
            }
            if (i.scene.name != "Island_v2") Fail("ожидается сцена Island_v2, открыта " + i.scene.name);
            if (i.scene.isDirty) Fail("в сцене несохранённые изменения; сначала Ctrl+S");
            i.root = IslandBlockout.FindRoot();
            if (i.root == null || i.root.scene != i.scene) Fail("в активной сцене нет IslandV2");
            else
            {
                if (i.root.transform.position.sqrMagnitude > 0.00001f ||
                    Quaternion.Angle(i.root.transform.rotation, Quaternion.identity) > 0.01f ||
                    Vector3.Distance(i.root.transform.lossyScale, Vector3.one) > 0.001f)
                    Fail("IslandV2 не в нуле/единичном масштабе; меши рассчитаны в координатах IslandField");
                i.generated = i.root.transform.Find(IslandBlockout.GeneratedName);
                i.manual = i.root.transform.Find(IslandBlockout.ManualName);
                if (i.generated == null || i.manual == null) Fail("отсутствуют _Generated или _Manual");
                else
                {
                    string[] native = { IslandCliff.ObjectName, IslandRiverWater.ObjectName, IslandRiverWater.FallObjectName, IslandBlockout.BridgeName };
                    foreach (string n in native)
                    {
                        var node = i.generated.Find(n);
                        if (node == null) { Fail("отсутствует нативный узел _Generated/" + n + "; полную регенерацию НЕ запускаем"); continue; }
                        if (node.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                            Fail("не найден видимый рендерер внутри _Generated/" + n);
                    }
                    if (i.manual.Find(LegacyRootName2) != null)
                        Note("Обнаружен старый " + LegacyRootName2 + " — сначала Rollback в меню v0.2.");
                    if (i.manual.Find(LegacyRootName) != null)
                        Note("Обнаружен legacy-арт " + LegacyRootName + "; перед v0.3 его нужно удалить/откатить.");
                    if (i.manual.Find(RootName) != null)
                        Note("арт уже установлен; Apply повторно запрещён, доступен Rollback");
                }
                var data = i.root.GetComponent<IslandV2Data>();
                if (data == null) Fail("IslandV2Data отсутствует");
                else Note("IslandV2Data: depth=" + data.depth.ToString("F1") + ", outline=" + (data.outline == null ? 0 : data.outline.Count));
            }
            i.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (i.layout == null) Fail("нет IslandLayout.asset в " + IslandLayout.AssetPath);
            else
            {
                i.layoutHash = AssetDatabase.GetAssetDependencyHash(IslandLayout.AssetPath).ToString();
                if (!i.layout.buildRiverWater || !i.layout.buildCliff) Fail("buildRiverWater или buildCliff выключены");
                if (i.layout.riverPoints == null || i.layout.riverPoints.Count < 2) Fail("нет riverPoints");
                else
                {
                    i.field = new IslandField(i.layout);
                    i.pieces = i.field.RiverInsidePieces();
                    var entrances = i.field.RiverEdges.Where(x => !x.isOutlet).ToList();
                    var outlets = i.field.RiverEdges.Where(x => x.isOutlet).ToList();
                    Note("RiverInsidePieces=" + i.pieces.Count + ", входов=" + entrances.Count + ", выходов=" + outlets.Count);
                    if (i.pieces.Count != 1 || entrances.Count != 1 || outlets.Count != 1) Fail("требуется одна непрерывная река: ровно один вход и выход");
                    else
                    {
                        i.inlet = entrances[0]; i.outlet = outlets[0];
                        Note("ИСТОК edge=" + i.inlet.pos.ToString("F2") + "; УСТЬЕ edge=" + i.outlet.pos.ToString("F2"));
                        Note("Y water inlet=" + i.field.RiverSurfaceAt(i.inlet.pos.x, i.inlet.pos.y).ToString("F2") +
                             ", outlet=" + i.field.RiverSurfaceAt(i.outlet.pos.x, i.outlet.pos.y).ToString("F2"));
                        if (Vector2.Distance(i.inlet.pos, i.outlet.pos) < 28f || AxisDistance(i.pieces[0]) < 35f)
                            Fail("слишком короткий участок между истоком и устьем для безопасного размещения");
                        Vector2 springDir = (i.pieces[0][1] - i.pieces[0][0]).normalized;
                        Vector2 spring = i.pieces[0][0] + springDir * 9f;
                        Vector2 cross = new Vector2(-springDir.y, springDir.x);
                        int foundations = 0;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = spring + cross * side * (i.layout.riverWidth * 0.5f + 2.1f);
                            if (i.field.Inside(p.x, p.y) && i.field.EdgeDistance(p.x, p.y) >= 1f) foundations++;
                        }
                        if (foundations == 0) Fail("нет места под опоры грота у входа реки");
                        if (i.layout.crossings != null)
                            foreach (var c in i.layout.crossings)
                                if (Vector2.Distance(c.pos, i.inlet.pos) < 19f || Vector2.Distance(c.pos, i.outlet.pos) < 12f)
                                    Fail("брод / мост слишком близко к истоку или устью: размещение декора опасно");
                        var markerRoot = i.root != null ? i.root.transform.Find(IslandBlockout.MarkersName) : null;
                        foreach (var pair in new[] { new { name = "FALL_RiverIn", edge = i.inlet.pos }, new { name = "FALL_RiverOut", edge = i.outlet.pos } })
                        {
                            Transform marker = markerRoot != null ? markerRoot.Find(pair.name) : null;
                            if (marker == null) Fail("нет подтверждающей метки " + pair.name + "; сначала проверь сгенерированную сцену");
                            else if (Vector2.Distance(new Vector2(marker.position.x, marker.position.z), pair.edge) > 1.5f)
                                Fail("маркер " + pair.name + " расходится с полем IslandField более чем на 1.5 м — генерация устарела");
                        }
                    }
                }
                Note("riverWidth=" + i.layout.riverWidth.ToString("F1") + ", cliffWall=" + i.layout.cliffWall.ToString("F1"));
            }
            if (Shader.Find(FoamShader) == null) Fail("не установлен шейдер " + FoamShader);
            if (AssetDatabase.LoadAssetAtPath<Material>(IslandCliff.MaterialPath) == null) Fail("нет готового материала обрыва " + IslandCliff.MaterialPath);
            if (i.root != null)
            {
                int terrainColliders = i.root.GetComponentsInChildren<TerrainCollider>(true).Length;
                if (terrainColliders == 0) Fail("TerrainCollider отсутствует — игровая география не подтверждена");
                int colliders = i.root.GetComponentsInChildren<Collider>(true).Length;
                int lights = i.root.GetComponentsInChildren<Light>(true).Length;
                var bridgeObj = i.root.transform.Find(IslandBlockout.GeneratedName + "/" + IslandBlockout.BridgeName);
                int bridgeColliders = bridgeObj == null ? -1 : bridgeObj.GetComponentsInChildren<Collider>(true).Length;
                if (bridgeColliders < 1) Fail("не найдены физические коллайдеры существующего моста");
                Note("AUDIT_BASELINE:colliders=" + colliders + ";terrain=" + terrainColliders +
                     ";bridge=" + bridgeColliders + ";lights=" + lights);
                Note("Collider island=" + colliders +
                     ", TerrainCollider=" + terrainColliders + ", Light=" + lights);
                Note("Foreign active location objects: " + string.Join("; ", IslandBlockout.FindForeign(i.root.transform)));
            }
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            Note(GitReport(projectPath));
            string header = "TW RIVER OVERHAUL v0.3 / Unity " + Application.unityVersion + " / " + DateTime.UtcNow.ToString("O") + "\n";
            report = header + (i.layout != null ? AuditToken(i) + "\n" : "") + string.Join("\n", checks) + "\n";
            reason = firstFailure.Length == 0 ? "OK" : firstFailure;
            return !checks.Any(x => x.StartsWith("BLOCK:"));
        }
    }
}

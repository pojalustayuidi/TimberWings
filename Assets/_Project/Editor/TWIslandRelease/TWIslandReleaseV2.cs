// Timber Wings — island-wide release-candidate art pass. Unity 6 URP.
// Reversible _Manual-only overlay; no deletions from _Generated, IslandLayout, Nav, scene gameplay IDs.
// This is an artistic pass, NOT a certification of a shipping build.
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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace IslandV2
{
    public static class TWIslandReleaseV2
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISLAND RELEASE ART v0.2/";
        const string Root = "TW_ISLAND_RELEASE_ART_V2";
        const string AssetRoot = "Assets/_Project/IslandV2/ReleaseArt_V2";
        const string Generated = AssetRoot + "/Generated";
        const string SceneShader = "Project/IslandV2/TW_ReleaseVertexLit";
        const string Mat = Generated + "/TW_Release_VertexLit.mat";
        static readonly string[] MeshPaths = {
            Generated + "/OceanRim.asset", Generated + "/MeadowFlowers.asset",
            Generated + "/WoodlandMushrooms.asset", Generated + "/PondReeds.asset",
            Generated + "/Woodcraft.asset", Generated + "/GroundStoneAccents.asset",
            Generated + "/CampMosaic.asset", Generated + "/TrailRhythm.asset", Generated + "/WorksiteMotifs.asset"
        };
        static readonly string[] MeshNames = {
            "01_Ocean_Rim_Faceted_Stone", "02_Flowering_Meadows", "03_Woodland_Mushrooms",
            "04_Pond_Shore_Reeds", "05_Woodcraft_Forest_Details", "06_Forest_Floor_Stone",
            "07_Campfire_Stone_Mosaic", "08_Trail_Shoulder_Rhythm", "09_Worksite_Motifs"
        };
        sealed class SceneCheck
        {
            internal Scene scene;
            internal GameObject root;
            internal Transform manual;
            internal IslandLayout layout;
            internal IslandField field;
            internal string sceneHash, layoutHash;
            internal string report, failure;
            internal bool valid;
        }

        [MenuItem(Menu + "1 · AUDIT / Сначала проверка", false, 211)]
        public static void Audit()
        {
            var c = Inspect(true);
            string folder = ReportDirectory();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "IslandReleaseAudit_v0_2.txt");
            File.WriteAllText(path, c.report, Encoding.UTF8);
            if (c.valid) Debug.Log("TW ISLAND RELEASE ART — Audit PASS. " + path + "\n" + c.report);
            else Debug.LogError("TW ISLAND RELEASE ART — Audit BLOCKED: " + c.failure + ".\n" + path + "\n" + c.report);
        }

        [MenuItem(Menu + "2 · APPLY / Арт острова", false, 212)]
        public static void Apply()
        {
            var c = Inspect(false);
            if (!c.valid) { Debug.LogError("TW ISLAND RELEASE: сначала исправь Audit: " + c.failure); return; }
            string report = Path.Combine(ReportDirectory(), "IslandReleaseAudit_v0_2.txt");
            if (!File.Exists(report) || !File.ReadAllText(report).Contains(Token(c)))
            {
                Debug.LogError("TW ISLAND RELEASE: нужен свежий Audit той же сохранённой сцены и раскладки.");
                return;
            }
            if (c.manual.Find(Root) != null)
            {
                Debug.LogError("TW ISLAND RELEASE: такой арт уже установлен. Сначала Rollback или Toggle, чтобы не создавать дубликаты.");
                return;
            }
            if (c.manual.Find("TW_ISLAND_RELEASE_ART_V1") != null)
            {
                Debug.LogError("TW ISLAND RELEASE v0.2: обнаружен TW_ISLAND_RELEASE_ART_V1. Выполни старый Rollback, сохрани сцену и повтори Audit.");
                return;
            }
            foreach (var path in AllAssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogError("TW ISLAND RELEASE: существующий ассет " + path + " не будет перезаписан. Отмени старый Apply.");
                    return;
                }
            if (!EditorUtility.DisplayDialog("Timber Wings / Release Art v2",
                "Будет сохранён независимый бэкап сцены + IslandLayout.\n" +
                "Добавятся только арт-объекты под IslandV2/_Manual/" + Root + ".\n" +
                "Ничего не удаляется, география, столкновения, вода, мосты, сейвы и Nav не меняются.\n\n" +
                "Это просмотрная арт-итерация, а не уже протестированный релиз. Продолжить?", "Backup и Apply", "Отмена")) return;

            string backup;
            try { backup = Backup(c); }
            catch (Exception e) { Debug.LogError("TW ISLAND RELEASE: не удалось создать бэкап; изменений нет. " + e); return; }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TW Island Release Art / Apply");
            Transform created = null;
            try
            {
                EnsureFolder(Generated);
                Shader shader = Shader.Find(SceneShader);
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("URP-шейдер " + SceneShader + " отсутствует или не поддерживается.");
                var m = new Material(shader) { name = "TW_Release_VertexLit" };
                AssetDatabase.CreateAsset(m, Mat);
                var go = new GameObject(Root);
                Undo.RegisterCreatedObjectUndo(go, "TW Island Release Art root");
                go.transform.SetParent(c.manual, false);
                created = go.transform;

                var obstacles = CaptureObstacles(c.root);
                string baselineFingerprint = GeometryFingerprint(c.root);
                var baked = TWIslandReleaseGeometryV2.Make(c.field, obstacles);
                var composed = TWIslandReleaseComposition.Make(c.field, obstacles);
                int totalTris = baked.triangles + composed.triangles;
                if (totalTris > 40000)
                    throw new InvalidOperationException("Art budget exceeded: " + totalTris + " triangles > 40,000. No scene changes will be kept.");
                var bakedMeshes = new[] {
                    baked.edgeStone, baked.meadow, baked.woodland, baked.pond, baked.driftwood, baked.accents,
                    composed.camp, composed.trails, composed.craft
                };
                int createdRenderers = 0;
                for (int i = 0; i < bakedMeshes.Length; i++)
                    if (CreateMesh(created, bakedMeshes[i], MeshPaths[i], MeshNames[i], m)) createdRenderers++;
                string finalFingerprint = GeometryFingerprint(c.root);
                if (baselineFingerprint != finalFingerprint)
                    throw new InvalidOperationException("PHYSICS/GEOMETRY FINGERPRINT MISMATCH: Apply cancelled; existing colliders or transforms changed.");
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(c.scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = go;
                string reportDir = ReportDirectory();
                Directory.CreateDirectory(reportDir);
                string summary = "TW ISLAND RELEASE ART v0.2 / " + DateTime.UtcNow.ToString("O") + "\n" +
                                 "Scene: " + c.scene.path + "\nBackup: " + backup + "\n" +
                                 "MeshRenderers: " + createdRenderers + "\n" + baked.statistics + "\n" + composed.report + "\n" +
                                 "Triangles total=" + totalTris + "; native collider fingerprint=" + baselineFingerprint + " (PASS)\n" +
                                 "New colliders=0, lights=0, rigidbodies=0, scripts=0, nav obstacles=0.\n" +
                                 "MANUAL QA: day/night, bridge, river, pond, paths, all interaction objects, save/load, gameplay progression, FPS.\n";
                File.WriteAllText(Path.Combine(reportDir, "ApplyReport_v0_2.txt"), summary, Encoding.UTF8);
                Debug.Log("TW ISLAND RELEASE ART v0.2 APPLY: " + baked.statistics + "; " + composed.report + ". " + createdRenderers +
                          " renderers. Новый корень: IslandV2/_Manual/" + Root + ". Backup " + backup +
                          ". Ctrl+S, затем проверь Game View и проведи playtests. Полную регенерацию острова НЕ запускать.");
            }
            catch (Exception e)
            {
                if (created != null) Undo.DestroyObjectImmediate(created.gameObject);
                foreach (var path in AllAssetPaths())
                    if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                Debug.LogError("TW ISLAND RELEASE: Apply отменён; наш корень и созданные ассеты очищены. Backup: " + backup + ". " + e);
            }
        }

        [MenuItem(Menu + "3 · TOGGLE / Сравнить до-после", false, 213)]
        public static void Toggle()
        {
            if (Application.isPlaying) { Debug.LogError("TW ISLAND RELEASE: отключи Play Mode."); return; }
            var root = IslandBlockout.FindRoot();
            Transform tr = root == null ? null : root.transform.Find(IslandBlockout.ManualName + "/" + Root);
            if (tr == null) { Debug.LogWarning("TW ISLAND RELEASE: арт-пакет ещё не установлен."); return; }
            Undo.RecordObject(tr.gameObject, "Toggle TW Island Release Art");
            tr.gameObject.SetActive(!tr.gameObject.activeSelf);
            EditorSceneManager.MarkSceneDirty(tr.gameObject.scene);
            Debug.Log("TW ISLAND RELEASE: арт " + (tr.gameObject.activeSelf ? "включён" : "отключён") + ". Сравни Game/Scene View в одной камере.");
        }

        [MenuItem(Menu + "4 · ROLLBACK / Убрать только новый арт", false, 214)]
        public static void Rollback()
        {
            if (Application.isPlaying) { Debug.LogError("TW ISLAND RELEASE: отключи Play Mode."); return; }
            GameObject sceneRoot = IslandBlockout.FindRoot();
            Transform tr = sceneRoot == null ? null : sceneRoot.transform.Find(IslandBlockout.ManualName + "/" + Root);
            if (tr == null) { Debug.LogWarning("TW ISLAND RELEASE: арт не найден; ничего не изменено."); return; }
            if (!EditorUtility.DisplayDialog("TW Island Release Art — Rollback",
                "Будет удалён только наш корень " + Root + " и десять ассетов в " + AssetRoot +
                ". Исходные игровые объекты, River Overhaul, материалы острова и _Generated останутся нетронутыми.", "Удалить наш арт", "Отмена")) return;
            Undo.DestroyObjectImmediate(tr.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            foreach (var path in AllAssetPaths())
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            Debug.Log("TW ISLAND RELEASE: rollback выполнен. Остальные объекты острова не изменены. Ctrl+S.");
        }

        [MenuItem(Menu + "5 · CLEANUP AUDIT / Старое и дубли", false, 215)]
        public static void CleanupAudit()
        {
            var c = Inspect(true);
            var result = new StringBuilder();
            result.AppendLine("TW ISLAND RELEASE — CLEANUP CANDIDATES (REPORT ONLY; NOTHING REMOVED)");
            result.AppendLine("Scene: " + c.scene.path);
            if (c.root != null)
            {
                var all = c.root.GetComponentsInChildren<Transform>(true);
                foreach (var name in new[] { "TW_RIVER_OVERHAUL_V1", "TW_RIVER_OVERHAUL_V2", "TW_RIVER_OVERHAUL_V3" })
                    foreach (var tr in all.Where(x => x.name == name))
                        result.AppendLine("LEGACY RIVER OVERLAY: " + HierarchyPath(tr) + ", active=" + tr.gameObject.activeSelf +
                                          " — рассмотреть Rollback ТОЛЬКО соответствующей версии после проверки Game View");
                var foreign = IslandBlockout.FindForeign(c.root.transform);
                foreach (string s in foreign) result.AppendLine("FOREIGN LOCATION OBJECT: " + s + " — проверить вручную, не удалять автоматически");
                foreach (var issue in all.Where(x => x.name.StartsWith("TW_ISLAND_RELEASE_ART_V") && x.name != Root))
                    result.AppendLine("OLD RELEASE OVERLAY: " + HierarchyPath(issue) + " — проверить вручную");
                result.AppendLine("Warning: BLK_*, Markers, Ground_Terrain, River_WaterVolume, Bridge, StoryFlow, SaveSystem and Nav objects are PROTECTED.");
            }
            result.AppendLine("Git status:\n" + GitStatus());
            string directory = ReportDirectory(); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "CleanupCandidates.txt");
            File.WriteAllText(path, result.ToString(), Encoding.UTF8);
            Debug.Log("TW ISLAND RELEASE: создан обзор кандидатов на уборку без удаления: " + path + "\n" + result);
        }

        [MenuItem(Menu + "6 · QA / Снимок Game View + статус", false, 216)]
        public static void CaptureGameView()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("TW ISLAND RELEASE: снимок Game View требует Play Mode и нужного ракурса камеры.");
                return;
            }
            GameObject root = IslandBlockout.FindRoot();
            Transform tr = root == null ? null : root.transform.Find(IslandBlockout.ManualName + "/" + Root);
            if (tr == null) { Debug.LogError("TW ISLAND RELEASE: Apply отсутствует."); return; }
            string dir = Path.Combine(ReportDirectory(), "GameView"); Directory.CreateDirectory(dir);
            string stem = "TW_Island_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, stem + ".png");
            ScreenCapture.CaptureScreenshot(path);
            int totalCol = root.GetComponentsInChildren<Collider>(true).Length;
            int addedCol = tr.GetComponentsInChildren<Collider>(true).Length;
            int triangles = 0;
            foreach (var mf in tr.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) triangles += (int)mf.sharedMesh.GetIndexCount(s) / 3;
            string text = "Scene: " + SceneManager.GetActiveScene().path + "\n" +
                          "Time UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                          "Screenshot (async request): " + path + "\n" +
                          "Art active: " + tr.gameObject.activeInHierarchy + "\n" +
                          "Art triangles: " + triangles + "\n" +
                          "Art colliders: " + addedCol + "\nIsland colliders: " + totalCol + "\n" +
                          "Day/night is user controlled. FPS, saves, gameplay navigation are NOT validated by a screenshot.\n";
            File.WriteAllText(Path.Combine(dir, stem + ".txt"), text, Encoding.UTF8);
            Debug.Log("TW ISLAND RELEASE: Game View screenshot requested: " + path + "; QA status: " + Path.Combine(dir, stem + ".txt"));
        }

        // Native physics fingerprint before/after Apply; catches accidental changes to existing collider transforms.
        static string GeometryFingerprint(GameObject root)
        {
            var sb = new StringBuilder();
            var list = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in list.OrderBy(x => HierarchyPath(x.transform), StringComparer.Ordinal)
                                  .ThenBy(x => x.GetType().FullName, StringComparer.Ordinal))
            {
                Transform tr = c.transform;
                Vector3 p = tr.position, s = tr.lossyScale;
                Quaternion q = tr.rotation;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append(HierarchyPath(tr)).Append('|').Append(c.GetType().FullName)
                  .Append('|').Append(c.enabled ? "1" : "0")
                  .Append('|').Append(p.x.ToString("R", ci)).Append(',').Append(p.y.ToString("R", ci)).Append(',').Append(p.z.ToString("R", ci))
                  .Append('|').Append(s.x.ToString("R", ci)).Append(',').Append(s.y.ToString("R", ci)).Append(',').Append(s.z.ToString("R", ci))
                  .Append('|').Append(q.x.ToString("R", ci)).Append(',').Append(q.y.ToString("R", ci)).Append(',').Append(q.z.ToString("R", ci)).Append(',').Append(q.w.ToString("R", ci))
                  .AppendLine();
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "");
        }

        static List<TWIslandReleaseGeometryV2.Avoid> CaptureObstacles(GameObject root)
        {
            var list = new List<TWIslandReleaseGeometryV2.Avoid>(800);
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c is TerrainCollider || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                Bounds b = c.bounds;
                if (b.size.x > 10f || b.size.z > 10f || b.size.y > 20f) continue; // WaterVolume / huge triggers.
                float r = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.55f, 3.3f);
                list.Add(new TWIslandReleaseGeometryV2.Avoid(new Vector2(b.center.x, b.center.z), r));
            }
            return list;
        }
        static bool CreateMesh(Transform parent, Mesh mesh, string path, string name, Material material)
        {
            if (mesh == null || mesh.vertexCount == 0) return false;
            AssetDatabase.CreateAsset(mesh, path);
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return true;
        }
        static IEnumerable<string> AllAssetPaths()
        {
            yield return Mat;
            foreach (var path in MeshPaths) yield return path;
        }
        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
        static string ReportDirectory()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TW_IslandRelease_Reports");
        }
        static string Token(SceneCheck c)
        {
            return "AUDIT_TOKEN:scene=" + c.sceneHash + ";layout=" + c.layoutHash + ";scenePath=" + c.scene.path;
        }
        static string HashFile(string path)
        {
            if (!File.Exists(path)) return "MISSING";
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string ProjectFile(string path)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar));
        }
        static string Backup(SceneCheck c)
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                      "TW_IslandRelease_Backups", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff"));
            Directory.CreateDirectory(dir);
            BackupFile(c.scene.path, Path.Combine(dir, "Scene_before_apply.unity"), true);
            BackupFile(c.scene.path + ".meta", Path.Combine(dir, "Scene_before_apply.unity.meta"), false);
            BackupFile(IslandLayout.AssetPath, Path.Combine(dir, "IslandLayout.asset"), true);
            BackupFile(IslandLayout.AssetPath + ".meta", Path.Combine(dir, "IslandLayout.asset.meta"), false);
            File.WriteAllText(Path.Combine(dir, "BACKUP_INFO.txt"),
                "Source scene: " + c.scene.path + "\nScene SHA-256: " + c.sceneHash +
                "\nIslandLayout: " + IslandLayout.AssetPath + "\nLayout hash: " + c.layoutHash + "\n" +
                "Do not replace scene asset GUIDs / meta without manual review.\n", Encoding.UTF8);
            return dir;
        }
        static void BackupFile(string src, string dest, bool required)
        {
            string full = ProjectFile(src);
            if (!File.Exists(full))
            {
                if (required) throw new FileNotFoundException("Не найден файл для резервной копии: " + full);
                return;
            }
            File.Copy(full, dest, false);
        }
        static string HierarchyPath(Transform tr)
        {
            var names = new List<string>();
            for (Transform x = tr; x != null; x = x.parent) names.Add(x.name);
            names.Reverse(); return string.Join("/", names);
        }
        static string GitStatus()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            try
            {
                using (var p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo("git", "-C \"" + project + "\" status --short")
                    { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    p.Start();
                    string s = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(4000) || p.ExitCode != 0) return "not available: " + err;
                    return string.IsNullOrWhiteSpace(s) ? "CLEAN" : s;
                }
            }
            catch (Exception e) { return "not available: " + e.Message; }
        }
        static SceneCheck Inspect(bool withGit)
        {
            var c = new SceneCheck { scene = SceneManager.GetActiveScene() };
            var sb = new StringBuilder(); string err = null;
            void Block(string text) { sb.AppendLine("BLOCK: " + text); if (err == null) err = text; }
            void Info(string text) { sb.AppendLine("INFO: " + text); }
            sb.AppendLine("TIMBER WINGS / RELEASE ART AUDIT / Unity " + Application.unityVersion + " / " + DateTime.UtcNow.ToString("O"));
            if (Application.isPlaying) Block("Play Mode — выйди из Play Mode");
            if (!c.scene.IsValid() || string.IsNullOrEmpty(c.scene.path)) Block("сцена не сохранена на диск");
            else
            {
                c.sceneHash = HashFile(ProjectFile(c.scene.path));
                if (c.sceneHash == "MISSING") Block("файл активной сцены не существует");
            }
            if (c.scene.name != "Island_v2") Block("открыта сцена '" + c.scene.name + "', ожидается отдельная сохранённая копия Island_v2.unity");
            if (c.scene.isDirty) Block("сцена имеет несохранённые изменения — Ctrl+S, затем снова Audit");
            c.root = IslandBlockout.FindRoot();
            if (c.root == null || c.root.scene != c.scene) Block("нет корня IslandV2 в активной сцене");
            else
            {
                if (c.root.transform.position.sqrMagnitude > 0.0001f ||
                    Quaternion.Angle(c.root.transform.rotation, Quaternion.identity) > 0.1f ||
                    Vector3.Distance(c.root.transform.lossyScale, Vector3.one) > 0.001f)
                    Block("IslandV2 имеет transform != identity — генерация в world space была бы неверной");
                c.manual = c.root.transform.Find(IslandBlockout.ManualName);
                Transform generated = c.root.transform.Find(IslandBlockout.GeneratedName);
                if (c.manual == null || generated == null) Block("нет _Manual или _Generated");
                else
                {
                    foreach (string n in new[] { IslandCliff.ObjectName, IslandRiverWater.ObjectName,
                                                    IslandRiverWater.FallObjectName, IslandBlockout.BridgeName })
                        if (generated.Find(n) == null) Block("отсутствует _Generated/" + n);
                    if (c.manual.Find(Root) != null) Info("наш арт уже установлен — Apply не создаёт дубликаты");
                    foreach (string n in new[] { "TW_RIVER_OVERHAUL_V1", "TW_RIVER_OVERHAUL_V2" })
                    {
                        var tr = c.manual.Find(n);
                        if (tr != null && tr.gameObject.activeSelf) Info("старый River Overhaul: " + n +
                            " — возможно визуальное наложение на новый водопад; см. Cleanup Audit");
                    }
                }
                var data = c.root.GetComponent<IslandV2Data>();
                if (data == null || data.outline == null || data.outline.Count < 30) Block("IslandV2Data / outline отсутствует либо повреждён");
                int col = c.root.GetComponentsInChildren<Collider>(true).Length;
                int tc = c.root.GetComponentsInChildren<TerrainCollider>(true).Length;
                Transform br = generated == null ? null : generated.Find(IslandBlockout.BridgeName);
                int bc = br == null ? -1 : br.GetComponentsInChildren<Collider>(true).Length;
                Info("BASELINE islandColliders=" + col + ", TerrainColliders=" + tc + ", BridgeColliders=" + bc);
                Info("BASELINE geometryFingerprint=" + GeometryFingerprint(c.root));
                if (tc < 1 || bc < 1) Block("TerrainCollider или игровые коллайдеры речного моста отсутствуют");
            }
            c.layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (c.layout == null) Block("не найден IslandLayout.asset");
            else
            {
                c.layoutHash = AssetDatabase.GetAssetDependencyHash(IslandLayout.AssetPath).ToString();
                if (c.layout.riverPoints == null || c.layout.riverPoints.Count < 2) Block("riverPoints недостаточно для проверки географии");
                else
                {
                    c.field = new IslandField(c.layout);
                    if (c.field.Contour == null || c.field.Contour.Count < 40) Block("IslandField.Contour повреждён");
                    if (c.field.RiverInsidePieces().Count != 1) Block("ожидается одна непрерывная река");
                    Info("Area=" + c.field.Area.ToString("F0") + ", meadows=" +
                         (c.layout.meadows == null ? 0 : c.layout.meadows.Count) + ", anchors=" +
                         (c.layout.anchors == null ? 0 : c.layout.anchors.Count));
                }
            }
            if (Shader.Find(SceneShader) == null) Block("не найден шейдер " + SceneShader);
            if (withGit) Info("Git status:\n" + GitStatus());
            c.valid = err == null;
            c.failure = err ?? "OK";
            c.report = sb.ToString() + (c.layout != null ? Token(c) + "\n" : "");
            return c;
        }
    }
}

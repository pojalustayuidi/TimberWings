// Explicit in-Unity validation harness; never alters DayNightCycle or Time.timeScale.
// Day/night must be set through the project's own controls. Captures the CURRENT Game View.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class TWRiverOverhaulQAV3
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/RIVER OVERHAUL v0.3/";
        static readonly List<float> ms = new List<float>(320);
        static int lastFrame;
        static double startTime;
        static string label;
        static string outDir;

        [MenuItem(Menu + "7. QA: День — Game View + FPS", false, 176)]
        static void Day() { Begin("DAY"); }

        [MenuItem(Menu + "8. QA: Ночь — Game View + FPS", false, 177)]
        static void Night() { Begin("NIGHT"); }

        static void Begin(string name)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("TW RIVER QA: открой Game View в Play Mode, выставь " + name +
                               " средствами проекта, наведи обзорную камеру и повтори команду.");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Island_v2") { Debug.LogError("TW RIVER QA: неверная сцена " + scene.name); return; }
            GameObject root = IslandBlockout.FindRoot();
            if (root == null || root.transform.Find(IslandBlockout.ManualName + "/" + TWRiverOverhaulV3.RootName) == null)
            {
                Debug.LogError("TW RIVER QA: Apply отсутствует — сначала открой сцену с художественным пакетом.");
                return;
            }
            EditorApplication.update -= Sample;
            label = name;
            ms.Clear();
            lastFrame = Time.frameCount;
            startTime = EditorApplication.timeSinceStartup;
            outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/TW_RiverOverhaul/QA");
            Directory.CreateDirectory(outDir);
            Debug.Log("TW RIVER QA: фиксация " + label + ": 180 игровых кадров (FPS редактора; не производительность билда).");
            EditorApplication.update += Sample;
        }
        static void Sample()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= Sample;
                Debug.LogWarning("TW RIVER QA: Play Mode остановлен до завершения захвата.");
                return;
            }
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.0001f && dt < 1f) ms.Add(dt * 1000f);
            }
            if (ms.Count >= 180 || EditorApplication.timeSinceStartup - startTime >= 20.0)
            {
                EditorApplication.update -= Sample;
                Finish();
            }
        }
        static void Finish()
        {
            if (ms.Count < 30) { Debug.LogError("TW RIVER QA: недостаточно игровых кадров для статистики FPS."); return; }
            var root = IslandBlockout.FindRoot();
            if (root == null) return;
            float meanMs = ms.Average();
            float fpsMean = 1000f / meanMs;
            var sorted = ms.OrderByDescending(x => x).ToList();
            // Lowest-performing 1% of frame rates = reciprocal of mean worst 1% frametimes.
            float worstMs = sorted.Take(Mathf.Max(1, Mathf.CeilToInt(sorted.Count * 0.01f))).Average();
            float low1Fps = 1000f / worstMs;
            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            int terrains = root.GetComponentsInChildren<TerrainCollider>(true).Length;
            int lights = root.GetComponentsInChildren<Light>(true).Length;
            var bridge = root.transform.Find(IslandBlockout.GeneratedName + "/" + IslandBlockout.BridgeName);
            int bridgeColliders = bridge != null ? bridge.GetComponentsInChildren<Collider>(true).Length : -1;
            var art = root.transform.Find(IslandBlockout.ManualName + "/" + TWRiverOverhaulV3.RootName);
            int artColliders = art != null ? art.GetComponentsInChildren<Collider>(true).Length : -1;
            int artLights = art != null ? art.GetComponentsInChildren<Light>(true).Length : -1;
            int artParticles = art != null ? art.GetComponentsInChildren<ParticleSystem>(true).Length : -1;
            int artTris = 0;
            if (art != null)
                foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                        for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                            artTris += (int)filter.sharedMesh.GetIndexCount(s) / 3;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string baseName = "TW_River_" + label + "_" + stamp;
            string png = Path.Combine(outDir, baseName + ".png");
            string txt = Path.Combine(outDir, baseName + ".txt");
            // Unity captures the current Game View asynchronously; file will appear after rendering.
            ScreenCapture.CaptureScreenshot(png);
            Camera camera = Camera.main;
            var sb = new StringBuilder();
            sb.AppendLine("TW RIVER / " + label + " / " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            sb.AppendLine("Frames: " + ms.Count + "; editor FPS mean: " + fpsMean.ToString("F1") +
                          "; editor 1% low: " + low1Fps.ToString("F1"));
            sb.AppendLine("Island colliders: " + colliders + "; TerrainColliders: " + terrains +
                          "; Bridge colliders: " + bridgeColliders + "; island Lights: " + lights);
            string auditPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library/TW_RiverOverhaul/Audit.txt");
            if (File.Exists(auditPath))
            {
                string baseline = File.ReadAllLines(auditPath).FirstOrDefault(x => x.Contains("AUDIT_BASELINE:"));
                if (baseline != null)
                {
                    sb.AppendLine("Original pre-Apply " + baseline.Trim());
                    bool matches = baseline.Contains("colliders=" + colliders + ";terrain=" + terrains +
                                    ";bridge=" + bridgeColliders + ";lights=" + lights);
                    sb.AppendLine("Baseline parity: " + (matches ? "PASS" : "REVIEW DIFFERENCES (runtime systems may create objects)"));
                }
                else sb.AppendLine("Baseline parity: MISSING baseline in Audit; cannot compare.");
            }
            sb.AppendLine("River art: tris=" + artTris + "; Colliders=" + artColliders +
                          "; Lights=" + artLights + "; ParticleSystems=" + artParticles);
            sb.AppendLine("Camera: " + (camera == null ? "Camera.main not set" : camera.transform.position.ToString("F2") +
                          " facing " + camera.transform.forward.ToString("F2")));
            sb.AppendLine("Screenshot: " + png);
            sb.AppendLine("Time of day: USER-SELECTED (this tool does not read or change DayNightCycle).");
            sb.AppendLine("FPS is measured inside Unity Editor; profile on target device for shipping decisions.");
            sb.AppendLine("Manual checks still required: visual continuity, nav paths, swimming, bridge traversal, saves / interactions.");
            File.WriteAllText(txt, sb.ToString(), Encoding.UTF8);
            Debug.Log("TW RIVER QA: " + label + " captured, " + fpsMean.ToString("F1") + " FPS mean / " + low1Fps.ToString("F1") +
                      " FPS 1% low. Colliders added=" + artColliders + ", lights added=" + artLights + ". Report: " + txt);
        }
    }
}

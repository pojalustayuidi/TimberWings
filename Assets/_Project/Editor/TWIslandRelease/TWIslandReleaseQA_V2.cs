// Timber Wings / Release Art / Editor FPS + visual evidence helper.
// No runtime scene modifications. Sampled Editor FPS is NOT a shipping benchmark.
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
    public static class TWIslandReleaseQA_V2
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISLAND RELEASE ART v0.2/";
        const string RootName = "TW_ISLAND_RELEASE_ART_V2";
        static readonly List<float> FrameTimes = new List<float>(240);
        static int lastFrame;
        static double started;
        static string phase;

        [MenuItem(Menu + "7 · QA / День, FPS и Game View", false, 217)]
        static void Day() { Begin("DAY"); }

        [MenuItem(Menu + "8 · QA / Ночь, FPS и Game View", false, 218)]
        static void Night() { Begin("NIGHT"); }

        static void Begin(string name)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("TW RELEASE QA: требуется Play Mode. Включи время суток (" + name +
                               ") средствами проекта и выставь контрольный Game View ракурс.");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Island_v2") { Debug.LogError("TW RELEASE QA: неверная сцена " + scene.name); return; }
            var root = IslandBlockout.FindRoot();
            if (root == null || root.transform.Find(IslandBlockout.ManualName + "/" + RootName) == null)
            {
                Debug.LogError("TW RELEASE QA: пакет не установлен. Выполни Audit и Apply.");
                return;
            }
            EditorApplication.update -= Update;
            FrameTimes.Clear();
            phase = name; started = EditorApplication.timeSinceStartup; lastFrame = Time.frameCount;
            EditorApplication.update += Update;
            Debug.Log("TW RELEASE QA: замер 180 кадров редактора в фазе " + name +
                      ". Фазу суток скрипт не переключает — она должна быть выставлена в игре.");
        }
        static void Update()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= Update;
                Debug.LogWarning("TW RELEASE QA: Play Mode завершён раньше измерения.");
                return;
            }
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                float dt = Time.unscaledDeltaTime;
                if (dt >= 0.0001f && dt <= 1f) FrameTimes.Add(dt * 1000f);
            }
            if (FrameTimes.Count >= 180 || EditorApplication.timeSinceStartup - started > 30.0)
            {
                EditorApplication.update -= Update;
                End();
            }
        }
        static void End()
        {
            if (FrameTimes.Count < 40)
            {
                Debug.LogError("TW RELEASE QA: недостаточно кадров для отчёта FPS: " + FrameTimes.Count);
                return;
            }
            var root = IslandBlockout.FindRoot();
            if (root == null) return;
            var art = root.transform.Find(IslandBlockout.ManualName + "/" + RootName);
            if (art == null) return;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(project, "TW_IslandRelease_Reports/QA");
            Directory.CreateDirectory(dir);
            string stem = "IslandRelease_" + phase + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string shot = Path.Combine(dir, stem + ".png");
            ScreenCapture.CaptureScreenshot(shot);
            float mean = FrameTimes.Average();
            var slow = FrameTimes.OrderByDescending(x => x).Take(Mathf.Max(1, Mathf.CeilToInt(FrameTimes.Count * 0.01f))).ToList();
            float low1 = 1000f / slow.Average();
            int wholeCols = root.GetComponentsInChildren<Collider>(true).Length;
            int artCols = art.GetComponentsInChildren<Collider>(true).Length;
            int lights = art.GetComponentsInChildren<Light>(true).Length;
            int renderers = art.GetComponentsInChildren<MeshRenderer>(true).Length;
            Transform bridge = root.transform.Find(IslandBlockout.GeneratedName + "/" + IslandBlockout.BridgeName);
            int bridgeCols = bridge == null ? -1 : bridge.GetComponentsInChildren<Collider>(true).Length;
            int tris = 0;
            foreach (var filter in art.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                        tris += (int)filter.sharedMesh.GetIndexCount(sub) / 3;
            var sb = new StringBuilder();
            sb.AppendLine("TIMBER WINGS / ISLAND RELEASE CANDIDATE / " + phase);
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            sb.AppendLine("Frames: " + FrameTimes.Count + "; average Editor FPS: " + (1000f / mean).ToString("F1") +
                          "; 1% low: " + low1.ToString("F1"));
            sb.AppendLine("Art renderers: " + renderers + "; triangles: " + tris +
                          "; art colliders: " + artCols + "; art lights: " + lights);
            sb.AppendLine("Island colliders: " + wholeCols + "; bridge colliders: " + bridgeCols);
            sb.AppendLine("ART SAFETY (only): " + (artCols == 0 && lights == 0 ? "PASS" : "FAIL"));
            sb.AppendLine("Screenshot request: " + shot);
            sb.AppendLine("Time of day is not verified automatically. User must set the phase using native system.");
            sb.AppendLine("NOT shipping certification: capture Windows build FPS, GPU timings and RAM; test saves, paths and all gameplay loops.");
            File.WriteAllText(Path.Combine(dir, stem + ".txt"), sb.ToString(), Encoding.UTF8);
            Debug.Log("TW RELEASE QA " + phase + ": Editor average " + (1000f / mean).ToString("F1") +
                      " FPS / 1% low " + low1.ToString("F1") +
                      "; new colliders=" + artCols + "; report " + Path.Combine(dir, stem + ".txt"));
        }
    }
}

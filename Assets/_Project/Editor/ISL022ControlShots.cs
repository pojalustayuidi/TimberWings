// Timber Wings / ISL-022 — repeatable GAME VIEW evidence (Unity 6 / URP).
// Install: Assets/_Project/Editor/IslandV2/ISL022ControlShots.cs
// This editor tool never modifies serialized scenes, player transforms, saves, materials or prefabs.
// Only on an explicit "Repeat pose" action it temporarily overrides the rendering camera
// within SRP begin/end camera rendering callbacks, restoring the camera at the end of each render.
// Captures are stored OUTSIDE Assets in <project>/ISL-022_ControlShots/.
// Source reviewed against Scripts(5).zip. NOT independently compiled in Unity.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public sealed class ISL022ControlShots : EditorWindow
    {
        [Serializable]
        private sealed class Pose
        {
            public string key, scenePath, recordedUtc;
            public bool ready, orthographic;
            public Vector3 position;
            public Quaternion rotation;
            public float fov, orthoSize, time01;
            public string dayPhase;
        }

        [Serializable]
        private sealed class PoseLibrary
        {
            public Pose[] poses = new Pose[4];
        }

        private const string DirectoryName = "ISL-022_ControlShots";
        private static readonly string[] Keys = { "CENTER", "BRIDGE", "OVERVIEW", "SYMBOL" };
        private PoseLibrary library;
        private int selected;
        private Camera trackedCamera;
        private bool replaying;
        private bool autoStopReplayAfterShot;
        private Pose replayPose;
        private bool renderingOverride;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private bool previousOrthographic;
        private float previousFov, previousOrthoSize;
        private readonly Queue<float> frameSeconds = new Queue<float>(120);
        private float frameSecondsSum;
        private int lastFrameId = -1;
        private string pendingPath;
        private string pendingMetadata;
        private string pendingOverlay;
        private double pendingStart;
        private string message = "Ready. Start Play Mode and position the GAME camera.";

        private static string OutputDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), DirectoryName);
        private static string LibraryPath => Path.Combine(OutputDirectory, "ISL022_CameraPresets.json");

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-022/1. Контрольные ракурсы (Game View)", false, 190)]
        private static void OpenTool() => GetWindow<ISL022ControlShots>("ISL-022 Shots");

        private void OnEnable()
        {
            LoadLibrary();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            // In the unlikely event a window closes during an SRP callback, undo the override.
            RestoreCamera();
            StopReplay();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreCamera();
                StopReplay();
                if (!string.IsNullOrEmpty(pendingPath))
                {
                    message = "Play stopped before the screenshot completed. Check output folder.";
                    pendingPath = null;
                }
            }
            frameSeconds.Clear();
            frameSecondsSum = 0f;
            lastFrameId = -1;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ISL-022 / Repeatable Game View", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode only. Captures 4 real Game View angles with a sampled FPS label, camera pose and day/night metadata. No scene or save files are changed.", MessageType.Info);
            string scene = SceneManager.GetActiveScene().path;
            EditorGUILayout.LabelField("Scene:", string.IsNullOrEmpty(scene) ? "(unsaved)" : scene);
            if (!string.IsNullOrEmpty(scene) && scene.IndexOf("_Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
                EditorGUILayout.HelpBox("Scene is under _Recovery. Images are tagged with its exact path; this does not confirm the shipping scene.", MessageType.Warning);
            EditorGUILayout.LabelField("Game camera:", Camera.main == null ? "MainCamera not found" : Camera.main.name);
            EditorGUILayout.LabelField("Camera mode:", !EditorApplication.isPlaying ? "not playing" : (OrbitCamera.Active ? "Orbit" : "Follow / other"));
            EditorGUILayout.LabelField("Time of day:", EditorApplication.isPlaying ? DayNightCycle.Phase + " / " + DayNightCycle.Time01.ToString("0.000", CultureInfo.InvariantCulture) : "(Play Mode only)");
            string fps = EstimateFPS();
            EditorGUILayout.LabelField("Sampled FPS:", fps + " (not a Profiler benchmark)");
            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(replaying))
                selected = GUILayout.SelectionGrid(selected, Keys, 2, GUILayout.Height(48));
            Pose pose = CurrentPose();
            EditorGUILayout.LabelField("Saved pose:", pose.ready ? (pose.recordedUtc + " | " + pose.scenePath) : "not recorded");
            EditorGUILayout.Space(4);
            bool usable = EditorApplication.isPlaying && !EditorApplication.isPaused && Camera.main != null && string.IsNullOrEmpty(pendingPath);
            using (new EditorGUI.DisabledScope(!usable))
            {
                if (GUILayout.Button("SAVE CURRENT VIEW + SCREENSHOT", GUILayout.Height(36)))
                    Capture(true);
                using (new EditorGUI.DisabledScope(!pose.ready || pose.scenePath != scene))
                {
                    if (!replaying && GUILayout.Button("REPEAT SAVED POSE (temporary render preview)", GUILayout.Height(30)))
                        StartReplay(pose);
                    if (replaying && GUILayout.Button("STOP REPEAT — return to live camera", GUILayout.Height(30)))
                        StopReplay();
                    if (replaying && GUILayout.Button("SCREENSHOT REPEATED POSE", GUILayout.Height(32)))
                        Capture(false);
                }
            }
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open ISL-022 output folder"))
            {
                Directory.CreateDirectory(OutputDirectory);
                EditorUtility.RevealInFinder(OutputDirectory);
            }
            if (GUILayout.Button("Open Game View tab")) EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorGUILayout.HelpBox(message, MessageType.None);
            EditorGUILayout.LabelField("Capture order: CENTER → BRIDGE → OVERVIEW → SYMBOL. Photograph same presets in subsequent passes using Repeat.", EditorStyles.wordWrappedMiniLabel);
        }

        private void StartReplay(Pose pose)
        {
            if (!EditorApplication.isPlaying || Camera.main == null || !pose.ready) return;
            if (pose.scenePath != SceneManager.GetActiveScene().path)
            {
                message = "Saved preset belongs to another scene. Reuse it only in the original scene.";
                return;
            }
            trackedCamera = Camera.main;
            replayPose = pose;
            replaying = true;
            autoStopReplayAfterShot = false;
            message = "Replay active: only the rendered camera is overridden. Stop Repeat when done.";
        }

        private void StopReplay()
        {
            RestoreCamera();
            replaying = false;
            autoStopReplayAfterShot = false;
            replayPose = null;
            trackedCamera = null;
        }

        private Pose CurrentPose()
        {
            if (library == null) library = new PoseLibrary();
            if (library.poses == null || library.poses.Length != 4) library.poses = new Pose[4];
            if (library.poses[selected] == null) library.poses[selected] = new Pose { key = Keys[selected] };
            return library.poses[selected];
        }

        private void LoadLibrary()
        {
            library = new PoseLibrary();
            try
            {
                if (File.Exists(LibraryPath))
                    library = JsonUtility.FromJson<PoseLibrary>(File.ReadAllText(LibraryPath, Encoding.UTF8)) ?? new PoseLibrary();
            }
            catch (Exception e) { message = "Could not read saved presets: " + e.Message; }
        }

        private void SaveLibrary()
        {
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(LibraryPath, JsonUtility.ToJson(library, true), new UTF8Encoding(false));
        }

        private void Capture(bool recordCurrentView)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Camera.main == null || !string.IsNullOrEmpty(pendingPath)) return;
            try
            {
                Pose pose = CurrentPose();
                Camera cam = Camera.main;
                if (recordCurrentView)
                {
                    if (replaying) { message = "Stop Repeat before saving a NEW live camera pose."; return; }
                    pose.key = Keys[selected];
                    pose.scenePath = SceneManager.GetActiveScene().path;
                    pose.recordedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                    pose.position = cam.transform.position;
                    pose.rotation = cam.transform.rotation;
                    pose.orthographic = cam.orthographic;
                    pose.fov = cam.fieldOfView;
                    pose.orthoSize = cam.orthographicSize;
                    pose.time01 = DayNightCycle.Time01;
                    pose.dayPhase = DayNightCycle.Phase.ToString();
                    pose.ready = true;
                    SaveLibrary();
                    // Lock only the render camera for the single capture to make the saved
                    // pose match the screenshot. No runtime camera controller is disabled.
                    StartReplay(pose);
                    autoStopReplayAfterShot = true;
                }
                else if (!replaying || !pose.ready || pose.scenePath != SceneManager.GetActiveScene().path)
                {
                    message = "Click Repeat Saved Pose for this scene first."; return;
                }
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                string basename = "ISL-022_" + Keys[selected] + (recordCurrentView ? "_live_" : "_repeat_") + stamp;
                pendingPath = Path.Combine(OutputDirectory, basename + ".png");
                pendingOverlay = "ISL022 " + Keys[selected] + " FPS " + EstimateFPS();
                pendingMetadata = MakeMetadata(pose, cam, recordCurrentView, pendingOverlay);
                pendingStart = EditorApplication.timeSinceStartup;
                ScreenCapture.CaptureScreenshot(pendingPath, 1); // Game View framebuffer, not Scene View.
                message = "Screenshot requested: " + Path.GetFileName(pendingPath);
            }
            catch (Exception e)
            {
                pendingPath = null;
                if (autoStopReplayAfterShot) StopReplay();
                message = "Capture failed: " + e.Message;
                Debug.LogException(e);
            }
        }

        private string MakeMetadata(Pose pose, Camera cam, bool live, string caption)
        {
            var sb = new StringBuilder(1500);
            sb.AppendLine("TIMBER WINGS / ISL-022 GAME VIEW EVIDENCE");
            sb.AppendLine("Captured UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            sb.AppendLine("Scene in _Recovery: " + (SceneManager.GetActiveScene().path.Contains("_Recovery") ? "YES / unverified playable scene" : "no"));
            sb.AppendLine("Slot: " + Keys[selected]);
            sb.AppendLine("Shot: " + (live ? "live GAME camera" : "saved rendering-pose replay"));
            sb.AppendLine("Caption: " + caption + " (sampled FPS, NOT Profiler FPS)");
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Resolution at request: " + Screen.width + "x" + Screen.height);
            sb.AppendLine("Quality preset: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            sb.AppendLine("Camera: " + cam.name);
            sb.AppendLine("OrbitCamera.Active: " + OrbitCamera.Active);
            sb.AppendLine("Preset camera position: " + pose.position.ToString("F4"));
            sb.AppendLine("Preset camera rotation quaternion: " + pose.rotation.ToString("F5"));
            sb.AppendLine("Projection: " + (pose.orthographic ? "Orthographic" : "Perspective"));
            sb.AppendLine("FieldOfView: " + pose.fov.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("OrthographicSize: " + pose.orthoSize.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("Preset phase / time01: " + pose.dayPhase + " / " + pose.time01.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine("Actual phase / time01 at capture: " + DayNightCycle.Phase + " / " + DayNightCycle.Time01.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine("FPS sample frames: " + frameSeconds.Count);
            sb.AppendLine("NOTICE: camera-pose replay does not teleport the player or freeze the day/night simulation.");
            sb.AppendLine("NOTICE: no Play Mode safety, saved-game migration or release-scene status is proven by this image.");
            return sb.ToString();
        }

        private void Tick()
        {
            if (string.IsNullOrEmpty(pendingPath)) return;
            if (!EditorApplication.isPlaying)
            {
                pendingPath = null;
                if (autoStopReplayAfterShot) StopReplay();
                return;
            }
            if (File.Exists(pendingPath))
            {
                try
                {
                    var info = new FileInfo(pendingPath);
                    if (info.Length < 100) return;
                    StampPngWithFPS(pendingPath, pendingOverlay);
                    File.WriteAllText(Path.ChangeExtension(pendingPath, ".txt"), pendingMetadata, new UTF8Encoding(false));
                    message = "DONE: " + Path.GetFileName(pendingPath) + " (+ .txt metadata).";
                    Debug.Log("[ISL-022] " + message + " Folder: " + OutputDirectory);
                }
                catch (IOException) { return; } // screenshot still being written; retry next tick
                catch (Exception e) { Debug.LogException(e); message = "PNG exists but metadata/overlay failed: " + e.Message; }
                pendingPath = null;
                if (autoStopReplayAfterShot) StopReplay();
            }
            else if (EditorApplication.timeSinceStartup - pendingStart > 20.0)
            {
                message = "Screenshot timed out. Keep Game View visible and Play Mode running; try again.";
                pendingPath = null;
                if (autoStopReplayAfterShot) StopReplay();
            }
            Repaint();
        }

        private void OnBeginCameraRender(ScriptableRenderContext context, Camera cam)
        {
            if (!EditorApplication.isPlaying || Camera.main == null || cam != Camera.main) return;
            if (lastFrameId != Time.frameCount)
            {
                lastFrameId = Time.frameCount;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.001f && dt < 0.5f)
                {
                    frameSeconds.Enqueue(dt);
                    frameSecondsSum += dt;
                    while (frameSeconds.Count > 120) frameSecondsSum -= frameSeconds.Dequeue();
                }
            }
            if (!replaying || trackedCamera != cam || renderingOverride) return;
            Pose pose = replayPose;
            if (pose == null || !pose.ready) return;
            previousPosition = cam.transform.position;
            previousRotation = cam.transform.rotation;
            previousOrthographic = cam.orthographic;
            previousFov = cam.fieldOfView;
            previousOrthoSize = cam.orthographicSize;
            renderingOverride = true;
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            cam.orthographic = pose.orthographic;
            cam.fieldOfView = pose.fov;
            cam.orthographicSize = pose.orthoSize;
        }

        private void OnEndCameraRender(ScriptableRenderContext context, Camera cam)
        {
            if (renderingOverride && trackedCamera == cam) RestoreCamera();
        }

        private void RestoreCamera()
        {
            if (!renderingOverride) return;
            if (trackedCamera != null)
            {
                trackedCamera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                trackedCamera.orthographic = previousOrthographic;
                trackedCamera.fieldOfView = previousFov;
                trackedCamera.orthographicSize = previousOrthoSize;
            }
            renderingOverride = false;
        }

        private string EstimateFPS()
        {
            if (frameSeconds.Count < 20 || frameSecondsSum <= 0f) return "N/A";
            return (frameSeconds.Count / frameSecondsSum).ToString("F1", CultureInfo.InvariantCulture);
        }

        // Labels are baked into saved PNGs AFTER ScreenCapture; they do not add UI, components or materials to the scene.
        private static void StampPngWithFPS(string path, string label)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false)) throw new InvalidOperationException("Invalid PNG");
                int w = tex.width, h = tex.height;
                var pixels = tex.GetPixels32();
                int scale = (w >= 1500) ? 3 : 2;
                int textWidth = label.Length * 6 * scale;
                int boxW = Mathf.Min(w, textWidth + 24);
                int boxH = 7 * scale + 24;
                int x0 = 12, y0 = Mathf.Max(0, h - boxH - 12); // pixel coordinates count from bottom
                for (int y = y0; y < y0 + boxH && y < h; y++)
                    for (int x = x0; x < x0 + boxW && x < w; x++)
                    {
                        int i = y * w + x;
                        Color32 old = pixels[i];
                        pixels[i] = new Color32((byte)(old.r * 0.25f), (byte)(old.g * 0.25f), (byte)(old.b * 0.25f), 255);
                    }
                DrawText(pixels, w, h, x0 + 12, y0 + 12, label, scale);
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { DestroyImmediate(tex); }
        }

        private static readonly Dictionary<char, string> Font = new Dictionary<char, string>
        {
            // Each glyph is seven rows of five pixels, starting at top-left.
            {'A',"01110 10001 10001 11111 10001 10001 10001"},
            {'B',"11110 10001 10001 11110 10001 10001 11110"},
            {'C',"01111 10000 10000 10000 10000 10000 01111"},
            {'D',"11110 10001 10001 10001 10001 10001 11110"},
            {'E',"11111 10000 10000 11110 10000 10000 11111"},
            {'F',"11111 10000 10000 11110 10000 10000 10000"},
            {'G',"01111 10000 10000 10111 10001 10001 01111"},
            {'H',"10001 10001 10001 11111 10001 10001 10001"},
            {'I',"11111 00100 00100 00100 00100 00100 11111"},
            {'J',"00111 00010 00010 00010 10010 10010 01100"},
            {'K',"10001 10010 10100 11000 10100 10010 10001"},
            {'L',"10000 10000 10000 10000 10000 10000 11111"},
            {'M',"10001 11011 10101 10101 10001 10001 10001"},
            {'N',"10001 11001 10101 10011 10001 10001 10001"},
            {'O',"01110 10001 10001 10001 10001 10001 01110"},
            {'P',"11110 10001 10001 11110 10000 10000 10000"},
            {'Q',"01110 10001 10001 10001 10101 10010 01101"},
            {'R',"11110 10001 10001 11110 10100 10010 10001"},
            {'S',"01111 10000 10000 01110 00001 00001 11110"},
            {'T',"11111 00100 00100 00100 00100 00100 00100"},
            {'U',"10001 10001 10001 10001 10001 10001 01110"},
            {'V',"10001 10001 10001 10001 10001 01010 00100"},
            {'W',"10001 10001 10001 10101 10101 10101 01010"},
            {'X',"10001 10001 01010 00100 01010 10001 10001"},
            {'Y',"10001 10001 01010 00100 00100 00100 00100"},
            {'Z',"11111 00001 00010 00100 01000 10000 11111"},
            {'0',"01110 10001 10011 10101 11001 10001 01110"},
            {'1',"00100 01100 00100 00100 00100 00100 01110"},
            {'2',"01110 10001 00001 00010 00100 01000 11111"},
            {'3',"11110 00001 00001 00110 00001 00001 11110"},
            {'4',"00010 00110 01010 10010 11111 00010 00010"},
            {'5',"11111 10000 10000 11110 00001 00001 11110"},
            {'6',"01110 10000 10000 11110 10001 10001 01110"},
            {'7',"11111 00001 00010 00100 01000 01000 01000"},
            {'8',"01110 10001 10001 01110 10001 10001 01110"},
            {'9',"01110 10001 10001 01111 00001 00001 01110"},
            {'.',"00000 00000 00000 00000 00000 01100 01100"},
            {'-',"00000 00000 00000 11111 00000 00000 00000"},
            {' ',"00000 00000 00000 00000 00000 00000 00000"}
        };

        private static void DrawText(Color32[] pixels, int w, int h, int ox, int oy, string text, int scale)
        {
            for (int c = 0; c < text.Length; c++)
            {
                string rows;
                if (!Font.TryGetValue(char.ToUpperInvariant(text[c]), out rows)) rows = Font[' '];
                string[] lines = rows.Split(' ');
                for (int ry = 0; ry < 7; ry++)
                    for (int rx = 0; rx < 5; rx++)
                        if (lines[ry][rx] == '1')
                            for (int yy = 0; yy < scale; yy++)
                                for (int xx = 0; xx < scale; xx++)
                                {
                                    int px = ox + (c * 6 + rx) * scale + xx;
                                    int py = oy + (6 - ry) * scale + yy;
                                    if (px >= 0 && px < w && py >= 0 && py < h)
                                        pixels[py * w + px] = new Color32(255, 246, 224, 255);
                                }
            }
        }
    }
}
#endif

#if UNITY_EDITOR
// Timber Wings | ISL-018 | Stone Harmony.
// Edit Mode only. Recolors existing plaza stone vertex colors WITHOUT rebuilding plaza,
// and adjusts special stone materials. Preserves ISL-011 Under_Rock/Grove_StoneMoss.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IslandV2
{
    public sealed class ISL018StoneSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float strength = 0.65f;
        public bool tunePlaza = true;
        public bool tuneStoryStones = true;
        [HideInInspector] public bool active = false;
    }

    [Serializable]
    public sealed class ISL018MaterialColorBackup
    {
        public string path;
        public string shader;
        public Color baseColor;
        public Color bottomColor;
        public float rim;
    }

    public sealed class ISL018StoneBackup : ScriptableObject
    {
        public bool captured;
        public string scenePath;
        public Vector3 meshBoundsSize;
        public int vertexCount;
        public Color[] plazaColors;
        public List<ISL018MaterialColorBackup> materials = new List<ISL018MaterialColorBackup>();
    }

    public static class ISL018StoneHarmony
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-018/";
        const string Folder = "Assets/_Project/IslandV2/";
        const string SettingsPath = Folder + "ISL018StoneSettings.asset";
        const string BackupPath = Folder + "ISL018StoneBackup.asset";
        const string RootPath = "IslandV2/_Generated/CampPlaza/Plaza_Mesh";
        const string TreeDir = "Assets/_Project/Prefabs/Trees/Generated/Materials/";
        const string TreeSDir = "Assets/_Project/Prefabs/TreeS/Generated/Materials/";
        const string TreeShader = "Project/TreeToon";

        // Index corresponds to original colors in IslandBlockout.Plaza.cs (W converts to linear).
        static readonly Color[] Sources =
        {
            Lin(0.93f, 0.88f, 0.76f), Lin(0.84f, 0.79f, 0.88f),
            Lin(0.88f, 0.78f, 0.60f), Lin(0.80f, 0.70f, 0.56f),
            Lin(0.72f, 0.66f, 0.78f), Lin(0.80f, 0.75f, 0.84f),
            Lin(0.66f, 0.62f, 0.74f)
        };
        static readonly Color[] Goals =
        {
            HexLin("#EDE2CC"), HexLin("#D9CCDB"), HexLin("#DFC9AE"),
            HexLin("#CEB69C"), HexLin("#BAACC9"), HexLin("#CFC4D8"),
            HexLin("#AEA2BE")
        };

        static Color Lin(float r, float g, float b) { return new Color(r, g, b, 1f).linear; }
        static Color Hex(string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c))
                throw new ArgumentException("Invalid ISL-018 color " + hex);
            return c;
        }
        static Color HexLin(string hex) { return Hex(hex).linear; }

        static ISL018StoneSettings GetSettings(bool create)
        {
            var s = AssetDatabase.LoadAssetAtPath<ISL018StoneSettings>(SettingsPath);
            if (s == null && create)
            {
                s = ScriptableObject.CreateInstance<ISL018StoneSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
                AssetDatabase.SaveAssets();
            }
            return s;
        }

        static ISL018StoneBackup GetBackup(bool create)
        {
            var s = AssetDatabase.LoadAssetAtPath<ISL018StoneBackup>(BackupPath);
            if (s == null && create)
            {
                s = ScriptableObject.CreateInstance<ISL018StoneBackup>();
                AssetDatabase.CreateAsset(s, BackupPath);
                AssetDatabase.SaveAssets();
            }
            return s;
        }

        // Called ONLY while generating Plaza. Until explicitly enabled, original colors stay intact.
        public static Color PlazaColor(int index, Color original)
        {
            if (index < 0 || index >= Goals.Length) return original;
            var s = GetSettings(false);
            if (s == null || !s.active || !s.tunePlaza) return original;
            return Color.Lerp(original, Goals[index], Mathf.Clamp01(s.strength));
        }

        static bool EditAllowed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ISL-018: выйдите из Play Mode.");
                return false;
            }
            if (string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path))
            {
                Debug.LogError("ISL-018: сначала сохраните сцену на диск.");
                return false;
            }
            return true;
        }

        static Mesh FindPlazaMesh()
        {
            var root = GameObject.Find("IslandV2");
            if (root == null) return null;
            var tr = root.transform.Find("_Generated/CampPlaza/Plaza_Mesh");
            if (tr == null) return null;
            var mf = tr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return null;
            // Never mutate imported or persistent mesh assets.
            if (AssetDatabase.Contains(mf.sharedMesh))
            {
                Debug.LogError("ISL-018: меш площади является ассетом. Изменять его напрямую небезопасно.");
                return null;
            }
            return mf.sharedMesh;
        }

        static bool SameMesh(Mesh mesh, ISL018StoneBackup b)
        {
            if (mesh == null || b.plazaColors == null || b.vertexCount != mesh.vertexCount ||
                b.plazaColors.Length != mesh.vertexCount) return false;
            var size = mesh.bounds.size;
            return (size - b.meshBoundsSize).sqrMagnitude < 0.0025f;
        }

        static bool IsStoryStone(Material m)
        {
            return m != null && m.shader != null && m.shader.name == TreeShader &&
                m.HasProperty("_BaseColor") && m.HasProperty("_BottomColor") && m.HasProperty("_RimStrength");
        }

        static IEnumerable<string> StoryPaths()
        {
            foreach (string dir in new[] { TreeDir, TreeSDir })
                foreach (string name in new[] { "Grandpa_Stone", "Sacred_Stone" })
                    yield return dir + name + ".mat";
        }

        static Color Mul(Color c, Vector3 f, float k)
        {
            return new Color(c.r * f.x * k, c.g * f.y * k, c.b * f.z * k, 1f);
        }
        static float Dot(Color a, Color b) { return a.r * b.r + a.g * b.g + a.b * b.b; }

        // Search for exactly the seven pre-ISL-018 stone pigments and generator face factors.
        // Other plaza vertices (flag, lamp, wood, flowers) remain byte-for-byte unchanged.
        static Color Retint(Color original, float weight, out bool matched)
        {
            const float threshold = 0.028f;
            Color best = original;
            float bestError = threshold;
            matched = false;
            var normalFace = new Vector3(1f, 1f, 1f);
            var slabSide = new Vector3(0.80f, 0.78f, 0.80f);
            var stoneTop = new Vector3(1.12f, 1.12f, 1.12f);
            for (int i = 0; i < Sources.Length; i++)
            {
                int count = i < 4 ? 2 : 2;
                for (int f = 0; f < count; f++)
                {
                    Vector3 factor = f == 0 ? normalFace : (i < 4 ? slabSide : stoneTop);
                    Color baseColor = Mul(Sources[i], factor, 1f);
                    float scale = Dot(original, baseColor) / Mathf.Max(1e-8f, Dot(baseColor, baseColor));
                    if (scale < 0.93f || scale > 1.07f) continue;
                    Color predicted = Mul(baseColor, normalFace, scale);
                    float dr = original.r - predicted.r;
                    float dg = original.g - predicted.g;
                    float db = original.b - predicted.b;
                    float error = Mathf.Sqrt((dr * dr + dg * dg + db * db) /
                        Mathf.Max(1e-6f, Dot(baseColor, baseColor)));
                    if (error >= bestError) continue;
                    matched = true;
                    bestError = error;
                    Color goal = Mul(Goals[i], factor, scale);
                    best = Color.Lerp(original, goal, weight);
                }
            }
            return best;
        }

        [MenuItem(Menu + "1. Открыть настройки", false, 181)]
        public static void OpenSettings()
        {
            var s = GetSettings(true);
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }

        [MenuItem(Menu + "2. Применить каменную палитру", false, 182)]
        public static void Apply()
        {
            if (!EditAllowed()) return;
            var mesh = FindPlazaMesh();
            if (mesh == null)
            {
                Debug.LogError("ISL-018: не найден сценовый меш " + RootPath + ". Ничего не изменено.");
                return;
            }
            var s = GetSettings(true);
            var backup = GetBackup(true);
            string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (!backup.captured)
            {
                Color[] colors = mesh.colors;
                if (colors == null || colors.Length != mesh.vertexCount)
                {
                    Debug.LogError("ISL-018: исходные цвета вершин площади отсутствуют. Ничего не изменено.");
                    return;
                }
                backup.scenePath = scenePath;
                backup.vertexCount = mesh.vertexCount;
                backup.meshBoundsSize = mesh.bounds.size;
                backup.plazaColors = (Color[])colors.Clone();
                backup.materials.Clear();
                foreach (string path in StoryPaths())
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!IsStoryStone(m)) continue;
                    backup.materials.Add(new ISL018MaterialColorBackup
                    {
                        path = path, shader = TreeShader,
                        baseColor = m.GetColor("_BaseColor"),
                        bottomColor = m.GetColor("_BottomColor"),
                        rim = m.GetFloat("_RimStrength")
                    });
                }
                backup.captured = true;
                EditorUtility.SetDirty(backup);
                AssetDatabase.SaveAssets(); // persist all originals BEFORE applying changes
            }
            if (backup.scenePath != scenePath || !SameMesh(mesh, backup))
            {
                Debug.LogError("ISL-018: сцена или меш площади изменились после создания резервной копии. " +
                    "Нельзя безопасно применить старую палитру; используйте Git/backup. Изменений нет.");
                return;
            }
            Undo.RecordObject(s, "ISL-018 active palette");
            s.active = true;
            EditorUtility.SetDirty(s);
            float weight = Mathf.Clamp01(s.strength);
            int painted = 0;
            if (s.tunePlaza)
            {
                var result = (Color[])backup.plazaColors.Clone();
                for (int i = 0; i < result.Length; i++)
                {
                    bool match;
                    result[i] = Retint(backup.plazaColors[i], weight, out match);
                    if (match) painted++;
                }
                Undo.RecordObject(mesh, "ISL-018 plaza stone vertex colors");
                mesh.colors = result;
                EditorUtility.SetDirty(mesh);
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            int materialsChanged = 0;
            foreach (var v in backup.materials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(v.path);
                if (!IsStoryStone(m)) continue;
                bool grandpa = v.path.EndsWith("Grandpa_Stone.mat", StringComparison.Ordinal);
                var top = Hex(grandpa ? "#C9C3D6" : "#D4CFE5");
                var bottom = Hex(grandpa ? "#746C8C" : "#81799D");
                float rim = grandpa ? 0.10f : 0.13f;
                float t = s.tuneStoryStones ? weight : 0f;
                Undo.RecordObject(m, "ISL-018 story stone palette");
                m.SetColor("_BaseColor", Color.Lerp(v.baseColor, top, t));
                m.SetColor("_BottomColor", Color.Lerp(v.bottomColor, bottom, t));
                m.SetFloat("_RimStrength", Mathf.Lerp(v.rim, rim, t));
                EditorUtility.SetDirty(m);
                materialsChanged++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-018: перекрашены вершины площади " + painted + "/" + mesh.vertexCount +
                ", материалов обработано " + materialsChanged + "; сила " + weight.ToString("0.00") +
                ". Деревья, рельеф, коллайдеры, ISL-011 валуны и река не перестраивались. " +
                "Резервная копия: " + BackupPath);
            if (painted < 100 && s.tunePlaza)
                Debug.LogWarning("ISL-018: распознано необычно мало каменных вершин. Проверьте, не изменялся ли генератор площади.");
        }

        [MenuItem(Menu + "3. Восстановить исходные цвета", false, 183)]
        public static void Restore()
        {
            if (!EditAllowed()) return;
            var b = GetBackup(false);
            if (b == null || !b.captured)
            {
                Debug.LogWarning("ISL-018: нет сохранённой резервной копии.");
                return;
            }
            var s = GetSettings(false);
            if (s != null)
            {
                Undo.RecordObject(s, "ISL-018 disable palette");
                s.active = false;
                EditorUtility.SetDirty(s);
            }
            int restored = 0;
            foreach (var v in b.materials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(v.path);
                if (!IsStoryStone(m)) continue;
                Undo.RecordObject(m, "ISL-018 restore stone material");
                m.SetColor("_BaseColor", v.baseColor);
                m.SetColor("_BottomColor", v.bottomColor);
                m.SetFloat("_RimStrength", v.rim);
                EditorUtility.SetDirty(m);
                restored++;
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var mesh = FindPlazaMesh();
            if (b.scenePath == scene.path && SameMesh(mesh, b))
            {
                Undo.RecordObject(mesh, "ISL-018 restore plaza stone colors");
                mesh.colors = (Color[])b.plazaColors.Clone();
                EditorUtility.SetDirty(mesh);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            else Debug.LogWarning("ISL-018: меш площади отличается от резервной копии. " +
                "Материалы восстановлены, но сценовый меш не изменён.");
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-018: восстановлено материалов " + restored +
                ". Резервная копия оставлена: " + BackupPath);
        }
    }
}
#endif

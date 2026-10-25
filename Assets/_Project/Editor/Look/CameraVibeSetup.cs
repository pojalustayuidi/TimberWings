#if UNITY_EDITOR
// Положи в Assets/_Project/Editor/CameraVibeSetup.cs
// Меню: Tools -> BuilderSim -> Camera Vibe (Apply)
// 1) вешает CameraVibe на основную камеру;
// 2) создаёт два глобальных Volume (URP): «Vibe Grade» (общий тёплый стилизованный вид)
//    и «Vibe Forest» (вес 0, плавно включается в лесу через ForestCameraZone: темнее, зеленее, виньетка сильнее).
// Тяжёлых эффектов нет (без DoF, зерна и хроматики): это нормально для телефона.
// Профили лежат в Assets/_Game/Camera. Кнопку можно нажимать повторно: настройки перезапишутся.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class CameraVibeSetup : ScriptableWizard
{
    [Header("Общий вид")]
    [Range(0f, 1f)] public float bloom = 0.45f;
    [Range(0f, 0.6f)] public float vignette = 0.28f;
    [Range(-20f, 40f)] public float saturation = 12f;
    [Range(-20f, 40f)] public float contrast = 8f;
    [Range(-30f, 30f)] public float warmth = 8f;          // температура: + теплее
    [Range(0f, 1f)] public float splitTone = 0.6f;        // холодные тени и тёплые света

    [Header("Лес (включается плавно у входа)")]
    public bool forestGrade = true;
    [Range(-1f, 0.5f)] public float forestExposure = -0.25f;
    [Range(0f, 0.7f)] public float forestVignette = 0.4f;

    [Header("Камера")]
    public bool addCameraVibe = true;

    const string Dir = "Assets/_Game/Camera";

    [MenuItem("Tools/BuilderSim/Визуал и свет/Camera Vibe (Apply)", false, 811)]
    static void Open()
    {
        var w = DisplayWizard<CameraVibeSetup>("Camera Vibe", "Применить");
        w.helpString = "Тёплый стилизованный грейдинг, отдельный грейд для леса и кинематографичные мелочи камеры.";
    }

    void OnWizardCreate()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("CameraVibe: нет камеры с тегом MainCamera."); return; }

        var data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Camera Vibe");
        data.renderPostProcessing = true;
        EditorUtility.SetDirty(data);

        if (addCameraVibe && cam.GetComponent<CameraVibe>() == null && cam.GetComponent<CameraFollow>() != null)
            Undo.AddComponent<CameraVibe>(cam.gameObject);

        if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Game", "Camera");

        // ---------- общий грейд
        var gradeProfile = Profile("VibeGrade");
        {
            var b = Get<Bloom>(gradeProfile);
            b.intensity.Override(bloom);
            b.threshold.Override(0.95f);
            b.scatter.Override(0.65f);
            b.tint.Override(new Color(1f, 0.93f, 0.82f));

            var vg = Get<Vignette>(gradeProfile);
            vg.intensity.Override(vignette);
            vg.smoothness.Override(0.5f);

            var ca = Get<ColorAdjustments>(gradeProfile);
            ca.postExposure.Override(0.12f);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(new Color(1f, 0.98f, 0.95f));

            var wb = Get<WhiteBalance>(gradeProfile);
            wb.temperature.Override(warmth);
            wb.tint.Override(-2f);

            var st = Get<SplitToning>(gradeProfile);
            st.shadows.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.32f, 0.45f, 0.66f), splitTone));
            st.highlights.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(1f, 0.82f, 0.55f), splitTone));
            st.balance.Override(-10f);

            var tm = Get<Tonemapping>(gradeProfile);
            tm.mode.Override(TonemappingMode.Neutral);
            Save(gradeProfile);
        }
        Vol("Vibe Grade", gradeProfile, 10f, 1f);

        // ---------- лесной грейд: перекрывает общий с весом, который ведёт ForestCameraZone
        var forestProfile = Profile("VibeForest");
        {
            var b = Get<Bloom>(forestProfile);
            b.intensity.Override(Mathf.Min(1f, bloom + 0.25f));
            b.threshold.Override(0.85f);
            b.scatter.Override(0.7f);
            b.tint.Override(new Color(0.85f, 1f, 0.9f));

            var vg = Get<Vignette>(forestProfile);
            vg.intensity.Override(forestVignette);
            vg.smoothness.Override(0.45f);

            var ca = Get<ColorAdjustments>(forestProfile);
            ca.postExposure.Override(0.12f + forestExposure);
            ca.contrast.Override(contrast + 6f);
            ca.saturation.Override(saturation + 8f);
            ca.colorFilter.Override(new Color(0.92f, 1f, 0.94f));

            var wb = Get<WhiteBalance>(forestProfile);
            wb.temperature.Override(warmth - 12f);
            wb.tint.Override(-6f);

            var st = Get<SplitToning>(forestProfile);
            st.shadows.Override(new Color(0.20f, 0.46f, 0.46f));
            st.highlights.Override(new Color(1f, 0.9f, 0.55f));
            st.balance.Override(-20f);

            var tm = Get<Tonemapping>(forestProfile);
            tm.mode.Override(TonemappingMode.Neutral);
            Save(forestProfile);
        }
        var fv = Vol("Vibe Forest", forestProfile, 20f, 0f);
        fv.gameObject.SetActive(forestGrade);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("CameraVibe: готово. Профили в " + Dir + ". Пересобери лес в Location Builder, чтобы ForestCameraZone подхватил лесной грейд (или он найдёт объект «Vibe Forest» сам при старте игры).");
    }

    static VolumeProfile Profile(string name)
    {
        string path = Dir + "/" + name + ".asset";
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (p == null)
        {
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
        }
        Undo.RecordObject(p, "Camera Vibe");
        return p;
    }

    static void Save(VolumeProfile p)
    {
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
    }

    static Volume Vol(string name, VolumeProfile profile, float priority, float weight)
    {
        Volume vol = null;
        foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (v.gameObject.name == name) { vol = v; break; }
        if (vol == null)
        {
            var go = new GameObject(name, typeof(Volume));
            Undo.RegisterCreatedObjectUndo(go, "Camera Vibe");
            vol = go.GetComponent<Volume>();
        }
        vol.isGlobal = true;
        vol.priority = priority;
        vol.weight = weight;
        vol.sharedProfile = profile;
        EditorUtility.SetDirty(vol);
        return vol;
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet(out T c))
        {
            c = p.Add<T>(true);
            if (AssetDatabase.Contains(p)) AssetDatabase.AddObjectToAsset(c, p);   // без этого настройки не сохранятся
        }
        c.active = true;
        return c;
    }
}
#endif

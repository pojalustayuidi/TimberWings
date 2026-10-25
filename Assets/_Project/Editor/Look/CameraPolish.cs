#if UNITY_EDITOR
// Положи в Assets/Editor/CameraPolish.cs
// Меню: Tools -> BuilderSim -> Camera Polish
// Настраивает картинку (URP): сглаживание на камере и эффекты в Global Volume.
// Меняет профиль Volume, который уже есть в сцене. Если хочешь сохранить старый вид, сначала продублируй профиль.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraPolish : ScriptableWizard
{
    [Header("Что включить")]
    public bool antiAliasing = true;      // SMAA на камере
    public bool bloom = true;             // мягкое свечение огоньков и фонариков
    public bool vignette = true;          // лёгкое затемнение по краям
    public bool colorGrading = true;      // чуть больше цвета и контраста
    [Tooltip("Размывает далёкий фон. Выглядит как «миниатюра», но заметно нагружает телефоны")]
    public bool depthOfField = false;

    [Header("Сила эффектов")]
    [Range(0f, 1f)] public float bloomIntensity = 0.4f;
    [Range(0f, 0.6f)] public float vignetteIntensity = 0.26f;
    [Range(-30f, 40f)] public float saturation = 14f;
    [Range(-30f, 40f)] public float contrast = 10f;

    [MenuItem("Tools/BuilderSim/Визуал и свет/Camera Polish", false, 810)]
    static void Open()
    {
        var w = DisplayWizard<CameraPolish>("Camera Polish", "Применить");
        w.helpString = "Настроит сглаживание камеры и пост-обработку в Global Volume (URP).";
    }

    void OnWizardCreate()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("CameraPolish: нет камеры с тегом MainCamera."); return; }

        // ---------- камера
        var data = cam.GetUniversalAdditionalCameraData();
        Undo.RecordObject(data, "Camera Polish");
        data.renderPostProcessing = true;
        if (antiAliasing)
        {
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
        EditorUtility.SetDirty(data);

        // ---------- Volume
        Volume vol = null;
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (!v.isGlobal) continue;
            if (vol == null || v.name.Contains("Global")) vol = v;
        }
        if (vol == null)
        {
            var go = new GameObject("Global Volume", typeof(Volume));
            Undo.RegisterCreatedObjectUndo(go, "Camera Polish");
            vol = go.GetComponent<Volume>();
            vol.isGlobal = true;
        }

        var profile = vol.sharedProfile;
        if (profile == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, "Assets/_Game/CameraPolishProfile.asset");
            vol.sharedProfile = profile;
        }
        Undo.RecordObject(profile, "Camera Polish");

        if (bloom)
        {
            var b = Get<Bloom>(profile);
            b.intensity.Override(bloomIntensity);
            b.threshold.Override(1.0f);
            b.scatter.Override(0.6f);
        }

        if (vignette)
        {
            var vg = Get<Vignette>(profile);
            vg.intensity.Override(vignetteIntensity);
            vg.smoothness.Override(0.45f);
        }

        if (colorGrading)
        {
            var ca = Get<ColorAdjustments>(profile);
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);

            var tm = Get<Tonemapping>(profile);
            tm.mode.Override(TonemappingMode.Neutral);
        }

        if (depthOfField)
        {
            float d = 26f;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) d = follow.distance;

            var dof = Get<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(d + 12f);   // всё ближе этого расстояния остаётся резким
            dof.gaussianEnd.Override(d + 45f);
            dof.gaussianMaxRadius.Override(1.0f);
            dof.highQualitySampling.Override(true);
        }
        else if (profile.TryGet(out DepthOfField old))
        {
            old.active = false;
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        EditorSceneManager_MarkDirty();
        Debug.Log("CameraPolish: готово. Профиль: " + AssetDatabase.GetAssetPath(profile));
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

    static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
#endif

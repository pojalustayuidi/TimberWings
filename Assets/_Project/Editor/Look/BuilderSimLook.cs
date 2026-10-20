#if UNITY_EDITOR
// Положи в Assets/Editor/BuilderSimLook.cs
// Меню: Tools → BuilderSim → Apply Look
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static EditorAssets;

public static class BuilderSimLook
{
    const string Dir = "Assets/_Game/Art";

    [MenuItem("Tools/BuilderSim/Apply Look")]
    public static void Apply()
    {
        EnsureFolder("Assets/_Game");
        EnsureFolder(Dir);

        SetupGround();
        SetupSkyAndFog();
        SetupLight();
        SetupCamera();
        SetupVolume();
        SetupUrpAsset();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("BuilderSim: вид применён. Сохрани сцену (Ctrl+S).");
    }

    // ---------- Земля ----------
    static void SetupGround()
    {
        var mat = GetMat("Grass", "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", Hex("#8CCB4A"));
        mat.SetFloat("_Smoothness", 0.1f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);

        var go = GameObject.Find("Ground_Auto");
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground_Auto";
            Undo.RegisterCreatedObjectUndo(go, "Create ground");
        }
        // Plane 10x10 м по умолчанию, scale 12 даёт 120x120 м
        go.transform.position = new Vector3(0f, -0.02f, 0f);
        go.transform.localScale = new Vector3(12f, 1f, 12f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Старые бирюзовые куски земли выключаем (не удаляем)
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            // тропы леса (Tools → Forest → Лес → «Тропы») лежат под корнем ForestTrails: их не выключаем
            if (t.name.StartsWith("ground_pathOpen") && !InForestTrails(t))
            {
                Undo.RecordObject(t.gameObject, "Hide old ground");
                t.gameObject.SetActive(false);
            }
        }
    }

    static bool InForestTrails(Transform t)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == "ForestTrails") return true;
        return false;
    }

    // ---------- Небо и туман ----------
    static void SetupSkyAndFog()
    {
        var sky = GetMat("Sky", "Skybox/Procedural");
        sky.SetColor("_SkyTint", Hex("#7FB6E6"));
        sky.SetColor("_GroundColor", Hex("#BFE3F5"));
        sky.SetFloat("_AtmosphereThickness", 0.9f);
        sky.SetFloat("_Exposure", 1.1f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.fogColor = Hex("#BFE3F5");

        DynamicGI.UpdateEnvironment();
    }

    // ---------- Свет ----------
    static void SetupLight()
    {
        Light sun = null;
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null)
        {
            var g = new GameObject("Directional Light");
            sun = g.AddComponent<Light>();
            sun.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(g, "Create sun");
        }

        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.color = Hex("#FFF1D6");
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        RenderSettings.sun = sun;
    }

    // ---------- Камера ----------
    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("BuilderSim: нет Main Camera"); return; }

        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 150f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    // ---------- Постобработка ----------
    static void SetupVolume()
    {
        var vol = UnityEngine.Object.FindFirstObjectByType<Volume>();
        if (vol == null)
        {
            var g = new GameObject("Global Volume");
            vol = g.AddComponent<Volume>();
            Undo.RegisterCreatedObjectUndo(g, "Create volume");
        }
        vol.isGlobal = true;

        string path = Dir + "/GlobalProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        vol.sharedProfile = profile;

        var tm = GetOverride<Tonemapping>(profile);
        tm.mode.Override(TonemappingMode.Neutral);

        var bloom = GetOverride<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.25f);

        var ca = GetOverride<ColorAdjustments>(profile);
        ca.postExposure.Override(0.2f);
        ca.contrast.Override(10f);
        ca.saturation.Override(15f);

        var vg = GetOverride<Vignette>(profile);
        vg.intensity.Override(0.2f);

        EditorUtility.SetDirty(profile);
    }

    static T GetOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T c))
        {
            c = profile.Add<T>(true);
            if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
        }
        c.active = true;
        return c;
    }

    // ---------- Тени и сглаживание в URP Asset ----------
    static void SetupUrpAsset()
    {
        var asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null) { Debug.LogWarning("BuilderSim: активный URP Asset не найден"); return; }

        var so = new SerializedObject(asset);
        SetFloat(so, "m_ShadowDistance", 60f);
        SetInt(so, "m_ShadowCascadeCount", 2);
        SetInt(so, "m_MSAA", 4);
        SetBool(so, "m_SoftShadowsSupported", true);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    // ---------- Вспомогательное ----------
    static void SetFloat(SerializedObject so, string n, float v)
    { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("URP: нет поля " + n); }
    static void SetInt(SerializedObject so, string n, int v)
    { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Debug.LogWarning("URP: нет поля " + n); }
    static void SetBool(SerializedObject so, string n, bool v)
    { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("URP: нет поля " + n); }

    static Material GetMat(string name, string shaderName)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
#endif
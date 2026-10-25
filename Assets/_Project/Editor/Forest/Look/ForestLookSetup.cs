#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/Look/ForestLookSetup.cs (Editor)
// Окно: Tools → Forest → Свет и атмосфера (задача 9).
//   «Применить всё» делает за один клик:
//     • из Assets/_Game/ForestLook/DappleCookie.png собирает несколько уровней контраста пятен света (Cookies/DappleLevel_N.png);
//     • пишет шейдеры лучей и пылинок и делает материалы;
//     • создаёт Volume «Forest Look» (тёплый грейд, мягкий bloom, лёгкая виньетка; priority 21, выше «Vibe Forest»);
//     • создаёт объект ForestLook (скрипт, который в лесу плавно включает пятна света, туман, лучи, пылинки);
//     • подтягивает ForestCameraZone (чуть круче сверху, чтобы кроны меньше закрывали тропу);
//     • приглушает старые SunShafts и SunPatches из «Декор леса», чтобы не дублировали новое.
//   «Проверка» печатает отчёт по производительности и камере, «Исправить URP» включает то, без чего эффекты не работают.
// Кнопку можно нажимать повторно: настройки перезаписываются, дубликатов не будет.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ForestLookWindow : EditorWindow
{
    const string Dir = "Assets/_Game/ForestLook";
    const string CookieSrc = Dir + "/DappleCookie.png";
    const string CookieDir = Dir + "/Cookies";
    const string ShaderDir = Dir + "/Shaders";
    const string MatDir = Dir + "/Materials";
    const string ProfilePath = Dir + "/ForestLookProfile.asset";
    const int Levels = 6;

    [MenuItem("Tools/BuilderSim/Лес/Свет и атмосфера", false, 201)]
    static void Open() { var w = GetWindow<ForestLookWindow>("Свет леса"); w.minSize = new Vector2(380f, 520f); }

    // ---- настройки окна (сохраняются между открытиями)
    bool doCookie = true, doShafts = true, doMotes = true, doVolume = true, doFog = true, doCamera = true, doMuteOld = true;
    float cookieSize = 22f;
    float fogMul = 1.5f;
    float shaftIntensity = 0.16f;
    float bloom = 0.55f, vignette = 0.30f, warmth = 14f;
    Vector2 scroll;
    string log = "";

    // ------------------------------------------------------------------------------------------ окно
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(
            "Пятна света (cookie на солнце), лёгкий туман, лучи, пылинки и тёплый грейд. Всё включается плавно, когда игрок заходит в лес.\n" +
            "Перед первым запуском положи DappleCookie.png в " + Dir + ".", MessageType.Info);

        GUILayout.Space(4f);
        EditorGUILayout.LabelField("Что делаем", EditorStyles.boldLabel);
        doCookie = EditorGUILayout.ToggleLeft("Пятна света и тени листвы на земле (cookie)", doCookie);
        if (doCookie) cookieSize = EditorGUILayout.Slider("   Размер узора, м", cookieSize, 8f, 50f);
        doFog = EditorGUILayout.ToggleLeft("Туман гуще и теплее в лесу", doFog);
        if (doFog) fogMul = EditorGUILayout.Slider("   Плотность в лесу, ×", fogMul, 1f, 3f);
        doShafts = EditorGUILayout.ToggleLeft("Лучи света сквозь крону", doShafts);
        if (doShafts) shaftIntensity = EditorGUILayout.Slider("   Яркость лучей", shaftIntensity, 0.04f, 0.4f);
        doMotes = EditorGUILayout.ToggleLeft("Пылинки в воздухе", doMotes);
        doVolume = EditorGUILayout.ToggleLeft("Volume: тёплый цветокор, bloom, виньетка", doVolume);
        if (doVolume)
        {
            bloom = EditorGUILayout.Slider("   Bloom", bloom, 0f, 1f);
            vignette = EditorGUILayout.Slider("   Виньетка", vignette, 0f, 0.6f);
            warmth = EditorGUILayout.Slider("   Теплота (температура)", warmth, -10f, 30f);
        }
        doCamera = EditorGUILayout.ToggleLeft("Камера леса: чуть круче сверху, меньше закрытия кронами", doCamera);
        doMuteOld = EditorGUILayout.ToggleLeft("Приглушить старые лучи и пятна из «Декор леса»", doMuteOld);

        GUILayout.Space(6f);
        if (GUILayout.Button("Применить всё", GUILayout.Height(34f))) Run("Применить", ApplyAll);

        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Проверка", EditorStyles.boldLabel);
        if (GUILayout.Button("Отчёт: производительность и камера", GUILayout.Height(26f))) Run("Отчёт", Audit);
        if (GUILayout.Button("Исправить URP (HDR, MSAA, масштаб, cookie, SRP Batcher)")) Run("URP", FixUrp);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Пятна в Scene View: вкл")) Run("Предпросмотр", () => Preview(true));
        if (GUILayout.Button("Пятна в Scene View: выкл")) Run("Предпросмотр", () => Preview(false));
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Скриншот игровой камеры (Tools → Forest → Лес)"))
            Run("Скриншот", () => ForestShots.Game(ForestSettings.Load()));

        if (!string.IsNullOrEmpty(log))
        {
            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(log, MessageType.None);
        }
        EditorGUILayout.EndScrollView();
    }

    void Run(string title, System.Func<string> action)
    {
        if (Application.isPlaying && title != "Отчёт") { log = "Во время Play это не меняется: выйди из Play."; return; }
        try { log = action(); }
        catch (System.Exception ex) { Debug.LogException(ex); log = title + ": ошибка, см. Console."; }
        SceneView.RepaintAll();
        Repaint();
    }

    // ------------------------------------------------------------------------------------------ применить всё
    string ApplyAll()
    {
        var sb = new StringBuilder();
        Ensure(Dir); Ensure(CookieDir); Ensure(ShaderDir); Ensure(MatDir);

        Texture2D[] cookies = null;
        if (doCookie)
        {
            cookies = BuildCookies(sb);
            if (cookies == null) return sb.ToString();
        }

        Material shaftMat = null, moteMat = null;
        if (doShafts || doMotes)
        {
            string sp = WriteShader(ShaderDir + "/ForestShaft.shader", ShaftSrc);
            string mp = WriteShader(ShaderDir + "/ForestMote.shader", MoteSrc);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (doShafts) shaftMat = MakeMat("ForestShaft", sp, new Color(1f, 0.88f, 0.58f, 1f), shaftIntensity, sb);
            if (doMotes) moteMat = MakeMat("ForestMote", mp, new Color(1f, 0.93f, 0.68f, 1f), 1f, sb);
        }

        Volume lookVol = doVolume ? SetupVolume(sb) : FindVolume("Forest Look");

        // объект ForestLook
        var look = Object.FindFirstObjectByType<ForestLook>();
        if (look == null)
        {
            var go = new GameObject("ForestLook");
            Undo.RegisterCreatedObjectUndo(go, "Forest Look");
            look = go.AddComponent<ForestLook>();
        }
        else Undo.RecordObject(look, "Forest Look");

        look.dappled = doCookie;
        if (cookies != null) look.cookieLevels = cookies;
        look.cookieSize = cookieSize;
        look.fog = doFog;
        look.fogDensityMul = fogMul;
        look.shafts = doShafts;
        if (shaftMat != null) look.shaftMaterial = shaftMat;
        look.lookVolume = lookVol;
        look.forestVolume = FindVolume("Vibe Forest");
        look.cameraAssist = doCamera;

        if (doMotes && moteMat != null) look.motes = SetupMotes(look.transform, moteMat);
        else if (!doMotes) look.motes = null;

        EditorUtility.SetDirty(look);

        if (doCamera) sb.AppendLine(SetupCamera());
        if (doMuteOld) sb.AppendLine(MuteOld());

        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
        AssetDatabase.SaveAssets();

        sb.Insert(0, "Готово. Сохрани сцену (Ctrl+S), запусти Play и зайди в лес.\n");
        sb.AppendLine("Нужны эффекты в URP: нажми «Исправить URP» один раз, если пятна света или bloom не видны.");
        Debug.Log("[ForestLook]\n" + sb);
        return sb.ToString();
    }

    // ------------------------------------------------------------------------------------------ cookie
    Texture2D[] BuildCookies(StringBuilder sb)
    {
        if (!File.Exists(ProjectPath(CookieSrc)))
        {
            sb.AppendLine("Нет файла " + CookieSrc + ". Положи туда DappleCookie.png (он лежит рядом со скриптами) и нажми ещё раз.");
            return null;
        }

        var src = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
        if (!ImageConversion.LoadImage(src, File.ReadAllBytes(ProjectPath(CookieSrc)), false))
        {
            sb.AppendLine("Не удалось прочитать " + CookieSrc);
            return null;
        }
        Color32[] basePx = src.GetPixels32();
        int w = src.width, h = src.height;
        Object.DestroyImmediate(src);

        var result = new Texture2D[Levels];
        for (int lvl = 1; lvl <= Levels; lvl++)
        {
            float k = lvl / (float)Levels;   // сила пятен: от слабой до полной
            var px = new Color32[basePx.Length];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Lerp(255f, basePx[i].r, k)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(255f, basePx[i].g, k)),
                    (byte)Mathf.RoundToInt(Mathf.Lerp(255f, basePx[i].b, k)), 255);
            }
            var t = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            t.SetPixels32(px);
            t.Apply();
            string path = CookieDir + "/DappleLevel_" + lvl + ".png";
            File.WriteAllBytes(ProjectPath(path), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;                      // значения = множитель света как есть
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.wrapMode = TextureWrapMode.Repeat;        // узор повторяется по миру
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 512;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.isReadable = false;
                imp.SaveAndReimport();
            }
            result[lvl - 1] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        sb.AppendLine("Cookie: " + Levels + " уровней в " + CookieDir + ".");
        return result;
    }

    // ------------------------------------------------------------------------------------------ шейдеры и материалы
    static string WriteShader(string assetPath, string src)
    {
        File.WriteAllText(ProjectPath(assetPath), src.Replace('`', '"'));
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        return assetPath;
    }

    static Material MakeMat(string name, string shaderPath, Color color, float intensity, StringBuilder sb)
    {
        var sh = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (sh == null) { sb.AppendLine("Шейдер не загрузился: " + shaderPath + " (см. Console, возможно ошибка компиляции)."); return null; }
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else m.shader = sh;
        m.SetColor("_Color", color);
        m.SetFloat("_Intensity", intensity);
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    const string ShaftSrc = @"Shader `ForestLook/Shaft`
{
    Properties
    {
        _Color(`Color`, Color) = (1,0.88,0.58,1)
        _Intensity(`Intensity`, Float) = 0.16
        _Alpha(`Alpha`, Float) = 1
        _FadeNear(`Fade near`, Float) = 3
        _FadeFar(`Fade far`, Float) = 8
    }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` }
        Pass
        {
            Name `Shaft`
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Alpha;
                float _FadeNear;
                float _FadeFar;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                o.wp = TransformObjectToWorld(i.pos.xyz);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                // мягкие бока
                float x = abs(i.uv.x * 2.0 - 1.0);
                float side = 1.0 - x * x;
                side = side * side;
                // у земли плавно появляется, к кроне тает
                float v = i.uv.y;
                float along = smoothstep(0.0, 0.12, v) * (1.0 - smoothstep(0.35, 1.0, v));
                // лёгкое мерцание, бегущее вдоль луча
                float flick = 0.78 + 0.22 * sin(_Time.y * 0.9 + i.wp.x * 1.3 + i.wp.z * 1.7 + v * 3.0);
                // не режем кадр жёстко, когда луч близко к камере
                float camD = distance(_WorldSpaceCameraPos, i.wp);
                float camFade = saturate((camD - _FadeNear) / max(0.01, _FadeFar - _FadeNear));
                float a = side * along * flick * camFade * _Alpha * _Intensity * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}";

    const string MoteSrc = @"Shader `ForestLook/Mote`
{
    Properties { _Color(`Color`, Color) = (1,0.93,0.68,1) _Intensity(`Intensity`, Float) = 1 }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` }
        Pass
        {
            Name `Mote`
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
            CBUFFER_END
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a = a * a;
                half4 c = _Color * i.col;
                return half4(c.rgb, c.a * a * _Intensity);
            }
            ENDHLSL
        }
    }
}";

    // ------------------------------------------------------------------------------------------ пылинки
    static ParticleSystem SetupMotes(Transform parent, Material mat)
    {
        Transform t = parent.Find("Motes");
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject("Motes");
            Undo.RegisterCreatedObjectUndo(go, "Forest Motes");
            go.transform.SetParent(parent, false);
        }
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.70f, 0.9f), new Color(1f, 0.85f, 0.55f, 0.55f));
        main.maxParticles = 120;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 14f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(22f, 5f, 22f);
        sh.position = new Vector3(0f, 1.5f, 0f);

        // лёгкий дрейф: у всех трёх осей один и тот же режим кривой, иначе Unity ругается
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        vel.y = new ParticleSystem.MinMaxCurve(0.00f, 0.10f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.1f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = LightProbeUsage.Off;
        rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

        EditorUtility.SetDirty(go);
        return ps;
    }

    // ------------------------------------------------------------------------------------------ Volume
    Volume SetupVolume(StringBuilder sb)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        Undo.RecordObject(profile, "Forest Look");

        // мягкий тёплый bloom: порог ниже общего, чтобы светились именно пятна солнца и фонари
        var b = Get<Bloom>(profile);
        b.intensity.Override(bloom);
        b.threshold.Override(0.9f);
        b.scatter.Override(0.72f);
        b.tint.Override(new Color(1f, 0.92f, 0.78f));
        b.highQualityFiltering.Override(false);   // для телефона

        var vg = Get<Vignette>(profile);
        vg.intensity.Override(vignette);
        vg.smoothness.Override(0.6f);

        var ca = Get<ColorAdjustments>(profile);
        ca.postExposure.Override(0f);
        ca.contrast.Override(12f);
        ca.saturation.Override(14f);
        ca.colorFilter.Override(new Color(1f, 0.97f, 0.90f));

        var wb = Get<WhiteBalance>(profile);
        wb.temperature.Override(warmth);
        wb.tint.Override(-4f);

        // тёплые света, прохладные зелёно-бирюзовые тени
        var st = Get<SplitToning>(profile);
        st.shadows.Override(new Color(0.30f, 0.55f, 0.52f));
        st.highlights.Override(new Color(1f, 0.80f, 0.50f));
        st.balance.Override(-15f);

        var tm = Get<Tonemapping>(profile);
        tm.mode.Override(TonemappingMode.Neutral);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        Volume vol = FindVolume("Forest Look");
        if (vol == null)
        {
            var go = new GameObject("Forest Look", typeof(Volume));
            Undo.RegisterCreatedObjectUndo(go, "Forest Look");
            vol = go.GetComponent<Volume>();
        }
        vol.isGlobal = true;
        vol.priority = 21f;       // Vibe Grade 10, Island Light 15, Vibe Forest 20, потолок Low-качества 100
        vol.weight = 0f;          // ведёт ForestLook
        vol.sharedProfile = profile;
        EditorUtility.SetDirty(vol);
        sb.AppendLine("Volume «Forest Look»: priority 21, weight ведёт скрипт.");
        return vol;
    }

    static Volume FindVolume(string name)
    {
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (v.gameObject.name == name) return v;
        return null;
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

    // ------------------------------------------------------------------------------------------ камера леса
    static string SetupCamera()
    {
        var cz = Object.FindFirstObjectByType<ForestCameraZone>();
        if (cz == null)
        {
            string r = ForestZones.CreateCameraZone();   // сам найдёт зону леса и создаст объект
            cz = Object.FindFirstObjectByType<ForestCameraZone>();
            if (cz == null) return "Камера леса: ForestCameraZone нет (" + r + ")";
        }
        Undo.RecordObject(cz, "Forest camera");
        // pitchAdd + = круче сверху: меньше видно кроны «в лоб», больше тропу. Дистанция чуть дальше обычного лесного 0.92
        cz.pitchAdd = 4f;
        cz.distanceMul = 1.0f;
        cz.lookUp = 0.2f;
        cz.blendTime = 1.2f;
        EditorUtility.SetDirty(cz);
        return "Камера леса: pitch +4°, дистанция ×1.0, blend 1.2 с. Радиус прозрачных преград в чаще расширяет ForestLook.";
    }

    // ------------------------------------------------------------------------------------------ старые лучи и пятна
    static string MuteOld()
    {
        int shaftsOff = 0, patchGroups = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "SunShafts" || t.GetComponentInParent<ForestAtmosphere>() == null) continue;
            if (!t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "Mute old shafts");
            t.gameObject.SetActive(false);
            shaftsOff++;
        }
        foreach (var atm in Object.FindObjectsByType<ForestAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (atm.groups == null) continue;
            Undo.RecordObject(atm, "Mute old patches");
            foreach (var g in atm.groups)
                if (g != null && g.name == "Пятна солнца") { g.day = Mathf.Min(g.day, 0.3f); patchGroups++; }
            EditorUtility.SetDirty(atm);
        }
        return "Старое: выключено объектов SunShafts: " + shaftsOff + ", приглушено групп «Пятна солнца»: " + patchGroups + ".";
    }

    // ------------------------------------------------------------------------------------------ предпросмотр
    static string Preview(bool on)
    {
        var look = Object.FindFirstObjectByType<ForestLook>();
        if (look == null) return "Сначала нажми «Применить всё».";
        look.EditorPreview(on);
        return on ? "Пятна света включены в Scene View. Это только предпросмотр: в игре скрипт сам ведёт cookie по входу в лес."
                  : "Предпросмотр выключен.";
    }

    // ------------------------------------------------------------------------------------------ исправить URP
    static string FixUrp()
    {
        var asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null) return "Активный URP Asset не найден (Project Settings → Graphics).";
        var so = new SerializedObject(asset);
        var sb = new StringBuilder("URP Asset «" + asset.name + "»:\n");

        sb.AppendLine(SetBool(so, "m_SupportsHDR", true, "HDR (нужен для мягкого bloom)"));
        sb.AppendLine(SetBool(so, "m_UseSRPBatcher", true, "SRP Batcher"));
        sb.AppendLine(SetBool(so, "m_SupportsLightCookies", true, "Light Cookies (пятна света)"));

        var rs = so.FindProperty("m_RenderScale");
        if (rs != null && rs.floatValue < 0.95f)
        {
            sb.AppendLine("  Render Scale был " + rs.floatValue.ToString("0.00") + " (картинка мылится), стал 1.00");
            rs.floatValue = 1f;
        }
        var ms = so.FindProperty("m_MSAA");
        if (ms != null && ms.intValue < 2)
        {
            sb.AppendLine("  MSAA был выключен, стал 4x (края деревьев и стволов без «лесенки»)");
            ms.intValue = 4;
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        sb.AppendLine("Если у тебя несколько URP-ассетов по уровням качества (Project Settings → Quality), проверь и остальные.");
        return sb.ToString();
    }

    static string SetBool(SerializedObject so, string field, bool value, string label)
    {
        var p = so.FindProperty(field);
        if (p == null) return "  " + label + ": поля нет в этой версии URP, проверь вручную";
        if (p.boolValue == value) return "  " + label + ": уже включено";
        p.boolValue = value;
        return "  " + label + ": включено";
    }

    // ------------------------------------------------------------------------------------------ отчёт
    string Audit()
    {
        var sb = new StringBuilder();
        var warns = new List<string>();

        // ---------------- сцена
        var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var mats = new HashSet<Material>();
        long tris = 0, forestTris = 0;
        int total = 0, casters = 0, transparent = 0, forestR = 0;
        GameObject treesGo = GameObject.Find("ForestTrees");
        Transform treesRoot = treesGo != null ? treesGo.transform : null;

        foreach (var r in rends)
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            total++;
            long t = 0;
            Mesh m = null;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) m = mf.sharedMesh;
            else { var smr = r as SkinnedMeshRenderer; if (smr != null) m = smr.sharedMesh; }
            if (m != null) for (int i = 0; i < m.subMeshCount; i++) t += (long)m.GetIndexCount(i) / 3;
            tris += t;
            if (r.shadowCastingMode != ShadowCastingMode.Off) casters++;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                mats.Add(mat);
                if (mat.renderQueue >= 3000) { transparent++; break; }
            }
            if (treesRoot != null && r.transform.IsChildOf(treesRoot)) { forestR++; forestTris += t; }
        }
        sb.AppendLine("СЦЕНА");
        sb.AppendLine("  Рендереров: " + total + ", треугольников: " + (tris / 1000) + "k, материалов: " + mats.Count + ", отбрасывают тень: " + casters + ", прозрачных: " + transparent);
        if (treesRoot != null) sb.AppendLine("  В ForestTrees: рендереров " + forestR + ", треугольников " + (forestTris / 1000) + "k");
        else sb.AppendLine("  Объекта ForestTrees нет: лес не создан или называется иначе");
        if (tris > 400000) warns.Add("Сцена тяжёлая для телефона (>400k треугольников всего). Если лес основная часть: меньше деревьев в чаще или LOD у кроны.");
        if (casters > 1500) warns.Add("Много теней от мелочи (" + casters + "). Выключи тени у травы, цветов, камней и мелкого декора (Cast Shadows = Off).");
        if (transparent > 80) warns.Add("Много прозрачных рендереров (" + transparent + "): это overdraw на телефоне. Объедини или убери лишнее.");

        // ---------------- свет
        int rt = 0, rtShadow = 0;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!l.enabled || l.lightmapBakeType == LightmapBakeType.Baked) continue;
            rt++;
            if (l.shadows != LightShadows.None && l.type != LightType.Directional) rtShadow++;
        }
        sb.AppendLine("СВЕТ");
        sb.AppendLine("  Динамических источников: " + rt + ", из них точечных с тенями: " + rtShadow);
        if (rt > 24) warns.Add("Много динамических огней (" + rt + "). FireLightPool уже ограничивает видимые, проверь, что пул включён.");
        if (rtShadow > 2) warns.Add("Тени у " + rtShadow + " точечных огней: дорого на телефоне. LightingRig отключает их на Низком, на Среднем лучше оставить максимум пару.");

        // ---------------- частицы
        int psCount = 0, psMax = 0;
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        { psCount++; psMax += ps.main.maxParticles; }
        sb.AppendLine("ЧАСТИЦЫ");
        sb.AppendLine("  Систем: " + psCount + ", суммарный максимум частиц: " + psMax);
        if (psMax > 3500) warns.Add("Суммарный максимум частиц " + psMax + ": для телефона много. Уменьши maxParticles у светлячков и пыли.");

        // ---------------- URP
        var urp = GraphicsSettings.currentRenderPipeline;
        sb.AppendLine("URP");
        if (urp == null) { sb.AppendLine("  Активного URP Asset нет!"); warns.Add("Не назначен URP Asset: постобработка, cookie и лучи работать не будут."); }
        else
        {
            var so = new SerializedObject(urp);
            sb.AppendLine("  Asset: " + urp.name);
            bool hdr = Bool(so, "m_SupportsHDR"), srp = Bool(so, "m_UseSRPBatcher"), cookies = Bool(so, "m_SupportsLightCookies", true);
            float scale = Flt(so, "m_RenderScale", 1f);
            int msaa = Int(so, "m_MSAA", 1);
            float shDist = Flt(so, "m_ShadowDistance", 0f);
            int casc = Int(so, "m_ShadowCascadeCount", 0);
            sb.AppendLine("  HDR: " + hdr + ", MSAA: " + msaa + "x, Render Scale: " + scale.ToString("0.00") + ", SRP Batcher: " + srp + ", Cookies: " + cookies);
            sb.AppendLine("  Тени: дальность " + shDist.ToString("0") + " м, каскадов " + casc);
            if (!hdr) warns.Add("HDR выключен: bloom будет резким и пересвеченным, пятна света плоскими. Кнопка «Исправить URP».");
            if (!srp) warns.Add("SRP Batcher выключен: сильно вырастет число draw calls. Кнопка «Исправить URP».");
            if (!cookies) warns.Add("Light Cookies выключены: пятна света не появятся. Кнопка «Исправить URP».");
            if (scale < 0.95f) warns.Add("Render Scale " + scale.ToString("0.00") + ": это главная причина «мыльной» картинки. Кнопка «Исправить URP» вернёт 1.0.");
            if (msaa < 2) warns.Add("MSAA выключен: края крон и стволов будут «лесенкой». Включи 4x (или 2x, если телефон слабый).");
            if (shDist > 80f) warns.Add("Дальность теней " + shDist.ToString("0") + " м: в лесу дальше ~50 м тени не видны, а стоят дорого. LightingRig на игре сам сокращает, но в ассете лучше 50-60.");
            if (casc > 2) warns.Add("Каскадов теней " + casc + ": для мобильной изометрии хватает 2.");
        }

        // ---------------- террейн
        var terrain = Terrain.activeTerrain;
        sb.AppendLine("ТЕРРЕЙН");
        if (terrain == null) sb.AppendLine("  Нет активного Terrain");
        else
        {
            sb.AppendLine("  Pixel Error: " + terrain.heightmapPixelError.ToString("0") + ", Base Map Dist: " + terrain.basemapDistance.ToString("0") +
                          ", Detail Dist: " + terrain.detailObjectDistance.ToString("0") + ", Detail Density: " + terrain.detailObjectDensity.ToString("0.0#") +
                          ", GPU Instancing: " + terrain.drawInstanced);
            if (terrain.heightmapPixelError < 5f) warns.Add("Terrain Pixel Error " + terrain.heightmapPixelError.ToString("0") + ": очень детально. 8-12 почти не заметно, но заметно быстрее.");
            if (terrain.detailObjectDistance > 70f) warns.Add("Detail Distance " + terrain.detailObjectDistance.ToString("0") + " м: трава дальше 50-60 м в изометрии не нужна, а стоит дорого.");
            if (!terrain.drawInstanced) warns.Add("У Terrain выключен GPU Instancing (Draw Instanced).");
        }

        // ---------------- камера
        sb.AppendLine("КАМЕРА");
        var cam = Camera.main;
        if (cam == null) { sb.AppendLine("  Нет Main Camera"); }
        else
        {
            var data = cam.GetUniversalAdditionalCameraData();
            var cf = cam.GetComponent<CameraFollow>();
            sb.AppendLine("  Near/Far: " + cam.nearClipPlane.ToString("0.##") + " / " + cam.farClipPlane.ToString("0") + ", FOV " + cam.fieldOfView.ToString("0") +
                          ", MSAA камеры: " + cam.allowMSAA + ", HDR камеры: " + cam.allowHDR + ", постобработка: " + data.renderPostProcessing + ", AA постэффект: " + data.antialiasing);
            if (!data.renderPostProcessing) warns.Add("На камере выключена постобработка (Rendering → Post Processing): Volume ничего не сделает.");
            if (!cam.allowHDR) warns.Add("На камере выключен HDR: bloom и тональная кривая будут хуже. Camera → Output → HDR.");
            if (cam.allowMSAA && data.antialiasing != AntialiasingMode.None) warns.Add("Одновременно MSAA и пост-AA (" + data.antialiasing + "): картинка мылится. Оставь что-то одно.");
            if (cam.farClipPlane > 130f) warns.Add("Far Plane " + cam.farClipPlane.ToString("0") + ": в лесу дальше ~80 м всё равно скрыто туманом и кронами. 100-110 экономит отрисовку.");
            if (cam.nearClipPlane < 0.1f) warns.Add("Near Plane " + cam.nearClipPlane.ToString("0.##") + ": со слишком малым near ухудшается точность глубины, возможны мерцающие тени и «зет-файтинг». 0.3 достаточно.");
            if (cf != null)
            {
                sb.AppendLine("  CameraFollow: distance " + cf.distance.ToString("0.#") + ", pitch " + cf.pitch.ToString("0") + "°, sway " + cf.swayAmount.ToString("0.##") +
                              "°, blockerRadius " + cf.blockerRadius.ToString("0.##") + ", hideBlockers " + cf.hideBlockers);
                if (!cf.hideBlockers) warns.Add("CameraFollow.hideBlockers выключен: кроны будут закрывать игрока.");
                if (cf.swayAmount > 0.4f) warns.Add("Дыхание камеры " + cf.swayAmount.ToString("0.##") + "°: заметно мелькают края крон. Для чёткости лучше ≤ 0.25.");
                if (cf.pitch < 40f) warns.Add("Pitch камеры " + cf.pitch.ToString("0") + "°: чем ниже угол, тем больше крон между камерой и игроком. 50-55° лучше всего для леса.");
            }
        }
        var cz = Object.FindFirstObjectByType<ForestCameraZone>();
        if (cz == null) warns.Add("ForestCameraZone нет в сцене: лесной грейд и сдвиг камеры не включатся. Tools → Forest → Лес → «Камера в лесу».");
        else if (cz.pitchAdd < 0f) warns.Add("ForestCameraZone.pitchAdd = " + cz.pitchAdd.ToString("0.#") + ": в лесу камера становится ниже, а кроны закрывают больше. «Применить всё» поставит +4.");

        var look = Object.FindFirstObjectByType<ForestLook>();
        if (look == null) warns.Add("Объекта ForestLook в сцене нет: нажми «Применить всё».");
        else
        {
            if (look.dappled && (look.cookieLevels == null || look.cookieLevels.Length == 0)) warns.Add("У ForestLook не назначены cookie-уровни.");
            if (look.shafts && look.shaftMaterial == null) warns.Add("У ForestLook не назначен материал лучей.");
            if (FindVolume("Forest Look") == null) warns.Add("Нет Volume «Forest Look»: цветокор леса не применится.");
        }

        sb.AppendLine();
        if (warns.Count == 0) sb.AppendLine("Замечаний нет.");
        else
        {
            sb.AppendLine("ЗАМЕЧАНИЯ (" + warns.Count + ")");
            for (int i = 0; i < warns.Count; i++) sb.AppendLine((i + 1) + ". " + warns[i]);
        }
        sb.AppendLine();
        sb.AppendLine("Цифры считаются по сцене в редакторе. Реальные кадры смотри на телефоне: Profiler или Unity Frame Debugger (число draw calls и SetPass).");
        Debug.Log("[ForestLook] Отчёт\n" + sb);
        return sb.ToString();
    }

    static bool Bool(SerializedObject so, string f, bool def = false) { var p = so.FindProperty(f); return p != null ? p.boolValue : def; }
    static float Flt(SerializedObject so, string f, float def) { var p = so.FindProperty(f); return p != null ? p.floatValue : def; }
    static int Int(SerializedObject so, string f, int def) { var p = so.FindProperty(f); return p != null ? p.intValue : def; }

    // ------------------------------------------------------------------------------------------ файлы
    static string ProjectPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        Ensure(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
#endif

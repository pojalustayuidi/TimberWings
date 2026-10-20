// Assets/_Project/Scripts/Forest/ForestLook.cs (НЕ в Editor)
// Атмосфера леса (задача 9). Создаётся и настраивается окном Tools → Forest → Свет и атмосфера, руками ничего вешать не нужно.
// Всё включается плавно по «весу леса» (0 на поляне, 1 в чаще). Вес берётся у Volume «Vibe Forest»: его уже ведёт ForestCameraZone.
// Если такого Volume нет или он выключен, вес считается по ForestZone под игроком.
//
// Что делает:
//  1. ПЯТНА СВЕТА: на главное солнце вешается cookie (проекция текстуры). Это даёт настоящие пятна света и тени от «листвы»
//     на траве, тропе, игроке и пропсах. Cookie нельзя «притушить» плавно, поэтому есть несколько готовых уровней контраста
//     (cookieLevels), скрипт переключает их по весу леса. Сам узор чуть дрожит (ветер в листве).
//  2. ТУМАН: RenderSettings.fog в лесу плотнее и зеленее/теплее. Базовые значения (LightingRig) запоминаются и возвращаются обратно.
//  3. ЛУЧИ: пул наклонных лучей по направлению солнца в просветах между деревьями вокруг игрока. Появляются, держатся и тают.
//  4. ПЫЛИНКИ: лёгкие искорки в воздухе вокруг игрока (одна ParticleSystem).
//  5. Volume «Forest Look» (тёплый грейд, мягкий bloom, виньетка): weight = вес леса.
//  6. КАМЕРА: в лесу радиус «прозрачных преград» у CameraFollow чуть шире, чтобы кроны не закрывали игрока и тропу.
// Качество берётся из GameSettings.LightQuality: Низкое = только пятна света и туман (без лучей и пылинок),
// Среднее = 60% лучей и пылинок, Высокое = всё. VibeSettings.ReduceEffects убирает дрожь узора и вдвое режет лучи.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]   // после ForestCameraZone (-101) и CameraFollow (-100): вес и камера уже посчитаны
public class ForestLook : MonoBehaviour
{
    [Header("Вес леса")]
    [Tooltip("Пусто = найдёт «Vibe Forest» сам. Его weight ведёт ForestCameraZone")]
    public Volume forestVolume;
    [Tooltip("Volume «Forest Look»: weight ведём мы")]
    public Volume lookVolume;
    [Tooltip("Секунд на полный переход (поверх сглаживания ForestCameraZone)")]
    public float blendTime = 0.8f;

    [Header("Пятна света (cookie у солнца)")]
    public bool dappled = true;
    [Tooltip("От слабого контраста к полному. Делает окно Tools → Forest → Свет и атмосфера")]
    public Texture2D[] cookieLevels = new Texture2D[0];
    [Tooltip("Сколько метров занимает один повтор узора")]
    public float cookieSize = 22f;
    [Tooltip("Дрожь узора, метры (ветер в листве). 0 = узор стоит")]
    public float shimmerAmount = 0.45f;
    public float shimmerSpeed = 0.4f;

    [Header("Туман")]
    public bool fog = true;
    public float fogDensityMul = 1.5f;
    public Color fogTint = new Color(0.74f, 0.80f, 0.60f);
    [Range(0f, 1f)] public float fogTintAmount = 0.5f;

    [Header("Лучи")]
    public bool shafts = true;
    public Material shaftMaterial;
    [Range(0, 16)] public int shaftCount = 9;
    public float shaftRadiusMin = 4f;
    public float shaftRadiusMax = 13f;
    public Vector2 shaftHeight = new Vector2(6f, 9f);
    public Vector2 shaftWidth = new Vector2(1.4f, 2.6f);
    public Vector2 shaftLife = new Vector2(7f, 13f);
    [Tooltip("Минимальная высота солнца (sin угла): ниже лучи не показываем")]
    public float shaftMinSunElevation = 0.25f;

    [Header("Пылинки")]
    public ParticleSystem motes;
    public float moteRate = 14f;

    [Header("Камера в лесу")]
    public bool cameraAssist = true;
    [Tooltip("Насколько шире «прозрачная труба» от камеры к игроку в чаще, метры")]
    public float blockerRadiusAdd = 0.55f;

    // ---------------------------------------------------------------- состояние
    class Shaft
    {
        public Transform t;
        public Renderer r;
        public bool active;
        public float age, life, nextTry, width, length;
    }

    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    Shaft[] pool;
    Mesh shaftMesh;
    MaterialPropertyBlock mpb;

    Transform player;
    float nextPlayerLook;
    CameraFollow cam;
    float nextCamLook;

    Light sun;
    UniversalAdditionalLightData sunData;
    float nextSunLook;
    int cookieLevel = -1;

    bool volLooked;
    float e;   // сглаженный вес леса

    bool fogHave;
    float fogBaseD, fogLastD;
    Color fogBaseC, fogLastC;

    float baseBlocker = -1f, addBlocker;

    // ---------------------------------------------------------------- жизненный цикл
    void OnEnable()
    {
        e = 0f;
        cookieLevel = -1;
        BuildPool();
    }

    void OnDisable()
    {
        ClearCookie();
        RestoreFog();
        RestoreCamera();
        if (lookVolume != null) lookVolume.weight = 0f;
        if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (pool != null)
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null && pool[i].r != null) { pool[i].r.enabled = false; pool[i].active = false; }
    }

    void OnDestroy()
    {
        if (shaftMesh != null) Destroy(shaftMesh);
    }

    void BuildPool()
    {
        if (pool != null || !shafts || shaftMaterial == null || shaftCount <= 0) return;
        shaftMesh = BuildShaftMesh();
        mpb = new MaterialPropertyBlock();
        pool = new Shaft[shaftCount];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Shaft_" + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = shaftMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = shaftMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            pool[i] = new Shaft { t = go.transform, r = r, nextTry = Time.time + Random.Range(0f, 1.5f) };
        }
    }

    // два скрещённых вертикальных квада: у основания уже, кверху шире
    static Mesh BuildShaftMesh()
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        for (int q = 0; q < 2; q++)
        {
            Vector3 ax = q == 0 ? Vector3.right : Vector3.forward;
            int b = v.Count;
            v.Add(-ax * 0.5f); uv.Add(new Vector2(0f, 0f));
            v.Add(ax * 0.5f); uv.Add(new Vector2(1f, 0f));
            v.Add(ax * 0.8f + Vector3.up); uv.Add(new Vector2(1f, 1f));
            v.Add(-ax * 0.8f + Vector3.up); uv.Add(new Vector2(0f, 1f));
            tri.Add(b); tri.Add(b + 2); tri.Add(b + 1);
            tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);
        }
        var m = new Mesh { name = "ForestLook_Shaft" };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        m.hideFlags = HideFlags.HideAndDontSave;
        return m;
    }

    // ---------------------------------------------------------------- главный цикл
    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt < 0.0001f) return;

        FindThings();

        float target = ReadWeight();
        e = Mathf.MoveTowards(e, target, dt / Mathf.Max(0.05f, blendTime));
        float k = e * e * (3f - 2f * e);

        float q = QualityFactor();
        float day = DayFactor();

        if (lookVolume != null) lookVolume.weight = k;

        ApplyCookie(k);
        ApplyFog(k);
        ApplyCamera(k);
        UpdateShafts(dt, k, day, q);
        UpdateMotes(k, day, q);
    }

    // ---------------------------------------------------------------- поиск объектов
    void FindThings()
    {
        float now = Time.unscaledTime;

        if (player == null && now >= nextPlayerLook)
        {
            nextPlayerLook = now + 1f;
            var p = PlayerMovement.Instance;
            if (p != null) player = p.transform;
        }
        if (cam == null && now >= nextCamLook)
        {
            nextCamLook = now + 1f;
            cam = FindFirstObjectByType<CameraFollow>();
        }
        if ((sun == null || !sun.isActiveAndEnabled) && now >= nextSunLook)
        {
            nextSunLook = now + 2f;
            Light s = RenderSettings.sun;
            if (s == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { s = l; break; }
            if (s != sun)
            {
                sun = s;
                sunData = sun != null ? sun.GetUniversalAdditionalLightData() : null;
                cookieLevel = -1;
            }
        }
        if (forestVolume == null && !volLooked)
        {
            volLooked = true;
            var go = GameObject.Find("Vibe Forest");
            if (go != null) forestVolume = go.GetComponent<Volume>();
        }
    }

    float ReadWeight()
    {
        if (forestVolume != null && forestVolume.gameObject.activeInHierarchy && forestVolume.enabled)
            return Mathf.Clamp01(forestVolume.weight);

        // запасной путь: по лесной зоне под игроком
        if (player == null || ForestZone.All.Count == 0) return 0f;
        ForestZone z = ForestZone.At(player.position);
        return z != null ? z.Weight(player.position) : 0f;
    }

    static float QualityFactor()
    {
        switch (GameSettings.LightQuality)
        {
            case LightQualityLevel.Low: return 0f;
            case LightQualityLevel.Medium: return 0.6f;
            default: return 1f;
        }
    }

    // 1 днём, к закату и ночью падает до 0 (как в ForestAtmosphere: по высоте солнца)
    float DayFactor()
    {
        if (sun == null) return 1f;
        float elev = -sun.transform.forward.y;
        return Mathf.Clamp01(Mathf.InverseLerp(0.02f, 0.45f, elev)) * Mathf.Clamp01(sun.intensity / 0.6f);
    }

    // ---------------------------------------------------------------- 1. пятна света
    void ApplyCookie(float k)
    {
        if (sun == null) return;
        int n = cookieLevels != null ? cookieLevels.Length : 0;
        if (!dappled || n == 0) { ClearCookie(); return; }

        // уровень 0 = без cookie, 1..n = готовые текстуры. Гистерезис, чтобы не мигало на границе уровней
        float want = k * n;
        int lvl = cookieLevel;
        if (lvl < 0 || Mathf.Abs(want - lvl) > 0.65f) lvl = Mathf.RoundToInt(want);
        lvl = Mathf.Clamp(lvl, 0, n);

        if (lvl != cookieLevel)
        {
            cookieLevel = lvl;
            sun.cookie = lvl == 0 ? null : cookieLevels[lvl - 1];
        }
        if (cookieLevel == 0 || sunData == null) return;

        Vector2 off = Vector2.zero;
        if (shimmerAmount > 0f && !VibeSettings.ReduceEffects)
        {
            float t = Time.time * shimmerSpeed;
            off.x = (Mathf.Sin(t * 0.83f) + 0.5f * Mathf.Sin(t * 2.1f + 1.3f)) * shimmerAmount * 0.66f;
            off.y = (Mathf.Cos(t * 0.67f) + 0.5f * Mathf.Sin(t * 1.9f + 0.4f)) * shimmerAmount * 0.66f;
        }
        sunData.lightCookieSize = new Vector2(cookieSize, cookieSize);
        sunData.lightCookieOffset = off;
    }

    void ClearCookie()
    {
        if (sun != null && cookieLevel != 0) sun.cookie = null;
        cookieLevel = -1;
    }

    // ---------------------------------------------------------------- 2. туман
    void ApplyFog(float k)
    {
        if (!fog) { RestoreFog(); return; }

        float curD = RenderSettings.fogDensity;
        Color curC = RenderSettings.fogColor;
        // кто-то другой (LightingRig) поменял туман: считаем это новой базой
        if (!fogHave || Mathf.Abs(curD - fogLastD) > 1e-6f || !Same(curC, fogLastC))
        {
            fogBaseD = curD; fogBaseC = curC; fogHave = true;
        }
        RenderSettings.fogDensity = fogBaseD * Mathf.Lerp(1f, fogDensityMul, k);
        RenderSettings.fogColor = Color.Lerp(fogBaseC, fogTint, fogTintAmount * k);
        fogLastD = RenderSettings.fogDensity;
        fogLastC = RenderSettings.fogColor;
    }

    void RestoreFog()
    {
        if (!fogHave) return;
        RenderSettings.fogDensity = fogBaseD;
        RenderSettings.fogColor = fogBaseC;
        fogHave = false;
    }

    static bool Same(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 1e-4f && Mathf.Abs(a.g - b.g) < 1e-4f && Mathf.Abs(a.b - b.b) < 1e-4f;
    }

    // ---------------------------------------------------------------- 6. камера
    void ApplyCamera(float k)
    {
        if (!cameraAssist || cam == null) { RestoreCamera(); return; }
        // кто-то поменял радиус (инспектор, другой скрипт): текущее значение становится базой
        if (baseBlocker < 0f || Mathf.Abs(cam.blockerRadius - (baseBlocker + addBlocker)) > 0.001f)
            baseBlocker = cam.blockerRadius;
        addBlocker = blockerRadiusAdd * k;
        cam.blockerRadius = baseBlocker + addBlocker;
    }

    void RestoreCamera()
    {
        if (cam != null && baseBlocker >= 0f) cam.blockerRadius = baseBlocker;
        baseBlocker = -1f;
        addBlocker = 0f;
    }

    // ---------------------------------------------------------------- 3. лучи
    void UpdateShafts(float dt, float k, float day, float q)
    {
        if (pool == null) { if (shafts && shaftMaterial != null) BuildPool(); return; }

        int want = shafts ? Mathf.RoundToInt(pool.Length * q * (VibeSettings.ReduceEffects ? 0.5f : 1f)) : 0;
        float master = k * day;

        Vector3 toSun = sun != null ? -sun.transform.forward : new Vector3(-0.35f, 0.8f, -0.25f).normalized;
        bool sunHigh = toSun.y >= shaftMinSunElevation;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, toSun);
        float lenK = 1f / Mathf.Max(0.3f, toSun.y);   // длина вдоль луча, чтобы по высоте получилось shaftHeight

        for (int i = 0; i < pool.Length; i++)
        {
            Shaft s = pool[i];
            bool allowed = i < want && master > 0.02f && sunHigh && player != null;

            if (!s.active)
            {
                if (allowed && Time.time >= s.nextTry) TrySpawn(s, toSun, rot, lenK);
                continue;
            }

            s.age += dt;
            if (!allowed || FlatDist(s.t.position, player != null ? player.position : s.t.position) > shaftRadiusMax + 3f)
                s.life = Mathf.Min(s.life, s.age + 1.8f);   // пора таять

            float fadeIn = Mathf.Clamp01(s.age / 1.8f);
            float fadeOut = Mathf.Clamp01((s.life - s.age) / 1.8f);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut)) * master;

            s.t.rotation = rot;   // солнце ползёт по небу: лучи следуют
            s.t.localScale = new Vector3(s.width, s.length * lenK, s.width);

            s.r.GetPropertyBlock(mpb);
            mpb.SetFloat(AlphaId, a);
            s.r.SetPropertyBlock(mpb);

            if (s.age >= s.life) { s.active = false; s.r.enabled = false; s.nextTry = Time.time + Random.Range(0.4f, 1.6f); }
        }
    }

    void TrySpawn(Shaft s, Vector3 toSun, Quaternion rot, float lenK)
    {
        s.nextTry = Time.time + Random.Range(0.5f, 1.5f);   // если не нашли место, следующая попытка не сразу

        for (int tries = 0; tries < 3; tries++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(shaftRadiusMin, shaftRadiusMax);
            Vector3 p = player.position + new Vector3(c.x, 0f, c.y);

            // только внутри леса
            if (ForestZone.All.Count > 0)
            {
                ForestZone z = ForestZone.At(p);
                if (z == null || z.Weight(p) < 0.35f) continue;
            }

            // земля под точкой: только террейн (не крыша и не мост)
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!(hit.collider is TerrainCollider)) continue;

            // просвет: на уровне пояса нет ни стволов, ни крон-триггеров, ни пропсов
            Vector3 g = hit.point;
            if (Physics.CheckSphere(g + Vector3.up * 1.6f, 0.9f, ~0, QueryTriggerInteraction.Collide)) continue;

            // не рядом с другим лучом
            bool near = false;
            for (int j = 0; j < pool.Length; j++)
                if (pool[j] != s && pool[j].active && FlatDist(pool[j].t.position, g) < 2.5f) { near = true; break; }
            if (near) continue;

            s.width = Random.Range(shaftWidth.x, shaftWidth.y);
            s.length = Random.Range(shaftHeight.x, shaftHeight.y);
            s.life = Random.Range(shaftLife.x, shaftLife.y);
            s.age = 0f;
            s.t.position = g + Vector3.down * 0.15f;
            s.t.rotation = rot;
            s.t.localScale = new Vector3(s.width, s.length * lenK, s.width);

            s.r.GetPropertyBlock(mpb);
            mpb.SetFloat(AlphaId, 0f);
            s.r.SetPropertyBlock(mpb);

            s.r.enabled = true;
            s.active = true;
            return;
        }
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ---------------------------------------------------------------- 4. пылинки
    void UpdateMotes(float k, float day, float q)
    {
        if (motes == null) return;
        float amount = k * day * q * (VibeSettings.ReduceEffects ? 0.5f : 1f);

        if (player != null) motes.transform.position = player.position + Vector3.up * 0.3f;

        if (amount < 0.03f)
        {
            if (motes.isPlaying) motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }
        if (!motes.isPlaying) motes.Play(true);
        var em = motes.emission;
        em.rateOverTime = moteRate * amount;
    }

#if UNITY_EDITOR
    // Предпросмотр узора в Scene View без Play. Перед сохранением сцены можно не выключать: в игре скрипт сам перепишет cookie.
    public void EditorPreview(bool on)
    {
        Light s = RenderSettings.sun;
        if (s == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { s = l; break; }
        if (s == null) { Debug.LogWarning("ForestLook: в сцене нет направленного света."); return; }

        if (!on || cookieLevels == null || cookieLevels.Length == 0) { s.cookie = null; return; }
        s.cookie = cookieLevels[cookieLevels.Length - 1];
        var d = s.GetUniversalAdditionalLightData();
        d.lightCookieSize = new Vector2(cookieSize, cookieSize);
        d.lightCookieOffset = Vector2.zero;
        UnityEditor.EditorUtility.SetDirty(s);
    }

    [ContextMenu("Предпросмотр пятен света: вкл")] void PreviewOn() { EditorPreview(true); }
    [ContextMenu("Предпросмотр пятен света: выкл")] void PreviewOff() { EditorPreview(false); }
#endif
}

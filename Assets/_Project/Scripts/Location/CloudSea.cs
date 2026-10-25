// Assets/_Project/Scripts/Location/CloudSea.cs (НЕ в Editor)
// Море облаков под парящим островом. Создаётся само после загрузки сцены с игроком и островом (как SkyDome), в сцену ничего вешать не нужно.
//
// Что рисует (всё через Graphics.DrawMesh только для главной камеры: миникарта и прочие камеры этого не видят):
//   • 2-3 слоя облаков (шейдер Project/CloudSea) под островом, чем ниже слой, тем темнее и медленнее он плывёт;
//   • объёмные клубы-«кучевые» вокруг и под островом, дрейфуют по ветру и растворяются у края поля;
//   • далёкие мини-острова с травой и ёлочками, медленно покачиваются в дымке.
// Цвета слоёв меняются по времени суток (DayNightCycle): белые днём, розовые на закате, синие ночью.
// Клубы и острова светятся обычным светом сцены (шейдер IslandRock), поэтому розовеют и темнеют сами.
//
// Качество (GameSettings.LightQuality): Низкое = 2 слоя, 10 клубов, 3 острова; Среднее = 3 / 22 / 6; Высокое = 3 / 36 / 8 и больше деталей.
// Выключить: CloudSea.Instance.enabledSea = false.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(210)]   // после камер и SkyDome (200)
public class CloudSea : MonoBehaviour
{
    public static CloudSea Instance { get; private set; }

    [Header("Общее")]
    public bool enabledSea = true;
    [Tooltip("Ветер, метры в секунду (x, z). Клубы плывут по нему, слои облаков тоже")]
    public Vector2 wind = new Vector2(0.9f, 0.4f);

    [Header("Слои облаков")]
    [Tooltip("Верхний слой: доля глубины острова ниже земли (0.7 = на уровне нижней трети скалы)")]
    [Range(0.2f, 1.2f)] public float topDepthFactor = 0.95f;   // ниже, чем раньше (0.7): над облаками виден весь обрыв, а не белая плоскость у кромки
    public float layerStep = 6f;
    public float cloudScale = 0.03f;
    [Range(0f, 1f)] public float cover = 0.42f;
    [Tooltip("Цвет верха облаков днём. Не чисто белый: чуть голубой, чтобы на облаках читались объём и тени")]
    public Color dayTop = new Color(0.90f, 0.95f, 1.00f);
    [Tooltip("Цвет теней облаков днём: глубже и синее, облака получают форму")]
    public Color dayShade = new Color(0.46f, 0.60f, 0.86f);
    [Tooltip("Насколько нижние слои темнее верхних (0 = одинаковые)")]
    [Range(0f, 1f)] public float layerDarken = 0.55f;

    [Header("Клубы и мини-острова")]
    public float puffFieldRadius = 140f;
    public Vector2 puffSize = new Vector2(3.5f, 7f);
    public Vector2 isletRing = new Vector2(95f, 230f);

    [Header("Масштаб под остров (остров v2 крупнее старого)")]
    [Tooltip("Подгонять поле клубов, их размер и кольцо мини-островов под радиус острова. Числа выше считаются минимумом")]
    public bool scaleWithIsland = true;
    [Tooltip("Радиус острова, под который подобраны размеры клубов (старый остров). Радиус острова v2 около 80: клубы станут до 1,8 раза крупнее")]
    public float refIslandRadius = 45f;
    [Tooltip("Доля клубов, прижатых к обрыву: облачный пояс у основания скалы, как на референсе. Они не плывут по ветру, а покачиваются на месте")]
    [Range(0f, 0.8f)] public float bankShare = 0.3f;

    struct Puff { public Vector3 pos; public float scale, rot, speed; public int mesh; public bool bank; }
    struct Islet { public Vector3 pos; public float scale, yaw, yawSpeed, phase; public int mesh; }

    IslandSource island;
    Vector3 center;
    float groundY, islandDepth, islandRadius;
    float effField, effPuffMul = 1f;     // поле клубов и множитель их размера с учётом размера острова
    Vector2 effIsletRing;

    Mesh quad;
    Mesh[] puffMeshes, isletMeshes;
    Material[] layerMats;
    Material puffMat, isletMat;
    Shader cloudShader, rockShader;

    Puff[] puffs = new Puff[0];
    Islet[] islets = new Islet[0];
    int layerCount, builtQuality = -1;
    float detail;

    Camera cam;
    float camAt, qualityAt;
    System.Random rnd;

    static readonly int TopId = Shader.PropertyToID("_Top");
    static readonly int ShadeId = Shader.PropertyToID("_Shade");
    static readonly int ScaleId = Shader.PropertyToID("_Scale");
    static readonly int CoverId = Shader.PropertyToID("_Cover");
    static readonly int WindId = Shader.PropertyToID("_Wind");
    static readonly int FadeId = Shader.PropertyToID("_Fade");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Init();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { Init(); }

    static void Init()
    {
        if (PlayerMovement.Instance == null) return;                       // меню и пустые сцены не трогаем
        if (IslandSource.Find() == null) return; // моря нет, пока остров не парящий
        if (Object.FindFirstObjectByType<CloudSea>() != null) return;
        new GameObject("_CloudSea").AddComponent<CloudSea>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        island = IslandSource.Find();
        if (island == null) { enabledSea = false; return; }

        // размеры острова
        var pts = island.Outline;
        Vector2 c = Vector2.zero;
        for (int i = 0; i < pts.Count; i++) c += pts[i];
        if (pts.Count > 0) c /= pts.Count;
        groundY = island.GroundY;
        islandDepth = Mathf.Max(8f, island.depth);
        center = new Vector3(c.x, groundY, c.y);
        islandRadius = 10f;
        for (int i = 0; i < pts.Count; i++) islandRadius = Mathf.Max(islandRadius, Vector2.Distance(pts[i], c));

        // поле клубов, размеры и кольцо мини-островов растут вместе с островом (у острова v2 радиус около 80 м, а числа в полях подобраны под старый)
        effField = puffFieldRadius; effPuffMul = 1f; effIsletRing = isletRing;
        if (scaleWithIsland)
        {
            effField = Mathf.Max(puffFieldRadius, islandRadius * 1.9f);
            effPuffMul = Mathf.Clamp(islandRadius / Mathf.Max(1f, refIslandRadius), 1f, 1.8f);
            effIsletRing = new Vector2(Mathf.Max(isletRing.x, islandRadius * 1.3f), Mathf.Max(isletRing.y, islandRadius * 2.8f));
        }

        cloudShader = island.cloudShader != null ? island.cloudShader : Shader.Find("Project/CloudSea");
        rockShader = island.rockMaterial != null ? island.rockMaterial.shader : Shader.Find("Project/IslandRock");
        if (cloudShader == null) Debug.LogWarning("[CloudSea] Шейдер Project/CloudSea не найден: слои облаков выключены (нажми «Собрать остров», чтобы ссылка проставилась).");
        if (rockShader == null) Debug.LogWarning("[CloudSea] Шейдер Project/IslandRock не найден: клубы и мини-острова выключены.");

        rnd = new System.Random(island.seed * 13 + 5);
        BuildMeshes();
        Rebuild();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (layerMats != null) foreach (var m in layerMats) if (m != null) Destroy(m);
        if (puffMat != null) Destroy(puffMat);
        if (isletMat != null) Destroy(isletMat);
        if (quad != null) Destroy(quad);
        if (puffMeshes != null) foreach (var m in puffMeshes) if (m != null) Destroy(m);
        if (isletMeshes != null) foreach (var m in isletMeshes) if (m != null) Destroy(m);
    }

    // ---------- сборка по качеству ----------
    void Rebuild()
    {
        builtQuality = (int)GameSettings.LightQuality;
        int puffN, isletN;
        switch (GameSettings.LightQuality)
        {
            case LightQualityLevel.Low: layerCount = 2; puffN = 10; isletN = 3; detail = 0f; break;
            case LightQualityLevel.High: layerCount = 3; puffN = 36; isletN = 8; detail = 1f; break;
            default: layerCount = 3; puffN = 22; isletN = 6; detail = 0.5f; break;
        }

        if (layerMats != null) foreach (var m in layerMats) if (m != null) Destroy(m);
        layerMats = new Material[layerCount];
        if (cloudShader != null)
            for (int l = 0; l < layerCount; l++)
                layerMats[l] = new Material(cloudShader) { name = "CloudLayer" + l + " (runtime)", hideFlags = HideFlags.DontSave };

        if (rockShader != null)
        {
            if (puffMat == null)
            {
                puffMat = new Material(rockShader) { name = "CloudPuff (runtime)", hideFlags = HideFlags.DontSave };
                puffMat.SetColor("_Tint", Color.white);
                puffMat.SetColor("_Shade", new Color(0.62f, 0.68f, 0.88f));
                puffMat.SetFloat("_Wrap", 0.6f);
                puffMat.SetFloat("_Rim", 0.35f);
                puffMat.SetFloat("_GlowIntensity", 0f);
            }
            if (isletMat == null)
            {
                isletMat = new Material(rockShader) { name = "FarIslet (runtime)", hideFlags = HideFlags.DontSave };
                isletMat.SetColor("_Shade", new Color(0.50f, 0.46f, 0.62f));
                isletMat.SetFloat("_GlowIntensity", 0f);
            }
        }

        if (scaleWithIsland) puffN = Mathf.RoundToInt(puffN * Mathf.Clamp(effField / Mathf.Max(1f, puffFieldRadius), 1f, 1.3f));
        puffs = new Puff[rockShader != null ? puffN : 0];
        for (int i = 0; i < puffs.Length; i++) puffs[i] = NewPuff(true);

        islets = new Islet[rockShader != null ? isletN : 0];
        for (int i = 0; i < islets.Length; i++)
        {
            float ang = (i + (float)rnd.NextDouble() * 0.8f) / islets.Length * Mathf.PI * 2f;   // равномерно по кругу, чтобы не слиплись
            float dist = Mathf.Lerp(effIsletRing.x, effIsletRing.y, (float)rnd.NextDouble());
            float topY = groundY - islandDepth * topDepthFactor;
            islets[i] = new Islet
            {
                pos = new Vector3(center.x + Mathf.Cos(ang) * dist, Mathf.Lerp(topY + 6f, groundY + 6f, (float)rnd.NextDouble()), center.z + Mathf.Sin(ang) * dist),
                scale = 6f + (float)rnd.NextDouble() * 12f,
                yaw = (float)rnd.NextDouble() * 360f,
                yawSpeed = ((float)rnd.NextDouble() - 0.5f) * 1.2f,
                phase = (float)rnd.NextDouble() * 6.28f,
                mesh = rnd.Next(isletMeshes.Length)
            };
        }
    }

    Puff NewPuff(bool initial)
    {
        float R = effField;
        float topY = groundY - islandDepth * topDepthFactor;
        Vector3 p;
        bool bank = false;
        if (initial)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            bank = (float)rnd.NextDouble() < bankShare;
            // пояс у обрыва: чуть дальше контура; остальные клубы по всему полю
            float dist = bank ? islandRadius * Mathf.Lerp(0.9f, 1.45f, (float)rnd.NextDouble()) : R * Mathf.Sqrt((float)rnd.NextDouble());
            p = new Vector3(center.x + Mathf.Cos(ang) * dist, 0f, center.z + Mathf.Sin(ang) * dist);
        }
        else
        {
            // новый клуб появляется с наветренного края
            Vector2 w = wind.sqrMagnitude > 1e-4f ? wind.normalized : Vector2.right;
            Vector2 perp = new Vector2(-w.y, w.x);
            float side = ((float)rnd.NextDouble() - 0.5f) * R * 1.6f;
            Vector2 o = new Vector2(center.x, center.z) - w * R * 0.97f + perp * side;
            p = new Vector3(o.x, 0f, o.y);
        }
        if (bank)
        {
            // пояс держится на высоте нижней половины скалы: основание обрыва тонет в облаках
            float r = (float)rnd.NextDouble();
            p.y = Mathf.Lerp(topY, groundY - islandDepth * 0.45f, r * r);
        }
        else p.y = topY + ((float)rnd.NextDouble() - 0.45f) * 6f;
        float sc = Mathf.Lerp(puffSize.x, puffSize.y, (float)rnd.NextDouble()) * effPuffMul * (bank ? 1.25f : 1f);
        return new Puff
        {
            pos = p,
            scale = sc,
            rot = (float)rnd.NextDouble() * 360f,
            speed = 0.6f + (float)rnd.NextDouble() * 0.9f,
            mesh = rnd.Next(puffMeshes.Length),
            bank = bank
        };
    }

    // ---------- кадр ----------
    void LateUpdate()
    {
        if (!enabledSea || island == null) return;

        if (cam == null || !cam.isActiveAndEnabled || Time.unscaledTime >= camAt)
        {
            camAt = Time.unscaledTime + 1f;
            cam = Camera.main;
        }
        if (cam == null) return;

        if (Time.unscaledTime >= qualityAt)
        {
            qualityAt = Time.unscaledTime + 1f;
            if (builtQuality != (int)GameSettings.LightQuality) Rebuild();
        }

        // Остров v2, этап 3: цвета и покрытие фона из ассета стиля (нет стиля = прежние значения полей)
        var style = CloudCandyStyle.Current;
        if (style != null) style.ApplyTo(this);

        float dt = Time.deltaTime;
        DrawLayers();
        DrawPuffs(dt);
        DrawIslets();
    }

    void DayColors(out Color top, out Color shade)
    {
        float t = DayNightCycle.Time01;
        float night = DayNightCycle.Night01;
        float tw = DayNightCycle.Twilight01;
        float day = Mathf.Clamp01(1f - night - tw);
        float dusk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.6f, t));   // 0 утро ... 1 вечер

        Color topTw = Color.Lerp(new Color(1.00f, 0.80f, 0.76f), new Color(1.00f, 0.62f, 0.50f), dusk);
        Color shTw = Color.Lerp(new Color(0.66f, 0.55f, 0.74f), new Color(0.62f, 0.40f, 0.58f), dusk);
        top = DayNightCycle.Mix(dayTop, topTw, new Color(0.34f, 0.40f, 0.62f), day, tw, night);
        shade = DayNightCycle.Mix(dayShade, shTw, new Color(0.10f, 0.13f, 0.28f), day, tw, night);
        top.a = 1f; shade.a = 1f;
    }

    void DrawLayers()
    {
        if (cloudShader == null || layerMats == null) return;
        if (quad == null) return;

        Color top, shade;
        DayColors(out top, out shade);

        Vector3 toSun;
        var dn = DayNightCycle.Instance;
        if (dn == null || !GetSun(dn, out toSun))
        {
            var sun = RenderSettings.sun;
            toSun = sun != null ? -sun.transform.forward : new Vector3(0.3f, 0.8f, 0.3f).normalized;
        }
        Vector2 sunXZ = new Vector2(toSun.x, toSun.z);
        sunXZ = sunXZ.sqrMagnitude > 1e-4f ? sunXZ.normalized : new Vector2(0.6f, 0.4f);

        float fadeR = Mathf.Clamp(cam.farClipPlane * 0.8f, 80f, 450f);
        float topY = groundY - islandDepth * topDepthFactor;
        float time = Time.time;
        Vector3 cp = cam.transform.position;

        for (int l = 0; l < layerCount; l++)
        {
            var mat = layerMats[l];
            if (mat == null) continue;

            float k = l / (float)Mathf.Max(1, layerCount - 1);          // 0 верхний, 1 нижний слой
            float scale = cloudScale * (1f + 0.3f * l);
            float speedMul = 1f - 0.2f * l;
            Vector2 off = -wind * (time * scale * speedMul);
            off.x = Mathf.Repeat(off.x, 512f); off.y = Mathf.Repeat(off.y, 512f);

            mat.SetColor(TopId, Color.Lerp(top, shade, k * layerDarken));
            mat.SetColor(ShadeId, Color.Lerp(shade, shade * 0.55f, k));
            mat.SetFloat(ScaleId, scale);
            mat.SetFloat(CoverId, Mathf.Clamp01(cover + 0.04f * l));
            mat.SetVector(WindId, new Vector4(off.x, off.y, sunXZ.x, sunXZ.y));
            mat.SetVector(FadeId, new Vector4(cp.x, cp.z, fadeR, detail));

            var m = Matrix4x4.TRS(new Vector3(cp.x, topY - l * layerStep, cp.z), Quaternion.identity, new Vector3(fadeR * 2.3f, 1f, fadeR * 2.3f));
            Graphics.DrawMesh(quad, m, mat, 0, cam, 0, null, ShadowCastingMode.Off, false);
        }
    }

    static bool GetSun(DayNightCycle dn, out Vector3 toSun)
    {
        Vector3 toMoon;
        return dn.GetSkyDirections(out toSun, out toMoon);
    }

    void DrawPuffs(float dt)
    {
        if (puffMat == null || puffs.Length == 0) return;

        float R = effField;
        Vector3 w3 = new Vector3(wind.x, 0f, wind.y);
        for (int i = 0; i < puffs.Length; i++)
        {
            var p = puffs[i];
            if (!p.bank) p.pos += w3 * (p.speed * dt);   // пояс у обрыва стоит на месте и только покачивается
            float dist = Vector2.Distance(new Vector2(p.pos.x, p.pos.z), new Vector2(center.x, center.z));
            if (dist > R) { puffs[i] = NewPuff(false); continue; }
            puffs[i] = p;

            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(R, R * 0.8f, dist));   // у края растут / тают, чтобы не появляться рывком
            float s = p.scale * edge;
            if (s < 0.05f) continue;
            Vector3 dp = p.pos;
            if (p.bank) dp.y += Mathf.Sin(Time.time * 0.35f + p.rot) * 0.6f;
            var m = Matrix4x4.TRS(dp, Quaternion.Euler(0f, p.rot, 0f), new Vector3(s, s, s));
            Graphics.DrawMesh(puffMeshes[p.mesh], m, puffMat, 0, cam, 0, null, ShadowCastingMode.Off, false);
        }
    }

    void DrawIslets()
    {
        if (isletMat == null || islets.Length == 0) return;
        float t = Time.time;
        for (int i = 0; i < islets.Length; i++)
        {
            var s = islets[i];
            float bob = Mathf.Sin(t * 0.35f + s.phase) * 0.7f;
            float yaw = s.yaw + s.yawSpeed * t;
            var m = Matrix4x4.TRS(s.pos + Vector3.up * bob, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s.scale);
            Graphics.DrawMesh(isletMeshes[s.mesh], m, isletMat, 0, cam, 0, null, ShadowCastingMode.Off, false);
        }
    }

    // ---------- меши ----------
    void BuildMeshes()
    {
        quad = new Mesh { name = "CloudQuad" };
        quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));   // единичная плоскость: размер задаёт матрица

        puffMeshes = new Mesh[3];
        for (int i = 0; i < puffMeshes.Length; i++) puffMeshes[i] = BuildPuff(1000 + i * 37);

        isletMeshes = new Mesh[3];
        for (int i = 0; i < isletMeshes.Length; i++) isletMeshes[i] = BuildIslet(2000 + i * 53);
    }

    static readonly int[] IcoFaces =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    // Гладкая сфера: икосаэдр, поделённый sub раз
    static void Icosphere(int sub, out List<Vector3> v, out List<int> tris)
    {
        float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
        v = new List<Vector3>
        {
            new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
            new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
            new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1)
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        tris = new List<int>(IcoFaces);

        for (int s = 0; s < sub; s++)
        {
            var cache = new Dictionary<long, int>();
            var nt = new List<int>(tris.Count * 4);
            for (int f = 0; f < tris.Count; f += 3)
            {
                int a = tris[f], b = tris[f + 1], c = tris[f + 2];
                int ab = Mid(v, cache, a, b), bc = Mid(v, cache, b, c), ca = Mid(v, cache, c, a);
                nt.Add(a); nt.Add(ab); nt.Add(ca);
                nt.Add(b); nt.Add(bc); nt.Add(ab);
                nt.Add(c); nt.Add(ca); nt.Add(bc);
                nt.Add(ab); nt.Add(bc); nt.Add(ca);
            }
            tris = nt;
        }
    }

    static int Mid(List<Vector3> v, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        int idx;
        if (cache.TryGetValue(key, out idx)) return idx;
        v.Add(((v[a] + v[b]) * 0.5f).normalized);
        idx = v.Count - 1;
        cache[key] = idx;
        return idx;
    }

    // Кучевое облако: несколько приплюснутых шаров с плоским низом, гладкие нормали
    Mesh BuildPuff(int seed)
    {
        var r = new System.Random(seed);
        List<Vector3> unit; List<int> ut;
        Icosphere(2, out unit, out ut);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        int balls = 5 + r.Next(2);
        for (int k = 0; k < balls; k++)
        {
            float rad = k == 0 ? 1f : 0.55f + (float)r.NextDouble() * 0.35f;
            Vector3 off = k == 0 ? Vector3.zero : new Vector3(((float)r.NextDouble() - 0.5f) * 2.4f, 0f, ((float)r.NextDouble() - 0.5f) * 1.5f);
            off.y = k == 0 ? 0.2f : 0.05f + rad * 0.25f;

            int b0 = verts.Count;
            for (int i = 0; i < unit.Count; i++)
            {
                Vector3 u = unit[i];
                Vector3 p = u * rad;
                p.y = Mathf.Max(p.y * 0.72f, -0.15f * rad);
                verts.Add(off + p);
                norms.Add(u);
                float h = Mathf.Lerp(0.88f, 1f, (u.y + 1f) * 0.5f);
                cols.Add(new Color(h, h, h, 1f));
            }
            for (int i = 0; i < ut.Count; i++) tris.Add(b0 + ut[i]);
        }

        var mesh = new Mesh { name = "CloudPuff" };
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Мини-остров единичного радиуса: трава сверху, рваная скала книзу, пара ёлочек
    Mesh BuildIslet(int seed)
    {
        var r = new System.Random(seed);
        System.Func<float> R01 = () => (float)r.NextDouble();

        const int SEG = 12;
        float[] rad = { 1.00f, 1.05f, 0.92f, 0.62f, 0.32f, 0.0f };
        float[] yy = { 0f, -0.12f, -0.34f, -0.85f, -1.45f, -2.1f };
        Color[] rc = { island.grassDark, island.grassDark, island.soil, island.rockWarm, island.rockMid, island.rockDark };
        int R = rad.Length;

        var ring = new Vector3[R, SEG];
        var col = new Color[R, SEG];
        for (int i = 0; i < R; i++)
            for (int j = 0; j < SEG; j++)
            {
                float a = j * Mathf.PI * 2f / SEG;
                float jr = i == R - 1 ? 0f : 0.82f + R01() * 0.36f;
                float jy = i >= 2 && i < R - 1 ? (R01() - 0.5f) * 0.25f : 0f;
                ring[i, j] = new Vector3(Mathf.Cos(a) * rad[i] * jr, yy[i] + jy, Mathf.Sin(a) * rad[i] * jr);
                Color c = rc[i] * (0.92f + R01() * 0.16f); c.a = 1f;
                col[i, j] = c;
            }

        var v = new List<Vector3>(); var cl = new List<Color>(); var tr = new List<int>();
        Vector3 inside = new Vector3(0f, -0.8f, 0f);
        Vector3 topC = new Vector3(0f, 0.02f, 0f);
        for (int j = 0; j < SEG; j++)
        {
            int j2 = (j + 1) % SEG;
            IslandUnderside.Face(v, cl, tr, topC, ring[0, j2], ring[0, j], island.grassTop, island.grassTop, island.grassTop, inside);
            for (int i = 0; i < R - 1; i++)
            {
                IslandUnderside.Face(v, cl, tr, ring[i, j], ring[i, j2], ring[i + 1, j2], col[i, j], col[i, j2], col[i + 1, j2], inside);
                if (i < R - 2)
                    IslandUnderside.Face(v, cl, tr, ring[i, j], ring[i + 1, j2], ring[i + 1, j], col[i, j], col[i + 1, j2], col[i + 1, j], inside);
            }
        }

        // ёлочки: два конуса друг на друге
        int trees = 2 + r.Next(3);
        Color g1 = new Color(0.20f, 0.42f, 0.24f), g2 = new Color(0.26f, 0.52f, 0.28f);
        for (int t = 0; t < trees; t++)
        {
            float a = R01() * Mathf.PI * 2f, d = R01() * 0.6f;
            Vector3 bp = new Vector3(Mathf.Cos(a) * d, 0.01f, Mathf.Sin(a) * d);
            float h = 0.28f + R01() * 0.25f, br = 0.11f + R01() * 0.05f;
            Cone(v, cl, tr, bp, br, h * 0.62f, g1);
            Cone(v, cl, tr, bp + Vector3.up * h * 0.38f, br * 0.68f, h * 0.62f, g2);
        }

        return IslandUnderside.ToMesh("FarIslet", v, cl, tr);
    }

    static void Cone(List<Vector3> v, List<Color> c, List<int> t, Vector3 basePos, float r, float h, Color col)
    {
        const int S = 6;
        Vector3 tip = basePos + Vector3.up * h;
        Vector3 inside = basePos + Vector3.up * h * 0.3f;
        Color tipCol = col * 1.12f; tipCol.a = 1f;
        for (int k = 0; k < S; k++)
        {
            float a0 = k * Mathf.PI * 2f / S, a1 = (k + 1) * Mathf.PI * 2f / S;
            Vector3 p0 = basePos + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
            Vector3 p1 = basePos + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
            IslandUnderside.Face(v, c, t, p0, p1, tip, col, col, tipCol, inside);
        }
    }
}

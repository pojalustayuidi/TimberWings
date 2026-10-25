// Assets/_Project/Scripts/UI/Map/HudMinimap.Bake.cs (НЕ в Editor)
// Снимок карты: стилизованная земля, границы по острову, перепечатка по событиям.
//
// Как рисуется (URP): SetReplacementShader в URP не работает, поэтому прямо перед camera.Render() каждому рендереру
// на кадр подменяются материалы на плоские (по слою: вода, трава, песок, лес, камни, мост, здания), а сразу после
// Render() возвращаются обратно. Всё в одном вызове одного кадра, игрок ничего не видит. Лишнее (частицы, свет, персонажи,
// мелочь) на снимок не попадает. Потом лёгкий проход MinimapPost добавляет мягкую обводку (если шейдер доступен).
//
// Перепечатка: события стройки, мост (FloatingIsland.GateOpened), смена фазы суток, загрузка. Не чаще, чем раз в rebakeInterval (2 с).
// Фриза нет: классификация рендереров идёт кусочками по sliceMs мс на кадр, рендер мини-карты один за кадр, а большая карта
// (mapResolution) печатается отдельным кадром и только когда она открыта (или один раз заранее после старта).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum MinimapLayer { Hide = 0, Water, Grass, Sand, Forest, Rock, Wood, Building, Prop, Count }

[Serializable]
public class MapPalette
{
    public Color water = new Color32(0x3B, 0x86, 0xA8, 255);
    public Color waterDeep = new Color32(0x2A, 0x66, 0x88, 255);
    public Color foam = new Color32(0xD8, 0xEE, 0xF2, 255);
    public Color grass = new Color32(0x86, 0xB8, 0x5E, 255);
    public Color sand = new Color32(0xE3, 0xCE, 0x93, 255);
    public Color forestA = new Color32(0x2E, 0x6B, 0x41, 255);
    public Color forestB = new Color32(0x3E, 0x80, 0x4B, 255);
    public Color rock = new Color32(0x8D, 0x95, 0x9E, 255);
    public Color wood = new Color32(0xB9, 0x85, 0x4D, 255);
    public Color roof = new Color32(0xC8, 0x55, 0x3D, 255);
    [Tooltip("Крыши и стены ночью чуть теплеют (окна горят)")]
    public Color roofLit = new Color32(0xFF, 0xC8, 0x70, 255);
    public Color prop = new Color32(0x9A, 0x86, 0x6A, 255);
}

// Ключевые слова для разбора объектов сцены по именам (имя объекта и его родителей). Регистр не важен.
// Слово считается найденным в начале имени, после «_», пробела, цифры или на стыке «слеваCamelCase».
[Serializable]
public class MapKeywords
{
    public string[] hide = { "shadow", "shaft", "glow", "highlight", "light", "lamp", "fx", "particle", "indicator", "arrow", "marker", "tag", "trigger", "collider", "sky", "cloud", "fog", "mote", "butterfly", "flies", "wind", "rain", "snow", "decal", "outline",
        "barrier", "blocker", "boundary", "mask", "ring", "lily", "sparkle", "patch", "pebble", "plant", "sign" };
    public string[] bridge = { "bridge", "мост" };
    public string[] water = { "water", "river", "sea", "ocean", "lake", "pond", "вода", "река", "море", "озеро" };
    public string[] building = { "hut", "tent", "market", "shop", "stall", "house", "sawmill", "workshop", "campfire", "fire", "barn", "cabin", "хиж", "палат", "рынок", "лавк", "костер", "костёр" };
    public string[] forest = { "tree", "pine", "spruce", "cedar", "birch", "fir", "bush", "shrub", "forest", "дерев", "ель", "сосн", "берез" };
    public string[] rock = { "rock", "stone", "boulder", "cliff", "камен", "скал", "валун" };
    public string[] sand = { "sand", "path", "road", "dirt", "beach", "track", "paving", "песок", "тропа", "дорож" };
    public string[] ground = { "ground", "terrain", "grass", "island", "land", "meadow", "земл", "трав" };
}

public partial class HudMinimap
{
    [Header("Стилизация карты")]
    [Tooltip("Плоская «нарисованная» карта: слои по палитре вместо снимка реальных материалов. Выключи, чтобы вернуть старый вид")]
    public bool stylized = true;
    public MapPalette palette = new MapPalette();
    public MapKeywords keywords = new MapKeywords();
    [Tooltip("Hidden/BuilderSim/MinimapPost. Пусто: ищется по имени (в редакторе всегда находится, в сборке нужен Always Included Shaders)")]
    public Shader postShader;
    [Tooltip("Мягкая обводка границ слоёв: берег, кромка леса, силуэты зданий")]
    public bool outlines = true;
    [Range(0f, 1f)] public float outlineStrength = 0.5f;

    [Header("Перепечатка карты")]
    [Tooltip("Печатать карту заново после стройки, моста, смены времени суток, загрузки")]
    public bool autoRebake = true;
    [Tooltip("Не чаще, чем раз в столько секунд")]
    public float rebakeInterval = 2f;
    [Tooltip("Пауза после события перед печатью: объекты успевают сменить вид")]
    public float rebakeDelay = 0.4f;
    public bool rebakeOnTimeOfDay = true;
    [Tooltip("Размер снимка мини-карты (большая карта: Map Resolution выше). Мини-карта показывает лишь часть снимка, поэтому ей нужно 512-768")]
    public int miniResolution = 640;
    [Range(1, 4)] public int bakeMsaa = 2;
    [Tooltip("Сколько мс за кадр тратить на разбор объектов сцены (без фриза)")]
    public float sliceMs = 3f;

    [Header("Границы мира")]
    [Tooltip("Отступ от острова (контур FloatingIsland), доля размера")]
    [Range(0f, 0.3f)] public float islandPadding = 0.05f;
    public float minPadding = 3f;

    // ------------------------------------------------------------ состояние снимка
    RenderTexture rtMini, rtBig;
    Camera bakeCam;
    Material[] flatMats;
    Material postMat;
    Shader flatShader;
    const int Variants = 4;
    bool mapReady, baking, rebakePending, bigDirty, bigPrefetched, boundsFixed;
    float rebakeAt, lastBake = -99f;
    float topY, botY;
    bool hasLand;
    Bounds landBounds;
    Vector2[] islandPoly;                 // контур острова (FloatingIsland): под него рисуется трава, даже если земля сцены не попала в кадр
    Mesh islandMesh;
    float islandY;
    [Tooltip("Один раз выписать в Console, как разобраны объекты сцены (слои, самые большие объекты). Нужно для настройки Keywords")]
    public bool logBake = false;

    readonly Dictionary<Transform, MinimapLayer> nameCache = new Dictionary<Transform, MinimapLayer>();
    readonly Dictionary<Renderer, MinimapLayer> rendCache = new Dictionary<Renderer, MinimapLayer>();
    readonly HashSet<Transform> hideRoots = new HashSet<Transform>();
    readonly HashSet<Transform> buildingRoots = new HashSet<Transform>();
    readonly HashSet<Transform> treeRoots = new HashSet<Transform>();
    readonly HashSet<Transform> woodRoots = new HashSet<Transform>();
    readonly List<Material> tmpMats = new List<Material>(8);
    int bakeCount;

    class RRec { public Renderer r; public Material[] saved; public bool wasOff, swapped; }
    RRec[] recs = new RRec[256];
    int recCount;

    Color WaterDeepColor { get { return stylized ? palette.waterDeep : background; } }

    // ------------------------------------------------------------ запуск
    void InitBake()
    {
        EnsureCam();
        boundsFixed = TryIslandBounds();   // есть FloatingIsland: границы известны сразу, без разбора рендереров
        int mini = Mathf.Clamp(miniResolution, 256, 2048);
        if (autoMiniResolution && boundsFixed) mini = Mathf.Max(mini, NeededMiniResolution());
        int big = Mathf.Clamp(mapResolution, 256, 4096);
        rtMini = NewRT(mini, "MinimapRT_Mini");
        rtBig = NewRT(big, "MinimapRT_Big");

        if (stylized)
        {
            flatMats = new Material[(int)MinimapLayer.Count * Variants];
            BuildFlatMats();
            if (outlines || paintGround)
            {
                var sh = postShader != null ? postShader : Shader.Find("Hidden/BuilderSim/MinimapPost");
                if (sh != null && sh.isSupported) postMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                else if (paintGround) Debug.LogWarning("Minimap: шейдер Hidden/BuilderSim/MinimapPost не найден, земля карты не будет видна. Перетащи MinimapPost.shader в поле Post Shader у HudMinimap или в Always Included Shaders.");
            }
        }

        StartCoroutine(BakeRoutine(true));
    }

    [Tooltip("Размер снимка мини-карты подбирается так, чтобы круг был чётким: мини-карта показывает лишь miniViewWorld метров острова")]
    public bool autoMiniResolution = true;

    int NeededMiniResolution()
    {
        float diameter = miniMask != null ? miniMask.rect.width : 210f;
        var cv = miniMask != null ? miniMask.GetComponentInParent<Canvas>() : null;
        if (cv != null) diameter *= cv.rootCanvas.scaleFactor;
        float frac = Mathf.Clamp(miniViewWorld / Mathf.Min(maxMiniZoom, 1.5f) / Mathf.Max(1f, side), 0.05f, 1f);
        int px = Mathf.CeilToInt(diameter * 1.25f / frac / 64f) * 64;
        return Mathf.Clamp(px, 256, 2048);
    }

    RenderTexture NewRT(int size, string name)
    {
        var t = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, useMipMap = false, autoGenerateMips = false
        };
        t.Create();
        return t;
    }

    void ReleaseBake()
    {
        if (rtMini != null) { rtMini.Release(); Destroy(rtMini); rtMini = null; }
        if (rtBig != null) { rtBig.Release(); Destroy(rtBig); rtBig = null; }
        if (bakeCam != null) Destroy(bakeCam.gameObject);
        if (postMat != null) Destroy(postMat);
        if (flatMats != null) for (int i = 0; i < flatMats.Length; i++) if (flatMats[i] != null) Destroy(flatMats[i]);
        flatMats = null;
        flatArrays.Clear();
        if (islandMesh != null) { Destroy(islandMesh); islandMesh = null; }
        if (groundTex != null) { Destroy(groundTex); groundTex = null; }
        groundReady = false;
    }

    void EnsureCam()
    {
        if (bakeCam != null) return;
        var go = new GameObject("MinimapBakeCam");
        go.hideFlags = HideFlags.HideInHierarchy;
        bakeCam = go.AddComponent<Camera>();
        bakeCam.orthographic = true;
        bakeCam.aspect = 1f;
        bakeCam.nearClipPlane = 0.1f;
        bakeCam.clearFlags = CameraClearFlags.SolidColor;
        bakeCam.allowHDR = false;
        bakeCam.allowMSAA = true;
        bakeCam.useOcclusionCulling = false;
        int ui = LayerMask.NameToLayer("UI");
        bakeCam.cullingMask = ui >= 0 ? ~(1 << ui) : ~0;
        bakeCam.enabled = false;
        var cd = bakeCam.GetUniversalAdditionalCameraData();
        if (cd != null)
        {
            cd.renderShadows = false;
            cd.renderPostProcessing = false;
            cd.antialiasing = AntialiasingMode.None;
            cd.requiresColorOption = CameraOverrideOption.Off;
            cd.requiresDepthOption = CameraOverrideOption.Off;
            cd.volumeLayerMask = 0;
        }
    }

    // ------------------------------------------------------------ границы острова
    // Контур FloatingIsland (точки PathPoints или запасной прямоугольник) плюс небольшой отступ. Ground_Auto за границей и всё, что дальше,
    // в кадр не попадает: морем за краем занимается маска берега (HudMinimap.Fog.cs).
    bool TryIslandBounds()
    {
        var fi = IslandSource.Find();
        if (fi == null) return false;
        var outline = fi.GetOutline();
        islandPoly = outline.ToArray();
        Vector2 min = islandPoly[0], max = islandPoly[0];
        foreach (var p in islandPoly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        islandY = fi.groundY - 0.25f;
        min -= Vector2.one * IslandSource.OuterMargin; max += Vector2.one * IslandSource.OuterMargin;
        var b = new Bounds(new Vector3((min.x + max.x) * 0.5f, fi.groundY, (min.y + max.y) * 0.5f), new Vector3(max.x - min.x, 10f, max.y - min.y));
        ApplyBounds(b);
        return true;
    }

    // Без FloatingIsland: границы по зданиям, лесу, камням (не по земле и воде: они огромные), с ограничением maxWorldSize
    void ApplyFallbackBounds()
    {
        Vector3 pp = player != null ? player.position : Vector3.zero;
        Bounds b = hasLand ? landBounds : new Bounds(pp, new Vector3(80f, 10f, 80f));
        if (manualBounds) b = new Bounds(worldCenter, new Vector3(worldSize.x, 10f, worldSize.y));
        ApplyBounds(b);
        boundsFixed = true;
    }

    void ApplyBounds(Bounds b)
    {
        if (manualBounds) b = new Bounds(worldCenter, new Vector3(worldSize.x, 10f, worldSize.y));
        else
        {
            float pad = Mathf.Max(minPadding, Mathf.Max(b.size.x, b.size.z) * islandPadding);
            Vector3 size = b.size + new Vector3(pad * 2f, 0f, pad * 2f);
            Vector3 c = b.center;
            size.x = Mathf.Min(size.x, maxWorldSize); size.z = Mathf.Min(size.z, maxWorldSize);
            size.x = Mathf.Max(size.x, 20f); size.z = Mathf.Max(size.z, 20f);
            // ограничили размер: сдвигаем рамку так, чтобы игрок остался в кадре
            if (player != null)
            {
                Vector3 pp = player.position;
                float hx = size.x * 0.5f - 2f, hz = size.z * 0.5f - 2f;
                c.x = Mathf.Clamp(c.x, pp.x - hx, pp.x + hx);
                c.z = Mathf.Clamp(c.z, pp.z - hz, pp.z + hz);
            }
            b = new Bounds(c, new Vector3(size.x, b.size.y, size.z));
        }

        center = b.center;
        float yaw = cameraYaw * Mathf.Deg2Rad;
        axRight = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
        axUp = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        float c2 = Mathf.Abs(Mathf.Cos(yaw)), s2 = Mathf.Abs(Mathf.Sin(yaw));
        float halfW = c2 * b.extents.x + s2 * b.extents.z;
        float halfH = s2 * b.extents.x + c2 * b.extents.z;
        side = 2f * Mathf.Max(halfW, halfH) * 1.02f;
    }

    // ------------------------------------------------------------ плоские материалы
    readonly Dictionary<int, Material[]> flatArrays = new Dictionary<int, Material[]>();

    void BuildFlatMats()
    {
        var p = palette;
        for (int v = 0; v < Variants; v++)
        {
            float k = Variants > 1 ? v / (float)(Variants - 1) : 0f;
            SetFlat(MinimapLayer.Water, v, p.water);
            SetFlat(MinimapLayer.Grass, v, p.grass);
            SetFlat(MinimapLayer.Sand, v, p.sand);
            SetFlat(MinimapLayer.Forest, v, Color.Lerp(p.forestA, p.forestB, k));        // кроны чуть отличаются: лес не сплошное пятно
            SetFlat(MinimapLayer.Rock, v, Color.Lerp(p.rock, p.rock * 0.86f, k * 0.8f));
            SetFlat(MinimapLayer.Wood, v, p.wood);
            SetFlat(MinimapLayer.Building, v, p.roof);
            SetFlat(MinimapLayer.Prop, v, p.prop);
        }
    }

    void SetFlat(MinimapLayer l, int v, Color c)
    {
        c.a = 1f;
        if (flatShader == null)
        {
            flatShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (flatShader == null) flatShader = Shader.Find("Sprites/Default");
            if (flatShader == null) flatShader = Shader.Find("Unlit/Color");
        }
        int i = (int)l * Variants + v;
        if (flatMats[i] == null)
        {
            if (flatShader == null) return;
            flatMats[i] = new Material(flatShader) { name = "MinimapFlat_" + l + "_" + v, hideFlags = HideFlags.HideAndDontSave };
        }
        var m = flatMats[i];
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // Здания ночью чуть теплеют: из-за этого карту есть смысл печатать заново при смене фазы суток
    void RefreshNightColors()
    {
        if (flatMats == null) return;
        float night = DayNightCycle.Night01;
        Color roof = Color.Lerp(palette.roof, palette.roofLit, Mathf.Clamp01(night) * 0.55f);
        for (int v = 0; v < Variants; v++) SetFlat(MinimapLayer.Building, v, roof);
    }

    Material[] FlatArr(MinimapLayer l, int count, int seed)
    {
        int v = (l == MinimapLayer.Forest || l == MinimapLayer.Rock) ? (seed & 0x7fffffff) % Variants : 0;
        int key = ((int)l * Variants + v) * 32 + Mathf.Min(count, 31);
        Material[] a;
        if (flatArrays.TryGetValue(key, out a)) return a;
        Material m = flatMats[(int)l * Variants + v];
        a = new Material[count];
        for (int i = 0; i < count; i++) a[i] = m;
        flatArrays[key] = a;
        return a;
    }

    // ------------------------------------------------------------ классификация
    void BuildRoots()
    {
        hideRoots.Clear(); buildingRoots.Clear(); treeRoots.Clear(); woodRoots.Clear();
        if (player != null) hideRoots.Add(player);
        if (heading != null) hideRoots.Add(heading);
        foreach (var pf in FindObjectsByType<PetFollower>(FindObjectsSortMode.None)) hideRoots.Add(pf.transform);
        foreach (var b in FindObjectsByType<BuildingController>(FindObjectsSortMode.None)) buildingRoots.Add(b.transform);
        foreach (var s in FindObjectsByType<HutStages>(FindObjectsSortMode.None)) buildingRoots.Add(s.transform);
        foreach (var s in FindObjectsByType<ShopStages>(FindObjectsSortMode.None)) buildingRoots.Add(s.transform);
        foreach (var s in FindObjectsByType<TentStages>(FindObjectsSortMode.None)) buildingRoots.Add(s.transform);
        foreach (var s in FindObjectsByType<BoardStages>(FindObjectsSortMode.None)) buildingRoots.Add(s.transform);
        foreach (var r in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None)) treeRoots.Add(r.transform);
        foreach (var r in FindObjectsByType<RiverBridge>(FindObjectsSortMode.None)) woodRoots.Add(r.transform);
    }

    static bool HasWord(string name, string[] keys)
    {
        if (keys == null) return false;
        for (int k = 0; k < keys.Length; k++)
        {
            string key = keys[k];
            if (string.IsNullOrEmpty(key)) continue;
            int from = 0;
            while (from < name.Length)
            {
                int i = name.IndexOf(key, from, StringComparison.OrdinalIgnoreCase);
                if (i < 0) break;
                from = i + 1;
                if (i > 0)
                {
                    char pc = name[i - 1], cc = name[i];
                    bool ok = !char.IsLetter(pc) || (char.IsLower(pc) && char.IsUpper(cc));
                    if (!ok) continue;
                }
                int e = i + key.Length;
                if (key.Length <= 3 && e < name.Length && char.IsLetter(name[e]) && char.IsLower(name[e])) continue;   // «fir» не должно находиться в «Fire»
                return true;
            }
        }
        return false;
    }

    // слой по имени одного объекта; Count = имя ничего не говорит
    MinimapLayer NameLayer(Transform t)
    {
        MinimapLayer l;
        if (nameCache.TryGetValue(t, out l)) return l;
        string n = t.name;
        var kw = keywords;
        if (HasWord(n, kw.hide)) l = MinimapLayer.Hide;
        else if (HasWord(n, kw.bridge)) l = MinimapLayer.Wood;
        else if (HasWord(n, kw.building)) l = MinimapLayer.Building;
        else if (HasWord(n, kw.rock)) l = MinimapLayer.Rock;
        else if (HasWord(n, kw.forest)) l = MinimapLayer.Forest;
        else if (HasWord(n, kw.water)) l = MinimapLayer.Water;
        else if (HasWord(n, kw.sand)) l = MinimapLayer.Sand;
        else if (HasWord(n, kw.ground)) l = MinimapLayer.Grass;
        else l = MinimapLayer.Count;
        nameCache[t] = l;
        return l;
    }

    MinimapLayer Classify(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SkinnedMeshRenderer || r is SpriteRenderer || r is BillboardRenderer)
            return MinimapLayer.Hide;
        Bounds b = r.bounds;
        if (b.size.sqrMagnitude > 1e6f) return MinimapLayer.Hide;                 // купол неба и т.п.
        float w = Mathf.Max(b.size.x, b.size.z), h = b.size.y;

        MinimapLayer found = MinimapLayer.Count;
        Transform t = r.transform;
        for (int d = 0; t != null && d < 10; d++, t = t.parent)
        {
            if (hideRoots.Contains(t)) return MinimapLayer.Hide;
            if (buildingRoots.Contains(t)) { found = MinimapLayer.Building; break; }
            if (woodRoots.Contains(t)) { found = MinimapLayer.Wood; break; }
            MinimapLayer nl = NameLayer(t);
            if (nl != MinimapLayer.Count) { found = nl; break; }
            if (treeRoots.Contains(t)) { found = MinimapLayer.Forest; break; }
        }

        if (found == MinimapLayer.Count)
        {
            // имена не помогли: смотрим на материал и размеры
            var sm = r.sharedMaterial;
            if (sm != null && sm.shader != null && sm.shader.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0) found = MinimapLayer.Water;
            else if (w > 24f && h < 3f) found = MinimapLayer.Grass;                 // большая плоская плита: земля
            else if (h < 0.4f && w >= 1f) found = MinimapLayer.Sand;                // плоская накладка: тропа
            else found = MinimapLayer.Prop;
        }
        if (found == MinimapLayer.Hide) return found;
        if (w < 0.3f && h < 0.3f && found != MinimapLayer.Building) return MinimapLayer.Hide;   // цветочки, камушки
        return found;
    }

    void TrackExtent(Renderer r, MinimapLayer l)
    {
        if (l == MinimapLayer.Hide) return;
        Bounds b = r.bounds;
        if (b.size.sqrMagnitude > 1e6f) return;
        if (b.max.y > topY) topY = b.max.y;
        if (b.min.y < botY) botY = b.min.y;
        if (!boundsFixed && l != MinimapLayer.Grass && l != MinimapLayer.Water)
        {
            if (!hasLand) { landBounds = b; hasLand = true; } else landBounds.Encapsulate(b);
        }
    }

    // ------------------------------------------------------------ печать
    IEnumerator BakeRoutine(bool first)
    {
        baking = true;
        rebakePending = false;
        BuildRoots();
        if (stylized) RefreshNightColors();

        var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null) continue;
            MinimapLayer l;
            if (!rendCache.TryGetValue(r, out l)) { l = Classify(r); rendCache[r] = l; }
            TrackExtent(r, l);
            if (sw.Elapsed.TotalMilliseconds > sliceMs) { yield return null; sw.Reset(); sw.Start(); }
        }
        if (!boundsFixed) ApplyFallbackBounds();
        if (logBake && first) LogClassification(all);
        PruneCaches();
        if (paintGround && stylized) yield return StartCoroutine(EnsureGround());     // земля из Terrain (HudMinimap.Ground.cs)

        yield return null;                       // сам рендер отдельным кадром: классификация и Render не складываются в один кадр
        RenderMini();
        mapReady = true;
        lastBake = Time.unscaledTime;
        bakeCount++;
        OnMapBaked();

        bool bigNow = open || (first && !bigPrefetched);
        if (bigNow)
        {
            if (first) for (int f = 0; f < 20; f++) yield return null;     // после старта: большая карта подгружается чуть позже
            else yield return null;
            RenderBig();
            bigDirty = false;
            bigPrefetched = true;
        }
        else bigDirty = true;
        baking = false;
    }

    // Большая карта открыта, а её снимок устарел: перепечатываем её одним кадром (под анимацией открытия)
    IEnumerator BakeBigOnly()
    {
        baking = true;
        yield return null;
        RenderBig();
        bigDirty = false;
        bigPrefetched = true;
        baking = false;
    }

    void LogClassification(Renderer[] all)
    {
        var counts = new int[(int)MinimapLayer.Count + 1];
        var big = new List<KeyValuePair<float, string>>();
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            MinimapLayer l;
            if (r == null || !rendCache.TryGetValue(r, out l)) continue;
            counts[(int)l]++;
            if (l == MinimapLayer.Hide) continue;
            Vector3 sz = r.bounds.size;
            big.Add(new KeyValuePair<float, string>(sz.x * sz.z, r.name + " [" + l + "] " + sz.x.ToString("0") + "x" + sz.y.ToString("0") + "x" + sz.z.ToString("0")));
        }
        big.Sort((a, b) => b.Key.CompareTo(a.Key));
        var sb = new System.Text.StringBuilder("Minimap: разбор сцены. Всего рендереров " + all.Length + "\n");
        for (int l = 0; l <= (int)MinimapLayer.Count; l++) if (counts[l] > 0) sb.Append("  " + (MinimapLayer)l + ": " + counts[l] + "\n");
        sb.Append("Самые большие по площади:\n");
        for (int i = 0; i < big.Count && i < 12; i++) sb.Append("  " + big[i].Value + "\n");
        Debug.Log(sb.ToString());
    }

    void PruneCaches()
    {
        if (bakeCount % 6 != 5) return;
        var dead = new List<Renderer>();
        foreach (var kv in rendCache) if (kv.Key == null) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) rendCache.Remove(dead[i]);
        var deadT = new List<Transform>();
        foreach (var kv in nameCache) if (kv.Key == null) deadT.Add(kv.Key);
        for (int i = 0; i < deadT.Count; i++) nameCache.Remove(deadT[i]);
    }

    void RenderMini() { RenderInto(rtMini); if (miniMap != null) { miniMap.texture = rtMini; miniMap.enabled = true; } }

    void RenderBig() { RenderInto(rtBig); if (bigMap != null) { bigMap.texture = rtBig; bigMap.enabled = true; } }

    void RenderInto(RenderTexture dst)
    {
        if (bakeCam == null || dst == null) return;
        int aa = Mathf.Clamp(bakeMsaa, 1, 4);
        if (aa == 3) aa = 2;
        var tmp = RenderTexture.GetTemporary(dst.width, dst.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, aa);
        bakeCam.targetTexture = tmp;
        RenderPass();
        bakeCam.targetTexture = null;
        if (postMat != null && stylized && (outlines || GroundReady))
        {
            postMat.SetFloat("_Width", Mathf.Max(0.75f, dst.width / 1024f * 1.25f));
            postMat.SetFloat("_Strength", outlines ? outlineStrength : 0f);
            ApplyPostTone();                                           // Candy: светлее и холоднее (HudMinimap.Candy.cs)
            postMat.SetFloat("_UseGround", GroundReady ? 1f : 0f);
            if (GroundReady) postMat.SetTexture("_GroundTex", groundTex);
            Graphics.Blit(tmp, dst, postMat);
        }
        else Graphics.Blit(tmp, dst);
        RenderTexture.ReleaseTemporary(tmp);
        if (!dst.IsCreated()) dst.Create();
    }

    // Подмена материалов, Render, возврат. Всё внутри одного вызова: ни один кадр игры не видит подмены.
    void RenderPass()
    {
        bool fogWas = RenderSettings.fog;
        float lodBias = QualitySettings.lodBias;
        var terrains = Terrain.activeTerrains;
        bool[] terrainHm = null, terrainTrees = null;
        bool hideTerrain = stylized && paintGround && GroundReady;      // землю даёт текстура, настоящий Terrain в снимке не нужен

        bakeCam.orthographicSize = side * 0.5f;
        bakeCam.transform.position = new Vector3(center.x, topY + 60f, center.z);
        bakeCam.transform.rotation = Quaternion.Euler(90f, cameraYaw, 0f);
        bakeCam.farClipPlane = (topY - botY) + 200f;
        // с землёй фон прозрачный (чёрный, альфа 0): MinimapPost подкладывает под объекты текстуру земли и море
        bakeCam.backgroundColor = (stylized && paintGround && GroundReady) ? new Color(0f, 0f, 0f, 0f) : WaterDeepColor;

        try
        {
            RenderSettings.fog = false;                       // туман острова не должен выбелить плоские цвета
            QualitySettings.lodBias = 1000f;                  // LOD по камере сверху выбрал бы самые грубые модели

            if (stylized)
            {
                SwapMaterials();
                terrainHm = new bool[terrains.Length]; terrainTrees = new bool[terrains.Length];
                for (int i = 0; i < terrains.Length; i++)
                {
                    var t = terrains[i];
                    if (t == null) continue;
                    terrainHm[i] = t.drawHeightmap; terrainTrees[i] = t.drawTreesAndFoliage;
                    if (hideTerrain) t.drawHeightmap = false;
                    t.drawTreesAndFoliage = false;
                }
            }
            if (!hideTerrain) DrawIslandBase();                  // запасной путь: земли из Terrain нет, рисуем траву под контуром FloatingIsland
            bakeCam.Render();
        }
        finally
        {
            RestoreMaterials();
            if (terrainHm != null)
                for (int i = 0; i < terrains.Length; i++)
                {
                    var t = terrains[i];
                    if (t == null) continue;
                    t.drawHeightmap = terrainHm[i]; t.drawTreesAndFoliage = terrainTrees[i];
                }
            QualitySettings.lodBias = lodBias;
            RenderSettings.fog = fogWas;
        }
    }

    // Трава под контуром FloatingIsland. Рисуется только для камеры карты (Graphics.DrawMesh с камерой), игра её не видит.
    // Настоящая земля сцены лежит выше и перекрывает эту основу; если земля не попала в снимок, остров всё равно зелёный, а не голубой.
    void DrawIslandBase()
    {
        if (islandPoly == null || islandPoly.Length < 3 || flatMats == null) return;
        var mat = flatMats[(int)MinimapLayer.Grass * Variants];
        if (mat == null) return;
        if (islandMesh == null)
        {
            Vector2 c = Vector2.zero;
            for (int i = 0; i < islandPoly.Length; i++) c += islandPoly[i];
            c /= islandPoly.Length;
            int n = islandPoly.Length;
            var v = new Vector3[n + 1];
            v[0] = new Vector3(c.x, islandY, c.y);
            for (int i = 0; i < n; i++) v[i + 1] = new Vector3(islandPoly[i].x, islandY, islandPoly[i].y);
            var tris = new int[n * 6];                              // обе стороны: порядок обхода точек не важен
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                tris[i * 6 + 0] = 0; tris[i * 6 + 1] = a; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = 0; tris[i * 6 + 4] = b; tris[i * 6 + 5] = a;
            }
            islandMesh = new Mesh { name = "MinimapIslandBase", hideFlags = HideFlags.HideAndDontSave };
            islandMesh.vertices = v;
            islandMesh.triangles = tris;
            islandMesh.RecalculateBounds();
        }
        Graphics.DrawMesh(islandMesh, Matrix4x4.identity, mat, 0, bakeCam);
    }

    RRec Rec(int i)
    {
        if (i >= recs.Length) Array.Resize(ref recs, recs.Length * 2);
        var r = recs[i];
        if (r == null) { r = new RRec(); recs[i] = r; }
        return r;
    }

    void SwapMaterials()
    {
        var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        recCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || !r.enabled) continue;
            MinimapLayer l;
            if (!rendCache.TryGetValue(r, out l)) { l = Classify(r); rendCache[r] = l; }   // появился между разбором и печатью
            var rec = Rec(recCount++);
            rec.r = r; rec.wasOff = r.forceRenderingOff; rec.swapped = false;
            if (l == MinimapLayer.Hide) { r.forceRenderingOff = true; continue; }
            r.GetSharedMaterials(tmpMats);
            int n = tmpMats.Count;
            if (n == 0) { r.forceRenderingOff = true; continue; }
            if (flatMats[(int)l * Variants] == null) continue;                              // нет шейдера для плоского материала: оставляем как есть
            if (rec.saved == null || rec.saved.Length != n) rec.saved = new Material[n];
            for (int m = 0; m < n; m++) rec.saved[m] = tmpMats[m];
            r.sharedMaterials = FlatArr(l, n, r.GetInstanceID());
            rec.swapped = true;
        }
    }

    void RestoreMaterials()
    {
        for (int i = 0; i < recCount; i++)
        {
            var rec = recs[i];
            if (rec == null || rec.r == null) { if (rec != null) rec.r = null; continue; }
            if (rec.swapped) rec.r.sharedMaterials = rec.saved;
            rec.r.forceRenderingOff = rec.wasOff;
            rec.r = null;
        }
        recCount = 0;
    }

    // ------------------------------------------------------------ расписание перепечатки
    public void RequestRebake(float delay)
    {
        if (!autoRebake) return;
        float want = Time.unscaledTime + Mathf.Max(0f, delay);
        if (!rebakePending) { rebakePending = true; rebakeAt = want; }
        else rebakeAt = Mathf.Min(rebakeAt, want);
    }

    // Из Update: не чаще раза в rebakeInterval, не во время другой печати
    void UpdateBake()
    {
        if (!mapReady || baking) return;
        if (rtMini != null && !rtMini.IsCreated()) { rebakePending = true; rebakeAt = 0f; lastBake = -99f; }   // текстуры потеряны (сворачивание на телефоне)
        if (!rebakePending) return;
        float now = Time.unscaledTime;
        if (now < rebakeAt || now < lastBake + rebakeInterval) return;
        StartCoroutine(BakeRoutine(false));
    }

    void OnApplicationFocus(bool focus) { if (focus && mapReady) RequestRebake(0.2f); }
}

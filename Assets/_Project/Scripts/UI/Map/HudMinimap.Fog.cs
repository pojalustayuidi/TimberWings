// Assets/_Project/Scripts/UI/Map/HudMinimap.Fog.cs (НЕ в Editor)
// Туман войны: зоны MapZone, в которых игрок ещё не был, закрыты мягкой лавандово-розовой дымкой-облаком (на мини- и большой карте).
// Вход в зону открывает её кругом от места, где стоит игрок (раскрытие revealSeconds). Метки внутри закрытых зон прячутся
// (кроме закреплённых: доска заданий и т.п.), на большой карте у закрытой зоны подпись «???» и нет цветной заливки.
// Открытые зоны сохраняются в файл сохранения (секция «minimap»). Плашку «НОВАЯ ЗОНА» показывает UpdateZone в HudMinimap.cs.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Туман войны")]
    public bool fogOfWar = true;
    // имена остались от пергамента: это светлый и тёмный тон пятен дымки (Candy: розовый и лавандовый, см. CandyPrepare)
    public Color parchmentLight = new Color32(0xFF, 0xDD, 0xEA, 255);
    public Color parchmentDark = new Color32(0xC9, 0xC4, 0xF7, 255);
    [Range(0.5f, 1f)] public float fogOpacity = 0.95f;
    [Tooltip("Сколько секунд раскрывается зона")]
    public float revealSeconds = 1.8f;
    [Tooltip("Размер текстуры тумана (на всю карту). 256 хватает: края и так мягкие")]
    public int fogResolution = 256;

    class FogState { public float t; public bool animating; public Vector2 origin; }

    readonly Dictionary<MapZone, FogState> fogStates = new Dictionary<MapZone, FogState>();
    readonly List<MapZone> fogKeys = new List<MapZone>();
    readonly HashSet<string> discoveredNames = new HashSet<string>();
    Texture2D fogTex;
    Color32[] fogPx, parch;
    float[] fogAlpha;
    int fogN;
    RawImage miniFog, bigFog;
    bool fogDirty;
    float fogTick;
    FogSave fogSave;

    // ------------------------------------------------------------ сохранение
    [Serializable] class FogSaveData { public string[] zones = new string[0]; }

    class FogSave : ISaveable
    {
        readonly HudMinimap h;
        public FogSave(HudMinimap hud) { h = hud; }
        public string SaveKey { get { return "minimap"; } }
        public int LoadOrder { get { return 95; } }
        public string Save()
        {
            var d = new FogSaveData();
            if (h != null) { d.zones = new string[h.discoveredNames.Count]; h.discoveredNames.CopyTo(d.zones); }
            return JsonUtility.ToJson(d);
        }
        public void Load(string json, int formatVersion)
        {
            if (h == null) return;
            var d = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<FogSaveData>(json);
            h.ApplySavedZones(d != null ? d.zones : null);
        }
        public void ResetToDefault() { if (h != null) h.ResetFog(); }
    }

    void InitFog()
    {
        fogSave = new FogSave(this);
        SaveSystem.Register(fogSave);        // если файл уже загружен, Load придёт прямо отсюда
    }

    void ReleaseFog()
    {
        if (fogSave != null) { SaveSystem.Unregister(fogSave); fogSave = null; }
        if (fogTex != null) { Destroy(fogTex); fogTex = null; }
    }

    void ApplySavedZones(string[] names)
    {
        discoveredNames.Clear();
        if (names != null) for (int i = 0; i < names.Length; i++) if (!string.IsNullOrEmpty(names[i])) discoveredNames.Add(names[i]);
        discovered.Clear();
        SyncFogZones();
        RefreshZoneUiAll();
        if (open) RefreshBigMarkers();
    }

    void ResetFog()
    {
        discoveredNames.Clear();
        discovered.Clear();
        fogStates.Clear();
        SyncFogZones();
        RefreshZoneUiAll();
    }

    // ------------------------------------------------------------ открытие зон
    public bool IsDiscovered(MapZone z) { return z == null || !fogOfWar || discovered.Contains(z); }

    void DiscoverZone(MapZone z, bool instant = false)
    {
        if (z == null) return;
        bool isNew = discovered.Add(z);
        discoveredNames.Add(z.zoneName);
        if (!isNew) return;
        if (!instant && fogOfWar && mapReady && fogTex != null && player != null)
            fogStates[z] = new FogState { t = 0f, animating = true, origin = ToMap(player.position) };
        else fogStates.Remove(z);
        fogDirty = true;
        ApplyZoneUi(z);
        if (open) RefreshBigMarkers();
    }

    // Приводит список закрытых зон в порядок: загруженные из файла, новые, удалённые
    void SyncFogZones()
    {
        if (!fogOfWar) { fogStates.Clear(); fogDirty = true; return; }
        var all = MapZone.All;
        for (int i = 0; i < all.Count; i++)
        {
            var z = all[i];
            if (z == null || !z.showOnMap) continue;
            if (!discovered.Contains(z) && discoveredNames.Contains(z.zoneName)) discovered.Add(z);
            if (discovered.Contains(z))
            {
                FogState st;
                if (fogStates.TryGetValue(z, out st) && !st.animating) fogStates.Remove(z);
            }
            else if (!fogStates.ContainsKey(z)) fogStates[z] = new FogState();
        }
        fogKeys.Clear();
        foreach (var kv in fogStates) if (kv.Key == null) fogKeys.Add(kv.Key);
        for (int i = 0; i < fogKeys.Count; i++) fogStates.Remove(fogKeys[i]);
        fogDirty = true;
    }

    // метка внутри закрытой зоны не показывается
    bool FogHides(Poi p)
    {
        if (!fogOfWar || p == null || p.pinned || p.t == null) return false;
        var z = MapZone.At(p.t.position);
        return z != null && z.showOnMap && !discovered.Contains(z);
    }

    // ------------------------------------------------------------ подписи и заливка зон на большой карте
    void ApplyZoneUi(MapZone z)
    {
        if (zoneLayer == null || z == null) return;
        bool hide = fogOfWar && !discovered.Contains(z);
        var area = zoneLayer.Find("Area_" + z.zoneName);
        if (area != null) area.gameObject.SetActive(!hide);
        var nm = zoneLayer.Find("Name_" + z.zoneName);
        if (nm == null) return;
        var tx = nm.GetComponent<TMP_Text>();
        if (tx == null) return;
        tx.text = hide ? "???" : z.zoneName;
        tx.color = ZoneNameColor(z, hide);
        tx.outlineColor = ZoneNameOutline(hide);
    }

    void RefreshZoneUiAll()
    {
        bigScaleDirty = true;
        if (zoneLayer == null) return;
        var all = MapZone.All;
        for (int i = 0; i < all.Count; i++) ApplyZoneUi(all[i]);
    }

    // ------------------------------------------------------------ слои
    void EnsureFogLayers()
    {
        if (!fogOfWar) return;
        EnsureFogTex();
        if (miniFog == null && miniMap != null) miniFog = MakeFogLayer(miniMap.rectTransform);
        if (bigFog == null && bigMap != null) bigFog = MakeFogLayer(bigMap.rectTransform);
    }

    RawImage MakeFogLayer(RectTransform over)
    {
        if (over == null || over.parent == null) return null;
        var go = new GameObject("Fog", typeof(RectTransform), typeof(RawImage));
        go.layer = over.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(over.parent, false);
        r.anchorMin = over.anchorMin; r.anchorMax = over.anchorMax; r.pivot = over.pivot;
        r.anchoredPosition = over.anchoredPosition; r.sizeDelta = over.sizeDelta;
        r.SetSiblingIndex(over.GetSiblingIndex() + 1);
        var ri = go.GetComponent<RawImage>();
        ri.texture = fogTex;
        ri.raycastTarget = false;
        ri.uvRect = new Rect(0, 0, 1, 1);
        ri.enabled = false;
        return ri;
    }

    void EnsureFogTex()
    {
        if (fogTex != null) return;
        fogN = Mathf.Clamp(fogResolution, 64, 512);
        fogTex = new Texture2D(fogN, fogN, TextureFormat.RGBA32, false) { name = "MinimapFog", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        fogPx = new Color32[fogN * fogN];
        fogAlpha = new float[fogN * fogN];
        parch = new Color32[fogN * fogN];
        for (int y = 0; y < fogN; y++)
            for (int x = 0; x < fogN; x++)
            {
                float u = x / (float)fogN, v = y / (float)fogN;
                float t = 0.52f * VNoise(u * 9f, v * 9f) + 0.30f * VNoise(u * 28f + 17f, v * 28f) + 0.18f * VNoise(u * 110f, v * 16f + 5f);   // пятна + волокна
                parch[y * fogN + x] = Color.Lerp(parchmentDark, parchmentLight, Mathf.Clamp01(t));
            }
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static float VNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // ------------------------------------------------------------ расчёт текстуры тумана
    void RebuildFog()
    {
        if (fogTex == null) return;
        int n = fogN;
        Array.Clear(fogAlpha, 0, fogAlpha.Length);
        foreach (var kv in fogStates) if (kv.Key != null) AddZoneAlpha(kv.Key, kv.Value, n);
        for (int i = 0; i < fogPx.Length; i++)
        {
            float a = fogAlpha[i];
            Color32 c = parch[i];
            if (a > 0.003f)
            {
                c = FogEdge(c, a);                                                           // кромка: Candy светлеет как облачко (без Candy темнеет)
            }
            else c.a = 0;
            fogPx[i] = c;
        }
        fogTex.SetPixels32(fogPx);
        fogTex.Apply(false);
    }

    void AddZoneAlpha(MapZone z, FogState st, int n)
    {
        Vector2 zc = ToMap(z.transform.position);
        bool circle = z.shape == MapZone.Shape.Circle;
        float ang = 0f, ext;
        if (circle) ext = z.radius;
        else
        {
            ext = 0.5f * Mathf.Sqrt(z.size.x * z.size.x + z.size.y * z.size.y);
            Vector3 zr = Quaternion.Euler(0f, z.transform.eulerAngles.y, 0f) * Vector3.right;
            ang = Mathf.Atan2(Vector3.Dot(zr, axUp), Vector3.Dot(zr, axRight));
        }
        float feather = Mathf.Clamp(ext * 0.1f, 1.5f, 4f);
        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
        float lim = ext + feather * 2f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(((zc.x - lim) / side + 0.5f) * n), 0, n - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(((zc.x + lim) / side + 0.5f) * n), 0, n - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(((zc.y - lim) / side + 0.5f) * n), 0, n - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(((zc.y + lim) / side + 0.5f) * n), 0, n - 1);
        float revealR = -1f;
        if (st.animating) revealR = UiDraw.EaseOutCubic(Mathf.Clamp01(st.t)) * ((zc - st.origin).magnitude + ext + 6f);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float mx = ((x + 0.5f) / n - 0.5f) * side, my = ((y + 0.5f) / n - 0.5f) * side;
                float dx = mx - zc.x, dy = my - zc.y;
                float sd;
                if (circle) sd = Mathf.Sqrt(dx * dx + dy * dy) - z.radius;
                else
                {
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    float qx = Mathf.Abs(lx) - z.size.x * 0.5f, qy = Mathf.Abs(ly) - z.size.y * 0.5f;
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    sd = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f);
                }
                float cov = Mathf.Clamp01(0.5f - sd / (2f * feather));
                if (cov <= 0f) continue;
                cov = cov * cov * (3f - 2f * cov);
                if (st.animating)
                {
                    float ex = mx - st.origin.x, ey = my - st.origin.y;
                    float dist = Mathf.Sqrt(ex * ex + ey * ey);
                    cov *= Mathf.Clamp01((dist - (revealR - 3f)) / 6f);        // круг раскрытия с мягкой кромкой
                    if (cov <= 0f) continue;
                }
                int idx = y * n + x;
                if (cov > fogAlpha[idx]) fogAlpha[idx] = cov;
            }
    }

    // ------------------------------------------------------------ каждый кадр
    void UpdateFog(float dt)
    {
        if (!fogOfWar || fogTex == null) return;

        // вход в зону без плашки (announce выключен или это не главная зона): открываем тут, остальное делает UpdateZone
        fogTick -= dt;
        if (fogTick <= 0f)
        {
            fogTick = 0.25f;
            if (player != null && Time.unscaledTime > 2.8f)
            {
                var all = MapZone.All;
                Vector3 pp = player.position;
                for (int i = 0; i < all.Count; i++)
                {
                    var z = all[i];
                    if (z == null || !z.showOnMap || discovered.Contains(z) || !z.Contains(pp)) continue;
                    if (z == currentZone && announceZones && z.announce) continue;
                    DiscoverZone(z);
                }
            }
        }

        bool anim = false;
        if (fogStates.Count > 0)
        {
            fogKeys.Clear();
            foreach (var kv in fogStates) if (kv.Value.animating) fogKeys.Add(kv.Key);
            for (int i = 0; i < fogKeys.Count; i++)
            {
                var key = fogKeys[i];
                FogState st;
                if (key == null || !fogStates.TryGetValue(key, out st)) continue;
                st.t += dt / Mathf.Max(0.2f, revealSeconds);
                fogDirty = true;
                if (st.t >= 1f) fogStates.Remove(key); else anim = true;
            }
        }
        if (fogDirty) { RebuildFog(); fogDirty = false; }

        bool any = fogStates.Count > 0;
        if (miniFog != null)
        {
            miniFog.enabled = any;
            if (miniMap != null) { miniFog.uvRect = miniMap.uvRect; miniFog.color = miniMap.color; }
        }
        if (bigFog != null)
        {
            bigFog.enabled = any;
            if (bigMap != null) bigFog.color = bigMap.color;
        }
        if (anim) fogDirty = true;
    }
}

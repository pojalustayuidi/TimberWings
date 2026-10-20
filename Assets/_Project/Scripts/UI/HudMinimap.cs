// Assets/_Project/Scripts/UI/HudMinimap.cs - v3 для BuilderSimHud (partial: Bake, Events, Fog, Route, BigMap в соседних файлах)
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public partial class HudMinimap : MonoBehaviour
{
    [Serializable]
    public class MapKind
    {
        public string key;
        public string label;
        public Sprite icon;
        public Color tint = Color.white;
        public float zoneRadius = 6f;
    }

    class Poi
    {
        public Transform t;
        public MapKind kind;
        public string label;
        public RectTransform mini, big;
        public CanvasGroup miniGroup;
        public bool pinned;
    }

    [Header("Мини-карта")]
    public RawImage miniMap;
    public RectTransform miniMask;
    public RectTransform miniMarkers;
    public RectTransform miniPlayer;
    public Image miniPing;
    public RectTransform miniNorth;
    public TMP_Text zoneLabel;
    public RectTransform zoneBanner;
    public Button openButton;

    [Header("Большая карта")]
    public Button closeButton;
    public CanvasGroup bigGroup;
    public RectTransform bigWindow;
    public RectTransform bigFrame;
    public RawImage bigMap;
    public RectTransform bigMarkers;
    public RectTransform bigPlayer;
    public Image bigPing;
    public RectTransform bigCompass;

    [Header("Метки")]
    public RectTransform markerTemplate;
    [Tooltip("Рисованные кодом значки (костёр, хижина, рынок, палатка, мост, лес) вместо спрайтов из kinds. Выключи, чтобы вернуть свои")]
    public bool codeIcons = true;
    public MapKind[] kinds;
    public bool autoFindByName = true;
    public string[] autoIgnoreKinds = { "Pad" };
    public string[] bigOnlyKinds = { "Forest", "Bridge" };
    public string[] pinnedKinds = { "QuestBoard", "Order" };

    [Header("Зоны")]
    public bool useZones = true;
    public bool announceZones = true;
    public bool showZonesOnBigMap = true;
    [Range(0.1f, 1f)] public float zoneOpacity = 0.7f;
    public string defaultZone = "Окрестности";

    [Header("Стиль")]
    [Tooltip("Сплошная подложка под снимком карты: прозрачные места (вода, края) показывают её цвет, а не тёмный диск HUD")]
    public bool solidBacking = true;
    [Range(0f, 1f)] public float miniVignette = 0.45f;     // затемнение к краю круга (было 0.75, карта выглядела мутной)
    [Tooltip("Метки вне зоны видимости рисуются мелкими точками на кромке круга")]
    public bool edgePips = true;
    [Tooltip("Метки, которые налезают друг на друга, раздвигаются")]
    public bool spreadMarkers = true;

    [Header("Время суток")]
    [Tooltip("Карта слегка темнеет ночью (холодный оттенок) и теплеет на закате. Оттенок берётся из DayNightCycle.HudTint")]
    public bool dayTint = true;
    [Tooltip("Другие картинки HUD, которые тоже слегка темнеют ночью (панели, рамки). Пусто: только карта")]
    public Graphic[] hudTint;

    [Header("День")]
    public bool showDay = true;
    [Tooltip("Время суток рядом с днём: «День 3 · 14:35 · Закат»")]
    public bool showClock = true;
    [Tooltip("Сдвиг подписи «День N» от низа круга миникарты")]
    public Vector2 dayLabelOffset = new Vector2(0f, -6f);
    public float dayFontSize = 26f;
    [Tooltip("Готовая табличка «День N» в сцене (собирает Tools > BuilderSim > Build HUD). Пусто = текст под кругом рисуется кодом, как раньше")]
    public RectTransform dayBanner;
    public TMP_Text dayText;
    [Tooltip("Цвет приписки «· Закат» (на деревянной табличке нужен тёмный)")]
    public Color phaseColor = new Color32(0xC9, 0xD3, 0xEE, 255);

    TMP_Text dayLabel;
    int shownDay = -1, shownMinute = -1;
    DayPhase shownPhase = (DayPhase)(-1);

    [Header("Настройки")]
    public KeyCode mapKey = KeyCode.M;
    public float miniViewWorld = 36f;
    public bool pauseWhileOpen = false;
    public Transform player;
    public float cameraYaw = 45f;

    [Header("Снимок карты")]
    public int mapResolution = 1024;
    public Color background = new Color(0.13f, 0.19f, 0.30f, 1f);
    public bool manualBounds = false;
    public Vector3 worldCenter = Vector3.zero;
    public Vector2 worldSize = new Vector2(100f, 100f);
    public float maxWorldSize = 160f;

    Vector3 center;
    float side = 100f;
    Vector3 axRight = Vector3.right, axUp = Vector3.forward;
    Transform heading;
    readonly List<Poi> pois = new List<Poi>();
    RectTransform zoneLayer;
    static Sprite circleArea, rectArea;
    bool open;
    // большая карта открыта: MobileInput не двигает персонажа и не рубит, пока смотришь карту
    public static bool BigMapOpen { get; private set; }
    float openT;
    float zoneTimer;
    string zoneNow;
    MapZone currentZone;
    readonly HashSet<MapZone> discovered = new HashSet<MapZone>();
    bool announcePending;
    float zonePunch = 1f;
    float savedTimeScale = 1f;
    bool pausedByUs;

    // оттенок по времени суток
    Color tintNow = Color.white;
    Color[] hudBase;
    readonly List<Image> backings = new List<Image>();

    void Start()
    {
        var pm = PlayerMovement.Instance;
        if (player == null && pm != null) player = pm.transform;
        if (pm != null)
        {
            cameraYaw = pm.cameraYaw;
            heading = pm.model != null ? pm.model : pm.transform;
        }
        if (heading == null) heading = player;
        if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
        if (miniMap != null) miniMap.enabled = false;     // пока снимка нет, белый квадрат не показываем
        if (bigMap != null) { bigMap.enabled = false; bigMap.uvRect = new Rect(0, 0, 1, 1); bigMap.color = Color.white; }
        if (miniMap != null) miniMap.color = Color.white;
        InitBake();
        Stylize();
        CaptureHudBase();
        CreateDayLabel();
        InitMiniZoom();
        InitBigMapUi();      // до Rescan: слой зон создаётся внутри контейнера большой карты
        InitFog();
        Rescan();
        CreateToast();
        if (miniNorth != null)
        {
            float y = cameraYaw * Mathf.Deg2Rad;
            miniNorth.anchoredPosition = new Vector2(-Mathf.Sin(y), Mathf.Cos(y)) * 118f;
        }
        if (bigCompass != null)
        {
            float y = cameraYaw * Mathf.Deg2Rad;
            bigCompass.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Mathf.Cos(y), -Mathf.Sin(y)) * Mathf.Rad2Deg - 90f);
        }
        if (bigGroup != null) { bigGroup.alpha = 0f; bigGroup.blocksRaycasts = false; bigGroup.interactable = false; }
        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (zoneLabel != null) zoneLabel.text = defaultZone;
        zoneNow = defaultZone;
    }

    void CreateToast()
    {
        Canvas cv = zoneLabel != null ? zoneLabel.canvas : GetComponentInParent<Canvas>();
        if (cv == null) return;
        var root = (RectTransform)cv.rootCanvas.transform;
        var toast = ZoneToast.Create(root, zoneLabel != null ? zoneLabel.font : null);
        if (toast != null && bigGroup != null && bigGroup.transform.parent == root)
            toast.transform.SetSiblingIndex(bigGroup.transform.GetSiblingIndex());
    }

    void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        ReleaseBake();
        ReleaseFog();
        BigMapOpen = false;
        RestoreTime();
    }
    void OnEnable() { SubscribeMapEvents(); }
    void OnDisable() { UnsubscribeMapEvents(); RestoreTime(); }

    // ---------------------------------------------------------------- стиль
    // Снимок карты рисуется камерой, а полупрозрачные объекты (вода и т.п.) пишут в RenderTexture неполную альфу:
    // сквозь них просвечивал тёмный диск HUD. Подкладываем сплошной цвет под карту.
    void Stylize()
    {
        if (solidBacking)
        {
            AddBacking(miniMap);
            AddBacking(bigMap);
        }
        if (miniMask != null)
        {
            var v = miniMask.Find("Vignette");
            var im = v != null ? v.GetComponent<Image>() : null;
            if (im != null) { var c = im.color; c.a = miniVignette; im.color = c; }
        }
    }

    void AddBacking(RawImage map)
    {
        if (map == null || map.transform.parent == null) return;
        var go = new GameObject("Backing", typeof(RectTransform), typeof(Image));
        go.layer = map.gameObject.layer;
        var r = (RectTransform)go.transform;
        var mr = map.rectTransform;
        r.SetParent(mr.parent, false);
        r.anchorMin = mr.anchorMin; r.anchorMax = mr.anchorMax; r.pivot = mr.pivot;
        r.offsetMin = mr.offsetMin; r.offsetMax = mr.offsetMax;
        r.SetSiblingIndex(mr.GetSiblingIndex());          // под снимок карты
        var im = go.GetComponent<Image>();
        var c = WaterDeepColor; c.a = 1f;
        im.color = c;
        im.raycastTarget = false;
        backings.Add(im);
    }

    // ---------------------------------------------------------------- время суток
    void CaptureHudBase()
    {
        if (hudTint == null) return;
        hudBase = new Color[hudTint.Length];
        for (int i = 0; i < hudTint.Length; i++) hudBase[i] = hudTint[i] != null ? hudTint[i].color : Color.white;
    }

    // «День 3 · Закат» под кругом миникарты
    void CreateDayLabel()
    {
        if (dayText != null)
        {
            // табличка собрана в сцене: только обновляем текст
            if (dayBanner != null) dayBanner.gameObject.SetActive(showDay);
            if (!showDay) return;
            dayLabel = dayText;
            UpdateDayLabel();
            return;
        }
        if (!showDay || miniMask == null || miniMask.parent == null) return;

        var go = new GameObject("DayLabel", typeof(RectTransform));
        go.layer = miniMask.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(miniMask.parent, false);
        rt.anchorMin = miniMask.anchorMin;
        rt.anchorMax = miniMask.anchorMax;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(260f, 40f);
        float w = miniMask.rect.width, h = miniMask.rect.height;
        rt.anchoredPosition = miniMask.anchoredPosition
            + new Vector2((0.5f - miniMask.pivot.x) * w, -miniMask.pivot.y * h)   // низ круга
            + dayLabelOffset;

        var tx = go.AddComponent<TextMeshProUGUI>();
        if (zoneLabel != null) tx.font = zoneLabel.font;          // тот же шрифт, что у названия зоны
        tx.fontSize = dayFontSize;
        tx.fontStyle = FontStyles.Bold;
        tx.alignment = TextAlignmentOptions.Center;
        tx.color = Color.white;
        tx.raycastTarget = false;
        tx.outlineWidth = 0.25f;
        tx.outlineColor = new Color32(0, 0, 0, 200);              // читается и на светлой, и на тёмной карте
        dayLabel = tx;
        UpdateDayLabel();
    }

    void UpdateDayLabel()
    {
        if (dayLabel == null) return;
        int day = DayNightCycle.DayNumber;
        DayPhase ph = DayNightCycle.Phase;
        int minute = Mathf.FloorToInt(Mathf.Repeat(DayNightCycle.Time01, 1f) * 1440f) % 1440;   // 0 = полночь, 0.5 = полдень
        if (showClock) minute -= minute % 5;                       // часы идут шагом 5 минут: текст не перерисовывается каждый кадр
        else minute = 0;
        if (day == shownDay && ph == shownPhase && minute == shownMinute) return;   // текст меняем только при смене
        shownDay = day;
        shownPhase = ph;
        shownMinute = minute;
        string phase = DayNightCycle.PhaseName(ph);
        string clock = showClock ? (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00") : "";
        string tail = clock + (clock.Length > 0 && phase.Length > 0 ? " · " : "") + phase;
        dayLabel.text = "День " + day + (tail.Length > 0 ? "  <size=65%><color=#" + ColorUtility.ToHtmlStringRGB(phaseColor) + ">· " + tail + "</color></size>" : "");
    }

    void UpdateTint()
    {
        Color c = dayTint ? DayNightCycle.HudTint : Color.white;
        if (Mathf.Abs(c.r - tintNow.r) + Mathf.Abs(c.g - tintNow.g) + Mathf.Abs(c.b - tintNow.b) < 0.004f) return;   // не дёргаем UI зря
        tintNow = c;
        if (miniMap != null) miniMap.color = c;
        if (bigMap != null) bigMap.color = c;
        for (int i = 0; i < backings.Count; i++)
            if (backings[i] != null) backings[i].color = new Color(WaterDeepColor.r * c.r, WaterDeepColor.g * c.g, WaterDeepColor.b * c.b, 1f);
        if (hudTint != null && hudBase != null)
            for (int i = 0; i < hudTint.Length && i < hudBase.Length; i++)
                if (hudTint[i] != null) hudTint[i].color = new Color(hudBase[i].r * c.r, hudBase[i].g * c.g, hudBase[i].b * c.b, hudBase[i].a);
    }

    // ---------------------------------------------------------------- раскладка меток
    // Метки, стоящие слишком близко, раздвигаются (несколько проходов). Метки на кромке остаются на кромке,
    // поэтому там они расходятся по дуге. maxR <= 0: границы нет (большая карта).
    static void Relax(List<Vector2> pos, List<float> radius, List<bool> edge, float maxR)
    {
        int n = pos.Count;
        if (n < 2) return;
        for (int it = 0; it < 5; it++)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 d = pos[j] - pos[i];
                    float dist = d.magnitude;
                    float need = radius[i] + radius[j];
                    if (dist >= need) continue;
                    Vector2 dir = dist > 0.01f ? d / dist : new Vector2(Mathf.Cos(i * 2.4f + j), Mathf.Sin(i * 2.4f + j));
                    float push = (need - dist) * 0.5f;
                    pos[i] -= dir * push;
                    pos[j] += dir * push;
                }
            }
            if (maxR <= 0f) continue;
            for (int i = 0; i < n; i++)
            {
                float m = pos[i].magnitude;
                if (m < 0.01f) continue;
                if (edge[i] || m > maxR) pos[i] = pos[i] / m * maxR;
            }
        }
    }

    readonly List<Poi> layPois = new List<Poi>();
    readonly List<Vector2> layPos = new List<Vector2>();
    readonly List<float> layRad = new List<float>();
    readonly List<bool> layEdge = new List<bool>();

    static bool IsIn(string[] arr, string key)
    {
        if (arr == null || string.IsNullOrEmpty(key)) return false;
        for (int i = 0; i < arr.Length; i++) if (string.Equals(arr[i], key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    MapKind FindKind(string key)
    {
        if (kinds == null || string.IsNullOrEmpty(key)) return null;
        foreach (var k in kinds) if (k != null && string.Equals(k.key, key, StringComparison.OrdinalIgnoreCase)) return k;
        return null;
    }
    MapKind KindByName(string name)
    {
        if (kinds == null) return null;
        foreach (var k in kinds) { if (k == null || string.IsNullOrEmpty(k.key)) continue; if (!name.StartsWith(k.key, StringComparison.OrdinalIgnoreCase)) continue; if (name.Length == k.key.Length || !char.IsLetter(name[k.key.Length])) return k; }
        return null;
    }
    public void Rescan()
    {
        foreach (var p in pois) { if (p.mini != null) Destroy(p.mini.gameObject); if (p.big != null) Destroy(p.big.gameObject); }
        pois.Clear();
        var used = new HashSet<Transform>();
        foreach (var m in FindObjectsByType<MapPoi>(FindObjectsSortMode.None)) { var k = FindKind(m.kind); if (k == null) continue; Add(m.transform, k, string.IsNullOrEmpty(m.label) ? k.label : m.label); used.Add(m.transform); }
        if (autoFindByName)
        {
            foreach (var tr in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (tr == null || used.Contains(tr) || !tr.gameObject.activeInHierarchy) continue;
                var k = KindByName(tr.name); if (k == null || IsIn(autoIgnoreKinds, k.key)) continue;
                if (tr.GetComponentInParent<Canvas>() != null) continue;
                bool nested = false; for (var p = tr.parent; p != null; p = p.parent) if (KindByName(p.name) != null) { nested = true; break; }
                if (nested) continue;
                Add(tr, k, k.label);
            }
        }
        BuildZoneLayer();
        HookBuildings();
    }
    void Add(Transform t, MapKind k, string label)
    {
        var poi = new Poi { t = t, kind = k, label = label, pinned = IsIn(pinnedKinds, k.key) };
        if (markerTemplate != null)
        {
            if (miniMarkers != null && !IsIn(bigOnlyKinds, k.key)) { poi.mini = MakeMarker(miniMarkers, poi, false); poi.miniGroup = poi.mini.gameObject.AddComponent<CanvasGroup>(); if (poi.pinned) poi.mini.SetAsLastSibling(); }
            if (bigMarkers != null) poi.big = MakeMarker(bigMarkers, poi, true);
        }
        pois.Add(poi);
    }
    RectTransform MakeMarker(RectTransform parent, Poi p, bool big)
    {
        var m = Instantiate(markerTemplate, parent, false);
        m.gameObject.name = "Marker_" + p.label; m.gameObject.SetActive(true); m.localScale = Vector3.one * (big ? 1.25f : 1f);
        var icon = m.Find("Icon"); if (icon != null) { var im = icon.GetComponent<Image>(); if (im != null) { Sprite sp = codeIcons || p.kind.icon == null ? MapIcons.Get(MapIcons.Guess(p.kind.key)) : p.kind.icon; if (sp != null) { im.sprite = sp; im.color = Color.white; im.preserveAspect = true; } } }
        var pin = m.Find("Pin"); if (pin != null) { var im = pin.GetComponent<Image>(); if (im != null) im.color = p.kind.tint; }
        var lb = m.Find("Label"); if (lb != null) { lb.gameObject.SetActive(big); var tx = lb.GetComponent<TMP_Text>(); if (tx != null) tx.text = p.label; }
        return m;
    }
    static Sprite MakeArea(bool circle)
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float fx = x + 0.5f, fy = y + 0.5f; float sd = circle ? Mathf.Sqrt((fx - 64f) * (fx - 64f) + (fy - 64f) * (fy - 64f)) - 63f : UiDraw.RRect(fx, fy, S, S, 18f); float cov = Mathf.Clamp01(0.5f - sd); float edge = Mathf.Clamp01(1f - (-sd) / 4f); px[y * S + x] = new Color(1f, 1f, 1f, cov * Mathf.Lerp(0.22f, 1f, edge)); }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(24, 24, 24, 24));
    }
    void BuildZoneLayer()
    {
        if (zoneLayer != null) Destroy(zoneLayer.gameObject);
        zoneLayer = null;
        zoneNameBase.Clear();
        if (!showZonesOnBigMap || bigMarkers == null || MapZone.All.Count == 0) return;
        if (circleArea == null) circleArea = MakeArea(true);
        if (rectArea == null) rectArea = MakeArea(false);
        var go = new GameObject("Zones", typeof(RectTransform)); go.layer = bigMarkers.gameObject.layer;
        zoneLayer = (RectTransform)go.transform; zoneLayer.SetParent(bigMarkers.parent, false);
        zoneLayer.anchorMin = Vector2.zero; zoneLayer.anchorMax = Vector2.one; zoneLayer.offsetMin = Vector2.zero; zoneLayer.offsetMax = Vector2.zero;
        zoneLayer.SetSiblingIndex(bigMarkers.GetSiblingIndex());
        float size = bigMap != null ? bigMap.rectTransform.rect.width : 800f;
        var c = new Vector2(0.5f, 0.5f);
        foreach (var z in MapZone.All)
        {
            if (z == null || !z.showOnMap) continue;
            var ao = new GameObject("Area_" + z.zoneName, typeof(RectTransform), typeof(Image)); ao.layer = go.layer;
            var r = (RectTransform)ao.transform; r.SetParent(zoneLayer, false); r.anchorMin = r.anchorMax = c; r.pivot = c;
            r.anchoredPosition = ToMap(z.transform.position) / side * size;
            var img = ao.GetComponent<Image>(); img.raycastTarget = false; img.color = new Color(z.color.r, z.color.g, z.color.b, zoneOpacity);
            if (z.shape == MapZone.Shape.Circle) { r.sizeDelta = Vector2.one * (z.radius * 2f / side * size); img.sprite = circleArea; img.type = Image.Type.Simple; }
            else { r.sizeDelta = new Vector2(z.size.x, z.size.y) / side * size; img.sprite = rectArea; img.type = Image.Type.Sliced; Vector3 zr = Quaternion.Euler(0f, z.transform.eulerAngles.y, 0f) * Vector3.right; float ang = Mathf.Atan2(Vector3.Dot(zr, axUp), Vector3.Dot(zr, axRight)) * Mathf.Rad2Deg; r.localRotation = Quaternion.Euler(0f, 0f, ang); }
        }
        foreach (var z in MapZone.All)
        {
            if (z == null || !z.showOnMap) continue;
            var lo = new GameObject("Name_" + z.zoneName, typeof(RectTransform)); lo.layer = go.layer;
            var r = (RectTransform)lo.transform; r.SetParent(zoneLayer, false); r.anchorMin = r.anchorMax = c; r.pivot = c; r.sizeDelta = new Vector2(320f, 44f);
            r.anchoredPosition = ToMap(z.transform.position) / side * size;
            var tx = lo.AddComponent<TextMeshProUGUI>(); if (zoneLabel != null) tx.font = zoneLabel.font;
            tx.text = z.zoneName; tx.fontSize = 30f; tx.fontStyle = FontStyles.Bold; tx.alignment = TextAlignmentOptions.Center; tx.color = Color.Lerp(Color.white, z.color, 0.3f); tx.raycastTarget = false; tx.enableWordWrapping = false; tx.outlineWidth = 0.22f; tx.outlineColor = new Color32(0x0B, 0x10, 0x20, 255);
        }
        RefreshZoneUiAll();      // закрытые зоны: «???» и без заливки
    }
    Vector2 ToMap(Vector3 world) { Vector3 d = world - center; return new Vector2(Vector3.Dot(d, axRight), Vector3.Dot(d, axUp)); }
    float ArrowAngle(Transform t) { if (t == null) return 0f; Vector3 f = t.forward; return Mathf.Atan2(Vector3.Dot(f, axUp), Vector3.Dot(f, axRight)) * Mathf.Rad2Deg - 90f; }
    public void Toggle() { SetOpen(!open); }
    public void Close() { SetOpen(false); }
    void SetOpen(bool v) { open = v; BigMapOpen = v; if (bigGroup != null) { bigGroup.blocksRaycasts = v; bigGroup.interactable = v; } if (v) { RefreshBigMarkers(); OnBigOpened(); } if (pauseWhileOpen) { if (v && !pausedByUs) { savedTimeScale = Time.timeScale; Time.timeScale = 0f; pausedByUs = true; } else if (!v) RestoreTime(); } }
    void RestoreTime() { if (!pausedByUs) return; Time.timeScale = savedTimeScale; pausedByUs = false; }
    void Update()
    {
        if (Input.GetKeyDown(mapKey)) Toggle(); else if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        float dt = Time.unscaledDeltaTime; float time = Time.unscaledTime;
        UpdateTint();
        UpdateDayLabel();
        UpdateMiniZoom(dt);
        UpdateBake();
        openT = Mathf.MoveTowards(openT, open ? 1f : 0f, dt / 0.22f);
        if (bigGroup != null) bigGroup.alpha = Mathf.SmoothStep(0f, 1f, openT);
        if (bigWindow != null) bigWindow.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, UiDraw.EaseOutBack(openT));
        if (player == null) return;
        if (!mapReady) { UpdateZone(dt); return; }
        Vector2 pm = ToMap(player.position); float k = Mathf.Repeat(time / 1.6f, 1f);
        UpdateMini(pm, k, time);
        if (openT > 0.001f) UpdateBig(pm, k);
        UpdateZone(dt);
        UpdateExtras(dt, time, pm);
    }
    void UpdateMini(Vector2 pm, float k, float time)
    {
        if (miniMap == null || miniMask == null) return;
        float diameter = miniMask.rect.width; float mapW = miniMap.rectTransform.rect.width; float frac = Mathf.Clamp01(ViewWorld * (mapW / Mathf.Max(1f, diameter)) / side);
        float u = 0.5f + pm.x / side, v = 0.5f + pm.y / side; miniMap.uvRect = new Rect(u - frac * 0.5f, v - frac * 0.5f, frac, frac);
        float ppu = diameter / ViewWorld; float maxR = diameter * 0.5f - 14f; float fadeFrom = maxR * 0.78f;
        float markerPx = markerTemplate != null ? Mathf.Max(24f, markerTemplate.sizeDelta.x) : 36f;
        layPois.Clear(); layPos.Clear(); layRad.Clear(); layEdge.Clear();
        foreach (var p in pois)
        {
            if (p.mini == null) continue;
            if (p.t == null || FogHides(p)) { p.mini.gameObject.SetActive(false); continue; }
            Vector2 pos = (ToMap(p.t.position) - pm) * ppu; float mag = pos.magnitude;
            float scale, alpha; bool edge = false;
            if (mag <= maxR)
            {
                // внутри круга: у кромки метка уменьшается и тускнеет, но не пропадает
                float f = mag <= fadeFrom ? 1f : Mathf.InverseLerp(maxR, fadeFrom, mag);
                scale = Mathf.Lerp(0.7f, 1f, f); alpha = Mathf.Lerp(0.6f, 1f, f);
            }
            else if (p.pinned) { edge = true; pos = pos / mag * maxR; scale = 0.82f + 0.08f * Mathf.Sin(time * 5f); alpha = 1f; }
            else if (edgePips) { edge = true; pos = pos / mag * maxR; scale = 0.5f; alpha = 0.7f; }
            else { p.mini.gameObject.SetActive(false); continue; }

            p.mini.gameObject.SetActive(true);
            p.mini.localScale = Vector3.one * (scale * markerScale);
            if (p.miniGroup != null) p.miniGroup.alpha = alpha;
            layPois.Add(p); layPos.Add(pos); layRad.Add(markerPx * 0.5f * scale * markerScale * 0.95f); layEdge.Add(edge);
        }
        if (spreadMarkers) Relax(layPos, layRad, layEdge, maxR);
        for (int i = 0; i < layPois.Count; i++) layPois[i].mini.anchoredPosition = layPos[i];
        if (miniPlayer != null) miniPlayer.localRotation = Quaternion.Euler(0f, 0f, ArrowAngle(heading));
        Ping(miniPing, k);
    }
    void UpdateBig(Vector2 pm, float k)
    {
        float size = bigMap != null ? bigMap.rectTransform.rect.width : 800f;
        if (bigPlayer != null) { bigPlayer.anchoredPosition = pm / side * size; bigPlayer.localRotation = Quaternion.Euler(0f, 0f, ArrowAngle(heading)); }
        Ping(bigPing, k);
    }
    void RefreshBigMarkers()
    {
        float size = bigMap != null ? bigMap.rectTransform.rect.width : 800f;
        layPois.Clear(); layPos.Clear(); layRad.Clear(); layEdge.Clear();
        foreach (var p in pois)
        {
            if (p.big == null) continue;
            if (p.t == null || FogHides(p)) { p.big.gameObject.SetActive(false); continue; }
            p.big.gameObject.SetActive(true);
            layPois.Add(p); layPos.Add(ToMap(p.t.position) / side * size); layRad.Add(38f); layEdge.Add(false);
        }
        if (spreadMarkers) Relax(layPos, layRad, layEdge, 0f);
        for (int i = 0; i < layPois.Count; i++) layPois[i].big.anchoredPosition = layPos[i];
    }
    static void Ping(Image ping, float k) { if (ping == null) return; ping.rectTransform.localScale = Vector3.one * (1f + 0.9f * k); var c = ping.color; c.a = 0.7f * (1f - k); ping.color = c; }
    void UpdateZone(float dt)
    {
        zoneTimer -= dt; if (zoneTimer <= 0f) { zoneTimer = 0.2f; MapZone z = null; string now = defaultZone; Vector3 pp = player.position; if (useZones && MapZone.All.Count > 0) { foreach (var c in MapZone.All) { if (c == null || !c.Contains(pp)) continue; if (z == null || c.priority > z.priority || (c.priority == z.priority && c.Area < z.Area)) z = c; } if (z != null) now = z.zoneName; } else { float best = float.MaxValue; foreach (var p in pois) { if (p.t == null) continue; Vector3 d = p.t.position - pp; d.y = 0f; float dist = d.magnitude; float r = Mathf.Max(0.5f, p.kind.zoneRadius); if (dist <= r && dist / r < best) { best = dist / r; now = p.label; } } } if (now != zoneNow || z != currentZone) { zoneNow = now; currentZone = z; zonePunch = 0f; if (zoneLabel != null) { zoneLabel.text = now; zoneLabel.color = z != null ? Color.Lerp(Color.white, z.color, 0.45f) : Color.white; } announcePending = announceZones && z != null && z.announce; } if (announcePending && currentZone != null && Time.unscaledTime > 2.8f) { announcePending = false; bool first = !discovered.Contains(currentZone); DiscoverZone(currentZone); string cap = !string.IsNullOrEmpty(currentZone.caption) ? currentZone.caption : (first ? "НОВАЯ ЗОНА" : "ЗОНА"); ZoneToast.Show(currentZone.zoneName, cap, currentZone.color); } }
        if (zoneBanner != null && zonePunch < 1f) { zonePunch = Mathf.Min(1f, zonePunch + dt / 0.3f); zoneBanner.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(zonePunch * Mathf.PI)); }
    }
}
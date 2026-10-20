// Assets/_Project/Scripts/UI/menu/MainMenuBackground.cs (НЕ в Editor)
// Шаг 3: фон главного меню. Строит Canvas (Screen Space Overlay, Scale With Screen Size 1920x1080) и один RectTransform "Art"
// с картинкой на весь экран (AspectRatioFitter, Envelope Parent). Pivot по X сдвинут к левому краю (pivotX, около 0.3):
// при обрезке на узких экранах вывеска остаётся в кадре, режется правый край (хижина).
//
// ВСЕ эффекты и надписи делаем детьми Art и привязываем к точкам (GetAnchor): точка лежит по нормализованным координатам картинки
// (x вправо, y ВНИЗ от левого верхнего угла, 0..1), поэтому совпадает с рисунком на любых пропорциях экрана.
//
// Отладка точек: F1 в игре (или галочка Debug Markers) рисует красные маркеры. Маркер можно ТАСКАТЬ мышью в Game view:
// координаты меняются в списке Points в инспекторе и печатаются в Console при отпускании кнопки.
// Изменения в Play Mode после выхода пропадают: перепиши числа в Points в обычном режиме или скопируй компонент
// (ПКМ по заголовку компонента > Copy Component, выйти из Play, ПКМ > Paste Component Values).
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuBackground : MonoBehaviour
{
    public static MainMenuBackground Instance { get; private set; }

    [Serializable]
    public class ArtPoint
    {
        public string id;
        public Vector2 pos;                   // x вправо, y вниз, доли 0..1 от размера картинки
        public ArtPoint(string id, float x, float y) { this.id = id; pos = new Vector2(x, y); }
    }

    [Header("Картинка")]
    public Sprite background;
    [Range(0f, 1f)] [Tooltip("0 = обрезается только левый край, 1 = только правый. 0.3: справа режется больше")]
    public float pivotX = 0.3f;

    [Header("Отладка")]
    [Tooltip("Красные маркеры точек привязки. В игре переключается клавишей F1")]
    public bool debugMarkers;

    [Tooltip("Точки привязки эффектов (доли 0..1 от левого верхнего угла картинки)")]
    public ArtPoint[] points = DefaultPoints();

    static ArtPoint[] DefaultPoints()
    {
        return new[]
        {
            new ArtPoint("campfire",     0.74f, 0.64f),
            new ArtPoint("sun",          0.56f, 0.39f),
            new ArtPoint("airship",      0.66f, 0.20f),
            new ArtPoint("chimney",      0.93f, 0.12f),
            new ArtPoint("lantern_left", 0.06f, 0.50f),
            new ArtPoint("lantern_right",0.89f, 0.69f),
            new ArtPoint("window_1",     0.93f, 0.32f),
            new ArtPoint("window_2",     0.88f, 0.46f),
            new ArtPoint("window_3",     0.98f, 0.46f),
            new ArtPoint("lantern_hut",  0.95f, 0.44f),   // фонарь у двери хижины (в ТЗ его не было, добавил на всякий случай)
        };
    }

    // ------------------------------------------------------------ состояние
    Canvas canvas;
    RectTransform art;
    Image artImg;
    readonly Dictionary<string, RectTransform> anchors = new Dictionary<string, RectTransform>();
    readonly List<RectTransform> anchorList = new List<RectTransform>();      // по индексу points
    readonly List<TMP_Text> markerLabels = new List<TMP_Text>();
    readonly List<GameObject> markerObjects = new List<GameObject>();
    bool markersBuilt, markersShown;
    int dragIndex = -1;
    static Sprite circle;

    // корень картинки: сюда кладём ВСЕ эффекты и надписи
    public RectTransform Art { get { return art; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; circle = null; }

    void Awake()
    {
        Instance = this;
        LockLandscape();
        EnsureBuilt();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Только горизонтальная ориентация (на телефоне). Настройка остаётся после выхода из меню: игра тоже горизонтальная.
    static void LockLandscape()
    {
        if (!Application.isMobilePlatform) return;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    public void EnsureBuilt()
    {
        if (art != null) return;

        var cgo = new GameObject("MenuArtCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        var go = new GameObject("Art", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        go.transform.SetParent(cgo.transform, false);
        art = (RectTransform)go.transform;
        art.pivot = new Vector2(pivotX, 0.5f);

        artImg = go.GetComponent<Image>();
        artImg.sprite = background;
        artImg.type = Image.Type.Simple;
        artImg.preserveAspect = false;          // прямоугольник уже нужной пропорции
        artImg.raycastTarget = false;
        artImg.color = background != null ? Color.white : new Color(0.11f, 0.16f, 0.23f, 1f);
        if (background == null) Debug.LogWarning("MainMenuBackground: не назначена картинка Background. Запусти Tools > Timber Wings > Шаг 3.");

        var fit = go.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = background != null ? background.rect.width / background.rect.height : 1672f / 940f;

        BuildAnchors();
    }

    void BuildAnchors()
    {
        anchors.Clear(); anchorList.Clear();
        for (int i = 0; i < points.Length; i++)
        {
            var go = new GameObject("Anchor_" + points[i].id, typeof(RectTransform));
            go.transform.SetParent(art, false);
            var r = (RectTransform)go.transform;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.zero;
            anchorList.Add(r);
            if (!anchors.ContainsKey(points[i].id)) anchors[points[i].id] = r;
            Place(i);
        }
    }

    void Place(int i)
    {
        if (i < 0 || i >= anchorList.Count) return;
        var r = anchorList[i];
        Vector2 p = points[i].pos;
        r.anchorMin = r.anchorMax = new Vector2(p.x, 1f - p.y);   // y в списке считается сверху, в Unity снизу
        r.anchoredPosition = Vector2.zero;
        if (i < markerLabels.Count && markerLabels[i] != null)
            markerLabels[i].text = points[i].id + "  (" + p.x.ToString("0.000") + ", " + p.y.ToString("0.000") + ")";
    }

    // Точка привязки по id ("campfire", "sun", "airship", "chimney", "lantern_left", "lantern_right", "window_1..3", "lantern_hut").
    // Делай эффект ребёнком этой точки: он будет стоять на нужном месте картинки на любом экране.
    public RectTransform GetAnchor(string id)
    {
        EnsureBuilt();
        RectTransform r;
        if (anchors.TryGetValue(id, out r)) return r;
        Debug.LogWarning("MainMenuBackground: нет точки «" + id + "»");
        return art;
    }

    // правки в инспекторе в Play Mode двигают точки сразу
    void OnValidate()
    {
        if (art == null) return;
        art.pivot = new Vector2(pivotX, 0.5f);
        for (int i = 0; i < points.Length && i < anchorList.Count; i++) Place(i);
    }

    // ------------------------------------------------------------ отладочные маркеры
    void Update()
    {
        if (art == null) return;
        if (Input.GetKeyDown(KeyCode.F1)) debugMarkers = !debugMarkers;
        if (debugMarkers != markersShown) SetMarkers(debugMarkers);
        if (!markersShown) return;
        Drag();
    }

    void SetMarkers(bool on)
    {
        if (on && !markersBuilt) BuildMarkers();
        for (int i = 0; i < markerObjects.Count; i++) if (markerObjects[i] != null) markerObjects[i].SetActive(on);
        markersShown = on;
        dragIndex = -1;
    }

    static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[n * n];
            float c = (n - 1) * 0.5f, rad = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    cols[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(rad - d + 0.5f));
                }
            tex.SetPixels(cols); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            circle.name = "MenuDebugCircle";
            return circle;
        }
    }

    void BuildMarkers()
    {
        markersBuilt = true;
        for (int i = 0; i < anchorList.Count; i++)
        {
            var ring = UiKit.MakeImage(anchorList[i], "Marker", Circle, false);
            ring.rectTransform.sizeDelta = new Vector2(30f, 30f);
            ring.color = Color.white;
            var dot = UiKit.MakeImage(ring.transform, "Dot", Circle, false);
            dot.rectTransform.sizeDelta = new Vector2(20f, 20f);
            dot.color = new Color(0.95f, 0.15f, 0.15f, 1f);
            markerObjects.Add(ring.gameObject);

            var holder = new GameObject("Text", typeof(RectTransform));
            holder.transform.SetParent(ring.transform, false);
            var hr = (RectTransform)holder.transform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
            hr.pivot = new Vector2(0f, 0.5f);
            hr.anchoredPosition = new Vector2(24f, 0f);
            hr.sizeDelta = new Vector2(420f, 34f);
            var lbl = UiKit.MakeLabel(hr, "", 24f, new Color(1f, 0.95f, 0.5f, 1f), TextAlignmentOptions.Left, true);
            lbl.outlineWidth = 0.3f;
            lbl.outlineColor = new Color32(0, 0, 0, 255);
            markerLabels.Add(lbl);
            Place(i);
        }
    }

    void Drag()
    {
        Vector2 mp = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            dragIndex = -1;
            float best = 44f * 44f;
            for (int i = 0; i < anchorList.Count; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, anchorList[i].position);
                float d = (sp - mp).sqrMagnitude;
                if (d < best) { best = d; dragIndex = i; }
            }
        }
        else if (Input.GetMouseButton(0) && dragIndex >= 0)
        {
            Vector2 lp;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(art, mp, null, out lp))
            {
                Rect r = art.rect;
                float u = Mathf.Clamp01(Mathf.InverseLerp(r.xMin, r.xMax, lp.x));
                float v = 1f - Mathf.Clamp01(Mathf.InverseLerp(r.yMin, r.yMax, lp.y));
                points[dragIndex].pos = new Vector2(Mathf.Round(u * 1000f) / 1000f, Mathf.Round(v * 1000f) / 1000f);
                Place(dragIndex);
            }
        }
        else if (Input.GetMouseButtonUp(0) && dragIndex >= 0)
        {
            dragIndex = -1;
            var sb = new StringBuilder("MainMenuBackground: текущие точки (x, y от левого верхнего угла):\n");
            for (int i = 0; i < points.Length; i++)
                sb.AppendLine(points[i].id + " = (" + points[i].pos.x.ToString("0.000") + ", " + points[i].pos.y.ToString("0.000") + ")");
            Debug.Log(sb.ToString());
        }
    }
}

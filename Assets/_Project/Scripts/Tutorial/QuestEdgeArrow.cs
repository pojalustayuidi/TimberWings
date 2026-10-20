// Assets/_Project/Scripts/Tutorial/QuestEdgeArrow.cs (НЕ в Editor)
// Указатель к цели. Цель задаётся извне: QuestEdgeArrow.Target = transform (это делают IntroFlow и StoryFlow).
//   Цель за кадром: круглый значок со стрелкой у края экрана, поворачивается к цели, под ним расстояние.
//   Цель в кадре, но дальше pinMinDistance: золотая булавка подпрыгивает над целью (раньше в кадре подсказки не было вообще).
//   Цель рядом: ничего не рисуем, игрок уже на месте.
// Если Target пуст, раз в полсекунды ищется стройплощадка в состоянии Next (как раньше).
// Позиция считается по «спокойной» камере (без дыхания и тряски), поэтому метки не дрожат.
// Вместе с ним на тот же объект ставится QuestGuide: подсказки, дорожка по земле и взгляд камеры к цели.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestEdgeArrow : MonoBehaviour
{
    public static Transform Target { get; set; }

    [Header("Экран")]
    public float edgeMargin = 90f;       // насколько значок отступает от края (px на холсте 1920x1080)
    public float hideInside = 50f;       // цель ближе этого к краю считается «на экране»

    [Header("Булавка над целью")]
    public bool showPin = true;
    public float pinMinDistance = 4f;    // ближе этого булавку не показываем
    public Vector3 pinOffset = new Vector3(0f, 3.2f, 0f);

    class Marker
    {
        public RectTransform rt, art;
        public CanvasGroup cg;
        public TMP_Text txt;
        public float alpha;
        public Vector2 pos;
        public bool placed;
    }

    Camera cam;
    Transform player, fallback, lastTarget;
    RectTransform canvasRt;
    Marker edge, pin;
    float scanAt, popT;
    int lastMeters = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Target = null; }

    void Awake()
    {
        Create();
        if (GetComponent<QuestGuide>() == null) gameObject.AddComponent<QuestGuide>();
    }

    void OnDestroy()
    {
        if (canvasRt != null) Destroy(canvasRt.gameObject);
    }

    // ------------------------------------------------------------ создание
    void Create()
    {
        // свой холст: не зависим от имён объектов в сцене
        var cgo = new GameObject("QuestArrow_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        TMP_FontAsset font = null;
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) font = UIManager.Instance.objectiveText.font;

        edge = NewMarker("EdgeBadge", VibeGfx.Badge, new Vector2(72f, 72f), new Vector2(0.5f, 0.5f), new Vector2(0f, -56f), font);
        pin = NewMarker("TargetPin", VibeGfx.Pin, new Vector2(64f, 64f), new Vector2(0.5f, 4f / 64f), new Vector2(0f, 84f), font);
    }

    Marker NewMarker(string name, Sprite sprite, Vector2 size, Vector2 artPivot, Vector2 textPos, TMP_FontAsset font)
    {
        var m = new Marker();
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = 5;
        go.transform.SetParent(canvasRt, false);
        m.rt = (RectTransform)go.transform;
        m.rt.anchorMin = m.rt.anchorMax = new Vector2(0.5f, 0.5f);
        m.rt.sizeDelta = Vector2.zero;
        m.cg = go.GetComponent<CanvasGroup>();
        m.cg.blocksRaycasts = false;
        m.cg.interactable = false;

        var ago = new GameObject("Art", typeof(RectTransform), typeof(Image));
        ago.layer = 5;
        ago.transform.SetParent(m.rt, false);
        m.art = (RectTransform)ago.transform;
        m.art.sizeDelta = size;
        m.art.pivot = artPivot;
        var im = ago.GetComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = false;

        var tgo = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        tgo.layer = 5;
        tgo.transform.SetParent(m.rt, false);
        var tr = (RectTransform)tgo.transform;
        tr.anchoredPosition = textPos;
        tr.sizeDelta = new Vector2(140f, 30f);
        var t = tgo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = 26f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.outlineColor = new Color32(0x1C, 0x2A, 0x3A, 255);
        t.outlineWidth = 0.3f;
        m.txt = t;

        go.SetActive(false);
        return m;
    }

    // ------------------------------------------------------------ цель
    Transform ResolveTarget()
    {
        if (Target != null) { fallback = null; return Target; }

        if (Time.unscaledTime >= scanAt)
        {
            scanAt = Time.unscaledTime + 0.5f;
            fallback = null;
            foreach (var v in FindObjectsByType<BuildSiteTagView>(FindObjectsSortMode.None))
                if (v.state == BuildSiteTagView.State.Next) { fallback = v.transform; break; }
        }
        return fallback;
    }

    // проекция по спокойной камере; запасной путь: обычная
    Vector3 Project(Vector3 world)
    {
        Vector3 s;
        if (CameraFollow.TryWorldToScreenStable(world, out s)) return s;
        return cam.WorldToScreenPoint(world);
    }

    // ------------------------------------------------------------ каждый кадр
    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Transform tgt = ResolveTarget();
        bool have = tgt != null && tgt.gameObject.activeInHierarchy;
        if (cam == null) cam = Camera.main;
        if (!have || cam == null)
        {
            lastTarget = null;
            Fade(edge, false, dt);
            Fade(pin, false, dt);
            return;
        }

        if (tgt != lastTarget) { lastTarget = tgt; popT = 0f; edge.placed = pin.placed = false; }
        popT += dt;

        Vector3 sp = Project(tgt.position);
        bool off = sp.z < 0f || sp.x < hideInside || sp.x > Screen.width - hideInside
                   || sp.y < hideInside || sp.y > Screen.height - hideInside;

        float dist = Distance(tgt);
        int m = Mathf.RoundToInt(dist);
        if (m != lastMeters)      // строка собирается только когда число поменялось, а не каждый кадр
        {
            lastMeters = m;
            string label = m + " м";
            edge.txt.text = label;
            pin.txt.text = label;
        }

        bool wantEdge = off;
        bool wantPin = !off && showPin && dist > pinMinDistance;

        if (wantEdge)
        {
            Vector2 c = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 d = (Vector2)sp - c;
            if (sp.z < 0f) d = -d;                     // цель за спиной: стрелка в противоположную сторону
            if (d.sqrMagnitude < 1f) d = Vector2.up;
            d.Normalize();

            float scale = canvasRt.localScale.x > 0.0001f ? canvasRt.localScale.x : 1f;   // px экрана на единицу холста
            float mx = edgeMargin * scale;
            float tx = (Screen.width * 0.5f - mx) / Mathf.Max(0.01f, Mathf.Abs(d.x));
            float ty = (Screen.height * 0.5f - mx) / Mathf.Max(0.01f, Mathf.Abs(d.y));
            Vector2 p = c + d * Mathf.Min(tx, ty);

            Place(edge, p, dt, 0f);
            edge.art.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        }

        if (wantPin)
        {
            Vector3 sp2 = Project(tgt.position + pinOffset);
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 18f;   // подпрыгивает, а не качается
            Place(pin, new Vector2(sp2.x, sp2.y), dt, bob);
        }

        Fade(edge, wantEdge, dt);
        Fade(pin, wantPin, dt);

        // появление с «щелчком»: новая цель выпрыгивает, а не просто возникает
        float pop = UiDraw.EaseOutBack(Mathf.Clamp01(popT / 0.35f));
        float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f);
        edge.rt.localScale = Vector3.one * (pop * pulse);
        pin.rt.localScale = Vector3.one * pop;
    }

    // расстояние считаем от игрока (камера высоко, от неё цифры были бы завышены)
    float Distance(Transform tgt)
    {
        if (player == null)
        {
            var pm = PlayerMovement.Instance;
            if (pm != null) player = pm.transform;
        }
        Vector3 from = player != null ? player.position : cam.transform.position;
        Vector3 d = tgt.position - from;
        d.y = 0f;
        return d.magnitude;
    }

    // экранная точка -> позиция на холсте, с плавным подтягиванием
    void Place(Marker mk, Vector2 screenPoint, float dt, float lift)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, null, out lp);
        lp.y += lift;
        if (!mk.placed) { mk.pos = lp; mk.placed = true; }
        else mk.pos = Vector2.Lerp(mk.pos, lp, 1f - Mathf.Exp(-16f * dt));
        mk.rt.anchoredPosition = mk.pos;
    }

    void Fade(Marker mk, bool on, float dt)
    {
        mk.alpha = Mathf.MoveTowards(mk.alpha, on ? 1f : 0f, dt * 5f);
        mk.cg.alpha = mk.alpha;
        bool vis = mk.alpha > 0.01f;
        if (mk.rt.gameObject.activeSelf != vis) mk.rt.gameObject.SetActive(vis);
        if (!on && !vis) mk.placed = false;
    }
}

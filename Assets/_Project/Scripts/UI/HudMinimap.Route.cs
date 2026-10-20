// Assets/_Project/Scripts/UI/HudMinimap.Route.cs (НЕ в Editor)
// Пунктирный маршрут от игрока до текущей цели (QuestEdgeArrow.Target), мигающая метка цели и след игрока (последние trailSeconds секунд).
// Рисуется на мини- и большой карте линиями MapLineGraphic (один меш на линию, без спрайтов). NavMesh в проекте нет: маршрут идёт по прямой,
// как дорожка QuestGuide. Штрихи «бегут» к цели. Метка цели за краем мини-карты прижимается к кромке круга.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Маршрут и след")]
    public bool showRoute = true;
    public Color routeColor = new Color32(0xFF, 0xD6, 0x6B, 255);
    [Tooltip("Толщина маршрута, px экрана (на большой карте не меняется при зуме)")]
    public float routeThickness = 4f;
    public bool showTrail = true;
    public float trailSeconds = 20f;
    public Color trailColor = new Color(1f, 1f, 1f, 0.8f);
    public float trailThickness = 2.4f;

    MapLineGraphic miniRoute, bigRoute, miniTrail, bigTrail;
    Image miniTarget, bigTarget;
    float routeT;                       // 0..1: сдвиг штрихов в долях периода
    float routeShownT = -1f;
    readonly List<Vector2> trailPos = new List<Vector2>(96);
    readonly List<float> trailTime = new List<float>(96);
    float trailClock, trailTimer;
    float lineZoomApplied = -1f;
    Transform lastTarget;

    // ------------------------------------------------------------ после печати карты
    partial void OnMapBakedExt(bool first)
    {
        if (first) EnsureLayers();
        SyncFogZones();
        bigScaleDirty = true;
    }

    void EnsureLayers()
    {
        EnsureFogLayers();
        if (miniRoute == null)
        {
            var parent = miniMarkers != null ? miniMarkers : miniMask;
            if (parent != null)
            {
                miniTrail = MakeLine(parent, "Trail", MapLineGraphic.Mode.Trail, trailColor, trailThickness);
                miniRoute = MakeLine(parent, "Route", MapLineGraphic.Mode.Dashed, routeColor, routeThickness);
                miniRoute.transform.SetAsFirstSibling();
                miniTrail.transform.SetAsFirstSibling();                  // след под маршрутом, оба под метками
                miniTarget = MakeTargetMark(parent, 34f);
            }
        }
        if (bigRoute == null && bigMarkers != null)
        {
            bigTrail = MakeLine(bigMarkers, "Trail", MapLineGraphic.Mode.Trail, trailColor, trailThickness);
            bigRoute = MakeLine(bigMarkers, "Route", MapLineGraphic.Mode.Dashed, routeColor, routeThickness);
            bigRoute.transform.SetAsFirstSibling();
            bigTrail.transform.SetAsFirstSibling();
            bigTarget = MakeTargetMark(bigMarkers, 44f);
            lineZoomApplied = -1f;
        }
    }

    static MapLineGraphic MakeLine(RectTransform parent, string name, MapLineGraphic.Mode mode, Color col, float th)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);                                 // начало координат линии = центр родителя, как у меток
        var g = go.AddComponent<MapLineGraphic>();
        g.mode = mode; g.color = col; g.thickness = th;
        g.raycastTarget = false;
        g.enabled = false;
        return g;
    }

    // кольцо + мягкое свечение + точка: мигают вместе
    static Image MakeTargetMark(RectTransform parent, float size)
    {
        var root = NewImage("TargetMark", parent, MapIcons.Ring, size);
        root.color = new Color32(0xFF, 0xD6, 0x6B, 255);
        var glow = NewImage("Glow", (RectTransform)root.transform, MapIcons.SoftCircle, size * 2.3f);
        glow.color = new Color(0.95f, 0.69f, 0.2f, 0.5f);
        glow.transform.SetAsFirstSibling();
        var dot = NewImage("Dot", (RectTransform)root.transform, MapIcons.Get(MapIcons.Id.Dot), size * 0.5f);
        dot.color = Color.white;
        root.gameObject.SetActive(false);
        return root;
    }

    static Image NewImage(string name, RectTransform parent, Sprite sp, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size);
        var im = go.GetComponent<Image>();
        im.sprite = sp; im.raycastTarget = false; im.preserveAspect = true;
        return im;
    }

    // ------------------------------------------------------------ каждый кадр
    void UpdateExtras(float dt, float time, Vector2 pm)
    {
        if (!mapReady) return;
        if (openT > 0.001f) UpdateBigUi(dt, time);
        UpdateFog(dt);
        UpdateTrail(pm);
        UpdateRoute(dt, time, pm);
    }

    void UpdateTrail(Vector2 pm)
    {
        trailClock += Time.deltaTime;
        trailTimer += Time.deltaTime;
        if (trailTimer >= 0.25f)
        {
            trailTimer = 0f;
            int n = trailPos.Count;
            if (n > 0 && (pm - trailPos[n - 1]).magnitude > 25f) { trailPos.Clear(); trailTime.Clear(); n = 0; }   // телепорт: старый след не тянем
            if (n == 0 || (pm - trailPos[n - 1]).sqrMagnitude > 0.35f * 0.35f) { trailPos.Add(pm); trailTime.Add(trailClock); }
        }
        while (trailTime.Count > 0 && trailClock - trailTime[0] > trailSeconds) { trailPos.RemoveAt(0); trailTime.RemoveAt(0); }
    }

    void FillTrail(MapLineGraphic g, Vector2 pm, float scale, bool relative)
    {
        g.points.Clear(); g.alphas.Clear();
        int n = trailPos.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = relative ? (trailPos[i] - pm) * scale : trailPos[i] * scale;
            g.points.Add(p);
            g.alphas.Add(Mathf.Clamp01(1f - (trailClock - trailTime[i]) / trailSeconds) * 0.9f);
        }
        g.points.Add(relative ? Vector2.zero : pm * scale);                  // живой конец у игрока
        g.alphas.Add(1f);
        g.Refresh();
    }

    void UpdateRoute(float dt, float time, Vector2 pm)
    {
        routeT = Mathf.Repeat(routeT - dt * 1.2f, 1f);                       // штрихи бегут к цели

        Transform tgt = showRoute ? QuestEdgeArrow.Target : null;
        bool has = tgt != null && player != null;
        Vector2 tm = Vector2.zero;
        if (has)
        {
            Vector3 d = tgt.position - player.position; d.y = 0f;
            if (d.magnitude < 3f) has = false;                                // уже на месте
            else tm = ToMap(tgt.position);
        }
        if (tgt != lastTarget) { lastTarget = tgt; routeShownT = -1f; }

        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5.5f);

        // ---- мини-карта
        if (miniMask != null && miniRoute != null)
        {
            float diameter = miniMask.rect.width;
            float ppu = diameter / ViewWorld;
            float maxR = diameter * 0.5f - 14f;

            if (showTrail && trailPos.Count > 0) { miniTrail.enabled = true; miniTrail.thickness = trailThickness; miniTrail.color = trailColor; FillTrail(miniTrail, pm, ppu, true); }
            else miniTrail.enabled = false;

            miniRoute.enabled = has;
            if (has)
            {
                Vector2 rel = (tm - pm) * ppu;
                miniRoute.points.Clear();
                miniRoute.points.Add(Vector2.zero);
                miniRoute.points.Add(rel);
                miniRoute.thickness = routeThickness; miniRoute.dash = 14f; miniRoute.gap = 10f; miniRoute.outlinePad = 1.6f;
                miniRoute.phase = routeT * 24f;
                miniRoute.Refresh();
            }
            PlaceTarget(miniTarget, has, has ? (tm - pm) * ppu : Vector2.zero, maxR, pulse, 1f);
        }

        // ---- большая карта
        if (bigRoute != null && openT > 0.001f && bigMap != null)
        {
            float size = bigMap.rectTransform.rect.width;
            float k = size / side;
            float z = Mathf.Max(1f, zoom);
            ApplyLineZoom(z);

            if (showTrail && trailPos.Count > 0) { bigTrail.enabled = true; FillTrail(bigTrail, pm, k, false); }
            else bigTrail.enabled = false;

            bigRoute.enabled = has;
            if (has)
            {
                bigRoute.points.Clear();
                bigRoute.points.Add(pm * k);
                bigRoute.points.Add(tm * k);
                float period = (14f + 10f) / z;
                bigRoute.phase = routeT * period;
                bigRoute.Refresh();
            }
            PlaceTarget(bigTarget, has, tm * k, 0f, pulse, 1f / Mathf.Pow(z, 0.85f));
        }
        else if (bigTarget != null && bigTarget.gameObject.activeSelf && openT <= 0.001f) bigTarget.gameObject.SetActive(false);
    }

    // толщина и штрихи большой карты постоянны на экране: делим на зум
    void ApplyLineZoom(float z)
    {
        if (Mathf.Abs(z - lineZoomApplied) < 0.002f) return;
        lineZoomApplied = z;
        float inv = 1f / z;
        bigRoute.thickness = routeThickness * 1.15f * inv; bigRoute.dash = 14f * inv; bigRoute.gap = 10f * inv; bigRoute.outlinePad = 1.6f * inv;
        bigTrail.thickness = trailThickness * 1.2f * inv;
        bigRoute.Refresh(); bigTrail.Refresh();
    }

    // maxR > 0: цель за кругом прижимается к кромке (мини-карта)
    void PlaceTarget(Image mark, bool has, Vector2 pos, float maxR, float pulse, float baseScale)
    {
        if (mark == null) return;
        if (!has) { if (mark.gameObject.activeSelf) mark.gameObject.SetActive(false); return; }
        if (!mark.gameObject.activeSelf) { mark.gameObject.SetActive(true); mark.transform.SetAsLastSibling(); }
        float s = baseScale;
        if (maxR > 0f)
        {
            float mag = pos.magnitude;
            if (mag > maxR) { pos = pos / mag * maxR; s *= 0.8f; }
        }
        var rt = mark.rectTransform;
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one * (s * (0.9f + 0.3f * pulse));
        var c = mark.color; c.a = 0.55f + 0.45f * pulse; mark.color = c;
        var glow = rt.GetChild(0).GetComponent<Image>();
        if (glow != null) { var g = glow.color; g.a = 0.2f + 0.5f * pulse; glow.color = g; }
    }
}

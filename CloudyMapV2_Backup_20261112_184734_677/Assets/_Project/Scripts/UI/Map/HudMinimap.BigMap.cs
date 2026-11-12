// Assets/_Project/Scripts/UI/Map/HudMinimap.BigMap.cs (НЕ в Editor)
// Большая карта: зум (кнопки +/-, колесо мыши, щипок двумя пальцами), перетаскивание, облачная рамка (Candy), компас.
// Как устроено: карта, зоны, туман, метки и игрок переезжают в один контейнер BigContent внутри BigViewport (RectMask2D).
// Зум и сдвиг меняют только масштаб и позицию контейнера, поэтому координаты меток (HudMinimap.cs) не меняются.
// Метки, подписи зон и линии подгоняются по размеру (ApplyBigScale), чтобы на экране оставались читаемыми.
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Большая карта: управление")]
    public bool codeFrame = true;                    // облачная рамка из MapIcons.Frame (стиль Candy)
    public bool codeCompass = true;                  // компас из MapIcons.Compass
    public bool zoomButtons = true;
    public float minZoom = 1f, maxZoom = 4f;
    [Tooltip("Метки на карте и в мини-карте крупнее (720p: значок 36 px превращается в 24)")]
    [Range(0.8f, 2f)] public float markerScale = 1.15f;
    [Tooltip("Подписи меток на большой карте появляются при таком зуме и выше (на 1x они слипаются в кучу). 0 = всегда")]
    public float labelsFromZoom = 1.7f;
    [Tooltip("Большая карта рисуется отдельным слоем поверх всего HUD (крестик и +/- всегда нажимаются)")]
    public bool bigOnTop = true;
    public int bigSortingOrder = 40;

    RectTransform bigViewport, bigContent;
    Button zoomInBtn, zoomOutBtn;
    float zoom = 1f, zoomTarget = 1f, appliedZoom = -1f;
    Vector2 pan, panTarget;
    bool bigScaleDirty = true;
    float bigPlayerBase = 1f;
    float pinchPrev;

    // ------------------------------------------------------------ сборка (до Rescan, чтобы слой зон попал в контейнер)
    void InitBigMapUi()
    {
        if (bigMap == null || bigMap.transform.parent == null) return;
        var parent = (RectTransform)bigMap.transform.parent;
        var mr = bigMap.rectTransform;
        int layer = bigMap.gameObject.layer;

        var vgo = new GameObject("BigViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        vgo.layer = layer;
        bigViewport = (RectTransform)vgo.transform;
        bigViewport.SetParent(parent, false);
        bigViewport.anchorMin = mr.anchorMin; bigViewport.anchorMax = mr.anchorMax; bigViewport.pivot = mr.pivot;
        bigViewport.anchoredPosition = mr.anchoredPosition; bigViewport.sizeDelta = mr.sizeDelta;
        bigViewport.SetSiblingIndex(mr.GetSiblingIndex());
        var hit = vgo.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;          // ловит перетаскивание и колесо
        var surf = vgo.AddComponent<MapDragSurface>(); surf.owner = this;

        var cgo = new GameObject("BigContent", typeof(RectTransform));
        cgo.layer = layer;
        bigContent = (RectTransform)cgo.transform;
        bigContent.SetParent(bigViewport, false);
        bigContent.anchorMin = Vector2.zero; bigContent.anchorMax = Vector2.one;
        bigContent.offsetMin = Vector2.zero; bigContent.offsetMax = Vector2.zero;
        bigContent.pivot = new Vector2(0.5f, 0.5f);

        // переносим в прежнем порядке: карта, затем метки (и игрок, если он рядом с метками)
        var list = new System.Collections.Generic.List<RectTransform>();
        list.Add(mr);
        if (bigMarkers != null && bigMarkers.parent == parent) list.Add(bigMarkers);
        if (bigPlayer != null && bigPlayer.parent == parent) list.Add(bigPlayer);
        list.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        foreach (var r in list)
        {
            r.SetParent(bigContent, false);
            if (r == bigPlayer) continue;                                         // игрок остаётся с якорями центра
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
        if (bigPlayer != null) bigPlayerBase = bigPlayer.localScale.x;

        BuildFrame(parent);
        if (codeCompass) StyleCompass();
        if (zoomButtons) BuildZoomButtons();
        LiftBigMapOverHud();
        RegisterClickFallbacks();
    }

    // клики по кнопкам карты не зависят от EventSystem (см. UiClickFallback)
    void RegisterClickFallbacks()
    {
        System.Func<bool> whenOpen = () => open && openT > 0.5f;
        System.Func<bool> whenClosed = () => !open;
        UiClickFallback.Register(closeButton, whenOpen, true);
        UiClickFallback.Register(zoomInBtn, whenOpen, true);
        UiClickFallback.Register(zoomOutBtn, whenOpen, true);
        UiClickFallback.Register(openButton, whenClosed, false);
        UiClickFallback.Register(miniZoomIn, whenClosed, false);
        UiClickFallback.Register(miniZoomOut, whenClosed, false);
    }

    // Крестик и +/- не нажимались, потому что что-то из HUD (круг мини-карты, его кнопка, панели) лежало выше окна карты.
    // Окно большой карты получает свой Canvas с перекрытием порядка и свой GraphicRaycaster: оно всегда сверху и первым ловит клики.
    void LiftBigMapOverHud()
    {
        if (bigOnTop && bigGroup != null)
        {
            var cv = bigGroup.GetComponent<Canvas>();
            if (cv == null) cv = bigGroup.gameObject.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = bigSortingOrder;
            if (bigGroup.GetComponent<GraphicRaycaster>() == null) bigGroup.gameObject.AddComponent<GraphicRaycaster>();
        }
        if (closeButton != null)
        {
            var ct = closeButton.transform;
            // крестик не должен жить внутри контейнера карты: там он масштабировался бы вместе с зумом и прятался под маской
            if (bigViewport != null && ct.IsChildOf(bigViewport) && bigViewport.parent != null) ct.SetParent(bigViewport.parent, true);
            ct.SetAsLastSibling();                                                // поверх карты, рамки и компаса
            if (closeButton.targetGraphic != null) closeButton.targetGraphic.raycastTarget = true;
            closeButton.interactable = true;
        }
    }

    void BuildFrame(RectTransform parent)
    {
        if (!codeFrame) return;
        Image fi = bigFrame != null ? bigFrame.GetComponent<Image>() : null;
        if (fi == null)
        {
            var go = new GameObject("CandyFrame", typeof(RectTransform), typeof(Image));
            go.layer = bigViewport.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = bigViewport.anchorMin; r.anchorMax = bigViewport.anchorMax; r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = bigViewport.anchoredPosition;
            r.sizeDelta = bigViewport.sizeDelta + new Vector2(18f, 18f);        // рамка чуть шире карты
            fi = go.GetComponent<Image>();
            r.SetSiblingIndex(bigViewport.GetSiblingIndex() + 1);
        }
        else if (bigFrame.parent == parent && bigFrame.GetSiblingIndex() < bigViewport.GetSiblingIndex())
            bigFrame.SetSiblingIndex(bigViewport.GetSiblingIndex() + 1);        // рамка поверх карты
        fi.sprite = MapIcons.Frame;
        fi.type = Image.Type.Sliced;
        fi.color = Color.white;
        fi.raycastTarget = false;
    }

    void StyleCompass()
    {
        if (bigCompass == null) return;
        var ci = bigCompass.GetComponent<Image>();
        if (ci == null) ci = bigCompass.gameObject.AddComponent<Image>();
        ci.sprite = MapIcons.Compass;
        ci.type = Image.Type.Simple;
        ci.preserveAspect = true;
        ci.color = Color.white;
        ci.raycastTarget = false;
        if (bigCompass.sizeDelta.x < 64f) bigCompass.sizeDelta = new Vector2(84f, 84f);
    }

    void BuildZoomButtons()
    {
        zoomInBtn = MakeZoomButton("+", new Vector2(-30f, 98f));
        zoomOutBtn = MakeZoomButton("-", new Vector2(-30f, 30f));
        if (zoomInBtn != null) zoomInBtn.onClick.AddListener(ZoomIn);
        if (zoomOutBtn != null) zoomOutBtn.onClick.AddListener(ZoomOut);
    }

    Button MakeZoomButton(string label, Vector2 pos)
    {
        var go = new GameObject("Zoom" + (label == "+" ? "In" : "Out"), typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = bigViewport.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(bigViewport, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
        r.sizeDelta = new Vector2(58f, 58f);
        r.anchoredPosition = pos;
        var im = go.GetComponent<Image>();
        var b = go.GetComponent<Button>();
        SkinZoomButton(im, b, 0.7f);

        var tgo = new GameObject("Label", typeof(RectTransform));
        tgo.layer = go.layer;
        var tr = (RectTransform)tgo.transform;
        tr.SetParent(r, false);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var tx = tgo.AddComponent<TextMeshProUGUI>();
        SetLabelFont(tx);
        tx.text = label; tx.fontSize = 44f; tx.fontStyle = FontStyles.Bold;
        tx.alignment = TextAlignmentOptions.Center; tx.color = ZoomLabelColor; tx.raycastTarget = false;
        return b;
    }

    // ------------------------------------------------------------ управление
    public void ZoomIn() { ZoomAtLocal(1.4f, Vector2.zero); }
    public void ZoomOut() { ZoomAtLocal(1f / 1.4f, Vector2.zero); }

    void ResetBigView()
    {
        zoom = zoomTarget = 1f;
        pan = panTarget = Vector2.zero;
        pinchPrev = 0f;
        appliedZoom = -1f;
    }

    // p: точка в координатах окна карты от его центра. Точка под курсором остаётся на месте
    void ZoomAtLocal(float factor, Vector2 p)
    {
        float nz = Mathf.Clamp(zoomTarget * factor, minZoom, maxZoom);
        if (Mathf.Approximately(nz, zoomTarget)) return;
        Vector2 c = (p - panTarget) / zoomTarget;
        zoomTarget = nz;
        panTarget = ClampPan(p - c * zoomTarget, zoomTarget);
    }

    Vector2 ClampPan(Vector2 v, float z)
    {
        if (bigViewport == null) return v;
        Rect r = bigViewport.rect;
        float mx = (z - 1f) * r.width * 0.5f, my = (z - 1f) * r.height * 0.5f;
        return new Vector2(Mathf.Clamp(v.x, -mx, mx), Mathf.Clamp(v.y, -my, my));
    }

    bool ToLocal(Vector2 screen, Camera cam, out Vector2 local)
    {
        local = Vector2.zero;
        if (bigViewport == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bigViewport, screen, cam, out local)) return false;
        local -= bigViewport.rect.center;
        return true;
    }

    // из MapDragSurface
    public void DragScreen(Vector2 screenPos, Vector2 screenDelta, Camera cam)
    {
        Vector2 a, b;
        if (!ToLocal(screenPos - screenDelta, cam, out a) || !ToLocal(screenPos, cam, out b)) return;
        panTarget = ClampPan(panTarget + (b - a), zoomTarget);
        pan = panTarget;
    }

    Camera UiCam(RectTransform rt)
    {
        var cv = rt != null ? rt.GetComponentInParent<Canvas>() : null;
        return cv != null && cv.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? cv.rootCanvas.worldCamera : null;
    }

    void HandleKeysAndPinch()
    {
        // колесо мыши: считаем сами (событие не доходило, если сверху лежал чужой элемент), курсор должен быть над картой
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && bigViewport != null)
        {
            Vector2 mp;
            Rect vr = bigViewport.rect;
            if (ToLocal(Input.mousePosition, UiCam(bigViewport), out mp) && Mathf.Abs(mp.x) <= vr.width * 0.5f && Mathf.Abs(mp.y) <= vr.height * 0.5f)
                ZoomAtLocal(Mathf.Pow(1.2f, wheel), mp);
        }

        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) ZoomIn();
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus)) ZoomOut();
        if (Input.GetKeyDown(KeyCode.Home)) { zoomTarget = 1f; panTarget = Vector2.zero; }

        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
            float d = Vector2.Distance(t0.position, t1.position);
            if (pinchPrev > 1f && d > 1f)
            {
                Vector2 mid;
                if (!ToLocal((t0.position + t1.position) * 0.5f, null, out mid)) mid = Vector2.zero;
                ZoomAtLocal(d / pinchPrev, mid);
                zoom = zoomTarget; pan = panTarget;                                // щипок без сглаживания: палец не отстаёт
            }
            pinchPrev = d;
        }
        else pinchPrev = 0f;
    }

    partial void OnBigOpenedExt()
    {
        ResetBigView();
        bigScaleDirty = true;
    }

    // ------------------------------------------------------------ каждый кадр, пока карта открыта
    void UpdateBigUi(float dt, float time)
    {
        if (bigContent == null) return;
        if (open) HandleKeysAndPinch();

        float k = 1f - Mathf.Exp(-14f * dt);
        zoom = Mathf.Lerp(zoom, zoomTarget, k);
        if (Mathf.Abs(zoom - zoomTarget) < 0.002f) zoom = zoomTarget;
        pan = ClampPan(Vector2.Lerp(pan, panTarget, k), zoom);
        bigContent.localScale = new Vector3(zoom, zoom, 1f);
        bigContent.anchoredPosition = pan;

        if (bigScaleDirty || Mathf.Abs(zoom - appliedZoom) > 0.0005f) ApplyBigScale();
        if (zoomInBtn != null) zoomInBtn.interactable = zoomTarget < maxZoom - 0.01f;
        if (zoomOutBtn != null) zoomOutBtn.interactable = zoomTarget > minZoom + 0.01f;
    }

    // метки растут медленнее карты (степень 0.85), чтобы при сильном зуме не закрывать её
    void ApplyBigScale()
    {
        bigScaleDirty = false;
        appliedZoom = zoom;
        float inv = 1f / Mathf.Pow(Mathf.Max(1f, zoom), 0.85f);
        foreach (var p in pois)
            if (p.big != null)
            {
                p.big.localScale = Vector3.one * (1.25f * markerScale * inv);
                var lb = p.big.Find("Label");
                if (lb != null) lb.gameObject.SetActive(labelsFromZoom <= 0f || zoomTarget >= labelsFromZoom);
            }
        if (bigPlayer != null) bigPlayer.localScale = Vector3.one * (bigPlayerBase * inv);
        if (zoneLayer != null)
            for (int i = 0; i < zoneLayer.childCount; i++)
            {
                var c = zoneLayer.GetChild(i);
                if (c.name.StartsWith("Name_")) c.localScale = Vector3.one * inv;
            }
        LayoutZoneNames(inv);
    }

    // ------------------------------------------------------------ подписи зон не налезают друг на друга
    // Подпись стоит в центре своей зоны, а зоны острова лежат рядом (поляна, холм, лес), поэтому подписи слипались в кашу.
    // Базовые позиции запоминаются один раз после BuildZoneLayer; при каждом зуме подписи раздвигаются по вертикали заново
    // (их экранный размер постоянный, значит в координатах карты на большом зуме они занимают меньше места).
    readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> zoneNameBase = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
    readonly System.Collections.Generic.List<RectTransform> nameRt = new System.Collections.Generic.List<RectTransform>();
    readonly System.Collections.Generic.List<Vector2> namePos = new System.Collections.Generic.List<Vector2>();
    readonly System.Collections.Generic.List<Vector2> nameHalf = new System.Collections.Generic.List<Vector2>();

    void LayoutZoneNames(float inv)
    {
        if (zoneLayer == null) return;
        nameRt.Clear(); namePos.Clear(); nameHalf.Clear();
        for (int i = 0; i < zoneLayer.childCount; i++)
        {
            var c = zoneLayer.GetChild(i) as RectTransform;
            if (c == null || !c.name.StartsWith("Name_") || !c.gameObject.activeSelf) continue;
            Vector2 b;
            if (!zoneNameBase.TryGetValue(c, out b)) { b = c.anchoredPosition; zoneNameBase[c] = b; }
            var tx = c.GetComponent<TMP_Text>();
            float w = tx != null ? tx.preferredWidth : 200f, h = tx != null ? tx.preferredHeight : 36f;
            nameRt.Add(c); namePos.Add(b); nameHalf.Add(new Vector2((w * 0.5f + 6f) * inv, (h * 0.5f + 2f) * inv));
        }
        SpreadRects(namePos, nameHalf, 8);
        for (int i = 0; i < nameRt.Count; i++) nameRt[i].anchoredPosition = namePos[i];
    }

    // Прямоугольники, которые пересекаются, раздвигаются по вертикали (горизонтальное положение подписи остаётся у зоны)
    static void SpreadRects(System.Collections.Generic.List<Vector2> pos, System.Collections.Generic.List<Vector2> half, int passes)
    {
        int n = pos.Count;
        if (n < 2) return;
        for (int it = 0; it < passes; it++)
        {
            bool moved = false;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float ox = half[i].x + half[j].x - Mathf.Abs(pos[i].x - pos[j].x);
                    float oy = half[i].y + half[j].y - Mathf.Abs(pos[i].y - pos[j].y);
                    if (ox <= 0f || oy <= 0f) continue;
                    float dy = pos[j].y - pos[i].y;
                    float dir = Mathf.Abs(dy) < 0.01f ? ((j & 1) == 0 ? 1f : -1f) : Mathf.Sign(dy);
                    float push = oy * 0.5f + 0.5f;
                    pos[i] = new Vector2(pos[i].x, pos[i].y - dir * push);
                    pos[j] = new Vector2(pos[j].x, pos[j].y + dir * push);
                    moved = true;
                }
            if (!moved) break;
        }
    }
}

// Ловит перетаскивание и колесо мыши на окне большой карты. Метки и кнопки внутри окна передают события выше по иерархии
public class MapDragSurface : MonoBehaviour, IDragHandler, IScrollHandler, IBeginDragHandler
{
    public HudMinimap owner;
    public void OnBeginDrag(PointerEventData e) { }
    public void OnDrag(PointerEventData e)
    {
        if (owner == null || Input.touchCount > 1) return;                        // двумя пальцами работает щипок
        owner.DragScreen(e.position, e.delta, e.pressEventCamera);
    }
    public void OnScroll(PointerEventData e)
    {
        // колесо обрабатывается опросом в HudMinimap.HandleKeysAndPinch (иначе зум удвоился бы)
    }
}

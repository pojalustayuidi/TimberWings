using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    static readonly Color MapInk = new Color32(84, 53, 102, 255);
    static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    static readonly Vector2 TopRight = Vector2.one;
    static readonly Vector2 BottomRight = Vector2.right;
    static readonly Vector2 Middle = Vector2.one * .5f;
    static readonly Regex ObjectiveTags = new Regex("<[^>]*>");

    RectTransform cloudyHeader, cloudyClip, cloudyFrame, cloudyFooter, cloudyObjectiveCard;
    TMP_Text cloudyObjectiveTitle, cloudyObjectiveCaption;
    Button cloudyFocusPlayer, cloudyFocusQuest;
    readonly Image[] legendIcons = new Image[3];
    readonly TMP_Text[] legendLabels = new TMP_Text[3];
    Rect cloudyWindowArea;
    float cloudyUiUnit = 1f;
    string lastMapObjective;
    string mapObjective = "";

    void CloudyV3Initialize(RectTransform clip)
    {
        if (!UseCloudyV3 || bigWindow == null) return;
        cloudyClip = clip;
        cloudyFrame = bigFrame != null ? bigFrame : clip.Find("CandyFrame") as RectTransform;
        foreach (string name in new[] { "Shadow", "Outline", "Body", "Gloss", "Rim", "Ring", "Hint",
            "CloudyCornerLeft", "CloudyCornerRight" })
            if (bigWindow.Find(name) is Transform child) child.gameObject.SetActive(false);

        if (bigWindow.TryGetComponent<Image>(out var oldPanel)) oldPanel.enabled = false;
        if (bigWindow.TryGetComponent<HudShine>(out var shine)) shine.enabled = false;
        foreach (string name in new[] { "Vignette", "Ring" })
            if (clip.Find(name) is Transform overlay) overlay.gameObject.SetActive(false);

        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var surface = CloudyPanel(bigWindow, "CloudyV3Surface", CloudySprite("WindowPanel") ?? UiKit.Panel);
            StretchMapRect(surface);
            surface.SetAsFirstSibling();

            cloudyHeader = bigWindow.Find("CloudyHeader") as RectTransform;
            if (cloudyHeader == null)
                cloudyHeader = MakeMapText(bigWindow, "CloudyHeader", "Карта локации", 30f, TextAlignmentOptions.Center).rectTransform;
            cloudyHeader.SetAsLastSibling();

            cloudyObjectiveCard = CloudyPanel(bigWindow, "ObjectiveCard", cloudyPill ?? UiKit.Plaque);
            cloudyObjectiveCaption = MakeMapText(cloudyObjectiveCard, "Caption", "Текущая цель", 14f, TextAlignmentOptions.MidlineLeft);
            cloudyObjectiveCaption.color = new Color32(116, 78, 95, 255);
            cloudyObjectiveTitle = MakeMapText(cloudyObjectiveCard, "Objective", "", 20f, TextAlignmentOptions.MidlineLeft);
            cloudyObjectiveTitle.textWrappingMode = TextWrappingModes.Normal;
            cloudyObjectiveTitle.richText = false;

            cloudyFocusPlayer = MakeMapFocusButton("К Лео", UiBtn.Gray, FocusMapPlayer);
            cloudyFocusQuest = MakeMapFocusButton("К цели", UiBtn.Amber, FocusMapQuest);

            cloudyFooter = CloudyPanel(bigWindow, "CloudyV3Footer", CloudySprite("LegendBar") ?? UiKit.Panel);
            BuildMapLegend(0, "Лео", MapIcons.PlayerArrow);
            BuildMapLegend(1, "Задание", CloudySprite("IconQuest") ?? MapIcons.Get(MapIcons.Id.Board));
            BuildMapLegend(2, "Осмотр", CloudySprite("StateUnknown") ?? MapIcons.Get(MapIcons.Id.Mystery));
        }
        if (cloudyTooltip != null)
        {
            cloudyTooltip.SetParent(bigWindow, false);
            cloudyTooltip.SetAsLastSibling();
        }
    }

    RectTransform CloudyPanel(RectTransform parent, string name, Sprite sprite)
    {
        Image image = CloudyImage(parent, name, sprite);
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        return image.rectTransform;
    }

    TMP_Text MakeMapText(RectTransform parent, string name, string content, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        SetLabelFont(text);
        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = MapInk;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    Button MakeMapFocusButton(string label, UiBtn style, UnityEngine.Events.UnityAction action)
    {
        var button = UiKit.MakeButton(cloudyObjectiveCard, label, style, new Vector2(95f, 40f), 18f, .7f);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        return button;
    }

    void BuildMapLegend(int index, string label, Sprite sprite)
    {
        legendIcons[index] = CloudyImage(cloudyFooter, "Icon_" + index, sprite);
        legendLabels[index] = MakeMapText(cloudyFooter, "Legend_" + index, label, 17f, TextAlignmentOptions.MidlineLeft);
        legendIcons[index].raycastTarget = true;
        var hover = legendIcons[index].gameObject.AddComponent<MapCloudyHover>();
        hover.owner = this;
        hover.title = label;
        hover.state = index == 0 ? "Положение Лео" : index == 1 ? "Метка текущего задания" : "Ближайшая находка для осмотра";
    }

    void ConfigureCloudyV3Controls()
    {
        if (cloudyObjectiveCard == null) return;
        SkinMapRoundButton(closeButton, "×");
        SkinMapRoundButton(zoomInBtn, "+");
        SkinMapRoundButton(zoomOutBtn, "−");
        if (cloudyTooltip != null) cloudyTooltip.SetAsLastSibling();
    }

    void SkinMapRoundButton(Button button, string symbol)
    {
        if (button == null || !button.TryGetComponent<Image>(out var image)) return;
        var rt = image.rectTransform;
        rt.SetParent(bigWindow, false);
        rt.SetAsLastSibling();
        var sprite = CloudySprite("RoundButton");
        if (sprite != null) image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, .96f, .88f),
            pressedColor = new Color(.89f, .78f, .97f),
            selectedColor = Color.white,
            disabledColor = new Color(1f, 1f, 1f, .42f),
            colorMultiplier = 1f,
            fadeDuration = .1f
        };
        if (rt.Find("Fill") is Transform fill) fill.gameObject.SetActive(false);
        var text = rt.GetComponentInChildren<TMP_Text>(true);
        if (text == null) text = MakeMapText(rt, "Label", symbol, 32f, TextAlignmentOptions.Center);
        SetLabelFont(text);
        StretchMapRect(text.rectTransform);
        text.text = symbol;
        text.color = MapInk;
        text.outlineWidth = 0f;
        text.raycastTarget = false;
    }

    static void PlaceMapRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    static void StretchMapRect(RectTransform rt, Vector2 lower = default, Vector2 upper = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = Middle;
        rt.offsetMin = lower;
        rt.offsetMax = upper;
    }

    Rect MapSafeArea(RectTransform host)
    {
        Rect available = host.rect;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f || (safe.width >= Screen.width && safe.height >= Screen.height)) return available;
        Camera cam = UiCam(host);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(host, safe.min, cam, out var min)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(host, safe.max, cam, out var max)) return available;
        var clipped = Rect.MinMaxRect(Mathf.Max(available.xMin, min.x), Mathf.Max(available.yMin, min.y),
            Mathf.Min(available.xMax, max.x), Mathf.Min(available.yMax, max.y));
        return clipped.width > 1f && clipped.height > 1f ? clipped : available;
    }

    void UpdateCloudyV3Window()
    {
        if (cloudyObjectiveCard == null || bigWindow.parent is not RectTransform host) return;
        Rect available = MapSafeArea(host);
        var root = bigWindow.GetComponentInParent<Canvas>()?.rootCanvas;
        float scale = root != null ? root.scaleFactor : 1f;
        var layout = CalculateMapWindow(available, scale);
        if (layout.unit <= 0f) return;
        if (available != cloudyWindowArea || !Mathf.Approximately(layout.unit, cloudyUiUnit))
            LayoutCloudyV3Window(host, available, layout);
        UpdateMapObjectiveCard();
    }

    static (float unit, Rect window, float square, bool wideHeader) CalculateMapWindow(Rect available, float scale)
    {
        float unit = Mathf.Min(Mathf.Clamp(.76f / Mathf.Max(.01f, scale), .85f, 2f),
            Mathf.Min(available.width / 360f, available.height / 365f));
        float outside = 16f * unit;
        float inset = 22f * unit;
        float footer = 64f * unit;
        float mapWidth = Mathf.Min(900f * Mathf.Max(1f, unit), available.width - (outside + inset) * 2f);
        bool wideHeader = Mathf.Min(mapWidth, available.height - 2f * outside - 132f * unit - footer) >= 640f * unit;
        float header = (wideHeader ? 132f : 200f) * unit;
        float square = Mathf.Max(1f, Mathf.Min(mapWidth, available.height - 2f * outside - header - footer));
        Vector2 size = new Vector2(square + inset * 2f, square + header + footer);
        return (unit, new Rect(available.center - size * .5f, size), square, wideHeader);
    }

    void LayoutCloudyV3Window(RectTransform host, Rect available,
        (float unit, Rect window, float square, bool wideHeader) layout)
    {
        cloudyWindowArea = available;
        float unit = cloudyUiUnit = layout.unit;
        float square = layout.square;
        bool wideHeader = layout.wideHeader;
        float inset = 22f * unit;
        float info = 114f * unit;
        float header = (wideHeader ? 132f : 200f) * unit;
        float windowWidth = layout.window.width;
        PlaceMapRect(bigWindow, Middle, Middle, layout.window.center - host.rect.center, layout.window.size);

        Vector2 mapTopLeft = new Vector2(inset, -header);
        Vector2 mapSize = Vector2.one * square;
        if (cloudyClip != bigWindow)
        {
            PlaceMapRect(cloudyClip, TopLeft, TopLeft, mapTopLeft, mapSize);
            StretchMapRect(bigViewport);
        }
        else PlaceMapRect(bigViewport, TopLeft, TopLeft, mapTopLeft, mapSize);

        if (cloudyFrame != null && cloudyFrame.parent == cloudyClip && cloudyClip != bigWindow)
            StretchMapRect(cloudyFrame, Vector2.one * -8f * unit, Vector2.one * 8f * unit);
        else if (cloudyFrame != null && cloudyFrame.parent == bigWindow)
            PlaceMapRect(cloudyFrame, TopLeft, TopLeft, mapTopLeft + new Vector2(-7f, 7f) * unit, mapSize + Vector2.one * 14f * unit);
        if (bigCompass != null && bigCompass.parent == cloudyClip && cloudyClip != bigWindow)
            PlaceMapRect(bigCompass, TopLeft, Middle, new Vector2(42f, -42f) * unit, Vector2.one * 56f * unit);

        float titleWidth = wideHeader ? 266f * unit : Mathf.Min(325f * unit, windowWidth - 112f * unit);
        Vector2 titleAnchor = wideHeader ? TopLeft : new Vector2(.5f, 1f);
        PlaceMapRect(cloudyHeader, titleAnchor, new Vector2(wideHeader ? 0f : .5f, 1f),
            wideHeader ? new Vector2(inset, -24f * unit) : new Vector2(0f, -7f * unit),
            new Vector2(titleWidth, titleWidth * 198f / 743f));
        float cardX = wideHeader ? inset + titleWidth + 12f * unit : inset;
        float cardRight = inset + (wideHeader ? 52f * unit : 0f);
        PlaceMapRect(cloudyObjectiveCard, TopLeft, TopLeft, new Vector2(cardX, (wideHeader ? -6f : -72f) * unit),
            new Vector2(windowWidth - cardX - cardRight, info));

        LayoutMapObjectiveText(cloudyObjectiveCaption, 9f, 27f, 14f);
        LayoutMapObjectiveText(cloudyObjectiveTitle, 27f, 68f, 20f);
        LayoutMapFocusButton(cloudyFocusPlayer, 109f);
        LayoutMapFocusButton(cloudyFocusQuest, 12f);

        float zoomWidth = zoomInBtn != null ? 104f * unit : 0f;
        PlaceMapRect(cloudyFooter, Vector2.zero, Vector2.zero, new Vector2(inset, 9f * unit),
            new Vector2(square - zoomWidth, 43f * unit));
        PlaceMapRoundButton(closeButton, TopRight, new Vector2(-18f, -10f), 48f);
        PlaceMapRoundButton(zoomInBtn, BottomRight, new Vector2(-19f, 10f), 43f);
        PlaceMapRoundButton(zoomOutBtn, BottomRight, new Vector2(-68f, 10f), 43f);

        if (cloudyTooltip != null)
        {
            cloudyTooltip.sizeDelta = new Vector2(230f, 64f) * unit;
            SetMapFontSize(cloudyTooltipTitle, 17f);
            SetMapFontSize(cloudyTooltipStatus, 13f);
            LayoutMapObjectiveText(cloudyTooltipTitle, 4f, 30f, 17f);
            LayoutMapObjectiveText(cloudyTooltipStatus, 30f, 58f, 13f);
        }
        Canvas.ForceUpdateCanvases();
        if (zoneLayer != null) BuildZoneLayer();
        pan = ClampPan(pan, zoom);
        panTarget = ClampPan(panTarget, zoomTarget);
        bigScaleDirty = cloudyNeedsLayout = true;
        HideCloudyMapTooltip();
    }

    void LayoutMapObjectiveText(TMP_Text text, float top, float bottom, float size)
    {
        float unit = cloudyUiUnit;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = TopLeft;
        rt.anchorMax = TopRight;
        rt.offsetMin = new Vector2(13f, -bottom) * unit;
        rt.offsetMax = new Vector2(-13f, -top) * unit;
        SetMapFontSize(text, size);
    }

    void SetMapFontSize(TMP_Text text, float size)
    {
        text.fontSize = text.fontSizeMax = size * cloudyUiUnit;
        text.fontSizeMin = size * .8f * cloudyUiUnit;
    }

    void LayoutMapFocusButton(Button button, float x)
    {
        var rt = (RectTransform)button.transform;
        PlaceMapRect(rt, BottomRight, BottomRight, new Vector2(-x, 6f) * cloudyUiUnit,
            new Vector2(91f, 38f) * cloudyUiUnit);
        SetMapFontSize(button.GetComponentInChildren<TMP_Text>(), 17f);
    }

    void PlaceMapRoundButton(Button button, Vector2 corner, Vector2 offset, float size)
    {
        if (button == null) return;
        PlaceMapRect((RectTransform)button.transform, corner, corner, offset * cloudyUiUnit, Vector2.one * size * cloudyUiUnit);
        SetMapFontSize(button.GetComponentInChildren<TMP_Text>(), 31f);
    }

    void UpdateMapObjectiveCard()
    {
        string objective = UIManager.Instance != null ? UIManager.Instance.CurrentObjectiveTitle : null;
        if (lastMapObjective != objective || string.IsNullOrEmpty(cloudyObjectiveTitle.text))
        {
            lastMapObjective = objective;
            mapObjective = ObjectiveTags.Replace(objective ?? "", "").Trim();
            cloudyObjectiveTitle.text = !string.IsNullOrEmpty(mapObjective) ? mapObjective : "Свободное исследование";
            if (questPin?.big != null && questPin.big.TryGetComponent<MapCloudyHover>(out var hover)) hover.state = mapObjective;
        }
        cloudyFocusPlayer.interactable = mapReady && player != null;
        cloudyFocusQuest.interactable = mapReady && questPin?.t != null;
    }

    void LayoutMapLegend(bool hasObjective, bool hasInspection)
    {
        if (cloudyFooter == null) return;
        int count = 1 + (hasObjective ? 1 : 0) + (hasInspection ? 1 : 0);
        int slot = 0;
        float unit = cloudyUiUnit;
        bool compact = cloudyFooter.rect.width / count < 100f * unit;
        for (int i = 0; i < legendIcons.Length; i++)
        {
            bool visible = i == 0 || (i == 1 ? hasObjective : hasInspection);
            legendIcons[i].gameObject.SetActive(visible);
            legendLabels[i].gameObject.SetActive(visible && !compact);
            if (!visible) continue;
            float start = (float)slot / count;
            var iconRt = legendIcons[i].rectTransform;
            Vector2 iconAnchor = new Vector2(start + (compact ? .5f / count : 0f), .5f);
            PlaceMapRect(iconRt, iconAnchor, new Vector2(compact ? .5f : 0f, .5f), new Vector2(compact ? 0f : 11f, 0f) * unit,
                Vector2.one * 26f * unit);
            var labelRt = legendLabels[i].rectTransform;
            labelRt.anchorMin = new Vector2(start, 0f);
            labelRt.anchorMax = new Vector2((float)++slot / count, 1f);
            labelRt.offsetMin = new Vector2(39f, 0f) * unit;
            labelRt.offsetMax = new Vector2(-4f, 0f) * unit;
            SetMapFontSize(legendLabels[i], 17f);
        }
    }

    void FocusMapPlayer() { if (player != null) FocusMapAt(player.position); }
    void FocusMapQuest() { if (questPin?.t != null) FocusMapAt(questPin.t.position); }

    void FocusMapAt(Vector3 point)
    {
        if (!mapReady || bigMap == null) return;
        zoomTarget = Mathf.Clamp(Mathf.Max(zoomTarget, 2f), minZoom, maxZoom);
        panTarget = ClampPan(-ToMap(point) * (bigMap.rectTransform.rect.width / side * zoomTarget), zoomTarget);
        HideCloudyMapTooltip();
    }
}

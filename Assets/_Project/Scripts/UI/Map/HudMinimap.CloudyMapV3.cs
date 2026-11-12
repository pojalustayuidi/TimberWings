// TimberWings | Cloudy Candy Map V3
// Cosmetic extension over the existing HudMinimap V2 hooks. No changes to StoryFlow, saves, or map baking.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Cloudy Candy • Большая карта V3")]
    [Tooltip("Цельное окно, подписи без наложений, компактная легенда и новые значки.")]
    public bool cloudyMapV3 = true;
    [Tooltip("Скрывать служебные подписи вроде BuildZone / PAD_*. На игровые сохранения не влияет.")]
    public bool cloudyV3HideTechnicalNames = true;
    [Range(1, 10)] public int cloudyV3OverviewPins = 9;
    [Range(0f, 20f)] public float cloudyV3SafePadding = 10f;

    bool cloudyV3ControlsStyled;
    bool cloudyV3ViewTracked;
    Vector2 cloudyV3LastPan;
    Vector2 cloudyV3LastPlayer;
    float cloudyV3LastZoom;
    readonly HashSet<string> cloudyV3ZoneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly List<Poi> cloudyV3Accepted = new List<Poi>(32);
    readonly List<Vector2> cloudyV3Directions = new List<Vector2>(8) {
        Vector2.right, Vector2.up, Vector2.left, Vector2.down,
        new Vector2(.7071f,.7071f), new Vector2(-.7071f,.7071f),
        new Vector2(.7071f,-.7071f), new Vector2(-.7071f,-.7071f)
    };

    // Called after V2 has loaded the sprites and created its header/tooltip.
    void CloudyV3Initialize(RectTransform parent)
    {
        if (!cloudyMapV3 || bigWindow == null) return;
        // BuilderSimHud.Panel's wood is on Window/Body, NOT on Window's root.
        // Disable the old backdrop only. Keep Clip, Block, Close, Hint bindings, and the map itself.
        string[] legacy = { "Shadow", "Outline", "Body", "Gloss", "Rim", "Ring" };
        foreach (string name in legacy)
        {
            Transform child = bigWindow.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }
        // Avoid a hard dependency on the HUD builder's optional shine component.
        foreach (Behaviour behaviour in bigWindow.GetComponents<Behaviour>())
            if (behaviour != null && behaviour.GetType().Name == "HudShine") behaviour.enabled = false;
        var rootImage = bigWindow.GetComponent<Image>();
        if (rootImage != null) rootImage.enabled = false;

        Image surface = CloudyImage(bigWindow, "CloudyV3Surface", CloudySprite("WindowPanel"));
        if (surface != null)
        {
            RectTransform rt = surface.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            surface.type = Image.Type.Sliced; surface.preserveAspect = false;
            surface.pixelsPerUnitMultiplier = 1f;
            surface.color = Color.white; surface.raycastTarget = false;
            rt.SetAsFirstSibling();
        }

        // Hide the old silver outline inside the live clip; V2's Aqua frame remains on top.
        Transform clip = bigMap != null ? bigMap.transform.parent : null;
        if (clip != null)
        {
            Transform ring = clip.Find("Ring");
            if (ring != null) ring.gameObject.SetActive(false);
        }
        Transform oldHint = bigWindow.Find("Hint");
        if (oldHint != null) oldHint.gameObject.SetActive(false);

        Transform header = bigWindow.Find("CloudyHeader");
        if (header is RectTransform)
        {
            RectTransform hr = (RectTransform)header;
            hr.anchorMin = hr.anchorMax = new Vector2(.5f, 1f);
            hr.pivot = new Vector2(.5f, .5f);
            hr.anchoredPosition = new Vector2(0f, -7f);
            hr.sizeDelta = new Vector2(625f, 166f);
            hr.SetAsLastSibling();
        }
        CloudyV3BuildFooter();
        CloudyV3SkinCloseButton();
        // Tooltip lives above the clip and is never accidentally hidden behind the map mask.
        if (cloudyTooltip != null)
        {
            cloudyTooltip.SetParent(bigWindow, false);
            cloudyTooltip.SetAsLastSibling();
        }
        if (closeButton != null) closeButton.transform.SetAsLastSibling();
        cloudyNeedsLayout = true;
    }

    void CloudyV3BuildFooter()
    {
        if (bigWindow == null) return;
        Image bg = CloudyImage(bigWindow, "CloudyV3Footer", CloudySprite("LegendBar"));
        if (bg == null) return;
        bg.type = Image.Type.Sliced; bg.preserveAspect = false;
        bg.pixelsPerUnitMultiplier = 1f; bg.raycastTarget = false;
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(.5f, 0f);
        rt.offsetMin = new Vector2(55f, 33f);
        rt.offsetMax = new Vector2(-55f, 92f);
        for (int i = 0; i < 4; i++)
        {
            string[] titles = { "Неизвестно", "Найдено", "Изучено", "Построено" };
            string[] icons = { "StateUnknown", "StateFound", "StateStudied", "StateBuilt" };
            var slot = CloudyV3Rect(rt, "Legend_" + i);
            float x = (i + .5f) / 4f;
            slot.anchorMin = slot.anchorMax = new Vector2(x, .5f);
            slot.pivot = new Vector2(.5f, .5f);
            slot.anchoredPosition = Vector2.zero;
            slot.sizeDelta = new Vector2(150f, 39f);
            Image icon = CloudyImage(slot, "Icon", CloudySprite(icons[i]));
            if (icon != null)
            {
                RectTransform ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, .5f);
                ir.pivot = new Vector2(0f, .5f);
                ir.anchoredPosition = new Vector2(0f, 0f);
                ir.sizeDelta = new Vector2(32f, 32f);
            }
            TMP_Text tx = CloudyV3Text(slot, "Text", titles[i], 17f);
            RectTransform tr = tx.rectTransform;
            tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(36f, 0f); tr.offsetMax = Vector2.zero;
            tx.alignment = TextAlignmentOptions.MidlineLeft;
            tx.fontSizeMin = 12f; tx.fontSizeMax = 17f;
        }
        var controls = CloudyV3Rect(bigWindow, "CloudyV3ControlsHint");
        controls.anchorMin = new Vector2(0f, 0f); controls.anchorMax = new Vector2(1f, 0f);
        controls.offsetMin = new Vector2(90f, 9f); controls.offsetMax = new Vector2(-90f, 31f);
        TMP_Text hint = CloudyV3Text(controls, "Text", "Колесо мыши — масштаб   •   Перетаскивание — обзор   •   M / Esc — закрыть", 14f);
        hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one;
        hint.rectTransform.offsetMin = Vector2.zero; hint.rectTransform.offsetMax = Vector2.zero;
        hint.color = new Color32(92, 85, 146, 255);
        hint.alignment = TextAlignmentOptions.Center;
        hint.fontSizeMin = 11f;
        // Corner clouds remain decorative, but do not cover the legend.
        foreach (string name in new[] { "CloudyCornerLeft", "CloudyCornerRight" })
        {
            RectTransform cloud = bigWindow.Find(name) as RectTransform;
            if (cloud == null) continue;
            cloud.sizeDelta = new Vector2(124f, 78f);
            cloud.anchoredPosition = new Vector2(name.EndsWith("Left", StringComparison.Ordinal) ? -8f : 8f, -13f);
            cloud.SetAsLastSibling();
        }
        rt.SetAsLastSibling();
        controls.SetAsLastSibling();
    }

    static RectTransform CloudyV3Rect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing as RectTransform;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        return r;
    }

    TMP_Text CloudyV3Text(Transform parent, string name, string content, float size)
    {
        RectTransform rt = CloudyV3Rect(parent, name);
        TextMeshProUGUI text = rt.GetComponent<TextMeshProUGUI>();
        if (text == null) text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        SetLabelFont(text);
        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color32(86, 49, 112, 255);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    void CloudyV3SkinCloseButton()
    {
        if (closeButton == null) return;
        RectTransform rt = closeButton.transform as RectTransform;
        if (rt == null) return;
        Image im = closeButton.GetComponent<Image>();
        if (im == null) im = closeButton.gameObject.AddComponent<Image>();
        Sprite art = CloudySprite("RoundButton");
        if (art != null) im.sprite = art;
        im.type = Image.Type.Simple;
        im.preserveAspect = true;
        im.color = Color.white;
        im.raycastTarget = true;
        closeButton.targetGraphic = im;
        rt.sizeDelta = new Vector2(64f, 64f);
        rt.anchoredPosition = new Vector2(-24f, -32f);
        Transform fill = rt.Find("Fill");
        if (fill != null) fill.gameObject.SetActive(false);
        TMP_Text label = rt.Find("Text") != null ? rt.Find("Text").GetComponent<TMP_Text>() : null;
        if (label != null)
        {
            SetLabelFont(label);
            label.text = "×";
            label.fontSize = 42f;
            label.color = new Color32(131, 65, 57, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
        CloudyV3ButtonColor(closeButton, im);
    }

    void CloudyV3ObserveView()
    {
        if (!cloudyV3ControlsStyled)
        {
            cloudyV3ControlsStyled = true;
            CloudyV3SkinZoomButton(zoomInBtn);
            CloudyV3SkinZoomButton(zoomOutBtn);
        }
        Vector2 playerAt = bigPlayer != null ? bigPlayer.anchoredPosition : Vector2.zero;
        if (!cloudyV3ViewTracked || Vector2.Distance(pan, cloudyV3LastPan) > 5f
            || Mathf.Abs(zoom - cloudyV3LastZoom) > .02f
            || Vector2.Distance(playerAt, cloudyV3LastPlayer) > 12f)
        {
            cloudyV3ViewTracked = true;
            cloudyV3LastPan = pan;
            cloudyV3LastPlayer = playerAt;
            cloudyV3LastZoom = zoom;
            cloudyNeedsLayout = true;
        }
    }

    void CloudyV3SkinZoomButton(Button btn)
    {
        if (btn == null) return;
        Image im = btn.GetComponent<Image>();
        if (im == null) return;
        Sprite sprite = CloudySprite("RoundButton");
        if (sprite != null) im.sprite = sprite;
        im.type = Image.Type.Simple;
        im.preserveAspect = true;
        im.color = Color.white;
        im.raycastTarget = true;
        btn.targetGraphic = im;
        CloudyV3ButtonColor(btn, im);
        var rt = btn.transform as RectTransform;
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(60f, 60f);
            rt.anchoredPosition = new Vector2(-38f, btn == zoomInBtn ? 108f : 40f);
        }
        TMP_Text tx = btn.GetComponentInChildren<TMP_Text>();
        if (tx != null)
        {
            SetLabelFont(tx);
            tx.fontSize = 38f;
            tx.color = new Color32(142, 74, 48, 255);
            tx.alignment = TextAlignmentOptions.Center;
        }
    }

    static void CloudyV3ButtonColor(Button btn, Image image)
    {
        btn.transition = Selectable.Transition.ColorTint;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.colors = new ColorBlock {
            normalColor = Color.white,
            highlightedColor = new Color(1f, .96f, .88f, 1f),
            pressedColor = new Color(.89f, .78f, .97f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(1f, 1f, 1f, .5f),
            colorMultiplier = 1f,
            fadeDuration = .10f
        };
    }

    // Only the visual label changes. The MapPoi ID, underlying p.label, saved progress and events stay intact.
    string CloudyV3DisplayLabel(Poi p)
    {
        if (p == null || p.kind == null) return null;
        string kind = p.kind.key ?? "";
        string original = p.label ?? "";
        if (CloudyContainsAny(kind, "BuildZone", "BuildArea", "Collider", "Trigger")
            || CloudyContainsAny(original, "BuildZone", "BuilderTest", "Clone")) return null;
        if (kind == "Mystery") return null;
        if (CloudyContainsAny(kind, "Workyard", "WorkerCamp")) return "Артель";
        if (CloudyContainsAny(kind, "Portal")) return "Портал";
        if (CloudyContainsAny(kind, "QuestBoard", "Board")) return "Доска заданий";
        if (CloudyContainsAny(kind, "Campfire") || CloudyContainsAny(original, "CAMPFIRE_Point")) return "Костёр";
        if (CloudyContainsAny(kind, "Bridge") || CloudyContainsAny(original, "PAD_Bridge")) return "Мост";
        if (CloudyContainsAny(kind, "Market")) return "Лавка";
        if (CloudyContainsAny(kind, "Forest")) return "Лес";
        if (CloudyContainsAny(kind, "Hut")) return "Хижина";
        if (CloudyContainsAny(kind, "Tent")) return "Палатка";
        if (CloudyContainsAny(kind, "Pets")) return "Питомцы";
        if (CloudyContainsAny(kind, "Discovery")) return string.IsNullOrWhiteSpace(original) ? "Находка" : original.Trim();
        if (cloudyV3HideTechnicalNames && (CloudyContainsAny(original, "PAD_", "_Point", "Site_", "Zone_")
            || CloudyContainsAny(kind, "BuildZone", "PAD_"))) return null;
        return string.IsNullOrWhiteSpace(original) ? null : original.Trim();
    }

    void CloudyV3StyleMarker(RectTransform marker, Poi p, bool big)
    {
        if (!cloudyMapV3 || marker == null || p == null) return;
        string kind = p.kind != null ? p.kind.key : "";
        string sprite = null;
        if (CloudyContainsAny(kind, "Campfire")) sprite = "IconCampfire";
        else if (CloudyContainsAny(kind, "QuestBoard", "Board")) sprite = "IconQuest";
        else if (CloudyContainsAny(kind, "Bridge")) sprite = "IconBridge";
        else if (CloudyContainsAny(kind, "Discovery")) sprite = "IconDiscovery";
        var icon = marker.Find("Icon");
        if (sprite != null && icon != null)
        {
            var image = icon.GetComponent<Image>();
            Sprite art = CloudySprite(sprite);
            if (image != null && art != null) { image.sprite = art; image.color = Color.white; }
        }
        if (!big) return;
        var label = marker.Find("Label") as RectTransform;
        if (label == null) return;
        var text = label.GetComponent<TMP_Text>();
        if (text == null) return;
        string resolved = CloudyV3DisplayLabel(p);
        text.text = resolved ?? "";
        text.fontSize = 17f;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 17f;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0f;
        text.color = new Color32(91, 54, 106, 255);
        float width = Mathf.Clamp(text.preferredWidth + 31f, 86f, 194f);
        label.sizeDelta = new Vector2(width, 32f);
        var plate = marker.Find("CloudyLabelPlate") as RectTransform;
        if (plate != null)
        {
            plate.sizeDelta = label.sizeDelta;
            var im = plate.GetComponent<Image>();
            if (im != null) im.color = Color.white;
        }
    }

    void CloudyV3RefreshPinStates()
    {
        foreach (Poi p in pois)
        {
            if (p == null || p.big == null || p.kind == null || p.kind.key != "Mystery") continue;
            Transform pin = p.big.Find("Pin");
            Image image = pin != null ? pin.GetComponent<Image>() : null;
            if (image != null) image.color = p.nearby
                ? new Color32(255, 220, 171, 255)
                : new Color32(206, 187, 255, 255);
        }
    }

    static bool CloudyV3TechnicalZone(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return true;
        return CloudyContainsAny(title, "BuildZone", "_Point", "PAD_", "Spawn", "Collider", "Trigger", "Debug");
    }

    Rect CloudyV3VisibleRect(float margin)
    {
        if (bigViewport == null) return new Rect(-340f, -340f, 680f, 680f);
        float z = Mathf.Max(1f, zoom);
        Rect r = bigViewport.rect;
        float xMin = (-r.width * .5f + margin - pan.x) / z;
        float yMin = (-r.height * .5f + margin - pan.y) / z;
        float w = (r.width - 2f * margin) / z;
        float h = (r.height - 2f * margin) / z;
        return new Rect(xMin, yMin, Mathf.Max(1f, w), Mathf.Max(1f, h));
    }

    static bool CloudyV3Contains(Rect viewport, Rect r)
    {
        return r.xMin >= viewport.xMin && r.xMax <= viewport.xMax
            && r.yMin >= viewport.yMin && r.yMax <= viewport.yMax;
    }

    bool CloudyV3TryPin(Vector2 anchor, float size, Rect viewport, out Vector2 position)
    {
        var rect = CloudyRect(anchor, Vector2.one * size);
        if (CloudyV3Contains(viewport, rect) && !CloudyCollides(rect))
        {
            position = anchor; cloudyOccupied.Add(rect); return true;
        }
        // Never move a pin far from its real-world location. Nudge at most one icon width.
        foreach (Vector2 direction in cloudyV3Directions)
        {
            Vector2 point = anchor + direction * size * 1.04f;
            rect = CloudyRect(point, Vector2.one * size);
            if (!CloudyV3Contains(viewport, rect) || CloudyCollides(rect)) continue;
            position = point; cloudyOccupied.Add(rect); return true;
        }
        position = anchor;
        return false;
    }

    bool CloudyV3TryPoiLabel(RectTransform marker, RectTransform label, float pinScale, Rect viewport, out Vector2 local)
    {
        Vector2 size = label.sizeDelta * pinScale;
        float glyph = 19f * pinScale + 6f;
        float dx = glyph + size.x * .5f + cloudyLabelGap;
        float dy = glyph + size.y * .5f + cloudyLabelGap;
        Vector2[] offsets = {
            new Vector2(dx, 0f), new Vector2(-dx, 0f),
            new Vector2(0f, dy), new Vector2(0f, -dy),
            new Vector2(dx * .72f, dy * .8f), new Vector2(-dx * .72f, dy * .8f),
            new Vector2(dx * .72f, -dy * .8f), new Vector2(-dx * .72f, -dy * .8f)
        };
        foreach (Vector2 offset in offsets)
        {
            Rect area = CloudyRect(marker.anchoredPosition + offset, size);
            if (!CloudyV3Contains(viewport, area) || CloudyCollides(area)) continue;
            cloudyOccupied.Add(area);
            local = offset / Mathf.Max(.01f, pinScale);
            return true;
        }
        local = Vector2.zero;
        return false;
    }

    bool CloudyV3TryZoneLabel(Vector2 home, Vector2 size, Rect viewport, out Vector2 accepted)
    {
        Vector2[] offsets = { Vector2.zero, Vector2.up * 44f, Vector2.down * 44f,
            Vector2.left * 52f, Vector2.right * 52f, new Vector2(-40f, 40f), new Vector2(40f, -40f) };
        foreach (Vector2 offset in offsets)
        {
            Vector2 center = home + offset;
            Rect area = CloudyRect(center, size);
            if (!CloudyV3Contains(viewport, area) || CloudyCollides(area)) continue;
            cloudyOccupied.Add(area);
            accepted = center;
            return true;
        }
        accepted = home;
        return false;
    }

    void CloudyV3Layout(float inv)
    {
        if (bigViewport == null || bigMap == null || !cloudyMapV3) return;
        cloudyNeedsLayout = false;
        cloudyOccupied.Clear(); cloudyClusters.Clear(); cloudyOrdered.Clear(); cloudyV3Accepted.Clear();
        Rect viewport = CloudyV3VisibleRect(cloudyV3SafePadding);
        float mapSize = bigMap.rectTransform.rect.width;
        float pinScale = 1.25f * markerScale * inv;
        float glyphSize = Mathf.Max(24f, 37f * pinScale);
        if (bigPlayer != null)
        {
            Vector2 playerPoint = bigPlayer.anchoredPosition;
            cloudyOccupied.Add(CloudyRect(playerPoint, Vector2.one * 50f * inv));
        }
        foreach (Poi p in pois)
        {
            if (p.big == null) continue;
            bool visible = p.t != null && !FogHides(p) && CloudyV3DisplayLabel(p) != null;
            // Unknown markers are icons only; do not hide them just because they have no label.
            if (p.kind != null && p.kind.key == "Mystery") visible = p.t != null && !FogHides(p);
            p.big.gameObject.SetActive(visible);
            CloudySetCluster(p.big, 0, cloudyClusterSprite);
            if (visible) cloudyOrdered.Add(p);
        }
        cloudyOrdered.Sort((a, b) => {
            int n = CloudyPriority(b).CompareTo(CloudyPriority(a));
            if (n != 0) return n;
            return string.Compare(a.label, b.label, StringComparison.Ordinal);
        });
        int maxPins = zoom < 1.42f ? cloudyV3OverviewPins : zoom < 2f ? 15 : zoom < 2.75f ? 23 : 64;
        for (int i = 0; i < cloudyOrdered.Count; i++)
        {
            Poi p = cloudyOrdered[i];
            Vector2 anchor = ToMap(p.t.position) / side * mapSize;
            int nearIndex = -1;
            float distance = float.MaxValue;
            for (int j = 0; j < cloudyV3Accepted.Count; j++)
            {
                float d = Vector2.Distance(cloudyV3Accepted[j].big.anchoredPosition, anchor);
                if (d < distance) { distance = d; nearIndex = j; }
            }
            bool close = nearIndex >= 0 && distance < glyphSize * 1.55f;
            if (cloudyV3Accepted.Count >= maxPins && CloudyPriority(p) < 140)
            {
                p.big.gameObject.SetActive(false);
                if (close) CloudyV3IncrementCluster(cloudyV3Accepted[nearIndex]);
                continue;
            }
            Vector2 placed;
            if (close && CloudyPriority(p) < 130)
            {
                p.big.gameObject.SetActive(false);
                CloudyV3IncrementCluster(cloudyV3Accepted[nearIndex]);
                continue;
            }
            if (!CloudyV3TryPin(anchor, glyphSize, viewport, out placed))
            {
                p.big.gameObject.SetActive(false);
                if (close) CloudyV3IncrementCluster(cloudyV3Accepted[nearIndex]);
                continue;
            }
            p.big.anchoredPosition = placed;
            cloudyV3Accepted.Add(p);
        }
        foreach (var cluster in cloudyClusters)
        {
            CloudySetCluster(cluster.Key.big, cluster.Value, cloudyClusterSprite);
            var hover = cluster.Key.big != null ? cluster.Key.big.GetComponent<MapCloudyHover>() : null;
            if (hover != null) hover.state = "+" + cluster.Value + " объекта рядом — приблизьте карту";
        }
        // Overview map: at most a couple of prominent POI names plus 1–2 zone names.
        int poiLimit = zoom < 1.4f ? 1 : zoom < 1.9f ? 3 : zoom < 2.65f ? 7 : 16;
        int poiCount = 0;
        foreach (Poi p in cloudyV3Accepted)
        {
            RectTransform label = p.big.Find("Label") as RectTransform;
            RectTransform plate = p.big.Find("CloudyLabelPlate") as RectTransform;
            if (label == null) continue;
            string shown = CloudyV3DisplayLabel(p);
            bool priority = CloudyPriority(p) >= (zoom < 1.9f ? 105 : 65);
            bool allow = !string.IsNullOrWhiteSpace(shown) && priority && poiCount < poiLimit;
            Vector2 offset;
            if (allow && CloudyV3TryPoiLabel(p.big, label, pinScale, viewport, out offset))
            {
                label.anchoredPosition = offset;
                label.gameObject.SetActive(true);
                if (plate != null) { plate.anchoredPosition = offset; plate.gameObject.SetActive(true); }
                poiCount++;
            }
            else
            {
                label.gameObject.SetActive(false);
                if (plate != null) plate.gameObject.SetActive(false);
            }
        }
        if (zoneLayer == null) return;
        cloudyNames.Clear(); cloudyV3ZoneNames.Clear();
        for (int i = 0; i < zoneLayer.childCount; i++)
        {
            RectTransform r = zoneLayer.GetChild(i) as RectTransform;
            if (r != null && r.name.StartsWith("Name_", StringComparison.Ordinal)) cloudyNames.Add(r);
        }
        cloudyNames.Sort((a,b) => CloudyZonePriority(b).CompareTo(CloudyZonePriority(a)));
        int maxZoneLabels = zoom < 1.4f ? Mathf.Max(1, cloudyOverviewLabels - poiCount)
            : zoom < 2f ? 3 : zoom < 3f ? 6 : 10;
        int mysteryLimit = zoom < 1.55f ? 2 : 3;
        int zones = 0, mysteries = 0;
        foreach (RectTransform r in cloudyNames)
        {
            TMP_Text text = r.GetComponent<TMP_Text>();
            if (text == null) continue;
            string name = r.name.Substring("Name_".Length).Trim();
            bool unknown = text.text == "?";
            bool hide = CloudyV3TechnicalZone(name)
                || (!unknown && cloudyV3ZoneNames.Contains(name))
                || (unknown && mysteries >= mysteryLimit);
            Vector2 basePos;
            if (!zoneNameBase.TryGetValue(r, out basePos)) { basePos = r.anchoredPosition; zoneNameBase[r] = basePos; }
            text.fontSize = unknown ? 31f : 20f;
            text.outlineWidth = 0f;
            text.color = unknown ? new Color32(131, 100, 184, 255) : new Color32(90, 55, 106, 255);
            text.enableAutoSizing = true; text.fontSizeMin = 16f; text.fontSizeMax = text.fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            r.sizeDelta = new Vector2(unknown ? 54f : Mathf.Clamp(text.preferredWidth + 35f, 110f, 235f), unknown ? 55f : 37f);
            r.localScale = Vector3.one * inv;
            Image plate = CloudyZonePlate(r);
            if (plate != null)
            {
                plate.rectTransform.sizeDelta = r.sizeDelta;
                plate.rectTransform.localScale = Vector3.one * inv;
                Sprite art = CloudySprite(unknown ? "MysteryCloud" : "Pill");
                if (art != null) plate.sprite = art;
                plate.type = unknown ? Image.Type.Simple : Image.Type.Sliced;
                plate.raycastTarget = false;
            }
            Vector2 candidate = basePos;
            bool allowZone = !hide && (unknown || zones < maxZoneLabels);
            bool show = allowZone && CloudyV3TryZoneLabel(basePos, r.sizeDelta * inv, viewport, out candidate);
            r.gameObject.SetActive(show);
            if (plate != null)
            {
                plate.rectTransform.anchoredPosition = candidate;
                plate.gameObject.SetActive(show);
            }
            if (show)
            {
                r.anchoredPosition = candidate;
                if (unknown) mysteries++;
                else { zones++; cloudyV3ZoneNames.Add(name); }
            }
        }
    }

    void CloudyV3IncrementCluster(Poi host)
    {
        if (host == null) return;
        int n;
        cloudyClusters.TryGetValue(host, out n);
        cloudyClusters[host] = n + 1;
    }
}

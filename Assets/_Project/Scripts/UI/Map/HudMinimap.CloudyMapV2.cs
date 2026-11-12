// TimberWings | Cloudy Map V2
// Extension to the EXISTING HudMinimap. No additional minimap, no StoryFlow/SaveSystem changes.
// Assets are baked PNG sprites in Resources/CloudyMapV2 (no per-frame texture generation).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Cloudy Candy • Большая карта V2")]
    [Tooltip("Оформление карты по референсу: облачный заголовок, мягкая рамка, умная раскладка подписей")]
    public bool cloudyMapV2 = true;
    [Tooltip("Плавное появление открытых сюжетных точек и завершённых построек")]
    public bool cloudyAnimateUnlocks = true;
    [Range(1, 8)] public int cloudyOverviewLabels = 3;
    [Range(0f, 20f)] public float cloudyLabelGap = 7f;

    readonly Dictionary<RectTransform, Image> cloudyZonePlates = new Dictionary<RectTransform, Image>();
    readonly Dictionary<RectTransform, float> cloudyNewPins = new Dictionary<RectTransform, float>();
    readonly HashSet<string> cloudySeen = new HashSet<string>(StringComparer.Ordinal);
    readonly Dictionary<Poi, int> cloudyClusters = new Dictionary<Poi, int>();
    readonly List<Poi> cloudyOrdered = new List<Poi>();
    readonly List<Rect> cloudyOccupied = new List<Rect>();
    readonly List<RectTransform> cloudyNames = new List<RectTransform>();
    readonly List<RectTransform> cloudyDead = new List<RectTransform>();
    bool cloudyInitialScanComplete;
    bool cloudyNeedsLayout = true;
    RectTransform cloudyTooltip;
    CanvasGroup cloudyTooltipGroup;
    TMP_Text cloudyTooltipTitle, cloudyTooltipStatus;
    Sprite cloudyPill, cloudyClusterSprite;

    static Sprite CloudySprite(string name) { return Resources.Load<Sprite>("CloudyMapV2/" + name); }

    // Called once after BigViewport/BigContent + old frame have been constructed.
    void InitCloudyMapV2(RectTransform parent)
    {
        if (!cloudyMapV2 || !candyStyle || bigViewport == null || parent == null) return;
        cloudyPill = CloudySprite("Pill");
        cloudyClusterSprite = CloudySprite("Cluster");
        if (bigWindow != null)
        {
            var panel = bigWindow.GetComponent<Image>();
            var panelArt = CloudySprite("WindowPanel");
            if (panel != null && panelArt != null)
            {
                panel.sprite = panelArt;
                panel.type = Image.Type.Sliced;
                panel.color = Color.white;
                panel.pixelsPerUnitMultiplier = 1f;
            }

            var originalTitle = bigWindow.Find("Title");
            if (originalTitle != null)
            {
                var titleText = originalTitle.GetComponent<TMP_Text>();
                if (titleText != null) titleText.enabled = false;
            }

            var header = CloudyImage(bigWindow, "CloudyHeader", CloudySprite("Header"));
            if (header != null)
            {
                var rt = header.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-9f, -6f);
                rt.sizeDelta = new Vector2(610f, 162f);
                rt.SetAsLastSibling();
            }

            // Decorations belong to the window, not to the zoomed map: no UI drift when panning.
            var cloud = CloudySprite("CornerCloud");
            if (cloud != null)
            {
                var left = CloudyImage(bigWindow, "CloudyCornerLeft", cloud);
                var right = CloudyImage(bigWindow, "CloudyCornerRight", cloud);
                ConfigureCorner(left, false);
                ConfigureCorner(right, true);
            }

            var hint = bigWindow.Find("Hint");
            var hintText = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (hintText != null)
            {
                SetLabelFont(hintText);
                hintText.text = "?  Неизвестно     ◇  Найдено     ✓  Изучено     ✦  Построено       •       M / Esc — закрыть";
                hintText.fontSize = 16f;
                hintText.color = UiCandy.Ink;
                hintText.enableAutoSizing = true;
                hintText.fontSizeMin = 12f;
                hintText.fontSizeMax = 16f;
                hintText.alignment = TextAlignmentOptions.Center;
            }
            if (closeButton != null) closeButton.transform.SetAsLastSibling();
        }

        // Preserve the existing live RenderTexture and its water/river/fog/bridge layers.
        Image frame = bigFrame != null ? bigFrame.GetComponent<Image>() : null;
        if (frame == null)
        {
            var generated = parent.Find("CandyFrame");
            if (generated != null) frame = generated.GetComponent<Image>();
        }
        var border = CloudySprite("Frame");
        if (frame != null && border != null)
        {
            frame.sprite = border;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 0.95f;
            frame.color = Color.white;
            frame.raycastTarget = false;
        }

        BuildCloudyTooltip(parent);
    }

    static Image CloudyImage(RectTransform parent, string name, Sprite sprite)
    {
        if (parent == null || sprite == null) return null;
        var current = parent.Find(name);
        var go = current != null ? current.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (current == null) go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
    }

    static void ConfigureCorner(Image image, bool right)
    {
        if (image == null) return;
        var r = image.rectTransform;
        float side = right ? 1f : 0f;
        r.anchorMin = r.anchorMax = new Vector2(side, 0f);
        r.pivot = new Vector2(side, 0f);
        r.anchoredPosition = new Vector2(right ? 17f : -17f, -7f);
        r.sizeDelta = new Vector2(174f, 104f);
        r.localScale = Vector3.one;
        // Draw above the frame, but never block controls.
        r.SetAsLastSibling();
    }

    void BuildCloudyTooltip(RectTransform parent)
    {
        var root = new GameObject("CloudyMapTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.layer = parent.gameObject.layer;
        cloudyTooltip = (RectTransform)root.transform;
        cloudyTooltip.SetParent(parent, false);
        cloudyTooltip.anchorMin = cloudyTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        cloudyTooltip.pivot = new Vector2(0f, 1f);
        cloudyTooltip.sizeDelta = new Vector2(230f, 64f);
        var image = root.GetComponent<Image>();
        image.sprite = cloudyPill;
        image.type = cloudyPill != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = cloudyPill != null ? Color.white : new Color32(255, 244, 218, 245);
        image.raycastTarget = false;
        cloudyTooltipGroup = root.GetComponent<CanvasGroup>();
        cloudyTooltipGroup.alpha = 0f;
        cloudyTooltipGroup.blocksRaycasts = false;
        cloudyTooltipGroup.interactable = false;
        cloudyTooltipTitle = CloudyTooltipText(cloudyTooltip, "Title", new Vector2(0f, -3f), 17f);
        cloudyTooltipStatus = CloudyTooltipText(cloudyTooltip, "Status", new Vector2(0f, -30f), 13f);
        cloudyTooltipStatus.color = new Color32(115, 93, 150, 255);
        cloudyTooltip.SetAsLastSibling();
    }

    TMP_Text CloudyTooltipText(RectTransform parent, string name, Vector2 offset, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(15f, offset.y - 28f);
        r.offsetMax = new Vector2(-15f, offset.y);
        var label = go.AddComponent<TextMeshProUGUI>();
        SetLabelFont(label);
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = UiCandy.Ink;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 11f;
        label.fontSizeMax = fontSize;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    // These methods are called by MapCloudyHover, which implements POINTER handlers only.
    // Drag/scroll events still bubble to MapDragSurface, keeping existing controls intact.
    public void ShowCloudyMapTooltip(string title, string status, Vector2 screenPosition, Camera eventCamera)
    {
        if (!open || cloudyTooltip == null || cloudyTooltipGroup == null) return;
        cloudyTooltipTitle.text = title;
        cloudyTooltipStatus.text = status;
        RectTransform parent = cloudyTooltip.parent as RectTransform;
        if (parent == null) return;
        Vector2 point;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out point)) return;
        float w = parent.rect.width, h = parent.rect.height;
        cloudyTooltip.pivot = new Vector2(0f, 1f);
        cloudyTooltip.anchoredPosition = new Vector2(Mathf.Clamp(point.x + 20f, -w * 0.5f + 5f, w * 0.5f - 235f),
            Mathf.Clamp(point.y + 72f, -h * 0.5f + 70f, h * 0.5f - 5f));
        cloudyTooltipGroup.alpha = 1f;
    }

    public void HideCloudyMapTooltip()
    {
        if (cloudyTooltipGroup != null) cloudyTooltipGroup.alpha = 0f;
    }

    // Called after base/Candy marker setup, before the standard Graphic.raycastTarget reset.
    void CloudyStyleMarker(RectTransform marker, Poi p, bool big)
    {
        if (!cloudyMapV2 || !candyStyle || marker == null || p == null) return;
        bool unknown = p.kind.key == "Mystery";
        bool done = p.completed;
        var icon = marker.Find("Icon") as RectTransform;
        if (icon != null && icon.GetComponent<Image>() != null)
        {
            var im = icon.GetComponent<Image>();
            im.preserveAspect = true;
            im.color = Color.white;
            if (CloudyContainsAny(p.kind.key, "Portal", "Workyard"))
            {
                var special = CloudySprite(CloudyContainsAny(p.kind.key, "Portal") ? "Portal" : "Workyard");
                if (special != null) im.sprite = special;
            }
        }
        var background = marker.Find("Back");
        if (background != null) background.gameObject.SetActive(false);
        var pin = marker.Find("Pin");
        var pinImage = pin != null ? pin.GetComponent<Image>() : null;
        if (pinImage != null)
        {
            if (unknown) pinImage.color = p.nearby ? new Color32(255, 220, 171, 255)
                : new Color32(206, 187, 255, 255); // discovered / unknown
            else if (done && CloudyIsInspection(p)) pinImage.color = new Color32(163, 237, 197, 255);
            else if (done) pinImage.color = new Color32(255, 219, 154, 255);
            else pinImage.color = new Color32(209, 230, 255, 255);
        }
        if (!big) return;

        var label = marker.Find("Label") as RectTransform;
        if (label != null)
        {
            label.anchorMin = label.anchorMax = Vector2.one * 0.5f;
            label.pivot = Vector2.one * 0.5f;
            label.anchoredPosition = new Vector2(0f, -44f);
            var tx = label.GetComponent<TMP_Text>();
            if (tx != null)
            {
                SetLabelFont(tx);
                tx.fontSize = 15f;
                tx.enableAutoSizing = true;
                tx.fontSizeMin = 12f;
                tx.fontSizeMax = 15f;
                tx.overflowMode = TextOverflowModes.Ellipsis;
                tx.outlineWidth = 0f;
                tx.color = new Color32(91, 53, 108, 255);
                label.sizeDelta = new Vector2(Mathf.Clamp(tx.preferredWidth + 25f, 74f, 182f), 31f);
            }
            if (cloudyPill != null)
            {
                var plate = CloudyImage(marker, "CloudyLabelPlate", cloudyPill);
                plate.type = Image.Type.Sliced;
                plate.preserveAspect = false;
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = label.anchorMin;
                plate.rectTransform.pivot = label.pivot;
                plate.rectTransform.sizeDelta = label.sizeDelta;
                plate.rectTransform.anchoredPosition = label.anchoredPosition;
                plate.rectTransform.SetSiblingIndex(label.GetSiblingIndex());
            }
        }
        string state = done ? "complete" : unknown ? "unknown" : "visible";
        string id = (p.t != null ? p.t.GetInstanceID().ToString() : p.label) + "|" + state;
        if (!cloudySeen.Contains(id))
        {
            if (cloudyInitialScanComplete && cloudyAnimateUnlocks && done)
                cloudyNewPins[marker] = -1f; // start animation when map is next visible
            cloudySeen.Add(id);
        }
    }

    void CloudySetupHover(RectTransform marker, Poi p, bool big)
    {
        if (!cloudyMapV2 || !candyStyle || !big || marker == null) return;
        var target = marker.GetComponent<Image>();
        if (target == null) target = marker.gameObject.AddComponent<Image>();
        target.color = new Color(1f, 1f, 1f, 0f);
        target.raycastTarget = true;
        var handler = marker.gameObject.AddComponent<MapCloudyHover>();
        handler.owner = this;
        bool mystery = p.kind.key == "Mystery";
        bool wasInspected = p.completed && CloudyIsInspection(p);
        handler.title = mystery ? "Таинственное место" : p.label;
        handler.state = mystery ? (p.nearby ? "Обнаружено • можно осмотреть" : "Неизвестно")
            : p.completed ? (wasInspected ? "Изучено" : "Построено / открыто") : "Место на острове";
    }

    static bool CloudyIsInspection(Poi p)
    {
        if (p == null || p.t == null || p.kind == null) return false;
        var authored = p.t.GetComponent<MapPoi>();
        return (authored != null && authored.requiresInspection)
            || CloudyContainsAny(p.kind.key, "Discovery")
            || p.t.GetComponent<LandmarkTree>() != null
            || p.t.GetComponent<MysticProp>() != null
            || p.t.GetComponent<FinalIslandEasterEgg>() != null;
    }

    // Reuses the baked finale portal, shown only after the bridge finale is unlocked.
    // No new 3D object or save key. StoryFlow remains the authority for availability.
    void CloudyAddPortalPoi()
    {
        if (!cloudyMapV2) return;
        var story = StoryFlow.Instance;
        if (story == null || !story.MapFinaleAvailable) return;
        var portals = FindObjectsByType<FinalIslandPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            var portal = portals[i];
            if (portal == null || portal.Anchor == null || !portal.IsBaked) continue;
            // Avoid doubling a hand-authored portal marker.
            bool alreadyMarked = false;
            foreach (var other in pois)
                if (other.t == portal.Anchor && CloudyContainsAny(other.kind.key, "Portal"))
                { alreadyMarked = true; break; }
            if (!alreadyMarked)
            {
                var kind = new MapKind { key = "Portal", label = "Портал", icon = CloudySprite("Portal"),
                    tint = new Color32(198, 204, 255, 255) };
                Add(portal.Anchor, kind, story.MapFinaleInspected ? "Портал" : "Дальний портал",
                    completed: story.MapFinaleInspected);
            }
            break;
        }
    }

    void CloudyAddWorkyardPoi()
    {
        if (!cloudyMapV2) return;
        foreach (var camp in FindObjectsByType<WorkerCamp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camp == null || !camp.Unlocked || camp.Anchor == null) continue;
            bool alreadyMarked = false;
            foreach (var p in pois)
                if (p.t == camp.Anchor && CloudyContainsAny(p.kind.key, "Workyard"))
                { alreadyMarked = true; break; }
            if (!alreadyMarked)
            {
                var kind = new MapKind { key = "Workyard", label = "Артель лесорубов",
                    icon = CloudySprite("Workyard"), tint = new Color32(255, 215, 157, 255) };
                Add(camp.Anchor, kind, "Артель лесорубов", completed: true);
            }
        }
    }

    // Run after Rescan; do not overwrite old saves or rebuild 3D objects.
    void CloudyAfterRescan()
    {
        cloudyInitialScanComplete = true;
        cloudyNeedsLayout = true;
        cloudyZonePlates.Clear();
        cloudyDead.Clear();
        foreach (var p in cloudyNewPins)
            if (p.Key == null) cloudyDead.Add(p.Key);
        for (int i = 0; i < cloudyDead.Count; i++) cloudyNewPins.Remove(cloudyDead[i]);
    }

    void CloudyRefreshMarkers()
    {
        cloudyNeedsLayout = true;
        HideCloudyMapTooltip();
        foreach (var p in pois)
        {
            if (p.big == null) continue;
            var hover = p.big.GetComponent<MapCloudyHover>();
            if (hover != null && p.kind.key == "Mystery")
                hover.state = p.nearby ? "Обнаружено • можно осмотреть" : "Неизвестно";
        }
    }

    void UpdateCloudyMapV2(float time)
    {
        if (!cloudyMapV2 || !candyStyle) return;
        if (!open) { HideCloudyMapTooltip(); return; }
        if (cloudyNeedsLayout)
        {
            cloudyNeedsLayout = false;
            LayoutCloudyMapLabels(1f / Mathf.Pow(Mathf.Max(1f, zoom), 0.85f));
        }
        if (cloudyNewPins.Count == 0) return;
        cloudyDead.Clear();
        float scale = 1.25f * markerScale / Mathf.Pow(Mathf.Max(1f, zoom), 0.85f);
        // Iterate over a snapshot: changing a Dictionary value during foreach is unsafe on Unity/Mono.
        var animated = new List<RectTransform>(cloudyNewPins.Keys);
        foreach (var rt in animated)
        {
            if (rt == null) { cloudyDead.Add(rt); continue; }
            if (!rt.gameObject.activeInHierarchy) continue;
            float born = cloudyNewPins[rt];
            if (born < 0f) { born = time; cloudyNewPins[rt] = born; }
            float progress = Mathf.Clamp01((time - born) / 0.43f);
            float spring = 1f + 0.18f * Mathf.Sin(progress * Mathf.PI * 1.5f) * (1f - progress);
            rt.localScale = Vector3.one * scale * Mathf.LerpUnclamped(0.45f, spring, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) { rt.localScale = Vector3.one * scale; cloudyDead.Add(rt); }
        }
        for (int i = 0; i < cloudyDead.Count; i++) cloudyNewPins.Remove(cloudyDead[i]);
    }

    static int CloudyPriority(Poi p)
    {
        if (p == null || p.kind == null) return 0;
        string key = p.kind.key ?? "";
        string label = p.label ?? "";
        if (CloudyContainsAny(key, "Portal", "Bridge") || CloudyContainsAny(label, "портал", "мост")) return 160;
        if (CloudyContainsAny(key, "Campfire", "Market", "Hut", "Tent", "Building") || p.completed) return 138;
        if (key == "Mystery") return p.nearby ? 106 : 68;
        if (CloudyContainsAny(key, "Quest", "Order", "Discovery")) return 105;
        if (CloudyContainsAny(key, "Forest", "Tree", "PAD_Forest")) return 35;
        return 74;
    }

    static bool CloudyContainsAny(string s, params string[] tokens)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (string t in tokens) if (s.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static Rect CloudyRect(Vector2 center, Vector2 size)
    {
        return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
    }

    bool CloudyCollides(Rect candidate)
    {
        for (int i = 0; i < cloudyOccupied.Count; i++)
        {
            Rect other = cloudyOccupied[i];
            if (candidate.xMin < other.xMax + cloudyLabelGap && candidate.xMax > other.xMin - cloudyLabelGap
                && candidate.yMin < other.yMax + cloudyLabelGap && candidate.yMax > other.yMin - cloudyLabelGap) return true;
        }
        return false;
    }

    bool CloudyInsideMap(Rect r, float margin)
    {
        float halfX = (bigViewport != null ? bigViewport.rect.width : 700f) * 0.5f - margin;
        float halfY = (bigViewport != null ? bigViewport.rect.height : 700f) * 0.5f - margin;
        return r.xMin >= -halfX && r.xMax <= halfX && r.yMin >= -halfY && r.yMax <= halfY;
    }

    static void CloudySetCluster(RectTransform marker, int count, Sprite sprite)
    {
        if (marker == null) return;
        var existing = marker.Find("CloudyCluster");
        if (count <= 0) { if (existing != null) existing.gameObject.SetActive(false); return; }
        RectTransform root;
        if (existing == null)
        {
            var go = new GameObject("CloudyCluster", typeof(RectTransform), typeof(Image));
            go.layer = marker.gameObject.layer;
            root = (RectTransform)go.transform;
            root.SetParent(marker, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = Vector2.one * 0.5f;
            root.anchoredPosition = new Vector2(18f, 18f);
            root.sizeDelta = new Vector2(22f, 22f);
            var im = go.GetComponent<Image>();
            im.sprite = sprite;
            im.color = Color.white;
            im.raycastTarget = false;
            var textGo = new GameObject("Count", typeof(RectTransform));
            textGo.layer = go.layer;
            var tr = (RectTransform)textGo.transform;
            tr.SetParent(root, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tx = textGo.AddComponent<TextMeshProUGUI>();
            tx.alignment = TextAlignmentOptions.Center;
            tx.fontSize = 11f;
            tx.fontStyle = FontStyles.Bold;
            tx.color = new Color32(70, 59, 110, 255);
            tx.raycastTarget = false;
        }
        else root = (RectTransform)existing;
        root.gameObject.SetActive(true);
        var text = root.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = "+" + count;
    }

    // Integrated decluttering: marker glyphs and zone/POI labels share ONE occupancy grid.
    // The first zoom level remains intentionally sparse; zooming in reveals detail.
    void LayoutCloudyMapLabels(float inv)
    {
        if (bigViewport == null || bigMap == null) return;
        cloudyNeedsLayout = false;
        cloudyOccupied.Clear();
        cloudyClusters.Clear();
        cloudyOrdered.Clear();
        float mapSize = bigMap.rectTransform.rect.width;
        float pinScale = 1.25f * markerScale * inv;
        float glyphSize = 34f * pinScale;
        float maxPinOffset = zoom < 1.6f ? 1.25f : 1.28f;
        foreach (var p in pois)
        {
            if (p.big == null) continue;
            bool visible = p.t != null && !FogHides(p);
            p.big.gameObject.SetActive(visible);
            if (visible) cloudyOrdered.Add(p);
            CloudySetCluster(p.big, 0, cloudyClusterSprite);
        }
        cloudyOrdered.Sort((a, b) =>
        {
            int pr = CloudyPriority(b).CompareTo(CloudyPriority(a));
            if (pr != 0) return pr;
            return string.Compare(a.label, b.label, StringComparison.Ordinal);
        });
        int limit = zoom < 1.35f ? 11 : zoom < 2.0f ? 19 : int.MaxValue;
        var visiblePins = new List<Poi>(cloudyOrdered.Count);
        for (int i = 0; i < cloudyOrdered.Count; i++)
        {
            Poi p = cloudyOrdered[i];
            Vector2 origin = ToMap(p.t.position) / side * mapSize;
            // Cluster only low-priority, tightly colocated markers at overview zoom.
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int j = 0; j < visiblePins.Count; j++)
            {
                var accepted = visiblePins[j];
                float distance = Vector2.Distance(origin, accepted.big.anchoredPosition);
                if (distance < nearestDistance) { nearestDistance = distance; nearest = j; }
            }
            bool tooClose = nearest >= 0 && nearestDistance < glyphSize * 1.32f;
            bool deEmphasize = (zoom < 1.9f && tooClose && CloudyPriority(p) < 139)
                || (i >= limit && CloudyPriority(p) < 139);
            if (deEmphasize)
            {
                p.big.gameObject.SetActive(false);
                if (nearest >= 0)
                {
                    Poi host = visiblePins[nearest];
                    int n;
                    cloudyClusters.TryGetValue(host, out n);
                    cloudyClusters[host] = n + 1;
                }
                continue;
            }
            Vector2 chosen = origin;
            bool placed = !CloudyCollides(CloudyRect(origin, Vector2.one * glyphSize));
            // Important colocated POIs may fan out (at most 1.3 glyph widths).
            // If there is no safe position, group instead of drawing overlapping glyphs.
            if (!placed)
            {
                Vector2[] tries = { new Vector2(1,0), new Vector2(-1,0), new Vector2(0,1), new Vector2(0,-1),
                    new Vector2(1,1).normalized, new Vector2(-1,1).normalized, new Vector2(-1,-1).normalized, new Vector2(1,-1).normalized };
                for (int ring = 1; ring <= 1 && !placed; ring++)
                    for (int j = 0; j < tries.Length; j++)
                    {
                        var candidate = origin + tries[j] * glyphSize * maxPinOffset * ring;
                        var box = CloudyRect(candidate, Vector2.one * glyphSize);
                        if (!CloudyInsideMap(box, 10f) || CloudyCollides(box)) continue;
                        chosen = candidate;
                        placed = true;
                        break;
                    }
            }
            if (!placed)
            {
                p.big.gameObject.SetActive(false);
                if (nearest >= 0)
                {
                    Poi host = visiblePins[nearest];
                    int n;
                    cloudyClusters.TryGetValue(host, out n);
                    cloudyClusters[host] = n + 1;
                }
                continue;
            }
            p.big.anchoredPosition = chosen;
            visiblePins.Add(p);
            cloudyOccupied.Add(CloudyRect(chosen, Vector2.one * glyphSize));
        }
        foreach (var group in cloudyClusters) CloudySetCluster(group.Key.big, group.Value, cloudyClusterSprite);

        // Important destinations get labels first. Lower-level detail appears with zoom.
        int labelLimit = zoom < 1.4f ? cloudyOverviewLabels : zoom < 1.95f ? 6 : zoom < 2.7f ? 11 : 30;
        int labels = 0;
        foreach (var p in visiblePins)
        {
            var label = p.big.Find("Label") as RectTransform;
            var plate = p.big.Find("CloudyLabelPlate") as RectTransform;
            if (label == null) continue;
            int priority = CloudyPriority(p);
            bool allowed = labels < labelLimit && (zoom >= 2.45f || priority >= 102 || (zoom >= 1.7f && priority >= 72));
            if (p.kind.key == "Mystery" && zoom < 2.3f) allowed = false;
            Vector2 pos = Vector2.zero;
            if (allowed && TryCloudyMarkerLabel(p.big, label, pinScale, out pos))
            {
                label.anchoredPosition = pos;
                label.gameObject.SetActive(true);
                if (plate != null) { plate.anchoredPosition = pos; plate.gameObject.SetActive(true); }
                labels++;
            }
            else
            {
                label.gameObject.SetActive(false);
                if (plate != null) plate.gameObject.SetActive(false);
            }
        }

        // Discovered zone names have lower priority than important destinations.
        // Unexplored zones keep their original '?' (from HudMinimap.Fog), but no wall of question marks.
        cloudyNames.Clear();
        if (zoneLayer == null) return;
        for (int i = 0; i < zoneLayer.childCount; i++)
        {
            var r = zoneLayer.GetChild(i) as RectTransform;
            if (r != null && r.name.StartsWith("Name_", StringComparison.Ordinal)) cloudyNames.Add(r);
        }
        cloudyNames.Sort((a, b) => CloudyZonePriority(b).CompareTo(CloudyZonePriority(a)));
        int zoneLimit = zoom < 1.45f ? 3 : zoom < 1.95f ? 5 : zoom < 2.75f ? 8 : 16;
        int zoneCount = 0;
        for (int i = 0; i < cloudyNames.Count; i++)
        {
            var r = cloudyNames[i];
            var text = r.GetComponent<TMP_Text>();
            if (text == null) continue;
            Vector2 basis;
            if (!zoneNameBase.TryGetValue(r, out basis)) { basis = r.anchoredPosition; zoneNameBase[r] = basis; }
            bool isMystery = text.text == "?";
            text.fontSize = isMystery ? 32f : 22f;
            text.outlineWidth = 0f;
            text.color = isMystery ? new Color32(135, 103, 194, 255) : new Color32(91, 53, 108, 255);
            r.sizeDelta = new Vector2(isMystery ? 64f : Mathf.Clamp(text.preferredWidth + 28f, 88f, 244f), isMystery ? 60f : 36f);
            r.localScale = Vector3.one * inv;
            var plate = CloudyZonePlate(r);
            if (plate != null)
            {
                plate.rectTransform.sizeDelta = r.sizeDelta;
                plate.rectTransform.localScale = Vector3.one * inv;
                var plateArt = isMystery ? CloudySprite("MysteryCloud") : cloudyPill;
                if (plateArt != null) plate.sprite = plateArt;
                plate.type = isMystery ? Image.Type.Simple : Image.Type.Sliced;
            }
            // Initialize before short-circuit evaluation; TryCloudyZoneLabel is not called when the label cap is reached.
            Vector2 candidate = basis;
            bool show = zoneCount < zoneLimit && TryCloudyZoneLabel(basis, r.sizeDelta * inv, out candidate);
            if (show)
            {
                r.anchoredPosition = candidate;
                r.gameObject.SetActive(true);
                if (plate != null)
                {
                    plate.rectTransform.anchoredPosition = candidate;
                    plate.gameObject.SetActive(true);
                }
                zoneCount++;
            }
            else
            {
                r.gameObject.SetActive(false);
                if (plate != null) plate.gameObject.SetActive(false);
            }
        }
    }

    bool TryCloudyMarkerLabel(RectTransform marker, RectTransform label, float pinScale, out Vector2 local)
    {
        // Candidate positions are in marker-local units; label rectangle measured in map-content units.
        Vector2[] placements = { new Vector2(0f,-47f), new Vector2(0f,47f), new Vector2(83f,0f), new Vector2(-83f,0f),
            new Vector2(77f,-43f), new Vector2(-77f,-43f), new Vector2(77f,43f), new Vector2(-77f,43f) };
        Vector2 size = label.sizeDelta * pinScale;
        for (int i = 0; i < placements.Length; i++)
        {
            Vector2 center = marker.anchoredPosition + placements[i] * pinScale;
            Rect box = CloudyRect(center, size);
            if (!CloudyInsideMap(box, 10f) || CloudyCollides(box)) continue;
            cloudyOccupied.Add(box);
            local = placements[i];
            return true;
        }
        local = Vector2.zero;
        return false;
    }

    int CloudyZonePriority(RectTransform r)
    {
        var tx = r.GetComponent<TMP_Text>();
        if (tx == null) return 0;
        if (tx.text == "?") return 22;
        if (CloudyContainsAny(tx.text, "Круг молчания", "Старый", "лагерь", "лес", "остров")) return 75;
        if (CloudyContainsAny(tx.text, "Речной", "Пруд")) return 56;
        return 45;
    }

    Image CloudyZonePlate(RectTransform name)
    {
        Image plate;
        if (cloudyZonePlates.TryGetValue(name, out plate) && plate != null) return plate;
        var go = new GameObject("CloudyPlate_" + name.name, typeof(RectTransform), typeof(Image));
        go.layer = name.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(zoneLayer, false);
        rt.anchorMin = name.anchorMin;
        rt.anchorMax = name.anchorMax;
        rt.pivot = name.pivot;
        rt.SetSiblingIndex(name.GetSiblingIndex());
        plate = go.GetComponent<Image>();
        plate.sprite = cloudyPill;
        plate.raycastTarget = false;
        cloudyZonePlates[name] = plate;
        return plate;
    }

    bool TryCloudyZoneLabel(Vector2 anchor, Vector2 size, out Vector2 candidate)
    {
        Vector2[] candidates = { Vector2.zero, Vector2.up * 38f, Vector2.down * 38f, Vector2.right * 35f,
            Vector2.left * 35f, new Vector2(36f, 35f), new Vector2(-36f, -35f) };
        for (int j = 0; j < candidates.Length; j++)
        {
            candidate = anchor + candidates[j];
            var rect = CloudyRect(candidate, size);
            if (!CloudyInsideMap(rect, 11f) || CloudyCollides(rect)) continue;
            cloudyOccupied.Add(rect);
            return true;
        }
        candidate = anchor;
        return false;
    }
}

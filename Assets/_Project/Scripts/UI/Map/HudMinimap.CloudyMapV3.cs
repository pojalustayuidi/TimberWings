using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Cloudy Candy • Большая карта V3")]
    [Tooltip("Адаптивное окно, метки заданий, построек и осмотров.")]
    public bool cloudyMapV3 = true;
    public bool cloudyV3HideTechnicalNames = true;
    [Range(1, 10)] public int cloudyV3OverviewPins = 6;
    [Range(0f, 20f)] public float cloudyV3SafePadding = 10f;

    bool UseCloudyV3 => cloudyMapV2 && candyStyle && cloudyMapV3;

    Vector2 cloudyLastPan, cloudyLastPlayer;
    Vector3 cloudyLastGoal;
    float cloudyLastZoom = -1f;
    readonly HashSet<string> cloudyZoneTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly List<Poi> cloudyPlaced = new List<Poi>();
    static readonly Vector2[] PinDirections =
    {
        Vector2.up, Vector2.right, Vector2.left, Vector2.down,
        new Vector2(.7071f, .7071f), new Vector2(-.7071f, .7071f),
        new Vector2(.7071f, -.7071f), new Vector2(-.7071f, -.7071f)
    };

    float BigMapCounterScale(float z)
    {
        z = Mathf.Max(1f, z);
        return UseCloudyV3 ? cloudyUiUnit / z : 1f / Mathf.Pow(z, .85f);
    }

    float BigPinScale(Poi p, float inverse)
    {
        return 1.25f * markerScale * inverse * (p.quest ? 1.1f : 1f);
    }

    void CloudyV3ObserveView()
    {
        Vector2 playerPoint = bigPlayer != null ? bigPlayer.anchoredPosition : Vector2.zero;
        Vector3 goal = questPin?.t != null ? questPin.t.position : Vector3.zero;
        if ((pan - cloudyLastPan).sqrMagnitude < 1f && (playerPoint - cloudyLastPlayer).sqrMagnitude < 4f
            && (goal - cloudyLastGoal).sqrMagnitude < .04f && Mathf.Abs(zoom - cloudyLastZoom) < .005f) return;
        cloudyLastPan = pan;
        cloudyLastPlayer = playerPoint;
        cloudyLastGoal = goal;
        cloudyLastZoom = zoom;
        cloudyNeedsLayout = true;
    }

    string CloudyV3DisplayLabel(Poi p)
    {
        if (p.quest || p.kind == MysteryKind || !cloudyV3HideTechnicalNames || !IsMapTechnicalName(p.label))
            return p.label;
        if (p.site == null) return null;
        return p.site.SiteKind switch
        {
            BuildSiteKind.Hut => "Хижина",
            BuildSiteKind.Market => "Лавка",
            BuildSiteKind.Campfire => "Костёр",
            BuildSiteKind.Tent => "Палатка",
            BuildSiteKind.Bridge => "Мост",
            _ => "Стройка"
        };
    }

    static bool IsMapTechnicalName(string title)
    {
        return string.IsNullOrWhiteSpace(title) ||
            CloudyContainsAny(title, "BuildZone", "BuildArea", "BuildSite", "_Point", "PAD_", "Site_", "Zone_",
                "Spawn", "Collider", "Trigger", "Debug", "(Clone)");
    }

    void CloudyV3StyleMarker(RectTransform marker, Poi p, bool big)
    {
        var icon = marker.Find("Icon") as RectTransform;
        var pin = marker.Find("Pin");
        if (icon != null && icon.TryGetComponent<Image>(out var iconImage))
        {
            if (p.quest || p.inspection)
            {
                string asset = p.quest ? "IconQuest" : p.completed ? "StateStudied" : "StateUnknown";
                Sprite art = CloudySprite(asset);
                if (art != null)
                {
                    iconImage.sprite = art;
                    if (p.inspection)
                    {
                        StretchMapRect(icon);
                        if (pin != null) pin.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                var id = MapIcons.Guess(p.kind.key);
                if (id is MapIcons.Id.Campfire or MapIcons.Id.Bridge)
                    iconImage.sprite = CloudySprite(id == MapIcons.Id.Campfire ? "IconCampfire" : "IconBridge") ?? iconImage.sprite;
            }
        }
        if (pin != null && pin.TryGetComponent<Image>(out var pinImage))
        {
            if (p.quest)
            {
                pinImage.sprite = CloudySprite("RoundButton") ?? pinImage.sprite;
                pinImage.color = new Color32(255, 232, 161, 255);
            }
            else if (p.site != null)
                pinImage.color = p.completed ? new Color32(185, 244, 214, 255) : new Color32(181, 222, 255, 255);
        }
        if (!big || marker.Find("Label") is not RectTransform label || !label.TryGetComponent<TMP_Text>(out var text)) return;
        text.text = CloudyV3DisplayLabel(p) ?? "";
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = MapInk;
        label.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 26f, 60f, 196f), 32f);
        if (marker.Find("CloudyLabelPlate") is RectTransform plate) plate.sizeDelta = label.sizeDelta;
    }

    string CloudyV3Status(Poi p)
    {
        if (p.quest) return mapObjective;
        if (p.inspection) return p.completed ? "Осмотрено" : "Где-то рядом есть находка";
        if (p.site != null)
        {
            if (p.completed) return "Построено";
            return p.site.StepsTotal > 1
                ? $"Этап {Mathf.Min(p.site.StepsDone + 1, p.site.StepsTotal)} из {p.site.StepsTotal}"
                : "Можно строить";
        }
        return p.completed ? "Открыто" : "";
    }

    bool TryPlaceMapBox(Vector2 anchor, Vector2 size, Rect viewport, float gap, out Vector2 position)
    {
        position = anchor;
        if (size.x > viewport.width || size.y > viewport.height) return false;
        Vector2 half = size * .5f;
        for (int i = -1; i < PinDirections.Length; i++)
        {
            Vector2 offset = i < 0 ? Vector2.zero : Vector2.Scale(PinDirections[i], Vector2.Min(size, Vector2.one * size.y * 1.4f));
            Vector2 point = anchor + offset;
            if (point.x - half.x < viewport.xMin || point.x + half.x > viewport.xMax
                || point.y - half.y < viewport.yMin || point.y + half.y > viewport.yMax) continue;
            Rect box = CloudyRect(point, size);
            if (CloudyCollides(box, gap)) continue;
            position = point;
            cloudyOccupied.Add(box);
            return true;
        }
        return false;
    }

    bool TryPlaceMapLabel(RectTransform marker, RectTransform label, float scale, Rect viewport, float gap, out Vector2 position)
    {
        Vector2 half = label.sizeDelta * scale * .5f;
        float iconHalf = marker.rect.width * scale * .5f + gap;
        Vector2 distance = half + Vector2.one * iconHalf;
        foreach (Vector2 direction in PinDirections)
        {
            Vector2 point = marker.anchoredPosition + Vector2.Scale(direction, distance);
            var box = CloudyRect(point, half * 2f);
            if (box.xMin < viewport.xMin || box.xMax > viewport.xMax || box.yMin < viewport.yMin || box.yMax > viewport.yMax
                || CloudyCollides(box, gap)) continue;
            cloudyOccupied.Add(box);
            position = point;
            return true;
        }
        position = marker.anchoredPosition;
        return false;
    }

    void CloudyV3Layout(float inverse)
    {
        if (bigViewport == null || bigMap == null) return;
        cloudyNeedsLayout = false;
        cloudyOccupied.Clear();
        cloudyOrdered.Clear();
        cloudyPlaced.Clear();
        float square = bigMap.rectTransform.rect.width;
        float margin = cloudyV3SafePadding * inverse * zoom;
        Vector2 visibleSize = bigViewport.rect.size / zoom;
        Rect viewport = CloudyRect(-pan / zoom, visibleSize - Vector2.one * (2f * margin / zoom));
        float gap = cloudyLabelGap * inverse * .55f;
        if (bigPlayer != null)
            cloudyOccupied.Add(CloudyRect(bigPlayer.anchoredPosition, bigPlayer.rect.size * (bigPlayerBase * inverse)));
        if (bigCompass != null && bigCompass.parent == cloudyClip && cloudyClip != bigWindow && bigCompass.gameObject.activeInHierarchy)
        {
            Vector2 point = new Vector2(-square, square) * .5f + bigCompass.anchoredPosition;
            cloudyOccupied.Add(CloudyRect((point - pan) / zoom, bigCompass.rect.size / zoom));
        }

        foreach (Poi p in pois)
        {
            if (p.big == null) continue;
            if (p.t == null || FogHides(p) || IsAtQuestPin(p)
                || (p.inspection && p.completed && zoom < 1.65f) || CloudyV3DisplayLabel(p) == null)
                p.big.gameObject.SetActive(false);
            else cloudyOrdered.Add(p);
        }
        cloudyOrdered.Sort((a, b) =>
        {
            int priority = CloudyPriority(b).CompareTo(CloudyPriority(a));
            if (priority != 0) return priority;
            int label = string.CompareOrdinal(a.label, b.label);
            return label != 0 ? label : a.t.GetInstanceID().CompareTo(b.t.GetInstanceID());
        });
        int budget = zoom < 1.45f ? Mathf.Min(cloudyV3OverviewPins, 6) : zoom < 2.25f ? 12 : 26;
        float templateWidth = Mathf.Max(34f, markerTemplate != null ? markerTemplate.rect.width : 34f);
        bool inspectionShown = false;
        foreach (Poi p in cloudyOrdered)
        {
            Vector2 anchor = ToMap(p.t.position) / side * square;
            float radius = templateWidth * BigPinScale(p, inverse);
            var placed = anchor;
            bool visible = (cloudyPlaced.Count < budget || p.quest || p == nearbyDiscovery)
                && viewport.Contains(anchor) && TryPlaceMapBox(anchor, Vector2.one * radius, viewport, gap, out placed);
            p.big.gameObject.SetActive(visible);
            if (!visible) continue;
            p.big.anchoredPosition = placed;
            cloudyPlaced.Add(p);
            if (p == nearbyDiscovery) inspectionShown = true;
            if (p.big.TryGetComponent<MapCloudyHover>(out var hover)) hover.state = CloudyV3Status(p);
        }
        LayoutMapLegend(questPin?.t != null, inspectionShown);

        int poiLabels = zoom < 1.45f ? 1 : zoom < 2.25f ? 4 : 9;
        foreach (Poi p in cloudyPlaced)
        {
            if (p.big.Find("Label") is not RectTransform label) continue;
            var plate = p.big.Find("CloudyLabelPlate") as RectTransform;
            float scale = BigPinScale(p, inverse);
            Vector2 placed = p.big.anchoredPosition;
            bool show = poiLabels > 0 && !p.quest && p.kind != MysteryKind
                && TryPlaceMapLabel(p.big, label, scale, viewport, gap, out placed);
            label.gameObject.SetActive(show);
            if (plate != null) plate.gameObject.SetActive(show);
            if (!show) continue;
            label.anchoredPosition = (placed - p.big.anchoredPosition) / scale;
            if (plate != null) plate.anchoredPosition = label.anchoredPosition;
            poiLabels--;
        }
        LayoutCloudyV3Zones(inverse, viewport, gap, zoom < 1.45f ? Mathf.Min(3, cloudyOverviewLabels) : zoom < 2.3f ? 4 : 7);
    }

    void LayoutCloudyV3Zones(float inverse, Rect viewport, float gap, int budget)
    {
        if (zoneLayer == null) return;
        cloudyNames.Clear();
        cloudyZoneTitles.Clear();
        foreach (RectTransform label in zoneNameBase.Keys) cloudyNames.Add(label);
        cloudyNames.Sort((a, b) => CloudyZonePriority(b).CompareTo(CloudyZonePriority(a)));
        foreach (RectTransform label in cloudyNames)
        {
            if (!label.TryGetComponent<TMP_Text>(out var text)) continue;
            string title = text.text;
            bool show = budget > 0 && title != "?" && (!cloudyV3HideTechnicalNames || !IsMapTechnicalName(title))
                && !cloudyZoneTitles.Contains(title);
            Vector2 placed = zoneNameBase[label];
            if (show)
            {
                text.fontSize = 20f;
                text.outlineWidth = 0f;
                text.color = MapInk;
                text.enableAutoSizing = true;
                text.fontSizeMin = 15f;
                text.fontSizeMax = 20f;
                text.overflowMode = TextOverflowModes.Ellipsis;
                label.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 28f, 75f, 235f), 36f);
                show = viewport.Contains(placed) && TryPlaceMapBox(placed, label.sizeDelta * inverse, viewport, gap, out placed);
            }
            label.gameObject.SetActive(show);
            if (!show)
            {
                if (cloudyZonePlates.TryGetValue(label, out var old) && old != null) old.gameObject.SetActive(false);
                continue;
            }
            label.anchoredPosition = placed;
            label.localScale = Vector3.one * inverse;
            var plate = CloudyZonePlate(label);
            plate.type = Image.Type.Sliced;
            plate.rectTransform.sizeDelta = label.sizeDelta;
            plate.rectTransform.localScale = label.localScale;
            plate.rectTransform.anchoredPosition = placed;
            plate.gameObject.SetActive(true);
            cloudyZoneTitles.Add(title);
            budget--;
        }
    }
}

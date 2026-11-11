using System;
using System.Collections.Generic;
using UnityEngine;

public partial class HudMinimap
{
    readonly HashSet<string> inspected = new HashSet<string>(StringComparer.Ordinal);
    readonly List<Poi> nearbyDiscoveries = new List<Poi>();
    bool progressReady, finaleWasInspected;
    MapZone generatedFinaleZone;
    Transform generatedZoneRoot;

    static readonly MapKind MysteryKind = new MapKind { key = "Mystery", label = "Можно осмотреть" };
    static readonly MapKind DiscoveryKind = new MapKind { key = "Discovery" };

    static MapIcons.Id SiteIcon(BuildSiteKind kind)
    {
        switch (kind)
        {
            case BuildSiteKind.Campfire: return MapIcons.Id.Campfire;
            case BuildSiteKind.Hut: return MapIcons.Id.Hut;
            case BuildSiteKind.Market: return MapIcons.Id.Market;
            case BuildSiteKind.Tent: return MapIcons.Id.Tent;
            case BuildSiteKind.Bridge: return MapIcons.Id.Bridge;
            default: return MapIcons.Id.Building;
        }
    }

    static MapKind BuildingKind(BuildingController site)
    {
        var id = SiteIcon(site.SiteKind);
        return new MapKind { key = id.ToString(), icon = MapIcons.Get(id), tint = MapIcons.PinTint(id) };
    }

    static bool IsLegacyBuildingPin(Transform t, MapKind kind, BuildingController[] sites)
    {
        MapIcons.Id id = MapIcons.Guess(kind.key);
        if (id != MapIcons.Id.Campfire && id != MapIcons.Id.Hut && id != MapIcons.Id.Market
            && id != MapIcons.Id.Tent && id != MapIcons.Id.Bridge && id != MapIcons.Id.Building) return false;
        foreach (var site in sites)
        {
            if (site == null || id != SiteIcon(site.SiteKind)) continue;
            if (site.transform == t || t.IsChildOf(site.transform) || site.transform.IsChildOf(t)) return true;
            var finished = site.finishedBuilding;
            if (finished != null && (t == finished.transform || t.IsChildOf(finished.transform))) return true;
            Vector3 delta = site.transform.position - t.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 18f * 18f) return true;
        }
        return false;
    }

    void AddBuildingPins(BuildingController[] sites)
    {
        foreach (var site in sites)
        {
            if (site == null || !site.IsFinished) continue;
            Transform anchor = site.finishedBuilding != null && site.finishedBuilding.activeInHierarchy
                ? site.finishedBuilding.transform : site.transform;
            if (site.SiteKind == BuildSiteKind.Bridge) anchor = site.transform;
            Add(anchor, BuildingKind(site), site.BuildingTitle, completed: true);
        }
    }

    bool AddAuthoredDiscovery(MapPoi marker, MapKind kind, StoryFlow story)
    {
        var target = marker.transform;
        if (story != null && target == story.MapFinaleTarget && story.MapFinaleAvailable)
        {
            string title = string.IsNullOrEmpty(marker.label) ? "След Гуннара" : marker.label;
            if (story.MapFinaleInspected) Add(target, kind ?? DiscoveryKind, title, completed: true);
            else AddDiscovery(target, kind ?? DiscoveryKind, title, marker.inspectionId, marker.hintRadius);
            return true;
        }

        string label = null;
        if (marker.TryGetComponent<LandmarkTree>(out var tree)) label = tree.title;
        else if (marker.TryGetComponent<MysticProp>(out var prop)) label = prop.InspectionTitle;
        else if (marker.TryGetComponent<FinalIslandEasterEgg>(out _)) label = "Зарубка Гуннара";
        if (!marker.requiresInspection && label == null) return false;
        if (!string.IsNullOrEmpty(marker.label)) label = marker.label;
        AddDiscovery(target, kind ?? DiscoveryKind, label ?? kind?.label ?? "Осмотрено", marker.inspectionId, marker.hintRadius);
        return true;
    }

    void AddDiscoveryPins(HashSet<Transform> authored)
    {
        foreach (var tree in LandmarkTree.All)
            if (tree != null && !authored.Contains(tree.transform))
                AddDiscovery(tree.transform, DiscoveryKind, tree.title, null, Mathf.Max(12f, tree.Radius + 2f));

        foreach (var prop in FindObjectsByType<MysticProp>(FindObjectsSortMode.None))
            if (prop.InspectionTitle != null && !authored.Contains(prop.transform))
                AddDiscovery(prop.transform, DiscoveryKind, prop.InspectionTitle, null, 9f);

        foreach (var egg in FindObjectsByType<FinalIslandEasterEgg>(FindObjectsSortMode.None))
            if (!authored.Contains(egg.transform))
                AddDiscovery(egg.transform, DiscoveryKind, "Зарубка Гуннара", null, 9f);

        var story = StoryFlow.Instance;
        finaleWasInspected = story != null && story.MapFinaleInspected;
        if (story != null && story.PetsOpen && story.MapChapterCompleted("pets")
            && story.petCrateSpot != null && !authored.Contains(story.petCrateSpot))
            Add(story.petCrateSpot, new MapKind { key = "Pets" }, "Питомцы", completed: true);
        if (story == null || !story.MapFinaleAvailable) return;
        var target = story.MapFinaleTarget;
        if (target == null || authored.Contains(target)) return;
        if (story.MapFinaleInspected)
            Add(target, DiscoveryKind, "След Гуннара", completed: true);
        else
            AddDiscovery(target, DiscoveryKind, "След Гуннара", null, 13f);
    }

    void AddDiscovery(Transform target, MapKind kind, string label, string id, float radius)
    {
        string key = MapPoi.GetInspectionId(target, id);
        bool done = inspected.Contains(key);
        var pin = Add(target, done ? kind : MysteryKind, done ? label : MysteryKind.label, completed: done);
        if (done) return;
        pin.hintRadius = Mathf.Max(1f, radius);
        pin.nearby = IsNearDiscovery(pin);
        nearbyDiscoveries.Add(pin);
    }

    bool IsNearDiscovery(Poi pin)
    {
        if (player == null || pin.t == null || !pin.t.gameObject.activeInHierarchy) return false;
        Vector3 delta = player.position - pin.t.position;
        return Mathf.Abs(delta.y) < 6f && delta.x * delta.x + delta.z * delta.z <= pin.hintRadius * pin.hintRadius;
    }

    void UpdateDiscoveryHints()
    {
        bool changed = false;
        foreach (var pin in nearbyDiscoveries)
        {
            bool nearby = IsNearDiscovery(pin);
            if (pin.nearby == nearby) continue;
            pin.nearby = nearby;
            changed = true;
        }
        if (changed && open) RefreshBigMarkers();
    }

    void OnPoiInspected(Transform target, string id)
    {
        if (!progressReady || !inspected.Add(id)) return;
        DiscoverZone(MapZone.At(target.position));
        SaveSystem.RequestSave("minimap");
        rescanPending = true;
    }

    void EnsureMapZones(BuildingController[] sites, Dictionary<string, Transform> anchors)
    {
        foreach (var site in sites)
        {
            if (site == null) continue;
            switch (site.SiteKind)
            {
                case BuildSiteKind.Campfire:
                    EnsureMapZone("Старый Стапель", site.transform.position, 17f, new Color32(243, 170, 112, 255));
                    break;
                case BuildSiteKind.Tent:
                    EnsureMapZone("Лагерь у палатки", site.transform.position, 14f, new Color32(219, 174, 111, 255));
                    break;
            }
        }

        if (IntroFlow.Instance != null && IntroFlow.Instance.campfirePoint != null)
            EnsureMapZone("Старый Стапель", IntroFlow.Instance.campfirePoint.position, 17f, new Color32(243, 170, 112, 255));
        EnsureAnchoredZone(anchors, "TW_MYSTIC_FOREST", "Круг молчания", 16f, new Color32(158, 119, 216, 255));
        EnsureAnchoredZone(anchors, "PAD_Grandpa", "Круг молчания", 16f, new Color32(158, 119, 216, 255));
        EnsureAnchoredZone(anchors, "PAD_Forest", "Кедровый Бор", 17f, new Color32(100, 186, 131, 255));
        EnsureAnchoredZone(anchors, "PAD_GroveSouth", "Южная роща", 16f, new Color32(88, 176, 152, 255));
        EnsureAnchoredZone(anchors, "Pond_Water", "Тихий Пруд", 16f, new Color32(94, 191, 202, 255));
        EnsureAnchoredZone(anchors, "Pond", "Тихий Пруд", 16f, new Color32(94, 191, 202, 255));
        var riverColor = new Color32(87, 166, 230, 255);
        if (anchors.ContainsKey("River_Water"))
            EnsureAnchoredZone(anchors, "River_Water", "Речной берег", 15f, riverColor);
        else
        {
            foreach (var volume in WaterVolume.All)
            {
                if (volume == null || !volume.TryGetMapSection(volume.PointCount / 2, out var left, out var right)) continue;
                EnsureMapZone("Речной берег", (left + right) * 0.5f, 15f, riverColor);
                break;
            }
        }

        bool finaleOpen = StoryFlow.Instance != null && StoryFlow.Instance.MapFinaleAvailable;
        if (generatedFinaleZone != null) generatedFinaleZone.gameObject.SetActive(finaleOpen);
        if (finaleOpen && generatedFinaleZone == null && mapGate != null && mapGate.HasFinalIsland)
            generatedFinaleZone = EnsureMapZone("Дальний остров", mapGate.arrivalSpot.parent.position,
                mapGate.islandRadius + 1f, new Color32(174, 156, 233, 255));
    }

    void EnsureAnchoredZone(Dictionary<string, Transform> anchors, string anchor, string title, float radius, Color tint)
    {
        if (!anchors.TryGetValue(anchor, out var t) || t == null) return;
        var renderer = t.GetComponent<Renderer>();
        EnsureMapZone(title, renderer != null ? renderer.bounds.center : t.position, radius, tint);
    }

    MapZone EnsureMapZone(string title, Vector3 position, float radius, Color tint)
    {
        foreach (var zone in MapZone.All)
        {
            if (zone == null || !zone.showOnMap) continue;
            if (string.Equals(zone.zoneName, title, StringComparison.OrdinalIgnoreCase)) return null;
        }

        if (generatedZoneRoot == null)
        {
            var root = new GameObject("MinimapWorldZones") { hideFlags = HideFlags.DontSave };
            var scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && root.scene != scene && scene.name != "DontDestroyOnLoad")
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            generatedZoneRoot = root.transform;
        }
        var go = new GameObject("MapZone_" + title);
        go.transform.SetParent(generatedZoneRoot, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        var generated = go.AddComponent<MapZone>();
        generated.zoneName = title;
        generated.radius = radius;
        generated.color = tint;
        // Локальная достопримечательность имеет приоритет над большой зоной биома.
        generated.priority = 25;
        return generated;
    }
}

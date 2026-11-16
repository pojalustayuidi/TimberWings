using System;
using System.Collections.Generic;
using UnityEngine;

public struct BuildWorkSpot
{
    public Vector3 stand;     // position of the player root
    public Vector3 hit;       // point on the visible material
    public Vector3 normal;    // actual surface normal, facing the player
    public float radius;      // framing radius, not a reach limit
    public float groundY;
    internal Renderer surface;
}

public struct BuildStrikeInfo
{
    public BuildingController site;
    public Vector3 point;
    public Vector3 normal;
    public Vector3 ground;
    public float power;
    public float progress;
    public int index;
}

public static class BuildStrikes
{
    public static event Action<BuildStrikeInfo> Struck;
    public static void Raise(BuildStrikeInfo strike) => Struck?.Invoke(strike);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Struck = null;
}

internal static class BuildWorkSpotFinder
{
    const float WalkRange = 3.1f;
    const float WorkHeight = .90f;
    const float BoardDepth = .24f;
    static readonly float[] Heights = { WorkHeight, .73f, 1.04f };
    static readonly float[] WorkpieceAngles = { 0f, -45f, 45f, -90f, 90f, 180f };
    static readonly string[] Decoration = { "ground", "shadow", "glow", "flame", "foliage", "crew", "pathstone", "garland" };
    static readonly RaycastHit[] groundHits = new RaycastHit[16];
    static readonly Collider[] obstacles = new Collider[24];
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<int> indices = new List<int>();
    static readonly List<Contact> candidates = new List<Contact>();
    static readonly Contact[] closest = new Contact[Heights.Length];

    struct Contact
    {
        public Vector3 point, normal;
        public float score;
        public Renderer renderer;
    }

    static Transform GeometryRoot(BuildingController site)
    {
        if (site.stages != null) return site.stages.transform;
        var link = site.GetComponent<BuildStageLink>();
        var gate = site.GetComponentInParent<GateBridge>();
        if (gate != null && gate.stages != null)
        {
            int stage = gate.Stage - 1;
            if (stage >= 0 && stage < gate.stages.Length && gate.stages[stage] != null)
                return gate.stages[stage].transform;
        }
        if (link != null && link.bridge != null) return link.bridge.transform;
        var tent = link != null && link.tent != null ? link.tent : site.GetComponentInParent<TentStages>();
        if (tent != null && tent.stage1 != null) return tent.stage1.transform;
        var shop = link != null && link.shop != null ? link.shop : site.GetComponentInParent<ShopStages>();
        if (shop != null) return shop.transform;
        return site.transform;
    }

    static bool IsWorkMesh(Transform item, Transform root, bool campfire)
    {
        bool fuel = !campfire;
        for (Transform node = item; node != null; node = node.parent)
        {
            string name = node.name;
            foreach (string word in Decoration)
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("fuel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0) fuel = true;
            if (node == root) break;
        }
        return fuel;
    }

    public static bool TryFind(BuildingController site, PlayerMovement player, CapsuleCollider capsule,
        out BuildWorkSpot spot)
    {
        spot = default;
        Transform root = GeometryRoot(site);
        Vector3 feet = Feet(player, capsule);
        float scale = player.model.lossyScale.y;
        var region = new Bounds(feet + Vector3.up * WorkHeight * scale,
            new Vector3(WalkRange * 2f, .5f * scale, WalkRange * 2f));
        bool campfire = site.SiteKind == BuildSiteKind.Campfire;

        candidates.Clear();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (!filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled ||
                !renderer.bounds.Intersects(region) || !IsWorkMesh(filter.transform, root, campfire)) continue;
            var mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            CollectContacts(filter, renderer, mesh, feet, scale, region);
        }

        candidates.Sort((a, b) => a.score.CompareTo(b.score));
        int count = Mathf.Min(12, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            if (!TryStand(candidates[i], player, capsule, feet, scale, out spot)) continue;
            candidates.Clear();
            return true;
        }
        candidates.Clear();
        return false;
    }

    static void CollectContacts(MeshFilter filter, MeshRenderer renderer, Mesh mesh, Vector3 feet,
        float scale, Bounds region)
    {
        // Merged models (including the bridge) have empty space inside their bounds.
        // Intersect actual triangles with the hand-height planes, once per approach.
        mesh.GetVertices(vertices);
        var toWorld = filter.transform.localToWorldMatrix;
        for (int i = 0; i < vertices.Count; i++) vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
        for (int i = 0; i < closest.Length; i++) closest[i] = default;
        float minY = region.min.y, maxY = region.max.y;

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
            mesh.GetTriangles(indices, sub, true);
            for (int i = 0; i < indices.Count; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < minY ||
                    Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > maxY) continue;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal == Vector3.zero || Mathf.Abs(normal.y) > .70f) continue;

                for (int h = 0; h < Heights.Length; h++)
                {
                    float y = feet.y + Heights[h] * scale;
                    if (!TrySlice(a, b, c, y, feet, out Vector3 point)) continue;
                    Vector3 outward = Flat(feet - point);
                    float distance = outward.sqrMagnitude;
                    if (distance > WalkRange * WalkRange || Vector3.Dot(normal, outward) <= .01f) continue;
                    float score = distance + .40f * Mathf.Abs(Heights[h] - WorkHeight);
                    if (closest[h].renderer != null && score >= closest[h].score) continue;
                    closest[h] = new Contact { point = point, normal = normal, renderer = renderer, score = score };
                }
            }
        }
        foreach (var contact in closest)
            if (contact.renderer != null) candidates.Add(contact);
    }

    static bool TrySlice(Vector3 a, Vector3 b, Vector3 c, float y, Vector3 feet, out Vector3 point)
    {
        point = default;
        Vector3 first = default, second = default;
        int count = 0;
        SliceEdge(a, b, y, ref count, ref first, ref second);
        SliceEdge(b, c, y, ref count, ref first, ref second);
        SliceEdge(c, a, y, ref count, ref first, ref second);
        if (count < 2) return false;
        Vector3 span = second - first;
        float length = span.sqrMagnitude;
        point = length > 1e-10f
            ? first + span * Mathf.Clamp01(Vector3.Dot(feet - first, span) / length) : first;
        return true;
    }

    static void SliceEdge(Vector3 a, Vector3 b, float y, ref int count, ref Vector3 first, ref Vector3 second)
    {
        if ((a.y < y) == (b.y < y)) return;
        Vector3 point = Vector3.LerpUnclamped(a, b, (y - a.y) / (b.y - a.y));
        if (count++ == 0) first = point; else second = point;
    }

    static bool TryStand(Contact contact, PlayerMovement player, CapsuleCollider capsule, Vector3 feet,
        float scale, out BuildWorkSpot spot)
    {
        spot = default;
        float radius = capsule.bounds.extents.x;
        Vector3 outwards = Flat(contact.normal).normalized;
        Vector3 right = Vector3.Cross(outwards, Vector3.up);
        Vector3 stand = contact.point + outwards * StandOff(radius, scale) - right * (.12f * scale);
        stand.y = feet.y;
        Vector3 delta = Flat(stand - feet);
        if (delta.sqrMagnitude > WalkRange * WalkRange) return false;

        // Construction uses a short direct step. Check the whole route as well as both edges
        // of the standing footprint, so the player cannot be led onto an unfinished bridge.
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .29f));
        float ground = feet.y;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 sample = feet + delta * (i / (float)steps);
            sample.y = ground;
            if (!TrySupport(sample, player.transform, radius * .72f, out ground) ||
                !HasClearance(sample, ground, radius, capsule.bounds.size.y, player.transform)) return false;
        }

        float height = (contact.point.y - ground) / scale;
        if (height < .62f || height > 1.16f) return false;
        stand.y = ground + (player.transform.position.y - feet.y);
        spot = new BuildWorkSpot { stand = stand, hit = contact.point, normal = contact.normal,
            groundY = ground, radius = 1.8f, surface = contact.renderer };
        return true;
    }

    // A low or empty site can still have material to work on. A small trestle is
    // placed on solid ground next to Leo, with the strike target on the plank itself.
    public static bool TryPlacePlank(BuildingController site, PlayerMovement player, CapsuleCollider capsule,
        out BuildWorkSpot spot, out Vector3 origin, out Quaternion rotation)
    {
        spot = default;
        origin = default;
        rotation = Quaternion.identity;
        Vector3 feet = Feet(player, capsule);
        float scale = player.model.lossyScale.y;
        float radius = capsule.bounds.extents.x;
        if (!TrySupport(feet, player.transform, radius * .72f, out float ground)) return false;
        var gate = site.GetComponentInParent<GateBridge>();
        // The bridge meshes are baked in world coordinates; their transform is not the entrance.
        Vector3 sitePoint = gate != null ? gate.transform.position : GeometryRoot(site).position;
        Vector3 forward = Flat(sitePoint - feet);
        if (forward.sqrMagnitude < .04f) forward = Flat(player.model.forward);
        forward.Normalize();

        foreach (float angle in WorkpieceAngles)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 pos = feet + direction * (StandOff(radius, scale) + BoardDepth * .5f * scale) +
                right * (.12f * scale);
            pos.y = ground;
            if (!TrySupport(pos, player.transform, .32f * scale, out float boardGround) ||
                Mathf.Abs(boardGround - ground) > .13f * scale ||
                !HasClearance(pos, boardGround, .38f * scale, scale, player.transform)) continue;

            pos.y = boardGround;
            origin = pos;
            rotation = Quaternion.LookRotation(direction, Vector3.up);
            spot = new BuildWorkSpot
            {
                stand = player.transform.position,
                hit = pos + Vector3.up * (WorkHeight * scale) - direction * (BoardDepth * .5f * scale),
                normal = -direction,
                groundY = boardGround,
                radius = 1.8f
            };
            return true;
        }
        return false;
    }

    public static Mesh CreateWorkPlank()
    {
        var wood = new Color(.63f, .39f, .22f);
        var end = new Color(.85f, .60f, .33f);
        var mesh = new GroveMesh(14);
        mesh.BoxBeam(new Vector3(-.27f, 0f, -.13f), new Vector3(-.22f, .91f, .02f), .11f, .13f, wood);
        mesh.BoxBeam(new Vector3(.27f, 0f, .20f), new Vector3(.22f, .91f, .02f), .11f, .13f, wood);
        mesh.Box(new Vector3(0f, .41f, .04f), new Vector3(.59f, .09f, .12f), wood, 0f);
        mesh.Box(new Vector3(0f, WorkHeight, 0f), new Vector3(.75f, .19f, BoardDepth), end, 0f);
        return mesh.ToMesh("Build work plank");
    }

    static float StandOff(float bodyRadius, float scale) => Mathf.Max(bodyRadius + .045f, .60f * scale);

    static Vector3 Feet(PlayerMovement player, CapsuleCollider capsule)
    {
        Vector3 feet = player.transform.position;
        feet.y = capsule.bounds.min.y;
        return feet;
    }

    static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

    static bool TrySupport(Vector3 pos, Transform player, float edge, out float height)
    {
        if (!TryGround(pos, player, out height)) return false;
        pos.y = height;
        return Supported(pos + Vector3.right * edge, player, height) &&
               Supported(pos - Vector3.right * edge, player, height) &&
               Supported(pos + Vector3.forward * edge, player, height) &&
               Supported(pos - Vector3.forward * edge, player, height);
    }

    static bool Supported(Vector3 pos, Transform player, float height)
        => TryGround(pos, player, out float y) && Mathf.Abs(y - height) < .23f;

    static bool TryGround(Vector3 pos, Transform player, out float height)
    {
        height = 0f;
        int count = Physics.RaycastNonAlloc(pos + Vector3.up * .36f, Vector3.down, groundHits, .70f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == groundHits.Length) return false;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider == null || hit.collider.attachedRigidbody != null ||
                hit.transform.IsChildOf(player) || hit.normal.y < .62f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            height = hit.point.y;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    static bool HasClearance(Vector3 pos, float ground, float radius, float height, Transform player)
    {
        pos.y = ground;
        int count = Physics.OverlapCapsuleNonAlloc(pos + Vector3.up * (radius + .08f),
            pos + Vector3.up * Mathf.Max(radius + .08f, height - radius), radius, obstacles,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == obstacles.Length) return false;
        for (int i = 0; i < count; i++)
            if (obstacles[i] != null && !obstacles[i].transform.IsChildOf(player)) return false;
        return true;
    }
}

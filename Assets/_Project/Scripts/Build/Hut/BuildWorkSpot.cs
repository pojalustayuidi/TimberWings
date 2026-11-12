// Assets/_Project/Scripts/Build/Hut/BuildWorkSpot.cs   (НЕ в папке Editor!)
// Рабочее место героя на стройке: у какой стены встать и в какую точку бить молотком.
//   BuildWorkSpotFinder.Compute(...)  считает место один раз, когда этап начался (BuildWorkPose)
//   BuildStrikes.Struck               событие «молоток ударил по зданию»: на него подписаны эффекты (BuildFxHook)
//
// Точка удара ищется так (от точного к грубому, первый сработавший вариант):
//   1) луч по коллайдерам самого здания (точная поверхность и нормаль);
//   2) луч по габаритам (bounds) каждой детали здания;
//   3) грань общего ящика здания.
using System;
using System.Collections.Generic;
using UnityEngine;

public struct BuildWorkSpot
{
    public bool valid;
    public bool approachAllowed; // false means hammer in place instead of risky auto-walk
    public Vector3 stand;     // куда встать герою (чуть снаружи от стены)
    public Vector3 hit;       // точка на стене, куда бьёт молоток
    public Vector3 normal;    // от стены наружу, по горизонтали
    public Vector3 center;    // центр здания (для камеры)
    public float radius;      // размер здания, метры
    public float groundY;     // высота земли у стены
}

// один удар молотка по зданию
public struct BuildStrikeInfo
{
    public BuildingController site;
    public Vector3 point;     // где молоток коснулся здания
    public Vector3 normal;    // наружу от стены
    public Vector3 ground;    // земля под ударом (пыль)
    public float power;       // 0.8 .. 1.9: растёт к концу этапа, каждый третий удар тяжелее
    public float progress;    // 0..1: сколько этапа уже построено
    public int index;         // номер удара на этом этапе, с нуля
}

public static class BuildStrikes
{
    public static event Action<BuildStrikeInfo> Struck;
    public static void Raise(BuildStrikeInfo s) { if (Struck != null) Struck(s); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Struck = null; }
}

public static class BuildWorkSpotFinder
{
    const float StandOff = 0.72f;     // насколько герой встаёт дальше от стены, чем точка удара (сам PlayerMovement не подходит ближе 0.3 м)
    const float HitHeight = 0.9f;     // высота удара над ногами героя (как у молотка в позе работы)
    static readonly RaycastHit[] hits = new RaycastHit[24];
    static readonly Collider[] obstacles = new Collider[32];
    static readonly List<Renderer> parts = new List<Renderer>();

    // Do not auto-walk the player into solid geometry or off the island.
    // Trigger zones are ignored; the player's own colliders are also ignored.
    static bool StandSafe(Vector3 pos, float feetY)
    {
        int count = Physics.OverlapCapsuleNonAlloc(pos + Vector3.up * .28f,
            pos + Vector3.up * 1.45f, .26f, obstacles,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Transform player = PlayerMovement.Instance != null ? PlayerMovement.Instance.transform : null;
        if (count == obstacles.Length) return false; // crowded: don't gamble on omitted colliders
        for (int i = 0; i < count; i++)
        {
            var c = obstacles[i];
            if (c == null || !c.enabled) continue;
            if (player != null && c.transform.IsChildOf(player)) continue;
            return false;
        }
        // A ground ray is required: an apparent stand point can lie outside a floating island.
        return Physics.Raycast(pos + Vector3.up * .65f, Vector3.down, 1.25f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // корень вида здания: стадии (HutStages), а у рынка, палатки и моста объект из BuildStageLink (так же, как в BuildReveal)
    public static Transform RootOf(BuildingController b)
    {
        if (b == null) return null;
        if (b.stages != null) return b.stages.transform;
        var link = b.GetComponent<BuildStageLink>();
        if (link != null)
        {
            if (link.shop != null) return link.shop.transform;
            if (link.tent != null) return link.tent.transform;
            if (link.bridge != null) return link.bridge.transform;
        }
        return null;
    }

    public static BuildWorkSpot Compute(BuildingController site, Vector3 playerPos, float feetY)
    {
        var s = new BuildWorkSpot();
        if (site == null) return s;

        Transform root = RootOf(site);
        Bounds bb = new Bounds();
        bool has = false;
        parts.Clear();
        if (root != null)
        {
            var rs = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                parts.Add(r);
                if (!has) { bb = r.bounds; has = true; } else bb.Encapsulate(r.bounds);
            }
        }
        if (!has)
        {
            var col = site.GetComponent<Collider>();
            if (col != null) { bb = col.bounds; has = true; }
        }
        if (!has) return s;

        // Мост и большие комплексы имеют единый огромный AABB. Подходить к его дальней
        // границе — значит уводить Лео с острова. Ищем ближайшую реальную деталь.
        if (Mathf.Max(bb.extents.x, bb.extents.z) > 8f)
        {
            Renderer closest = null;
            float bestDistanceSq = float.PositiveInfinity;
            Vector3 probe = playerPos + Vector3.up * HitHeight;
            for (int i = 0; i < parts.Count; i++)
            {
                Renderer r = parts[i];
                if (r == null || Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) > 5f) continue;
                Vector3 delta = r.bounds.ClosestPoint(probe) - probe;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d < bestDistanceSq) { bestDistanceSq = d; closest = r; }
            }
            if (closest == null || bestDistanceSq > 3.5f * 3.5f)
            {
                parts.Clear();
                return s; // слишком далеко: молоток работает рядом с игроком, не идёт через весь мост
            }
            bb = closest.bounds;
        }

        Vector3 c = bb.center;
        Vector3 e = bb.extents;

        // с какой стороны подходить: с той, где сейчас стоит герой (ближайшая грань ящика здания)
        float dx = playerPos.x - c.x, dz = playerPos.z - c.z;
        float outX = Mathf.Abs(dx) - e.x;      // > 0: герой снаружи по X
        float outZ = Mathf.Abs(dz) - e.z;
        Vector3 n = outX > outZ ? new Vector3(Mathf.Sign(dx), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dz));
        bool alongX = Mathf.Abs(n.x) > 0.5f;

        float lo = bb.min.y + 0.25f;
        float hi = Mathf.Max(lo, bb.max.y - 0.15f);
        float y = Mathf.Clamp(feetY + HitHeight, lo, hi);

        // точка на грани ящика: вдоль грани ближе к герою, но не в самом углу
        Vector3 hit = new Vector3(c.x + n.x * e.x, y, c.z + n.z * e.z);
        if (alongX) hit.z = Mathf.Clamp(playerPos.z, c.z - Mathf.Max(0f, e.z - 0.6f), c.z + Mathf.Max(0f, e.z - 0.6f));
        else hit.x = Mathf.Clamp(playerPos.x, c.x - Mathf.Max(0f, e.x - 0.6f), c.x + Mathf.Max(0f, e.x - 0.6f));

        // уточняем по настоящей поверхности: луч снаружи внутрь на высоте удара
        float depth = 2f * (alongX ? e.x : e.z);
        Ray ray = new Ray(hit + n * 2.5f, -n);
        float maxDist = 2.5f + depth + 0.5f;
        bool refined = false;

        if (root != null)
        {
            int cnt = Physics.RaycastNonAlloc(ray, hits, maxDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < cnt; i++)
            {
                var h = hits[i];
                if (h.collider == null || !h.collider.transform.IsChildOf(root)) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                hit = h.point;
                Vector3 hn = h.normal;
                hn.y = 0f;
                if (hn.sqrMagnitude > 0.25f) n = hn.normalized;   // крыша и пол (нормаль вверх) не годятся: остаётся нормаль грани
                refined = true;
            }
        }
        if (!refined)
        {
            float best = float.MaxValue;
            for (int i = 0; i < parts.Count; i++)
            {
                float d;
                if (parts[i] != null && parts[i].bounds.IntersectRay(ray, out d) && d < best) best = d;
            }
            if (best < float.MaxValue) hit = ray.GetPoint(best);
        }
        parts.Clear();

        Vector3 stand = hit + n * StandOff;
        stand.y = playerPos.y;
        Vector3 walkDelta = stand - playerPos;
        walkDelta.y = 0f;
        if (walkDelta.sqrMagnitude > 3.4f * 3.4f) return s;

        // Prefer the same contact point; move the player slightly farther away
        // instead of walking into the fire pit / hut collider. Never cross the edge.
        bool safe = false;
        for (int i = 0; i < 4; i++)
        {
            Vector3 candidate = stand + n * (.23f * i);
            if (!StandSafe(candidate, feetY)) continue;
            stand = candidate;
            safe = true;
            break;
        }
        // Keep the reliable hit location even when the approach point is unsafe:
        // visual work can continue from the hero's current place without walking off an island.
        s.approachAllowed = safe;
        s.valid = true;
        s.hit = hit;
        s.normal = n;
        s.stand = stand;
        s.center = c;
        s.radius = Mathf.Max(e.x, e.z, e.y * 0.8f);
        s.groundY = feetY;
        return s;
    }
}

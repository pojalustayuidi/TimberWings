using UnityEngine;

public partial class PlayerInteraction
{
    readonly RaycastHit[] fpRayHits = new RaycastHit[64];

    // Exact solid hit, not an expanded crown trigger or nearest-tree fallback.
    // No chopping through buildings, rock, or other non-resource obstacles.
    ResourceItem PickFirstPersonTree(Camera cam, out bool far, float maxPlanarDistance)
    {
        far = false;
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        int count = Physics.RaycastNonAlloc(ray, fpRayHits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        Collider hit = null;
        for (int i = 0; i < count; i++)
        {
            var c = fpRayHits[i].collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (fpRayHits[i].distance >= nearest) continue;
            nearest = fpRayHits[i].distance;
            hit = c;
        }
        if (hit == null) return null;
        var item = hit.GetComponentInParent<ResourceItem>();
        if (item == null || !item.gameObject.activeInHierarchy) return null;
        Vector3 d = item.transform.position - transform.position;
        d.y = 0f;
        if (maxPlanarDistance > 0f && d.sqrMagnitude > maxPlanarDistance * maxPlanarDistance) return null;
        far = d.sqrMagnitude > (radius + .5f) * (radius + .5f) || nearest > radius + .5f;
        return item;
    }
}

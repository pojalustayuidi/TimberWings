using UnityEngine;

internal enum FirstPersonAim { None, Tree, FarTree, Cut, FarCut, Collect, FarCollect }

public partial class PlayerInteraction
{
    readonly RaycastHit[] fpRayHits = new RaycastHit[64];
    internal FirstPersonAim FirstPersonFocus { get; private set; }
    Vector3 fpAimPoint;
    float fpObjectCheckAt;
    IInteractable fpObjectChecked, fpObjectAimed;

    internal bool PrepareFirstPersonSwitch()
    {
        if (IsChopping && !IsChop03Working) return false;
        ResetPrototypeInput();
        clickArmed = false;
        return true;
    }

    internal IInteractable ReadFirstPersonInteractable(Camera camera)
    {
        IInteractable current = InteractionHub.Current;
        if (current == null || current.Anchor == null || camera == null) return null;
        if (ReferenceEquals(current, fpObjectChecked) && Time.unscaledTime < fpObjectCheckAt) return fpObjectAimed;
        fpObjectChecked = current;
        fpObjectAimed = null;
        fpObjectCheckAt = Time.unscaledTime + .05f;

        Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
        var hit = ClosestPrototypeHit(ray, 12f, out var point);
        if (hit != null && (hit.transform.IsChildOf(current.Anchor) || current.Anchor.IsChildOf(hit.transform)))
            return fpObjectAimed = current;

        // Some story props only have triggers. Check their prompt span, with the same solid
        // ray as tree selection so an interactable behind a wall cannot change the reticle.
        Vector3 top = camera.WorldToViewportPoint(current.PromptPoint);
        Vector3 bottom = camera.WorldToViewportPoint(current.Anchor.position);
        if (top.z <= .1f || Mathf.Abs(top.x - .5f) > .055f) return null;
        float low = Mathf.Min(bottom.y, top.y) - .05f;
        float high = Mathf.Max(bottom.y, top.y) + .05f;
        if (.5f < low || .5f > high) return null;
        if (hit != null && camera.WorldToViewportPoint(point).z < top.z - .45f) return null;
        return fpObjectAimed = current;
    }

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

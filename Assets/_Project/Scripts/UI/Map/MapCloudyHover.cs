// Pointer-only helper. Does not intercept drag/scroll from MapDragSurface.
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MapCloudyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [System.NonSerialized] public HudMinimap owner;
    [System.NonSerialized] public string title;
    [System.NonSerialized] public string state;

    public void OnPointerEnter(PointerEventData e)
    {
        if (owner != null) owner.ShowCloudyMapTooltip(title, state, e.position, e.enterEventCamera);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (owner != null) owner.HideCloudyMapTooltip();
    }

    void OnDisable()
    {
        if (owner != null) owner.HideCloudyMapTooltip();
    }
}

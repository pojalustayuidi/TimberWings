// Pointer-only helper. Does not intercept drag/scroll from MapDragSurface.
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MapCloudyHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.NonSerialized] public HudMinimap owner;
    [System.NonSerialized] public string title;
    [System.NonSerialized] public string state;
    bool touched;

    public void OnPointerEnter(PointerEventData e)
    {
        touched = false;
        if (owner != null && !Application.isMobilePlatform && Input.touchCount == 0)
            owner.ShowCloudyMapTooltip(title, state, e.position, e.enterEventCamera, this);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (owner != null && !touched) owner.HideCloudyMapTooltip(this);
    }

    public void OnPointerClick(PointerEventData e)
    {
        touched = Application.isMobilePlatform || Input.touchCount > 0;
        if (owner != null) owner.ShowCloudyMapTooltip(title, state, e.position, e.pressEventCamera, this);
    }

    void OnDisable()
    {
        if (owner != null) owner.HideCloudyMapTooltip(this);
    }
}

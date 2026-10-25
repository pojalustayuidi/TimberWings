using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Maps mouse-wheel vertical input to a horizontal ScrollRect, for worker reserve strips.</summary>
[DisallowMultipleComponent]
public sealed class WorkerReserveWheel : MonoBehaviour, IScrollHandler
{
    public RectTransform Target;
    private ScrollRect _scroll;

    private void Awake()
    {
        _scroll = GetComponent<ScrollRect>();
        if (_scroll == null) _scroll = GetComponentInParent<ScrollRect>();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (_scroll == null) _scroll = GetComponentInParent<ScrollRect>();
        if (_scroll == null || !_scroll.horizontal || Target == null) return;

        float contentWidth = Target.rect.width;
        float viewportWidth = _scroll.viewport != null ? _scroll.viewport.rect.width : 0f;
        if (contentWidth <= viewportWidth + 1f) return;

        float delta = eventData.scrollDelta.y;
        if (Mathf.Abs(delta) < 0.01f) delta = eventData.scrollDelta.x;
        float step = Mathf.Max(80f, viewportWidth * 0.22f);
        _scroll.horizontalNormalizedPosition = Mathf.Clamp01(
            _scroll.horizontalNormalizedPosition - delta * step / (contentWidth - viewportWidth));
        eventData.Use();
    }
}

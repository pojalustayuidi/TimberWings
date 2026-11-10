using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class HudPolishButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public string title, detail;
    public void OnPointerEnter(PointerEventData e) { Show(); }
    public void OnPointerExit(PointerEventData e) { Hide(); }
    public void OnSelect(BaseEventData e) { Show(); }
    public void OnDeselect(BaseEventData e) { Hide(); }
    void OnDisable() { Hide(); }
    void Show() { if (HudPolishRoot.Instance != null) HudPolishRoot.Instance.ShowTip(this, title, detail); }
    void Hide() { if (HudPolishRoot.Instance != null) HudPolishRoot.Instance.HideTip(this); }
}

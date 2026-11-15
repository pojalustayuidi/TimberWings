using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Focus / press feedback for mouse, keyboard and gamepad. Respects ReduceEffects and GameAudio.
// Visuals are additive to Button SpriteSwap. The button's own position and size belong to the
// LayoutGroup, so only localScale and child objects (halo, icon) are animated here.
public sealed class TWJournalButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Range(1f, 1.08f)] public float hoverScale = 1.032f;
    [Range(0.9f, 1f)] public float pressedScale = .968f;
    [Header("Spring (stiffness / damping)")]
    [Range(100f, 900f)] public float stiffness = 430f;
    [Range(8f, 60f)] public float damping = 27f;
    [Range(0f, 1.6f)] public float clickKick = .9f;
    [Header("Parts")]
    public Image hoverHalo;
    public RectTransform icon;
    [Range(0f, 12f)] public float iconNudgePixels = 5f;
    [Range(0f, .5f)] public float iconPopOnHover = .08f;

    static TWJournalButtonHover pointerOwner;
    bool hovered, selected, pointerDown, focusedLastFrame;
    float bornAt;
    float scaleOffset;      // spring state: current multiplier (1 = rest)
    float scaleVelocity;
    float iconAmount;       // 0..1
    Vector3 baseScale = Vector3.one;
    Vector2 iconBase;
    Vector3 iconBaseScale = Vector3.one;
    Button button;
    TWJournalMenuController menu;

    void Awake()
    {
        baseScale = transform.localScale;
        button = GetComponent<Button>();
        menu = GetComponentInParent<TWJournalMenuController>();
        bornAt = Time.unscaledTime;
        scaleOffset = 1f;
        if (icon == null)
        {
            var t = transform.Find("Icon");
            if (t != null) icon = t as RectTransform;
        }
        if (icon != null) { iconBase = icon.anchoredPosition; iconBaseScale = icon.localScale; }
        if (hoverHalo != null) SetHalo(0f);
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    void OnDisable()
    {
        hovered = selected = pointerDown = focusedLastFrame = false;
        if (pointerOwner == this) pointerOwner = null;
        scaleOffset = 1f; scaleVelocity = 0f; iconAmount = 0f;
        transform.localScale = baseScale;
        if (icon != null) { icon.anchoredPosition = iconBase; icon.localScale = iconBaseScale; }
        SetHalo(0f);
    }

    void OnDestroy()
    {
        if (pointerOwner == this) pointerOwner = null;
        if (button != null) button.onClick.RemoveListener(OnClicked);
    }

    public void OnPointerEnter(PointerEventData data) { hovered = true; pointerOwner = this; }
    public void OnPointerExit(PointerEventData data)
    {
        hovered = false; pointerDown = false;
        if (pointerOwner == this) pointerOwner = null;
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) pointerDown = true;
    }
    public void OnPointerUp(PointerEventData data) { pointerDown = false; }
    public void OnSelect(BaseEventData data) { selected = true; }
    public void OnDeselect(BaseEventData data) { selected = false; }

    void OnClicked()
    {
        // The spring gets a kick: a short "pop" right when the click is registered.
        if (!VibeSettings.ReduceEffects) scaleVelocity += clickKick * 6f;
        if (menu != null) menu.PlayClickSound();
    }

    void SetHalo(float a)
    {
        if (hoverHalo == null) return;
        Color c = hoverHalo.color; c.a = a; hoverHalo.color = c;
    }

    void Update()
    {
        if (button == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        bool animate = !VibeSettings.ReduceEffects;
        // One highlighted button while the mouse is over the menu; keyboard focus stays visible otherwise.
        bool focus = button.IsActive() && button.interactable &&
                     (hovered || (selected && pointerOwner == null));
        bool pressed = pointerDown && button.interactable;

        float target = !animate ? 1f : pressed ? pressedScale : focus ? hoverScale : 1f;
        if (animate)
        {
            scaleVelocity += ((target - scaleOffset) * stiffness - scaleVelocity * damping) * dt;
            scaleOffset += scaleVelocity * dt;
            scaleOffset = Mathf.Clamp(scaleOffset, .85f, 1.15f);
        }
        else { scaleOffset = 1f; scaleVelocity = 0f; }
        transform.localScale = baseScale * scaleOffset;

        float k = 1f - Mathf.Exp(-14f * dt);
        iconAmount = Mathf.Lerp(iconAmount, focus && animate ? 1f : 0f, k);
        if (icon != null)
        {
            icon.anchoredPosition = iconBase + Vector2.right * (iconNudgePixels * iconAmount);
            icon.localScale = iconBaseScale * (1f + iconPopOnHover * iconAmount);
        }
        if (hoverHalo != null)
        {
            Color c = hoverHalo.color;
            c.a = Mathf.Lerp(c.a, focus && animate ? .62f : 0f, k);
            hoverHalo.color = c;
        }
        if (focus && !focusedLastFrame && Time.unscaledTime - bornAt > .25f && menu != null)
            menu.PlayHoverSound();
        focusedLastFrame = focus;
    }
}

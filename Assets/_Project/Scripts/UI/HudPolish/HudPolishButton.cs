using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HudPolishButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public string title, detail;
    string englishTitle, englishDetail;
    Func<bool> locked;
    int slot;
    Canvas canvas;
    Button button;
    RectTransform frame, fallback;
    TMP_Text fallbackTitle, fallbackDetail;
    HudSafeAreaRegistry registry;
    HudPolishRoot activeTip;
    float enteredAt;
    bool hovering;

    internal void Bind(Button shortcut, int index, string ruTitle, string ruDetail,
        string enTitle, string enDetail, Func<bool> isLocked)
    {
        button = shortcut;
        slot = index;
        title = ruTitle;
        detail = ruDetail;
        englishTitle = enTitle;
        englishDetail = enDetail;
        locked = isLocked;
        canvas = GetComponentInParent<Canvas>();
        var rect = (RectTransform)transform;
        var dock = new GameObject(gameObject.name + " dock", typeof(RectTransform)) { layer = 5 };
        frame = (RectTransform)dock.transform;
        frame.SetParent(rect.parent, false);
        frame.SetSiblingIndex(rect.GetSiblingIndex());
        frame.sizeDelta = Vector2.one * HudGameplayLayout.ShortcutSize;
        rect.SetParent(frame, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * .5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (button.targetGraphic is Image image)
        {
            image.sprite = HudPolishRoot.Skin?.chip ?? CandyTag.Panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2.3f;
            button.transition = Selectable.Transition.ColorTint;
            var palette = button.colors;
            palette.normalColor = new Color32(255, 244, 225, 255);
            palette.highlightedColor = new Color32(255, 226, 168, 255);
            palette.selectedColor = palette.highlightedColor;
            palette.pressedColor = new Color32(218, 196, 242, 255);
            palette.fadeDuration = .08f;
            button.colors = palette;
            image.color = Color.white;
        }
        Reflow();
    }

    void Reflow()
    {
        float width = 2f * HudGameplayLayout.ShortcutPitch + HudGameplayLayout.ShortcutSize;
        float scale = HudGameplayLayout.Fit(canvas, width, .48f);
        // Keep addressable slots fixed even before the other windows unlock.
        HudGameplayLayout.Dock(frame, canvas, true, slot * HudGameplayLayout.ShortcutPitch * scale);
        frame.localScale = Vector3.one * scale;
    }

    void LateUpdate()
    {
        if (canvas == null) return;
        Reflow();
        var currentRegistry = HudSafeAreaRegistry.Instance;
        if (registry != currentRegistry)
        {
            if (registry != null) registry.Unregister((RectTransform)transform);
            registry = currentRegistry;
            if (registry != null) registry.Register((RectTransform)transform);
        }
        if (!hovering) return;
        if (HudGameplayLayout.Blocked || !button.IsInteractable() || Cursor.lockState == CursorLockMode.Locked)
        {
            Hide();
            return;
        }
        if (activeTip != null || Time.unscaledTime < enteredAt + .25f) return;
        if (fallback == null) BuildFallback();
        fallback.gameObject.SetActive(true);
        fallbackTitle.text = ReadTitle();
        fallbackDetail.text = ReadDetail();
        HudGameplayLayout.Dock(fallback, canvas, true);
        float scale = HudGameplayLayout.Fit(canvas, fallback.rect.width, .92f);
        fallback.localScale = Vector3.one * scale;
        fallback.anchoredPosition += Vector2.up *
            (HudGameplayLayout.ShortcutSize * HudGameplayLayout.Fit(canvas, 234f, .48f) + 11f);
    }

    string ReadTitle() => GameLanguage.English ? englishTitle ?? title : title;

    string ReadDetail() => locked != null && locked()
        ? ChopPrototypeLog.Text("Откроется по сюжету", "Unlocks through the story")
        : GameLanguage.English ? englishDetail ?? detail : detail;

    void BuildFallback()
    {
        var panel = UiKit.MakeImage(canvas.transform, "Shortcut tip", CandyTag.Panel, true, .55f);
        fallback = panel.rectTransform;
        fallback.sizeDelta = new Vector2(284f, 68f);
        fallbackTitle = CandyTag.Text(fallback, "Title", 21f, HudPolishRoot.Ink, TextAlignmentOptions.MidlineLeft, true);
        fallbackDetail = CandyTag.Text(fallback, "Description", 16f, CandyTag.Muted, TextAlignmentOptions.MidlineLeft, false);
        fallbackDetail.textWrappingMode = TextWrappingModes.Normal;
        PlaceLabel(fallbackTitle.rectTransform, 34f, 23f);
        PlaceLabel(fallbackDetail.rectTransform, 6f, 27f);
    }

    static void PlaceLabel(RectTransform label, float y, float height)
    {
        label.anchorMin = label.anchorMax = label.pivot = Vector2.zero;
        label.anchoredPosition = new Vector2(15f, y);
        label.sizeDelta = new Vector2(252f, height);
    }

    void Show()
    {
        if (button == null || HudGameplayLayout.Blocked) return;
        hovering = true;
        enteredAt = Time.unscaledTime;
        var owner = HudPolishRoot.Instance;
        if (owner == null || owner.tooltip == null) return;
        activeTip = owner;
        owner.ShowTip(this, ReadTitle(), ReadDetail());
    }

    void Hide()
    {
        hovering = false;
        if (activeTip != null) activeTip.HideTip(this);
        activeTip = null;
        if (fallback != null) fallback.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData e) => Show();
    public void OnPointerExit(PointerEventData e) => Hide();
    public void OnSelect(BaseEventData e) => Show();
    public void OnDeselect(BaseEventData e) => Hide();
    void OnDisable() => Hide();

    void OnDestroy()
    {
        if (registry != null) registry.Unregister((RectTransform)transform);
        if (fallback != null) Destroy(fallback.gameObject);
    }
}

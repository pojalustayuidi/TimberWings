// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.HudHelpers.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): кнопка коллекции в HUD и мелкие помощники.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.Fill.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.Interact.cs.
// Методы этой части: BuildHud, UpdateHud, BtnIco, Ico, Tip, MakeBtn, AddBtn, Anch.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    // ============================================================ кнопка в HUD
    void BuildHud()
    {
        var cgo = new GameObject("PetHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.HudOverlay;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        hudGroup = cgo.GetComponent<CanvasGroup>();
        hudGroup.alpha = 0f;
        hudGroup.blocksRaycasts = false;

        var btn = UiKit.MakeButton(cgo.transform, "", UiBtn.Amber, new Vector2(HudBtn, HudBtn), 20f, 1f);
        btn.name = "PetsButton";
        hudRt = btn.GetComponent<RectTransform>();
        // раньше обе кнопки стояли в одной точке справа внизу, и «Прокачка» закрывала лапку: теперь питомцы левее неё через зазор
        Anch(hudRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-(UpgradeWindow.HudRight + UpgradeWindow.HudBtnW + UpgradeWindow.HudGap), UpgradeWindow.HudBottom), new Vector2(HudBtn, HudBtn));
        btn.onClick.AddListener(Toggle);

        var paw = UiKit.MakeIcon(btn.transform, UiIcon.Paw, 54f, "Paw");
        paw.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        // подсказка клавиши (на телефоне клавиатуры нет)
        if (!Application.isMobilePlatform)
        {
            var key = UiKit.MakeKeycap(btn.transform, "P", 38f);
            Anch(key.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(8f, -8f), new Vector2(38f, 38f));
        }

        // красная «!»: есть питомец вне слота и есть свободный слот
        var dot = UiKit.MakeImage(btn.transform, "Dot", UiKit.Medallion, false);
        dot.color = DotColor;
        hudDot = dot.rectTransform;
        Anch(hudDot, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(34f, 34f));
        var bang = UiKit.MakeLabel(dot.transform, "!", 24f, Color.white, TextAlignmentOptions.Center, true);
        Stretch(bang.rectTransform);
        hudBang = bang;
        hudDot.gameObject.SetActive(false);
    }

    void UpdateHud(float dt)
    {
        if (hudGroup == null) return;

        // пока не зарегистрировали в реестре HUD, метки над миром могут оказаться под кнопкой
        if (!hudRegistered && HudSafeAreaRegistry.Instance != null)
        {
            HudSafeAreaRegistry.Instance.Register(hudRt);
            hudRegistered = true;
        }

        bool visible = Available && !MobileInput.ModalOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying;
        hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, dt / 0.2f);
        bool on = visible && hudGroup.alpha > 0.5f;
        hudGroup.blocksRaycasts = on;
        hudGroup.interactable = on;

        var pm = PetManager.Instance;
        bool hint = visible && pm != null && pm.Owned.Count > pm.EquippedCount && pm.EquippedCount < pm.SlotCount;
        int fresh = visible && pm != null ? pm.NewCount : 0;          // новые питомцы, которых ещё не смотрели: число на значке
        bool showDot = hint || fresh > 0;
        if (hudDot.gameObject.activeSelf != showDot) hudDot.gameObject.SetActive(showDot);
        if (showDot)
        {
            Set(hudBang, fresh > 0 ? (fresh > 9 ? "9+" : fresh.ToString()) : "!");
            dotT += dt;
            hudDot.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(dotT * 5f));
        }
    }

    // ============================================================ мелкие помощники
    // иконка поверх кнопки слева, подпись сдвигается правее
    static Image BtnIco(Button b, Sprite sp, float size, float padLeft = 14f)
    {
        var img = UiKit.MakeImage(b.transform, "Ico", sp, false);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Anch(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(padLeft + size * 0.5f, 1f), new Vector2(size, size));
        var lbl = b.GetComponentInChildren<TMP_Text>();
        if (lbl != null)
        {
            lbl.rectTransform.offsetMin = new Vector2(padLeft + size + 4f, lbl.rectTransform.offsetMin.y);
            lbl.rectTransform.offsetMax = new Vector2(-8f, lbl.rectTransform.offsetMax.y);
        }
        return img;
    }

    static Image Ico(Transform parent, PetIco ico, float size)
    {
        var img = UiKit.MakeImage(parent, "Ico", PetIcons.Get(ico), false);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Anch(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        return img;
    }

    // подсказка в строке под окном при наведении на кнопку
    static void Tip(Button b, string text)
    {
        if (b == null) return;
        var h = b.gameObject.GetComponent<PetHover>();
        if (h == null) h = b.gameObject.AddComponent<PetHover>();
        h.tip = text;
    }

    static Button MakeBtn(Transform parent, string label, UiBtn style, float x, float y, float w, float h, float font,
                          UnityAction onClick, out TMP_Text labelText)
    {
        var b = UiKit.MakeButton(parent, label, style, new Vector2(w, h), font, 1f);
        TL(b.GetComponent<RectTransform>(), x, y, w, h);
        b.onClick.AddListener(onClick);
        labelText = b.GetComponentInChildren<TMP_Text>();
        if (labelText != null) PetUi.AutoSize(labelText, 14f, font);
        return b;
    }

    static void AddBtn(Image img, UnityAction a)
    {
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(a);
    }

    static void Anch(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        PetUi.Anchor(r, anchor, pivot, pos, size);
    }
}

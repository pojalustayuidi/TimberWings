// Assets/_Project/Scripts/UI/Kit/UiCandy.Controls.cs   (НЕ в Editor)
// Вторая половина UiCandy: вкладки-пилюли, счётчик «− число +», переключатель, ползунок, область прокрутки.
// Каждый сборщик сам включает скин Candy (см. шапку UiCandy.cs).
// Примечание: методы называются Toggle и Slider, поэтому типы Unity внутри написаны полностью (UnityEngine.UI.Toggle).
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class UiCandy
{
    // ======================================================================================
    //  ВКЛАДКИ-ПИЛЮЛИ
    // ======================================================================================
    public sealed class TabsRefs
    {
        public RectTransform root;
        public Button[] buttons;
        public int Selected { get; private set; }
        Action<int> onSelect;

        internal TabsRefs(Action<int> cb, int selected) { onSelect = cb; Selected = selected; }

        // выбрать вкладку: выбранная персиковая, остальные облачные. notify = вызвать onSelect
        public void Select(int i, bool notify = true)
        {
            if (i < 0 || i >= buttons.Length) return;
            Selected = i;
            for (int k = 0; k < buttons.Length; k++) SetStyle(buttons[k], k == i ? UiBtn.Amber : UiBtn.Gray);
            if (notify && onSelect != null) onSelect(i);
        }
    }

    // ряд вкладок. Ширина каждой считается по длине текста, tabWidth > 0 задаёт одну ширину для всех.
    // Корень получает размер (сумма ширин, высота). Положение задайте сами (якоря и anchoredPosition)
    public static TabsRefs Tabs(Transform parent, string[] labels, int selected, Action<int> onSelect,
                                float height = 52f, float fontSize = 26f, float tabWidth = 0f)
    {
        using (Scope())
        {
            var t = new TabsRefs(onSelect, selected);
            var root = NewRect(parent, "Tabs");
            var hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = false;   // размеры кнопок задаём сами
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            t.buttons = new Button[labels.Length];
            float total = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                float w = tabWidth > 0f ? tabWidth : Mathf.Max(96f, labels[i].Length * fontSize * 0.62f + 44f);
                var b = UiKit.MakeButton(root, labels[i], UiBtn.Gray, new Vector2(w, height), fontSize, height / 48f);
                int idx = i;
                b.onClick.AddListener(() => t.Select(idx, true));
                t.buttons[i] = b;
                total += w + (i > 0 ? hlg.spacing : 0f);
            }
            root.sizeDelta = new Vector2(total, height);
            t.root = root;
            t.Select(selected, false);
            return t;
        }
    }

    // ======================================================================================
    //  СЧЁТЧИК «− число +»
    // ======================================================================================
    public sealed class StepperRefs
    {
        public RectTransform root;
        public TMP_Text label;
        public Button minus, plus;
        public int Value { get; private set; }
        public int Min, Max;
        public Action<int> OnChange;
        public Func<int, string> Format;   // как показывать число (по умолчанию просто число)

        // notify = вызвать OnChange. Значение зажимается в [Min, Max], кнопки гаснут на краях
        public void Set(int v, bool notify = false)
        {
            v = Mathf.Clamp(v, Min, Max);
            bool changed = v != Value;
            Value = v;
            label.text = Format != null ? Format(v) : v.ToString();
            minus.interactable = v > Min;
            plus.interactable = v < Max;
            if (notify && changed && OnChange != null) OnChange(v);
        }
    }

    // wireClicks = true: «−» и «+» сами меняют число на 1. Окну с удержанием «+» передайте false и повесьте свой код на minus.onClick / plus,
    // а число меняйте через Set
    public static StepperRefs Stepper(Transform parent, int value, int min, int max, Action<int> onChange,
                                      float height = 56f, float width = 0f, bool wireClicks = true)
    {
        using (Scope())
        {
            var s = new StepperRefs { Min = min, Max = max, OnChange = onChange };
            float w = width > 0f ? width : height * 2f + 120f;
            var root = NewRect(parent, "Stepper");
            root.sizeDelta = new Vector2(w, height);
            s.root = root;

            float bs = height / 48f;
            s.minus = UiKit.MakeButton(root, "−", UiBtn.Gray, new Vector2(height, height), height * 0.62f, bs);
            var mr = (RectTransform)s.minus.transform;
            mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f);
            mr.pivot = new Vector2(0f, 0.5f);
            mr.anchoredPosition = Vector2.zero;

            s.plus = UiKit.MakeButton(root, "+", UiBtn.Green, new Vector2(height, height), height * 0.62f, bs);
            var pr = (RectTransform)s.plus.transform;
            pr.anchorMin = pr.anchorMax = new Vector2(1f, 0.5f);
            pr.pivot = new Vector2(1f, 0.5f);
            pr.anchoredPosition = Vector2.zero;

            var pit = UiKit.MakeImage(root, "Value", UiKit.Slot, true, height / 56f * 0.9f);
            Stretch(pit.rectTransform, height + 8f, 2f, height + 8f, 2f);
            s.label = UiKit.MakeLabel(pit.transform, "", height * 0.52f, Ink, TextAlignmentOptions.Center, true);

            s.Set(value, false);
            if (wireClicks)
            {
                s.minus.onClick.AddListener(() => s.Set(s.Value - 1, true));
                s.plus.onClick.AddListener(() => s.Set(s.Value + 1, true));
            }
            return s;
        }
    }

    // ======================================================================================
    //  ПЕРЕКЛЮЧАТЕЛЬ
    // ======================================================================================
    public sealed class ToggleRefs
    {
        public RectTransform root;
        public UnityEngine.UI.Toggle toggle;
        public Image track, knob;
        public bool Value { get { return toggle.isOn; } }
        public void Set(bool v, bool notify = false)
        {
            if (notify) toggle.isOn = v; else toggle.SetIsOnWithoutNotify(v);
            if (!notify) refresh(v, false);
        }
        internal Action<bool, bool> refresh;
    }

    // пилюля 84 x 44: включено мятное, выключено облачно-серое. Ручка пружинит. Размер меняется через height (ширина = 1.9 * height)
    public static ToggleRefs Toggle(Transform parent, bool value, Action<bool> onChange, float height = 44f)
    {
        using (Scope())
        {
            var r = new ToggleRefs();
            float w = height * 1.9f;
            var track = UiKit.MakeImage(parent, "Toggle", UiKit.BarFrame, true, height / 28f);
            track.raycastTarget = true;
            track.rectTransform.sizeDelta = new Vector2(w, height);
            r.track = track;
            r.root = track.rectTransform;

            float ksz = height - 8f;
            var knob = UiKit.MakeImage(track.transform, "Knob", UiKit.Medallion, false);
            var kr = knob.rectTransform;
            kr.anchorMin = kr.anchorMax = new Vector2(0f, 0.5f);
            kr.pivot = new Vector2(0.5f, 0.5f);
            kr.sizeDelta = new Vector2(ksz, ksz);
            kr.anchoredPosition = new Vector2(height * 0.5f, 0f);
            var mover = knob.gameObject.AddComponent<UiCandyKnob>();
            r.knob = knob;

            var tg = track.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            tg.targetGraphic = track;
            tg.transition = Selectable.Transition.None;
            tg.graphic = null;
            r.toggle = tg;

            Color on = Mint, off = C(0xC9, 0xCF, 0xEA);
            float xOff = height * 0.5f, xOn = w - height * 0.5f;
            r.refresh = (v, animate) =>
            {
                track.color = v ? on : off;
                mover.SetTarget(v ? xOn : xOff, !animate);
            };
            tg.SetIsOnWithoutNotify(value);
            r.refresh(value, false);
            tg.onValueChanged.AddListener(v =>
            {
                r.refresh(v, true);
                if (onChange != null) onChange(v);
            });
            Juice(track.gameObject, UiBtn.Green);   // лёгкое «желе» и искры при щелчке
            return r;
        }
    }

    // ======================================================================================
    //  ПОЛЗУНОК
    // ======================================================================================
    // дорожка с мягкой заливкой и круглой ручкой. size.y лучше 28..36 (заливка рассчитана на тонкую полосу)
    public static UnityEngine.UI.Slider Slider(Transform parent, Vector2 size, float min, float max, float value,
                                               Action<float> onChange, bool whole = false, UiBar fill = UiBar.Blue)
    {
        using (Scope())
        {
            float bs = size.y / 28f;
            var bg = UiKit.MakeImage(parent, "Slider", UiKit.BarFrame, true, bs);
            bg.raycastTarget = true;
            bg.rectTransform.sizeDelta = size;

            float inset = 5f * bs;
            var fillArea = NewRect(bg.transform, "Fill Area");
            Stretch(fillArea, inset, inset, inset, inset);
            var fillImg = UiKit.MakeImage(fillArea, "Fill", UiKit.BarFill(fill), false);
            fillImg.type = Image.Type.Simple;
            var fr = fillImg.rectTransform;   // якоря заливки ведёт сам Slider
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = fr.offsetMax = Vector2.zero;

            float hw = size.y * 1.45f;   // ручка крупнее дорожки
            var slide = NewRect(bg.transform, "Handle Slide Area");
            Stretch(slide, hw * 0.5f, 0f, hw * 0.5f, 0f);
            var handle = UiKit.MakeImage(slide, "Handle", UiKit.Medallion, false);
            handle.raycastTarget = true;
            var hr = handle.rectTransform;
            hr.sizeDelta = new Vector2(hw, hw - size.y);   // якоря по высоте растянутые, итоговая высота = hw

            var sl = bg.gameObject.AddComponent<UnityEngine.UI.Slider>();
            sl.fillRect = fr;
            sl.handleRect = hr;
            sl.targetGraphic = handle;
            sl.transition = Selectable.Transition.None;
            sl.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            sl.minValue = min;
            sl.maxValue = max;
            sl.wholeNumbers = whole;
            sl.SetValueWithoutNotify(value);
            if (onChange != null) sl.onValueChanged.AddListener(v => onChange(v));
            return sl;
        }
    }

    // ======================================================================================
    //  ОБЛАСТЬ ПРОКРУТКИ С КОНФЕТНОЙ ПОЛОСОЙ
    // ======================================================================================
    public sealed class ScrollRefs
    {
        public RectTransform root;
        public ScrollRect scroll;
        public RectTransform viewport, content;
        public Scrollbar bar;
        public void ToTop() { scroll.verticalNormalizedPosition = 1f; }
    }

    // вертикальная прокрутка. autoLayout = true: в content уже есть VerticalLayoutGroup (ширина детей растягивается,
    // высота берётся из sizeDelta.y) и ContentSizeFitter, то есть достаточно просто добавлять строки (Card и так далее)
    public static ScrollRefs ScrollArea(Transform parent, Vector2 size, bool autoLayout = true, float spacing = 10f)
    {
        using (Scope())
        {
            var s = new ScrollRefs();
            var root = NewRect(parent, "ScrollArea");
            root.sizeDelta = size;
            s.root = root;
            var sr = root.gameObject.AddComponent<ScrollRect>();
            s.scroll = sr;

            var vp = NewRect(root, "Viewport");
            Stretch(vp, 0f, 0f, 0f, 0f);
            vp.gameObject.AddComponent<RectMask2D>();
            var vpHit = vp.gameObject.AddComponent<Image>();   // невидимая подложка: за неё можно тянуть список даже там, где нет карточек
            vpHit.color = new Color(0f, 0f, 0f, 0f);
            s.viewport = vp;

            var content = NewRect(vp, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            s.content = content;
            if (autoLayout)
            {
                var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = spacing;
                vlg.padding = new RectOffset(2, 2, 2, 6);   // запас под тень и «юбку» карточек
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                var fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // полоса: дорожка-пилюля и желейная ручка
            const float barW = 18f;
            var track = UiKit.MakeImage(root, "Scrollbar", UiKit.BarFrame, true, 0.55f);
            track.raycastTarget = true;
            var tr = track.rectTransform;
            tr.anchorMin = new Vector2(1f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(1f, 0.5f);
            tr.sizeDelta = new Vector2(barW, 0f);
            tr.anchoredPosition = Vector2.zero;

            var area = NewRect(track.transform, "Sliding Area");
            Stretch(area, 2f, 2f, 2f, 2f);
            var handle = UiKit.MakeImage(area, "Handle", UiKit.BtnSprite(UiBtn.Amber, UiBtnState.Normal), true, 0.45f);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = Vector2.zero;

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.transition = Selectable.Transition.None;
            bar.direction = Scrollbar.Direction.BottomToTop;
            s.bar = bar;

            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 32f;
            sr.verticalScrollbar = bar;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            sr.verticalScrollbarSpacing = 6f;
            return s;
        }
    }
}

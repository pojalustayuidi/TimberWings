// Assets/_Project/Scripts/UI/Kit/UiKitShowcase.cs   (НЕ в Editor)
// Образец всего кита на одном экране. Повесь на пустой объект (или Tools > BuilderSim > UI Kit > Образец в сцене) и нажми Play.
// Клавиша T переключает скин: Candy (по умолчанию) и Brass (старый латунный). Esc или кнопка «Закрыть» убирает образец. Из этого скрипта удобно копировать, как собирать окна.
// Справа внизу образец окна Candy: собран только из помощников UiCandy (Window, Tabs, Card, Chip, Stepper, Toggle, Slider, ScrollArea).
// Он выглядит одинаково в обоих скинах: UiCandy сам включает Candy. Действия в образце пишутся в заголовок окна.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiKitShowcase : MonoBehaviour
{
    GameObject root;
    UiSkin skin = UiSkin.Candy;

    void Start() { Rebuild(); }

    // пересобирает весь образец под выбранным скином (спрайты берутся при сборке, поэтому нужен using)
    void Rebuild()
    {
        if (root != null) Destroy(root);
        using (UiKit.UseSkin(skin)) Build();
    }

    void Build()
    {
        bool candy = skin == UiSkin.Candy;
        Color txt = candy ? UiKit.CandyInk : Color.white;
        Color soft = candy ? UiKit.CandyMuted : new Color(0.82f, 0.87f, 0.93f);
        var cgo = new GameObject("UiKitShowcaseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        root = cgo;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Showcase;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        var bg = UiKit.MakeImage(cgo.transform, "Bg", null, false);
        bg.color = candy ? new Color32(0xCF, 0xE9, 0xFF, 255) : new Color32(0x7A, 0x7C, 0x80, 255);
        Stretch(bg.rectTransform);

        // ---- подпись скина
        var cap = UiKit.MakeLabel(cgo.transform, "Скин: " + skin + "   (T: сменить, Esc: закрыть)", 24f, candy ? UiKit.CandyMuted : Color.white, TextAlignmentOptions.Left, false);
        cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(0, 1);
        cap.rectTransform.pivot = new Vector2(0, 1);
        cap.rectTransform.sizeDelta = new Vector2(760, 36);
        cap.rectTransform.anchoredPosition = new Vector2(1060, -6);

        // ---- окно с деревянным заголовком
        var win = UiKit.MakePanel(cgo.transform, new Vector2(560, 340), true, 1.5f, "WindowWood");
        Place(win.rectTransform, 60, 50);
        var title = UiKit.MakeLabel(win.transform, "Прокачка", 34f, Color.white, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0, 1);
        title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.offsetMin = new Vector2(40, -54);
        title.rectTransform.offsetMax = new Vector2(-40, -4);
        var body = UiKit.MakeLabel(win.transform, candy ? "Окно: облачное поле, голубая юбка, мягкий индиго-контур." : "Окно: тёмно-синее поле, латунный ободок, заклёпки.", 26f, soft, TextAlignmentOptions.TopLeft, false);
        body.rectTransform.offsetMin = new Vector2(40, 40);
        body.rectTransform.offsetMax = new Vector2(-40, -80);

        var panel = UiKit.MakePanel(cgo.transform, new Vector2(300, 340), false, 1.5f);
        Place(panel.rectTransform, 650, 50);
        var pl = UiKit.MakeLabel(panel.transform, "Обычная панель", 26f, txt);

        // ---- полосы с медальонами
        UiBar[] bars = { UiBar.Amber, UiBar.Green, UiBar.Blue };
        UiIcon[] barIcons = { UiIcon.Backpack, UiIcon.Axe, UiIcon.Flame };
        float[] amounts = { 0.55f, 0.8f, 0.35f };
        for (int i = 0; i < 3; i++)
        {
            float y = 60 + i * 76;
            var med = UiKit.MakeImage(cgo.transform, "Medallion", UiKit.Medallion, false);
            med.rectTransform.sizeDelta = new Vector2(72, 72);
            Place(med.rectTransform, 1010, y - 6);
            var ic = UiKit.MakeIcon(med.transform, barIcons[i], 38f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
            Image fill;
            var bar = UiKit.MakeBar(cgo.transform, bars[i], new Vector2(560, 42), out fill, 1.5f);
            Place(bar.rectTransform, 1090, y + 3);
            fill.fillAmount = amounts[i];
        }

        // ---- табличка «готово»
        var plaque = UiKit.MakePlaque(cgo.transform, new Vector2(360, 90), 1.5f);
        Place(plaque.rectTransform, 1010, 300);
        var chk = UiKit.MakeIcon(plaque.transform, UiIcon.Check, 52f);
        chk.rectTransform.anchorMin = chk.rectTransform.anchorMax = new Vector2(0, 0.5f);
        chk.rectTransform.anchoredPosition = new Vector2(62, 0);
        var pt = UiKit.MakeLabel(plaque.transform, "Хижина построена", 26f, new Color(0.19f, 0.1f, 0.03f), TextAlignmentOptions.Left);
        pt.rectTransform.offsetMin = new Vector2(100, 0);
        pt.rectTransform.offsetMax = new Vector2(-20, 0);

        // ---- кнопки: 4 цвета x 4 состояния
        UiBtn[] styles = { UiBtn.Amber, UiBtn.Yellow, UiBtn.Green, UiBtn.Gray };
        UiBtnState[] states = { UiBtnState.Normal, UiBtnState.Hover, UiBtnState.Pressed, UiBtnState.Disabled };
        string[] names = { "обычная", "наведение", "нажата", "выкл." };
        for (int r = 0; r < styles.Length; r++)
        {
            for (int c = 0; c < states.Length; c++)
            {
                var im = UiKit.MakeImage(cgo.transform, "Btn", UiKit.BtnSprite(styles[r], states[c]), true, 1.3f);
                im.rectTransform.sizeDelta = new Vector2(190, 64);
                Place(im.rectTransform, 60 + c * 210, 440 + r * 84);
                UiKit.AddButtonLabel(im.transform, names[c], states[c] == UiBtnState.Disabled ? UiBtn.Gray : styles[r], 26f, 1.3f);
            }
        }

        // живая кнопка (с наведением и нажатием)
        var live = UiKit.MakeButton(cgo.transform, "Закрыть", UiBtn.Amber, new Vector2(240, 72), 32f, 1.4f);
        Place(live.GetComponent<RectTransform>(), 60, 800);
        live.onClick.AddListener(() => Destroy(gameObject));
        var buy = UiKit.MakeButton(cgo.transform, "Купить · 30", UiBtn.Green, new Vector2(280, 72), 32f, 1.4f);
        Place(buy.GetComponent<RectTransform>(), 330, 800);

        // ---- клавиша и слоты
        var key = UiKit.MakeKeycap(cgo.transform, "E", 88f);
        Place(key.rectTransform, 700, 790);
        for (int i = 0; i < 4; i++)
        {
            var slot = UiKit.MakeSlot(cgo.transform, 92f, 1.4f);
            Place(slot.rectTransform, 820 + i * 104, 790);
            if (i < 3)
            {
                UiIcon[] fill3 = { UiIcon.Log, UiIcon.Plank, UiIcon.Fish };
                var ic = UiKit.MakeIcon(slot.transform, fill3[i], 64f);
                ic.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        // ---- подсказка с хвостиком
        var tag = UiKit.MakePanel(cgo.transform, new Vector2(420, 96), false, 1.1f, "Tag");
        Place(tag.rectTransform, 1300, 780);
        UiKit.MakeLabel(tag.transform, "Нажми E, чтобы поговорить", 26f, txt);
        var tail = UiKit.MakeImage(tag.transform, "Tail", UiKit.Tail, false);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0);
        tail.rectTransform.pivot = new Vector2(0.5f, 1);
        tail.rectTransform.sizeDelta = new Vector2(54, 33);
        tail.rectTransform.anchoredPosition = new Vector2(0, 8);

        // ---- все иконки
        var icons = (UiIcon[])System.Enum.GetValues(typeof(UiIcon));
        for (int i = 0; i < icons.Length; i++)
        {
            var slot = UiKit.MakeSlot(cgo.transform, 62f, 1f);
            Place(slot.rectTransform, 40 + (i % 27) * 68, 925 + (i / 27) * 70);   // два ряда: все 34 иконки на экране
            var ic = UiKit.MakeIcon(slot.transform, icons[i], 46f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
        }

        BuildCandySample(cgo.transform);
    }

    // ======================================================================================
    //  ОБРАЗЕЦ ОКНА CANDY: только UiCandy.*. Копируйте отсюда при переносе окон
    // ======================================================================================
    void BuildCandySample(Transform parent)
    {
        UiCandy.WindowRefs w = null;
        w = UiCandy.Window(parent, new Vector2(940f, 360f), "Лавка лесоруба", () => { if (w != null) Destroy(w.root.gameObject); });
        Place(w.root, 920f, 410f);
        var c = w.content;   // 892 x 270
        System.Action<string> say = s => w.title.text = s;

        // верхний ряд: вкладки слева, чипы с запасами справа
        var tabs = UiCandy.Tabs(c, new[] { "Топоры", "Рюкзаки", "Обувь" }, 0, i => say("Вкладка " + (i + 1)));
        Place(tabs.root, 0f, 0f);
        var gold = UiCandy.Chip(c, UiIcon.Coin, "1 250", 44f, UiTone.Peach);
        PlaceTR(gold.rt, 0f, 4f);
        var logs = UiCandy.Chip(c, UiIcon.Log, "12 / 20", 44f);
        PlaceTR(logs.rt, 170f, 4f);

        // левая колонка: прокручиваемый список карточек (пятая закрыта)
        var list = UiCandy.ScrollArea(c, new Vector2(520f, 196f));
        Place(list.root, 0f, 70f);
        string[] names = { "Лёгкий топор", "Крепкий топор", "Походный рюкзак", "Охотничьи ботинки", "Пила" };
        string[] subs = { "Рубит быстрее", "Меньше усталости", "+10 мест", "Быстрее бег", "Пока закрыто" };
        UiIcon[] icons = { UiIcon.Axe, UiIcon.Axe, UiIcon.Backpack, UiIcon.Boot, UiIcon.Saw };
        for (int i = 0; i < names.Length; i++)
        {
            bool locked = i == names.Length - 1;
            var card = UiCandy.Card(list.content, 84f, names[i], subs[i], icons[i],
                                    locked ? UiTone.Locked : (i == 1 ? UiTone.Mint : UiTone.Neutral), 130f);
            if (locked)
            {
                var lk = UiCandy.Icon(card.right, UiIcon.Lock, 40f);
                lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                lk.rectTransform.pivot = new Vector2(1f, 0.5f);
                lk.rectTransform.anchoredPosition = Vector2.zero;
                continue;
            }
            var price = UiCandy.Chip(card.right, UiIcon.Coin, (80 + i * 60).ToString(), 40f, UiTone.Peach);
            price.rt.anchorMin = price.rt.anchorMax = new Vector2(1f, 0.5f);
            price.rt.pivot = new Vector2(1f, 0.5f);
            price.rt.anchoredPosition = Vector2.zero;
        }

        // правая колонка (x 550..892): счётчик, переключатель, ползунок
        var l1 = UiCandy.Label(c, "Количество", 24f, UiCandy.Muted, TextAlignmentOptions.Left);
        PlaceText(l1, 550f, 70f, 342f, 28f);
        var step = UiCandy.Stepper(c, 3, 1, 9, v => say("Количество: " + v), 48f, 220f);
        Place(step.root, 550f, 98f);

        var l2 = UiCandy.Label(c, "Подсказки", 24f, UiCandy.Muted, TextAlignmentOptions.Left);
        PlaceText(l2, 550f, 158f, 200f, 40f);
        var tg = UiCandy.Toggle(c, true, v => say("Подсказки: " + (v ? "вкл" : "выкл")), 40f);
        PlaceTR(tg.root, 0f, 158f);

        var l3 = UiCandy.Label(c, "Громкость", 24f, UiCandy.Muted, TextAlignmentOptions.Left);
        PlaceText(l3, 550f, 206f, 342f, 26f);
        var sl = UiCandy.Slider(c, new Vector2(330f, 32f), 0f, 100f, 60f, v => say("Громкость: " + Mathf.RoundToInt(v)), true);
        Place((RectTransform)sl.transform, 556f, 234f);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Destroy(gameObject);
        if (Input.GetKeyDown(KeyCode.T))
        {
            skin = skin == UiSkin.Candy ? UiSkin.Brass : UiSkin.Candy;
            Rebuild();
        }
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // позиция от правого верхнего угла родителя (x и y отступы внутрь)
    static void PlaceTR(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-x, -y);
    }

    // подпись с фиксированным прямоугольником от левого верхнего угла родителя
    static void PlaceText(TMP_Text t, float x, float y, float w, float h)
    {
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    // позиция от левого верхнего угла экрана
    static void Place(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
    }
}

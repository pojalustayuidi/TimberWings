// Assets/_Project/Scripts/UI/UiKitShowcase.cs   (НЕ в Editor)
// Образец всего кита на одном экране. Повесь на пустой объект (или Tools > BuilderSim > UI Kit > Образец в сцене) и нажми Play.
// Esc или кнопка «Закрыть» убирает образец. Из этого скрипта удобно копировать, как собирать окна.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiKitShowcase : MonoBehaviour
{
    GameObject root;

    void Start()
    {
        var cgo = new GameObject("UiKitShowcaseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        root = cgo;
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;

        var bg = UiKit.MakeImage(cgo.transform, "Bg", null, false);
        bg.color = new Color32(0x7A, 0x7C, 0x80, 255);
        Stretch(bg.rectTransform);

        // ---- окно с деревянным заголовком
        var win = UiKit.MakePanel(cgo.transform, new Vector2(560, 340), true, 1.5f, "WindowWood");
        Place(win.rectTransform, 60, 50);
        var title = UiKit.MakeLabel(win.transform, "Прокачка", 34f, Color.white, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0, 1);
        title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.pivot = new Vector2(0.5f, 1);
        title.rectTransform.offsetMin = new Vector2(40, -54);
        title.rectTransform.offsetMax = new Vector2(-40, -4);
        var body = UiKit.MakeLabel(win.transform, "Окно: тёмно-синее поле, латунный ободок, заклёпки.", 26f, new Color(0.82f, 0.87f, 0.93f), TextAlignmentOptions.TopLeft, false);
        body.rectTransform.offsetMin = new Vector2(40, 40);
        body.rectTransform.offsetMax = new Vector2(-40, -80);

        var panel = UiKit.MakePanel(cgo.transform, new Vector2(300, 340), false, 1.5f);
        Place(panel.rectTransform, 650, 50);
        var pl = UiKit.MakeLabel(panel.transform, "Обычная панель", 26f, Color.white);

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
        UiKit.MakeLabel(tag.transform, "Нажми E, чтобы поговорить", 26f, Color.white);
        var tail = UiKit.MakeImage(tag.transform, "Tail", UiKit.Tail, false);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0);
        tail.rectTransform.pivot = new Vector2(0.5f, 1);
        tail.rectTransform.sizeDelta = new Vector2(54, 33);
        tail.rectTransform.anchoredPosition = new Vector2(0, 8);

        // ---- все иконки
        var icons = (UiIcon[])System.Enum.GetValues(typeof(UiIcon));
        for (int i = 0; i < icons.Length; i++)
        {
            var slot = UiKit.MakeSlot(cgo.transform, 100f, 1.4f);
            Place(slot.rectTransform, 60 + i * 114, 940);
            var ic = UiKit.MakeIcon(slot.transform, icons[i], 70f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Destroy(gameObject);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // позиция от левого верхнего угла экрана
    static void Place(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
    }
}

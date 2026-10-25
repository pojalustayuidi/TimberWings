// Assets/_Project/Scripts/UI/Kit/UiCandy.cs   (НЕ в Editor)
// Помощники Candy для окон: палитра, шрифт, сборщики готовых кусков интерфейса. Вторая половина (вкладки, счётчик, переключатель,
// ползунок, прокрутка) лежит в UiCandy.Controls.cs.
//
// ГЛАВНОЕ ПРАВИЛО: каждый сборщик UiCandy сам включает скин Candy на время работы (внутри using UiKit.UseSkin), поэтому им можно
// пользоваться из любого окна, даже если окно ещё собирается латунным скином. Окну не нужно оборачивать эти вызовы в using.
// Спрайты, взятые позже (смена стиля вкладки, состояния переключателя), тоже берутся в Candy: см. UiCandy.SetStyle.
//
// Сборщики (все возвращают маленький объект с ссылками на нужные части):
//     UiCandy.Window(parent, size, title, onClose)    окно: панель с лавандовой лентой-заголовком, «×», CandyPopIn, область Content
//     UiCandy.Tabs(parent, labels, selected, onSelect) вкладки-пилюли
//     UiCandy.Card(parent, height, title, sub, icon)   строка-карточка: медальон с иконкой, два текста, правый слот под чип/кнопку
//     UiCandy.Chip(parent, icon, text)                 пилюля «иконка + число»
//     UiCandy.Stepper(parent, value, min, max, onChange) «− число +»
//     UiCandy.Toggle(parent, value, onChange)          переключатель-пилюля
//     UiCandy.Slider(parent, size, min, max, value, onChange) ползунок
//     UiCandy.ScrollArea(parent, size)                 область прокрутки с конфетной полосой
//     UiCandy.Label(parent, text, size)                текст индиго круглым шрифтом
//     UiCandy.Icon(parent, icon, size)                 иконка Candy
//     UiCandy.SetStyle(button, UiBtn.Green)            сменить цвет готовой кнопки (для вкладок и переключателей состояния)
//     UiCandy.Juice(gameObject, style)                 повесить «желе» на любой объект (UiKit.MakeButton делает это сам в Candy)
//
// ШРИФТ: положи в Assets/_Project/Resources/Fonts/ один из двух файлов (кириллица обязательна):
//     «CandyFont SDF.asset»  готовый TMP Font Asset (Window > TextMeshPro > Font Asset Creator, набор символов Cyrillic), или
//     «CandyFont.ttf»        обычный шрифт (Nunito, Comfortaa или Marmelad): TMP сам соберёт динамический атлас при запуске.
// Пока файла нет, текст идёт старым шрифтом HUD, ничего не ломается (в Console будет одно предупреждение).
// Шрифт включается только в скине Candy (см. UiKit.ResolveFont), латунные окна остаются со своим.
//
// ИСКРЫ: CandyButtonJuice сыплет искры, если у него есть CandyFxKit. UiCandy.Kit ищет его сам: Resources/CandyFxKit, затем любая
// CandyButtonJuice в сцене с заполненным kit (после меню «Candy Kit > Fun > 2. Оживить все кнопки» такие есть). Без kit кнопки
// всё равно пружинят, только без искр. Можно задать вручную: UiCandy.Kit = мойKit.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// оттенок для карточек и чипов (красится через Image.color поверх светлого спрайта)
public enum UiTone { Neutral, Mint, Lemon, Peach, Lavender, Locked, Danger }

public static partial class UiCandy
{
    // ======================================================================================
    //  ПАЛИТРА
    // ======================================================================================
    static Color C(int r, int g, int b, int a = 255) { return new Color(r / 255f, g / 255f, b / 255f, a / 255f); }

    public static readonly Color Ink = UiKit.CandyInk;               // основной текст и контур #2B2F5C
    public static readonly Color Muted = UiKit.CandyMuted;           // второстепенный текст #5E6A96
    public static readonly Color Done = C(0x2A, 0x95, 0x73);         // «выполнено» (как в CandyTaskView)
    public static readonly Color Danger = C(0xC8, 0x36, 0x5C);       // ошибка, нехватка (ягодный: читается на светлом)
    public static readonly Color Gold = C(0xFF, 0xB5, 0x2E);         // монеты: заливка, блики, значки
    public static readonly Color GoldText = C(0x9A, 0x5F, 0x00);     // монеты: текст-число на светлом поле
    public static readonly Color Cloud = C(0xF4, 0xF7, 0xFF);        // светлое поле
    public static readonly Color Sky = C(0x73, 0xBD, 0xFF);
    public static readonly Color SkyLight = C(0xCF, 0xE9, 0xFF);
    public static readonly Color Mint = C(0x5F, 0xD9, 0xA2);
    public static readonly Color MintLight = C(0xDD, 0xF7, 0xEA);
    public static readonly Color Peach = C(0xFF, 0xD1, 0xCC);
    public static readonly Color PeachDeep = C(0xFF, 0xB4, 0x5E);
    public static readonly Color Lemon = C(0xFF, 0xDD, 0x48);
    public static readonly Color LemonLight = C(0xFF, 0xF3, 0xC4);
    public static readonly Color Lavender = C(0xBD, 0xC2, 0xFA);
    public static readonly Color LavenderDeep = C(0x8E, 0x86, 0xEE);

    // оттенок поверх светлой карточки или пилюли
    public static Color TintOf(UiTone t)
    {
        switch (t)
        {
            case UiTone.Mint: return MintLight;
            case UiTone.Lemon: return LemonLight;
            case UiTone.Peach: return C(0xFF, 0xE3, 0xD6);
            case UiTone.Lavender: return C(0xE4, 0xE7, 0xFF);
            case UiTone.Locked: return C(0xD5, 0xDA, 0xEC);
            case UiTone.Danger: return C(0xFF, 0xD9, 0xE1);
        }
        return Color.white;
    }

    // цвет искр для кнопки каждого стиля
    public static Color FxColor(UiBtn style)
    {
        switch (style)
        {
            case UiBtn.Green: return C(0xA8, 0xF0, 0xCE);
            case UiBtn.Gray: return C(0xC9, 0xCF, 0xF5);
        }
        return C(0xFF, 0xEB, 0x8C);
    }

    // ======================================================================================
    //  СКИН, ШРИФТ, ИСКРЫ
    // ======================================================================================
    static IDisposable Scope() { return UiKit.UseSkin(UiSkin.Candy); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { kit = null; nextKitTry = 0f; font = null; fontTried = false; }

    public const string FontResource = "Fonts/CandyFont";          // сырой шрифт (.ttf, .otf)
    public const string FontAssetResource = "Fonts/CandyFont SDF"; // готовый TMP Font Asset
    static TMP_FontAsset font;
    static bool fontTried;

    // единый круглый шрифт Candy. null, если файла нет: тогда UiKit.ResolveFont вернёт старый шрифт
    public static TMP_FontAsset RoundFont
    {
        get
        {
            if (font != null || fontTried) return font;
            fontTried = true;
            font = Resources.Load<TMP_FontAsset>(FontAssetResource);
            if (font == null)
            {
                var raw = Resources.Load<UnityEngine.Font>(FontResource);
                if (raw != null)
                {
                    font = TMP_FontAsset.CreateFontAsset(raw, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                    if (font != null) font.name = "CandyFont (runtime)";
                }
            }
            if (font == null)
                Debug.LogWarning("UiCandy: не найден шрифт Candy. Положи «CandyFont SDF.asset» или «CandyFont.ttf» (с кириллицей) в Resources/Fonts. Пока текст идёт шрифтом HUD.");
            return font;
        }
    }

    static CandyFxKit kit;
    static float nextKitTry;

    // набор спрайтов для искр. Не найден: кнопки работают без искр
    public static CandyFxKit Kit
    {
        get
        {
            if (kit != null) return kit;
            if (Time.unscaledTime < nextKitTry) return null;
            nextKitTry = Time.unscaledTime + 3f;   // не сканировать сцену на каждую кнопку
            kit = Resources.Load<CandyFxKit>("CandyFxKit");
            if (kit == null)
            {
                var all = UnityEngine.Object.FindObjectsByType<CandyButtonJuice>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < all.Length && kit == null; i++) kit = all[i].kit;
            }
            return kit;
        }
        set { kit = value; }
    }

    // «желе» на кнопку: пружина, искры, «мотает головой», если недоступна
    public static CandyButtonJuice Juice(GameObject go, UiBtn style, bool attention = false)
    {
        var j = go.GetComponent<CandyButtonJuice>();
        if (j == null) j = go.AddComponent<CandyButtonJuice>();
        j.kit = Kit;
        j.fxColor = FxColor(style);
        j.attention = attention;
        return j;
    }

    // сменить цвет уже собранной кнопки (вкладка выбрана или нет). Всегда берёт спрайты Candy
    public static void SetStyle(Button b, UiBtn style)
    {
        if (b == null) return;
        using (Scope())
        {
            var img = b.targetGraphic as Image;
            if (img != null) img.sprite = UiKit.BtnSprite(style, UiBtnState.Normal);
            var ss = b.spriteState;
            ss.highlightedSprite = UiKit.BtnSprite(style, UiBtnState.Hover);
            ss.pressedSprite = UiKit.BtnSprite(style, UiBtnState.Pressed);
            ss.selectedSprite = UiKit.BtnSprite(style, UiBtnState.Normal);
            ss.disabledSprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Disabled);
            b.spriteState = ss;
            var j = b.GetComponent<CandyButtonJuice>();
            if (j != null) j.fxColor = FxColor(style);
        }
    }

    // ======================================================================================
    //  МЕЛОЧИ
    // ======================================================================================
    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // растянуть на родителя с отступами (слева, снизу, справа, сверху)
    static void Stretch(RectTransform rt, float l, float b, float r, float t)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    // текст индиго круглым шрифтом Candy
    public static TMP_Text Label(Transform parent, string text, float size, Color? color = null,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
    {
        using (Scope()) return UiKit.MakeLabel(parent, text, size, color ?? Ink, align, bold);
    }

    // иконка Candy (готовая из HUD или нарисованная кодом), независимо от текущего скина окна
    public static Image Icon(Transform parent, UiIcon icon, float size, string name = null)
    {
        using (Scope()) return UiKit.MakeIcon(parent, icon, size, name);
    }

    // ======================================================================================
    //  ОКНО
    // ======================================================================================
    public sealed class WindowRefs
    {
        public RectTransform root;      // само окно (по центру родителя, можно двигать и менять sizeDelta)
        public RectTransform content;   // сюда класть всё содержимое (ниже ленты, выше «юбки»)
        public Image panel;
        public TMP_Text title;
        public Button close;            // null, если onClose не задан
        public CandyPopIn popIn;
        public float headerHeight;
        public void Replay() { if (popIn != null) popIn.Replay(); }
    }

    public const float WindowScale = 1.5f;   // во столько раз толще ободок окна, чем в спрайте

    public static WindowRefs Window(Transform parent, Vector2 size, string title, Action onClose = null, string name = "CandyWindow")
    {
        using (Scope())
        {
            var w = new WindowRefs();
            float s = WindowScale;
            float hh = 32f * s;   // высота ленты-заголовка в спрайте PanelWood: 32 дизайн-пикселя
            w.headerHeight = hh;

            var panel = UiKit.MakePanel(parent, size, true, s, name);
            panel.raycastTarget = true;   // окно не пропускает клики на то, что под ним
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            w.panel = panel;
            w.root = rt;

            // заголовок на ленте. Поля по 70 слева и справа: справа живёт «×», слева такой же запас для симметрии
            var tx = UiKit.MakeLabel(panel.transform, title, 34f, Ink, TextAlignmentOptions.Center, true);
            var tr = tx.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-140f, hh);
            tr.anchoredPosition = new Vector2(0f, -3f);
            tx.enableAutoSizing = true;
            tx.fontSizeMin = 20f;
            tx.fontSizeMax = 34f;
            tx.enableWordWrapping = false;
            tx.overflowMode = TextOverflowModes.Ellipsis;
            w.title = tx;

            if (onClose != null)
            {
                var b = UiKit.MakeButton(panel.transform, "", UiBtn.Gray, new Vector2(44f, 44f), 28f, 0.9f);
                var br = (RectTransform)b.transform;
                br.anchorMin = br.anchorMax = new Vector2(1f, 1f);
                br.pivot = new Vector2(1f, 0.5f);
                br.anchoredPosition = new Vector2(-16f, -hh * 0.5f - 2f);
                var cross = UiKit.MakeIcon(b.transform, UiIcon.Cross, 26f, "Cross");
                cross.rectTransform.anchoredPosition = new Vector2(0f, 2f);
                b.onClick.AddListener(() => onClose());
                w.close = b;
            }

            // область содержимого: под лентой, над голубой «юбкой», без заступа на скруглённые углы
            var content = NewRect(panel.transform, "Content");
            Stretch(content, 24f, 15f * s + 10f, 24f, hh + 10f);
            w.content = content;

            w.popIn = panel.gameObject.AddComponent<CandyPopIn>();   // окно выскакивает при каждом включении
            return w;
        }
    }

    // ======================================================================================
    //  ЧИП: иконка и число
    // ======================================================================================
    public sealed class ChipRefs
    {
        public Image bg, icon;
        public TMP_Text label;
        public RectTransform rt { get { return bg.rectTransform; } }
        public void Set(string text) { label.text = text; }
        public void Set(string text, Color color) { label.text = text; label.color = color; }
    }

    // ширина подгоняется под текст (ContentSizeFitter), высота задана. Neutral: светлая ямка, Peach: тёплая (для монет)
    public static ChipRefs Chip(Transform parent, UiIcon icon, string text, float height = 44f, UiTone tone = UiTone.Neutral, float minWidth = 0f)
    {
        using (Scope())
        {
            var c = new ChipRefs();
            var bg = UiKit.MakeImage(parent, "Chip", UiKit.Slot, true, height / 40f);
            bg.color = TintOf(tone);
            bg.rectTransform.sizeDelta = new Vector2(minWidth, height);
            c.bg = bg;

            var hlg = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(Mathf.RoundToInt(height * 0.22f), Mathf.RoundToInt(height * 0.32f), 2, 2);
            hlg.spacing = height * 0.14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            if (minWidth > 0f) bg.gameObject.AddComponent<LayoutElement>().minWidth = minWidth;

            float isz = height * 0.72f;
            var ic = UiKit.MakeIcon(bg.transform, icon, isz);
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = isz;
            le.preferredHeight = le.minHeight = isz;
            c.icon = ic;

            var tx = UiKit.MakeLabel(bg.transform, text, Mathf.Round(height * 0.52f), tone == UiTone.Locked ? Muted : Ink, TextAlignmentOptions.Left, true);
            tx.enableWordWrapping = false;
            tx.overflowMode = TextOverflowModes.Overflow;
            c.label = tx;
            return c;
        }
    }

    // ======================================================================================
    //  КАРТОЧКА-СТРОКА
    // ======================================================================================
    public sealed class CardRefs
    {
        public Image bg, icon;
        public RectTransform rt;
        public RectTransform right;     // правый слот: кладите туда чип или кнопку (ширина rightWidth, по центру по высоте). null без rightWidth
        public TMP_Text title, sub;     // sub может быть null
    }

    // строка списка: медальон с иконкой слева, название и пояснение, справа слот. Ширину задаёт родитель (растяните или задайте sizeDelta.x)
    public static CardRefs Card(Transform parent, float height, string title, string sub = null, UiIcon? icon = null,
                                UiTone tone = UiTone.Neutral, float rightWidth = 0f, string name = "Card")
    {
        using (Scope())
        {
            var c = new CardRefs();
            float bs = Mathf.Clamp(height / 110f, 0.45f, 0.8f);
            float skirt = 15f * bs;           // голубая «юбка» панели
            float padX = 14f;
            float usable = height - skirt - 8f;

            var bg = UiKit.MakeImage(parent, name, UiKit.Panel, true, bs);
            bg.color = TintOf(tone);
            bg.rectTransform.sizeDelta = new Vector2(0f, height);
            c.bg = bg;
            c.rt = bg.rectTransform;

            float left = padX;
            if (icon.HasValue)
            {
                float msz = Mathf.Min(usable, height - 14f);
                var med = UiKit.MakeImage(bg.transform, "IconBack", UiKit.Medallion, false);
                med.color = tone == UiTone.Locked ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
                var mr = med.rectTransform;
                mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f);
                mr.pivot = new Vector2(0f, 0.5f);
                mr.sizeDelta = new Vector2(msz, msz);
                mr.anchoredPosition = new Vector2(padX, skirt * 0.5f);
                var ic = UiKit.MakeIcon(med.transform, icon.Value, msz * 0.62f);
                ic.rectTransform.anchoredPosition = Vector2.zero;
                if (tone == UiTone.Locked) ic.color = new Color(1f, 1f, 1f, 0.7f);
                c.icon = ic;
                left = padX + msz + 12f;
            }

            float right = padX;
            if (rightWidth > 0f)
            {
                var slot = NewRect(bg.transform, "Right");
                slot.anchorMin = new Vector2(1f, 0f);
                slot.anchorMax = new Vector2(1f, 1f);
                slot.pivot = new Vector2(1f, 0.5f);
                slot.sizeDelta = new Vector2(rightWidth, -(skirt + 8f));
                slot.anchoredPosition = new Vector2(-padX, skirt * 0.5f);
                c.right = slot;
                right = padX + rightWidth + 10f;
            }

            Color tcol = tone == UiTone.Locked ? Muted : Ink;
            float tsz = Mathf.Clamp(height * 0.3f, 22f, 30f);
            var tt = UiKit.MakeLabel(bg.transform, title, tsz, tcol, TextAlignmentOptions.Left, true);
            tt.enableWordWrapping = false;
            tt.overflowMode = TextOverflowModes.Ellipsis;
            c.title = tt;
            if (string.IsNullOrEmpty(sub))
            {
                Stretch(tt.rectTransform, left, skirt + 2f, right, 4f);
                tt.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                float split = skirt + 4f + usable * 0.42f;   // граница между пояснением (снизу) и названием (сверху)
                Stretch(tt.rectTransform, left, split, right, 4f);
                tt.alignment = TextAlignmentOptions.BottomLeft;
                var st = UiKit.MakeLabel(bg.transform, sub, Mathf.Clamp(height * 0.22f, 17f, 24f), Muted, TextAlignmentOptions.TopLeft, false);
                st.enableWordWrapping = false;
                st.overflowMode = TextOverflowModes.Ellipsis;
                Stretch(st.rectTransform, left, skirt + 2f, right, height - split + 2f);
                c.sub = st;
            }
            return c;
        }
    }
}

// Assets/_Project/Scripts/UI/Map/HudMinimap.Candy.cs (НЕ в Editor)
// Вид Candy для карты (задача 8). Здесь только внешний вид: логика Bake, Ground, Route, туман и сохранения не менялись.
//   CandyPrepare()      в самом начале Start: палитра карты, цвета тумана, маршрута и следа берутся из кода (до печати снимка)
//   CandyStylize()      после сборки: виньетка, пульс игрока, стрелка игрока, табличка «День N», подписи
//   CandyMarker()       кружок метки: пастельная подложка (MapIcons.Pin, цвет по виду метки), значок Candy, подпись индиго
//   FogEdge()           кромка тумана светлеет (облачко), а не темнеет (опалённый край)
//   ApplyPostTone()     тон снимка в MinimapPost.shader: светлее и холоднее
//   SkinZoomButton()    желейные кнопки зума (мини- и большая карта)
// Всё включается одной галочкой candyStyle. Выключишь, и карта вернётся к прежним цветам из инспектора.
// Цвета «Ink», «Muted», «Sky» и другие берутся из UiCandy, чтобы карта была родной рядом с остальными окнами.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class HudMinimap
{
    [Header("Candy")]
    [Tooltip("Вид Candy: облачные цвета карты, туман-дымка, круглые метки, желейные кнопки зума, маршрут цветами неба. Выключи, чтобы вернуть прежний вид")]
    public bool candyStyle = true;
    [Tooltip("Палитра карты, туман, маршрут и след берутся из кода Candy, а цвета из инспектора игнорируются. Выключи, чтобы взять свои цвета")]
    public bool candyPalette = true;
    [Tooltip("Стрелка игрока (на мини- и большой карте) заменяется Candy-стрелкой, если на объекте стрелки есть Image")]
    public bool candyPlayerArrow = true;
    [Range(0f, 0.6f)]
    [Tooltip("Ночью и на закате карта не темнее, чем нужно для чтения: оттенок суток смешивается с белым на эту долю")]
    public float nightLift = 0.3f;
    [Tooltip("Тон снимка карты (MinimapPost.shader): к этому цвету смешивается картинка, чтобы стать светлее и холоднее")]
    public Color toneColor = new Color32(0xCF, 0xE9, 0xFF, 255);
    [Range(0f, 0.5f)] public float toneAmount = 0.16f;
    [Range(0.6f, 1.2f)] [Tooltip("Меньше 1: середина светлее")] public float toneGamma = 0.92f;

    static Color CC(int r, int g, int b, int a = 255) { return new Color(r / 255f, g / 255f, b / 255f, a / 255f); }

    // ------------------------------------------------------------ цвета карты
    static MapPalette CandyPalette()
    {
        return new MapPalette
        {
            water = CC(0x8C, 0xCB, 0xFF),
            waterDeep = CC(0x6D, 0xB4, 0xF7),
            foam = CC(0xF4, 0xFA, 0xFF),
            grass = CC(0xA8, 0xE0, 0x8A),
            sand = CC(0xFF, 0xE3, 0xA8),
            forestA = CC(0x4F, 0xBF, 0x8A),
            forestB = CC(0x6E, 0xD3, 0xA0),
            rock = CC(0xB7, 0xBC, 0xE0),
            wood = CC(0xF0, 0xB2, 0x7A),
            roof = CC(0xFF, 0x7F, 0x91),
            roofLit = CC(0xFF, 0xD7, 0x6A),
            prop = CC(0xE8, 0xC9, 0xA0)
        };
    }

    // до InitBake: палитра нужна уже при создании плоских материалов
    void CandyPrepare()
    {
        if (!candyStyle || !candyPalette) return;
        palette = CandyPalette();
        background = palette.waterDeep;
        // туман: лавандово-розовая дымка вместо пергамента
        parchmentLight = CC(0xFF, 0xDD, 0xEA);
        parchmentDark = CC(0xC9, 0xC4, 0xF7);
        fogOpacity = 0.92f;
        // маршрут и след
        routeColor = UiCandy.Sky;
        trailColor = new Color(1f, 1f, 1f, 0.9f);
        routeThickness = Mathf.Max(routeThickness, 5f);
        trailThickness = Mathf.Max(trailThickness, 3f);
        phaseColor = UiCandy.Muted;
    }

    void CandyStylize()
    {
        if (!candyStyle) return;

        // виньетка миникарты: мягкая индиго-дымка по краю круга вместо чёрной
        if (miniMask != null)
        {
            var v = miniMask.Find("Vignette");
            var im = v != null ? v.GetComponent<Image>() : null;
            if (im != null) { var c = UiCandy.Muted; c.a = miniVignette * 0.55f; im.color = c; }
        }

        TintPing(miniPing);
        TintPing(bigPing);

        if (candyPlayerArrow)
        {
            SwapArrow(miniPlayer);
            SwapArrow(bigPlayer);
        }

        // «День N»: табличка становится персиковой, текст индиго
        if (dayBanner != null) StyleDayBanner(dayBanner);
        if (dayText != null)
        {
            SetLabelFont(dayText);
            dayText.color = UiCandy.Ink;
            dayText.outlineWidth = 0f;
        }
    }

    static void TintPing(Image ping)
    {
        if (ping == null) return;
        var c = UiCandy.Sky;
        c.a = ping.color.a;
        ping.color = c;
    }

    static void SwapArrow(RectTransform rt)
    {
        if (rt == null) return;
        var im = rt.GetComponent<Image>();     // стрелка из слоёв (картинки внутри) остаётся как была
        if (im == null) return;
        im.sprite = MapIcons.PlayerArrow;
        im.type = Image.Type.Simple;
        im.preserveAspect = true;
        im.color = Color.white;
    }

    // табличка «День N» (собирается Build HUD): тело получает персиковый Plaque, лишние слои выключаются
    void StyleDayBanner(RectTransform rt)
    {
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            string[] off = { "Outline", "Gloss", "Rim", "Ring" };
            for (int i = 0; i < off.Length; i++)
            {
                var c = rt.Find(off[i]);
                if (c != null) c.gameObject.SetActive(false);
            }
            var body = rt.Find("Body") as RectTransform;
            if (body != null) body.offsetMin = body.offsetMax = Vector2.zero;
            var im = body != null ? body.GetComponent<Image>() : rt.GetComponent<Image>();
            if (im == null) return;
            im.sprite = UiKit.Plaque;
            im.type = Image.Type.Sliced;
            im.pixelsPerUnitMultiplier = 1f / 0.55f;
            im.color = Color.white;
        }
    }

    // ------------------------------------------------------------ шрифт и подписи
    // Candy-шрифт (круглый, с кириллицей). Нет файла: берётся шрифт HUD, как раньше
    TMP_FontAsset LabelFont()
    {
        if (!candyStyle) return zoneLabel != null ? zoneLabel.font : null;
        TMP_FontAsset f;
        using (UiKit.UseSkin(UiSkin.Candy)) f = UiKit.ResolveFont(null);
        return f != null ? f : (zoneLabel != null ? zoneLabel.font : null);
    }

    void SetLabelFont(TMP_Text tx)
    {
        if (tx == null) return;
        var f = LabelFont();
        if (f != null) tx.font = f;
    }

    // «День N» под кругом, когда таблички в сцене нет (текст рисуется кодом)
    void CandyDayLabel(TMP_Text tx)
    {
        if (!candyStyle || tx == null) return;
        tx.color = UiCandy.Ink;
        tx.outlineWidth = 0.22f;
        tx.outlineColor = UiCandy.Cloud;      // светлая «наклейка»: читается и на траве, и на воде
    }

    Color ZoneLabelColor(MapZone z)
    {
        if (candyStyle) return UiCandy.Ink;
        return z != null ? Color.Lerp(Color.white, z.color, 0.45f) : Color.white;
    }

    Color ZoneNameColor(MapZone z, bool hide)
    {
        if (candyStyle) return hide ? UiCandy.Muted : UiCandy.Ink;
        return hide ? (Color)new Color32(0x4A, 0x3A, 0x22, 255) : Color.Lerp(Color.white, z.color, 0.3f);
    }

    Color ZoneNameOutline(bool hide)
    {
        if (candyStyle) return UiCandy.Cloud;
        return hide ? (Color)new Color32(0xE9, 0xDC, 0xB6, 255) : (Color)new Color32(0x0B, 0x10, 0x20, 255);
    }

    // цветная область зоны на большой карте: пастельнее и прозрачнее, чтобы не гасила карту
    Color ZoneFill(MapZone z)
    {
        Color c = z.color;
        float a = zoneOpacity;
        if (candyStyle) { c = Color.Lerp(c, Color.white, 0.38f); a = zoneOpacity * 0.8f; }
        return new Color(c.r, c.g, c.b, a);
    }

    // ------------------------------------------------------------ время суток
    Color CandyLift(Color c)
    {
        if (!candyStyle || nightLift <= 0f) return c;
        var r = Color.Lerp(c, Color.white, nightLift);
        r.a = c.a;
        return r;
    }

    // ------------------------------------------------------------ метки
    // Подложка: белая «конфета» (спрайт MapIcons.Pin), цвет по виду метки. Значок поверх: рисунок Candy. Подпись индиго с облачной окантовкой
    void CandyMarker(RectTransform m, Poi p, bool big)
    {
        var id = MapIcons.Guess(p.kind.key);
        var pin = m.Find("Pin");
        Image pi = pin != null ? pin.GetComponent<Image>() : null;
        if (pi == null)
        {
            var go = new GameObject("Pin", typeof(RectTransform), typeof(Image));
            go.layer = m.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(m, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            r.SetAsFirstSibling();
            pi = go.GetComponent<Image>();
        }
        pi.sprite = MapIcons.Pin;
        pi.type = Image.Type.Simple;
        pi.preserveAspect = true;
        pi.color = MapIcons.PinTint(id);
        pi.raycastTarget = false;

        var lb = m.Find("Label");
        var tx = lb != null ? lb.GetComponent<TMP_Text>() : null;
        if (tx != null)
        {
            SetLabelFont(tx);
            tx.color = UiCandy.Ink;
            tx.fontStyle = FontStyles.Bold;
            tx.outlineWidth = 0.28f;
            tx.outlineColor = UiCandy.Cloud;
        }
    }

    // ------------------------------------------------------------ туман
    // Candy: кромка тумана светлеет (как край облака). Без Candy: прежнее потемнение «опалённого края»
    Color32 FogEdge(Color32 c, float a)
    {
        float k = Mathf.SmoothStep(0.15f, 0.95f, a);
        if (candyStyle)
        {
            float w = (1f - k) * 0.55f;
            c.r = (byte)Mathf.Lerp(c.r, 255f, w);
            c.g = (byte)Mathf.Lerp(c.g, 255f, w);
            c.b = (byte)Mathf.Lerp(c.b, 255f, w);
        }
        else
        {
            float shade = Mathf.Lerp(0.55f, 1f, k);
            c.r = (byte)(c.r * shade); c.g = (byte)(c.g * shade); c.b = (byte)(c.b * shade);
        }
        c.a = (byte)(Mathf.Clamp01(a * fogOpacity) * 255f);
        return c;
    }

    // ------------------------------------------------------------ маршрут
    // штрихи по кругу цветами неба (небо, лаванда, персик), круглые концы, индиго-подложка
    void CandyRoute(MapLineGraphic g)
    {
        if (!candyStyle || g == null) return;
        g.dashColors = new[] { UiCandy.Sky, UiCandy.Lavender, UiCandy.Peach };
        g.roundCaps = true;
    }

    // ------------------------------------------------------------ тон снимка (MinimapPost.shader)
    void ApplyPostTone()
    {
        if (postMat == null) return;
        postMat.SetColor("_ToneColor", candyStyle ? toneColor : Color.white);
        postMat.SetFloat("_ToneAmount", candyStyle ? toneAmount : 0f);
        postMat.SetFloat("_ToneGamma", candyStyle ? toneGamma : 1f);
        // обводка границ слоёв: индиго вместо чёрно-синего (на светлой карте мягче и холоднее)
        postMat.SetColor("_OutlineColor", candyStyle ? CC(0x2B, 0x2F, 0x5C) : CC(0x14, 0x1F, 0x2E));
    }

    // ------------------------------------------------------------ кнопки зума
    // borderScale: во сколько раз тоньше рамка спрайта на этой кнопке (мини-кнопка 36 px: 0.6, большая 58 px: 0.7)
    void SkinZoomButton(Image im, Button b, float borderScale)
    {
        if (candyStyle)
        {
            using (UiKit.UseSkin(UiSkin.Candy))
            {
                im.sprite = UiKit.BtnSprite(UiBtn.Amber);
                im.type = Image.Type.Sliced;
                im.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
                im.preserveAspect = false;
                im.color = Color.white;
                im.raycastTarget = true;
                b.targetGraphic = im;
                b.transition = Selectable.Transition.SpriteSwap;      // наведение, нажатие и «недоступно» тоже конфетные
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                UiCandy.SetStyle(b, UiBtn.Amber);
                UiCandy.Juice(b.gameObject, UiBtn.Amber);             // пружина и искры
            }
            return;
        }

        im.sprite = UiKit.BtnSprite(UiBtn.Amber);
        im.type = Image.Type.Simple;
        im.preserveAspect = false;
        im.color = Color.white;
        im.raycastTarget = true;
        b.targetGraphic = im;
        b.transition = Selectable.Transition.ColorTint;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        b.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 0.94f, 0.78f, 1f),
            pressedColor = new Color(0.78f, 0.72f, 0.62f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }

    Color ZoomLabelColor { get { return candyStyle ? UiCandy.Ink : UiKit.Ink; } }
}

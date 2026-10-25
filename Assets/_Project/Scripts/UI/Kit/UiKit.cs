// Assets/_Project/Scripts/UI/Kit/UiKit.cs   (НЕ в Editor)
// Процедурный UI-кит «приборный щиток + деревянная табличка». Все спрайты рисуются кодом при первом обращении,
// картинок в проекте не нужно. Стиль: тёмный ободок, латунная окантовка, заклёпки, дерево с волокнами,
// янтарные кнопки с «губой» снизу.
//
// СКИНЫ: UiKit.Skin = Brass (старый латунь+дерево) или Candy (облачный, см. UiKit.Candy.cs). Скин включается на время сборки окна:
//         using (UiKit.UseSkin(UiSkin.Candy)) { ...собираем окно... }. Кэш спрайтов раздельный для каждого скина.
//
// Спрайты:   UiKit.Panel, PanelWood, Plaque, Slot, BarFrame, Medallion, Keycap, Tail,
//            UiKit.BtnSprite(стиль, состояние), UiKit.BarFill(цвет), UiKit.Icon(значок)
// Сборщики:  UiKit.MakePanel / MakeButton / MakeKeycap / MakeBar / MakeIcon / MakeLabel
// Готовые куски окон в стиле Candy (окно, вкладки, карточка, чип, счётчик, переключатель, ползунок, прокрутка): UiCandy (UiCandy.cs).
//
// Размеры. Спрайты нарисованы в «дизайн-пикселях» (1 дизайн-пиксель = 1 единица Canvas при Reference Pixels Per Unit 100)
// с запасом разрешения x2, поэтому остаются чёткими при масштабе до x2.
// Рамки 9-slice растягиваются через Image.pixelsPerUnitMultiplier: borderScale 1.5 делает ободок и заклёпки в 1.5 раза крупнее.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sdf = System.Func<float, float, float>;
using Shade = System.Func<float, float, UnityEngine.Color>;

public enum UiBtn { Amber, Green, Yellow, Gray }
public enum UiBtnState { Normal, Hover, Pressed, Disabled }
public enum UiBar { Amber, Green, Blue }
public enum UiIcon { Coin, Star, Log, Plank, Axe, Flame, Lock, Backpack, Fish, Tent, Check, Boot, Saw, Target, Cross, Clock, Sawmill, ArrowUp, Play, Box, ArrowRight, Planks, Paw, Gem, Trophy, Tree, Hut, Stall, Bridge, Scroll, Person, Hammer, Book, Bolt }

public static partial class UiKit
{
    const int Q = 2;   // пикселей текстуры на один дизайн-пиксель

    // ---------- палитра (из GDD: #1C2A3A, #F2B134, #6DBE6A, #F2D45C) ----------
    public static readonly Color Ink = C(0x0B, 0x13, 0x20);
    public static readonly Color Navy = C(0x1C, 0x2A, 0x3A);
    public static readonly Color Brass = C(0xF2, 0xB1, 0x34);
    public static readonly Color BrassHi = C(0xFF, 0xD6, 0x6B);
    public static readonly Color BrassLo = C(0xB8, 0x74, 0x1A);
    public static readonly Color WoodHi = C(0xC9, 0x8A, 0x4B);
    public static readonly Color WoodLo = C(0x8F, 0x5A, 0x2E);
    public static readonly Color Good = C(0x6D, 0xBE, 0x6A);
    public static readonly Color Warn = C(0xF2, 0xD4, 0x5C);

    // шрифт для сборщиков. Если пусто, берётся шрифт objectiveText из UIManager (там у тебя кириллица)
    public static TMP_FontAsset DefaultFont;

    static Color C(int r, int g, int b, int a = 255) { return new Color(r / 255f, g / 255f, b / 255f, a / 255f); }
    static Color Brighten(Color c, float k) { Color r = Color.Lerp(c, Color.white, k); r.a = c.a; return r; }
    static Color Darken(Color c, float k) { Color r = Color.Lerp(c, Color.black, k); r.a = c.a; return r; }
    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    // ======================================================================================
    //  КЭШ СПРАЙТОВ
    // ======================================================================================
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static Sprite Get(string key, Func<Sprite> make)
    {
        if (Skin == UiSkin.Candy) key = "candy_" + key;   // отдельный кэш для Candy
        Sprite s;
        if (cache.TryGetValue(key, out s) && s != null) return s;   // s != null: спрайт мог быть уничтожен при выходе из Play
        s = make();
        s.name = "UiKit_" + key;
        cache[key] = s;
        return s;
    }

    public static void ClearCache() { cache.Clear(); }

    public static Sprite Panel { get { return Get("panel", () => BuildPanel(false)); } }
    public static Sprite PanelWood { get { return Get("panel_wood", () => BuildPanel(true)); } }
    public static Sprite Plaque { get { return Get("plaque", BuildPlaque); } }
    public static Sprite Slot { get { return Get("slot", BuildSlot); } }
    public static Sprite BarFrame { get { return Get("bar_frame", BuildBarFrame); } }
    public static Sprite Medallion { get { return Get("medallion", () => BuildMedallion(false)); } }
    public static Sprite Keycap { get { return Get("keycap", () => BuildMedallion(true)); } }
    public static Sprite Tail { get { return Get("tail", BuildTail); } }
    public static Sprite MapRing { get { return Get("map_ring", BuildMapRing); } }   // латунное кольцо с прозрачным центром (рамка мини-карты)
    public static Sprite Banner { get { return Get("banner", BuildBanner); } }   // белый вымпел, красится через Image.color (ветки талантов)
    public static Sprite BtnSprite(UiBtn style, UiBtnState state = UiBtnState.Normal) { return Get("btn_" + style + "_" + state, () => BuildButton(style, state)); }
    public static Sprite BarFill(UiBar color) { return Get("barfill_" + color, () => BuildBarFill(color)); }
    // свои иконки: UiKit.SetIconOverride(UiIcon.Plank, моёСпрайт); null возвращает нарисованную кодом
    static readonly Dictionary<UiIcon, Sprite> iconOverrides = new Dictionary<UiIcon, Sprite>();
    public static void SetIconOverride(UiIcon icon, Sprite sprite)
    {
        if (sprite == null) iconOverrides.Remove(icon); else iconOverrides[icon] = sprite;
    }
    public static Sprite Icon(UiIcon icon)
    {
        Sprite o;
        if (iconOverrides.TryGetValue(icon, out o) && o != null) return o;
        if (Skin == UiSkin.Candy)
        {
            Sprite hud = CandyHudIcon(icon);   // готовые иконки Candy Kit из сцены, если есть
            if (hud != null) return hud;
        }
        return Get("icon_" + icon, () => BuildIcon(icon));
    }

    // все спрайты разом (для экспорта в PNG из редактора)
    public static List<KeyValuePair<string, Sprite>> BuildAll()
    {
        var list = new List<KeyValuePair<string, Sprite>>();
        Action<string, Sprite> add = (n, s) => list.Add(new KeyValuePair<string, Sprite>(n, s));
        add("panel", Panel); add("panel_wood", PanelWood); add("plaque", Plaque); add("slot", Slot);
        add("bar_frame", BarFrame); add("medallion", Medallion); add("keycap", Keycap); add("map_ring", MapRing); add("tail", Tail); add("banner", Banner);
        foreach (UiBtn b in Enum.GetValues(typeof(UiBtn)))
            foreach (UiBtnState st in Enum.GetValues(typeof(UiBtnState)))
                add("btn_" + b + "_" + st, BtnSprite(b, st));
        foreach (UiBar c in Enum.GetValues(typeof(UiBar))) add("barfill_" + c, BarFill(c));
        foreach (UiIcon i in Enum.GetValues(typeof(UiIcon))) add("icon_" + i, Icon(i));
        return list;
    }

    // ======================================================================================
    //  СБОРЩИКИ UI
    // ======================================================================================
    public static TMP_FontAsset ResolveFont(TMP_FontAsset f)
    {
        if (f != null) return f;
        if (Skin == UiSkin.Candy)
        {
            var round = UiCandy.RoundFont;   // единый круглый шрифт Candy (Resources/Fonts), null если файла нет
            if (round != null) return round;
        }
        if (DefaultFont != null) return DefaultFont;
        var ui = UIManager.Instance;
        if (ui != null && ui.objectiveText != null) return ui.objectiveText.font;
        return null;
    }

    public static Image MakeImage(Transform parent, string name, Sprite sprite, bool sliced = true, float borderScale = 1f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, borderScale);
        img.raycastTarget = false;
        img.color = Color.white;
        return img;
    }

    public static Image MakePanel(Transform parent, Vector2 size, bool wood = false, float borderScale = 1f, string name = "Panel")
    {
        var img = MakeImage(parent, name, wood ? PanelWood : Panel, true, borderScale);
        img.rectTransform.sizeDelta = size;
        return img;
    }

    public static Image MakePlaque(Transform parent, Vector2 size, float borderScale = 1f, string name = "Plaque")
    {
        var img = MakeImage(parent, name, Plaque, true, borderScale);
        img.rectTransform.sizeDelta = size;
        return img;
    }

    public static Image MakeSlot(Transform parent, float size, float borderScale = 1f, string name = "Slot")
    {
        var img = MakeImage(parent, name, Slot, true, borderScale);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    public static Image MakeIcon(Transform parent, UiIcon icon, float size, string name = null)
    {
        var img = MakeImage(parent, name ?? ("Icon_" + icon), Icon(icon), false);
        img.preserveAspect = true;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    public static TMP_Text MakeLabel(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true, TMP_FontAsset font = null)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tx = go.AddComponent<TextMeshProUGUI>();
        var f = ResolveFont(font);
        if (f != null) tx.font = f;
        tx.text = text;
        tx.fontSize = size;
        tx.color = color;
        tx.alignment = align;
        tx.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tx.raycastTarget = false;
        return tx;
    }

    static Color ButtonTextColor(UiBtn style)
    {
        if (Skin == UiSkin.Candy) return CandyInk;   // на светлых конфетных кнопках всегда индиго
        return (style == UiBtn.Amber || style == UiBtn.Yellow) ? Navy : Color.white;
    }

    // подпись на кнопке: смещена вверх на толщину «губы», чтобы стоять по центру лицевой части
    public static TMP_Text AddButtonLabel(Transform buttonTransform, string text, UiBtn style, float fontSize, float borderScale = 1f)
    {
        var tx = MakeLabel(buttonTransform, text, fontSize, ButtonTextColor(style), TextAlignmentOptions.Center, true);
        tx.rectTransform.offsetMin = new Vector2(0f, 5f * borderScale);
        tx.rectTransform.offsetMax = new Vector2(0f, -1f * borderScale);
        return tx;
    }

    public static Button MakeButton(Transform parent, string label, UiBtn style, Vector2 size, float fontSize = 30f, float borderScale = 1f)
    {
        var img = MakeImage(parent, "Button_" + label, BtnSprite(style, UiBtnState.Normal), true, borderScale);
        img.rectTransform.sizeDelta = size;
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.SpriteSwap;
        var ss = new SpriteState();
        ss.highlightedSprite = BtnSprite(style, UiBtnState.Hover);
        ss.pressedSprite = BtnSprite(style, UiBtnState.Pressed);
        ss.selectedSprite = BtnSprite(style, UiBtnState.Normal);
        ss.disabledSprite = BtnSprite(UiBtn.Gray, UiBtnState.Disabled);
        btn.spriteState = ss;
        AddButtonLabel(img.transform, label, style, fontSize, borderScale);
        if (Skin == UiSkin.Candy) UiCandy.Juice(btn.gameObject, style);   // «желе»: пружина, искры, «мотает головой» если недоступна
        return btn;
    }

    // клавиша-подсказка («E») в латунном медальоне
    public static Image MakeKeycap(Transform parent, string key, float size = 64f)
    {
        var img = MakeImage(parent, "Keycap_" + key, Keycap, false);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        var tx = MakeLabel(img.transform, key, size * 0.42f, Skin == UiSkin.Candy ? CandyInk : Navy, TextAlignmentOptions.Center, true);
        tx.rectTransform.offsetMin = new Vector2(0f, size * 0.02f);
        tx.rectTransform.offsetMax = new Vector2(0f, size * 0.02f);
        return img;
    }

    // полоса: рамка + заливка. fill.fillAmount = 0..1.
    // Заливка нарисована с пропорциями 16:1 и не растягивается 9-slice'ом, поэтому лучше всего смотрится при пропорциях полосы от 10:1 до 18:1.
    public static Image MakeBar(Transform parent, UiBar color, Vector2 size, out Image fill, float borderScale = 1f)
    {
        var frame = MakeImage(parent, "Bar", BarFrame, true, borderScale);
        frame.rectTransform.sizeDelta = size;
        fill = MakeImage(frame.transform, "Fill", BarFill(color), false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        float inset = 5f * borderScale;
        var rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return frame;
    }

    // ======================================================================================
    //  ЖИВОПИСЬ: SDF-фигуры и слои
    // ======================================================================================
    static Sdf RR(float x, float y, float w, float h, float r)
    {
        float cx = x + w * 0.5f, cy = y + h * 0.5f, hx = w * 0.5f - r, hy = h * 0.5f - r;
        return (px, py) =>
        {
            float qx = Mathf.Abs(px - cx) - hx, qy = Mathf.Abs(py - cy) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        };
    }

    static Sdf Circle(float cx, float cy, float r)
    {
        return (px, py) => Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r;
    }

    static Sdf Ellipse(float cx, float cy, float rx, float ry)
    {
        float m = Mathf.Min(rx, ry);
        return (px, py) =>
        {
            float dx = (px - cx) / rx, dy = (py - cy) / ry;
            return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * m;
        };
    }

    static Sdf Capsule(float ax, float ay, float bx, float by, float r)
    {
        return (px, py) =>
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        };
    }

    static Sdf Poly(params Vector2[] v)
    {
        int n = v.Length;
        return (px, py) =>
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = v[i], b = v[j];
                float ex = b.x - a.x, ey = b.y - a.y, wx = px - a.x, wy = py - a.y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float dx = wx - ex * t, dy = wy - ey * t;
                d = Mathf.Min(d, dx * dx + dy * dy);
                if (((a.y > py) != (b.y > py)) && (px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            }
            d = Mathf.Sqrt(d);
            return inside ? -d : d;
        };
    }

    static Sdf Ring(Sdf f, float t) { return (x, y) => { float d = f(x, y); return Mathf.Max(d, -(d + t)); }; }
    static Sdf Union(Sdf a, Sdf b) { return (x, y) => Mathf.Min(a(x, y), b(x, y)); }
    static Sdf Intersect(Sdf a, Sdf b) { return (x, y) => Mathf.Max(a(x, y), b(x, y)); }
    static Sdf ClipMin(Sdf f, float minY) { return (x, y) => Mathf.Max(f(x, y), minY - y); }   // оставить только то, что выше minY

    static Shade Solid(Color c) { return (x, y) => c; }

    static Shade VGrad(float y0, float y1, Color bottom, Color top)
    {
        return (x, y) => Color.Lerp(bottom, top, Mathf.Clamp01((y - y0) / (y1 - y0)));
    }

    // дерево: вертикальный градиент и волокна (вытянутый по горизонтали шум)
    static Shade Wood(float y0, float y1, Color bottom, Color top, float seed)
    {
        return (x, y) =>
        {
            Color c = Color.Lerp(bottom, top, Mathf.Clamp01((y - y0) / (y1 - y0)));
            float n = Noise(x * 0.045f, y * 0.55f + seed) - 0.5f;
            float n2 = Noise(x * 0.12f, y * 1.9f + seed * 3f) - 0.5f;
            float k = 1f + n * 0.28f + n2 * 0.18f;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        };
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static Color Over(Color s, Color d)
    {
        float a = s.a + d.a * (1f - s.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float k = d.a * (1f - s.a);
        return new Color((s.r * s.a + d.r * k) / a, (s.g * s.a + d.g * k) / a, (s.b * s.a + d.b * k) / a, a);
    }

    // холст: координаты в дизайн-пикселях, начало внизу слева, ось Y вверх
    sealed class Painter
    {
        public readonly int W, H;
        public readonly Color[] px;

        public Painter(int designW, int designH)
        {
            W = designW * Q;
            H = designH * Q;
            px = new Color[W * H];
        }

        public void Fill(Sdf f, Shade c)
        {
            float inv = 1f / Q;
            for (int y = 0; y < H; y++)
            {
                float dy = (y + 0.5f) * inv;
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) * inv;
                    float cov = Mathf.Clamp01(0.5f - f(dx, dy) * Q);
                    if (cov <= 0f) continue;
                    Color s = c(dx, dy);
                    s.a *= cov;
                    int i = y * W + x;
                    px[i] = Over(s, px[i]);
                }
            }
        }

        public void Fill(Sdf f, Color c) { Fill(f, Solid(c)); }

        // тёмный контур вокруг всего нарисованного (для иконок)
        public void Outline(float t, Color c)
        {
            int r = Mathf.CeilToInt(t * Q);
            var a = new float[px.Length];
            for (int i = 0; i < a.Length; i++) a[i] = px[i].a;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float m = a[y * W + x];
                    if (m < 0.999f)
                    {
                        for (int oy = -r; oy <= r; oy++)
                        {
                            int yy = y + oy;
                            if (yy < 0 || yy >= H) continue;
                            for (int ox = -r; ox <= r; ox++)
                            {
                                if (ox * ox + oy * oy > r * r) continue;
                                int xx = x + ox;
                                if (xx < 0 || xx >= W) continue;
                                float v = a[yy * W + xx];
                                if (v > m) m = v;
                            }
                        }
                    }
                    if (m > 0.001f)
                    {
                        int i = y * W + x;
                        px[i] = Over(px[i], new Color(c.r, c.g, c.b, m * c.a));
                    }
                }
            }
        }

        // цвет границы «протекает» в прозрачные пиксели, иначе при билинейной фильтрации по краям лезет тёмная кайма
        public void Bleed()
        {
            var src = (Color[])px.Clone();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (src[i].a > 0.001f) continue;
                    bool found = false;
                    for (int oy = -2; oy <= 2 && !found; oy++)
                    {
                        int yy = y + oy;
                        if (yy < 0 || yy >= H) continue;
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int xx = x + ox;
                            if (xx < 0 || xx >= W) continue;
                            Color n = src[yy * W + xx];
                            if (n.a > 0.05f) { px[i] = new Color(n.r, n.g, n.b, 0f); found = true; break; }
                        }
                    }
                }
            }
        }
    }

    static Sprite Make(Painter p, float bl, float bb, float br, float bt)
    {
        p.Bleed();
        var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(p.px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, p.W, p.H), new Vector2(0.5f, 0.5f), 100f * Q, 0,
                             SpriteMeshType.FullRect, new Vector4(bl * Q, bb * Q, br * Q, bt * Q));
    }

    static void Rivet(Painter p, float x, float y)
    {
        p.Fill(Circle(x, y, 4.2f), Ink);
        p.Fill(Circle(x, y, 3.2f), VGrad(y - 3.2f, y + 3.2f, BrassLo, BrassHi));
        p.Fill(Circle(x - 0.9f, y + 1f, 1f), new Color(1f, 1f, 1f, 0.55f));
    }

    // ======================================================================================
    //  ПАНЕЛИ
    // ======================================================================================
    static Sprite BuildPanel(bool wood)
    {
        if (Skin == UiSkin.Candy) return BuildPanelCandy(wood);
        const float S = 96f, R = 16f;
        var p = new Painter(96, 96);
        p.Fill(RR(0, 0, S, S, R), Ink);
        p.Fill(RR(3, 3, S - 6, S - 6, R - 3), VGrad(3, S - 3, BrassLo, BrassHi));
        p.Fill(RR(6, 6, S - 12, S - 12, R - 6), Ink);
        Sdf body = RR(7, 7, S - 14, S - 14, R - 7);
        p.Fill(body, VGrad(7, S - 7, C(0x15, 0x21, 0x30), C(0x22, 0x34, 0x4A)));
        p.Fill(Ring(body, 1.5f), new Color(1f, 1f, 1f, 0.07f));

        Rivet(p, 15, 15);
        Rivet(p, S - 15, 15);

        if (wood)
        {
            float hy = S - 32f;   // нижняя граница деревянной планки-заголовка
            p.Fill(ClipMin(RR(0, hy - R, S, 32f + R, R), hy), Ink);
            p.Fill(ClipMin(RR(3, hy - R, S - 6, 32f + R - 3, R - 3), hy + 2f), Wood(hy + 2f, S - 3f, WoodLo, WoodHi, 3f));
            p.Fill(Intersect(body, RR(0, hy - 6, S, 6, 0)), new Color(0f, 0f, 0f, 0.22f));   // тень под планкой
            p.Fill(RR(8, S - 9, S - 16, 2f, 1f), new Color(1f, 1f, 1f, 0.18f));              // блик сверху
            Rivet(p, 12, S - 17);
            Rivet(p, S - 12, S - 17);
        }
        else
        {
            Rivet(p, 15, S - 15);
            Rivet(p, S - 15, S - 15);
        }
        return Make(p, 26, 26, 26, wood ? 36 : 26);
    }

    static Sprite BuildPlaque()
    {
        if (Skin == UiSkin.Candy) return BuildPlaqueCandy();
        var p = new Painter(72, 44);
        p.Fill(RR(0, 0, 72, 44, 12), Ink);
        Sdf face = RR(3, 3, 66, 38, 9);
        p.Fill(face, Wood(3, 41, WoodLo, WoodHi, 7f));
        p.Fill(Ring(face, 1.5f), new Color(0f, 0f, 0f, 0.18f));
        p.Fill(RR(7, 35, 58, 2f, 1f), new Color(1f, 1f, 1f, 0.2f));
        Rivet(p, 10, 22);
        Rivet(p, 62, 22);
        return Make(p, 18, 18, 18, 18);
    }

    static Sprite BuildSlot()
    {
        if (Skin == UiSkin.Candy) return BuildSlotCandy();
        var p = new Painter(56, 56);
        p.Fill(RR(0, 0, 56, 56, 10), Ink);
        p.Fill(RR(2, 2, 52, 52, 8), VGrad(2, 54, BrassLo, Brass));
        p.Fill(RR(4, 4, 48, 48, 6), VGrad(4, 52, C(0x1A, 0x26, 0x35), C(0x0F, 0x18, 0x24)));   // внутри темнее сверху: вдавленность
        p.Fill(Ring(RR(4, 4, 48, 48, 6), 1.5f), new Color(0f, 0f, 0f, 0.25f));
        return Make(p, 16, 16, 16, 16);
    }

    static Sprite BuildBarFrame()
    {
        if (Skin == UiSkin.Candy) return BuildBarFrameCandy();
        var p = new Painter(128, 28);
        p.Fill(RR(0, 0, 128, 28, 14), Ink);
        p.Fill(RR(2, 2, 124, 24, 12), VGrad(2, 26, BrassLo, BrassHi));
        p.Fill(RR(4, 4, 120, 20, 10), Ink);
        p.Fill(RR(5, 5, 118, 18, 9), VGrad(5, 23, C(0x0F, 0x18, 0x24), C(0x1B, 0x28, 0x38)));
        return Make(p, 14, 14, 14, 14);
    }

    static Sprite BuildBarFill(UiBar color)
    {
        if (Skin == UiSkin.Candy) return BuildBarFillCandy(color);
        Color c;
        switch (color)
        {
            case UiBar.Green: c = Good; break;
            case UiBar.Blue: c = C(0x3D, 0x9B, 0xE0); break;
            default: c = Brass; break;
        }
        var p = new Painter(256, 16);
        Sdf pill = RR(0, 0, 256, 16, 8);
        p.Fill(pill, VGrad(0, 16, Darken(c, 0.2f), Brighten(c, 0.12f)));
        for (int i = 1; i < 8; i++) p.Fill(RR(i * 32f - 1f, 0, 2f, 16f, 0f), new Color(0f, 0f, 0f, 0.22f));   // сегменты
        p.Fill(RR(8, 10, 240, 3f, 1.5f), new Color(1f, 1f, 1f, 0.28f));
        return Make(p, 0, 0, 0, 0);
    }

    static Sprite BuildMedallion(bool withKey)
    {
        if (Skin == UiSkin.Candy) return BuildMedallionCandy(withKey);
        var p = new Painter(48, 48);
        p.Fill(Circle(24, 24, 24), Ink);
        p.Fill(Circle(24, 24, 22), VGrad(2, 46, BrassLo, BrassHi));
        p.Fill(Circle(24, 24, 19.5f), Ink);
        p.Fill(Circle(24, 24, 18.5f), VGrad(5, 43, C(0x15, 0x21, 0x30), C(0x24, 0x38, 0x50)));
        if (withKey)
        {
            p.Fill(RR(10.5f, 9f, 27f, 29f, 7.5f), Ink);
            p.Fill(RR(12f, 10.5f, 24f, 26f, 6.5f), C(0xB0, 0x66, 0x18));                         // «губа» клавиши
            p.Fill(RR(12f, 14.5f, 24f, 22f, 6.5f), VGrad(14.5f, 36.5f, C(0xEE, 0x9A, 0x2C), C(0xFF, 0xC2, 0x4D)));
            p.Fill(RR(15f, 31f, 18f, 2.5f, 1.25f), new Color(1f, 1f, 1f, 0.35f));
        }
        return Make(p, 0, 0, 0, 0);
    }

    static Sprite BuildMapRing()
    {
        if (Skin == UiSkin.Candy) return BuildMapRingCandy();
        var p = new Painter(96, 96);
        p.Fill(Ring(Circle(48, 48, 48f), 6f), Ink);
        p.Fill(Ring(Circle(48, 48, 47f), 3.6f), VGrad(0, 96, BrassLo, BrassHi));
        return Make(p, 0, 0, 0, 0);
    }

    // хвостик для плашек-подсказок, ставится под панелью по центру внахлёст на 7 единиц
    static Sprite BuildTail()
    {
        if (Skin == UiSkin.Candy) return BuildTailCandy();
        var p = new Painter(36, 22);
        p.Fill(Poly(V(0, 22), V(36, 22), V(18, 0)), Ink);
        p.Fill(Poly(V(4, 22), V(32, 22), V(18, 5.5f)), VGrad(0, 22, BrassLo, BrassHi));
        p.Fill(Poly(V(8, 22), V(28, 22), V(18, 11f)), C(0x1E, 0x2F, 0x44));
        return Make(p, 0, 0, 0, 0);
    }

    // вымпел с вырезом снизу: белый, чтобы красить через Image.color. Пропорции 50:64
    static Sprite BuildBanner()
    {
        if (Skin == UiSkin.Candy) return BuildBannerCandy();
        var p = new Painter(50, 64);
        p.Fill(Poly(V(0, 64), V(50, 64), V(50, 11), V(25, 0), V(0, 11)), Ink);
        Sdf body = Poly(V(2.5f, 64), V(47.5f, 64), V(47.5f, 12.2f), V(25, 3.2f), V(2.5f, 12.2f));
        p.Fill(body, VGrad(3, 64, new Color(0.78f, 0.78f, 0.78f, 1f), Color.white));
        p.Fill(RR(6, 14, 4f, 44f, 2f), new Color(1f, 1f, 1f, 0.35f));            // блик вдоль левого края
        p.Fill(Intersect(body, RR(0, 50, 50, 14, 0)), new Color(0f, 0f, 0f, 0.10f));   // тень под планкой сверху
        return Make(p, 0, 0, 0, 0);
    }

    // ======================================================================================
    //  КНОПКИ
    // ======================================================================================
    static void BtnColors(UiBtn s, out Color top, out Color mid, out Color lip)
    {
        switch (s)
        {
            case UiBtn.Amber: top = C(0xFF, 0xC2, 0x4D); mid = C(0xEE, 0x9A, 0x2C); lip = C(0xB0, 0x66, 0x18); break;
            case UiBtn.Green: top = C(0x86, 0xD9, 0x7F); mid = C(0x58, 0xB0, 0x55); lip = C(0x36, 0x7C, 0x38); break;
            case UiBtn.Yellow: top = C(0xFF, 0xE5, 0x7A); mid = C(0xF2, 0xC8, 0x35); lip = C(0xB3, 0x8F, 0x1B); break;
            default: top = C(0xA6, 0xA6, 0xA6); mid = C(0x80, 0x80, 0x80); lip = C(0x57, 0x57, 0x57); break;
        }
    }

    static Sprite BuildButton(UiBtn style, UiBtnState state)
    {
        if (Skin == UiSkin.Candy) return BuildButtonCandy(style, state);
        const float W = 64f, H = 44f, R = 12f;
        Color top, mid, lip;
        BtnColors(state == UiBtnState.Disabled ? UiBtn.Gray : style, out top, out mid, out lip);
        float lipH = 6f;
        if (state == UiBtnState.Hover) { top = Brighten(top, 0.14f); mid = Brighten(mid, 0.10f); }
        else if (state == UiBtnState.Pressed) { top = Darken(top, 0.06f); mid = Darken(mid, 0.08f); lipH = 2f; }
        else if (state == UiBtnState.Disabled) lipH = 3f;

        var p = new Painter(64, 44);
        p.Fill(RR(0, 0, W, H, R), Ink);
        p.Fill(RR(2, 2, W - 4, H - 4, R - 2), lip);
        float fy = 2f + lipH;
        p.Fill(RR(2, fy, W - 4, H - 2f - fy, R - 2), VGrad(fy, H - 2f, mid, top));
        if (state != UiBtnState.Disabled) p.Fill(RR(6, H - 9f, W - 12f, 3.5f, 1.75f), new Color(1f, 1f, 1f, 0.32f));
        return Make(p, 16, 16, 16, 16);
    }

    // ======================================================================================
    //  ИКОНКИ (64x64, без рамки, с тёмным контуром)
    // ======================================================================================
    static Sprite BuildIcon(UiIcon icon)
    {
        if (Skin == UiSkin.Candy) return BuildIconCandy(icon);
        var p = new Painter(64, 64);
        DrawIcon(p, icon);
        p.Outline(2f, Ink);
        return Make(p, 0, 0, 0, 0);
    }

    // одна доска: дерево с волокнами, блик, сучок, гвоздь и светлый торец с годовыми кольцами справа
    static void DrawBoard(Painter p, float x, float y, float w, float h, float seed)
    {
        p.Fill(RR(x, y, w, h, Mathf.Min(4f, h * 0.3f)), Wood(y, y + h, C(0xC0, 0x8C, 0x52), C(0xF0, 0xD0, 0x98), seed));
        p.Fill(RR(x + 3f, y + h - 3.4f, w - 15f, 1.7f, 0.85f), new Color(1f, 1f, 1f, 0.38f));          // блик сверху
        p.Fill(RR(x + 2f, y + 1.2f, w - 11f, 1.6f, 0.8f), new Color(0.35f, 0.2f, 0.06f, 0.25f));       // тень снизу
        p.Fill(RR(x + 6f, y + h * 0.5f - 0.5f, w * 0.45f, 1.1f, 0.55f), new Color(0.4f, 0.25f, 0.1f, 0.28f));   // волокно
        p.Fill(RR(x + w - 7.5f, y, 7.5f, h, Mathf.Min(4f, h * 0.3f)), C(0xF6, 0xDF, 0xB2));            // торец
        p.Fill(RR(x + w - 7.5f, y + 0.8f, 1.2f, h - 1.6f, 0.6f), new Color(0.45f, 0.28f, 0.1f, 0.45f));
        p.Fill(Ring(Ellipse(x + w - 3.6f, y + h * 0.5f, 2.3f, Mathf.Max(2.2f, h * 0.36f)), 0.8f), C(0xB9, 0x84, 0x55, 200));
        p.Fill(Circle(x + 6f, y + h * 0.5f, 1.15f), new Color(0.3f, 0.17f, 0.06f, 0.7f));              // гвоздь
    }

    static void DrawIcon(Painter p, UiIcon icon)
    {
        switch (icon)
        {
            case UiIcon.Coin:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, C(0xB8, 0x7A, 0x14), C(0xFF, 0xD9, 0x5A)));
                p.Fill(Circle(32, 32, 21.5f), VGrad(10, 54, C(0xF2, 0xB1, 0x2A), C(0xFF, 0xE0, 0x70)));
                p.Fill(Ring(Circle(32, 32, 15.5f), 1.8f), C(0xC4, 0x84, 0x14, 150));
                p.Fill(Capsule(22, 41, 30, 49, 1.7f), new Color(1f, 1f, 1f, 0.7f));
                p.Fill(Circle(35, 51.5f, 1.4f), new Color(1f, 1f, 1f, 0.7f));
                break;

            case UiIcon.Star:
            {
                var pts = new Vector2[10];
                var small = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float r = (i % 2 == 0) ? 27f : 11.5f;
                    pts[i] = V(32f + Mathf.Cos(a) * r, 30.5f + Mathf.Sin(a) * r);
                    small[i] = V(32f + Mathf.Cos(a) * r * 0.55f, 32f + Mathf.Sin(a) * r * 0.55f);
                }
                p.Fill(Poly(pts), VGrad(8, 58, C(0xE0, 0x9A, 0x14), C(0xFF, 0xDC, 0x55)));
                p.Fill(Poly(small), new Color(1f, 1f, 1f, 0.28f));
                break;
            }

            case UiIcon.Log:
                p.Fill(RR(14, 15, 40, 32, 7), VGrad(15, 47, C(0x8A, 0x5A, 0x34), C(0xB9, 0x83, 0x53)));
                p.Fill(Capsule(22, 22, 48, 22, 0.9f), new Color(0f, 0f, 0f, 0.22f));
                p.Fill(Capsule(24, 31, 50, 31, 0.9f), new Color(0f, 0f, 0f, 0.22f));
                p.Fill(Capsule(22, 40, 46, 40, 0.9f), new Color(0f, 0f, 0f, 0.22f));
                p.Fill(Capsule(38, 49, 44, 53, 2.6f), C(0x8A, 0x5A, 0x34));                              // сучок
                p.Fill(Ellipse(14, 31, 10.5f, 16f), Ink);
                p.Fill(Ellipse(14, 31, 9.4f, 14.9f), VGrad(16, 46, C(0xD2, 0xA6, 0x70), C(0xEC, 0xC8, 0x92)));
                p.Fill(Ring(Ellipse(14, 31, 6.5f, 10.5f), 1.2f), C(0xB9, 0x84, 0x55, 190));
                p.Fill(Ring(Ellipse(14, 31, 3f, 5f), 1.1f), C(0xB9, 0x84, 0x55, 190));
                break;

            case UiIcon.Plank:
                DrawBoard(p, 5f, 8f, 52f, 20f, 2f);
                DrawBoard(p, 9f, 32f, 52f, 20f, 5f);
                break;

            case UiIcon.Planks:
            {
                float[] bx = { 6f, 10f, 4f, 9f };
                for (int i = 0; i < 4; i++) DrawBoard(p, bx[i], 5f + i * 13f, 50f, 11.5f, 2f + i);
                break;
            }

            case UiIcon.Axe:
            {
                Vector2 a = V(12f, 9f), b = V(48f, 50f);
                Vector2 dir = (b - a).normalized, nrm = new Vector2(-dir.y, dir.x);
                Vector2 c = a + (b - a) * 0.8f;
                Func<float, float, Vector2> at = (u, v) => c + dir * u + nrm * v;
                p.Fill(Capsule(a.x, a.y, b.x, b.y, 3.8f), VGrad(9, 52, C(0x8A, 0x55, 0x2B), C(0xC8, 0x8A, 0x4C)));
                p.Fill(Poly(at(-4f, -9f), at(4f, -9f), at(4f, -5f), at(-4f, -5f)), C(0x59, 0x66, 0x75));   // обух
                p.Fill(Poly(at(-4.5f, -5f), at(4.5f, -5f), at(4.5f, 4f), at(10.5f, 17f), at(-10.5f, 17f), at(-4.5f, 4f)),
                       VGrad(28, 62, C(0x7C, 0x8D, 0xA0), C(0xD2, 0xDD, 0xE8)));
                p.Fill(Poly(at(-9.5f, 17f), at(9.5f, 17f), at(8.6f, 13.5f), at(-8.6f, 13.5f)), new Color(1f, 1f, 1f, 0.55f));   // кромка лезвия
                break;
            }

            case UiIcon.Flame:
                p.Fill(Union(Circle(32, 28, 15), Poly(V(17.5f, 30), V(46.5f, 30), V(35, 60))),
                       VGrad(14, 60, C(0xE2, 0x48, 0x1F), C(0xFF, 0xB0, 0x2E)));
                p.Fill(Union(Circle(32, 26, 8), Poly(V(24.5f, 27), V(39.5f, 27), V(33, 45))),
                       VGrad(18, 45, C(0xFF, 0xB0, 0x2E), C(0xFF, 0xE6, 0x80)));
                p.Fill(Capsule(10, 12, 54, 22, 5.2f), VGrad(7, 27, C(0x7A, 0x4B, 0x26), C(0xB0, 0x78, 0x45)));
                p.Fill(Capsule(10, 22, 54, 12, 5.2f), VGrad(7, 27, C(0x8A, 0x5A, 0x34), C(0xC0, 0x88, 0x50)));
                break;

            case UiIcon.Lock:
            {
                Sdf shackle = Ring(Circle(32, 38, 13), 5.5f);
                Shade steel = VGrad(26, 52, C(0x7F, 0x90, 0xA3), C(0xC8, 0xD4, 0xE0));
                p.Fill(ClipMin(shackle, 38f), steel);
                p.Fill(RR(19, 26, 5.5f, 12, 0), steel);
                p.Fill(RR(39.5f, 26, 5.5f, 12, 0), steel);
                p.Fill(RR(12, 8, 40, 28, 7), VGrad(8, 36, C(0xC9, 0x84, 0x14), C(0xFF, 0xCF, 0x4A)));
                p.Fill(RR(15, 29, 34, 3f, 1.5f), new Color(1f, 1f, 1f, 0.3f));
                p.Fill(Circle(32, 24, 4.3f), Navy);
                p.Fill(Poly(V(29.5f, 22), V(34.5f, 22), V(36, 13), V(28, 13)), Navy);
                break;
            }

            case UiIcon.Backpack:
                p.Fill(Capsule(12, 14, 12, 34, 2.6f), C(0x6B, 0x3E, 0x1E));
                p.Fill(Capsule(52, 14, 52, 34, 2.6f), C(0x6B, 0x3E, 0x1E));
                p.Fill(Ring(RR(25, 45, 14, 12, 5), 3f), C(0x7A, 0x43, 0x20));
                p.Fill(RR(14, 6, 36, 42, 11), VGrad(6, 48, C(0x8E, 0x51, 0x27), C(0xB5, 0x70, 0x3A)));
                p.Fill(RR(14, 26, 36, 22, 11), VGrad(26, 48, C(0xA8, 0x64, 0x32), C(0xCC, 0x8A, 0x4E)));
                p.Fill(RR(16, 26, 32, 2f, 1f), new Color(0f, 0f, 0f, 0.3f));
                p.Fill(RR(20, 8, 24, 15, 5), VGrad(8, 23, C(0x7A, 0x43, 0x20), C(0x9C, 0x5A, 0x2D)));
                p.Fill(RR(28, 22, 8, 9, 2), VGrad(22, 31, BrassLo, BrassHi));
                break;

            case UiIcon.Fish:
                p.Fill(Poly(V(42, 32), V(60, 47), V(55, 32), V(60, 17)), C(0x2E, 0x78, 0xB4));
                p.Fill(Ellipse(28, 32, 20, 12.5f), VGrad(19, 45, C(0x3A, 0x86, 0xC8), C(0x7C, 0xC9, 0xF2)));
                p.Fill(Poly(V(25, 42), V(35, 53), V(39, 40)), C(0x2E, 0x78, 0xB4));
                p.Fill(Ellipse(27, 26.5f, 14, 4f), new Color(1f, 1f, 1f, 0.25f));
                p.Fill(Circle(16.5f, 35, 3.2f), Color.white);
                p.Fill(Circle(16f, 35, 1.6f), Ink);
                break;

            case UiIcon.Tent:
                p.Fill(Poly(V(6, 10), V(58, 10), V(32, 54)), VGrad(10, 54, C(0x3F, 0x8F, 0x3F), C(0x7A, 0xCB, 0x6E)));
                p.Fill(Poly(V(32, 54), V(58, 10), V(32, 10)), new Color(0f, 0f, 0f, 0.14f));
                p.Fill(Poly(V(24, 10), V(40, 10), V(32, 33)), C(0x1F, 0x4D, 0x26));
                p.Fill(Capsule(32, 53, 32, 59, 1.4f), C(0x8A, 0x5A, 0x34));
                break;

            case UiIcon.Check:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, C(0x3F, 0x9B, 0x45), C(0x86, 0xD9, 0x7F)));
                p.Fill(Capsule(19, 32, 28, 22.5f, 4.2f), Color.white);
                p.Fill(Capsule(28, 22.5f, 46, 42, 4.2f), Color.white);
                break;

            case UiIcon.Boot:
                p.Fill(RR(15, 5, 46, 8, 3.5f), C(0x4A, 0x2B, 0x14));                                   // подошва
                p.Fill(Union(RR(16, 11, 24, 46, 6), RR(16, 11, 42, 18, 8)), VGrad(11, 57, C(0x8E, 0x51, 0x27), C(0xC4, 0x80, 0x42)));
                p.Fill(RR(14, 47, 28, 9, 3.5f), C(0xE8, 0xC8, 0x8F));                                  // отворот
                p.Fill(Capsule(24, 40, 34, 41, 1.3f), C(0xE8, 0xC8, 0x8F));                            // шнуровка
                p.Fill(Capsule(24, 34, 34, 35, 1.3f), C(0xE8, 0xC8, 0x8F));
                p.Fill(Capsule(24, 28, 34, 29, 1.3f), C(0xE8, 0xC8, 0x8F));
                p.Fill(RR(44, 22, 11, 3f, 1.5f), new Color(1f, 1f, 1f, 0.3f));                         // блик на носке
                break;

            case UiIcon.Saw:
            {
                Shade steel = VGrad(30, 52, C(0x8E, 0x9E, 0xB0), C(0xE0, 0xE8, 0xF0));
                p.Fill(Poly(V(20, 44), V(59, 52), V(59, 38), V(20, 32)), steel);                       // полотно
                for (int i = 0; i < 8; i++)                                                            // зубья
                {
                    float x0 = 22f + i * 4.6f, y0 = 32f + (x0 - 20f) * (6f / 39f);
                    p.Fill(Poly(V(x0, y0 + 0.6f), V(x0 + 4.6f, y0 + 1.3f), V(x0 + 2.3f, y0 - 4.5f)), steel);
                }
                p.Fill(Capsule(24, 41, 56, 47, 0.9f), new Color(1f, 1f, 1f, 0.5f));
                p.Fill(RR(4, 24, 19, 26, 7), VGrad(24, 50, C(0x8A, 0x55, 0x2B), C(0xC8, 0x8A, 0x4C)));  // рукоять
                p.Fill(Circle(13.5f, 37, 4.2f), new Color(0.04f, 0.07f, 0.12f, 0.85f));
                break;
            }

            case UiIcon.Target:
                p.Fill(Circle(32, 32, 27), C(0xE2, 0x48, 0x3A));
                p.Fill(Circle(32, 32, 20), Color.white);
                p.Fill(Circle(32, 32, 13), C(0xE2, 0x48, 0x3A));
                p.Fill(Circle(32, 32, 6), Color.white);
                p.Fill(Capsule(32, 32, 54, 54, 1.9f), C(0xC9, 0x9C, 0x62));                            // стрела
                p.Fill(Poly(V(47, 47), V(61, 49), V(59, 60)), Color.white);                            // оперение
                p.Fill(Poly(V(47, 47), V(49, 61), V(59, 60)), C(0xE8, 0xE8, 0xE8));
                break;

            case UiIcon.Cross:
                p.Fill(Capsule(18, 18, 46, 46, 5f), Navy);
                p.Fill(Capsule(18, 46, 46, 18, 5f), Navy);
                break;

            case UiIcon.Clock:
                p.Fill(Circle(32, 32, 27), VGrad(5, 59, BrassLo, BrassHi));
                p.Fill(Circle(32, 32, 21.5f), C(0xFF, 0xF1, 0xCC));
                p.Fill(Capsule(32, 49, 32, 52, 1.3f), Navy);
                p.Fill(Capsule(32, 12, 32, 15, 1.3f), Navy);
                p.Fill(Capsule(12, 32, 15, 32, 1.3f), Navy);
                p.Fill(Capsule(49, 32, 52, 32, 1.3f), Navy);
                p.Fill(Capsule(32, 32, 32, 45, 2.2f), Navy);
                p.Fill(Capsule(32, 32, 42, 27, 2.2f), Navy);
                p.Fill(Circle(32, 32, 3.2f), BrassLo);
                break;

            case UiIcon.Sawmill:
            {
                Shade steel = VGrad(8, 32, C(0x8E, 0x9E, 0xB0), C(0xE0, 0xE8, 0xF0));
                p.Fill(RR(10, 6, 44, 30, 3), VGrad(6, 36, C(0x9A, 0x62, 0x34), C(0xC9, 0x8A, 0x4B)));      // стены
                p.Fill(RR(14, 9, 4f, 24f, 1.5f), new Color(0f, 0f, 0f, 0.2f));                              // столб
                for (int i = 0; i < 10; i++)                                                                // зубья диска
                {
                    float a = i * Mathf.PI * 0.2f;
                    p.Fill(Poly(V(40f + Mathf.Cos(a - 0.2f) * 11f, 20f + Mathf.Sin(a - 0.2f) * 11f),
                                V(40f + Mathf.Cos(a + 0.05f) * 15.5f, 20f + Mathf.Sin(a + 0.05f) * 15.5f),
                                V(40f + Mathf.Cos(a + 0.2f) * 11f, 20f + Mathf.Sin(a + 0.2f) * 11f)), steel);
                }
                p.Fill(Circle(40, 20, 12), steel);
                p.Fill(Circle(40, 20, 7.5f), C(0x5C, 0x6B, 0x7C));
                p.Fill(Circle(40, 20, 2.6f), C(0xE0, 0xE8, 0xF0));
                p.Fill(Poly(V(2, 35), V(32, 62), V(62, 35)), VGrad(35, 62, C(0xB8, 0x34, 0x24), C(0xEE, 0x5A, 0x3C)));   // крыша
                p.Fill(Capsule(14, 38.5f, 30, 53, 1.3f), new Color(1f, 1f, 1f, 0.28f));
                p.Fill(Circle(14, 13, 7.5f), C(0xD2, 0xA6, 0x70));                                          // торцы брёвен
                p.Fill(Ring(Circle(14, 13, 4.2f), 1.1f), C(0xB9, 0x84, 0x55));
                p.Fill(Circle(25, 10, 6.5f), C(0xE0, 0xB6, 0x80));
                p.Fill(Ring(Circle(25, 10, 3.5f), 1.1f), C(0xB9, 0x84, 0x55));
                break;
            }

            case UiIcon.ArrowRight:
                p.Fill(Poly(V(6, 23), V(34, 23), V(34, 11), V(59, 32), V(34, 53), V(34, 41), V(6, 41)), VGrad(11, 53, BrassLo, BrassHi));
                p.Fill(Capsule(10, 36, 32, 36, 1.3f), new Color(1f, 1f, 1f, 0.35f));
                break;

            case UiIcon.ArrowUp:
                p.Fill(Poly(V(32, 57), V(55, 31), V(41, 31), V(41, 8), V(23, 8), V(23, 31), V(9, 31)), Navy);
                break;

            case UiIcon.Play:
                p.Fill(Poly(V(16, 8), V(16, 56), V(56, 32)), C(0x1E, 0x5A, 0x2A));
                break;

            case UiIcon.Box:
                p.Fill(Poly(V(8, 44), V(32, 32), V(32, 6), V(8, 18)), C(0xC9, 0x9A, 0x62));                 // левая грань
                p.Fill(Poly(V(56, 44), V(32, 32), V(32, 6), V(56, 18)), C(0xA6, 0x78, 0x46));               // правая грань
                p.Fill(Poly(V(32, 57), V(56, 44), V(32, 32), V(8, 44)), C(0xEA, 0xCA, 0x96));               // крышка
                p.Fill(Capsule(32, 56, 32, 33, 1.2f), new Color(0.4f, 0.25f, 0.1f, 0.28f));
                break;

            case UiIcon.Paw:   // лапка питомца: заглушка, когда у PetData нет своей иконки
            {
                Shade fur = VGrad(6, 58, C(0xE2, 0xB8, 0x82), C(0xFF, 0xF1, 0xD6));
                Color bean = C(0xE8, 0x8F, 0x86);
                Color beanHi = C(0xFF, 0xC4, 0xBC);
                p.Fill(Union(Ellipse(32, 21, 17f, 13.5f), Ellipse(32, 29, 12f, 10f)), fur);          // большая подушечка
                p.Fill(Ellipse(12.5f, 37.5f, 6.4f, 8.6f), fur);                                       // пальцы
                p.Fill(Ellipse(23.5f, 48.5f, 6.8f, 8.8f), fur);
                p.Fill(Ellipse(40.5f, 48.5f, 6.8f, 8.8f), fur);
                p.Fill(Ellipse(51.5f, 37.5f, 6.4f, 8.6f), fur);
                p.Fill(Union(Ellipse(32, 20, 9.5f, 7.2f), Ellipse(32, 25.5f, 6.5f, 5f)), bean);       // розовые бобы
                p.Fill(Ellipse(12.8f, 36.5f, 3f, 4.2f), bean);
                p.Fill(Ellipse(23.8f, 47.5f, 3.2f, 4.4f), bean);
                p.Fill(Ellipse(40.2f, 47.5f, 3.2f, 4.4f), bean);
                p.Fill(Ellipse(51.2f, 36.5f, 3f, 4.2f), bean);
                p.Fill(Ellipse(29.5f, 23.5f, 3.4f, 1.8f), beanHi);                                    // блики
                p.Fill(Ellipse(22.5f, 50.5f, 1.4f, 2f), beanHi);
                p.Fill(Ellipse(39.5f, 50.5f, 1.4f, 2f), beanHi);
                break;
            }

            case UiIcon.Trophy:   // кубок: ручки, чаша, ножка, основание
            {
                Shade gold = VGrad(6, 58, C(0xC9, 0x84, 0x14), C(0xFF, 0xDB, 0x5E));
                p.Fill(Ring(Circle(13, 41, 9), 3.4f), gold);
                p.Fill(Ring(Circle(51, 41, 9), 3.4f), gold);
                p.Fill(Poly(V(13, 58), V(51, 58), V(46, 35), V(32, 25), V(18, 35)), gold);
                p.Fill(Poly(V(51, 58), V(46, 35), V(32, 25), V(38, 58)), new Color(0f, 0f, 0f, 0.10f));     // тень справа
                p.Fill(Capsule(20, 52, 21, 38, 1.8f), new Color(1f, 1f, 1f, 0.5f));                          // блик
                p.Fill(Ring(Circle(32, 44, 7.5f), 1.8f), C(0xB8, 0x74, 0x1A, 200));                         // медальон на чаше
                p.Fill(Circle(32, 44, 3.4f), C(0xFF, 0xF1, 0xB0));
                p.Fill(RR(27, 15, 10, 12, 1), gold);
                p.Fill(RR(17, 5, 30, 11, 3.5f), VGrad(5, 16, C(0x8A, 0x5A, 0x34), C(0xC9, 0x8A, 0x4B)));      // деревянная подставка
                p.Fill(RR(21, 12.5f, 22, 2f, 1f), new Color(1f, 1f, 1f, 0.3f));
                break;
            }

            case UiIcon.Tree:     // ёлка в три яруса
                p.Fill(RR(27.5f, 5, 9, 16, 1.5f), VGrad(5, 21, C(0x6B, 0x3E, 0x1E), C(0x9C, 0x5F, 0x30)));
                p.Fill(Poly(V(8, 17), V(56, 17), V(32, 38)), VGrad(17, 38, C(0x2C, 0x70, 0x38), C(0x4F, 0xA8, 0x55)));
                p.Fill(Poly(V(12, 31), V(52, 31), V(32, 50)), VGrad(31, 50, C(0x35, 0x80, 0x3F), C(0x62, 0xBE, 0x66)));
                p.Fill(Poly(V(17, 43), V(47, 43), V(32, 61)), VGrad(43, 61, C(0x3F, 0x90, 0x48), C(0x78, 0xD2, 0x78)));
                p.Fill(Poly(V(32, 61), V(47, 43), V(32, 43)), new Color(0f, 0f, 0f, 0.13f));
                p.Fill(Poly(V(32, 50), V(52, 31), V(32, 31)), new Color(0f, 0f, 0f, 0.10f));
                p.Fill(Capsule(24, 40, 29, 49, 1.1f), new Color(1f, 1f, 1f, 0.28f));
                break;

            case UiIcon.Hut:      // хижина с красной крышей
                p.Fill(RR(43, 38, 7, 16, 1.2f), C(0x8C, 0x8F, 0x99));                                       // труба
                p.Fill(RR(11, 7, 42, 30, 3), Wood(7, 37, C(0xB0, 0x78, 0x40), C(0xE0, 0xAE, 0x6E), 3f));
                p.Fill(Poly(V(4, 33), V(60, 33), V(32, 58)), VGrad(33, 58, C(0x9A, 0x35, 0x2A), C(0xE0, 0x6E, 0x48)));
                p.Fill(Poly(V(32, 58), V(60, 33), V(32, 33)), new Color(0f, 0f, 0f, 0.14f));
                p.Fill(RR(26, 7, 12, 19, 3), C(0x4A, 0x2B, 0x14));                                          // дверь
                p.Fill(Circle(35, 16, 1.2f), BrassHi);
                p.Fill(RR(15, 19, 8, 8, 1.5f), C(0xFF, 0xE9, 0x9A));                                        // окошко
                p.Fill(Capsule(19, 19, 19, 27, 0.7f), C(0x6B, 0x3E, 0x1E));
                p.Fill(Capsule(15, 23, 23, 23, 0.7f), C(0x6B, 0x3E, 0x1E));
                break;

            case UiIcon.Stall:    // торговая лавка с полосатым навесом
            {
                Color red = C(0xD9, 0x4A, 0x3A);
                p.Fill(RR(10, 6, 44, 24, 3), Wood(6, 30, C(0x9C, 0x62, 0x30), C(0xD0, 0x98, 0x58), 7f));
                p.Fill(Circle(20, 34, 4f), C(0xE2, 0x48, 0x3A));                                             // товары
                p.Fill(Circle(32, 34, 4.4f), VGrad(30, 39, C(0xE0, 0xA0, 0x20), C(0xFF, 0xE0, 0x70)));
                p.Fill(Circle(44, 34, 4f), C(0x6D, 0xBE, 0x6A));
                p.Fill(Poly(V(10, 58), V(54, 58), V(58, 38), V(6, 38)), red);
                for (int i = 1; i < 6; i += 2)
                {
                    float f0 = i / 6f, f1 = (i + 1) / 6f;
                    p.Fill(Poly(V(10 + 44 * f0, 58), V(10 + 44 * f1, 58), V(6 + 52 * f1, 38), V(6 + 52 * f0, 38)), C(0xFF, 0xF4, 0xDC));
                }
                for (int i = 0; i < 6; i++)
                    p.Fill(Circle(6 + 52 * (i + 0.5f) / 6f, 38, 4.4f), (i % 2 == 0) ? red : C(0xFF, 0xF4, 0xDC));
                p.Fill(Capsule(14, 54, 50, 54, 1f), new Color(1f, 1f, 1f, 0.3f));
                break;
            }

            case UiIcon.Bridge:   // подвесной мост над рекой
            {
                p.Fill(RR(0, 4, 64, 9, 4), VGrad(4, 13, C(0x2E, 0x78, 0xB4), C(0x7C, 0xC9, 0xF2)));
                p.Fill(RR(3, 14, 6, 38, 2), Wood(14, 52, C(0x8A, 0x5A, 0x34), C(0xC0, 0x88, 0x50), 2f));
                p.Fill(RR(55, 14, 6, 38, 2), Wood(14, 52, C(0x8A, 0x5A, 0x34), C(0xC0, 0x88, 0x50), 4f));
                float[] bx = { 8f, 20f, 32f, 44f, 56f }, by = { 50f, 40f, 36f, 40f, 50f };
                for (int i = 0; i < 4; i++) p.Fill(Capsule(bx[i], by[i], bx[i + 1], by[i + 1], 1.5f), C(0xE8, 0xC8, 0x8F));
                for (int i = 1; i < 4; i++) p.Fill(Capsule(bx[i], by[i], bx[i], 24, 0.9f), C(0xE8, 0xC8, 0x8F));
                p.Fill(RR(4, 18, 56, 7, 2.5f), Wood(18, 25, C(0xB0, 0x78, 0x40), C(0xE0, 0xAE, 0x6E), 9f));
                for (int i = 0; i < 6; i++) p.Fill(Capsule(11 + i * 8.4f, 19, 11 + i * 8.4f, 24, 0.5f), new Color(0.3f, 0.17f, 0.06f, 0.5f));
                break;
            }

            case UiIcon.Scroll:   // записка-свиток с печатью
                p.Fill(RR(14, 8, 36, 48, 4), VGrad(8, 56, C(0xE9, 0xD2, 0x9E), C(0xFF, 0xF1, 0xCC)));
                p.Fill(Capsule(12, 54, 52, 54, 5.5f), VGrad(48, 60, C(0xC9, 0x9A, 0x62), C(0xF0, 0xD4, 0x9C)));
                p.Fill(Capsule(12, 10, 52, 10, 5.5f), VGrad(4, 16, C(0xB8, 0x86, 0x50), C(0xE8, 0xC8, 0x8F)));
                p.Fill(Capsule(21, 45, 43, 45, 1.2f), new Color(0.3f, 0.17f, 0.06f, 0.55f));
                p.Fill(Capsule(21, 39, 43, 39, 1.2f), new Color(0.3f, 0.17f, 0.06f, 0.55f));
                p.Fill(Capsule(21, 33, 37, 33, 1.2f), new Color(0.3f, 0.17f, 0.06f, 0.55f));
                p.Fill(Circle(40, 22, 6f), VGrad(16, 28, C(0xA8, 0x24, 0x1C), C(0xE2, 0x48, 0x3A)));
                p.Fill(Ring(Circle(40, 22, 3.4f), 1f), new Color(1f, 1f, 1f, 0.5f));
                break;

            case UiIcon.Person:   // рабочий в каске
                p.Fill(RR(15, 5, 34, 28, 11), VGrad(5, 33, C(0x2E, 0x7D, 0x9E), C(0x58, 0xB4, 0xD2)));
                p.Fill(Capsule(32, 10, 32, 28, 1f), new Color(0f, 0f, 0f, 0.18f));
                p.Fill(Circle(32, 44, 11f), VGrad(33, 55, C(0xE0, 0xA8, 0x78), C(0xFF, 0xD9, 0xB0)));
                p.Fill(Circle(27.5f, 43, 1.5f), Ink);
                p.Fill(Circle(36.5f, 43, 1.5f), Ink);
                p.Fill(Capsule(29.5f, 38, 34.5f, 38, 0.9f), C(0xB8, 0x66, 0x4A));
                p.Fill(ClipMin(Circle(32, 46, 13.5f), 46f), VGrad(46, 60, C(0xE0, 0x9A, 0x1C), C(0xFF, 0xD0, 0x4A)));   // каска
                p.Fill(RR(16, 45, 32, 4f, 2f), C(0xC9, 0x80, 0x14));
                p.Fill(Capsule(26, 55, 30, 58, 1f), new Color(1f, 1f, 1f, 0.45f));
                break;

            case UiIcon.Hammer:   // молоток улучшений
                p.Fill(Capsule(14, 10, 43, 42, 3.8f), VGrad(10, 42, C(0x8A, 0x55, 0x2B), C(0xC8, 0x8A, 0x4C)));
                p.Fill(Poly(V(36.5f, 58.3f), V(28.7f, 49.2f), V(51.5f, 29.7f), V(59.3f, 38.8f)), VGrad(30, 58, C(0x7F, 0x90, 0xA3), C(0xDC, 0xE6, 0xF0)));
                p.Fill(Poly(V(36.5f, 58.3f), V(59.3f, 38.8f), V(55f, 34f), V(33f, 54f)), new Color(1f, 1f, 1f, 0.28f));
                p.Fill(Capsule(32, 51, 52, 33, 0.8f), new Color(0f, 0f, 0f, 0.15f));
                break;

            case UiIcon.Book:     // книга с закладкой
                p.Fill(RR(15, 6, 41, 48, 3), VGrad(6, 54, C(0xE0, 0xCC, 0xA0), C(0xFF, 0xF1, 0xCC)));   // страницы
                p.Fill(RR(9, 10, 42, 48, 4), VGrad(10, 58, C(0x2A, 0x58, 0x98), C(0x56, 0x94, 0xD8)));   // обложка
                p.Fill(Capsule(14, 14, 14, 54, 1.4f), new Color(0f, 0f, 0f, 0.2f));                     // корешок
                p.Fill(Ring(Circle(32, 34, 9.5f), 1.8f), C(0xFF, 0xD6, 0x6B));                          // тиснение
                p.Fill(Circle(32, 34, 4f), C(0xFF, 0xD6, 0x6B));
                p.Fill(Poly(V(38, 58), V(46, 58), V(46, 42), V(42, 46), V(38, 42)), C(0xE2, 0x48, 0x3A));   // закладка
                p.Fill(Capsule(20, 52, 40, 52, 1f), new Color(1f, 1f, 1f, 0.28f));
                break;

            case UiIcon.Bolt:     // молния: навыки
                p.Fill(Poly(V(38, 61), V(15, 31), V(29, 31), V(24, 3), V(50, 39), V(36, 39), V(47, 61)), VGrad(3, 61, C(0xE0, 0x9A, 0x14), C(0xFF, 0xE8, 0x70)));
                p.Fill(Poly(V(38, 61), V(15, 31), V(22, 31), V(42, 58)), new Color(1f, 1f, 1f, 0.35f));
                break;

            case UiIcon.Gem:   // огранённый камень, почти белый: красится через Image.color (цвет редкости)
            {
                Vector2 gl = V(4, 38), gr = V(60, 38), gtl = V(16, 53), gtr = V(48, 53), gb = V(32, 5);
                p.Fill(Poly(gl, gtl, gtr, gr, gb), VGrad(5, 53, new Color(0.62f, 0.62f, 0.68f, 1f), Color.white));
                p.Fill(Poly(gl, gr, gb), VGrad(5, 38, new Color(0.58f, 0.58f, 0.64f, 1f), new Color(0.9f, 0.9f, 0.95f, 1f)));
                // грани короны
                p.Fill(Poly(V(4, 38), V(16, 53), V(24, 38)), new Color(1f, 1f, 1f, 0.30f));
                p.Fill(Poly(V(16, 53), V(48, 53), V(40, 38), V(24, 38)), new Color(1f, 1f, 1f, 0.72f));
                p.Fill(Poly(V(48, 53), V(60, 38), V(40, 38)), new Color(0f, 0f, 0f, 0.12f));
                // грани павильона
                p.Fill(Poly(V(4, 38), V(24, 38), V(32, 5)), new Color(1f, 1f, 1f, 0.20f));
                p.Fill(Poly(V(24, 38), V(40, 38), V(32, 5)), new Color(1f, 1f, 1f, 0.50f));
                p.Fill(Poly(V(40, 38), V(60, 38), V(32, 5)), new Color(0f, 0f, 0f, 0.16f));
                p.Fill(Capsule(5, 38, 59, 38, 0.6f), new Color(0f, 0f, 0f, 0.14f));
                // искорка-блик
                p.Fill(Capsule(21, 47, 21, 55, 0.9f), new Color(1f, 1f, 1f, 0.95f));
                p.Fill(Capsule(17, 51, 25, 51, 0.9f), new Color(1f, 1f, 1f, 0.95f));
                p.Fill(Circle(21, 51, 1.6f), Color.white);
                break;
            }
        }
    }
}

// Assets/_Project/Scripts/UI/PetUi.cs (НЕ в Editor)
// Общие помощники UI питомцев: цвета редкости, иконки статов, нарисованные кодом спрайты (свечение, искра, лучи, затухание),
// и PetCardView: карточка питомца в стиле UiKit. Используется окном клети (PetCrateWindow), дальше пригодится окну коллекции.
//
// Карточка: латунная рамка, плашка цвета редкости, иконка питомца (нет иконки: лапка), медальон со статом слева сверху,
// камень редкости справа сверху, имя в тёмной полосе снизу, при withStars ещё звёзды редкости над полосой.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PetUi
{
    // ============================================================ цвета
    public static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    public static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    // плашка карточки (темнее): серый, зелёный, синий, золотой
    public static Color RarityFill(PetRarity r)
    {
        switch (r)
        {
            case PetRarity.Common: return new Color32(0x6B, 0x75, 0x84, 255);
            case PetRarity.Uncommon: return new Color32(0x3F, 0x8F, 0x47, 255);
            case PetRarity.Rare: return new Color32(0x2F, 0x78, 0xB8, 255);
            default: return new Color32(0xC9, 0x8A, 0x14, 255);
        }
    }

    // акцент для текста, вспышки и камней (ярче)
    public static Color RarityAccent(PetRarity r)
    {
        switch (r)
        {
            case PetRarity.Common: return new Color32(0xB5, 0xBD, 0xC9, 255);
            case PetRarity.Uncommon: return new Color32(0x6D, 0xBE, 0x6A, 255);
            case PetRarity.Rare: return new Color32(0x4D, 0xA9, 0xF0, 255);
            default: return new Color32(0xFF, 0xC8, 0x3D, 255);
        }
    }

    // цвет клети: ореол вокруг ящика и искры на экране подготовки
    public static Color CrateAccent(CrateType c)
    {
        switch (c)
        {
            case CrateType.Forest: return new Color32(0x6D, 0xBE, 0x6A, 255);
            case CrateType.River: return new Color32(0x4D, 0xA9, 0xF0, 255);
            default: return new Color32(0xB4, 0x9C, 0xFF, 255);
        }
    }

    public static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

    // ============================================================ иконка стата (значки из UiKit)
    public static UiIcon StatIcon(PetStat s)
    {
        switch (s)
        {
            case PetStat.ChopPower: return UiIcon.Axe;
            case PetStat.MoveSpeed: return UiIcon.Boot;
            case PetStat.Capacity: return UiIcon.Backpack;
            case PetStat.DoubleYield: return UiIcon.Log;
            case PetStat.Haggle: return UiIcon.Coin;
            case PetStat.SawSpeed: return UiIcon.Saw;
            default: return UiIcon.Tent;      // доход рабочих
        }
    }

    // ============================================================ тексты
    public static string NameOf(PetData d)
    {
        if (d == null) return "";
        return string.IsNullOrEmpty(d.displayName) ? d.id : d.displayName;
    }

    static readonly List<PetData> tmpList = new List<PetData>(1);

    static readonly List<int> tmpStars = new List<int>(1);

    // доля -> проценты без лишних нулей: 0.03 -> «3», 0.0345 -> «3.5»
    public static string Pct(float fraction)
    {
        return (fraction * 100f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    // «+8%» или «+2 бревна»; stars: звёзды слияния (усиливают бонус)
    public static string BonusText(PetData d, int stars = 0)
    {
        if (d.stat == PetStat.Capacity)
        {
            tmpList.Clear(); tmpStars.Clear();
            tmpList.Add(d); tmpStars.Add(stars);
            int logs = PetBonus.Compute(tmpList, tmpStars).capacityLogs;       // рюкзак считается в брёвнах, а не в процентах
            tmpList.Clear(); tmpStars.Clear();
            return "+" + logs + " " + UiDraw.Plural(logs, "бревно", "бревна", "бревен");
        }
        return "+" + Pct(GameBalance.PetBonus(d.rarity, stars)) + "%";
    }

    // «Скорость рубки +8%» с подсветкой бонуса
    public static string StatLine(PetData d, int stars = 0)
    {
        return "<b>" + PetText.Stat(d.stat) + "</b>  <color=#" + Hex(UiKit.Good) + ">" + BonusText(d, stars) + "</color>";
    }

    // ============================================================ спрайты, нарисованные кодом (один раз, красятся через Image.color)
    static Sprite glow, spark, rays, edgeFade;

    // мягкое круглое свечение
    public static Sprite GlowSprite
    {
        get
        {
            if (glow == null)
                glow = MakeTex("Pet_Glow", 64, 64, (u, v) =>
                {
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    return a * a;
                });
            return glow;
        }
    }

    // четырёхлучевая искра с яркой точкой в центре
    public static Sprite SparkSprite
    {
        get
        {
            if (spark == null)
                spark = MakeTex("Pet_Spark", 64, 64, (u, v) =>
                {
                    float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                    float star = Mathf.Clamp01((1f - (Mathf.Pow(ax, 0.55f) + Mathf.Pow(ay, 0.55f))) * 2.2f);
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 2.2f);
                    return Mathf.Max(star, core * core * 0.9f);
                });
            return spark;
        }
    }

    // «солнечные лучи»: 12 мягких клиньев, гаснут к краю. Крутится за карточкой редкого питомца
    public static Sprite RaysSprite
    {
        get
        {
            if (rays == null)
                rays = MakeTex("Pet_Rays", 256, 256, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    if (r >= 1f) return 0f;
                    float ang = Mathf.Atan2(v, u);
                    float ray = Mathf.Clamp01(Mathf.Cos(ang * 12f) * 1.5f);
                    float fall = (1f - r) * (1f - r) * Mathf.Clamp01(r / 0.12f);
                    return ray * fall;
                });
            return rays;
        }
    }

    // горизонтальное затухание: слева непрозрачно, справа прозрачно (края ленты рулетки)
    public static Sprite EdgeFadeSprite
    {
        get
        {
            if (edgeFade == null)
                edgeFade = MakeTex("Pet_EdgeFade", 32, 2, (u, v) => Mathf.Pow(1f - (u * 0.5f + 0.5f), 1.5f));
            return edgeFade;
        }
    }

    // белая текстура с альфой из функции alpha(u, v), где u и v от -1 до 1
    static Sprite MakeTex(string name, int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Clamp01(alpha(u, v));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        return s;
    }

    // ============================================================ мелкие помощники для сборки
    public static void AutoSize(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.enableWordWrapping = false;
    }

    public static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}

// Карточка питомца. Create один раз, Set(данные) сколько угодно раз (ленту рулетки не пересобираем).
public class PetCardView
{
    const float Inset = 7f;     // толщина рамки Slot, под ней плашка
    static readonly Color Clear0 = new Color(0f, 0f, 0f, 0f);

    public RectTransform rt;
    public Image frame, fill, shine, icon, badge, badgeIcon, gem, strip, topBar, dim;
    public Image[] stars;
    public TMP_Text nameLabel;

    public int LiveStage = 2;            // стадия роста для живой модели (0 малыш, 1 подросток, 2 взрослый), ставить до Set
    public bool pendingPortrait;         // 3D-снимок ещё не готов: окно перерисует карточку позже

    // дополнительные элементы создаются лениво (ленте рулетки они не нужны)
    Image newBadge, newBg;
    TMP_Text newLabel;
    Image[] mergeStars;

    float w, h;
    PetLiveView live;     // живая вращающаяся модель (только если вызван EnableLive)
    public PetLiveView Live { get { return live; } }     // нужен окну коллекции: погладить (Hop) и покрутить (Drag)

    public static PetCardView Create(Transform parent, float w, float h, string name, bool withStars)
    {
        var c = new PetCardView();
        c.w = w; c.h = h;
        var mid = new Vector2(0.5f, 0.5f);

        c.frame = UiKit.MakeImage(parent, name, UiKit.Slot, true, 1f);
        c.rt = c.frame.rectTransform;
        c.rt.sizeDelta = new Vector2(w, h);

        c.fill = UiKit.MakeImage(c.rt, "Fill", null, false);
        c.fill.rectTransform.anchorMin = Vector2.zero;
        c.fill.rectTransform.anchorMax = Vector2.one;
        c.fill.rectTransform.offsetMin = new Vector2(Inset, Inset);
        c.fill.rectTransform.offsetMax = new Vector2(-Inset, -Inset);

        // цветная полоса редкости сверху плашки
        c.topBar = UiKit.MakeImage(c.rt, "TopBar", null, false);
        var tb = c.topBar.rectTransform;
        tb.anchorMin = new Vector2(0f, 1f);
        tb.anchorMax = new Vector2(1f, 1f);
        tb.pivot = new Vector2(0.5f, 1f);
        tb.offsetMin = new Vector2(Inset, -Inset - h * 0.035f);
        tb.offsetMax = new Vector2(-Inset, -Inset);

        // светлое пятно за иконкой, чтобы плашка не была плоской
        c.shine = UiKit.MakeImage(c.rt, "Shine", PetUi.GlowSprite, false);
        c.shine.color = new Color(1f, 1f, 1f, 0.30f);
        PetUi.Anchor(c.shine.rectTransform, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(w * 1.0f, w * 1.0f));

        c.icon = UiKit.MakeImage(c.rt, "Icon", null, false);
        c.icon.preserveAspect = true;
        PetUi.Anchor(c.icon.rectTransform, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(w * 0.58f, w * 0.58f));

        // затемнение: Обычные чуть притушены, чтобы редкие бросались в глаза
        c.dim = UiKit.MakeImage(c.rt, "Dim", null, false);
        c.dim.rectTransform.anchorMin = Vector2.zero;
        c.dim.rectTransform.anchorMax = Vector2.one;
        c.dim.rectTransform.offsetMin = new Vector2(Inset, Inset);
        c.dim.rectTransform.offsetMax = new Vector2(-Inset, -Inset);
        c.dim.color = Clear0;

        // медальон со статом (слева сверху)
        float bs = w * 0.27f, off = Inset + bs * 0.5f + 3f;
        c.badge = UiKit.MakeImage(c.rt, "StatBadge", UiKit.Medallion, false);
        PetUi.Anchor(c.badge.rectTransform, new Vector2(0f, 1f), mid, new Vector2(off, -off), new Vector2(bs, bs));
        c.badgeIcon = UiKit.MakeImage(c.badge.rectTransform, "StatIcon", null, false);
        c.badgeIcon.preserveAspect = true;
        PetUi.Anchor(c.badgeIcon.rectTransform, mid, mid, Vector2.zero, new Vector2(bs * 0.62f, bs * 0.62f));

        // камень редкости (справа сверху)
        float gs = w * 0.21f, goff = Inset + gs * 0.5f + 3f;
        c.gem = UiKit.MakeImage(c.rt, "Gem", UiKit.Icon(UiIcon.Gem), false);
        c.gem.preserveAspect = true;
        PetUi.Anchor(c.gem.rectTransform, new Vector2(1f, 1f), mid, new Vector2(-goff, -goff), new Vector2(gs, gs));

        // тёмная полоса с именем
        float sh = h * 0.2f;
        c.strip = UiKit.MakeImage(c.rt, "Strip", null, false);
        c.strip.color = new Color(0f, 0f, 0f, 0.45f);
        var sr = c.strip.rectTransform;
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(Inset, Inset);
        sr.offsetMax = new Vector2(-Inset, Inset + sh);
        c.nameLabel = UiKit.MakeLabel(c.strip.transform, "", h * 0.11f, Color.white, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(c.nameLabel, 10f, h * 0.11f);

        // звёзды редкости над полосой (1..4)
        if (withStars)
        {
            c.stars = new Image[4];
            float ss = h * 0.085f;
            for (int i = 0; i < 4; i++)
            {
                var st = UiKit.MakeImage(c.rt, "Star" + i, UiKit.Icon(UiIcon.Star), false);
                st.preserveAspect = true;
                PetUi.Anchor(st.rectTransform, new Vector2(0.5f, 0f), mid, Vector2.zero, new Vector2(ss, ss));
                c.stars[i] = st;
            }
        }
        return c;
    }

    // Показывать питомца живой вращающейся 3D-моделью вместо снимка (для больших карточек: результат клети, окно подробностей).
    // Звать один раз после Create. Остальные карточки получают 3D-снимок (PetPortrait) без лишней нагрузки.
    public void EnableLive()
    {
        if (live != null) return;
        var mid = new Vector2(0.5f, 0.5f);
        live = PetLiveView.Create(rt, "Live");
        PetUi.Anchor(live.rect, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(w * 0.92f, w * 0.92f));
        live.transform.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1);   // над иконкой, под затемнением, медальоном и именем
    }

    void EnsureExtras()
    {
        if (newBadge != null) return;
        var mid = new Vector2(0.5f, 0.5f);

        // красная плашка NEW над именем слева
        newBadge = UiKit.MakeImage(rt, "NewBadge", null, false);
        newBadge.color = new Color32(0xE8, 0x4B, 0x3C, 255);
        PetUi.Anchor(newBadge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(Inset + 3f, Inset + h * 0.2f + 4f), new Vector2(w * 0.38f, h * 0.12f));
        newLabel = UiKit.MakeLabel(newBadge.transform, "NEW", h * 0.09f, Color.white, TextAlignmentOptions.Center, true);
        newLabel.rectTransform.anchorMin = Vector2.zero; newLabel.rectTransform.anchorMax = Vector2.one;
        newLabel.rectTransform.offsetMin = newLabel.rectTransform.offsetMax = Vector2.zero;
        newBadge.raycastTarget = false;
        newBadge.gameObject.SetActive(false);

        // звёзды слияния по центру сверху (золотые)
        mergeStars = new Image[GameBalance.PetStarMax];
        float ss = Mathf.Min(h * 0.085f, w * 0.10f);
        for (int i = 0; i < mergeStars.Length; i++)
        {
            var st = UiKit.MakeImage(rt, "MergeStar" + i, UiKit.Icon(UiIcon.Star), false);
            st.preserveAspect = true;
            st.color = new Color32(0xFF, 0xD2, 0x4A, 255);
            st.raycastTarget = false;
            PetUi.Anchor(st.rectTransform, new Vector2(0.5f, 1f), mid,
                new Vector2((i - (mergeStars.Length - 1) * 0.5f) * ss * 1.05f, -(Inset + ss * 0.5f + 4f)), new Vector2(ss, ss));
            st.gameObject.SetActive(false);
            mergeStars[i] = st;
        }
    }

    void SetExtras(int starCount, bool isNew)
    {
        if (starCount <= 0 && !isNew && newBadge == null) return;   // обычной карточке ничего не нужно
        EnsureExtras();
        newBadge.gameObject.SetActive(isNew);
        for (int i = 0; i < mergeStars.Length; i++) mergeStars[i].gameObject.SetActive(i < starCount);
    }

    // Тёмный силуэт (вид из Книги питомцев, который ещё не открыт)
    public void SetLocked(PetData d)
    {
        frame.color = new Color(0.55f, 0.55f, 0.6f, 1f);
        fill.color = new Color(0.13f, 0.14f, 0.17f, 1f);
        topBar.color = new Color(0.25f, 0.27f, 0.32f, 1f);
        dim.color = Clear0;
        shine.color = new Color(1f, 1f, 1f, 0.05f);
        if (live != null) live.Show(null);
        Sprite portrait = d != null ? PetPortrait.Get(d) : null;
        pendingPortrait = portrait == null && d != null && PetPortrait.Enabled && d.prefab != null;
        icon.sprite = portrait != null ? portrait : UiKit.Icon(UiIcon.Paw);
        icon.color = new Color(0.03f, 0.03f, 0.05f, 1f);            // силуэт: рисунок залит чёрным
        icon.enabled = true;
        float isz = w * (portrait != null ? 0.8f : 0.44f);
        icon.rectTransform.sizeDelta = new Vector2(isz, isz);
        badge.gameObject.SetActive(false);
        gem.gameObject.SetActive(false);
        nameLabel.text = "???";
        if (stars != null) for (int i = 0; i < stars.Length; i++) stars[i].gameObject.SetActive(false);
        SetExtras(0, false);
    }

    public void Set(PetData d, int starCount = 0, bool isNew = false)
    {
        badge.gameObject.SetActive(true);
        gem.gameObject.SetActive(true);
        PetRarity r = d.rarity;
        Color acc = PetUi.RarityAccent(r);
        frame.color = Color.Lerp(Color.white, acc, r == PetRarity.Common ? 0.15f : 0.55f);   // рамка слегка окрашена редкостью
        fill.color = PetUi.RarityFill(r);
        topBar.color = acc;
        dim.color = new Color(0f, 0f, 0f, r == PetRarity.Common ? 0.22f : r == PetRarity.Uncommon ? 0.08f : 0f);
        shine.color = new Color(1f, 1f, 1f, 0.22f + 0.12f * (int)r);

        // что рисовать: живая модель, иначе 3D-снимок, иначе иконка PetData.icon, иначе лапка
        bool liveOn = live != null && live.Show(d, LiveStage);
        Sprite pic;
        float k;
        Sprite portrait = liveOn ? null : PetPortrait.Get(d);
        pendingPortrait = !liveOn && portrait == null && PetPortrait.Enabled && d.prefab != null;
        if (portrait != null) { pic = portrait; k = 0.8f; }
        else if (d.icon != null) { pic = d.icon; k = 0.58f; }
        else { pic = UiKit.Icon(UiIcon.Paw); k = 0.44f; }
        icon.sprite = pic;
        icon.color = Color.white;
        icon.enabled = !liveOn;
        float isz = w * k;
        icon.rectTransform.sizeDelta = new Vector2(isz, isz);

        badgeIcon.sprite = UiKit.Icon(PetUi.StatIcon(d.stat));
        gem.color = acc;
        nameLabel.text = PetUi.NameOf(d);
        SetExtras(starCount, isNew);

        if (stars != null)
        {
            int n = (int)r + 1;
            float ss = h * 0.085f, y = Inset + h * 0.2f + ss * 0.5f + 2f;
            for (int i = 0; i < stars.Length; i++)
            {
                bool on = i < n;
                stars[i].gameObject.SetActive(on);
                if (on) stars[i].rectTransform.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * ss * 1.08f, y);
            }
        }
    }
}

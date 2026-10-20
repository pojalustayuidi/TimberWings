// Assets/_Project/Scripts/UI/WorkerUi.cs (НЕ в Editor)
// Общие помощники UI рабочих (дизайн «Система рабочих», задача 5): цвета редкости, иконки ролей, цвета паков,
// и WorkerCardView: карточка рабочего в стиле UiKit (как PetCardView у питомцев).
//
// Цвет НИКОГДА не единственный носитель смысла (GDD 10.4): редкость всегда продублирована
//   - словом («Редкий»), - числом звёзд (Обычный 1 … Мифический 5), - камнем-значком;
// роль продублирована иконкой (монета / топор / пила) и словом.
// Рамка мифика анимированная (мерцает фиолетовым), у остальных редкостей статичная.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class WorkerUi
{
    public static readonly Color Soft = new Color(0.72f, 0.78f, 0.88f, 1f);
    public static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);
    public static readonly Color Bad = new Color32(0xE8, 0x4B, 0x3C, 255);

    // плашка карточки (темнее): серый, зелёный #6DBE6A, синий, янтарный #F2B134, фиолетовый
    public static Color RarityFill(WorkerRarity r)
    {
        switch (r)
        {
            case WorkerRarity.Common: return new Color32(0x6B, 0x75, 0x84, 255);
            case WorkerRarity.Uncommon: return new Color32(0x3F, 0x8F, 0x47, 255);
            case WorkerRarity.Rare: return new Color32(0x2F, 0x78, 0xB8, 255);
            case WorkerRarity.Legendary: return new Color32(0x5E, 0x38, 0xA8, 255);
            default: return new Color32(0x7B, 0x4A, 0x12, 255);
        }
    }

    // акцент для рамки, текста, вспышек и камней (ярче)
    public static Color RarityAccent(WorkerRarity r)
    {
        switch (r)
        {
            case WorkerRarity.Common: return new Color32(0xB5, 0xBD, 0xC9, 255);
            case WorkerRarity.Uncommon: return new Color32(0x6D, 0xBE, 0x6A, 255);
            case WorkerRarity.Rare: return new Color32(0x4D, 0xA9, 0xF0, 255);
            case WorkerRarity.Legendary: return new Color32(0xC7, 0x8C, 0xFF, 255);
            default: return new Color32(0xFF, 0xD5, 0x75, 255);
        }
    }

    public static Color PackColor(WorkerPackType t)
    {
        switch (t)
        {
            case WorkerPackType.Artel: return new Color32(0x6D, 0xBE, 0x6A, 255);
            case WorkerPackType.Workshop: return new Color32(0x4D, 0xA9, 0xF0, 255);
            default: return new Color32(0xB4, 0x8C, 0xFF, 255);
        }
    }

    public static UiIcon RoleIcon(WorkerRole r)
    {
        switch (r)
        {
            case WorkerRole.Clerk: return UiIcon.Coin;
            case WorkerRole.Lumberjack: return UiIcon.Axe;
            default: return UiIcon.Saw;
        }
    }

    // Приказчики, Дровосеки, Пильщики (для подписей рядов коллекции)
    public static string RolePlural(WorkerRole r)
    {
        switch (r)
        {
            case WorkerRole.Clerk: return "Приказчики";
            case WorkerRole.Lumberjack: return "Дровосеки";
            default: return "Пильщики";
        }
    }

    // «НК» для карточки без портрета
    public static string Initials(WorkerDefinition d)
    {
        if (d == null) return "?";
        string a = string.IsNullOrEmpty(d.firstName) ? "" : d.firstName.Substring(0, 1);
        string b = string.IsNullOrEmpty(d.lastName) ? "" : d.lastName.Substring(0, 1);
        string s = (a + b).ToUpperInvariant();
        return s.Length > 0 ? s : "?";
    }

    public static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

    public static string Inv(double v, string fmt) { return v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }

    // «0.5%», «5.5%», «70%»
    public static string OddsText(float pct)
    {
        if (pct <= 0f) return "—";
        return pct.ToString(pct >= 10f || Mathf.Approximately(pct, Mathf.Round(pct)) ? "0.#" : "0.0#", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    // В каких паках можно найти рабочего этой редкости (для карточки, которую ещё не нашли)
    public static string PacksWith(WorkerRarity r)
    {
        string s = "";
        for (int t = 0; t < 3; t++)
        {
            if (WorkerBalance.OddsOf((WorkerPackType)t)[(int)r] <= 0f) continue;
            if (s.Length > 0) s += ", ";
            s += WorkerText.Pack((WorkerPackType)t);
        }
        return s.Length > 0 ? s : "—";
    }

    // Мелкая полоса без UiKit-рамки: тёмная подложка + цветная заливка. fill = anchorMax.x
    public static Image MiniBar(Transform parent, Color back, Color fillColor, out RectTransform fillRt)
    {
        var bg = UiKit.MakeImage(parent, "MiniBar", null, false);
        bg.color = back;
        bg.raycastTarget = false;
        var f = UiKit.MakeImage(bg.transform, "Fill", null, false);
        f.color = fillColor;
        f.raycastTarget = false;
        fillRt = f.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        return bg;
    }

    public static void SetBar(RectTransform fillRt, float k)
    {
        fillRt.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
    }
}

// Мерцание рамки мифической карточки. Висит на рамке, работает только пока включён (карточка мифическая и не выбрана)
public class WorkerMythicShimmer : MonoBehaviour
{
    public Image target;
    static readonly Color A = new Color32(0xD8, 0x9A, 0x36, 255);
    static readonly Color B = new Color32(0xFF, 0xF0, 0xB5, 255);

    void Update()
    {
        if (target == null) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
        target.color = Color.Lerp(A, B, k);
    }
}

// Карточка рабочего. Create один раз, Set(данные) сколько угодно раз.
// Рамка Slot, под ней плашка цвета редкости, портрет (нет портрета: круг с инициалами), медальон роли слева сверху,
// камень редкости справа сверху, звёзды редкости над полосой, полоса снизу: имя, «роль · ур. N» и тонкая полоска опыта.
public class WorkerCardView
{
    const float Inset = 7f;
    static readonly Color Clear0 = new Color(0f, 0f, 0f, 0f);

    public RectTransform rt;
    public Image frame, fill, topBar, shine, portrait, initialsBg, roleBadge, roleIcon, gem, strip, dim;
    public Image[] stars;
    public TMP_Text initials, nameLabel, subLabel;
    public bool showRole = true;          // в подписи «Роль · ур. N» (на узких карточках только «ур. N»)

    Image xpBack, xpFill;
    WorkerMythicShimmer shimmer;
    float w, h;

    public static WorkerCardView Create(Transform parent, float w, float h, string name)
    {
        var c = new WorkerCardView();
        c.w = w; c.h = h;
        var mid = new Vector2(0.5f, 0.5f);

        c.frame = UiKit.MakeImage(parent, name, UiKit.Slot, true, 1f);
        c.rt = c.frame.rectTransform;
        c.rt.sizeDelta = new Vector2(w, h);
        c.shimmer = c.frame.gameObject.AddComponent<WorkerMythicShimmer>();
        c.shimmer.target = c.frame;
        c.shimmer.enabled = false;

        c.fill = UiKit.MakeImage(c.rt, "Fill", null, false);
        Stretch(c.fill.rectTransform, Inset);

        c.topBar = UiKit.MakeImage(c.rt, "TopBar", null, false);
        var tb = c.topBar.rectTransform;
        tb.anchorMin = new Vector2(0f, 1f); tb.anchorMax = new Vector2(1f, 1f); tb.pivot = new Vector2(0.5f, 1f);
        tb.offsetMin = new Vector2(Inset, -Inset - h * 0.035f); tb.offsetMax = new Vector2(-Inset, -Inset);

        c.shine = UiKit.MakeImage(c.rt, "Shine", PetUi.GlowSprite, false);
        c.shine.color = new Color(1f, 1f, 1f, 0.28f);
        PetUi.Anchor(c.shine.rectTransform, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(w, w));

        // круг с инициалами (если нет портрета)
        float ib = w * 0.52f;
        c.initialsBg = UiKit.MakeImage(c.rt, "Initials", UiKit.Medallion, false);
        PetUi.Anchor(c.initialsBg.rectTransform, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(ib, ib));
        c.initials = UiKit.MakeLabel(c.initialsBg.transform, "", ib * 0.42f, UiKit.Navy, TextAlignmentOptions.Center, true);
        PetUi.AutoSize(c.initials, 10f, ib * 0.42f);

        c.portrait = UiKit.MakeImage(c.rt, "Portrait", null, false);
        c.portrait.preserveAspect = true;
        PetUi.Anchor(c.portrait.rectTransform, mid, mid, new Vector2(0f, h * 0.10f), new Vector2(w * 0.8f, w * 0.8f));

        c.dim = UiKit.MakeImage(c.rt, "Dim", null, false);
        Stretch(c.dim.rectTransform, Inset);
        c.dim.color = Clear0;
        c.dim.raycastTarget = false;

        // медальон роли (слева сверху)
        float bs = w * 0.27f, off = Inset + bs * 0.5f + 3f;
        c.roleBadge = UiKit.MakeImage(c.rt, "RoleBadge", UiKit.Medallion, false);
        PetUi.Anchor(c.roleBadge.rectTransform, new Vector2(0f, 1f), mid, new Vector2(off, -off), new Vector2(bs, bs));
        c.roleIcon = UiKit.MakeImage(c.roleBadge.rectTransform, "RoleIcon", null, false);
        c.roleIcon.preserveAspect = true;
        PetUi.Anchor(c.roleIcon.rectTransform, mid, mid, Vector2.zero, new Vector2(bs * 0.62f, bs * 0.62f));

        // камень редкости (справа сверху)
        float gs = w * 0.21f, goff = Inset + gs * 0.5f + 3f;
        c.gem = UiKit.MakeImage(c.rt, "Gem", UiKit.Icon(UiIcon.Gem), false);
        c.gem.preserveAspect = true;
        PetUi.Anchor(c.gem.rectTransform, new Vector2(1f, 1f), mid, new Vector2(-goff, -goff), new Vector2(gs, gs));

        // полоса снизу
        float sh = h * 0.27f;
        c.strip = UiKit.MakeImage(c.rt, "Strip", null, false);
        c.strip.color = new Color(0f, 0f, 0f, 0.5f);
        c.strip.raycastTarget = false;
        var sr = c.strip.rectTransform;
        sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(Inset, Inset); sr.offsetMax = new Vector2(-Inset, Inset + sh);

        c.nameLabel = UiKit.MakeLabel(c.strip.transform, "", h * 0.10f, Color.white, TextAlignmentOptions.Center, true);
        c.nameLabel.rectTransform.anchorMin = new Vector2(0f, 0.44f); c.nameLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        c.nameLabel.rectTransform.offsetMin = new Vector2(3f, 0f); c.nameLabel.rectTransform.offsetMax = new Vector2(-3f, 0f);
        PetUi.AutoSize(c.nameLabel, 9f, h * 0.10f);

        c.subLabel = UiKit.MakeLabel(c.strip.transform, "", h * 0.075f, WorkerUi.Soft, TextAlignmentOptions.Center, false);
        c.subLabel.rectTransform.anchorMin = new Vector2(0f, 0.08f); c.subLabel.rectTransform.anchorMax = new Vector2(1f, 0.46f);
        c.subLabel.rectTransform.offsetMin = new Vector2(3f, 0f); c.subLabel.rectTransform.offsetMax = new Vector2(-3f, 0f);
        PetUi.AutoSize(c.subLabel, 8f, h * 0.075f);

        // тонкая полоска опыта у нижнего края полосы
        c.xpBack = UiKit.MakeImage(c.strip.transform, "XpBack", null, false);
        c.xpBack.color = new Color(0f, 0f, 0f, 0.6f);
        c.xpBack.raycastTarget = false;
        var xb = c.xpBack.rectTransform;
        xb.anchorMin = new Vector2(0f, 0f); xb.anchorMax = new Vector2(1f, 0f); xb.pivot = new Vector2(0.5f, 0f);
        xb.offsetMin = Vector2.zero; xb.offsetMax = new Vector2(0f, Mathf.Max(4f, h * 0.025f));
        c.xpFill = UiKit.MakeImage(c.xpBack.transform, "XpFill", null, false);
        c.xpFill.color = UiKit.Brass;
        c.xpFill.raycastTarget = false;
        var xf = c.xpFill.rectTransform;
        xf.anchorMin = Vector2.zero; xf.anchorMax = new Vector2(0f, 1f); xf.offsetMin = xf.offsetMax = Vector2.zero;

        // звёзды редкости над полосой (1..5)
        c.stars = new Image[5];
        float ss = Mathf.Min(h * 0.075f, w * 0.14f);
        for (int i = 0; i < 5; i++)
        {
            var st = UiKit.MakeImage(c.rt, "Star" + i, UiKit.Icon(UiIcon.Star), false);
            st.preserveAspect = true;
            st.raycastTarget = false;
            PetUi.Anchor(st.rectTransform, new Vector2(0.5f, 0f), mid, Vector2.zero, new Vector2(ss, ss));
            c.stars[i] = st;
        }

        // всё, кроме рамки, не ловит клики: клик идёт в кнопку на рамке
        var gr = c.rt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < gr.Length; i++) if (gr[i] != c.frame) gr[i].raycastTarget = false;
        return c;
    }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    void SetStars(WorkerRarity r)
    {
        int n = (int)r + 1;
        float ss = Mathf.Min(h * 0.075f, w * 0.14f), y = Inset + h * 0.27f + ss * 0.5f + 2f;
        for (int i = 0; i < stars.Length; i++)
        {
            bool on = i < n;
            stars[i].gameObject.SetActive(on);
            if (on) stars[i].rectTransform.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * ss * 1.08f, y);
        }
    }

    // locked: «ещё не найден» (тёмный силуэт, имя скрыто; редкость и роль видны). st: состояние рабочего (уровень, опыт), может быть null
    public void Set(WorkerDefinition d, WorkerState st, bool locked, bool selected = false)
    {
        if (d == null) return;
        WorkerRarity r = d.rarity;
        Color acc = WorkerUi.RarityAccent(r);

        roleBadge.gameObject.SetActive(true);
        roleIcon.sprite = UiKit.Icon(WorkerUi.RoleIcon(d.role));
        gem.gameObject.SetActive(true);
        gem.color = acc;
        SetStars(r);

        bool mythic = r == WorkerRarity.Mythic;
        shimmer.enabled = mythic && !selected;
        if (selected) frame.color = UiKit.BrassHi;
        else if (!mythic) frame.color = locked ? new Color(0.55f, 0.55f, 0.6f, 1f) : Color.Lerp(Color.white, acc, r == WorkerRarity.Common ? 0.15f : 0.55f);
        rt.localScale = selected ? Vector3.one * 1.05f : Vector3.one;

        topBar.color = locked ? new Color(0.25f, 0.27f, 0.32f, 1f) : acc;
        fill.color = locked ? new Color(0.13f, 0.14f, 0.17f, 1f) : WorkerUi.RarityFill(r);
        dim.color = locked ? Clear0 : new Color(0f, 0f, 0f, r == WorkerRarity.Common ? 0.18f : r == WorkerRarity.Uncommon ? 0.06f : 0f);
        shine.color = locked ? new Color(1f, 1f, 1f, 0.035f) :
            new Color(acc.r, acc.g, acc.b, r == WorkerRarity.Mythic ? 0.22f : r == WorkerRarity.Legendary ? 0.16f : 0.08f);
        // Выразительная латунная кромка для мификов, не меняя размеры карточки и hit-area.
        if (mythic) topBar.color = Color.Lerp(acc, new Color32(0xFF, 0xD5, 0x75, 255), 0.42f);

        bool hasPortrait = d.portrait != null;
        portrait.gameObject.SetActive(hasPortrait);
        initialsBg.gameObject.SetActive(!hasPortrait);
        if (hasPortrait)
        {
            portrait.sprite = d.portrait;
            portrait.color = locked ? new Color(0.03f, 0.03f, 0.05f, 1f) : Color.white;
        }
        else
        {
            initialsBg.color = locked ? new Color(0.2f, 0.21f, 0.25f, 1f) : Color.Lerp(Color.white, acc, 0.35f);
            initials.text = locked ? "?" : WorkerUi.Initials(d);
            initials.color = locked ? WorkerUi.Dim : UiKit.Navy;
        }

        if (locked)
        {
            nameLabel.text = "???";
            subLabel.text = showRole ? WorkerText.Role(d.role) + " · " + WorkerText.Rarity(r) : WorkerText.Rarity(r);
            xpBack.gameObject.SetActive(false);
            return;
        }

        nameLabel.text = d.FullName;
        string lvl = st != null ? "ур. " + st.level + (st.IsMax ? " МАКС" : "") : WorkerText.Rarity(r);
        subLabel.text = showRole && st != null ? WorkerText.Role(d.role) + " · " + lvl : (st != null ? lvl : WorkerText.Role(d.role) + " · " + lvl);
        if (st != null)
        {
            xpBack.gameObject.SetActive(true);
            float k = 1f;
            if (!st.IsMax)
            {
                double into = st.totalXp - WorkerBalance.TotalXpForLevel(st.level);
                k = Mathf.Clamp01((float)(into / Mathf.Max(1, WorkerBalance.XpToNext(st.level))));
            }
            xpFill.rectTransform.anchorMax = new Vector2(k, 1f);
        }
        else xpBack.gameObject.SetActive(false);
    }
}

// Assets/_Project/Scripts/UI/Hud/ZoneToast.cs (НЕ в Editor)
// Плашка при входе в зону (Candy): облачное поле, маленькая подпись («НОВАЯ ЗОНА») и крупное название индиго,
// под ними скруглённая линия цвета зоны. Появление на пружине CandyPopIn, на плашке искры.
// Создаёт и показывает HudMinimap:  ZoneToast.Create(canvasRoot, font);  ZoneToast.Show("Кедровый Бор", "НОВАЯ ЗОНА", цвет);
// Параметр font нужен только как запасной: основной шрифт плашки круглый шрифт Candy.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneToast : MonoBehaviour
{
    static ZoneToast inst;

    const float FadeIn = 0.28f, Hold = 2.2f, FadeOut = 0.6f;
    const float LineMax = 380f, LineH = 8f;

    CanvasGroup group;
    RectTransform box, line;
    TextMeshProUGUI capTxt, nameTxt;
    Image fill, lineImg;
    CandyPopIn popIn;
    float t = 99f;

    public static ZoneToast Create(RectTransform root, TMP_FontAsset font)
    {
        if (inst != null) return inst;
        if (root == null) return null;

        var go = new GameObject("ZoneToast", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = 5;
        go.transform.SetParent(root, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -190f);        // под панелью задания
        rt.sizeDelta = new Vector2(700f, 156f);

        inst = go.AddComponent<ZoneToast>();
        inst.Build(rt, font);
        return inst;
    }

    public static void Show(string zoneName, string caption, Color color)
    {
        if (inst != null) inst.Play(zoneName, caption, color);
    }

    void OnDestroy() { if (inst == this) inst = null; }

    RectTransform Child(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    TextMeshProUGUI MakeText(RectTransform r, float size, TMP_FontAsset fallback)
    {
        var tx = r.gameObject.AddComponent<TextMeshProUGUI>();
        var f = UiCandy.RoundFont != null ? UiCandy.RoundFont : fallback;
        if (f != null) tx.font = f;
        tx.fontSize = size;
        tx.fontStyle = FontStyles.Bold;
        tx.alignment = TextAlignmentOptions.Center;
        tx.raycastTarget = false;
        tx.textWrappingMode = TextWrappingModes.NoWrap;
        return tx;   // обводки нет: плашка светлая, текст индиго
    }

    void Build(RectTransform rt, TMP_FontAsset font)
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        box = rt;

        fill = CandyPlaque.Fill(rt, Color.white, 0.55f);
        fill.gameObject.layer = 5;

        var cr = Child("Caption", rt);
        cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = new Vector2(0f, -16f);
        cr.sizeDelta = new Vector2(-48f, 34f);
        capTxt = MakeText(cr, 24f, font);
        capTxt.characterSpacing = 8f;

        var nr = Child("Name", rt);
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.anchoredPosition = new Vector2(0f, -50f);
        nr.sizeDelta = new Vector2(-64f, 66f);
        nameTxt = MakeText(nr, 56f, font);
        nameTxt.enableAutoSizing = true;
        nameTxt.fontSizeMin = 28f;
        nameTxt.fontSizeMax = 56f;

        line = Child("Line", rt);
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = new Vector2(0f, -120f);
        line.sizeDelta = new Vector2(0f, LineH);
        lineImg = line.gameObject.AddComponent<Image>();
        lineImg.sprite = CandyPlaque.Pill;
        lineImg.type = Image.Type.Sliced;
        lineImg.pixelsPerUnitMultiplier = 3f;     // граница 12 пикс. спрайта = 4 пикс. на экране = половина высоты линии: концы круглые
        lineImg.raycastTarget = false;

        // пружинное появление. fade выключен: прозрачностью управляет сам тост. Компонент добавлен после настройки,
        // первый запуск (OnEnable) безвреден: группа прозрачна
        popIn = AddPop(rt);
    }

    static CandyPopIn AddPop(RectTransform rt)
    {
        var p = rt.gameObject.AddComponent<CandyPopIn>();
        p.fade = false;
        p.fromScale = 0.75f;
        return p;
    }

    void Play(string zoneName, string caption, Color color)
    {
        nameTxt.text = zoneName;
        capTxt.text = caption;
        Color c = new Color(color.r, color.g, color.b, 1f);
        capTxt.color = CandyPlaque.Readable(c, 0.45f);   // цвет зоны, затемнённый к индиго: читается на светлом поле
        nameTxt.color = UiCandy.Ink;
        lineImg.color = c;
        fill.color = CandyPlaque.Tint(c, 0.12f);
        t = 0f;

        if (VibeSettings.ReduceEffects)
        {
            if (popIn != null) popIn.enabled = false;
            box.localScale = Vector3.one;
        }
        else
        {
            if (popIn != null) { popIn.enabled = true; popIn.Replay(); }
            CandyFx.Burst(UiCandy.Kit, line, c, 6, 240f);
        }
    }

    void Update()
    {
        if (t > FadeIn + Hold + FadeOut)
        {
            if (group.alpha != 0f) group.alpha = 0f;
            return;
        }

        t += Time.unscaledDeltaTime;

        float a;
        if (t < FadeIn) a = t / FadeIn;
        else if (t < FadeIn + Hold) a = 1f;
        else a = 1f - (t - FadeIn - Hold) / FadeOut;
        a = Mathf.Clamp01(a);

        group.alpha = a;

        // линия «выезжает» из центра
        float w = Mathf.Lerp(0f, LineMax, UiDraw.EaseOutCubic(Mathf.Clamp01(t / 0.5f)));
        line.sizeDelta = new Vector2(w, LineH);
    }
}

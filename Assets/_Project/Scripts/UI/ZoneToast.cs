// Assets/_Project/Scripts/UI/ZoneToast.cs (НЕ в Editor)
// Плашка при входе в зону: маленькая подпись («НОВАЯ ЗОНА») и крупное название, цветная линия под ними.
// Создаёт и показывает HudMinimap:  ZoneToast.Create(canvasRoot, font);  ZoneToast.Show("Кедровый Бор", "НОВАЯ ЗОНА", цвет);
// Добавь этот файл, только если в проекте ещё нет ZoneToast.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ZoneToast : MonoBehaviour
{
    static ZoneToast inst;

    const float FadeIn = 0.28f, Hold = 2.2f, FadeOut = 0.6f;

    CanvasGroup group;
    RectTransform box, line;
    TextMeshProUGUI capTxt, nameTxt;
    Image lineImg;
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
        rt.sizeDelta = new Vector2(760f, 150f);

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

    TextMeshProUGUI MakeText(RectTransform r, float size, TMP_FontAsset font, bool outline)
    {
        var tx = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tx.font = font;
        tx.fontSize = size;
        tx.fontStyle = FontStyles.Bold;
        tx.alignment = TextAlignmentOptions.Center;
        tx.raycastTarget = false;
        tx.textWrappingMode = TextWrappingModes.NoWrap;
        if (outline)
        {
            tx.outlineWidth = 0.22f;
            tx.outlineColor = new Color32(0x0B, 0x10, 0x20, 255);
        }
        return tx;
    }

    void Build(RectTransform rt, TMP_FontAsset font)
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        box = rt;

        var cr = Child("Caption", rt);
        cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = new Vector2(0f, -6f);
        cr.sizeDelta = new Vector2(0f, 34f);
        capTxt = MakeText(cr, 26f, font, true);
        capTxt.characterSpacing = 8f;

        var nr = Child("Name", rt);
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.anchoredPosition = new Vector2(0f, -40f);
        nr.sizeDelta = new Vector2(0f, 72f);
        nameTxt = MakeText(nr, 60f, font, true);
        nameTxt.enableAutoSizing = true;
        nameTxt.fontSizeMin = 30f;
        nameTxt.fontSizeMax = 60f;

        line = Child("Line", rt);
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = new Vector2(0f, -116f);
        line.sizeDelta = new Vector2(0f, 5f);
        lineImg = line.gameObject.AddComponent<Image>();
        lineImg.raycastTarget = false;
    }

    void Play(string zoneName, string caption, Color color)
    {
        nameTxt.text = zoneName;
        capTxt.text = caption;
        Color c = new Color(color.r, color.g, color.b, 1f);
        capTxt.color = Color.Lerp(Color.white, c, 0.55f);
        nameTxt.color = Color.white;
        lineImg.color = c;
        t = 0f;
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
        float pop = UiDraw.EaseOutBack(Mathf.Clamp01(t / (FadeIn * 1.3f)));
        box.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, pop);

        // линия «выезжает» из центра
        float w = Mathf.Lerp(0f, 380f, UiDraw.EaseOutCubic(Mathf.Clamp01(t / 0.5f)));
        line.sizeDelta = new Vector2(w, 5f);
    }
}
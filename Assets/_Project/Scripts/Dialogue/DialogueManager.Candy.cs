// Assets/_Project/Scripts/Dialogue/DialogueManager.Candy.cs (НЕ в Editor)
// Вид реплик в стиле Candy. Логика (очередь, пропуск, озвучка) лежит в DialogueManager.cs, здесь только картинка:
//   BuildUi()        облачный пузырь с голубой «юбкой» и хвостиком, лента-пилюля с именем, субтитры, плашка «Разговор пропущен»
//   ApplySpeaker()   красит ленту под цвет говорящего, подгоняет её ширину и отступы текста, возвращает верхний отступ
//   SoftType()       мягкое появление букв: последние буквы проявляются из прозрачности, а не выскакивают разом
//   NameInk()        цвет имени для светлого фона (цвет говорящего, притемнённый к индиго)
// Всё строится в скине Candy (UiKit.UseSkin), остальные окна от этого не меняются.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class DialogueManager
{
    // ------------------------------------------------------------ размеры
    const float TailH = 22f;
    const float HintH = 26f;
    const float BottomPad = 24f;                 // от низа пузыря (над «юбкой») до подсказки
    const float NamePillH = 36f;
    const float PillOver = NamePillH * 0.5f;     // лента наполовину выступает над верхним краем пузыря
    const int SoftChars = 3;                     // за сколько букв последняя буква проявляется до конца

    // ------------------------------------------------------------ UI
    RectTransform canvasRt, bubbleRt, tailRt, subRt, bodyRt, namePillRt;
    CanvasGroup bubbleGroup, subGroup, hintGroup, holdGroup, noteGroup;
    Image namePill, holdRing;
    TMP_Text nameTxt, bodyTxt, subTxt, hintTxt, noteTxt;
    static Sprite ringSprite;
    bool softDirty;                              // в сетке текста остались прозрачные буквы: надо один раз перестроить

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
        BuildUi();
    }

    void OnDestroy() { if (inst == this) inst = null; }

    // ------------------------------------------------------------ мелочи сборки
    RectTransform Child(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform r, float l, float b, float rt, float tp)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -tp);
    }

    // индиго, без тёмной обводки (на светлом поле она грязнит буквы)
    TMP_Text MakeText(RectTransform r, float size, Color c, TMP_FontAsset font, TextAlignmentOptions al)
    {
        var tx = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tx.font = font;
        tx.fontSize = size;
        tx.color = c;
        tx.fontStyle = FontStyles.Bold;
        tx.alignment = al;
        tx.raycastTarget = false;
        return tx;
    }

    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        float ro = n * 0.5f - 1f, ri = ro - 7f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ro - d + 0.5f) * Mathf.Clamp01(d - ri + 0.5f));
            }
        tex.SetPixels(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    // ------------------------------------------------------------ сборка интерфейса
    void BuildUi()
    {
        using (UiKit.UseSkin(UiSkin.Candy)) BuildCandyUi();
    }

    void BuildCandyUi()
    {
        TMP_FontAsset font = UiKit.ResolveFont(null);   // круглый шрифт Candy, иначе прежний шрифт HUD (с кириллицей)

        var cgo = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Dialogue;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        var c = new Vector2(0.5f, 0.5f);

        // ---------- пузырь
        bubbleRt = Child("Bubble", canvasRt);
        bubbleRt.anchorMin = bubbleRt.anchorMax = c;
        bubbleRt.pivot = new Vector2(0.5f, 0f);
        bubbleRt.sizeDelta = new Vector2(bubbleWidth, 120f);
        bubbleGroup = bubbleRt.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f;
        bubbleGroup.blocksRaycasts = false;
        bubbleGroup.interactable = false;

        // облачное поле с голубой «юбкой» снизу; хвостик ниже продолжает юбку
        var back = Child("Back", bubbleRt);
        Stretch(back, 0, TailH, 0, 0);
        var bi = back.gameObject.AddComponent<Image>();
        bi.sprite = UiKit.Panel; bi.type = Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 1f / 0.6f; bi.color = Color.white; bi.raycastTarget = false;

        // лента-пилюля с именем: желейный спрайт, цвет берётся у говорящего (ApplySpeaker)
        namePillRt = Child("NamePill", back);
        namePillRt.anchorMin = namePillRt.anchorMax = new Vector2(0f, 1f);
        namePillRt.pivot = new Vector2(0f, 0.5f);
        namePillRt.anchoredPosition = new Vector2(24f, 0f);
        namePillRt.sizeDelta = new Vector2(120f, NamePillH);
        namePill = namePillRt.gameObject.AddComponent<Image>();
        namePill.sprite = UiKit.BtnSprite(UiBtn.Gray, UiBtnState.Normal);
        namePill.type = Image.Type.Sliced; namePill.raycastTarget = false;

        var nr = Child("Name", namePillRt);
        Stretch(nr, 14f, 5f, 14f, 1f);      // чуть выше центра: снизу у желе «губа»
        nameTxt = MakeText(nr, 24f, UiCandy.Ink, font, TextAlignmentOptions.Center);
        nameTxt.enableWordWrapping = false;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;

        bodyRt = Child("Text", back);
        Stretch(bodyRt, 28f, BottomPad + HintH, 28f, 34f);
        bodyTxt = MakeText(bodyRt, 30f, UiCandy.Ink, font, TextAlignmentOptions.TopLeft);
        bodyTxt.fontStyle = FontStyles.Normal;
        bodyTxt.enableWordWrapping = true;

        // строка подсказки внизу пузыря + кольцо удержания справа
        var hr = Child("Hint", back);
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, BottomPad - 8f);
        hr.sizeDelta = new Vector2(-84f, HintH);
        hintTxt = MakeText(hr, 20f, UiCandy.Muted, font, TextAlignmentOptions.MidlineLeft);
        hintTxt.fontStyle = FontStyles.Normal;
        hintTxt.richText = true;
        hintTxt.enableWordWrapping = false;
        hintTxt.overflowMode = TextOverflowModes.Ellipsis;
        hintGroup = hr.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        var gr = Child("HoldRing", back);
        gr.anchorMin = gr.anchorMax = new Vector2(1f, 0f);
        gr.pivot = new Vector2(1f, 0f);
        gr.anchoredPosition = new Vector2(-24f, BottomPad - 10f);
        gr.sizeDelta = new Vector2(34f, 34f);
        var gbg = gr.gameObject.AddComponent<Image>();
        gbg.sprite = RingSprite(); gbg.color = new Color(UiCandy.Ink.r, UiCandy.Ink.g, UiCandy.Ink.b, 0.14f); gbg.raycastTarget = false;
        var gf = Child("Fill", gr);
        Stretch(gf, 0, 0, 0, 0);
        holdRing = gf.gameObject.AddComponent<Image>();
        holdRing.sprite = RingSprite(); holdRing.color = UiCandy.Done; holdRing.raycastTarget = false;
        holdRing.type = Image.Type.Filled; holdRing.fillMethod = Image.FillMethod.Radial360;
        holdRing.fillOrigin = (int)Image.Origin360.Top; holdRing.fillClockwise = true; holdRing.fillAmount = 0f;
        holdGroup = gr.gameObject.AddComponent<CanvasGroup>();
        holdGroup.alpha = 0f;

        tailRt = Child("Tail", bubbleRt);
        tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 0f);
        tailRt.sizeDelta = new Vector2(34f, TailH + 2f);
        var tailImg = tailRt.gameObject.AddComponent<Image>();
        tailImg.sprite = UiKit.Tail; tailImg.color = Color.white; tailImg.raycastTarget = false;

        // ---------- субтитры: облачная плашка внизу экрана
        subRt = Child("Subtitles", canvasRt);
        subRt.anchorMin = subRt.anchorMax = new Vector2(0.5f, 0f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.anchoredPosition = new Vector2(0f, 28f);
        subRt.sizeDelta = new Vector2(900f, 70f);
        subGroup = subRt.gameObject.AddComponent<CanvasGroup>();
        subGroup.alpha = 0f;
        subGroup.blocksRaycasts = false;
        subGroup.interactable = false;

        var sb = Child("Back", subRt);
        Stretch(sb, 0, 0, 0, 0);
        var si = sb.gameObject.AddComponent<Image>();
        si.sprite = UiKit.Panel; si.type = Image.Type.Sliced; si.pixelsPerUnitMultiplier = 1f / 0.5f; si.color = new Color(1f, 1f, 1f, 0.96f); si.raycastTarget = false;

        var st = Child("Text", subRt);
        Stretch(st, 36f, SubPadBottom, 36f, SubPadTop);   // снизу больше: там голубая «юбка»
        subTxt = MakeText(st, 32f, UiCandy.Ink, font, TextAlignmentOptions.Center);
        subTxt.fontStyle = FontStyles.Normal;
        subTxt.richText = true;
        subTxt.enableWordWrapping = true;

        // плашка «Разговор пропущен»: чуть выше субтитров, живёт ~1.3 с. Персиковая табличка Candy
        var nt = Child("SkipNote", canvasRt);
        nt.anchorMin = nt.anchorMax = new Vector2(0.5f, 0f);
        nt.pivot = new Vector2(0.5f, 0f);
        nt.anchoredPosition = new Vector2(0f, 120f);
        nt.sizeDelta = new Vector2(430f, 52f);
        noteGroup = nt.gameObject.AddComponent<CanvasGroup>();
        noteGroup.alpha = 0f; noteGroup.blocksRaycasts = false; noteGroup.interactable = false;
        var nb = Child("Back", nt);
        Stretch(nb, 0, 0, 0, 0);
        var ni = nb.gameObject.AddComponent<Image>();
        ni.sprite = UiKit.Plaque; ni.type = Image.Type.Sliced; ni.pixelsPerUnitMultiplier = 1f / 0.5f; ni.color = Color.white; ni.raycastTarget = false;
        var nx = Child("Text", nt);
        Stretch(nx, 16f, 6f, 16f, 6f);
        noteTxt = MakeText(nx, 26f, UiCandy.Ink, font, TextAlignmentOptions.Center);
        noteTxt.fontStyle = FontStyles.Normal;
        noteTxt.text = "Разговор пропущен";
    }

    const float SubPadTop = 12f;
    const float SubPadBottom = 20f;

    // ------------------------------------------------------------ говорящий
    // Красит ленту с именем, подгоняет ширину. Возвращает верхний отступ текста: с именем больше, без имени меньше
    float ApplySpeaker(string who, Color col)
    {
        bool named = !string.IsNullOrEmpty(who);
        namePillRt.gameObject.SetActive(named);
        float topPad = named ? PillOver + 16f : 22f;
        if (named)
        {
            nameTxt.text = who;
            // тёмные цвета осветляем сильнее, чтобы индиго-текст читался
            namePill.color = Color.Lerp(col, Color.white, col.grayscale < 0.45f ? 0.55f : 0.2f);
            float w = nameTxt.GetPreferredValues(who).x + 40f;
            namePillRt.sizeDelta = new Vector2(Mathf.Clamp(w, 92f, bubbleWidth - 64f), NamePillH);
        }
        Stretch(bodyRt, 28f, BottomPad + HintH, 28f, topPad);
        return topPad;
    }

    // цвет имени в субтитрах: цвет говорящего, притемнённый к индиго (золотой на светлом фоне не читается)
    static Color NameInk(Color col) { return Color.Lerp(col, UiCandy.Ink, 0.55f); }

    // ------------------------------------------------------------ мягкая печать
    // typed (дробное число напечатанных букв) двигает Update. Последние SoftChars букв получают прозрачность по возрасту.
    // Сетку каждый кадр перестраиваем, пока идёт печать (реплика до 120 символов, это дёшево), потом один раз возвращаем цвета.
    void SoftType()
    {
        bool fading = typed < total + SoftChars;
        if (!fading && !softDirty) return;

        bodyTxt.ForceMeshUpdate();      // вершины возвращаются к базовому цвету
        softDirty = false;
        if (!fading) return;

        var ti = bodyTxt.textInfo;
        int end = Mathf.Min(Mathf.Min(total, (int)typed), ti.characterCount);
        int from = Mathf.Max(0, end - SoftChars - 1);
        for (int i = from; i < end; i++)
        {
            var ci = ti.characterInfo[i];
            if (!ci.isVisible) continue;
            float a = Mathf.Clamp01((typed - i - 1f) / SoftChars);
            if (a >= 0.999f) continue;
            a = a * a * (3f - 2f * a);
            var cols = ti.meshInfo[ci.materialReferenceIndex].colors32;
            byte ab = (byte)Mathf.RoundToInt(255f * a);
            for (int k = 0; k < 4; k++)
            {
                var cc = cols[ci.vertexIndex + k];
                cc.a = ab;
                cols[ci.vertexIndex + k] = cc;
            }
            softDirty = true;
        }
        if (softDirty) bodyTxt.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}

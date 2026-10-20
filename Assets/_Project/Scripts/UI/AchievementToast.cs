// Assets/_Project/Scripts/UI/AchievementToast.cs (НЕ в Editor)
// Показ нового достижения. Создаётся сам при первом вызове, в сцене ничего настраивать не нужно.
//   AchievementToast.Show(def)   поставить достижение в очередь (зовёт AchievementManager)
//
// Два вида показа:
//   Бронза и серебро: плашка выезжает справа, медальон «хлопает» с искрами, по плашке пробегает блик. ~4 с.
//   ЗОЛОТО: затемнение экрана, вспышка, медальон влетает с поворотом, за ним крутятся лучи, россыпь искр,
//           подпись «разъезжается», название падает с увеличением, линия выезжает из центра, награда считается вверх. ~5 с.
// Очередь: пока идёт диалог или финал, показ ждёт. Если за кадром стоит ещё очередь, показы короче.
//
// Положение плашки подгоняется двумя константами ниже (SlideAnchorY и SlideMargin).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public class AchievementToast : MonoBehaviour
{
    // ------------------------------------------------------------ настройки
    const float SlideAnchorY = 0.70f;       // высота плашки на экране справа: 0 низ, 1 верх (под мини-картой)
    const float SlideMargin = 30f;          // отступ от правого края
    const float SlideW = 580f, SlideH = 140f;
    const float SlideTime = 4.3f, SlideTimeShort = 2.9f;
    const float EpicTime = 5.4f, EpicTimeShort = 4.0f;
    const float MedalY = 120f;              // высота медальона в золотом показе (от центра блока)

    static AchievementToast inst;
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f), TopLeft = new Vector2(0f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; }

    public static void Show(AchievementDef d)
    {
        if (d == null) return;
        if (inst == null)
        {
            var go = new GameObject("AchievementToast");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<AchievementToast>();
        }
        inst.queue.Enqueue(d);
    }

    void OnDestroy() { if (inst == this) inst = null; }

    // ------------------------------------------------------------ состояние
    readonly Queue<AchievementDef> queue = new Queue<AchievementDef>();
    AchievementDef cur;
    Color tc;
    bool built, epic;
    float t, dur;
    int sfxStep;
    bool burst1, burst2;

    RectTransform canvasRt;

    // плашка
    RectTransform sRoot, sMedal, sShine;
    CanvasGroup sGroup;
    Image sGlow, sMedalImg, sIcon, sShineImg, sCoinIcon;
    TMP_Text sCap, sTitle, sCoins, sXp, sHint;
    AchSparks sSparks;

    // золотой показ
    RectTransform eBox, eMedal, eTitleRt, eRaysRt, eLine;
    CanvasGroup eGroup;
    Image eDim, eFlash, eRays, eGlow, eMedalImg, eIcon, eLineImg, eCoinIcon;
    TMP_Text eCap, eTitle, eCoins, eXp, eHint;
    AchSparks eSparks;

    // ------------------------------------------------------------ сборка
    void EnsureBuilt()
    {
        if (built) return;
        var cgo = new GameObject("AchievementToastCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;                              // выше всех окон (31)
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        BuildEpic();
        BuildSlide();
        built = true;
    }

    TMP_Text Lab(Transform p, float size, Color c, TextAlignmentOptions al, bool outline = true)
    {
        var tx = UiKit.MakeLabel(p, "", size, c, al, true);
        if (outline) Outlined(tx);
        return tx;
    }

    void BuildSlide()
    {
        sRoot = NewRT("Slide", canvasRt);
        Anch(sRoot, new Vector2(1f, SlideAnchorY), new Vector2(1f, 0.5f), new Vector2(SlideW + 60f, 0f), new Vector2(SlideW, SlideH));
        sGroup = sRoot.gameObject.AddComponent<CanvasGroup>();
        sGroup.blocksRaycasts = false;
        sGroup.interactable = false;

        var panel = UiKit.MakePanel(sRoot, new Vector2(SlideW, SlideH), false, 0.7f, "Panel");
        Stretch(panel.rectTransform);

        // блик бегает внутри маски
        var clip = NewRT("Clip", sRoot);
        Stretch(clip);
        clip.offsetMin = new Vector2(12f, 12f);
        clip.offsetMax = new Vector2(-12f, -12f);
        clip.gameObject.AddComponent<RectMask2D>();
        sShineImg = UiKit.MakeImage(clip, "Shine", null, false);
        sShineImg.color = new Color(1f, 1f, 1f, 0f);
        sShine = sShineImg.rectTransform;
        Anch(sShine, new Vector2(0f, 0.5f), Mid, new Vector2(-200f, 0f), new Vector2(64f, 340f));
        sShine.localEulerAngles = new Vector3(0f, 0f, 18f);

        sGlow = UiKit.MakeImage(sRoot, "Glow", PetUi.GlowSprite, false);
        TLC(sGlow.rectTransform, 84f, SlideH * 0.5f, 210f, 210f);

        sMedalImg = UiKit.MakeImage(sRoot, "Medal", UiKit.Medallion, false);
        sMedal = sMedalImg.rectTransform;
        TLC(sMedal, 84f, SlideH * 0.5f, 104f, 104f);
        sIcon = UiKit.MakeIcon(sMedal, UiIcon.Star, 62f, "Icon");
        sIcon.rectTransform.anchoredPosition = new Vector2(0f, 1f);

        sCap = Lab(sRoot, 19f, Color.white, TextAlignmentOptions.Left);
        TL(sCap.rectTransform, 156f, 18f, 400f, 26f);
        sCap.characterSpacing = 5f;

        sTitle = Lab(sRoot, 36f, Color.white, TextAlignmentOptions.Left);
        TL(sTitle.rectTransform, 156f, 44f, 400f, 46f);
        sTitle.enableAutoSizing = true; sTitle.fontSizeMin = 18f; sTitle.fontSizeMax = 36f;
        sTitle.textWrappingMode = TextWrappingModes.NoWrap;

        sCoinIcon = UiKit.MakeIcon(sRoot, UiIcon.Coin, 28f, "Coin");
        TLC(sCoinIcon.rectTransform, 170f, 109f, 28f, 28f);
        sCoins = Lab(sRoot, 25f, UiKit.BrassHi, TextAlignmentOptions.Left);
        TL(sCoins.rectTransform, 190f, 94f, 110f, 30f);
        sXp = Lab(sRoot, 21f, UiKit.Good, TextAlignmentOptions.Left);
        TL(sXp.rectTransform, 300f, 96f, 120f, 28f);
        sHint = Lab(sRoot, 17f, PetUi.Dim, TextAlignmentOptions.Right, false);
        TL(sHint.rectTransform, 380f, 98f, 170f, 26f);

        sSparks = AchSparks.Create(sRoot, TopLeft);
        sRoot.gameObject.SetActive(false);
    }

    void BuildEpic()
    {
        var root = NewRT("Epic", canvasRt);
        Stretch(root);
        eGroup = root.gameObject.AddComponent<CanvasGroup>();
        eGroup.blocksRaycasts = false;
        eGroup.interactable = false;

        eDim = UiKit.MakeImage(root, "Dim", null, false);
        eDim.color = new Color(0.02f, 0.03f, 0.07f, 0f);
        Stretch(eDim.rectTransform);

        eFlash = UiKit.MakeImage(root, "Flash", null, false);
        eFlash.color = new Color(1f, 0.95f, 0.8f, 0f);
        Stretch(eFlash.rectTransform);

        eBox = NewRT("Box", root);
        Anch(eBox, new Vector2(0.5f, 0.6f), Mid, Vector2.zero, new Vector2(1200f, 560f));

        eRays = UiKit.MakeImage(eBox, "Rays", PetUi.RaysSprite, false);
        eRaysRt = eRays.rectTransform;
        Anch(eRaysRt, Mid, Mid, new Vector2(0f, MedalY), new Vector2(980f, 980f));

        eGlow = UiKit.MakeImage(eBox, "Glow", PetUi.GlowSprite, false);
        Anch(eGlow.rectTransform, Mid, Mid, new Vector2(0f, MedalY), new Vector2(520f, 520f));

        eMedalImg = UiKit.MakeImage(eBox, "Medal", UiKit.Medallion, false);
        eMedal = eMedalImg.rectTransform;
        Anch(eMedal, Mid, Mid, new Vector2(0f, MedalY), new Vector2(200f, 200f));
        eIcon = UiKit.MakeIcon(eMedal, UiIcon.Star, 118f, "Icon");
        eIcon.rectTransform.anchoredPosition = new Vector2(0f, 2f);

        eCap = Lab(eBox, 30f, Color.white, TextAlignmentOptions.Center);
        Anch(eCap.rectTransform, Mid, Mid, new Vector2(0f, -8f), new Vector2(1200f, 44f));
        eCap.textWrappingMode = TextWrappingModes.NoWrap;

        eTitle = Lab(eBox, 78f, Color.white, TextAlignmentOptions.Center);
        eTitleRt = eTitle.rectTransform;
        Anch(eTitleRt, Mid, Mid, new Vector2(0f, -72f), new Vector2(1160f, 104f));
        eTitle.enableAutoSizing = true; eTitle.fontSizeMin = 36f; eTitle.fontSizeMax = 78f;
        eTitle.textWrappingMode = TextWrappingModes.NoWrap;
        AchUi.Outlined(eTitle, 0.26f);

        eLineImg = UiKit.MakeImage(eBox, "Line", null, false);
        eLine = eLineImg.rectTransform;
        Anch(eLine, Mid, Mid, new Vector2(0f, -136f), new Vector2(0f, 5f));

        eCoinIcon = UiKit.MakeIcon(eBox, UiIcon.Coin, 46f, "Coin");
        Anch(eCoinIcon.rectTransform, Mid, Mid, new Vector2(-92f, -190f), new Vector2(46f, 46f));
        eCoins = Lab(eBox, 44f, UiKit.BrassHi, TextAlignmentOptions.Left);
        Anch(eCoins.rectTransform, Mid, new Vector2(0f, 0.5f), new Vector2(-62f, -190f), new Vector2(170f, 56f));
        eXp = Lab(eBox, 32f, UiKit.Good, TextAlignmentOptions.Left);
        Anch(eXp.rectTransform, Mid, new Vector2(0f, 0.5f), new Vector2(118f, -190f), new Vector2(180f, 44f));
        eHint = Lab(eBox, 22f, PetUi.Soft, TextAlignmentOptions.Center, false);
        Anch(eHint.rectTransform, Mid, Mid, new Vector2(0f, -246f), new Vector2(900f, 34f));

        eSparks = AchSparks.Create(eBox, Mid);   // последним: искры поверх всего
        root.gameObject.SetActive(false);
        eRoot = root.gameObject;
    }
    GameObject eRoot;

    // ------------------------------------------------------------ цикл
    static bool Blocked()
    {
        return DialogueManager.IsBusy || DialogueSequence.AnyPlaying || EndingSequence.Active;
    }

    void Update()
    {
        if (cur == null)
        {
            if (queue.Count == 0 || Blocked()) return;
            EnsureBuilt();
            Begin(queue.Dequeue());
        }

        t += Time.unscaledDeltaTime;
        if (t >= dur) { End(); return; }
        if (epic) TickEpic(Time.unscaledDeltaTime); else TickSlide(Time.unscaledDeltaTime);
    }

    void Begin(AchievementDef d)
    {
        cur = d;
        tc = TierColor(d.tier);
        epic = d.tier == AchTier.Gold;
        bool crowd = queue.Count > 0;
        dur = epic ? (crowd ? EpicTimeShort : EpicTime) : (crowd ? SlideTimeShort : SlideTime);
        t = 0f; sfxStep = 0; burst1 = burst2 = false;

        bool mobile = Application.isMobilePlatform;
        string hint = mobile ? "Награда ждёт в «Достижениях»" : "Забери награду: клавиша K";

        if (epic)
        {
            eRoot.SetActive(true);
            eGroup.alpha = 0f;
            eMedalImg.color = tc;
            eIcon.sprite = UiKit.Icon(d.icon);
            eRays.color = new Color(tc.r, tc.g, tc.b, 0f);
            eGlow.color = new Color(tc.r, tc.g, tc.b, 0f);
            eCap.text = "ЗОЛОТОЕ ДОСТИЖЕНИЕ";
            eCap.color = Color.Lerp(Color.white, tc, 0.6f);
            eTitle.text = d.title;
            eLineImg.color = tc;
            eCoinIcon.gameObject.SetActive(d.coins > 0);
            eCoins.gameObject.SetActive(d.coins > 0);
            eXp.gameObject.SetActive(d.xp > 0);
            eXp.text = d.xp > 0 ? "+" + d.xp + " XP" : "";
            eHint.text = hint;
            eMedal.localScale = Vector3.zero;
        }
        else
        {
            sRoot.gameObject.SetActive(true);
            sGroup.alpha = 1f;
            sMedalImg.color = tc;
            sIcon.sprite = UiKit.Icon(d.icon);
            sGlow.color = new Color(tc.r, tc.g, tc.b, 0f);
            sCap.text = d.tier == AchTier.Silver ? "СЕРЕБРЯНОЕ ДОСТИЖЕНИЕ" : "ДОСТИЖЕНИЕ";
            sCap.color = Color.Lerp(Color.white, tc, 0.65f);
            sTitle.text = d.title;
            sCoinIcon.gameObject.SetActive(d.coins > 0);
            sCoins.gameObject.SetActive(d.coins > 0);
            sCoins.text = d.coins > 0 ? "+" + d.coins : "";
            sXp.gameObject.SetActive(d.xp > 0);
            sXp.text = d.xp > 0 ? "+" + d.xp + " XP" : "";
            sHint.text = mobile ? "" : "K: забрать";
            sMedal.localScale = Vector3.zero;
            sShineImg.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    void End()
    {
        cur = null;
        if (sRoot != null) sRoot.gameObject.SetActive(false);
        if (eRoot != null) eRoot.SetActive(false);
    }

    // ------------------------------------------------------------ бронза и серебро
    void TickSlide(float dt)
    {
        float shown = -SlideMargin, hidden = SlideW + 60f;
        float inK = Ph(t, 0f, 0.5f), outK = Ph(t, dur - 0.45f, dur);
        float x = Mathf.LerpUnclamped(hidden, shown, UiDraw.EaseOutBack(inK));
        x = Mathf.Lerp(x, hidden, outK * outK);
        sRoot.anchoredPosition = new Vector2(x, sRoot.anchoredPosition.y);

        // звук и искры в момент, когда плашка доехала
        if (sfxStep == 0 && t >= 0.12f)
        {
            sfxStep = 1;
            SfxManager.Play("hint", 0.8f, cur.tier == AchTier.Silver ? 1.45f : 1.25f);
        }
        if (!burst1 && t >= 0.3f)
        {
            burst1 = true;
            sSparks.Burst(new Vector2(84f, -SlideH * 0.5f), tc, cur.tier == AchTier.Silver ? 18 : 12, 300f, 22f, 260f, 0.8f);
        }

        float mk = Ph(t, 0.2f, 0.65f);
        sMedal.localScale = Vector3.one * UiDraw.EaseOutBack(mk);
        sMedal.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-18f, 0f, UiDraw.EaseOutCubic(mk)));

        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        sGlow.color = new Color(tc.r, tc.g, tc.b, mk * Mathf.Lerp(0.30f, 0.6f, pulse) * (1f - outK));

        // блик по плашке
        float sk = Ph(t, 0.55f, 1.3f);
        sShine.anchoredPosition = new Vector2(Mathf.Lerp(-120f, SlideW + 120f, UiDraw.EaseOutCubic(sk)), 0f);
        sShineImg.color = new Color(1f, 1f, 1f, 0.26f * Mathf.Sin(sk * Mathf.PI));
    }

    // ------------------------------------------------------------ золото
    void TickEpic(float dt)
    {
        eGroup.alpha = 1f - Ph(t, dur - 0.6f, dur);

        // затемнение и вспышка
        eDim.color = new Color(0.02f, 0.03f, 0.07f, 0.55f * Ph(t, 0f, 0.4f));
        float fl = t < 0.06f ? t / 0.06f : 1f - Ph(t, 0.06f, 0.55f);
        eFlash.color = new Color(1f, 0.95f, 0.8f, Mathf.Clamp01(fl * 0.5f));

        // звуки по сценарию
        if (sfxStep == 0) { sfxStep = 1; SfxManager.Play("quest", 1f, 1f); }
        if (sfxStep == 1 && t >= 0.28f) { sfxStep = 2; SfxManager.Play("level", 1f, 1.05f); }
        if (sfxStep == 2 && t >= 0.95f) { sfxStep = 3; SfxManager.Play("hint", 0.8f, 1.7f); }

        // медальон влетает
        float mk = Ph(t, 0.05f, 0.65f);
        eMedal.localScale = Vector3.one * UiDraw.EaseOutBack(mk);
        eMedal.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-34f, 0f, UiDraw.EaseOutCubic(mk)));

        // лучи и свечение
        float rk = Ph(t, 0.15f, 1.0f);
        eRays.color = new Color(tc.r, tc.g, tc.b, 0.75f * rk);
        eRaysRt.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, UiDraw.EaseOutCubic(rk));
        eRaysRt.Rotate(0f, 0f, -20f * dt);
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
        eGlow.color = new Color(tc.r, tc.g, tc.b, rk * Mathf.Lerp(0.45f, 0.8f, pulse));

        // подпись «разъезжается»
        float ck = Ph(t, 0.45f, 0.95f);
        eCap.alpha = ck;
        eCap.characterSpacing = Mathf.Lerp(48f, 12f, UiDraw.EaseOutCubic(ck));

        // название падает с увеличением
        float tk = Ph(t, 0.6f, 1.1f);
        eTitle.alpha = tk;
        eTitleRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.4f, 1f, UiDraw.EaseOutBack(tk));

        // линия выезжает из центра
        eLine.sizeDelta = new Vector2(Mathf.Lerp(0f, 560f, UiDraw.EaseOutCubic(Ph(t, 0.85f, 1.45f))), 5f);

        // награда: монеты считаются вверх
        float rw = Ph(t, 1.1f, 1.6f);
        eCoinIcon.color = new Color(1f, 1f, 1f, rw);
        eCoins.alpha = rw;
        eXp.alpha = rw;
        eCoins.text = "+" + Mathf.RoundToInt(cur.coins * UiDraw.EaseOutCubic(Ph(t, 1.15f, 2.1f)));
        eHint.alpha = 0.85f * Ph(t, 1.9f, 2.4f);

        // россыпь искр: в момент удара и ещё раз, когда появилось название
        if (!burst1 && t >= 0.28f)
        {
            burst1 = true;
            eSparks.Burst(new Vector2(0f, MedalY), tc, 54, 680f, 36f, 540f, 1.3f);
        }
        if (!burst2 && t >= 0.9f)
        {
            burst2 = true;
            eSparks.Burst(new Vector2(0f, MedalY), Color.white, 26, 440f, 28f, 380f, 1.1f);
            eSparks.Burst(new Vector2(0f, -72f), tc, 20, 360f, 24f, 260f, 1.0f);
        }
    }
}

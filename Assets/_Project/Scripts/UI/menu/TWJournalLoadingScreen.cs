// TimberWings — Journal loading screen shown after "Продолжить".
//
// Design
//  * The overlay is a CLONE of the live menu canvas (background, book, logo, bookmark, version badge), so it is
//    pixel-identical to what the player has just seen; only the two pages change: the buttons / illustrations are
//    replaced by "С возвращением!" + the save line + short atmospheric messages (left page) and sketches from
//    Leo's journey (right page). There is no cut and no new screen, the book simply turns the page.
//  * No fake percentages. The motion (leaves, sketches, messages) is purely atmospheric.
//  * The real asynchronous scene load stays in MenuFlow. The overlay only listens to SceneManager.sceneLoaded
//    for the game scene, shows "Готово!" and fades out. If the game scene cannot be opened it closes itself.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TWJournalLoadingScreen : MonoBehaviour
{
    public static TWJournalLoadingScreen Active { get; private set; }

    const int SortingOrder = 32000;           // above MenuFlow fade (5000), Settings (1000) and SaveIndicator (30000)
    const float BeatSeconds = 2.8f;           // one message + one sketch
    const float FadeInSeconds = .32f;
    const float PasteSeconds = .46f;
    const float PeelSeconds = .30f;
    static readonly Color Ink = new Color32(70, 32, 62, 255);
    static readonly Color SubtleInk = new Color32(108, 66, 62, 255);
    static readonly Color Gold = new Color32(214, 150, 40, 255);

    static readonly string[] DefaultMessages =
    {
        "Открываем дневник Лео…",
        "Перелистываем записи последних дней…",
        "Достаём карту острова…",
        "Сверяем заметки с последним привалом…",
        "Ветер шепчет о мосте, который ещё можно починить…",
        "Руны на камне едва заметно светятся…",
    };

    // Hero area of the right page, book-local units (inside the safe page area, see installer).
    static readonly Vector2 SketchCenter = new Vector2(327f, 59f);
    static readonly Vector2 SketchMaxSize = new Vector2(380f, 280f);
    const float SketchMaxUpscale = 1.25f;      // keep illustrations crisp
    const float CaptionGap = 38f;              // caption sits right under the current sketch

    // ---- built UI
    CanvasGroup rootGroup, leftGroup, rightGroup;
    RectTransform canvasRect, layoutRect;
    TMP_Text titleText, saveText, messageText, captionText;
    RectTransform titleRect, messageRect, captionRect;
    readonly Image[] slotImage = new Image[2];
    readonly RectTransform[] slotRect = new RectTransform[2];
    readonly float[] slotStart = { -100f, -100f };
    readonly float[] slotPeel = { -100f, -100f };
    readonly float[] slotTilt = new float[2];
    readonly Vector2[] slotBase = new Vector2[2];
    RectTransform[] leafRect;
    Image[] leafImage;
    RectTransform[] sparkleRect;
    Image[] sparkleImage;
    AudioSource audioSrc;

    // ---- content
    Sprite[] sketches;
    string[] captions;
    string[] messages;
    AudioClip pageClip, readyClip;
    float soundLevel;

    // ---- state
    float began;
    float beatStart;
    int beat = -1;
    int currentSlot = 1;
    float closeAt = -1f;
    float readyAt = -1f;
    bool changingScene, finishing, reduced, fallbackMode;
    float fallbackAlpha;

    /// <summary>Creates the loading screen over the menu. Safe to call twice (second call is ignored).</summary>
    public static void Show(TWJournalMenuController menu)
    {
        if (Active != null) return;
        var holder = new GameObject("[TW] Journal Continue Loading", typeof(RectTransform));
        holder.SetActive(false);                 // Awake / OnEnable run after Build()
        DontDestroyOnLoad(holder);
        var screen = holder.AddComponent<TWJournalLoadingScreen>();
        Active = screen;
        screen.Build(menu);
        holder.SetActive(true);
        screen.AfterActivated();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        began = Time.unscaledTime;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Active == this) Active = null;
    }

    // ------------------------------------------------------------------ build

    void Build(TWJournalMenuController menu)
    {
        reduced = VibeSettings.ReduceEffects;
        pageClip = menu != null ? menu.pageClip : null;
        readyClip = menu != null ? menu.readyClip : null;
        soundLevel = menu != null ? menu.uiSoundVolume : .7f;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;

        // Message / sketch content (all editable on the controller, with safe built-in defaults).
        messages = menu != null && menu.loadingMessages != null && menu.loadingMessages.Length > 0
            ? menu.loadingMessages : DefaultMessages;
        var sk = new List<Sprite>();
        if (menu != null && menu.loadingSketches != null)
            foreach (var s in menu.loadingSketches) if (s != null) sk.Add(s);
        if (sk.Count == 0 && menu != null)
        {
            if (menu.loadingCedar != null) sk.Add(menu.loadingCedar);
            if (menu.loadingBridge != null) sk.Add(menu.loadingBridge);
            if (menu.loadingPortal != null) sk.Add(menu.loadingPortal);
        }
        sketches = sk.ToArray();
        captions = menu != null && menu.loadingSketchCaptions != null ? menu.loadingSketchCaptions : new string[0];

        if (menu == null || menu.canvasRect == null || menu.bookRoot == null || menu.layoutRoot == null)
        {
            BuildFallback(menu);
            return;
        }

        // 1) Clone the menu canvas while the holder is inactive (no Awake / Start runs on the clone).
        GameObject clone = Instantiate(menu.canvasRect.gameObject, transform, false);
        clone.name = "Canvas_JournalLoading";
        canvasRect = clone.transform as RectTransform;

        // Locate the pieces inside the clone through the sibling-index path of the originals (rename-proof).
        Transform cloneLayout = Follow(clone.transform, PathTo(menu.canvasRect, menu.layoutRoot));
        Transform cloneBook = Follow(clone.transform, PathTo(menu.canvasRect, menu.bookRoot));
        layoutRect = cloneLayout as RectTransform;
        var bookRect = cloneBook as RectTransform;
        if (layoutRect == null || bookRect == null) { Destroy(clone); BuildFallback(menu); return; }

        Transform leftOriginal = menu.leftPageRoot != null ? menu.leftPageRoot
            : (menu.continueButton != null ? menu.continueButton.transform.parent as RectTransform : null);
        Transform rightOriginal = menu.rightPageRoot;
        var leftGeo = leftOriginal as RectTransform;

        RemoveFromClone(clone.transform, menu.canvasRect, leftOriginal);
        RemoveFromClone(clone.transform, menu.canvasRect, rightOriginal);
        RemoveFromClone(clone.transform, menu.canvasRect, menu.hintGroup != null ? menu.hintGroup.transform : null);
        RemoveFromClone(clone.transform, menu.canvasRect, menu.confirmation != null ? menu.confirmation.transform : null);

        var cv = clone.GetComponent<Canvas>();
        if (cv != null) cv.sortingOrder = SortingOrder;
        rootGroup = clone.GetComponent<CanvasGroup>();
        if (rootGroup == null) rootGroup = clone.AddComponent<CanvasGroup>();
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = false;

        // Swallow every click while loading.
        var blocker = NewRect("Input_Blocker", clone.transform);
        blocker.SetAsFirstSibling();
        Stretch(blocker);
        var bi = blocker.gameObject.AddComponent<Image>();
        bi.color = new Color(0f, 0f, 0f, 0f);
        bi.raycastTarget = true;

        // Layout CanvasGroup of the clone must be fully visible and static.
        var layoutGroup = layoutRect.GetComponent<CanvasGroup>();
        if (layoutGroup != null) { layoutGroup.alpha = 1f; layoutGroup.interactable = false; layoutGroup.blocksRaycasts = false; }

        TMP_FontAsset font = menu.continueSubtitle != null ? menu.continueSubtitle.font : null;
        Sprite leaf = menu.loadingLeaf;

        // 2) Left page: greeting, save line, atmospheric message, leaf trail.
        Vector2 leftCenter = leftGeo != null ? leftGeo.anchoredPosition : new Vector2(-316f, 30f);
        var left = NewRect("LeftPage_Loading", bookRect);
        left.anchorMin = left.anchorMax = left.pivot = new Vector2(.5f, .5f);
        left.anchoredPosition = leftCenter;
        left.sizeDelta = new Vector2(450f, 460f);
        leftGroup = left.gameObject.AddComponent<CanvasGroup>();
        leftGroup.alpha = reduced ? 1f : 0f;
        leftGroup.blocksRaycasts = false;

        titleText = MakeText("Title", left, "С возвращением!", 50f, FontStyles.Bold, Ink, font, new Vector2(440f, 64f));
        titleRect = titleText.rectTransform; titleRect.anchoredPosition = new Vector2(0f, 120f);

        bool any;
        string line = TWJournalMenuController.BuildSaveLine(out any);
        saveText = MakeText("SaveLine", left, line, 28f, FontStyles.Normal, SubtleInk, font, new Vector2(440f, 38f));
        saveText.rectTransform.anchoredPosition = new Vector2(0f, 66f);
        saveText.gameObject.SetActive(any);

        var rule = NewRect("Rule", left);
        rule.sizeDelta = new Vector2(250f, 3f);
        rule.anchoredPosition = new Vector2(0f, any ? 30f : 50f);
        var ruleImg = rule.gameObject.AddComponent<Image>();
        ruleImg.color = new Color(Gold.r, Gold.g, Gold.b, .55f);
        ruleImg.raycastTarget = false;

        messageText = MakeText("Message", left, messages[0], 30f, FontStyles.Italic, Ink, font, new Vector2(390f, 124f));
        messageRect = messageText.rectTransform; messageRect.anchoredPosition = new Vector2(0f, -38f);

        int leaves = leaf != null ? 5 : 0;
        leafRect = new RectTransform[leaves];
        leafImage = new Image[leaves];
        for (int i = 0; i < leaves; i++)
        {
            var lr = NewRect("Leaf_" + (i + 1), left);
            lr.sizeDelta = new Vector2(54f, 54f * leaf.rect.height / Mathf.Max(1f, leaf.rect.width));
            lr.anchoredPosition = new Vector2((i - (leaves - 1) * .5f) * 66f, -150f);
            var li = lr.gameObject.AddComponent<Image>();
            li.sprite = leaf; li.preserveAspect = true; li.raycastTarget = false;
            li.color = new Color(1f, 1f, 1f, .45f);
            leafRect[i] = lr; leafImage[i] = li;
        }

        // 3) Right page: two sketch slots that "paste / peel" alternately, caption below.
        var right = NewRect("RightPage_Loading", bookRect);
        right.anchorMin = right.anchorMax = right.pivot = new Vector2(.5f, .5f);
        right.anchoredPosition = Vector2.zero;
        right.sizeDelta = new Vector2(1600f, 900f);
        rightGroup = right.gameObject.AddComponent<CanvasGroup>();
        rightGroup.alpha = reduced ? 1f : 0f;
        rightGroup.blocksRaycasts = false;

        for (int s = 0; s < 2; s++)
        {
            var sr = NewRect("Sketch_" + (s == 0 ? "A" : "B"), right);
            sr.anchoredPosition = SketchCenter;
            var si = sr.gameObject.AddComponent<Image>();
            si.preserveAspect = true; si.raycastTarget = false;
            si.color = new Color(1f, 1f, 1f, 0f);
            var shadow = sr.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.18f, .08f, .16f, .30f);
            shadow.effectDistance = new Vector2(3f, -5f);
            slotRect[s] = sr; slotImage[s] = si;
        }

        captionText = MakeText("Caption", right, string.Empty, 26f, FontStyles.Italic, SubtleInk, font, new Vector2(400f, 70f));
        captionRect = captionText.rectTransform; captionRect.anchoredPosition = new Vector2(SketchCenter.x, SketchCenter.y - 130f);

        // Two gold sparkles around the sketch (re-use the menu's own sparkle sprite when there is one).
        Sprite sparkle = null;
        foreach (var img in clone.GetComponentsInChildren<Image>(true))
            if (img.sprite != null && img.name.ToLowerInvariant().Contains("sparkle")) { sparkle = img.sprite; break; }
        if (sparkle != null && !reduced)
        {
            sparkleRect = new RectTransform[2]; sparkleImage = new Image[2];
            Vector2[] pos = { SketchCenter + new Vector2(-150f, 110f), SketchCenter + new Vector2(158f, -96f) };
            for (int i = 0; i < 2; i++)
            {
                var sp = NewRect("Sparkle_" + (i + 1), right);
                sp.sizeDelta = new Vector2(34f, 34f);
                sp.anchoredPosition = pos[i];
                var im = sp.gameObject.AddComponent<Image>();
                im.sprite = sparkle; im.raycastTarget = false; im.color = new Color(1f, 1f, 1f, 0f);
                sparkleRect[i] = sp; sparkleImage[i] = im;
            }
        }

        fallbackMode = false;
    }

    // Minimal screen for a broken / incomplete menu scene: still reacts and finishes correctly.
    void BuildFallback(TWJournalMenuController menu)
    {
        fallbackMode = true;
        var canvasGo = new GameObject("Canvas_JournalLoadingFallback", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var cv = canvasGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasRect = canvasGo.transform as RectTransform;
        rootGroup = canvasGo.AddComponent<CanvasGroup>();
        var bg = NewRect("Dim", canvasGo.transform);
        Stretch(bg);
        var bgi = bg.gameObject.AddComponent<Image>();
        bgi.color = new Color32(32, 18, 40, 255);
        TMP_FontAsset font = menu != null && menu.continueSubtitle != null ? menu.continueSubtitle.font : null;
        titleText = MakeText("Title", bg, "С возвращением!", 56f, FontStyles.Bold, new Color32(255, 236, 190, 255), font, new Vector2(900f, 80f));
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        messageText = MakeText("Message", bg, messages[0], 32f, FontStyles.Italic, new Color32(232, 210, 255, 255), font, new Vector2(900f, 60f));
        messageText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        messageRect = messageText.rectTransform;
        fallbackAlpha = 0f;
        rootGroup.alpha = 0f;
    }

    void AfterActivated()
    {
        if (fallbackMode) return;
        // The clone was taken mid-animation: restore true base poses so the ornaments continue smoothly.
        foreach (var amb in GetComponentsInChildren<TWJournalAmbientMotion>(true)) amb.RebaseFromAnimatedState();
        ApplyFit();
        beatStart = Time.unscaledTime;
        NextBeat(false);
    }

    // ------------------------------------------------------------------ helpers

    static int[] PathTo(Transform root, Transform t)
    {
        if (root == null || t == null) return null;
        var list = new List<int>();
        Transform cur = t;
        while (cur != null && cur != root) { list.Add(cur.GetSiblingIndex()); cur = cur.parent; }
        if (cur != root) return null;
        list.Reverse();
        return list.ToArray();
    }

    static Transform Follow(Transform root, int[] path)
    {
        if (root == null || path == null) return null;
        Transform cur = root;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= cur.childCount) return null;
            cur = cur.GetChild(path[i]);
        }
        return cur;
    }

    static void RemoveFromClone(Transform cloneRoot, Transform originalRoot, Transform original)
    {
        Transform t = Follow(cloneRoot, PathTo(originalRoot, original));
        if (t == null || t == cloneRoot) return;
        t.gameObject.SetActive(false);
        Destroy(t.gameObject);
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.transform as RectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TMP_Text MakeText(string name, Transform parent, string text, float size, FontStyles style,
        Color color, TMP_FontAsset font, Vector2 box)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    void ApplyFit()
    {
        if (layoutRect != null && canvasRect != null)
            layoutRect.localScale = Vector3.one * TWJournalMenuController.ComputeFit(canvasRect);
    }

    void PlayClip(AudioClip clip, float gain)
    {
        if (clip == null || audioSrc == null) return;
        float vol = GameAudio.SfxLevel * soundLevel * gain;
        if (vol > .001f) audioSrc.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // ------------------------------------------------------------------ beats (message + sketch)

    void NextBeat(bool playSound)
    {
        beat++;
        beatStart = Time.unscaledTime;
        if (messageText != null && messages.Length > 0)
            messageText.text = messages[beat % messages.Length];
        if (playSound) PlayClip(pageClip, .3f);
        if (sketches == null || sketches.Length == 0 || slotImage[0] == null) return;

        int next = 1 - currentSlot;
        slotPeel[currentSlot] = Time.unscaledTime;             // old sketch peels off
        int idx = beat % sketches.Length;
        Sprite sp = sketches[idx];
        slotImage[next].sprite = sp;
        // Size: fit the hero area, never upscale beyond SketchMaxUpscale (keeps the art crisp).
        float scale = Mathf.Min(SketchMaxUpscale, SketchMaxSize.x / sp.rect.width, SketchMaxSize.y / sp.rect.height);
        slotRect[next].sizeDelta = new Vector2(sp.rect.width * scale, sp.rect.height * scale);
        slotTilt[next] = (idx % 2 == 0 ? -1.6f : 1.9f);
        slotBase[next] = SketchCenter;
        if (captionRect != null)
            captionRect.anchoredPosition = new Vector2(SketchCenter.x, SketchCenter.y - slotRect[next].sizeDelta.y * .5f - CaptionGap);
        slotStart[next] = Time.unscaledTime;
        slotPeel[next] = float.MaxValue;
        currentSlot = next;
        if (captionText != null)
            captionText.text = idx < captions.Length ? captions[idx] : string.Empty;
    }

    void AnimateSlots(float now)
    {
        for (int s = 0; s < 2; s++)
        {
            var img = slotImage[s];
            if (img == null) continue;
            float alpha, scale = 1f, rot = slotTilt[s];
            Vector2 offset = Vector2.zero;
            if (reduced)
            {
                alpha = s == currentSlot ? 1f : 0f;
            }
            else
            {
                float u = Mathf.Clamp01((now - slotStart[s]) / PasteSeconds);
                alpha = Mathf.Clamp01(u * 3f);
                scale = Mathf.LerpUnclamped(1.14f, 1f, EaseOutBack(u));
                rot += (1f - u) * -6f;
                offset.y = (1f - u) * 14f;
                if (slotPeel[s] < now)
                {
                    float v = Mathf.Clamp01((now - slotPeel[s]) / PeelSeconds);
                    alpha *= 1f - v;
                    offset += new Vector2(18f, 26f) * v;
                    rot += 8f * v;
                    scale *= 1f + .03f * v;
                }
            }
            var c = img.color; c.a = alpha; img.color = c;
            slotRect[s].anchoredPosition = slotBase[s] + offset;
            slotRect[s].localRotation = Quaternion.Euler(0f, 0f, rot);
            slotRect[s].localScale = Vector3.one * scale;
        }
    }

    // ------------------------------------------------------------------ frame update

    void Update()
    {
        float now = Time.unscaledTime;

        if (fallbackMode)
        {
            fallbackAlpha = Mathf.MoveTowards(fallbackAlpha, 1f, Time.unscaledDeltaTime / .25f);
            UpdateClose(now, fallbackAlpha);
            return;
        }

        ApplyFit();
        float t = now - began;

        // Fade the new page content in over the identical book.
        float fadeIn = reduced ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / FadeInSeconds));
        if (leftGroup != null) leftGroup.alpha = fadeIn;
        if (rightGroup != null) rightGroup.alpha = fadeIn;

        // Title pops in once.
        if (titleRect != null && !reduced)
        {
            float k = Mathf.Clamp01((t - .05f) / .5f);
            titleRect.localScale = Vector3.one * Mathf.LerpUnclamped(.9f, 1f, EaseOutBack(k));
        }

        // Real state only: new beat while the scene is still loading.
        if (!finishing && !reduced && now - beatStart >= BeatSeconds) NextBeat(true);
        AnimateSlots(now);

        // Message fades in/out inside each beat with a small rise.
        if (messageText != null && messageRect != null)
        {
            float b = now - beatStart;
            float a = reduced || finishing ? 1f : Mathf.Clamp01(b / .35f) * Mathf.Clamp01((BeatSeconds - b) / .3f);
            var mc = messageText.color; mc.a = a; messageText.color = mc;
            float rise = reduced || finishing ? 0f : (1f - Mathf.Clamp01(b / .35f)) * 10f;
            messageRect.anchoredPosition = new Vector2(0f, -38f - rise);
        }
        if (captionText != null)
        {
            float b = now - slotStart[currentSlot];
            float a = reduced ? 1f : Mathf.Clamp01((b - .25f) / .35f);
            if (slotPeel[currentSlot] < now) a = 0f;
            var cc = captionText.color; cc.a = a; captionText.color = cc;
        }

        AnimateLeavesAndSparkles(now);
        UpdateClose(now, 1f);
        CheckFailure(now);
    }

    void AnimateLeavesAndSparkles(float now)
    {
        if (leafRect == null) return;
        for (int i = 0; i < leafRect.Length; i++)
        {
            if (leafRect[i] == null) continue;
            float s = 1f, rot = 0f, alpha = .45f;
            Color tint = Color.white;
            if (readyAt >= 0f)
            {
                // Done: every leaf blossoms once, in sequence, and stays bright and golden.
                float k = Mathf.Clamp01((now - readyAt - i * .05f) / .35f);
                s = reduced ? 1.12f : Mathf.LerpUnclamped(1f, 1.18f, EaseOutBack(k));
                alpha = Mathf.Lerp(.55f, 1f, k);
                tint = Color.Lerp(Color.white, new Color(1f, .9f, .55f), k);
            }
            else if (!reduced)
            {
                // Indeterminate "wave" of leaves, not a progress bar.
                float w = Mathf.Max(0f, Mathf.Sin((now * .95f - i * .16f) * Mathf.PI * 2f));
                s = 1f + .30f * w;
                rot = Mathf.Sin((now * .8f + i * .5f) * Mathf.PI * 2f) * 6f;
                alpha = .45f + .55f * w;
            }
            leafRect[i].localScale = Vector3.one * s;
            leafRect[i].localRotation = Quaternion.Euler(0f, 0f, rot);
            var c = tint; c.a = alpha; leafImage[i].color = c;
        }
        if (sparkleRect != null)
        {
            for (int i = 0; i < sparkleRect.Length; i++)
            {
                float p = Mathf.Max(0f, Mathf.Sin((now * .55f + i * .5f) * Mathf.PI * 2f));
                sparkleRect[i].localScale = Vector3.one * (.6f + .5f * p);
                sparkleRect[i].localRotation = Quaternion.Euler(0f, 0f, now * 18f * (i == 0 ? 1f : -1f));
                var c = sparkleImage[i].color; c.a = p * .85f; sparkleImage[i].color = c;
            }
        }
    }

    // ------------------------------------------------------------------ finishing

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Real completion signal: the game scene has been loaded and activated by MenuFlow.
        if (mode != LoadSceneMode.Single || scene.buildIndex != MenuFlow.GameScene) return;
        changingScene = true;
        if (!finishing) StartCoroutine(FinishAfterFirstGameFrames());
    }

    IEnumerator FinishAfterFirstGameFrames()
    {
        finishing = true;
        readyAt = Time.unscaledTime;
        if (messageText != null) messageText.text = "Готово! Приятной игры.";
        if (titleText != null && fallbackMode) titleText.text = "Готово!";
        if (captionText != null) captionText.text = string.Empty;
        PlayClip(readyClip, .85f);
        yield return null;
        yield return null;                    // let the game scene run its first frames behind the overlay
        yield return new WaitForSecondsRealtime(reduced ? .05f : .5f);
        closeAt = Time.unscaledTime;
    }

    // If the game scene could not be opened (e.g. not in Build Settings), do not trap the player.
    void CheckFailure(float now)
    {
        if (changingScene || finishing || closeAt >= 0f) return;
        bool inMenu = SceneManager.GetActiveScene().buildIndex == MenuFlow.MainMenuScene ||
                      SceneManager.GetActiveScene().name == "MainMenu";
        if ((now - began > 1.25f && !MenuFlow.Busy && inMenu) || now - began > 120f) closeAt = now;
    }

    void UpdateClose(float now, float baseAlpha)
    {
        if (fallbackMode) CheckFailure(now);
        if (closeAt < 0f) { if (rootGroup != null && fallbackMode) rootGroup.alpha = baseAlpha; return; }
        float k = reduced ? 1f : Mathf.Clamp01((now - closeAt) / .42f);
        if (rootGroup != null) rootGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, k);
        if (k >= 1f) Destroy(gameObject);
    }
}

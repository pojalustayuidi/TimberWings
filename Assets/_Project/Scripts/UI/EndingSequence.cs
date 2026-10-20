// Assets/_Project/Scripts/UI/EndingSequence.cs (НЕ в Editor)
// Финальная заставка после достройки моста. Всё строится из кода, ассеты и настройка в сцене не нужны.
// Версия 2: в стиле игры (жирный шрифт HUD с тёмной обводкой, золотой градиент, панель и иконки из UiKit) и быстрее (около минуты).
//
// Что видит игрок:
//   1) экран уходит в чёрное с тёмно-синим (#1C2A3A) свечением снизу, мир затихает, играет пэд (синтез в коде);
//   2) короткие фразы сюжета: буквы по одной «влетают» снизу, раскалённо-белые, и остывают до своего цвета; плывут искры;
//   3) записка Гуннара на панели UiKit: четыре строки золотом, под каждой колокольчик;
//   4) вопрос-крючок, нарастающий «райзер», удар со встряской и вспышкой: название игры вбивается буквами;
//   5) «ПРОДОЛЖЕНИЕ СЛЕДУЕТ...»;
//   6) титры снизу вверх (автор, вуз, направление);
//   7) финальная карточка с кнопками «Вернуться в игру» и «Начать заново».
//
// Управление: Пробел, Enter или клик пропускают паузу (первые 2,5 с не работают); удержание Esc 1,2 с перематывает к финальной карточке.
// Пока заставка идёт, MobileInput.ModalOpen = true: персонаж стоит.
// Тексты: блок «ТЕКСТЫ» ниже. Скорость: поля fly / hold у каждой фразы и CreditsSpeed.
// Шрифт: берётся шрифт HUD (UIManager.objectiveText), как у диалогов. Символов, которых в нём нет (ё, «», —, …), заставка сама
// заменяет на безопасные (см. Fix), поэтому «квадратиков» быть не должно.
// Запуск: EndingSequence.Play(() => ...). Из StoryFlow вызывается сам (StoryFlow.useEnding). Проверка: Tools > BuilderSim > Показать финал.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiLayout;

public class EndingSequence : MonoBehaviour
{
    // true, пока заставка на экране (MobileInput.ModalOpen читает это)
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Active = false; }

    public static void Play(Action onDone)
    {
        if (Active) return;
        Active = true;
        var go = new GameObject("EndingSequence");
        var e = go.AddComponent<EndingSequence>();
        e.onDone = onDone;
    }

    // ============================================================ ТЕКСТЫ (правь здесь)
    public static string GameTitle = "TIMBER WINGS";
    public static string ToBeContinued = "ПРОДОЛЖЕНИЕ СЛЕДУЕТ";

    // палитра игры (UiKit): латунь, золото, тёмный navy
    static readonly Color GoldHi = new Color32(0xFF, 0xEC, 0xA8, 255);
    static readonly Color GoldLo = new Color32(0xF2, 0xB1, 0x34, 255);
    static readonly Color Gold = new Color32(0xFF, 0xD6, 0x6B, 255);
    static readonly Color Soft = new Color(0.90f, 0.93f, 0.98f, 1f);
    static readonly Color Dim = new Color(0.62f, 0.68f, 0.78f, 1f);
    static readonly Color Navy = new Color32(0x1C, 0x2A, 0x3A, 255);
    static readonly Color Outline = new Color32(0x0B, 0x10, 0x20, 255);

    // fly = сколько секунд буквы влетают, hold = сколько держится, rise = откуда снизу влетают (px), scale = с какого размера сжимаются
    class Beat
    {
        public string text;
        public float size, fly, hold, rise, scale;
        public Color color;
        public bool gold;
    }

    // сюжет до записки
    static readonly Beat[] StoryA =
    {
        new Beat { text = "МОСТ СТОИТ.", size = 128f, color = Color.white, gold = true, fly = 1.1f, hold = 1.0f, rise = 0f, scale = 2.4f },
        new Beat { text = "Шторм разнёс его в щепки.\nЛагерь собрал его заново.", size = 58f, color = Soft, fly = 1.1f, hold = 1.9f, rise = 46f, scale = 1.25f },
        new Beat { text = "Но за рекой — тишина.\nНи дыма над мастерской. Ни стука топора.", size = 54f, color = Soft, fly = 1.1f, hold = 2.0f, rise = 46f, scale = 1.25f },
        new Beat { text = "А в кронах кедров что-то скрипнуло,\nбудто раскрылись огромные крылья.", size = 54f, color = Soft, fly = 1.2f, hold = 2.1f, rise = 46f, scale = 1.25f },
        new Beat { text = "Под перилами моста — записка.\nПочерк Гуннара.", size = 54f, color = Soft, fly = 1.0f, hold = 1.5f, rise = 46f, scale = 1.25f }
    };

    // загадка Гуннара (4 строки, подпись отдельно)
    static readonly string[] Riddle =
    {
        "Рождён в лесу, но не растёт.",
        "Есть крылья, но не птица.",
        "Он ждёт, когда ветер придёт,",
        "и небо ему приснится."
    };
    const string RiddleSign = "— Гуннар";

    // вопрос-крючок перед названием
    static readonly Beat StoryB = new Beat { text = "Что строит Гуннар за рекой?", size = 84f, color = Color.white, gold = true, fly = 1.1f, hold = 1.5f, rise = 40f, scale = 1.5f };

    // титры. Теги TMP: <size>, <color>. Пустые строки = отступы
    const string CreditsText =
        "<size=34><color=#F2B134>ИГРА</color></size>\n" +
        "<size=112><color=#FFD66B>TIMBER WINGS</color></size>\n\n\n" +
        "<size=34><color=#F2B134>НАД ИГРОЙ РАБОТАЛ</color></size>\n" +
        "<size=92>Комаров Егор</size>\n" +
        "<size=44><color=#B9C3D6>группа 241-339</color></size>\n\n\n" +
        "<size=34><color=#F2B134>МОСКОВСКИЙ ПОЛИТЕХНИЧЕСКИЙ УНИВЕРСИТЕТ</color></size>\n" +
        "<size=50>Информационные системы и технологии</size>\n\n\n" +
        "<size=34><color=#F2B134>СДЕЛАНО НА</color></size>\n" +
        "<size=50>Unity 6</size>\n\n\n\n" +
        "<size=56>Спасибо, что дошли до конца моста.</size>\n" +
        "<size=40><color=#B9C3D6>Лес помнит всех, кто взялся за топор.</color></size>";

    const float CreditsSpeed = 130f;    // px/с (единицы Canvas 1920x1080)
    const float InputGrace = 2.5f;      // первые секунды Пробел/Enter/клик не пропускают паузы

    // ============================================================ состояние
    Action onDone;
    RectTransform root, emberRoot, textRoot;
    Image bg, glow, navyGlow;
    TMP_Text hint, finalLabel;
    CanvasGroup btnGroup;
    TMP_FontAsset font;
    readonly List<TMP_Text> live = new List<TMP_Text>();

    AudioSource padSrc, sfxSrc;
    AudioClip bellClip;
    float savedListener = 1f;
    bool ducked;

    bool skip, advance, leaving, finalShown, dotsOn, choiceMade;
    float age, skipHold, glowTarget, glowAlpha;
    float emberRate, emberAcc;
    bool reduceFx;

    static float Dt { get { return Time.unscaledDeltaTime; } }

    // ============================================================ жизненный цикл
    void Start()
    {
        reduceFx = VibeSettings.ReduceEffects;
        BuildUi();
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        if (ducked) { AudioListener.volume = savedListener; ducked = false; }
        Active = false;
    }

    void Update()
    {
        age += Dt;
        if (root == null) return;

        if (age > InputGrace && !finalShown)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0))
                advance = true;

            if (Input.GetKey(KeyCode.Escape)) skipHold += Dt; else skipHold = 0f;
            if (skipHold > 1.2f) skip = true;
        }
        if (finalShown && !leaving && btnGroup != null && btnGroup.interactable
            && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Choose(false);

        // подсказка внизу: появляется через 4 с, при удержании Esc ярче
        if (hint != null)
        {
            float a = (age > 4f && !finalShown) ? (skipHold > 0f ? 0.95f : 0.5f) : 0f;
            var c = hint.color;
            hint.color = new Color(c.r, c.g, c.b, Mathf.MoveTowards(c.a, a, Dt * 1.5f));
        }

        // свечение за названием
        glowAlpha = Mathf.MoveTowards(glowAlpha, glowTarget, Dt * 0.35f);
        if (glow != null)
        {
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 1.6f);
            glow.color = new Color(1f, 0.72f, 0.28f, glowAlpha * pulse);
        }

        // точки в «ПРОДОЛЖЕНИЕ СЛЕДУЕТ...»: невидимые точки держат центровку
        if (finalLabel != null && dotsOn)
        {
            int n = (int)(Time.unscaledTime * 1.6f) % 4;
            string dots = new string('.', n);
            string rest = new string('.', 3 - n);
            string s = ToBeContinued + dots + (rest.Length > 0 ? "<alpha=#00>" + rest + "</alpha>" : "");
            if (finalLabel.text != s) finalLabel.text = s;
        }

        TickEmbers(Dt);
    }

    // ============================================================ главный сценарий
    IEnumerator Run()
    {
        yield return null;
        SetupAudio();
        StartCoroutine(FadeListener(0.18f, 2.0f));

        yield return FadeBg(0f, 1f, 1.5f);
        emberRate = reduceFx ? 1.5f : 5f;

        StartPad();
        yield return Wait(0.7f);

        for (int i = 0; i < StoryA.Length && !skip; i++) yield return PlayBeat(StoryA[i], i == 0);
        if (!skip) yield return PlayRiddle();
        if (!skip)
        {
            // райзер длится 4,6 с, удар названия должен прийтись на его конец
            float t0 = age;
            PlayRiser();
            yield return PlayBeat(StoryB, false);
            yield return WaitHard(Mathf.Max(0f, 4.5f - (age - t0)));
        }
        if (!skip) yield return TitleCard();
        if (!skip) yield return Credits();

        skip = false;
        yield return FinalCard();
    }

    // пауза: Пробел/Enter/клик её пропускают
    IEnumerator Wait(float s)
    {
        advance = false;
        float t = 0f;
        while (t < s && !skip && !advance) { t += Dt; yield return null; }
        advance = false;
    }

    // пауза, которую нельзя пропустить кликом (нужна, чтобы удар совпал со звуком)
    IEnumerator WaitHard(float s)
    {
        float t = 0f;
        while (t < s && !skip) { t += Dt; yield return null; }
        advance = false;
    }

    IEnumerator PlayBeat(Beat b, bool slam)
    {
        var t = NewText(textRoot, b.text, b.size, b.color, Vector2.zero, 1600f, b.gold);
        if (slam)
        {
            PlayBell(0.5f);
            if (!reduceFx) StartCoroutine(Shake(8f, 0.45f));
        }
        yield return Fly(t, b.fly, b.rise, b.scale);
        yield return Wait(b.hold);
        yield return FadeOut(new[] { t }, 0.5f);
        Kill(t);
        yield return Wait(0.12f);
    }

    IEnumerator PlayRiddle()
    {
        // записка Гуннара лежит на панели из UiKit, как окна игры
        var card = UiKit.MakePanel(textRoot, new Vector2(1260f, 700f), false, 1.4f, "RiddleCard");
        var crt = card.rectTransform;
        Anch(crt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1260f, 700f));
        var cg = card.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        var icon = UiKit.MakeIcon(card.transform, UiIcon.Axe, 96f, "RiddleAxe");
        Anch(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 275f), new Vector2(96f, 96f));

        float k = 0f;
        while (k < 1f)
        {
            k += Dt / 0.4f;
            if (skip) k = 1f;
            float e = Mathf.Clamp01(k);
            cg.alpha = e;
            crt.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, UiDraw.EaseOutBack(e));
            yield return null;
        }
        cg.alpha = 1f;
        crt.localScale = Vector3.one;

        float[] ys = { 140f, 55f, -30f, -115f };
        float[] pitch = { 1f, 1.122f, 1.26f, 1.5f };

        for (int i = 0; i < Riddle.Length && !skip; i++)
        {
            var t = NewText(card.transform, Riddle[i], 56f, Color.white, new Vector2(0f, ys[i]), 1100f, true);
            PlayBell(pitch[i]);
            yield return Fly(t, 0.9f, 40f, 1.4f);
            yield return Wait(0.2f);
        }
        if (!skip)
        {
            var sign = NewText(card.transform, RiddleSign, 40f, Soft, new Vector2(0f, -225f), 700f, false);
            yield return Fly(sign, 0.7f, 20f, 1.2f);
        }
        yield return Wait(2.4f);

        k = 0f;
        while (k < 1f)
        {
            k += Dt / 0.6f;
            if (skip) k = 1f;
            cg.alpha = 1f - Mathf.Clamp01(k);
            yield return null;
        }
        Destroy(card.gameObject);
        live.RemoveAll(x => x == null);
        yield return Wait(0.2f);
    }

    IEnumerator TitleCard()
    {
        PlayBoom();
        glowTarget = 0.30f;
        emberRate = reduceFx ? 4f : 18f;
        if (!reduceFx)
        {
            StartCoroutine(Flare(80f));
            StartCoroutine(Shake(14f, 0.7f));
        }

        // название вбивается по буквам, с огромного размера
        var title = NewText(textRoot, GameTitle, 176f, Color.white, new Vector2(0f, 80f), 1800f, true);
        yield return Fly(title, 1.6f, 0f, 2.6f);

        // золотая линия растёт в стороны от топора
        var c = new Vector2(0.5f, 0.5f);
        var lineL = UiKit.MakeImage(textRoot, "TitleLineL", null, false);
        lineL.color = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
        Anch(lineL.rectTransform, c, new Vector2(1f, 0.5f), new Vector2(-62f, -38f), new Vector2(0f, 5f));
        var lineR = UiKit.MakeImage(textRoot, "TitleLineR", null, false);
        lineR.color = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
        Anch(lineR.rectTransform, c, new Vector2(0f, 0.5f), new Vector2(62f, -38f), new Vector2(0f, 5f));
        var axe = UiKit.MakeIcon(textRoot, UiIcon.Axe, 84f, "TitleAxe");
        Anch(axe.rectTransform, c, c, new Vector2(0f, -38f), new Vector2(84f, 84f));
        axe.color = new Color(1f, 1f, 1f, 0f);

        float k = 0f;
        while (k < 1f)
        {
            k += Dt / 0.9f;
            if (skip) k = 1f;
            float e = UiDraw.EaseOutCubic(Mathf.Clamp01(k));
            float w = 520f * e;
            lineL.rectTransform.sizeDelta = new Vector2(w, 5f);
            lineR.rectTransform.sizeDelta = new Vector2(w, 5f);
            axe.color = new Color(1f, 1f, 1f, e);
            axe.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, UiDraw.EaseOutBack(Mathf.Clamp01(k)));
            yield return null;
        }

        var sub = NewText(textRoot, ToBeContinued + "...", 64f, Soft, new Vector2(0f, -150f), 1500f, false);
        PlayBell(0.75f);
        yield return Fly(sub, 1.1f, 30f, 1.25f);

        yield return Wait(3.0f);

        var both = new List<TMP_Text> { title, sub };
        glowTarget = 0f;
        yield return FadeOut(both, 1.0f);
        Kill(title); Kill(sub);
        if (lineL != null) Destroy(lineL.gameObject);
        if (lineR != null) Destroy(lineR.gameObject);
        if (axe != null) Destroy(axe.gameObject);
        emberRate = reduceFx ? 1.5f : 6f;
        yield return Wait(0.3f);
    }

    IEnumerator Credits()
    {
        var t = UiKit.MakeLabel(textRoot, Fix(CreditsText), 44f, Color.white, TextAlignmentOptions.Top, true);
        StyleOutline(t);
        t.richText = true;
        t.lineSpacing = 12f;
        t.raycastTarget = false;
        live.Add(t);

        const float W = 1400f;
        Vector2 pref = t.GetPreferredValues(Fix(CreditsText), W, 0f);
        var rt = t.rectTransform;
        Anch(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(W, pref.y));

        float canvasH = root.rect.height;
        float y = -canvasH;
        float end = pref.y + 40f;
        rt.anchoredPosition = new Vector2(0f, y);

        // мягкое затухание краёв
        var top = MakeFade(true);
        var bottom = MakeFade(false);

        advance = false;
        while (y < end && !skip)
        {
            float speed = CreditsSpeed * (advance ? 4f : 1f);   // Пробел/клик ускоряет
            y += speed * Dt;
            if (advance && !(Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetMouseButton(0))) advance = false;
            rt.anchoredPosition = new Vector2(0f, y);
            yield return null;
        }

        Kill(t);
        if (top != null) Destroy(top.gameObject);
        if (bottom != null) Destroy(bottom.gameObject);
        yield return Wait(0.3f);
    }

    IEnumerator FinalCard()
    {
        ClearAll();
        glowTarget = 0.16f;
        emberRate = reduceFx ? 2f : 7f;

        finalLabel = NewText(textRoot, ToBeContinued + "...", 92f, Color.white, new Vector2(0f, 70f), 1800f, true);
        finalLabel.richText = true;
        var year = NewText(textRoot, GameTitle + "  -  2026", 38f, Dim, new Vector2(0f, -35f), 1200f, false);
        finalShown = true;                                  // с этого момента ввод паузы не слушаем
        StartCoroutine(Pop(year, 1.2f, 1.0f));
        yield return Fly(finalLabel, 1.5f, 40f, 1.5f);
        dotsOn = true;                                      // теперь Update гоняет точки

        yield return Wait(0.9f);
        BuildButtons();
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / 0.7f;
            btnGroup.alpha = Mathf.Clamp01(k);
            yield return null;
        }
        btnGroup.interactable = true;
        btnGroup.blocksRaycasts = true;
    }

    // ============================================================ выход
    void Choose(bool restart)
    {
        if (leaving || !finalShown || choiceMade) return;
        choiceMade = true;
        StartCoroutine(Leave(restart));
    }

    IEnumerator Leave(bool restart)
    {
        leaving = true;
        btnGroup.interactable = false;
        StartCoroutine(FadeVolume(padSrc, 0f, 1.6f));

        if (restart)
        {
            yield return FadeOut(live, 0.8f);
            yield return new WaitForSecondsRealtime(0.4f);
            if (ducked) { AudioListener.volume = savedListener; ducked = false; }
            Active = false;
            Time.timeScale = 1f;
            var sc = SceneManager.GetActiveScene();
            if (sc.buildIndex >= 0) SceneManager.LoadScene(sc.buildIndex); else SceneManager.LoadScene(sc.name);
            yield break;
        }

        // назад в игру: текст гаснет, чёрный экран уходит, звук мира возвращается
        StartCoroutine(FadeListener(savedListener, 1.6f));
        glowTarget = 0f;
        emberRate = 0f;
        dotsOn = false;
        btnGroup.alpha = 0f;
        yield return FadeOut(live, 0.9f);
        yield return FadeBg(1f, 0f, 1.6f);

        if (ducked) { AudioListener.volume = savedListener; ducked = false; }
        Active = false;
        if (onDone != null) onDone();
        Destroy(gameObject);
    }

    // ============================================================ анимации текста
    // Буквы влетают по одной слева направо: снизу вверх, с большого размера, раскалённо-белыми, остывают до своего цвета.
    IEnumerator Fly(TMP_Text t, float dur, float rise, float scaleFrom)
    {
        if (t == null) yield break;
        const float spread = 0.55f;                       // доля времени, на которую растянут старт первой и последней буквы
        float total = Mathf.Max(0.1f, dur);
        t.alpha = 1f;
        t.ForceMeshUpdate();
        int n = Mathf.Max(1, t.textInfo.characterCount);
        float stagger = n > 1 ? total * spread / (n - 1) : 0f;
        float per = Mathf.Max(0.1f, total * (1f - spread));
        float full = stagger * (n - 1) + per;

        float k = 0f;
        while (k < full && !skip)
        {
            k += Dt;
            AnimateChars(t, k, stagger, per, rise, scaleFrom);
            yield return null;
        }
        if (t != null)
        {
            t.alpha = 1f;
            t.ForceMeshUpdate();                          // чистая итоговая раскладка
        }
    }

    void AnimateChars(TMP_Text t, float time, float stagger, float per, float rise, float scaleFrom)
    {
        if (t == null) return;
        t.ForceMeshUpdate();
        var ti = t.textInfo;
        for (int i = 0; i < ti.characterCount; i++)
        {
            var ci = ti.characterInfo[i];
            if (!ci.isVisible) continue;
            float x = Mathf.Clamp01((time - i * stagger) / per);
            float cub = UiDraw.EaseOutCubic(x);
            float sc = Mathf.Lerp(scaleFrom, 1f, UiDraw.EaseOutBack(x));
            float alpha = Mathf.Clamp01(x * 2.5f);

            int mi = ci.materialReferenceIndex, vi = ci.vertexIndex;
            var verts = ti.meshInfo[mi].vertices;
            var cols = ti.meshInfo[mi].colors32;
            Vector3 center = (verts[vi] + verts[vi + 2]) * 0.5f;
            Vector3 off = new Vector3(0f, -rise * (1f - cub), 0f);
            for (int j = 0; j < 4; j++)
            {
                verts[vi + j] = center + (verts[vi + j] - center) * sc + off;
                Color32 baseCol = cols[vi + j];
                Color32 hot = Color32.Lerp(new Color32(255, 255, 255, 255), baseCol, cub);
                hot.a = (byte)(baseCol.a * alpha);
                cols[vi + j] = hot;
            }
        }
        t.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    // простое появление: прозрачность и размер
    IEnumerator Pop(TMP_Text t, float dur, float scaleFrom)
    {
        if (t == null) yield break;
        var rt = t.rectTransform;
        t.alpha = 0f;
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / Mathf.Max(0.05f, dur);
            if (skip) k = 1f;
            if (t == null) yield break;
            float e = UiDraw.EaseOutCubic(Mathf.Clamp01(k));
            t.alpha = e;
            rt.localScale = Vector3.one * Mathf.Lerp(scaleFrom, 1f, e);
            yield return null;
        }
        if (t != null) { t.alpha = 1f; rt.localScale = Vector3.one; }
    }

    IEnumerator FadeOut(IList<TMP_Text> ts, float dur)
    {
        var a0 = new float[ts.Count];
        for (int i = 0; i < ts.Count; i++) a0[i] = ts[i] != null ? ts[i].alpha : 0f;
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / Mathf.Max(0.05f, dur);
            if (skip) k = 1f;
            float f = 1f - Mathf.Clamp01(k);
            for (int i = 0; i < ts.Count; i++) if (ts[i] != null) ts[i].alpha = a0[i] * f;
            yield return null;
        }
    }

    IEnumerator FadeBg(float from, float to, float dur)
    {
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / Mathf.Max(0.05f, dur);
            SetBgAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(k)));
            yield return null;
        }
        SetBgAlpha(to);
    }

    void SetBgAlpha(float a)
    {
        bg.color = new Color(0f, 0f, 0f, a);
        if (navyGlow != null) navyGlow.color = new Color(Navy.r, Navy.g, Navy.b, a * 0.75f);
    }

    // встряска всего текста (удар названия)
    IEnumerator Shake(float amp, float dur)
    {
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / Mathf.Max(0.05f, dur);
            float a = amp * (1f - Mathf.Clamp01(k));
            if (textRoot != null) textRoot.anchoredPosition = new Vector2(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a));
            yield return null;
        }
        if (textRoot != null) textRoot.anchoredPosition = Vector2.zero;
    }

    // вспышка-полоса через весь экран в момент появления названия
    IEnumerator Flare(float y)
    {
        var img = UiKit.MakeImage(textRoot, "Flare", SoftCircle(), false);
        img.color = new Color(1f, 0.92f, 0.65f, 0f);
        var rt = img.rectTransform;
        Anch(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(200f, 60f));
        rt.SetAsFirstSibling();
        float k = 0f;
        while (k < 1f)
        {
            k += Dt / 1.6f;
            float e = UiDraw.EaseOutCubic(Mathf.Clamp01(k));
            rt.sizeDelta = new Vector2(Mathf.Lerp(200f, 2400f, e), Mathf.Lerp(80f, 14f, e));
            img.color = new Color(1f, 0.92f, 0.65f, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.85f);
            yield return null;
        }
        Destroy(img.gameObject);
    }

    // ============================================================ UI
    void BuildUi()
    {
        font = UiKit.ResolveFont(null);   // шрифт HUD с кириллицей, как у диалогов
        if (font == null) Debug.LogWarning("EndingSequence: не найден шрифт HUD (UIManager.objectiveText), текст может быть без кириллицы. Можно задать UiKit.DefaultFont.");

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var cgo = new GameObject("EndingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;     // выше всего: HUD, диалоги, окна
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        root = (RectTransform)cgo.transform;

        // чёрный экран; перехватывает клики, чтобы игра под ним не реагировала
        bg = UiKit.MakeImage(root, "Black", null, false);
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;
        Stretch(bg.rectTransform);

        // тёмно-синее (#1C2A3A) свечение снизу: цвет интерфейса игры
        navyGlow = UiKit.MakeImage(root, "NavyGlow", SoftCircle(), false);
        navyGlow.color = new Color(Navy.r, Navy.g, Navy.b, 0f);
        Anch(navyGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(2800f, 1300f));

        glow = UiKit.MakeImage(root, "Glow", SoftCircle(), false);
        glow.color = new Color(1f, 0.72f, 0.28f, 0f);
        Anch(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1700f, 820f));

        emberRoot = NewRT("Embers", root);
        Stretch(emberRoot);
        textRoot = NewRT("Text", root);
        Stretch(textRoot);

        hint = UiKit.MakeLabel(root, Fix("Пробел — дальше,  удерживайте Esc — пропустить"), 26f, Dim, TextAlignmentOptions.Center, true);
        StyleOutline(hint);
        Anch(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1200f, 40f));
        hint.color = new Color(Dim.r, Dim.g, Dim.b, 0f);
    }

    // жирный шрифт HUD с тёмной обводкой: как диалоги и плашки наград
    static void StyleOutline(TMP_Text t)
    {
        t.outlineWidth = 0.22f;
        t.outlineColor = Outline;
    }

    TMP_Text NewText(Transform parent, string text, float size, Color color, Vector2 pos, float width, bool goldGradient)
    {
        var t = UiKit.MakeLabel(parent, Fix(text), size, color, TextAlignmentOptions.Center, true);
        t.fontStyle = FontStyles.Bold;                 // без курсива: на мультяшном шрифте он выглядит сломанным
        t.richText = false;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.characterSpacing = 0f;
        StyleOutline(t);
        if (goldGradient)
        {
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(GoldHi, GoldHi, GoldLo, GoldLo);
        }
        Anch(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, size * 5f));
        live.Add(t);
        return t;
    }

    // заменяет символы, которых нет в шрифте HUD (иначе вместо них квадраты)
    string Fix(string s)
    {
        if (font == null || string.IsNullOrEmpty(s)) return s;
        s = Rep(s, 'ё', "е");
        s = Rep(s, 'Ё', "Е");
        s = Rep(s, '…', "...");
        s = Rep(s, '—', "-");
        s = Rep(s, '«', "\"");
        s = Rep(s, '»', "\"");
        s = Rep(s, '·', "-");
        return s;
    }

    string Rep(string s, char ch, string alt)
    {
        if (s.IndexOf(ch) < 0) return s;
        if (font.HasCharacter(ch, true, true)) return s;
        return s.Replace(ch.ToString(), alt);
    }

    void Kill(TMP_Text t)
    {
        if (t == null) return;
        live.Remove(t);
        Destroy(t.gameObject);
    }

    void ClearAll()
    {
        for (int i = live.Count - 1; i >= 0; i--) if (live[i] != null) Destroy(live[i].gameObject);
        live.Clear();
    }

    Image MakeFade(bool top)
    {
        var img = UiKit.MakeImage(root, top ? "FadeTop" : "FadeBottom", GradientSprite(), false);
        img.color = Color.black;
        var rt = img.rectTransform;
        float h = 300f;
        if (top) { Anch(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, h)); rt.localScale = new Vector3(1f, -1f, 1f); }
        else Anch(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, h));
        hint.transform.SetAsLastSibling();
        return img;
    }

    void BuildButtons()
    {
        var go = NewRT("Buttons", root);
        Stretch(go);
        btnGroup = go.gameObject.AddComponent<CanvasGroup>();
        btnGroup.alpha = 0f;
        btnGroup.interactable = false;
        btnGroup.blocksRaycasts = false;

        var back = UiKit.MakeButton(go, "Вернуться в игру", UiBtn.Green, new Vector2(470f, 86f), 34f, 1f);
        Anch(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-255f, -250f), new Vector2(470f, 86f));
        back.navigation = new Navigation { mode = Navigation.Mode.None };
        back.onClick.AddListener(() => Choose(false));
        var bt = back.GetComponentInChildren<TMP_Text>();
        bt.color = UiKit.Navy; bt.enableAutoSizing = true; bt.fontSizeMin = 18f; bt.fontSizeMax = 34f;

        var again = UiKit.MakeButton(go, "Начать заново", UiBtn.Gray, new Vector2(380f, 86f), 34f, 1f);
        Anch(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, -250f), new Vector2(380f, 86f));
        again.navigation = new Navigation { mode = Navigation.Mode.None };
        again.onClick.AddListener(() => Choose(true));
        var at = again.GetComponentInChildren<TMP_Text>();
        at.color = Color.white; at.enableAutoSizing = true; at.fontSizeMin = 18f; at.fontSizeMax = 34f;

        hint.transform.SetAsLastSibling();
    }

    // ============================================================ искры
    class Ember
    {
        public RectTransform rt;
        public Image img;
        public bool alive;
        public float x, y, vy, amp, freq, phase, a;
        public Color col;
    }

    readonly List<Ember> embers = new List<Ember>();
    static readonly Color[] EmberColors =
    {
        new Color(1f, 0.84f, 0.42f), new Color(1f, 0.62f, 0.25f), new Color(1f, 0.95f, 0.78f), new Color(0.95f, 0.50f, 0.20f),
        new Color(0.60f, 0.88f, 0.50f)   // редкие зелёные светлячки кедрового леса
    };

    void TickEmbers(float dt)
    {
        if (emberRoot == null) return;
        Vector2 sz = root.rect.size;

        emberAcc += emberRate * dt;
        while (emberAcc >= 1f) { emberAcc -= 1f; SpawnEmber(sz); }

        float now = Time.unscaledTime;
        for (int i = 0; i < embers.Count; i++)
        {
            var e = embers[i];
            if (!e.alive) continue;
            e.y += e.vy * dt;
            if (e.y > sz.y + 30f) { e.alive = false; e.rt.gameObject.SetActive(false); continue; }

            float x = e.x + Mathf.Sin(now * e.freq + e.phase) * e.amp;
            float edge = Mathf.Clamp01(e.y / 120f) * Mathf.Clamp01((sz.y - e.y) / 220f);
            float tw = 0.65f + 0.35f * Mathf.Sin(now * e.freq * 3f + e.phase);
            e.rt.anchoredPosition = new Vector2(x, e.y);
            e.img.color = new Color(e.col.r, e.col.g, e.col.b, e.a * edge * tw);
        }
    }

    void SpawnEmber(Vector2 sz)
    {
        Ember e = null;
        for (int i = 0; i < embers.Count; i++) if (!embers[i].alive) { e = embers[i]; break; }
        if (e == null)
        {
            if (embers.Count >= 90) return;
            var img = UiKit.MakeImage(emberRoot, "Ember", SoftCircle(), false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            e = new Ember { rt = rt, img = img };
            embers.Add(e);
        }
        e.alive = true;
        e.x = UnityEngine.Random.Range(0f, sz.x);
        e.y = -10f;
        e.vy = UnityEngine.Random.Range(35f, 115f);
        e.amp = UnityEngine.Random.Range(15f, 70f);
        e.freq = UnityEngine.Random.Range(0.4f, 1.3f);
        e.phase = UnityEngine.Random.Range(0f, 6.28f);
        e.a = UnityEngine.Random.Range(0.35f, 0.9f);
        e.col = UnityEngine.Random.value < 0.15f ? EmberColors[4] : EmberColors[UnityEngine.Random.Range(0, 4)];
        float s = UnityEngine.Random.Range(6f, 20f);
        e.rt.sizeDelta = new Vector2(s, s);
        e.rt.gameObject.SetActive(true);
    }

    // ============================================================ спрайты
    static Sprite softCircle, gradient;

    static Sprite SoftCircle()
    {
        if (softCircle != null) return softCircle;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                a = Mathf.Pow(a, 1.4f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        softCircle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return softCircle;
    }

    // сверху вниз: непрозрачный низ (y = 0) переходит в прозрачный верх
    static Sprite GradientSprite()
    {
        if (gradient != null) return gradient;
        const int H = 64;
        var tex = new Texture2D(2, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[2 * H];
        for (int y = 0; y < H; y++)
        {
            float a = 1f - y / (float)(H - 1);
            a = a * a * (3f - 2f * a);
            for (int x = 0; x < 2; x++) px[y * 2 + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        gradient = Sprite.Create(tex, new Rect(0, 0, 2, H), new Vector2(0.5f, 0.5f), 100f);
        return gradient;
    }

    // ============================================================ звук (всё синтезируется в коде, файлов не нужно)
    // раньше тут было Max(0.35, громкость звуков): при «Звуки 0%» и «Звук ВЫКЛ» финал всё равно гремел. Теперь настройки уважаются.
    float Vol() { return GameAudio.SfxLevel; }
    float PadVol() { return Mathf.Clamp01(GameAudio.MusicLevel * 0.8f); }   // подложка финала = музыка

    void SetupAudio()
    {
        savedListener = AudioListener.volume;
        ducked = true;

        padSrc = gameObject.AddComponent<AudioSource>();
        padSrc.playOnAwake = false; padSrc.loop = true; padSrc.spatialBlend = 0f; padSrc.ignoreListenerVolume = true; padSrc.volume = 0f;
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false; sfxSrc.spatialBlend = 0f; sfxSrc.ignoreListenerVolume = true;
    }

    void StartPad()
    {
        padSrc.clip = MakePad();
        padSrc.Play();
        StartCoroutine(FadeVolume(padSrc, 0.5f * PadVol(), 4f));
    }

    void PlayBell(float pitch)
    {
        if (bellClip == null) bellClip = MakeBell();
        sfxSrc.pitch = pitch;
        sfxSrc.PlayOneShot(bellClip, 0.55f * Vol());
    }

    void PlayRiser()
    {
        sfxSrc.pitch = 1f;
        sfxSrc.PlayOneShot(MakeRiser(), 0.7f * Vol());
    }

    void PlayBoom()
    {
        sfxSrc.pitch = 1f;
        sfxSrc.PlayOneShot(MakeBoom(), 1.0f * Vol());
    }

    IEnumerator FadeVolume(AudioSource s, float to, float time)
    {
        if (s == null) yield break;
        float from = s.volume, k = 0f;
        while (k < 1f && s != null)
        {
            k += Dt / Mathf.Max(0.05f, time);
            s.volume = Mathf.Lerp(from, to, Mathf.Clamp01(k));
            yield return null;
        }
    }

    // громкость мира (музыка, шаги, природа) на время заставки приглушена, моя музыка идёт мимо неё (ignoreListenerVolume)
    IEnumerator FadeListener(float to, float time)
    {
        float from = AudioListener.volume, k = 0f;
        while (k < 1f)
        {
            k += Dt / Mathf.Max(0.05f, time);
            if (ducked || to >= savedListener) AudioListener.volume = Mathf.Lerp(from, to, Mathf.Clamp01(k));
            yield return null;
        }
    }

    // Аккорды Am, F, C, G, плавно перетекают друг в друга (окна sin², сумма всегда 1). Частоты подогнаны так, чтобы петля 16 с не щёлкала
    static AudioClip MakePad()
    {
        const float L = 16f, P = 4f, W = 8f;
        float[][] chords =
        {
            new[] { 110f, 164.81f, 220f, 261.63f, 329.63f },
            new[] { 87.31f, 130.81f, 174.61f, 220f, 261.63f },
            new[] { 130.81f, 196f, 261.63f, 329.63f, 392f },
            new[] { 98f, 146.83f, 196f, 246.94f, 293.66f }
        };
        for (int c = 0; c < chords.Length; c++)
            for (int i = 0; i < chords[c].Length; i++)
                chords[c][i] = Mathf.Round(chords[c][i] * L) / L;   // целое число периодов за петлю

        return VibeSynth.Make("EndingPad", L, t =>
        {
            float s = 0f;
            for (int k = 0; k < 4; k++)
            {
                float ph = Mathf.Repeat(t - k * P, L);
                if (ph >= W) continue;
                float w = Mathf.Sin(Mathf.PI * ph / W);
                w *= w;
                var ch = chords[k];
                float c = 0f;
                for (int i = 0; i < ch.Length; i++)
                {
                    float amp = i == 0 ? 1.2f : 0.55f;
                    c += amp * (VibeSynth.Sin(ch[i], t) * 0.7f + VibeSynth.Sin(ch[i] * 2f, t) * 0.2f);
                }
                s += w * c;
            }
            return s * (0.85f + 0.15f * VibeSynth.Sin(0.125f, t));
        }, 0.7f);
    }

    static AudioClip MakeBell()
    {
        return VibeSynth.Make("EndingBell", 2.4f, t =>
            VibeSynth.Bell(659.25f, t, 0.9f) + 0.5f * VibeSynth.Bell(987.77f, t, 0.6f) + 0.25f * VibeSynth.Bell(1318.5f, t, 0.4f), 0.8f);
    }

    // нарастающий свист с шумом перед ударом
    static AudioClip MakeRiser()
    {
        const float D = 4.6f;
        float r = 7.5f, lr = Mathf.Log(r), f0 = 180f;
        return VibeSynth.Make("EndingRiser", D, t =>
        {
            float u = t / D;
            float phase = 2f * Mathf.PI * f0 * D / lr * (Mathf.Pow(r, u) - 1f);
            float tone = Mathf.Sin(phase) * u * u;
            float noise = VibeSynth.Noise() * u * u * u * 0.35f;
            return tone + noise;
        }, 0.8f);
    }

    // глубокий удар: падающий тон и короткий шумовой всплеск
    static AudioClip MakeBoom()
    {
        return VibeSynth.Make("EndingBoom", 3.6f, t =>
        {
            float phase = 2f * Mathf.PI * (28f * t + (70f / 0.6f) * (1f - Mathf.Exp(-0.6f * t)));
            float body = Mathf.Sin(phase) * VibeSynth.Env(t, 0.01f, 1.1f);
            float hit = VibeSynth.Noise() * VibeSynth.Env(t, 0.002f, 0.12f) * 0.5f;
            return body + hit;
        }, 0.95f);
    }

    // ============================================================ помощники разметки

}

// Assets/_Project/Scripts/Vibe/BuildReveal.cs (НЕ в Editor)
// Показ готовой стадии здания («reveal»). Создаётся сам: BuildingController зовёт BuildReveal.Ensure(), когда сдан этап (руками в сцену ничего добавлять не нужно).
//
// Что происходит, пока BuildingController.IsRevealing (GameBalance.RevealTime, у последней стадии RevealTimeFinal):
//   - площадка заблокирована, игрок замер (MobileInput.ModalOpen читает BuildingController.AnyRevealing), эту часть ведёт контроллер;
//   - камера плавно наезжает на здание (CameraVibe.SetReveal: zoomBias GameBalance.RevealZoom) и чуть обходит его (~25°),
//     по краям экрана тёмная виньетка;
//   - внизу плашка «Хижина · стадия 2/3» и название этапа (у последней стадии «Построено!»);
//   - пыль и подпрыгивание здания играет BuildFxHook;
//   - E, Пробел, Esc (и клик) пропускают показ после RevealSkipGrace: камера ещё RevealSkipTail секунд возвращается.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): без облёта, наезда и виньетки, остаются пауза и плашка.
//
// После показа, если у игрока уже хватает ресурсов на следующую стадию, внизу появляется подсказка
// «Следующая стадия готова к постройке». Окно стройки само не открывается: игрок жмёт E.
//
// Время показа ведёт BuildingController, а не этот скрипт: блокировка площадки работает, даже если эффекты сломаны.
// Конец показа: GameEvents.BuildRevealFinished (StoryFlow ждёт его перед следующим заданием).
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public class BuildReveal : MonoBehaviour
{
    public static BuildReveal Instance { get; private set; }

    // идёт ли показ хоть на одной стройке (то же, что читает MobileInput.ModalOpen)
    public static bool Active { get { return BuildingController.AnyRevealing; } }

    // BuildingController зовёт перед событием BuildRevealStarted: если компонента ещё нет, создаём (он сам подпишется в OnEnable)
    public static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("_BuildReveal");
        go.AddComponent<BuildReveal>();   // Awake выставит Instance
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; vignette = null; }

    // ------------------------------------------------------------ размеры (единицы Canvas 1920x1080) и цвета
    const float PlaqueW = 460f, PlaqueH = 88f, PlaqueY = 70f;     // плашка стадии: компактная, внизу по центру (не закрывает здание)
    const float HintW = 430f, HintH = 58f, HintY = 70f;           // подсказка «Следующая стадия готова» (на месте плашки: они не живут одновременно)
    const float PlaqueFadeIn = 0.25f, PlaqueFadeOut = 0.4f;       // появление и уход плашки, секунды
    const float VignetteNormal = 0.55f, VignetteFinal = 0.8f;        // сила виньетки (доля альфы спрайта)

    static readonly Color Amber = new Color32(0xFF, 0xD6, 0x6B, 255);
    static readonly Color Dim = new Color(0.50f, 0.56f, 0.66f, 1f);

    // ------------------------------------------------------------ состояние
    BuildingController bc;        // здание, которое показываем сейчас (null вне показа)
    bool finalReveal;
    Vector3 point;                // куда смотрит камера
    float radius;                 // размер здания, метры (для свободной камеры)
    float sweepDir = 1f;          // направление облёта; на каждой стадии меняется, чтобы камера не крутилась в одну сторону
    float skipShown;              // плавность подсказки «E: пропустить»
    PlayerInventory inv;

    BuildingController hintFor;   // у какой стройки показываем «Следующая стадия готова»
    float hintUntil, hintK;

    // ------------------------------------------------------------ UI
    bool built;
    GameObject canvasGo;
    CanvasGroup vignetteGroup, plaqueGroup, skipGroup, hintGroup;
    RectTransform plaqueRt, hintRt;
    TMP_Text line1, line2, skipText;
    static Sprite vignette;

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { Instance = this; }

    void OnEnable()
    {
        GameEvents.BuildRevealStarted += OnRevealStarted;
        GameEvents.BuildRevealFinished += OnRevealFinished;
    }

    void OnDisable()
    {
        GameEvents.BuildRevealStarted -= OnRevealStarted;
        GameEvents.BuildRevealFinished -= OnRevealFinished;
        CameraVibe.ClearReveal();
    }

    void OnDestroy()
    {
        CameraVibe.ClearReveal();
        if (Instance == this) Instance = null;
        if (canvasGo != null) Destroy(canvasGo);
    }

    // ------------------------------------------------------------ события
    void OnRevealStarted(BuildingController b)
    {
        if (b == null) return;
        bc = b;
        finalReveal = b.RevealIsFinal;
        point = FocusPoint(b, out radius);
        sweepDir = -sweepDir;
        skipShown = 0f;
        hintFor = null;                 // новая стадия: старая подсказка не нужна
        EnsureBuilt();
        FillPlaque(b);
    }

    void OnRevealFinished(BuildingController b)
    {
        if (b == null || b != bc) return;
        CameraVibe.ClearReveal();
        var done = bc;
        bc = null;

        // хватает ресурсов на следующую стадию: подсказка. Окно само не открывается
        if (done.IsFinished || !done.PlayerInside) return;
        if (inv == null) inv = PlayerInventory.Instance;
        if (done.CanAffordNext(inv)) ShowHint(done);
    }

    // ------------------------------------------------------------ кадр
    void Update()
    {
        float amount = 0f, plaqueA = 0f, skipA = 0f;

        if (bc != null)
        {
            if (!bc.IsRevealing)
            {
                // стройку выключили посреди показа: событие не придёт, закругляемся сами (без подсказки)
                CameraVibe.ClearReveal();
                bc = null;
            }
            else
            {
                float t = bc.RevealElapsed, left = bc.RevealLeft, dur = Mathf.Max(0.01f, bc.RevealDuration);

                if (SkipPressed()) bc.SkipReveal();   // раньше RevealSkipGrace контроллер пропуск не примет

                // наезд в начале и возврат в конце; облёт от -half до +half за весь показ, затухает вместе с наездом
                amount = Mathf.Min(Smooth01(t / GameBalance.RevealCamIn), Smooth01(left / GameBalance.RevealCamOut));
                if (!VibeSettings.ReduceEffects)
                {
                    // облёт с плавным стартом и финишем (smoothstep), без рывка на постоянной скорости
                    float sweep = Mathf.Lerp(-0.5f, 0.5f, Smooth01(t / dur)) * GameBalance.RevealOrbitDegrees * sweepDir;
                    CameraVibe.SetReveal(point, amount, sweep * amount, radius);
                }
                else CameraVibe.ClearReveal();

                plaqueA = Mathf.Min(Mathf.Clamp01(t / PlaqueFadeIn), Mathf.Clamp01(left / PlaqueFadeOut));
                skipA = (t >= GameBalance.RevealSkipGrace && left > GameBalance.RevealSkipTail) ? 1f : 0f;
            }
        }

        skipShown = Mathf.MoveTowards(skipShown, skipA, Time.unscaledDeltaTime / 0.25f);
        UpdateHint();
        ApplyUi(amount, plaqueA);
    }

    // E, Пробел, Esc и клик (на телефоне тап). Идут напрямую через Input: во время показа ModalOpen = true и MobileInput.ActionDown молчит
    static bool SkipPressed()
    {
        if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return false;   // E листает реплику, а не пропускает показ
        return Input.GetKeyDown(MobileInput.ActionKey) || Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0);
    }

    static float Smooth01(float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x)); }

    // ------------------------------------------------------------ точка, на которую смотрит камера
    // центр построенных стадий (HutStages, а у рынка, палатки и моста объект из BuildStageLink), иначе зона стройки
    static Vector3 FocusPoint(BuildingController b, out float radius)
    {
        radius = 0f;
        Transform root = null;
        if (b.stages != null) root = b.stages.transform;
        else
        {
            var link = b.GetComponent<BuildStageLink>();
            if (link != null)
            {
                if (link.shop != null) root = link.shop.transform;
                else if (link.tent != null) root = link.tent.transform;
                else if (link.bridge != null) root = link.bridge.transform;
            }
        }

        bool has = false;
        Bounds bb = new Bounds();
        if (root != null)
        {
            var rs = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { bb = r.bounds; has = true; } else bb.Encapsulate(r.bounds);
            }
        }
        if (!has)
        {
            var col = b.GetComponent<Collider>();
            if (col != null) { bb = col.bounds; has = true; }
        }
        if (!has) { radius = 3f; return b.transform.position + Vector3.up; }

        radius = Mathf.Max(bb.extents.x, bb.extents.z, bb.extents.y * 0.8f);
        Vector3 c = bb.center;
        c.y = Mathf.Min(c.y, bb.min.y + 1.6f);   // смотрим на «тело» здания, а не на крышу: кадр не уезжает вверх
        return c;
    }

    // ------------------------------------------------------------ подсказка «Следующая стадия готова к постройке»
    void ShowHint(BuildingController b)
    {
        EnsureBuilt();
        hintFor = b;
        hintUntil = Time.unscaledTime + GameBalance.RevealHintSeconds;
        SfxManager.Play("hint", 0.6f, 1.15f);
    }

    void UpdateHint()
    {
        bool want = false;
        if (hintFor != null)
        {
            var w = BuildWindow.Instance;
            bool windowOpen = w != null && w.IsOpen;
            if (Time.unscaledTime >= hintUntil || bc != null || windowOpen ||
                !hintFor.PlayerInside || hintFor.StageRunning || hintFor.IsFinished || hintFor.IsRevealing || hintFor.IsLocked ||
                DialogueManager.IsBusy || DialogueSequence.AnyPlaying)
                hintFor = null;       // игрок открыл окно, ушёл, началась стройка или разговор: подсказка сделала своё дело
            else want = true;
        }
        hintK = Mathf.MoveTowards(hintK, want ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
    }

    // ------------------------------------------------------------ UI: плашка, подсказки, виньетка
    void EnsureBuilt()
    {
        if (built) return;
        Build();
    }

    void Build()
    {
        canvasGo = new GameObject("BuildRevealCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.layer = 5;
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 28;       // ниже окна стройки (29) и прокачки (30), выше обычного HUD
        var sc = canvasGo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // виньетка на весь экран (клики не перехватывает)
        var vg = UiKit.MakeImage(root, "Vignette", VignetteSprite(), false);
        Stretch(vg.rectTransform);
        vignetteGroup = vg.gameObject.AddComponent<CanvasGroup>();
        vignetteGroup.blocksRaycasts = false;
        vignetteGroup.interactable = false;

        BuildPlaque(root);
        BuildHint(root);

        canvasGo.SetActive(false);
        built = true;
    }

    // плашка: «Хижина · стадия 2/3» крупно, под ней название этапа, под плашкой «E: пропустить»
    void BuildPlaque(RectTransform root)
    {
        var holder = NewGroup("Plaque", root, out plaqueGroup);
        plaqueRt = holder;
        Anchor(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PlaqueY), new Vector2(PlaqueW, PlaqueH));

        var panel = UiKit.MakePanel(holder, new Vector2(PlaqueW, PlaqueH), false, 0.8f, "PlaquePanel");
        Stretch(panel.rectTransform);

        line1 = UiKit.MakeLabel(holder, "", 30f, Color.white, TextAlignmentOptions.Center, true);
        TopRect(line1.rectTransform, 18f, 10f, PlaqueW - 36f, 40f);
        AutoSize(line1, 18f, 30f);

        line2 = UiKit.MakeLabel(holder, "", 22f, Amber, TextAlignmentOptions.Center, true);
        line2.richText = true;
        TopRect(line2.rectTransform, 18f, 50f, PlaqueW - 36f, 30f);
        AutoSize(line2, 14f, 22f);

        // «E: пропустить» под плашкой, появляется, когда пропуск уже принимается
        var skipHolder = NewGroup("Skip", root, out skipGroup);
        Anchor(skipHolder, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, PlaqueY - 6f), new Vector2(460f, 26f));
        skipText = UiKit.MakeLabel(skipHolder, "", 16f, Dim, TextAlignmentOptions.Center, false);
        skipText.text = MobileInput.ActionKey + ", Пробел или Esc: пропустить";
    }

    void BuildHint(RectTransform root)
    {
        var holder = NewGroup("NextHint", root, out hintGroup);
        hintRt = holder;
        Anchor(holder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, HintY), new Vector2(HintW, HintH));

        var panel = UiKit.MakePanel(holder, new Vector2(HintW, HintH), false, 0.7f, "HintPanel");
        Stretch(panel.rectTransform);

        var key = UiKit.MakeKeycap(holder, MobileInput.ActionKey.ToString(), 38f);
        Anchor(key.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36f, 1f), new Vector2(38f, 38f));

        var tx = UiKit.MakeLabel(holder, "Следующая стадия готова к постройке", 21f, Color.white, TextAlignmentOptions.Left, true);
        tx.rectTransform.offsetMin = new Vector2(66f, 4f);
        tx.rectTransform.offsetMax = new Vector2(-14f, -4f);
        AutoSize(tx, 12f, 21f);
    }

    // тексты плашки: название здания, номер стадии, название этапа
    void FillPlaque(BuildingController b)
    {
        int tot = b.StageTotal, cur = b.StageCurrent;
        string title = b.BuildingTitle;
        line1.text = tot > 1 ? title + " · стадия " + Mathf.Min(cur, tot) + "/" + tot : title;

        string name = b.LastStepName;
        bool meaningful = !string.IsNullOrEmpty(name) && name != "Этап" && !name.StartsWith("Стадия");
        if (b.IsFinished) line2.text = "<color=#6BCB5A>Построено!</color>" + (meaningful ? "  ·  " + name : "");
        else line2.text = "<color=#FFD66B>" + (meaningful ? name : "Стадия готова") + "</color>";
    }

    // раз в кадр: виньетка, плашка, «пропустить» и подсказка
    void ApplyUi(float amount, float plaqueA)
    {
        if (!built) return;
        bool showing = bc != null;
        bool any = showing || hintK > 0.001f;
        if (canvasGo.activeSelf != any) canvasGo.SetActive(any);
        if (!any) return;

        // виньетка идёт вместе с наездом камеры; при «Уменьшить эффекты» её нет
        vignetteGroup.alpha = (showing && !VibeSettings.ReduceEffects) ? amount * (finalReveal ? VignetteFinal : VignetteNormal) : 0f;

        plaqueGroup.alpha = showing ? plaqueA : 0f;
        float e = UiDraw.EaseOutCubic(plaqueA);
        plaqueRt.anchoredPosition = new Vector2(0f, PlaqueY + Mathf.Lerp(-24f, 0f, e));
        plaqueRt.localScale = Vector3.one * ((finalReveal ? 1.08f : 1f) * Mathf.Lerp(0.96f, 1f, e));
        skipGroup.alpha = showing ? skipShown * plaqueA : 0f;

        hintGroup.alpha = hintK;
        hintRt.anchoredPosition = new Vector2(0f, HintY + Mathf.Lerp(-18f, 0f, UiDraw.EaseOutCubic(hintK)));
    }

    // ------------------------------------------------------------ мелочи сборки UI
    static RectTransform NewGroup(string name, RectTransform parent, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;      // показ ничего не нажимает: пропуск идёт с клавиатуры
        group.interactable = false;
        group.alpha = 0f;
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // от левого верхнего угла родителя вниз-вправо
    static void TopRect(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    // тёмно-синяя виньетка (цвет #0B1320, как Ink в UiKit): прозрачный центр, плотные края. Рисуется один раз
    static Sprite VignetteSprite()
    {
        if (vignette != null) return vignette;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.name = "BuildReveal_Vignette";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float nx = (x + 0.5f) / N * 2f - 1f;
                float ny = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny) / 1.4142f;           // 0 в центре, 1 в углу
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 1f, d));
                px[y * N + x] = new Color32(0x0B, 0x13, 0x20, (byte)Mathf.RoundToInt(a * 0.8f * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignette = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        vignette.name = "BuildReveal_Vignette";
        return vignette;
    }
}

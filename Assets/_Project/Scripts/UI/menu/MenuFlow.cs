// Assets/_Project/Scripts/UI/menu/MenuFlow.cs (НЕ в Editor)
// Поток «главное меню <-> игра».
//   Меню пишет выбор (StartGame), игровая сцена на старте его читает (Consume, это делает OnSceneLoaded ниже):
//     Continue = SaveSystem.Load(), NewGame = SaveSystem.NewGame(), None (запуск из редактора прямо из игровой сцены) = как раньше.
//   Выход из игры в меню: ReturnToMenu() (пауза зовёт после SaveNow).
//   Переход: затемнение, LoadSceneAsync, проявление. Всё по реальному времени (unscaled), пауза не мешает.
//   Продолжить: короткая заставка «С возвращением» (уровень и день из сохранения), пока грузится игра, минимум 1.6 с.
//   Новая игра: перед загрузкой играет видео-заставка Assets/StreamingAssets/Intro/NewGameIntro.mp4 (любая клавиша / клик / тап = пропуск).
//   Нет файла или видео не запустилось за 10 секунд: заставка молча пропускается. Игровая сцена грузится в фоне под видео.
// Индексы сцен в Build Settings: 0 = MainMenu, 1 = игровая сцена. Их ставит Tools > Timber Wings > Создать сцену MainMenu.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public enum MenuRequest { None, Continue, NewGame }

public static class MenuFlow
{
    public const int MainMenuScene = 0;
    public const int GameScene = 1;
    public const string IntroFile = "Intro/NewGameIntro.mp4";   // относительно Assets/StreamingAssets

    public static MenuRequest Request { get; private set; }

    // идёт переход между сценами: повторные нажатия и открытие паузы игнорируются
    public static bool Busy { get; internal set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Request = MenuRequest.None; Busy = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;     // -= перед +=: без дублей при выключенном Domain Reload
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static MenuRequest Consume()
    {
        var r = Request;
        Request = MenuRequest.None;
        return r;
    }

    // Меню: запомнить выбор и загрузить игровую сцену
    public static void StartGame(MenuRequest request)
    {
        if (Busy || request == MenuRequest.None) return;
        Request = request;
        Go(GameScene, request);
    }

    // Игра: вернуться в главное меню (сохранить и сбросить timeScale должен вызывающий)
    public static void ReturnToMenu()
    {
        if (Busy) return;
        Request = MenuRequest.None;
        Go(MainMenuScene, MenuRequest.None);
    }

    static void Go(int buildIndex, MenuRequest kind)
    {
        Busy = true;
        MenuFlowRunner.Get().Begin(buildIndex, kind);
    }

    // Сцена загружена: Awake и OnEnable её объектов уже были (сейверы зарегистрированы), Start ещё не было
    static void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (s.buildIndex == MainMenuScene)
        {
            Time.timeScale = 1f;                       // пауза, из которой вышли, не должна залипнуть
            SettingsMenu.Suppressed = true;            // Esc в меню не открывает паузу
            Request = MenuRequest.None;
            return;
        }

        SettingsMenu.Suppressed = false;
        if (s.buildIndex != GameScene) return;

        Time.timeScale = 1f;
        var req = Consume();
        if (req == MenuRequest.Continue) SaveSystem.Load();
        else if (req == MenuRequest.NewGame) SaveSystem.NewGame();
        // None: игровая сцена запущена из редактора напрямую, всё как раньше (автозагрузка в SaveAutoRunner)
    }
}

// Живёт между сценами: рисует чёрную шторку и гонит корутину перехода
sealed class MenuFlowRunner : MonoBehaviour
{
    static MenuFlowRunner inst;
    CanvasGroup group;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; }

    public static MenuFlowRunner Get()
    {
        if (inst == null)
        {
            var go = new GameObject("_MenuFlow", typeof(RectTransform));
            DontDestroyOnLoad(go);
            inst = go.AddComponent<MenuFlowRunner>();
            inst.BuildFader();
        }
        return inst;
    }

    void BuildFader()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;                    // выше всего, в том числе окна настроек (1000)
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(transform, false);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var im = img.GetComponent<Image>();
        im.color = Color.black;
        im.raycastTarget = true;                       // на время перехода клики никуда не проходят
    }

    public void Begin(int buildIndex, MenuRequest kind) { StartCoroutine(Run(buildIndex, kind)); }

    IEnumerator Fade(float to, float seconds)
    {
        float from = group.alpha;
        if (to > 0f) group.blocksRaycasts = true;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        group.alpha = to;
        if (to <= 0f) group.blocksRaycasts = false;
    }

    IEnumerator Run(int buildIndex, MenuRequest kind)
    {
        if (string.IsNullOrEmpty(SceneUtility.GetScenePathByBuildIndex(buildIndex)))
        {
            Debug.LogError("MenuFlow: в Build Settings нет сцены с индексом " + buildIndex + ". Запусти Tools > Timber Wings > Создать сцену MainMenu.");
            MenuFlow.Busy = false;
            yield break;
        }

        yield return Fade(1f, 0.35f);

        // сцена грузится в фоне (активация позже), пока играет заставка
        var op = SceneManager.LoadSceneAsync(buildIndex);
        if (op == null)
        {
            MenuFlow.Busy = false;
            yield return Fade(0f, 0.3f);
            yield break;
        }
        op.allowSceneActivation = false;

        if (kind == MenuRequest.NewGame) yield return PlayIntro();                 // видео-заставка
        else if (kind == MenuRequest.Continue) yield return ContinueSplash(op);    // короткая заставка «С возвращением»

        while (op.progress < 0.9f) yield return null;
        yield return new WaitForSecondsRealtime(0.05f);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;                             // кадр на Start новой сцены под чёрным

        MenuFlow.Busy = false;
        yield return Fade(0f, 0.5f);
    }

    // ------------------------------------------------------------ заставка «Продолжить»
    IEnumerator ContinueSplash(AsyncOperation op)
    {
        var cgo = new GameObject("ContinueSplash", typeof(RectTransform), typeof(CanvasGroup));
        cgo.transform.SetParent(transform, false);
        var crt = (RectTransform)cgo.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        var cg = cgo.GetComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false;

        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(crt, false);
        var br = (RectTransform)bgGo.transform;
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.075f, 0.095f, 0.135f, 1f);          // глубокий синий из палитры, под цвет Navy
        bgImg.raycastTarget = false;

        var cream = new Color(1f, 0.945f, 0.816f, 1f);
        var outline = new Color32(0x1A, 0x0E, 0x05, 255);
        var title = UiKit.MakeLabel(crt, "С возвращением!", 64f, cream, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0f, 50f), new Vector2(1200f, 90f));
        title.outlineWidth = 0.22f; title.outlineColor = outline; title.raycastTarget = false;

        string info = SaveSummary.Text();
        var sub = UiKit.MakeLabel(crt, info ?? "", 36f, UiKit.Brass, TextAlignmentOptions.Center, true);
        Place(sub.rectTransform, new Vector2(0f, -20f), new Vector2(1200f, 56f));
        sub.outlineWidth = 0.2f; sub.outlineColor = outline; sub.raycastTarget = false;

        var load = UiKit.MakeLabel(crt, "Загрузка", 30f, new Color(cream.r, cream.g, cream.b, 0.75f), TextAlignmentOptions.Center, true);
        Place(load.rectTransform, new Vector2(0f, -110f), new Vector2(600f, 44f));
        load.raycastTarget = false;

        float t0 = Time.unscaledTime;
        const float minShow = 1.6f;
        while (true)
        {
            float e = Time.unscaledTime - t0;
            cg.alpha = Mathf.Clamp01(e / 0.35f);                                       // проявление
            int dots = (int)(e * 2.5f) % 4;
            load.text = "Загрузка" + new string('.', dots);
            if (e >= minShow && op.progress >= 0.9f) break;
            yield return null;
        }

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)                      // уход в чёрное
        {
            cg.alpha = 1f - t / 0.3f;
            yield return null;
        }
        Destroy(cgo);
    }

    static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    // ------------------------------------------------------------ видео-заставка новой игры
    static string IntroPath()
    {
#if UNITY_WEBGL || UNITY_ANDROID
        return Application.streamingAssetsPath + "/" + MenuFlow.IntroFile;      // здесь файл проверить нельзя: ошибку поймает errorReceived
#else
        string p = System.IO.Path.Combine(Application.streamingAssetsPath, MenuFlow.IntroFile);
        return System.IO.File.Exists(p) ? p : null;
#endif
    }

    static bool SkipPressed()
    {
        if (Input.anyKeyDown) return true;               // клавиатура, мышь, кнопки геймпада
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    IEnumerator PlayIntro()
    {
        string path = IntroPath();
        if (path == null)
        {
            Debug.LogWarning("MenuFlow: нет видео заставки Assets/StreamingAssets/" + MenuFlow.IntroFile + ", заставка пропущена.");
            yield break;
        }

        // контейнер на весь экран поверх чёрной шторки
        var cgo = new GameObject("IntroVideo", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        var crt = (RectTransform)cgo.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;

        var vgo = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        vgo.transform.SetParent(crt, false);
        var raw = vgo.GetComponent<RawImage>();
        raw.raycastTarget = false; raw.enabled = false;
        var fit = vgo.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;      // чёрные полосы на нестандартных экранах
        fit.aspectRatio = 16f / 9f;

        var vp = cgo.AddComponent<VideoPlayer>();
        vp.playOnAwake = false; vp.isLooping = false; vp.skipOnDrop = true;
        vp.renderMode = VideoRenderMode.APIOnly;
        vp.source = VideoSource.Url; vp.url = path;

        AudioSource asrc = null;
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            asrc = cgo.AddComponent<AudioSource>();
            asrc.playOnAwake = false; asrc.spatialBlend = 0f; asrc.volume = 0f;
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.controlledAudioTrackCount = 1;
            vp.EnableAudioTrack(0, true);
            vp.SetTargetAudioSource(0, asrc);
        }

        bool failed = false, ended = false;
        vp.errorReceived += (p, m) => { Debug.LogWarning("MenuFlow: видео заставки не играет: " + m); failed = true; };
        vp.loopPointReached += p => ended = true;
        vp.Prepare();

        float tp = Time.unscaledTime;
        while (!vp.isPrepared && !failed && Time.unscaledTime - tp < 10f) yield return null;
        if (!vp.isPrepared || failed)
        {
            if (!failed) Debug.LogWarning("MenuFlow: видео заставки не подготовилось за 10 секунд, пропускаю.");
            Destroy(cgo);
            yield break;
        }

        if (vp.width > 0 && vp.height > 0) fit.aspectRatio = vp.width / (float)vp.height;
        MusicManager.SilenceForCutscene(0.6f);          // музыка меню уходит, игровая придёт после загрузки сцены
        vp.Play();

        // подсказка про пропуск: появляется через секунду и гаснет
        var hint = UiKit.MakeLabel(crt, "Нажмите любую клавишу, чтобы пропустить", 26f, new Color(1f, 0.945f, 0.816f, 1f), TextAlignmentOptions.BottomRight, true);
        var hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f);
        hr.anchoredPosition = new Vector2(-32f, 24f); hr.sizeDelta = new Vector2(900f, 40f);
        hint.outlineWidth = 0.25f; hint.outlineColor = new Color32(0, 0, 0, 255);
        hint.raycastTarget = false; hint.alpha = 0f;

        float t0 = Time.unscaledTime;
        float hardStop = vp.length > 0.5 ? (float)vp.length + 3f : 180f;
        float vol = GameAudio.MusicLevel;
        if (asrc != null) asrc.volume = vol;
        else { try { vp.SetDirectAudioVolume(0, vol); } catch (System.Exception) { } }

        while (!failed && !ended)
        {
            float e = Time.unscaledTime - t0;
            if (e > hardStop) break;
            if (e > 0.5f && SkipPressed()) break;        // 0.5 с: нажатие, которым запустили игру, не считается

            if (!raw.enabled && vp.texture != null && vp.frame >= 1) { raw.texture = vp.texture; raw.enabled = true; }
            hint.alpha = Mathf.Clamp01((e - 1f) / 0.6f) * (1f - Mathf.Clamp01((e - 5f) / 0.8f));
            yield return null;
        }

        // короткое затухание кадра и звука в чёрное
        float fadeT = 0.45f, ft = 0f;
        while (ft < fadeT)
        {
            ft += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(ft / fadeT);
            raw.color = new Color(k, k, k, 1f);
            hint.alpha = 0f;
            if (asrc != null) asrc.volume = vol * k;
            yield return null;
        }
        vp.Stop();
        Destroy(cgo);
    }
}

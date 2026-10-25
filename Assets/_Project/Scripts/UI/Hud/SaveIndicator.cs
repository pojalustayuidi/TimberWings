// Assets/_Project/Scripts/UI/Hud/SaveIndicator.cs (НЕ в Editor)
// Создаётся сам после загрузки сцены, ничего вешать не нужно. Переживает перезагрузку сцены.
// Вид Candy: плашки это светлые облачка с индиго-текстом, появляются на пружине CandyPopIn.
//  - «Сохранено»: маленькое мятное облачко с галочкой в левом нижнем углу (событие SaveSystem.Saved), fade ~1.5 с.
//    При VibeSettings.ReduceEffects без анимации: просто появляется и пропадает.
//  - Тост сверху по центру (~3.5 с): LoadedFromBackup, LoadFailed, SaveFailed (персиковый, с крестиком) и обычные
//    сообщения из SaveIndicator.ShowToast (светлый, без значка). Ширина тоста по тексту. Итог загрузки, случившейся
//    до создания UI, забираем через SaveSystem.ConsumeLoadNotice().
// Плашки регистрируются в HudSafeAreaRegistry, чтобы метки над миром не наезжали на них.
// Время только unscaled: работает и на паузе (меню настроек).
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveIndicator : MonoBehaviour
{
    const float SavedLife = 1.5f, SavedFadeIn = 0.12f, SavedFadeOut = 0.9f;
    const float ToastLife = 3.5f, ToastFadeIn = 0.2f, ToastFadeOut = 0.6f;
    const float Margin = 24f;

    public const string TextBackup = "Сохранение повреждено, загружена резервная копия";
    public const string TextFailed = "Сохранение повреждено. Начата новая игра";
    public const string TextSaveFailed = "Не удалось сохранить игру";

    static SaveIndicator instance;

    class Fader
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public TMP_Text label;
        public Image bg, icon;
        public CandyPopIn popIn;
        public float age = -1f, life, fadeIn, fadeOut;
    }

    const float IconSize = 32f, SidePad = 22f, TextGap = 10f;

    Canvas canvas;
    Fader saved, toast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("SaveIndicator");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SaveIndicator>();
    }

    // тост из кода: SaveIndicator.ShowToast("текст")
    public static void ShowToast(string text)
    {
        if (instance != null) instance.Toast(text);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.SaveIndicator;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        saved = MakeFader("Saved", new Vector2(220f, 60f), new Vector2(0f, 0f), new Vector2(0f, 0f), 26f, SavedLife, SavedFadeIn, SavedFadeOut);
        saved.label.text = "Сохранено";
        saved.bg.color = CandyPlaque.Tint(UiCandy.Mint, 0.3f);
        saved.icon.sprite = UiKit.CandyIcon(UiIcon.Check);
        Layout(saved, true);
        toast = MakeFader("Toast", new Vector2(900f, 68f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 28f, ToastLife, ToastFadeIn, ToastFadeOut);
    }

    Fader MakeFader(string name, Vector2 size, Vector2 anchor, Vector2 pivot, float fontSize, float life, float fin, float fout)
    {
        Image bg;
        TMP_Text label;
        Image icon;
        using (UiKit.UseSkin(UiSkin.Candy))
        {
            bg = UiKit.MakeImage(transform, name, UiKit.Panel, true, 0.5f);   // светлое облачко Candy
            var rt0 = bg.rectTransform;
            rt0.anchorMin = rt0.anchorMax = anchor;
            rt0.pivot = pivot;
            rt0.sizeDelta = size;

            icon = UiKit.MakeIcon(rt0, UiIcon.Check, IconSize, "Icon");
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(SidePad, 3f);   // чуть выше середины: снизу у панели «юбка»

            label = UiKit.MakeLabel(rt0, "", fontSize, UiCandy.Ink, TextAlignmentOptions.Center, true);
        }
        var rt = bg.rectTransform;

        var cg = bg.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = fontSize;

        var f = new Fader { rt = rt, cg = cg, label = label, bg = bg, icon = icon, life = life, fadeIn = fin, fadeOut = fout };
        Layout(f, true);

        rt.gameObject.SetActive(false);

        // пружинное появление (CandyPopIn). Добавляем на выключенный объект: OnEnable сработает при первом показе.
        // fade выключен: прозрачностью управляет Tick, чтобы не спорить с ним
        f.popIn = rt.gameObject.AddComponent<CandyPopIn>();
        f.popIn.fade = false;
        f.popIn.fromScale = 0.8f;
        return f;
    }

    // положение значка и текста внутри плашки: с иконкой текст сдвигается вправо
    static void Layout(Fader f, bool withIcon)
    {
        f.icon.gameObject.SetActive(withIcon);
        var lr = f.label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        float left = withIcon ? SidePad + IconSize + TextGap : SidePad;
        lr.offsetMin = new Vector2(left, 12f);        // снизу больше: у панели Candy голубая «юбка»
        lr.offsetMax = new Vector2(-SidePad, -6f);
        f.label.alignment = withIcon ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
    }

    void OnEnable()
    {
        SaveSystem.Saved += OnSaved;
        SaveSystem.SaveFailed += OnSaveFailed;
        SaveSystem.LoadedFromBackup += OnLoadedFromBackup;
        SaveSystem.LoadFailed += OnLoadFailed;
    }

    void OnDisable()
    {
        SaveSystem.Saved -= OnSaved;
        SaveSystem.SaveFailed -= OnSaveFailed;
        SaveSystem.LoadedFromBackup -= OnLoadedFromBackup;
        SaveSystem.LoadFailed -= OnLoadFailed;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void Start()
    {
        // Load() мог отработать до создания UI (SaveAutoRunner в BeforeSceneLoad)
        switch (SaveSystem.ConsumeLoadNotice())
        {
            case SaveLoadNotice.LoadedFromBackup: Toast(TextBackup); break;
            case SaveLoadNotice.Failed: Toast(TextFailed); break;
        }
    }

    void OnSaved() { Show(saved); }
    void OnSaveFailed() { Toast(TextSaveFailed, true); }
    void OnLoadedFromBackup() { SaveSystem.ConsumeLoadNotice(); Toast(TextBackup, true); }
    void OnLoadFailed() { SaveSystem.ConsumeLoadNotice(); Toast(TextFailed, true); }

    // warn: персиковая плашка с крестиком (сбои сохранения). Обычное сообщение: светлая плашка без значка
    void Toast(string text, bool warn = false)
    {
        toast.label.text = text;
        toast.bg.color = warn ? CandyPlaque.Tint(UiCandy.Peach, 0.75f) : CandyPlaque.Tint(UiCandy.Sky, 0.12f);
        if (warn) toast.icon.sprite = UiKit.CandyIcon(UiIcon.Cross);
        Layout(toast, warn);

        // ширина по тексту: короткое «Доброе утро!» не растягивается на 900
        var lab = toast.label;
        float size = lab.fontSizeMax;
        bool auto = lab.enableAutoSizing;
        lab.enableAutoSizing = false;
        lab.fontSize = size;
        float tw = lab.GetPreferredValues(text, 4000f, 0f).x;
        lab.enableAutoSizing = auto;
        float extra = SidePad * 2f + (warn ? IconSize + TextGap : 0f) + 8f;
        toast.rt.sizeDelta = new Vector2(Mathf.Clamp(tw + extra, 300f, 900f), toast.rt.sizeDelta.y);

        Show(toast);
    }

    void Show(Fader f)
    {
        ApplySafeMargins();
        TryRegister();
        f.age = 0f;
        bool calm = VibeSettings.ReduceEffects;
        bool wasActive = f.rt.gameObject.activeSelf;
        f.popIn.enabled = !calm;                       // без анимации: плашка просто появляется
        f.rt.gameObject.SetActive(true);
        if (wasActive && !calm) f.popIn.Replay();      // показали повторно, пока ещё висит: «подпрыгнуть» заново
        f.cg.alpha = calm ? 1f : 0f;
    }

    // отступы от краёв с учётом выреза телефона (Screen.safeArea)
    void ApplySafeMargins()
    {
        Rect sa = Screen.safeArea;
        float s = Mathf.Max(0.01f, canvas.scaleFactor);
        saved.rt.anchoredPosition = new Vector2(sa.xMin / s + Margin, sa.yMin / s + Margin);
        toast.rt.anchoredPosition = new Vector2(0f, -((Screen.height - sa.yMax) / s + Margin));
    }

    // метки над миром обходят плашки; реестр мог появиться позже нас, поэтому пробуем при каждом показе (Register идемпотентен)
    void TryRegister()
    {
        var reg = HudSafeAreaRegistry.Instance;
        if (reg == null) return;
        reg.Register(saved.rt);
        reg.Register(toast.rt);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Tick(saved, dt);
        Tick(toast, dt);
    }

    static void Tick(Fader f, float dt)
    {
        if (f.age < 0f) return;
        f.age += dt;
        if (f.age >= f.life)
        {
            f.age = -1f;
            f.cg.alpha = 0f;
            f.rt.gameObject.SetActive(false);
            return;
        }
        if (VibeSettings.ReduceEffects) { f.cg.alpha = 1f; return; }   // без анимации
        float a = 1f;
        if (f.age < f.fadeIn) a = f.age / f.fadeIn;
        else if (f.age > f.life - f.fadeOut) a = (f.life - f.age) / f.fadeOut;
        f.cg.alpha = Mathf.Clamp01(a);
    }
}

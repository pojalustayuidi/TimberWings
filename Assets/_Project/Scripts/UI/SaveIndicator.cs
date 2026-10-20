// Assets/_Project/Scripts/UI/SaveIndicator.cs (НЕ в Editor)
// Создаётся сам после загрузки сцены, ничего вешать не нужно. Переживает перезагрузку сцены.
//  - «Сохранено»: маленькая плашка в левом нижнем углу (событие SaveSystem.Saved), fade ~1.5 с.
//    При VibeSettings.ReduceEffects без анимации: просто появляется и пропадает.
//  - Тост сверху по центру (~3.5 с): LoadedFromBackup, LoadFailed, SaveFailed. Итог загрузки, случившейся до создания UI,
//    забираем через SaveSystem.ConsumeLoadNotice().
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
        public float age = -1f, life, fadeIn, fadeOut;
    }

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
        canvas.sortingOrder = 30000;   // поверх меню настроек
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        saved = MakeFader("Saved", new Vector2(190f, 44f), new Vector2(0f, 0f), new Vector2(0f, 0f), 24f, SavedLife, SavedFadeIn, SavedFadeOut);
        saved.label.text = "Сохранено";
        toast = MakeFader("Toast", new Vector2(900f, 64f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 28f, ToastLife, ToastFadeIn, ToastFadeOut);
    }

    Fader MakeFader(string name, Vector2 size, Vector2 anchor, Vector2 pivot, float fontSize, float life, float fin, float fout)
    {
        var bg = UiKit.MakeImage(transform, name, null, false);
        bg.color = new Color(0.08f, 0.11f, 0.19f, 0.82f);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;

        var cg = bg.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var label = UiKit.MakeLabel(rt, "", fontSize, Color.white, TextAlignmentOptions.Center, true);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = fontSize;
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(12f, 4f); lr.offsetMax = new Vector2(-12f, -4f);

        rt.gameObject.SetActive(false);
        return new Fader { rt = rt, cg = cg, label = label, life = life, fadeIn = fin, fadeOut = fout };
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
    void OnSaveFailed() { Toast(TextSaveFailed); }
    void OnLoadedFromBackup() { SaveSystem.ConsumeLoadNotice(); Toast(TextBackup); }
    void OnLoadFailed() { SaveSystem.ConsumeLoadNotice(); Toast(TextFailed); }

    void Toast(string text)
    {
        toast.label.text = text;
        Show(toast);
    }

    void Show(Fader f)
    {
        ApplySafeMargins();
        TryRegister();
        f.age = 0f;
        f.rt.gameObject.SetActive(true);
        f.cg.alpha = VibeSettings.ReduceEffects ? 1f : 0f;
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

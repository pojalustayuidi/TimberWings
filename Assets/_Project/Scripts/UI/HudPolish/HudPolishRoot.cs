using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. The scene builder supplies all sprite and HUD references.
[DefaultExecutionOrder(-1000), DisallowMultipleComponent]
public sealed class HudPolishRoot : MonoBehaviour
{
    [Serializable] public sealed class SkinSet
    {
        public Sprite chip, toast, quest, sign, medal, keycap, tail, mapRing, reticle, pill;
        public Sprite backpack, star, sparkle, coin, log, planks, paw, trophy, check, arrow;
        public Sprite[] gold = new Sprite[4], mint = new Sprite[4], blue = new Sprite[4];
    }
    [Serializable] public sealed class SparkWatch
    {
        public TMP_Text label;
        public Image[] sparks;
        [NonSerialized] public string previous;
        [NonSerialized] public bool initialized;
        [NonSerialized] public int value;
        [NonSerialized] public float age = 10f;
        [NonSerialized] public Vector2[] home;
    }
    [Serializable] public sealed class Dock
    { public RectTransform rect; public int slot; }

    public SkinSet skin = new SkinSet();
    public RectTransform resources, level, minimap, action;
    public CandyTaskView task;
    public Canvas canvas;
    public SparkWatch[] rewards;
    public Image phaseIcon;
    public TMP_Text phaseLabel;
    public CanvasGroup tooltip;
    public TMP_Text tooltipTitle, tooltipBody;
    public CanvasGroup levelNotice;
    public TMP_Text levelNoticeText;
    [Range(0f, 1f)] public float effects = 0.7f;
    public bool animateRewards = true;
    [HideInInspector] public string sceneBackup;
    [HideInInspector] public HudPolishSnapshot before = new HudPolishSnapshot();
    [HideInInspector] public List<GameObject> created = new List<GameObject>();
    readonly List<Dock> docks = new List<Dock>();
    readonly List<RectTransform> safeRects = new List<RectTransform>();
    static HudPolishRoot instance;
    int screenW, screenH;
    Rect lastSafe;
    Vector2 lastCanvas;
    float noticeUntil, tooltipAt, nextPhase;
    HudPolishButton tipOwner;
    string tipTitle, tipBody;
    bool safeRegistered;
    public static HudPolishRoot Instance { get { return instance != null && instance.isActiveAndEnabled ? instance : null; } }
    public static SkinSet Skin { get { var p = Instance; return p != null && p.skin.chip != null ? p.skin : null; } }
    public static bool Reduced { get { return VibeSettings.ReduceEffects || (Instance != null && Instance.effects <= 0.01f); } }
    public static readonly Color Ink = new Color32(49, 39, 95, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }
    void Awake() { instance = this; }
    void OnEnable() { instance = this; GameEvents.LevelUp += OnLevelUp; }
    void Start()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (tooltip != null) tooltip.alpha = 0f;
        if (levelNotice != null) levelNotice.alpha = 0f;
        Reflow();
    }
    void OnDisable()
    {
        GameEvents.LevelUp -= OnLevelUp;
        if (instance == this) instance = null;
        if (tooltip != null) tooltip.alpha = 0f;
        if (levelNotice != null) levelNotice.alpha = 0f;
        ResetSparks();
        if (HudSafeAreaRegistry.Instance != null)
            foreach (var rect in safeRects) HudSafeAreaRegistry.Instance.Unregister(rect);
        safeRegistered = false; safeRects.Clear(); tipOwner = null;
    }
    void OnLevelUp(int value)
    {
        if (levelNoticeText != null) levelNoticeText.text = "НОВЫЙ УРОВЕНЬ  ·  " + value;
        noticeUntil = Time.unscaledTime + 2.6f;
    }
    void LateUpdate()
    {
        if (canvas == null) return;
        var cr = canvas.transform as RectTransform;
        if (screenW != Screen.width || screenH != Screen.height || lastSafe != Screen.safeArea ||
            (cr != null && (lastCanvas - cr.rect.size).sqrMagnitude > 1f)) Reflow();
        if (!safeRegistered && HudSafeAreaRegistry.Instance != null)
        {
            RegisterSafe(resources); RegisterSafe(level); RegisterSafe(minimap); RegisterSafe(action);
            if (task != null) RegisterSafe(task.panel);
            if (tooltip != null) RegisterSafe(tooltip.transform as RectTransform);
            safeRegistered = true;
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f), now = Time.unscaledTime;
        bool busy = MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying;
        if (rewards != null) foreach (var watch in rewards) TickSpark(watch, dt, busy);
        if (levelNotice != null)
        {
            float want = !busy && now < noticeUntil ? 1f : 0f;
            levelNotice.alpha = Reduced ? want : Mathf.MoveTowards(levelNotice.alpha, want, dt * 6f);
        }
        bool tip = tipOwner != null && tipOwner.isActiveAndEnabled && !busy && now >= tooltipAt;
        if (tooltip != null)
        {
            if (tip)
            {
                if (tooltipTitle != null && tooltipTitle.text != tipTitle) tooltipTitle.text = tipTitle;
                if (tooltipBody != null && tooltipBody.text != tipBody) tooltipBody.text = tipBody;
            }
            tooltip.alpha = Mathf.MoveTowards(tooltip.alpha, tip ? 1f : 0f, dt * 10f);
            tooltip.blocksRaycasts = false;
        }
        if (now >= nextPhase)
        {
            nextPhase = now + 0.75f;
            bool night = DayNightCycle.Phase == DayPhase.Night;
            if (phaseLabel != null) phaseLabel.text = night ? "НОЧЬ" : DayNightCycle.PhaseName(DayNightCycle.Phase).ToUpperInvariant();
            if (phaseIcon != null) phaseIcon.color = night ? new Color32(166, 173, 255, 255) : new Color32(255, 208, 91, 255);
        }
    }
    void TickSpark(SparkWatch w, float dt, bool busy)
    {
        if (w == null || w.label == null || w.sparks == null) return;
        if (w.home == null)
        {
            w.home = new Vector2[w.sparks.Length];
            for (int i = 0; i < w.sparks.Length; i++) if (w.sparks[i] != null) w.home[i] = w.sparks[i].rectTransform.anchoredPosition;
        }
        if (!w.initialized) { w.previous = w.label.text; w.value = ReadCounter(w.previous); w.initialized = true; }
        if (w.label.text != w.previous)
        {
            w.previous = w.label.text;
            int next = ReadCounter(w.previous);
            bool gained = next > w.value; w.value = next;
            w.age = !gained || busy || Reduced || !animateRewards ? 10f : 0f;
        }
        w.age += dt;
        float t = Mathf.Clamp01(w.age / 0.6f);
        for (int i = 0; i < w.sparks.Length; i++)
        {
            var image = w.sparks[i]; if (image == null) continue;
            bool on = t < 1f && !busy && !Reduced && animateRewards;
            if (image.enabled != on) image.enabled = on;
            if (!on) continue;
            float direction = i % 2 == 0 ? 1f : -1f;
            image.rectTransform.anchoredPosition = w.home[i] + new Vector2(direction * 17f * t, 22f * t);
            image.rectTransform.localScale = Vector3.one * (0.45f + Mathf.Sin(t * Mathf.PI) * 0.7f);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, direction * 35f * t);
            image.color = new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.PI) * effects);
        }
    }
    public static int ReadCounter(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        long value = 0; bool tag = false;
        foreach (char c in text)
        {
            if (c == '<') { tag = true; continue; }
            if (c == '>') { tag = false; continue; }
            if (!tag && c >= '0' && c <= '9') value = Math.Min(int.MaxValue, value * 10 + c - '0');
        }
        return (int)value;
    }
    void ResetSparks()
    {
        if (rewards == null) return;
        foreach (var watch in rewards)
        {
            if (watch == null) continue;
            watch.initialized = false; watch.age = 10;
            if (watch.sparks == null) continue;
            for (int i = 0; i < watch.sparks.Length; i++)
            {
                var im = watch.sparks[i]; if (im == null) continue;
                im.enabled = false;
                if (watch.home != null && i < watch.home.Length) im.rectTransform.anchoredPosition = watch.home[i];
                im.rectTransform.localScale = Vector3.one; im.rectTransform.localRotation = Quaternion.identity;
            }
        }
    }
    void RegisterSafe(RectTransform rect)
    {
        if (rect == null || HudSafeAreaRegistry.Instance == null || safeRects.Contains(rect)) return;
        HudSafeAreaRegistry.Instance.Register(rect); safeRects.Add(rect);
    }
    public static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (r == null) return;
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
    }
    public void Reflow()
    {
        if (canvas == null) return;
        var cr = canvas.transform as RectTransform; if (cr == null) return;
        screenW = Screen.width; screenH = Screen.height; lastSafe = Screen.safeArea; lastCanvas = cr.rect.size;
        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        float left = lastSafe.xMin / scale + 34f, right = (Screen.width - lastSafe.xMax) / scale + 34f;
        float top = (Screen.height - lastSafe.yMax) / scale + 34f, bottom = lastSafe.yMin / scale + 42f;
        Anchor(resources, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -top), new Vector2(584, 78));
        Anchor(level, Vector2.one, Vector2.one, new Vector2(-right, -top), new Vector2(356, 104));
        float headerScale = Mathf.Clamp((lastCanvas.x - left - right - 32f) / 940f, .65f, 1f);
        if (resources != null) resources.localScale = Vector3.one * headerScale;
        if (level != null) level.localScale = Vector3.one * headerScale;
        bool narrow = lastCanvas.x - left - right < 1740f;
        float questWidth = Mathf.Clamp(lastCanvas.x - left - right - (narrow ? 40f : 1020f), 360f, 520f);
        float questX = lastCanvas.x < 1280f ? left + questWidth * .5f - lastCanvas.x * .5f : 0f;
        if (task != null) task.SetPresentationLayout(new Vector2(questX, -top - (narrow ? 150f : 50f)), questWidth);
        if (minimap != null)
        {
            minimap.anchorMin = minimap.anchorMax = minimap.pivot = Vector2.one;
            minimap.anchoredPosition = new Vector2(-right - 10f, -top - 176f);
            minimap.localScale = Vector3.one * (lastCanvas.x < 1280f ? .78f : 1f);
        }
        Anchor(action, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, bottom + 10f), new Vector2(390, 94));
        if (tooltip != null) Anchor(tooltip.transform as RectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-right, bottom + 122), new Vector2(362, 96));
        if (levelNotice != null) Anchor(levelNotice.transform as RectTransform, Vector2.one, Vector2.one, new Vector2(-right, -top - 116), new Vector2(356, 42));
        foreach (var d in docks) if (d.rect != null) DockNow(d.rect, d.slot);
    }
    static void DockNow(RectTransform r, int slot)
    {
        var c = r.GetComponentInParent<Canvas>(); float scale = c != null ? Mathf.Max(.01f, c.scaleFactor) : 1f;
        float right = (Screen.width - Screen.safeArea.xMax) / scale + 34f;
        float bottom = Screen.safeArea.yMin / scale + 42f;
        float x = slot == 0 ? right : right + 300f + 16f + (slot - 1) * 108f;
        Anchor(r, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-x, bottom), new Vector2(slot == 0 ? 300f : 92f, 92f));
    }
    public static void SetupShortcut(Button button, int slot, string title, string detail)
    {
        if (Skin == null || button == null) return;
        DockNow(button.transform as RectTransform, slot);
        var owner = Instance;
        bool found = false; foreach (var d in owner.docks) if (d.rect == button.transform) { found = true; break; }
        if (!found) owner.docks.Add(new Dock { rect = button.transform as RectTransform, slot = slot });
        var tip = button.GetComponent<HudPolishButton>();
        if (tip == null) tip = button.gameObject.AddComponent<HudPolishButton>();
        tip.title = title; tip.detail = detail;
        Palette(button.targetGraphic as Image, slot == 1 ? UiBtn.Green : UiBtn.Amber);
        if (slot > 0)
        {
            var image = button.targetGraphic as Image;
            if (image != null) { image.sprite = slot == 1 ? Skin.medal : Skin.keycap; image.type = Image.Type.Simple; image.preserveAspect = true; }
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = slot == 1 ? new Color(.81f, 1f, .91f) : Color.white;
            colors.highlightedColor = Color.white; colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.83f, .86f, 1f); button.colors = colors;
        }
        var juice = button.GetComponent<CandyButtonJuice>();
        if (juice == null) juice = UiCandy.Juice(button.gameObject, slot == 1 ? UiBtn.Green : UiBtn.Amber);
        juice.hoverScale = 1.045f; juice.burstCount = 4; juice.ringOnClick = false;
        owner.RegisterSafe(button.transform as RectTransform);
    }
    public static void Palette(Image image, UiBtn style)
    {
        var s = Skin; if (s == null || image == null) return;
        Sprite[] set = style == UiBtn.Green ? s.mint : s.gold;
        if (set == null || set.Length < 4 || set[0] == null) return;
        image.sprite = set[style == UiBtn.Gray ? 3 : 0]; image.color = Color.white;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1f;
        var button = image.GetComponent<Button>();
        if (button == null) return;
        button.transition = style == UiBtn.Gray ? Selectable.Transition.None : Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = set[1], selectedSprite = set[1], pressedSprite = set[2], disabledSprite = set[3] };
        image.overrideSprite = null;
    }
    public void ShowTip(HudPolishButton owner, string title, string detail)
    { tipOwner = owner; tipTitle = title; tipBody = detail; tooltipAt = Time.unscaledTime + .3f; }
    public void HideTip(HudPolishButton owner) { if (tipOwner == owner) tipOwner = null; }
}

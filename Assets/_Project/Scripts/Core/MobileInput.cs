// Assets/_Project/Scripts/Core/MobileInput.cs (НЕ в Editor)
// Единая точка ввода: клавиатура (WASD / стрелки, E) + экранный джойстик и кнопка действия для телефона.
//   MobileInput.Horizontal / Vertical   движение, -1..1
//   MobileInput.ActionHeld / ActionDown удержание и нажатие кнопки действия (E на ПК, круглая кнопка на телефоне)
// Экранные кнопки включаются сами на Android/iOS. Проверить на ПК: Tools > BuilderSim > Экранные кнопки (тест на ПК).
// Работает через старый Input (как и остальной проект), мышь в редакторе заменяет палец.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MobileInput
{
    public static KeyCode ActionKey = KeyCode.E;

    // значения от экранных кнопок (пишет MobileControls)
    public static Vector2 Virtual;
    public static bool VirtualActionHeld;
    static int virtualDownFrame = -1;
    static int virtualDashFrame = -1;

    // ГЛАВНЫЙ ВЫКЛЮЧАТЕЛЬ экранных кнопок (джойстик и круглая кнопка E).
    // false: их нет вообще, ни на ПК, ни на телефоне, ни в настройках; старое сохранение в PlayerPrefs игнорируется.
    // Чтобы вернуть управление для телефона, поставь true (код джойстика ниже не тронут).
    public static readonly bool Enabled = false;

    const string PrefOnScreen = "BuilderSim.OnScreenControls";

    public static bool OnScreenControls
    {
        get { return Enabled && PlayerPrefs.GetInt(PrefOnScreen, Application.isMobilePlatform ? 1 : 0) == 1; }
        set { if (Enabled) PlayerPrefs.SetInt(PrefOnScreen, value ? 1 : 0); }
    }

    // Окна (прокачка, мастерская досок, продажа, рабочие, стройка) и большая карта забирают управление: пока они открыты,
    // персонаж не бегает и не рубит дерево под окном (раньше джойстик и кнопка E работали «сквозь» интерфейс).
    public static bool ModalOpen
    {
        get
        {
            if (EndingSequence.Active) return true;   // финальная заставка: персонаж стоит
            if (SettingsMenu.IsOpen) return true;     // меню настроек (Esc): игра на паузе, управление не нужно
            if (DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return false;   // реплики листаются кнопкой действия, их не блокируем
            if (BuildingController.AnyRevealing) return true;   // показ готовой стадии здания: персонаж стоит, E не открывает окна
            // все окна и большая карта лежат в реестре UiModal (окно регистрируется само); свёрнутая полоска стройки управление не забирает
            return UiModal.AnyOpen();
        }
    }

    public static float Horizontal
    {
        get { return ModalOpen ? 0f : Mathf.Clamp(Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow) + Virtual.x, -1f, 1f); }
    }

    public static float Vertical
    {
        get { return ModalOpen ? 0f : Mathf.Clamp(Axis(KeyCode.W, KeyCode.UpArrow) - Axis(KeyCode.S, KeyCode.DownArrow) + Virtual.y, -1f, 1f); }
    }

    public static bool ActionHeld { get { return !ModalOpen && (Input.GetKey(ActionKey) || VirtualActionHeld); } }
    public static bool ActionDown { get { return !ModalOpen && (Input.GetKeyDown(ActionKey) || virtualDownFrame == Time.frameCount); } }

    // Бонус рубки: Пробел, пока держишь E у дерева (на телефоне пока нет кнопки: см. MobileControls)
    public static bool BonusDown { get { return !ModalOpen && Input.GetKeyDown(KeyCode.Space); } }

    // Рывок (навык carry_dash): Shift на ПК или кнопка на телефоне. Окна и диалоги его не пропускают.
    public static bool DashDown
    {
        get { return !ModalOpen && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || virtualDashFrame == Time.frameCount); }
    }

    static float Axis(KeyCode a, KeyCode b) { return (Input.GetKey(a) || Input.GetKey(b)) ? 1f : 0f; }

    internal static void PressAction() { VirtualActionHeld = true; virtualDownFrame = Time.frameCount; }
    internal static void ReleaseAction() { VirtualActionHeld = false; }
    internal static void PressDash() { virtualDashFrame = Time.frameCount; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Virtual = Vector2.zero;
        VirtualActionHeld = false;
        virtualDownFrame = -1;
        virtualDashFrame = -1;
    }
}

// Сам джойстик и кнопка. Создаётся автоматически после загрузки сцены, если включены экранные кнопки.
[DefaultExecutionOrder(-100)]
public class MobileControls : MonoBehaviour
{
    struct Ptr { public int id; public Vector2 pos; public int phase; }   // phase: 0 начало, 1 держим, 2 конец

    const int None = int.MinValue;

    RectTransform canvasRt, joyBase, joyKnob, actBtn, dashBtn;
    CanvasGroup joyGroup, actGroup, dashGroup;
    Image actImg, dashImg;
    TMP_Text actLabel, dashLabel;
    PlayerMovement dashPlayer;
    float findDashAt;
    readonly List<Ptr> ptrs = new List<Ptr>(8);
    int joyId = None, actId = None;
    Vector2 joyCenter;
    float R;
    static Sprite circle, ring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!MobileInput.Enabled || !MobileInput.OnScreenControls) return;
        if (FindFirstObjectByType<MobileControls>() != null) return;
        var go = new GameObject("MobileControls");
        DontDestroyOnLoad(go);
        go.AddComponent<MobileControls>();
    }

    void Awake() { Build(); }

    void OnDestroy()
    {
        MobileInput.Virtual = Vector2.zero;
        MobileInput.ReleaseAction();
    }

    // ------------------------------------------------------------ ввод
    void Collect()
    {
        ptrs.Clear();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            int ph = t.phase == TouchPhase.Began ? 0 : (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled ? 2 : 1);
            ptrs.Add(new Ptr { id = t.fingerId, pos = t.position, phase = ph });
        }
        if (Input.touchCount == 0 && !Input.touchSupported)   // ПК: мышь вместо пальца
        {
            Vector2 m = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) ptrs.Add(new Ptr { id = -100, pos = m, phase = 0 });
            else if (Input.GetMouseButtonUp(0)) ptrs.Add(new Ptr { id = -100, pos = m, phase = 2 });
            else if (Input.GetMouseButton(0)) ptrs.Add(new Ptr { id = -100, pos = m, phase = 1 });
        }
    }

    // вырез экрана, скруглённые углы и жестовая полоса: кнопки держим внутри Screen.safeArea
    static Rect SafeRect()
    {
        Rect s = Screen.safeArea;
        if (s.width < 1f || s.height < 1f) s = new Rect(0f, 0f, Screen.width, Screen.height);
        return s;
    }

    // кнопка рывка стоит над кнопкой действия
    Vector2 DashCenter() { Vector2 a = ActionCenter(); return new Vector2(a.x, a.y + R * 2.0f); }

    Vector2 ActionCenter() { Rect s = SafeRect(); return new Vector2(s.xMax - R * 1.9f, s.yMin + R * 1.9f); }
    Vector2 IdleJoyCenter() { Rect s = SafeRect(); return new Vector2(Mathf.Max(s.xMin + R * 2.1f, s.xMin + s.width * 0.13f), s.yMin + R * 2.1f); }

    // Касание началось на кнопке интерфейса (закрыть окно, карта, улучшения): это не джойстик и не кнопка действия.
    // Смотрим именно на Selectable, а не на любой Graphic: сплошные фоны HUD не должны глушить управление.
    static readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);
    static bool OverUiButton(Vector2 pos)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        uiHits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            var sel = uiHits[i].gameObject.GetComponentInParent<Selectable>();
            if (sel != null && sel.IsInteractable()) return true;
        }
        return false;
    }

    void Update()
    {
        R = Mathf.Min(Screen.width, Screen.height) * 0.11f;
        Collect();

        bool joyAlive = false, actAlive = false;
        Vector2 act = ActionCenter();

        for (int i = 0; i < ptrs.Count; i++)
        {
            Ptr p = ptrs[i];
            if (p.id == joyId)
            {
                if (p.phase != 2) { joyAlive = true; Drag(p.pos); }
                continue;
            }
            if (p.id == actId)
            {
                if (p.phase != 2) actAlive = true;
                continue;
            }
            if (p.phase != 0) continue;
            if (MobileInput.ModalOpen || OverUiButton(p.pos)) continue;   // палец лёг на окно или кнопку интерфейса

            if (PlayerSkills.Has(SkillIds.CarryDash) && (p.pos - DashCenter()).magnitude <= R * 0.95f)
            {
                MobileInput.PressDash();   // рывок: одно нажатие, палец дальше не ведём
                continue;
            }

            if (actId == None && (p.pos - act).magnitude <= R * 1.2f)
            {
                actId = p.id; actAlive = true;
                MobileInput.PressAction();
            }
            else if (joyId == None && p.pos.x < Screen.width * 0.5f)
            {
                joyId = p.id; joyAlive = true;
                joyCenter = p.pos;
            }
        }

        if (joyId != None && !joyAlive) joyId = None;
        if (actId != None && !actAlive) actId = None;
        if (joyId == None) MobileInput.Virtual = Vector2.zero;
        if (actId == None) MobileInput.ReleaseAction();

        Draw(act);
        DrawDash();
    }

    // кнопка видна только с навыком «Рывок»; на перезарядке тёмная и показывает секунды
    void DrawDash()
    {
        bool has = PlayerSkills.Has(SkillIds.CarryDash);
        dashGroup.alpha = has ? 1f : 0f;
        if (!has) return;

        if (dashPlayer == null && Time.unscaledTime >= findDashAt)
        {
            findDashAt = Time.unscaledTime + 1f;
            dashPlayer = PlayerMovement.Instance;
        }
        float left = dashPlayer != null ? dashPlayer.DashCooldownLeft : 0f;
        bool ready = left <= 0f;

        dashBtn.sizeDelta = Vector2.one * (R * 1.5f);
        dashBtn.anchoredPosition = DashCenter();
        dashImg.color = ready ? new Color(0.42f, 0.80f, 0.35f, 0.95f) : new Color(0.06f, 0.09f, 0.17f, 0.75f);
        dashLabel.fontSize = R * 0.7f;
        dashLabel.text = ready ? ">>" : Mathf.CeilToInt(left).ToString();
    }

    void Drag(Vector2 pos)
    {
        Vector2 d = pos - joyCenter;
        float m = d.magnitude;
        if (m > R) { joyCenter += d / m * (m - R); d = pos - joyCenter; m = R; }   // плавающий центр: следует за пальцем
        Vector2 v = d / R;
        MobileInput.Virtual = v.magnitude < 0.15f ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
    }

    // ------------------------------------------------------------ отрисовка
    void Draw(Vector2 act)
    {
        bool joyOn = joyId != None;
        Vector2 jc = joyOn ? joyCenter : IdleJoyCenter();
        joyBase.sizeDelta = Vector2.one * (R * 2.2f);
        joyBase.anchoredPosition = jc;
        joyKnob.sizeDelta = Vector2.one * (R * 1.0f);
        joyKnob.anchoredPosition = MobileInput.Virtual * R;
        joyGroup.alpha = Mathf.MoveTowards(joyGroup.alpha, joyOn ? 1f : 0.35f, Time.unscaledDeltaTime * 6f);

        bool held = actId != None;
        actBtn.sizeDelta = Vector2.one * (R * 1.7f);
        actBtn.anchoredPosition = act;
        actBtn.localScale = Vector3.one * Mathf.MoveTowards(actBtn.localScale.x, held ? 0.9f : 1f, Time.unscaledDeltaTime * 8f);
        actImg.color = held ? new Color(0.42f, 0.80f, 0.35f, 0.95f) : new Color(0.06f, 0.09f, 0.17f, 0.75f);
        actLabel.fontSize = R * 0.8f;
        actGroup.alpha = 1f;
    }

    // ------------------------------------------------------------ сборка интерфейса
    static Sprite Circle(bool ringOnly)
    {
        if (ringOnly && ring != null) return ring;
        if (!ringOnly && circle != null) return circle;

        const int S = 96;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - S * 0.5f) * (x + 0.5f - S * 0.5f) + (y + 0.5f - S * 0.5f) * (y + 0.5f - S * 0.5f)) - (S * 0.5f - 1f);
                float a = ringOnly ? Mathf.Clamp01(UiDraw.Cov(d) - UiDraw.Cov(d + 5f)) : UiDraw.Cov(d);
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        if (ringOnly) ring = sp; else circle = sp;
        return sp;
    }

    RectTransform Img(string name, Transform parent, Sprite sprite, Color color, out Image image)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;   // координаты в пикселях экрана от левого нижнего угла
        rt.pivot = new Vector2(0.5f, 0.5f);
        image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rt;
    }

    void Build()
    {
        var cgo = new GameObject("MobileControls_Canvas", typeof(RectTransform), typeof(Canvas));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.HudOverlay;
        canvasRt = (RectTransform)cgo.transform;

        Image bi, ki;
        joyBase = Img("JoyBase", canvasRt, Circle(false), new Color(0.06f, 0.09f, 0.17f, 0.45f), out bi);
        joyGroup = joyBase.gameObject.AddComponent<CanvasGroup>();
        joyGroup.blocksRaycasts = false;
        joyGroup.interactable = false;
        Image br;
        var rr = Img("Ring", joyBase, Circle(true), new Color(0.62f, 0.72f, 0.95f, 0.9f), out br);
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        joyKnob = Img("Knob", joyBase, Circle(false), new Color(0.62f, 0.72f, 0.95f, 0.85f), out ki);
        joyKnob.anchorMin = joyKnob.anchorMax = new Vector2(0.5f, 0.5f);   // ручка двигается относительно центра основы

        actBtn = Img("Action", canvasRt, Circle(false), new Color(0.06f, 0.09f, 0.17f, 0.75f), out actImg);
        actGroup = actBtn.gameObject.AddComponent<CanvasGroup>();
        actGroup.blocksRaycasts = false;
        actGroup.interactable = false;
        Image ar;
        var ra = Img("Ring", actBtn, Circle(true), new Color(0.62f, 0.72f, 0.95f, 1f), out ar);
        ra.anchorMin = Vector2.zero; ra.anchorMax = Vector2.one; ra.offsetMin = ra.offsetMax = Vector2.zero;

        dashBtn = Img("Dash", canvasRt, Circle(false), new Color(0.06f, 0.09f, 0.17f, 0.75f), out dashImg);
        dashGroup = dashBtn.gameObject.AddComponent<CanvasGroup>();
        dashGroup.blocksRaycasts = false;
        dashGroup.interactable = false;
        dashGroup.alpha = 0f;
        Image dr;
        var rd = Img("Ring", dashBtn, Circle(true), new Color(0.62f, 0.72f, 0.95f, 1f), out dr);
        rd.anchorMin = Vector2.zero; rd.anchorMax = Vector2.one; rd.offsetMin = rd.offsetMax = Vector2.zero;

        var dl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        dl.layer = 5;
        dl.transform.SetParent(dashBtn, false);
        var dlr = (RectTransform)dl.transform;
        dlr.anchorMin = Vector2.zero; dlr.anchorMax = Vector2.one; dlr.offsetMin = dlr.offsetMax = Vector2.zero;
        dashLabel = dl.GetComponent<TextMeshProUGUI>();
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) dashLabel.font = UIManager.Instance.objectiveText.font;
        dashLabel.text = ">>";
        dashLabel.fontStyle = FontStyles.Bold;
        dashLabel.alignment = TextAlignmentOptions.Center;
        dashLabel.color = Color.white;
        dashLabel.raycastTarget = false;

        var tg = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        tg.layer = 5;
        tg.transform.SetParent(actBtn, false);
        var tr = (RectTransform)tg.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        actLabel = tg.GetComponent<TextMeshProUGUI>();
        if (UIManager.Instance != null && UIManager.Instance.objectiveText != null) actLabel.font = UIManager.Instance.objectiveText.font;
        actLabel.text = "E";
        actLabel.fontStyle = FontStyles.Bold;
        actLabel.alignment = TextAlignmentOptions.Center;
        actLabel.color = Color.white;
        actLabel.raycastTarget = false;
    }
}

// Assets/_Project/Scripts/UI/Core/GameCursor.cs (НЕ в Editor)
// Курсор игры: кремовая стрелка с индиго-контуром и золотым ободком плюс тонкий золотой след (референс «Курсор с эффектом следа»).
// Создаётся сама после загрузки сцены (как UiInputGuard), на сцену ничего вешать не нужно.
//
// Стрелка АППАРАТНАЯ (Cursor.SetCursor): рисует её система, поэтому она не отстаёт от мыши и клик попадает ровно в острие.
// Три картинки: обычная, золотая «наведение» и серая «недоступно» (нарисованы кодом, GameCursorArt, размер подстраивается под экран).
// Поверх неё отдельный канвас рисует только украшения: след, искры, свечение нажатия, значок «недоступно».
//
// Как это работает вместе с OrbitCamera: камера прячет и запирает системный курсор при обзоре мышью, а в окнах, меню и с Alt
// отпускает. Cursor.visible и lockState здесь НЕ трогаем; украшения рисуем только когда курсор виден (не Locked, окно в фокусе).
//
// Чтобы не мешать игре:
//   - след рисуется только при быстром взмахе, при медленном движении и точных кликах его нет совсем;
//   - след короткий (0,22 с), тонкий, полупрозрачный, под стрелкой; искры редкие и мелкие;
//   - «Уменьшить тряску и эффекты» (VibeSettings.ReduceEffects): след вдвое короче, без искр;
//   - GameCursor.TrailEnabled = false выключает след и искры совсем (стрелка и состояния остаются).
// Состояния: обычный, наведение на кнопку (стрелка тёплого золотого цвета), нажатие (золотое свечение и кольцо), недоступно (серая стрелка и значок).
// Ничего не перехватывает: у канваса нет GraphicRaycaster, у всех картинок raycastTarget = false.
// Части: GameCursor (поведение), GameCursorTrail (меш следа), GameCursorArt (текстуры).
// Снаружи: GameCursor.Pulse() даёт золотую вспышку на курсоре («успех», награда; при выключенном следе молчит),
// GameCursor.TrailEnabled (переключатель «След за курсором» в настройках, вкладка с мышью).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameCursor : MonoBehaviour
{
    // ======== настройки (все числа здесь; размеры в px для экрана 1080p, дальше масштабируются) ========
    const float ArrowScale = 0.446f;          // масштаб значка «недоступно» относительно его текстуры (стрелка около 20x32 px на 1080p)
    const float TrailLife = 0.30f;
    const float TrailLifeReduced = 0.10f;
    const float TrailWidth = 9f;
    const float TrailMinSpeed = 40f;          // медленнее (px/с на 1080p): следа нет
    const float TrailMidSpeed = 300f;         // обычное движение мышью: след уже хорошо виден
    const float TrailFullSpeed = 1000f;       // быстрый взмах: в полную силу
    const float SparkMinSpeed = 650f;
    const float SparkEvery = 0.09f;
    static readonly Vector2 TrailAnchor = new Vector2(5f, -13.5f); // голова следа в теле стрелки, не на острие (y вверх)
    static readonly Vector2 BadgeOffset = new Vector2(15f, -26f);  // значок «недоступно» у нижнего правого края стрелки

    const string KTrail = "tw_cursor_trail";

    enum Over { None, Clickable, Unavailable }

    static GameCursor instance;
    static bool trailCache = true, trailRead;

    /// <summary>След и искры. Хранится в PlayerPrefs, по умолчанию включено (можно повесить на переключатель в настройках).</summary>
    public static bool TrailEnabled
    {
        get { if (!trailRead) { trailCache = PlayerPrefs.GetInt(KTrail, 1) == 1; trailRead = true; } return trailCache; }
        set { trailCache = value; trailRead = true; PlayerPrefs.SetInt(KTrail, value ? 1 : 0); }
    }

    /// <summary>Золотая вспышка на курсоре: успешное действие, награда, важное взаимодействие.</summary>
    public static void Pulse() { if (instance != null) instance.DoPulse(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; trailRead = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null || Application.isMobilePlatform || Application.isBatchMode) return;   // на телефоне курсора нет
        var go = new GameObject("GameCursor");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameCursor>();
    }

    // ---- части
    Canvas canvas;
    CanvasGroup group;
    GameCursorTrail trail;
    RectTransform badgeRt;
    Image badgeImg;
    readonly Texture2D[] cur = new Texture2D[3];     // обычная, наведение, недоступно
    readonly Vector2[] hot = new Vector2[3];
    float curK;
    int curApplied = -1;      // что сейчас установлено системным курсором: 0 обычный, 1 недоступно
    PointerEventData ped;
    EventSystem pedEs;
    readonly List<RaycastResult> hits = new List<RaycastResult>();

    // ---- состояние
    float scale = 1f;
    int lastScreenH = -1;
    float shownA;
    bool wasShown;
    bool havePrev;
    Vector2 prevPos, moveDir = Vector2.right;
    float speedF, sparkT, probeT;
    Over over;
    float glowA, disA;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Cursor;
        canvas.scaleFactor = 1f;                       // без CanvasScaler: координаты канваса = пиксели экрана
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // порядок снизу вверх: след и свечение, значок
        var tg = NewChild("Trail", typeof(GameCursorTrail));
        trail = tg.GetComponent<GameCursorTrail>();
        trail.atlas = GameCursorArt.BuildAtlas();
        trail.raycastTarget = false;
        var trt = trail.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero; trt.pivot = Vector2.zero;

        badgeImg = NewImage("Badge", GameCursorArt.BuildBadge(), out badgeRt);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);

        canvas.enabled = false;
        RefreshScale();
        Debug.Log("[GameCursor] запущен, стрелка " + cur[0].width + "x" + cur[0].height + ", след " + (TrailEnabled ? "вкл" : "выкл"));
    }

    GameObject NewChild(string name, System.Type component)
    {
        var go = new GameObject(name, typeof(RectTransform), component);
        go.transform.SetParent(transform, false);
        return go;
    }

    Image NewImage(string name, Sprite sprite, out RectTransform rt)
    {
        var go = NewChild(name, typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;    // позиция = пиксели экрана от левого нижнего угла
        return img;
    }

    void OnDestroy()
    {
        if (instance == this) { instance = null; Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
        for (int i = 0; i < cur.Length; i++) if (cur[i] != null) Destroy(cur[i]);
    }

    void RefreshScale()
    {
        if (lastScreenH == Screen.height) return;
        lastScreenH = Screen.height;
        scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.2f);
        float k = Mathf.Round(scale * 4f) / 4f;            // стрелку перерисовываем ступенями по 0,25, а не при каждом пикселе
        if (!Mathf.Approximately(k, curK))
        {
            curK = k;
            for (int i = 0; i < cur.Length; i++)
            {
                if (cur[i] != null) Destroy(cur[i]);
                cur[i] = GameCursorArt.BuildCursor(k, i, out hot[i]);
            }
            curApplied = -1;                                // заставить поставить новую картинку
        }
        badgeRt.sizeDelta = Vector2.one * (GameCursorArt.BadgeSize * ArrowScale * scale * 1.1f);
        trail.maxWidth = TrailWidth * scale;
        trail.minSpacing = 3f * scale;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        RefreshScale();

        Vector2 mp = MousePos();
        bool show = CanShow(mp);

        shownA = Mathf.MoveTowards(shownA, show ? 1f : 0f, dt * (show ? 14f : 30f));
        group.alpha = shownA;
        if (!show)
        {
            ApplyCursor(GameCursorArt.StateNormal);
            wasShown = false;
            if (shownA <= 0.001f) { canvas.enabled = false; trail.Clear(); }
            return;
        }
        canvas.enabled = true;
        if (!wasShown)                                   // курсор только что появился (окно, Alt, фокус): мышь могла «прыгнуть»
        {
            wasShown = true; havePrev = false; trail.Clear(); probeT = 0f; speedF = 0f;
        }

        // ---- скорость
        float spd = 0f;
        if (havePrev)
        {
            Vector2 d = mp - prevPos;
            if (d.magnitude > Screen.height * 0.35f) trail.Clear();       // телепорт, не взмах
            else
            {
                spd = d.magnitude / Mathf.Max(dt, 0.0001f);
                if (d.sqrMagnitude > 0.25f) moveDir = d.normalized;
            }
        }
        prevPos = mp; havePrev = true;
        float spdNorm = spd / scale;

        // ---- что под курсором
        probeT -= dt;
        if (probeT <= 0f) { probeT = 0.05f; Probe(mp); }
        bool held = LeftHeld();
        bool unavailable = over == Over.Unavailable;
        ApplyCursor(unavailable ? GameCursorArt.StateDisabled : over == Over.Clickable ? GameCursorArt.StateHover : GameCursorArt.StateNormal);
        bool press = held && !unavailable;

        // ---- плавные переходы состояний
        float k = 1f - Mathf.Exp(-18f * dt);
        glowA = Mathf.Lerp(glowA, press ? 0.75f : 0f, k);
        disA = Mathf.Lerp(disA, unavailable ? 1f : 0f, k);

        badgeRt.anchoredPosition = mp + BadgeOffset * scale;
        badgeRt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, disA);
        badgeImg.color = new Color(1f, 1f, 1f, disA);
        badgeImg.enabled = disA > 0.01f;

        // ---- след
        bool fx = TrailEnabled;
        bool reduce = VibeSettings.ReduceEffects;
        Vector2 head = mp + TrailAnchor * scale;
        trail.life = reduce ? TrailLifeReduced : TrailLife;
        // почти ноль на медленном движении, хорошо виден уже при обычном, на быстром взмахе в полную силу
        float f = 0.4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TrailMinSpeed, TrailMidSpeed, spdNorm))
                + 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TrailMidSpeed, TrailFullSpeed, spdNorm));
        if (unavailable) f *= 0.3f;
        speedF = Mathf.Lerp(speedF, f, 1f - Mathf.Exp(-30f * dt));
        if (fx && speedF > 0.02f) trail.AddSample(head, speedF);

        if (fx && !reduce && spdNorm > SparkMinSpeed)
        {
            sparkT -= dt;
            if (sparkT <= 0f)
            {
                sparkT = SparkEvery * Random.Range(0.8f, 1.6f);
                Vector2 pos = head - moveDir * Random.Range(4f, 22f) * scale + Random.insideUnitCircle * 5f * scale;
                Vector2 vel = -moveDir * 25f * scale + Random.insideUnitCircle * 30f * scale;
                trail.Spawn(pos, vel, Random.Range(5f, 9f) * scale, 0.4f);
            }
        }

        // ---- клик по кнопке: маленькое кольцо и пара искр (по пустому месту и по недоступному кольца нет)
        if (fx && over == Over.Clickable && LeftDown())
        {
            trail.Ring(head, 46f * scale, 0.28f);
            if (!reduce) Burst(head, 5, 70f);
        }

        trail.glowPos = head;
        trail.glowAlpha = fx ? glowA * 0.5f : 0f;
        trail.glowSize = 54f * scale;
        trail.Tick(dt);
    }

    void Burst(Vector2 at, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            trail.Spawn(at + dir * 6f * scale, dir * speed * scale, Random.Range(6f, 10f) * scale, 0.45f);
        }
    }

    void DoPulse()
    {
        if (!TrailEnabled || !wasShown) return;
        Vector2 head = prevPos + TrailAnchor * scale;
        trail.Ring(head, 80f * scale, 0.45f);
        if (!VibeSettings.ReduceEffects) Burst(head, 8, 110f);
    }

    // ---------------------------------------------------------------- что под курсором
    void Probe(Vector2 mp)
    {
        over = Over.None;
        var es = EventSystem.current;
        if (es == null) return;
        if (ped == null || pedEs != es) { ped = new PointerEventData(es); pedEs = es; }
        ped.position = mp;
        hits.Clear();
        es.RaycastAll(ped, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            if (go == null) continue;
            // решает самый верхний попавшийся объект: панель поверх кнопки её перекрывает
            var h = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
            if (h == null) h = ExecuteEvents.GetEventHandler<IPointerDownHandler>(go);
            if (h == null) return;
            var sel = h.GetComponent<Selectable>();
            over = sel != null && !sel.IsInteractable() ? Over.Unavailable : Over.Clickable;
            return;
        }
    }

    // ---------------------------------------------------------------- системный курсор
    bool CanShow(Vector2 mp)
    {
        if (!Application.isFocused) return false;
        if (Cursor.lockState == CursorLockMode.Locked) return false;   // обзор мышью: курсор спрятан камерой
        if (!Cursor.visible) return false;
        return mp.x >= 0f && mp.y >= 0f && mp.x <= Screen.width && mp.y <= Screen.height;
    }

    /// <summary>Ставит системному курсору нужную картинку, только при смене (без периодических сбросов: они мерцают).</summary>
    void ApplyCursor(int state)
    {
        if (state == curApplied || cur[state] == null) return;
        curApplied = state;
        Cursor.SetCursor(cur[state], hot[state], CursorMode.Auto);
    }

    void OnApplicationFocus(bool focused) { if (focused) curApplied = -1; }   // вернули фокус: поставим картинку заново

    // ---------------------------------------------------------------- ввод
    static Vector2 MousePos()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null ? m.position.ReadValue() : Vector2.zero;
#else
        return Vector2.zero;
#endif
    }

    static bool LeftHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#elif ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null && m.leftButton.isPressed;
#else
        return false;
#endif
    }

    static bool LeftDown()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }
}

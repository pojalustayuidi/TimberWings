// Assets/_Project/Scripts/Vibe/CampSleep.cs (НЕ в Editor)
// Сон у костра: ночью рядом с ГОРЯЩИМ (достроенным) костром появляется «Нажми E, чтобы лечь спать до рассвета».
// Экран темнеет, время быстро доматывается до рассвета, свет возвращается. Весь сон занимает GameBalance.SleepFadeSeconds (1.5 с).
// Любая клавиша или клик пропускает сон: время сразу встаёт на рассвет, экран быстро светлеет.
//
// Создаёт DayNightCycle сам, вешать ничего не нужно. Использует обычную систему E: IInteractable (InteractionHub), с приоритетом над деревьями.
// Костёр ищется как BuildingController с видом Campfire. Пока он не достроен, E у костра принадлежит стройке (BuildSiteInteractable), и сна нет.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampSleep : MonoBehaviour, IInteractable, IPriorityInteractable
{
    // true, пока идёт сон. Читают NightBonus и другие
    public static bool Sleeping { get; private set; }

    BuildingController camp;
    Transform spot;             // точка у костра: от неё считается расстояние
    float findAt;

    Canvas canvas;
    Image black;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Sleeping = false; }

    void Awake()
    {
        spot = new GameObject("CampSleepSpot").transform;
        spot.SetParent(transform, false);
    }

    void OnEnable() { Interactables.Register(this); }

    void OnDisable()
    {
        Interactables.Unregister(this);
        DayNightCycle.ExternalSpeed = 1f;
        if (Sleeping) { Sleeping = false; SetAlpha(0f); }
    }

    void Update()
    {
        if (camp == null)
        {
            if (Time.unscaledTime < findAt) return;
            findAt = Time.unscaledTime + 2f;
            foreach (var b in FindObjectsByType<BuildingController>(FindObjectsSortMode.None))
            {
                if (b != null && b.SiteKind == BuildSiteKind.Campfire) { camp = b; break; }
            }
            if (camp == null) return;
        }
        spot.position = camp.transform.position;
        UpdateFastTime();
    }

    // У достроенного костра: «Зажми F, чтобы ускорить время». Пока F зажата, сутки идут в GameBalance.CampTimeSpeed раз быстрее
    // (днём и ночью; в режиме «Фиксированный день» подсказки нет). Разгон плавный, его делает DayNightCycle по ExternalSpeed.
    public static KeyCode FastKey = KeyCode.F;

    Transform player;
    float playerAt;
    WorldTag fastTag;
    int fastTagState = -1;   // 0 подсказка, 1 «идёт быстрее»

    void UpdateFastTime()
    {
        if (player == null && Time.unscaledTime >= playerAt)
        {
            playerAt = Time.unscaledTime + 1f;
            var pm = PlayerMovement.Instance;
            if (pm != null) player = pm.transform;
        }

        bool near = false;
        if (player != null && camp.IsFinished && !camp.IsRevealing && !Sleeping && !DayNightCycle.Warping
            && !GameSettings.DayFrozen && !DialogueManager.IsBusy && Time.timeScale > 0f)
        {
            Vector3 d = player.position - spot.position;
            d.y = 0f;
            float r = GameBalance.SleepRadius + GameBalance.CampTimeRadiusExtra;
            near = d.sqrMagnitude <= r * r;
        }

        bool fast = near && Input.GetKey(FastKey);
        DayNightCycle.ExternalSpeed = fast ? GameBalance.CampTimeSpeed : 1f;

        if (fastTag == null)
        {
            if (!near) return;
            fastTag = WorldTag.Add(spot.gameObject, false);
            fastTag.offset = new Vector3(0f, 3.0f, 0f);   // выше «Нажми E» (она на 1.8 м)
            fastTag.SetFontSize(28f);
            fastTag.visible = false;
        }
        int state = fast ? 1 : 0;
        if (state != fastTagState)
        {
            fastTagState = state;
            fastTag.SetText(fast
                ? "<color=#F2A93B>Время идёт быстрее</color> <size=70%>x" + GameBalance.CampTimeSpeed.ToString("0") + "</size>"
                : "Зажми <color=#6BCB5A>" + FastKey + "</color>, чтобы ускорить время");
        }
        fastTag.visible = near;
    }

    // ------------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return spot; } }
    public Vector3 PromptPoint { get { return spot.position + Vector3.up * 1.8f; } }
    public string Prompt { get { return "лечь спать до рассвета"; } }
    public float Radius { get { return GameBalance.SleepRadius; } }
    public bool Priority { get { return true; } }   // E принадлежит костру, даже если рядом дерево

    public bool CanInteract
    {
        get
        {
            return camp != null && camp.IsFinished && !camp.IsRevealing
                && !Sleeping && Time.timeScale > 0f
                && DayNightCycle.Phase == DayPhase.Night && !DayNightCycle.Warping
                && !DialogueManager.IsBusy;
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        StartCoroutine(Run());
    }

    // ------------------------------------------------------------------ сон
    IEnumerator Run()
    {
        Sleeping = true;
        EnsureOverlay();
        DayNightCycle cycle = DayNightCycle.Instance;

        float total = Mathf.Max(0.6f, GameBalance.SleepFadeSeconds);
        float fade = total * 0.3f;               // темнеет 0.45 с
        float hold = total - 2f * fade;          // в темноте идёт время, 0.6 с
        float alpha = 0f;
        bool skipped = false;

        SfxManager.Play("sleep", 0.8f, 1f);

        // 1) темнеет
        for (float x = 0f; x < fade; x += Time.unscaledDeltaTime)
        {
            alpha = Mathf.SmoothStep(0f, 1f, x / fade);
            SetAlpha(alpha);
            yield return null;
            if (SkipPressed()) { skipped = true; break; }
        }

        // пропустили, пока ещё не совсем темно: быстро дотемняем, чтобы смена света не мелькнула
        if (skipped && alpha < 1f)
        {
            float a0 = alpha;
            for (float x = 0f; x < 0.12f; x += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(a0, 1f, x / 0.12f));
                yield return null;
            }
        }
        SetAlpha(1f);

        // 2) в темноте время бежит до рассвета
        if (cycle != null && !skipped) cycle.WarpTo(GameBalance.SleepWakeTime, hold);
        for (float x = 0f; x < hold && !skipped; x += Time.unscaledDeltaTime)
        {
            yield return null;
            if (SkipPressed()) skipped = true;
        }
        if (cycle != null)
        {
            cycle.WarpTo(GameBalance.SleepWakeTime, 0.05f);   // если перемотка не дошла (пропуск, пауза), ставим время сейчас
            cycle.FinishWarp();
        }

        // 3) светлеет
        float fo = skipped ? 0.3f : fade;
        for (float x = 0f; x < fo; x += Time.unscaledDeltaTime)
        {
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, x / fo));
            yield return null;
        }
        SetAlpha(0f);

        Sleeping = false;
        SaveSystem.RequestSave("time");
        SaveIndicator.ShowToast("Доброе утро!");
    }

    // любая клавиша, клик или касание
    static bool SkipPressed()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) return true;
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
    }

    // ------------------------------------------------------------------ чёрный экран
    void EnsureOverlay()
    {
        if (canvas != null) return;

        var go = new GameObject("SleepFade", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29000;   // выше всего HUD, ниже «Сохранено» и тостов (30000)

        var bg = new GameObject("Black", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        black = bg.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;
        var rt = (RectTransform)bg.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        canvas.enabled = false;
    }

    void SetAlpha(float a)
    {
        if (canvas == null || black == null) return;
        black.color = new Color(0f, 0f, 0f, a);
        canvas.enabled = a > 0.001f;
    }
}

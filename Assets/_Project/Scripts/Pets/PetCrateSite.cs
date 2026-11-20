// The Forest Crate is introduced by the story. River and Sky sites can be discovered before
// their opening level. The reveal window still checks availability, price and the real reward.
using UnityEngine;

public class PetCrateSite : MonoBehaviour, IInteractable, IPriorityInteractable
{
    public CrateType crate = CrateType.Forest;
    public float radius = 3.2f;
    public string prompt = "открыть клеть";
    public Vector3 promptOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("Дочерний объект с моделью. Пусто = дочерний объект «Visual». Прячется, пока клеть недоступна")]
    public GameObject visual;

    [Header("Открытие (ставит PetCrateBuilder)")]
    [Tooltip("Крышка с осью на задней кромке")]
    public Transform lid;
    [Tooltip("Свет изнутри, разгорается при открытии")]
    public Light glow;
    [Tooltip("Угол откидывания крышки (минус = вверх и назад)")]
    public float openAngle = -110f;
    [Tooltip("Скорость открытия: 2.5 = 0.4 секунды")]
    public float openSpeed = 2.5f;
    public float glowMax = 2.2f;

    [Header("Записка Гуннара (читает Лео при первом E; пусто = без записки)")]
    public string noteSpeaker = "Лео";
    public string[] noteLines =
    {
        "На клети записка от Гуннара.",
        "«Заказ №14. Звери-помощники. Ворчать бесплатно, кормить по расписанию.»"
    };

    bool noteBusy;
    bool shown;
    bool mine;            // это окно открыто именно с этой клети
    float pollAt;
    float openT;          // 0 закрыта, 1 открыта
    float popT = 1f;      // 0..1 анимация появления

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + promptOffset; } }
    public string Prompt { get { return GameLanguage.T(prompt); } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }

    public bool Available
    {
        get
        {
            var pm = PetManager.Instance;
            if (pm == null) return false;
            if (crate == CrateType.Forest)
            {
                var story = StoryFlow.Instance;
                if (story != null && !story.PetsOpen) return false;
            }
            return pm.IsCrateUnlocked(crate);
        }
    }

    public bool CanInteract
    {
        get { return (Available || crate != CrateType.Forest && PetManager.Instance != null) && !noteBusy && !PetCrateWindow.IsOpen && !DialogueManager.IsBusy && !DialogueSequence.AnyPlaying; }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        if (crate == CrateType.Forest && !DialogueSeen.Has("chapter1/forest-note") && noteLines != null && noteLines.Length > 0)
        {
            DialogueSeen.Mark("chapter1/forest-note");
            noteBusy = true;
            ChapterOneVoice.SpeakSequence(() =>
            {
                noteBusy = false;
                if (this != null && isActiveAndEnabled) OpenWindow();
            }, "twv3_s11_note_01", "twv3_s11_02");
            return;
        }
        OpenWindow();
    }

    void OpenWindow()
    {
        mine = true;
        PetCrateWindow.Open(crate);
    }

    // ------------------------------------------------------------ жизненный цикл
    void OnEnable()
    {
        Interactables.Register(this);
        pollAt = 0f;
    }

    void OnDisable() { Interactables.Unregister(this); }

    void DiscoverNearby()
    {
        string key = "chapter1/crate/" + crate;
        if (DialogueSeen.Has(key) || SaveSystem.IsLoading) return;
        var actor = PlayerMovement.Instance;
        if (actor == null) return;
        var delta = actor.transform.position - transform.position;
        if (Mathf.Abs(delta.y) > 3f || delta.sqrMagnitude > 36f) return;
        DialogueSeen.Mark(key);
        var poi = GetComponent<MapPoi>();
        if (poi != null) DialogueSeen.Mark("inspection/" + MapPoi.GetInspectionId(transform, poi.inspectionId));
        MapPoi.ReportInspection(transform);
        if (crate == CrateType.Sky && !IsPondSite()) return;
        ChapterOneVoice.Queue(crate == CrateType.River ? "twv3_s11_04" : "twv3_s11_05",
            ChapterOneVoice.Priority.Lesson, stillRelevant: () => this != null && actor != null &&
                (actor.transform.position - transform.position).sqrMagnitude < 100f);
    }

    internal bool IsPondSite()
    {
        var pond = FindFirstObjectByType<PondLife>();
        if (pond != null && (pond.transform.position - transform.position).sqrMagnitude < 36f * 36f) return true;
        return StoryFlow.HasAny(StoryFlow.PathOf(transform), new[] { "pond", "пруд" });
    }

    void Awake()
    {
        if (visual == null)
        {
            var t = transform.Find("Visual");
            if (t != null) visual = t.gameObject;
        }
        // в игре клеть сначала спрятана, первый опрос покажет её, если она доступна
        shown = false;
        if (visual != null) visual.SetActive(false);
        ApplyOpen();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // доступность и появление: раз в 0.25 с
        if (Time.unscaledTime >= pollAt)
        {
            pollAt = Time.unscaledTime + 0.25f;
            bool want = Available || crate != CrateType.Forest && PetManager.Instance != null;
            if (want && crate != CrateType.Forest && StoryFlow.Instance != null)
                DiscoverNearby();
            if (want != shown)
            {
                shown = want;
                if (visual != null)
                {
                    visual.SetActive(want);
                    if (want) { popT = 0f; visual.transform.localScale = Vector3.zero; }
                    else visual.transform.localScale = Vector3.one;
                }
            }
        }

        // выскакивание с лёгким отскоком
        if (popT < 1f && visual != null)
        {
            popT = Mathf.Min(1f, popT + dt / 0.45f);
            float s = Mathf.Sin(popT * Mathf.PI * 0.5f) * (1f + 0.18f * Mathf.Sin(popT * Mathf.PI));
            visual.transform.localScale = popT >= 1f ? Vector3.one : Vector3.one * s;
        }

        // крышка и свет
        if (!PetCrateWindow.IsOpen) mine = false;
        float target = mine ? 1f : 0f;
        if (!Mathf.Approximately(openT, target))
        {
            openT = Mathf.MoveTowards(openT, target, dt * openSpeed);
            ApplyOpen();
        }
    }

    void ApplyOpen()
    {
        float e = openT * openT * (3f - 2f * openT);
        if (lid != null) lid.localRotation = Quaternion.Euler(openAngle * e, 0f, 0f);
        if (glow != null) glow.intensity = glowMax * e;
    }
}

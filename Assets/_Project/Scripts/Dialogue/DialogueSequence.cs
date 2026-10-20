// Assets/_Project/Scripts/Dialogue/DialogueSequence.cs (НЕ в Editor)
// Набор реплик. Запускается: на старте, когда игрок подошёл (PlayerNear), по кнопке E (OnInteract) или из кода.
// Висит на NPC (говорит он сам) или на любом объекте (тогда в строке указывается имя говорящего, например «Лео»).
// По окончании кидает GameEvents.DialogueFinished и вызывает onFinished.
//
// OnInteract: объект становится IInteractable. Игрок подходит ближе radius, над NPC появляется «Нажми E, чтобы поговорить»,
// по E начинается разговор (выбирает и нажимает InteractionHub на игроке).
// Значения enum Trigger: Manual=0, OnStart=1, PlayerNear=2, OnInteract=3. Старые сцены не ломаются.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DialogueSequence : MonoBehaviour, IInteractable, IPriorityInteractable
{
    // сколько разговоров идёт прямо сейчас. Нужно хабу: между репликами DialogueManager на миг пуст, и E успевала рубить дерево
    static int activeCount;
    public static bool AnyPlaying { get { return activeCount > 0; } }

    // идёт разговор, во время которого игрок стоит на месте (PlayerMovement читает это)
    static int lockCount;
    public static bool FreezesPlayer { get { return lockCount > 0; } }
    // с кем сейчас говорим: игрок поворачивается к этому объекту
    public static Transform Focus { get; private set; }
    public bool Priority { get { return true; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetActive() { activeCount = 0; lockCount = 0; Focus = null; }

    public enum Trigger { Manual, OnStart, PlayerNear, OnInteract }

    [Serializable]
    public class Line
    {
        [Tooltip("Имя из Speaker.displayName (например Лео). Пусто = говорит этот объект")]
        public string speaker = "";
        [TextArea(1, 3)] public string text = "";
        [Tooltip("Озвучка (необязательно). Реплика держится, пока играет звук")]
        public AudioClip voice;
        public float pauseAfter = 0.4f;
    }

    [Serializable] public class IntEvent : UnityEvent<int> { }

    [Header("Когда запускать")]
    public Trigger trigger = Trigger.PlayerNear;
    public float radius = 3f;            // для PlayerNear и OnInteract
    public float startDelay = 0.5f;      // для OnStart
    public bool once = true;
    [Tooltip("Для once-разговора: запомнить в сохранении, что он уже звучал, и не повторять после перезапуска. Пусто = у разговора с триггером OnStart id берётся из пути объекта сам, у остальных не запоминается")]
    public string saveId = "";
    [Tooltip("Пока идёт разговор, игрок не может ходить (не уйдёт посреди реплики). У разговоров самого Лео отключается само")]
    public bool freezePlayer = true;

    [Header("Подсказка (для OnInteract)")]
    [Tooltip("Подставляется в «Нажми E, чтобы …»")]
    public string prompt = "поговорить";
    [Tooltip("Где показать подсказку, если у объекта нет Speaker (иначе берётся точка над головой говорящего)")]
    public Vector3 promptOffset = new Vector3(0f, 2.9f, 0f);

    [Header("Реплики (до 120 символов каждая)")]
    public Line[] lines;

    [Header("Кружок над головой (необязательно)")]
    public NpcMarker marker;

    [Header("События")]
    public IntEvent onLineStarted;       // номер реплики, с 0
    public UnityEvent onFinished;

    Speaker own;
    Transform player;
    bool playing, played;
    float timer;

    public bool IsPlaying { get { return playing; } }
    public bool Played { get { return played; } }

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return own != null ? own.BubblePoint + Vector3.up * 0.5f : transform.position + promptOffset; } }
    public string Prompt { get { return prompt; } }
    public float Radius { get { return radius; } }

    public bool CanInteract
    {
        get
        {
            return trigger == Trigger.OnInteract && !playing && lines != null && lines.Length > 0 && (!played || !once);
        }
    }

    public void Interact() { Play(); }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        own = GetComponent<Speaker>();
        if (GetComponent<PlayerMovement>() != null) freezePlayer = false;   // Лео говорит сам с собой: стоять незачем
        if (marker == null) marker = GetComponentInChildren<NpcMarker>(true);
    }

    void OnEnable()
    {
        if (trigger == Trigger.OnInteract) Interactables.Register(this);
    }

    void OnDisable()
    {
        Interactables.Unregister(this);
        if (playing)
        {
            // корутина остановилась вместе с объектом: не оставляем «вечный» разговор
            playing = false;
            activeCount = Mathf.Max(0, activeCount - 1);
            DialogueManager.EndConversation();
            Unfreeze();
        }
    }

    bool froze;

    void Freeze()
    {
        if (!freezePlayer || froze) return;
        froze = true;
        lockCount++;
        Focus = transform;
    }

    void Unfreeze()
    {
        if (!froze) return;
        froze = false;
        lockCount = Mathf.Max(0, lockCount - 1);
        if (Focus == transform) Focus = null;
    }

    // id «уже звучало» для сохранения (null = не запоминать)
    string PersistId
    {
        get
        {
            if (!once) return null;
            if (!string.IsNullOrEmpty(saveId)) return saveId;
            if (trigger != Trigger.OnStart) return null;
            string path = name;
            for (var p = transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return gameObject.scene.name + ":" + path + "|start";
        }
    }

    void Start()
    {
        string pid = PersistId;
        if (pid != null && DialogueSeen.Has(pid)) played = true;   // уже звучало в прошлом запуске: не повторяем

        if (marker != null) marker.SetVisible(trigger != Trigger.OnStart && !(played && once));
        if (trigger == Trigger.OnStart && !played) StartCoroutine(DelayedStart());
    }

    // сменить режим из кода (регистрация в хабе обновится)
    public void SetTrigger(Trigger t)
    {
        trigger = t;
        if (!isActiveAndEnabled) return;
        if (t == Trigger.OnInteract) Interactables.Register(this);
        else Interactables.Unregister(this);
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(startDelay);
        Play();
    }

    public void Play()
    {
        if (playing || lines == null || lines.Length == 0) return;
        StartCoroutine(Run());
    }

    // заменить реплики из кода (квесты ведут NPC по сюжету). rearm = разговор снова доступен по E
    public void SetLines(Line[] newLines, bool rearm = true)
    {
        if (playing) return;
        lines = newLines;
        if (rearm) Rearm();
    }

    // снова доступен по E, но не запускается сам
    public void Rearm()
    {
        if (playing) return;
        played = false;
        if (marker != null) marker.SetVisible(true);
    }

    // для повторного запуска «разового» разговора
    public void ResetAndPlay() { played = false; Play(); }

    IEnumerator Run()
    {
        playing = true;
        played = true;
        DialogueSeen.Mark(PersistId);   // запоминаем сразу: выход посреди реплики тоже считается «звучало»
        activeCount++;
        DialogueManager.BeginConversation();
        Freeze();
        if (marker != null) marker.SetVisible(false);

        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l == null || string.IsNullOrEmpty(l.text)) continue;

            Speaker who = string.IsNullOrEmpty(l.speaker) ? own : Speaker.Find(l.speaker);
            if (who == null)
                Debug.LogWarning("DialogueSequence (" + name + "): не найден говорящий \"" + l.speaker + "\". Реплика пойдёт только субтитрами.");

            if (onLineStarted != null) onLineStarted.Invoke(i);

            // разговор пропущен (удержание E): реплика не показывается, но её событие уже сработало,
            // а текст ушёл в журнал: игрок может перечитать
            if (DialogueManager.SkipAllRequested)
            {
                DialogueLog.Add(who != null ? who.displayName : "", l.text, true);
                continue;
            }

            bool done = false;
            DialogueManager.Say(who, l.text, l.voice, () => done = true);
            while (!done) yield return null;
            if (l.pauseAfter > 0f && !DialogueManager.SkipAllRequested) yield return new WaitForSecondsRealtime(l.pauseAfter);
        }
        DialogueManager.EndConversation();

        playing = false;
        activeCount = Mathf.Max(0, activeCount - 1);
        Unfreeze();
        if (marker != null) marker.SetVisible(!once);
        GameEvents.RaiseDialogueFinished(this);
        if (onFinished != null) onFinished.Invoke();
    }

    void Update()
    {
        if (trigger != Trigger.PlayerNear || playing) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 0.15f;

        if (player == null)
        {
            var pm = PlayerMovement.Instance;
            if (pm == null) return;
            player = pm.transform;
        }

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        float sq = d.sqrMagnitude;

        if (!played)
        {
            if (sq <= radius * radius) Play();
        }
        else if (!once && sq > radius * radius * 1.96f)
        {
            // игрок отошёл: можно сработать снова
            played = false;
            if (marker != null) marker.SetVisible(true);
        }
    }

    void OnValidate()
    {
        if (lines == null) return;
        foreach (var l in lines)
            if (l != null && l.text != null && l.text.Length > 120)
                Debug.LogWarning(name + ": реплика длиннее 120 символов, будет обрезана: " + l.text, this);
    }

    void OnDrawGizmosSelected()
    {
        if (trigger != Trigger.PlayerNear && trigger != Trigger.OnInteract) return;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        const int n = 48;
        Vector3 p = transform.position;
        Vector3 prev = p + new Vector3(radius, 0.05f, 0f);
        for (int i = 1; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 cur = p + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
}

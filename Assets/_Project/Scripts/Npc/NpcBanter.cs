// Assets/_Project/Scripts/Npc/NpcBanter.cs (НЕ в Editor)
// «Характер» NPC: болтает сама по себе, реагирует на игрока и отвечает на E после основного разговора.
//   1. Реплики по настроению: раз в 35-80 секунд бросает фразу, если игрок рядом и никто не говорит.
//   2. Реакция на рубку: пока игрок долго рубит рядом, подбадривает его.
//   3. «Поболтать» по E: когда главный разговор уже был, E даёт случайную шутку; если тыкать часто, NPC возмущается.
//   4. Эмодзи над головой («!», «♪», «♥», «?») всплывают в такт: приветствие, шутка, конец разговора.
// Реплики показываются обычными пузырями (DialogueManager.Say), голос не нужен.
// Работает вместе с NpcLife (приветствие и «ура» после разговора), но и без него тоже.
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcBanter : MonoBehaviour, IInteractable, IPriorityInteractable
{
    [Header("Ссылки (пусто = найдутся сами)")]
    public Speaker speaker;
    public DialogueSequence dialogue;
    public NpcLife life;

    [Header("Болтовня сама по себе")]
    public bool idleBarks = true;
    public float barkRadius = 8f;
    public Vector2 barkInterval = new Vector2(35f, 80f);
    [TextArea(1, 3)]
    public string[] idleLines =
    {
        "Ох, пахнет дымком. Или это мои носки?",
        "Если слышишь скрип, это не лес. Это моя спина.",
        "Знаешь, в лагере тихо только до первой щепки.",
        "Не забывай пить воду, Лео. И не пей из реки.",
        "Говорят, у этих деревьев есть характер. Вот и проверим.",
        "Один топор хорошо, а два лагеря лучше.",
        "Эй! Я не ленюсь, я наблюдаю за процессом."
    };

    [Header("Реакция на приветствие")]
    public string[] greetLines =
    {
        "О, Лео! Как дела?",
        "Привет, лесоруб!",
        "Опять ты! Заходи, не стесняйся."
    };
    [Range(0f, 1f)] public float greetBarkChance = 0.6f;

    [Header("Реакция на рубку")]
    public bool reactToChopping = true;
    public float chopSeconds = 8f;
    public float chopCooldown = 60f;
    [TextArea(1, 3)]
    public string[] chopLines =
    {
        "Вот это замах! Так держать, Лео!",
        "Щепки летят, значит дело идёт!",
        "Ты рубишь как настоящий Гуннар. Почти.",
        "Тихо-тихо, оставь хоть пару деревьев на потом!"
    };

    [Header("Поболтать по E (после основного разговора)")]
    public bool pokeEnabled = true;
    public string pokePrompt = "поболтать";
    public float pokeRadius = 2.6f;
    public float pokeCooldown = 1.2f;
    [Tooltip("true: E забирает у ближайшего дерева. Обычно false, чтобы рубка рядом с NPC не ломалась")]
    public bool priority = false;
    [TextArea(1, 3)]
    public string[] pokeLines =
    {
        "Что, соскучился? Тогда руби, а то дров не хватит.",
        "Ты знаешь, что пенёк это просто дерево, которое сдалось?",
        "Мне снилось, что я белка. Было здорово.",
        "Если что, я за костёр, а ты за дрова. Командная работа!",
        "Слушай, а ты правда племянник Гуннара? Тогда понятно...",
        "Не обижайся, но у тебя щепка в волосах. Нет, левее.",
        "Хочешь совет? Сначала топор, потом болтовня."
    };
    [TextArea(1, 3)]
    public string[] annoyedLines =
    {
        "Всё-всё, я поняла, ты здесь! Хватит тыкать!",
        "Ещё раз ткнёшь, и я скажу Гуннару!",
        "Я не кнопка, Лео!"
    };
    public int annoyedAfter = 4;
    public float streakWindow = 6f;

    [Header("Эмодзи над головой")]
    public bool emotes = true;
    public float emoteSize = 0.07f;
    public float emoteRise = 0.8f;
    public float emoteTime = 1.4f;

    // ---- внутреннее
    Transform player;
    PlayerInteraction playerChop;
    float findTimer;
    float barkTimer;
    float chopTime;
    float nextChopReact;
    float nextPoke;
    float lastPoke = -999f;
    int streak;
    int lastIdle = -1, lastGreet = -1, lastChop = -1, lastPokeLine = -1, lastAnnoyed = -1;
    static Font emoteFont;

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return speaker != null ? speaker.BubblePoint + Vector3.up * 0.5f : transform.position + Vector3.up * 2.9f; } }
    public string Prompt { get { return pokePrompt; } }
    public float Radius { get { return pokeRadius; } }
    public bool Priority { get { return priority; } }

    public bool CanInteract
    {
        get
        {
            if (!pokeEnabled || Time.time < nextPoke) return false;
            if (DialogueSequence.AnyPlaying || DialogueManager.IsBusy) return false;
            // пока главный разговор не состоялся (или может повториться), E принадлежит ему
            if (dialogue != null && (!dialogue.Played || dialogue.IsPlaying || dialogue.CanInteract)) return false;
            return true;
        }
    }

    public void Interact()
    {
        float now = Time.time;
        streak = (now - lastPoke <= streakWindow) ? streak + 1 : 1;
        lastPoke = now;
        nextPoke = now + pokeCooldown;

        if (annoyedAfter > 0 && streak >= annoyedAfter)
        {
            Say(Next(annoyedLines, ref lastAnnoyed));
            Emote("!", new Color(1f, 0.35f, 0.3f, 1f));
            if (life != null) life.Hop();
            streak = 0;
            return;
        }

        Say(Next(pokeLines, ref lastPokeLine));
        string[] pool = { "♪", "♥", "?", "!" };
        Emote(pool[Random.Range(0, pool.Length)], new Color(1f, 0.85f, 0.3f, 1f));
        if (Random.value < 0.35f && life != null) life.Hop();
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        if (speaker == null) speaker = GetComponent<Speaker>();
        if (speaker == null) speaker = GetComponentInChildren<Speaker>();
        if (dialogue == null) dialogue = GetComponent<DialogueSequence>();
        if (life == null) life = GetComponent<NpcLife>();
        if (life == null) life = GetComponentInChildren<NpcLife>();
        ResetBarkTimer();
    }

    void OnEnable()
    {
        Interactables.Register(this);
        if (life != null)
        {
            life.Greeted += OnGreeted;
            life.DialogueEnded += OnDialogueEnded;
        }
    }

    void OnDisable()
    {
        Interactables.Unregister(this);
        if (life != null)
        {
            life.Greeted -= OnGreeted;
            life.DialogueEnded -= OnDialogueEnded;
        }
    }

    void ResetBarkTimer() { barkTimer = Random.Range(barkInterval.x, Mathf.Max(barkInterval.x, barkInterval.y)); }

    bool FindPlayer()
    {
        if (player != null) return true;
        findTimer -= Time.deltaTime;
        if (findTimer > 0f) return false;
        findTimer = 0.5f;
        var pm = PlayerMovement.Instance;
        if (pm != null)
        {
            player = pm.transform;
            playerChop = pm.GetComponent<PlayerInteraction>();
        }
        return player != null;
    }

    float FlatDistance()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // можно ли сейчас что-то сказать «от себя»
    bool CanBark()
    {
        if (speaker == null || !FindPlayer()) return false;
        if (DialogueSequence.AnyPlaying || DialogueManager.IsBusy) return false;
        if (dialogue != null && !dialogue.Played) return false;     // до знакомства молчит: главный разговор важнее
        return true;
    }

    void Update()
    {
        if (!FindPlayer()) return;
        float dt = Time.deltaTime;
        float dist = FlatDistance();

        if (idleBarks)
        {
            barkTimer -= dt;
            if (barkTimer <= 0f)
            {
                ResetBarkTimer();
                if (dist <= barkRadius && CanBark())
                {
                    Say(Next(idleLines, ref lastIdle));
                    Emote("♪", new Color(0.6f, 0.9f, 1f, 1f));
                }
            }
        }

        if (reactToChopping && playerChop != null)
        {
            if (playerChop.IsChopping && dist <= barkRadius) chopTime += dt; else chopTime = 0f;

            if (chopTime >= chopSeconds && Time.time >= nextChopReact && CanBark())
            {
                nextChopReact = Time.time + chopCooldown;
                chopTime = 0f;
                Say(Next(chopLines, ref lastChop));
                Emote("!", new Color(0.55f, 1f, 0.45f, 1f));
                if (life != null) life.Hop();
            }
        }
    }

    // ------------------------------------------------------------ реакции от NpcLife
    void OnGreeted()
    {
        Emote("!", new Color(1f, 0.9f, 0.3f, 1f));
        if (CanBark() && Random.value < greetBarkChance) Say(Next(greetLines, ref lastGreet));
    }

    void OnDialogueEnded()
    {
        Emote("♥", new Color(1f, 0.5f, 0.6f, 1f));
    }

    // ------------------------------------------------------------ вспомогательное
    void Say(string text)
    {
        if (speaker == null || string.IsNullOrEmpty(text)) return;
        DialogueManager.Say(speaker, text);
    }

    // случайная строка, но не та же самая, что в прошлый раз
    static string Next(string[] arr, ref int last)
    {
        if (arr == null || arr.Length == 0) return null;
        if (arr.Length == 1) { last = 0; return arr[0]; }
        int i;
        do { i = Random.Range(0, arr.Length); } while (i == last);
        last = i;
        return arr[i];
    }

    // ------------------------------------------------------------ эмодзи над головой
    public void Emote(string symbol, Color color)
    {
        if (!emotes || !isActiveAndEnabled || string.IsNullOrEmpty(symbol)) return;
        StartCoroutine(EmoteRoutine(symbol, color));
    }

    IEnumerator EmoteRoutine(string symbol, Color color)
    {
        if (emoteFont == null) emoteFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var go = new GameObject("NpcEmote");
        var tm = go.AddComponent<TextMesh>();
        tm.font = emoteFont;
        tm.text = symbol;
        tm.fontSize = 64;
        tm.characterSize = emoteSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        if (emoteFont != null) mr.sharedMaterial = emoteFont.material;
        mr.sortingOrder = 60;

        for (float t = 0f; t < emoteTime; t += Time.deltaTime)
        {
            if (go == null) yield break;
            float k = t / emoteTime;

            // «выскакивает» с перелётом, потом плавно поднимается и тает
            float pop = t < 0.2f ? Mathf.SmoothStep(0f, 1.25f, t / 0.2f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.2f) / 0.2f));
            go.transform.localScale = Vector3.one * pop;

            Vector3 follow = (speaker != null ? speaker.BubblePoint : transform.position + Vector3.up * 2.6f) + Vector3.down * 0.35f;
            go.transform.position = follow + Vector3.up * (emoteRise * k);

            var cam = Camera.main;
            if (cam != null) go.transform.rotation = cam.transform.rotation;

            var c = color;
            c.a = k < 0.6f ? 1f : Mathf.Clamp01(1f - (k - 0.6f) / 0.4f);
            tm.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}

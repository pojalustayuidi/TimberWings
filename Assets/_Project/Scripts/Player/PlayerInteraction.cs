// Assets/_Project/Scripts/Player/PlayerInteraction.cs
// Рубка деревьев на ближайшем дереве внутри радиуса. Два режима (PlayerInteraction.RhythmChop):
//   РИТМ (по умолчанию): E входит в рубку, дальше каждое нажатие E = удар. Попал маркером в зелёную или золотую зону:
//        сильный удар; промах не наказывается, просто слабее. Серия удачных ударов даёт бонус. Числа: GameBalance.Rhythm*.
//        Выход из рубки: пошёл (WASD или стик). Надрубленное дерево помнит прогресс (TreeHits.rhythmProgress).
//   ЗАЖМИ E (по умолчанию с v2.1): зажимаешь E -> тикает таймер GameBalance.ChopTime (3с) -> дерево падает.
//        БОНУС: пока держишь E, бегает маркер; Пробел в зелёной зоне срезает часть времени, промах ничего не делает (GameBalance.ChopBonus*).
//   В обоих режимах дальше одно и то же: дерево падает -> бревна летят в HUD.
// РУБКА МЫШЬЮ (ПК): наведи курсор на дерево (оно подсветится) и держи ЛКМ: то же самое, что держать E. В ритм-режиме клик = удар.
//   Курсор может быть на кроне, не обязательно на стволе. Дерево дальше radius: клик показывает «Подойди ближе». Клик по кнопкам и окнам игнорируется.
// Интеграция: MobileInput, InteractionHub.OwnsInput, BackpackTag, ChopProgress, ResourceItem, TreeHits
// РЮКЗАК (вариант «в», «Уют без наказаний»): рубить можно, пока есть хотя бы 1 свободный слот. Дерево даёт, сколько влезло,
// над ним заранее видно «+2/4», после падения всплывает «+2 из 4, рюкзак полон». Лимит не нарушается никогда.
// Свободные слоты считает PlayerInventory.FreeSlots (несём + ещё летящие в HUD), выдача только через GrantReward → ReserveLogs.
// Ветка «Лесоруб» (задача 3): серия рубки ускоряет рубку (GatherTime), «Крепкая древесина» и золотое дерево меняют число бревен,
// «Эхо удара» валит соседа (CompleteChop). Все формулы и порядок бонусов описаны в LumberSkills.
using UnityEngine;

[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(BackpackTag))]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Ввод")]
    public KeyCode chopKey = KeyCode.E;
    public float radius = 2.5f;
    public string resourceTag = "Resource";
    public LayerMask mask = ~0;

    [Header("Рубка мышью (ПК)")]
    [Tooltip("Наведи курсор на дерево и держи ЛКМ. E работает как раньше")]
    public bool mouseChop = true;
    [Tooltip("Как близко к дереву на экране должен быть курсор, в долях высоты экрана (крона кликается, не только ствол)")]
    public float mousePickScreen = 0.10f;

    [Header("Ход к дереву (клик по дальнему дереву / E без дерева рядом)")]
    [Tooltip("Клик по дереву вдали или E, когда рядом нет дерева: персонаж идёт к нему, дерево подпрыгивает и подсвечивается")]
    public bool walkToTree = true;
    [Tooltip("E без дерева рядом: идём к ближайшему не дальше этого расстояния, м")]
    public float walkRangeE = 9f;
    [Tooltip("Клик по дальнему дереву: дальше этого расстояния не идём (покажет «Подойди ближе»), м")]
    public float walkRangeClick = 30f;

    [Header("Настройка (если пусто - берет из GameBalance)")]
    [Tooltip("Переопределить время рубки. -1 = брать из GameBalance.ChopTime")]
    public float gatherTimeOverride = -1f;

    [Header("Анимация (необязательно)")]
    public Animator animator;
    [Tooltip("Имя bool-параметра в аниматоре. Регистр не важен: если в контроллере isChopping, найдётся и так")]
    public string chopBool = "isChopping";
    public string chopTrigger = "";
    [Tooltip("Скорость проигрывания клипа рубки (1 = как в файле)")]
    public float chopAnimSpeed = 1f;

    [Header("Удары топора (звук, тряска дерева, щепки)")]
    [Tooltip("Пауза между ударами, если удар не приходит событием из анимации")]
    public float hitInterval = 0.62f;
    public float firstHitDelay = 0.25f;
    [Tooltip("true = удар приходит Animation Event OnChopHit из клипа (компонент PlayerAnimationEvents на модели). false = по таймеру hitInterval")]
    public bool useAnimationEvents = false;

    [Header("Ритм-рубка")]
    [Tooltip("Через сколько секунд после нажатия E приходит удар (тряска, щепки, звук, прогресс). Подгони под кадр удара в клипе рубки. 0 = сразу")]
    public float impactDelay = 0.2f;

    // false = рубка выключена (IntroFlow держит её выключенной, пока игрок не поговорил с Мирой)
    public static bool ChoppingEnabled = true;

    // true = ритм-рубка, false = старая: зажми E (режим для доступности). Хранится в PlayerPrefs, переключать через SetRhythmChop
    public static bool RhythmChop = GameBalance.RhythmChopDefault;
    const string RhythmPref = "chop_rhythm";
    static PlayerInteraction inst;

    // бонус «Пробел в зелёной зоне» в режиме «зажми E». Хранится в PlayerPrefs (можно выключить в настройках доступности)
    public static bool ChopBonus = GameBalance.ChopBonusDefault;
    const string BonusPref = "chop_bonus";
    public static void SetChopBonus(bool on) { ChopBonus = on; PlayerPrefs.SetInt(BonusPref, on ? 1 : 0); }

    // Обучение бонусу: сколько раз игрок уже попал Пробелом в зелёную зону (хранится в PlayerPrefs).
    // Пока их меньше GameBalance.ChopBonusTeachHits, над деревом, в цели и на шкале висит подсказка про Пробел.
    const string BonusHitsPref = "chop_bonus_hits";
    static int bonusHits = -1;     // -1 = ещё не читали из PlayerPrefs

    public static bool BonusTeaching
    {
        get
        {
            if (RhythmChop || !ChopBonus || Application.isMobilePlatform) return false;   // на телефоне Пробела нет
            if (bonusHits < 0) bonusHits = PlayerPrefs.GetInt(BonusHitsPref, 0);
            return bonusHits < GameBalance.ChopBonusTeachHits;
        }
    }

    static void RegisterBonusHit()
    {
        if (bonusHits < 0) bonusHits = PlayerPrefs.GetInt(BonusHitsPref, 0);
        if (bonusHits >= GameBalance.ChopBonusTeachHits) return;
        bonusHits++;
        PlayerPrefs.SetInt(BonusHitsPref, bonusHits);
    }

    // для тестов: показать обучение заново (Tools или консоль: PlayerInteraction.ResetBonusTeaching())
    public static void ResetBonusTeaching() { bonusHits = 0; PlayerPrefs.SetInt(BonusHitsPref, 0); }

    public static void SetRhythmChop(bool on)
    {
        RhythmChop = on;
        PlayerPrefs.SetInt(RhythmPref, on ? 1 : 0);
        if (inst != null) inst.StopChop();   // стойка прерывается, дальше рубка пойдёт по новым правилам
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { ChoppingEnabled = true; inst = null; bonusHits = -1; }

    float lockedHintAt;

    // рубка мышью
    bool clickArmed;                   // ЛКМ нажата на дереве в зоне досягаемости и ещё не отпущена
    int clickDownFrame = -1;           // кадр, в котором ЛКМ нажали на дереве
    float farToastAt;
    static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> uiHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(8);
    float nextHitAt;
    bool chopParamResolved;

    // Публичное API для других систем (ResourceHighlighter, InteractionHint, ActionPrompt, CameraFollow)
    // дерево, к которому сейчас идём (клик или E издалека). null = не идём. ResourceHighlighter подсвечивает его так же, как цель рубки
    public ResourceItem WalkTarget { get; private set; }
    // дерево под курсором (ПК), в том числе далёкое: ResourceHighlighter подсвечивает его заранее. null = курсор не над деревом
    public ResourceItem HoverItem { get; private set; }
    float hoverAt;
    public Transform CurrentTarget { get; private set; }
    public ResourceItem TargetItem { get; private set; }
    public bool IsChopping { get; private set; }
    public float Progress { get; private set; }

    PlayerInventory inventory;
    PlayerMovement movement;
    BackpackTag backpack;
    PlayerStats stats;
    ChopProgress progressUI;

    readonly Collider[] buffer = new Collider[48];
    float holdTime;
    int hitsDone;
    float findTimer;
    Transform lastTarget;
    ResourceItem lastItem;

    PlayerUpgrades upgrades;
    // «пауза» рубки: отпустил E, но в течение GameBalance.ChopGrace секунд можно продолжить с того же места
    float savedHold, savedUntil;
    ResourceItem savedItem;

    // состояние бонуса «зажми E + Пробел»
    float bonusClock;                  // время с начала рубки: по нему бегает маркер
    float bonusNextAt;                 // раньше этого момента Пробел не принимается

    // состояние ритм-рубки
    PlayerHeadHud hud;
    TreeHits rhythmTree;
    bool rhythmActive;                 // игрок в «стойке»: каждое нажатие E = удар
    float rhythmClock;                 // время с начала стойки: по нему бегает маркер
    float nextSwingAt;                 // раньше этого момента ударить нельзя
    bool pendingHit;                   // удар нажат, но ещё «летит» (impactDelay)
    float pendingDue;
    int pendingQuality;                // 0 слабый, 1 хороший, 2 идеальный
    int combo;                         // удачных ударов подряд
    int chainLock;                     // «Серия рубки»: серия, зафиксированная в начале рубки этого дерева (не растёт посреди рубки)
    bool cleanCut;                     // добивающий идеальный удар в серии: +1 бревно
    bool animBoolOn;
    float animUntil;
    string comboLabel = "В ритм!";
    int comboLabelFor = -1;

    public bool InRhythm { get { return rhythmActive; } }
    public int Combo { get { return combo; } }

    public float GatherTime
    {
        get
        {
            if (gatherTimeOverride > 0f) return gatherTimeOverride;
            // «Сила удара» и таланты уже внутри upgrades.ChopTime (3 с × 0.9^L × ...). Серия рубки делит это время на (1 + бонус):
            // бонусы перемножаются. Без навыка (или после сброса) ChainBonus = 0, и время прежнее.
            float t = upgrades != null ? upgrades.ChopTime : GameBalance.ChopTime;
            int chain = IsChopping ? chainLock : LumberSkills.ChainNow;
            // размер дерева: молодое рубится быстрее (×0.7), старое дольше (×1.5). Во время рубки цель не меняется (смена цели сбрасывает рубку)
            float sizeMul = TargetItem != null ? TargetItem.ChopMul : 1f;
            return t / (1f + LumberSkills.ChainBonus(chain)) * sizeMul;
        }
    }

    // сколько бревен у дерева: база без случайной «Двойной добычи» (3 с «Крепкой древесиной», 6 у золотого, 1 / 2 / 4 по размеру)
    public int WoodFull { get { return LumberSkills.BaseWood(TargetItem); } }

    // сколько из них ВЛЕЗЕТ в рюкзак прямо сейчас: это число плашка рисует как «+2», а «+2/4», если влезает не всё
    public int ExpectedWood
    {
        get
        {
            int full = WoodFull;
            return backpack != null ? Mathf.Min(full, backpack.Free) : full;
        }
    }

    // «нажал» и «держу»: E (или кнопка на телефоне) либо ЛКМ по дереву
    bool PressDown { get { return MobileInput.ActionDown || (clickArmed && clickDownFrame == Time.frameCount); } }
    bool PressHeld { get { return MobileInput.ActionHeld || (clickArmed && Input.GetMouseButton(0)); } }

    static bool DialogueBusy()
    {
        return DialogueManager.IsBusy || DialogueSequence.AnyPlaying || DialogueManager.BlocksUntilRelease;
    }

    // мышь работает как «руки»: только на ПК, курсор свободен, нет окон и реплик
    bool MouseActive()
    {
        if (!mouseChop || Application.isMobilePlatform || !Input.mousePresent) return false;
        if (Cursor.lockState != CursorLockMode.None && !OrbitCamera.CrosshairActive) return false;   // курсор скрыт, но есть прицел OrbitCamera: целимся им
        if (MobileInput.ModalOpen || DialogueBusy()) return false;
        return true;
    }

    // курсор над кнопкой интерфейса (смотрим только на Selectable: сплошные фоны HUD клик не глушат)
    static bool OverUiButton(Vector2 pos)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        uiHits.Clear();
        es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = pos }, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            var sel = uiHits[i].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>();
            if (sel != null && sel.IsInteractable()) return true;
        }
        return false;
    }

    // Дерево под курсором. Два шага:
    //  1) луч из камеры через зоны выбора (триггеры по кроне, их видит QueryTriggerInteraction.Collide; чужие коллайдеры не мешают,
    //     потому что берём только деревья). Если луч прошёл через несколько крон, берём ту, что ближе к камере: как и видно на экране.
    //  2) луч мимо: ищем по экрану дерево, к чьей линии «земля → верхушка» курсор ближе всего (раньше считали одну точку на стволе,
    //     поэтому за край кроны и за верхушку промахивались, а в чаще цеплялось не то дерево). Допуск: mousePickScreen от высоты экрана.
    // Сначала деревья в досягаемости; если рядом с курсором только далёкие, возвращаем ближайшее из них и far = true.
    readonly RaycastHit[] rayBuf = new RaycastHit[24];

    ResourceItem PickUnderMouse(out bool far)
    {
        far = false;
        var cam = Camera.main;
        if (cam == null) return null;
        Vector2 m = OrbitCamera.PointerPos;
        if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return null;

        float reach = radius + 0.5f;

        // 1) луч
        int rn = Physics.RaycastNonAlloc(cam.ScreenPointToRay(m), rayBuf, 200f, ~0, QueryTriggerInteraction.Collide);
        ResourceItem rayBest = null;
        float rayDist = float.MaxValue;
        for (int i = 0; i < rn; i++)
        {
            var col = rayBuf[i].collider;
            if (col == null) continue;
            var it = col.GetComponentInParent<ResourceItem>();
            if (it == null || !it.gameObject.activeInHierarchy) continue;
            if (rayBuf[i].distance < rayDist) { rayDist = rayBuf[i].distance; rayBest = it; }
        }
        if (rayBest != null)
        {
            Vector3 dr = rayBest.transform.position - transform.position;
            dr.y = 0f;
            far = dr.magnitude > reach;
            return rayBest;
        }

        // 2) по экрану: расстояние от курсора до отрезка «основание → верхушка» (в пикселях)
        float maxPx = Mathf.Max(40f, Screen.height * Mathf.Max(0.01f, mousePickScreen));
        float nearPx = maxPx, farPx = maxPx;
        ResourceItem nearBest = null, farBest = null;

        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it == null || !it.gameObject.activeInHierarchy) continue;

            Vector3 p = it.transform.position;
            Vector3 sb = cam.WorldToScreenPoint(p + Vector3.up * 0.2f);
            Vector3 st = cam.WorldToScreenPoint(p + Vector3.up * (3.4f * Mathf.Max(0.5f, it.ScaleMul)));
            if (sb.z <= 0f || st.z <= 0f) continue;
            float px = DistToSegment(m, sb, st);

            Vector3 dp = p - transform.position;
            dp.y = 0f;
            if (dp.magnitude <= reach)
            {
                if (px < nearPx) { nearPx = px; nearBest = it; }
            }
            else if (px < farPx) { farPx = px; farBest = it; }
        }

        if (nearBest != null) return nearBest;
        far = farBest != null;
        return farBest;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        float t = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
        return (p - (a + ab * t)).magnitude;
    }

    // клик ЛКМ: если курсор на дереве рядом, «взводим» рубку; если на далёком, просим подойти
    void UpdateClick()
    {
        if (!MouseActive()) { clickArmed = false; HoverItem = null; return; }

        // подсветка под курсором: считаем 20 раз в секунду, лишнего на кадр не тратим
        if (Time.unscaledTime >= hoverAt)
        {
            hoverAt = Time.unscaledTime + 0.05f;
            bool hf;
            HoverItem = (ChoppingEnabled && !OverUiButton(OrbitCamera.PointerPos)) ? PickUnderMouse(out hf) : null;
        }

        if (Input.GetMouseButtonDown(0))
        {
            clickArmed = false;
            if (!OverUiButton(OrbitCamera.PointerPos))
            {
                bool far;
                var it = PickUnderMouse(out far);
                if (it != null)
                {
                    if (!far)
                    {
                        clickArmed = true;
                        clickDownFrame = Time.frameCount;
                        findTimer = 0f;      // цель пересчитать прямо сейчас (дерево под курсором)
                        if (ChoppingEnabled && !IsChopping && !rhythmActive) it.Poke();   // отклик на клик: дерево подпрыгнуло
                    }
                    else if (ChoppingEnabled)
                    {
                        // дерево далеко: идём к нему. Не получилось (слишком далеко): как раньше, просим подойти
                        if (!StartWalk(it, walkRangeClick) && Time.time >= farToastAt)
                        {
                            farToastAt = Time.time + 2f;
                            SaveIndicator.ShowToast("Подойди ближе к дереву");
                        }
                    }
                }
            }
        }

        if (clickArmed && !Input.GetMouseButton(0)) clickArmed = false;
    }

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        movement = GetComponent<PlayerMovement>();
        backpack = GetComponent<BackpackTag>();
        stats = GetComponent<PlayerStats>();
        progressUI = GetComponent<ChopProgress>();
        upgrades = GetComponent<PlayerUpgrades>();
        if (upgrades == null) upgrades = gameObject.AddComponent<PlayerUpgrades>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // чтобы MobileInput знал какую клавишу слушать
        MobileInput.ActionKey = chopKey;
        inst = this;
        RhythmChop = PlayerPrefs.GetInt(RhythmPref, GameBalance.RhythmChopDefault ? 1 : 0) == 1;
        ChopBonus = PlayerPrefs.GetInt(BonusPref, GameBalance.ChopBonusDefault ? 1 : 0) == 1;
    }

    void OnDestroy()
    {
        if (inst == this) inst = null;
    }

    void OnDisable()
    {
        clickArmed = false;
        StopChop();
    }

    void Update()
    {
        UpdateClick();

        // цель хода пропадает, когда дошли, прервали ход (WASD, разговор, рубка) или дерево срубили
        if (WalkTarget != null && (movement == null || !movement.AutoWalking || !WalkTarget.gameObject.activeInHierarchy))
            WalkTarget = null;

        // E занята диалогом/NPC - рубку не начинаем и не продолжаем.
        // Исключение: клик ЛКМ по дереву (у NPC своя клавиша E, клик им не мешает), но не во время реплик
        if (InteractionHub.OwnsInput && !(clickArmed && !DialogueBusy()))
        {
            StopChop();
            FindTarget(true);
            return;
        }

        // рубка закрыта (обучение: сначала поговори с Мирой)
        if (!ChoppingEnabled)
        {
            StopChop();
            FindTarget(false);
            bool nearTree = CurrentTarget != null;
            CurrentTarget = null;     // подсказки и подсветка деревьев молчат
            TargetItem = null;
            if (nearTree && PressDown && Time.time >= lockedHintAt)
            {
                lockedHintAt = Time.time + 6f;
                DialogueManager.SayAs("Лео", "Сначала надо поговорить с Мирой.");
            }
            return;
        }

        FindTarget(false);

        bool hasTarget = CurrentTarget != null && TargetItem != null && TargetItem.gameObject.activeInHierarchy;
        bool isFull = backpack != null && backpack.NoRoom;   // места нет вообще (с учётом брёвен, что ещё летят в HUD)

        if (!hasTarget)
        {
            StopChop();
            // E (или кнопка действия на телефоне) без дерева рядом: идём к ближайшему
            if (walkToTree && MobileInput.ActionDown && WalkTarget == null) StartWalkNearest();
            return;
        }

        if (isFull)
        {
            // места нет (хотя бы один слот свободен: рубить можно, лишнее честно покажется заранее) - дергаем табличку
            if (PressDown) backpack.Flash();
            StopChop();
            return;
        }

        if (RhythmChop)
        {
            if (!rhythmActive)
            {
                if (IsChopping) StopChop();                       // переключили режим посреди рубки «зажми E»
                if (PressDown) BeginRhythm();        // первое нажатие только входит в рубку, удара нет
            }
            else
            {
                TickRhythm();
            }
        }
        else if (PressHeld)
        {
            if (rhythmActive) StopChop();
            TickChop();
        }
        else
        {
            StopChop(true);   // отпустил: прогресс держится 0.5 с
        }
    }

    // ------------------------------------------------------------ ход к дереву
    // Идём к дереву it. false = не пошли (выключено, дерево срублено, дальше maxRange)
    bool StartWalk(ResourceItem it, float maxRange)
    {
        if (!walkToTree || movement == null || it == null || !it.gameObject.activeInHierarchy) return false;
        Vector3 d = it.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude > maxRange) return false;

        // встаём у ствола, но так, чтобы дерево было в радиусе рубки (radius) и мы не упёрлись в сплошной ствол
        float stop = Mathf.Clamp(it.TrunkRadius + 1.05f, 1.2f, Mathf.Max(1.2f, radius - 0.35f));
        if (d.magnitude <= stop) { it.Poke(); return true; }   // уже стоим где надо: просто «тык»

        WalkTarget = it;
        it.Poke();
        SfxManager.Play("hint", 0.45f, 1.25f);
        movement.WalkTo(it.transform, stop, () => OnWalkArrived(it));
        return true;
    }

    void StartWalkNearest()
    {
        ResourceItem best = null;
        float bestSqr = walkRangeE * walkRangeE;
        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            if (it == null || !it.gameObject.activeInHierarchy) continue;
            Vector3 d = it.transform.position - transform.position;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bestSqr) { bestSqr = s; best = it; }
        }
        if (best != null) StartWalk(best, walkRangeE);
    }

    // дошли: дерево подпрыгивает ещё раз («я здесь»), цель рубки пересчитывается сразу
    void OnWalkArrived(ResourceItem it)
    {
        WalkTarget = null;
        if (it == null || !it.gameObject.activeInHierarchy) return;
        it.Poke();
        findTimer = 0f;
    }

    // В контроллере параметр называется isChopping, в коде раньше было IsChopping: Unity различает регистр,
    // и анимация рубки не включалась. Подбираем реальное имя один раз, без учёта регистра.
    void ResolveChopParam()
    {
        if (chopParamResolved || animator == null || animator.runtimeAnimatorController == null) return;
        chopParamResolved = true;
        if (string.IsNullOrEmpty(chopBool)) return;
        string found = null;
        foreach (var p in animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.name == chopBool) { found = p.name; break; }
            if (found == null && string.Equals(p.name, chopBool, System.StringComparison.OrdinalIgnoreCase)) found = p.name;
        }
        if (found != null) chopBool = found;
        else Debug.LogWarning("[PlayerInteraction] В аниматоре нет bool-параметра '" + chopBool + "': анимация рубки не включится.", this);
    }

    // Один удар топора: тряска дерева, щепки, событие для звука. Зовётся таймером или Animation Event из клипа.
    public void NotifyHit()
    {
        if (RhythmChop) return;   // в ритм-режиме удары идут от нажатий E (ResolveHit), Animation Event не нужен
        if (!IsChopping || TargetItem == null) return;
        var th = TargetItem.GetComponent<TreeHits>();
        if (th == null) th = TargetItem.GetComponentInChildren<TreeHits>();
        if (th == null) th = TargetItem.gameObject.AddComponent<TreeHits>();   // TreeHits нигде не добавлялся, дерево не реагировало на удары
        th.Shake();
        GameEvents.RaiseChopHit();
    }

    void FindTarget(bool force)
    {
        findTimer -= Time.deltaTime;
        if (!force && findTimer > 0f && lastTarget == CurrentTarget) return;
        findTimer = 0.08f;

        // пока рубим, цель не меняем: курсор ушёл с дерева или рядом оказалось другое дерево ближе, рубка не сбрасывается
        if (IsChopping && lastTarget != null && lastItem != null && lastItem.gameObject.activeInHierarchy)
        {
            Vector3 ld = lastTarget.position - transform.position;
            ld.y = 0f;
            float keepR = radius * 1.35f;
            if (ld.sqrMagnitude <= keepR * keepR) return;
        }

        // дерево под курсором (ПК): оно и есть цель, пока в зоне досягаемости; курсор мимо: цель = ближайшее, как раньше
        ResourceItem hov = null;
        if (MouseActive() && !OverUiButton(OrbitCamera.PointerPos))
        {
            bool hovFar;
            hov = PickUnderMouse(out hovFar);
            if (hovFar) hov = null;
        }
        Transform hovT = null;
        float hovSqr = float.MaxValue;

        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Collide);
        Transform best = null;
        ResourceItem bestItem = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            var c = buffer[i];
            if (c == null || !c.CompareTag(resourceTag)) continue;
            var item = c.GetComponentInParent<ResourceItem>();
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = c.transform; bestItem = item; }
            if (hov != null && item == hov && d < hovSqr) { hovSqr = d; hovT = c.transform; }
        }

        if (hovT != null) { best = hovT; bestItem = hov; }

        if (best != lastTarget)
        {
            if (IsChopping) StopChop();   // цель сменилась: прогресс старого дерева не переносим на новое
            savedItem = null;
            if (lastItem != null) lastItem.SetChopping(false);
            lastTarget = best;
            lastItem = bestItem;
        }

        CurrentTarget = best;
        TargetItem = bestItem;
    }

    void TickChop()
    {
        if (TargetItem == null) return;

        if (!IsChopping)
        {
            IsChopping = true;
            bool resume = savedItem == TargetItem && Time.time <= savedUntil;
            if (!resume) chainLock = LumberSkills.ChainNow;   // серия фиксируется на всё дерево; при возобновлении (пауза 0.5 с) остаётся прежней
            holdTime = resume ? savedHold : 0f;
            savedItem = null;
            Progress = Mathf.Clamp01(holdTime / Mathf.Max(0.1f, GatherTime));
            hitsDone = Mathf.FloorToInt(Progress * GameBalance.HitsPerTree);
            nextHitAt = Time.time + Mathf.Max(0.05f, firstHitDelay);
            bonusClock = 0f;                       // маркер бонуса стартует с левого края
            bonusNextAt = Time.time + 0.2f;        // защита от случайного Пробела в момент начала рубки
            ResolveChopParam();
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(chopBool)) animator.SetBool(chopBool, true);
                if (!string.IsNullOrEmpty(chopTrigger)) animator.SetTrigger(chopTrigger);
            }
            TargetItem.SetChopping(true);
        }

        holdTime += Time.deltaTime;
        Progress = Mathf.Clamp01(holdTime / Mathf.Max(0.1f, GatherTime));

        // UI кольца над головой
        if (progressUI != null)
            progressUI.Show(Progress, hitsDone, GameBalance.HitsPerTree);

        // Счётчик ударов для кольца над головой
        hitsDone = Mathf.FloorToInt(Progress * GameBalance.HitsPerTree);

        // бонус: Пробел в зелёной зоне срезает часть времени (промах ничего не делает)
        TickChopBonus();
        if (!IsChopping) return;     // бонус мог срубить дерево

        // Удары топора: при HitsPerTree = 1 старая проверка по прогрессу не срабатывала ни разу, дерево стояло как вкопанное
        if (!useAnimationEvents && Time.time >= nextHitAt)
        {
            nextHitAt = Time.time + Mathf.Max(0.15f, hitInterval);
            NotifyHit();
        }

        if (holdTime >= GatherTime)
        {
            CompleteChop();
        }
    }

    // Маркер бежит туда-сюда, пока E зажата. Пробел в зелёной зоне: holdTime += доля от GatherTime (сила топора учитывается сама).
    void TickChopBonus()
    {
        if (!ChopBonus)
        {
            Hud.SetChopBonus(false, 0f, 0f, 0f, false);
            return;
        }

        bonusClock += Time.deltaTime;
        float marker = Mathf.PingPong(bonusClock / Mathf.Max(0.1f, GameBalance.ChopBonusSweepTime), 1f);
        float cd = Mathf.Clamp01((bonusNextAt - Time.time) / Mathf.Max(0.05f, GameBalance.ChopBonusCooldown));

        if (MobileInput.BonusDown && Time.time >= bonusNextAt)
        {
            bonusNextAt = Time.time + GameBalance.ChopBonusCooldown;     // пауза и после промаха: спам не работает
            bool hit = Mathf.Abs(marker - 0.5f) <= GameBalance.ChopBonusZoneHalf;
            Hud.RhythmFlash(hit ? 1 : 0);                                // зелёная вспышка маркера / серая
            if (hit)
            {
                float saved = GatherTime * GameBalance.ChopBonusFraction;
                holdTime += saved;                                       // дерево падает чуть раньше
                RegisterBonusHit();
                Hud.ShowBonusResult(true, saved);                        // «Отлично! −0,3 с» на шкале
                var th = TargetItem != null ? GetTreeHits(TargetItem) : null;
                if (th != null) th.Shake(1.5f);                          // сильнее качается, больше щепок
                GameEvents.RaiseChopHit();
                GameEvents.RaiseChopQuality(1);
                Progress = Mathf.Clamp01(holdTime / Mathf.Max(0.1f, GatherTime));
                if (progressUI != null) progressUI.Show(Progress, hitsDone, GameBalance.HitsPerTree);
                if (holdTime >= GatherTime) { CompleteChop(); return; }
            }
            else
            {
                Hud.ShowBonusResult(false, 0f);                          // «Мимо, ещё раз» (без наказания)
                GameEvents.RaiseChopQuality(0);
            }
        }

        Hud.SetChopBonus(true, marker, GameBalance.ChopBonusZoneHalf, cd, BonusTeaching);
    }

    void CompleteChop()
    {
        if (TargetItem == null) return;

        // страховка: пока рубили, места не осталось (вместимость упала, питомец ушёл). Дерево не валим, ничего не теряется
        if (backpack != null && backpack.NoRoom)
        {
            backpack.Flash();
            StopChop();
            return;
        }

        var item = TargetItem;
        var treePos = item.transform.position;
        item.SetChopping(false);

        if (progressUI != null) progressUI.Complete();
        if (rhythmActive && hud != null) hud.CompleteChop();

        // ---- сколько бревен (порядок описан в LumberSkills) ----
        // 1) база дерева: 2; «Крепкая древесина» делает 3; золотое дерево 6 (берётся большее)
        // 2) «Двойная добыча»: ×2 к базе (у золотого 12, у крепкого 6)
        // 3) «Чистый спил» ритм-режима: +1 после умножения
        // 4) «Эхо удара» даёт отдельное бревно с упавшего соседа (ниже), «Двойная добыча» на него не действует
        int wood = LumberSkills.BaseWood(item);
        if (upgrades != null && Random.value < upgrades.DoubleChance) wood *= 2;   // «Двойная добыча»
        if (rhythmActive && cleanCut) wood += GameBalance.RhythmCleanCutLogs;       // «Чистый спил»
        if (rhythmTree != null) rhythmTree.rhythmProgress = 0f;
        int xp = GameBalance.XpPerTree;
        xp += SilverTree.TakeBonusXp(item);   // серебряное дерево: бонусный опыт, звук и тост (0 у обычного)

        bool wasGolden = item.IsGolden;
        LumberSkills.RegisterFelled(chainLock);        // «Серия рубки»: это дерево продлило серию

        // Бревна летят в HUD, начисляем по прилёту. Влезло столько, сколько свободно (ReserveLogs резервирует место сразу,
        // поэтому второе дерево подряд не обойдёт проверку, пока бревна ещё в полёте)
        int granted = GrantReward(treePos + Vector3.up * 1.2f, wood, xp);
        if (granted < wood) ShowPartialNote(treePos, granted, wood);

        // «Эхо удара»: бросок кубика и поиск соседа ДО того, как это дерево спрячется. Если места на бонусное бревно нет,
        // эхо не срабатывает вовсе: сосед остаётся стоять, ничего не пропадает
        ResourceItem echo = null;
        if (backpack == null || backpack.Free >= GameBalance.SkillEchoBonusWood) echo = LumberSkills.RollEcho(item);
        // событие для IntroFlow / квестов - кидаем сразу, не ждем полета
        GameEvents.RaiseTreeChopped();
        if (wasGolden) SfxManager.Play("coin", 0.9f, 0.9f);

        savedItem = null;
        item.ChopDown();
        StopChop();

        // «Эхо удара»: сосед падает тоже. Квестовое событие TreeChopped и XP для него не даются (это не рубка руками)
        if (echo != null) FellEcho(echo);

        // сразу ищем следующее дерево
        FindTarget(true);
    }

    // Выдать бревна и XP: с анимацией полёта в HUD, а если ChopRewards в сцене нет, то сразу.
    // ЕДИНСТВЕННОЕ место, где рубка кладёт брёвна: все пути (база × «Двойная добыча» × «Чистый спил», золотое, эхо) идут сюда.
    // Возвращает, сколько брёвен реально выдано (может быть меньше want, если рюкзак почти полон).
    int GrantReward(Vector3 pos, int want, int xp)
    {
        // место резервируется СРАЗУ, до полёта: Carried + летящие никогда не превысят Capacity
        int wood = inventory != null ? inventory.ReserveLogs(want) : 0;

        // Если ChopRewards нет в сцене - начисляем сразу
        bool hasRewardSystem = FindFirstObjectByType<ChopRewards>() != null || GameObject.Find("ChopRewards") != null;

        if (hasRewardSystem)
        {
            ChopRewards.Give(pos, wood, xp,
                onLogLanded: () =>
                {
                    if (inventory != null) inventory.LandReserved(1);
                },
                onXp: xp > 0 ? (System.Action)(() =>
                {
                    if (stats != null) stats.AddXP(xp);
                }) : null);
        }
        else
        {
            // Fallback без анимации полета
            if (inventory != null) inventory.LandReserved(wood);
            if (xp > 0 && stats != null) stats.AddXP(xp);
            // попапы если есть
            if (xp > 0 && RewardPopups.Instance != null) RewardPopups.Instance.Show(pos, wood, xp);
            else if (wood > 0 && HudGainAnchor.Find(HudGain.Kind.Wood) != null) HudGain.Show(HudGain.Kind.Wood, wood);
        }
        return wood;
    }

    // «+2 из 4, рюкзак полон»: игрок видит, что часть брёвен не влезла (предупреждение было ещё до рубки: «+2/4» над головой)
    void ShowPartialNote(Vector3 treePos, int got, int want)
    {
        string msg = "+" + got + " из " + want + ", рюкзак полон";
        if (RewardPopups.Instance != null) RewardPopups.Instance.ShowText(treePos + Vector3.up * 2.4f, msg, new Color32(0xF2, 0xA9, 0x3B, 255), 40f);
        else SaveIndicator.ShowToast(msg);
    }

    // «Эхо удара»: соседнее дерево падает само и даёт GameBalance.SkillEchoBonusWood брёвен
    void FellEcho(ResourceItem echo)
    {
        if (echo == null || !echo.gameObject.activeInHierarchy) return;
        Vector3 pos = echo.transform.position;
        echo.ChopDown();
        SfxManager.Play("fall", 0.8f, 1.12f);
        GrantReward(pos + Vector3.up * 1.2f, GameBalance.SkillEchoBonusWood, 0);   // место под него проверено в CompleteChop
    }

    void StopChop(bool keepProgress = false)
    {
        if (!IsChopping) return;
        if (keepProgress && TargetItem != null && holdTime > 0f)
        {
            savedItem = TargetItem;
            savedHold = holdTime;
            savedUntil = Time.time + GameBalance.ChopGrace;
        }
        IsChopping = false;
        if (rhythmActive)
        {
            rhythmActive = false;
            pendingHit = false;
            combo = 0;
            cleanCut = false;
            rhythmTree = null;
            animBoolOn = false;
            if (hud != null) hud.HideChop();
        }
        holdTime = 0f;
        Progress = 0f;
        if (TargetItem != null) TargetItem.SetChopping(false);
        if (lastItem != null) lastItem.SetChopping(false);
        if (animator != null && !string.IsNullOrEmpty(chopBool)) animator.SetBool(chopBool, false);
        if (progressUI != null) progressUI.Hide();
    }

    // ============================================================ ритм-рубка

    PlayerHeadHud Hud { get { if (hud == null) hud = PlayerHeadHud.Get(gameObject); return hud; } }

    // положение маркера 0..1: ходит туда-сюда, центр (0.5) пересекает каждые RhythmSweepTime секунд
    float Marker01() { return Mathf.PingPong(rhythmClock / Mathf.Max(0.1f, GameBalance.RhythmSweepTime), 1f); }

    // сила топора и таланты: во сколько раз рубка быстрее базовой (3 с)
    float PowerMul() { return GameBalance.ChopTime / Mathf.Max(0.1f, GatherTime); }

    static TreeHits GetTreeHits(ResourceItem item)
    {
        var th = item.GetComponent<TreeHits>();
        if (th == null) th = item.GetComponentInChildren<TreeHits>();
        if (th == null) th = item.gameObject.AddComponent<TreeHits>();
        return th;
    }

    void BeginRhythm()
    {
        if (TargetItem == null) return;
        rhythmTree = GetTreeHits(TargetItem);
        chainLock = LumberSkills.ChainNow;
        IsChopping = true;                 // камера, движение и подсказки видят, что игрок рубит
        rhythmActive = true;
        rhythmClock = 0f;                  // маркер стартует с левого края: первый центр придёт через полпрохода
        combo = 0;
        cleanCut = false;
        pendingHit = false;
        nextSwingAt = Time.time + 0.2f;
        Progress = rhythmTree.rhythmProgress;
        ResolveChopParam();
        PushHud();
    }

    void TickRhythm()
    {
        // вышел из стойки: пошёл (клавиши или стик). Без этого игрок оказался бы заперт у дерева
        if (Mathf.Abs(MobileInput.Horizontal) + Mathf.Abs(MobileInput.Vertical) > 0.3f)
        {
            StopChop();
            return;
        }

        rhythmClock += Time.deltaTime;

        if (pendingHit && Time.time >= pendingDue)
        {
            ResolveHit();
            if (!rhythmActive) return;     // дерево упало, стойка закончилась
        }

        if (PressDown && Time.time >= nextSwingAt) Swing();

        if (animBoolOn && Time.time >= animUntil)
        {
            animBoolOn = false;
            if (animator != null && !string.IsNullOrEmpty(chopBool)) animator.SetBool(chopBool, false);
        }

        PushHud();
    }

    // нажатие E: качество удара определяется ЗДЕСЬ (по положению маркера), а эффект придёт через impactDelay
    void Swing()
    {
        float d = Mathf.Abs(Marker01() - 0.5f);
        int q = d <= GameBalance.RhythmPerfectHalf ? 2 : d <= GameBalance.RhythmGoodHalf ? 1 : 0;

        nextSwingAt = Time.time + GameBalance.RhythmCooldown;
        pendingQuality = q;
        pendingDue = Time.time + Mathf.Clamp(impactDelay, 0f, GameBalance.RhythmCooldown * 0.8f);
        pendingHit = true;

        if (animator != null)
        {
            ResolveChopParam();
            if (!string.IsNullOrEmpty(chopBool)) animator.SetBool(chopBool, true);
            if (!string.IsNullOrEmpty(chopTrigger)) animator.SetTrigger(chopTrigger);
            animBoolOn = true;
            animUntil = Time.time + 0.75f;
        }

        Hud.RhythmFlash(q);
    }

    // момент удара: прогресс, тряска, щепки, звук
    void ResolveHit()
    {
        pendingHit = false;
        if (TargetItem == null || rhythmTree == null) return;

        int q = pendingQuality;
        combo = q == 0 ? 0 : combo + 1;

        float amount = q == 2 ? GameBalance.RhythmPerfect : q == 1 ? GameBalance.RhythmGood : GameBalance.RhythmWeak;
        if (combo >= GameBalance.RhythmComboNeed) amount *= GameBalance.RhythmComboMul;
        amount *= PowerMul();

        float before = rhythmTree.rhythmProgress;
        rhythmTree.rhythmProgress = Mathf.Clamp01(before + amount);
        Progress = rhythmTree.rhythmProgress;
        bool fells = before + amount >= 0.999f;

        rhythmTree.Shake(q == 2 ? 1.5f : q == 1 ? 1f : 0.5f);
        GameEvents.RaiseChopHit();
        GameEvents.RaiseChopQuality(q);

        if (fells)
        {
            cleanCut = q == 2 && combo >= GameBalance.RhythmComboNeed;
            CompleteChop();
        }
    }

    void PushHud()
    {
        var h = Hud;
        if (h == null) return;

        if (comboLabelFor != combo)
        {
            comboLabelFor = combo;
            comboLabel = combo >= 2 ? "Серия ×" + combo : "В ритм!";
        }

        float cd = GameBalance.RhythmCooldown > 0f ? Mathf.Clamp01((nextSwingAt - Time.time) / GameBalance.RhythmCooldown) : 0f;
        h.ShowChopRhythm(Progress, ExpectedWood, comboLabel, WoodFull);
        h.SetRhythm(Marker01(), GameBalance.RhythmGoodHalf, GameBalance.RhythmPerfectHalf, combo, cd);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.05f);
        Gizmos.DrawSphere(transform.position, radius);
        if (CurrentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, CurrentTarget.position + Vector3.up * 0.5f);
        }
    }
}
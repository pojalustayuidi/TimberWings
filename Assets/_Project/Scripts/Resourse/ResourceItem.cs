// Assets/_Project/Scripts/Resource/ResourceItem.cs (НЕ в Editor)
// Дерево-ресурс: дрожит при рубке, падает, оставляет пень, на пне растёт росток, через время дерево возвращается
// (время зависит от размера и навыков «Лесоруба», см. TreeRespawn).
// Update больше не пишет position/scale каждый кадр у каждого дерева на карте: только пока рубят и один раз после.
//
// Задача 3 (ветка «Лесоруб»), что добавлено:
//   Active      список спелых (включённых) деревьев: по нему ищут соседа для «Эха удара», золотое дерево и подсветку «Быстрого роста».
//               Заполняется само в OnEnable/OnDisable, FindObjectsByType для этого больше не нужен.
//   IsGolden    золотое дерево (lumber_golden). Включает и выключает LumberjackEffects через SetGolden.
//   IsChopping  дерево рубят прямо сейчас.
//
// Задача 2a (размеры деревьев), что добавлено:
//   Size        молодое / зрелое / старое. Выбирается по seed позиции (TreeSizing.Roll), между запусками не меняется.
//   Logs        бревна с дерева (GameBalance.TreeLogs): их читает LumberSkills.BaseWood.
//   ChopMul     множитель времени рубки (GameBalance.TreeChopMul): его читает PlayerInteraction.GatherTime.
//   Масштаб корня и тон листвы подгоняются в Awake. Модель можно заменить на сгенерированную (useGeneratedModel).
//   Growth      двойник для падения и отроста (TreeGrowthVisual), создаётся при первой рубке.
using System.Collections.Generic;
using UnityEngine;

public class ResourceItem : MonoBehaviour
{
    [Header("Настройки ресурса")]
    public float gatherTime = GameBalance.ChopTime;   // секунд удержания E на дерево (GDD v0.2: 3). Реальное время считает PlayerInteraction.GatherTime × ChopMul

    [Header("Размер дерева (TreeSizing)")]
    [Tooltip("Масштаб корня и тон листвы по размеру. Выключи, чтобы оставить внешний вид как есть (бревна, время рубки и респаун по размеру останутся)")]
    public bool autoSize = true;
    [Tooltip("Заменить модель на сгенерированную (3 вида + пень, TreeMeshFactory). Выключи, чтобы остался твой префаб")]
    public bool useGeneratedModel = GameBalance.GeneratedTreeModels;
    [Tooltip("Задать размер вручную (иначе по seed позиции)")]
    public bool overrideSize = false;
    public TreeSize forcedSize = TreeSize.Mature;

    [Header("Респаун (GDD: 30 / 45 / 75 с по размеру)")]
    public bool respawn = true;
    [Tooltip("true = брать GameBalance.TreeRespawnBySize (по размеру дерева). false = случайное из диапазона ниже")]
    public bool useBalanceRespawn = true;
    public Vector2 respawnSeconds = new Vector2(20f, 30f);
    [Tooltip("Падение и отрост ростка на пне. Выключи, чтобы дерево просто пропадало и возвращалось (как раньше)")]
    public bool growVisual = true;

    [Header("Визуал при разрушении (Конец)")]
    public GameObject stumpPrefab;
    public GameObject particlePrefab;

    [Header("Визуал ВО ВРЕМЯ рубки (Импакт)")]
    public ParticleSystem woodChipsParticles;

    // все спелые деревья (включённые объекты). Не менять снаружи
    public static readonly List<ResourceItem> Active = new List<ResourceItem>(64);

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private bool isBeingChopped = false;
    private bool dirty = false;   // трансформ сдвинут тряской и ещё не возвращён
    private bool started = false;
    private GoldenTreeFx goldenFx;

    // появление: волна при старте и загрузке, пружинка в конце респауна
    private int introMode;                        // 0 нет, 1 волна (0 → перелёт 1.06 → 1), 2 пружинка
    private float introT, introDelay, introDur;
    static Transform waveOrigin;                  // игрок: волна бежит от него (нет игрока: от центра острова)
    static int lastWaveSoundFrame = -1;

    // «стоячая» локальная поза (для двойника): позиция и масштаб после Awake, ориентация без ветра
    private Vector3 restLocalPos, restLocalScale;
    private Quaternion restLocalRot;

    public TreeSize Size { get; private set; }
    public int Logs { get { return GameBalance.TreeLogs[(int)Size]; } }
    public float ChopMul { get { return GameBalance.TreeChopMul[(int)Size]; } }
    public float RespawnBase { get { return GameBalance.TreeRespawnBySize[(int)Size]; } }
    public float ScaleMul { get { return autoSize ? GameBalance.TreeScaleMul[(int)Size] : 1f; } }

    [System.NonSerialized] public TreeGrowthVisual Growth;
    GameObject dirt;   // пятно земли (TreeGround): живёт и пока дерево срублено, пень стоит на земле

    void OnDestroy() { TreeGround.Destroy(dirt); dirt = null; }

    // радиус сплошного ствола в метрах: PlayerInteraction по нему решает, где встать у дерева
    public float TrunkRadius { get; private set; }

    public bool IsChopping { get { return isBeingChopped; } }
    public bool IsGolden { get { return goldenFx != null; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Active.Clear(); waveOrigin = null; lastWaveSoundFrame = -1; }

    void Awake()
    {
        Size = overrideSize ? forcedSize : TreeSizing.Roll(transform.position);
        if (autoSize) transform.localScale *= GameBalance.TreeScaleMul[(int)Size];
        bool jittered = false;
        if (autoSize && GameBalance.TreeJitter) { ApplyJitter(); jittered = true; }   // размер уже брошен по исходной позиции, сдвиг его не меняет

        Renderer gen = null;
        if (useGeneratedModel) gen = TreeMeshFactory.Apply(this);
        if (autoSize) TreeSizing.ApplyTone(gameObject, Size);

        TrunkRadius = TreeCollision.Setup(this);   // сплошной ствол + зона выбора по кроне (игрок не проходит сквозь дерево)

        if (jittered) { var th = GetComponent<TreeHits>(); if (th != null) th.Rebase(); }

        var sway = GetComponent<ToonSway>();
        if (sway != null)
        {
            if (jittered) sway.Rebase();
            sway.SetAmplitude(GameBalance.TreeSwayMul[(int)Size]);   // большие деревья качаются меньше
            if (gen != null) sway.SetRenderer(gen);
        }
    }

    void Start()
    {
        originalPosition = transform.position;
        originalScale = transform.localScale;

        var sway = GetComponent<ToonSway>();
        restLocalPos = transform.localPosition;
        restLocalScale = transform.localScale;
        restLocalRot = (sway != null && sway.Agent != null) ? sway.Agent.r0 : transform.localRotation;
        started = true;
        if (dirt == null) dirt = TreeGround.Create(originalPosition, (int)Size, gameObject.layer, SpeciesKey());   // земля под деревом (своя у каждого вида): видно, что его можно рубить
        BeginWave(false);   // старт сцены: деревья вырастают волной от игрока
    }

    // ------------------------------------------------------------ лёгкий наклон и сдвиг (только рантайм, позиции в сцене не трогаем)
    // всё считается от хэша позиции: лес выглядит одинаково при каждом запуске. Старые деревья наклонены меньше
    void ApplyJitter()
    {
        uint h = TreeSizing.Hash(transform.position);
        float mul = GameBalance.TreeSwayMul[(int)Size];
        float tx = (((h >> 8) & 0xFFFF) / 65535f * 2f - 1f) * GameBalance.TreeTiltDeg * mul;
        float tz = (((h >> 16) & 0xFF) / 255f * 2f - 1f) * GameBalance.TreeTiltDeg * mul;
        float ang = (h & 0xFF) / 255f * Mathf.PI * 2f;
        float rad = ((h >> 24) & 0xFF) / 255f * GameBalance.TreeShiftMax;

        transform.localRotation = Quaternion.Euler(tx, 0f, tz) * transform.localRotation;
        transform.position += new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
    }

    // ------------------------------------------------------------ появление: волна и пружинка
    static Vector3 WaveCenter()
    {
        if (waveOrigin == null)
        {
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) waveOrigin = pi.transform;
        }
        return waveOrigin != null ? waveOrigin.position : TreeSizing.IslandCenter;
    }

    static float WaveK(float u)
    {
        float o = GameBalance.TreeWaveOvershoot;
        if (u < 0.7f)
        {
            float s = u / 0.7f;
            s = 1f - (1f - s) * (1f - s) * (1f - s);       // easeOutCubic до перелёта
            return Mathf.Max(0.001f, o * s);
        }
        float t = (u - 0.7f) / 0.3f;
        t = t * t * (3f - 2f * t);                          // возврат 1.06 → 1, в конце скорость ноль
        return Mathf.Lerp(o, 1f, t);
    }

    // дерево выросло волной: задержка = расстояние / скорость. playSound: один тихий звук на кадр (при загрузке), а не на каждое дерево
    public void BeginWave(bool playSound)
    {
        if (!GameBalance.TreeWaveOnStart || !started || isBeingChopped) return;
        Vector3 d = originalPosition - WaveCenter();
        d.y = 0f;
        introDelay = Mathf.Min(d.magnitude / Mathf.Max(0.1f, GameBalance.TreeWaveSpeed), GameBalance.TreeWaveMaxDelay)
                   + TreeSizing.Value01(originalPosition) * 0.2f;     // чуть неровный фронт волны
        introDur = Mathf.Max(0.1f, GameBalance.TreeWaveGrowSeconds);
        introT = 0f;
        introMode = 1;
        transform.position = originalPosition;
        transform.localScale = originalScale * 0.001f;
        if (playSound && lastWaveSoundFrame != Time.frameCount)
        {
            lastWaveSoundFrame = Time.frameCount;
            SfxManager.Play("grow", 0.5f, 0.9f);
        }
    }

    // ключ вида для земли под деревом (Cedar, Birch, Autumn, Willow_Dead, Random, Oak). null = дерево без вида (сгенерированное)
    string SpeciesKey()
    {
        var info = GetComponent<TreeSpeciesInfo>();
        return info != null ? info.species : null;
    }

    // пень ушёл в землю: облачко пыли у корней (дальше TreeFxMaxDist от игрока не рисуем)
    public void PlayDust()
    {
        if (!started) return;
        Vector3 d = originalPosition - WaveCenter();
        d.y = 0f;
        if (d.sqrMagnitude <= GameBalance.TreeFxMaxDist * GameBalance.TreeFxMaxDist) TreeFx.Puff(originalPosition, 0);
    }

    // финал респауна: пружинка (сжатие и растяжение), облачко листьев и тихий звук. Дальше FxMaxDist от игрока только пружинка (её не видно)
    public void PlaySpring()
    {
        if (!started) return;
        Vector3 d = originalPosition - WaveCenter();
        d.y = 0f;
        bool near = d.sqrMagnitude <= GameBalance.TreeFxMaxDist * GameBalance.TreeFxMaxDist;
        introMode = 2;
        introT = 0f;
        introDelay = 0f;
        introDur = Mathf.Max(0.1f, GameBalance.TreeSpringSeconds);
        if (near)
        {
            TreeFx.Puff(originalPosition, (int)Size);
            SfxManager.Play("grow", 0.35f, Random.Range(0.92f, 1.08f));
        }
    }

    // «Тык»: дерево присело, подпрыгнуло и вытянулось, с кроны посыпались листья. Зовёт PlayerInteraction, когда игрок кликнул
    // по дереву или нажал E и пошёл к нему, и ещё раз, когда дошёл. Во время рубки и пока дерево растёт волной не срабатывает.
    public void Poke(bool leaves = true)
    {
        if (!started || isBeingChopped || introMode == 1) return;
        introMode = 3;
        introT = 0f;
        introDelay = 0f;
        introDur = 0.55f;
        if (leaves)
        {
            Vector3 d = originalPosition - WaveCenter();
            d.y = 0f;
            if (d.sqrMagnitude <= GameBalance.TreeFxMaxDist * GameBalance.TreeFxMaxDist)
                TreeFx.Puff(originalPosition + Vector3.up * (1.9f * Mathf.Max(0.5f, ScaleMul)), (int)Size);
        }
    }

    void TickIntro()
    {
        introT += Time.deltaTime;
        float u = (introT - introDelay) / introDur;
        if (u < 0f) { transform.localScale = originalScale * 0.001f; return; }   // волна ещё не дошла
        if (u >= 1f)
        {
            introMode = 0;
            transform.position = originalPosition;   // «тык» поднимал дерево над землёй
            transform.localScale = originalScale;
            dirty = false;
            return;
        }

        if (introMode == 1) transform.localScale = originalScale * WaveK(u);
        else if (introMode == 3)
        {
            // 0..0.2: присел (шире и ниже). 0.2..1: прыжок вверх с вытяжкой, приземление и затухающее колебание
            float k, hop = 0f;
            if (u < 0.2f)
            {
                float s = u / 0.2f;
                k = -0.10f * Mathf.Sin(s * Mathf.PI * 0.5f);
            }
            else
            {
                float s = (u - 0.2f) / 0.8f;
                hop = Mathf.Sin(s * Mathf.PI) * 0.30f * (0.7f + 0.3f * Mathf.Clamp(ScaleMul, 0.7f, 1.3f));
                k = 0.14f * Mathf.Sin(s * Mathf.PI * 2f) * (1f - s) - 0.10f * (1f - s) * (1f - s) * (1f - s);
            }
            transform.position = originalPosition + new Vector3(0f, hop, 0f);
            transform.localScale = new Vector3(originalScale.x * (1f - 0.5f * k), originalScale.y * (1f + k), originalScale.z * (1f - 0.5f * k));
        }
        else
        {
            float d = GameBalance.TreeSpringAmount * Mathf.Sin(u * Mathf.PI * 2f) * (1f - u);   // затухающее колебание
            transform.localScale = new Vector3(originalScale.x * (1f - 0.5f * d), originalScale.y * (1f + d), originalScale.z * (1f - 0.5f * d));
        }
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        SaveSystem.Loaded += OnLoaded;
        introMode = 0;   // после респауна PlaySpring включит пружинку заново

        // после респауна дерево стоит ровно и не «трясётся»
        isBeingChopped = false;
        dirty = true;   // вернём исходный трансформ в первом Update (после Start)
        if (started) RestoreTransform();   // и сразу, чтобы в кадре возвращения не мелькнула поза с тряской
    }

    // загрузка сохранения: все деревья снова вырастают волной
    void OnLoaded() { BeginWave(true); }

    void OnDisable()
    {
        SaveSystem.Loaded -= OnLoaded;
        Active.Remove(this);
        // дерево спрятано (срублено) или объект выключен: золотой статус не переживает это, награду уже выдали
        if (goldenFx != null) { Destroy(goldenFx); goldenFx = null; }
    }

    void Update()
    {
        if (isBeingChopped)
        {
            // 1. Мягкая микро-вибрация
            transform.position = originalPosition + new Vector3(Random.Range(-0.01f, 0.01f), 0, Random.Range(-0.01f, 0.01f));

            // 2. Мягкая пружинистость
            float bounce = Mathf.PingPong(Time.time * 4f, 0.04f);
            transform.localScale = new Vector3(originalScale.x - bounce, originalScale.y + bounce, originalScale.z - bounce);
            dirty = true;
        }
        else if (introMode != 0)
        {
            TickIntro();
        }
        else if (dirty)
        {
            RestoreTransform();
        }
    }

    // вернуть позицию и масштаб после тряски (зовёт и TreeRespawn перед SetActive(true))
    public void RestoreTransform()
    {
        transform.position = originalPosition;
        transform.localScale = originalScale;
        dirty = false;
    }

    // локальная поза зрелого дерева в родителе (для двойника, см. TreeGrowthVisual)
    public void GetRestPose(out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        if (started) { pos = restLocalPos; rot = restLocalRot; scale = restLocalScale; }
        else { pos = transform.localPosition; rot = transform.localRotation; scale = transform.localScale; }
    }

    public void SetChopping(bool state)
    {
        isBeingChopped = state;
        if (state) { dirty = true; if (introMode != 0) { introMode = 0; transform.localScale = originalScale; } }   // рубят: появление обрывается

        if (woodChipsParticles != null)
        {
            if (state && !woodChipsParticles.isPlaying)
                woodChipsParticles.Play();
            else if (!state && woodChipsParticles.isPlaying)
                woodChipsParticles.Stop();
        }
    }

    // золотое дерево (lumber_golden): вид и статус. Зовёт LumberjackEffects; статус не сохраняется
    public void SetGolden(bool on)
    {
        if (on)
        {
            if (goldenFx == null && gameObject.activeInHierarchy) goldenFx = GoldenTreeFx.Attach(this);
        }
        else if (goldenFx != null)
        {
            Destroy(goldenFx);
            goldenFx = null;
        }
    }

    public void ChopDown()
    {
        GameObject stump = null;

        if (stumpPrefab != null)
        {
            Vector3 stumpPos = transform.position + new Vector3(0, 0.2f, 0);
            stump = Instantiate(stumpPrefab, stumpPos, stumpPrefab.transform.rotation);
            if (autoSize) stump.transform.localScale *= ScaleMul;   // пень старого дерева толще
        }
        else if (useGeneratedModel)
        {
            // своего пня нет: рисуем сгенерированный (масштаб берём с реального дерева, чтобы пень совпал с его толщиной)
            Quaternion rot = Quaternion.Euler(0f, TreeSizing.Value01(transform.position) * 360f, 0f);
            stump = TreeMeshFactory.CreateStump(transform.position, rot, transform.lossyScale.x, gameObject.layer);
        }

        if (particlePrefab != null)
        {
            Vector3 particlePos = transform.position + new Vector3(0, 1.5f, 0);
            GameObject fx = Instantiate(particlePrefab, particlePos, Quaternion.identity);
            Destroy(fx, 4f); // чтобы (Clone) не копились в иерархии
        }

        if (respawn)
        {
            // дерево не удаляем, а прячем: над пнём идёт таймер, потом оно возвращается.
            // Передаём время роста БЕЗ навыков: множитель «Лесоруба» TreeRespawn применяет сам и каждый кадр (сброс навыка действует сразу)
            float seconds = useBalanceRespawn ? RespawnBase : Random.Range(respawnSeconds.x, respawnSeconds.y);

            // двойник должен стартовать, пока настоящее дерево ещё включено (копия поз и мешей), и только потом дерево прячется
            TreeGrowthVisual growth = null;
            if (growVisual)
            {
                growth = TreeGrowthVisual.GetOrCreate(this);
                growth.BeginFall();
            }

            TreeRespawn.Begin(gameObject, stump, transform.position, seconds, growth);
            gameObject.SetActive(false);   // OnDisable уберёт золотой статус
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

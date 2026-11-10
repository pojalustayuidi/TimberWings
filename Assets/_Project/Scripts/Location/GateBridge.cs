using System.Collections.Generic;
using UnityEngine;

// Assets/_Project/Scripts/Location/GateBridge.cs (НЕ в Editor)
// Подвесной мост от ворот главного острова к финальной площадке на дальнем острове.
//
// Что это за компонент: только показ и проход. Геометрию собирает Editor-команда (Editor/GateBridge/GateBridgeBuilder*.cs,
// меню Tools > BuilderSim > Локация > Собрать подвесной мост и финальный остров). Здесь рантайм:
//   - три вида из RiverBridge (1 сломан, 2 строится, 3 готов): Stage1, Stage2, Stage3 включаются по одному;
//   - оплачиваемых этапов три (GameBalance.Bridge*: Опоры, Настил, Тросы и перила), видов три. Чтобы КАЖДЫЙ вклад был виден,
//     внутри вида 2 есть объект deckStep2 (доски и подвесы): он появляется после второго этапа. После первого видны новые опоры и тросы;
//   - ходить по мосту можно только когда стройка закончена (RiverBridge.IsOpen), а не когда включился вид 3: коллайдеры
//     настила и бортиков лежат в walkway, а не в Stage3. SetStage(3) из меню или кода проход не открывает;
//   - на виде 1 с обрубков падают доски и исчезают в облаках (как раньше).
// Загрузка сохранения: BuildStageLink (на зоне стройки) ставит RiverBridge в сохранённый этап без наград и без показа стройки,
// RiverBridge шлёт ProgressChanged и этот компонент показывает нужный вид и проход. Своего сохранения у моста нет.
// Записка Гуннара: noteSpot (точка на площадке дальнего острова). StoryFlow берёт её отсюда, если своя точка не задана.
public class GateBridge : MonoBehaviour
{
    [Header("Размеры (меняй и жми «Собрать мост и остров»)")]
    [Tooltip("Расстояние по воздуху от края главного острова до края дальнего острова, метры")]
    public float bridgeLength = 25f;
    public float deckWidth = 2.6f;
    [Tooltip("Насколько настил провисает посередине, метры")]
    public float sag = 0.7f;
    [Tooltip("Радиус дальнего острова, метры. Не меньше 10: у входа и на площадке нужно место для камеры и разворота Лео")]
    public float islandRadius = 11f;
    [Tooltip("УСТАРЕЛО: радиус острова-заглушки. Не используется, поле оставлено, чтобы не слетели значения в сцене")]
    public float stubRadius = 9f;
    [Tooltip("На сколько метров верх дальнего острова ниже края главного острова")]
    public float stubDrop = 0.6f;
    public int seed = 5;

    [Header("Ссылки (проставляются при сборке)")]
    public RiverBridge source;
    [Tooltip("Три вида: 1 сломан, 2 строится, 3 готов. Коллайдеров ходьбы у них нет")]
    public GameObject[] stages = new GameObject[3];
    [Tooltip("Доски и подвесы, которые добавляются к виду 2 после второго оплаченного этапа (ребёнок Stage2)")]
    public GameObject deckStep2;
    [Tooltip("Простые непрерывные коллайдеры настила и бортиков. Включаются, только когда стройка закончена")]
    public GameObject walkway;
    [Tooltip("Точка записки Гуннара на площадке дальнего острова. StoryFlow ведёт сюда стрелку и ждёт здесь Лео")]
    public Transform noteSpot;
    [Tooltip("Где Лео встаёт, сойдя с моста на дальний остров (для проверок и снимков)")]
    public Transform arrivalSpot;
    public Mesh plankMesh;
    public Material plankMaterial;
    public Transform[] fallPoints = new Transform[0];

    [Header("Падающие доски (только вид 1)")]
    public int fallingPlanks = 3;
    public Vector2 fallInterval = new Vector2(3f, 8f);
    public float fallDepth = 45f;

    class Faller
    {
        public Transform t;
        public float wait, vy, baseY;
        public Vector3 spin;
        public bool falling;
    }

    readonly List<Faller> fallers = new List<Faller>();
    int stage = 1, stepsDone, stepsTotal = 3;
    bool subscribed, previewWalk;

    public int Stage { get { return stage; } }
    public bool WalkwayActive { get { return walkway != null && walkway.activeSelf; } }

    void Awake()
    {
        if (plankMesh == null || plankMaterial == null) return;
        for (int i = 0; i < fallingPlanks; i++)
        {
            var go = new GameObject("FallingPlank");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = plankMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = plankMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            fallers.Add(new Faller { t = go.transform, wait = Random.Range(fallInterval.x, fallInterval.y) * (0.3f + i * 0.5f) });
        }
    }

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (source != null && subscribed)
        {
            source.OnStageChanged -= Apply;
            source.ProgressChanged -= ApplyProgress;
        }
        subscribed = false;
    }

    void Start()
    {
        TrySubscribe();
        Refresh();
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (source == null) source = FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);
        if (source == null) return;
        source.OnStageChanged += Apply;
        source.ProgressChanged += ApplyProgress;
        subscribed = true;
    }

    // Взять состояние у RiverBridge и показать (старт сцены, загрузка сохранения)
    public void Refresh()
    {
        if (source != null)
        {
            stage = Mathf.Clamp(source.CurrentStage, 1, 3);
            stepsDone = source.StepsDone;
            stepsTotal = source.StepsTotal;
        }
        Show();
    }

    // Показать нужный вид (1..3). Зовёт RiverBridge.OnStageChanged
    public void Apply(int s)
    {
        stage = Mathf.Clamp(s, 1, 3);
        Show();
    }

    // Сдано этапов стройки. Зовёт RiverBridge.ProgressChanged: меняет вид внутри стадии 2 и открывает проход
    public void ApplyProgress(int done, int total)
    {
        stepsDone = done;
        stepsTotal = total;
        Show();
    }

    // Просмотр в редакторе (кнопки инспектора и снимки): вид, число сданных этапов и можно ли ходить
    public void Preview(int viewStage, int done, bool walkable)
    {
        stage = Mathf.Clamp(viewStage, 1, 3);
        stepsDone = done;
        previewWalk = walkable;
        Show();
    }

    void Show()
    {
        for (int i = 0; i < stages.Length; i++)
            if (stages[i] != null) stages[i].SetActive(i == stage - 1);
        if (deckStep2 != null) deckStep2.SetActive(stage == 2 && stepsDone >= 2);

        // проход открывает завершённая стройка, а не номер вида
        bool open = previewWalk || (source != null && source.IsOpen);
        if (walkway != null && walkway.activeSelf != open) walkway.SetActive(open);

        if (stage != 1)
            foreach (var f in fallers) { f.t.gameObject.SetActive(false); f.falling = false; }
    }

    void Update()
    {
        if (stage != 1 || fallPoints == null || fallPoints.Length == 0) return;
        float dt = Time.deltaTime;
        foreach (var f in fallers)
        {
            if (!f.falling)
            {
                f.wait -= dt;
                if (f.wait > 0f) continue;
                var fp = fallPoints[Random.Range(0, fallPoints.Length)];
                if (fp == null) continue;
                f.t.position = fp.position + fp.right * Random.Range(-0.6f, 0.6f);
                f.t.rotation = fp.rotation;
                f.baseY = fp.position.y;
                f.vy = 0f;
                f.spin = new Vector3(Random.Range(-60f, 60f), Random.Range(-40f, 40f), Random.Range(-90f, 90f));
                f.falling = true;
                f.t.gameObject.SetActive(true);
                continue;
            }
            f.vy += 7f * dt;
            f.t.position += Vector3.down * (f.vy * dt);
            f.t.Rotate(f.spin * dt, Space.Self);
            if (f.t.position.y < f.baseY - fallDepth)
            {
                f.t.gameObject.SetActive(false);
                f.falling = false;
                f.wait = Random.Range(fallInterval.x, fallInterval.y);
            }
        }
    }
}

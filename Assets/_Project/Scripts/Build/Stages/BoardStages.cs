using System;
using UnityEngine;

/// <summary>
/// Стадии доски заказов. Этапы накапливаются (как у HutStages):
/// SetStage(2) -> Stage1 + Stage2, SetStage(3) -> все три.
/// Заказы на доске = листки бумаги (Stage3/Papers). Взял заказ -> TakeOrder() прячет один листок.
/// Работать с заказами можно только на этапе 3 (IsOpen).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Board Stages")]
public class BoardStages : MonoBehaviour
{
    [Header("Объекты этапов")]
    [SerializeField] private GameObject stage1;
    [SerializeField] private GameObject stage2;
    [SerializeField] private GameObject stage3;

    [Header("Начальный этап (1-3)")]
    [SerializeField, Range(1, 3)] private int initialStage = 1;

    [Header("Точки для игры")]
    [SerializeField] private Transform buildPoint;      // куда подводить игрока для стройки
    [SerializeField] private Transform interactPoint;   // где стоит игрок, когда берёт заказы
    [SerializeField] private Transform boardCenter;     // центр доски (для камеры / UI)
    [SerializeField] private GameObject interactZone;   // триггер-зона перед доской, активна только на этапе 3

    [Header("Заказы на доске")]
    [SerializeField] private Transform[] papers;        // листки (заполняет билдер)
    [Tooltip("-1 = показывать все листки")]
    [SerializeField] private int initialOrders = -1;

    public int CurrentStage { get; private set; }
    public int StageCount => 3;
    public bool IsOpen => CurrentStage >= 3;
    public Transform BuildPoint => buildPoint;
    public Transform InteractPoint => interactPoint;
    public Transform BoardCenter => boardCenter;
    public int PapersTotal => papers != null ? papers.Length : 0;
    public int OrdersVisible { get; private set; }

    /// <summary>Новый этап.</summary>
    public event Action<int> OnStageChanged;
    /// <summary>Сколько листков (доступных заказов) сейчас на доске.</summary>
    public event Action<int> OnOrdersChanged;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        ResolveRefs();
        ApplyStage(initialStage);
        SetOrdersInternal(initialOrders < 0 ? PapersTotal : initialOrders);
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        OnStageChanged?.Invoke(CurrentStage);
        OnOrdersChanged?.Invoke(OrdersVisible);
    }

    public void SetStage(int stage)
    {
        ResolveRefs();
        stage = Mathf.Clamp(stage, 1, 3);
        bool changed = stage != CurrentStage;
        ApplyStage(stage);
        if (changed) OnStageChanged?.Invoke(CurrentStage);
    }

    public bool Upgrade()
    {
        if (CurrentStage >= 3) return false;
        SetStage(CurrentStage + 1);
        return true;
    }

    /// <summary>Выставить, сколько заказов висит на доске (0..PapersTotal).</summary>
    public void SetOrdersVisible(int count)
    {
        ResolveRefs();
        SetOrdersInternal(count);
        OnOrdersChanged?.Invoke(OrdersVisible);
    }

    /// <summary>Игрок взял заказ: один листок исчезает. false, если доска пуста или ещё не достроена.</summary>
    public bool TakeOrder()
    {
        if (!IsOpen || OrdersVisible <= 0) return false;
        SetOrdersVisible(OrdersVisible - 1);
        return true;
    }

    /// <summary>Появился новый заказ.</summary>
    public bool AddOrder()
    {
        if (OrdersVisible >= PapersTotal) return false;
        SetOrdersVisible(OrdersVisible + 1);
        return true;
    }

    /// <summary>Вызывается билдером.</summary>
    public void Configure(GameObject s1, GameObject s2, GameObject s3, Transform build, Transform interact,
                          Transform board, GameObject zone, Transform[] sheets, int initial)
    {
        stage1 = s1; stage2 = s2; stage3 = s3;
        buildPoint = build; interactPoint = interact; boardCenter = board; interactZone = zone;
        papers = sheets;
        initialStage = Mathf.Clamp(initial, 1, 3);
        initialOrders = -1;
        OrdersVisible = PapersTotal;
    }

    private void SetOrdersInternal(int count)
    {
        OrdersVisible = Mathf.Clamp(count, 0, PapersTotal);
        if (papers == null) return;
        for (int i = 0; i < papers.Length; i++)
            if (papers[i] != null) papers[i].gameObject.SetActive(i < OrdersVisible);
    }

    private void ApplyStage(int stage)
    {
        CurrentStage = Mathf.Clamp(stage, 1, 3);
        if (stage1 != null) stage1.SetActive(CurrentStage >= 1);
        if (stage2 != null) stage2.SetActive(CurrentStage >= 2);
        if (stage3 != null) stage3.SetActive(CurrentStage >= 3);
        if (interactZone != null) interactZone.SetActive(CurrentStage >= 3);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (stage1 != null) UnityEditor.EditorUtility.SetDirty(stage1);
            if (stage2 != null) UnityEditor.EditorUtility.SetDirty(stage2);
            if (stage3 != null) UnityEditor.EditorUtility.SetDirty(stage3);
            if (interactZone != null) UnityEditor.EditorUtility.SetDirty(interactZone);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void ResolveRefs()
    {
        if (stage1 == null) { var t = transform.Find("Stage1"); if (t != null) stage1 = t.gameObject; }
        if (stage2 == null) { var t = transform.Find("Stage2"); if (t != null) stage2 = t.gameObject; }
        if (stage3 == null) { var t = transform.Find("Stage3"); if (t != null) stage3 = t.gameObject; }
        if (buildPoint == null) buildPoint = transform.Find("BuildPoint");
        if (interactPoint == null) interactPoint = transform.Find("InteractPoint");
        if (boardCenter == null) boardCenter = transform.Find("BoardCenter");
        if (interactZone == null) { var t = transform.Find("InteractZone"); if (t != null) interactZone = t.gameObject; }
        if ((papers == null || papers.Length == 0) && stage3 != null)
        {
            var h = stage3.transform.Find("Papers");
            if (h != null)
            {
                papers = new Transform[h.childCount];
                for (int i = 0; i < papers.Length; i++) papers[i] = h.GetChild(i);
            }
        }
    }

    private void OnValidate() { initialStage = Mathf.Clamp(initialStage, 1, 3); }

    [ContextMenu("Тест: показать этап 1")] private void TestStage1() => SetStage(1);
    [ContextMenu("Тест: показать этап 2")] private void TestStage2() => SetStage(2);
    [ContextMenu("Тест: показать этап 3")] private void TestStage3() => SetStage(3);
    [ContextMenu("Тест: взять заказ")] private void TestTake() => TakeOrder();
    [ContextMenu("Тест: добавить заказ")] private void TestAdd() => AddOrder();
}

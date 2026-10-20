using System;
using UnityEngine;

/// <summary>
/// Управляет этапами стройки хижины/костра. Этапы накапливаются:
/// SetStage(2) → видны Stage1 + Stage2, SetStage(3) → видны все три.
/// Коллайдеры лежат на объектах этапов, поэтому включаются вместе с ними.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("BuilderSim/Hut Stages")]
public class HutStages : MonoBehaviour
{
    [Header("Объекты этапов (если пусто, ищутся по именам Stage1, Stage2, Stage3)")]
    [SerializeField] private GameObject stage1;
    [SerializeField] private GameObject stage2;
    [SerializeField] private GameObject stage3;

    [Header("Начальный этап (1–3)")]
    [SerializeField, Range(1, 3)] private int initialStage = 1;

    [Header("Точка, куда игра подводит игрока")]
    [SerializeField] private Transform buildPoint;

    public int CurrentStage { get; private set; }
    public int StageCount => 3;
    public Transform BuildPoint => buildPoint;

    /// <summary>Вызывается при смене этапа (аргумент — новый этап).</summary>
    public event Action<int> OnStageChanged;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        ResolveStages();
        ApplyStage(initialStage);
    }

    private void Start()
    {
        // Подписчики, которые подписались в Awake/OnEnable, получат начальный этап.
        if (Application.isPlaying) OnStageChanged?.Invoke(CurrentStage);
    }

    /// <summary>Включает Stage1..stage и выключает остальные.</summary>
    public void SetStage(int stage)
    {
        ResolveStages();
        stage = Mathf.Clamp(stage, 1, StageCount);
        bool changed = stage != CurrentStage;
        ApplyStage(stage);
        if (changed) OnStageChanged?.Invoke(CurrentStage);
    }

    /// <summary>Загрузка сохранения: ставит этап БЕЗ события OnStageChanged (его слушают BuildFx и показ стадии). Вид у остальных обновит GameEvents.BuildingRestored.</summary>
    public void SetStageSilent(int stage)
    {
        ResolveStages();
        ApplyStage(Mathf.Clamp(stage, 1, StageCount));
    }

    /// <summary>Есть ли объект этапа n (1..3).</summary>
    public bool HasStage(int n)
    {
        ResolveStages();
        switch (n)
        {
            case 1: return stage1 != null;
            case 2: return stage2 != null;
            case 3: return stage3 != null;
            default: return false;
        }
    }

    /// <summary>Объект этапа n (1..3) или null. Нужен BuildFx: по включению объекта он понимает, какая стадия новая, и собирает её по деталям.</summary>
    public GameObject GetStageObject(int n)
    {
        ResolveStages();
        switch (n)
        {
            case 1: return stage1;
            case 2: return stage2;
            case 3: return stage3;
            default: return null;
        }
    }

    /// <summary>Вызывается из Editor-инструмента после сборки.</summary>
    public void Configure(GameObject s1, GameObject s2, GameObject s3, Transform bp, int initial)
    {
        stage1 = s1; stage2 = s2; stage3 = s3; buildPoint = bp;
        initialStage = Mathf.Clamp(initial, 1, 3);
    }

    private void ApplyStage(int stage)
    {
        CurrentStage = Mathf.Clamp(stage, 1, StageCount);
        if (stage1 != null) stage1.SetActive(CurrentStage >= 1);
        if (stage2 != null) stage2.SetActive(CurrentStage >= 2);
        if (stage3 != null) stage3.SetActive(CurrentStage >= 3);

        // нужный этап не найден: раньше это молча выглядело как «этап не включается»
        if (Application.isPlaying)
            for (int i = 1; i <= CurrentStage; i++)
                if (!HasStage(i))
                    Debug.LogError("[HutStages] «" + name + "»: объект этапа Stage" + i + " не найден. " +
                                   "Назначь его в Inspector или назови дочерний объект «Stage" + i + "».", this);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (stage1 != null) UnityEditor.EditorUtility.SetDirty(stage1);
            if (stage2 != null) UnityEditor.EditorUtility.SetDirty(stage2);
            if (stage3 != null) UnityEditor.EditorUtility.SetDirty(stage3);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void ResolveStages()
    {
        if (stage1 == null) stage1 = FindStage("Stage1");
        if (stage2 == null) stage2 = FindStage("Stage2");
        if (stage3 == null) stage3 = FindStage("Stage3");
        if (buildPoint == null) buildPoint = transform.Find("BuildPoint");
    }

    // сначала прямой потомок, потом поиск глубже по имени (в том числе среди выключенных)
    private GameObject FindStage(string stageName)
    {
        var t = transform.Find(stageName);
        if (t != null) return t.gameObject;
        foreach (var c in GetComponentsInChildren<Transform>(true))
            if (c != transform && string.Equals(c.name, stageName, StringComparison.OrdinalIgnoreCase))
                return c.gameObject;
        return null;
    }

    private void OnValidate()
    {
        initialStage = Mathf.Clamp(initialStage, 1, 3);
    }

    // ---- Тест в редакторе: правый клик по заголовку компонента ----
    [ContextMenu("Тест: показать этап 1")] private void TestStage1() => SetStage(1);
    [ContextMenu("Тест: показать этап 2")] private void TestStage2() => SetStage(2);
    [ContextMenu("Тест: показать этап 3")] private void TestStage3() => SetStage(3);
    [ContextMenu("Тест: следующий этап (по кругу)")]
    private void TestNext() => SetStage(CurrentStage >= 3 ? 1 : CurrentStage + 1);
}
using System;
using UnityEngine;

/// <summary>
/// Стадии моста: 1 сломан, 2 починен, 3 красивый.
/// Поля stages, blockers, stage. Генератор реки удалён, мост на следующую локацию (GateBridge) всё ещё слушает этот компонент.
/// Из игры: GetComponent<RiverBridge>().SetStage(2);
///
/// Стройка (BuildStageLink) ведёт мост через SetBuildProgress(этапов сдано, этапов всего):
///   вид: 0 этапов = стадия 1 (сломан), промежуточные = стадия 2 (чинится), последний сдан = стадия 3;
///   невидимые стенки на провале (blockers) держатся, пока НЕ сдан последний этап, иначе проход открылся бы на полпути.
/// Видов у моста три, а этапов стройки три (четыре состояния), поэтому после этапов 1 и 2 стадия одна и та же (2).
/// Чтобы после каждого вклада было видно изменение, GateBridge читает ещё StepsDone / ProgressChanged и внутри стадии 2 показывает
/// разное. Проход (IsOpen, Opened) зависит только от того, что стройка закончена, а не от номера вида: SetStage(3) его не открывает.
/// Повторный вызов с теми же числами ничего не меняет. Загрузка сохранения проходит тот же путь (BuildStageLink.OnRestored).
/// </summary>
[Obsolete("УСТАРЕЛ: мост через реку убран (LocationSettings.hasBridge = false, BrokenBridge не строится). " +
          "Скрипт оставлен только для GateBridge/BuildStageLink/StoryFlow/BuildingLightLink/HudMinimap.Bake до перепривязки.", false)]
public class RiverBridge : MonoBehaviour
{
    public GameObject[] stages;
    public GameObject blockers;       // невидимые стенки на провале, только на 1 стадии (или пока стройка не закончена)
    public int stage = 1;

    public event Action<int> OnStageChanged;
    public event Action Opened;       // проход открыт (один раз). После загрузки сохранения тоже приходит: это состояние, не награда
    public event Action<int, int> ProgressChanged;   // этапов сдано, этапов всего: при каждом изменении этих чисел
    public int CurrentStage => stage;
    public bool IsOpen => open;
    public int StepsDone => stepsDone;
    public int StepsTotal => stepsTotal;

    bool buildDriven;                 // true после первого SetBuildProgress: стенки зависят от стройки, а не от номера стадии
    bool open;
    int stepsDone, stepsTotal = 3;

    void Awake() { Apply(stage); }

    public void SetStage(int value)
    {
        value = Mathf.Clamp(value, 1, 3);
        bool changed = value != stage;
        stage = value;
        Apply(value);
        if (changed) OnStageChanged?.Invoke(value);
    }

    public void SetBuildProgress(int stepsDone, int stepsTotal)
    {
        stepsTotal = Mathf.Max(1, stepsTotal);
        stepsDone = Mathf.Clamp(stepsDone, 0, stepsTotal);
        bool progressChanged = !buildDriven || stepsDone != this.stepsDone || stepsTotal != this.stepsTotal;
        buildDriven = true;
        this.stepsDone = stepsDone;
        this.stepsTotal = stepsTotal;

        int visual = stepsDone <= 0 ? 1 : (stepsDone >= stepsTotal ? 3 : 2);
        bool wasOpen = open;
        open = stepsDone >= stepsTotal;

        bool changed = visual != stage;
        stage = visual;
        Apply(visual);

        if (changed) OnStageChanged?.Invoke(visual);
        if (progressChanged) ProgressChanged?.Invoke(stepsDone, stepsTotal);
        if (open && !wasOpen) Opened?.Invoke();
    }

    void Apply(int value)
    {
        if (stages != null)
            for (int i = 0; i < stages.Length; i++)
                if (stages[i] != null) stages[i].SetActive(i == value - 1);
        if (blockers != null) blockers.SetActive(buildDriven ? !open : value == 1);
    }

    [ContextMenu("Тест: стадия 1")] void T1() { SetStage(1); }
    [ContextMenu("Тест: стадия 2")] void T2() { SetStage(2); }
    [ContextMenu("Тест: стадия 3")] void T3() { SetStage(3); }
}

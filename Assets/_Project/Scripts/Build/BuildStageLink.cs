// Assets/_Project/Scripts/Build/BuildStageLink.cs (НЕ в Editor)
// BuildingController умеет менять вид только через HutStages (хижина, костёр). У рынка свой ShopStages (3 стадии),
// у палатки свой TentStages (2 состояния). Этот мостик слушает стройку и переключает нужный компонент:
//   рынок:   1 -> 2 -> 3 по этапам стройки (при одном этапе сразу 3);
//   палатка: 1 -> 2 (с анимацией «раскладывания») по завершении;
//   мост:    RiverBridge.SetBuildProgress по этапам, после последнего открывается проход (стенки + камни MapBorder).
// StoryFlow добавляет его сам, руками вешать не нужно.
using UnityEngine;

public class BuildStageLink : MonoBehaviour
{
    public BuildingController build;
    public ShopStages shop;
    public TentStages tent;
    public RiverBridge bridge;

    bool subscribed;

    void OnEnable() { Subscribe(); }

    void OnDisable()
    {
        if (build == null || !subscribed) return;
        build.StageBuilt -= Apply;
        build.Finished -= Apply;
        subscribed = false;
    }

    void Start()
    {
        Subscribe();
        // игра всегда начинается с первой стадии (ShopStages по умолчанию показывает готовую лавку)
        if (shop != null) shop.SetStage(1);
        if (tent != null) tent.SetStage(1, false);
        if (bridge != null) bridge.SetBuildProgress(0, build != null ? build.StepsTotal : 3);
    }

    void Subscribe()
    {
        if (build == null || subscribed) return;
        build.StageBuilt += Apply;
        build.Finished += Apply;
        subscribed = true;
    }

    void Apply()
    {
        if (build == null) return;
        bool done = build.IsFinished;
        // у ступенчатой стройки стадия = StageCurrent, у одноэтапной StageTotal == 1
        int cur = build.StageTotal > 1 ? build.StageCurrent : (done ? 3 : 1);

        if (shop != null) shop.SetStage(done ? 3 : Mathf.Clamp(cur, 1, 3));
        if (tent != null) tent.SetStage(done ? 2 : 1);

        if (bridge != null)
        {
            bridge.SetBuildProgress(build.StepsDone, build.StepsTotal);
            // камни у прохода к мосту убираются один раз (MapBorder.OpenGate повторный вызов игнорирует)
            if (done && MapBorder.Instance != null) MapBorder.Instance.OpenGate();
        }
    }
}

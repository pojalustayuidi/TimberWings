// Assets/_Project/Scripts/Build/BuildStageLink.cs (НЕ в Editor)
// BuildingController умеет менять вид только через HutStages (хижина, костёр). У рынка свой ShopStages (3 стадии),
// у палатки свой TentStages (2 состояния). Этот мостик слушает стройку и переключает нужный компонент:
//   рынок:   1 -> 2 -> 3 по этапам стройки (при одном этапе сразу 3);
//   палатка: 1 -> 2 (с анимацией «раскладывания») по завершении;
//   рынок (v2): пока игрок реально строит, бригады Stage1_Crew / Stage2_Crew (HutBuildSite) стучат молотками, кран таскает груз,
//            а полоска на табличке растёт вместе с прогрессом стройки (сигнал берётся из BuildingController.IsBuilding);
//   мост:    RiverBridge.SetBuildProgress по этапам, после последнего открывается проход (стена GateWalls у FloatingIsland).
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

    // сигнал «идёт стройка» для бригад лавки: HutBuildSite сам ищет себя среди детей ShopStages, если HutStages нет
    bool lastBuilding;
    float lastProgress = -1f;

    void Update()
    {
        if (build == null || shop == null) return;
        bool now = build.IsBuilding;
        if (now != lastBuilding)
        {
            lastBuilding = now;
            HutBuildSite.SetWorkingFor(shop, now);
            if (!now) { HutBuildSite.SetProgressFor(shop, 0f); lastProgress = -1f; }
        }
        if (now)
        {
            float p = build.GetProgress();
            if (Mathf.Abs(p - lastProgress) > 0.004f)
            {
                lastProgress = p;
                HutBuildSite.SetProgressFor(shop, p);
            }
        }
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
            // стена у прохода к мосту убирается один раз (FloatingIsland.OpenGate повторный вызов игнорирует)
            if (done && FloatingIsland.Instance != null) FloatingIsland.Instance.OpenGate();
        }
    }
}

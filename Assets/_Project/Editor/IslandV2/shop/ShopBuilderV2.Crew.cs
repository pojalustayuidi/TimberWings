// Assets/_Project/Scripts/Editor/Buildings/ShopBuilderV2.Crew.cs   (ТОЛЬКО в папке Editor!)
// ShopBuilderV2: бригады строителей этапов 1 и 2 (так же, как у хижины v2). Стройка не мёртвая: пока игрок строит,
// строители бьют молотками, кран таскает бревно (этап 1) и доски (этап 2), летят щепки и пыль, на табличке растёт полоска прогресса.
//
// Группы «Stage1_Crew» и «Stage2_Crew» лежат прямо под корнем лавки (не внутри Stage1_Props), у каждой:
//   • StageVisibility (видна только на своём этапе, при сдаче этапа «хлопает» и исчезает, как строители хижины);
//   • HutBuildSite с alwaysWorking = false: работают только когда BuildStageLink сообщает «идёт стройка».
// Все координаты в локальных метрах корня лавки (лицо на +Z). Места правятся в блоке «Места (правь тут)» ниже.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class ShopBuilderV2
{
    // ---------------------------------------------------------------- Места (правь тут)
    Vector3 CranePos { get { return new Vector3(-W / 2f - 0.3f, 0.05f, -D / 2f - 0.35f); } }   // кран-журавль: сзади слева
    Vector3 SignPos { get { return new Vector3(W / 2f + 0.7f, 0.06f, D / 2f + 0.2f); } }       // табличка прогресса: справа у входа

    // ---------------------------------------------------------------- бригада
    void BuildCrew(Transform root, int stage)
    {
        var grp = Obj(root, stage == 1 ? "Stage1_Crew" : "Stage2_Crew", Vector3.zero);
        Undo.RegisterCreatedObjectUndo(grp, "Лавка v2");
        var t = grp.transform;
        var anims = new List<HutBuildSite.Anim>();
        var hoists = new List<HutBuildSite.Hoist>();

        if (stage == 1)
        {
            // этап 1 -> 2: настилают помост, кран поднимает бревно
            AddHammerer(t, anims, new Vector3(-0.9f, PlatH, 0.15f), 0f, "Red", 1.00f, 0.00f);
            AddHammerer(t, anims, new Vector3(0.8f, PlatH, -0.35f), 180f, "Lilac", 0.90f, 0.37f);
            hoists.Add(MakeCrane(t, CranePos, 0f, false, 0f));
        }
        else
        {
            // этап 2 -> 3: ставят стропила и вешают навес, кран поднимает доски
            AddHammerer(t, anims, new Vector3(1.15f, PlatH, -0.35f), -90f, "Red", 1.00f, 0.10f);
            AddHammerer(t, anims, new Vector3(-1.55f, PlatH, 0.5f), 90f, "Tarp", 0.95f, 0.50f);
            hoists.Add(MakeCrane(t, CranePos, 0f, true, 0.35f));
        }

        var fill = AddProgressSign(t, SignPos, -20f);

        var site = grp.AddComponent<HutBuildSite>();
        site.minStage = stage;
        site.maxStage = stage;
        site.alwaysWorking = false;             // работают только пока игрок реально строит (сигнал из BuildStageLink)
        site.anims = anims.ToArray();
        site.hoists = hoists.ToArray();
        site.progressFill = fill;

        var vis = grp.AddComponent<StageVisibility>();
        vis.minStage = stage;
        vis.maxStage = stage;
        vis.exitStyle = StageVisibility.ExitStyle.Pop;
        vis.exitTime = 1.0f;
    }

    // строитель с молотком: правая рука на шарнире (плечо), удар идёт по волне Strike
    void AddHammerer(Transform p, List<HutBuildSite.Anim> anims, Vector3 pos, float yaw, string shirt, float freq, float phase)
    {
        Transform shoulder, hit;
        MakeBuilder(p, "Builder", pos, yaw, shirt, out shoulder, out hit);
        anims.Add(new HutBuildSite.Anim
        {
            t = shoulder, rotate = true, axis = Vector3.right, wave = HutBuildSite.Wave.Strike,
            amp = -85f, bias = -20f, rest = 0f, freq = freq, phase = phase, fx = hit
        });
    }

    // низкополигональный строитель в жёлтой каске с облачным помпоном. shoulder = правое плечо (шарнир), hit = точка удара перед ним
    Transform MakeBuilder(Transform p, string name, Vector3 pos, float yaw, string shirt, out Transform shoulder, out Transform hit)
    {
        var root = Obj(p, name, pos);
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = root.transform;

        Box(t, "LegL", new Vector3(-0.1f, 0.26f, 0f), new Vector3(0.15f, 0.52f, 0.17f), "WoodDark");
        Box(t, "LegR", new Vector3(0.1f, 0.26f, 0f), new Vector3(0.15f, 0.52f, 0.17f), "WoodDark");
        Box(t, "Torso", new Vector3(0f, 0.78f, 0f), new Vector3(0.42f, 0.54f, 0.26f), shirt);
        Box(t, "Belt", new Vector3(0f, 0.55f, 0f), new Vector3(0.44f, 0.07f, 0.28f), "WoodDark");
        Blob(t, "Head", new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 0.3f, 0.3f), "Skin", 1);
        Cyl(t, "HatTop", new Vector3(0f, 1.37f, 0f), 0.17f, 0.1f, "Roof", 8);
        Cyl(t, "HatBrim", new Vector3(0f, 1.33f, 0f), 0.24f, 0.03f, "RoofShade", 8);
        Blob(t, "HatPuff", new Vector3(0f, 1.47f, 0f), new Vector3(0.13f, 0.1f, 0.13f), "Cloud", 2);
        Box(t, "ArmL", new Vector3(-0.27f, 0.78f, 0.05f), new Vector3(0.1f, 0.4f, 0.1f), shirt, new Vector3(-20f, 0f, 0f));

        var sh = Obj(t, "ShoulderR", new Vector3(0.27f, 0.98f, 0f));
        Box(sh.transform, "ArmR", new Vector3(0f, -0.19f, 0f), new Vector3(0.1f, 0.4f, 0.1f), shirt);
        Box(sh.transform, "HandR", new Vector3(0f, -0.4f, 0f), new Vector3(0.1f, 0.1f, 0.1f), "Skin");
        Box(sh.transform, "HammerHandle", new Vector3(0f, -0.4f, 0.15f), new Vector3(0.04f, 0.04f, 0.34f), "WoodDark");
        Box(sh.transform, "HammerHead", new Vector3(0f, -0.4f, 0.34f), new Vector3(0.18f, 0.08f, 0.08f), "Metal");

        var h = Obj(t, "HitPoint", new Vector3(0.27f, 0.85f, 0.65f));
        shoulder = sh.transform;
        hit = h.transform;
        return t;
    }

    // кран-журавль: треножник со стрелой, верёвка идёт от шкива к крюку, груз ходит вверх-вниз (HutBuildSite.Hoist)
    HutBuildSite.Hoist MakeCrane(Transform p, Vector3 pos, float yaw, bool planks, float phase)
    {
        var c = Obj(p, "Crane", pos);
        c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = c.transform;

        Vector3 apex = new Vector3(0f, 3.2f, 0f);
        Rod(t, "LegA", new Vector3(-0.6f, 0f, -0.5f), apex, 0.07f, "WoodMid");
        Rod(t, "LegB", new Vector3(-0.6f, 0f, 0.5f), apex, 0.07f, "WoodMid");
        Rod(t, "LegC", new Vector3(0.5f, 0f, 0f), apex, 0.07f, "WoodMid");
        Beam(t, "Boom", new Vector3(-0.2f, 3.2f, 0f), new Vector3(1.5f, 3.05f, 0f), 0.1f, "WoodDark");
        Cyl(t, "Pulley", new Vector3(1.5f, 3.0f, 0f), 0.1f, 0.06f, "MetalDark", 10, new Vector3(90f, 0f, 0f));
        var top = Obj(t, "RopeTop", new Vector3(1.5f, 2.95f, 0f));
        // лебёдка на ногах: бочонок с рукоятью; на вершине флажок-облачко, чтобы кран был «свой»
        Cyl(t, "WinchDrum", new Vector3(-0.6f, 0.9f, 0f), 0.1f, 1.0f, "WoodLight", 8, new Vector3(90f, 0f, 0f));
        Box(t, "WinchCrank", new Vector3(-0.6f, 0.9f, 0.62f), new Vector3(0.05f, 0.34f, 0.05f), "Red");
        Blob(t, "CraneCloud", new Vector3(0f, 3.32f, 0f), new Vector3(0.34f, 0.2f, 0.28f), "Cloud", 1);

        Vector3 low = new Vector3(1.5f, 0.55f, 0f), high = new Vector3(1.5f, 2.45f, 0f);
        var rope = Rod(t, "HoistRope", new Vector3(1.5f, 2.95f, 0f), new Vector3(1.5f, 1.5f, 0f), 0.018f, "Rope", 5);

        var hook = Obj(t, "Hook", low);
        var ht = hook.transform;
        Cyl(ht, "HookRing", Vector3.zero, 0.06f, 0.03f, "MetalDark", 8, new Vector3(90f, 0f, 0f));
        Rod(ht, "SlingA", Vector3.zero, new Vector3(0f, -0.45f, -0.45f), 0.012f, "Rope", 4);
        Rod(ht, "SlingB", Vector3.zero, new Vector3(0f, -0.45f, 0.45f), 0.012f, "Rope", 4);
        if (planks)
        {
            for (int i = 0; i < 3; i++)
                Box(ht, "HoistPlank", new Vector3(0f, -0.52f - i * 0.07f, 0f), new Vector3(0.5f, 0.06f, 1.3f), i % 2 == 0 ? "Plank" : "PlankDark");
            Box(ht, "HoistBand", new Vector3(0f, -0.6f, -0.4f), new Vector3(0.54f, 0.3f, 0.05f), "Rope");
            Box(ht, "HoistBand", new Vector3(0f, -0.6f, 0.4f), new Vector3(0.54f, 0.3f, 0.05f), "Rope");
        }
        else
        {
            Log(ht, "HoistLog", new Vector3(0f, -0.5f, -0.7f), new Vector3(0f, -0.5f, 0.7f), 0.14f, "WoodLight");
        }

        return new HutBuildSite.Hoist
        {
            load = ht, top = top.transform, rope = rope.transform,
            low = low, high = high, period = 7f, swayDeg = 3f, phase = phase
        };
    }

    // табличка с полоской прогресса; возвращает пивот заливки (слева), HutBuildSite меняет его масштаб по X
    Transform AddProgressSign(Transform p, Vector3 pos, float yaw)
    {
        var sg = Obj(p, "ProgressSign", pos);
        sg.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = sg.transform;
        Box(t, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f), "WoodDark");
        Box(t, "Frame", new Vector3(0f, 1.45f, -0.01f), new Vector3(1.4f, 0.72f, 0.05f), "Sky");
        Box(t, "Board", new Vector3(0f, 1.45f, 0.02f), new Vector3(1.3f, 0.62f, 0.06f), "Cream");
        Box(t, "BarBack", new Vector3(0f, 1.34f, 0.06f), new Vector3(1.0f, 0.14f, 0.03f), "WoodDark");
        var fill = Obj(t, "BarFill", new Vector3(-0.5f, 1.34f, 0.085f));
        Box(fill.transform, "Fill", new Vector3(0.5f, 0f, 0f), new Vector3(1.0f, 0.1f, 0.03f), "Moss");
        // значок молотка и облачко сверху
        Box(t, "IconHandle", new Vector3(-0.34f, 1.56f, 0.06f), new Vector3(0.04f, 0.2f, 0.03f), "WoodDark", new Vector3(0f, 0f, 30f));
        Box(t, "IconHead", new Vector3(-0.27f, 1.65f, 0.06f), new Vector3(0.2f, 0.09f, 0.04f), "Red", new Vector3(0f, 0f, 30f));
        Blob(t, "SignCloudA", new Vector3(-0.35f, 1.86f, 0f), new Vector3(0.42f, 0.2f, 0.16f), "Cloud", 1);
        Blob(t, "SignCloudB", new Vector3(0.05f, 1.9f, 0f), new Vector3(0.34f, 0.18f, 0.14f), "Cloud", 2);
        Box(t, "RibbonA", new Vector3(0.6f, 1.66f, 0.05f), new Vector3(0.09f, 0.4f, 0.025f), "Red", new Vector3(0f, 0f, 8f));
        return fill.transform;
    }
}
#endif

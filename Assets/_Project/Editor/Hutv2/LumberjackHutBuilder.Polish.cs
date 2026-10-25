// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Polish.cs   (ТОЛЬКО в папке Editor!)
// «Доводка» хижины v2. Вызывается из BuildStage1/2/3 одной строкой перед EndStage (PolishStage1/2/3).
//
//  Стадия 1: забор получает StageVisibility 1..2 (на стадии 3 он «уходит в землю»), костровая яма с огнём (HutFlicker + свет),
//            бригада строителей WorkCrew (HutBuildSite) и табличка прогресса.
//  Стадия 2: другая бригада (леса, пилорама, тыл хижины), табличка прогресса.
//  Стадия 3: арка с фонарями на месте ворот (забора уже нет), гирлянда на свесе крыши, ящик с цветами под окном,
//            колода с топором, очаг в хижине (HutFlicker + свет в окне).
//
// Все координаты в локальных метрах стадии. Положения правятся в блоке «Места» ниже.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class LumberjackHutBuilder
{
    // ---------------------------------------------------------------- Места (правь тут)
    static readonly Vector3 PitPos = new Vector3(-4.0f, 0f, 2.9f);        // костровая яма
    static readonly Vector3 SignPos = new Vector3(0.2f, 0f, 4.1f);        // табличка прогресса стройки
    static readonly Vector3 SawyerPos = new Vector3(-3.6f, 0f, 0.2f);     // козлы с распиловкой
    static readonly Vector3 CranePos = new Vector3(-3.3f, 0f, -2.8f);     // кран-журавль с грузом
    static readonly Vector3 ChopBlockPos = new Vector3(-3.0f, 0f, 1.9f);  // колода с топором (стадия 3)

    // ---------------------------------------------------------------- точки входа
    static void PolishStage1(Transform st)
    {
        EnsurePolishPalette();
        FenceGoesAtStage3(st);
        AddFirePit(st);
        AddWorkCrew(st, 1);
    }

    static void PolishStage2(Transform st)
    {
        EnsurePolishPalette();
        AddWorkCrew(st, 2);
    }

    static void PolishStage3(Transform st)
    {
        EnsurePolishPalette();
        AddGateArch(st);
        AddGarland(st);
        AddWindowBox(st);
        AddChopBlock(st);
        AddHearth(st);
    }

    static void EnsurePolishPalette()
    {
        if (!Palette.ContainsKey("Skin")) Palette["Skin"] = new Color(0.98f, 0.78f, 0.62f);
        if (!Palette.ContainsKey("Flame")) Palette["Flame"] = new Color(1.00f, 0.52f, 0.16f);
        if (!Palette.ContainsKey("Coal")) Palette["Coal"] = new Color(0.27f, 0.23f, 0.24f);
    }

    static void NoShadow(GameObject g)
    {
        var r = g != null ? g.GetComponent<Renderer>() : null;
        if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- забор: виден на стадиях 1 и 2, на 3 уходит в землю
    static void FenceGoesAtStage3(Transform st)
    {
        var fence = st.Find("Fence");
        if (fence == null) { Debug.LogWarning("[HutV2] Polish: группа «Fence» в Stage1 не найдена, забор останется на всех стадиях."); return; }
        var v = fence.gameObject.AddComponent<StageVisibility>();
        v.minStage = 1;
        v.maxStage = 2;
        v.exitStyle = StageVisibility.ExitStyle.Sink;
        v.exitTime = 1.8f;
    }

    // ---------------------------------------------------------------- костровая яма во дворе (остаётся на всех стадиях)
    static void AddFirePit(Transform st)
    {
        var pit = Obj(st, "FirePit", PitPos);
        var t = pit.transform;

        NoShadow(Cyl(t, "Ash", new Vector3(0f, 0.03f, 0f), 0.62f, 0.05f, "Coal", 10));
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            float sz = R(0.24f, 0.34f);
            Box(t, "RingStone", new Vector3(Mathf.Cos(a) * 0.62f, sz * 0.3f, Mathf.Sin(a) * 0.62f), new Vector3(sz, sz * 0.6f, sz * R(0.8f, 1.1f)),
                R() < 0.35f ? "StoneDark" : "Stone", new Vector3(0f, R(0f, 90f), 0f));
        }
        // дрова шалашиком
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 20f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Log(t, "FireLog", dir * 0.42f + Vector3.up * 0.1f, dir * 0.05f + Vector3.up * 0.55f, 0.06f);
        }

        // пламя: пивот у основания, чтобы HutFlicker «дышал» вверх
        var flames = new List<Transform>();
        flames.Add(FlamePivot(t, "FlameOuter", new Vector3(0f, 0.1f, 0f), 0.30f, 0.75f, "Flame"));
        flames.Add(FlamePivot(t, "FlameMid", new Vector3(0.07f, 0.1f, 0.04f), 0.20f, 0.58f, "Roof"));
        flames.Add(FlamePivot(t, "FlameCore", new Vector3(-0.04f, 0.1f, -0.05f), 0.11f, 0.38f, "HutLamp"));
        var fl = pit.AddComponent<HutFlicker>();
        fl.flames = flames.ToArray();
        fl.speed = 6.5f;
        fl.amount = 0.24f;
        fl.sway = 6f;

        // треножник с котелком над огнём
        Vector3 apex = new Vector3(0f, 1.65f, 0f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 40f) * Mathf.Deg2Rad;
            Rod(t, "TripodLeg", new Vector3(Mathf.Cos(a) * 0.95f, 0f, Mathf.Sin(a) * 0.95f), apex, 0.035f, "WoodDark", 5);
        }
        Rod(t, "PotChain", apex, new Vector3(0f, 1.28f, 0f), 0.012f, "MetalDark", 4);
        Cyl(t, "Pot", new Vector3(0f, 1.12f, 0f), 0.2f, 0.22f, "MetalDark", 10);
        Cyl(t, "PotLid", new Vector3(0f, 1.25f, 0f), 0.21f, 0.04f, "Metal", 10);

        // два бревна-сиденья
        Log(t, "SeatLogA", new Vector3(-0.7f, 0.16f, 1.25f), new Vector3(0.7f, 0.16f, 1.25f), 0.16f, "WoodMid");
        Log(t, "SeatLogB", new Vector3(-1.4f, 0.16f, -0.3f), new Vector3(-1.1f, 0.16f, -1.3f), 0.15f, "WoodLight");

        // свет: «LanternLight…» подхватывает FireSource (мерцание, пул настоящих светов)
        var lgo = new GameObject("LanternLight_FirePit");
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.62f, 0.30f);
        l.range = 7.5f;
        l.intensity = 1.8f;
        l.shadows = LightShadows.None;
    }

    static Transform FlamePivot(Transform p, string name, Vector3 basePos, float radius, float height, string mat)
    {
        var pv = Obj(p, name + "Pivot", basePos);
        NoShadow(ConeP(pv.transform, name, new Vector3(0f, height * 0.5f, 0f), radius, height, mat, 7));
        return pv.transform;
    }

    // ---------------------------------------------------------------- бригада строителей (HutBuildSite)
    static void AddWorkCrew(Transform st, int stage)
    {
        var crew = Obj(st, "WorkCrew", Vector3.zero);
        var t = crew.transform;
        var anims = new List<HutBuildSite.Anim>();
        var hoists = new List<HutBuildSite.Hoist>();

        if (stage == 1)
        {
            // стадия 1 -> 2: кладут нижние венцы, пилят брёвна, поднимают бревно краном
            AddHammerer(t, anims, new Vector3(-1.0f, 0f, 2.55f), 180f, "Red", 1.00f, 0.00f);
            AddHammerer(t, anims, new Vector3(1.6f, 0f, -2.7f), 0f, "Lilac", 0.90f, 0.37f);
            AddSawyer(t, anims, SawyerPos, 0f, "Leaf", 1.10f);
            hoists.Add(MakeCrane(t, CranePos, 0f, false, 0f));
        }
        else
        {
            // стадия 2 -> 3: стропила и кровля: верхний настил лесов, пилорама, тыл хижины, доски краном
            AddHammerer(t, anims, new Vector3(-1.95f, 1.98f, 2.23f), 180f, "Red", 1.00f, 0.10f);
            AddHammerer(t, anims, new Vector3(5.0f, 0.12f, 1.2f), 180f, "Tarp", 0.95f, 0.50f);
            AddHammerer(t, anims, new Vector3(1.0f, 0f, -2.7f), 0f, "Lilac", 0.90f, 0.30f);
            AddSawyer(t, anims, SawyerPos, 0f, "Leaf", 1.10f);
            hoists.Add(MakeCrane(t, CranePos, 0f, true, 0.35f));
        }

        var fill = AddProgressSign(t, SignPos, 0f);

        var site = crew.AddComponent<HutBuildSite>();
        site.minStage = stage;
        site.maxStage = stage;
        site.alwaysWorking = false;               // работают только пока игрок реально строит (сигнал из BuildingController)
        site.anims = anims.ToArray();
        site.hoists = hoists.ToArray();
        site.progressFill = fill;

        var vis = crew.AddComponent<StageVisibility>();
        vis.minStage = stage;
        vis.maxStage = stage;
        vis.exitStyle = StageVisibility.ExitStyle.Pop;
        vis.exitTime = 1.0f;
    }

    // строитель с молотком: правая рука на шарнире (плечо), удар по Strike
    static void AddHammerer(Transform p, List<HutBuildSite.Anim> anims, Vector3 pos, float yaw, string shirt, float freq, float phase)
    {
        Transform shoulder, hit;
        MakeBuilder(p, "Builder", pos, yaw, shirt, true, out shoulder, out hit);
        anims.Add(new HutBuildSite.Anim
        {
            t = shoulder, rotate = true, axis = Vector3.right, wave = HutBuildSite.Wave.Strike,
            amp = -85f, bias = -20f, rest = 0f, freq = freq, phase = phase, fx = hit
        });
    }

    // козлы с брёвнышком и пильщик: пила ходит туда-сюда, летят опилки, строитель чуть кланяется в такт
    static void AddSawyer(Transform p, List<HutBuildSite.Anim> anims, Vector3 pos, float yaw, string shirt, float freq)
    {
        var stn = Obj(p, "SawStation", pos);
        stn.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = stn.transform;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * 0.55f;
            Beam(t, "HorseLegA", new Vector3(x, 0f, -0.28f), new Vector3(x, 0.5f, 0f), 0.07f, "WoodDark");
            Beam(t, "HorseLegB", new Vector3(x, 0f, 0.28f), new Vector3(x, 0.5f, 0f), 0.07f, "WoodDark");
            Beam(t, "HorseTop", new Vector3(x, 0.5f, -0.3f), new Vector3(x, 0.5f, 0.3f), 0.08f, "WoodMid");
        }
        Log(t, "CutLog", new Vector3(-0.95f, 0.64f, 0f), new Vector3(0.95f, 0.64f, 0f), 0.13f, "WoodLight");
        Blob(t, "Sawdust", new Vector3(0f, 0.03f, 0f), new Vector3(0.7f, 0.07f, 0.4f), "Plank", 2);

        var cut = Obj(t, "CutPoint", new Vector3(0f, 0.66f, 0f));
        var saw = Obj(t, "SawPivot", new Vector3(0f, 0f, 0f));
        Box(saw.transform, "SawBlade", new Vector3(0f, 0.68f, 0f), new Vector3(0.02f, 0.14f, 0.55f), "Metal");
        Box(saw.transform, "SawHandle", new Vector3(0f, 0.8f, 0.3f), new Vector3(0.05f, 0.1f, 0.1f), "Red");

        Transform shoulder, hit;
        var fig = MakeBuilder(t, "Sawyer", new Vector3(0f, 0f, 0.7f), 180f, shirt, false, out shoulder, out hit);
        // рука «держит» пилу: шарнир чуть вперёд
        shoulder.localRotation = Quaternion.Euler(-70f, 0f, 0f);

        anims.Add(new HutBuildSite.Anim
        {
            t = saw.transform, rotate = false, axis = Vector3.forward, wave = HutBuildSite.Wave.Sine,
            amp = 0.14f, bias = 0f, rest = 0f, freq = freq, phase = 0f, fx = cut.transform, dustPerSec = 7f
        });
        anims.Add(new HutBuildSite.Anim
        {
            t = fig, rotate = true, axis = Vector3.right, wave = HutBuildSite.Wave.Sine,
            amp = 5f, bias = 0f, rest = 0f, freq = freq, phase = 0f
        });
    }

    // низкополигональный строитель в каске. Возвращает корень; shoulder = правое плечо (шарнир), hit = точка удара перед ним
    static Transform MakeBuilder(Transform p, string name, Vector3 pos, float yaw, string shirt, bool hammer, out Transform shoulder, out Transform hit)
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
        Box(t, "ArmL", new Vector3(-0.27f, 0.78f, 0.05f), new Vector3(0.1f, 0.4f, 0.1f), shirt, new Vector3(-20f, 0f, 0f));

        var sh = Obj(t, "ShoulderR", new Vector3(0.27f, 0.98f, 0f));
        Box(sh.transform, "ArmR", new Vector3(0f, -0.19f, 0f), new Vector3(0.1f, 0.4f, 0.1f), shirt);
        Box(sh.transform, "HandR", new Vector3(0f, -0.4f, 0f), new Vector3(0.1f, 0.1f, 0.1f), "Skin");
        if (hammer)
        {
            Box(sh.transform, "HammerHandle", new Vector3(0f, -0.4f, 0.15f), new Vector3(0.04f, 0.04f, 0.34f), "WoodDark");
            Box(sh.transform, "HammerHead", new Vector3(0f, -0.4f, 0.34f), new Vector3(0.18f, 0.08f, 0.08f), "Metal");
        }
        var h = Obj(t, "HitPoint", new Vector3(0.27f, 0.85f, 0.65f));
        shoulder = sh.transform;
        hit = h.transform;
        return t;
    }

    // кран-журавль: треножник со стрелой, верёвка идёт от шкива к крюку, груз ходит вверх-вниз (HutBuildSite.Hoist)
    static HutBuildSite.Hoist MakeCrane(Transform p, Vector3 pos, float yaw, bool planks, float phase)
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
        // лебёдка на ногах: бочонок с рукоятью
        Cyl(t, "WinchDrum", new Vector3(-0.6f, 0.9f, 0f), 0.1f, 1.0f, "WoodLight", 8, new Vector3(90f, 0f, 0f));
        Box(t, "WinchCrank", new Vector3(-0.6f, 0.9f, 0.62f), new Vector3(0.05f, 0.34f, 0.05f), "Red");

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
    static Transform AddProgressSign(Transform p, Vector3 pos, float yaw)
    {
        var sg = Obj(p, "ProgressSign", pos);
        sg.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = sg.transform;
        Box(t, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f), "WoodDark");
        Box(t, "Frame", new Vector3(0f, 1.45f, -0.01f), new Vector3(1.4f, 0.72f, 0.05f), "WoodMid");
        Box(t, "Board", new Vector3(0f, 1.45f, 0.02f), new Vector3(1.3f, 0.62f, 0.06f), "Cream");
        Box(t, "BarBack", new Vector3(0f, 1.34f, 0.06f), new Vector3(1.0f, 0.14f, 0.03f), "WoodDark");
        var fill = Obj(t, "BarFill", new Vector3(-0.5f, 1.34f, 0.085f));
        Box(fill.transform, "Fill", new Vector3(0.5f, 0f, 0f), new Vector3(1.0f, 0.1f, 0.03f), "Moss");
        Box(t, "IconHandle", new Vector3(-0.34f, 1.56f, 0.06f), new Vector3(0.04f, 0.2f, 0.03f), "WoodDark", new Vector3(0f, 0f, 30f));
        Box(t, "IconHead", new Vector3(-0.27f, 1.65f, 0.06f), new Vector3(0.2f, 0.09f, 0.04f), "Red", new Vector3(0f, 0f, 30f));
        cloths.Add(Cloth(t, "SignRibbon", new Vector3(0.62f, 1.8f, 0.05f), 0.09f, 0.14f, 3, "Red", 20f, 0.2f));
        return fill.transform;
    }

    // ---------------------------------------------------------------- стадия 3: арка у ворот (забор ушёл, вход остаётся нарядным)
    static void AddGateArch(Transform st)
    {
        var arch = Obj(st, "GateArch", new Vector3(GateX, 0f, FenceZ1));
        var t = arch.transform;
        float hx = GateW * 0.5f + 0.15f;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * hx;
            Box(t, "PillarBase", new Vector3(x, 0.2f, 0f), new Vector3(0.62f, 0.4f, 0.62f), "StoneDark");
            Box(t, "Pillar", new Vector3(x, 0.95f, 0f), new Vector3(0.46f, 1.1f, 0.46f), "Stone", new Vector3(0f, R(-4f, 4f), 0f));
            Box(t, "PillarCap", new Vector3(x, 1.56f, 0f), new Vector3(0.6f, 0.12f, 0.6f), "StoneDark");
            Blob(t, "PillarMoss", new Vector3(x + sx * 0.05f, 0.45f, 0.2f), new Vector3(0.4f, 0.16f, 0.25f), "Moss");
            for (int i = 0; i < 4; i++)
                Flower(t, new Vector3(x + sx * 0.5f + R(-0.12f, 0.12f), 0.05f, R(-0.2f, 0.25f)), i % 2 == 0 ? "Pink" : "Cream");
            Box(t, "LanternBracket", new Vector3(x - sx * 0.2f, 1.3f, 0.3f), new Vector3(0.3f, 0.05f, 0.05f), "WoodDark");
            LanternAt(t, "GateLantern" + (sx < 0 ? "L" : "R"), new Vector3(x - sx * 0.32f, 1.3f, 0.3f));
        }
        // перекладина с вывеской и вымпелами
        Beam(t, "ArchBeam", new Vector3(-hx, 1.85f, 0f), new Vector3(hx, 1.85f, 0f), 0.14f, "WoodDark");
        Rod(t, "SignRopeL", new Vector3(-0.45f, 1.85f, 0f), new Vector3(-0.45f, 1.62f, 0f), 0.012f, "Rope", 4);
        Rod(t, "SignRopeR", new Vector3(0.45f, 1.85f, 0f), new Vector3(0.45f, 1.62f, 0f), 0.012f, "Rope", 4);
        var sign = Obj(t, "ArchSign", new Vector3(0f, 1.5f, 0f));
        Box(sign.transform, "SignBoard", new Vector3(0f, 0f, 0f), new Vector3(1.0f, 0.3f, 0.06f), "WoodLight");
        Box(sign.transform, "SignTrim", new Vector3(0f, 0f, 0.035f), new Vector3(0.9f, 0.04f, 0.02f), "Cream");
        Box(sign.transform, "SignAxe", new Vector3(0f, 0.02f, 0.045f), new Vector3(0.22f, 0.1f, 0.02f), "Red");
        swingers.Add(sign.transform);
        for (int sx = -1; sx <= 1; sx += 2)
            cloths.Add(Cloth(t, "ArchPennant" + (sx < 0 ? "L" : "R"), new Vector3(sx * (hx - 0.15f), 1.78f, 0f), 0.1f, 0.16f, 4, sx < 0 ? "Red" : "Lilac", 16f, sx * 0.7f));
    }

    // ---------------------------------------------------------------- гирлянда из лампочек на свесе крыши
    static void AddGarland(Transform st)
    {
        float z = D / 2f + 0.5f;
        float y = EaveY - 0.12f;
        Swag(st, new Vector3(-W / 2f - 0.2f, y, z), new Vector3(-0.75f, y, z), 0.28f, 5);
        Swag(st, new Vector3(0.75f, y, z), new Vector3(W / 2f + 0.2f, y, z), 0.28f, 5);
    }

    static void Swag(Transform p, Vector3 a, Vector3 b, float sag, int bulbs)
    {
        Vector3 prev = a;
        int segs = bulbs + 1;
        for (int i = 1; i <= segs; i++)
        {
            float u = i / (float)segs;
            Vector3 pt = Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u));
            Rod(p, "GarlandWire", prev, pt, 0.01f, "MetalDark", 4);
            if (i < segs) Blob(p, "GarlandBulb", pt + Vector3.down * 0.07f, new Vector3(0.1f, 0.12f, 0.1f), "HutLamp", 1);
            prev = pt;
        }
    }

    // ---------------------------------------------------------------- ящик с цветами под окном фасада
    static void AddWindowBox(Transform st)
    {
        var wb = Obj(st, "WindowBox", new Vector3(-1.45f, 1.0f, D / 2f + LogR + 0.14f));
        var t = wb.transform;
        Box(t, "BoxBody", Vector3.zero, new Vector3(0.95f, 0.17f, 0.24f), "PlankDark");
        Box(t, "BoxTrim", new Vector3(0f, 0.07f, 0.12f), new Vector3(0.97f, 0.04f, 0.03f), "WoodDark");
        for (int i = 0; i < 6; i++)
        {
            float x = -0.38f + i * 0.152f;
            Blob(t, "BoxLeaf", new Vector3(x, 0.12f, 0f), new Vector3(0.14f, 0.1f, 0.14f), "Leaf", 0);
            Flower(t, new Vector3(x, 0.17f, R(-0.04f, 0.04f)), i % 3 == 0 ? "Pink" : (i % 3 == 1 ? "Lilac" : "Cream"));
        }
    }

    // ---------------------------------------------------------------- колода с топором и щепками
    static void AddChopBlock(Transform st)
    {
        var cb = Obj(st, "ChopBlock", ChopBlockPos);
        var t = cb.transform;
        Part(t, "BlockLog", HutMeshLib.LogCyl(10), Mat("WoodLight"), new Vector3(0f, 0.21f, 0f), new Vector3(0.5f, 0.42f, 0.5f), Quaternion.identity)
            .GetComponent<MeshRenderer>().sharedMaterials = new[] { Mat("WoodLight"), Mat("LogEnd") };
        Beam(t, "AxeHandle", new Vector3(0.02f, 0.4f, 0f), new Vector3(0.27f, 0.86f, 0.05f), 0.05f, "WoodDark");
        Box(t, "AxeHead", new Vector3(0.02f, 0.44f, 0f), new Vector3(0.05f, 0.16f, 0.2f), "Metal", new Vector3(0f, 0f, 0f));
        for (int i = 0; i < 6; i++)
            Box(t, "Chip", new Vector3(R(-0.6f, 0.6f), 0.02f, R(-0.6f, 0.6f)), new Vector3(R(0.08f, 0.16f), 0.03f, R(0.04f, 0.07f)), "LogEnd", new Vector3(0f, R(0f, 180f), 0f));
        // пара расколотых поленьев рядом
        Log(t, "SplitLogA", new Vector3(-0.55f, 0.1f, 0.4f), new Vector3(-0.55f, 0.1f, 0.95f), 0.09f, "WoodMid");
        Log(t, "SplitLogB", new Vector3(-0.72f, 0.1f, 0.5f), new Vector3(-0.7f, 0.1f, 1.0f), 0.09f, "WoodLight");
    }

    // ---------------------------------------------------------------- очаг в хижине: огонь, свет через окно
    static void AddHearth(Transform st)
    {
        var h = Obj(st, "Hearth", new Vector3(-1.78f, FoundH + 0.06f, -0.4f));
        var t = h.transform;
        Box(t, "HearthBase", new Vector3(0f, 0.22f, 0f), new Vector3(0.44f, 0.44f, 0.9f), "StoneDark");
        Box(t, "HearthMouth", new Vector3(0.2f, 0.24f, 0f), new Vector3(0.1f, 0.28f, 0.5f), "Coal");
        Box(t, "HearthBreast", new Vector3(-0.05f, 1.1f, 0f), new Vector3(0.3f, 1.3f, 0.6f), "Stone");
        Box(t, "HearthMantel", new Vector3(0.05f, 0.5f, 0f), new Vector3(0.5f, 0.07f, 0.98f), "WoodDark");
        Cyl(t, "MantelCup", new Vector3(0.05f, 0.6f, 0.3f), 0.05f, 0.1f, "Red", 8);

        var flames = new List<Transform>();
        flames.Add(FlamePivot(t, "HearthFlameOuter", new Vector3(0.16f, 0.12f, 0f), 0.16f, 0.3f, "Flame"));
        flames.Add(FlamePivot(t, "HearthFlameCore", new Vector3(0.18f, 0.12f, 0.05f), 0.09f, 0.2f, "HutLamp"));
        var fl = t.gameObject.AddComponent<HutFlicker>();
        fl.flames = flames.ToArray();
        fl.speed = 7f;
        fl.amount = 0.2f;
        fl.sway = 4f;

        var lgo = new GameObject("LanternLight_Hearth");
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(0.35f, 0.5f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.60f, 0.28f);
        l.range = 4.8f;
        l.intensity = 1.4f;
        l.shadows = LightShadows.None;
    }
}
#endif

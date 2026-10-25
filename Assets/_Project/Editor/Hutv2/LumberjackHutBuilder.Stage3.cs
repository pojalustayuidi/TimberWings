// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Stage3.cs   (ТОЛЬКО в папке Editor!)
// СТАДИЯ 3 = «готово»: остальные венцы, фронтоны, кровля из досок, дверь, окна (HutWindowGlass), труба с дымом,
// крыльцо с фонарями (LanternLight), вывески и флаги, дрова, дорожка от ворот, а также рабочая пилорама:
// стол, каретка с брёвнышком, диск, водяное колесо, шкивы и ремень, сырьё и склад готовых досок.
// Пилорама собрана под SawmillAnimator (диск, колесо, шкивы, каретка, queueLogs, readyStacks, sawdustPoint).
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class LumberjackHutBuilder
{
    static Transform BuildStage3(Transform root)
    {
        var s = BeginStage(root, "Stage3");
        var st = s.transform;

        // ---------------------------------------------------------------- стены: оставшиеся венцы
        {
            var walls = Obj(st, "WallsTop", Vector3.zero);
            for (int c = CoursesStage2; c < CoursesTotal; c++) WallCourse(walls.transform, c);

            // перемычка над дверью
            Box(walls.transform, "DoorLintel", new Vector3(0f, FoundH + DoorH + 0.1f, D / 2f + 0.02f), new Vector3(DoorHalf * 2f + 0.5f, 0.18f, 0.44f), "WoodDark");

            // фронтоны: призмы из досок + круглое чердачное окошко
            float gy = EaveY + 0.78f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * (W / 2f);
                Part(walls.transform, "Gable", HutMeshLib.Prism, Mat("Plank"), new Vector3(x, gy, 0f), new Vector3(D + 0.1f, 1.56f, 0.3f), Quaternion.Euler(0f, 90f, 0f));
                // горизонтальные доски фронтона (рельеф)
                for (int k = 0; k < 4; k++)
                {
                    float yy = EaveY + 0.2f + k * 0.34f;
                    float half = (D / 2f + 0.05f) * (1f - (k * 0.34f + 0.2f) / 1.56f) - 0.05f;
                    if (half < 0.15f) continue;
                    Box(walls.transform, "GableBoard", new Vector3(x + sx * 0.16f, yy, 0f), new Vector3(0.04f, 0.05f, half * 2f), "PlankDark");
                }
                var frameGo = Cyl(walls.transform, "AtticFrame", new Vector3(x + sx * 0.17f, EaveY + 0.95f, 0f), 0.34f, 0.07f, "WoodDark", 10, new Vector3(0f, 0f, 90f));
                var glass = Cyl(walls.transform, "HutWindowGlass", new Vector3(x + sx * 0.2f, EaveY + 0.95f, 0f), 0.27f, 0.06f, "HutWindowGlass", 10, new Vector3(0f, 0f, 90f));
                frameGo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------------------------------------------------------------- крыша
        {
            GableRoof(st, "Roof", Vector3.zero, EaveY, Rise, D / 2f + 0.45f, RoofLen, 7, true, O.mossyRoof);
        }

        // ---------------------------------------------------------------- дверь
        {
            var pivot = Obj(st, "DoorPivot", new Vector3(-DoorHalf, FoundH, D / 2f + 0.04f));
            pivot.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
            var dt = pivot.transform;
            Box(dt, "DoorLeaf", new Vector3(DoorHalf, DoorH * 0.5f - 0.02f, 0f), new Vector3(DoorHalf * 2f - 0.04f, DoorH - 0.06f, 0.09f), "WoodLight");
            for (int i = 1; i < 4; i++)
                Box(dt, "DoorGroove", new Vector3(DoorHalf * 2f * i / 4f, DoorH * 0.5f - 0.02f, 0.05f), new Vector3(0.02f, DoorH - 0.1f, 0.015f), "WoodDark");
            Box(dt, "DoorBraceA", new Vector3(DoorHalf, 0.45f, 0.06f), new Vector3(DoorHalf * 2f - 0.12f, 0.12f, 0.04f), "WoodDark");
            Box(dt, "DoorBraceB", new Vector3(DoorHalf, DoorH - 0.5f, 0.06f), new Vector3(DoorHalf * 2f - 0.12f, 0.12f, 0.04f), "WoodDark");
            Box(dt, "HingeA", new Vector3(0.06f, 0.45f, 0.08f), new Vector3(0.22f, 0.07f, 0.04f), "MetalDark");
            Box(dt, "HingeB", new Vector3(0.06f, DoorH - 0.5f, 0.08f), new Vector3(0.22f, 0.07f, 0.04f), "MetalDark");
            Cyl(dt, "DoorKnob", new Vector3(DoorHalf * 2f - 0.14f, DoorH * 0.5f - 0.1f, 0.1f), 0.05f, 0.08f, "Metal", 8, new Vector3(90f, 0f, 0f));
        }

        // ---------------------------------------------------------------- окна
        WinFrame(st, "WindowFront", new Vector3(-1.45f, 1.5f, D / 2f + LogR), 0f, 0.8f, 0.7f, true);
        WinFrame(st, "WindowBack", new Vector3(1.2f, 1.5f, -D / 2f - LogR), 180f, 0.8f, 0.7f, false);
        WinFrame(st, "WindowLeft", new Vector3(-W / 2f - LogR, 1.5f, 0.15f), -90f, 0.8f, 0.7f, true);

        // ---------------------------------------------------------------- интерьер
        {
            var inn = Obj(st, "Interior", Vector3.zero);
            var it = inn.transform;
            Cyl(it, "Rug", new Vector3(0.1f, FoundH + 0.075f, 0.2f), 0.7f, 0.02f, "Red", 12);
            Box(it, "BedFrame", new Vector3(1.55f, FoundH + 0.28f, -1.15f), new Vector3(1.0f, 0.22f, 1.35f), "WoodDark");
            Box(it, "BedMattress", new Vector3(1.55f, FoundH + 0.45f, -1.15f), new Vector3(0.92f, 0.14f, 1.25f), "Cream");
            Blob(it, "Pillow", new Vector3(1.55f, FoundH + 0.58f, -1.6f), new Vector3(0.5f, 0.16f, 0.3f), "Pink", 1);
            Box(it, "TableTop", new Vector3(-1.4f, FoundH + 0.55f, -1.2f), new Vector3(0.9f, 0.07f, 0.6f), "Plank");
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(it, "TableLeg", new Vector3(-1.4f + sx * 0.38f, FoundH + 0.28f, -1.2f + sz * 0.23f), new Vector3(0.07f, 0.5f, 0.07f), "WoodDark");
            Cyl(it, "Mug", new Vector3(-1.2f, FoundH + 0.64f, -1.15f), 0.06f, 0.1f, "Red", 8);
            Box(it, "Stool", new Vector3(-1.4f, FoundH + 0.2f, -0.55f), new Vector3(0.32f, 0.32f, 0.32f), "WoodMid");
        }

        // ---------------------------------------------------------------- труба и дым
        {
            var ch = Obj(st, "Chimney", new Vector3(-W / 2f - 0.4f, 0f, -0.4f));
            var ct = ch.transform;
            Box(ct, "ChimneyBase", new Vector3(0f, 0.2f, 0f), new Vector3(1.05f, 0.4f, 1.05f), "StoneDark");
            for (int i = 0; i < 9; i++)
                Box(ct, "ChimneyBlock", new Vector3(R(-0.02f, 0.02f), 0.58f + i * 0.46f, R(-0.02f, 0.02f)), new Vector3(0.8f + R(-0.03f, 0.03f), 0.44f, 0.8f + R(-0.03f, 0.03f)), (i % 2 == 0) ? "Stone" : "StoneDark", new Vector3(0f, R(-4f, 4f), 0f));
            float topY = 0.58f + 8 * 0.46f + 0.3f;
            Box(ct, "ChimneyCap", new Vector3(0f, topY, 0f), new Vector3(1.0f, 0.14f, 1.0f), "Stone");
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                Box(ct, "ChimneyRim", new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.36f, topY + 0.14f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.36f), new Vector3(0.22f, 0.14f, 0.22f), "StoneDark", new Vector3(0f, a, 0f));
            }
            var sp = Obj(ct, "SmokePoint", new Vector3(0f, topY + 0.4f, 0f));
            smokePoint = sp.transform;
            Blob(ct, "ChimneyMoss", new Vector3(0.1f, topY + 0.1f, 0.2f), new Vector3(0.45f, 0.1f, 0.3f), "Moss");
        }

        // ---------------------------------------------------------------- крыльцо и фонари
        {
            var pc = Obj(st, "Porch", Vector3.zero);
            var pt = pc.transform;
            Box(pt, "PorchDeck", new Vector3(0f, 0.13f, D / 2f + 0.55f), new Vector3(2.5f, 0.26f, 1.0f), "Plank");
            Box(pt, "PorchStep", new Vector3(0f, 0.05f, D / 2f + 1.18f), new Vector3(1.4f, 0.1f, 0.4f), "PlankDark");
            for (int i = 0; i < 4; i++)
                Box(pt, "PorchGap", new Vector3(-0.9f + i * 0.6f, 0.265f, D / 2f + 0.55f), new Vector3(0.02f, 0.01f, 0.95f), "PlankDark");

            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * 1.1f;
                float z = D / 2f + 0.9f;
                Log(pt, "PorchPost", new Vector3(x, 0.26f, z), new Vector3(x, 1.95f, z), 0.08f, "WoodDark");
                Box(pt, "PostCap", new Vector3(x, 1.97f, z), new Vector3(0.2f, 0.06f, 0.2f), "WoodLight");
                Beam(pt, "LampBracket", new Vector3(x, 1.8f, z), new Vector3(x - sx * 0.3f, 1.8f, z), 0.05f, "WoodDark");
                Beam(pt, "LampBraceDiag", new Vector3(x, 1.5f, z), new Vector3(x - sx * 0.3f, 1.8f, z), 0.04f, "WoodDark");
                LanternAt(pt, "Lantern" + (sx < 0 ? "L" : "R"), new Vector3(x - sx * 0.3f, 1.8f, z));
            }
            // цветочный ящик у крыльца и пара бочек
            Box(pt, "PlanterBox", new Vector3(1.75f, 0.2f, D / 2f + 0.5f), new Vector3(0.7f, 0.36f, 0.34f), "PlankDark");
            for (int i = 0; i < 4; i++)
                Flower(pt, new Vector3(1.55f + i * 0.14f, 0.38f, D / 2f + 0.5f + R(-0.06f, 0.06f)), (i % 2 == 0) ? "Pink" : "Lilac");
            Cyl(pt, "RainBarrel", new Vector3(-1.8f, 0.4f, D / 2f + 0.4f), 0.3f, 0.8f, "WoodMid", 10);
            Cyl(pt, "RainBarrelBand", new Vector3(-1.8f, 0.25f, D / 2f + 0.4f), 0.315f, 0.07f, "MetalDark", 10);
            Cyl(pt, "RainBarrelBand", new Vector3(-1.8f, 0.58f, D / 2f + 0.4f), 0.315f, 0.07f, "MetalDark", 10);
            Cyl(pt, "RainBarrelWater", new Vector3(-1.8f, 0.78f, D / 2f + 0.4f), 0.26f, 0.03f, "Water", 10);
        }

        // ---------------------------------------------------------------- вывеска над дверью и флажки на коньке
        {
            var sg = Obj(st, "DoorSign", new Vector3(0f, FoundH + DoorH + 0.58f, D / 2f + LogR + 0.05f));
            var sw = Obj(sg.transform, "SignPivot", Vector3.zero);
            Box(sw.transform, "SignBoard", new Vector3(0f, -0.2f, 0f), new Vector3(0.9f, 0.34f, 0.05f), "Cream");
            Box(sw.transform, "SignFrame", new Vector3(0f, -0.2f, -0.02f), new Vector3(0.98f, 0.42f, 0.04f), "WoodMid");
            Part(sw.transform, "SignSaw", HutMeshLib.SawBlade(14), Mat("Metal"), new Vector3(0f, -0.2f, 0.04f), new Vector3(0.3f, 0.3f, 0.02f), Quaternion.identity);
            Rod(sw.transform, "SignChainL", new Vector3(-0.35f, -0.04f, 0f), new Vector3(-0.35f, 0.08f, 0f), 0.012f, "Rope", 4);
            Rod(sw.transform, "SignChainR", new Vector3(0.35f, -0.04f, 0f), new Vector3(0.35f, 0.08f, 0f), 0.012f, "Rope", 4);
            Beam(sg.transform, "SignBar", new Vector3(-0.5f, 0.08f, 0f), new Vector3(0.5f, 0.08f, 0f), 0.05f, "WoodDark");
            swingers.Add(sw.transform);

            // вымпелы на концах конька
            float ry = EaveY + Rise + 0.07f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * (RoofLen * 0.5f + 0.1f);
                Rod(st, "RidgePole", new Vector3(x, ry, 0f), new Vector3(x, ry + 0.9f, 0f), 0.03f, "WoodLight", 5);
                cloths.Add(Cloth(st, "RidgeFlag", new Vector3(x + sx * 0.03f, ry + 0.86f, 0f), 0.32f, 0.15f, 4, sx < 0 ? "Pink" : "Lilac", 18f, sx * 1.3f, "Red"));
            }
        }

        // ---------------------------------------------------------------- дрова, дорожка, мелочи
        {
            LogPile(st, "FirewoodPile", new Vector3(0.9f, 0f, -D / 2f - 0.75f), 0f, new[] { 4, 3, 2 }, 1.9f, 0.14f);
            Box(st, "FirewoodRoof", new Vector3(0.9f - 0.0f, 0.78f, -D / 2f - 0.75f), new Vector3(2.1f, 0.06f, 0.95f), "PlankDark", new Vector3(0f, 0f, 0f));

            // дорожка из плоских камней от ворот к крыльцу
            Vector3 a = new Vector3(GateX, 0f, FenceZ1 - 0.2f);
            Vector3 b = new Vector3(0.4f, 0f, D / 2f + 1.5f);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                Vector3 p = Vector3.Lerp(a, b, t) + new Vector3(R(-0.15f, 0.15f), 0.04f + O.groundY, R(-0.12f, 0.12f));
                var go = Cyl(st, "PathStone", p, R(0.26f, 0.34f), 0.07f, R() < 0.3f ? "StoneDark" : "Stone", 7, new Vector3(0f, R(0f, 60f), 0f));
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------------------------------------------------------------- пилорама
        SawMachine(st, root);

        PolishStage3(st);
        EndStage(s, 3);
        return s.transform;
    }

    // ---------------------------------------------------------------- окно: рама, стекло (HutWindowGlass), ставни, подоконник
    // pos: на наружной поверхности стены; yaw: куда смотрит наружная нормаль (0 = +Z, 90 = +X, 180 = -Z, -90 = -X)
    static void WinFrame(Transform p, string name, Vector3 pos, float yaw, float w, float h, bool shutters)
    {
        var win = Obj(p, name, pos);
        win.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = win.transform;
        Box(t, "HutWindowGlass", new Vector3(0f, 0f, 0.03f), new Vector3(w, h, 0.04f), "HutWindowGlass");
        float fb = 0.07f;
        Box(t, "FrameTop", new Vector3(0f, h * 0.5f + fb * 0.5f, 0.05f), new Vector3(w + fb * 2f, fb, 0.07f), "WoodDark");
        Box(t, "FrameBottom", new Vector3(0f, -h * 0.5f - fb * 0.5f, 0.05f), new Vector3(w + fb * 2f, fb, 0.07f), "WoodDark");
        Box(t, "FrameL", new Vector3(-w * 0.5f - fb * 0.5f, 0f, 0.05f), new Vector3(fb, h, 0.07f), "WoodDark");
        Box(t, "FrameR", new Vector3(w * 0.5f + fb * 0.5f, 0f, 0.05f), new Vector3(fb, h, 0.07f), "WoodDark");
        Box(t, "MullionV", new Vector3(0f, 0f, 0.06f), new Vector3(0.04f, h, 0.04f), "WoodDark");
        Box(t, "MullionH", new Vector3(0f, 0f, 0.06f), new Vector3(w, 0.04f, 0.04f), "WoodDark");
        Box(t, "Sill", new Vector3(0f, -h * 0.5f - fb - 0.03f, 0.12f), new Vector3(w + 0.3f, 0.06f, 0.2f), "WoodLight");
        if (shutters)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                var sh = Obj(t, "ShutterPivot", new Vector3(sx * (w * 0.5f + fb), 0f, 0.06f));
                sh.transform.localRotation = Quaternion.Euler(0f, sx * 62f, 0f);
                Box(sh.transform, "Shutter", new Vector3(sx * w * 0.25f, 0f, 0f), new Vector3(w * 0.5f, h + 0.04f, 0.04f), "Red");
                Box(sh.transform, "ShutterSlat", new Vector3(sx * w * 0.25f, 0f, 0.025f), new Vector3(w * 0.38f, 0.04f, 0.02f), "Cream");
            }
        }
    }

    // ---------------------------------------------------------------- фонарь на цепочке: качается (swingers) и светит (LanternLight)
    static void LanternAt(Transform p, string name, Vector3 hangPoint)
    {
        var pivot = Obj(p, name + "Pivot", hangPoint);
        var t = pivot.transform;
        Rod(t, "Chain", Vector3.zero, new Vector3(0f, -0.14f, 0f), 0.012f, "MetalDark", 4);
        Box(t, "LampCapTop", new Vector3(0f, -0.2f, 0f), new Vector3(0.22f, 0.05f, 0.22f), "MetalDark");
        Box(t, "HutLamp", new Vector3(0f, -0.33f, 0f), new Vector3(0.16f, 0.2f, 0.16f), "HutLamp");
        Box(t, "LampBase", new Vector3(0f, -0.46f, 0f), new Vector3(0.2f, 0.05f, 0.2f), "MetalDark");
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(t, "LampBar", new Vector3(sx * 0.08f, -0.33f, sz * 0.08f), new Vector3(0.025f, 0.22f, 0.025f), "MetalDark");

        var lgo = new GameObject("LanternLight_" + name);
        lgo.transform.SetParent(t, false);
        lgo.transform.localPosition = new Vector3(0f, -0.33f, 0f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.8f, 0.45f);
        l.range = 6f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
        swingers.Add(t);
    }

    // ---------------------------------------------------------------- рабочая пилорама под SawmillAnimator
    static void SawMachine(Transform st, Transform hutRoot)
    {
        var sm = Obj(st, "Sawmill", Vector3.zero);
        var t = sm.transform;

        var anim = sm.AddComponent<SawmillAnimator>();
        var ws = hutRoot.GetComponentInParent<PlankWorkshop>(true);
        if (ws == null) ws = hutRoot.GetComponentInChildren<PlankWorkshop>(true);
        anim.workshop = ws;

        // столешница: две доски с щелью под диск
        for (int sz = -1; sz <= 1; sz += 2)
            Box(t, "TableTop", new Vector3(6.7f, TableY - 0.05f, SawZ + sz * 0.42f), new Vector3(4.7f, 0.1f, 0.68f), sz < 0 ? "Plank" : "PlankDark");
        for (int sz = -1; sz <= 1; sz += 2)
            Beam(t, "TableGuide", new Vector3(4.4f, TableY + 0.04f, SawZ + sz * 0.8f), new Vector3(9.0f, TableY + 0.04f, SawZ + sz * 0.8f), 0.07f, "MetalDark");

        // диск и ось
        var blade = Part(t, "Blade", HutMeshLib.SawBlade(22), Mat("Metal"), new Vector3(BladeX, BladeY, SawZ), new Vector3(BladeScale, BladeScale, 0.05f), Quaternion.identity);
        anim.blade = blade.transform;
        Rod(t, "BladeAxle", new Vector3(BladeX, BladeY, PulleyZ - 0.12f), new Vector3(BladeX, BladeY, SawZ + 1.1f), 0.05f, "Metal", 6);
        Part(t, "BladeHub", HutMeshLib.Cyl(8), Mat("MetalDark"), new Vector3(BladeX, BladeY, SawZ), new Vector3(0.22f, 0.12f, 0.22f), Quaternion.Euler(90f, 0f, 0f));
        // защитный кожух над диском
        Box(t, "BladeHood", new Vector3(BladeX, BladeY + BladeScale * 0.5f + 0.02f, SawZ), new Vector3(1.1f, 0.06f, 0.26f), "Red");

        // водяное колесо и ось
        var wheel = Part(t, "WaterWheel", HutMeshLib.Paddle(10), Mat("WoodMid"), new Vector3(WheelX, WheelY, WheelZ), new Vector3(2.0f, 2.0f, 0.55f), Quaternion.identity);
        anim.wheel = wheel.transform;
        Rod(t, "WheelAxle", new Vector3(WheelX, WheelY, WheelZ - 0.85f), new Vector3(WheelX, WheelY, PulleyZ - 0.1f), 0.06f, "Metal", 6);
        var water = Box(t, "FlumeWater", new Vector3(WheelX, 0.1f, WheelZ), new Vector3(3.0f, 0.12f, 1.3f), "Water");
        water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        // подводящий жёлоб
        Beam(t, "FlumeIn", new Vector3(WheelX + 3.0f, 2.35f, WheelZ), new Vector3(WheelX + 0.1f, 2.0f, WheelZ), 0.34f, "PlankDark");
        Beam(t, "FlumeInWater", new Vector3(WheelX + 3.0f, 2.5f, WheelZ), new Vector3(WheelX + 0.1f, 2.15f, WheelZ), 0.2f, "Water");
        Beam(t, "FlumePostA", new Vector3(WheelX + 2.2f, 0f, WheelZ), new Vector3(WheelX + 2.2f, 2.2f, WheelZ), 0.14f, "WoodDark");

        // шкивы и ремень (в плоскости PulleyZ): малый на оси диска, большой на оси колеса
        float rA = 0.24f, rB = 0.42f;
        var pA = Obj(t, "PulleyA", new Vector3(BladeX, BladeY, PulleyZ));
        var pB = Obj(t, "PulleyB", new Vector3(WheelX, WheelY, PulleyZ));
        Part(pA.transform, "Disc", HutMeshLib.Cyl(10), Mat("WoodDark"), Vector3.zero, new Vector3(rA * 2f, 0.14f, rA * 2f), Quaternion.Euler(90f, 0f, 0f));
        Box(pA.transform, "Mark", new Vector3(0f, rA * 0.55f, -0.08f), new Vector3(0.05f, rA * 0.8f, 0.03f), "Red");
        Part(pB.transform, "Disc", HutMeshLib.Cyl(12), Mat("WoodDark"), Vector3.zero, new Vector3(rB * 2f, 0.14f, rB * 2f), Quaternion.Euler(90f, 0f, 0f));
        Box(pB.transform, "Mark", new Vector3(0f, rB * 0.55f, -0.08f), new Vector3(0.05f, rB * 0.8f, 0.03f), "Red");
        anim.pulleys = new[] { pA.transform, pB.transform };
        {
            Vector3 ca = new Vector3(BladeX, BladeY, PulleyZ), cb = new Vector3(WheelX, WheelY, PulleyZ);
            Vector3 dir = (cb - ca).normalized;
            Vector3 n = new Vector3(-dir.y, dir.x, 0f);
            Rod(t, "BeltTop", ca + n * rA, cb + n * rB, 0.02f, "MetalDark", 4);
            Rod(t, "BeltBottom", ca - n * rA, cb - n * rB, 0.02f, "MetalDark", 4);
        }

        // каретка: салазки, упоры и бревно, едет вдоль X
        var car = Obj(t, "Carriage", new Vector3(4.8f, TableY, SawZ));
        var ct = car.transform;
        Box(ct, "Sled", new Vector3(0f, 0.07f, 0f), new Vector3(2.4f, 0.1f, 1.0f), "Plank");
        Box(ct, "SledRunnerF", new Vector3(0f, 0.03f, 0.5f), new Vector3(2.4f, 0.06f, 0.08f), "WoodDark");
        Box(ct, "SledRunnerB", new Vector3(0f, 0.03f, -0.5f), new Vector3(2.4f, 0.06f, 0.08f), "WoodDark");
        for (int sx = -1; sx <= 1; sx += 2)
        {
            Box(ct, "Cradle", new Vector3(sx * 0.95f, 0.26f, 0f), new Vector3(0.12f, 0.3f, 0.7f), "WoodDark");
            Box(ct, "CradleClamp", new Vector3(sx * 0.95f, 0.5f, 0f), new Vector3(0.1f, 0.08f, 0.5f), "MetalDark");
        }
        Log(ct, "CarriageLog", new Vector3(-1.1f, 0.4f, 0f), new Vector3(1.1f, 0.4f, 0f), 0.25f, "WoodLight");
        // рукоятка для рабочего: две стойки от салазок и поперечина на уровне его рук (HutSawyer ведёт её вместе с кареткой)
        for (int sx = -1; sx <= 1; sx += 2)
            Beam(ct, "PushArm", new Vector3(sx * 0.42f, 0.08f, 0.5f), new Vector3(sx * 0.42f, 0.15f, 0.82f), 0.06f, "WoodDark");
        Log(ct, "PushBar", new Vector3(-0.62f, 0.15f, 0.82f), new Vector3(0.62f, 0.15f, 0.82f), 0.04f, "WoodLight");
        anim.carriage = ct;
        anim.carriageStart = new Vector3(4.8f, TableY, SawZ);
        anim.carriageEnd = new Vector3(8.3f, TableY, SawZ);

        // сырьё у начала рельсов (видно, сколько досок в работе)
        var q0 = Log(t, "QueueLog0", new Vector3(3.95f, 0.3f, SawZ - 1.6f), new Vector3(5.95f, 0.3f, SawZ - 1.6f), 0.17f, "WoodMid");
        var q1 = Log(t, "QueueLog1", new Vector3(3.95f, 0.3f, SawZ - 1.25f), new Vector3(5.95f, 0.3f, SawZ - 1.25f), 0.17f, "WoodLight");
        var q2 = Log(t, "QueueLog2", new Vector3(3.95f, 0.6f, SawZ - 1.43f), new Vector3(5.95f, 0.6f, SawZ - 1.43f), 0.17f, "WoodMid");
        anim.queueLogs = new[] { q0, q1, q2 };

        // склад готовых досок на земле перед настилом (у точки E): каждая готовая доска лежит отдельно, ровно столько, сколько готово.
        // Стопки по 6 (2 слоя по 3) на поддонах; рабочий кладёт очередную доску в её ячейку (HutSawyer)
        {
            int slotsN = GameBalance.PlankOutputCap + GameBalance.SkillHoardOutput;
            int piles = (slotsN + 5) / 6;
            var slots = new GameObject[slotsN];
            var depot = Obj(t, "ReadyPlanks", Vector3.zero);
            for (int pl = 0; pl < piles; pl++)
            {
                var pile = Obj(depot.transform, "PlankPile" + pl, new Vector3(4.6f + pl * 1.0f, 0.1f + O.groundY, SawZ + 2.45f));
                pile.transform.localRotation = Quaternion.Euler(0f, pl % 2 == 0 ? 4f : -5f, 0f);
                for (int k = -1; k <= 1; k += 2)
                    Box(pile.transform, "Skid", new Vector3(0f, -0.05f, k * 0.25f), new Vector3(0.95f, 0.1f, 0.09f), "WoodDark");
                for (int j = 0; j < 6; j++)
                {
                    int idx = pl * 6 + j;
                    if (idx >= slotsN) break;
                    int col = j % 3, layer = j / 3;
                    slots[idx] = Box(pile.transform, "ReadyPlank" + idx,
                        new Vector3(R(-0.015f, 0.015f), 0.0275f + layer * 0.052f, (col - 1) * 0.225f),
                        new Vector3(0.8f, 0.045f, 0.2f), ((col + layer) % 2 == 0) ? "Plank" : "PlankDark", new Vector3(0f, R(-1.2f, 1.2f), 0f));
                    slots[idx].SetActive(false);
                }
            }
            anim.readySlots = slots;
        }

        // опилки: у точки реза
        var sd = Obj(t, "SawdustPoint", new Vector3(BladeX, 1.15f, SawZ));
        anim.sawdustPoint = sd.transform;

        // вывеска на столбе у края настила: качается
        {
            Rod(t, "SignPole", new Vector3(SawX0 + 0.05f, 0.13f, SawZ + 1.95f), new Vector3(SawX0 + 0.05f, 2.5f, SawZ + 1.95f), 0.07f, "WoodDark", 6);
            Beam(t, "SignArm", new Vector3(SawX0 + 0.05f, 2.4f, SawZ + 1.95f), new Vector3(SawX0 + 0.95f, 2.4f, SawZ + 1.95f), 0.06f, "WoodDark");
            var hang = Obj(t, "SawSignPivot", new Vector3(SawX0 + 0.8f, 2.4f, SawZ + 1.95f));
            var ht = hang.transform;
            Box(ht, "SawSignBoard", new Vector3(0f, -0.38f, 0f), new Vector3(0.8f, 0.5f, 0.05f), "Cream");
            Box(ht, "SawSignFrame", new Vector3(0f, -0.38f, -0.02f), new Vector3(0.88f, 0.58f, 0.04f), "WoodMid");
            Part(ht, "SawSignBlade", HutMeshLib.SawBlade(14), Mat("Metal"), new Vector3(0f, -0.38f, 0.04f), new Vector3(0.4f, 0.4f, 0.02f), Quaternion.identity);
            Rod(ht, "SawSignChainL", new Vector3(-0.3f, -0.12f, 0f), new Vector3(-0.3f, 0f, 0f), 0.012f, "Rope", 4);
            Rod(ht, "SawSignChainR", new Vector3(0.3f, -0.12f, 0f), new Vector3(0.3f, 0f, 0f), 0.012f, "Rope", 4);
            swingers.Add(ht);
            cloths.Add(Cloth(t, "SawFlag", new Vector3(BladeX, 2.8f, SawZ + 0.95f), 0.3f, 0.15f, 4, "Red", 18f, 0.4f, "Red"));
        }

        // рабочий-пильщик: козлы у хижины, пилит бревно, при заказе идёт к каретке (код: Scripts/Build/Hut/HutSawyer.cs)
        BuildHutSawyer(t, anim);

        // лампа на арке, чтобы пилорама светилась вечером
        LanternAt(t, "SawLantern", new Vector3(BladeX + 0.3f, 2.6f, SawZ + 1.0f));
    }
}
#endif

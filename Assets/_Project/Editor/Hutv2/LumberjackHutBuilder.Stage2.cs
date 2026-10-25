// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Stage2.cs   (ТОЛЬКО в папке Editor!)
// СТАДИЯ 2 = «каркас»: стены хижины до пятого венца, дверная рама, угловые столбы, стропила и конёк без кровли,
// леса у фасада (уходят на стадии 3), скелет пилорамы: ноги и обвязка стола, арка под диск, опоры колеса, каменный лоток.
// Стадии накапливаются (Stage1 остаётся), поэтому здесь только то, что добавляется к нулевой стадии.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class LumberjackHutBuilder
{
    // габариты пилорамы (общие для стадий 2 и 3, все в локальных метрах стадии)
    const float TableY = 0.85f;                 // верх стола
    const float BladeX = 7.0f, BladeY = 1.9f;   // ось диска
    const float BladeScale = 1.9f;              // диаметр диска
    const float PulleyZ = SawZ - 1.55f;         // плоскость ремня
    const float WheelX = 8.4f, WheelY = 1.15f, WheelZ = SawZ - 2.45f;
    const float DoorHalf = 0.5f;                // полуширина дверного проёма
    const float DoorH = 1.85f;                  // высота проёма от основания

    static Transform BuildStage2(Transform root)
    {
        var s = BeginStage(root, "Stage2");
        var st = s.transform;

        // ---------------------------------------------------------------- стены: первые пять венцов
        {
            var walls = Obj(st, "Walls", Vector3.zero);
            for (int c = 0; c < CoursesStage2; c++) WallCourse(walls.transform, c);

            // внутренний пол и коврик (видны через дверь)
            Box(walls.transform, "Floor", new Vector3(0f, FoundH + 0.03f, 0f), new Vector3(W - 0.2f, 0.06f, D - 0.2f), "Plank");
            for (int i = -2; i <= 2; i++)
                Box(walls.transform, "FloorGap", new Vector3(i * 0.8f, FoundH + 0.065f, 0f), new Vector3(0.02f, 0.01f, D - 0.25f), "PlankDark");

            // невидимая коллизия по периметру хижины
            Solid(walls.transform, "HutCollider", new Vector3(0f, 1.3f, 0f), new Vector3(W + 0.5f, 2.6f, D + 0.5f));
        }

        // ---------------------------------------------------------------- дверная рама и угловые столбы
        {
            var fr = Obj(st, "Frame", Vector3.zero);
            var ft = fr.transform;
            float doorTop = FoundH + DoorH;
            Box(ft, "DoorPostL", new Vector3(-DoorHalf - 0.08f, FoundH + DoorH * 0.5f, D / 2f), new Vector3(0.16f, DoorH, 0.42f), "WoodDark");
            Box(ft, "DoorPostR", new Vector3(DoorHalf + 0.08f, FoundH + DoorH * 0.5f, D / 2f), new Vector3(0.16f, DoorH, 0.42f), "WoodDark");

            float ph = EaveY - FoundH;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(ft, "CornerPost", new Vector3(sx * (W / 2f), FoundH + ph * 0.5f, sz * (D / 2f)), new Vector3(0.2f, ph, 0.2f), "WoodDark", new Vector3(0f, R(-2f, 2f), 0f));

            // верхняя обвязка (мауэрлаты) вдоль X
            for (int sz = -1; sz <= 1; sz += 2)
                Beam(ft, "TopPlate", new Vector3(-W / 2f - 0.3f, EaveY - 0.05f, sz * D / 2f), new Vector3(W / 2f + 0.3f, EaveY - 0.05f, sz * D / 2f), 0.17f, "WoodDark");

            // стропильные фермы: три штуки (по краям и по центру)
            float[] xs = { -W / 2f - 0.1f, 0f, W / 2f + 0.1f };
            float halfSpan = D / 2f + 0.45f;
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i];
                Beam(ft, "TieBeam", new Vector3(x, EaveY - 0.05f, -D / 2f), new Vector3(x, EaveY - 0.05f, D / 2f), 0.15f, "WoodDark");
                Beam(ft, "KingPost", new Vector3(x, EaveY - 0.05f, 0f), new Vector3(x, EaveY + Rise - 0.05f, 0f), 0.11f, "WoodMid");
                Beam(ft, "RafterF", new Vector3(x, EaveY - 0.1f, halfSpan), new Vector3(x, EaveY + Rise - 0.05f, 0f), 0.13f, "WoodMid");
                Beam(ft, "RafterB", new Vector3(x, EaveY - 0.1f, -halfSpan), new Vector3(x, EaveY + Rise - 0.05f, 0f), 0.13f, "WoodMid");
            }
            // конёк и две прогонные жерди на каждый скат
            Beam(ft, "RidgeBeam", new Vector3(-W / 2f - 0.4f, EaveY + Rise - 0.06f, 0f), new Vector3(W / 2f + 0.4f, EaveY + Rise - 0.06f, 0f), 0.15f, "WoodDark");
            for (int sz = -1; sz <= 1; sz += 2)
                for (int k = 1; k <= 2; k++)
                {
                    float t = k / 3f;
                    float z = Mathf.Lerp(halfSpan, 0f, t) * sz;
                    float y = Mathf.Lerp(EaveY - 0.1f, EaveY + Rise - 0.05f, t) + 0.06f;
                    Beam(ft, "Purlin", new Vector3(-W / 2f - 0.3f, y, z), new Vector3(W / 2f + 0.3f, y, z), 0.09f, "WoodLight");
                }

            // красный вымпел на коньке: стройка идёт
            Rod(ft, "PennantPole", new Vector3(W / 2f + 0.4f, EaveY + Rise - 0.06f, 0f), new Vector3(W / 2f + 0.4f, EaveY + Rise + 0.85f, 0f), 0.03f, "WoodLight", 5);
            cloths.Add(Cloth(ft, "PennantRed", new Vector3(W / 2f + 0.43f, EaveY + Rise + 0.8f, 0f), 0.3f, 0.14f, 4, "Red", 18f, 0.9f, "Red"));
        }

        // ---------------------------------------------------------------- леса у фасада (уходят на стадии 3)
        {
            var sc = Obj(st, "Scaffold", Vector3.zero);
            var vis = sc.AddComponent<StageVisibility>();
            vis.minStage = 2;
            vis.maxStage = 2;
            vis.exitTime = 1.2f;
            pendingFlyTarget.Add(vis);

            var t = sc.transform;
            float z = D / 2f + 0.62f;
            float x0 = -2.9f, x1 = -1.0f;
            float top = 2.9f;
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? x0 : x1;
                Rod(t, "ScaffoldPole", new Vector3(x, 0f, z), new Vector3(x, top, z), 0.05f, "WoodLight", 6);
                Rod(t, "ScaffoldPoleBack", new Vector3(x, 0f, z - 0.55f), new Vector3(x, top - 0.4f, z - 0.55f), 0.045f, "WoodLight", 6);
                Beam(t, "ScaffoldTie", new Vector3(x, 1.0f, z), new Vector3(x, 1.0f, z - 0.55f), 0.05f, "Rope");
                Beam(t, "ScaffoldTie", new Vector3(x, 1.9f, z), new Vector3(x, 1.9f, z - 0.55f), 0.05f, "Rope");
            }
            Beam(t, "ScaffoldBrace", new Vector3(x0, 0.1f, z), new Vector3(x1, 1.9f, z), 0.05f, "WoodLight");
            Beam(t, "ScaffoldBrace", new Vector3(x1, 0.1f, z), new Vector3(x0, 1.9f, z), 0.05f, "WoodLight");
            foreach (float py in new[] { 1.0f, 1.9f })
            {
                for (int k = 0; k < 3; k++)
                    Box(t, "ScaffoldBoard", new Vector3((x0 + x1) * 0.5f, py + 0.04f, z - 0.12f - k * 0.17f), new Vector3(x1 - x0 + 0.4f, 0.05f, 0.16f), k % 2 == 0 ? "Plank" : "PlankDark");
            }
            // приставная лесенка к нижнему настилу
            Beam(t, "ScaffoldLadderL", new Vector3(x1 + 0.7f, 0f, z + 0.45f), new Vector3(x1 + 0.2f, 1.0f, z + 0.05f), 0.07f, "WoodMid");
            Beam(t, "ScaffoldLadderR", new Vector3(x1 + 1.1f, 0f, z + 0.45f), new Vector3(x1 + 0.6f, 1.0f, z + 0.05f), 0.07f, "WoodMid");
            for (int k = 1; k <= 3; k++)
            {
                float u = k / 4f;
                Beam(t, "ScaffoldRung", Vector3.Lerp(new Vector3(x1 + 0.7f, 0f, z + 0.45f), new Vector3(x1 + 0.2f, 1.0f, z + 0.05f), u),
                    Vector3.Lerp(new Vector3(x1 + 1.1f, 0f, z + 0.45f), new Vector3(x1 + 0.6f, 1.0f, z + 0.05f), u), 0.05f, "WoodLight");
            }
            // ведро и молоток на настиле
            Cyl(t, "Bucket", new Vector3(x0 + 0.4f, 1.0f + 0.2f, z - 0.2f), 0.14f, 0.3f, "MetalDark", 8);
            Box(t, "ScaffoldHammerHead", new Vector3(x1 - 0.2f, 1.93f + 0.04f, z - 0.3f), new Vector3(0.14f, 0.07f, 0.07f), "MetalDark");
        }

        // ---------------------------------------------------------------- скелет пилорамы
        SawSkeleton(st);

        PolishStage2(st);
        EndStage(s, 2);
        return s.transform;
    }

    // ---------------------------------------------------------------- один венец стен
    // чётные венцы: фасад и тыл длиннее (выступают углами), нечётные: выступают боковые. Над дверным проёмом фасад целый.
    static void WallCourse(Transform p, int c)
    {
        float y = YC(c);
        bool even = (c % 2) == 0;
        float ex = 0.24f;
        float xa = even ? W / 2f + ex : W / 2f;
        float za = even ? D / 2f : D / 2f + ex;
        bool door = (y + LogR) <= (FoundH + DoorH + 0.02f);
        string wood = R() < 0.3f ? "WoodLight" : "WoodMid";

        Log(p, "Wall_B" + c, new Vector3(-xa, y, -D / 2f), new Vector3(xa, y, -D / 2f), LogR * R(0.96f, 1.04f), wood);
        wood = R() < 0.3f ? "WoodLight" : "WoodMid";
        if (!door)
        {
            Log(p, "Wall_F" + c, new Vector3(-xa, y, D / 2f), new Vector3(xa, y, D / 2f), LogR * R(0.96f, 1.04f), wood);
        }
        else
        {
            float g = DoorHalf + 0.07f;
            Log(p, "Wall_FL" + c, new Vector3(-xa, y, D / 2f), new Vector3(-g, y, D / 2f), LogR * R(0.96f, 1.04f), wood);
            Log(p, "Wall_FR" + c, new Vector3(g, y, D / 2f), new Vector3(xa, y, D / 2f), LogR * R(0.96f, 1.04f), wood);
        }
        wood = R() < 0.3f ? "WoodLight" : "WoodMid";
        Log(p, "Wall_L" + c, new Vector3(-W / 2f, y, -za), new Vector3(-W / 2f, y, za), LogR * R(0.96f, 1.04f), wood);
        wood = R() < 0.3f ? "WoodLight" : "WoodMid";
        Log(p, "Wall_R" + c, new Vector3(W / 2f, y, -za), new Vector3(W / 2f, y, za), LogR * R(0.96f, 1.04f), wood);
    }

    // ---------------------------------------------------------------- скелет пилорамы (остаётся и на стадии 3)
    static void SawSkeleton(Transform st)
    {
        var sk = Obj(st, "SawSkeleton", Vector3.zero);
        var t = sk.transform;
        float deckTop = 0.13f;

        // ноги и обвязка стола: четыре пары ног вдоль X, две продольные балки, поперечины
        float[] lx = { 4.5f, 6.0f, 7.5f, 8.9f };
        for (int i = 0; i < lx.Length; i++)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(t, "TableLeg", new Vector3(lx[i], (deckTop + TableY - 0.1f) * 0.5f, SawZ + sz * 0.72f), new Vector3(0.16f, TableY - 0.1f - deckTop, 0.16f), "WoodDark");
        for (int sz = -1; sz <= 1; sz += 2)
            Beam(t, "TableStringer", new Vector3(4.4f, TableY - 0.14f, SawZ + sz * 0.72f), new Vector3(9.0f, TableY - 0.14f, SawZ + sz * 0.72f), 0.15f, "WoodMid");
        for (int i = 0; i < lx.Length; i++)
            Beam(t, "TableCross", new Vector3(lx[i], TableY - 0.2f, SawZ - 0.72f), new Vector3(lx[i], TableY - 0.2f, SawZ + 0.72f), 0.12f, "WoodDark");

        // арка диска: две стойки по бокам стола и верхняя балка, подшипниковые колодки на высоте оси
        for (int sz = -1; sz <= 1; sz += 2)
        {
            float z = SawZ + sz * 1.0f;
            Box(t, "ArchPost", new Vector3(BladeX, 1.4f, z), new Vector3(0.24f, 2.55f, 0.24f), "WoodDark");
            Box(t, "ArchBearing", new Vector3(BladeX, BladeY, z), new Vector3(0.34f, 0.34f, 0.3f), "MetalDark");
            Beam(t, "ArchBrace", new Vector3(BladeX - 0.9f, 0.13f, z), new Vector3(BladeX, 1.4f, z), 0.1f, "WoodMid");
            Beam(t, "ArchBrace", new Vector3(BladeX + 0.9f, 0.13f, z), new Vector3(BladeX, 1.4f, z), 0.1f, "WoodMid");
        }
        Beam(t, "ArchTop", new Vector3(BladeX, 2.72f, SawZ - 1.0f), new Vector3(BladeX, 2.72f, SawZ + 1.0f), 0.2f, "WoodMid");

        // опоры водяного колеса и каменный лоток под ним
        for (int sz = 0; sz < 2; sz++)
        {
            float z = sz == 0 ? WheelZ - 0.6f : WheelZ + 0.6f;
            Box(t, "WheelPost", new Vector3(WheelX, (WheelY + 0.2f) * 0.5f, z), new Vector3(0.22f, WheelY + 0.2f, 0.22f), "WoodDark");
            Box(t, "WheelBearing", new Vector3(WheelX, WheelY, z), new Vector3(0.3f, 0.3f, 0.26f), "MetalDark");
            Beam(t, "WheelBrace", new Vector3(WheelX - 0.8f, 0f, z), new Vector3(WheelX, WheelY, z), 0.09f, "WoodMid");
            Beam(t, "WheelBrace", new Vector3(WheelX + 0.8f, 0f, z), new Vector3(WheelX, WheelY, z), 0.09f, "WoodMid");
        }
        float cz = WheelZ;
        Box(t, "FlumeRimF", new Vector3(WheelX, 0.14f, cz + 0.68f), new Vector3(3.2f, 0.26f, 0.2f), "Stone");
        Box(t, "FlumeRimB", new Vector3(WheelX, 0.14f, cz - 0.68f), new Vector3(3.2f, 0.26f, 0.2f), "Stone");
        Box(t, "FlumeRimL", new Vector3(WheelX - 1.6f, 0.14f, cz), new Vector3(0.2f, 0.26f, 1.5f), "StoneDark");
        Box(t, "FlumeRimR", new Vector3(WheelX + 1.6f, 0.14f, cz), new Vector3(0.2f, 0.26f, 1.5f), "StoneDark");
        Rock(t, new Vector3(WheelX - 1.9f, 0f, cz + 0.8f), 0.45f);
        Rock(t, new Vector3(WheelX + 1.9f, 0f, cz - 0.7f), 0.4f, "Stone");

        // колышки с красной лентой: стройка ещё идёт
        Vector3[] pegs = { new Vector3(SawX0 - 0.1f, 0f, SawZ + 1.95f), new Vector3(SawX1 + 0.1f, 0f, SawZ + 1.95f) };
        for (int i = 0; i < pegs.Length; i++)
        {
            Beam(t, "Peg", pegs[i], pegs[i] + Vector3.up * 0.8f, 0.07f, "WoodLight");
            cloths.Add(Cloth(t, "PegFlag", pegs[i] + Vector3.up * 0.78f + new Vector3(0.05f, 0f, 0f), 0.12f, 0.12f, 3, "Red", 20f, i * 2.1f));
        }
        Rod(t, "PegRope", pegs[0] + Vector3.up * 0.65f, pegs[1] + Vector3.up * 0.65f, 0.016f, "Red", 4);
    }
}
#endif

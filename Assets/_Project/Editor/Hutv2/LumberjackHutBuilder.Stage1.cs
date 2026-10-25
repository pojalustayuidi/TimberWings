// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Stage1.cs   (ТОЛЬКО в папке Editor!)
// СТАДИЯ 1 = «нулевая»: старую хижину разобрали, двор обнесён верёвочным забором с воротами.
//
// Остаётся на всех стадиях (то, что строится дальше): земля двора, каменный фундамент хижины, настил под пилораму,
//                       забор с воротами и лентами, цветы/камни/мох.
// Уходит в стройку (группа «Debris», StageVisibility 1..1): разобранные материалы, разложенные по кучам
//                       (брёвна, доски, пучки кровельных досок, балки, бочки, ящики, тачка, козлы, верстак, чертёж на мольберте,
//                       колышки с верёвкой на месте пилорамы, табличка «стройка»).
// Пила лежит у ящика до стадии 3 (группа «LooseBlade», 1..2): потом её «ставят» на место.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class LumberjackHutBuilder
{
    static Transform BuildStage1(Transform root)
    {
        var s = BeginStage(root, "Stage1");
        var st = s.transform;

        // ---------------------------------------------------------------- земля двора и фундаменты (остаются)
        {
            var yard = Part(st, "YardGround", HutMeshLib.Cyl(16), Mat("Dirt"), new Vector3(2.6f, 0.025f + O.groundY, 0.1f), new Vector3(17.2f, 0.05f, 10.6f), Quaternion.identity);
            yard.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // каменное основание хижины + неровные блоки по кромке
            Box(st, "HutFoundation", new Vector3(0f, FoundH * 0.5f, 0f), new Vector3(W + 0.5f, FoundH, D + 0.5f), "Stone");
            for (int i = 0; i < 14; i++)
            {
                float t = i / 14f * 4f;
                Vector3 p;
                if (t < 1f) p = new Vector3(Mathf.Lerp(-W / 2 - 0.25f, W / 2 + 0.25f, t), 0f, D / 2 + 0.28f);
                else if (t < 2f) p = new Vector3(W / 2 + 0.28f, 0f, Mathf.Lerp(D / 2 + 0.25f, -D / 2 - 0.25f, t - 1f));
                else if (t < 3f) p = new Vector3(Mathf.Lerp(W / 2 + 0.25f, -W / 2 - 0.25f, t - 2f), 0f, -D / 2 - 0.28f);
                else p = new Vector3(-W / 2 - 0.28f, 0f, Mathf.Lerp(-D / 2 - 0.25f, D / 2 + 0.25f, t - 3f));
                float sz = R(0.34f, 0.52f);
                Box(st, "FoundBlock", p + new Vector3(0f, sz * 0.32f, 0f), new Vector3(sz, sz * 0.62f, sz * R(0.8f, 1.1f)), R() < 0.3f ? "StoneDark" : "Stone", new Vector3(0f, R(-12f, 12f), 0f));
            }
            // нижний венец (лежни) по периметру: от старой хижины остались только они
            float sy = FoundH + 0.09f;
            Log(st, "Sill_F", new Vector3(-W / 2, sy, D / 2), new Vector3(W / 2, sy, D / 2), 0.1f, "WoodDark");
            Log(st, "Sill_B", new Vector3(-W / 2, sy, -D / 2), new Vector3(W / 2, sy, -D / 2), 0.1f, "WoodDark");
            Log(st, "Sill_L", new Vector3(-W / 2, sy, -D / 2), new Vector3(-W / 2, sy, D / 2), 0.1f, "WoodDark");
            Log(st, "Sill_R", new Vector3(W / 2, sy, -D / 2), new Vector3(W / 2, sy, D / 2), 0.1f, "WoodDark");

            // настил под пилораму: шпалы и доски (рельсы и каркас появятся на стадии 2)
            for (int i = 0; i < DeckN; i++)
                Box(st, "DeckBoard", new Vector3(SawX0 + 0.36f + i * 0.72f, 0.07f + O.groundY, SawZ), new Vector3(0.62f, 0.12f, 3.5f), i % 2 == 0 ? "PlankDark" : "Plank", new Vector3(0f, R(-1f, 1f), 0f));
            Log(st, "DeckEdge_F", new Vector3(SawX0, 0.16f, SawZ + 1.78f), new Vector3(SawX1, 0.16f, SawZ + 1.78f), 0.1f, "WoodDark");
            Log(st, "DeckEdge_B", new Vector3(SawX0, 0.16f, SawZ - 1.78f), new Vector3(SawX1, 0.16f, SawZ - 1.78f), 0.1f, "WoodDark");
        }

        // ---------------------------------------------------------------- забор вокруг двора (остаётся)
        BuildFence(st);

        // ---------------------------------------------------------------- природа вокруг (остаётся)
        {
            var nat = Obj(st, "Nature", Vector3.zero);
            for (int i = 0; i < 26; i++)
            {
                Vector3 p = YardPoint(1.15f);
                if (InsideBuilding(p)) continue;
                GrassTuft(nat.transform, p + Vector3.up * O.groundY, R(0.22f, 0.4f));
            }
            for (int i = 0; i < 16; i++)
            {
                Vector3 p = YardPoint(1.3f);
                if (InsideBuilding(p)) continue;
                Flower(nat.transform, p + Vector3.up * O.groundY, (i % 3 == 0) ? "Lilac" : (i % 3 == 1 ? "Cream" : "Pink"));
            }
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = YardPoint(1.2f);
                if (InsideBuilding(p)) continue;
                Rock(nat.transform, p + Vector3.up * O.groundY, R(0.3f, 0.7f), R() < 0.3f ? "Stone" : "StoneDark");
            }
            Mushroom(nat.transform, new Vector3(-4.6f, O.groundY, -3.4f), 0.28f);
            Mushroom(nat.transform, new Vector3(-4.35f, O.groundY, -3.55f), 0.2f);
            // пышные кусты за забором (как на референсе)
            Vector3[] bushes = { new Vector3(-6.4f, 0.4f, -2.2f), new Vector3(-6.2f, 0.45f, 2.8f), new Vector3(11.5f, 0.45f, -2.6f), new Vector3(5.2f, 0.4f, -5.7f), new Vector3(-2.8f, 0.4f, -5.6f) };
            for (int i = 0; i < bushes.Length; i++)
                Blob(nat.transform, "Bush", bushes[i] + Vector3.up * O.groundY, new Vector3(R(1.5f, 2.2f), R(1.0f, 1.5f), R(1.5f, 2.2f)), i % 2 == 0 ? "Moss" : "Leaf");
        }

        // ---------------------------------------------------------------- разобранные материалы (уходят в стройку)
        var debris = Obj(st, "Debris", Vector3.zero);
        var vis = debris.AddComponent<StageVisibility>();
        vis.minStage = 1;
        vis.maxStage = 1;
        vis.exitTime = 1.6f;
        pendingFlyTarget.Add(vis);
        BuildDebris(debris.transform);

        // пила лежит у ящика, пока её не поставят (видна на 1 и 2 стадии)
        var loose = Obj(st, "LooseBlade", new Vector3(3.7f, 0f, 3.1f));
        var lv = loose.AddComponent<StageVisibility>();
        lv.minStage = 1;
        lv.maxStage = 2;
        lv.exitTime = 1.0f;
        pendingFlyTarget.Add(lv);
        {
            var lt = loose.transform;
            Box(lt, "Crate", new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), "Plank", new Vector3(0f, 12f, 0f));
            Box(lt, "CrateBand1", new Vector3(0f, 0.3f, 0.46f), new Vector3(0.92f, 0.1f, 0.04f), "PlankDark", new Vector3(0f, 12f, 0f));
            // диск прислонён к ящику (ось Z мира, наклон назад)
            Part(lt, "Blade", HutMeshLib.SawBlade(20), Mat("Metal"), new Vector3(0.1f, 0.28f, 1.11f), new Vector3(1.5f, 1.5f, 0.05f), Quaternion.Euler(-62f, 12f, 0f));
        }

        PolishStage1(st);
        EndStage(s, 1);
        return s.transform;
    }

    // ---------------------------------------------------------------- забор: столбики, две верёвки, ворота, ленты
    static void BuildFence(Transform st)
    {
        var f = Obj(st, "Fence", Vector3.zero);
        var ft = f.transform;
        float y = O.groundY;

        // углы двора и ворота
        float x0 = FenceX0, x1 = FenceX1, z0 = FenceZ0, z1 = FenceZ1;
        float gx0 = GateX - GateW * 0.5f, gx1 = GateX + GateW * 0.5f;

        var lines = new List<Vector3[]>
        {
            new[] { new Vector3(x0, y, z1), new Vector3(gx0, y, z1) },     // фронт слева от ворот
            new[] { new Vector3(gx1, y, z1), new Vector3(x1, y, z1) },     // фронт справа от ворот
            new[] { new Vector3(x1, y, z1), new Vector3(x1, y, z0) },      // правая
            new[] { new Vector3(x1, y, z0), new Vector3(x0, y, z0) },      // задняя
            new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1) },      // левая
        };

        int ribbonCount = 0;
        var posts = new List<Vector3>();
        for (int li = 0; li < lines.Count; li++)
        {
            Vector3 a = lines[li][0], b = lines[li][1];
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.3f));
            Vector3 prevTop1 = Vector3.zero, prevTop2 = Vector3.zero;
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                float h = R(1.02f, 1.18f);
                Box(ft, "FencePost", p + Vector3.up * (h * 0.5f), new Vector3(0.22f, h, 0.22f), "WoodMid", new Vector3(0f, R(-8f, 8f), 0f));
                Box(ft, "PostCap", p + Vector3.up * (h + 0.03f), new Vector3(0.27f, 0.07f, 0.27f), "WoodLight", new Vector3(0f, R(-8f, 8f), 0f));
                posts.Add(p);
                Vector3 t1 = p + Vector3.up * (h - 0.16f), t2 = p + Vector3.up * (h * 0.52f);
                if (i > 0)
                {
                    // верёвка слегка провисает: две ломаных половинки
                    Sag(ft, prevTop1, t1, "Rope");
                    Sag(ft, prevTop2, t2, "Rope");
                }
                prevTop1 = t1; prevTop2 = t2;

                // красная лента на каждом третьем столбике
                if ((i + li) % 3 == 1 && ribbonCount < 8)
                {
                    ribbonCount++;
                    cloths.Add(Cloth(ft, "Ribbon", p + Vector3.up * (h - 0.05f) + new Vector3(0.12f, 0f, 0f), 0.11f, 0.17f, 3, "Red", 22f, ribbonCount * 1.3f));
                }
            }
        }

        // ворота: два высоких столба с поперечной балкой, створки сняты и лежат рядом (потом на их месте будут фонари)
        {
            var g = Obj(ft, "Gate", new Vector3(GateX, y, z1));
            float gh = 1.9f;
            Log(g.transform, "GatePostL", new Vector3(-GateW * 0.5f, 0f, 0f), new Vector3(-GateW * 0.5f, gh, 0f), 0.13f, "WoodDark");
            Log(g.transform, "GatePostR", new Vector3(GateW * 0.5f, 0f, 0f), new Vector3(GateW * 0.5f, gh, 0f), 0.13f, "WoodDark");
            Box(g.transform, "GateBeam", new Vector3(0f, gh - 0.08f, 0f), new Vector3(GateW + 0.5f, 0.2f, 0.22f), "WoodMid");
            Box(g.transform, "GateBeamTop", new Vector3(0f, gh + 0.1f, 0f), new Vector3(GateW + 0.9f, 0.12f, 0.3f), "WoodLight");
            Box(g.transform, "GateBrace_L", new Vector3(-GateW * 0.5f + 0.28f, gh - 0.32f, 0f), new Vector3(0.1f, 0.62f, 0.12f), "WoodMid", new Vector3(0f, 0f, 38f));
            Box(g.transform, "GateBrace_R", new Vector3(GateW * 0.5f - 0.28f, gh - 0.32f, 0f), new Vector3(0.1f, 0.62f, 0.12f), "WoodMid", new Vector3(0f, 0f, -38f));
            // мох на балке
            Blob(g.transform, "GateMoss", new Vector3(-0.5f, gh + 0.2f, 0f), new Vector3(0.8f, 0.14f, 0.35f), "Moss");
            // красные флажки на столбах
            cloths.Add(Cloth(g.transform, "GateFlagL", new Vector3(-GateW * 0.5f - 0.02f, gh - 0.05f, 0.14f), 0.2f, 0.2f, 4, "Red", 14f, 0.5f, "Red"));
            cloths.Add(Cloth(g.transform, "GateFlagR", new Vector3(GateW * 0.5f + 0.02f, gh - 0.05f, 0.14f), 0.2f, 0.2f, 4, "Red", 14f, 2.4f, "Red"));
        }
    }

    // верёвка с провисанием: две половинки со спадом посередине
    static void Sag(Transform p, Vector3 a, Vector3 b, string mat)
    {
        Vector3 mid = (a + b) * 0.5f + Vector3.down * Mathf.Min(0.17f, Vector3.Distance(a, b) * 0.07f);
        Rod(p, "Rope", a, mid, 0.022f, mat, 5);
        Rod(p, "Rope", mid, b, 0.022f, mat, 5);
    }

    // ---------------------------------------------------------------- разобранные материалы
    static void BuildDebris(Transform d)
    {
        float y = O.groundY;

        // куча А: брёвна пирамидой у левой стены
        LogPile(d, "LogPileA", new Vector3(-4.1f, y, 0.4f), 90f, new[] { 4, 3, 2 }, 2.6f, 0.2f);
        // куча Б: у пилорамы (будущее сырьё)
        LogPile(d, "LogPileB", new Vector3(4.9f, y, -3.45f), 0f, new[] { 4, 3 }, 3.0f, 0.24f);

        // сортированные доски (одна под брезентом)
        PlankStack(d, "PlankStackA", new Vector3(-4.4f, y, -2.9f), 2.2f, 0.9f, 6, true, 12f);
        PlankStack(d, "PlankStackB", new Vector3(-3.0f, y, -3.5f), 2.2f, 0.9f, 4, true, -6f);
        {
            var tp = Obj(d, "TarpStack", new Vector3(-0.2f, y, -3.35f));
            tp.transform.localRotation = Quaternion.Euler(0f, 4f, 0f);
            PlankStack(tp.transform, "Boards", Vector3.zero, 2.0f, 0.85f, 5, false, 0f);
            Box(tp.transform, "Tarp", new Vector3(0f, 0.42f, 0f), new Vector3(2.2f, 0.09f, 1.0f), "Tarp");
            Blob(tp.transform, "TarpFold1", new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.9f, 0.14f, 0.85f), "Tarp", 1);
            Blob(tp.transform, "TarpFold2", new Vector3(0.5f, 0.5f, 0.1f), new Vector3(0.8f, 0.12f, 0.8f), "Tarp", 2);
            Rock(tp.transform, new Vector3(-0.95f, 0.45f, 0.42f), 0.26f);
            Rock(tp.transform, new Vector3(0.95f, 0.45f, -0.4f), 0.24f);
            Rock(tp.transform, new Vector3(0.9f, 0.45f, 0.42f), 0.22f);
        }

        // пучки кровельных досок (жёлтые, перевязаны): три рядом у левого забора
        for (int i = 0; i < 3; i++)
        {
            var b = Obj(d, "ShingleBundle", new Vector3(-4.9f + i * 0.6f, y, 3.0f));
            b.transform.localRotation = Quaternion.Euler(0f, 90f + (i - 1) * 5f, 0f);
            for (int k = 0; k < 5; k++)
                Box(b.transform, "Board", new Vector3(0f, 0.05f + k * 0.07f, 0f), new Vector3(1.5f, 0.06f, 0.5f), (k % 2 == 0) ? "Roof" : "RoofShade", new Vector3(0f, R(-1.5f, 1.5f), 0f));
            for (int sd = -1; sd <= 1; sd += 2)
                Box(b.transform, "Strap", new Vector3(sd * 0.45f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.54f), "Rope");
        }

        // лежащие балки: несколько длинных, разбросанных по двору
        Vector3[,] beams =
        {
            { new Vector3(0.2f, 0f, -2.5f), new Vector3(3.2f, 0f, -1.9f) },
            { new Vector3(-1.2f, 0f, -2.0f), new Vector3(1.8f, 0f, -2.3f) },
            { new Vector3(-0.4f, 0f, 2.7f), new Vector3(-3.0f, 0f, 4.0f) },
            { new Vector3(8.4f, 0f, 3.3f), new Vector3(5.6f, 0f, 3.9f) },
        };
        for (int i = 0; i < beams.GetLength(0); i++)
        {
            Vector3 a = beams[i, 0] + Vector3.up * (y + 0.11f), b = beams[i, 1] + Vector3.up * (y + 0.11f);
            Beam(d, "LooseBeam", a, b, 0.22f, i % 2 == 0 ? "WoodMid" : "WoodDark");
        }

        // бочки
        for (int i = 0; i < 2; i++)
        {
            var bp = new Vector3(-5.0f + i * 0.75f, y, 4.35f - i * 0.1f);
            Cyl(d, "Barrel", bp + Vector3.up * 0.4f, 0.32f, 0.8f, "WoodMid", 10);
            Cyl(d, "BarrelBandA", bp + Vector3.up * 0.2f, 0.335f, 0.07f, "MetalDark", 10);
            Cyl(d, "BarrelBandB", bp + Vector3.up * 0.6f, 0.335f, 0.07f, "MetalDark", 10);
        }

        // ящики с гвоздями и болтами у ворот
        Box(d, "Crate1", new Vector3(5.2f, y + 0.3f, 4.1f), new Vector3(0.9f, 0.6f, 0.75f), "Plank", new Vector3(0f, 10f, 0f));
        Box(d, "Crate2", new Vector3(6.2f, y + 0.25f, 4.2f), new Vector3(0.7f, 0.5f, 0.7f), "PlankDark", new Vector3(0f, -14f, 0f));
        Box(d, "Crate3", new Vector3(5.45f, y + 0.85f, 4.1f), new Vector3(0.6f, 0.5f, 0.6f), "Plank", new Vector3(0f, 32f, 0f));

        // тачка с мешком и лопатой
        {
            var wb = Obj(d, "Wheelbarrow", new Vector3(8.6f, y, 2.5f));
            wb.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
            var w = wb.transform;
            Box(w, "Tray", new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.3f, 0.6f), "WoodMid");
            Part(w, "Wheel", HutMeshLib.Cyl(10), Mat("MetalDark"), new Vector3(0.55f, 0.28f, 0f), new Vector3(0.56f, 0.1f, 0.56f), Quaternion.Euler(0f, 0f, 90f));
            Beam(w, "HandleL", new Vector3(-0.35f, 0.5f, -0.25f), new Vector3(-0.95f, 0.72f, -0.32f), 0.06f, "WoodDark");
            Beam(w, "HandleR", new Vector3(-0.35f, 0.5f, 0.25f), new Vector3(-0.95f, 0.72f, 0.32f), 0.06f, "WoodDark");
            Beam(w, "Leg", new Vector3(-0.3f, 0.45f, 0f), new Vector3(-0.3f, 0f, 0f), 0.06f, "WoodDark");
            Blob(w, "Sack", new Vector3(-0.05f, 0.72f, 0f), new Vector3(0.6f, 0.4f, 0.5f), "Cream", 2);
            Beam(d, "Shovel", new Vector3(8.0f, y + 0.9f, 3.4f), new Vector3(8.4f, y + 0.05f, 3.7f), 0.05f, "WoodDark");
            Box(d, "ShovelBlade", new Vector3(8.45f, y + 0.08f, 3.73f), new Vector3(0.24f, 0.04f, 0.3f), "Metal", new Vector3(0f, 25f, 0f));
        }

        // козлы с недопиленным брёвнышком
        for (int i = 0; i < 2; i++)
        {
            float x = -0.3f + i * 1.6f;
            Beam(d, "HorseLegA", new Vector3(x, y, -1.6f), new Vector3(x, y + 0.82f, -1.35f), 0.1f, "WoodDark");
            Beam(d, "HorseLegB", new Vector3(x, y, -1.1f), new Vector3(x, y + 0.82f, -1.35f), 0.1f, "WoodDark");
        }
        Log(d, "HorseLog", new Vector3(-0.9f, y + 0.96f, -1.35f), new Vector3(2.2f, y + 0.96f, -1.35f), 0.13f, "WoodLight");

        // верстак с инструментами
        {
            var bn = Obj(d, "Workbench", new Vector3(-2.6f, y, 3.3f));
            bn.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            var b = bn.transform;
            Box(b, "Top", new Vector3(0f, 0.82f, 0f), new Vector3(1.5f, 0.1f, 0.7f), "Plank");
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(b, "Leg", new Vector3(sx * 0.65f, 0.4f, sz * 0.28f), new Vector3(0.1f, 0.8f, 0.1f), "WoodDark");
            Box(b, "Shelf", new Vector3(0f, 0.25f, 0f), new Vector3(1.3f, 0.06f, 0.6f), "PlankDark");
            // ручная пила, молоток, пара гвоздей-коробочек
            Box(b, "HandSaw", new Vector3(-0.3f, 0.89f, 0.05f), new Vector3(0.7f, 0.025f, 0.14f), "Metal", new Vector3(0f, 14f, 0f));
            Box(b, "SawHandle", new Vector3(-0.7f, 0.9f, 0.02f), new Vector3(0.14f, 0.05f, 0.1f), "WoodDark", new Vector3(0f, 14f, 0f));
            Beam(b, "HammerHandle", new Vector3(0.3f, 0.9f, -0.12f), new Vector3(0.62f, 0.9f, -0.02f), 0.04f, "WoodDark");
            Box(b, "HammerHead", new Vector3(0.28f, 0.93f, -0.13f), new Vector3(0.07f, 0.08f, 0.16f), "MetalDark", new Vector3(0f, 20f, 0f));
            Box(b, "NailBox", new Vector3(0.1f, 0.91f, 0.2f), new Vector3(0.2f, 0.07f, 0.14f), "Red");
        }

        // лестница на земле
        {
            var l = Obj(d, "Ladder", new Vector3(-1.0f, y + 0.07f, 4.15f));
            l.transform.localRotation = Quaternion.Euler(0f, 4f, 0f);
            var lt = l.transform;
            Beam(lt, "RailL", new Vector3(-1.3f, 0f, -0.24f), new Vector3(1.3f, 0f, -0.24f), 0.09f, "WoodMid");
            Beam(lt, "RailR", new Vector3(-1.3f, 0f, 0.24f), new Vector3(1.3f, 0f, 0.24f), 0.09f, "WoodMid");
            for (int i = 0; i < 6; i++)
                Beam(lt, "Rung", new Vector3(-1.05f + i * 0.42f, 0f, -0.24f), new Vector3(-1.05f + i * 0.42f, 0f, 0.24f), 0.055f, "WoodLight");
        }

        // мольберт с чертежом хижины и пилорамы
        {
            var e = Obj(d, "PlanEasel", new Vector3(-1.6f, y, 3.4f));
            e.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);
            var et = e.transform;
            Beam(et, "LegL", new Vector3(-0.45f, 0f, -0.2f), new Vector3(-0.2f, 1.55f, 0f), 0.07f, "WoodDark");
            Beam(et, "LegR", new Vector3(0.45f, 0f, -0.2f), new Vector3(0.2f, 1.55f, 0f), 0.07f, "WoodDark");
            Beam(et, "LegBack", new Vector3(0f, 0f, 0.5f), new Vector3(0f, 1.4f, 0.05f), 0.06f, "WoodDark");
            Box(et, "Paper", new Vector3(0f, 1.2f, -0.05f), new Vector3(0.95f, 0.75f, 0.03f), "Cream", new Vector3(-6f, 0f, 0f));
            // нарисованный план: красный контур домика и крыши
            Box(et, "PlanHouse", new Vector3(-0.2f, 1.1f, -0.07f), new Vector3(0.34f, 0.22f, 0.02f), "Red", new Vector3(-6f, 0f, 0f));
            Part(et, "PlanRoof", HutMeshLib.Prism, Mat("Red"), new Vector3(-0.2f, 1.28f, -0.07f), new Vector3(0.42f, 0.16f, 0.02f), Quaternion.Euler(-6f, 0f, 0f));
            Box(et, "PlanSaw", new Vector3(0.24f, 1.12f, -0.07f), new Vector3(0.3f, 0.12f, 0.02f), "MetalDark", new Vector3(-6f, 0f, 0f));
        }

        // колышки с верёвкой вокруг будущей пилорамы + красные флажки
        {
            float ax = SawX0 - 0.2f, bx = SawX1 + 0.3f, az = SawZ - 1.9f, bz = SawZ + 1.9f;
            Vector3[] c = { new Vector3(ax, y, az), new Vector3(bx, y, az), new Vector3(bx, y, bz), new Vector3(ax, y, bz) };
            for (int i = 0; i < 4; i++)
            {
                Beam(d, "Stake", c[i], c[i] + Vector3.up * 0.9f, 0.07f, "WoodLight");
                Vector3 nxt = c[(i + 1) % 4];
                Rod(d, "StakeRope", c[i] + Vector3.up * 0.7f, nxt + Vector3.up * 0.7f, 0.016f, "Red", 4);
                cloths.Add(Cloth(d, "StakeFlag", c[i] + Vector3.up * 0.88f + new Vector3(0.06f, 0f, 0f), 0.12f, 0.12f, 3, "Red", 20f, i * 1.7f));
            }
        }

        // табличка «стройка» у ворот: столб и доска со значками молотка и пилы
        {
            var sg = Obj(d, "ConstructionSign", new Vector3(GateX - 2.4f, y, FenceZ1 + 0.9f));
            sg.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
            var t = sg.transform;
            Box(t, "SignPost", new Vector3(0f, 0.8f, 0f), new Vector3(0.14f, 1.6f, 0.14f), "WoodDark");
            Box(t, "SignBoard", new Vector3(0f, 1.45f, 0.1f), new Vector3(1.2f, 0.62f, 0.07f), "Cream");
            Box(t, "SignFrame", new Vector3(0f, 1.45f, 0.07f), new Vector3(1.3f, 0.72f, 0.05f), "WoodMid");
            // молоток
            Beam(t, "HammerShaft", new Vector3(-0.24f, 1.28f, 0.16f), new Vector3(-0.1f, 1.62f, 0.16f), 0.04f, "WoodDark");
            Box(t, "HammerHead", new Vector3(-0.07f, 1.64f, 0.16f), new Vector3(0.24f, 0.11f, 0.05f), "Red", new Vector3(0f, 0f, 24f));
            // пила (зубчатая полоса)
            Box(t, "SawBlade", new Vector3(0.28f, 1.46f, 0.16f), new Vector3(0.42f, 0.1f, 0.03f), "MetalDark", new Vector3(0f, 0f, -18f));
            Box(t, "SawHandle", new Vector3(0.52f, 1.52f, 0.16f), new Vector3(0.08f, 0.14f, 0.05f), "Red", new Vector3(0f, 0f, -18f));
            cloths.Add(Cloth(t, "SignRibbon", new Vector3(-0.58f, 1.76f, 0.1f), 0.09f, 0.14f, 3, "Red", 20f, 0.4f));
        }
    }

    // пирамида брёвен: rows[i] = сколько брёвен в i-м ряду снизу
    static void LogPile(Transform p, string name, Vector3 pos, float yaw, int[] rows, float length, float radius)
    {
        var pile = Obj(p, name, pos);
        pile.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        float step = radius * 2.05f;
        for (int row = 0; row < rows.Length; row++)
        {
            int n = rows[row];
            float y = radius + row * radius * 1.78f;
            float z0 = -(n - 1) * 0.5f * step;
            for (int i = 0; i < n; i++)
            {
                float z = z0 + i * step + R(-0.015f, 0.015f);
                float len = length * R(0.9f, 1.05f);
                float sh = R(-0.12f, 0.12f);
                Log(pile.transform, "PileLog", new Vector3(-len * 0.5f + sh, y, z), new Vector3(len * 0.5f + sh, y, z), radius * R(0.92f, 1.08f), R() < 0.35f ? "WoodLight" : "WoodMid");
            }
        }
    }

    // случайная точка двора (внутри ограды с отступом inset), для природных мелочей
    static Vector3 YardPoint(float inset)
    {
        return new Vector3(R(FenceX0 - 0.8f, FenceX1 + 0.8f), 0f, R(FenceZ0 - 0.8f, FenceZ1 + 0.8f));
    }

    // чтобы цветы и трава не лезли в здания и в проход
    static bool InsideBuilding(Vector3 p)
    {
        if (p.x > -W / 2 - 0.9f && p.x < W / 2 + 0.9f && p.z > -D / 2 - 0.9f && p.z < D / 2 + 1.3f) return true;      // хижина с крыльцом
        if (p.x > SawX0 - 0.6f && p.x < SawX1 + 0.6f && p.z > SawZ - 2.1f && p.z < SawZ + 2.3f) return true;         // пилорама
        if (p.x > GateX - 1.6f && p.x < GateX + 1.6f && p.z > 1.5f) return true;                                      // проход от ворот
        return false;
    }
}
#endif

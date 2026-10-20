#if UNITY_EDITOR
// =====================================================================================
//  LocationCenter.cs  ->  Assets/Editor/LocationCenter.cs
//  Центр локации (главная точка). КОСТРА НЕТ: ставь свой префаб на CAMPFIRE_Point.
//  В центре оставлена платформа радиусом ~2.5 u (очаг + каменная кромка).
//
//  Композиция (всё считается от направлений троп, поэтому работает с любой раскладкой):
//    1) платформа под костёр -> 2) брусчатка веером концентрическими кольцами
//    3) светлые «спицы» от платформы к каждой тропе, с тёмной окантовкой
//    4) бордюр площади с проёмами у троп и большими камнями-воротами
//    5) между тропами сектора с ролями:
//         Arrival (там где спавн: пусто + указатель), Lounge (лавка, фонари, гирлянда),
//         Garden (дуги-клумбы), Camp (бочки, ящики, мешки, дрова), Cairn (кэрн, менгиры)
//       у каждого сектора сиденье у костра и декор у кромки.
//  Всё строится в "u" внутри узла CenterDecor (localScale = worldScale), детерминировано по seed.
//  Нужны новые поля Settings в LocationBuilder (см. правки): centerSeats, centerLamps,
//  centerGarland, centerPlanters, centerProps, centerSign, centerDetail.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LocationCenter
{
    const float TwoPi = Mathf.PI * 2f;
    const float PlatEdge = 2.85f;      // внешний край платформы костра (с зазором)
    const float SpokeHalf = 1.0f;      // полуширина светлой «спицы»
    const int R_ARRIVAL = 0, R_LOUNGE = 1, R_GARDEN = 2, R_CAMP = 3, R_CAIRN = 4;

    sealed class Arm { public float ang; public Vector2 dir; public Color col; }
    sealed class Gap { public float start, width; public int role; }

    readonly LocationCtx c;
    readonly System.Random rng;
    readonly List<Arm> arms = new List<Arm>();
    readonly List<Gap> gaps = new List<Gap>();
    readonly List<Vector3> occ = new List<Vector3>();     // занятые круги: x, z, радиус
    readonly List<Mesh> temp = new List<Mesh>();
    readonly Dictionary<Material, LocMB> pave = new Dictionary<Material, LocMB>();
    readonly Vector2 spawnPos = new Vector2(0f, -4.2f);

    LocBatcher bt, btFlat;
    Transform node, cols;
    float R, rSeat, rOuter;
    int loungeIdx = -1;
    Vector3[] lampTip;
    int[] lampSide;

    Mesh box, cyl6, cyl8, cyl12, cone6, post, octa, tip;
    Mesh[] ico, curb;
    Material paveA, paveB, paveC, paveL, paveL2, paveD, hearth, curbA, curbB, rockA, rockB, moss;
    Material wood, woodL, lantern, iron, soil, leaf, sack, rope;
    Material[] petals, bulbs;

    LocationCenter(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 7 + 5);
    }

    public static void Build(LocationCtx ctx) { new LocationCenter(ctx).Run(); }

    // ------------------------------------------------------------------ утилиты
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Color E(int i) { return c.s.envColors[i]; }
    int Cnt(int n) { return Mathf.Max(0, Mathf.RoundToInt(n * Mathf.Clamp(c.s.centerDetail, 0f, 2f))); }
    Mesh Keep(Mesh m) { temp.Add(m); return m; }
    static Vector2 Dir(float a) { return new Vector2(Mathf.Sin(a), Mathf.Cos(a)); }
    static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
    static Vector3 Pol(float r, float a, float y) { return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r); }

    LocMB PaveMB(Material m)
    {
        LocMB mb;
        if (!pave.TryGetValue(m, out mb)) { mb = new LocMB(); pave[m] = mb; }
        return mb;
    }

    void AddCol(Vector3 pos, Quaternion rot, Vector3 size)
    {
        var g = new GameObject("Col");
        g.transform.SetParent(cols, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.AddComponent<BoxCollider>().size = size;
    }

    void Beam(Material m, Vector3 p0, Vector3 p1, float th)
    {
        Vector3 d = p1 - p0;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Vector3 dn = d / len;
        Vector3 up = Mathf.Abs(dn.y) > 0.98f ? Vector3.right : Vector3.up;
        bt.Add(box, m, (p0 + p1) * 0.5f, Quaternion.LookRotation(dn, up), new Vector3(th, th, len));
    }

    bool InMouth(Vector2 p, float halfW)
    {
        for (int i = 0; i < arms.Count; i++)
        {
            Vector2 d = arms[i].dir;
            float along = p.x * d.x + p.y * d.y;
            if (along <= 0f) continue;
            if (Mathf.Abs(d.x * p.y - d.y * p.x) < halfW) return true;
        }
        return false;
    }

    bool PointOk(Vector2 p, float clear)
    {
        if (InMouth(p, clear)) return false;
        // Чуть больший буфер вокруг предполагаемой точки появления игрока.
        if ((p - spawnPos).sqrMagnitude < 2.56f) return false;
        for (int i = 0; i < occ.Count; i++)
        {
            Vector3 o = occ[i];
            float dx = p.x - o.x, dz = p.y - o.y;
            float rr = o.z + 0.15f;
            if (dx * dx + dz * dz < rr * rr) return false;
        }
        return true;
    }

    // влезает ли прямоугольник (hu = полудлина вдоль касательной, hv = полуглубина) на дуге радиуса r
    bool Fits(float ang, float r, float hu, float hv, float clear)
    {
        Vector2 o = Dir(ang), t = new Vector2(o.y, -o.x), ctr = o * r;
        for (int iu = -2; iu <= 2; iu++)
            for (int iv = -1; iv <= 1; iv++)
            {
                Vector2 p = ctr + t * (hu * iu * 0.5f) + o * (hv * iv);
                if (!PointOk(p, clear)) return false;
            }
        return true;
    }

    void Occupy(float ang, float r, float hu, float hv)
    {
        Vector2 o = Dir(ang), t = new Vector2(o.y, -o.x), ctr = o * r;
        float rad = Mathf.Max(0.35f, hv);
        for (int iu = -2; iu <= 2; iu++)
        {
            Vector2 p = ctr + t * (hu * iu * 0.5f);
            occ.Add(new Vector3(p.x, p.y, rad));
        }
    }

    // ------------------------------------------------------------------ запуск
    void Run()
    {
        var go = new GameObject("CenterDecor");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(node, false);
        cols = cg.transform;
        bt = new LocBatcher(1e6f);
        btFlat = new LocBatcher(1e6f);

        R = Mathf.Max(5.2f, c.centerRadius - 1.1f);   // внешний радиус брусчатки
        rSeat = Mathf.Lerp(PlatEdge, R, 0.5f);
        rOuter = R - 0.8f;

        Prep();
        Layout();

        Platform();
        Paving();
        Spokes();
        Curb();
        Apron();

        if (c.s.centerSign) Sign();
        if (c.s.centerLamps) Lamps();
        if (c.s.centerSeats) Seats();
        if (c.s.centerProps) Props();
        if (c.s.centerPlanters) Planters();

        FlushAll();
    }

    void FlushAll()
    {
        foreach (var kv in pave)
        {
            if (kv.Value.v.Count == 0) continue;
            Mesh m = kv.Value.ToMesh("Pave_" + kv.Key.name);
            temp.Add(m);
            btFlat.Add(m, kv.Key, Vector3.zero, Quaternion.identity, Vector3.one);
        }
        var ph = new GameObject("Paving").transform;
        ph.SetParent(node, false);
        btFlat.Flush(ph, "Paving", false);
        // брусчатка не отбрасывает тень, но принимает (иначе плитка «светится» в тени лавок и деревьев)
        foreach (var mr in ph.GetComponentsInChildren<MeshRenderer>()) mr.receiveShadows = true;

        bt.Flush(node, "Center", true);

        foreach (var m in temp) if (m != null) UnityEngine.Object.DestroyImmediate(m);
        temp.Clear();
    }

    // ------------------------------------------------------------------ меши и материалы
    void Prep()
    {
        box = Keep(LocKit.Box());
        cyl6 = Keep(LocKit.Prism("PCyl6", 6, 0.5f, 0.5f));
        cyl8 = Keep(LocKit.Prism("PCyl8", 8, 0.5f, 0.5f));
        cyl12 = Keep(LocKit.Prism("PCyl12", 12, 0.5f, 0.5f));
        cone6 = Keep(LocKit.Prism("PCone6", 6, 0.5f, 0f));
        post = Keep(LocKit.Prism("PPost", 6, 0.5f, 0.34f));
        octa = Keep(LocKit.Octa());
        tip = Keep(MakeTip());
        ico = new Mesh[3];
        curb = new Mesh[3];
        for (int i = 0; i < 3; i++)
        {
            ico[i] = Keep(LocKit.Ico("PRock_" + i, i + 31, LocKit.RockDeform));
            curb[i] = Keep(MakeCurb(i + 1));
        }

        paveA = LocKit.Mat("PlzPaveA", E(23), 0.05f);
        paveB = LocKit.Mat("PlzPaveB", Color.Lerp(E(23), E(22), 0.55f), 0.05f);
        paveC = LocKit.Mat("PlzPaveC", Color.Lerp(E(23), E(0), 0.5f), 0.05f);
        paveL = LocKit.Mat("PlzPaveLight", Color.Lerp(E(20), Color.white, 0.38f), 0.05f);
        paveL2 = LocKit.Mat("PlzPaveLight2", Color.Lerp(E(20), E(23), 0.5f), 0.05f);
        paveD = LocKit.Mat("PlzPaveDark", Color.Lerp(E(20), E(16), 0.5f), 0.05f);
        hearth = LocKit.Mat("PlzHearth", Color.Lerp(E(26), E(16), 0.5f), 0.05f);
        curbA = LocKit.Mat("PlzCurbA", Color.Lerp(E(0), E(2), 0.5f), 0.05f);
        curbB = LocKit.Mat("PlzCurbB", Color.Lerp(E(0), E(1), 0.5f), 0.05f);
        rockA = LocKit.Mat("PlzRockA", E(0), 0.05f);
        rockB = LocKit.Mat("PlzRockB", E(1), 0.05f);
        moss = LocKit.Mat("PlzMoss", E(17), 0.05f);
        wood = LocKit.Mat("PlzWood", E(10), 0.1f);
        woodL = LocKit.Mat("PlzWoodL", E(11), 0.1f);
        lantern = LocKit.Mat("PlzLantern", E(29), 0.2f, 1.8f);
        iron = LocKit.Mat("PlzIron", new Color(0.20f, 0.20f, 0.24f), 0.2f);
        soil = LocKit.Mat("PlzSoil", new Color(0.30f, 0.20f, 0.12f), 0.05f);
        leaf = LocKit.Mat("PlzLeaf", E(4), 0.05f);
        sack = LocKit.Mat("PlzSack", new Color(0.80f, 0.70f, 0.50f), 0.05f);
        rope = LocKit.Mat("PlzRope", new Color(0.78f, 0.68f, 0.45f), 0.05f);
        petals = new[]
        {
            LocKit.Mat("PlzFlowerW", E(7), 0.2f),
            LocKit.Mat("PlzFlowerR", E(8), 0.2f),
            LocKit.Mat("PlzFlowerY", E(9), 0.2f),
            LocKit.Mat("PlzFlowerP", E(28), 0.2f),
            LocKit.Mat("PlzFlowerB", new Color(0.55f, 0.65f, 1f), 0.2f)
        };
        bulbs = new[]
        {
            LocKit.Mat("PlzBulbA", E(29), 0.2f, 1.8f),
            LocKit.Mat("PlzBulbB", E(28), 0.2f, 1.5f),
            LocKit.Mat("PlzBulbC", new Color(0.60f, 0.90f, 1f), 0.2f, 1.5f)
        };
    }

    // бордюрный камень: усечённая призма с фаской, ось Y от 0 до 1, X = длина, Z = глубина
    static Mesh MakeCurb(int seed)
    {
        var r = new System.Random(seed * 97 + 3);
        var mb = new LocMB();
        float[] ys = { 0f, 0.78f, 1f };
        float[] hx = { 0.5f, 0.47f, 0.38f };
        float[] hz = { 0.5f, 0.46f, 0.36f };
        var ring = new Vector3[3][];
        for (int k = 0; k < 3; k++)
        {
            ring[k] = new Vector3[4];
            float jx = k == 0 ? 0f : ((float)r.NextDouble() - 0.5f) * 0.06f;
            float jz = k == 0 ? 0f : ((float)r.NextDouble() - 0.5f) * 0.06f;
            ring[k][0] = new Vector3(-hx[k] + jx, ys[k], -hz[k] + jz);
            ring[k][1] = new Vector3(hx[k] + jx, ys[k], -hz[k] - jz);
            ring[k][2] = new Vector3(hx[k] - jx, ys[k], hz[k] + jz);
            ring[k][3] = new Vector3(-hx[k] - jx, ys[k], hz[k] - jz);
        }
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 p0 = ring[k][i], p1 = ring[k][j], q0 = ring[k + 1][i], q1 = ring[k + 1][j];
                Vector3 o = new Vector3((p0.x + p1.x) * 0.5f, 0.3f, (p0.z + p1.z) * 0.5f);
                mb.Tri(p0, p1, q1, o);
                mb.Tri(p0, q1, q0, o);
            }
        mb.Tri(ring[2][0], ring[2][1], ring[2][2], Vector3.up);
        mb.Tri(ring[2][0], ring[2][2], ring[2][3], Vector3.up);
        return mb.ToMesh("PCurb" + seed);
    }

    // острие доски-указателя: плоская треугольная призма, z от 0 (основание) до 1 (остриё)
    static Mesh MakeTip()
    {
        var mb = new LocMB();
        Vector3 a = new Vector3(0f, 0.5f, 0f), b = new Vector3(0f, -0.5f, 0f), e = new Vector3(0f, 0f, 1f);
        Vector3 l = new Vector3(-0.5f, 0f, 0f), r = new Vector3(0.5f, 0f, 0f);
        mb.Tri(a + l, b + l, e + l, Vector3.left);
        mb.Tri(a + r, b + r, e + r, Vector3.right);
        mb.Tri(a + l, a + r, e + r, new Vector3(0f, 1f, 0.5f));
        mb.Tri(a + l, e + r, e + l, new Vector3(0f, 1f, 0.5f));
        mb.Tri(b + l, b + r, e + r, new Vector3(0f, -1f, 0.5f));
        mb.Tri(b + l, e + r, e + l, new Vector3(0f, -1f, 0.5f));
        return mb.ToMesh("PTip");
    }

    // ------------------------------------------------------------------ раскладка секторов
    void Layout()
    {
        arms.Clear();
        gaps.Clear();
        occ.Clear();
        int k = 0;
        foreach (var d in c.trailDirs)
        {
            Vector2 n = d.sqrMagnitude < 1e-6f ? Vector2.up : d.normalized;
            float a = Mathf.Atan2(n.x, n.y);
            if (a < 0f) a += TwoPi;
            Color col = k < c.trailColors.Count ? c.trailColors[k] : Color.white;
            arms.Add(new Arm { ang = a, dir = n, col = col });
            k++;
        }
        arms.Sort((p, q) => p.ang.CompareTo(q.ang));

        int cnt = arms.Count;
        if (cnt == 0)
            gaps.Add(new Gap { start = 0f, width = TwoPi, role = R_LOUNGE });
        else
            for (int j = 0; j < cnt; j++)
            {
                float st = arms[j].ang, en = arms[(j + 1) % cnt].ang;
                float w = en - st;
                if (w <= 0.0001f) w += TwoPi;
                gaps.Add(new Gap { start = st, width = w, role = R_GARDEN });
            }

        int arrival = -1;
        if (cnt > 0)
        {
            float sa = Mathf.Atan2(spawnPos.x, spawnPos.y);
            if (sa < 0f) sa += TwoPi;
            for (int j = 0; j < gaps.Count; j++)
                if (Mathf.Repeat(sa - gaps[j].start, TwoPi) <= gaps[j].width) { arrival = j; break; }
        }

        loungeIdx = -1;
        float best = -1e9f;
        for (int j = 0; j < gaps.Count; j++)
        {
            if (j == arrival) continue;
            float mid = gaps[j].start + gaps[j].width * 0.5f;
            float score = gaps[j].width * Mathf.Rad2Deg + 25f * Mathf.Cos(mid);   // шире и ближе к северу (дальше от камеры)
            if (score > best) { best = score; loungeIdx = j; }
        }

        int[] pattern = { R_GARDEN, R_CAMP, R_CAIRN };
        int cycle = 0;
        for (int j = 0; j < gaps.Count; j++)
        {
            if (j == arrival) gaps[j].role = R_ARRIVAL;
            else if (j == loungeIdx) gaps[j].role = R_LOUNGE;
            else gaps[j].role = pattern[cycle++ % 3];
        }
    }

    // ------------------------------------------------------------------ плитка
    void Slab(LocMB mb, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, float yb)
    {
        Vector3 cen = (a + b + cc + d) * 0.25f;
        mb.Tri(a, b, cc, Vector3.up);
        mb.Tri(a, cc, d, Vector3.up);
        Vector3[] p = { a, b, cc, d };
        for (int i = 0; i < 4; i++)
        {
            Vector3 p0 = p[i], p1 = p[(i + 1) % 4];
            Vector3 q0 = new Vector3(p0.x, yb, p0.z), q1 = new Vector3(p1.x, yb, p1.z);
            Vector3 o = (p0 + p1) * 0.5f - cen;
            o.y = 0f;
            mb.Tri(p0, p1, q1, o);
            mb.Tri(p0, q1, q0, o);
        }
    }

    // плитка-сектор кольца (радиусы r0..r1, углы a0..a1 по часовой от севера)
    void Sector(LocMB mb, float r0, float r1, float a0, float a1, float top, float joint)
    {
        float i0 = r0 + joint, i1 = r1 - joint;
        float d0 = joint / Mathf.Max(0.5f, i0), d1 = joint / Mathf.Max(0.5f, i1);
        Vector3 p00 = Pol(i0, a0 + d0, top + Rf(-0.006f, 0.006f));
        Vector3 p01 = Pol(i0, a1 - d0, top + Rf(-0.006f, 0.006f));
        Vector3 p11 = Pol(i1, a1 - d1, top + Rf(-0.006f, 0.006f));
        Vector3 p10 = Pol(i1, a0 + d1, top + Rf(-0.006f, 0.006f));
        Slab(mb, p00, p01, p11, p10, -0.03f);
    }

    static Vector3 RP(Vector2 ctr, float lx, float lz, float cs, float sn, float y)
    {
        return new Vector3(ctr.x + lx * cs - lz * sn, y, ctr.y + lx * sn + lz * cs);
    }

    void RectSlab(LocMB mb, Vector2 ctr, float hx, float hz, float yaw, float yTop, float yb)
    {
        float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
        Slab(mb,
            RP(ctr, -hx, -hz, cs, sn, yTop + Rf(-0.005f, 0.005f)),
            RP(ctr, hx, -hz, cs, sn, yTop + Rf(-0.005f, 0.005f)),
            RP(ctr, hx, hz, cs, sn, yTop + Rf(-0.005f, 0.005f)),
            RP(ctr, -hx, hz, cs, sn, yTop + Rf(-0.005f, 0.005f)), yb);
    }

    // ------------------------------------------------------------------ платформа под костёр
    void Platform()
    {
        btFlat.Add(cyl12, paveD, new Vector3(0f, 0.045f, 0f), Quaternion.identity, new Vector3(5.8f, 0.09f, 5.8f));
        btFlat.Add(cyl12, hearth, new Vector3(0f, 0.058f, 0f), Quaternion.identity, new Vector3(3.0f, 0.116f, 3.0f));

        LocMB mbA = PaveMB(paveL2), mbB = PaveMB(paveA);
        const int n = 14;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * TwoPi / n;
            Sector(i % 2 == 0 ? mbA : mbB, 1.5f, 2.5f, a0, a0 + TwoPi / n, 0.14f, 0.03f);
        }
        // каменная кромка платформы
        for (int i = 0; i < n; i++)
        {
            float a = (i + 0.5f) / n * TwoPi + Rf(-0.03f, 0.03f);
            Vector2 dv = Dir(a);
            Quaternion q = Quaternion.LookRotation(V3(dv, 0f), Vector3.up) * Quaternion.Euler(Rf(-4f, 4f), Rf(-6f, 6f), Rf(-4f, 4f));
            Vector3 pos = new Vector3(dv.x * 2.64f, 0.15f, dv.y * 2.64f);
            bt.Add(ico[i % 3], i % 2 == 0 ? rockA : rockB, pos, q, new Vector3(Rf(0.95f, 1.1f), 0.36f, Rf(0.52f, 0.6f)));
            if (rng.NextDouble() < 0.3)
                bt.Add(ico[(i + 1) % 3], moss, pos + Vector3.up * 0.1f, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.5f, 0.12f, 0.3f));
        }
    }

    // ------------------------------------------------------------------ брусчатка кольцами
    Material PaveMat(int ring, Vector2 p)
    {
        if (ring == 0) return paveL;
        if (ring == 4) return rng.NextDouble() < 0.5 ? paveD : paveC;
        float n = Mathf.PerlinNoise(p.x * 0.45f + 37f + c.seed * 0.13f, p.y * 0.45f + 11f);
        if (rng.NextDouble() < 0.12) n = (float)rng.NextDouble();
        if (n < 0.36f) return paveA;
        return n < 0.70f ? paveB : paveC;
    }

    void Paving()
    {
        bool hasArms = arms.Count > 0;
        for (int ring = 0; ring < 5; ring++)
        {
            float r0 = Mathf.Lerp(PlatEdge, R, ring / 5f);
            float r1 = Mathf.Lerp(PlatEdge, R, (ring + 1) / 5f);
            float rm = (r0 + r1) * 0.5f;
            float hA = hasArms ? Mathf.Asin(Mathf.Min(0.95f, (SpokeHalf + 0.16f) / rm)) : 0f;
            foreach (var g in gaps)
            {
                float span = g.width - 2f * hA;
                if (span <= 0.02f) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(span * rm / 0.95f));
                float step = span / n, s0 = g.start + hA;
                for (int i = 0; i < n; i++)
                {
                    float a0 = s0 + i * step;
                    Material m = PaveMat(ring, Pol(rm, a0 + step * 0.5f, 0f).XZ());
                    Sector(PaveMB(m), r0, r1, a0, a0 + step, 0.06f, 0.045f);
                }
            }
        }
    }

    // светлые плиты от платформы к каждой тропе + окантовка + «рассыпающиеся» плиты за бордюром
    void Spokes()
    {
        foreach (var arm in arms)
        {
            Vector2 d = arm.dir, p = new Vector2(d.y, -d.x);
            float a0 = PlatEdge + 0.02f, a1 = R;
            int n = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 0.9f));
            float len = (a1 - a0) / n;
            const float j = 0.04f;
            for (int i = 0; i < n; i++)
                for (int col = 0; col < 2; col++)
                {
                    float l0 = -SpokeHalf + col * SpokeHalf, l1 = l0 + SpokeHalf;
                    float s0 = a0 + i * len, s1 = s0 + len;
                    float y = 0.065f;
                    Vector3 A = V3(d * (s0 + j) + p * (l0 + j), y + Rf(-0.004f, 0.004f));
                    Vector3 B = V3(d * (s1 - j) + p * (l0 + j), y + Rf(-0.004f, 0.004f));
                    Vector3 C = V3(d * (s1 - j) + p * (l1 - j), y + Rf(-0.004f, 0.004f));
                    Vector3 D = V3(d * (s0 + j) + p * (l1 - j), y + Rf(-0.004f, 0.004f));
                    Slab(PaveMB(((i + col) & 1) == 0 ? paveL : paveL2), A, B, C, D, -0.03f);
                }
            // тёмная окантовка вдоль спицы
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float lc = sgn * (SpokeHalf + 0.07f);
                Vector3 A = V3(d * a0 + p * (lc - 0.07f), 0.085f);
                Vector3 B = V3(d * a1 + p * (lc - 0.07f), 0.085f);
                Vector3 C = V3(d * a1 + p * (lc + 0.07f), 0.085f);
                Vector3 D = V3(d * a0 + p * (lc + 0.07f), 0.085f);
                Slab(PaveMB(paveD), A, B, C, D, -0.03f);
            }
            // плиты за бордюром: редеют и «тонут» в тропе
            float rStart = R + 0.75f, rEnd = c.centerRadius + 2.4f;
            for (float rr = rStart; rr < rEnd; rr += 0.95f)
            {
                float t = Mathf.InverseLerp(rStart, rEnd, rr);
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    if (rng.NextDouble() < t * 0.8) continue;
                    float lat = sgn * (0.5f + Rf(-0.12f, 0.12f));
                    Vector2 ctr = d * (rr + Rf(-0.15f, 0.15f)) + p * lat;
                    float y = c.heightU(ctr.x, ctr.y);
                    float sz = Mathf.Lerp(0.38f, 0.26f, t) * Rf(0.9f, 1.15f);
                    RectSlab(PaveMB((sgn > 0) ? paveL : paveL2), ctr, sz, sz * Rf(0.85f, 1.1f), Rf(-0.4f, 0.4f) + Mathf.Atan2(d.x, d.y), y + 0.06f, y - 0.03f);
                }
            }
        }
    }

    // ------------------------------------------------------------------ бордюр
    void Curb()
    {
        float Rc = R + 0.26f;
        int n = Mathf.Max(12, Mathf.RoundToInt(TwoPi * Rc / 1.02f));
        var skip = new bool[n];
        for (int i = 0; i < n; i++) skip[i] = InMouth(Dir(i * TwoPi / n) * Rc, 1.5f);
        for (int i = 0; i < n; i++)
        {
            if (skip[i]) continue;
            float a = i * TwoPi / n;
            Vector2 o = Dir(a);
            Quaternion q = Quaternion.LookRotation(V3(o, 0f), Vector3.up);
            Vector3 pos = V3(o * Rc, 0f);
            bool gate = skip[(i + 1) % n] || skip[(i + n - 1) % n];
            if (gate)
            {
                // камень-«ворота» у проёма
                float k = Rf(1.0f, 1.18f);
                int v = rng.Next(3);
                Quaternion gq = q * Quaternion.Euler(Rf(-4f, 4f), Rf(-30f, 30f), Rf(-4f, 4f));
                bt.Add(ico[v], (i & 1) == 0 ? rockA : rockB, pos + Vector3.up * 0.36f * k, gq, new Vector3(0.95f * k, 0.9f * k, 0.9f * k));
                bt.Add(ico[v], moss, pos + Vector3.up * 0.70f * k, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.6f * k, 0.2f * k, 0.58f * k));
                continue;
            }
            float sy = Rf(0.22f, 0.34f);
            Material m = (i & 1) == 0 ? curbA : curbB;
            if (rng.NextDouble() < 0.15) m = (m == curbA) ? curbB : curbA;
            bt.Add(curb[rng.Next(3)], m, pos, q * Quaternion.Euler(0f, Rf(-4f, 4f), 0f), new Vector3(Rf(0.95f, 1.03f), sy, Rf(0.46f, 0.54f)));
            if (rng.NextDouble() < 0.22)
                bt.Add(ico[rng.Next(3)], moss, pos + Vector3.up * sy * 0.95f, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.55f, 0.1f, 0.3f));
        }
    }

    // галька и пятна мха сразу за бордюром: мягкий переход в траву
    void Apron()
    {
        int cnt = Cnt(40);
        for (int k = 0; k < cnt; k++)
        {
            float a = Rf(0f, TwoPi), r = Rf(R + 0.62f, R + 1.25f);
            Vector2 p = Dir(a) * r;
            if (InMouth(p, 1.75f)) continue;
            float y = c.heightU(p.x, p.y);
            if (rng.NextDouble() < 0.55)
            {
                float s = Rf(0.1f, 0.24f);
                btFlat.Add(ico[k % 3], (k & 1) == 0 ? rockA : rockB, V3(p, y + s * 0.12f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(s * 1.3f, s * 0.7f, s));
            }
            else
            {
                float s = Rf(0.5f, 0.95f);
                btFlat.Add(cyl8, moss, V3(p, y + 0.015f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(s, 0.03f, s * Rf(0.7f, 1f)));
            }
        }
    }

    // ------------------------------------------------------------------ фонари и гирлянда
    Vector3 Lamp(Vector2 p, Vector2 face)
    {
        Vector3 g = V3(p, 0f);
        Vector3 f3 = V3(face.normalized, 0f);
        bt.Add(curb[0], rockB, g, Quaternion.LookRotation(f3, Vector3.up), new Vector3(0.52f, 0.2f, 0.52f));
        bt.Add(post, wood, g + Vector3.up * 1.3f, Quaternion.identity, new Vector3(0.2f, 2.2f, 0.2f));
        bt.Add(cyl6, iron, g + Vector3.up * 0.28f, Quaternion.identity, new Vector3(0.27f, 0.1f, 0.27f));
        bt.Add(cyl6, iron, g + Vector3.up * 2.38f, Quaternion.identity, new Vector3(0.24f, 0.08f, 0.24f));
        Vector3 armEnd = g + f3 * 0.5f + Vector3.up * 2.28f;
        Beam(iron, g + Vector3.up * 2.28f, armEnd, 0.06f);
        Vector3 lp = armEnd - Vector3.up * 0.22f;
        bt.Add(box, iron, armEnd - Vector3.up * 0.05f, Quaternion.identity, new Vector3(0.03f, 0.1f, 0.03f));
        bt.Add(cyl6, lantern, lp, Quaternion.identity, new Vector3(0.27f, 0.32f, 0.27f));
        bt.Add(cone6, iron, lp + Vector3.up * 0.24f, Quaternion.identity, new Vector3(0.42f, 0.2f, 0.42f));
        bt.Add(cyl6, iron, lp - Vector3.up * 0.18f, Quaternion.identity, new Vector3(0.3f, 0.04f, 0.3f));
        AddCol(g + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.4f, 2.4f, 0.4f));
        return g + Vector3.up * 2.3f;
    }

    // по фонарю у каждого проёма, со стороны более «важного» сектора
    void Lamps()
    {
        int n = arms.Count;
        if (n == 0) return;
        lampTip = new Vector3[n];
        lampSide = new int[n];
        for (int j = 0; j < n; j++)
        {
            Gap after = gaps[j], before = gaps[(j + n - 1) % n];
            int side;
            if (after.role == R_LOUNGE) side = 1;
            else if (before.role == R_LOUNGE) side = -1;
            else
            {
                float sa = after.role == R_ARRIVAL ? -1000f : after.width;
                float sb = before.role == R_ARRIVAL ? -1000f : before.width;
                side = sa >= sb ? 1 : -1;
            }
            Vector2 d = arms[j].dir, perp = new Vector2(d.y, -d.x);
            Vector2 p = d * (R - 0.45f) + perp * (side * (SpokeHalf + 0.14f + 0.85f));
            lampTip[j] = Lamp(p, -p);
            lampSide[j] = side;
            occ.Add(new Vector3(p.x, p.y, 0.45f));
        }
        if (c.s.centerGarland && loungeIdx >= 0 && n >= 2)
        {
            int a = loungeIdx, b = (loungeIdx + 1) % n;
            if (lampSide[a] == 1 && lampSide[b] == -1) Garland(lampTip[a], lampTip[b]);
        }
    }

    void Garland(Vector3 p0, Vector3 p1)
    {
        float dist = Vector3.Distance(p0, p1);
        if (dist < 1.5f || dist > 11f) return;
        float sag = Mathf.Clamp(dist * 0.12f, 0.4f, 1.0f);
        int seg = Mathf.Max(6, Mathf.RoundToInt(dist / 0.7f));
        Vector3 prev = p0;
        for (int i = 1; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 q = Vector3.Lerp(p0, p1, t);
            q.y -= sag * 4f * t * (1f - t);
            Beam(rope, prev, q, 0.03f);
            if (i < seg)
                bt.Add(octa, bulbs[i % 3], q - Vector3.up * 0.1f, Quaternion.identity, new Vector3(0.15f, 0.19f, 0.15f));
            prev = q;
        }
    }

    // ------------------------------------------------------------------ сиденья у костра
    void Bench(Vector3 P, Quaternion q)
    {
        for (int i = -1; i <= 1; i++)
            bt.Add(box, woodL, P + q * new Vector3(0f, 0.47f, i * 0.17f), q, new Vector3(1.8f, 0.06f, 0.15f));
        Quaternion back = q * Quaternion.Euler(12f, 0f, 0f);
        foreach (float sx in new[] { -0.72f, 0.72f })
        {
            bt.Add(box, wood, P + q * new Vector3(sx, 0.22f, 0.18f), q, new Vector3(0.1f, 0.44f, 0.08f));
            bt.Add(box, wood, P + q * new Vector3(sx, 0.22f, -0.18f), q, new Vector3(0.1f, 0.44f, 0.08f));
            bt.Add(box, wood, P + q * new Vector3(sx, 0.34f, 0f), q, new Vector3(0.07f, 0.06f, 0.42f));
            bt.Add(box, wood, P + q * new Vector3(sx * 1.04f, 0.75f, 0.3f), back, new Vector3(0.08f, 0.62f, 0.07f));
        }
        bt.Add(box, woodL, P + q * new Vector3(0f, 0.66f, 0.28f), back, new Vector3(1.8f, 0.13f, 0.04f));
        bt.Add(box, woodL, P + q * new Vector3(0f, 0.86f, 0.31f), back, new Vector3(1.8f, 0.13f, 0.04f));
        AddCol(P + q * new Vector3(0f, 0.45f, 0.1f), q, new Vector3(1.9f, 0.9f, 0.6f));
    }

    void LogSeat(Vector3 P, Quaternion q)
    {
        Quaternion lay = q * Quaternion.Euler(0f, 0f, 90f);
        float d = 0.46f;
        bt.Add(cyl8, wood, P + q * new Vector3(0f, d * 0.5f + 0.02f, 0.05f), lay, new Vector3(d, 1.9f, d));
        foreach (float sx in new[] { -0.955f, 0.955f })
            bt.Add(cyl8, woodL, P + q * new Vector3(sx, d * 0.5f + 0.02f, 0.05f), lay, new Vector3(d * 0.82f, 0.03f, d * 0.82f));
        foreach (float sx in new[] { -0.7f, 0.7f })
            bt.Add(box, wood, P + q * new Vector3(sx, 0.05f, 0.05f), q, new Vector3(0.18f, 0.1f, 0.34f));
        // пенёк-табурет перед брусом
        Vector3 sp = P + q * new Vector3(0.35f, 0f, -0.7f);
        bt.Add(cyl8, wood, sp + Vector3.up * 0.2f, Quaternion.identity, new Vector3(0.5f, 0.4f, 0.5f));
        bt.Add(cyl8, woodL, sp + Vector3.up * 0.41f, Quaternion.identity, new Vector3(0.42f, 0.04f, 0.42f));
        AddCol(P + q * new Vector3(0f, 0.26f, 0f), q, new Vector3(2.0f, 0.5f, 0.55f));
        AddCol(sp + Vector3.up * 0.2f, Quaternion.identity, new Vector3(0.5f, 0.4f, 0.5f));
    }

    void Seats()
    {
        int type = 0;
        foreach (var g in gaps)
        {
            if (g.role == R_ARRIVAL) continue;
            int count = arms.Count == 0 ? 8 : (g.width > 2.0f ? 2 : 1);
            for (int i = 0; i < count; i++)
            {
                float pref = g.start + g.width * (i + 0.5f) / count;
                for (int t = 0; t < 9; t++)
                {
                    float off = ((t + 1) / 2) * 2.5f * Mathf.Deg2Rad * ((t & 1) == 1 ? 1f : -1f);
                    float ang = pref + off;
                    if (!Fits(ang, rSeat, 1.05f, 0.5f, SpokeHalf + 0.3f)) continue;
                    Vector2 o = Dir(ang);
                    Quaternion q = Quaternion.LookRotation(V3(o, 0f), Vector3.up);
                    Vector3 P = V3(o * rSeat, 0f);
                    bool bench = g.role == R_LOUNGE || (type++ % 2 == 0);
                    if (bench) Bench(P, q); else LogSeat(P, q);
                    Occupy(ang, rSeat, 1.05f, 0.5f);
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ хозяйственный уголок и кэрн
    void Barrel(Vector3 g)
    {
        bt.Add(cyl8, wood, g + Vector3.up * 0.35f, Quaternion.identity, new Vector3(0.52f, 0.7f, 0.52f));
        bt.Add(cyl8, iron, g + Vector3.up * 0.17f, Quaternion.identity, new Vector3(0.55f, 0.05f, 0.55f));
        bt.Add(cyl8, iron, g + Vector3.up * 0.5f, Quaternion.identity, new Vector3(0.55f, 0.05f, 0.55f));
        bt.Add(cyl8, woodL, g + Vector3.up * 0.7f, Quaternion.identity, new Vector3(0.44f, 0.03f, 0.44f));
    }

    void Crate(Vector3 g, Quaternion q, float size)
    {
        bt.Add(box, woodL, g + Vector3.up * (size * 0.5f), q, Vector3.one * size);
        bt.Add(box, wood, g + Vector3.up * (size * 0.5f), q, new Vector3(size * 1.04f, size * 0.12f, size * 1.04f));
        bt.Add(box, wood, g + Vector3.up * (size + 0.015f), q, new Vector3(size * 1.04f, 0.04f, size * 1.04f));
    }

    void Sack(Vector3 g, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        bt.Add(ico[0], sack, g + Vector3.up * 0.22f, q, new Vector3(0.52f, 0.46f, 0.44f));
        bt.Add(cone6, sack, g + Vector3.up * 0.5f, q, new Vector3(0.18f, 0.16f, 0.18f));
        bt.Add(cyl6, rope, g + Vector3.up * 0.44f, q, new Vector3(0.2f, 0.03f, 0.2f));
    }

    void LogZ(Vector3 g, Quaternion q, float d, float len)
    {
        Quaternion lay = q * Quaternion.Euler(90f, 0f, 0f);
        bt.Add(cyl8, wood, g, lay, new Vector3(d, len, d));
        bt.Add(cyl8, woodL, g + q * new Vector3(0f, 0f, len * 0.5f + 0.005f), lay, new Vector3(d * 0.84f, 0.03f, d * 0.84f));
    }

    void Camp(float ang)
    {
        Vector2 o = Dir(ang);
        Quaternion q = Quaternion.LookRotation(V3(o, 0f), Vector3.up);
        Vector3 P = V3(o * rOuter, 0f);
        Barrel(P + q * new Vector3(-0.85f, 0f, 0.05f));
        Barrel(P + q * new Vector3(-0.3f, 0f, -0.1f));
        Vector3 cp = P + q * new Vector3(0.35f, 0f, 0f);
        Crate(cp, q * Quaternion.Euler(0f, 12f, 0f), 0.52f);
        Crate(cp + Vector3.up * 0.55f, q * Quaternion.Euler(0f, -22f, 0f), 0.36f);
        Sack(P + q * new Vector3(-1.55f, 0f, -0.05f), Rf(0f, 360f));
        Sack(P + q * new Vector3(-1.38f, 0f, 0.42f), Rf(0f, 360f));
        // поленница
        float dl = 0.28f;
        for (int i = 0; i < 3; i++) LogZ(P + q * new Vector3(1.0f + i * 0.3f, dl * 0.5f, 0f), q, dl, 0.9f);
        for (int i = 0; i < 2; i++) LogZ(P + q * new Vector3(1.15f + i * 0.3f, dl * 0.5f + 0.26f, 0f), q, dl, 0.9f);
        LogZ(P + q * new Vector3(1.3f, dl * 0.5f + 0.52f, 0f), q, dl, 0.9f);
        AddCol(P + q * new Vector3(-0.75f, 0.4f, 0f), q, new Vector3(2.0f, 0.8f, 1.0f));
        AddCol(P + q * new Vector3(1.3f, 0.35f, 0f), q, new Vector3(0.9f, 0.7f, 0.95f));
    }

    void Cairn(float ang)
    {
        Vector2 o = Dir(ang);
        Quaternion q = Quaternion.LookRotation(V3(o, 0f), Vector3.up);
        Vector3 P = V3(o * rOuter, 0f);
        // менгиры
        bt.Add(ico[0], rockB, P + q * new Vector3(0.75f, 0.62f, 0f), q * Quaternion.Euler(Rf(-5f, 5f), Rf(0f, 360f), Rf(-5f, 5f)), new Vector3(0.62f, 1.5f, 0.52f));
        bt.Add(ico[1], rockA, P + q * new Vector3(1.2f, 0.42f, 0.2f), q * Quaternion.Euler(Rf(-5f, 5f), Rf(0f, 360f), Rf(-5f, 5f)), new Vector3(0.5f, 1.0f, 0.44f));
        bt.Add(ico[2], rockB, P + q * new Vector3(0.35f, 0.3f, 0.1f), q * Quaternion.Euler(Rf(-5f, 5f), Rf(0f, 360f), Rf(-5f, 5f)), new Vector3(0.45f, 0.7f, 0.42f));
        bt.Add(ico[0], moss, P + q * new Vector3(0.75f, 1.2f, 0f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.42f, 0.18f, 0.36f));
        // стопка камней (кэрн)
        float[] w = { 0.8f, 0.6f, 0.46f, 0.3f };
        float[] h = { 0.3f, 0.26f, 0.22f, 0.2f };
        float[] y = { 0.15f, 0.4f, 0.6f, 0.78f };
        for (int i = 0; i < 4; i++)
            bt.Add(ico[i % 3], (i & 1) == 0 ? rockA : rockB, P + q * new Vector3(-0.8f + Rf(-0.04f, 0.04f), y[i], Rf(-0.04f, 0.04f)),
                Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(w[i], h[i], w[i] * 0.88f));
        bt.Add(ico[1], moss, P + q * new Vector3(-0.8f, 0.3f, 0f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.5f, 0.1f, 0.44f));
        // куст и цветы у подножия
        bt.Add(ico[1], leaf, P + q * new Vector3(-0.1f, 0.2f, 0.42f), Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.62f, 0.4f, 0.5f));
        int nf = Mathf.Max(1, Cnt(3));
        for (int i = 0; i < nf; i++)
            bt.Add(octa, petals[rng.Next(petals.Length)], P + q * new Vector3(Rf(-0.35f, 0.15f), 0.46f + Rf(0f, 0.06f), 0.42f + Rf(-0.15f, 0.15f)), Quaternion.identity, new Vector3(0.16f, 0.12f, 0.16f));
        AddCol(P + q * new Vector3(0.1f, 0.6f, 0f), q, new Vector3(2.6f, 1.2f, 0.9f));
    }

    void Props()
    {
        foreach (var g in gaps)
        {
            if (g.role != R_CAMP && g.role != R_CAIRN) continue;
            float hu = g.role == R_CAMP ? 1.9f : 1.4f;
            float pref = g.start + g.width * 0.5f;
            for (int t = 0; t < 12; t++)
            {
                float off = ((t + 1) / 2) * 2.5f * Mathf.Deg2Rad * ((t & 1) == 1 ? 1f : -1f);
                float ang = pref + off;
                if (!Fits(ang, rOuter, hu, 0.5f, SpokeHalf + 0.35f)) continue;
                if (g.role == R_CAMP) Camp(ang); else Cairn(ang);
                Occupy(ang, rOuter, hu, 0.5f);
                break;
            }
        }
    }

    // ------------------------------------------------------------------ дуги-клумбы
    void PlanterSeg(Vector2 p, float ang, float len, Material pa, Material pb)
    {
        Quaternion q = Quaternion.LookRotation(V3(Dir(ang), 0f), Vector3.up);
        Vector3 P = V3(p, 0f);
        bt.Add(box, rockB, P + Vector3.up * 0.17f, q, new Vector3(len * 1.02f, 0.34f, 0.62f));
        bt.Add(box, rockA, P + Vector3.up * 0.35f, q, new Vector3(len * 1.04f, 0.05f, 0.72f));
        bt.Add(box, soil, P + Vector3.up * 0.38f, q, new Vector3(len * 0.98f, 0.04f, 0.46f));
        bt.Add(ico[rng.Next(3)], leaf, P + Vector3.up * 0.5f + q * new Vector3(Rf(-0.1f, 0.1f), 0f, Rf(-0.06f, 0.06f)),
            Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(len * 0.85f, 0.34f, 0.46f));
        int nf = Mathf.Max(1, Mathf.RoundToInt(rng.Next(2, 4) * c.s.centerDetail));
        for (int i = 0; i < nf; i++)
            bt.Add(octa, (i & 1) == 0 ? pa : pb, P + q * new Vector3(Rf(-len * 0.4f, len * 0.4f), 0.72f + Rf(0f, 0.08f), Rf(-0.12f, 0.12f)),
                Quaternion.identity, new Vector3(0.17f, 0.14f, 0.17f));
        AddCol(P + Vector3.up * 0.25f, q, new Vector3(len, 0.5f, 0.7f));
    }

    void Planters()
    {
        for (int gi = 0; gi < gaps.Count; gi++)
        {
            var g = gaps[gi];
            // Входной сектор оставляем визуально свободным: никаких клумб на пути от спавна к костру.
            if (g.role == R_ARRIVAL) continue;
            float r = rOuter;
            float m = arms.Count > 0 ? Mathf.Asin(Mathf.Clamp((SpokeHalf + 0.14f + 0.5f) / r, 0f, 0.95f)) : 0f;
            float s0 = g.start + m, span = g.width - 2f * m;
            if (span < 0.12f) continue;
            // Более редкие клумбы дают площадке «дышать» и не превращают край в сплошной забор.
            int n = Mathf.Max(1, Mathf.RoundToInt(span * r / 0.86f));
            float step = span / n, len = step * r;
            Material pa = petals[gi % petals.Length], pb = petals[(gi + 2) % petals.Length];
            for (int i = 0; i < n; i++)
            {
                float ang = s0 + (i + 0.5f) * step;
                Vector2 p = Dir(ang) * r;
                if (!PointOk(p, SpokeHalf + 0.3f)) continue;
                PlanterSeg(p, ang, len, pa, pb);
            }
        }
    }

    // ------------------------------------------------------------------ указатель
    void Sign()
    {
        if (arms.Count == 0) return;
        Gap arr = null;
        foreach (var g in gaps) if (g.role == R_ARRIVAL) { arr = g; break; }
        if (arr == null) arr = gaps[0];
        float mid = arr.start + arr.width * 0.5f;
        // Указатель стоит сбоку от оси входа, а не посреди основного коридора.
        float rs = R - 0.85f;
        for (int k = 0; k < 16; k++)
        {
            float off = (7f + 2.5f * (k / 2)) * Mathf.Deg2Rad * ((k & 1) == 0 ? 1f : -1f);
            float ang = mid + off;
            if (!Fits(ang, rs, 0.5f, 0.5f, SpokeHalf + 0.45f)) continue;
            BuildSign(Dir(ang) * rs);
            Occupy(ang, rs, 0.5f, 0.5f);
            return;
        }
    }

    // столб с досками-стрелками: каждая смотрит на свою тропу и окрашена в цвет её площадки
    void BuildSign(Vector2 S)
    {
        Vector3 g = V3(S, 0f);
        bt.Add(curb[1], rockB, g, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(0.7f, 0.2f, 0.7f));
        bt.Add(post, wood, g + Vector3.up * 1.4f, Quaternion.identity, new Vector3(0.2f, 2.4f, 0.2f));
        bt.Add(cone6, woodL, g + Vector3.up * 2.72f, Quaternion.identity, new Vector3(0.34f, 0.26f, 0.34f));
        int n = Mathf.Min(6, arms.Count);
        for (int i = 0; i < n; i++)
        {
            Vector2 to = arms[i].dir * R - S;
            if (to.sqrMagnitude < 0.01f) continue;
            Vector3 dv = V3(to.normalized, 0f);
            Quaternion q = Quaternion.LookRotation(dv, Vector3.up);
            Material m = LocKit.Mat("PlzSign" + i, Color.Lerp(arms[i].col, E(11), 0.45f), 0.1f);
            Vector3 b0 = g + Vector3.up * (2.35f - i * 0.3f);
            float len = 1.0f + (i % 2) * 0.15f;
            bt.Add(box, iron, b0 + dv * 0.05f, q, new Vector3(0.24f, 0.27f, 0.05f));
            bt.Add(box, m, b0 + dv * (0.1f + len * 0.5f), q, new Vector3(0.07f, 0.23f, len));
            bt.Add(tip, m, b0 + dv * (0.1f + len), q, new Vector3(0.07f, 0.23f, 0.26f));
        }
        AddCol(g + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.45f, 2.4f, 0.45f));
    }
}

// маленькое расширение: Vector3 -> Vector2 (x, z)
static class LocCenterExt
{
    public static Vector2 XZ(this Vector3 v) { return new Vector2(v.x, v.z); }
}
#endif

// Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBuilder.Props.cs (НЕ в Editor)
// Реквизит поляны: ворота-арка с вывеской, гирлянды, пни (один с топором), козлы с брёвнами, телега, навес, поленницы, упавшие брёвна,
// указатель и фонарные столбы. Координаты локальные (метры от PAD, ворота на -Z), высоты берутся с земли лучом вниз.
using UnityEngine;

public partial class ChopGroveBuilder
{
    Mesh lanternFrame, lanternLamp;

    float Y(float x, float z) { return GroundY(x, z); }

    // ---------------------------------------------------------------- общие кирпичики
    static readonly Color[] RingsLog = { new Color(0.66f, 0.43f, 0.24f), GrovePal.WoodLight, GrovePal.WoodRing, GrovePal.WoodLight };

    void Log(Vector3 c, float yaw, float len, float r, int sides = 8)
    {
        props.Push(c, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -90f));
        props.Cylinder(new Vector3(0f, -len * 0.5f, 0f), r, r * 0.98f, len, sides, GrovePal.Bark, RingsLog, RingsLog);
        props.Pop();
    }

    void Beam(Vector3 a, Vector3 b, float r, int sides, Color col)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        props.Push(a, Quaternion.FromToRotation(Vector3.up, d / len));
        props.Cylinder(Vector3.zero, r, r, len, sides, col, null, null, false, false);
        props.Pop();
    }

    void BuildProps()
    {
        EnsureLanternMeshes();
        Gate();
        PolesAndBunting();

        // пни: первый с топором
        float axeTop = Stump(-4.6f, 2.4f, 0.58f, 0.62f);
        AxeInStump(-4.6f, 2.4f, axeTop);
        Stump(-2.0f, 3.2f, 0.42f, 0.42f);
        Stump(-6.6f, -1.4f, 0.50f, 0.50f);
        Stump(3.4f, -3.6f, 0.42f, 0.40f);
        Stump(7.0f, -3.4f, 0.50f, 0.46f);
        Stump(-5.8f, -5.4f, 0.55f, 0.38f);

        Sawhorse(-3.6f, 5.2f, 25f);
        Cart(4.0f, -0.7f, 20f);
        Shed(6.6f, 6.0f);

        LogPile(2.4f, 7.6f, -80f, 3);
        LogPile(-7.0f, -3.4f, 70f, 3);
        FallenLog(7.0f, 0.8f, 80f);
        FallenLog(-4.4f, 8.2f, -20f);

        LanternPost(-3.0f, -1.2f, 1f);
        LanternPost(2.0f, -3.0f, -1f);
        Signpost(-3.0f, -3.8f);

        if (interactiveStump) MakeChopBlock(-4.6f, 2.4f, axeTop);
    }

    // ---------------------------------------------------------------- фонарь (отдельный объект: качается и светится)
    void EnsureLanternMeshes()
    {
        if (lanternFrame != null) return;
        var f = new GroveMesh(seed + 3);
        Color dark = GrovePal.MetalDark;
        f.Box(new Vector3(0f, -0.10f, 0f), new Vector3(0.02f, 0.20f, 0.02f), dark, 0f);          // цепь
        f.Cone(new Vector3(0f, -0.30f, 0f), 0.15f, 0.10f, 6, dark, 0f);                           // крышка
        f.Box(new Vector3(0f, -0.595f, 0f), new Vector3(0.17f, 0.04f, 0.17f), dark, 0f);          // основание
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                f.Box(new Vector3(0.07f * sx, -0.44f, 0.07f * sz), new Vector3(0.02f, 0.29f, 0.02f), dark, 0f);
        lanternFrame = f.ToMesh("ChopGrove_LanternFrame");
        var l = new GroveMesh(seed + 4);
        l.Box(new Vector3(0f, -0.44f, 0f), new Vector3(0.11f, 0.25f, 0.11f), GrovePal.Lamp, 0f);
        lanternLamp = l.ToMesh("ChopGrove_LanternLamp");
    }

    void AddLantern(Vector3 top, bool real)
    {
        if (!lanterns) return;
        var go = new GameObject("Lantern");
        go.SetActive(false);   // сначала собираем и настраиваем, потом включаем (FireSource читает поля при старте)
        go.transform.SetParent(liveRoot, false);
        go.transform.localPosition = top;
        MeshObject("Frame", go.transform, lanternFrame, true);
        var lamp = MeshObject("Lamp", go.transform, lanternLamp, false, false);
        var sway = go.AddComponent<ToonSway>();
        sway.angle = 3.0f; sway.speed = 0.85f;
        var fire = go.AddComponent<FireSource>();
        fire.preset = FlickerPreset.Lantern;
        fire.color = new Color(1f, 0.78f, 0.45f);
        fire.intensity = 1.8f;
        fire.range = 5.5f;
        fire.lightOffset = new Vector3(0f, -0.45f, 0f);
        fire.simOnly = !real;
        fire.emissive = new Renderer[] { lamp.GetComponent<Renderer>() };
        fire.emissiveStrength = 2.2f;
        fire.glow = true;
        fire.glowSize = 1.0f;
        fire.glowPoints = new[] { new Vector3(0f, -0.44f, 0f) };
        go.SetActive(true);
    }

    // ---------------------------------------------------------------- ворота
    const float GateZ = -8.2f, GateHalf = 2.5f, GateH = 3.7f;

    void Gate()
    {
        float yL = Y(-GateHalf, GateZ), yR = Y(GateHalf, GateZ);
        float yTop = Mathf.Max(yL, yR) + GateH;

        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * GateHalf;
            float y = s < 0 ? yL : yR;
            float baseY = y - 0.15f;
            props.Cylinder(new Vector3(x, baseY, GateZ), 0.74f, 0.60f, 1.0f, 7, GrovePal.Stone, null, null, true, false);       // каменное основание
            props.Cylinder(new Vector3(x, baseY + 0.98f, GateZ), 0.62f, 0.62f, 0.07f, 7, GrovePal.StoneDark, null, null, true, false);
            props.Blob(new Vector3(x + s * 0.62f, y + 0.12f, GateZ - 0.30f), new Vector3(0.40f, 0.30f, 0.36f), GrovePal.Rock);
            props.Blob(new Vector3(x + s * 0.30f, y + 0.08f, GateZ + 0.62f), new Vector3(0.30f, 0.22f, 0.28f), GrovePal.Stone);
            props.Cylinder(new Vector3(x, y + 0.9f, GateZ), 0.34f, 0.29f, yTop - (y + 0.9f), 8, GrovePal.Bark,
                           new[] { GrovePal.WoodRing, GrovePal.WoodLight, GrovePal.WoodRing }, null, true, false);               // столб-бревно
            props.Cylinder(new Vector3(x, y + 1.7f, GateZ), 0.37f, 0.37f, 0.08f, 8, GrovePal.Rope, null, null, true, true, 0.02f);   // обвязки
            props.Cylinder(new Vector3(x, y + 3.0f, GateZ), 0.34f, 0.34f, 0.08f, 8, GrovePal.Rope, null, null, true, true, 0.02f);
            AddBoxCollider("Col_GatePost", new Vector3(x, y + GateH * 0.5f, GateZ), new Vector3(0.85f, GateH, 0.85f), 0f);
            Occupy(x, GateZ, 1.2f);

            // кронштейн и фонарь спереди столба (со стороны входа)
            Vector3 arm0 = new Vector3(x, y + 2.75f, GateZ - 0.28f);
            Vector3 arm1 = new Vector3(x - s * 0.1f, y + 2.75f, GateZ - 0.78f);
            props.BoxBeam(arm0, arm1, 0.07f, 0.07f, GrovePal.MetalDark, 0.02f);
            AddLantern(arm1 + new Vector3(0f, -0.04f, 0f), true);
        }

        // арка: дугой из брёвен, узлы скруглены
        const int n = 12;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float x = Mathf.Lerp(-GateHalf, GateHalf, t);
            float y = yTop - 0.05f + 0.72f * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.75f);
            Vector3 p = new Vector3(x, y, GateZ);
            if (i > 0) Beam(prev, p, 0.21f, 7, GrovePal.Bark);
            prev = p;
            props.Octa(p, new Vector3(0.26f, 0.26f, 0.26f), GrovePal.BarkDark, 0.04f);
            if (i > 0 && i < n && i % 2 == 0)
                props.Octa(p + new Vector3(0f, -0.34f, -0.05f), new Vector3(0.075f, 0.09f, 0.075f), GrovePal.Bulbs[(i / 2) % GrovePal.Bulbs.Length], 0.05f);   // лампочки
        }

        // вывеска с топором: отдельный объект, чуть покачивается
        float archBottom = yTop - 0.05f + 0.72f - 0.22f;
        MakeSign(new Vector3(0f, archBottom, GateZ));
        Occupy(0f, GateZ, 1.0f);   // сам проход свободен: тропа идёт через него, поэтому радиус маленький
    }

    void MakeSign(Vector3 pivot)
    {
        var g = new GroveMesh(seed + 5);
        for (int sx = -1; sx <= 1; sx += 2)
            g.Box(new Vector3(0.52f * sx, -0.26f, 0f), new Vector3(0.025f, 0.52f, 0.025f), GrovePal.Rope, 0.02f);   // верёвки
        g.Box(new Vector3(0f, -0.88f, 0f), new Vector3(1.62f, 0.86f, 0.09f), GrovePal.PlankDark, 0.03f);          // рама
        g.Box(new Vector3(0f, -0.88f, 0f), new Vector3(1.46f, 0.70f, 0.115f), GrovePal.Plank, 0.04f);              // доска
        for (int k = -1; k <= 1; k += 2) g.Box(new Vector3(0f, -0.88f + 0.17f * k, 0f), new Vector3(1.46f, 0.012f, 0.12f), GrovePal.PlankDark, 0.02f);   // стыки досок
        for (int side = -1; side <= 1; side += 2) AxeIcon(g, new Vector3(0f, -0.88f, 0.062f * side), 1.0f);
        var go = MeshObject("Sign", liveRoot, g.ToMesh("ChopGrove_Sign"), true);
        go.transform.localPosition = pivot;
        var sway = go.AddComponent<ToonSway>();
        sway.angle = 2.0f; sway.speed = 0.7f;
    }

    // значок топора в плоскости XY (рисуется с обеих сторон вывески)
    void AxeIcon(GroveMesh g, Vector3 c, float s)
    {
        g.Push(c, Quaternion.Euler(0f, 0f, 38f));
        g.Box(new Vector3(0f, -0.02f * s, 0f), new Vector3(0.075f * s, 0.62f * s, 0.05f), GrovePal.WoodLight, 0.02f);
        g.Box(new Vector3(-0.115f * s, 0.20f * s, 0f), new Vector3(0.20f * s, 0.20f * s, 0.055f), GrovePal.Metal, 0.02f);
        g.Box(new Vector3(-0.235f * s, 0.20f * s, 0f), new Vector3(0.05f * s, 0.27f * s, 0.058f), new Color(0.90f, 0.93f, 0.98f), 0f);
        g.Box(new Vector3(0.05f * s, 0.20f * s, 0f), new Vector3(0.08f * s, 0.12f * s, 0.055f), GrovePal.MetalDark, 0.02f);
        g.Pop();
    }

    // ---------------------------------------------------------------- столбы и гирлянды
    void Pole(float x, float z, float h)
    {
        float y = Y(x, z);
        props.Cylinder(new Vector3(x, y - 0.1f, z), 0.13f, 0.08f, h + 0.1f, 6, GrovePal.Bark, new[] { GrovePal.WoodLight }, null, true, false);
        props.Octa(new Vector3(x, y + h + 0.05f, z), new Vector3(0.12f, 0.12f, 0.12f), GrovePal.BarkDark);
        Occupy(x, z, 0.9f);
    }

    void PolesAndBunting()
    {
        Vector3 postL = new Vector3(-GateHalf, Y(-GateHalf, GateZ) + GateH - 0.35f, GateZ);
        Vector3 postR = new Vector3(GateHalf, Y(GateHalf, GateZ) + GateH - 0.35f, GateZ);
        Pole(-7.8f, -5.6f, 3.3f); Pole(7.6f, -5.2f, 3.3f);
        Pole(-8.2f, 0.8f, 3.2f); Pole(8.3f, 3.2f, 3.2f);
        if (!bunting) return;
        Vector3 pA = new Vector3(-7.8f, Y(-7.8f, -5.6f) + 3.25f, -5.6f), pB = new Vector3(7.6f, Y(7.6f, -5.2f) + 3.25f, -5.2f);
        Vector3 pC = new Vector3(-8.2f, Y(-8.2f, 0.8f) + 3.15f, 0.8f), pD = new Vector3(8.3f, Y(8.3f, 3.2f) + 3.15f, 3.2f);
        Bunting(postL, pA, 0.55f, 0);
        Bunting(postR, pB, 0.55f, 2);
        Bunting(pA, pC, 0.75f, 1);
        Bunting(pB, pD, 0.75f, 3);
    }

    void Bunting(Vector3 a, Vector3 b, float sag, int salt)
    {
        float len = (b - a).magnitude;
        int n = Mathf.Max(4, Mathf.RoundToInt(len / 0.55f));
        Vector3[] pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= sag * 4f * t * (1f - t);
            pts[i] = p;
        }
        for (int i = 0; i < n; i++) props.BoxBeam(pts[i], pts[i + 1], 0.028f, 0.028f, GrovePal.BarkDark, 0.02f);
        for (int i = 1; i < n; i++)
        {
            Vector3 dir = (pts[i + 1] - pts[i - 1]).normalized;
            Vector3 p = pts[i];
            if (i % 4 == 2)
            {
                props.Octa(p + new Vector3(0f, -0.13f, 0f), new Vector3(0.065f, 0.085f, 0.065f), GrovePal.Bulbs[(i + salt) % GrovePal.Bulbs.Length], 0.04f);
                continue;
            }
            Color c = GrovePal.Flags[(i + salt) % GrovePal.Flags.Length];
            float w = 0.34f, hgt = 0.36f + props.Range(-0.04f, 0.05f);
            props.TriBoth(p - dir * (w * 0.5f), p + dir * (w * 0.5f), p + new Vector3(0f, -hgt, 0f), props.Jit(c, 0.05f));
        }
    }

    // ---------------------------------------------------------------- пни
    float Stump(float x, float z, float r, float h)
    {
        float y = Y(x, z);
        Color[] rings = { new Color(0.64f, 0.42f, 0.23f), GrovePal.WoodLight, GrovePal.WoodRing, GrovePal.WoodLight };
        props.Cylinder(new Vector3(x, y - 0.18f, z), r * 1.14f, r * 0.97f, h + 0.18f, 9, GrovePal.Bark, rings, null, true, false);
        for (int k = 0; k < 4; k++)       // корни
        {
            float a = props.Range(0f, 6.28f);
            props.Octa(new Vector3(x + Mathf.Cos(a) * r * 1.05f, y + 0.03f, z + Mathf.Sin(a) * r * 1.05f),
                       new Vector3(r * 0.34f, 0.14f, r * 0.22f), GrovePal.Bark, 0.08f);
        }
        Occupy(x, z, r + 0.5f);
        AddCapsuleCollider("Col_Stump", new Vector3(x, y + h * 0.5f, z), r * 0.95f, h);
        return y + h;
    }

    void AxeInStump(float x, float z, float topY)
    {
        props.Push(new Vector3(x + 0.08f, topY - 0.04f, z - 0.02f), Quaternion.Euler(10f, 24f, -22f));
        props.Box(new Vector3(0f, 0.48f, 0f), new Vector3(0.08f, 0.96f, 0.08f), GrovePal.WoodLight, 0.02f);
        props.Box(new Vector3(-0.15f, 0.88f, 0f), new Vector3(0.30f, 0.22f, 0.07f), GrovePal.Metal, 0.02f);
        props.Box(new Vector3(-0.32f, 0.88f, 0f), new Vector3(0.06f, 0.30f, 0.075f), new Color(0.92f, 0.95f, 1f), 0f);
        props.Box(new Vector3(0.06f, 0.88f, 0f), new Vector3(0.10f, 0.14f, 0.075f), GrovePal.MetalDark, 0.02f);
        props.Pop();
        // щепки вокруг пня
        for (int i = 0; i < 9; i++) Chip(x + props.Range(-1.0f, 1.0f), z + props.Range(-1.0f, 1.0f));
    }

    void Chip(float x, float z)
    {
        props.Push(new Vector3(x, Y(x, z) + 0.02f, z), Quaternion.Euler(0f, props.Range(0f, 360f), 0f));
        props.Box(Vector3.zero, new Vector3(props.Range(0.10f, 0.20f), 0.025f, props.Range(0.04f, 0.07f)), props.Rand() > 0.5f ? GrovePal.WoodLight : GrovePal.WoodRing, 0.08f);
        props.Pop();
    }

    // ---------------------------------------------------------------- козлы с бревном
    void Sawhorse(float x, float z, float yaw)
    {
        float y = Y(x, z);
        props.Push(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        for (int sx = -1; sx <= 1; sx += 2)
        {
            props.BoxBeam(new Vector3(0.72f * sx, 0f, -0.42f), new Vector3(0.72f * sx, 0.92f, 0.05f), 0.11f, 0.11f, GrovePal.Plank);
            props.BoxBeam(new Vector3(0.72f * sx, 0f, 0.42f), new Vector3(0.72f * sx, 0.92f, -0.05f), 0.11f, 0.11f, GrovePal.PlankDark);
        }
        props.Box(new Vector3(0f, 0.93f, 0f), new Vector3(1.75f, 0.10f, 0.16f), GrovePal.Plank);
        Log(new Vector3(0.05f, 1.22f, 0f), 0f, 2.4f, 0.27f);
        props.Pop();
        for (int i = 0; i < 6; i++) Chip(x + props.Range(-1.2f, 1.2f), z + props.Range(-0.9f, 0.9f));
        Occupy(x, z, 1.5f);
        AddBoxCollider("Col_Sawhorse", new Vector3(x, y + 0.65f, z), new Vector3(2.5f, 1.3f, 0.9f), yaw);
    }

    // ---------------------------------------------------------------- телега с брёвнами
    void Cart(float x, float z, float yaw)
    {
        float y = Y(x, z);
        props.Push(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        props.Box(new Vector3(0f, 0.58f, 0f), new Vector3(1.95f, 0.14f, 1.08f), GrovePal.Plank);
        props.Box(new Vector3(0f, 0.78f, 0.52f), new Vector3(1.95f, 0.28f, 0.06f), GrovePal.PlankDark);
        props.Box(new Vector3(0f, 0.78f, -0.52f), new Vector3(1.95f, 0.28f, 0.06f), GrovePal.PlankDark);
        props.Box(new Vector3(-0.95f, 0.78f, 0f), new Vector3(0.06f, 0.28f, 1.08f), GrovePal.PlankDark);
        props.Box(new Vector3(0.20f, 0.42f, 0f), new Vector3(0.10f, 0.10f, 1.5f), GrovePal.Bark);   // ось
        for (int side = -1; side <= 1; side += 2)
        {
            float zb = side > 0 ? 0.52f : -0.64f;
            props.Push(new Vector3(0.20f, 0.42f, zb), Quaternion.Euler(90f, 0f, 0f));
            props.Cylinder(Vector3.zero, 0.42f, 0.42f, 0.12f, 10, GrovePal.Plank, new[] { GrovePal.WoodRing, GrovePal.Plank, GrovePal.WoodLight }, new[] { GrovePal.WoodRing, GrovePal.Plank, GrovePal.WoodLight });
            for (int k = 0; k < 3; k++)
            {
                props.Push(Vector3.zero, Quaternion.Euler(0f, k * 60f, 0f));
                props.Box(new Vector3(0f, 0.065f, 0f), new Vector3(0.82f, 0.145f, 0.07f), GrovePal.PlankDark, 0.03f);
                props.Pop();
            }
            props.Cylinder(new Vector3(0f, -0.04f, 0f), 0.09f, 0.09f, 0.20f, 6, GrovePal.MetalDark, null, null, true, true, 0.02f);
            props.Pop();
        }
        // оглобли вперёд (+X)
        props.BoxBeam(new Vector3(0.9f, 0.62f, 0.44f), new Vector3(2.15f, 0.20f, 0.40f), 0.07f, 0.07f, GrovePal.Wood);
        props.BoxBeam(new Vector3(0.9f, 0.62f, -0.44f), new Vector3(2.15f, 0.20f, -0.40f), 0.07f, 0.07f, GrovePal.Wood);
        props.Box(new Vector3(2.15f, 0.18f, 0f), new Vector3(0.07f, 0.07f, 0.88f), GrovePal.Wood);
        // брёвна в кузове вдоль X
        float[] row0 = { -0.36f, 0f, 0.36f };
        foreach (float zz in row0) Log(new Vector3(-0.02f, 0.65f + 0.17f, zz), 0f, 1.65f, 0.17f, 7);
        Log(new Vector3(0.03f, 0.65f + 0.17f + 0.30f, -0.18f), 0f, 1.6f, 0.17f, 7);
        Log(new Vector3(-0.04f, 0.65f + 0.17f + 0.30f, 0.18f), 0f, 1.6f, 0.17f, 7);
        props.Pop();
        Occupy(x, z, 1.7f);
        AddBoxCollider("Col_Cart", new Vector3(x, y + 0.85f, z), new Vector3(2.1f, 1.5f, 1.5f), yaw);
        for (int i = 0; i < 5; i++) Chip(x + props.Range(-1.8f, 1.8f), z + props.Range(-1.5f, 1.5f));
    }

    // ---------------------------------------------------------------- навес (пилорама-сарай)
    void Shed(float x, float z)
    {
        float y = Y(x, z);
        Vector2 toCenter = new Vector2(-x, 0.8f - z);
        float yaw = Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg;      // вход смотрит на площадь
        props.Push(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        const float hw = 1.75f, bz = -1.2f, fz = 1.2f;
        // посты: сзади выше, спереди ниже, крыша уклоном вперёд
        for (int sx = -1; sx <= 1; sx += 2)
        {
            props.Cylinder(new Vector3(hw * sx, -0.1f, bz), 0.11f, 0.10f, 2.95f, 6, GrovePal.Bark, null, null, false, false);
            props.Cylinder(new Vector3(hw * sx, -0.1f, fz), 0.11f, 0.10f, 2.35f, 6, GrovePal.Bark, null, null, false, false);
        }
        // задняя стена из досок
        for (int i = 0; i < 11; i++)
        {
            float px = -hw + 0.12f + i * ((2f * hw - 0.24f) / 10f);
            props.Box(new Vector3(px, 1.15f, bz + 0.02f), new Vector3(0.33f, 2.2f + props.Range(-0.12f, 0.1f), 0.06f), i % 2 == 0 ? GrovePal.Plank : GrovePal.PlankDark, 0.05f);
        }
        // левая стенка наполовину
        for (int i = 0; i < 4; i++)
            props.Box(new Vector3(-hw + 0.02f, 0.5f, bz + 0.3f + i * 0.6f), new Vector3(0.06f, 0.9f, 0.55f), i % 2 == 0 ? GrovePal.PlankDark : GrovePal.Plank, 0.05f);
        // крыша: ряды досок по уклону
        float slope = Mathf.Atan2(0.6f, fz - bz) * Mathf.Rad2Deg;
        props.Push(new Vector3(0f, 2.62f, 0f), Quaternion.Euler(slope, 0f, 0f));
        props.Box(new Vector3(0f, -0.14f, 0f), new Vector3(2f * hw + 0.2f, 0.10f, 2.75f), GrovePal.RoofDark);
        const int rows = 9;
        for (int i = 0; i < rows; i++)
        {
            float rz = -1.45f + i * (2.95f / (rows - 1));
            props.Box(new Vector3(0f, 0.01f + (i % 2) * 0.012f, rz), new Vector3(2f * hw + 0.45f, 0.07f, 0.40f), i % 2 == 0 ? GrovePal.Roof : GrovePal.Plank, 0.05f);
        }
        props.Pop();
        // внутри: стопка досок, поленница, колода-верстак
        for (int r = 0; r < 6; r++)
            props.Box(new Vector3(-0.75f, 0.08f + r * 0.085f, -0.45f), new Vector3(1.45f, 0.075f, 0.52f + props.Range(-0.04f, 0.04f)), r % 2 == 0 ? GrovePal.WoodLight : GrovePal.Plank, 0.05f);
        props.Box(new Vector3(0.95f, 0.45f, -0.5f), new Vector3(0.9f, 0.09f, 0.5f), GrovePal.PlankDark);
        props.Box(new Vector3(0.65f, 0.22f, -0.5f), new Vector3(0.09f, 0.45f, 0.45f), GrovePal.Bark);
        props.Box(new Vector3(1.25f, 0.22f, -0.5f), new Vector3(0.09f, 0.45f, 0.45f), GrovePal.Bark);
        props.Push(new Vector3(0.85f, 0f, 0.35f), Quaternion.identity);
        for (int r = 0; r < 3; r++)
            for (int k = 0; k < 3 - r; k++)
                Log(new Vector3(0f, 0.17f + r * 0.30f, -0.34f + k * 0.34f + r * 0.17f), 0f, 1.2f, 0.16f, 7);
        props.Pop();
        props.Pop();

        Occupy(x, z, 3.2f);
        // коллайдер: задняя часть навеса, спереди можно зайти под крышу
        Vector3 back = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.55f);
        AddBoxCollider("Col_Shed", new Vector3(x + back.x, y + 1.1f, z + back.z), new Vector3(3.6f, 2.2f, 1.4f), yaw);
        // фонарь на правом переднем столбе
        Vector3 lp = Quaternion.Euler(0f, yaw, 0f) * new Vector3(hw - 0.35f, 0f, fz + 0.15f);
        AddLantern(new Vector3(x + lp.x, y + 2.15f, z + lp.z), false);
    }

    // ---------------------------------------------------------------- поленница и упавшие брёвна
    void LogPile(float x, float z, float yaw, int rows)
    {
        float y = Y(x, z);
        props.Push(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        for (int r = 0; r < rows; r++)
        {
            int cnt = rows - r;
            for (int k = 0; k < cnt; k++)
            {
                float zz = (k - (cnt - 1) * 0.5f) * 0.40f;
                Log(new Vector3(props.Range(-0.05f, 0.05f), 0.19f + r * 0.34f, zz), 0f, 1.5f + props.Range(-0.12f, 0.12f), 0.19f, 7);
            }
        }
        props.Pop();
        Occupy(x, z, 1.3f);
        AddBoxCollider("Col_LogPile", new Vector3(x, y + 0.4f, z), new Vector3(1.6f, 0.8f, 0.4f * rows + 0.2f), yaw);
    }

    void FallenLog(float x, float z, float yaw)
    {
        float y = Y(x, z);
        Log(new Vector3(x, y + 0.34f, z), yaw, 3.0f, 0.36f, 9);
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        for (int i = -1; i <= 1; i += 2)
        {
            Vector3 o = q * new Vector3(0.7f * i, 0f, 0f);
            props.Cone(new Vector3(x + o.x, y + 0.60f, z + o.z), 0.07f, 0.28f, 5, GrovePal.Bark);
        }
        Vector3 m = q * new Vector3(-0.3f, 0f, 0f);
        props.Blob(new Vector3(x + m.x, y + 0.70f, z + m.z), new Vector3(0.55f, 0.10f, 0.22f), GrovePal.Moss, 0.2f);
        Occupy(x, z, 1.6f);
        AddBoxCollider("Col_FallenLog", new Vector3(x, y + 0.36f, z), new Vector3(3.0f, 0.72f, 0.8f), yaw);
    }

    // ---------------------------------------------------------------- фонарный столб и указатель
    void LanternPost(float x, float z, float armSide)
    {
        float y = Y(x, z);
        props.Cylinder(new Vector3(x, y - 0.1f, z), 0.10f, 0.07f, 2.5f, 6, GrovePal.Bark, null, null, false, false);
        props.Box(new Vector3(x, y + 0.05f, z), new Vector3(0.36f, 0.12f, 0.36f), GrovePal.PlankDark);
        Vector3 top = new Vector3(x, y + 2.35f, z);
        Vector3 end = new Vector3(x + 0.55f * armSide, y + 2.35f, z);
        props.BoxBeam(top, end, 0.07f, 0.07f, GrovePal.Bark, 0.02f);
        props.BoxBeam(new Vector3(x, y + 1.9f, z), new Vector3(x + 0.38f * armSide, y + 2.3f, z), 0.045f, 0.045f, GrovePal.BarkDark, 0.02f);
        AddLantern(end + new Vector3(0f, -0.04f, 0f), true);
        Occupy(x, z, 0.8f);
    }

    void Signpost(float x, float z)
    {
        float y = Y(x, z);
        props.Cylinder(new Vector3(x, y - 0.1f, z), 0.09f, 0.07f, 2.05f, 6, GrovePal.Bark, new[] { GrovePal.WoodLight }, null, true, false);
        Vector2[] targets = { new Vector2(-4.6f, 2.4f), new Vector2(6.6f, 6.0f), new Vector2(4.0f, -0.7f) };   // пень, навес, телега
        for (int i = 0; i < targets.Length; i++)
        {
            Vector2 d = targets[i] - new Vector2(x, z);
            float yaw = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;                 // локальный +X смотрит на цель
            props.Push(new Vector3(x, y + 1.85f - i * 0.30f, z), Quaternion.Euler(0f, yaw, 0f));
            props.Box(new Vector3(0.42f, 0f, 0f), new Vector3(0.84f, 0.22f, 0.05f), i % 2 == 0 ? GrovePal.Plank : GrovePal.WoodLight, 0.04f);
            props.TriBoth(new Vector3(0.84f, 0.11f, 0f), new Vector3(1.0f, 0f, 0f), new Vector3(0.84f, -0.11f, 0f), i % 2 == 0 ? GrovePal.Plank : GrovePal.WoodLight);
            props.Pop();
        }
        Occupy(x, z, 0.8f);
    }

    // ---------------------------------------------------------------- пень лесоруба: E → щепки и реплика
    void MakeChopBlock(float x, float z, float topY)
    {
        var go = new GameObject("ChopBlock");
        go.SetActive(false);
        go.transform.SetParent(liveRoot, false);
        go.transform.localPosition = new Vector3(x, topY, z);
        go.AddComponent<GroveChopBlock>();
        go.SetActive(true);
    }
}

#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Modules/LocationMeadow.cs
// Мультяшная поляна: «карманы» (сад, сказочное кольцо, кристаллы, колодец, пикник, стоячие камни, пугало)
// и россыпи цветов. Места выбираются в стороне от троп, площадок, центра, реки и леса (ctx.distFlat).
// Бабочки, пыльца и трава делает LocationPolish.
using System.Collections.Generic;
using UnityEngine;

public sealed class LocationMeadow
{
    readonly LocationCtx c;
    readonly System.Random rng;
    readonly ToonKit k = new ToonKit();
    Transform node, cols, fx;
    LocBatcher bt;

    // кадр текущего «кармана»
    Vector2 org;
    Quaternion rot = Quaternion.identity;
    float gy;

    Material mStem, mRock, mMoss, mWood, mWoodL, mHay, mSoil, mMound, mRoof, mRed, mWhite, mPink, mBlue, mCobble,
        mWater, mDark, mYellow, mOrange, mBrown, mShirt, mShroomStem, mShroomCap, mShroomGlow, mFairy, mGlowDot;
    Material[] petals, crystals;

    LocationMeadow(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 17 + 909);
    }

    public static void Build(LocationCtx ctx) { new LocationMeadow(ctx).Run(); }

    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Color E(int i) { return c.s.envColors[i]; }

    void Run()
    {
        var go = new GameObject("Meadow");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(node, false);
        cols = cg.transform;
        var fg = new GameObject("Sparkles");
        fg.transform.SetParent(node, false);
        fx = fg.transform;
        bt = new LocBatcher(24f);

        Prep();
        var spots = PickSpots(c.s.meadowSpots);

        // виды карманов раздаём «мешками», чтобы подряд не повторялись
        var kinds = new List<int>();
        while (kinds.Count < Mathf.Max(7, spots.Count))
        {
            var bag = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            while (bag.Count > 0) { int j = rng.Next(bag.Count); kinds.Add(bag[j]); bag.RemoveAt(j); }
        }
        for (int i = 0; i < spots.Count; i++) Pocket(kinds[i], spots[i]);

        if (c.s.meadowFlowers > 0.01f) Sprinkles(spots);
        bt.Flush(node, "Meadow", true);
    }

    void Prep()
    {
        mStem = LocKit.Mat("MeadowStem", E(6), 0.05f);
        mRock = LocKit.Mat("MeadowRock", E(0), 0.05f);
        mMoss = LocKit.Mat("MeadowMoss", E(17), 0.05f);
        mWood = LocKit.Mat("MeadowWood", E(10), 0.05f);
        mWoodL = LocKit.Mat("MeadowWoodL", E(11), 0.05f);
        mHay = LocKit.Mat("MeadowHay", new Color(0.93f, 0.78f, 0.30f), 0.05f);
        mSoil = LocKit.Mat("MeadowSoil", new Color(0.38f, 0.25f, 0.15f), 0.05f);
        mMound = LocKit.Mat("MeadowMound", Color.Lerp(c.s.grassA, Color.white, 0.12f), 0.05f);
        mRoof = LocKit.Mat("MeadowRoof", new Color(0.78f, 0.30f, 0.25f), 0.1f);
        mRed = LocKit.Mat("MeadowRed", new Color(0.90f, 0.25f, 0.22f), 0.1f);
        mWhite = LocKit.Mat("MeadowWhite", E(7), 0.1f);
        mPink = LocKit.Mat("MeadowPink", new Color(0.98f, 0.55f, 0.75f), 0.1f);
        mBlue = LocKit.Mat("MeadowBlue", new Color(0.45f, 0.62f, 1f), 0.1f);
        mCobble = LocKit.Mat("MeadowCobble", E(23), 0.05f);
        mWater = LocKit.Mat("MeadowWater", new Color(0.30f, 0.65f, 0.90f), 0.6f);
        mDark = LocKit.Mat("MeadowDark", new Color(0.12f, 0.11f, 0.12f), 0.1f);
        mYellow = LocKit.Mat("MeadowYellow", E(9), 0.1f);
        mOrange = LocKit.Mat("MeadowOrange", new Color(0.98f, 0.55f, 0.15f), 0.1f);
        mBrown = LocKit.Mat("MeadowBrown", new Color(0.40f, 0.26f, 0.16f), 0.05f);
        mShirt = LocKit.Mat("MeadowShirt", new Color(0.30f, 0.50f, 0.85f), 0.05f);
        mShroomStem = LocKit.Mat("MeadowShroomStem", E(15), 0.1f);
        mShroomCap = LocKit.Mat("MeadowShroomCap", E(14), 0.3f);
        mShroomGlow = LocKit.Mat("MeadowShroomGlow", new Color(0.75f, 0.45f, 1f), 0.2f, 2f);
        mFairy = LocKit.Mat("MeadowFairyGlow", new Color(0.65f, 1f, 0.8f), 0.1f, 0.7f);
        petals = new[] { mWhite, mRed, mYellow, mPink, mBlue, LocKit.Mat("MeadowPurple", new Color(0.70f, 0.45f, 0.95f), 0.1f) };
        crystals = new[]
        {
            LocKit.Mat("MeadowCrystalA", new Color(0.30f, 0.90f, 1f), 0.6f, 1.6f),
            LocKit.Mat("MeadowCrystalB", new Color(0.95f, 0.40f, 0.95f), 0.6f, 1.6f),
            LocKit.Mat("MeadowCrystalC", new Color(0.55f, 0.50f, 1f), 0.6f, 1.6f)
        };
        mGlowDot = ToonKit.GlowMat("MeadowGlow", Color.white);
    }

    // ------------------------------------------------------------------ выбор мест

    // distFlat = расстояние до центра, троп и площадок (с учётом реального пути троп)
    bool Free(float x, float z, float extra)
    {
        float lim = c.half - 26f;
        if (Mathf.Abs(x) > lim || Mathf.Abs(z) > lim) return false;
        if (c.heightU(x, z) < -0.05f) return false;
        if (c.distFlat(x, z) < 2f + extra) return false;
        if (c.hasForest && (new Vector2(x, z) - c.forestPos).magnitude < c.forestRad + 3f + extra) return false;
        if (c.s.buildRiver && Mathf.Abs(x - c.riverX(z)) < c.riverWidth * 0.5f + c.riverBank + 3f + extra) return false;
        return true;
    }

    bool Flat(float x, float z, float r, float tol)
    {
        float h = c.heightU(x, z);
        return Mathf.Abs(c.heightU(x + r, z) - h) < tol && Mathf.Abs(c.heightU(x - r, z) - h) < tol &&
               Mathf.Abs(c.heightU(x, z + r) - h) < tol && Mathf.Abs(c.heightU(x, z - r) - h) < tol;
    }

    List<Vector2> PickSpots(int count)
    {
        var res = new List<Vector2>();
        float[] extras = { 4.5f, 2.5f };
        for (int pass = 0; pass < extras.Length && res.Count < count; pass++)
        {
            for (int t = 0; t < 6000 && res.Count < count; t++)
            {
                float x = Rf(-c.half, c.half), z = Rf(-c.half, c.half);
                if (!Free(x, z, extras[pass]) || !Flat(x, z, 3f, 0.6f)) continue;
                bool ok = true;
                foreach (var q in res)
                    if ((q - new Vector2(x, z)).magnitude < 11f) { ok = false; break; }
                if (ok) res.Add(new Vector2(x, z));
            }
        }
        if (res.Count < count) Debug.LogWarning("LocationMeadow: нашлось мест " + res.Count + " из " + count + " (мало свободной поляны).");
        return res;
    }

    // ------------------------------------------------------------------ кадр и примитивы

    void SetFrame(Vector2 p, float yawDeg)
    {
        org = p;
        rot = Quaternion.Euler(0f, yawDeg, 0f);
        gy = c.heightU(p.x, p.y);
    }

    Vector3 W(float lx, float ly, float lz)
    {
        Vector3 p = rot * new Vector3(lx, 0f, lz);
        return new Vector3(org.x + p.x, gy + ly, org.y + p.z);
    }

    void A(Mesh m, Material mat, float lx, float ly, float lz, float rx, float ry, float rz, float sx, float sy, float sz)
    {
        bt.Add(m, mat, W(lx, ly, lz), rot * Quaternion.Euler(rx, ry, rz), new Vector3(sx, sy, sz));
    }

    void Mound(float r)
    {
        A(k.cyl12, mMound, 0f, -0.2f, 0f, 0f, 0f, 0f, r * 2f, 0.5f, r * 2f);
    }

    void Flower(float lx, float lz, float ly, float h, Material petal)
    {
        A(k.cyl, mStem, lx, ly + h * 0.5f, lz, 0f, 0f, 0f, 0.07f, h, 0.07f);
        float by = ly + h;
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f;
            A(k.blob[i % 4], petal, lx + Mathf.Cos(a) * 0.17f, by, lz + Mathf.Sin(a) * 0.17f, 0f, 0f, 0f, 0.2f, 0.12f, 0.2f);
        }
        A(k.blob[1], mYellow, lx, by + 0.04f, lz, 0f, 0f, 0f, 0.17f, 0.14f, 0.17f);
    }

    void ColBox(float lx, float ly, float lz, float sx, float sy, float sz, float yaw)
    {
        var g = new GameObject("Col");
        g.transform.SetParent(cols, false);
        g.transform.localPosition = W(lx, ly, lz);
        g.transform.localRotation = rot * Quaternion.Euler(0f, yaw, 0f);
        g.AddComponent<BoxCollider>().size = new Vector3(sx, sy, sz);
    }

    // ------------------------------------------------------------------ карманы

    void Pocket(int kind, Vector2 p)
    {
        SetFrame(p, Rf(0f, 360f));
        switch (kind)
        {
            case 0: Garden(); break;
            case 1: FairyRing(); break;
            case 2: Crystals(); break;
            case 3: Well(); break;
            case 4: Picnic(); break;
            case 5: Stones(); break;
            default: Scarecrow(); break;
        }
    }

    void Garden()
    {
        Mound(4.6f);
        A(k.cyl12, mSoil, 0f, 0.12f, 0f, 0f, 0f, 0f, 6.4f, 0.3f, 6.4f);
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            A(k.blob[i % 4], mRock, Mathf.Cos(a) * 3.3f, 0.25f, Mathf.Sin(a) * 3.3f, 0f, Rf(0f, 360f), 0f, Rf(0.9f, 1.2f), 0.7f, Rf(0.8f, 1.1f));
        }
        for (int i = 0; i < 20; i++)
        {
            float a = Rf(0f, Mathf.PI * 2f), d = Mathf.Sqrt((float)rng.NextDouble()) * 2.7f;
            if (d < 0.7f) d += 0.7f;
            Flower(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.2f, Rf(0.5f, 1f), petals[rng.Next(petals.Length)]);
        }
        // большой подсолнух в центре
        A(k.cyl, mStem, 0f, 1.4f, 0f, 0f, 0f, 0f, 0.2f, 2.6f, 0.2f);
        A(k.blob[0], mYellow, 0f, 2.7f, 0.2f, 70f, 0f, 0f, 1.6f, 0.35f, 1.6f);
        A(k.blob[1], mBrown, 0f, 2.75f, 0.42f, 70f, 0f, 0f, 0.85f, 0.4f, 0.85f);
        A(k.blob[2], mStem, 0.5f, 1.2f, 0f, 0f, 0f, 30f, 0.9f, 0.12f, 0.5f);
        A(k.blob[3], mStem, -0.5f, 1.5f, 0f, 0f, 0f, -30f, 0.9f, 0.12f, 0.5f);
        ColBox(0f, 0.6f, 0f, 6.2f, 1.2f, 6.2f, 0f);
    }

    void FairyRing()
    {
        Mound(4.4f);
        A(k.cyl12, mFairy, 0f, 0.08f, 0f, 0f, 0f, 0f, 5.4f, 0.05f, 5.4f);
        const int n = 12;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f + Rf(-0.1f, 0.1f);
            k.Shroom(bt, W(Mathf.Cos(a) * 3.3f, 0.05f, Mathf.Sin(a) * 3.3f), Rf(0.5f, 0.95f), mShroomStem, mShroomCap, mWhite, rng, true);
        }
        k.Shroom(bt, W(0f, 0.05f, 0f), 1.5f, mShroomStem, mShroomGlow, null, rng, false);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + 0.5f;
            k.Shroom(bt, W(Mathf.Cos(a) * 1f, 0.05f, Mathf.Sin(a) * 1f), Rf(0.4f, 0.6f), mShroomStem, mShroomGlow, null, rng, false);
        }
        if (c.s.meadowLife)
            ToonKit.MakeGlow(fx, "FairySparkles", W(0f, 1.4f, 0f), new Vector3(5.5f, 2.2f, 5.5f),
                new Color(0.8f, 0.6f, 1f, 1f), new Color(0.6f, 1f, 0.8f, 1f), 0.35f, 18, 5f, 0.1f, mGlowDot);
    }

    void Crystals()
    {
        Mound(3.8f);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * Mathf.PI * 2f + Rf(0f, 1f);
            A(k.blob[i % 4], mRock, Mathf.Cos(a) * 1.8f, 0.3f, Mathf.Sin(a) * 1.8f, 0f, Rf(0f, 360f), 0f, 1.6f, 1f, 1.4f);
        }
        A(k.octa, crystals[0], 0f, 1.7f, 0f, 0f, Rf(0f, 90f), 6f, 1.1f, 4.2f, 1.1f);
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + Rf(-0.3f, 0.3f), d = Rf(0.9f, 1.5f), h = Rf(2f, 3.4f);
            A(k.octa, crystals[(i + 1) % 3], Mathf.Cos(a) * d, h * 0.42f, Mathf.Sin(a) * d,
                Mathf.Sin(a) * 16f, Rf(0f, 90f), -Mathf.Cos(a) * 16f, Rf(0.6f, 0.9f), h, Rf(0.6f, 0.9f));
        }
        for (int i = 0; i < 6; i++)
        {
            float a = Rf(0f, Mathf.PI * 2f), d = Rf(1.6f, 2.5f);
            A(k.octa, crystals[i % 3], Mathf.Cos(a) * d, 0.3f, Mathf.Sin(a) * d, Rf(-10f, 10f), Rf(0f, 90f), Rf(-10f, 10f), 0.28f, 0.7f, 0.28f);
        }
        ColBox(0f, 1.5f, 0f, 2.6f, 3f, 2.6f, 0f);

        var lg = new GameObject("CrystalLight");
        lg.transform.SetParent(node, false);
        lg.transform.localPosition = W(0f, 2.2f, 0f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.55f, 0.85f, 1f);
        l.range = 8f * c.ws;
        l.intensity = 1.6f;
        l.shadows = LightShadows.None;
        if (c.s.meadowLife)
            ToonKit.MakeGlow(fx, "CrystalSparkles", W(0f, 1.8f, 0f), new Vector3(3.5f, 3f, 3.5f),
                new Color(0.4f, 0.95f, 1f, 1f), new Color(0.9f, 0.5f, 1f, 1f), 0.3f, 14, 4f, 0.1f, mGlowDot);
    }

    void Well()
    {
        Mound(3.4f);
        A(k.cyl12, mCobble, 0f, 0.55f, 0f, 0f, 0f, 0f, 2.6f, 1.2f, 2.6f);
        A(k.cyl12, mWater, 0f, 1.12f, 0f, 0f, 0f, 0f, 1.8f, 0.1f, 1.8f);
        for (int s = -1; s <= 1; s += 2) A(k.box, mWood, s * 1.15f, 1.9f, 0f, 0f, 0f, 0f, 0.24f, 2.7f, 0.24f);
        A(k.cyl, mWoodL, 0f, 2.4f, 0f, 0f, 0f, 90f, 0.16f, 2.5f, 0.16f);
        A(k.cyl, mWoodL, 0f, 1.95f, 0f, 0f, 0f, 0f, 0.03f, 0.9f, 0.03f);
        A(k.cyl, mWood, 0f, 1.35f, 0f, 0f, 0f, 0f, 0.5f, 0.4f, 0.5f);
        A(k.box, mRoof, 0f, 3.2f, 0.85f, 33f, 0f, 0f, 3.3f, 0.14f, 1.9f);
        A(k.box, mRoof, 0f, 3.2f, -0.85f, -33f, 0f, 0f, 3.3f, 0.14f, 1.9f);
        for (int i = 0; i < 4; i++)
            Flower(1.7f + Rf(-0.2f, 0.4f), -1.2f + i * 0.7f, 0f, Rf(0.5f, 0.9f), petals[rng.Next(petals.Length)]);
        ColBox(0f, 0.7f, 0f, 2.7f, 1.4f, 2.7f, 0f);
    }

    void Picnic()
    {
        Mound(3.6f);
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                A(k.box, (i + j) % 2 == 0 ? mRed : mWhite, -1.5f + i, 0.06f, -1f + j, 0f, 0f, 0f, 1f, 0.07f, 1f);
        A(k.box, mWoodL, -0.7f, 0.38f, 0.3f, 0f, 15f, 0f, 0.85f, 0.55f, 0.6f);
        A(k.cyl, mWood, -0.7f, 0.78f, 0.3f, 0f, 15f, 90f, 0.05f, 0.7f, 0.05f);
        A(k.blob[0], mRed, -0.2f, 0.28f, 0.8f, 0f, 0f, 0f, 0.28f, 0.28f, 0.28f);
        A(k.blob[1], mRed, 0.1f, 0.28f, 0.6f, 0f, 0f, 0f, 0.28f, 0.28f, 0.28f);
        A(k.cyl12, mHay, 0.6f, 0.16f, -0.4f, 0f, 0f, 0f, 0.6f, 0.15f, 0.6f);
        A(k.cyl, mWhite, 0.2f, 0.18f, -0.7f, 0f, 0f, 0f, 0.22f, 0.2f, 0.22f);
        A(k.blob[2], mPink, 1.0f, 0.22f, 0.7f, 0f, 20f, 0f, 0.9f, 0.35f, 0.7f);
        Quaternion tq = Quaternion.Euler(0f, 0f, 8f);
        Vector3 b = new Vector3(1.9f, 0f, -0.6f);
        Vector3 p0 = b + tq * new Vector3(0f, 1.4f, 0f), p1 = b + tq * new Vector3(0f, 2.95f, 0f), p2 = b + tq * new Vector3(0f, 3.5f, 0f);
        A(k.cyl, mWood, p0.x, p0.y, p0.z, 0f, 0f, 8f, 0.09f, 2.8f, 0.09f);
        A(k.cone, mPink, p1.x, p1.y, p1.z, 0f, 0f, 8f, 3.4f, 1f, 3.4f);
        A(k.blob[0], mWhite, p2.x, p2.y, p2.z, 0f, 0f, 0f, 0.3f, 0.3f, 0.3f);
        for (int i = 0; i < 5; i++)
            Flower(Rf(-2.8f, 2.8f), Rf(-2.4f, 2.4f) + (i % 2 == 0 ? 2.2f : -2.2f), 0f, Rf(0.5f, 0.9f), petals[rng.Next(petals.Length)]);
    }

    void Stones()
    {
        Mound(5f);
        A(k.cyl12, mDark, 0f, 0.07f, 0f, 0f, 0f, 0f, 7.4f, 0.04f, 7.4f);
        const int n = 7;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f, h = Rf(2.2f, 3.6f);
            float x = Mathf.Cos(a) * 3.6f, z = Mathf.Sin(a) * 3.6f;
            float yaw = -a * Mathf.Rad2Deg + 90f;
            A(k.box, mRock, x, h * 0.5f, z, Rf(-4f, 4f), yaw, Rf(-4f, 4f), 0.95f, h, 0.65f);
            A(k.blob[i % 4], mMoss, x, h, z, 0f, yaw, 0f, 1f, 0.42f, 0.75f);
            ColBox(x, h * 0.5f, z, 0.95f, h, 0.65f, yaw);
        }
        A(k.box, mRock, 0f, 0.25f, 0f, 0f, Rf(0f, 90f), 0f, 1.6f, 0.45f, 1.6f);
        A(k.octa, crystals[0], 0f, 1.7f, 0f, 0f, 20f, 0f, 0.5f, 0.9f, 0.5f);
        var lg = new GameObject("RuneLight");
        lg.transform.SetParent(node, false);
        lg.transform.localPosition = W(0f, 1.9f, 0f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.4f, 0.9f, 1f);
        l.range = 6f * c.ws;
        l.intensity = 1.1f;
        l.shadows = LightShadows.None;
    }

    void Scarecrow()
    {
        Mound(3.4f);
        A(k.cyl, mWood, 0f, 1.4f, 0f, 0f, 0f, 0f, 0.14f, 2.8f, 0.14f);
        A(k.box, mWood, 0f, 2.0f, 0f, 0f, 0f, 0f, 2.3f, 0.14f, 0.14f);
        A(k.box, mShirt, 0f, 1.55f, 0f, 0f, 0f, 0f, 0.9f, 1f, 0.5f);
        A(k.blob[0], mHay, 0f, 3f, 0f, 0f, 0f, 0f, 0.75f, 0.75f, 0.75f);
        for (int s = -1; s <= 1; s += 2)
        {
            A(k.blob[1], mDark, s * 0.17f, 3.08f, 0.33f, 0f, 0f, 0f, 0.1f, 0.1f, 0.1f);
            A(k.cone, mHay, s * 1.2f, 2f, 0f, 0f, 0f, -s * 90f, 0.35f, 0.6f, 0.35f);
            A(k.cone, mHay, s * 0.25f, 0.95f, 0f, 180f, 0f, 0f, 0.3f, 0.5f, 0.3f);
        }
        A(k.cone, mOrange, 0f, 2.98f, 0.42f, 90f, 0f, 0f, 0.12f, 0.35f, 0.12f);
        A(k.cyl, mBrown, 0f, 3.28f, 0f, 0f, 0f, 0f, 1f, 0.07f, 1f);
        A(k.cone, mBrown, 0f, 3.62f, 0f, 0f, 0f, 0f, 0.65f, 0.75f, 0.65f);
        // ворона на перекладине
        A(k.blob[2], mDark, 0.9f, 2.28f, 0f, 0f, 0f, 0f, 0.38f, 0.3f, 0.5f);
        A(k.blob[3], mDark, 0.9f, 2.45f, 0.25f, 0f, 0f, 0f, 0.2f, 0.2f, 0.2f);
        A(k.cone, mOrange, 0.9f, 2.43f, 0.4f, 90f, 0f, 0f, 0.06f, 0.14f, 0.06f);
        // тюки сена
        A(k.cyl, mHay, 2.2f, 0.55f, 1f, 0f, 0f, 90f, 1.1f, 1.3f, 1.1f);
        A(k.cyl, mHay, 2.6f, 0.55f, -0.4f, 0f, 20f, 90f, 1.1f, 1.3f, 1.1f);
        A(k.cyl, mHay, 2.4f, 1.5f, 0.3f, 0f, 70f, 90f, 1.1f, 1.3f, 1.1f);
        ColBox(0f, 1.4f, 0f, 0.5f, 2.8f, 0.5f, 0f);
        ColBox(2.4f, 0.9f, 0.3f, 1.4f, 1.8f, 2.2f, 0f);
    }

    // ------------------------------------------------------------------ россыпи цветов

    void Sprinkles(List<Vector2> spots)
    {
        int clusters = Mathf.RoundToInt(26f * c.s.meadowFlowers);
        for (int t = 0, placed = 0; t < 1500 && placed < clusters; t++)
        {
            float x = Rf(-c.half, c.half), z = Rf(-c.half, c.half);
            if (!Free(x, z, 0.5f)) continue;
            bool near = false;
            foreach (var q in spots)
                if ((q - new Vector2(x, z)).magnitude < 6f) { near = true; break; }
            if (near) continue;
            SetFrame(new Vector2(x, z), 0f);
            Material pm = petals[rng.Next(petals.Length)];
            int n = rng.Next(3, 7);
            for (int i = 0; i < n; i++)
                Flower(Rf(-1.2f, 1.2f), Rf(-1.2f, 1.2f), 0f, Rf(0.4f, 0.8f), rng.NextDouble() < 0.75 ? pm : petals[rng.Next(petals.Length)]);
            placed++;
        }
    }
}
#endif
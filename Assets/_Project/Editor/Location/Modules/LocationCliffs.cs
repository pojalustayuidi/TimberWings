#if UNITY_EDITOR
// =====================================================================================
//  LocationExtras.cs  ->  Assets/Editor/LocationExtras.cs
//  LocationCliffs - большие слоистые скалы-барьеры по периметру (углы, ворота у моста)
//  Центр теперь в отдельном файле LocationCenter.cs.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

// ------------------------------------------------------------------------------------
public sealed class LocationCliffs
{
    readonly LocationCtx c;
    readonly System.Random rng;
    LocBatcher bt;
    Transform node, cols;
    Mesh[] chunk, rock;
    Mesh spire;
    Material[] strata;
    Material moss;
    float half;

    LocationCliffs(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 13 + 77);
    }

    public static void Build(LocationCtx ctx) { new LocationCliffs(ctx).Run(); }

    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Color E(int i) { return c.s.envColors[i]; }

    void Run()
    {
        var go = new GameObject("Cliffs");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(node, false);
        cols = cg.transform;
        bt = new LocBatcher(24f);
        half = c.half;
        Prep();

        float dens = Mathf.Max(0.3f, c.s.cliffDensity);

        // внешний ряд (крупные) и внутренний ряд (средние) вдоль каждой стороны
        for (int side = 0; side < 4; side++)
        {
            float t = -half + Rf(0f, 3f);
            while (t < half)
            {
                float w = Rf(7.5f, 12f);
                Vector2 p = EdgePoint(side, t, Rf(0f, 2.2f));
                if (Ok(p.x, p.y, w, 0)) Formation(p.x, p.y, w, Hgt(p.x, p.y, w, 0, side), 0);
                t += w * Rf(0.5f, 0.68f) / dens;
            }
            t = -half + Rf(2f, 6f);
            while (t < half)
            {
                float w = Rf(5f, 8f);
                Vector2 p = EdgePoint(side, t, Rf(6.5f, 10f));
                if (Ok(p.x, p.y, w, 1)) Formation(p.x, p.y, w, Hgt(p.x, p.y, w, 1, side), 1);
                t += w * Rf(0.8f, 1.1f) / dens;
            }
        }

        // угловые монолиты
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float x = sx * (half - 5f), z = sz * (half - 5f);
                if (c.heightU(x, z) < -0.12f) continue;
                Formation(x, z, 15f, 30f * c.s.cliffHeight * (sz < 0 ? 0.8f : 1f), 0);
            }

        // «ворота» у моста: две высокие скалы по бокам проёма (проход на следующую локацию)
        if (c.hasBridge)
        {
            float gx = c.bridgeSide * (half - 3.5f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float gz = c.bridgePos.y + sgn * 11f;
                if (c.heightU(gx, gz) < -0.12f) continue;
                Formation(gx, gz, 11f, 26f * c.s.cliffHeight, 0);
            }
        }

        // предгорья: кучки валунов глубже внутрь
        int clusters = Mathf.RoundToInt(36f * dens);
        for (int k = 0, placed = 0; k < 800 && placed < clusters; k++)
        {
            float x = Rf(-half + 2f, half - 2f), z = Rf(-half + 2f, half - 2f);
            float edge = half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            if (edge < 9f || edge > 22f) continue;
            if (!Ok(x, z, 3f, 1)) continue;
            int n = rng.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), d = Rf(0f, 3f);
                float bx = x + Mathf.Cos(a) * d, bz = z + Mathf.Sin(a) * d;
                if (c.heightU(bx, bz) < -0.12f || c.distFlat(bx, bz) < 2.5f) continue;
                Boulder(bx, bz, Rf(1.6f, 3.6f));
            }
            placed++;
        }

        bt.Flush(node, "Cliff", true);
    }

    void Prep()
    {
        chunk = new Mesh[4];
        for (int i = 0; i < chunk.Length; i++) chunk[i] = MakeChunk("CliffChunk_" + i, i + 1, 6 + (i % 3), 0.58f + 0.07f * i);
        rock = new Mesh[3];
        for (int i = 0; i < rock.Length; i++) rock[i] = LocKit.Ico("CliffRock_" + i, i + 11, LocKit.RockDeform);
        spire = LocKit.Prism("CliffSpire", 5, 0.5f, 0f);
        strata = new[]
        {
            LocKit.Mat("CliffA", E(16), 0.05f),
            LocKit.Mat("CliffB", E(1), 0.05f),
            LocKit.Mat("CliffC", E(0), 0.05f),
            LocKit.Mat("CliffD", E(2), 0.05f),
            LocKit.Mat("CliffE", E(22), 0.05f)
        };
        moss = LocKit.Mat("CliffMoss", E(17), 0.05f);
    }

    // граненый «столб»: 3 кольца вершин с шумом, плоские грани
    static Mesh MakeChunk(string name, int seed, int sides, float topScale)
    {
        var r = new System.Random(seed * 911 + 13);
        var mb = new LocMB();
        int rings = 3;
        var pts = new Vector3[rings, sides];
        float[] ys = { -0.5f, 0.05f, 0.5f };
        float[] rs = { 0.5f, 0.5f * Mathf.Lerp(1f, topScale, 0.5f) * 1.04f, 0.5f * topScale };
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                float a = (i + (float)r.NextDouble() * 0.5f - 0.25f) / sides * Mathf.PI * 2f;
                float rad = rs[k] * (0.82f + (float)r.NextDouble() * 0.36f);
                float y = ys[k] + (k == 0 ? 0f : ((float)r.NextDouble() - 0.5f) * 0.14f);
                pts[k, i] = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
            }
        Vector3 topC = new Vector3(0f, 0.5f + (float)r.NextDouble() * 0.12f, 0f);
        Vector3 botC = new Vector3(0f, -0.5f, 0f);
        for (int k = 0; k < rings - 1; k++)
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 a = pts[k, i], b = pts[k, j], c2 = pts[k + 1, j], d = pts[k + 1, i];
                Vector3 o = new Vector3(a.x + b.x + c2.x + d.x, 0f, a.z + b.z + c2.z + d.z);
                mb.Tri(a, b, c2, o);
                mb.Tri(a, c2, d, o);
            }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            mb.Tri(topC, pts[2, i], pts[2, j], Vector3.up);
            mb.Tri(botC, pts[0, j], pts[0, i], Vector3.down);
        }
        return mb.ToMesh(name);
    }

    Vector2 EdgePoint(int side, float t, float inset)
    {
        switch (side)
        {
            case 0: return new Vector2(t, half - inset);
            case 1: return new Vector2(t, -half + inset);
            case 2: return new Vector2(half - inset, t);
            default: return new Vector2(-half + inset, t);
        }
    }

    float Hgt(float x, float z, float w, int tier, int side)
    {
        float y0 = c.heightU(x, z);
        float deficit = Mathf.Clamp(c.maxHeight * 0.7f - y0, 0f, 9f);
        float h = (tier == 0 ? w * Rf(1.15f, 1.65f) : w * Rf(0.8f, 1.2f)) + deficit * 0.5f;
        if (side == 1) h *= 0.75f;   // южный край ниже, чтобы не закрывать обзор камеры
        return h * c.s.cliffHeight;
    }

    bool Ok(float x, float z, float w, int tier)
    {
        float r = w * 0.4f;
        if (c.heightU(x, z) < -0.12f || c.heightU(x + r, z) < -0.12f || c.heightU(x - r, z) < -0.12f ||
            c.heightU(x, z + r) < -0.12f || c.heightU(x, z - r) < -0.12f) return false;
        float margin = tier == 0 ? 2.2f + w * 0.15f : 4.5f + w * 0.3f;
        if (c.distFlat(x, z) < margin) return false;
        if (c.inGlade(x, z, tier == 0 ? -1f : -3f)) return false;
        // проём у моста оставляем свободным
        if (c.hasBridge && Mathf.Abs(z - c.bridgePos.y) < 9f + w * 0.3f && (x - c.bridgePos.x) * c.bridgeSide > -2f) return false;
        return true;
    }

    void Boulder(float x, float z, float size)
    {
        float y = c.heightU(x, z);
        Vector3 sc = new Vector3(size * Rf(1.1f, 1.5f), size * Rf(0.7f, 1.0f), size * Rf(1.0f, 1.3f));
        Quaternion q = Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f));
        int v = rng.Next(rock.Length);
        bt.Add(rock[v], strata[rng.Next(strata.Length)], new Vector3(x, y + sc.y * 0.3f, z), q, sc);
        if (size > 2f && rng.NextDouble() < 0.6)
            bt.Add(rock[v], moss, new Vector3(x, y + sc.y * 0.52f, z), Quaternion.Euler(0f, Rf(0f, 360f), 0f),
                new Vector3(sc.x * 0.66f, sc.y * 0.28f, sc.z * 0.66f));
    }

    void Formation(float x, float z, float w, float h, int tier)
    {
        float y0 = c.heightU(x, z) - 0.8f;
        int layers = tier == 0 ? rng.Next(3, 5) : rng.Next(2, 4);
        float layerH = h / layers;
        float cx = x, cz = z, cw = w, y = y0;
        int shift = rng.Next(strata.Length);
        float bigYaw = Rf(0f, 360f);

        for (int i = 0; i < layers; i++)
        {
            float lh = layerH * Rf(1.0f, 1.3f);
            Quaternion q = Quaternion.Euler(Rf(-3f, 3f), bigYaw + Rf(-40f, 40f) + i * 25f, Rf(-3f, 3f));
            Vector3 sc = new Vector3(cw * Rf(1.0f, 1.18f), lh, cw * Rf(0.95f, 1.12f));
            bt.Add(chunk[rng.Next(chunk.Length)], strata[(shift + i) % strata.Length],
                new Vector3(cx, y + lh * 0.5f, cz), q, sc);

            // выступ-полка сбоку
            if (i < layers - 1 && rng.NextDouble() < 0.55)
            {
                float a = Rf(0f, Mathf.PI * 2f), d = cw * Rf(0.45f, 0.6f);
                float ls = cw * Rf(0.35f, 0.5f);
                bt.Add(chunk[rng.Next(chunk.Length)], strata[(shift + i + 1) % strata.Length],
                    new Vector3(cx + Mathf.Cos(a) * d, y + lh * Rf(0.3f, 0.6f), cz + Mathf.Sin(a) * d),
                    Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f)),
                    new Vector3(ls * 1.2f, ls * Rf(0.8f, 1.3f), ls));
            }
            // пятно мха на боку слоя
            if (rng.NextDouble() < 0.4)
            {
                float a = Rf(0f, Mathf.PI * 2f), d = cw * 0.38f;
                bt.Add(rock[rng.Next(rock.Length)], moss,
                    new Vector3(cx + Mathf.Cos(a) * d, y + lh * 0.7f, cz + Mathf.Sin(a) * d),
                    Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(cw * 0.34f, lh * 0.22f, cw * 0.34f));
            }

            y += lh * 0.86f;
            cx += Rf(-0.1f, 0.1f) * cw;
            cz += Rf(-0.1f, 0.1f) * cw;
            cw *= Rf(0.74f, 0.88f);
        }

        float topY = y + layerH * 0.15f;
        // моховая шапка
        bt.Add(rock[rng.Next(rock.Length)], moss, new Vector3(cx, topY, cz), Quaternion.Euler(0f, Rf(0f, 360f), 0f),
            new Vector3(cw * 1.35f, cw * 0.5f, cw * 1.35f));
        // шпили
        int sp = tier == 0 ? rng.Next(1, 4) : rng.Next(0, 2);
        for (int k = 0; k < sp; k++)
        {
            float sh = h * Rf(0.3f, 0.55f);
            bt.Add(spire, strata[(shift + k + 1) % strata.Length],
                new Vector3(cx + Rf(-0.3f, 0.3f) * cw, topY + sh * 0.3f, cz + Rf(-0.3f, 0.3f) * cw),
                Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f)),
                new Vector3(cw * Rf(0.5f, 0.85f), sh, cw * Rf(0.5f, 0.85f)));
        }
        // валуны у подножия
        int nb = tier == 0 ? rng.Next(3, 6) : rng.Next(2, 4);
        for (int k = 0; k < nb; k++)
        {
            float a = Rf(0f, Mathf.PI * 2f), d = w * Rf(0.55f, 0.8f);
            float bx = x + Mathf.Cos(a) * d, bz = z + Mathf.Sin(a) * d;
            if (c.heightU(bx, bz) < -0.12f || c.distFlat(bx, bz) < 2f) continue;
            Boulder(bx, bz, w * Rf(0.18f, 0.32f));
        }

        // коллайдер
        var col = new GameObject("Col");
        col.transform.SetParent(cols, false);
        col.transform.localPosition = new Vector3(x, y0 + 0.8f + h * 0.5f, z);
        col.transform.localRotation = Quaternion.Euler(0f, bigYaw, 0f);
        col.AddComponent<BoxCollider>().size = new Vector3(w * 0.95f, h, w * 0.95f);
    }
}
#endif
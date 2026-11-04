// TIMBER WINGS / ISL-004 / release-candidate visual overlay.
// Editor-only deterministic mesh baking. Absolutely no colliders, cameras, lights or gameplay scripts.
// Only creates geometry; all placement decisions derive from the active IslandField and IslandLayout.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWIslandReleaseGeometryV2
    {
        internal sealed class Bake
        {
            internal Mesh edgeStone, meadow, woodland, pond, driftwood, accents;
            internal int triangles;
            internal string statistics;
        }
        internal struct Avoid
        {
            public Vector2 p;
            public float radius;
            public Avoid(Vector2 pos, float r) { p = pos; radius = r; }
        }
        sealed class Geo
        {
            readonly List<Vector3> vs = new List<Vector3>(24000);
            readonly List<Vector3> ns = new List<Vector3>(24000);
            readonly List<Color> cs = new List<Color>(24000);
            readonly List<Vector2> uv = new List<Vector2>(24000);
            readonly List<int> ix = new List<int>(36000);
            internal int Tris { get { return ix.Count / 3; } }
            internal void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                int i = vs.Count;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
                vs.Add(a); vs.Add(b); vs.Add(c);
                ns.Add(normal); ns.Add(normal); ns.Add(normal);
                cs.Add(col); cs.Add(col); cs.Add(col);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
                ix.Add(i); ix.Add(i + 1); ix.Add(i + 2);
            }
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                Tri(a, b, c, col); Tri(a, c, d, col);
            }
            internal Mesh Mesh(string name)
            {
                if (vs.Count == 0) return null;
                var m = new Mesh { name = name };
                if (vs.Count >= 65535) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(vs); m.SetNormals(ns); m.SetColors(cs); m.SetUVs(0, uv);
                m.SetTriangles(ix, 0); m.RecalculateBounds(); return m;
            }
        }
        static Vector3 At(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        static float F(System.Random r, float min, float max) { return Mathf.Lerp(min, max, (float)r.NextDouble()); }
        static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, t); }
        static Vector2 Polar(float rad, float a) { return new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad); }

        // Low-poly clustered stone: solid, distinctly faceted silhouette with deterministic surface light.
        static void Boulder(Geo g, Vector3 p, float sx, float sy, float sz, Color color, System.Random rng)
        {
            const int n = 9;
            Vector3[,] ring = new Vector3[3, n];
            float[] heights = { -0.42f, 0.12f, 0.47f };
            float[] radial = { 0.82f, 1.00f, 0.48f };
            float angleOffset = F(rng, 0f, 6.283185f);
            for (int y = 0; y < 3; y++)
                for (int i = 0; i < n; i++)
                {
                    float a = angleOffset + i * Mathf.PI * 2f / n;
                    float j = F(rng, 0.91f, 1.09f);
                    ring[y, i] = p + new Vector3(Mathf.Cos(a) * sx * 0.5f * radial[y] * j,
                                                heights[y] * sy + F(rng, -0.045f, 0.045f) * sy,
                                                Mathf.Sin(a) * sz * 0.5f * radial[y] * j);
                }
            for (int y = 0; y < 2; y++)
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Color tone = Mix(color, Color.white, F(rng, 0.00f, 0.10f));
                    g.Quad(ring[y, i], ring[y, j], ring[y + 1, j], ring[y + 1, i], tone);
                }
            Vector3 peak = p + Vector3.up * (0.52f * sy);
            for (int i = 0; i < n; i++)
                g.Tri(ring[2, i], ring[2, (i + 1) % n], peak, Mix(color, Color.white, 0.08f));
        }
        static void Stem(Geo g, Vector3 p, float height, float radius, Color c)
        {
            const int n = 5;
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n, b = 2f * Mathf.PI * (i + 1) / n;
                Vector3 o0 = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Vector3 o1 = new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius);
                g.Quad(p + o0, p + o1, p + o1 + Vector3.up * height, p + o0 + Vector3.up * height, c);
            }
        }
        static void Flower(Geo g, Vector3 p, float size, Color petals, Color green, System.Random rng)
        {
            float h = size * F(rng, 0.72f, 1.15f);
            Vector3 top = p + Vector3.up * h;
            Stem(g, p, h, size * 0.026f, green);
            float twist = F(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = Polar(size * 0.21f, twist + i * Mathf.PI * 2f / 5f);
                Vector2 side = new Vector2(-d.y, d.x) * 0.43f;
                Vector3 a = top + new Vector3(side.x, 0f, side.y);
                Vector3 b = top + new Vector3(d.x * 1.2f, size * 0.05f, d.y * 1.2f);
                Vector3 c = top - new Vector3(side.x, 0f, side.y);
                g.Tri(a, b, c, petals);
            }
            // Accent disk, leaves tilted toward ground. All art small enough not to require colliders.
            float s = size * 0.09f;
            g.Tri(top + Vector3.left * s, top + Vector3.forward * s, top + Vector3.right * s, new Color(0.98f, 0.75f, 0.33f, 1f));
            Vector3 mid = p + Vector3.up * (h * 0.28f);
            g.Tri(mid, mid + Vector3.left * size * 0.30f + Vector3.up * size * 0.03f,
                  mid + Vector3.back * size * 0.14f, green);
        }
        static void Mushroom(Geo g, Vector3 p, float size, Color cap, System.Random rng)
        {
            float stemH = size * F(rng, 0.60f, 0.90f);
            Stem(g, p, stemH, size * 0.10f, new Color(0.88f, 0.83f, 0.69f, 1f));
            const int n = 7;
            float twist = F(rng, 0f, Mathf.PI);
            Vector3 crown = p + Vector3.up * (stemH + size * 0.37f);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Polar(size * 0.52f, twist + Mathf.PI * 2f * i / n);
                Vector2 b = Polar(size * 0.52f, twist + Mathf.PI * 2f * (i + 1) / n);
                g.Tri(At(new Vector2(p.x, p.z) + a, p.y + stemH),
                      At(new Vector2(p.x, p.z) + b, p.y + stemH), crown, Mix(cap, Color.white, (i % 2) * 0.08f));
            }
        }
        static void Reed(Geo g, Vector3 p, float size, Color green, System.Random rng)
        {
            float angle = F(rng, 0, 6.28318f);
            for (int j = 0; j < 5; j++)
            {
                Vector2 dir = Polar(1f, angle + j * Mathf.PI * 2f / 5f);
                float h = size * F(rng, 0.62f, 1.1f);
                Vector3 mid = p + new Vector3(dir.x * size * 0.12f, h * 0.65f, dir.y * size * 0.12f);
                Vector3 tip = p + new Vector3(dir.x * size * 0.31f, h, dir.y * size * 0.31f);
                Vector3 side = new Vector3(-dir.y, 0f, dir.x) * size * 0.075f;
                g.Tri(p - side, mid, tip, green); g.Tri(p + side, tip, mid, green);
            }
        }
        static void Log(Geo g, Vector3 p, float length, float radius, float yaw, Color bark, Color cut)
        {
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            const int n = 7;
            Vector3[] a = new Vector3[n], b = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI * 2f * i / n;
                Vector3 radial = (Vector3.up * Mathf.Sin(ang) + right * Mathf.Cos(ang)) * radius;
                a[i] = p - fwd * length * 0.5f + radial;
                b[i] = p + fwd * length * 0.5f + radial;
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                g.Quad(a[i], a[j], b[j], b[i], bark);
                g.Tri(p - fwd * length * 0.5f, a[j], a[i], cut);
                g.Tri(p + fwd * length * 0.5f, b[i], b[j], cut);
            }
        }
        static bool FarFromAnchors(IslandField f, Vector2 p, float extra)
        {
            if (f.L.anchors == null) return true;
            foreach (var a in f.L.anchors)
            {
                Vector2 wp = f.AnchorWorld(a);
                float rad = Mathf.Max(6f, a.flatRadius + extra);
                if (Vector2.Distance(wp, p) < rad) return false;
            }
            return true;
        }
        static bool FarFromObstacles(List<Avoid> obstacles, Vector2 p, float extra)
        {
            for (int i = 0; i < obstacles.Count; i++)
                if ((obstacles[i].p - p).sqrMagnitude < (obstacles[i].radius + extra) * (obstacles[i].radius + extra))
                    return false;
            return true;
        }
        static bool DrySafe(IslandField f, Vector2 p, float edge, float water, float path,
                            List<Avoid> obstacles, float obstacleExtra, float anchorExtra)
        {
            return f.Inside(p.x, p.y) && f.EdgeDistance(p.x, p.y) > edge &&
                   f.WaterDistance(p.x, p.y) > water && f.PathDistance(p.x, p.y) > path &&
                   f.Slope(p.x, p.y) < 24f && f.FordWeight(p.x, p.y) < 0.01f &&
                   FarFromAnchors(f, p, anchorExtra) && FarFromObstacles(obstacles, p, obstacleExtra);
        }
        static bool SafePond(IslandField f, Vector2 p, List<Avoid> obstacles)
        {
            float d = f.PondValue(p.x, p.y);
            return d > 1.07f && d < 1.36f && f.EdgeDistance(p.x, p.y) > 4.0f &&
                   f.PathDistance(p.x, p.y) > 2.1f && f.Slope(p.x, p.y) < 25f &&
                   FarFromAnchors(f, p, 3f) && FarFromObstacles(obstacles, p, 0.65f);
        }

        internal static Bake Make(IslandField f, List<Avoid> obstacles)
        {
            var r = new System.Random(unchecked(f.L.seed * 19717 + 253));
            var stone = new Geo(); var flowers = new Geo(); var mushrooms = new Geo();
            var reeds = new Geo(); var woods = new Geo(); var focal = new Geo();
            var l = f.L;
            int stoneCount = 0, flowersCount = 0, mushroomCount = 0, reedCount = 0, woodCount = 0, accentCount = 0;

            // A. Ocean-facing rim: quiet clusters of large low-poly boulders, separated from paths/anchors.
            // Avoid the two river crossings, markers and the broken cloud bridge approach by using anchor/river/path clearances.
            for (int i = 0; i < f.Contour.Count; i += 4)
            {
                Vector2 onEdge = f.Contour[i];
                Vector2 inside = (l.centerOffset - onEdge).normalized;
                Vector2 p = onEdge + inside * F(r, 2.5f, 5.4f);
                if (!DrySafe(f, p, 1.7f, 4.3f, 4.6f, obstacles, 1.5f, 5f)) continue;
                if (F(r, 0f, 1f) > 0.62f) continue;
                float s = F(r, 2.5f, 4.8f);
                Boulder(stone, At(p, f.Height(p.x, p.y) + s * 0.22f), s, s * F(r, 0.5f, 0.80f), s * F(r, 0.7f, 1.2f),
                        Mix(l.rockMid, l.rockTop, F(r, 0.23f, 0.65f)), r);
                stoneCount++;
                Vector2 companion = p + Polar(s * 0.75f, F(r, 0f, 6.283185f));
                if (DrySafe(f, companion, 1.2f, 4f, 4f, obstacles, 1f, 4f))
                {
                    Boulder(stone, At(companion, f.Height(companion.x, companion.y) + 0.18f), s * 0.48f, s * 0.42f, s * 0.56f,
                            Mix(l.rockMid, l.cliffWet, 0.18f), r);
                    stoneCount++;
                }
            }

            // B. Meadow flowering corridors: playful but sparse pastel focal pockets, no busy path overlap.
            if (l.meadows != null)
            {
                foreach (var meadow in l.meadows)
                {
                    int target = Mathf.Clamp(Mathf.RoundToInt(meadow.radius * meadow.radius * 0.48f), 15, 85);
                    for (int i = 0; i < target; i++)
                    {
                        Vector2 p = meadow.pos + Polar(meadow.radius * Mathf.Sqrt(F(r, 0.03f, 0.93f)), F(r, 0f, 6.283185f));
                        if (!DrySafe(f, p, 4f, 4f, 1.15f, obstacles, 0.45f, 2f)) continue;
                        int count = 3 + r.Next(4);
                        Color petal = (i % 4 == 0) ? new Color(0.99f, 0.72f, 0.77f) :
                                      (i % 4 == 1) ? new Color(0.94f, 0.88f, 0.62f) :
                                      (i % 4 == 2) ? new Color(0.77f, 0.78f, 0.97f) :
                                                     new Color(0.99f, 0.95f, 0.82f);
                        for (int j = 0; j < count; j++)
                        {
                            Vector2 q = p + Polar(F(r, 0f, 0.75f), F(r, 0f, 6.283185f));
                            if (!DrySafe(f, q, 3.5f, 3.5f, 0.8f, obstacles, 0.28f, 1.5f)) continue;
                            Flower(flowers, At(q, f.Height(q.x, q.y) - 0.025f), F(r, 0.44f, 0.92f), petal,
                                   Mix(l.lipDark, l.lipTop, F(r, 0.3f, 0.72f)), r);
                            flowersCount++;
                        }
                    }
                }
            }
            // Additional tiny meadow splashes outside named meadows (bounded budget for release performance).
            for (int i = 0; i < 360 && flowersCount < 820; i++)
            {
                Vector2 p = new Vector2(F(r, f.Min.x, f.Max.x), F(r, f.Min.y, f.Max.y));
                if (!DrySafe(f, p, 6f, 5.5f, 1.3f, obstacles, 0.45f, 3f)) continue;
                if (Mathf.PerlinNoise(p.x * 0.085f + 23f, p.y * 0.085f + 77f) < 0.67f) continue;
                Color col = i % 3 == 0 ? new Color(0.98f, 0.75f, 0.80f) : i % 3 == 1 ?
                            new Color(0.89f, 0.82f, 0.99f) : new Color(0.99f, 0.94f, 0.76f);
                for (int j = 0; j < 3; j++)
                {
                    Vector2 q = p + Polar(F(r, 0.03f, 0.53f), F(r, 0f, 6.283185f));
                    if (!DrySafe(f, q, 5.7f, 5.0f, 1f, obstacles, 0.35f, 2f)) continue;
                    Flower(flowers, At(q, f.Height(q.x, q.y) - 0.04f), F(r, 0.42f, 0.8f), col, l.lipDark, r);
                    flowersCount++;
                }
            }

            // C. Woodland undergrowth: mushrooms staged under existing forest, safely away from interactions.
            for (int i = 0; i < 380 && mushroomCount < 230; i++)
            {
                Vector2 p = new Vector2(F(r, f.Min.x, f.Max.x), F(r, f.Min.y, f.Max.y));
                if (!DrySafe(f, p, 6.5f, 5f, 3f, obstacles, 1.1f, 4f)) continue;
                if (Mathf.PerlinNoise(p.x * 0.06f + 7.3f, p.y * 0.06f + 25.6f) < 0.56f) continue;
                int c = 2 + r.Next(3);
                for (int j = 0; j < c; j++)
                {
                    Vector2 q = p + Polar(F(r, 0f, 0.8f), F(r, 0f, 6.283185f));
                    if (!DrySafe(f, q, 6.0f, 4.8f, 2.8f, obstacles, 0.75f, 3.5f)) continue;
                    Color cap = (i + j) % 3 == 0 ? new Color(0.94f, 0.57f, 0.55f) :
                                (i + j) % 3 == 1 ? new Color(0.89f, 0.71f, 0.85f) :
                                                   new Color(0.93f, 0.79f, 0.56f);
                    Mushroom(mushrooms, At(q, f.Height(q.x, q.y)), F(r, 0.37f, 0.77f), cap, r);
                    mushroomCount++;
                }
            }

            // D. Pond: rhythmic reed tufts outside the water and footpaths (no lily pads floating at wrong height).
            for (int i = 0; i < 150; i++)
            {
                float a = i * (Mathf.PI * 2f / 150f);
                float offset = F(r, 1.12f, 1.29f);
                Vector2 p = l.pondPos + new Vector2(Mathf.Cos(a) * l.pondRadius.x * offset,
                                                      Mathf.Sin(a) * l.pondRadius.y * offset);
                if (!SafePond(f, p, obstacles) || F(r, 0f, 1f) > 0.82f) continue;
                Reed(reeds, At(p, f.Height(p.x, p.y) - 0.04f), F(r, 0.55f, 1.26f),
                     Mix(l.lipTop, l.lipDark, F(r, 0.1f, 0.5f)), r);
                reedCount++;
            }

            // E. Small woodcraft accents: weathered cut wood only away from important gameplay/work sites.
            for (int i = 0; i < 180 && woodCount < 26; i++)
            {
                Vector2 p = new Vector2(F(r, f.Min.x, f.Max.x), F(r, f.Min.y, f.Max.y));
                if (!DrySafe(f, p, 8.0f, 7.0f, 5.5f, obstacles, 3.0f, 7.0f)) continue;
                float len = F(r, 1.3f, 2.4f), radius = F(r, 0.18f, 0.34f);
                // Art object is visually low and off-path; no misleading interaction prompt or collision barrier.
                Log(woods, At(p, f.Height(p.x, p.y) + radius * 0.60f), len, radius, F(r, 0f, 6.283185f),
                    new Color(0.47f, 0.31f, 0.22f), new Color(0.83f, 0.66f, 0.42f));
                woodCount++;
            }

            // F. Forest-floor stone accents under flowers, subtle enough not to become walk-through boulders.
            for (int i = 0; i < 120 && accentCount < 58; i++)
            {
                Vector2 p = new Vector2(F(r, f.Min.x, f.Max.x), F(r, f.Min.y, f.Max.y));
                if (!DrySafe(f, p, 5.8f, 4.9f, 3.5f, obstacles, 0.95f, 3.2f)) continue;
                if (Mathf.PerlinNoise(p.x * 0.048f + 53f, p.y * 0.048f + 19f) < 0.55f) continue;
                float s = F(r, 0.7f, 1.35f);
                Boulder(focal, At(p, f.Height(p.x, p.y) + s * 0.08f), s, s * 0.60f, s,
                        Mix(l.rockTop, l.rockMid, F(r, 0.15f, 0.60f)), r);
                accentCount++;
            }

            var result = new Bake();
            result.edgeStone = stone.Mesh("TW_Release_OceanRimBoulders");
            result.meadow = flowers.Mesh("TW_Release_MeadowFlowers");
            result.woodland = mushrooms.Mesh("TW_Release_WoodlandMushrooms");
            result.pond = reeds.Mesh("TW_Release_PondReeds");
            result.driftwood = woods.Mesh("TW_Release_WoodcraftScenery");
            result.accents = focal.Mesh("TW_Release_LowRocks");
            result.triangles = stone.Tris + flowers.Tris + mushrooms.Tris + reeds.Tris + woods.Tris + focal.Tris;
            result.statistics = "edge rocks=" + stoneCount + "; flowers=" + flowersCount +
                                "; mushrooms=" + mushroomCount + "; reed tufts=" + reedCount +
                                "; forest logs=" + woodCount + "; small stones=" + accentCount +
                                "; triangles=" + result.triangles;
            return result;
        }
    }
}

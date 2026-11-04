// River Overhaul v0.2 / deterministic connected art pass. No terrain deformation, colliders or extra water volumes.
// Native IslandField supplies all coordinates/heights; native River_Water/River_Fall remain authoritative.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWRiverOverhaulGeometry
    {
        internal sealed class Result
        {
            public Mesh rocks, plants, foam, falls;
            public int triangles;
        }

        sealed class Faces
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public int TriangleCount { get { return tris.Count / 3; } }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color,
                                 Vector2 uvA, Vector2 uvB, Vector2 uvC, bool fixedUp = false)
            {
                Vector3 n = fixedUp ? Vector3.up : Vector3.Cross(b - a, c - a).normalized;
                if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
                int start = positions.Count;
                positions.Add(a); positions.Add(b); positions.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                colors.Add(color); colors.Add(color); colors.Add(color);
                uv.Add(uvA); uv.Add(uvB); uv.Add(uvC);
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            }
            public void QuadUp(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color near, Color far)
            {
                int k = positions.Count;
                positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
                for (int j = 0; j < 4; j++) normals.Add(Vector3.up);
                colors.Add(near); colors.Add(near); colors.Add(far); colors.Add(far);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }
            public void QuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color near, Color far)
            {
                Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
                int k = positions.Count;
                positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
                for (int j = 0; j < 4; j++) normals.Add(n);
                colors.Add(near); colors.Add(near); colors.Add(far); colors.Add(far);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k); tris.Add(k + 3); tris.Add(k + 2);
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (positions.Count >= 65535) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(positions);
                m.SetNormals(normals);
                m.SetColors(colors);
                m.SetUVs(0, uv);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Vector2 Side(Vector2 along) { return new Vector2(-along.y, along.x); }
        static Vector3 V(Vector2 xz, float y) { return new Vector3(xz.x, y, xz.y); }
        static float Hash(int a, int b, int seed)
        {
            uint x = (uint)(a * 1103515245 + b * 134775813 + seed * 4756439);
            x ^= x >> 16; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / 16777215f;
        }
        static Vector2 Rot(Vector2 p, float theta)
        {
            float s = Mathf.Sin(theta), c = Mathf.Cos(theta);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }
        static float AxisLength(List<Vector2> axis)
        {
            float s = 0f;
            for (int i = 1; i < axis.Count; i++) s += Vector2.Distance(axis[i - 1], axis[i]);
            return s;
        }
        static Vector2 OnAxis(List<Vector2> axis, float meters, out Vector2 forward)
        {
            float left = Mathf.Max(0f, meters);
            for (int i = 1; i < axis.Count; i++)
            {
                Vector2 a = axis[i - 1], b = axis[i], v = b - a;
                float len = v.magnitude;
                if (len < 1e-5f) continue;
                if (left <= len || i == axis.Count - 1)
                {
                    forward = v / len;
                    return a + v * Mathf.Clamp01(left / len);
                }
                left -= len;
            }
            forward = Vector2.up;
            return axis.Count > 0 ? axis[axis.Count - 1] : Vector2.zero;
        }

        static void Rock(Faces mesh, Vector3 center, Vector3 size, float yaw, Color shade, int id)
        {
            const int sides = 10;
            float[] y = { -0.5f, -0.30f, 0.24f, 0.48f };
            float[] rad = { 0.65f, 1.00f, 0.80f, 0.22f };
            var rings = new Vector3[y.Length, sides];
            for (int h = 0; h < y.Length; h++)
                for (int j = 0; j < sides; j++)
                {
                    float theta = j * Mathf.PI * 2f / sides;
                    float jitter = Mathf.Lerp(0.90f, 1.10f, Hash(j, h, id));
                    Vector2 p = Rot(new Vector2(Mathf.Cos(theta) * size.x * rad[h] * 0.5f * jitter,
                                                Mathf.Sin(theta) * size.z * rad[h] * 0.5f * jitter), yaw);
                    rings[h, j] = center + new Vector3(p.x, y[h] * size.y, p.y);
                }
            for (int h = 0; h < y.Length - 1; h++)
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    float tone = Mathf.Lerp(0.91f, 1.09f, Hash(j, h + 97, id));
                    Color col = new Color(Mathf.Clamp01(shade.r * tone), Mathf.Clamp01(shade.g * tone),
                                          Mathf.Clamp01(shade.b * tone), 1f);
                    mesh.Triangle(rings[h, j], rings[h + 1, j], rings[h, k], col, Vector2.zero, Vector2.zero, Vector2.zero);
                    mesh.Triangle(rings[h, k], rings[h + 1, j], rings[h + 1, k], col, Vector2.zero, Vector2.zero, Vector2.zero);
                }
            var roof = center + new Vector3(0f, size.y * 0.52f, 0f);
            for (int j = 0; j < sides; j++)
                mesh.Triangle(rings[y.Length - 1, j], roof, rings[y.Length - 1, (j + 1) % sides], shade,
                              Vector2.zero, Vector2.zero, Vector2.zero);
        }

        static void Plant(Faces mesh, Vector2 pos, float ground, float scale, Color green, int id)
        {
            Rock(mesh, new Vector3(pos.x, ground + scale * 0.37f, pos.y),
                new Vector3(scale * 1.3f, scale, scale * 1.1f), id * 0.23f, green, id);
            var side = new Vector2(Mathf.Cos(id), Mathf.Sin(id));
            Rock(mesh, V(pos + side * scale * 0.36f, ground + scale * 0.32f),
                new Vector3(scale * 0.72f, scale * 0.73f, scale * 0.72f), id * 0.8f,
                Color.Lerp(green, Color.white, 0.13f), id + 257);
        }

        static bool Eligible(IslandField f, Vector2 pos, float clearance, bool avoidPaths)
        {
            if (!f.Inside(pos.x, pos.y) || f.EdgeDistance(pos.x, pos.y) < clearance) return false;
            if (avoidPaths && f.PathDistance(pos.x, pos.y) < 4.6f) return false;
            if (f.FordWeight(pos.x, pos.y) > 0.05f) return false;
            if (f.L.crossings != null)
                foreach (var c in f.L.crossings)
                    if (Vector2.Distance(pos, c.pos) < 13f) return false;
            if (f.L.anchors != null)
                foreach (var a in f.L.anchors)
                {
                    Vector2 ap = f.AnchorWorld(a);
                    if (Vector2.Distance(pos, ap) < Mathf.Max(7f, a.flatRadius + 2f)) return false;
                }
            return true;
        }
        static bool NearCrossing(IslandLayout l, Vector2 pos, float radius)
        {
            if (l.crossings == null) return false;
            foreach (var c in l.crossings)
                if (Vector2.Distance(pos, c.pos) < radius) return true;
            return false;
        }
        static void FoamBand(Faces foam, IslandField f, Vector2 a, Vector2 b, float width, float fade0, float fade1)
        {
            Vector2 forward = (b - a).normalized;
            if (forward.sqrMagnitude < 1e-7f) return;
            Vector2 side = Side(forward);
            float ya = f.RiverSurfaceAt(a.x, a.y) + 0.085f;
            float yb = f.RiverSurfaceAt(b.x, b.y) + 0.085f;
            var white = new Color(1f, 1f, 1f, Mathf.Clamp01(fade0));
            var whiteEnd = new Color(1f, 1f, 1f, Mathf.Clamp01(fade1));
            foam.QuadUp(V(a - side * (width * 0.5f), ya), V(a + side * (width * 0.5f), ya),
                        V(b + side * (width * 0.5f), yb), V(b - side * (width * 0.5f), yb), white, whiteEnd);
        }
        static Color RockTone(IslandLayout l, int n)
        {
            return n % 4 == 0 ? l.rockTop : n % 4 == 1 ? l.rockMid :
                   n % 4 == 2 ? Color.Lerp(l.rockMid, l.rockTop, 0.38f) :
                                Color.Lerp(l.rockMid, l.cliffWet, 0.27f);
        }

        static void HeroBankCluster(Faces stone, Faces foam, IslandField f, Vector2 centre, Vector2 along, float half, int id)
        {
            Vector2 side = Side(along);
            for (int bank = -1; bank <= 1; bank += 2)
            {
                Vector2 p0 = centre + side * (bank * (half + 1.2f));
                Vector2 p1 = centre + side * (bank * (half + 2.6f)) + along * 0.8f;
                if (Eligible(f, p0, 1.2f, true))
                    Rock(stone, V(p0, f.Height(p0.x, p0.y) + 1.05f), new Vector3(3.6f, 3.3f, 4.0f), bank * 0.22f, RockTone(f.L, id + bank + 30), id + bank + 900);
                if (Eligible(f, p1, 1.2f, true))
                    Rock(stone, V(p1, f.Height(p1.x, p1.y) + 0.58f), new Vector3(2.4f, 1.9f, 2.7f), bank * 0.36f, RockTone(f.L, id + bank + 31), id + bank + 910);
            }
            FoamBand(foam, f, centre - along * 1.4f, centre + along * 1.4f, 0.52f, 0.22f, 0.02f);
        }

        static void BuildWaterfallVeil(Faces falls, Faces foam, IslandField f, IslandField.RiverEdge outlet, float half)
        {
            var l = f.L;
            Vector2 fallDir = outlet.outward.normalized;
            Vector2 edgeSide = Side(fallDir);
            Vector2 lip = outlet.pos;
            float lipY = f.RiverSurfaceAt(lip.x, lip.y) + 0.10f;
            float width = Mathf.Max(half * 0.55f, half * Mathf.Clamp(l.fallWidthShare, 0.45f, 1.2f));
            float drop = Mathf.Max(l.cliffWall + Mathf.Max(2.5f, l.fallExtra * 0.55f), 9f);
            float outerPush = Mathf.Max(0.65f, l.fallOffset + 0.45f);

            for (int layer = 0; layer < 2; layer++)
            {
                float layerWidth = layer == 0 ? width : width * 0.58f;
                float alphaTop = layer == 0 ? 0.28f : 0.48f;
                float alphaBottom = layer == 0 ? 0.07f : 0.16f;
                int strips = layer == 0 ? 8 : 6;
                for (int i = 0; i < strips; i++)
                {
                    float t0 = Mathf.Lerp(-1f, 1f, (float)i / strips);
                    float t1 = Mathf.Lerp(-1f, 1f, (float)(i + 1) / strips);
                    Vector2 topA = lip + edgeSide * (t0 * layerWidth) - fallDir * 0.12f;
                    Vector2 topB = lip + edgeSide * (t1 * layerWidth) - fallDir * 0.12f;
                    Vector2 botA = topA + fallDir * (outerPush + Mathf.Abs(t0) * 0.22f + layer * 0.12f);
                    Vector2 botB = topB + fallDir * (outerPush + Mathf.Abs(t1) * 0.22f + layer * 0.12f);
                    float yA = lipY - drop * (0.96f + Mathf.Abs(t0) * 0.04f);
                    float yB = lipY - drop * (0.96f + Mathf.Abs(t1) * 0.04f);
                    var topCol = new Color(1f, 1f, 1f, alphaTop);
                    var botCol = new Color(0.86f, 0.97f, 1f, alphaBottom);
                    falls.QuadFacing(V(topA, lipY), V(topB, lipY), V(botB, yB), V(botA, yA), topCol, botCol);
                }
            }

            // Crest foam right before the break.
            Vector2 before = lip - fallDir * 1.2f;
            Vector2 atLip = lip - fallDir * 0.15f;
            for (int i = -3; i <= 3; i++)
            {
                Vector2 off = edgeSide * (i * width * 0.20f);
                FoamBand(foam, f, before + off, atLip + off, 0.34f, 0.10f, 0.74f);
            }
        }

        internal static Result Make(IslandField f, IslandField.RiverEdge inlet, IslandField.RiverEdge outlet)
        {
            var l = f.L;
            var pieces = f.RiverInsidePieces();
            if (pieces.Count != 1 || pieces[0].Count < 2)
                throw new InvalidOperationException("Не один непрерывный участок RiverInsidePieces.");
            var axis = pieces[0];
            float riverLength = AxisLength(axis);
            float half = l.riverWidth * 0.5f;
            if (riverLength < 35f) throw new InvalidOperationException("Река короче 35м — грот и водопад нельзя безопасно размещать.");

            var stone = new Faces();
            var plants = new Faces();
            var foam = new Faces();
            var falls = new Faces();

            // 1. SOURCE: large silhouette with a recognisable rock grotto.
            Vector2 d;
            Vector2 heart = OnAxis(axis, Mathf.Min(8f, riverLength * 0.10f), out d);
            Vector2 sideAxis = Side(d);
            float waterY = f.RiverSurfaceAt(heart.x, heart.y);

            for (int bank = -1; bank <= 1; bank += 2)
            {
                Vector2 shoulder = heart + sideAxis * bank * (half + 2.2f);
                if (!Eligible(f, shoulder, 1.0f, false)) continue;
                float top = f.Height(shoulder.x, shoulder.y);
                Rock(stone, V(shoulder, top + 2.10f), new Vector3(6.4f, 7.0f, 6.6f), bank * 0.20f, RockTone(l, bank + 5), 100 + bank);
                Vector2 back = shoulder - d * 3.4f + sideAxis * bank * 1.4f;
                if (Eligible(f, back, 1f, false))
                    Rock(stone, V(back, f.Height(back.x, back.y) + 2.55f), new Vector3(6.8f, 8.2f, 6.0f), bank * 0.13f, RockTone(l, bank + 8), 110 + bank);
                Vector2 down = shoulder + d * 3.0f;
                if (Eligible(f, down, 1f, false))
                    Rock(stone, V(down, f.Height(down.x, down.y) + 1.28f), new Vector3(4.8f, 4.7f, 5.4f), bank * 0.31f, RockTone(l, bank + 10), 120 + bank);
                Vector2 mouthStone = heart + sideAxis * bank * (half + 0.62f) + d * 0.8f;
                if (Eligible(f, mouthStone, 0.8f, false))
                    Rock(stone, V(mouthStone, f.Height(mouthStone.x, mouthStone.y) + 0.68f), new Vector3(2.0f, 1.7f, 2.2f), bank * 0.45f, RockTone(l, bank + 15), 130 + bank);
            }
            for (int k = -2; k <= 2; k++)
            {
                Vector2 p = heart - d * (4.2f + Mathf.Abs(k) * 0.3f) + sideAxis * (k * 2.0f);
                if (!Eligible(f, p, 0.8f, false)) continue;
                Rock(stone, V(p, f.Height(p.x, p.y) + 1.8f + (k == 0 ? 0.7f : 0f)),
                     new Vector3(4.9f, 4.4f + (k == 0 ? 1.4f : 0f), 4.4f), k * 0.18f, RockTone(l, 150 + k), 150 + k);
            }
            float crown = Mathf.Max(waterY + 7.2f, f.Height(heart.x + sideAxis.x * (half + 2f), heart.y + sideAxis.y * (half + 2f)) + 4.0f);
            for (int k = -3; k <= 3; k++)
            {
                if (Mathf.Abs(k) < 1) continue;
                Vector2 p = heart - d * 1.1f + sideAxis * (k * half * 0.35f);
                Rock(stone, V(p, crown + 0.45f - 0.32f * Mathf.Abs(k)),
                     new Vector3(4.6f, 2.9f + 0.18f * Mathf.Abs(k), 3.7f), k * 0.15f, RockTone(l, 180 + k), 180 + k);
            }
            Rock(stone, V(heart - d * 0.95f, crown + 1.15f), new Vector3(3.6f, 2.0f, 3.0f), 0.04f, Color.Lerp(l.rockTop, l.rockMid, 0.35f), 188);
            for (int line = -1; line <= 1; line++)
            {
                Vector2 start = heart + sideAxis * (line * half * 0.24f) + d * 0.6f;
                Vector2 end = start + d * (4.8f + (line == 0 ? 1.4f : 0f));
                FoamBand(foam, f, start, end, line == 0 ? 0.78f : 0.46f, 0.78f, 0.02f);
            }

            // 2. CHANNEL: stronger, more scenic banks while keeping walkable lanes clear.
            int row = 0;
            for (float s = 15f; s < riverLength - 16f; s += 5.6f, row++)
            {
                Vector2 along;
                Vector2 centre = OnAxis(axis, s, out along);
                Vector2 side = Side(along);
                if (NearCrossing(l, centre, 8.5f)) continue;
                int bank = row % 2 == 0 ? -1 : 1;
                float offset = half + 1.7f + Hash(row, 3, l.seed) * 2.4f;
                Vector2 p = centre + side * (bank * offset);
                if (!Eligible(f, p, 1.6f, true)) continue;
                float ground = f.Height(p.x, p.y);
                float factor = Mathf.Lerp(0.90f, 1.35f, Hash(row, 4, l.seed));
                Rock(stone, V(p, ground + 0.52f * factor),
                     new Vector3(2.8f * factor, 1.9f * factor, 2.6f * factor),
                     Hash(row, 5, l.seed) * 3.0f, RockTone(l, row + 220), row + 300);
                if (row % 2 == 0)
                {
                    Vector2 q = p + side * (bank * 1.9f) + along * (0.5f + Hash(row, 18, l.seed));
                    if (Eligible(f, q, 1.3f, true))
                        Rock(stone, V(q, f.Height(q.x, q.y) + 0.28f), new Vector3(1.65f, 1.25f, 1.7f), Hash(row, 19, l.seed) * 2.2f, RockTone(l, row + 227), row + 340);
                }
                if (row % 3 == 0)
                {
                    Vector2 q = p + side * (bank * 2.7f) + along * 1.2f;
                    if (Eligible(f, q, 1.4f, true))
                        Plant(plants, q, f.Height(q.x, q.y), 1.12f + Hash(row, 10, l.seed) * 0.55f,
                              Color.Lerp(l.lipTop, l.lipDark, 0.16f), row + 530);
                }
                if (row % 4 == 1 && f.FordWeight(centre.x, centre.y) < 0.1f)
                {
                    Vector2 a = centre + side * bank * (half * 0.37f) - along * 0.5f;
                    Vector2 b = a + along * (1.35f + 0.6f * Hash(row, 8, l.seed));
                    FoamBand(foam, f, a, b, 0.28f, 0.44f, 0.01f);
                }
            }
            // Hero clusters to break the monotony when seen from overview.
            for (int h = 0; h < 3; h++)
            {
                float pos = Mathf.Lerp(riverLength * 0.28f, riverLength * 0.73f, h / 2f);
                Vector2 along;
                Vector2 centre = OnAxis(axis, pos, out along);
                if (!NearCrossing(l, centre, 10f)) HeroBankCluster(stone, foam, f, centre, along, half, h);
            }

            // 3. MOUTH + WATERFALL: frame the native waterfall and add a readable continuous fall veil.
            Vector2 fallDir = outlet.outward.normalized;
            Vector2 edgeSide = Side(fallDir);
            Vector2 lip = outlet.pos;
            float lipY = f.RiverSurfaceAt(lip.x, lip.y) + 0.09f;
            float fallHalf = Mathf.Max(half * 0.7f, half * l.fallWidthShare);
            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                Vector2 throat = lip + edgeSide * (sideSign * (fallHalf + 1.5f)) - fallDir * 2.0f;
                if (Eligible(f, throat, 1.0f, false))
                    Rock(stone, V(throat, f.Height(throat.x, throat.y) + 1.25f), new Vector3(5.0f, 5.0f, 5.4f), sideSign * 0.18f, Color.Lerp(l.rockMid, l.cliffWet, 0.24f), 600 + sideSign);
                Vector2 ledge = lip + edgeSide * (sideSign * (fallHalf + 2.2f)) - fallDir * 0.65f;
                Rock(stone, V(ledge, f.Height(ledge.x, ledge.y) + 0.75f), new Vector3(4.5f, 2.9f, 4.7f), sideSign * 0.24f, Color.Lerp(l.rockMid, l.cliffWet, 0.34f), 620 + sideSign);
                for (int layer = 0; layer < 5; layer++)
                {
                    Vector2 q = lip + edgeSide * (sideSign * (fallHalf + 2.7f + layer * 0.30f)) + fallDir * (0.10f + layer * 0.22f);
                    float y = lipY - 1.9f - layer * 2.2f;
                    Rock(stone, V(q, y), new Vector3(2.7f - layer * 0.18f, 3.8f, 2.4f), sideSign * 0.14f + layer * 0.10f,
                         Color.Lerp(l.rockMid, l.cliffWet, 0.62f), 650 + layer * 2 + sideSign);
                }
            }
            for (int k = -1; k <= 1; k++)
            {
                Vector2 cap = lip - fallDir * 1.45f + edgeSide * (k * fallHalf * 0.45f);
                if (Eligible(f, cap, 0.8f, false))
                    Rock(stone, V(cap, f.Height(cap.x, cap.y) + 0.74f), new Vector3(2.8f, 2.1f, 3.0f), k * 0.18f, Color.Lerp(l.rockMid, l.rockTop, 0.2f), 700 + k);
            }
            BuildWaterfallVeil(falls, foam, f, outlet, half);

            var result = new Result
            {
                rocks = stone.ToMesh("TW_Grotto_Channel_Fall_Rocks"),
                plants = plants.ToMesh("TW_RiverBank_Soft_Growth"),
                foam = foam.ToMesh("TW_Spring_Rapids_Fall_Crests"),
                falls = falls.ToMesh("TW_Waterfall_Veil"),
                triangles = stone.TriangleCount + plants.TriangleCount + foam.TriangleCount + falls.TriangleCount
            };
            return result;
        }
    }
}

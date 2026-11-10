using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    // A rock headland with a low spring fissure. No tunnel, circular section or
    // continuous roof. Every intersecting rock mass is independently closed.
    internal static class TWRiverSource
    {
        static readonly Vector2[] Plan =
        {
            new Vector2(-0.76f, -1f), new Vector2(0.57f, -0.94f),
            new Vector2(1f, -0.39f), new Vector2(0.91f, 0.62f),
            new Vector2(0.43f, 1f), new Vector2(-0.64f, 0.84f),
            new Vector2(-1f, 0.21f), new Vector2(-0.94f, -0.59f)
        };
        static readonly float[] Levels = { 0f, 0.22f, 0.70f, 1f };
        static readonly float[] Widths = { 0.68f, 1.03f, 0.97f, 0.55f };
        static readonly float[] Crown = { -0.07f, -0.02f, 0.06f, 0.09f, 0.08f, 0.02f, -0.04f, -0.08f };

        public static Mesh Build(TWRiverCtx x, float cliffDepth = 0f)
        {
            var b = new RockBuilder(x);
            float water = x.Yw(0f);
            float floor = water - Mathf.Max(11f, Mathf.Max(cliffDepth, x.L.cliffWall + x.L.cliffTaper) + 1f);
            // The deep foundation replaces the empty underside at the inlet.
            b.Rock(0.6f, 0f, x.half + 5.8f, 6.4f, floor - 1f, water - 0.18f, 0f, 0f, 3, false);
            // Three unequal skyline masses, buried into the foundation.
            b.Rock(0.7f, -1.2f, x.half + 0.5f, 3.8f, water - 2.0f, water + 8.6f, -0.55f, -0.8f, 0, false);
            b.Rock(1.9f, x.half + 0.3f, 4.8f, 4.0f, water - 2.3f, water + 10.2f, 0.8f, -0.5f, 1, true);
            b.Rock(-0.5f, -x.half - 0.9f, 4.0f, 3.5f, water - 2.0f, water + 6.8f, -0.5f, -0.6f, 0, true);
            // Front buttresses form an irregular low crack, never an arch.
            b.Rock(6.0f, 4.7f, 3.2f, 2.8f, water - 1.3f, water + 5.2f, 0.25f, -0.35f, 0, true);
            b.Rock(7.0f, -4.3f, 3.1f, 2.4f, water - 1.1f, water + 3.3f, -0.35f, -0.2f, 1, true);
            // Low, solid shoulders close the junction to both existing banks.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = x.OnGround(6.7f, side * (x.half + 4.2f), 0f);
                float top = Mathf.Max(shoulder.y + 0.40f, water + 1.7f);
                b.Rock(6.7f, side * (x.half + 4.2f), 3.4f, 3.0f, water - 1.5f,
                       top, side * 0.2f, -0.1f, 1, true);
            }
            return b.ToMesh();
        }

        public static Mesh Shadow(TWRiverCtx x)
        {
            // An inset, irregular slit against the rear rock. It is below the
            // front buttresses; there is no open passage through the island.
            var b = new TWFaceted();
            float s = 4.8f, y = x.Yw(s);
            Vector3[] p =
            {
                x.P(s, -1.8f, y - 0.35f), x.P(s + 0.05f, -1.45f, y + 0.45f),
                x.P(s - 0.10f, -0.65f, y + 0.85f), x.P(s + 0.12f, 0.50f, y + 0.60f),
                x.P(s, 1.60f, y + 0.20f), x.P(s, 1.80f, y - 0.35f)
            };
            Vector3 centre = x.P(s, -0.1f, y + 0.20f);
            Vector2 t = x.ax.Tangent(s);
            Vector3 toward = centre + new Vector3(t.x, 0f, t.y);
            for (int i = 0; i < p.Length; i++)
                b.TriToward(centre, p[i], p[(i + 1) % p.Length], new Color(0.095f, 0.16f, 0.18f), toward);
            return b.ToMesh("Spring_Fissure");
        }

        public static Mesh OutletBanks(TWRiverCtx x, float depth)
        {
            var b = new RockBuilder(x);
            float s = x.ax.Length - 1.8f, water = x.Yw(s);
            for (int side = -1; side <= 1; side += 2)
            {
                // Thick, tapered rock ends at the waterfall, not sand planes
                // hanging over the edge. Their inside faces stay outside water.
                float offset = side * (x.half + 3.45f);
                Vector3 ground = x.OnGround(s, offset, 0f);
                float top = Mathf.Min(ground.y - 0.12f, water + 1.8f);
                b.Rock(s - (side > 0 ? 0.6f : 0f), offset, 2.8f, side > 0 ? 3.8f : 3.1f,
                       water - depth - 0.8f, Mathf.Max(water + 0.55f, top),
                       side * 0.45f, -1.15f, side > 0 ? 0 : 1, true);
            }
            return b.ToMesh();
        }

        sealed class RockBuilder
        {
            readonly TWRiverCtx x;
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            public RockBuilder(TWRiverCtx river) { x = river; }

            public void Rock(float s, float offset, float radiusX, float radiusZ, float bottom, float top,
                             float leanX, float leanZ, int tone, bool moss)
            {
                int first = vertices.Count;
                Vector2 axis = x.ax.At(s), side = x.ax.Side(s), forward = x.ax.Tangent(s);
                Vector3 centre = new Vector3(axis.x + side.x * offset, (bottom + top) * 0.5f, axis.y + side.y * offset);
                for (int ring = 0; ring < Levels.Length; ring++)
                    for (int i = 0; i < Plan.Length; i++)
                    {
                        float t = Levels[ring];
                        float ox = Plan[i].x * radiusX * Widths[ring] + leanX * t;
                        float oz = Plan[i].y * radiusZ * Widths[ring] + leanZ * t;
                        float y = Mathf.Lerp(bottom, top, t);
                        if (ring == Levels.Length - 1) y += Crown[i] * Mathf.Min(4f, top - bottom);
                        vertices.Add(new Vector3(centre.x + side.x * ox + forward.x * oz, y,
                                                  centre.z + side.y * ox + forward.y * oz));
                    }
                for (int ring = 0; ring < Levels.Length - 1; ring++)
                    for (int i = 0; i < Plan.Length; i++)
                    {
                        int next = (i + 1) % Plan.Length;
                        int a = first + ring * Plan.Length + i, b = first + ring * Plan.Length + next;
                        Add(a, b, b + Plan.Length, centre, tone);
                        Add(a, b + Plan.Length, a + Plan.Length, centre, tone);
                    }
                int capBottom = vertices.Count;
                vertices.Add(new Vector3(centre.x, bottom, centre.z));
                int capTop = vertices.Count;
                vertices.Add(new Vector3(centre.x + side.x * leanX + forward.x * leanZ, top,
                                         centre.z + side.y * leanX + forward.y * leanZ));
                int last = first + (Levels.Length - 1) * Plan.Length;
                for (int i = 0; i < Plan.Length; i++)
                {
                    int next = (i + 1) % Plan.Length;
                    Add(capBottom, first + next, first + i, centre, tone);
                    Add(capTop, last + i, last + next, centre, moss && i >= 2 && i <= 5 ? 2 : tone);
                }
            }

            void Add(int a, int b, int c, Vector3 centre, int material)
            {
                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                bool flip = Vector3.Dot(normal, (vertices[a] + vertices[b] + vertices[c]) / 3f - centre) < 0f;
                triangles[material].Add(a); triangles[material].Add(flip ? c : b); triangles[material].Add(flip ? b : c);
            }

            public Mesh ToMesh()
            {
                // Hard face normals match the project's faceted stone assets.
                // Keep exact positions so QA can weld and check every edge.
                var flat = new List<Vector3>();
                var normals = new List<Vector3>();
                var indices = new List<int>[triangles.Length];
                for (int m = 0; m < triangles.Length; m++)
                {
                    indices[m] = new List<int>();
                    for (int i = 0; i < triangles[m].Count; i += 3)
                    {
                        Vector3 a = vertices[triangles[m][i]], b = vertices[triangles[m][i + 1]], c = vertices[triangles[m][i + 2]];
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        int first = flat.Count;
                        flat.Add(a); flat.Add(b); flat.Add(c);
                        normals.Add(normal); normals.Add(normal); normals.Add(normal);
                        indices[m].Add(first); indices[m].Add(first + 1); indices[m].Add(first + 2);
                    }
                }
                var mesh = new Mesh { name = "Spring_Rock_Headland" };
                mesh.SetVertices(flat); mesh.SetNormals(normals); mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

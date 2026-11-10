using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    // One indexed ribbon. The last river row is also the first waterfall row:
    // position, normals and flow UVs have no independent seam to drift apart.
    internal static class TWRiverWater
    {
        public const int Columns = 17;
        public const int ArcSegments = 18;
        public const int TailSegments = 20;
        public const float SurfaceLift = 0.035f;
        public const float ArcDepth = 2.1f;

        internal sealed class Result
        {
            public Mesh mesh;
            public int lipRow;
            public Vector3 mistPosition;
        }

        public static float HalfWidth(TWRiverCtx x, float s)
        {
            // Small variation stays inside the existing banks / swim envelope.
            float wiggle = Mathf.Sin(s * 0.18f) * Mathf.Sin(s * 0.061f + 0.7f);
            return x.half + 0.18f + wiggle * 0.10f;
        }

        public static Vector3 ArcCentre(Vector3 lip, Vector2 direction, float theta, float reach)
        {
            float outDist = reach * Mathf.Sin(theta);
            return lip + new Vector3(direction.x * outDist, -ArcDepth * (1f - Mathf.Cos(theta)), direction.y * outDist);
        }

        public static Result Build(TWRiverCtx x, float cliffDepth)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var colours = new List<Color>();
            var indices = new List<int>();
            int rows = Mathf.Max(2, Mathf.CeilToInt(x.ax.Length / 0.8f));
            float len = x.ax.Length;

            for (int row = 0; row <= rows; row++)
            {
                float s = len * row / rows;
                Vector2 p = x.ax.At(s);
                Vector2 side = x.ax.Side(s);
                float cave = x.hasSource ? TWRiverSpec.CaveShade(s, TWRiverSpec.MouthDist) : 0f;
                float depth = Mathf.Clamp01(x.f.RiverWaterDepthAt(x.f.FordWeight(p.x, p.y), x.f.NotchCut(p.x, p.y)) /
                                            Mathf.Max(0.1f, x.L.riverWaterDepth));
                Row(v, uv, colours, x.P(s, 0f, x.Yw(s) + SurfaceLift), side, HalfWidth(x, s), s,
                    depth, 0f, 1f, cave);
            }

            // Flow keeps its exit tangent. A second contour normal would twist the lip.
            Vector2 dir = x.ax.Tangent(len);
            Vector2 across = x.ax.Side(len);
            Vector3 lip = x.P(len, 0f, x.Yw(len) + SurfaceLift);
            float reach = Mathf.Max(2.2f, x.L.fallOffset + 1.0f);
            float totalDrop = Mathf.Max(ArcDepth + 5f, cliffDepth + x.L.fallExtra + 1.5f);
            float topHalf = HalfWidth(x, len);
            float midHalf = x.half * Mathf.Clamp(x.L.fallWidthShare, 0.88f, 1.04f);
            float distance = len;
            Vector3 previous = lip;
            float lipDepth = colours[rows * Columns].r;

            for (int i = 1; i <= ArcSegments; i++)
            {
                float t = (float)i / ArcSegments;
                Vector3 p = ArcCentre(lip, dir, t * Mathf.PI * 0.5f, reach);
                distance += Vector3.Distance(previous, p);
                Row(v, uv, colours, p, across, Mathf.Lerp(topHalf, midHalf, TWRiverSpec.Smooth(0f, 1f, t)),
                    distance, Mathf.Lerp(lipDepth, 0.75f, TWRiverSpec.Smooth(0f, 1f, t)),
                    TWRiverSpec.Smooth(0f, 0.8f, t), 1f, 0f);
                previous = p;
            }

            Vector3 shoulder = previous;
            for (int i = 1; i <= TailSegments; i++)
            {
                float t = (float)i / TailSegments;
                float drop = (totalDrop - ArcDepth) * t;
                Vector3 p = shoulder + Vector3.down * drop;
                distance += Vector3.Distance(previous, p);
                float fade = 1f - TWRiverSpec.Smooth(0.58f, 1f, t);
                // Gently opens into mist; a solid centre remains until the final third.
                float half = midHalf * (1f + 0.08f * t * t);
                Row(v, uv, colours, p, across, half, distance, 0.7f, 1f, fade, 0f);
                previous = p;
            }

            int rowCount = v.Count / Columns;
            for (int row = 0; row < rowCount - 1; row++)
                for (int col = 0; col < Columns - 1; col++)
                {
                    int a = row * Columns + col, b = a + Columns;
                    indices.Add(a); indices.Add(a + 1); indices.Add(b);
                    indices.Add(a + 1); indices.Add(b + 1); indices.Add(b);
                }

            var mesh = new Mesh { name = "River_Source_To_Clouds" };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(colours); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return new Result
            {
                mesh = mesh, lipRow = rows,
                mistPosition = shoulder + Vector3.down * ((totalDrop - ArcDepth) * 0.78f)
            };
        }

        static void Row(List<Vector3> v, List<Vector2> uv, List<Color> colours, Vector3 centre,
                        Vector2 side, float half, float along, float depth, float fall, float fade, float cave)
        {
            for (int col = 0; col < Columns; col++)
            {
                float u = 2f * col / (Columns - 1) - 1f;
                v.Add(centre + new Vector3(side.x * half * u, 0f, side.y * half * u));
                uv.Add(new Vector2(u, along));
                // Data, not RGB: do NOT convert this array to linear colour space.
                colours.Add(new Color(depth, fall, fade, cave));
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverShore
    {
        public static float BeachWeight(TWRiverCtx x, float s)
        {
            float start = (x.hasSource ? TWRiverSpec.MouthDist : 0f) + 5f;
            float usable = Mathf.Max(12f, x.ax.Length - start - 10f);
            float centre = start + usable * 0.46f;
            float distance = Mathf.Abs(s - centre) / Mathf.Max(7f, usable * 0.34f);
            return 1f - TWRiverSpec.Smooth(0.38f, 1f, distance);
        }

        // Draped over the existing ground. Alpha=0 at the outer edge restores
        // the island's own world-space ground texture using its original shader.
        public static Mesh Build(TWRiverCtx x)
        {
            var verts = new List<Vector3>(); var colours = new List<Color>(); var tris = new List<int>();
            const int cross = 10;
            int rows = Mathf.CeilToInt(x.ax.Length / 0.65f);
            for (int side = -1; side <= 1; side += 2)
            {
                int first = verts.Count;
                for (int i = 0; i <= rows; i++)
                {
                    float s = x.ax.Length * i / rows;
                    float beach = side == -x.rockSide ? BeachWeight(x, s) : 0f;
                    float taper = TWRiverSpec.Smooth(TWRiverSpec.MouthDist - 1f, TWRiverSpec.MouthDist + 6f, s) *
                                  (1f - TWRiverSpec.Smooth(x.ax.Length - 9f, x.ax.Length - 2f, s));
                    float wide = 2.2f + beach * 4.4f + 0.35f * Mathf.Sin(s * 0.42f + side);
                    Vector2 a = x.ax.At(s), n = x.ax.Side(s) * side;
                    for (int k = 0; k < cross; k++)
                    {
                        float t = (float)k / (cross - 1);
                        float off = x.half - 0.25f + wide * t;
                        Vector3 p = new Vector3(a.x + n.x * off, 0f, a.y + n.y * off);
                        p.y = x.f.Height(p.x, p.z) + 0.025f;
                        float shore = 1f - TWRiverSpec.Smooth(0.35f + beach * 0.15f, 1f, t);
                        float wet = 1f - TWRiverSpec.Smooth(x.Yw(s) - 0.15f, x.Yw(s) + 0.95f, p.y);
                        Color sand = Color.Lerp(new Color(0.92f, 0.83f, 0.63f), new Color(0.64f, 0.70f, 0.61f), wet);
                        sand *= 0.98f + 0.04f * Mathf.PerlinNoise(p.x * 0.6f, p.z * 0.6f);
                        sand = sand.linear;
                        sand.a = shore * taper * (0.48f + beach * 0.48f);
                        sand.a *= 1f - x.ExclusionWeight(new Vector2(p.x, p.z), 3f);
                        sand.a *= 1f - TWRiverSpec.Smooth(0f, 0.10f, x.f.PathWeight(p.x, p.z));
                        sand.a *= TWRiverSpec.Smooth(2f, 4.5f, x.f.EdgeDistance(p.x, p.z));
                        colours.Add(sand); verts.Add(p);
                    }
                }
                for (int i = 0; i < rows; i++)
                    for (int k = 0; k < cross - 1; k++)
                    {
                        int a = first + i * cross + k, b = a + cross;
                        Vector3 centre = (verts[a] + verts[a + 1] + verts[b] + verts[b + 1]) * 0.25f;
                        if (!x.f.Inside(centre.x, centre.z) || x.f.EdgeDistance(centre.x, centre.z) < 0.7f ||
                            x.Protected(centre, 1.1f) || x.f.PathWeight(centre.x, centre.z) > 0.08f) continue;
                        // Every corner must have ground, not just the quad centre.
                        bool supported = true;
                        foreach (int corner in new[] { a, a + 1, b, b + 1 })
                            if (x.f.EdgeDistance(verts[corner].x, verts[corner].z) < 2f) supported = false;
                        if (!supported) continue;
                        if (side > 0)
                        {
                            tris.Add(a); tris.Add(a + 1); tris.Add(b);
                            tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                        }
                        else
                        {
                            tris.Add(a); tris.Add(b); tris.Add(a + 1);
                            tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                        }
                    }
            }
            var mesh = new Mesh { name = "River_SoftSand_Shore" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetColors(colours); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

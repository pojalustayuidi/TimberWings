using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWGroveGround
    {
        // Native terrain triangles clipped to a round envelope. The dirt itself
        // is a network of feathered paths, not a rectangular overlay texture.
        public static void Build(TWGroveContext x, TWGroveAssets assets)
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath)) { name = "Grove_Native_Ground" };
            AssetDatabase.CreateAsset(material, assets.folder + "/Ground.mat");
            const int sides = 64; var boundary = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                Vector3 wp = x.World(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 19f); boundary[i] = new Vector2(wp.x, wp.z);
            }
            var vertices = new List<Vector3>(); var colours = new List<Color>(); var indices = new List<int>();
            foreach (var face in x.ground)
            {
                var polygon = new List<Vector3> { face.a, face.b, face.c };
                for (int i = 0; i < sides && polygon.Count != 0; i++) polygon = Clip(polygon, boundary[i], boundary[(i + 1) % sides]);
                for (int i = 1; i + 1 < polygon.Count; i++) Add(polygon[0], polygon[i], polygon[i + 1], 0);
            }
            var mesh = new Mesh { name = "Grove_Conforming_Paths", indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            assets.Object(x.root, "00_Земля_тропы_и_рабочие_пятна", assets.Save(mesh, "Paths"), new[] { material }, false);

            void Add(Vector3 a, Vector3 b, Vector3 c, int depth)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return;
                if (depth < 6 && Mathf.Max((b - a).sqrMagnitude, Mathf.Max((c - b).sqrMagnitude, (a - c).sqrMagnitude)) > 0.72f * 0.72f)
                {
                    Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
                    Add(a, ab, ca, depth + 1); Add(ab, b, bc, depth + 1); Add(ca, bc, c, depth + 1); Add(ab, bc, ca, depth + 1); return;
                }
                if (normal.y < 0) { Vector3 swap = b; b = c; c = swap; }
                int start = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    vertices.Add(x.root.InverseTransformPoint(p + Vector3.up * 0.014f)); colours.Add(Colour(x, p));
                }
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }
        }

        internal static Color Colour(TWGroveContext x, Vector3 world)
        {
            Vector2 p = x.Local(world); float weight = 0;
            for (int i = 0; i < x.trails.Count; i++)
            {
                float d = TWGrovePaths.Distance(p, x.trails[i]), width = i == 0 ? 1.15f : i == 1 ? 0.8f : 0.55f;
                weight = Mathf.Max(weight, 1 - Smooth(width - 0.25f, width + 0.55f, d));
            }
            foreach (var patch in x.earthPatches)
                weight = Mathf.Max(weight, (1 - Smooth(patch.radius * 0.45f, patch.radius + 0.55f, Vector2.Distance(p, patch.point))) * 0.88f);
            Vector2 hub = p-x.hub;
            weight = Mathf.Max(weight,(1-Smooth(1.8f,3.25f,new Vector2(hub.x/1.15f,hub.y).magnitude))*0.78f);
            foreach (var tree in x.trees) weight *= Smooth(0.5f, 1.4f, Vector2.Distance(p, tree.point));
            float envelope = 1 - Smooth(17.6f, 18.7f, p.magnitude);
            weight *= envelope;
            float noise = Mathf.PerlinNoise(world.x * 0.44f + 7, world.z * 0.44f + 19);
            Color earth = Color.Lerp(new Color(0.73f, 0.61f, 0.42f), new Color(0.85f, 0.73f, 0.52f), noise * 0.6f + 0.2f).linear;
            // Locally repaint grass shoulders over the old wide baked road.
            // The source ground texture and other parts of the island are untouched.
            Color grass = (x.candy!=null && x.candy.candyOn ? x.candy.GrassAt(0.27f+noise*0.25f) : Color.Lerp(x.field.L.grassMid,x.field.L.grassLight,noise*0.3f)).linear;
            float shoulders=(1-Smooth(10.5f,13.0f,p.magnitude))*Smooth(-12f,-9.0f,p.y);
            float earthAlpha=weight*0.94f,grassAlpha=shoulders*(1-weight)*0.90f;
            float alpha=earthAlpha+grassAlpha;
            Color colour=alpha>0.00001f ? (earth*earthAlpha+grass*grassAlpha)/alpha : earth;
            colour.a=alpha; return colour;
        }

        static List<Vector3> Clip(List<Vector3> polygon, Vector2 a, Vector2 b)
        {
            var result = new List<Vector3>(); Vector2 edge = b - a;
            float Side(Vector3 p) { return edge.x * (p.z - a.y) - edge.y * (p.x - a.x); }
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 p = polygon[i], q = polygon[(i + 1) % polygon.Count]; float sp = Side(p), sq = Side(q);
                bool ip = sp >= 0, iq = sq >= 0;
                if (ip) result.Add(p); if (ip != iq) result.Add(Vector3.Lerp(p, q, sp / (sp - sq)));
            }
            return result;
        }
        static float Smooth(float a, float b, float v) { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v)); }
    }
}

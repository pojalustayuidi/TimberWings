using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWCenterGround
    {
        const int Sides = 64;
        internal static float EdgeRadius(TWCenterContext x, float a)
        {
            return x.radius + 0.12f * Mathf.Sin(a * 3f + 0.4f) + 0.045f * Mathf.Sin(a * 7f);
        }

        public static void Build(TWCenterContext x, TWCenterAssets assets, Transform root)
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(IslandBlockout.MaterialPath)) { name = "Center_Matte_Earth" };
            AssetDatabase.CreateAsset(material, assets.folder + "/Clearing_Earth.mat");
            var boundary = new Vector2[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides, r = EdgeRadius(x, a);
                boundary[i] = x.centre + new Vector2(Mathf.Cos(a) * r * 1.10f, Mathf.Sin(a) * r * 0.97f);
            }
            var vertices = new List<Vector3>(); var colours = new List<Color>(); var triangles = new List<int>();
            foreach (var face in x.ground)
            {
                var polygon = new List<Vector3> { face.a, face.b, face.c };
                for (int i = 0; i < Sides && polygon.Count > 0; i++) polygon = Clip(polygon, boundary[i], boundary[(i + 1) % Sides]);
                for (int i = 1; i + 1 < polygon.Count; i++) Add(polygon[0], polygon[i], polygon[i + 1], 0);
            }
            var mesh = new Mesh { name = "Centre_Clipped_Oval_Earth" };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            assets.Object(root, "Organic_Earth_Clearing", assets.Save(mesh, "Clearing_Ground"), new[] { material }, false);

            void Add(Vector3 a, Vector3 b, Vector3 c, int depth)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return;
                if (depth < 5 && Mathf.Max((b - a).sqrMagnitude, Mathf.Max((c - b).sqrMagnitude, (a - c).sqrMagnitude)) > 0.78f * 0.78f)
                {
                    Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
                    Add(a, ab, ca, depth + 1); Add(ab, b, bc, depth + 1); Add(ca, bc, c, depth + 1); Add(ab, bc, ca, depth + 1); return;
                }
                if (normal.y < 0f) { Vector3 swap = b; b = c; c = swap; }
                int first = vertices.Count;
                foreach (Vector3 p in new[] { a, b, c }) { vertices.Add(p + Vector3.up * 0.014f); colours.Add(Colour(x, p)); }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }
        }

        internal static Color Colour(TWCenterContext x, Vector3 p)
        {
            Vector2 d = new Vector2((p.x - x.centre.x) / 1.10f, (p.z - x.centre.y) / 0.97f);
            float distance = d.magnitude, edge = EdgeRadius(x, Mathf.Atan2(d.y, d.x));
            float broad = Mathf.PerlinNoise(p.x * 0.42f + 11f, p.z * 0.42f + 27f);
            // Warm focal area and a quiet earth collar interrupt the through-road.
            Color earth = Color.Lerp(new Color(0.77f, 0.62f, 0.40f), new Color(0.86f, 0.73f, 0.52f), 0.35f + broad * 0.45f);
            earth = Color.Lerp(earth, new Color(0.69f, 0.57f, 0.38f), Smooth(edge - 2.0f, edge - 0.7f, distance) * 0.22f);
            float trail = x.field.PathWeight(p.x, p.z);
            earth = Color.Lerp(earth, x.field.L.pathSand, trail * Smooth(edge - 0.9f, edge, distance) * 0.65f);
            float alpha = 1f - Smooth(edge - 0.95f, edge - 0.10f, distance);
            if (Vector2.Distance(new Vector2(p.x, p.z), x.centre) <= x.field.L.plazaRadius + 0.2f) alpha = 1f;
            earth = earth.linear; earth.a = alpha;
            // The existing island shader blends RGB with its original ground
            // by vertex alpha. No transparent quad, PNG or blend-state override.
            return earth;
        }

        static List<Vector3> Clip(List<Vector3> polygon, Vector2 a, Vector2 b)
        {
            var result = new List<Vector3>(); Vector2 edge = b - a;
            float Side(Vector3 p) { return edge.x * (p.z - a.y) - edge.y * (p.x - a.x); }
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 p = polygon[i], q = polygon[(i + 1) % polygon.Count];
                float sp = Side(p), sq = Side(q); bool ip = sp >= 0f, iq = sq >= 0f;
                if (ip) result.Add(p);
                if (ip != iq) result.Add(Vector3.Lerp(p, q, sp / (sp - sq)));
            }
            return result;
        }

        static float Smooth(float a, float b, float value) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, value)); }
    }
}


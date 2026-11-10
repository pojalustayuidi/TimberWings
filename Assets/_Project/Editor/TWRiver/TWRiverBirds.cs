using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWRiverBirds
    {
        public static void Build(Transform parent, TWRiverCtx x, Material material, string folder)
        {
            // Pick the longest quiet interval, with a margin around the bridge,
            // source and waterfall. Never send the birds through a crossing.
            var best = new List<float>(); var current = new List<float>();
            float off = -x.rockSide * x.half * 0.25f;
            for (float s = TWRiverSpec.MouthDist + 13f; s < x.ax.Length - 18f; s += 1f)
            {
                Vector3 p = x.P(s, off, x.Yw(s));
                if (x.Protected(p, 3f))
                {
                    if (current.Count > best.Count) best = new List<float>(current);
                    current.Clear();
                }
                else current.Add(s);
            }
            if (current.Count > best.Count) best = current;
            if (best.Count < 8) { Debug.LogWarning("RIVER ART: нет свободного участка для уточек; птицы пропущены."); return; }
            float centre = (best[0] + best[best.Count - 1]) * 0.5f;
            float span = Mathf.Min(7f, (best[best.Count - 1] - best[0]) * 0.5f - 1f);
            var route = new Vector3[48];
            for (int i = 0; i < route.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / route.Length;
                float s = centre + Mathf.Cos(angle) * span;
                route[i] = x.P(s, off + Mathf.Sin(angle) * 0.72f, x.Yw(s) + TWRiverWater.SurfaceLift + 0.015f);
            }
            Mesh adult = Duck(false), baby = Duck(true);
            AssetDatabase.CreateAsset(adult, folder + "/Duck_Adult.asset");
            AssetDatabase.CreateAsset(baby, folder + "/Duckling.asset");
            var root = new GameObject("River_Ducks").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(i == 0 ? "Mallard" : "Duckling_" + i);
                go.transform.SetParent(root, false);
                go.transform.localScale = Vector3.one * (i == 0 ? 1f : 0.56f);
                go.AddComponent<MeshFilter>().sharedMesh = i == 0 ? adult : baby;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                var motion = go.AddComponent<RiverDuckMotion>();
                motion.route = route; motion.phase = -i * 0.048f; motion.secondsPerLoop = 75f;
                motion.PlaceAt(0f);
            }
        }

        static Mesh Duck(bool baby)
        {
            var v = new List<Vector3>(); var colours = new List<Color>(); var indices = new List<int>();
            Color body = baby ? new Color(1f, 0.81f, 0.27f) : new Color(0.72f, 0.62f, 0.43f);
            Color head = baby ? new Color(1f, 0.87f, 0.38f) : new Color(0.19f, 0.48f, 0.36f);
            Ellipsoid(v, colours, indices, new Vector3(0f, 0.14f, 0f), new Vector3(0.32f, 0.23f, 0.48f), body);
            Ellipsoid(v, colours, indices, new Vector3(0f, 0.38f, 0.31f), new Vector3(0.15f, 0.13f, 0.16f), new Color(1f, 0.91f, 0.69f));
            Ellipsoid(v, colours, indices, new Vector3(0f, 0.47f, 0.37f), new Vector3(0.18f, 0.19f, 0.20f), head);
            Ellipsoid(v, colours, indices, new Vector3(0f, 0.42f, 0.58f), new Vector3(0.115f, 0.045f, 0.16f), new Color(1f, 0.59f, 0.12f));
            for (int side = -1; side <= 1; side += 2)
            {
                Ellipsoid(v, colours, indices, new Vector3(side * 0.275f, 0.18f, -0.03f),
                          new Vector3(0.08f, 0.15f, 0.29f), Color.Lerp(body, new Color(0.34f, 0.28f, 0.23f), baby ? 0.1f : 0.35f));
                Ellipsoid(v, colours, indices, new Vector3(side * 0.151f, 0.52f, 0.46f),
                          new Vector3(0.026f, 0.028f, 0.029f), new Color(0.055f, 0.065f, 0.075f));
            }
            Ellipsoid(v, colours, indices, new Vector3(0f, 0.20f, -0.43f), new Vector3(0.15f, 0.095f, 0.17f), body);
            var mesh = new Mesh { name = baby ? "Candy_Duckling" : "Candy_Mallard" };
            mesh.SetVertices(v); mesh.SetColors(colours); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Ellipsoid(List<Vector3> v, List<Color> c, List<int> tris, Vector3 centre, Vector3 radii, Color colour)
        {
            const int segments = 10, latitudes = 4;
            int start = v.Count;
            Color linear = colour.linear;
            v.Add(centre + Vector3.up * radii.y); c.Add(linear);
            for (int ring = 1; ring < latitudes; ring++)
                for (int i = 0; i < segments; i++)
                {
                    float phi = ring * Mathf.PI / latitudes, theta = i * Mathf.PI * 2f / segments;
                    v.Add(centre + new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * radii.x,
                        Mathf.Cos(phi) * radii.y, Mathf.Sin(phi) * Mathf.Sin(theta) * radii.z)); c.Add(linear);
                }
            int bottom = v.Count; v.Add(centre - Vector3.up * radii.y); c.Add(linear);
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                Triangle(v, tris, start, start + 1 + i, start + 1 + next, centre);
                for (int ring = 0; ring < latitudes - 2; ring++)
                {
                    int a = start + 1 + ring * segments + i, b = start + 1 + ring * segments + next;
                    Triangle(v, tris, a, a + segments, b, centre);
                    Triangle(v, tris, b, a + segments, b + segments, centre);
                }
                int last = start + 1 + (latitudes - 2) * segments;
                Triangle(v, tris, last + i, bottom, last + next, centre);
            }
        }

        static void Triangle(List<Vector3> v, List<int> tris, int a, int b, int c, Vector3 centre)
        {
            bool flip = Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), (v[a] + v[b] + v[c]) / 3f - centre) < 0f;
            tris.Add(a); tris.Add(flip ? c : b); tris.Add(flip ? b : c);
        }
    }
}

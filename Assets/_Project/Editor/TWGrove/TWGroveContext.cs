using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IslandV2
{
    internal sealed class TWGroveContext
    {
        public struct Face { public Vector3 a, b, c; }
        public struct Circle { public Vector2 point; public float radius; }
        public struct Footprint { public Vector2 point, half; public float yaw; }
        public Transform marker, manual, generated, root;
        public GroveArtState previous;
        public IslandField field;
        public IslandCandy candy;
        public Vector3 origin;
        public Quaternion rotation;
        public Vector2 hub;
        public readonly List<Face> ground = new List<Face>();
        public readonly List<Circle> trees = new List<Circle>();
        public readonly List<Circle> buildings = new List<Circle>();
        public readonly List<Footprint> occupied = new List<Footprint>();
        public readonly List<GameObject> old = new List<GameObject>();
        public readonly List<List<Vector2>> trails = new List<List<Vector2>>();
        public readonly List<Circle> earthPatches = new List<Circle>();
        public readonly Dictionary<string, GroveArtHandle> keep = new Dictionary<string, GroveArtHandle>();
        public readonly Dictionary<string, Matrix4x4> keptMatrices = new Dictionary<string, Matrix4x4>();
        public ResourceItem[] resources;

        public Vector3 World(Vector2 p) { return origin + rotation * new Vector3(p.x, 0f, p.y); }
        public Vector2 Local(Vector3 p) { Vector3 q = Quaternion.Inverse(rotation) * (p - origin); return new Vector2(q.x, q.z); }
        public Vector3 Point(Vector2 p)
        {
            Vector3 w = World(p); w.y = GroundY(new Vector2(w.x, w.z)); return w;
        }

        public void ReadGround()
        {
            foreach (var filter in generated.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith(IslandBlockout.ChunkPrefix, StringComparison.Ordinal) || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[triangles[i]]), b = matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]), c = matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > origin.x + 23f || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < origin.x - 23f ||
                        Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > origin.z + 23f || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < origin.z - 23f) continue;
                    ground.Add(new Face { a = a, b = b, c = c });
                }
            }
            if (ground.Count == 0) throw new InvalidOperationException("Нет существующих Ground_Chunk_* у южной рощи.");
        }

        public bool TryGround(Vector2 p, out float height)
        {
            foreach (var face in ground)
            {
                Vector2 a = new Vector2(face.a.x, face.a.z), b = new Vector2(face.b.x, face.b.z), c = new Vector2(face.c.x, face.c.z);
                float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(den) < 0.000001f) continue;
                float u = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / den;
                float v = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / den;
                if (u >= -0.0001f && v >= -0.0001f && u + v <= 1.0001f)
                { height = u * face.a.y + v * face.b.y + (1f - u - v) * face.c.y; return true; }
            }
            height = 0f; return false;
        }

        public float GroundY(Vector2 p)
        {
            if (TryGround(p, out float y)) return y;
            throw new InvalidOperationException("Нет земли в точке " + p + ".");
        }

        public void ReadObstacles(Transform[] all)
        {
            resources = all.Select(t => t.GetComponent<ResourceItem>()).Where(t => t != null && Local(t.transform.position).magnitude < 19f).ToArray();
            foreach (var t in all)
            {
                if (old.Any(g => t.IsChildOf(g.transform)) || (previous != null && previous.artRoot != null && t.IsChildOf(previous.artRoot))) continue;
                bool tree = t.GetComponent<ResourceItem>() != null || (t.GetComponent<ToonSway>() != null &&
                    (t.CompareTag("Tree") || t.name.StartsWith("Tree_", StringComparison.Ordinal) || t.GetComponent<LandmarkTree>() != null));
                Vector2 p = Local(t.position);
                if (tree && p.magnitude < 22f && !trees.Any(c => Vector2.Distance(c.point, p) < 0.35f))
                    trees.Add(new Circle { point = p, radius = t.GetComponent<ResourceItem>() != null ? 2.35f : 1.8f });
                if (t.GetComponent<BuildingController>() != null && p.magnitude < 22f)
                {
                    Vector2 centre = p; float radius = 3.4f;
                    var stages = t.GetComponentInParent<HutStages>();
                    if (stages != null)
                    {
                        centre = Local(stages.transform.position);
                        foreach (var mf in stages.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (mf.sharedMesh == null || mf.GetComponentInParent<Canvas>() != null) continue;
                            Vector3 ext = Vector3.Scale(mf.sharedMesh.bounds.extents, mf.transform.lossyScale);
                            radius = Mathf.Max(radius, Vector2.Distance(centre, Local(mf.transform.TransformPoint(mf.sharedMesh.bounds.center))) + new Vector2(ext.x, ext.z).magnitude + 0.7f);
                        }
                    }
                    buildings.Add(new Circle { point = centre, radius = radius });
                }
            }
        }

        public bool CanPlace(Vector2 p, Vector2 half, float yaw, float height, bool gate = false)
        {
            if (p.magnitude > 18.3f) return false;
            float r = half.magnitude;
            var foot = new Footprint { point = p, half = half, yaw = yaw };
            if (trees.Any(t => TWGrovePaths.PointDistance(t.point, foot) < t.radius)) return false;
            if (buildings.Any(b => Vector2.Distance(b.point, p) < b.radius + r)) return false;
            if (!gate && Mathf.Abs(p.x) < r + 1.5f && p.y < 8.4f && p.y > -16f) return false;
            if (!gate && trails.Any(line => TWGrovePaths.Distance(p, line) < r + 1.1f)) return false;
            if (occupied.Any(f => Intersects(foot, f, 0.45f))) return false;
            Quaternion q = rotation * Quaternion.Euler(0f, yaw, 0f);
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var d in new[] { new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y), new Vector2(half.x, half.y), new Vector2(-half.x, half.y) })
            {
                Vector3 wp = World(p) + q * new Vector3(d.x, 0, d.y);
                if (!TryGround(new Vector2(wp.x, wp.z), out float y) || field.IsWater(wp.x, wp.z) || field.EdgeDistance(wp.x, wp.z) < 1.6f) return false;
                if (!gate && field.PathDistance(wp.x, wp.z) < 0.25f) return false;
                low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            if (high - low > (gate ? 0.55f : 0.4f)) return false;
            Vector3 point = Point(p);
            foreach (var collider in Physics.OverlapBox(point + Vector3.up * (height * 0.5f + 0.12f), new Vector3(half.x, height * 0.5f, half.y), q, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider is TerrainCollider || collider.name.StartsWith(IslandBlockout.ChunkPrefix, StringComparison.Ordinal) || collider.transform.IsChildOf(root) ||
                    old.Any(g => collider.transform.IsChildOf(g.transform)) || (previous != null && previous.artRoot != null && collider.transform.IsChildOf(previous.artRoot))) continue;
                return false;
            }
            return true;
        }

        public bool Place(Vector2 desired, Vector2 half, float yaw, float height, out Vector3 point, out Quaternion q, bool gate = false)
        {
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 16); i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    Vector2 p = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 0.85f;
                    if (!CanPlace(p, half, yaw, height, gate)) continue;
                    point = Point(p); q = rotation * Quaternion.Euler(0, yaw, 0);
                    occupied.Add(new Footprint { point = p, half = half, yaw = yaw });
                    return true;
                }
            point = default; q = Quaternion.identity; return false;
        }

        public static bool Intersects(Footprint a, Footprint b, float pad)
        {
            Vector2 Axis(float angle) { float r = angle * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), -Mathf.Sin(r)); }
            Vector2 ar = Axis(a.yaw), af = Axis(a.yaw + 90), br = Axis(b.yaw), bf = Axis(b.yaw + 90);
            foreach (var axis in new[] { ar, af, br, bf })
            {
                float ra = a.half.x * Mathf.Abs(Vector2.Dot(ar, axis)) + a.half.y * Mathf.Abs(Vector2.Dot(af, axis));
                float rb = b.half.x * Mathf.Abs(Vector2.Dot(br, axis)) + b.half.y * Mathf.Abs(Vector2.Dot(bf, axis));
                if (Mathf.Abs(Vector2.Dot(b.point - a.point, axis)) >= ra + rb + pad) return false;
            }
            return true;
        }
    }
}

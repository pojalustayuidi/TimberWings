using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IslandV2
{
    internal sealed class TWCenterContext
    {
        internal struct Face
        {
            public Vector3 a, b, c;
        }

        public IslandField field;
        public Vector2 centre;
        public float radius;
        public Transform island, manual, generated, fire, marker, buildPoint, newRoot;
        public CenterArtState previous;
        public HearthArtState hearth;
        public readonly List<GameObject> oldDecor = new List<GameObject>();
        public readonly List<Bounds> reserved = new List<Bounds>();
        public readonly List<Bounds> occupied = new List<Bounds>();
        public readonly List<Face> ground = new List<Face>();
        public Vector2 approach;

        public void ReadGround()
        {
            float reach = 15f;
            foreach (var filter in generated.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith(IslandBlockout.ChunkPrefix, StringComparison.Ordinal) || filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                var v = mesh.vertices; var t = mesh.triangles;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(v[t[i]]), b = matrix.MultiplyPoint3x4(v[t[i + 1]]), c = matrix.MultiplyPoint3x4(v[t[i + 2]]);
                    if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > centre.x + reach || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < centre.x - reach ||
                        Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > centre.y + reach || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < centre.y - reach) continue;
                    ground.Add(new Face { a = a, b = b, c = c });
                }
            }
            if (ground.Count == 0) throw new InvalidOperationException("Не найдены видимые Ground_Chunk_* у костра.");
        }

        public float GroundY(Vector2 p)
        {
            foreach (var face in ground)
            {
                Vector2 a = new Vector2(face.a.x, face.a.z), b = new Vector2(face.b.x, face.b.z), c = new Vector2(face.c.x, face.c.z);
                float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(denominator) < 0.000001f) continue;
                float u = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / denominator;
                float v = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / denominator;
                if (u >= -0.0001f && v >= -0.0001f && u + v <= 1.0001f) return u * face.a.y + v * face.b.y + (1f - u - v) * face.c.y;
            }
            throw new InvalidOperationException("Нет поверхности земли в точке " + p + ".");
        }

        public void ReserveBuilding()
        {
            // Inactive stages count too. Renderer.bounds/Collider.bounds on an
            // inactive stage can be empty, so transform the local asset bounds.
            foreach (var filter in fire.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.GetComponentInParent<Canvas>() == null && !Retired(filter.transform))
                    reserved.Add(WorldBounds(filter.sharedMesh.bounds, filter.transform.localToWorldMatrix));
            foreach (var box in fire.GetComponentsInChildren<BoxCollider>(true))
                if (!Retired(box.transform) && !RetiredCollider(box)) reserved.Add(WorldBounds(new Bounds(box.center, box.size), box.transform.localToWorldMatrix));
            foreach (var col in fire.GetComponentsInChildren<Collider>(true))
            {
                if (col is BoxCollider || Retired(col.transform) || RetiredCollider(col)) continue;
                if (col is MeshCollider mesh && mesh.sharedMesh != null) reserved.Add(WorldBounds(mesh.sharedMesh.bounds, mesh.transform.localToWorldMatrix));
                else if (col is SphereCollider sphere) reserved.Add(WorldBounds(new Bounds(sphere.center, Vector3.one * sphere.radius * 2f), sphere.transform.localToWorldMatrix));
                else if (col is CapsuleCollider capsule)
                {
                    Vector3 size = Vector3.one * capsule.radius * 2f; size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    reserved.Add(WorldBounds(new Bounds(capsule.center, size), capsule.transform.localToWorldMatrix));
                }
            }
            buildPoint = fire.Find("BuildPoint");
            approach = buildPoint != null ? new Vector2(buildPoint.position.x, buildPoint.position.z) - centre : Vector2.down;
            if (approach.sqrMagnitude < 0.01f) approach = Vector2.down;
            approach.Normalize();
        }

        bool Retired(Transform t) { return hearth != null && hearth.hidden.Any(h => h.target != null && t.IsChildOf(h.target.transform)); }
        bool RetiredCollider(Collider c) { return hearth != null && hearth.colliders.Any(h => h.target == c); }

        internal static Bounds WorldBounds(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return result;
        }

        static bool IntersectsXZ(Bounds a, Bounds b, float pad)
        {
            return a.min.x < b.max.x + pad && a.max.x > b.min.x - pad && a.min.z < b.max.z + pad && a.max.z > b.min.z - pad;
        }

        public bool Protected(Bounds bounds)
        {
            foreach (var b in reserved) if (IntersectsXZ(bounds, b, 0.45f)) return true;
            // A broad entrance remains open from the construction stand point
            // to the foreground, even when no IslandLayout path starts there.
            Vector2 offset = new Vector2(bounds.center.x, bounds.center.z) - centre;
            float along = Vector2.Dot(offset, approach), across = Mathf.Abs(offset.x * approach.y - offset.y * approach.x);
            float pad = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            return along > 1.5f && across < 1.25f + pad;
        }

        public bool ProtectedPose(Vector3 point, Quaternion rotation, Vector2 half)
        {
            Vector3 r = rotation * Vector3.right, f = rotation * Vector3.forward;
            Vector2 right = new Vector2(r.x, r.z), forward = new Vector2(f.x, f.z);
            Vector2 p = new Vector2(point.x, point.z);
            foreach (var bounds in reserved)
            {
                Vector2 delta = new Vector2(bounds.center.x, bounds.center.z) - p;
                bool separated = false;
                foreach (Vector2 axis in new[] { Vector2.right, Vector2.up, right, forward })
                {
                    float a = half.x * Mathf.Abs(Vector2.Dot(right, axis)) + half.y * Mathf.Abs(Vector2.Dot(forward, axis));
                    float b = (bounds.extents.x + 0.45f) * Mathf.Abs(axis.x) + (bounds.extents.z + 0.45f) * Mathf.Abs(axis.y);
                    if (Mathf.Abs(Vector2.Dot(delta, axis)) > a + b) { separated = true; break; }
                }
                if (!separated) return true;
            }
            Vector2 offset = p - centre;
            float along = Vector2.Dot(offset, approach), across = Mathf.Abs(offset.x * approach.y - offset.y * approach.x);
            return along > 1.5f && across < 1.25f + half.magnitude;
        }

        public bool TryPlace(float angle, float distance, Vector2 half, float height, out Vector3 point, out Quaternion rotation, bool widerSearch = false)
        {
            foreach (float radial in widerSearch ? new[] { 0f, 0.65f, 1.2f, 1.9f, 2.6f } : new[] { 0f, 0.65f, 1.2f })
                foreach (float delta in new[] { 0f, -8f, 8f, -16f, 16f, -24f, 24f, -32f, 32f, -40f, 40f })
                {
                    float a = (angle + delta) * Mathf.Deg2Rad;
                    Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (distance + radial);
                    rotation = Quaternion.LookRotation(new Vector3(centre.x - p.x, 0f, centre.y - p.y));
                    point = new Vector3(p.x, GroundY(p), p.y);
                    var bounds = WorldBounds(new Bounds(Vector3.up * height * 0.5f, new Vector3(half.x * 2f, height, half.y * 2f)), Matrix4x4.TRS(point, rotation, Vector3.one));
                    if (ProtectedPose(point, rotation, half) || occupied.Any(b => IntersectsXZ(bounds, b, 0.35f))) continue;
                    bool supported = true;
                    float low = point.y, high = point.y;
                    foreach (var local in new[] { Vector3.zero, new Vector3(-half.x, 0f, -half.y), new Vector3(half.x, 0f, -half.y), new Vector3(-half.x, 0f, half.y), new Vector3(half.x, 0f, half.y) })
                    {
                        Vector3 sample = point + rotation * local;
                        float y = GroundY(new Vector2(sample.x, sample.z)); low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                        if (field.EdgeDistance(sample.x, sample.z) < 3f || field.IsWater(sample.x, sample.z) || field.PathDistance(sample.x, sample.z) < 0.25f) supported = false;
                    }
                    if (!supported || high - low > 0.32f) continue;
                    bool blocked = false;
                    foreach (var col in Physics.OverlapBox(point + Vector3.up * (height * 0.5f + 0.1f), new Vector3(half.x, height * 0.5f, half.y), rotation, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (col is TerrainCollider || col.transform.IsChildOf(newRoot) || oldDecor.Any(g => col.transform.IsChildOf(g.transform)) ||
                            (previous != null && col.transform.IsChildOf(previous.transform))) continue;
                        blocked = true; break;
                    }
                    if (blocked) continue;
                    point.y = high - 0.025f;
                    bounds.center += Vector3.up * (point.y - bounds.min.y);
                    occupied.Add(bounds);
                    return true;
                }
            point = default; rotation = Quaternion.identity; return false;
        }
    }
}

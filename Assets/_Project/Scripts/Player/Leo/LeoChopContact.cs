using System.Collections.Generic;
using UnityEngine;

internal static class LeoChopContact
{
    sealed class Bark
    {
        public Vector3[] vertices;
        public int[] indices;
    }

    static readonly Dictionary<Mesh, Bark> meshes = new Dictionary<Mesh, Bark>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => meshes.Clear();

    public static bool TryPoint(ResourceItem item, Transform leo, out Vector3 point)
    {
        point = default;
        if (item == null || leo == null || !item.gameObject.activeInHierarchy) return false;
        Vector3 toward = Vector3.ProjectOnPlane(item.transform.position - leo.position, Vector3.up);
        if (toward.sqrMagnitude < .0001f) return false;
        Vector3 direction = toward.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        float scale = Mathf.Abs(leo.lossyScale.y);
        Vector3 axis = item.transform.position;
        axis.y = leo.position.y + 1.02f * scale;
        float side = Mathf.Min(item.TrunkRadius * .18f, .13f * scale);
        float length = Mathf.Max(1f, item.TrunkRadius + .40f);
        var ray = new Ray(axis + right * side - direction * length, direction);

        Transform generated = item.transform.Find("GenModel");
        if (generated != null && generated.TryGetComponent<MeshFilter>(out var filter))
        {
            float reach = length + item.TrunkRadius;
            if (IntersectBark(filter, ray, reach, out point)) return true;
            // Narrow trunks can move off the side ray during the squash.
            ray = new Ray(axis - direction * length, direction);
            if (IntersectBark(filter, ray, reach, out point)) return true;
        }

        Transform solid = item.transform.Find(TreeCollision.TrunkName);
        if (solid == null || !solid.TryGetComponent<Collider>(out var trunk) ||
            !trunk.Raycast(ray, out var hit, length + item.TrunkRadius))
            return false;
        point = hit.point;
        return true;
    }

    // Only the first submesh of each shared generated tree contains bark.
    // A local-space ray also follows the tree's squash and tilt.
    static bool IntersectBark(MeshFilter filter, Ray ray, float maxDistance, out Vector3 point)
    {
        point = default;
        Mesh mesh = filter.sharedMesh;
        if (mesh == null || !mesh.isReadable || mesh.subMeshCount == 0) return false;
        if (!meshes.TryGetValue(mesh, out Bark bark))
        {
            bark = new Bark { vertices = mesh.vertices, indices = mesh.GetTriangles(0) };
            meshes.Add(mesh, bark);
        }

        Transform t = filter.transform;
        Vector3 origin = t.InverseTransformPoint(ray.origin);
        Vector3 direction = t.InverseTransformVector(ray.direction);
        float closest = maxDistance;
        for (int i = 0; i < bark.indices.Length; i += 3)
        {
            Vector3 a = bark.vertices[bark.indices[i]];
            Vector3 e1 = bark.vertices[bark.indices[i + 1]] - a;
            Vector3 e2 = bark.vertices[bark.indices[i + 2]] - a;
            Vector3 cross = Vector3.Cross(direction, e2);
            float det = Vector3.Dot(e1, cross);
            if (Mathf.Abs(det) < .000001f) continue;
            Vector3 offset = origin - a;
            float u = Vector3.Dot(offset, cross) / det;
            if (u < 0f || u > 1f) continue;
            Vector3 q = Vector3.Cross(offset, e1);
            float v = Vector3.Dot(direction, q) / det;
            if (v < 0f || u + v > 1f) continue;
            float d = Vector3.Dot(e2, q) / det;
            if (d >= 0f && d < closest) closest = d;
        }
        if (closest >= maxDistance) return false;
        point = ray.GetPoint(closest);
        return true;
    }
}

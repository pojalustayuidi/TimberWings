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

    // TryPoint is called every frame while Leo chops. It used to run a CPU ray test over every bark
    // triangle and two Transform.Find string lookups each time. The answer only depends on Leo's
    // position and the tree's transform, so one slot remembers the children and the last result.
    static ResourceItem boundItem;
    static Transform generated, solid;
    static MeshFilter generatedFilter;
    static Collider trunkCollider;
    static bool hadGenerated, hadSolid;

    static ResourceItem memoItem;
    static Vector3 memoLeoPosition, memoItemPosition, memoItemScale;
    static Quaternion memoItemRotation;
    static float memoLeoScale, memoRadius;
    static bool memoValid, memoHit;
    static Vector3 memoPoint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        meshes.Clear();
        boundItem = memoItem = null;
        generated = solid = null;
        generatedFilter = null;
        trunkCollider = null;
        hadGenerated = hadSolid = memoValid = false;
        bindFrame = -100;
    }

    static int bindFrame = -100;

    static void Bind(ResourceItem item)
    {
        // Rebind when the tree changed, when a remembered child was destroyed and recreated, or
        // (at most twice a second) while a child is still missing, as the old per-frame Find did.
        bool changedItem = boundItem != item;
        bool destroyed = (hadGenerated && generated == null) || (hadSolid && solid == null);
        bool incomplete = (!hadGenerated || !hadSolid) && Time.frameCount - bindFrame >= 30;
        if (!changedItem && !destroyed && !incomplete) return;

        bool hadGeneratedBefore = hadGenerated, hadSolidBefore = hadSolid;
        boundItem = item;
        bindFrame = Time.frameCount;
        generated = item.transform.Find("GenModel");
        generatedFilter = generated != null && generated.TryGetComponent<MeshFilter>(out var filter) ? filter : null;
        solid = item.transform.Find(TreeCollision.TrunkName);
        trunkCollider = solid != null && solid.TryGetComponent<Collider>(out var collider) ? collider : null;
        hadGenerated = generated != null;
        hadSolid = solid != null;
        if (changedItem || destroyed || hadGeneratedBefore != hadGenerated || hadSolidBefore != hadSolid)
            memoValid = false;
    }

    static bool SameInputs(ResourceItem item, Transform leo)
    {
        if (!memoValid || memoItem != item) return false;
        Transform t = item.transform;
        return (leo.position - memoLeoPosition).sqrMagnitude < 2.5e-7f &&
               Mathf.Abs(Mathf.Abs(leo.lossyScale.y) - memoLeoScale) < 1e-5f &&
               Mathf.Abs(item.TrunkRadius - memoRadius) < 1e-5f &&
               (t.position - memoItemPosition).sqrMagnitude < 2.5e-7f &&
               (t.lossyScale - memoItemScale).sqrMagnitude < 1e-10f &&
               Mathf.Abs(Quaternion.Dot(t.rotation, memoItemRotation)) > .999999f;
    }

    public static bool TryPoint(ResourceItem item, Transform leo, out Vector3 point)
    {
        point = default;
        if (item == null || leo == null || !item.gameObject.activeInHierarchy) return false;
        Bind(item);
        if (SameInputs(item, leo))
        {
            point = memoPoint;
            return memoHit;
        }

        memoHit = Compute(item, leo, out memoPoint);
        memoItem = item;
        memoLeoPosition = leo.position;
        memoLeoScale = Mathf.Abs(leo.lossyScale.y);
        memoRadius = item.TrunkRadius;
        memoItemPosition = item.transform.position;
        memoItemScale = item.transform.lossyScale;
        memoItemRotation = item.transform.rotation;
        memoValid = true;
        point = memoPoint;
        return memoHit;
    }

    static bool Compute(ResourceItem item, Transform leo, out Vector3 point)
    {
        point = default;
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

        if (generatedFilter != null)
        {
            float reach = length + item.TrunkRadius;
            if (IntersectBark(generatedFilter, ray, reach, out point)) return true;
            // Narrow trunks can move off the side ray during the squash.
            ray = new Ray(axis - direction * length, direction);
            if (IntersectBark(generatedFilter, ray, reach, out point)) return true;
        }

        if (trunkCollider == null || !trunkCollider.Raycast(ray, out var hit, length + item.TrunkRadius))
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
        // A ray that misses the mesh bounds cannot hit any triangle: skip the whole loop.
        if (!RayHitsBounds(mesh.bounds, origin, direction, maxDistance)) return false;
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

    // Slab test in the mesh's local space; the box is padded so a grazing ray never gets rejected.
    static bool RayHitsBounds(Bounds bounds, Vector3 origin, Vector3 direction, float maxDistance)
    {
        bounds.Expand(.002f);
        Vector3 min = bounds.min, max = bounds.max;
        float near = 0f, far = maxDistance;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = origin[axis], d = direction[axis];
            if (Mathf.Abs(d) < 1e-8f)
            {
                if (o < min[axis] || o > max[axis]) return false;
                continue;
            }
            float inverse = 1f / d, t1 = (min[axis] - o) * inverse, t2 = (max[axis] - o) * inverse;
            if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }
            if (t1 > near) near = t1;
            if (t2 < far) far = t2;
            if (near > far) return false;
        }
        return true;
    }
}

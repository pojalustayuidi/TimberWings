using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// One local wound per tree. Only the triangles touching its footprint are
// split, and the exposed wood is folded inward while the rim stays joined to the bark.
internal sealed class TreeChopNotch : IDisposable
{
    readonly Transform root;
    readonly List<BarkMesh> models = new List<BarkMesh>(3);
    Mesh fallbackMesh;
    Transform fallback;

    internal TreeChopNotch(ResourceItem item)
    {
        root = item.transform;
        foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
        {
            string name = filter.gameObject.name;
            if (name == "Outline" || name == "Hull" || name == "HighlightRing" ||
                !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                continue;
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable || mesh.subMeshCount == 0 || mesh.blendShapeCount != 0 ||
                mesh.GetTopology(0) != MeshTopology.Triangles || mesh.vertexCount > 50000 ||
                mesh.HasVertexAttribute(VertexAttribute.TexCoord2))
                continue;
            // The trunk is submesh zero of GenModel and of TimberWings' authored species LODs.
            models.Add(new BarkMesh(root, filter, renderer));
        }
    }

    internal void Seat(ChopPrototypeSave.Standing notch)
    {
        Vector3 alongSurface = notch.localPoint - notch.localOutward *
            Vector3.Dot(notch.localPoint, notch.localOutward);
        var ray = new Ray(alongSurface + notch.localOutward * 3f, -notch.localOutward);
        float nearest = 3.25f;
        bool hit = false;
        // Prefer the near model. The same footprint is cut on the more distant LODs.
        foreach (var model in models)
        {
            if (model.IsDistantLod) continue;
            hit |= model.FindSurface(ray, ref nearest);
        }
        if (hit) notch.localPoint = ray.GetPoint(nearest);
        Vector3 radial = Vector3.ProjectOnPlane(notch.localPoint, Vector3.up);
        if (hit && radial.magnitude > .035f)
            notch.radius = Mathf.Clamp(radial.magnitude, .04f, notch.radius * 1.22f);
    }

    internal static float Depth(ChopPrototypeSave.Standing notch) =>
        notch.radius * .68f * Mathf.Pow(notch.damage, .78f);

    internal Vector3 Contact(ChopPrototypeSave.Standing notch)
    {
        Vector3 point = notch.localPoint;
        if (fallback == null) point -= notch.localOutward * Depth(notch);
        return root.TransformPoint(point);
    }

    internal void Draw(ChopPrototypeSave.Standing notch)
    {
        var shape = new Wound(notch);
        bool visibleMesh = false;
        foreach (var model in models)
        {
            bool changed = model.Cut(shape);
            if (changed && !model.IsDistantLod) visibleMesh = true;
        }
        if (!visibleMesh) DrawOnBark(notch, shape);
        else if (fallback != null) fallback.gameObject.SetActive(false);
    }

    // A readable trunk mesh is available for the generated and baked species.
    // Imported prefabs with an unreadable mesh get a small conforming wood inset.
    void DrawOnBark(ChopPrototypeSave.Standing notch, Wound shape)
    {
        if (fallback == null)
        {
            var go = new GameObject("Chop notch inset");
            go.layer = root.gameObject.layer;
            fallback = go.transform;
            fallback.SetParent(root, false);
            fallbackMesh = new Mesh { name = "Chop notch inset" };
            fallbackMesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = fallbackMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = TreeMeshFactory.Cut();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        fallback.gameObject.SetActive(true);
        fallback.position = root.TransformPoint(notch.localPoint);
        fallback.localRotation = Quaternion.LookRotation(notch.localOutward);
        fallback.localScale = new Vector3(shape.halfWidth, shape.halfHeight, notch.radius);
        // Curved edges sit against the estimated trunk; the split centre shades as two facets.
        if (fallbackMesh.vertexCount > 0) return;
        fallbackMesh.vertices = new[]
        {
            new Vector3(-1, 0, -.20f), new Vector3(0, -1, .015f),
            new Vector3(1, 0, -.20f), new Vector3(0, 1, .015f),
            new Vector3(0, .10f, -.05f), new Vector3(0, .10f, -.05f)
        };
        fallbackMesh.triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 5, 3, 0, 5 };
        fallbackMesh.RecalculateNormals();
        fallbackMesh.RecalculateBounds();
    }

    public void Dispose()
    {
        foreach (var model in models) model.Dispose();
        if (fallback != null)
        {
            fallback.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(fallback.gameObject);
        }
        if (fallbackMesh != null) UnityEngine.Object.Destroy(fallbackMesh);
    }

    readonly struct Wound
    {
        internal readonly Vector3 point, outward, right;
        internal readonly float radius, halfWidth, halfHeight, depth;
        internal Wound(ChopPrototypeSave.Standing notch)
        {
            point = notch.localPoint;
            outward = notch.localOutward;
            right = Vector3.Cross(Vector3.up, outward);
            radius = notch.radius;
            halfWidth = radius * Mathf.Lerp(.46f, .99f, Mathf.Sqrt(notch.damage));
            halfHeight = radius * Mathf.Lerp(.22f, .63f, notch.damage);
            depth = Depth(notch);
        }

        internal Vector3 PressIn(Vector3 vertex)
        {
            Vector3 delta = vertex - point;
            float side = Vector3.Dot(delta, right) / halfWidth;
            float vertical = delta.y / halfHeight;
            float rim = Mathf.Clamp01(1f - Mathf.Abs(side) - Mathf.Abs(vertical));
            float face = Mathf.Clamp01((Vector3.Dot(delta, outward) + radius * .64f) / (radius * .24f));
            return vertex - outward * (depth * rim * face);
        }
    }

    struct Vertex
    {
        internal Vector3 position, normal;
        internal Vector2 uv, uv2;
        internal Vector4 tangent;
        internal Color color;

        internal static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
        {
            position = Vector3.LerpUnclamped(a.position, b.position, t),
            normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
            uv = Vector2.LerpUnclamped(a.uv, b.uv, t),
            uv2 = Vector2.LerpUnclamped(a.uv2, b.uv2, t),
            tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t),
            color = Color.LerpUnclamped(a.color, b.color, t)
        };
    }

    sealed class BarkMesh : IDisposable
    {
        readonly MeshFilter filter;
        readonly MeshRenderer renderer;
        readonly Mesh original;
        readonly Material[] originalMaterials;
        readonly Material[] woundMaterials;
        readonly Matrix4x4 toMesh;
        readonly Vertex[] vertices;
        readonly int[] bark;
        readonly int[][] otherSubmeshes;
        readonly MeshTopology[] topology;
        readonly bool hasNormals, hasUv, hasUv2, hasTangent, hasColor;
        readonly List<Vector3> positions, normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>(), uv2 = new List<Vector2>();
        readonly List<Vector4> tangents = new List<Vector4>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> barkFaces, cutFaces = new List<int>(96);
        readonly List<Vertex> inside = new List<Vertex>(12), remaining = new List<Vertex>(12),
            outside = new List<Vertex>(12), split = new List<Vertex>(12), face = new List<Vertex>(12);
        readonly Plane[] boundaries = new Plane[5];
        Mesh mesh;

        internal bool IsDistantLod => filter.name == "LOD1" || filter.name == "LOD2";

        internal BarkMesh(Transform root, MeshFilter filter, MeshRenderer renderer)
        {
            this.filter = filter;
            this.renderer = renderer;
            original = filter.sharedMesh;
            originalMaterials = renderer.sharedMaterials;
            woundMaterials = new Material[original.subMeshCount + 1];
            for (int i = 0; i < original.subMeshCount && originalMaterials.Length > 0; i++)
                woundMaterials[i] = originalMaterials[Math.Min(i, originalMaterials.Length - 1)];
            woundMaterials[original.subMeshCount] = TreeMeshFactory.Cut();
            toMesh = filter.transform.worldToLocalMatrix * root.localToWorldMatrix;
            Matrix4x4 toTree = toMesh.inverse;
            var originalPositions = original.vertices;
            var originalNormals = original.normals;
            var originalUv = original.uv;
            var originalUv2 = original.uv2;
            var originalTangents = original.tangents;
            var originalColors = original.colors;
            int count = originalPositions.Length;
            hasNormals = originalNormals.Length == count;
            hasUv = originalUv.Length == count;
            hasUv2 = originalUv2.Length == count;
            hasTangent = originalTangents.Length == count;
            hasColor = originalColors.Length == count;
            vertices = new Vertex[count];
            positions = new List<Vector3>(count + 96);
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vertex
                {
                    position = toTree.MultiplyPoint3x4(originalPositions[i]),
                    normal = hasNormals ? originalNormals[i] : Vector3.up,
                    uv = hasUv ? originalUv[i] : default,
                    uv2 = hasUv2 ? originalUv2[i] : default,
                    tangent = hasTangent ? originalTangents[i] : default,
                    color = hasColor ? originalColors[i] : Color.white
                };
            }

            bark = original.GetTriangles(0);
            barkFaces = new List<int>(bark.Length + 96);
            otherSubmeshes = new int[original.subMeshCount - 1][];
            topology = new MeshTopology[otherSubmeshes.Length];
            for (int i = 0; i < otherSubmeshes.Length; i++)
            {
                otherSubmeshes[i] = original.GetIndices(i + 1);
                topology[i] = original.GetTopology(i + 1);
            }
        }

        internal bool FindSurface(Ray ray, ref float nearest)
        {
            bool found = false;
            for (int i = 0; i < bark.Length; i += 3)
            {
                Vector3 a = vertices[bark[i]].position;
                Vector3 e1 = vertices[bark[i + 1]].position - a;
                Vector3 e2 = vertices[bark[i + 2]].position - a;
                Vector3 cross = Vector3.Cross(ray.direction, e2);
                float determinant = Vector3.Dot(e1, cross);
                if (Mathf.Abs(determinant) < .000001f) continue;
                Vector3 from = ray.origin - a;
                float u = Vector3.Dot(from, cross) / determinant;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(from, e1);
                float v = Vector3.Dot(ray.direction, q) / determinant;
                if (v < 0f || u + v > 1f) continue;
                float distance = Vector3.Dot(e2, q) / determinant;
                if (distance < 0f || distance >= nearest) continue;
                nearest = distance;
                found = true;
            }
            return found;
        }

        internal bool Cut(Wound wound)
        {
            if (filter == null || renderer == null) return false;
            SetBoundaries(wound);
            positions.Clear();
            normals.Clear();
            uv.Clear();
            uv2.Clear();
            tangents.Clear();
            colors.Clear();
            barkFaces.Clear();
            cutFaces.Clear();
            foreach (var vertex in vertices) AddVertex(vertex, toMesh.MultiplyPoint3x4(vertex.position), vertex.normal);
            for (int i = 0; i < bark.Length; i += 3)
                CutTriangle(bark[i], bark[i + 1], bark[i + 2], wound);
            if (cutFaces.Count == 0) return false;

            if (mesh == null)
            {
                mesh = new Mesh { name = original.name + " (notched)" };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            if (positions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            if (hasNormals) mesh.SetNormals(normals);
            if (hasUv) mesh.SetUVs(0, uv);
            if (hasUv2) mesh.SetUVs(1, uv2);
            if (hasTangent) mesh.SetTangents(tangents);
            if (hasColor) mesh.SetColors(colors);
            mesh.subMeshCount = woundMaterials.Length;
            mesh.SetTriangles(barkFaces, 0, false);
            for (int i = 0; i < otherSubmeshes.Length; i++)
                mesh.SetIndices(otherSubmeshes[i], topology[i], i + 1, false);
            mesh.SetTriangles(cutFaces, woundMaterials.Length - 1, false);
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.bounds = original.bounds;
            if (filter.sharedMesh != mesh)
            {
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = woundMaterials;
            }
            return true;
        }

        void SetBoundaries(Wound wound)
        {
            Vector3 across = wound.right / wound.halfWidth;
            Vector3 vertical = Vector3.up / wound.halfHeight;
            boundaries[0] = new Plane(across + vertical, wound.point + wound.right * wound.halfWidth);
            boundaries[1] = new Plane(across - vertical, wound.point + wound.right * wound.halfWidth);
            boundaries[2] = new Plane(-across + vertical, wound.point - wound.right * wound.halfWidth);
            boundaries[3] = new Plane(-across - vertical, wound.point - wound.right * wound.halfWidth);
            boundaries[4] = new Plane(-wound.outward, wound.point - wound.outward * (wound.radius * .64f));
        }

        void CutTriangle(int a, int b, int c, Wound wound)
        {
            var va = vertices[a];
            var vb = vertices[b];
            var vc = vertices[c];
            foreach (var boundary in boundaries)
            {
                if (boundary.GetDistanceToPoint(va.position) > 0f &&
                    boundary.GetDistanceToPoint(vb.position) > 0f &&
                    boundary.GetDistanceToPoint(vc.position) > 0f)
                {
                    barkFaces.Add(a);
                    barkFaces.Add(b);
                    barkFaces.Add(c);
                    return;
                }
            }

            remaining.Clear();
            remaining.Add(va);
            remaining.Add(vb);
            remaining.Add(vc);
            List<Vertex> polygon = remaining, next = inside;
            foreach (var boundary in boundaries)
            {
                Split(polygon, boundary, next, outside);
                AddFaces(outside, false, wound);
                if (next.Count < 3) return;
                (polygon, next) = (next, polygon);
            }

            var fold = new Plane(Vector3.up, wound.point);
            for (int side = -1; side <= 1; side += 2)
            {
                Split(polygon, new Plane(wound.right * side, wound.point), split, null);
                Split(split, fold, face, outside);
                AddFaces(face, true, wound);
                AddFaces(outside, true, wound);
            }
        }

        static void Split(List<Vertex> source, Plane plane, List<Vertex> negative, List<Vertex> positive)
        {
            negative.Clear();
            positive?.Clear();
            if (source.Count == 0) return;
            Vertex previous = source[source.Count - 1];
            float last = plane.GetDistanceToPoint(previous.position);
            foreach (var vertex in source)
            {
                float distance = plane.GetDistanceToPoint(vertex.position);
                if ((last <= 0f) != (distance <= 0f))
                {
                    var edge = Vertex.Lerp(previous, vertex, last / (last - distance));
                    negative.Add(edge);
                    positive?.Add(edge);
                }
                if (distance <= 0f) negative.Add(vertex);
                else positive?.Add(vertex);
                previous = vertex;
                last = distance;
            }
        }

        void AddFaces(List<Vertex> polygon, bool freshWood, Wound wound)
        {
            if (polygon.Count < 3) return;
            Vertex corner = polygon[0];
            Vector3 a = Position(corner, freshWood, wound);
            var triangles = freshWood ? cutFaces : barkFaces;
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                var vb = polygon[i];
                var vc = polygon[i + 1];
                Vector3 b = Position(vb, freshWood, wound);
                Vector3 c = Position(vc, freshWood, wound);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-13f) continue;
                normal.Normalize();
                int index = positions.Count;
                AddVertex(corner, a, freshWood ? normal : corner.normal);
                AddVertex(vb, b, freshWood ? normal : vb.normal);
                AddVertex(vc, c, freshWood ? normal : vc.normal);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
            }
        }

        Vector3 Position(Vertex v, bool freshWood, Wound wound) =>
            toMesh.MultiplyPoint3x4(freshWood ? wound.PressIn(v.position) : v.position);

        void AddVertex(Vertex vertex, Vector3 position, Vector3 normal)
        {
            positions.Add(position);
            if (hasNormals) normals.Add(normal);
            if (hasUv) uv.Add(vertex.uv);
            if (hasUv2) uv2.Add(vertex.uv2);
            if (hasTangent) tangents.Add(vertex.tangent);
            if (hasColor) colors.Add(vertex.color);
        }

        public void Dispose()
        {
            if (mesh == null) return;
            if (filter != null && filter.sharedMesh == mesh)
            {
                filter.sharedMesh = original;
                if (renderer != null) renderer.sharedMaterials = originalMaterials;
            }
            UnityEngine.Object.Destroy(mesh);
        }
    }
}

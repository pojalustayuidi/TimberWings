using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWMysticRibbonInk
    {
        internal const string FrameName = "Ribbon_Ink_Frame";
        const string Keyword = "TW_RIBBON_INK";
        const string Paint = "_TWRibbonInk";
        const string InkMaterial = "Ribbon_Inscription.mat";
        const string Enabled = "_TWRibbonInkEnabled";

        internal sealed class Placement
        {
            internal MeshRenderer renderer;
            internal Vector3 point, size;
            internal Quaternion rotation;
            internal string diagnostic;
            internal bool legacy, approximate;
        }

        readonly struct Triangle
        {
            internal readonly Vector3 a, ab, ac, normal;
            internal Vector3 Center => a + (ab + ac) / 3f;

            internal Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                this.a = a;
                ab = b - a;
                ac = c - a;
                normal = Vector3.Cross(ab, ac).normalized;
            }
        }

        internal static Placement Prepare(Transform owner, StoryInspection inspection)
        {
            var cloth = owner.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.gameObject.activeInHierarchy &&
                    filter.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
                .ToArray();
            var authored = cloth.Where(filter => filter.name == TWRibbonStoryModel.BakedClothName).ToArray();
            if (authored.Length == 0)
            {
                var legacy = cloth.FirstOrDefault(filter => filter.name.StartsWith("06 · Inner side with six cuts",
                    System.StringComparison.Ordinal));
                if (legacy != null)
                    return new Placement { legacy = true, renderer = legacy.GetComponent<MeshRenderer>() };
                return new Placement { diagnostic = "Нужна активная ткань Ribbon_Cloth для нанесения зарубок." };
            }
            if (authored.Length != 1)
                return new Placement { diagnostic = "У ленты несколько активных тканей Ribbon_Cloth." };

            var target = authored[0];
            var clothRenderer = target.GetComponent<MeshRenderer>();
            if (clothRenderer.sharedMaterials.Length != 1 || clothRenderer.sharedMaterial == null ||
                !clothRenderer.sharedMaterial.HasProperty(Paint))
                return new Placement { diagnostic = "В материале ткани нет свойства Ribbon tally mask." };

            var result = new Placement { renderer = clothRenderer };
            if (target.transform.Find(FrameName) != null)
                return result;

            var mesh = target.sharedMesh;
            if (mesh == null)
                return new Placement { diagnostic = "У Ribbon_Cloth отсутствует меш." };

            Transform shot = inspection != null ? inspection.detail : null;
            var center = shot != null ? target.transform.InverseTransformPoint(shot.position) : mesh.bounds.center;
            var direction = shot != null ? target.transform.InverseTransformDirection(shot.forward + shot.right * .68f) :
                Vector3.forward;
            var right = shot != null ? target.transform.InverseTransformDirection(shot.right) : Vector3.right;
            if (mesh.isReadable && TryFit(mesh, center, direction.normalized, right.normalized, result))
                return result;

            // Retain an editable projection even on imported or heavily folded cloth.
            var up = Vector3.ProjectOnPlane(Vector3.up, direction);
            result.point = mesh.bounds.ClosestPoint(center);
            result.rotation = up.sqrMagnitude > .001f ? Quaternion.LookRotation(direction, up) :
                Quaternion.LookRotation(direction, Vector3.forward);
            float width = Mathf.Clamp(mesh.bounds.size.magnitude * .23f, .028f, .14f);
            result.size = new Vector3(width, width * .43f, .04f);
            result.approximate = true;
            return result;
        }

        static bool TryFit(Mesh mesh, Vector3 hint, Vector3 towardsCamera, Vector3 right, Placement result)
        {
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            var triangles = new List<Triangle>(indices.Length / 3);
            for (int i = 0; i < indices.Length; i += 3)
            {
                var triangle = new Triangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]);
                if (Vector3.Dot(triangle.normal, towardsCamera) > .19f)
                    triangles.Add(triangle);
            }
            if (triangles.Count == 0)
                return false;

            float length = mesh.bounds.size.magnitude * 1.5f + .01f;
            var seeds = triangles.OrderBy(triangle => (triangle.Center - hint).sqrMagnitude)
                .Take(35).Select(triangle => triangle.Center).ToList();
            seeds.Insert(0, hint);
            seeds.Insert(1, mesh.bounds.center);
            // A good-sized mark is preferable to an exact fit on a single fold.
            float[] widths = { .41f, .33f, .26f, .20f, .15f, .10f };
            foreach (float fraction in widths)
            foreach (var seed in seeds)
            {
                if (!Hit(triangles, seed, towardsCamera, length, out var point, out var normal))
                    continue;
                var across = Vector3.ProjectOnPlane(right, normal);
                if (across.sqrMagnitude < .02f)
                    continue;
                across.Normalize();
                var up = Vector3.Cross(normal, across).normalized;
                float width = VertexSpan(vertices, across);
                float height = VertexSpan(vertices, up);
                float halfWidth = Mathf.Min(.19f, width * fraction);
                float halfHeight = Mathf.Min(halfWidth * .43f, height * .30f);
                if (halfWidth < .017f || halfHeight < .006f)
                    continue;
                if (!CoversMarks(triangles, point, normal, across, up, halfWidth, halfHeight, length, out float depth))
                    continue;
                result.point = point;
                result.size = new Vector3(halfWidth, halfHeight, depth);
                result.rotation = Quaternion.LookRotation(normal, up);
                return true;
            }
            return false;
        }

        static float VertexSpan(Vector3[] vertices, Vector3 axis)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var vertex in vertices)
            {
                float value = Vector3.Dot(vertex, axis);
                low = Mathf.Min(low, value);
                high = Mathf.Max(high, value);
            }
            return high - low;
        }

        static bool CoversMarks(List<Triangle> triangles, Vector3 point, Vector3 normal, Vector3 across,
            Vector3 up, float width, float height, float length, out float depth)
        {
            var maximumShift = Mathf.Min(.055f, Mathf.Max(height * .72f, .008f));
            float deviation = 0f;
            depth = 0f;
            for (int column = 0; column < 6; column++)
            for (int row = -1; row <= 1; row++)
            {
                var sample = point + across * (width * Mathf.Lerp(-.74f, .74f, column / 5f)) +
                    up * (row * height * .67f);
                if (!Hit(triangles, sample, normal, length, out var hit, out _))
                    return false;
                float shift = Mathf.Abs(Vector3.Dot(hit - sample, normal));
                if (shift > maximumShift)
                    return false;
                deviation = Mathf.Max(deviation, shift);
            }
            depth = Mathf.Max(.011f, deviation * 1.5f + .004f);
            return true;
        }

        static bool Hit(List<Triangle> triangles, Vector3 target, Vector3 front, float length,
            out Vector3 point, out Vector3 normal)
        {
            var origin = target + front * length;
            var direction = -front;
            float nearest = 2f * length;
            point = normal = Vector3.zero;
            bool found = false;
            foreach (var face in triangles)
            {
                if (Vector3.Dot(face.normal, front) < .18f) continue;
                var cross = Vector3.Cross(direction, face.ac);
                float det = Vector3.Dot(face.ab, cross);
                if (Mathf.Abs(det) < 1e-8f) continue;
                var delta = origin - face.a;
                float u = Vector3.Dot(delta, cross) / det;
                if (u < -.0003f || u > 1.0003f) continue;
                var side = Vector3.Cross(delta, face.ab);
                float v = Vector3.Dot(direction, side) / det;
                if (v < -.0003f || u + v > 1.0003f) continue;
                float distance = Vector3.Dot(face.ac, side) / det;
                if (distance < 0f || distance >= nearest) continue;
                nearest = distance;
                normal = face.normal;
                found = true;
            }
            if (found)
                point = origin + direction * nearest;
            return found;
        }

        internal static void Apply(Placement data, Texture2D mask, string folder, List<string> created)
        {
            if (data.legacy)
            {
                Debug.Log("Круг молчания: у ленты на старом маркере уже запечены пять зарубок и одна незаконченная.",
                    data.renderer);
                return;
            }
            if (!string.IsNullOrEmpty(data.diagnostic))
            {
                Debug.LogWarning("Круг молчания: " + data.diagnostic);
                return;
            }

            var cloth = data.renderer.transform;
            var frame = cloth.Find(FrameName);
            if (frame == null)
            {
                frame = TWMysticClearingPolish.Child(cloth, FrameName, data.point, data.rotation);
                Undo.RecordObject(frame, TWMysticClearingPolish.UndoName);
                frame.localScale = data.size;
                if (data.approximate)
                    Debug.LogWarning("Круг молчания: рамка зарубок поставлена ориентировочно. Уточни её в Scene View.", frame);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + InkMaterial);
            bool fresh = material == null;
            if (fresh)
                material = new Material(data.renderer.sharedMaterial) { name = "Torn ribbon – six tallies" };
            else
                Undo.RecordObject(material, TWMysticClearingPolish.UndoName);

            material.SetTexture(Paint, mask);
            material.SetFloat(Enabled, 1f);
            material.EnableKeyword(Keyword);
            SetProjection(material, frame);
            if (fresh)
                TWMysticClearingSurface.SaveNew(material, folder, InkMaterial, created);
            else
                AssetDatabase.SaveAssetIfDirty(material);
            TWMysticClearingSurface.ApplyMaterial(data.renderer, material);
        }

        internal static bool UpdatePlacement(Transform frame)
        {
            var renderer = frame.parent != null ? frame.parent.GetComponent<MeshRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null ||
                !renderer.sharedMaterial.IsKeywordEnabled(Keyword))
                return false;
            var material = renderer.sharedMaterial;
            Undo.RecordObject(material, "Метки на ленте");
            SetProjection(material, frame);
            AssetDatabase.SaveAssetIfDirty(material);
            SceneView.RepaintAll();
            return true;
        }

        static void SetProjection(Material material, Transform frame)
        {
            var rotation = frame.localRotation;
            var center = frame.localPosition;
            var size = frame.localScale;
            var normal = rotation * Vector3.forward;
            var u = (rotation * Vector3.right) / Mathf.Max(.002f, 2f * Mathf.Abs(size.x));
            var v = (rotation * Vector3.up) / Mathf.Max(.002f, 2f * Mathf.Abs(size.y));
            var depth = normal / Mathf.Max(.002f, Mathf.Abs(size.z));
            material.SetVector("_TWRibbonInkU", Row(u, center, .5f));
            material.SetVector("_TWRibbonInkV", Row(v, center, .5f));
            material.SetVector("_TWRibbonInkDepth", Row(depth, center, 0f));
            material.SetVector("_TWRibbonInkFacing", new Vector4(normal.x, normal.y, normal.z, 0f));
            EditorUtility.SetDirty(material);
        }

        static Vector4 Row(Vector3 axis, Vector3 center, float offset) =>
            new Vector4(axis.x, axis.y, axis.z, offset - Vector3.Dot(axis, center));
    }
}

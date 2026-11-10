using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    // Authored project rocks, placed in a few deliberate groups. No bank ribbons,
    // cubes beside the waterfall or randomly fractured procedural boulders.
    internal static class TWRiverBanks
    {
        static readonly string[] Rocks =
        {
            "Assets/_Project/Prefabs/Rock1.prefab",
            "Assets/_Project/Prefabs/Rock2.prefab",
            "Assets/_Project/Prefabs/Rock3.prefab"
        };

        public static float Cover(TWRiverCtx x, float s, int side)
        {
            float n = TWNoise.Val1(s * 0.055f, x.seed + side * 13);
            return TWRiverSpec.Smooth(0.5f, 0.8f, n) * (side == x.rockSide ? 0.75f : 0.2f);
        }

        public static void Dress(Transform parent, TWRiverCtx x)
        {
            var group = new GameObject("Project_Rock_Groups").transform;
            group.SetParent(parent, false);
            int index = 0;
            for (float s = TWRiverSpec.MouthDist + 10f; s < x.ax.Length - 11f; s += 22f)
            {
                int side = index % 3 == 2 ? -x.rockSide : x.rockSide;
                Place(group, x, s, side * (x.half + 1.8f), 1.15f, index, false);
                Place(group, x, s + 1.5f, side * (x.half + 2.2f), 0.62f, index + 1, false);
                if (index % 2 == 0) Place(group, x, s + 0.8f, side * (x.half - 0.8f), 0.65f, index + 2, true);
                index++;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // Smaller authored stones blend the large headland masses into
                // the banks, with unequal group sizes on the two sides.
                Place(group, x, TWRiverSpec.MouthDist - (side > 0 ? 0.6f : 1.8f),
                    side * (x.half + 1.7f), side > 0 ? 2.8f : 2.3f, side + 2, false);
                Place(group, x, TWRiverSpec.MouthDist + 1.2f, side * (x.half + 3.4f), 1.0f, side + 3, false);
            }
        }

        static void Place(Transform parent, TWRiverCtx x, float s, float off, float radius, int variant, bool wet)
        {
            Vector3 target = x.OnGround(s, off, 0f);
            if (!x.f.Inside(target.x, target.z) || x.f.EdgeDistance(target.x, target.z) < 3f ||
                x.Protected(target, 1.5f) || (!wet && x.f.PathWeight(target.x, target.z) > 0.1f)) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rocks[Mathf.Abs(variant) % Rocks.Length]);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = wet ? "River_Boulder" : "Shore_Boulder";
            go.transform.localPosition = target;
            go.transform.localRotation = Quaternion.Euler(0f, variant * 67f + 19f, 0f);
            go.transform.localScale = Vector3.one;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Object.DestroyImmediate(go); return; }
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float scale = radius * 2f / Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z));
            go.transform.localScale = Vector3.one * scale;
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 shift = new Vector3(target.x - b.center.x, 0f, target.z - b.center.z);
            shift.y = wet ? x.Yw(s) + 0.22f - b.max.y : target.y - b.min.y - b.size.y * 0.28f;
            go.transform.position += shift;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            if (wet) x.stones.Add(new Vector4(s, off, x.Yw(s), radius));
        }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using Surface = UnityEngine.PhysicsMaterial;
#else
using Surface = UnityEngine.PhysicMaterial;
#endif

namespace IslandV2
{
    // Authored meshes are imported once. Scene building only places them and wires existing systems.
    internal static class TWMysticCedar
    {
        internal const string AssetRoot = "Assets/_Project/Art/MysticCedar/";

        internal static GameObject PlaceModel(string file, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + file);
            if (asset == null) throw new InvalidOperationException("Не импортирована модель " + AssetRoot + file);
            var go = UnityEngine.Object.Instantiate(asset, parent, false);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        internal static void Build(TWMysticContext x, TWMysticAssets assets, Surface surface)
        {
            // Preserve the LandmarkTree object, save references and interaction. Retire its old visible model.
            var renderers = x.state.worldRenderers.ToList();
            foreach (var r in x.elder.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderers.Any(v => v.target == r))
                    renderers.Add(new MysticArtState.RendererRecord { target = r, enabled = r.enabled });
                Undo.RecordObject(r, "Replace elder model");
                r.enabled = false;
            }
            x.state.worldRenderers = renderers.ToArray();
            var colliders = x.state.worldCollisions.ToList();
            foreach (var c in x.elder.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                if (!colliders.Any(v => v.target == c))
                    colliders.Add(new MysticArtState.ColliderRecord { target = c, json = EditorJsonUtility.ToJson(c) });
                Undo.RecordObject(c, "Replace elder collision");
                c.enabled = false;
            }
            x.state.worldCollisions = colliders.ToArray();
            var lods = x.state.elderLods.ToList();
            foreach (var lod in x.elder.GetComponentsInChildren<LODGroup>(true))
            {
                if (!lods.Any(v => v.target == lod)) lods.Add(new MysticArtState.LodRecord { target = lod, enabled = lod.enabled });
                Undo.RecordObject(lod, "Retire elder LOD");
                lod.enabled = false;
            }
            x.state.elderLods = lods.ToArray();
            var sacred = x.elder.GetComponent<SacredVibe>();
            if (sacred != null) TWMysticArt.Disable(x.state, sacred);
            foreach (var fire in x.elder.GetComponentsInChildren<FireSource>(true))
                TWMysticArt.Disable(x.state, fire);
            foreach (var light in x.elder.GetComponentsInChildren<Light>(true))
                TWMysticArt.Disable(x.state, light);
            foreach (var flicker in x.elder.GetComponentsInChildren<LightFlicker>(true))
                TWMysticArt.Disable(x.state, flicker);

            var tree = PlaceModel("MysticCedar.fbx", x.root, "Cedar_Landmark");
            tree.transform.position = x.Ground(Vector2.zero);
            foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = renderer.name.StartsWith("Rune_", StringComparison.Ordinal) ? assets.veil : assets.scenery;
                if (renderer.name.StartsWith("Rune_", StringComparison.Ordinal))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            // Smooth trunk collision leaves the perimeter path and the low swing clear.
            var capsule = tree.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(-0.2f, 2.1f, 0);
            capsule.radius = 1.25f;
            capsule.height = 4.6f;
            capsule.sharedMaterial = surface;
            var trunkObstacle = tree.AddComponent<NavMeshObstacle>();
            trunkObstacle.carving = true;
            trunkObstacle.shape = NavMeshObstacleShape.Capsule;
            trunkObstacle.center = capsule.center;
            trunkObstacle.radius = capsule.radius;
            trunkObstacle.height = capsule.height;
            AddSwingSeatCollider(tree.transform, surface);

            Vector2 point = x.Site("SecretBench", new Vector2(1.5f, -9.5f));
            float yaw = Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg;
            var bench = x.Prop(assets, TWMysticModels.Bench(false), "SecretBench", "Седьмая_скамья_komarov", point, yaw);
            if (bench.GetComponent<BoxCollider>() == null)
            {
                var c = bench.AddComponent<BoxCollider>();
                c.center = new Vector3(0, 0.7f, -0.06f);
                c.size = new Vector3(3.1f, 1.4f, 0.75f);
                c.sharedMaterial = surface;
            }
            if (bench.GetComponent<NavMeshObstacle>() == null)
            {
                var obstacle = bench.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0, 0.7f, -0.06f);
                obstacle.size = new Vector3(3.1f, 1.4f, 0.75f);
            }
            // A continuous back panel supports the delicate night inlay.
            if (bench.transform.Find("Signature_Backboard") == null)
            {
                var board = new TWMysticModel();
                TWMysticModels.Box(board, "Wood", new Vector3(0, 1.0f, -0.35f), new Vector3(2.6f, 0.84f, 0.13f));
                board.Finish(assets, bench.transform, "Signature_Backboard", bench.transform.position, bench.transform.rotation);
            }
            if (bench.transform.Find("Signature_Inlay") == null)
            {
                var signature = PlaceModel("BenchSignature.fbx", bench.transform, "Signature_Inlay");
                signature.transform.localPosition = new Vector3(0, 1.11f, -0.424f);
                signature.transform.localRotation = Quaternion.Euler(0, 180, 0);
                foreach (var renderer in signature.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = assets.veil;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.enabled = false;
                }
            }
            // Small physical bridge symbol on Gunnar's keepsake.
            var memory = x.root.GetComponentsInChildren<MysticProp>(true).FirstOrDefault(p => p.key == "Gunnar_Memory");
            if (memory != null && memory.transform.Find("Bridge_Engraving") == null)
            {
                var sign = new TWMysticModel();
                TWMysticModels.Box(sign, "Wood", new Vector3(0, 0.63f, 0), new Vector3(0.72f, 0.65f, 0.08f));
                for (int i = 0; i < 9; i++)
                {
                    float xx = -0.26f + i * 0.065f;
                    float y = 0.59f + xx * xx * 1.6f;
                    TWMysticModels.Rod(sign, "DarkWood", new Vector3(xx, y, 0.046f), new Vector3(xx, y + 0.11f, 0.046f), 0.009f);
                }
                TWMysticModels.Rod(sign, "DarkWood", new Vector3(-0.3f, 0.5f, 0.046f), new Vector3(-0.3f, 0.85f, 0.046f), 0.015f);
                TWMysticModels.Rod(sign, "DarkWood", new Vector3(0.3f, 0.5f, 0.046f), new Vector3(0.3f, 0.85f, 0.046f), 0.015f);
                sign.Finish(assets, memory.transform, "Bridge_Engraving", memory.transform.position, memory.transform.rotation);
            }
        }

        static void AddSwingSeatCollider(Transform cedar, Surface surface)
        {
            var pivot = cedar.GetComponentsInChildren<Transform>(true).First(t => t.name == "SwingMotion");
            var seatMesh = pivot.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Swing_Wood");
            var seat = new GameObject("Swing_Seat_Collision").transform;
            seat.SetParent(pivot, false);
            seat.rotation = cedar.rotation;

            Matrix4x4 toSeat = seat.worldToLocalMatrix * seatMesh.transform.localToWorldMatrix;
            Vector3[] points = seatMesh.sharedMesh.vertices.Select(p => toSeat.MultiplyPoint3x4(p)).ToArray();
            float bottom = points.Min(p => p.y);
            float top = points.Max(p => p.y);
            // The imported mesh contains both ropes and the seat. Fit the box to the lowest part only.
            float seatTop = bottom + (top - bottom) * 0.09f;
            var lowerPoints = points.Where(p => p.y <= seatTop).ToArray();
            var bounds = new Bounds(lowerPoints[0], Vector3.zero);
            foreach (var point in lowerPoints)
                bounds.Encapsulate(point);

            Vector3 size = Vector3.Scale(bounds.size, seat.lossyScale);
            if (size.x < 0.5f || size.y < 0.04f || size.y > 0.38f || size.z < 0.25f)
                throw new InvalidOperationException("Не удалось подобрать коллайдер сиденья. Проверь масштаб импорта MysticCedar.fbx.");

            var collider = seat.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            collider.sharedMaterial = surface;
        }
    }
}

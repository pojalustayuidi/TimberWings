using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace IslandV2
{
    internal static class TWCenterProps
    {
        static readonly Color White = Color.white;
        static readonly Quaternion Identity = Quaternion.identity;

        public static void Build(TWCenterContext x, TWCenterAssets assets, Transform root, CenterArtState state)
        {
            Physics.SyncTransforms();
            Transform left = null;
            foreach (float angle in new[] { 180f, -18f })
            {
                if (!x.TryPlace(angle, x.radius - 0.15f, new Vector2(1.6f, 0.56f), 0.75f, out Vector3 p, out Quaternion q)) continue;
                var model = new TWCenterModel(); Bench(model);
                var go = model.Finish(assets, root, "Curved_Bench_" + state.benches, p, q);
                // Several short seats follow the curve; no oversized collider
                // across the clearing or open trail entrances.
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.Lerp(-14f, 14f, (i + 0.5f) / 5f) * Mathf.Deg2Rad;
                    Vector3 centre = new Vector3(Mathf.Sin(a) * 6.0f, 0.285f, 6.0f * (1f - Mathf.Cos(a)));
                    var colGo = new GameObject("Seat_Collision_" + i); colGo.transform.SetParent(go.transform, false);
                    colGo.transform.localPosition = centre; colGo.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    colGo.AddComponent<BoxCollider>().size = new Vector3(0.62f, 0.57f, 0.54f);
                }
                Obstacle(go, new Vector3(0f, 0.30f, 0.10f), new Vector3(3.05f, 0.60f, 0.78f));
                if (left == null) left = go.transform;
                state.benches++;
            }

            float rackAngle = 180f, rackDistance = x.radius + 1.8f;
            if (left != null)
            {
                Vector2 offset = new Vector2(left.position.x, left.position.z) - x.centre;
                rackAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                rackDistance = offset.magnitude + 1.9f;
            }
            if (x.TryPlace(rackAngle, rackDistance, new Vector2(1.2f, 0.7f), 1.95f, out Vector3 rp, out Quaternion rq))
            {
                var model = new TWCenterModel(); WoodRack(model);
                var go = model.Finish(assets, root, "Covered_Firewood_Rack", rp, rq);
                var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 0.82f, 0f); col.size = new Vector3(2.1f, 1.64f, 1.05f);
                Obstacle(go, col.center, col.size);
            }
            if (x.TryPlace(-25f, x.radius + 0.1f, new Vector2(0.44f, 0.44f), 0.8f, out Vector3 sp, out Quaternion sq))
            {
                var model = new TWCenterModel(); StumpAndCup(model);
                var go = model.Finish(assets, root, "Stump_With_Cup", sp, sq);
                var col = go.AddComponent<CapsuleCollider>(); col.radius = 0.34f; col.height = 0.48f; col.center = Vector3.up * 0.24f;
                Obstacle(go, col.center, new Vector3(0.68f, 0.48f, 0.68f));
            }

            var lamps = new List<Transform>();
            foreach (float angle in new[] { 215f, 143f, 40f })
            {
                if (!x.TryPlace(angle, x.radius + 0.7f, new Vector2(0.35f, 0.65f), 3.0f, out Vector3 p, out Quaternion q, true)) continue;
                var model = new TWCenterModel(); Lantern(model);
                var go = model.Finish(assets, root, "Timber_Lantern_" + lamps.Count, p, q);
                var glass = new TWCenterModel();
                glass.Part("Glass").Box(new Vector3(0f, 2.22f, 0.37f), new Vector3(0.25f, 0.35f, 0.25f), Identity, White);
                var bulb = glass.Finish(assets, go.transform, "Lantern_Glass_" + lamps.Count, p, q).GetComponent<MeshRenderer>();
                bulb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fire = go.AddComponent<FireSource>();
                fire.preset = FlickerPreset.Lantern; fire.color = new Color(1f, 0.77f, 0.43f);
                fire.intensity = 2.1f; fire.range = 7.0f; fire.lightOffset = new Vector3(0f, 2.22f, 0.37f);
                fire.emissive = new Renderer[] { bulb }; fire.emissiveStrength = 2.1f;
                fire.glow = true; fire.glowSize = 0.7f; fire.glowPoints = new[] { fire.lightOffset };
                var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 1.30f, 0f); col.size = new Vector3(0.19f, 2.60f, 0.19f);
                Obstacle(go, col.center, col.size);
                lamps.Add(go.transform); state.lanterns++;
            }
            var back = lamps.Where(t => t.position.z > x.centre.y + 1f).OrderByDescending(t => t.position.z).Take(2).ToArray();
            if (back.Length == 2) Garland(assets, root, back[0].TransformPoint(new Vector3(0f, 2.77f, 0f)), back[1].TransformPoint(new Vector3(0f, 2.77f, 0f)));
            BorderAndPlants(x, assets, root);
            if (state.benches < 2 || state.lanterns < 3)
                Debug.LogWarning("CENTER ART: размещено лавочек " + state.benches + "/2, фонарей " + state.lanterns + "/3. Остальные пропущены ради свободных троп и существующего здания.");
        }

        static void Beam(SoftMesh mesh, Vector3 a, Vector3 b, float width, float depth)
        {
            Vector3 dir = b - a;
            mesh.Box((a + b) * 0.5f, new Vector3(width, depth, dir.magnitude), Quaternion.LookRotation(dir), White);
        }

        static void Obstacle(GameObject go, Vector3 centre, Vector3 size)
        {
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = centre; obstacle.size = size;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
        }

        static void Bench(TWCenterModel model)
        {
            var wood = model.Part("Wood"); var dark = model.Part("DarkWood"); var ends = model.Part("EndGrain");
            // Two curved rows of broad seat planks. Five deliberate segments
            // are enough to read as handmade timber at the gameplay camera.
            for (int i = 0; i < 5; i++)
            {
                float a0 = Mathf.Lerp(-14f, 14f, (float)i / 5f) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(-14f, 14f, (float)(i + 1) / 5f) * Mathf.Deg2Rad;
                float a = (a0 + a1) * 0.5f;
                Vector3 p = new Vector3(Mathf.Sin(a) * 6.0f, 0.50f, 6.0f * (1f - Mathf.Cos(a)));
                Quaternion q = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                for (int row = -1; row <= 1; row += 2)
                    wood.Box(p + q * new Vector3(0f, 0f, row * 0.15f), new Vector3(0.565f, 0.14f, 0.28f), q, White);
                if (i == 0 || i == 4)
                {
                    dark.Box(p + Vector3.down * 0.29f, new Vector3(0.20f, 0.46f, 0.58f), q, White);
                    ends.Box(p + q * new Vector3(0f, -0.31f, 0.298f), new Vector3(0.18f, 0.39f, 0.014f), q, White);
                }
                dark.Box(p + Vector3.down * 0.16f, new Vector3(0.565f, 0.10f, 0.22f), q, White);
            }
        }

        static void WoodRack(TWCenterModel model)
        {
            var dark = model.Part("DarkWood"); var wood = model.Part("Wood"); var ends = model.Part("EndGrain");
            foreach (int side in new[] { -1, 1 })
                foreach (int back in new[] { -1, 1 })
                    dark.Box(new Vector3(side * 0.96f, 0.80f, back * 0.43f), new Vector3(0.12f, 1.6f, 0.12f), Identity, White);
            dark.Box(new Vector3(0f, 0.14f, 0f), new Vector3(2.02f, 0.15f, 0.96f), Identity, White);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    Vector3 p = new Vector3((i - (3 - row) * 0.5f) * 0.43f, 0.40f + row * 0.34f, 0.02f + (i % 2) * 0.035f);
                    wood.Cyl(p + Vector3.back * 0.47f, p + Vector3.forward * 0.51f, 0.20f, 0.19f, White, 7);
                    ends.Cyl(p + Vector3.forward * 0.512f, p + Vector3.forward * 0.524f, 0.171f, 0.171f, White, 7);
                    dark.Cyl(p + Vector3.forward * 0.526f, p + Vector3.forward * 0.532f, 0.045f, 0.045f, White, 6);
                }
            Quaternion slope = Quaternion.Euler(-11f, 0f, 0f);
            for (int i = 0; i < 5; i++)
                wood.Box(new Vector3((i - 2) * 0.46f, 1.72f, 0f), new Vector3(0.45f, 0.12f, 1.36f), slope, White);
            Beam(dark, new Vector3(-0.96f, 1.44f, 0.43f), new Vector3(-0.52f, 0.90f, 0.43f), 0.09f, 0.09f);
            Beam(dark, new Vector3(0.96f, 1.44f, 0.43f), new Vector3(0.52f, 0.90f, 0.43f), 0.09f, 0.09f);
        }

        static void StumpAndCup(TWCenterModel model)
        {
            model.Part("DarkWood").Cyl(Vector3.down * 0.03f, Vector3.up * 0.48f, 0.38f, 0.31f, White, 9);
            model.Part("EndGrain").Cyl(Vector3.up * 0.48f, Vector3.up * 0.50f, 0.30f, 0.30f, White, 9);
            var cup = model.Part("Cup"); Vector3 p = new Vector3(0.05f, 0.51f, 0.02f);
            cup.Cyl(p, p + Vector3.up * 0.23f, 0.095f, 0.115f, White, 9);
            model.Part("DarkWood").Cyl(p + Vector3.up * 0.232f, p + Vector3.up * 0.236f, 0.088f, 0.088f, White, 9);
            for (int i = 0; i < 5; i++)
            {
                float a = (-90f + i * 36f) * Mathf.Deg2Rad, b = (-90f + (i + 1) * 36f) * Mathf.Deg2Rad;
                Vector3 c = p + new Vector3(0.105f, 0.12f, 0f);
                cup.Cyl(c + new Vector3(Mathf.Cos(a) * 0.085f, Mathf.Sin(a) * 0.085f, 0f), c + new Vector3(Mathf.Cos(b) * 0.085f, Mathf.Sin(b) * 0.085f, 0f), 0.017f, 0.017f, White, 5);
            }
        }

        static void Lantern(TWCenterModel model)
        {
            var wood = model.Part("DarkWood"); var warm = model.Part("Wood"); var rope = model.Part("Rope"); var metal = model.Part("Metal");
            wood.Box(new Vector3(0f, 1.37f, 0f), new Vector3(0.18f, 2.82f, 0.18f), Identity, White);
            warm.Box(new Vector3(0f, 2.73f, 0.21f), new Vector3(0.15f, 0.16f, 0.78f), Identity, White);
            Beam(wood, new Vector3(0f, 2.25f, 0.01f), new Vector3(0f, 2.66f, 0.43f), 0.065f, 0.065f);
            for (int i = 0; i < 3; i++) rope.Box(new Vector3(0f, 2.63f + i * 0.055f, 0f), new Vector3(0.205f, 0.027f, 0.205f), Identity, White);
            metal.Cyl(new Vector3(0f, 2.53f, 0.37f), new Vector3(0f, 2.65f, 0.37f), 0.018f, 0.018f, White, 5);
            metal.Box(new Vector3(0f, 2.0f, 0.37f), new Vector3(0.36f, 0.06f, 0.36f), Identity, White);
            metal.Box(new Vector3(0f, 2.44f, 0.37f), new Vector3(0.39f, 0.08f, 0.39f), Identity, White);
            warm.Box(new Vector3(0f, 2.50f, 0.37f), new Vector3(0.28f, 0.07f, 0.28f), Identity, White);
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                metal.Box(new Vector3(sx * 0.145f, 2.22f, 0.37f + sz * 0.145f), new Vector3(0.025f, 0.42f, 0.025f), Identity, White);
        }

        static void Garland(TWCenterAssets assets, Transform root, Vector3 a, Vector3 b)
        {
            var wire = new TWCenterModel(); var bulbs = new TWCenterModel(); var points = new List<Vector3>();
            Vector3 previous = a;
            int segments = Mathf.Max(8, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.65f));
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (0.28f * 4f * t * (1f - t));
                wire.Part("Rope").Cyl(previous, p, 0.016f, 0.016f, White, 5); previous = p;
                if (i % 2 != 0 || i == segments) continue;
                Vector3 centre = p + Vector3.down * 0.11f;
                bulbs.Part("Glass").Puff(centre, new Vector3(0.075f, 0.105f, 0.075f), White, 0f, 7, 4, 0f); points.Add(centre);
                wire.Part("Metal").Cyl(p, centre + Vector3.up * 0.06f, 0.016f, 0.016f, White, 5);
            }
            wire.Finish(assets, root, "Rear_Light_String", Vector3.zero, Identity);
            var go = bulbs.Finish(assets, root, "Rear_String_Bulbs", Vector3.zero, Identity);
            var fs = go.AddComponent<FireSource>(); fs.preset = FlickerPreset.Garland; fs.simOnly = true;
            fs.color = new Color(1f, 0.81f, 0.45f); fs.emissive = new Renderer[] { go.GetComponent<MeshRenderer>() };
            fs.emissiveStrength = 1.7f; fs.glow = true; fs.glowSize = 0.30f; fs.glowPoints = points.ToArray();
        }

        static void BorderAndPlants(TWCenterContext x, TWCenterAssets assets, Transform root)
        {
            var model = new TWCenterModel();
            foreach (float angle in new[] { 32f, 44f, 52f, 150f, 161f, 174f, 198f, 208f, 219f, 229f, 320f, 330f, 341f, 351f })
            {
                float a = angle * Mathf.Deg2Rad; Vector2 p = x.centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (x.radius - 0.35f);
                var bounds = new Bounds(new Vector3(p.x, 0f, p.y), new Vector3(0.95f, 0.3f, 0.8f));
                if (x.Protected(bounds) || x.field.PathDistance(p.x, p.y) < 0.7f || x.occupied.Any(b => b.SqrDistance(bounds.center) < 0.6f)) continue;
                model.Part("Stone").Puff(new Vector3(p.x, x.GroundY(p) + 0.035f, p.y), new Vector3(0.48f, 0.13f, 0.35f), White, -angle, 7, 3, 0f);
            }
            for (int i = 0; i < 32; i++)
            {
                float a = (i * 137.5f + 20f) * Mathf.Deg2Rad;
                Vector2 p = x.centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (x.radius + 0.35f + 1.3f * Mathf.PerlinNoise(i * 0.7f, 4.8f));
                var bounds = new Bounds(new Vector3(p.x, 0f, p.y), Vector3.one * 0.5f);
                if (x.Protected(bounds) || x.field.PathDistance(p.x, p.y) < 0.65f || x.occupied.Any(b => b.min.x - 0.1f < p.x && b.max.x + 0.1f > p.x && b.min.z - 0.1f < p.y && b.max.z + 0.1f > p.y)) continue;
                Vector3 origin = new Vector3(p.x, x.GroundY(p) - 0.035f, p.y);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 stem = origin + new Vector3((k - 1) * 0.18f, 0f, Mathf.Sin(k * 2.3f) * 0.16f);
                    float height = 0.28f + k * 0.07f;
                    model.Part("Leaf").Cyl(stem, stem + Vector3.up * height, 0.016f, 0.010f, White, 5);
                    model.Part("LeafLight").Puff(stem + new Vector3(0.09f, 0.08f, 0f), new Vector3(0.14f, 0.035f, 0.06f), White, i * 31f, 6, 3, 0f);
                    string colour = i % 3 == 0 ? "PurpleFlower" : "CreamFlower";
                    for (int petal = 0; petal < 4; petal++)
                    {
                        float theta = petal * Mathf.PI * 0.5f;
                        model.Part(colour).Puff(stem + new Vector3(Mathf.Cos(theta) * 0.06f, height, Mathf.Sin(theta) * 0.06f), new Vector3(0.075f, 0.040f, 0.065f), White, petal * 90f, 5, 3, 0f);
                    }
                }
            }
            if (model.HasGeometry) model.Finish(assets, root, "Low_Stones_And_Flower_Clumps", Vector3.zero, Identity);
        }
    }
}

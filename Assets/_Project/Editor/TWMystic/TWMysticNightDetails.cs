using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal static class TWMysticNightDetails
    {
        internal const string AssetRoot = TWMysticCedar.AssetRoot + "NightDetail/";
        internal const string BenchPlaque = "Engraving_Bench";
        internal const string WitnessPlaque = "Engraving_Witness";
        internal const int AccentCount = 5;
        internal const int FireflyGroupCount = 3;

        internal static void Build(TWMysticContext context, TWMysticAssets assets, MysticAtmosphere vibe, Material fireflyMaterial)
        {
            var props = context.root.GetComponentsInChildren<MysticProp>(true);
            Transform bench = props.Single(p => p.key == "Bench_4").transform;
            Transform witness = props.Single(p => p.key == "Witness_1").transform;
            Transform entrance = props.Single(p => p.key == "Entrance").transform;
            Transform cedar = context.root.Find("Cedar_Landmark");
            var seat = vibe.swing.Find("Swing_Seat_Collision").GetComponent<BoxCollider>();
            Vector3 seatCenter = seat.transform.TransformPoint(seat.center);

            vibe.engravingSurfaces = new[]
            {
                PlacePlaque(assets, bench, BenchPlaque, "Bench_Engraving",
                    new Vector3(0f, 1.155f, -0.27f), new Vector2(1.84f, 0.43f), 0.16f, 63f),
                PlacePlaque(assets, witness, WitnessPlaque, "Witness_Engraving",
                    new Vector3(0f, 0.70f, 0.59f), new Vector2(0.9f, 0.74f), 0.55f, 54f)
            };
            AddTabletCollision(vibe.engravingSurfaces[1].gameObject, witness);

            Transform lantern = PlaceLantern(assets, bench);
            Transform crystals = PlaceCrystals(assets, witness);
            Renderer gateLantern = PlaceGateLantern(assets, entrance);
            Color amber = new Color(1f, 0.72f, 0.37f);
            Color mint = new Color(0.46f, 0.93f, 0.79f);
            Color lilac = new Color(0.72f, 0.58f, 1f);
            Color moonBlue = new Color(0.44f, 0.74f, 1f);
            Color blueGreen = new Color(0.39f, 0.89f, 1f);
            vibe.nightAccents = new[]
            {
                Accent(lantern, "Night_Amber", new Vector3(-0.32f, 0.37f, 0.50f), amber, 5.4f, 2.7f,
                    lantern.Find("Lantern_Core").GetComponent<Renderer>(),
                    GroundGlow(context, assets, lantern.TransformPoint(new Vector3(-0.20f, 0f, 0.66f)), 2.9f, amber, 0.25f, "Night_Glow_Lantern")),
                Accent(crystals, "Night_Mint", new Vector3(0f, 0.50f, 0.30f), mint, 4.8f, 2.1f,
                    crystals.Find("Witness_Quartz").GetComponent<Renderer>(),
                    GroundGlow(context, assets, crystals.TransformPoint(new Vector3(0f, 0f, 0.42f)), 2.4f, mint, 0.20f, "Night_Glow_Witness")),
                Accent(cedar, "Night_Lilac", new Vector3(-0.8f, 2.65f, 1.45f), lilac, 7.4f, 1.45f, null,
                    GroundGlow(context, assets, cedar.TransformPoint(new Vector3(0.4f, 0f, 1.25f)), 3.65f, lilac, 0.20f, "Night_Glow_Roots")),
                Accent(cedar, "Night_Swing", cedar.InverseTransformPoint(seatCenter + Vector3.up * 0.72f),
                    blueGreen, 4.6f, 1.38f, null,
                    GroundGlow(context, assets, seatCenter, 2.55f, blueGreen, 0.19f, "Night_Glow_Swing")),
                Accent(entrance, "Night_Gate", new Vector3(0f, 2.43f, 0f), moonBlue, 5.65f, 2f,
                    gateLantern, null)
            };

            vibe.fireflies = new[]
            {
                Fireflies(vibe.transform, fireflyMaterial, "Fireflies_Bench",
                    bench.TransformPoint(new Vector3(0f, 0.89f, 0.91f)), new Vector3(4.3f, 1.45f, 2.8f),
                    new Color(1f, 0.84f, 0.46f), 12),
                Fireflies(vibe.transform, fireflyMaterial, "Fireflies_Witness",
                    witness.TransformPoint(new Vector3(0f, 1.1f, 0.85f)), new Vector3(3.3f, 1.55f, 3.0f),
                    new Color(0.59f, 1f, 0.81f), 10),
                Fireflies(vibe.transform, fireflyMaterial, "Fireflies_Roots",
                    Vector3.Lerp(cedar.position + Vector3.up * 0.9f, seatCenter, 0.62f),
                    new Vector3(5.0f, 1.35f, 3.15f), new Color(0.66f, 0.77f, 1f), 10)
            };

            vibe.ambience = Sound(vibe.transform, "OldForest_Breath", "Forest_Breath", vibe.transform.position, false);
            vibe.nightAmbienceLevel = 0.21f;
            vibe.nightEchoes = new[]
            {
                Sound(vibe.transform, "OldForest_StoneEcho", "Witness_Echo", witness.TransformPoint(Vector3.up), true)
            };
            vibe.swingCreak = Sound(vibe.transform, "OldForest_SwingCreak", "Swing_Creak", seatCenter + Vector3.up, true);
        }

        static void AddTabletCollision(GameObject slab, Transform witness)
        {
            if (slab.GetComponents<Collider>().Any(collider => collider.enabled && !collider.isTrigger &&
                (!(collider is MeshCollider mesh) || mesh.sharedMesh != null)))
                return;

            var collision = slab.GetComponent<MeshCollider>();
            if (collision == null)
                collision = Undo.AddComponent<MeshCollider>(slab);
            else
                Undo.RecordObject(collision, "Fit stone inscription collision");
            collision.sharedMesh = slab.GetComponent<MeshFilter>().sharedMesh;
            collision.convex = true;
            collision.isTrigger = false;
            collision.enabled = true;
            var baseCollider = witness.GetComponentInChildren<CapsuleCollider>(true);
            if (baseCollider != null)
                collision.sharedMaterial = baseCollider.sharedMaterial;
        }

        static MeshRenderer PlacePlaque(TWMysticAssets assets, Transform parent, string key, string texture,
            Vector3 localPosition, Vector2 size, float depth, float tilt)
        {
            var existing = parent.Find(key);
            if (existing != null)
                return existing.GetComponent<MeshRenderer>();

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Для гравировок Круга молчания нужен шейдер URP/Lit.");

            var material = new Material(shader) { name = key };
            material.SetTexture("_BaseMap", Load<Texture2D>(texture + ".png"));
            material.SetTexture("_EmissionMap", Load<Texture2D>(texture + "Glow.png"));
            material.SetColor("_EmissionColor", Color.white * 0.015f);
            material.SetFloat("_Smoothness", 0.14f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, assets.folder + "/" + key + ".mat");

            var mesh = PlaqueMesh(key, size, depth);
            var plaque = assets.Object(parent, key, assets.Save(mesh, key), new[] { material }, true);
            plaque.transform.localPosition = localPosition;
            // The engraved face tilts up toward the game camera and the path.
            plaque.transform.localRotation = Quaternion.Euler(-tilt, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            Undo.RegisterCreatedObjectUndo(plaque, "Place carved forest inscription");
            return plaque.GetComponent<MeshRenderer>();
        }

        static Mesh PlaqueMesh(string name, Vector2 size, float depth)
        {
            Vector2[] outline =
            {
                new Vector2(-0.50f, -0.27f), new Vector2(-0.44f, -0.49f),
                new Vector2(0.42f, -0.50f), new Vector2(0.50f, -0.31f),
                new Vector2(0.49f, 0.30f), new Vector2(0.42f, 0.49f),
                new Vector2(-0.42f, 0.50f), new Vector2(-0.49f, 0.31f)
            };

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                Vector3 front = Corner(i, 0.94f, 0f);
                Vector3 frontNext = Corner(next, 0.94f, 0f);
                Vector3 bevel = Corner(i, 1f, 0.022f);
                Vector3 bevelNext = Corner(next, 1f, 0.022f);
                Vector3 back = Corner(i, 0.97f, depth);
                Vector3 backNext = Corner(next, 0.97f, depth);
                Triangle(Vector3.zero, frontNext, front);
                Triangle(front, frontNext, bevelNext);
                Triangle(front, bevelNext, bevel);
                Triangle(bevel, bevelNext, backNext);
                Triangle(bevel, backNext, back);
                Triangle(new Vector3(0f, 0f, depth), back, backNext, true);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            Vector3 Corner(int i, float scale, float z)
            {
                Vector2 p = Vector2.Scale(outline[i] * scale, size);
                return new Vector3(p.x, p.y, z);
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c, bool back = false)
            {
                foreach (Vector3 point in new[] { a, b, c })
                {
                    indices.Add(vertices.Count);
                    vertices.Add(point);
                    uv.Add(back ? new Vector2(0.018f, 0.018f)
                        : new Vector2(0.5f + point.x / size.x, 0.5f + point.y / size.y));
                }
            }
        }

        static Transform PlaceLantern(TWMysticAssets assets, Transform bench)
        {
            const string key = "Bench_Lantern";
            var existing = bench.Find(key);
            if (existing != null)
                return existing;

            var anchor = Child(bench, key, new Vector3(1.17f, 0.90f, 0.12f));
            var frame = new TWMysticModel();
            TWMysticModels.Box(frame, "Metal", new Vector3(0f, -0.15f, 0f), new Vector3(0.27f, 0.045f, 0.23f));
            TWMysticModels.Box(frame, "Copper", new Vector3(0f, 0.13f, 0f), new Vector3(0.28f, 0.05f, 0.25f));
            TWMysticModels.Box(frame, "DarkWood", new Vector3(0f, 0.176f, 0f), new Vector3(0.19f, 0.045f, 0.16f));
            TWMysticModels.Rod(frame, "Metal", new Vector3(0f, 0.255f, -0.24f), new Vector3(0f, 0.255f, 0f), 0.025f);
            TWMysticModels.Rod(frame, "Copper", new Vector3(0f, 0.256f, 0f), new Vector3(0f, 0.19f, 0f), 0.022f);
            foreach (int x in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
            {
                var end = new Vector3(x * 0.115f, 0f, z * 0.096f);
                TWMysticModels.Rod(frame, "Metal", end - Vector3.up * 0.15f, end + Vector3.up * 0.14f, 0.014f);
            }
            frame.Finish(assets, anchor, "Lantern_Frame", anchor.position, anchor.rotation);

            var glass = new TWMysticModel();
            TWMysticModels.Box(glass, "Glass", new Vector3(0f, -0.005f, 0f), new Vector3(0.172f, 0.22f, 0.152f));
            glass.Finish(assets, anchor, "Lantern_Core", anchor.position, anchor.rotation);
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Place bench lantern");
            return anchor;
        }

        static Transform PlaceCrystals(TWMysticAssets assets, Transform witness)
        {
            const string key = "Witness_Crystals";
            var existing = witness.Find(key);
            if (existing != null)
                return existing;

            var anchor = Child(witness, key, new Vector3(-0.54f, 0.12f, 0.43f));
            var baseRock = new TWMysticModel();
            baseRock.Part("Stone").Puff(new Vector3(0f, -0.02f, 0f), new Vector3(0.23f, 0.13f, 0.17f), Color.white, 32, 7, 3, 0.12f);
            baseRock.Finish(assets, anchor, "Witness_Quartz_Bed", anchor.position, anchor.rotation);

            var quartz = new TWMysticModel();
            var glass = quartz.Part("Glass");
            Shard(new Vector3(-0.10f, 0.02f, 0f), new Vector3(-0.10f, 0.31f, -0.025f), 0.071f);
            Shard(new Vector3(0.01f, 0.015f, 0.07f), new Vector3(0.045f, 0.23f, 0.12f), 0.053f);
            Shard(new Vector3(0.09f, 0.006f, -0.045f), new Vector3(0.16f, 0.18f, -0.06f), 0.048f);
            quartz.Finish(assets, anchor, "Witness_Quartz", anchor.position, anchor.rotation);
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Place stone crystals");
            return anchor;

            void Shard(Vector3 foot, Vector3 tip, float radius)
            {
                Vector3 shoulder = Vector3.Lerp(foot, tip, 0.72f);
                glass.Cyl(foot, shoulder, radius, radius * 0.82f, new Color(0.42f, 0.82f, 0.79f), 5);
                glass.Cyl(shoulder, tip, radius * 0.82f, 0.003f, new Color(0.58f, 0.95f, 0.86f), 5);
            }
        }

        static Renderer PlaceGateLantern(TWMysticAssets assets, Transform entrance)
        {
            const string key = "Gate_Lantern_Core";
            var existing = entrance.Find(key);
            if (existing != null)
                return existing.GetComponent<Renderer>();

            var glass = new TWMysticModel();
            TWMysticModels.Box(glass, "Glass", new Vector3(0f, 2.64f, 0f), new Vector3(0.205f, 0.15f, 0.17f));
            var lantern = glass.Finish(assets, entrance, key, entrance.position, entrance.rotation);
            Undo.RegisterCreatedObjectUndo(lantern, "Place the entrance lantern glass");
            return lantern.GetComponent<Renderer>();
        }

        static MysticAtmosphere.NightAccent Accent(Transform parent, string key, Vector3 position, Color color,
            float range, float intensity, Renderer glow, Renderer groundGlow)
        {
            var anchor = parent.Find(key);
            bool create = anchor == null;
            if (create)
                anchor = Child(parent, key, position);
            var light = anchor.GetComponent<Light>();
            if (light == null)
                light = create ? anchor.gameObject.AddComponent<Light>() : Undo.AddComponent<Light>(anchor.gameObject);
            if (!create)
                Undo.RecordObject(light, "Tune night accent light");
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            light.enabled = false;
            if (create)
                Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Place night accent light");
            return new MysticAtmosphere.NightAccent
            {
                light = light,
                peakIntensity = intensity,
                glow = glow,
                glowColor = color * 0.62f,
                groundGlow = groundGlow
            };
        }

        static Renderer GroundGlow(TWMysticContext context, TWMysticAssets assets, Vector3 center,
            float radius, Color tint, float opacity, string key)
        {
            var ground = context.root.Find("00_Conforming_Ground").GetComponent<MeshFilter>();
            Mesh source = ground.sharedMesh;
            Vector3 localCenter = ground.transform.InverseTransformPoint(center);
            Vector3[] points = source.vertices;
            int[] triangles = source.triangles;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 lift = Vector3.up * 0.023f;
                Vector3 a = points[triangles[i]] + lift;
                Vector3 b = points[triangles[i + 1]] + lift;
                Vector3 c = points[triangles[i + 2]] + lift;
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f)
                    continue;
                float alphaA = Falloff(a);
                float alphaB = Falloff(b);
                float alphaC = Falloff(c);
                if (alphaA + alphaB + alphaC < 0.001f)
                    continue;
                Add(a, alphaA);
                Add(b, alphaB);
                Add(c, alphaC);
            }
            if (vertices.Count == 0)
                return null;

            var mesh = new Mesh { name = key };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            var glow = assets.Object(ground.transform, key, assets.Save(mesh, key), new[] { assets.veil }, false);
            var renderer = glow.GetComponent<MeshRenderer>();
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;

            float Falloff(Vector3 point)
            {
                Vector3 delta = point - localCenter;
                float fade = Mathf.Clamp01(1f - (delta.x * delta.x + delta.z * delta.z) / (radius * radius));
                return opacity * fade * fade;
            }

            void Add(Vector3 point, float alpha)
            {
                indices.Add(vertices.Count);
                vertices.Add(point);
                colors.Add(new Color(tint.r, tint.g, tint.b, alpha));
            }
        }

        internal static ParticleSystem Fireflies(Transform parent, Material material, string key,
            Vector3 worldCenter, Vector3 spread, Color tint, int maxParticles)
        {
            var existing = parent.Find(key);
            bool create = existing == null;
            Transform swarm = create ? Child(parent, key, Vector3.zero) : existing;
            ParticleSystem ps;
            if (create)
            {
                ps = swarm.gameObject.AddComponent<ParticleSystem>();
            }
            else
            {
                ps = swarm.GetComponent<ParticleSystem>();
                Undo.RecordObject(swarm, "Adjust forest fireflies position");
                Undo.RecordObject(ps, "Adjust forest fireflies");
                Undo.RecordObject(ps.GetComponent<ParticleSystemRenderer>(), "Adjust forest fireflies material");
            }
            swarm.position = worldCenter;

            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            main.startColor = tint;
            main.maxParticles = maxParticles;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = spread;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = false;
            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.19f;
            noise.frequency = 0.68f;
            noise.scrollSpeed = 0.18f;
            noise.damping = true;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.86f, 0.26f),
                    new GradientAlphaKey(0.37f, 0.49f), new GradientAlphaKey(0.8f, 0.73f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (create)
                Undo.RegisterCreatedObjectUndo(swarm.gameObject, "Place forest fireflies");
            return ps;
        }

        static AudioSource Sound(Transform parent, string name, string clip, Vector3 worldPosition, bool spatial)
        {
            var anchor = Child(parent, name, Vector3.zero);
            anchor.position = worldPosition;
            var source = anchor.gameObject.AddComponent<AudioSource>();
            source.clip = Load<AudioClip>(clip + ".wav");
            source.playOnAwake = !spatial;
            source.loop = !spatial;
            source.volume = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            // The listener is on the elevated game camera; the controller fades by player distance.
            source.minDistance = 8f;
            source.maxDistance = 35f;
            return source;
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static T Load<T>(string filename) where T : UnityEngine.Object
        {
            string path = AssetRoot + filename;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("Не импортирован ассет Круга молчания: " + path);
            return asset;
        }
    }
}

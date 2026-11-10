using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    // Editor-only scenery. Runtime light selection, flicker and day/night are
    // owned by the existing FireSource / FireLightPool, never a second manager.
    internal static class TWRiverDetails
    {
        public const string PoolShader = "Project/IslandV2/TW_LanternPool";
        static readonly Color Wood = new Color(0.39f, 0.25f, 0.16f);
        static readonly Color Copper = new Color(0.64f, 0.40f, 0.21f);
        static readonly Color Iron = new Color(0.23f, 0.29f, 0.28f);

        public static Material Lit(string folder, string name, Color colour, bool emission = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) throw new InvalidOperationException("Не импортирован URP Simple Lit.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetColor("_SpecColor", new Color(0.06f, 0.06f, 0.06f));
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(1.3f, 0.77f, 0.31f));
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
            return material;
        }

        public static Material[] RockMaterials(string folder)
        {
            return new[]
            {
                Lit(folder, "Stone_Cool", new Color(0.56f, 0.60f, 0.64f)),
                Lit(folder, "Stone_Warm", new Color(0.64f, 0.61f, 0.55f)),
                Lit(folder, "Stone_Moss", new Color(0.46f, 0.61f, 0.32f)),
                Lit(folder, "Stone_Wet", new Color(0.30f, 0.40f, 0.44f))
            };
        }

        public static int Build(Transform parent, TWRiverCtx x, TerrainCollider[] terrain, Material ground, string folder)
        {
            Physics.SyncTransforms();
            var root = new GameObject("River_Lanterns").transform;
            root.SetParent(parent, false);
            var frame = new TWFaceted(); var glass = new TWFaceted();
            Lantern(frame, glass);
            Mesh frameMesh = Save(frame.ToMesh("Lantern_Wood_And_Copper"), folder, "Lantern_Frame", true);
            Mesh glassMesh = Save(glass.ToMesh("Lantern_Glass"), folder, "Lantern_Glass", false);
            Material glassMat = Lit(folder, "Lantern_Amber_Glass", new Color(1f, 0.80f, 0.42f), true);
            var poolMat = new Material(Shader.Find(PoolShader)) { name = "Lantern_Soft_Ground_Light" };
            poolMat.EnableKeyword("_EMISSION");
            poolMat.SetColor("_EmissionColor", new Color(1.3f, 0.77f, 0.31f));
            AssetDatabase.CreateAsset(poolMat, folder + "/Lantern_Ground_Light.mat");
            var sites = new List<Vector3>();
            // A pair introduces the spring; the remaining lamps mark bends,
            // leaving the beach, bridge approaches and waterfall unobstructed.
            AddSite(x, terrain, sites, 13.5f, -1);
            AddSite(x, terrain, sites, 16f, 1);
            int side = -x.rockSide;
            for (float s = 34f; s < x.ax.Length - 12f && sites.Count < 8; s += 24f)
            {
                AddSite(x, terrain, sites, s, side); side = -side;
            }
            for (int i = 0; i < sites.Count; i++)
            {
                var lamp = new GameObject("Bank_Lantern_" + (i + 1)).transform;
                lamp.SetParent(root, false); lamp.position = sites[i];
                float s = NearestAlong(x, sites[i]);
                Vector2 tangent = x.ax.Tangent(s);
                lamp.rotation = Quaternion.LookRotation(new Vector3(tangent.x, 0f, tangent.y));
                Renderer frameRenderer = Object(lamp, "Timber_Frame", frameMesh, ground, true);
                Renderer bulb = Object(lamp, "Amber_Panes", glassMesh, glassMat, false);
                Mesh pool = GroundPool(x, terrain, sites[i], lamp);
                Renderer glow = null;
                if (pool != null)
                {
                    Save(pool, folder, "Lantern_Pool_" + i, false);
                    glow = Object(lamp, "Soft_Light_On_Bank", pool, poolMat, false);
                    glow.receiveShadows = false;
                }
                var fire = lamp.gameObject.AddComponent<FireSource>();
                fire.preset = FlickerPreset.Lantern;
                fire.color = new Color(1f, 0.74f, 0.39f);
                fire.intensity = 2.0f; fire.range = 6.5f;
                fire.lightOffset = new Vector3(0f, 1.76f, 0f);
                fire.emissiveStrength = 2.0f;
                fire.emissive = glow != null ? new[] { bulb, glow } : new[] { bulb };
                fire.glow = true; fire.glowSize = 0.72f;
                fire.glowPoints = new[] { fire.lightOffset };
                frameRenderer.receiveShadows = true;
            }
            if (sites.Count > 0) EasterEggs(parent, x, terrain, sites, ground, folder);
            else Debug.LogWarning("RIVER ART: свободные площадки для фонарей не найдены; проверь берега.");
            return sites.Count;
        }

        static void AddSite(TWRiverCtx x, TerrainCollider[] terrain, List<Vector3> sites, float along, int side)
        {
            foreach (float shift in new[] { 0f, -2.5f, 2.5f, -5f, 5f })
                foreach (float inset in new[] { 3.2f, 4.5f, 5.7f })
                {
                    float s = Mathf.Clamp(along + shift, 11.5f, x.ax.Length - 10f);
                    Vector3 p = x.P(s, side * (x.half + inset), 0f);
                    if (!Site(x, terrain, ref p)) continue;
                    bool near = false;
                    foreach (Vector3 other in sites) if ((p - other).sqrMagnitude < 36f) near = true;
                    if (near) continue;
                    sites.Add(p); return;
                }
        }

        static bool Site(TWRiverCtx x, TerrainCollider[] terrain, ref Vector3 p)
        {
            if (x.f.EdgeDistance(p.x, p.z) < 3f || x.Protected(p, 1.7f) ||
                x.f.PathWeight(p.x, p.z) > 0.18f || !Ground(terrain, p, out RaycastHit hit) ||
                hit.normal.y < 0.83f || hit.point.y < x.Yw(NearestAlong(x, p)) + 0.35f) return false;
            p.y = hit.point.y - 0.035f;
            foreach (var collider in Physics.OverlapCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 2.2f,
                         0.32f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (collider is TerrainCollider || collider.GetComponentInParent<RiverArtState>() != null) continue;
                return false;
            }
            return true;
        }

        static bool Ground(TerrainCollider[] terrain, Vector3 p, out RaycastHit best)
        {
            best = default;
            bool found = false;
            foreach (var collider in terrain)
            {
                if (collider == null || !collider.enabled) continue;
                var ray = new Ray(new Vector3(p.x, collider.bounds.max.y + 10f, p.z), Vector3.down);
                if (collider.Raycast(ray, out RaycastHit hit, collider.bounds.size.y + 20f) &&
                    (!found || hit.point.y > best.point.y)) { best = hit; found = true; }
            }
            return found;
        }

        static float NearestAlong(TWRiverCtx x, Vector3 point)
        {
            float best = float.PositiveInfinity, result = 0f;
            for (float s = 0f; s <= x.ax.Length; s += 0.7f)
            {
                Vector2 p = x.ax.At(s);
                float d = (p - new Vector2(point.x, point.z)).sqrMagnitude;
                if (d < best) { best = d; result = s; }
            }
            return result;
        }

        static Mesh GroundPool(TWRiverCtx x, TerrainCollider[] terrain, Vector3 site, Transform lamp)
        {
            const int segments = 24, rings = 5;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var valid = new List<bool>();
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i < segments; i++)
                {
                    float radius = 0.13f + 2.3f * ring / (rings - 1);
                    float theta = i * Mathf.PI * 2f / segments;
                    Vector3 p = site + new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
                    bool grounded = Ground(terrain, p, out RaycastHit hit);
                    bool supported = x.f.EdgeDistance(p.x, p.z) > 1f && !x.Protected(p, 0.2f) && grounded;
                    if (grounded) p.y = hit.point.y + 0.04f;
                    valid.Add(supported && Mathf.Abs(p.y - site.y) < 1.5f);
                    v.Add(lamp.InverseTransformPoint(p));
                    float fade = 1f - (float)ring / (rings - 1);
                    c.Add(new Color(1f, 1f, 1f, fade * fade));
                }
            for (int ring = 0; ring < rings - 1; ring++)
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    int a = ring * segments + i, b = ring * segments + next, d = a + segments, e = b + segments;
                    if (!valid[a] || !valid[b] || !valid[d] || !valid[e]) continue;
                    t.Add(a); t.Add(d); t.Add(b); t.Add(b); t.Add(d); t.Add(e);
                }
            if (t.Count == 0) return null;
            var mesh = new Mesh { name = "Draped_Lantern_Pool" };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            return mesh;
        }

        static void Lantern(TWFaceted frame, TWFaceted glass)
        {
            Box(frame, new Vector3(0f, 0.10f, 0f), new Vector3(0.52f, 0.20f, 0.50f), new Color(0.49f, 0.53f, 0.49f));
            Box(frame, new Vector3(0f, 0.70f, 0f), new Vector3(0.19f, 1.35f, 0.19f), Wood);
            Box(frame, new Vector3(0f, 1.43f, 0f), new Vector3(0.56f, 0.12f, 0.52f), Copper);
            Box(glass, new Vector3(0f, 1.75f, 0f), new Vector3(0.33f, 0.52f, 0.33f), Color.white);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(frame, new Vector3(sx * 0.21f, 1.75f, sz * 0.21f), new Vector3(0.045f, 0.63f, 0.045f), Iron);
            Box(frame, new Vector3(0f, 2.06f, 0f), new Vector3(0.56f, 0.10f, 0.52f), Iron);
            Taper(frame, new Vector3(0f, 2.11f, 0f), 0.35f, 0.10f, 0.25f, Copper, 4);
            Box(frame, new Vector3(0f, 2.40f, 0f), new Vector3(0.09f, 0.11f, 0.09f), Iron);
        }

        static void EasterEggs(Transform parent, TWRiverCtx x, TerrainCollider[] terrain, List<Vector3> sites, Material ground, string folder)
        {
            var root = new GameObject("River_Quiet_Secrets").transform;
            root.SetParent(parent, false);
            var b = new TWFaceted();
            // A tiny rubber-duck debugger on a weathered stump. No quest IDs,
            // floating text, fonts, triggers or extra update loop.
            Vector3 p = sites[0] + new Vector3(0.85f, 0f, 0.45f);
            if (Site(x, terrain, ref p))
            {
                Taper(b, p, 0.31f, 0.25f, 0.39f, Wood, 7);
                var duck = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Duckling.asset");
                if (duck != null)
                {
                    var figurine = Object(root, "Rubber_Duck_Debugger", duck, ground, true).transform;
                    figurine.position = p + Vector3.up * 0.445f;
                    figurine.localScale = Vector3.one * 0.6f;
                }
            }
            // A sealed bottle and a small wing-marked note, tucked beside a
            // later lamp. The wing is modelled so no missing glyphs appear.
            p = sites[sites.Count - 1] + new Vector3(-0.65f, 0f, 0.4f);
            if (Site(x, terrain, ref p))
            {
                Taper(b, p, 0.12f, 0.13f, 0.29f, new Color(0.32f, 0.64f, 0.60f), 7);
                Taper(b, p + Vector3.up * 0.29f, 0.13f, 0.06f, 0.09f, new Color(0.42f, 0.73f, 0.66f), 7);
                Taper(b, p + Vector3.up * 0.38f, 0.055f, 0.055f, 0.09f, Copper, 7);
                Vector3 note = p + new Vector3(0.24f, 0.025f, 0.02f);
                Box(b, note, new Vector3(0.28f, 0.035f, 0.23f), new Color(0.95f, 0.84f, 0.62f));
                for (int wing = -1; wing <= 1; wing += 2)
                    for (int feather = 0; feather < 3; feather++)
                        Box(b, note + new Vector3(wing * (0.035f + feather * 0.025f), 0.024f, feather * 0.025f),
                            new Vector3(0.025f, 0.009f, 0.09f - feather * 0.018f), Iron);
            }
            if (b.Tris > 0) Object(root, "Duck_Debugger_And_Wing_Message", Save(b.ToMesh("Quiet_River_Secrets"), folder, "River_Secrets", true), ground, true);
        }

        static Renderer Object(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        static Mesh Save(Mesh mesh, string folder, string name, bool linear)
        {
            TWRiverValidation.CheckMesh(mesh);
            if (linear)
            {
                var colours = mesh.colors;
                for (int i = 0; i < colours.Length; i++) colours[i] = colours[i].linear;
                mesh.colors = colours;
            }
            AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset");
            return mesh;
        }

        static void Box(TWFaceted b, Vector3 p, Vector3 size, Color c)
        {
            Vector3 h = size * 0.5f;
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = p + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            int[] q = { 0, 1, 3, 2, 4, 6, 7, 5, 0, 4, 5, 1, 2, 3, 7, 6, 0, 2, 6, 4, 1, 5, 7, 3 };
            for (int i = 0; i < q.Length; i += 4)
            {
                b.TriAway(v[q[i]], v[q[i + 1]], v[q[i + 2]], c, p);
                b.TriAway(v[q[i]], v[q[i + 2]], v[q[i + 3]], c, p);
            }
        }

        static void Taper(TWFaceted b, Vector3 p, float radius, float topRadius, float height, Color c, int sides)
        {
            Vector3 inside = p + Vector3.up * height * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / sides, d = (i + 1.5f) * Mathf.PI * 2f / sides;
                Vector3 u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), v = new Vector3(Mathf.Cos(d), 0f, Mathf.Sin(d));
                Vector3 bottomA = p + u * radius, bottomB = p + v * radius;
                Vector3 topA = p + Vector3.up * height + u * topRadius, topB = p + Vector3.up * height + v * topRadius;
                b.TriAway(bottomA, bottomB, topA, c, inside); b.TriAway(topA, bottomB, topB, c, inside);
                b.TriAway(p, bottomB, bottomA, c, inside);
                b.TriAway(p + Vector3.up * height, topA, topB, c, inside);
            }
        }

    }
}

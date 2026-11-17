using System;
using UnityEngine;

public partial class PondBuilder
{
    void ValidatePlacement()
    {
        var yaw = Quaternion.Euler(0f, S.yaw, 0f);
        if (!islandGround.HasGround(S.position))
            throw new InvalidOperationException("Центр пруда должен быть над землёй острова.");

        for (int i = 0; i < N; i += 4)
        {
            var p = Dir(i * TwoPi / N) * (shoreR[i] + 0.75f);
            var world = S.position + yaw * Vector3.Scale(p, S.scale);
            if (!islandGround.HasGround(world))
                throw new InvalidOperationException("Оставь немного земли вокруг берега пруда.");
        }
    }

    // The original mesh and heightfield continue under the basin. Lower them
    // only where needed so neither becomes a visible shelf or a false floor.
    float CarvedGroundY(Vector3 world, float padding)
    {
        Vector3 point = root.InverseTransformPoint(world);
        float pad = padding / Mathf.Min(S.scale.x, S.scale.z);
        float radius = Shore(Mathf.Atan2(point.z, point.x));
        float r = new Vector2(point.x, point.z).magnitude;
        float outside = r - radius - pad;
        if (outside >= 1.05f) return world.y;

        float s = Mathf.Clamp01((r - pad) / radius);
        float y = Mathf.Min(point.y, BedY(s) - 0.23f);
        float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.05f, outside));
        point.y = Mathf.Lerp(y, point.y, edge);
        return root.TransformPoint(point).y;
    }

    float BankHeight(Vector3 local)
    {
        float phi = Mathf.Atan2(local.z, local.x);
        float radius = Shore(phi);
        float distance = new Vector2(local.x, local.z).magnitude;
        return distance < radius ? BedY(distance / radius) : OutY(distance - radius, phi);
    }

    void BuildShoreSteps()
    {
        float phi = S.shoreExitAngle * Mathf.Deg2Rad;
        var steps = NewChild(root, "Stone steps");
        var outward = Dir(phi);
        var rotation = Quaternion.LookRotation(outward, Vector3.up);
        float shore = Shore(phi);
        steps.localPosition = outward * shore;

        const float rampWidth = 1.28f;
        const float reachIntoWater = 2.3f;
        const float dryEnd = 1.3f;
        Vector3 start = outward * (shore - reachIntoWater);
        Vector3 end = outward * (shore + dryEnd);
        float RampY(Vector3 at)
        {
            float d = Vector3.Dot(at, outward) - shore;
            float t = Mathf.InverseLerp(-reachIntoWater, dryEnd, d);
            float entryY = W - 1.05f / S.scale.y;
            float crest = Mathf.Lerp(entryY, Ys + 0.02f, Mathf.InverseLerp(-reachIntoWater, -0.22f, d));
            crest -= 0.18f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, dryEnd, d));
            return Mathf.Max(crest, BankHeight(at) + 0.055f * Mathf.Sin(Mathf.PI * t));
        }
        BuildWalkStrip(steps, "Shore ramp collider", "Pond_ShoreRamp", start, end, rampWidth, RampY);

        const int stepsCount = 6;
        for (int i = 0; i < stepsCount; i++)
        {
            float t = (i + 0.5f) / stepsCount;
            Vector3 center = Vector3.Lerp(start, end, t);
            float bankY = BankHeight(center);
            center.y = RampY(center);
            var stone = new PondMB();
            float width = 1.36f + Rf(-0.08f, 0.08f);
            float foundation = Mathf.Max(0.24f, center.y - bankY + 0.19f);
            StepStone(stone, width, 0.51f + Rf(-0.025f, 0.035f), foundation,
                Vc(Mul(S.rock, Rf(0.84f, 1.02f))));
            string name = "Stone step " + (i + 1);
            var go = MeshGO(steps, name, SaveMesh(stone.ToMesh(name), "Pond_Step" + i), matDecor, true);
            go.transform.localPosition = center - steps.localPosition;
            go.transform.localRotation = rotation * Quaternion.Euler(0f, Rf(-3f, 3f), 0f);
        }
    }

    static void StepStone(PondMB mesh, float width, float length, float foundation, Color color)
    {
        var ring = new[]
        {
            new Vector2(-0.50f, -0.26f), new Vector2(-0.38f, -0.50f), new Vector2(0.35f, -0.48f),
            new Vector2(0.50f, -0.27f), new Vector2(0.48f, 0.30f), new Vector2(0.33f, 0.50f),
            new Vector2(-0.39f, 0.46f), new Vector2(-0.50f, 0.24f)
        };
        for (int i = 0; i < ring.Length; i++)
        {
            Vector2 a = ring[i], b = ring[(i + 1) % ring.Length];
            Vector3 x = new Vector3(a.x * width, 0f, a.y * length);
            Vector3 y = new Vector3(b.x * width, 0f, b.y * length);
            Vector3 topX = x * 0.88f + Vector3.up * 0.035f;
            Vector3 topY = y * 0.88f + Vector3.up * 0.035f;
            Vector3 facing = (x + y).normalized;
            mesh.Tri(Vector3.up * 0.035f, topX, topY, color, 0f, Vector3.up);
            mesh.Quad(topX, x, y, topY, Mul(color, 0.93f), 0f, facing);
            mesh.Quad(x, x - Vector3.up * foundation, y - Vector3.up * foundation, y, Mul(color, 0.78f), 0f, facing);
        }
    }

    void BuildPier()
    {
        pier = NewChild(root, "Pier");
        pier.localPosition = dockStart;
        decor = new PondMB();
        BuildDock();
        var go = MeshGO(pier, "Deck and rails", SaveMesh(decor.ToMesh("Pond_Pier"), "Pond_Pier"), matDecor, true);
        go.transform.localPosition = -dockStart;

        const float approachLength = 2.95f;
        Vector3 start = dockStart + outDir * approachLength;
        Vector3 end = dockStart + dockIn * 0.16f;
        float groundY = BankHeight(start) + 0.025f;
        Vector3 dryEnd = root.TransformPoint(start);
        dryEnd.y = islandGround.HeightAt(dryEnd);
        groundY = Mathf.Max(groundY, root.InverseTransformPoint(dryEnd).y);
        float deckY = DY + 0.038f;
        float SurfaceY(Vector3 at) => Mathf.Lerp(groundY, deckY,
            Vector3.Dot(at - start, dockIn) / (approachLength + 0.16f));
        BuildWalkStrip(pier, "Pier ramp collider", "Pond_PierRamp", start, end, dockW, SurfaceY);

        var boards = new PondMB();
        var rotation = Quaternion.LookRotation(dockIn, Vector3.up);
        for (int i = 0; i < 11; i++)
        {
            var center = Vector3.Lerp(start, end, (i + 0.5f) / 11f);
            center.y = SurfaceY(center) - 0.032f;
            boards.Box(center - pier.localPosition, new Vector3(dockW + 0.06f, 0.09f, 0.30f),
                rotation, Vc(Color.Lerp(S.woodDark, S.woodLight, Rf(0.4f, 0.86f))), 0f);
        }
        MeshGO(pier, "Approach boards", SaveMesh(boards.ToMesh("Pond_PierApproach"), "Pond_PierApproach"), matDecor, true);

        var rails = NewChild(pier, "Rail colliders");
        rails.localPosition = -pier.localPosition;
        for (int side = -1; side <= 1; side += 2)
        {
            float walkway = dockL - 1.5f;
            AddPierCollider(rails, "Side railing", DockPt(walkway * 0.5f + 0.13f, side * (dockW * 0.5f + 0.04f), DY + 0.39f),
                new Vector3(0.13f, 0.66f, walkway), rotation);
            AddPierCollider(rails, "Landing railing", DockPt(dockL - 0.74f, side * (HalfW(dockL) + 0.02f), DY + 0.39f),
                new Vector3(0.13f, 0.66f, 1.48f), rotation);
        }
        AddPierCollider(rails, "End railing", DockPt(dockL - 0.10f, 0f, DY + 0.38f),
            new Vector3(dockW + 0.7f, 0.64f, 0.12f), rotation);
    }

    void BuildWalkStrip(Transform parent, string childName, string assetName, Vector3 start, Vector3 end,
        float width, Func<Vector3, float> surface)
    {
        const int sections = 12;
        Vector3 delta = end - start;
        Vector3 side = Vector3.Cross(Vector3.up, delta).normalized * (width * 0.5f);
        var strip = new PondMB();
        for (int i = 0; i <= sections; i++)
        {
            Vector3 center = Vector3.Lerp(start, end, (float)i / sections);
            Vector3 left = center - side;
            Vector3 right = center + side;
            left.y = surface(left);
            right.y = surface(right);
            int a = strip.V(left - parent.localPosition, Color.white, 0f);
            strip.V(right - parent.localPosition, Color.white, 0f);
            if (i == 0) continue;
            strip.I(a - 2, a, a - 1);
            strip.I(a - 1, a, a + 1);
        }

        var walk = NewChild(parent, childName);
        walk.gameObject.AddComponent<MeshCollider>().sharedMesh = SaveMesh(strip.ToMesh(assetName), assetName);
    }

    static void AddPierCollider(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation)
    {
        var part = NewChild(parent, name);
        part.localPosition = center;
        part.localRotation = rotation;
        part.gameObject.AddComponent<BoxCollider>().size = size;
    }
}

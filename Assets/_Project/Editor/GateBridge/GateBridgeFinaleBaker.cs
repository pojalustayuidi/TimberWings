#if UNITY_EDITOR
// Генерация финального перехода исключительно в редакторе.
// Результат - обычные объекты сцены с persistent mesh/material assets.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class GateBridgeFinaleBaker
{
    const float RampStart = -3.35f;
    const float RampEnd = 0.28f;
    const float SpringY = 1.38f;
    const float OpeningX = 1.56f;
    const float OpeningY = 1.65f;
    static readonly Color Stone = new Color(.30f, .35f, .39f);
    static readonly Color Slate = new Color(.20f, .25f, .29f);
    static readonly Color Brass = new Color(.67f, .47f, .26f);
    static readonly Color Timber = new Color(.43f, .29f, .18f);
    static readonly Color Amber = new Color(1f, .71f, .32f, .12f);
    static readonly Color Mint = new Color(.22f, .87f, .73f, 0f);
    static readonly Color Moss = new Color(.35f, .49f, .31f);

    readonly List<Light> lanterns = new List<Light>();
    Material stoneMaterial;
    GameObject barrier;
    GameObject finishedBridge;
    GateBridge bridge;
    Transform entry;
    Transform finaleRoot;
    float halfWidth, span;

    FinalIslandPortal portal;

    internal static GateBridgeFinale Bake(GateBridge gate, Transform generatedRoot)
    {
        var builder = new GateBridgeFinaleBaker();
        return builder.Build(gate, generatedRoot);
    }

    GateBridgeFinale Build(GateBridge gate, Transform generatedRoot)
    {
        bridge = gate;
        finaleRoot = Child(generatedRoot, "FinalIsland", Vector3.zero);
        stoneMaterial = gate.plankMaterial;
        if (stoneMaterial == null)
        {
            var deckRenderer = gate.stages[2].GetComponentInChildren<MeshRenderer>(true);
            if (deckRenderer != null) stoneMaterial = deckRenderer.sharedMaterial;
        }
        halfWidth = gate.deckWidth * .5f;
        span = gate.bridgeLength + GateBridge.DeckStartInset + GateBridge.DeckEndInset;
        entry = Child(finaleRoot, "BridgeDetails", Vector3.zero);
        entry.SetPositionAndRotation(gate.DeckEntrance, Quaternion.LookRotation(gate.BridgeForward));
        BuildCrossing();
        BuildIsland();
        var component = finaleRoot.gameObject.AddComponent<GateBridgeFinale>();
        component.Configure(barrier, finishedBridge, lanterns.ToArray(), portal);
        return component;
    }

    static Transform Child(Transform parent, string name, Vector3 at)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = at;
        return child;
    }

    static void BoxColliderAt(Transform parent, Vector3 center, Vector3 size)
    {
        var box = parent.gameObject.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    MeshRenderer Draw(Transform parent, string name, Mesh mesh, Material material, bool shadows = true)
    {
        var obj = Child(parent, name, Vector3.zero).gameObject;
        obj.AddComponent<MeshFilter>().sharedMesh = GateBridgeAssetVault.Mesh(mesh);
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        if (!shadows) renderer.shadowCastingMode = ShadowCastingMode.Off;
        return renderer;
    }

    Vector3 Deck(float distance)
    {
        float t = Mathf.Clamp01(distance / span);
        float y = -bridge.stubDrop * t - bridge.sag * 4f * t * (1f - t);
        return new Vector3(0f, y, distance);
    }

    void BuildCrossing()
    {
        float shoreY = bridge.ShoreHeight(entry.TransformPoint(Vector3.forward * RampStart)) - entry.position.y;
        Vector3 Ramp(float s) => new Vector3(0f,
            s < 0f ? Mathf.Lerp(shoreY, 0f, Mathf.InverseLerp(RampStart, 0f, s)) : Deck(s).y, s);

        var lockRoot = Child(entry, "ConstructionBarrier", Vector3.forward * -1.65f);
        float lockY = Ramp(lockRoot.localPosition.z).y;
        BoxColliderAt(lockRoot, new Vector3(0f, lockY + .9f, 0f), new Vector3(halfWidth * 2f + 1.65f, 2.5f, .38f));
        var timber = new GateBridgeMesh();
        float postX = halfWidth + .48f;
        for (int side = -1; side <= 1; side += 2)
        {
            timber.BoxAxis(new Vector3(side * postX, lockY + .46f, 0f), .095f, .63f, .095f, Timber);
            timber.BoxAxis(new Vector3(side * postX, lockY + 1.01f, 0f), .112f, .035f, .11f, Brass);
        }
        timber.Beam(new Vector3(-postX, lockY + .78f, 0f), new Vector3(postX, lockY + .78f, 0f), .083f, Timber);
        timber.Beam(new Vector3(-postX, lockY + .38f, 0f), new Vector3(postX, lockY + .38f, 0f), .045f, Brass);
        timber.BoxAxis(new Vector3(0f, lockY + .78f, -.095f), .22f, .11f, .035f, Brass);
        Draw(lockRoot, "WorkGate", timber.ToMesh("GateBridgeWorkGate"), stoneMaterial);
        barrier = lockRoot.gameObject;

        var ready = Child(entry, "CompletedCrossing", Vector3.zero);
        finishedBridge = ready.gameObject;

        // The scene's deck is raised at the shore. Fit the ramp to that actual height.
        var ramp = new GateBridgeMesh();
        var rampCollision = new GateBridgeMesh();
        rampCollision.Sweep(Ramp, RampStart - .12f, RampEnd + .03f, .24f, -halfWidth, halfWidth, -.24f, 0f, Color.white);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = halfWidth * side;
            rampCollision.Sweep(Ramp, -2.45f, RampEnd, .25f, x - .075f, x + .075f, -.2f, .74f, Color.white);
            var lower = Ramp(-2.45f) + new Vector3(x, .7f, 0f);
            var upper = Ramp(RampEnd) + new Vector3(x, .7f, 0f);
            ramp.Beam(lower, upper, .044f, Timber);
            foreach (var cap in new[] { lower, upper })
                ramp.Beam(cap - Vector3.up * .73f, cap, .068f, Timber);
        }

        var collision = rampCollision.ToMesh("GateBridgeApproachCollision");
        ready.gameObject.AddComponent<MeshCollider>().sharedMesh = GateBridgeAssetVault.Mesh(collision);
        ramp.Sweep(Ramp, RampStart, RampEnd, .42f, -halfWidth, halfWidth, -.15f, -.035f, Slate);
        for (float s = RampStart + .16f; s < RampEnd; s += .32f)
            ramp.BoxAxis(Ramp(s) - Vector3.up * .045f, halfWidth * .99f, .045f, .147f,
                GateBridgeMesh.Sh(Timber, .9f + .17f * Mathf.PerlinNoise(s + 10f, 2f)));
        Draw(ready, "ApproachPlanks", ramp.ToMesh("GateBridgeApproach"), stoneMaterial);

        if (bridge.walkway == null)
            bridge.walkway = Child(entry, "Walkway", Vector3.zero).gameObject;
        var walkwayCollider = bridge.walkway.GetComponent<MeshCollider>();
        if (walkwayCollider == null || walkwayCollider.sharedMesh == null)
        {
            var deckCollision = new GateBridgeMesh();
            deckCollision.Sweep(Deck, -.05f, span + .65f, .35f, -halfWidth, halfWidth, -.25f, 0f, Color.white);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = halfWidth * side;
                deckCollision.Sweep(Deck, 0f, span + .65f, .35f, x - .08f, x + .08f, -.25f, .92f, Color.white);
            }

            var deckMesh = deckCollision.ToMesh("GateBridgeDeckCollision");
            deckMesh = GateBridgeAssetVault.Mesh(deckMesh);
            var deckRoot = Child(bridge.walkway.transform, "DeckAndRails", Vector3.zero);
            deckRoot.SetPositionAndRotation(entry.position, entry.rotation);
            deckRoot.gameObject.AddComponent<MeshCollider>().sharedMesh = deckMesh;
        }
        else
        {
            walkwayCollider.enabled = true;
            walkwayCollider.isTrigger = false;
        }

        var details = new GateBridgeMesh();
        for (float s = 2.8f; s < span - 2f; s += 4.15f)
        {
            var at = Deck(s) - Vector3.up * .31f;
            details.Beam(at + new Vector3(-halfWidth, .06f, -.7f), at + new Vector3(halfWidth, -.15f, .7f), .067f, Timber);
            details.Beam(at + new Vector3(halfWidth, .06f, -.7f), at + new Vector3(-halfWidth, -.15f, .7f), .067f, Timber);
        }
        foreach (float s in new[] { .65f, span * .29f, span * .71f, span - .65f })
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (halfWidth + .28f);
                Vector3 foot = Deck(s) + new Vector3(x, .26f, 0f);
                float height = s < 1f || s > span - 1f ? 1.54f : .94f;
                Vector3 lamp = foot + Vector3.up * height;
                details.Beam(foot, lamp, .075f, Timber);
                details.BoxAxis(lamp, .15f, .19f, .15f, Amber);
                details.BoxAxis(lamp + Vector3.up * .2f, .20f, .05f, .20f, Slate);
                details.BoxAxis(lamp - Vector3.up * .21f, .17f, .035f, .17f, Brass);
                for (int corner = 0; corner < 4; corner++)
                {
                    var c = lamp + new Vector3(corner < 2 ? -.15f : .15f, 0f, corner % 2 == 0 ? -.15f : .15f);
                    details.Beam(c - Vector3.up * .2f, c + Vector3.up * .2f, .017f, Slate);
                }
                if (side == (s < span * .5f ? -1 : 1)) AddLantern(ready, lamp, 4.2f);
            }
        }
        Draw(ready, "BracesAndLanterns", details.ToMesh("GateBridgeBracesAndLanterns"), stoneMaterial);
    }

    void AddLantern(Transform parent, Vector3 pos, float range)
    {
        var light = Child(parent, "LanternLight", pos).gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, .71f, .36f);
        light.range = range;
        light.shadows = LightShadows.None;
        lanterns.Add(light);
    }

    void BuildIsland()
    {
        var island = bridge.arrivalSpot.parent;
        var oldArch = island.Find("Arch");
        Vector3 world = oldArch != null ? oldArch.position : bridge.noteSpot.parent.position + bridge.BridgeForward * 1.8f;
        var gate = Child(finaleRoot, "Portal", Vector3.zero);
        gate.SetPositionAndRotation(world, Quaternion.LookRotation(bridge.BridgeForward));

        if (oldArch != null)
        {
            foreach (var r in oldArch.GetComponents<MeshRenderer>()) r.enabled = false;
            foreach (var col in oldArch.GetComponents<BoxCollider>()) col.enabled = false;
        }

        BuildArch(gate);
        var altar = bridge.noteSpot.parent;
        altar.position = gate.TransformPoint(new Vector3(-1.47f, 0f, -2.28f));
        var seal = new GateBridgeMesh();
        seal.Disc(new Vector3(-1.47f, .074f, -2.28f), .86f, .034f, 12, Slate);
        seal.Disc(new Vector3(-1.47f, .08f, -2.28f), .68f, .024f, 12, Stone);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * .2f;
            seal.BoxAxis(new Vector3(-1.47f + Mathf.Cos(a) * .77f, .092f, -2.28f + Mathf.Sin(a) * .77f),
                .04f, .015f, .04f, i % 2 == 0 ? Mint : Brass);
        }
        Draw(gate, "AltarSeal", seal.ToMesh("FinalIslandAltarSeal"), stoneMaterial, false);
        AddLantern(gate, new Vector3(-2.8f, .8f, -3.55f), 3.9f);

        var rocks = new GateBridgeMesh();
        for (int side = -1; side <= 1; side += 2)
        {
            rocks.Rock(new Vector3(side * 2.85f, .12f, -.13f), .58f, .34f, .56f, 23 + side, .15f, .3f, Stone);
            rocks.Rock(new Vector3(side * 2.85f, .27f, -.18f), .28f, .61f, .26f, 27 + side, .10f, .25f, Mint);
            rocks.Rock(new Vector3(side * 3.17f, .12f, .20f), .17f, .36f, .18f, 19 + side, .12f, .25f, Mint);
            for (int i = 0; i < 6; i++)
            {
                float x = side * (1.22f + .16f * Mathf.Sin(i * .8f));
                float z = -4.15f - i * .62f;
                rocks.Rock(new Vector3(x, .015f, z), .18f + i * .021f, .085f, .28f, i + side * 5, .24f, .3f, Moss);
            }
        }
        Draw(gate, "PathStonesAndCrystals", rocks.ToMesh("FinalIslandStones"), stoneMaterial);

        MeshRenderer field = BuildPortalSurface(gate);

        var portalLight = Child(gate, "PortalLight", new Vector3(0f, 1.5f, -.45f)).gameObject.AddComponent<Light>();
        portalLight.type = LightType.Point;
        portalLight.color = new Color(.29f, .94f, .82f);
        portalLight.range = 6f;
        portalLight.shadows = LightShadows.None;

        var walkPoint = Child(gate, "PortalWalkPoint", Vector3.zero);
        portal = gate.gameObject.AddComponent<FinalIslandPortal>();
        portal.Configure(bridge, bridge.noteSpot, field, portalLight, walkPoint);
        BuildBench(gate);
    }

    MeshRenderer BuildPortalSurface(Transform gate)
    {
        const string path = "Assets/_Project/Resources/Finale/PortalSurface.shader";
        // Persist the imported asset even when the editor's current graphics device cannot preview it.
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) shader = Shader.Find("TimberWings/PortalSurface");
        bool fallback = shader == null;
        if (fallback)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
            Debug.LogWarning("TimberWings: импортируй PortalSurface.shader. Запечён запасной материал портала.", bridge);
        }
        if (shader == null) return null;

        var material = new Material(shader) { name = "PortalSurface" };
        if (fallback) material.SetColor("_BaseColor", new Color(.18f, .77f, .66f, 1f));
        const int arcSegments = 24;
        const float bottom = .035f, depth = .015f;
        var vertices = new List<Vector3>
        {
            new Vector3(0f, SpringY, depth),
            new Vector3(-OpeningX, bottom, depth),
            new Vector3(OpeningX, bottom, depth)
        };
        for (int i = 0; i <= arcSegments; i++)
        {
            float a = Mathf.PI * i / arcSegments;
            vertices.Add(new Vector3(OpeningX * Mathf.Cos(a), SpringY + OpeningY * Mathf.Sin(a), depth));
        }
        var uv = new List<Vector2>(vertices.Count);
        var triangles = new List<int>((vertices.Count - 1) * 3);
        foreach (var point in vertices)
            uv.Add(new Vector2(Mathf.InverseLerp(-OpeningX, OpeningX, point.x),
                Mathf.InverseLerp(bottom, SpringY + OpeningY, point.y)));
        for (int i = 1; i < vertices.Count; i++)
        {
            triangles.Add(0);
            triangles.Add(i == vertices.Count - 1 ? 1 : i + 1);
            triangles.Add(i);
        }
        var surface = new Mesh { name = "PortalOpening" };
        surface.SetVertices(vertices);
        surface.SetUVs(0, uv);
        surface.SetTriangles(triangles, 0);
        surface.RecalculateBounds();
        return Draw(gate, "PortalSurface", surface, GateBridgeAssetVault.Material(material), false);
    }

    void BuildArch(Transform gate)
    {
        var arch = new GateBridgeMesh();
        const float outerX = 2.16f, outerY = 2.16f, depth = .40f;
        const int blocks = 15;
        Vector3 Point(float angle, float width, float height, float z) =>
            new Vector3(Mathf.Cos(angle) * width, SpringY + Mathf.Sin(angle) * height, z);

        for (int i = 0; i < blocks; i++)
        {
            float a = Mathf.PI * i / blocks + .008f, b = Mathf.PI * (i + 1) / blocks - .008f;
            float front = -depth - .045f * Mathf.Sin(i * 2.41f);
            Vector3 ai = Point(a, OpeningX, OpeningY, front), bi = Point(b, OpeningX, OpeningY, front);
            Vector3 ao = Point(a, outerX, outerY, front), bo = Point(b, outerX, outerY, front);
            Vector3 back = Vector3.forward * (depth - front);
            Vector3 center = (ai + bi + ao + bo) * .25f + back * .5f;
            Color color = GateBridgeMesh.Sh(Stone, .91f + .21f * Mathf.PerlinNoise(i * .51f, 2f));
            arch.Quad(ai, bi, bo, ao, color, center);
            arch.Quad(ai + back, bi + back, bo + back, ao + back, Slate, center);
            arch.Quad(ai, bi, bi + back, ai + back, Slate, center);
            arch.Quad(ao, bo, bo + back, ao + back, color, center);
            arch.Quad(ai, ao, ao + back, ai + back, Slate, center);
            arch.Quad(bi, bo, bo + back, bi + back, Slate, center);
            float mid = (a + b) * .5f;
            arch.Beam(Point(mid, 1.76f, 1.83f, front - .016f),
                Point(mid + .025f, 1.98f, 2.00f, front - .026f), .024f, i % 3 == 1 ? Brass : Mint);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            float x = (OpeningX + outerX) * .5f * side;
            arch.BoxAxis(new Vector3(x, .16f, 0f), .41f, .18f, .57f, Slate);
            for (int row = 0; row < 4; row++)
            {
                var at = new Vector3(x, .32f + row * .30f, .012f * Mathf.Sin(row * 2f));
                arch.BoxAxis(at, .30f, .147f, .40f - .018f * row, GateBridgeMesh.Sh(Stone, 1f + row * .035f));
            }
            arch.BoxAxis(new Vector3(x, .50f, 0f), .33f, .034f, .43f, Brass);
            arch.BoxAxis(new Vector3(x, 1.31f, 0f), .34f, .032f, .44f, Brass);
            BoxColliderAt(gate, new Vector3(x, .74f, 0f), new Vector3(.78f, 1.52f, 1.13f));
        }

        arch.Rock(new Vector3(0f, 3.62f, 0f), .52f, .24f, .37f, 14, .08f, .55f, Brass);
        arch.Rock(new Vector3(0f, 4.13f, 0f), .38f, .72f, .34f, 11, .04f, .8f, Mint);
        Draw(gate, "RunestoneArch", arch.ToMesh("FinalIslandArch"), stoneMaterial);
    }

    void BuildBench(Transform gate)
    {
        var bench = Child(gate, "GunnarsBench", new Vector3(4.18f, 0f, .6f));
        bench.localRotation = Quaternion.Euler(0f, 24f, 0f);
        var wood = new GateBridgeMesh();
        foreach (float x in new[] { -.65f, .65f })
            wood.BoxAxis(new Vector3(x, .2f, 0f), .13f, .2f, .22f, Slate);
        wood.BoxAxis(new Vector3(0f, .45f, 0f), .96f, .065f, .31f, Timber);
        wood.BoxAxis(new Vector3(0f, .74f, .28f), .91f, .14f, .058f, Timber);
        wood.Beam(new Vector3(-.16f, .815f, .216f), new Vector3(-.10f, .68f, .216f), .013f, Brass);
        wood.Beam(new Vector3(-.10f, .68f, .216f), new Vector3(-.04f, .815f, .216f), .013f, Brass);
        foreach (float x in new[] { .04f, .13f })
            wood.Beam(new Vector3(x, .68f, .216f), new Vector3(x, .815f, .216f), .013f, Brass);
        Draw(bench, "CarvedBench", wood.ToMesh("GunnarsSeventhBench"), stoneMaterial);
        BoxColliderAt(bench, new Vector3(0f, .23f, 0f), new Vector3(1.92f, .51f, .62f));
        bench.gameObject.AddComponent<FinalIslandEasterEgg>();
    }
}
#endif

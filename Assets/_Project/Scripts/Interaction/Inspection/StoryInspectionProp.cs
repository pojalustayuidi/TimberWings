using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The generators bake the scenery into one vertex-coloured mesh. Keep its backing
// mesh in place and give just the movable piece its own transform for the scene.
sealed class StoryInspectionProp
{
    readonly Transform owner;
    readonly MeshFilter backing;
    readonly Mesh original, scenery, part;
    readonly Transform visual, support;
    readonly Mesh supportMesh;
    readonly Material ownedMaterial;
    readonly bool usesBakedArrow;
    readonly Vector3 restPoint;
    readonly Quaternion restRotation;
    Vector3 releasePosition;
    Quaternion releaseRotation;
    bool hasRelease, arrowMoved;
    readonly StoryInspection.Kind kind;

    public Vector3 RestGrip => owner.TransformPoint(restPoint);
    public Quaternion RestRotation => owner.rotation * restRotation;
    public Vector3 Grip => visual.position;

    StoryInspectionProp(Transform owner, MeshFilter backing, Mesh original, Mesh scenery, Mesh part,
        Transform visual, Transform support, Mesh supportMesh, Material ownedMaterial, StoryInspection.Kind kind, bool usesBakedArrow)
    {
        this.owner = owner;
        this.backing = backing;
        this.original = original;
        this.scenery = scenery;
        this.part = part;
        this.visual = visual;
        this.support = support;
        this.supportMesh = supportMesh;
        this.ownedMaterial = ownedMaterial;
        this.kind = kind;
        this.usesBakedArrow = usesBakedArrow;
        restPoint = kind switch
        {
            StoryInspection.Kind.Knife => new Vector3(-.12f, .425f, .255f),
            StoryInspection.Kind.CopperArrow => new Vector3(.02f, usesBakedArrow ? .435f : .46f, .02f),
            _ => new Vector3(.035f, .402f, .075f)
        };
        restRotation = usesBakedArrow ? Quaternion.identity : Quaternion.LookRotation(Vector3.up, Vector3.right);
        ResetPose();
        if (support != null)
        {
            support.localPosition = restPoint;
            support.localRotation = restRotation;
        }
    }

    public static StoryInspectionProp Create(Transform owner, StoryInspection.Kind kind)
    {
        var filter = owner.GetComponent<MeshFilter>();
        var renderer = owner.GetComponent<MeshRenderer>();
        Mesh source = filter != null ? filter.sharedMesh : null;
        bool split = TrySplit(source, kind, out var body, out var moving);
        bool bakedArrow = split && kind == StoryInspection.Kind.CopperArrow;

        Material material = renderer != null ? renderer.sharedMaterial : null;
        Material ownedMaterial = null;
        if (material == null || material.shader == null || material.shader.name != "Project/CandyToon")
        {
            var shader = Shader.Find("Project/CandyToon") ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            ownedMaterial = new Material(shader) { name = "Story Inspection · Artefacts" };
            if (ownedMaterial.HasProperty("_BaseColor")) ownedMaterial.SetColor("_BaseColor", Color.white);
            if (ownedMaterial.HasProperty("_VertexColorStrength")) ownedMaterial.SetFloat("_VertexColorStrength", 1f);
            material = ownedMaterial;
        }

        Mesh rest = null;
        if (split)
        {
            rest = Object.Instantiate(source);
            rest.name = source.name + " (inspection backing)";
            rest.SetTriangles(body, 0);
            filter.sharedMesh = rest;
        }

        var piece = bakedArrow ? Object.Instantiate(source) : BuildArtefact(kind);
        if (bakedArrow) piece.SetTriangles(moving, 0);
        Transform visual = AddVisual(owner, "Inspection visual · " + kind, piece, material);
        Mesh supportMesh = kind == StoryInspection.Kind.Knife ? BuildArtefact(kind, true) : null;
        Transform support = supportMesh != null ? AddVisual(owner, "Gunnar knife · holder", supportMesh, material) : null;
        return new StoryInspectionProp(owner, filter, source, rest, piece, visual, support, supportMesh, ownedMaterial, kind, bakedArrow);
    }

    static Transform AddVisual(Transform owner, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name) { layer = owner.gameObject.layer };
        go.transform.SetParent(owner, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go.transform;
    }

    public static bool CanSeparate(Transform owner, StoryInspection.Kind kind)
    {
        var filter = owner.GetComponent<MeshFilter>();
        return filter != null && TrySplit(filter.sharedMesh, kind, out _, out _);
    }

    static bool TrySplit(Mesh source, StoryInspection.Kind kind, out List<int> body, out List<int> moving)
    {
        body = moving = null;
        if (source == null || !source.isReadable || source.subMeshCount != 1) return false;

        Bounds bounds = source.bounds;
        bool arrow = kind == StoryInspection.Kind.CopperArrow;
        // Only cut meshes that match the footprint of the original generated support.
        // Other versions keep their baked mesh and receive a separate inspection prop.
        if (!arrow && (bounds.min.y < -.23f || bounds.max.y > .48f || bounds.max.y < .36f ||
            bounds.size.x < .5f || bounds.size.x > 1.75f)) return false;

        var vertices = source.vertices;
        var colors = arrow ? source.colors32 : null;
        if (arrow && colors.Length != vertices.Length) return false;
        body = new List<int>();
        moving = new List<int>();
        var indices = source.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            bool match = arrow ? ArrowVertex(vertices[a], colors[a]) && ArrowVertex(vertices[b], colors[b]) &&
                ArrowVertex(vertices[c], colors[c]) : MemoryTriangle(vertices[a], vertices[b], vertices[c]);
            var output = match ? moving : body;
            output.Add(a); output.Add(b); output.Add(c);
        }

        return moving.Count > 0 && body.Count > 0 && moving.Count < body.Count * 2;
    }

    static bool MemoryTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
        float high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
        return low >= .338f && high > .351f && high <= .418f &&
            Mathf.Max(Mathf.Abs(a.z), Mathf.Max(Mathf.Abs(b.z), Mathf.Abs(c.z))) < .14f;
    }

    static bool ArrowVertex(Vector3 vertex, Color32 color)
    {
        if (vertex.y < .34f || vertex.y > .58f) return false;
        return Near(color, 0xE4, 0xB7, 0x7C) || Near(color, 0xCB, 0x92, 0x53) ||
            Near(color, 198, 121, 51);
    }

    static bool Near(Color32 color, int r, int g, int b) =>
        Mathf.Abs(color.r - r) + Mathf.Abs(color.g - g) + Mathf.Abs(color.b - b) <= 24;

    public void Hold(Vector3 grip, Quaternion rotation)
    {
        hasRelease = false;
        visual.SetPositionAndRotation(grip, rotation);
    }

    public void Release(float fraction)
    {
        if (!hasRelease)
        {
            releasePosition = visual.position;
            releaseRotation = visual.rotation;
            hasRelease = true;
        }
        visual.SetPositionAndRotation(Vector3.Lerp(releasePosition, RestGrip, fraction), Quaternion.Slerp(releaseRotation, RestRotation, fraction));
    }

    public void ResetPose()
    {
        hasRelease = false;
        arrowMoved = false;
        visual.localPosition = usesBakedArrow ? Vector3.zero : restPoint;
        visual.localRotation = restRotation;
    }

    public void TurnArrow()
    {
        arrowMoved = !arrowMoved;
        Quaternion rotation = Quaternion.Euler(0f, arrowMoved ? 16f : 0f, 0f);
        visual.localRotation = rotation * restRotation;
        if (usesBakedArrow) visual.localPosition = restPoint - rotation * restPoint;
    }

    public void Dispose()
    {
        if (backing != null && scenery != null && backing.sharedMesh == scenery) backing.sharedMesh = original;
        if (visual != null) Object.Destroy(visual.gameObject);
        if (scenery != null) Object.Destroy(scenery);
        if (part != null) Object.Destroy(part);
        if (support != null) Object.Destroy(support.gameObject);
        if (supportMesh != null) Object.Destroy(supportMesh);
        if (ownedMaterial != null) Object.Destroy(ownedMaterial);
    }

    static Mesh BuildArtefact(StoryInspection.Kind kind, bool support = false)
    {
        var vertices = new List<Vector3>();
        var colours = new List<Color32>();
        var indices = new List<int>();

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colours.Add(color); colours.Add(color); colours.Add(color);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        void Prism(Vector2[] outline, float low, float high, Color32 color)
        {
            Vector3 At(int i, float z) => new Vector3(outline[i].x, outline[i].y, z);
            for (int i = 1; i < outline.Length - 1; i++)
            {
                Triangle(At(0, high), At(i, high), At(i + 1, high), color);
                Triangle(At(0, low), At(i + 1, low), At(i, low), color);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                Triangle(At(i, low), At(j, low), At(j, high), color);
                Triangle(At(i, low), At(j, high), At(i, high), color);
            }
        }

        void Box(float left, float bottom, float right, float top, float low, float high, Color32 color) =>
            Prism(new[] { new Vector2(left, bottom), new Vector2(right, bottom),
                new Vector2(right, top), new Vector2(left, top) }, low, high, color);

        var steel = new Color32(151, 174, 173, 255);
        var edge = new Color32(206, 216, 207, 255);
        var wood = new Color32(106, 68, 48, 255);
        var brass = new Color32(192, 141, 83, 255);
        var engraving = new Color32(46, 27, 22, 255);

        if (support)
        {
            Box(-.095f, -.18f, .095f, .305f, -.075f, -.029f, wood);
            Box(-.092f, -.175f, .092f, .300f, -.031f, -.026f, new Color32(140, 101, 72, 255));
        }
        else if (kind == StoryInspection.Kind.Knife)
        {
            Prism(new[] { new Vector2(-.046f, .081f), new Vector2(.060f, .081f),
                new Vector2(.052f, .216f), new Vector2(-.015f, .315f), new Vector2(-.044f, .249f) },
                -.012f, .012f, steel);
            Prism(new[] { new Vector2(.044f, .09f), new Vector2(.060f, .09f),
                new Vector2(.052f, .216f), new Vector2(-.015f, .315f), new Vector2(.031f, .219f) },
                -.0122f, .0122f, edge);
            Prism(new[] { new Vector2(-.027f, -.14f), new Vector2(.024f, -.14f),
                new Vector2(.041f, -.11f), new Vector2(.034f, .075f),
                new Vector2(-.033f, .075f), new Vector2(-.039f, -.11f) }, -.026f, .026f, wood);
            Box(-.059f, .065f, .059f, .086f, -.030f, .030f, brass);
            Box(-.027f, -.142f, .024f, -.133f, -.027f, .027f, brass);

            // Two shallow strokes forming the Cyrillic Г on the front of the pommel.
            Box(-.018f, -.121f, -.010f, -.084f, .026f, .027f, engraving);
            Box(-.010f, -.092f, .016f, -.084f, .026f, .027f, engraving);
        }
        else if (kind == StoryInspection.Kind.CopperArrow)
        {
            Box(-.021f, -.22f, .021f, .095f, -.013f, .013f, brass);
            Prism(new[] { new Vector2(-.079f, .077f), new Vector2(.077f, .077f), new Vector2(.007f, .235f) },
                -.014f, .014f, new Color32(214, 146, 79, 255));
            Box(-.031f, -.159f, .031f, -.117f, -.015f, .015f, engraving);
        }
        else if (kind == StoryInspection.Kind.Ribbon)
        {
            var cloth = new Color32(172, 150, 192, 255);
            Prism(new[] { new Vector2(-.071f, -.245f), new Vector2(.079f, -.215f),
                new Vector2(.058f, .205f), new Vector2(.010f, .251f), new Vector2(-.061f, .213f) },
                -.009f, .009f, cloth);
            for (int i = 0; i < 6; i++)
            {
                float y = -.116f + i * .042f;
                Box(-.046f, y, .002f, y + (i == 5 ? .005f : .008f), .009f, .010f, engraving);
            }
        }
        else
        {
            var oldWood = new Color32(140, 101, 72, 255);
            Prism(new[] { new Vector2(-.112f, -.247f), new Vector2(.096f, -.230f),
                new Vector2(.104f, .213f), new Vector2(-.087f, .235f) }, -.014f, .014f, oldWood);
            for (int row = 0; row < 3; row++)
            for (int word = 0; word < 4; word++)
            {
                float y = -.164f + word * .09f + (row % 2) * .016f;
                float x = -.057f + row * .049f;
                Box(x, y, x + .008f, y + .04f + (word % 3) * .008f, .014f, .015f, wood);
            }
        }

        var mesh = new Mesh { name = "Inspection artefact · " + kind };
        mesh.SetVertices(vertices);
        mesh.SetColors(colours);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
}

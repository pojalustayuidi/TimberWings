using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChopPrototypeLog
{
    const int Sides = 12;
    const int BarkRings = 5;
    static readonly float[] RingPositions = { 0f, .12f, .50f, .88f, 1f };

    sealed class Section
    {
        internal GameObject root;
        internal BoxCollider solid;
        internal Mesh mesh;
        internal float start, end, length, shift;
    }

    Section[] sections;
    Transform[] cutAnchors;
    MeshRenderer[] cutMarks;
    Material ringMaterial;
    MaterialPropertyBlock markerProperties;
    readonly List<Vector3> sectionVertices = new List<Vector3>(BarkRings * Sides + Sides * 12 + 2);
    readonly List<int> barkTriangles = new List<int>(BarkRings * Sides * 6);
    readonly List<int> woodTriangles = new List<int>(Sides * 50);
    readonly List<int> ringTriangles = new List<int>(Sides * 36);

    void BuildSections()
    {
        var layout = state.layout;
        sections = new Section[SectionCount];
        cutAnchors = new Transform[CutCount];
        cutMarks = new MeshRenderer[CutCount];
        Material bark = TreeMeshFactory.Bark();
        Material endgrain = TreeMeshFactory.Cut();
        ringMaterial = new Material(endgrain) { color = new Color(.56f, .34f, .17f) };
        Material[] materials = { bark, endgrain, ringMaterial };
        for (int i = 0; i < SectionCount; i++)
        {
            var root = new GameObject("Log section " + (i + 1));
            root.transform.SetParent(transform, false);
            float start = Joint(i);
            float end = Joint(i + 1);
            var part = new Section
            {
                root = root, start = start, end = end, length = end - start,
                mesh = new Mesh { name = "Tapered bark and end grain" }
            };
            sections[i] = part;
            part.solid = root.AddComponent<BoxCollider>();
            part.solid.size = new Vector3(layout.radius * 1.72f, layout.radius * 1.59f, part.length);
            part.solid.sharedMaterial = TreeCollision.SlipMat;
            root.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            RebuildSection(i);
        }

        for (int i = 0; i < CutCount; i++)
        {
            var anchor = new GameObject("Cut " + (i + 1)).transform;
            anchor.SetParent(transform, false);
            cutAnchors[i] = anchor;
            cutMarks[i] = CreateCutMark(anchor, endgrain);
        }
    }

    // Keep the butt a little longer than the crown pieces.
    float Joint(int index)
    {
        float fraction;
        if (index == 0) fraction = 0f;
        else if (index == SectionCount) fraction = 1f;
        else if (CutCount == 1) fraction = .53f;
        else fraction = index == 1 ? .37f : .70f;
        return (fraction - .5f) * state.layout.length;
    }

    void UpdateSections()
    {
        for (int i = 0; i < sections.Length; i++)
        {
            var part = sections[i];
            float shift = 0f;
            float bounce = 0f;
            for (int seam = 0; seam < separation.Length; seam++)
            {
                float t = separation[seam];
                float split = (seam + 1f) / sections.Length;
                shift += SectionGap * Mathf.SmoothStep(0f, 1f, t) * (i <= seam ? split - 1f : split);
                bounce = Mathf.Max(bounce, Mathf.Sin(Mathf.PI * t) * .027f);
            }
            part.shift = shift;
            part.root.transform.localPosition = new Vector3(0f, bounce, (part.start + part.end) * .5f + shift);
            bool visible = Ready && (state.wood[i] > 0 || !state.SectionFree(i));
            if (part.root.activeSelf != visible) part.root.SetActive(visible);
        }
        for (int i = 0; i < CutCount; i++)
        {
            UpdateCutAnchor(i);
            bool visible = CanCut(i);
            if (cutMarks[i].enabled != visible) StyleCut(i);
        }
    }

    void UpdateCutAnchor(int cut)
    {
        float z = Joint(cut + 1) + (sections[cut].shift + sections[cut + 1].shift) * .5f;
        cutAnchors[cut].localPosition = new Vector3(0f, state.layout.radius * .86f, z);
    }

    internal Vector3 CutContact(int cut, Vector3 actor)
    {
        bool positive = transform.InverseTransformPoint(actor).x >= 0f;
        float joint = Joint(cut + 1);
        float depth = KerfDepth(state.cuts[cut]);
        // Use the near sloping face of the kerf. The axe stays above the wood
        // and reaches it without forcing either of Leo's wrists past its limit.
        Vector3 edge = Vector3.Lerp(Rim(joint, joint, positive ? 1 : Sides - 1, depth),
            Rim(joint, joint, positive ? 2 : Sides - 2, depth), .72f);
        edge.z = cutAnchors[cut].localPosition.z;
        return transform.TransformPoint(edge);
    }

    void RebuildSection(int index)
    {
        Section part = sections[index];
        var vertices = sectionVertices;
        var bark = barkTriangles;
        var wood = woodTriangles;
        var rings = ringTriangles;
        vertices.Clear();
        bark.Clear();
        wood.Clear();
        rings.Clear();
        float middle = (part.start + part.end) * .5f;
        for (int ring = 0; ring < BarkRings; ring++)
        {
            float z = Mathf.Lerp(part.start, part.end, RingPositions[ring]);
            if (ring == 1 && index > 0) z = part.start + KerfShoulder(index - 1, part.length);
            if (ring == 3 && index < CutCount) z = part.end - KerfShoulder(index, part.length);
            for (int i = 0; i < Sides; i++)
                vertices.Add(Rim(z, middle, i, Kerf(index, ring)));
        }

        for (int ring = 0; ring < BarkRings - 1; ring++)
        {
            for (int i = 0; i < Sides; i++)
            {
                int a = ring * Sides + i;
                int b = ring * Sides + (i + 1) % Sides;
                bool worked = ring == 0 && index > 0 && state.cuts[index - 1] > 0f ||
                    ring == BarkRings - 2 && index < CutCount && state.cuts[index] > 0f;
                var surface = worked && (i < 2 || i >= Sides - 2) ? wood : bark;
                Quad(surface, a, a + Sides, b + Sides, b);
            }
        }

        // Internal faces are hidden until the seam is opened, avoiding coplanar end grain.
        if (index == 0 || state.cuts[index - 1] >= 1f)
            Cap(part.start, middle, -1f, Kerf(index, 0), vertices, wood, rings);
        if (index == CutCount || state.cuts[index] >= 1f)
            Cap(part.end, middle, 1f, Kerf(index, BarkRings - 1), vertices, wood, rings);

        part.mesh.Clear();
        part.mesh.SetVertices(vertices);
        part.mesh.subMeshCount = 3;
        part.mesh.SetTriangles(bark, 0);
        part.mesh.SetTriangles(wood, 1);
        part.mesh.SetTriangles(rings, 2);
        part.mesh.RecalculateNormals();
        part.mesh.RecalculateBounds();
    }

    float Kerf(int section, int ring)
    {
        if (ring == 0 && section > 0) return KerfDepth(state.cuts[section - 1]);
        if (ring == BarkRings - 1 && section < CutCount) return KerfDepth(state.cuts[section]);
        return 0f;
    }

    static float KerfDepth(float damage) => Mathf.Lerp(.018f, .81f, Mathf.Pow(damage, .84f));

    float KerfShoulder(int cut, float length) => Mathf.Min(length * .22f,
        (.055f + state.layout.radius * .18f) * Mathf.Lerp(.64f, 1.5f, state.cuts[cut]));

    Vector3 Rim(float z, float middle, int side, float kerf)
    {
        float angle = 2f * Mathf.PI * side / Sides;
        float cos = Mathf.Cos(angle);
        float taper = 1f - .22f * (z / state.layout.length + .5f);
        float rough = 1f + .052f * Mathf.Sin(side * 2.4f + z * 2.1f);
        float r = state.layout.radius;
        float x = Mathf.Sin(angle) * r * taper * rough;
        // Preserve the resting surface along the bottom while the bark narrows towards the top.
        float y = cos * r * (cos < 0f ? 1f : Mathf.Lerp(.90f, 1f, taper) - kerf * cos * cos);
        return new Vector3(x, y, z - middle);
    }

    static readonly float[] GrainRadii = { 1f, .78f, .70f, .48f, .40f, .22f };
    void Cap(float z, float middle, float sign, float kerf, List<Vector3> vertices, List<int> wood, List<int> rings)
    {
        int first = vertices.Count;
        for (int ring = 0; ring < GrainRadii.Length; ring++)
        {
            float r = GrainRadii[ring];
            for (int side = 0; side < Sides; side++)
            {
                Vector3 p = Rim(z, middle, side, kerf);
                p.x *= r;
                p.y = p.y * r + .028f * (1f - r) * state.layout.radius;
                vertices.Add(p);
            }
        }

        int center = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, z - middle));
        for (int i = 0; i < Sides; i++)
        {
            int next = (i + 1) % Sides;
            for (int ring = 0; ring < GrainRadii.Length - 1; ring++)
            {
                int a = first + ring * Sides + i;
                int b = first + ring * Sides + next;
                var surface = ring % 2 == 0 ? wood : rings;
                if (sign > 0f) Quad(surface, b, a, a + Sides, b + Sides);
                else Quad(surface, a, b, b + Sides, a + Sides);
            }
            int last = first + (GrainRadii.Length - 1) * Sides;
            wood.Add(center);
            wood.Add(last + (sign > 0f ? next : i));
            wood.Add(last + (sign > 0f ? i : next));
        }
    }

    static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    void OnDestroy()
    {
        if (sections != null)
            foreach (var section in sections)
                if (section?.mesh != null) Destroy(section.mesh);
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}

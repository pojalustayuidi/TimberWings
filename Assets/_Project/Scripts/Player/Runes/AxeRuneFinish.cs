using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Uses the axe renderer itself so first-person culling, shadows and holstering also mask the inlay.
internal sealed class AxeRuneFinish
{
    static readonly Dictionary<(Mesh mesh, AxeId axe, AxeRuneId rune), Mesh> FinishedMeshes =
        new Dictionary<(Mesh, AxeId, AxeRuneId), Mesh>();
    static readonly Material[] Metals = new Material[(int)AxeRuneId.GunnarStrike + 1];
    static readonly Vector2[] Corners =
    {
        new Vector2(0, -.94f), new Vector2(.32f, -.79f), new Vector2(.89f, -.29f), new Vector2(.96f, 0),
        new Vector2(.79f, .37f), new Vector2(.32f, .82f), new Vector2(0, .96f), new Vector2(-.32f, .79f),
        new Vector2(-.86f, .31f), new Vector2(-.96f, 0), new Vector2(-.80f, -.36f), new Vector2(-.32f, -.81f)
    };

    MeshRenderer renderer;
    MeshFilter filter;
    Mesh originalMesh;
    Material[] originalMaterials;
    AxeRuneId shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        foreach (Mesh mesh in FinishedMeshes.Values)
            if (mesh != null) Object.Destroy(mesh);
        FinishedMeshes.Clear();
        for (int i = 0; i < Metals.Length; i++)
        {
            if (Metals[i] != null) Object.Destroy(Metals[i]);
            Metals[i] = null;
        }
    }

    internal void Set(MeshRenderer target, AxeId axe, AxeRuneId rune)
    {
        if (target != renderer)
        {
            Restore();
            renderer = target;
            filter = target != null ? target.GetComponent<MeshFilter>() : null;
            originalMesh = filter != null ? filter.sharedMesh : null;
            originalMaterials = target != null ? target.sharedMaterials : null;
        }
        if (!AxeRunes.Supports(axe) || !AxeRunes.IsValid(rune)) rune = AxeRuneId.None;
        if (renderer == null || shown == rune) return;
        if (rune == AxeRuneId.None)
        {
            Restore();
            return;
        }
        if (originalMesh == null || !originalMesh.isReadable || originalMaterials.Length != originalMesh.subMeshCount) return;
        Material socket = Metal(AxeRuneId.None);
        Material light = Metal(rune);
        if (socket == null || light == null) return;

        var key = (originalMesh, axe, rune);
        if (!FinishedMeshes.TryGetValue(key, out Mesh finished) || finished == null)
        {
            finished = Build(originalMesh, axe, rune);
            FinishedMeshes[key] = finished;
        }
        var materials = new Material[originalMaterials.Length + 2];
        originalMaterials.CopyTo(materials, 0);
        materials[materials.Length - 2] = socket;
        materials[materials.Length - 1] = light;
        filter.sharedMesh = finished;
        renderer.sharedMaterials = materials;
        shown = rune;
    }

    void Restore()
    {
        if (shown == AxeRuneId.None) return;
        if (renderer != null && filter != null)
        {
            filter.sharedMesh = originalMesh;
            renderer.sharedMaterials = originalMaterials;
        }
        shown = AxeRuneId.None;
    }

    static Material Metal(AxeRuneId rune)
    {
        int index = (int)rune;
        if (Metals[index] != null) return Metals[index];
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        bool socket = rune == AxeRuneId.None;
        var material = new Material(shader) { name = "Axe inlay " + rune, hideFlags = HideFlags.HideAndDontSave };
        Color accent = AxeRuneArt.For(rune).Accent;
        material.SetColor("_BaseColor", socket ? new Color32(44, 40, 67, 255) : accent);
        material.SetFloat("_Metallic", socket ? .30f : .16f);
        material.SetFloat("_Smoothness", .43f);
        if (!socket)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", accent * .33f);
        }
        return Metals[index] = material;
    }

    // Centres measured in the imported Iron, Steel and Gunnar meshes. X points out of the cheek.
    static Vector3 Mount(AxeId axe)
    {
        if (axe == AxeId.Iron) return new Vector3(.0365f, .267f, .139f);
        if (axe == AxeId.Gunnar) return new Vector3(.0447f, .276f, .159f);
        return new Vector3(.0338f, .272f, .153f);
    }

    static Mesh Build(Mesh original, AxeId axe, AxeRuneId rune)
    {
        var centre = Mount(axe);
        const float radius = .050f;
        var vertices = new List<Vector3>(256);
        var plate = new List<int>(192);
        var ink = new List<int>(192);
        var design = AxeRuneArt.For(rune);
        bool ornate = AxeRunes.Get(rune).Rarity >= RuneRarity.Legendary;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector2 a = Corners[i], b = Corners[(i + 1) % Corners.Length];
                Triangle(plate, At(Vector2.zero), At(a), At(b));
                Quad(plate, At(a * 1.025f, -.010f), At(b * 1.025f, -.010f), At(b), At(a));
                if (ornate || i % 3 == 1) Stroke(a * .83f, b * .83f, .024f);
            }
            foreach (Vector2[] path in design.Lines)
                for (int i = 1; i < path.Length; i++) Stroke(path[i - 1] * .72f, path[i] * .72f, .039f);

            Vector3 At(Vector2 point, float layer = 0f) =>
                new Vector3(side * (centre.x + layer), centre.y + point.y * radius, centre.z - side * point.x * radius);

            void Stroke(Vector2 a, Vector2 b, float width)
            {
                Vector2 forward = (b - a).normalized;
                Vector2 n = new Vector2(-forward.y, forward.x) * width;
                a -= forward * width * .47f;
                b += forward * width * .47f;
                const float top = .0014f;
                Quad(ink, At(a - n, top), At(b - n, top), At(b + n, top), At(a + n, top));
            }
        }

        var stamp = new Mesh { name = "Rune stamp", subMeshCount = 2 };
        stamp.SetVertices(vertices);
        stamp.SetTriangles(plate, 0);
        stamp.SetTriangles(ink, 1);
        stamp.RecalculateNormals();
        var parts = new CombineInstance[original.subMeshCount + stamp.subMeshCount];
        for (int i = 0; i < original.subMeshCount; i++)
            parts[i] = new CombineInstance { mesh = original, subMeshIndex = i };
        for (int i = 0; i < stamp.subMeshCount; i++)
            parts[original.subMeshCount + i] = new CombineInstance { mesh = stamp, subMeshIndex = i };
        var result = new Mesh
        {
            name = axe + " • " + rune, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave
        };
        result.CombineMeshes(parts, false, false);
        Object.Destroy(stamp);
        return result;

        void Triangle(List<int> faces, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            faces.Add(start); faces.Add(start + 1); faces.Add(start + 2);
        }

        void Quad(List<int> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(faces, a, b, c);
            Triangle(faces, a, c, d);
        }
    }
}

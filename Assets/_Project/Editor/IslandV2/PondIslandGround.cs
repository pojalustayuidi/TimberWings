using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// IslandV2 uses separate mesh chunks for rendering and a hidden Terrain for physics.
sealed class PondIslandGround
{
    internal const string BakeFolder = "Assets/_Game/Pond/Baked";
    const string SourceName = "Pond Ground Source";

    readonly Scene scene;
    readonly Terrain terrain;
    readonly TerrainCollider collider;
    readonly TerrainData originalTerrain;
    readonly Chunk[] chunks;
    readonly GameObject previousSource;

    sealed class Chunk
    {
        internal MeshFilter filter;
        internal MeshRenderer renderer;
        internal Mesh mesh;
        internal Material material;
    }

    internal PondIslandGround(Scene scene)
    {
        this.scene = scene;
        var roots = scene.GetRootGameObjects();
        previousSource = Array.Find(roots, go => go.name == SourceName);
        var generated = Array.Find(roots, go => go.name == "IslandV2")?.transform.Find("_Generated");
        var ground = generated != null ? generated.Find(IslandV2.IslandBlockout.TerrainName) : null;
        terrain = ground != null ? ground.GetComponent<Terrain>() : null;
        collider = ground != null ? ground.GetComponent<TerrainCollider>() : null;
        if (terrain == null || collider == null || !collider.enabled || !ground.gameObject.activeInHierarchy ||
            collider.terrainData == null || terrain.terrainData != collider.terrainData)
            throw new InvalidOperationException("Открой игровую сцену острова с активным Ground_Terrain.");

        var savedTerrain = previousSource != null ? previousSource.GetComponent<TerrainCollider>() : null;
        originalTerrain = savedTerrain != null && IsBakedAsset(collider.terrainData) ? savedTerrain.terrainData : collider.terrainData;
        chunks = generated.GetComponentsInChildren<MeshFilter>(false)
            .Where(filter => filter.name.StartsWith(IslandV2.IslandBlockout.ChunkPrefix, StringComparison.Ordinal))
            .Select(ReadChunk)
            .Where(chunk => chunk.renderer != null && chunk.renderer.enabled && chunk.mesh != null)
            .ToArray();
        if (originalTerrain == null || chunks.Length == 0)
            throw new InvalidOperationException("Для пруда нужны TerrainData и видимые чанки земли острова.");
    }

    Chunk ReadChunk(MeshFilter filter)
    {
        var renderer = filter.GetComponent<MeshRenderer>();
        var saved = previousSource != null ? previousSource.transform.Find(filter.name) : null;
        var mesh = filter.sharedMesh;
        var material = renderer != null ? renderer.sharedMaterial : null;
        if (saved != null)
        {
            if (IsBakedAsset(mesh)) mesh = saved.GetComponent<MeshFilter>().sharedMesh;
            if (IsBakedAsset(material)) material = saved.GetComponent<MeshRenderer>().sharedMaterial;
        }

        return new Chunk { filter = filter, renderer = renderer, mesh = mesh, material = material };
    }

    static bool IsBakedAsset(Object asset) =>
        asset != null && AssetDatabase.GetAssetPath(asset).StartsWith(BakeFolder + "/", StringComparison.Ordinal);

    internal float HeightAt(Vector3 world)
    {
        Vector3 center = terrain.GetPosition();
        Vector3 size = originalTerrain.size;
        return center.y + originalTerrain.GetInterpolatedHeight(
            (world.x - center.x) / size.x, (world.z - center.z) / size.z);
    }

    internal bool HasGround(Vector3 world)
    {
        Vector3 origin = terrain.GetPosition();
        Vector3 size = originalTerrain.size;
        float x = (world.x - origin.x) / size.x;
        float z = (world.z - origin.z) / size.z;
        if (x < 0f || x >= 1f || z < 0f || z >= 1f) return false;
        int res = originalTerrain.holesResolution;
        return !originalTerrain.IsHole(Mathf.FloorToInt(x * res), Mathf.FloorToInt(z * res));
    }

    internal void Apply(string folder, Vector3 center, float radius, Func<Vector3, float, float> carvedY)
    {
        SaveSource();
        TerrainData cutTerrain = CutTerrain(center, radius, carvedY);
        AssetDatabase.CreateAsset(cutTerrain, folder + "/Terrain.asset");
        Undo.RecordObjects(new Object[] { terrain, collider }, "Запечь рельеф пруда");
        terrain.terrainData = cutTerrain;
        collider.terrainData = cutTerrain;

        var materials = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (var chunk in chunks)
        {
            Mesh mesh = CutChunk(chunk, center, radius, carvedY);
            if (mesh != chunk.mesh)
                AssetDatabase.CreateAsset(mesh, folder + "/" + chunk.filter.name + ".asset");
            Undo.RecordObject(chunk.filter, "Запечь меш земли");
            chunk.filter.sharedMesh = mesh;

            if (chunk.material == null) continue;
            if (!materials.TryGetValue(chunk.material, out var corrected))
            {
                corrected = IslandV2.IslandBlockout.PondGroundMaterial(chunk.material, folder, materials.Count);
                materials.Add(chunk.material, corrected);
            }
            Undo.RecordObject(chunk.renderer, "Обновить цвет берега");
            chunk.renderer.sharedMaterial = corrected;
        }

        terrain.Flush();
    }

    void SaveSource()
    {
        var source = new GameObject(SourceName) { tag = "EditorOnly" };
        SceneManager.MoveGameObjectToScene(source, scene);
        source.SetActive(false);
        var sourceCollider = source.AddComponent<TerrainCollider>();
        sourceCollider.terrainData = originalTerrain;
        foreach (var chunk in chunks)
        {
            var saved = new GameObject(chunk.filter.name);
            saved.transform.SetParent(source.transform, false);
            saved.AddComponent<MeshFilter>().sharedMesh = chunk.mesh;
            saved.AddComponent<MeshRenderer>().sharedMaterial = chunk.material;
        }
        Undo.RegisterCreatedObjectUndo(source, "Сохранить исходный рельеф");
        if (previousSource != null) Undo.DestroyObjectImmediate(previousSource);
    }

    internal void Restore()
    {
        if (previousSource == null) return;
        Undo.RecordObjects(new Object[] { terrain, collider }, "Восстановить землю под прудом");
        terrain.terrainData = originalTerrain;
        collider.terrainData = originalTerrain;
        foreach (var chunk in chunks)
        {
            Undo.RecordObjects(new Object[] { chunk.filter, chunk.renderer }, "Восстановить землю под прудом");
            chunk.filter.sharedMesh = chunk.mesh;
            chunk.renderer.sharedMaterial = chunk.material;
        }
        Undo.DestroyObjectImmediate(previousSource);
        terrain.Flush();
    }

    TerrainData CutTerrain(Vector3 center, float radius, Func<Vector3, float, float> carvedY)
    {
        Vector3 origin = terrain.GetPosition();
        Vector3 size = originalTerrain.size;
        int last = originalTerrain.heightmapResolution - 1;
        float dx = size.x / last, dz = size.z / last;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - origin.x) / dx), 0, last);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((center.x + radius - origin.x) / dx), 0, last);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((center.z - radius - origin.z) / dz), 0, last);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((center.z + radius - origin.z) / dz), 0, last);
        var heights = originalTerrain.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        float padding = Mathf.Max(dx, dz);
        for (int j = 0; j < heights.GetLength(0); j++)
        for (int i = 0; i < heights.GetLength(1); i++)
        {
            Vector3 world = origin + new Vector3((x0 + i) * dx, heights[j, i] * size.y, (z0 + j) * dz);
            float y = carvedY(world, padding);
            if (y == world.y) continue;
            if (y < origin.y)
                throw new InvalidOperationException("Глубина пруда выходит за диапазон высот Terrain.");
            heights[j, i] = (y - origin.y) / size.y;
        }

        var result = Object.Instantiate(originalTerrain);
        result.name = "Pond Island Terrain";
        result.SetHeights(x0, z0, heights);
        return result;
    }

    static Mesh CutChunk(Chunk chunk, Vector3 center, float radius, Func<Vector3, float, float> carvedY)
    {
        Bounds bounds = chunk.renderer.bounds;
        if (center.x + radius < bounds.min.x || center.x - radius > bounds.max.x ||
            center.z + radius < bounds.min.z || center.z - radius > bounds.max.z)
            return chunk.mesh;

        var vertices = chunk.mesh.vertices;
        Transform space = chunk.filter.transform;
        // The original island chunks use 2 m cells. Padding by a diagonal keeps
        // their coarser triangles below the new basin at the waterline.
        const float padding = 2.85f;
        bool modified = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = space.TransformPoint(vertices[i]);
            float y = carvedY(world, padding);
            if (y == world.y) continue;
            world.y = y;
            vertices[i] = space.InverseTransformPoint(world);
            modified = true;
        }
        if (!modified) return chunk.mesh;

        var result = Object.Instantiate(chunk.mesh);
        result.name = chunk.mesh.name;
        result.vertices = vertices;
        result.RecalculateBounds();
        return result;
    }
}

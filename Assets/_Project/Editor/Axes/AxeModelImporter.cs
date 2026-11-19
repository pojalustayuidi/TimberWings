using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "twaxe")]
public sealed class AxeModelImporter : ScriptedImporter
{
    [Serializable]
    sealed class ModelData
    {
        public float[] vertices, normals;
        public Vector3 edge;
        public Surface[] surfaces;
    }

    [Serializable]
    sealed class Surface
    {
        public string name, color, emission;
        public float metallic, smoothness;
        public int[] indices;
    }

    public override void OnImportAsset(AssetImportContext context)
    {
        var data = JsonUtility.FromJson<ModelData>(File.ReadAllText(context.assetPath));
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("Требуется URP Lit для материалов топора.");

        string name = Path.GetFileNameWithoutExtension(context.assetPath);
        var mesh = new Mesh { name = name + " • mesh" };
        mesh.vertices = Unpack(data.vertices);
        mesh.normals = Unpack(data.normals);
        mesh.subMeshCount = data.surfaces.Length;
        var materials = new Material[data.surfaces.Length];

        for (int i = 0; i < data.surfaces.Length; i++)
        {
            var surface = data.surfaces[i];
            var material = new Material(shader) { name = surface.name };
            if (ColorUtility.TryParseHtmlString(surface.color, out var color))
                material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", surface.metallic);
            material.SetFloat("_Smoothness", surface.smoothness);
            if (ColorUtility.TryParseHtmlString(surface.emission, out var emission))
            {
                material.SetColor("_EmissionColor", emission * .36f);
                material.EnableKeyword("_EMISSION");
            }
            materials[i] = material;
            mesh.SetTriangles(surface.indices, i, false);
            context.AddObjectToAsset("material_" + surface.name, material);
        }

        mesh.RecalculateBounds();
        var model = new GameObject(name);
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        model.AddComponent<MeshRenderer>().sharedMaterials = materials;
        var edge = new GameObject("CuttingEdge").transform;
        edge.SetParent(model.transform, false);
        edge.localPosition = data.edge;

        context.AddObjectToAsset("mesh", mesh);
        context.AddObjectToAsset("axe", model);
        context.SetMainObject(model);
    }

    static Vector3[] Unpack(float[] coordinates)
    {
        var vectors = new Vector3[coordinates.Length / 3];
        for (int i = 0; i < vectors.Length; i++)
            vectors[i] = new Vector3(coordinates[i * 3], coordinates[i * 3 + 1], coordinates[i * 3 + 2]);
        return vectors;
    }
}

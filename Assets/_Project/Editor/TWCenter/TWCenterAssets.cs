using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal sealed class TWCenterAssets
    {
        public readonly string folder;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        public TWCenterAssets(string folder)
        {
            this.folder = folder;
            Add("Wood", 0xB77C45); Add("DarkWood", 0x704A30); Add("EndGrain", 0xE4B77C);
            Add("Stone", 0xDBC79D); Add("Leaf", 0x67A951); Add("LeafLight", 0x8BBD5E);
            Add("CreamFlower", 0xFFF1CB); Add("PurpleFlower", 0xAB87CB); Add("Metal", 0x3E4B49);
            Add("Rope", 0xBFA474); Add("Cup", 0xE9D5A7); Add("Glass", 0xFFD586, true);
        }

        void Add(string name, int hex, bool emission = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) throw new InvalidOperationException("Не найден URP Simple Lit.");
            Color colour = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
            var mat = new Material(shader) { name = "Center_" + name };
            mat.SetColor("_BaseColor", colour); mat.SetFloat("_Smoothness", 0.07f);
            mat.SetColor("_SpecColor", new Color(0.06f, 0.06f, 0.06f));
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.32f));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, folder + "/" + name + ".mat"); materials.Add(name, mat);
        }

        public Material Material(string key) { return materials[key]; }

        public void AddHearthPalette()
        {
            Add("Ash", 0x51463D); Add("Coal", 0xA34022); Add("FireOrange", 0xFF7528, true); Add("FireCore", 0xFFE783, true);
            materials["FireOrange"].SetColor("_EmissionColor", new Color(2.8f, 0.65f, 0.10f));
            materials["FireCore"].SetColor("_EmissionColor", new Color(3.2f, 2.0f, 0.65f));
        }

        public Mesh Save(Mesh mesh, string name)
        {
            TWCenterValidation.Mesh(mesh);
            AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset"); return mesh;
        }

        public GameObject Object(Transform parent, string name, Mesh mesh, Material[] mats, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }
    }

    internal sealed class TWCenterModel
    {
        readonly Dictionary<string, SoftMesh> parts = new Dictionary<string, SoftMesh>();
        public bool HasGeometry { get { foreach (var part in parts.Values) if (part.Tris > 0) return true; return false; } }
        public SoftMesh Part(string material)
        {
            if (!parts.TryGetValue(material, out SoftMesh result)) { result = new SoftMesh(); parts.Add(material, result); }
            return result;
        }

        public GameObject Finish(TWCenterAssets assets, Transform parent, string name, Vector3 point, Quaternion rotation)
        {
            var combine = new List<CombineInstance>(); var mats = new List<Material>(); var temporary = new List<Mesh>();
            try
            {
                foreach (var pair in parts)
                {
                    if (pair.Value.Tris == 0) continue;
                    Mesh part = pair.Value.ToMesh(name + "_" + pair.Key); temporary.Add(part);
                    combine.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity }); mats.Add(assets.Material(pair.Key));
                }
                if (combine.Count == 0) throw new InvalidOperationException("Пустая модель " + name);
                int vertices = 0; foreach (var part in temporary) vertices += part.vertexCount;
                var mesh = new Mesh { name = name, indexFormat = vertices > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.CombineMeshes(combine.ToArray(), false, false); mesh.RecalculateBounds();
                var go = assets.Object(parent, name, assets.Save(mesh, name), mats.ToArray(), true);
                go.transform.SetPositionAndRotation(point, rotation); return go;
            }
            finally { foreach (var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}

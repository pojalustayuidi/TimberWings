using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal sealed class TWMysticAssets
    {
        public readonly string folder;
        public readonly Material scenery, lamp, veil;
        readonly Dictionary<string, Color> palette = new Dictionary<string, Color>();

        public TWMysticAssets(string folder)
        {
            this.folder = folder;
            Add("Wood", 0xA47A50); Add("DarkWood", 0x614939); Add("EndGrain", 0xB2A1B7); Add("Bark", 0x745640);
            Add("Stone", 0x87908B); Add("Leaf", 0x537D64); Add("LeafLight", 0x9BAC7B); Add("Teal", 0x729E9C);
            Add("Chips", 0xE2BC82); Add("CreamFlower", 0xFFF1CB); Add("PurpleFlower", 0xAB87CB);
            Add("Metal", 0x3E4B49); Add("Rope", 0xBFA474); Add("Cup", 0xE9D5A7); Add("Glass", 0xFFD586);
            Add("Canvas", 0xE7D1A2); Add("CanvasShade", 0xB9A784); Add("Skin", 0xD6A580); Add("Hair", 0x5A4438); Add("Boots", 0x4A4038); Add("GreenShirt", 0x648B59); Add("BlueShirt", 0x587D9F); Add("OchreShirt", 0xBBA05B); Add("Warning", 0xDC9A43); Add("Banner", 0x3F9388); Add("Copper", 0xCB9253); Add("Mushroom", 0xBD7969);
            Add("Rune",0xADDDE0); Add("Ghost",0xB7CDD7); Add("Ribbon",0xB5A3C7); Add("Moss",0x7B9563);
            var shader = Shader.Find("Project/CandyToon");
            if (shader == null) throw new InvalidOperationException("Нет родного шейдера Project/CandyToon.");
            scenery = new Material(shader) { name = "Mystic_Candy_Palette" };
            scenery.SetColor("_BaseColor", Color.white); scenery.SetFloat("_VertexColorStrength", 1f);
            scenery.SetFloat("_PosVary", 0.035f); scenery.SetFloat("_GlossStrength", 0.025f);
            scenery.SetFloat("_RimStrength", 0.06f);
            lamp = new Material(scenery) { name = "Mystic_Lantern_Glass" };
            lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", Color.black);
            lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(scenery, folder + "/Scenery.mat"); AssetDatabase.CreateAsset(lamp, folder + "/Lantern.mat");
            var glowShader=Shader.Find("Project/IslandV2/TW_MysticVeil");
            if(glowShader==null)throw new InvalidOperationException("Нет TW_MysticVeil.shader. Скопируй всю папку Assets из архива.");
            veil=new Material(glowShader){name="Mystic_Veil"};veil.SetColor("_Color",Color.white);veil.SetFloat("_Alpha",0.6f);veil.SetFloat("_Intensity",1.2f);
            AssetDatabase.CreateAsset(veil,folder+"/Veil.mat");
        }

        void Add(string key, int hex) { palette.Add(key, SoftMesh.C(hex)); }
        public Color Colour(string key) { return palette[key]; }

        public Mesh Save(Mesh mesh, string name)
        {
            TWMysticValidation.Mesh(mesh); AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset"); return mesh;
        }

        public GameObject Object(Transform parent, string name, Mesh mesh, Material[] materials, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; renderer.receiveShadows = true;
            return go;
        }
    }

    internal sealed class TWMysticModel
    {
        readonly Dictionary<string, SoftMesh> parts = new Dictionary<string, SoftMesh>();
        public SoftMesh Part(string key)
        {
            if (!parts.TryGetValue(key, out SoftMesh part)) { part = new SoftMesh(); parts.Add(key, part); }
            return part;
        }

        public GameObject Finish(TWMysticAssets assets, Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var combine = new List<CombineInstance>(); var temporary = new List<Mesh>();
            try
            {
                bool emissive = parts.ContainsKey("Glass");
                if (emissive && parts.Count != 1) throw new InvalidOperationException("Стекло фонаря должно быть отдельным мешем.");
                foreach (var pair in parts)
                {
                    if (pair.Value.Tris == 0) continue;
                    Mesh part = pair.Value.ToMesh(name + "_" + pair.Key); temporary.Add(part);
                    Color tint = assets.Colour(pair.Key); Color[] colours = part.colors;
                    for (int i = 0; i < colours.Length; i++) colours[i] *= tint;
                    part.colors = colours;
                    combine.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                }
                if (combine.Count == 0) throw new InvalidOperationException("Пустая модель: " + name);
                var mesh = new Mesh { name = name, indexFormat = temporary.Sum(m => m.vertexCount) > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                // One draw call per movable prop; palette is baked into vertex colours.
                mesh.CombineMeshes(combine.ToArray(), true, false); mesh.RecalculateBounds();
                var go = assets.Object(parent, name, assets.Save(mesh, name), new[] { emissive ? assets.lamp : assets.scenery }, true);
                go.transform.SetPositionAndRotation(position, rotation); return go;
            }
            finally { foreach (Mesh mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}


using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWMysticClearingSurface
    {
        const string BaseMap = "_BaseMap";

        internal static void ApplyMaterial(Renderer renderer, Material material)
        {
            if (renderer.sharedMaterial == material) return;
            Undo.RecordObject(renderer, TWMysticClearingPolish.UndoName);
            renderer.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        internal static void SaveNew(Object asset, string folder, string name, List<string> created)
        {
            IslandLayout.EnsureFolder(folder);
            string path = folder + "/" + name;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Путь ресурса занят: " + path);
            AssetDatabase.CreateAsset(asset, path);
            if (!AssetDatabase.Contains(asset))
                throw new InvalidOperationException("Не удалось сохранить ресурс " + path);
            created.Add(path);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        internal static void PolishSlab(MeshRenderer renderer, string folder, List<string> created)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Witness_Slab.mat");
            if (material != null)
            {
                ApplyMaterial(renderer, material);
                return;
            }

            var original = renderer.sharedMaterial;
            if (original == null || !original.HasProperty(BaseMap) || original.GetTexture(BaseMap) == null)
            {
                Debug.LogWarning("Круг молчания: у плиты нет исходной карты надписи.", renderer);
                return;
            }

            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Witness_Engraving_Contrast.asset");
            if (image == null)
            {
                image = ContrastPass(original.GetTexture(BaseMap));
                SaveNew(image, folder, "Witness_Engraving_Contrast.asset", created);
            }
            material = new Material(original) { name = "Witness Slab – original carving" };
            material.SetTexture(BaseMap, image);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", Mathf.Min(.13f, material.GetFloat("_Smoothness")));
            SaveNew(material, folder, "Witness_Slab.mat", created);
            ApplyMaterial(renderer, material);
        }

        static Texture2D ContrastPass(Texture source)
        {
            float scale = Mathf.Min(1f, 1536f / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var image = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "Witness – original inscription and bridge",
                wrapMode = source.wrapMode,
                filterMode = FilterMode.Trilinear,
                anisoLevel = Mathf.Max(2, source.anisoLevel)
            };
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }

            var pixels = image.GetPixels32();
            var output = (Color32[])pixels.Clone();
            for (int y = 2; y < height - 2; y++)
            for (int x = 2; x < width - 2; x++)
            {
                int i = y * width + x;
                Color32 color = pixels[i];
                float around = (Luma(pixels[i - 2]) + Luma(pixels[i + 2]) +
                    Luma(pixels[i - 2 * width]) + Luma(pixels[i + 2 * width])) * .25f;
                float deviation = Luma(color) - around;
                // Deepen engraved edges while leaving the authored shapes and palette legible.
                float effect = Mathf.Abs(deviation) < 3.5f ? 0f : deviation * (deviation < 0f ? .54f : .26f);
                int delta = Mathf.RoundToInt(Mathf.Clamp(effect, -27f, 16f));
                output[i] = new Color32(Shift(color.r, delta), Shift(color.g, delta), Shift(color.b, delta), color.a);
            }
            image.SetPixels32(output);
            image.Apply(true, false);
            return image;
        }

        static float Luma(Color32 color) => .2126f * color.r + .7152f * color.g + .0722f * color.b;
        static byte Shift(byte component, int delta) => (byte)Mathf.Clamp(component + delta, 0, 255);
    }
}

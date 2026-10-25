// Assets/_Project/Editor/IslandV2/IslandBlockout.GroundTex.cs
// Остров v2, этап 2 (задача G): запекание цвета земли в текстуру. Часть partial-класса IslandBlockout.
//
// Зачем: меш земли идёт клеткой 2 м (бюджет Tris), цвет вершин на такой сетке размывает тропу шириной 3 м и каёмку 1 м.
// Решение: тот же цвет считается не по вершинам, а по пикселям текстуры (0.4-0.5 м на пиксель), шейдер читает её по мировым XZ. Треугольников не прибавилось.
//
// Что делает BakeGroundTexture(field, mat):
//   1. считает рамку текстуры (контур + groundTexMargin), размер пикселя = max(texel, размер / groundTexMax)
//   2. заполняет пиксели цветом: Candy-земля (IslandBlockout.CandyGround.cs, настройки IslandCandy.asset, по умолчанию ВКЛ)
//      или старая раскраска GroundColorSrgb, если в IslandCandy снята галка candyOn
//   3. сохраняет PNG Assets/_Project/IslandV2/Generated/IslandV2_GroundColor.png (sRGB, mip-уровни, без сжатия, Clamp)
//   4. Candy: считает уклон «подушки» тропы по сетке и сохраняет маску IslandV2_GroundMask.png (линейная, RGBA: уклон X, уклон Z, посыпка, глянец)
//   5. кладёт текстуры, рамку и флаг _CandyOn в материал (_GroundTex, _GroundMask, _GroundRect, _CandyOn)
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string GroundTexPath = IslandLayout.AssetFolder + "/Generated/IslandV2_GroundColor.png";
        public const string GroundMaskPath = IslandLayout.AssetFolder + "/Generated/IslandV2_GroundMask.png";

        static bool BakeGroundTexture(IslandField f, Material mat)
        {
            var L = f.L;
            // старый ассет раскладки (без PAD_* меток, троп и реки) даёт землю без троп: говорим об этом сразу
            if (L.paths.Count == 0 || L.riverPoints.Count < 2 || L.FindAnchor("PAD_Hut") == null)
                Debug.LogWarning("IslandV2: в раскладке троп " + L.paths.Count + ", точек реки " + L.riverPoints.Count + ", метка PAD_Hut " + (L.FindAnchor("PAD_Hut") != null ? "есть" : "НЕТ")
                    + ". Похоже на ассет от старой версии: выбери " + IslandLayout.AssetPath + " и нажми внизу «Сбросить раскладку к значениям по умолчанию», потом пункт 121.");

            var candy = IslandCandy.GetOrCreate();
            bool useCandy = candy != null && candy.candyOn && mat.HasProperty("_GroundMask") && mat.HasProperty("_CandyOn");
            if (candy != null && candy.candyOn && !useCandy)
                Debug.LogWarning("IslandV2: у материала «" + mat.name + "» нет свойств Candy (_GroundMask, _CandyOn). Положи новую версию шейдера IslandVertexColor.shader, дождись компиляции и повтори. Пока красим по-старому.");

            float margin = Mathf.Max(0f, L.groundTexMargin);
            float x0 = f.Min.x - margin, z0 = f.Min.y - margin;
            float sizeX = (f.Max.x - f.Min.x) + margin * 2f, sizeZ = (f.Max.y - f.Min.y) + margin * 2f;
            int maxPx = Mathf.Clamp(L.groundTexMax, 64, 2048);
            float wantTexel = useCandy ? candy.texelSize : L.groundTexelSize;
            float texel = Mathf.Max(Mathf.Max(0.1f, wantTexel), Mathf.Max(sizeX, sizeZ) / maxPx);
            int w = Mathf.Clamp(Mathf.CeilToInt(sizeX / texel), 8, 2048);
            int h = Mathf.Clamp(Mathf.CeilToInt(sizeZ / texel), 8, 2048);

            var pixels = new Color32[w * h];
            float[] height = useCandy ? new float[w * h] : null;
            byte[] gloss = useCandy ? new byte[w * h] : null;
            byte[] sprinkle = useCandy ? new byte[w * h] : null;
            try
            {
                for (int j = 0; j < h; j++)
                {
                    if ((j & 15) == 0 && EditorUtility.DisplayCancelableProgressBar("Остров v2: цвет земли" + (useCandy ? " (Candy)" : ""), "Запекание текстуры " + w + "x" + h, (float)j / h))
                    {
                        Debug.LogWarning("IslandV2: запекание цвета земли отменено. Старые PNG и материал не перезаписывались.");
                        return false;
                    }
                    float z = z0 + (j + 0.5f) * texel;
                    for (int i = 0; i < w; i++)
                    {
                        float x = x0 + (i + 0.5f) * texel;
                        int k = j * w + i;
                        if (useCandy)
                        {
                            var p = CandyPixel(f, candy, x, z);
                            pixels[k] = (Color32)p.color;
                            height[k] = p.height;
                            gloss[k] = (byte)Mathf.Clamp(Mathf.RoundToInt(p.gloss * 255f), 0, 255);
                            sprinkle[k] = (byte)Mathf.Clamp(Mathf.RoundToInt(p.sprinkle * 255f), 0, 255);
                        }
                        else pixels[k] = (Color32)GroundColorSrgb(f, x, z);
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            IslandLayout.EnsureFolder(IslandLayout.AssetFolder + "/Generated");
            SaveGroundPng(pixels, w, h, GroundTexPath, true);

            if (useCandy)
            {
                // уклон «подушки» по сетке: центральные разности, h в 0..1 на метр. Кодируем -2..+2 в 0..1 (0.5 = ровно)
                var mask = new Color32[w * h];
                for (int j = 0; j < h; j++)
                {
                    int jm = Mathf.Max(j - 1, 0), jp = Mathf.Min(j + 1, h - 1);
                    for (int i = 0; i < w; i++)
                    {
                        int im = Mathf.Max(i - 1, 0), ip = Mathf.Min(i + 1, w - 1);
                        float gx = (height[j * w + ip] - height[j * w + im]) / ((ip - im) * texel);
                        float gz = (height[jp * w + i] - height[jm * w + i]) / ((jp - jm) * texel);
                        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((0.5f + gx / 4f) * 255f), 0, 255);
                        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((0.5f + gz / 4f) * 255f), 0, 255);
                        int k = j * w + i;
                        mask[k] = new Color32(r, g, sprinkle[k], gloss[k]);
                    }
                }
                SaveGroundPng(mask, w, h, GroundMaskPath, false);
            }

            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexPath);
            mat.SetTexture("_GroundTex", asset);
            mat.SetVector("_GroundRect", new Vector4(x0, z0, w * texel, h * texel));
            if (mat.HasProperty("_CandyOn")) mat.SetFloat("_CandyOn", useCandy ? 1f : 0f);
            if (useCandy) mat.SetTexture("_GroundMask", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundMaskPath));
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("IslandV2: текстура цвета земли " + w + "x" + h + ", пиксель " + texel.ToString("0.00") + " м (" + GroundTexPath + ")"
                + (useCandy ? ", маска Candy (" + GroundMaskPath + ")." : ", старая раскраска."));
            return true;
        }

        // PNG в проект и настройка импорта. srgb = true для цвета; для маски false: значения читаются как есть (иначе уклон и глянец исказятся гаммой)
        static void SaveGroundPng(Color32[] px, int w, int h, string path, bool srgb)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = srgb;
                ti.alphaSource = srgb ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.anisoLevel = 4;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }
    }
}

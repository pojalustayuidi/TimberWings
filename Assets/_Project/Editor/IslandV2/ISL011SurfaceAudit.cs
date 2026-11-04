#if UNITY_EDITOR
// Timber Wings / ISL-011: read-only inventory of the actual Island V2 surface assets.
// Does not create/update any Unity asset, mesh, shader, material, or scene object.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ISL011SurfaceAudit
    {
        const string Menu = "Tools/BuilderSim/Локация/Остров v2/ISL-011: отчёт по материалам поверхности";
        const string OutputName = "ISL-011_SurfaceAudit.txt";

        [MenuItem(Menu, false, 190)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("ISL-011: останови Play Mode, затем создай отчёт по материалам.");
                return;
            }

            var sb = new StringBuilder(16384);
            sb.AppendLine("TIMBER WINGS / ISL-011 / SURFACE MATERIAL AUDIT");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            sb.AppendLine("Only existing assets are inspected. Missing files are NOT created.");
            sb.AppendLine();

            var candy = AssetDatabase.LoadAssetAtPath<IslandCandy>(IslandCandy.AssetPath);
            sb.AppendLine("[1] CANDY GROUND SETTINGS");
            sb.AppendLine("Asset: " + IslandCandy.AssetPath);
            if (candy == null) sb.AppendLine("MISSING: actual IslandCandy.asset was not found");
            else
            {
                sb.AppendLine("Candy enabled: " + candy.candyOn + "; texel m: " + F(candy.texelSize));
                sb.AppendLine("Grass Mint/Green/Lime/Sun: " + C(candy.grassMint) + " / " + C(candy.grassGreen) + " / " + C(candy.grassLime) + " / " + C(candy.grassSun));
                sb.AppendLine("Forest floor: " + C(candy.forestFloor) + "; strength: " + F(candy.forestStrength));
                sb.AppendLine("Path cream/edge: " + C(candy.pathCream) + " / " + C(candy.pathEdge));
                sb.AppendLine("Beach/Pad: " + C(candy.beachColor) + " / " + C(candy.padColor));
                sb.AppendLine("Plaza stone/rim: " + C(candy.plazaStone) + " / " + C(candy.plazaRim));
                sb.AppendLine("Gloss mask grass/path: " + F(candy.glossGrass) + " / " + F(candy.glossPath));
            }
            sb.AppendLine();

            var layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            sb.AppendLine("[2] LAYOUT COLOURS (DO NOT RESET OR REBUILD LAYOUT)");
            sb.AppendLine("Asset: " + IslandLayout.AssetPath);
            if (layout == null) sb.AppendLine("MISSING: actual IslandLayout.asset was not found");
            else
            {
                sb.AppendLine("Rock top/mid/dark: " + C(layout.rockTop) + " / " + C(layout.rockMid) + " / " + C(layout.rockDark));
                sb.AppendLine("River wall/damp: " + C(layout.riverWallColor) + " / " + C(layout.riverWallWet));
                sb.AppendLine("Cliff wet/lip: " + C(layout.cliffWet) + " / " + C(layout.lipTop));
                sb.AppendLine("Plaza stone/rim (layout): " + C(layout.plazaStone) + " / " + C(layout.plazaRim));
            }
            sb.AppendLine();

            sb.AppendLine("[3] SHARED MATERIALS, CURRENT STORED VALUES");
            MaterialInfo(sb, "Island ground + plaza", IslandBlockout.MaterialPath);
            MaterialInfo(sb, "Island cliff", IslandCliff.MaterialPath);
            var forest = ForestMats.MatDir + "/";
            MaterialInfo(sb, "Forest rock", forest + "Under_Rock.mat");
            MaterialInfo(sb, "Grove moss stone", forest + "Grove_StoneMoss.mat");
            MaterialInfo(sb, "Grandpa stone", forest + "Grandpa_Stone.mat");
            MaterialInfo(sb, "Sacred stone", forest + "Sacred_Stone.mat");
            sb.AppendLine();

            sb.AppendLine("[4] SCENE MATERIAL USAGE (open scene only)");
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var usage = new Dictionary<Material, int>();
            int counted = 0;
            foreach (var r in renderers)
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                counted++;
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (usage.ContainsKey(mat)) usage[mat]++;
                    else usage.Add(mat, 1);
                }
            }
            sb.AppendLine("Scene renderers (including disabled): " + counted);
            sb.AppendLine("Unique referenced materials: " + usage.Count);
            foreach (var item in usage.OrderByDescending(x => x.Value).ThenBy(x => x.Key.name).Take(70))
            {
                var m = item.Key;
                string shader = m.shader == null ? "MISSING SHADER" : m.shader.name;
                sb.AppendLine("  slots=" + item.Value.ToString().PadLeft(4) + " | " + m.name + " | " + shader + " | " + AssetDatabase.GetAssetPath(m));
            }
            if (usage.Count > 70) sb.AppendLine("  ... remaining " + (usage.Count - 70) + " materials omitted");
            sb.AppendLine();
            sb.AppendLine("[5] NOTES FOR ISL-011");
            sb.AppendLine("_Gloss/_GlossPower (IslandVertexColor) and _RimStrength (TreeToon) are different lighting models.");
            sb.AppendLine("Island plaza mesh uses vertex colours on the SAME shared ground material; its palette is not a separate .mat.");
            sb.AppendLine("Changing the IslandCandy palette requires Candy-ground repaint; changing materials does not rebuild geometry.");
            sb.AppendLine("Do NOT run ForestMats.Get(..., force:true) while inspecting authored materials.");
            sb.AppendLine("The report is read-only; asset or scene changes performed: NONE.");

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(root, OutputName);
            try
            {
                File.WriteAllText(output, sb.ToString(), new UTF8Encoding(false));
                Debug.Log("ISL-011: отчёт готов: " + output + "\n" +
                          "Это только аудит; Unity-ассеты, сцена и генераторы не изменены.");
                EditorUtility.RevealInFinder(output);
            }
            catch (Exception e)
            {
                Debug.LogError("ISL-011: не удалось записать отчёт: " + e.Message);
            }
        }

        static void MaterialInfo(StringBuilder sb, string label, string path)
        {
            sb.AppendLine("-- " + label + " : " + path);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { sb.AppendLine("   MISSING"); return; }
            sb.AppendLine("   Shader: " + (m.shader == null ? "MISSING" : m.shader.name)
                + "; instancing=" + m.enableInstancing + "; renderQueue=" + m.renderQueue);
            string[] colors = { "_Tint", "_BaseColor", "_BottomColor", "_Shade", "_ShadowTint", "_GroundTint" };
            foreach (string p in colors)
                if (m.HasProperty(p)) sb.AppendLine("   " + p + "=" + C(m.GetColor(p)));
            string[] floats = {
                "_Bright", "_AmbientMul", "_Wrap", "_Toon", "_Gloss", "_GlossPower", "_Sparkle", "_Sprinkles", "_Airy",
                "_Rim", "_RimStrength", "_StepSoft", "_Level0", "_Level1", "_Level2", "_Ambient", "_ShadowStrength",
                "_Metallic", "_Smoothness", "_Glossiness", "_CandyOn", "_UseCandyGround", "_AllGround"
            };
            foreach (string p in floats)
                if (m.HasProperty(p)) sb.AppendLine("   " + p + "=" + F(m.GetFloat(p)));
            string[] textures = { "_GroundTex", "_GroundMask" };
            foreach (string p in textures)
            {
                if (!m.HasProperty(p)) continue;
                var tex = m.GetTexture(p);
                string texPath = tex != null ? AssetDatabase.GetAssetPath(tex) : "(not assigned)";
                sb.Append("   " + p + "=" + texPath);
                var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null) sb.Append(" [sRGB=" + ti.sRGBTexture + "; mipmaps=" + ti.mipmapEnabled + "; compression=" + ti.textureCompression + "]");
                sb.AppendLine();
            }
        }

        static string C(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c) + " (" + F(c.r) + ", " + F(c.g) + ", " + F(c.b) + ")";
        }
        static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
#endif

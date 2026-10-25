#if UNITY_EDITOR
// Assets/_Project/Editor/Terrain/LocationBuilderTester.CandyGround.cs
// partial-часть LocationBuilderTester: материал земли (меш Surface) на шейдере Project/CandyGround Mesh без текстур.
// Вызывается из сборки поверхности; null = шейдера нет или он не собрался, тогда остаётся картонный материал как раньше.
using UnityEditor;
using UnityEngine;

public partial class LocationBuilderTester
{
    static Material MakeCandyGroundMat(Cfg c, Texture2D ctrl0, Texture2D ctrl1)
    {
        var sh = Shader.Find("Project/CandyGroundMesh");
        if (sh == null || !sh.isSupported)
        {
            Debug.LogWarning("[LocationTester] Project/CandyGroundMesh не найден или не скомпилировался (Assets/_Project/Shaders/Candy/CandyGroundMesh.shader). Земля осталась на картонном шейдере.");
            return null;
        }
        var m = new Material(sh) { name = "Candy_Ground" };
        // те же параметры вида, что у картонного материала
        m.SetColor("_Shade", c.shadowTint);
        m.SetFloat("_Sharp", c.sharpEdges);
        m.SetFloat("_Wrap", c.toonWrap);
        m.SetFloat("_Rim", c.rimLight);
        m.SetFloat("_ContourStep", c.contourStep);
        m.SetFloat("_ContourAmt", c.contourAmt);
        m.SetFloat("_ContourBase", c.origin.y - 0.5f * c.contourStep);
        m.SetFloat("_Bright", c.brightness);
        m.SetFloat("_Sat", c.saturation);
        if (ctrl0 != null) m.SetTexture("_Ctrl0", ctrl0);
        if (ctrl1 != null) m.SetTexture("_Ctrl1", ctrl1);
        SaveAsset(m, AssetDir + "/Materials/Candy_Ground.mat");
        return m;
    }
}
#endif

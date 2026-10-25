// Assets/_Project/Scripts/Editor/Buildings/ShopBuilderV2.Rainbow.cs   (ТОЛЬКО в папке Editor!)
// ShopBuilderV2: радуга для праздников лавки (новый ярус витрины, крупная продажа, серия продаж).
// Семь цветных дуг (один меш дуги на все, семь материалов Rb0..Rb6) и пушистые облачка на концах.
// Группа «Rainbow» лежит внутри Stage3 и ВЫКЛЮЧЕНА: включает, увеличивает и убирает её ShopLife.
// Радуга стоит позади лавки и выше конька, дугой через всю лужайку. Размеры правятся в блоке «Размеры радуги».
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class ShopBuilderV2
{
    // ---------------------------------------------------------------- Размеры радуги (правь тут)
    const float RbOuter = 4.3f;     // внешний радиус верхней (красной) полосы, м
    const float RbBand = 0.17f;     // ширина одной полосы, м
    const float RbThick = 0.22f;    // толщина дуги (по Z), м
    const int RbSegments = 28;      // сегментов на полукруг

    static readonly string[] RainbowMats = { "Rb0", "Rb1", "Rb2", "Rb3", "Rb4", "Rb5", "Rb6" };

    void BuildRainbow(Transform p)
    {
        var rb = Obj(p, "Rainbow", new Vector3(0f, PlatH, -D / 2f - 0.9f)).transform;

        for (int i = 0; i < RainbowMats.Length; i++)
        {
            float ro = RbOuter - i * RbBand;
            float ri = ro - RbBand * 1.02f;      // нахлёст 2% закрывает стык (раньше был зазор, через него мигало небо)
            // каждая следующая полоса чуть тоньше по Z (6 мм): передние грани в нахлёсте лежат в разных плоскостях, z-файтинга нет
            float bt = RbThick - i * 0.006f;
            NoShadow(Part(rb, "Band" + i, ArcMesh(ri, ro, bt), Mat(RainbowMats[i]), Vector3.zero, Vector3.one, Quaternion.identity));
        }

        // облачка на концах: по центру полосы, чтобы дуга «вырастала» из ваты
        float footX = RbOuter - RbBand * RainbowMats.Length * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * footX;
            NoShadow(Blob(rb, "FootCloudA", new Vector3(x, 0.12f, 0f), new Vector3(1.05f, 0.62f, 0.8f), "Cloud", 1));
            NoShadow(Blob(rb, "FootCloudB", new Vector3(x - side * 0.5f, 0.07f, 0.05f), new Vector3(0.75f, 0.46f, 0.6f), "Cloud", 2));
            NoShadow(Blob(rb, "FootCloudC", new Vector3(x + side * 0.45f, 0.1f, -0.05f), new Vector3(0.7f, 0.5f, 0.6f), "Cloud", 3));
            var foot = Obj(rb, side < 0 ? "FootL" : "FootR", new Vector3(x, 0.45f, 0f)).transform;
            if (side < 0) L.rainbowFootL = foot; else L.rainbowFootR = foot;
        }

        rb.gameObject.SetActive(false);
        L.rainbow = rb;
    }

    // ---------------------------------------------------------------- меш дуги
    // Полукольцо в плоскости XY (от угла 0 до π), толщина по Z. Четыре грани со своими вершинами:
    // передняя и задняя плоские, внешняя и внутренняя гладкие вдоль дуги. Торцы не нужны: они прячутся в облачках.
    // Намотка граней: у Unity нормаль треугольника (a, b, c) = cross(b − a, c − a), лицевая сторона смотрит по нормали.
    static Mesh ArcMesh(float rIn, float rOut, float thick)
    {
        string name = "ShopV2_Arc_" + Mathf.RoundToInt(rIn * 100f) + "_" + Mathf.RoundToInt(rOut * 100f) + "_" + Mathf.RoundToInt(thick * 100f) + "_s" + RbSegments;
        HutMeshLib.EnsureFolders();
        string path = HutMeshLib.MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m != null) return m;

        float h = thick * 0.5f;
        var v = new List<Vector3>();
        var t = new List<int>();

        // лицевая (+Z): внутренняя кромка -> внешняя
        Strip(v, t, th => Arc(rIn, th, h), th => Arc(rOut, th, h), false);
        // задняя (−Z): те же точки, обратная намотка
        Strip(v, t, th => Arc(rIn, th, -h), th => Arc(rOut, th, -h), true);
        // внешняя поверхность (нормаль наружу): от задней кромки к передней, обратная намотка
        Strip(v, t, th => Arc(rOut, th, -h), th => Arc(rOut, th, h), true);
        // внутренняя поверхность (нормаль внутрь): от задней кромки к передней
        Strip(v, t, th => Arc(rIn, th, -h), th => Arc(rIn, th, h), false);

        m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Vector3 Arc(float r, float theta, float z)
    {
        return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
    }

    // полоса из квадов вдоль дуги между кривыми a(θ) и b(θ); flip меняет сторону лицевой грани
    static void Strip(List<Vector3> v, List<int> t, System.Func<float, Vector3> a, System.Func<float, Vector3> b, bool flip)
    {
        int b0 = v.Count;
        for (int i = 0; i <= RbSegments; i++)
        {
            float th = Mathf.PI * i / RbSegments;
            v.Add(a(th));
            v.Add(b(th));
        }
        for (int i = 0; i < RbSegments; i++)
        {
            int k = b0 + i * 2;       // k = a_i, k+1 = b_i, k+2 = a_(i+1), k+3 = b_(i+1)
            if (!flip) { t.Add(k); t.Add(k + 1); t.Add(k + 3); t.Add(k); t.Add(k + 3); t.Add(k + 2); }
            else       { t.Add(k); t.Add(k + 3); t.Add(k + 1); t.Add(k); t.Add(k + 2); t.Add(k + 3); }
        }
    }
}
#endif

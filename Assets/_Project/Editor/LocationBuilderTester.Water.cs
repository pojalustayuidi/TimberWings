#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilderTester.Water.cs
// partial-часть LocationBuilderTester: вершинная маска пены реки (для тира Low шейдера Project/CandyWater) и брызги у водопадов (до 40 частиц).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class LocationBuilderTester
{
    // все препятствия в воде: валуны + островки (тот же список, что запекает BakeFlowMap)
    static List<RiverObst> AllRiverObst(Layout L)
    {
        var list = new List<RiverObst>(L.obst);
        for (int k = 0; k < L.islets.Count; k++)
        {
            var isl = L.islets[k];
            list.Add(new RiverObst { pos = isl.pos, flow = isl.dir, r = isl.r, k = 1f, il = isl });
        }
        return list;
    }

    // маска пены в точке q (u) для vertex color реки: R = кольцо у кромки камня, G = V-след по течению, B = ускорение рядом.
    // Формулы те же, что в BakeFlowMap, поэтому на Low пена стоит там же, где на Mid она читается из карты следов.
    static Color32 RiverFoamVertex(List<RiverObst> list, Vector2 q)
    {
        float dist = 1f, wake = 0f, acc = 0f;
        for (int n = 0; n < list.Count; n++)
        {
            var o = list[n];
            bool isl = o.il != null;
            float a0 = (isl ? 1.2f : 0.35f) * o.r;
            float w0 = (isl ? 0.75f : 0.55f) * o.r;
            float wl = 3.2f + o.r * 3.2f;
            float reach = a0 + wl + o.r + 2f;
            Vector2 rel = q - o.pos;
            if (rel.sqrMagnitude > reach * reach) continue;
            float d = isl ? IsletD(o.il, q, 0.5f) - o.il.r : rel.magnitude - o.r;
            float de = Mathf.Max(0f, d + (1f - o.k) * 0.3f);
            dist = Mathf.Min(dist, Mathf.Clamp01(de / 1.2f));
            acc = Mathf.Max(acc, (1f - SS(0f, 2.4f, Mathf.Max(0f, d))) * o.k);
            float along = Vector2.Dot(rel, o.flow) - a0;
            if (along > 0f && along < wl)
            {
                Vector2 perp = new Vector2(-o.flow.y, o.flow.x);
                float lat = Mathf.Abs(Vector2.Dot(rel, perp));
                float half = w0 + 0.3f * along;
                float wd = 0.13f + 0.03f * along;
                float tt = (lat - half) / wd;
                float fade = SS(0f, 0.7f, along) * (1f - SS(wl * 0.35f, wl, along));
                wake = Mathf.Max(wake, Mathf.Exp(-tt * tt) * fade * o.k);
            }
        }
        float ring = 1f - SS(0.16f, 0.30f, dist * 1.2f);   // толщина кольца как у шейдера Mid при _WakeAmt = 1
        return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(ring) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(wake) * 255f),
                           (byte)Mathf.RoundToInt(Mathf.Clamp01(acc) * 255f), 255);
    }

    static Material MakeSprayMat()
    {
        var sh = Shader.Find("Project/CandyParticle");
        if (sh == null || !sh.isSupported) return null;
        var m = new Material(sh) { name = "Candy_WaterSpray" };
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
        SaveAsset(m, AssetDir + "/Materials/Candy_WaterSpray.mat");
        return m;
    }

    // брызги: у подножия водопада с края острова (вес 2) и горного водопада (вес 1). Всего не больше 40 частиц на весь остров,
    // бюджет делит CandyWaterFx по тиру качества. Нет шейдера частиц = брызг нет, остальное строится как раньше.
    static void BuildWaterFx(Layout L, Transform parent)
    {
        var rv = L.river; var c = L.c; float ws = c.worldScale;
        if (rv == null) return;
        var mat = MakeSprayMat();
        if (mat == null)
        {
            Debug.LogWarning("[LocationTester] Project/CandyParticle не найден или не скомпилировался (Assets/_Project/Shaders/Candy/CandyParticle.shader). Брызг не будет.");
            return;
        }
        var root = new GameObject("WaterFx");
        root.transform.SetParent(parent, false);

        float y0 = rv.WaterY(rv.s[rv.p.Count - 2]);
        Vector2 e = rv.lip + rv.lipDir * 2.5f;
        CandyWaterFx.Create(root.transform, "Spray_Edge", new Vector3(e.x * ws, (y0 - c.fallLength + 0.3f) * ws, e.y * ws), ws, 2.4f, 2, mat);

        if (L.mtn != null)
        {
            float wy = rv.WaterY(0f);
            CandyWaterFx.Create(root.transform, "Spray_Mountain", new Vector3(L.foamC.x * ws, (wy + 0.3f) * ws, L.foamC.y * ws), ws, Mathf.Max(1.2f, c.sourceFoamR * 0.5f), 1, mat);
        }
    }
}
#endif

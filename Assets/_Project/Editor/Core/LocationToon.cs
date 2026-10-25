#if UNITY_EDITOR
// =====================================================================================
//  LocationToon.cs  ->  Assets/_Project/Editor/Island/Core/LocationToon.cs
//  Мультяшные утилиты для LocationForest и LocationMeadow:
//    LocMulti  - меш из нескольких материалов (слоты), плоские грани
//    ToonKit   - набор примитивов, контур (inverted hull), грибы, светящийся материал, частицы
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LocMulti
{
    readonly SortedDictionary<int, LocMB> parts = new SortedDictionary<int, LocMB>();
    public readonly List<int> slots = new List<int>();

    LocMB P(int slot)
    {
        LocMB mb;
        if (!parts.TryGetValue(slot, out mb)) { mb = new LocMB(); parts[slot] = mb; }
        return mb;
    }

    // запекает копию меша (порядок вершин сохраняется, поэтому масштаб должен быть положительным)
    public void Add(int slot, Mesh src, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var mb = P(slot);
        var M = Matrix4x4.TRS(pos, rot, scale);
        var vs = src.vertices;
        var ts = src.triangles;
        for (int i = 0; i < ts.Length; i++)
        {
            mb.t.Add(mb.v.Count);
            mb.v.Add(M.MultiplyPoint3x4(vs[ts[i]]));
        }
    }

    public void Tube(int slot, Vector3 p0, Vector3 p1, float r0, float r1, int n)
    {
        LocKit.AddTube(P(slot), p0, p1, r0, r1, n);
    }

    // submesh i соответствует slots[i]
    public Mesh Build(string name)
    {
        var ci = new List<CombineInstance>();
        var temps = new List<Mesh>();
        slots.Clear();
        foreach (var kv in parts)
        {
            if (kv.Value.v.Count == 0) continue;
            var m = kv.Value.ToMesh(name + "_s" + kv.Key);
            temps.Add(m);
            slots.Add(kv.Key);
            ci.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
        }
        // CombineMeshes с пустым массивом падает с ошибкой
        if (ci.Count == 0) return new Mesh { name = name };

        var res = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        res.CombineMeshes(ci.ToArray(), false, false);
        res.RecalculateBounds();
        foreach (var t in temps) UnityEngine.Object.DestroyImmediate(t);
        return res;
    }
}

public sealed class ToonKit
{
    public readonly Mesh[] blob = new Mesh[4];     // «пухлый» шарик диаметром ~1
    public readonly Mesh cyl, cyl12, cone, box, octa, stem;

    const string GlowSrc = @"Shader `Location/Glow`
{
    Properties { _Color(`Color`, Color) = (1,1,1,1) }
    SubShader
    {
        Tags { `RenderType`=`Transparent` `Queue`=`Transparent` `RenderPipeline`=`UniversalPipeline` }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            struct A { float4 pos : POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 col : COLOR; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.col = i.col; o.uv = i.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a *= a;
                half4 c = _Color * i.col;
                return half4(c.rgb * a * 2.0, c.a * a);
            }
            ENDHLSL
        }
    }
}";

    public ToonKit()
    {
        for (int i = 0; i < blob.Length; i++) blob[i] = LocKit.Ico("ToonBlob" + i, i + 1, BlobDeform);
        cyl = LocKit.Prism("ToonCyl8", 8, 0.5f, 0.5f);
        cyl12 = LocKit.Prism("ToonCyl12", 12, 0.5f, 0.5f);
        cone = LocKit.Prism("ToonCone8", 8, 0.5f, 0f);
        stem = LocKit.Prism("ToonStem6", 6, 0.5f, 0.38f);
        box = LocKit.Box();
        octa = LocKit.Octa();
    }

    static Vector3 BlobDeform(Vector3 p, int seed)
    {
        float n = Mathf.PerlinNoise(p.x * 2f + seed * 3.1f + 7f, p.y * 2f + p.z * 2f + 7f);
        return p * (0.5f * (0.9f + 0.2f * n));
    }

    // ------------------------------------------------------------ контур (inverted hull)
    // Копия меша, раздутая по сглаженным нормалям; рисуется с Cull Front тёмным цветом.
    public static Mesh Outline(Mesh src, float width, string name)
    {
        var vs = src.vertices;
        var ns = src.normals;
        var acc = new Dictionary<Vector3Int, Vector3>();
        var keys = new Vector3Int[vs.Length];
        for (int i = 0; i < vs.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(vs[i].x * 400f), Mathf.RoundToInt(vs[i].y * 400f), Mathf.RoundToInt(vs[i].z * 400f));
            keys[i] = k;
            Vector3 s;
            acc[k] = acc.TryGetValue(k, out s) ? s + ns[i] : ns[i];
        }
        var nv = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++) nv[i] = vs[i] + acc[keys[i]].normalized * width;
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.vertices = nv;
        m.triangles = src.triangles;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------ свечение и частицы
    public static Material GlowMat(string name, Color col)
    {
        Material cached = LocKit.Cached(name);
        if (cached != null) return cached;

        Shader sh = LocKit.EnsureShader(LocKit.ShaderDir + "/LocGlow.shader", GlowSrc);
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        m.renderQueue = (int)RenderQueue.Transparent;
        m = LocKit.SaveAsset(m, LocKit.MatDir + "/" + name + ".mat");
        LocKit.Cache(name, m);
        return m;
    }

    public static ParticleSystem MakeGlow(Transform parent, string name, Vector3 localPos, Vector3 box,
        Color a, Color b, float size, int count, float life, float speed, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.maxParticles = count;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var em = ps.emission;
        em.rateOverTime = count / life;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = box;

        var nz = ps.noise;
        nz.enabled = true;
        nz.strength = 0.8f;
        nz.frequency = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // ------------------------------------------------------------ гриб (в батчер)
    public void Shroom(LocBatcher bt, Vector3 pos, float h, Material stemM, Material capM, Material spotM, System.Random r, bool spots)
    {
        Quaternion q = Quaternion.Euler(((float)r.NextDouble() - 0.5f) * 14f, (float)r.NextDouble() * 360f, ((float)r.NextDouble() - 0.5f) * 14f);
        bt.Add(stem, stemM, pos + q * new Vector3(0f, h * 0.5f, 0f), q, new Vector3(h * 0.36f, h, h * 0.36f));
        float cw = h * 1.1f;
        Vector3 cp = pos + q * new Vector3(0f, h * 0.95f, 0f);
        bt.Add(blob[0], capM, cp, q, new Vector3(cw * 1.7f, cw * 0.9f, cw * 1.7f));
        if (!spots || spotM == null) return;
        for (int i = 0; i < 5; i++)
        {
            float a = (float)r.NextDouble() * Mathf.PI * 2f;
            float f = 0.25f + (float)r.NextDouble() * 0.45f;
            float x = Mathf.Cos(a) * f * cw * 0.85f, z = Mathf.Sin(a) * f * cw * 0.85f;
            float y = cw * 0.45f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - f * f)) * 0.95f;
            bt.Add(blob[1], spotM, cp + q * new Vector3(x, y, z), q, new Vector3(cw * 0.3f, cw * 0.16f, cw * 0.3f));
        }
    }
}
#endif
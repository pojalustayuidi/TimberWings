#if UNITY_EDITOR
// =====================================================================================
//  Assets/_Project/Editor/Island/Features/LocationPolish.cs
//  Доводка локации (всё дёшево по нагрузке). Настройки: Location Builder -> «Доводка».
//   1. Трава «клочками» с ветром, гнётся под игроком (свой шейдер, вертексные цвета, чанки, без теней)
//   2. Цветочные поляны (тот же шейдер и материал)
//   3. Бабочки у цветов (LocButterfly, убегают от игрока)
//   4. Пыльца над лугом и падающие листья в лесу (по 1 ParticleSystem)
//   5. Пыль из-под ног игрока и мерцание костра (LocationAmbient)
// =====================================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LocationPolish
{
    sealed class Acc
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Color> col = new List<Color>();
        public readonly List<int> t = new List<int>();
    }

    const float Cell = 22f;

    readonly LocationCtx c;
    readonly System.Random rng;
    readonly float ws, half;
    readonly bool linear;
    readonly Dictionary<int, Acc> chunks = new Dictionary<int, Acc>();
    readonly List<Vector2> flowerSpots = new List<Vector2>();
    Transform node;
    Material grassMat, dotMat;
    Texture2D dotTex;
    int bladeCount, flowerCount;

    LocationPolish(LocationCtx ctx)
    {
        c = ctx;
        ws = ctx.ws;
        half = ctx.half;
        rng = new System.Random(ctx.seed * 7 + 101);
        linear = PlayerSettings.colorSpace == ColorSpace.Linear;
    }

    public static void Build(LocationCtx ctx) { new LocationPolish(ctx).Run(); }

    // ------------------------------------------------------------------ utils
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // вертексные цвета не конвертируются автоматически -> в Linear-проекте переводим сами
    Color Lin(Color k) { return linear ? k.linear : k; }
    static Color A(Color k, float a) { return new Color(k.r, k.g, k.b, a); }
    Color E(int i) { return c.s.envColors[i]; }

    Acc Get(float x, float z)
    {
        int n = Mathf.CeilToInt(c.terrainSize / Cell);
        int cx = Mathf.Clamp(Mathf.FloorToInt((x + half) / Cell), 0, n - 1);
        int cz = Mathf.Clamp(Mathf.FloorToInt((z + half) / Cell), 0, n - 1);
        int key = cz * 1000 + cx;
        Acc a;
        if (!chunks.TryGetValue(key, out a)) { a = new Acc(); chunks[key] = a; }
        return a;
    }

    // ------------------------------------------------------------------ run
    void Run()
    {
        var go = new GameObject("Polish");
        go.transform.SetParent(c.root, false);
        node = go.transform;

        grassMat = MakeGrassMat();
        if (grassMat != null)
        {
            BuildGrass();
            BuildFlowers();
            FlushChunks();
        }

        if (c.s.polishButterflies > 0 && flowerSpots.Count > 0) BuildButterflies();

        if (c.s.polishParticles || c.s.polishFootDust) EnsureDot();

        var amb = go.AddComponent<LocationAmbient>();
        if (c.s.polishFootDust) amb.footDust = MakeFootDust();
        if (c.s.polishParticles) { MakePollen(); MakeLeaves(); }

        Debug.Log("[LocationPolish] травинок: " + bladeCount + ", цветов: " + flowerCount + ", бабочек: " + c.s.polishButterflies);
    }

    // ------------------------------------------------------------------ grass shader / material
    Material MakeGrassMat()
    {
        Shader sh = LocKit.EnsureShader(LocKit.ShaderDir + "/BuilderSimWindGrass.shader", GrassShaderSrc);
        if (sh == null || !sh.isSupported)
        {
            Debug.LogError("[LocationPolish] Шейдер BuilderSim/WindGrass не скомпилировался: смотри ошибки в Console (Assets/_Game/Location/Shaders). Трава пропущена.");
            return null;
        }
        var m = new Material(sh) { name = "Mat_WindGrass" };
        m.SetFloat("_Sway", c.s.polishSway);
        return LocKit.SaveAsset(m, LocKit.MatDir + "/Mat_WindGrass.mat");
    }

    // обратные кавычки превращаются в обычные (LocKit.EnsureShader)
    const string GrassShaderSrc = @"Shader `BuilderSim/WindGrass`
{
    Properties
    {
        _Sway (`Sway (m)`, Float) = 0.12
        _Speed (`Speed`, Float) = 1.7
        _Bend (`Player bend (m)`, Float) = 0.45
        _Wrap (`Light wrap`, Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { `RenderType`=`Opaque` `RenderPipeline`=`UniversalPipeline` `Queue`=`Geometry` }
        Pass
        {
            Name `ForwardLit`
            Tags { `LightMode`=`UniversalForward` }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl`
            #include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl`

            CBUFFER_START(UnityPerMaterial)
                float _Sway;
                float _Speed;
                float _Bend;
                float _Wrap;
            CBUFFER_END

            // глобальная позиция игрока (xyz) и радиус (w, 0 = выключено); в своём cbuffer, чтобы не ломать SRP Batcher
            CBUFFER_START(LocGlobals)
                float4 _LocPlayer;
            CBUFFER_END

            struct Attr
            {
                float4 pos : POSITION;
                float4 col : COLOR;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float3 col : TEXCOORD0;
                float4 sc : TEXCOORD1;
            };

            Varyings vert(Attr v)
            {
                Varyings o;
                float3 pw = TransformObjectToWorld(v.pos.xyz);
                float w = v.col.a;
                w = w * w;

                float t = _Time.y * _Speed;
                float ph = dot(pw.xz, float2(0.42, 0.31));
                float g = sin(t + ph) * 0.65 + sin(t * 2.3 + ph * 2.1) * 0.35;
                pw.x += (g + 0.35) * _Sway * w;
                pw.z += g * 0.45 * _Sway * w;

                float2 dp = pw.xz - _LocPlayer.xz;
                float d = length(dp);
                float push = saturate(1.0 - d / max(_LocPlayer.w, 0.001)) * step(0.001, _LocPlayer.w);
                push = push * push;
                pw.xz += dp / max(d, 0.05) * push * _Bend * w;
                pw.y -= push * _Bend * 0.5 * w;

                VertexPositionInputs vi = (VertexPositionInputs)0;
                vi.positionWS = pw;
                vi.positionVS = TransformWorldToView(pw);
                vi.positionCS = TransformWorldToHClip(pw);
                float4 ndc = vi.positionCS * 0.5f;
                vi.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                vi.positionNDC.zw = vi.positionCS.zw;

                o.posCS = vi.positionCS;
                o.col = v.col.rgb;
                o.sc = GetShadowCoord(vi);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light l = GetMainLight(i.sc);
                float ndl = saturate(l.direction.y * (1.0 - _Wrap) + _Wrap);
                float3 amb = SampleSH(float3(0, 1, 0));
                float3 c = i.col * (amb + l.color * ndl * l.distanceAttenuation * l.shadowAttenuation);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}";

    // ------------------------------------------------------------------ geometry
    // травинка = 1 треугольник; alpha вершины = вес качания (0 у корня, 1 на кончике)
    Vector3 Blade(Acc a, float x, float y, float z, float yaw, float h, float w, float lean, Color cb, Color ct)
    {
        float sn = Mathf.Sin(yaw), cs = Mathf.Cos(yaw);
        Vector3 r = new Vector3(cs, 0f, -sn) * (w * 0.5f);
        Vector3 f = new Vector3(sn, 0f, cs);
        Vector3 b = new Vector3(x, y, z);
        Vector3 tip = b + f * (lean * h) + new Vector3(0f, h, 0f);
        int i = a.v.Count;
        a.v.Add(b - r); a.v.Add(b + r); a.v.Add(tip);
        a.col.Add(cb); a.col.Add(cb); a.col.Add(ct);
        a.t.Add(i); a.t.Add(i + 1); a.t.Add(i + 2);
        bladeCount++;
        return tip;
    }

    void Head(Acc a, Vector3 ctr, Vector3 axis, float s, Color col)
    {
        Vector3 L = ctr - axis * (s * 0.5f), R = ctr + axis * (s * 0.5f);
        Vector3 T = ctr + new Vector3(0f, s * 0.5f, 0f), B = ctr - new Vector3(0f, s * 0.4f, 0f);
        int i = a.v.Count;
        a.v.Add(L); a.v.Add(R); a.v.Add(T); a.v.Add(B);
        for (int k = 0; k < 4; k++) a.col.Add(col);
        a.t.Add(i); a.t.Add(i + 1); a.t.Add(i + 2);
        a.t.Add(i); a.t.Add(i + 1); a.t.Add(i + 3);
    }

    bool NearRiver(float x, float z, float extra)
    {
        if (!c.s.buildRiver) return false;
        float hwB = c.riverWidth * 0.5f + c.riverBank + extra;
        return Mathf.Abs(x - c.riverX(z)) < hwB && Mathf.Abs(z) < 60f;
    }

    // ------------------------------------------------------------------ grass
    void BuildGrass()
    {
        float dens = 0.5f * c.s.polishGrass;
        if (dens <= 0.02f) return;
        float g = 1f / Mathf.Sqrt(dens);
        float lim = half - 3f;
        float po = (c.seed % 997) * 0.13f;

        for (float gx = -lim; gx <= lim; gx += g)
        {
            for (float gz = -lim; gz <= lim; gz += g)
            {
                float x = gx + Rf(-0.5f, 0.5f) * g, z = gz + Rf(-0.5f, 0.5f) * g;
                if (Mathf.Abs(x) > lim || Mathf.Abs(z) > lim) continue;
                float h = c.heightU(x, z);
                if (h < -0.05f) continue;
                if (NearRiver(x, z, 1.5f)) continue;

                float d = c.distFlat(x, z);
                if (d < 0.6f + Rf(0f, 0.3f)) continue;

                // гуще у троп (бахрома), «лысые» пятна по шуму, реже на горах
                float p = 0.5f + 0.5f * (1f - SS(0.7f, 3.5f, d));
                float patch = Mathf.PerlinNoise(x * 0.07f + po, z * 0.07f + 11.3f);
                p *= Mathf.Lerp(0.25f, 1f, SS(0.3f, 0.6f, patch));
                p *= 1f - SS(2.5f, 6f, h);
                bool glade = c.inGlade(x, z, 0f);
                if (glade) p *= 0.7f;
                if (Rf(0f, 1f) > p) continue;

                float sl = (Mathf.Abs(c.heightU(x + 0.8f, z) - c.heightU(x - 0.8f, z)) +
                            Mathf.Abs(c.heightU(x, z + 0.8f) - c.heightU(x, z - 0.8f))) / 1.6f;
                if (sl > 0.9f) continue;

                Clump(x, z, h, glade);
            }
        }
    }

    void Clump(float x, float z, float h, bool glade)
    {
        Acc a = Get(x, z);
        Color baseG = Color.Lerp(c.s.grassA, c.s.grassB, Mathf.PerlinNoise(x * 0.05f + 7f, z * 0.05f + 3f));
        if (glade) baseG = Color.Lerp(baseG, c.s.forestColor, 0.5f);
        float tone = Rf(0f, 1f);
        Color cb = Lin(A(Color.Lerp(baseG, new Color(0.06f, 0.20f, 0.08f), 0.5f), 0f));
        Color ct = Lin(A(Color.Lerp(baseG, new Color(0.85f, 0.95f, 0.35f), 0.25f + 0.25f * tone), 1f));
        cb.a = 0f; ct.a = 1f;

        int blades = rng.Next(5, 8);
        float rad = Rf(0.15f, 0.35f), hh = Rf(0.45f, 0.85f);
        for (int k = 0; k < blades; k++)
        {
            float ang = Rf(0f, 6.283f), o = Rf(0f, rad);
            float bx = x + Mathf.Cos(ang) * o, bz = z + Mathf.Sin(ang) * o;
            float bh = hh * Rf(0.6f, 1.15f) * (1f - o / rad * 0.3f);
            Blade(a, bx, h - 0.03f, bz, Rf(0f, 6.283f), bh, Rf(0.09f, 0.15f), Rf(0.05f, 0.3f), cb, ct);
        }
    }

    // ------------------------------------------------------------------ flowers
    void BuildFlowers()
    {
        float area = (c.terrainSize / 140f) * (c.terrainSize / 140f);
        int clusters = Mathf.RoundToInt(260f * area * c.s.polishFlowers);
        if (clusters <= 0) return;
        float lim = half - 4f;
        Color[] pal =
        {
            E(7), E(8), E(9), new Color(0.95f, 0.55f, 0.78f), new Color(0.62f, 0.55f, 0.95f), new Color(1f, 0.6f, 0.2f)
        };
        float po = (c.seed % 389) * 0.21f;

        for (int k = 0, placed = 0; k < clusters * 8 && placed < clusters; k++)
        {
            float x = Rf(-lim, lim), z = Rf(-lim, lim);
            float h = c.heightU(x, z);
            if (h < 0f || h > 2f) continue;
            if (NearRiver(x, z, 2f)) continue;
            if (c.distFlat(x, z) < 1.4f) continue;
            if (c.inGlade(x, z, -1f)) continue;
            if (Mathf.PerlinNoise(x * 0.05f + 91f + po, z * 0.05f + 17f) < 0.42f) continue;

            Color main = pal[rng.Next(pal.Length)];
            int n = rng.Next(5, 10);
            for (int i = 0; i < n; i++)
            {
                float fx = x + Rf(-0.9f, 0.9f), fz = z + Rf(-0.9f, 0.9f);
                if (c.distFlat(fx, fz) < 1.0f || c.heightU(fx, fz) < 0f || NearRiver(fx, fz, 1.5f)) continue;
                Flower(fx, fz, rng.NextDouble() < 0.2 ? pal[rng.Next(pal.Length)] : main);
            }
            flowerSpots.Add(new Vector2(x, z));
            placed++;
        }
    }

    void Flower(float x, float z, Color head)
    {
        float y = c.heightU(x, z) - 0.02f;
        float h = Rf(0.35f, 0.6f), lean = Rf(-0.1f, 0.1f), yaw = Rf(0f, 6.283f);
        Acc a = Get(x, z);
        Color sb = Lin(A(new Color(0.15f, 0.38f, 0.14f), 0f));
        Color st = Lin(A(new Color(0.30f, 0.60f, 0.22f), 1f));
        sb.a = 0f; st.a = 1f;
        Vector3 tip = Blade(a, x, y, z, yaw, h, 0.05f, lean, sb, st);

        float k = Rf(0.92f, 1.05f);
        Color hc = Lin(new Color(Mathf.Min(1f, head.r * k), Mathf.Min(1f, head.g * k), Mathf.Min(1f, head.b * k), 1f));
        hc.a = 1f;
        float s = Rf(0.15f, 0.22f);
        Vector3 ctr = tip + new Vector3(0f, s * 0.15f, 0f);
        Head(a, ctr, new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)), s, hc);
        Head(a, ctr, new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), s, hc);
        flowerCount++;
    }

    // ------------------------------------------------------------------ flush chunks
    void FlushChunks()
    {
        var parent = new GameObject("Grass").transform;
        parent.SetParent(node, false);
        foreach (var kv in chunks)
        {
            Acc a = kv.Value;
            if (a.v.Count == 0) continue;
            int cx = kv.Key % 1000, cz = kv.Key / 1000;
            string nm = "Grass_" + cx + "_" + cz;

            var vs = new Vector3[a.v.Count];
            for (int i = 0; i < vs.Length; i++) vs[i] = a.v[i] * ws;

            var mesh = new Mesh { name = nm, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vs);
            mesh.SetColors(a.col);
            mesh.SetTriangles(a.t, 0);
            mesh.RecalculateBounds();
            var b = mesh.bounds;
            b.Expand(1.0f);   // запас под ветер и изгиб
            mesh.bounds = b;
            mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/" + nm + ".asset");

            var go = new GameObject(nm);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = grassMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
        }
        chunks.Clear();
    }

    // ------------------------------------------------------------------ butterflies
    Mesh MakeWing()
    {
        var mb = new LocMB();
        Vector3 O = Vector3.zero;
        Vector3 P1 = new Vector3(0.06f, 0f, 0.08f), P2 = new Vector3(0.12f, 0f, 0.04f);
        Vector3 P3 = new Vector3(0.11f, 0f, -0.02f), P4 = new Vector3(0.06f, 0f, -0.07f);
        Vector3[][] tris = { new[] { O, P1, P2 }, new[] { O, P2, P3 }, new[] { O, P3, P4 } };
        foreach (var t in tris)
        {
            mb.Tri(t[0], t[1], t[2], Vector3.up);
            mb.Tri(t[0], t[1], t[2], Vector3.down);
        }
        return LocKit.SaveAsset(mb.ToMesh("ButterflyWing"), LocKit.Batches + "/ButterflyWing.asset");
    }

    void BuildButterflies()
    {
        Mesh wing = MakeWing();
        Mesh body = LocKit.SaveAsset(LocKit.Box(), LocKit.Batches + "/ButterflyBody.asset");
        Material bodyMat = LocKit.Mat("ButterflyBody", new Color(0.12f, 0.1f, 0.1f), 0.1f);
        Material[] wm =
        {
            LocKit.Mat("ButterflyY", new Color(1f, 0.85f, 0.2f), 0.1f, 0.25f),
            LocKit.Mat("ButterflyW", new Color(0.97f, 0.97f, 0.95f), 0.1f, 0.2f),
            LocKit.Mat("ButterflyO", new Color(1f, 0.55f, 0.15f), 0.1f, 0.25f),
            LocKit.Mat("ButterflyB", new Color(0.45f, 0.7f, 1f), 0.1f, 0.25f)
        };

        var parent = new GameObject("Butterflies").transform;
        parent.SetParent(node, false);

        for (int i = 0; i < c.s.polishButterflies; i++)
        {
            Vector2 sp = flowerSpots[rng.Next(flowerSpots.Count)];
            Vector3 home = new Vector3(sp.x * ws, c.heightU(sp.x, sp.y) * ws, sp.y * ws);

            var go = new GameObject("Butterfly_" + i);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = home + Vector3.up * 0.8f;
            go.transform.localScale = Vector3.one * 1.6f;

            var bf = go.AddComponent<LocButterfly>();
            bf.homeLocal = home;
            bf.radius = Rf(4f, 9f) * ws;
            bf.speed = Rf(0.5f, 0.9f);
            bf.height = Rf(0.5f, 0.9f);
            bf.phase = Rf(0f, 6.283f);
            bf.flapSpeed = Rf(12f, 18f);

            Material mat = wm[rng.Next(wm.Length)];
            Part(go.transform, "Body", body, bodyMat, new Vector3(0.02f, 0.02f, 0.09f), Vector3.zero);
            var wr = Part(go.transform, "WingR", wing, mat, Vector3.one, new Vector3(0.005f, 0.01f, 0f));
            var wl = Part(go.transform, "WingL", wing, mat, new Vector3(-1f, 1f, 1f), new Vector3(-0.005f, 0.01f, 0f));
            bf.wingR = wr.transform;
            bf.wingL = wl.transform;
        }
    }

    GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 scale, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    // ------------------------------------------------------------------ particles
    void EnsureDot()
    {
        if (dotTex == null)
        {
            const int n = 32;
            dotTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - SS(0.35f, 1f, r)));
                }
            dotTex.SetPixels(px);
            dotTex.Apply(false);
            dotTex = LocKit.SaveAsset(dotTex, LocKit.MatDir + "/SoftDot.asset");
        }
        if (dotMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "Mat_ParticleDot" };
            m.SetTexture("_BaseMap", dotTex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            dotMat = LocKit.SaveAsset(m, LocKit.MatDir + "/Mat_ParticleDot.mat");
        }
    }

    static ParticleSystem.MinMaxGradient FadeGradient(Color col, float aMax)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(aMax, 0.2f), new GradientAlphaKey(aMax, 0.8f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    ParticleSystem NewPS(string name, Vector3 localPos, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(node, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dotMat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    void MakePollen()
    {
        float S = c.terrainSize * ws;
        var ps = NewPS("Pollen", new Vector3(0f, 1.0f, 0f), Quaternion.identity);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 0.97f, 0.75f, 1f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 14f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(S * 0.9f, 1.2f, S * 0.9f);

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0.15f);
        vol.y = new ParticleSystem.MinMaxCurve(0.03f);
        vol.z = new ParticleSystem.MinMaxCurve(0.05f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white, 0.7f);

        ps.Play();
    }

    void MakeLeaves()
    {
        if (!c.hasForest) return;
        Vector2 f = c.forestPos;
        Vector3 pos = new Vector3(f.x * ws, c.heightU(f.x, f.y) * ws + 3.8f, f.y * ws);
        var ps = NewPS("FallingLeaves", pos, Quaternion.Euler(90f, 0f, 0f));

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(11f, 13f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.6f, 0.15f, 1f), new Color(0.55f, 0.8f, 0.2f, 1f));
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var em = ps.emission;
        em.rateOverTime = 4f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = c.forestRad * ws * 0.9f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(0.1f);
        vol.y = new ParticleSystem.MinMaxCurve(-0.35f);
        vol.z = new ParticleSystem.MinMaxCurve(0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.8f;
        noise.frequency = 0.35f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white, 1f);

        ps.Play();
    }

    ParticleSystem MakeFootDust()
    {
        var ps = NewPS("FootDust", Vector3.zero, Quaternion.identity);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = new Color(0.86f, 0.74f, 0.55f, 1f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f;

        var em = ps.emission;
        em.rateOverTime = 0f;   // эмитит LocationAmbient вручную

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.08f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeGradient(Color.white, 0.6f);

        return ps;
    }
}
#endif
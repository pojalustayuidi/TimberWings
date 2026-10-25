#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilderTester.River.cs
// Живая река для Location Builder TESTER (partial-часть LocationBuilderTester).
// Профиль русла (ширина 8-10 u, глубина 0.3-0.8 u), Field и стенки RiverWall НЕ меняются: всё ниже только украшает воду и берега.
//
// Что здесь:
//   1. Карта следов (BakeFlowMap -> RiverFlowMap.png): по препятствиям в воде (валуны из BuildRocks, островки L.islets) запекается
//      R = расстояние до кромки камня (пенное кольцо), G = светлый V-след по течению за камнем, B = зона ускорения у камня.
//      Шейдер LocationToonWater читает карту по positionOS.xz (ConfigureRiverWater выставляет _WakeMap/_WakeRect/_WakeAmt).
//   2. Туман и брызги под водопадом с края острова (Cfg.riverMist): BuildFallMist = полупрозрачные диски на шейдере воды
//      (альфа по uv.x: центр непрозрачный, кромка прозрачная), брызги = белые комки пены в BuildRiverDecor.
//   3. Берега и мелочи (BuildRiverDecor, один меш RiverDecor):
//        - дуги травы с цветами на пологих участках, песчаные отмели с камешками (Cfg.props);
//        - 2-3 упавших бревна у воды (Cfg.riverLogs);
//        - кувшинки в плёсах (Cfg.props + вода), рыбы-силуэты под водой у островков (Cfg.riverFish);
//        - камыш и рогоз у кромки (Cfg.riverReeds), мокрая галька у уреза и на перекатах, утиные семьи с рябью, стрекозы и бабочки (Cfg.riverLife),
//          рыбацкие мостки с ведром и удочкой (Cfg.riverJetty).
//   4. Блики солнца на воде (_Sparkle, Cfg.riverSparkle) считаются в шейдере воды.
// Вызывается из сборки в сцене: BuildRocks -> AddObst, ветка воды -> ConfigureRiverWater / BuildFallMist, после Props -> BuildRiverDecor.
// Размеры в u; меш собирается в u и в конце масштабируется на worldScale (как BuildBridgeMesh).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilderTester
{
    // препятствие в воде: валун или островок (для следов течения и пены)
    sealed class RiverObst
    {
        public Vector2 pos, flow;   // центр и направление течения в этой точке
        public float r, k;          // радиус, сила (1 = торчит из воды, меньше = под водой)
        public Islet il;            // не null = островок (вытянутая форма)
    }

    // подмеши RiverDecor (индекс = номер материала в MakeRiverDecorMats)
    const int RD_BARK = 0, RD_END = 1, RD_GRASS = 2, RD_FPINK = 3, RD_FYEL = 4, RD_FWHITE = 5, RD_SAND = 6, RD_PEBBLE = 7,
              RD_LILY = 8, RD_LILYF = 9, RD_FISH = 10, RD_FOAM = 11,
              RD_REED = 12, RD_CATTAIL = 13, RD_DUCKB = 14, RD_DUCKG = 15, RD_DUCKH = 16, RD_DUCKY = 17, RD_BEAK = 18,
              RD_WING = 19, RD_DRAGON = 20, RD_PLANK = 21, RD_ROPE = 22, RD_RED = 23;
    const int RdSubs = 24;

    // =====================================================================================================
    //  КАРТА СЛЕДОВ ТЕЧЕНИЯ
    // =====================================================================================================

    // запоминает валун как препятствие (вызывается из BuildRocks); направление течения = у ближайшей точки русла
    static void AddObst(Layout L, Vector2 pos, float r, float k)
    {
        var rv = L.river;
        if (rv == null || rv.p.Count < 2) return;
        int bi = 0; float bd = float.MaxValue;
        for (int i = 0; i < rv.p.Count - 1; i++)
        {
            float dd = (rv.p[i] - pos).sqrMagnitude;
            if (dd < bd) { bd = dd; bi = i; }
        }
        L.obst.Add(new RiverObst { pos = pos, r = r, k = k, flow = rv.Dir(bi) });
    }

    // запекает карту: R = расстояние до кромки препятствия (0..1.2 u -> 0..1), G = V-след за ним, B = ускорение рядом.
    // rect = (x0, z0, 1/ширина, 1/высота) в метрах корня (positionOS воды).
    static Texture2D BakeFlowMap(Layout L, out Vector4 rect)
    {
        var c = L.c; var rv = L.river; float ws = c.worldScale;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        for (int i = 0; i < rv.p.Count; i++)
        {
            x0 = Mathf.Min(x0, rv.p[i].x); x1 = Mathf.Max(x1, rv.p[i].x);
            z0 = Mathf.Min(z0, rv.p[i].y); z1 = Mathf.Max(z1, rv.p[i].y);
        }
        float mg = c.riverWidth * 0.5f + 10f;
        x0 -= mg; x1 += mg; z0 -= mg; z1 += mg;
        float px = 0.2f;
        int W = Mathf.CeilToInt((x1 - x0) / px), H = Mathf.CeilToInt((z1 - z0) / px);
        while (W > 2048 || H > 2048)
        {
            px *= 1.2f;
            W = Mathf.CeilToInt((x1 - x0) / px); H = Mathf.CeilToInt((z1 - z0) / px);
        }
        W = Mathf.Max(W, 4); H = Mathf.Max(H, 4);

        var dist = new float[W * H]; var wake = new float[W * H]; var acc = new float[W * H];
        for (int k = 0; k < dist.Length; k++) dist[k] = 1f;

        var list = new List<RiverObst>(L.obst);
        for (int k = 0; k < L.islets.Count; k++)
        {
            var isl = L.islets[k];
            list.Add(new RiverObst { pos = isl.pos, flow = isl.dir, r = isl.r, k = 1f, il = isl });
        }

        foreach (var o in list)
        {
            bool isIslet = o.il != null;
            float a0 = (isIslet ? 1.2f : 0.35f) * o.r;       // где начинается след (от центра по течению)
            float w0 = (isIslet ? 0.75f : 0.55f) * o.r;      // полуширина следа в начале
            float wl = 3.2f + o.r * 3.2f;                    // длина следа
            float reach = a0 + wl + o.r + 2f;
            int ia = Mathf.Max(0, Mathf.FloorToInt((o.pos.x - reach - x0) / px));
            int ib = Mathf.Min(W - 1, Mathf.CeilToInt((o.pos.x + reach - x0) / px));
            int ja = Mathf.Max(0, Mathf.FloorToInt((o.pos.y - reach - z0) / px));
            int jb = Mathf.Min(H - 1, Mathf.CeilToInt((o.pos.y + reach - z0) / px));
            Vector2 perp = new Vector2(-o.flow.y, o.flow.x);
            for (int j = ja; j <= jb; j++)
                for (int i = ia; i <= ib; i++)
                {
                    Vector2 q = new Vector2(x0 + (i + 0.5f) * px, z0 + (j + 0.5f) * px);
                    Vector2 rel = q - o.pos;
                    float d = isIslet ? IsletD(o.il, q, 0.5f) - o.il.r : rel.magnitude - o.r;
                    int idx = j * W + i;
                    float de = Mathf.Max(0f, d + (1f - o.k) * 0.3f);
                    dist[idx] = Mathf.Min(dist[idx], Mathf.Clamp01(de / 1.2f));
                    acc[idx] = Mathf.Max(acc[idx], (1f - SS(0f, 2.4f, Mathf.Max(0f, d))) * o.k);
                    float along = Vector2.Dot(rel, o.flow) - a0;
                    if (along > 0f && along < wl)
                    {
                        float lat = Mathf.Abs(Vector2.Dot(rel, perp));
                        float half = w0 + 0.3f * along;                 // V расходится по течению
                        float wd = 0.13f + 0.03f * along;
                        float tt = (lat - half) / wd;
                        float fade = SS(0f, 0.7f, along) * (1f - SS(wl * 0.35f, wl, along));
                        wake[idx] = Mathf.Max(wake[idx], Mathf.Exp(-tt * tt) * fade * o.k);
                    }
                }
        }

        var pix = new Color32[W * H];
        for (int k = 0; k < pix.Length; k++)
            pix[k] = new Color32((byte)Mathf.RoundToInt(dist[k] * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(wake[k]) * 255f),
                                 (byte)Mathf.RoundToInt(Mathf.Clamp01(acc[k]) * 255f), 255);

        var tx = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
        tx.SetPixels32(pix);
        tx.Apply();
        string path = AssetDir + "/Layers/RiverFlowMap.png";
        string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
        File.WriteAllBytes(full, tx.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tx);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.sRGBTexture = false;           // данные, не цвет
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        rect = new Vector4(x0 * ws, z0 * ws, 1f / (W * px * ws), 1f / (H * px * ws));
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // материал реки: карта следов, ленты течения, прозрачное мелководье (видно дно), чистая белая пена.
    // Новые свойства шейдера по умолчанию выключены, поэтому остальные материалы воды (пруд, водопады) не меняются.
    static void ConfigureRiverWater(Material m, Layout L)
    {
        if (m == null || L.river == null || !m.HasProperty("_WakeMap")) return;
        var c = L.c;
        Vector4 rect;
        var tex = BakeFlowMap(L, out rect);
        m.SetTexture("_WakeMap", tex);
        m.SetVector("_WakeRect", rect);
        m.SetFloat("_WakeAmt", Mathf.Clamp(c.riverFoamAmount, 0f, 2f));
        m.SetFloat("_Ribbons", 0.5f);
        m.SetFloat("_ShallowAlpha", 0.6f);
        m.SetColor("_Foam", new Color(1f, 0.99f, 0.94f));
        m.SetFloat("_Caustic", 0.15f);
        if (m.HasProperty("_VertexFoam")) m.SetFloat("_VertexFoam", 1f);   // у реки есть вершинная маска пены (BuildRiverRibbon)
        if (m.HasProperty("_Sparkle")) m.SetFloat("_Sparkle", Mathf.Clamp01(c.riverSparkle));   // солнечные блики
        EditorUtility.SetDirty(m);
    }

    // =====================================================================================================
    //  ТУМАН ПОД ВОДОПАДОМ С КРАЯ
    // =====================================================================================================

    // светлые полупрозрачные диски-облака внизу падающей воды. Шейдер воды: альфа идёт от uv.x (центр 0.1 = полная, кромка -0.03 = ноль),
    // полос и пены нет (Lines = 0, FoamDepth = 0), поэтому диск получается мягким.
    static Mesh BuildFallMist(Layout L)
    {
        var rv = L.river; var c = L.c; float ws = c.worldScale;
        var rnd = new System.Random(c.seed * 71 + 5);
        float y0 = rv.WaterY(rv.s[rv.p.Count - 2]);
        float yBot = y0 - c.fallLength;
        // 11 дисков от низа листа вверх: широкие и плотные внизу, мельче и выше к кромке
        return BuildMistDiscs("LocationTest_FallMist", rv.lip + rv.lipDir * 2.5f, rv.lipDir, yBot + 0.2f, yBot + 4.6f, 11, 2.6f, 4.2f, 2.2f, 2.4f, ws, rnd);
    }

    // =====================================================================================================
    //  БЕРЕГА, БРЕВНА, КУВШИНКИ, РЫБЫ, БРЫЗГИ
    // =====================================================================================================

    // точка у берега: s по длине реки, side = +-1 (знак нормали), k = 0 кромка воды .. 1 внешний край откоса (меньше 0 = в воду)
    static Vector2 RvBank(River rv, float s, float side, float k)
    {
        int i = rv.Index(s);
        Vector2 dv = rv.Dir(i);
        Vector2 nr = new Vector2(-dv.y, dv.x);
        return rv.p[i] + nr * (side * (rv.HW(s) + k * rv.Wall(s, side > 0f)));
    }

    // k, где земля поднимается до уровня воды (настоящая кромка воды при любой глубине русла)
    static float RvShoreK(River rv, Func<Vector2, float> Hy, float s, float side)
    {
        float wy = rv.WaterY(s);
        for (float k = -0.3f; k <= 1.2f; k += 0.05f)
            if (Hy(RvBank(rv, s, side, k)) >= wy - 0.02f) return k;
        return 0.6f;
    }

    // пологий берег: от настоящей кромки воды до внешнего края откоса земля поднимается небольшим уклоном
    static bool RvGentle(Layout L, Field F, float s, float side)
    {
        var rv = L.river;
        Func<Vector2, float> Hy = q => F.g.At(F.H, q.x, q.y);
        float k0 = RvShoreK(rv, Hy, s, side);
        if (k0 > 0.7f) return false;
        Vector2 a = RvBank(rv, s, side, k0), b = RvBank(rv, s, side, 1f);
        float run = Mathf.Max(0.8f, (1f - k0) * Mathf.Max(1f, rv.Wall(s, side > 0f)));
        return Mathf.Abs(Hy(b) - Hy(a)) / run < 0.45f;
    }

    // суша, где можно ставить декор: внутри острова, не на тропе, не у горы, смотровой и площадок
    static bool RvLandOk(Layout L, Field F, Vector2 pt)
    {
        if (!L.outline.Inside(pt.x, pt.y)) return false;
        if (F.g.At(F.dT, pt.x, pt.y) < 4.5f) return false;
        if (L.mtn != null && L.mtn.SD(pt) < 1.5f) return false;
        if (L.lookoutPad != null && LookDist(L, pt) < LookRad(L) + 2f) return false;
        foreach (var pd in L.pads)
            if (Vector2.Distance(pt, pd.pos) < Mathf.Max(pd.hw, Mathf.Max(pd.hd, pd.rad)) + 2f) return false;
        return true;
    }

    static Vector2 RvRot(Vector2 v, float a)
    {
        float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    // пучок травы: изогнутые лезвия из двух звеньев, двусторонние
    static void RvGrassTuft(MB mb, Vector2 p, float y, System.Random rnd)
    {
        int n = 6 + rnd.Next(0, 4);
        for (int k = 0; k < n; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 0.3f, h = 0.5f + Rf(rnd) * 0.6f;
            Vector3 b = new Vector3(p.x + Mathf.Cos(a) * rr, y - 0.03f, p.y + Mathf.Sin(a) * rr);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * 0.06f;
            Vector3 m = b + dir * 0.08f + new Vector3(0f, h * 0.55f, 0f);
            Vector3 tip = b + dir * 0.3f + new Vector3(0f, h * 0.92f, 0f);
            Vector3 nrm = Vector3.Cross(m - b, side).normalized;
            mb.Quad(RD_GRASS, b - side, b + side, m + side * 0.6f, m - side * 0.6f, nrm);
            mb.Quad(RD_GRASS, b - side, b + side, m + side * 0.6f, m - side * 0.6f, -nrm);
            mb.Tri(RD_GRASS, m - side * 0.6f, m + side * 0.6f, tip, nrm);
            mb.Tri(RD_GRASS, m - side * 0.6f, m + side * 0.6f, tip, -nrm);
        }
    }

    // цветок на стебле: голова из повёрнутого кубика (розовый, жёлтый или белый)
    static void RvFlower(MB mb, Vector2 p, float y, System.Random rnd)
    {
        float h = 0.45f + Rf(rnd) * 0.35f;
        Vector3 top = new Vector3(p.x, y + h, p.y);
        LkBeam(mb, RD_GRASS, new Vector3(p.x, y - 0.02f, p.y), top, 0.035f);
        float pick = Rf(rnd);
        int sub = pick < 0.4f ? RD_FPINK : (pick < 0.75f ? RD_FYEL : RD_FWHITE);
        LkBox(mb, sub, top, new Vector3(0.09f, 0.07f, 0.09f), Quaternion.Euler(0f, Rf(rnd) * 90f, 0f));
    }

    // песчаная отмель: неровный эллипс (центр и два кольца), вершины лежат на земле + 0.05 u, поэтому повторяют рельеф
    static void RvSand(MB mb, Func<Vector2, float> Hy, Vector2 cen, Vector2 fl, Vector2 nr, float rx, float rz, System.Random rnd)
    {
        const int n = 14;
        var ring = new Vector3[2, n];
        float[] kr = { 0.55f, 1f };
        for (int j = 0; j < 2; j++)
            for (int k = 0; k < n; k++)
            {
                float a = k * 6.2832f / n;
                float jit = j == 1 ? 0.85f + 0.3f * Rf(rnd) : 1f;
                Vector2 pt = cen + fl * (Mathf.Cos(a) * rx * kr[j] * jit) + nr * (Mathf.Sin(a) * rz * kr[j] * jit);
                ring[j, k] = new Vector3(pt.x, Hy(pt) + 0.05f, pt.y);
            }
        Vector3 c0 = new Vector3(cen.x, Hy(cen) + 0.05f, cen.y);
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            mb.Tri(RD_SAND, c0, ring[0, k], ring[0, k2], Vector3.up);
            mb.Quad(RD_SAND, ring[0, k], ring[1, k], ring[1, k2], ring[0, k2], Vector3.up);
        }
    }

    // упавшее бревно: семигранная призма от a до b (комель толще), светлые торцы и два сучка
    static void RvLog(MB mb, Vector3 a, Vector3 b, float rad, System.Random rnd)
    {
        Vector3 ax = (b - a).normalized;
        Vector3 u = Vector3.Cross(ax, Vector3.up);
        if (u.sqrMagnitude < 1e-4f) u = Vector3.right;
        u.Normalize();
        Vector3 v = Vector3.Cross(ax, u).normalized;
        const int n = 7;
        var ra = new Vector3[n]; var rb = new Vector3[n];
        for (int k = 0; k < n; k++)
        {
            float ang = k * 6.2832f / n;
            Vector3 o = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
            float jj = 0.92f + 0.16f * Rf(rnd);
            ra[k] = a + o * (rad * 1.05f * jj);
            rb[k] = b + o * (rad * 0.85f * jj);
        }
        Vector3 mid = (a + b) * 0.5f;
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            Vector3 fc = (ra[k] + ra[k2] + rb[k] + rb[k2]) * 0.25f - mid;
            mb.Quad(RD_BARK, ra[k], ra[k2], rb[k2], rb[k], fc);
            mb.Tri(RD_END, a, ra[k2], ra[k], -ax);
            mb.Tri(RD_END, b, rb[k], rb[k2], ax);
        }
        for (int bn = 0; bn < 2; bn++)
        {
            float t = 0.3f + 0.35f * bn + 0.1f * Rf(rnd);
            Vector3 pb = Vector3.Lerp(a, b, t) + Vector3.up * (rad * 0.6f);
            Vector3 dirb = (Vector3.up * 0.8f + u * (bn == 0 ? 1f : -1f) * 0.6f + ax * 0.2f).normalized;
            LkBeam(mb, RD_BARK, pb, pb + dirb * (0.5f + 0.4f * Rf(rnd)), 0.1f);
        }
    }

    // кувшинка: плоский диск с вырезом-клином и цветок
    static void RvLily(MB mb, Vector2 p, float y, float rad, bool flower, System.Random rnd)
    {
        const int n = 9;
        float notch = Rf(rnd) * 6.28f, sweep = Mathf.PI * 2f - 0.55f;
        Vector3 c0 = new Vector3(p.x, y, p.y);
        for (int k = 0; k < n; k++)
        {
            float a0 = notch + 0.55f + k * sweep / n, a1 = notch + 0.55f + (k + 1) * sweep / n;
            Vector3 v0 = new Vector3(p.x + Mathf.Cos(a0) * rad, y, p.y + Mathf.Sin(a0) * rad);
            Vector3 v1 = new Vector3(p.x + Mathf.Cos(a1) * rad, y, p.y + Mathf.Sin(a1) * rad);
            mb.Tri(RD_LILY, c0, v0, v1, Vector3.up);
        }
        if (!flower) return;
        LkBox(mb, RD_LILYF, c0 + new Vector3(0f, 0.07f, 0f), new Vector3(rad * 0.28f, 0.07f, rad * 0.28f), Quaternion.Euler(0f, Rf(rnd) * 90f, 0f));
        LkBox(mb, RD_FYEL, c0 + new Vector3(0f, 0.15f, 0f), new Vector3(rad * 0.1f, 0.04f, rad * 0.1f), Quaternion.identity);
    }

    // рыба-силуэт: плоский многоугольник с хвостом, смотрит носом по fw, len = длина в u
    static void RvFish(MB mb, Vector2 p, float y, Vector2 fw, float len)
    {
        Vector2 sd = new Vector2(-fw.y, fw.x);
        float[,] body = { { 0.5f, 0f }, { 0.3f, 0.11f }, { 0f, 0.14f }, { -0.25f, 0.07f }, { -0.36f, 0.03f },
                          { -0.36f, -0.03f }, { -0.25f, -0.07f }, { 0f, -0.14f }, { 0.3f, -0.11f } };
        int n = body.GetLength(0);
        Func<float, float, Vector3> P = (fa, fb) =>
        {
            Vector2 q = p + fw * (fa * len) + sd * (fb * len);
            return new Vector3(q.x, y, q.y);
        };
        Vector3 c0 = P(0f, 0f);
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            mb.Tri(RD_FISH, c0, P(body[k, 0], body[k, 1]), P(body[k2, 0], body[k2, 1]), Vector3.up);
        }
        mb.Tri(RD_FISH, P(-0.33f, 0f), P(-0.56f, 0.13f), P(-0.5f, 0f), Vector3.up);
        mb.Tri(RD_FISH, P(-0.33f, 0f), P(-0.5f, 0f), P(-0.56f, -0.13f), Vector3.up);
    }

    // двусторонний треугольник и четырёхугольник (листья, крылья)
    static void RvTri2(MB mb, int sub, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-12f) return;
        mb.Tri(sub, a, b, c, n);
        mb.Tri(sub, a, b, c, -n);
    }

    static void RvQuad2(MB mb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        RvTri2(mb, sub, a, b, c);
        RvTri2(mb, sub, a, c, d);
    }

    // камыш и рогоз: пучок изогнутых стеблей; у части стеблей коричневая «сосиска» на верхушке, у остальных узкий лист
    static void RvReeds(MB mb, Vector2 p, float y, System.Random rnd, int stems)
    {
        for (int k = 0; k < stems; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 0.35f, h = 1.0f + Rf(rnd) * 1.0f;
            Vector3 b = new Vector3(p.x + Mathf.Cos(a) * rr, y - 0.05f, p.y + Mathf.Sin(a) * rr);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 m = b + dir * (0.05f + 0.08f * Rf(rnd)) + new Vector3(0f, h * 0.6f, 0f);
            Vector3 tip = m + dir * (0.12f + 0.25f * Rf(rnd)) + new Vector3(0f, h * 0.4f, 0f);
            if (Rf(rnd) < 0.38f)
            {
                LkBeam(mb, RD_REED, b, m, 0.035f);
                LkBeam(mb, RD_REED, m, tip, 0.03f);
                Vector3 hd = (tip - m).normalized;
                LkBeam(mb, RD_CATTAIL, tip - hd * 0.34f, tip, 0.1f);
                LkBeam(mb, RD_REED, tip, tip + hd * 0.16f, 0.02f);
            }
            else
            {
                Vector3 sd = new Vector3(-dir.z, 0f, dir.x) * 0.05f;
                RvQuad2(mb, RD_REED, b - sd, b + sd, m + sd * 0.6f, m - sd * 0.6f);
                RvTri2(mb, RD_REED, m - sd * 0.6f, m + sd * 0.6f, tip);
            }
        }
    }

    // стрекоза: тельце, голова и две пары прозрачных крыльев
    static void RvDragonfly(MB mb, Vector3 p, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fw = q * Vector3.forward, sd = q * Vector3.right;
        LkBeam(mb, RD_DRAGON, p - fw * 0.34f, p + fw * 0.06f, 0.035f);
        LkBox(mb, RD_DRAGON, p + fw * 0.1f, new Vector3(0.045f, 0.045f, 0.045f), q);
        for (int pair = 0; pair < 2; pair++)
        {
            Vector3 root = p - fw * (0.02f + 0.09f * pair);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 tp = root + sd * (sg * 0.3f) - fw * 0.04f + new Vector3(0f, 0.03f, 0f);
                RvQuad2(mb, RD_WING, root - fw * 0.03f, root + fw * 0.03f, tp + fw * 0.02f, tp - fw * 0.05f);
            }
        }
    }

    // бабочка: тельце и два крыла «ёлочкой» (sub = цвет крыльев)
    static void RvButterfly(MB mb, Vector3 p, float yaw, int wingSub)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fw = q * Vector3.forward, sd = q * Vector3.right;
        LkBeam(mb, RD_BARK, p - fw * 0.07f, p + fw * 0.07f, 0.03f);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 tp = p + sd * (sg * 0.17f) + new Vector3(0f, 0.11f, 0f);
            RvQuad2(mb, wingSub, p + fw * 0.03f, p - fw * 0.05f, tp - fw * 0.07f, tp + fw * 0.08f);
        }
    }

    // кольцо ряби на воде (прерывистое)
    static void RvRipple(MB mb, Vector2 p, float y, float rad, System.Random rnd)
    {
        const int n = 14;
        const float w = 0.05f;
        for (int k = 0; k < n; k++)
        {
            if (Rf(rnd) < 0.3f) continue;
            float a0 = k * 6.2832f / n, a1 = (k + 0.8f) * 6.2832f / n;
            Vector3 i0 = new Vector3(p.x + Mathf.Cos(a0) * rad, y, p.y + Mathf.Sin(a0) * rad);
            Vector3 i1 = new Vector3(p.x + Mathf.Cos(a1) * rad, y, p.y + Mathf.Sin(a1) * rad);
            Vector3 o0 = new Vector3(p.x + Mathf.Cos(a0) * (rad + w), y, p.y + Mathf.Sin(a0) * (rad + w));
            Vector3 o1 = new Vector3(p.x + Mathf.Cos(a1) * (rad + w), y, p.y + Mathf.Sin(a1) * (rad + w));
            mb.Quad(RD_FOAM, i0, i1, o1, o0, Vector3.up);
        }
    }

    // утка на воде: kind 0 = селезень (зелёная голова), 1 = утка (коричневая), 2 = утёнок (жёлтый); sc = масштаб
    static void RvDuck(MB mb, Vector2 p, float y, Vector2 fw2, float sc, int kind, System.Random rnd)
    {
        Vector3 f3 = new Vector3(fw2.x, 0f, fw2.y).normalized;
        Quaternion q = Quaternion.LookRotation(f3, Vector3.up);
        Vector3 fw = f3, sd = q * Vector3.right, up = Vector3.up;
        Vector3 c0 = new Vector3(p.x, y, p.y);
        int body = kind == 0 ? RD_DUCKG : (kind == 1 ? RD_DUCKB : RD_DUCKY);
        int head = kind == 0 ? RD_DUCKH : (kind == 1 ? RD_DUCKB : RD_DUCKY);
        LkBox(mb, body, c0 + up * (0.06f * sc), new Vector3(0.14f, 0.11f, 0.26f) * sc, q);
        LkBox(mb, body, c0 - fw * (0.27f * sc) + up * (0.13f * sc), new Vector3(0.07f, 0.03f, 0.1f) * sc, q * Quaternion.Euler(20f, 0f, 0f));
        LkBox(mb, body, c0 + fw * (0.2f * sc) + up * (0.22f * sc), new Vector3(0.055f, 0.1f, 0.055f) * sc, q);
        LkBox(mb, head, c0 + fw * (0.25f * sc) + up * (0.36f * sc), new Vector3(0.075f, 0.07f, 0.08f) * sc, q);
        LkBox(mb, RD_BEAK, c0 + fw * (0.35f * sc) + up * (0.34f * sc), new Vector3(0.04f, 0.015f, 0.06f) * sc, q);
        if (kind < 2)
            for (int sg = -1; sg <= 1; sg += 2)
                LkBox(mb, RD_DUCKB, c0 + sd * (sg * 0.145f * sc) + up * (0.11f * sc) - fw * (0.04f * sc), new Vector3(0.025f, 0.07f, 0.17f) * sc, q);
        RvRipple(mb, p, y + 0.025f, 0.38f * sc + 0.1f, rnd);
    }

    // рыбацкие мостки: настил с досками, две балки, сваи, верёвочные перила, ведро и удочка с поплавком.
    // Стоят на пологом берегу, дальний конец уходит в воду. Возвращает false, если подходящего места не нашлось.
    static bool RvJetty(Layout L, Field F, MB mb, Func<Vector2, float> Hy, System.Random rnd, List<float> avoid)
    {
        var rv = L.river;
        for (int tries = 0; tries < 90; tries++)
        {
            float s = 20f + Rf(rnd) * (rv.lipS - 55f);
            if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 18f) continue;
            bool far = true;
            foreach (float ps in avoid) if (Mathf.Abs(ps - s) < 14f) { far = false; break; }
            if (!far) continue;
            float side = Rf(rnd) < 0.5f ? -1f : 1f;
            if (!RvGentle(L, F, s, side)) continue;
            int bi = rv.Index(s);
            Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
            Vector2 outD = -nr * side;
            float wall = rv.Wall(s, side > 0f);
            float jk0 = RvShoreK(rv, Hy, s, side);
            Vector2 st = RvBank(rv, s, side, jk0 + 0.15f);
            if (!RvLandOk(L, F, st) || !RvLandOk(L, F, RvBank(rv, s, side, Mathf.Min(1.2f, jk0 + 0.5f)))) continue;
            float len = 0.15f * wall + 4.2f;
            Vector2 en = st + outD * len;
            float wy = rv.WaterY(s);
            if (RiverDist(L, en) > -2f || InIslet(L, en, 1.8f) || Hy(en) > wy - 0.25f) continue;
            bool free = true;
            foreach (var ob in L.obst) if (Vector2.Distance(ob.pos, en) < ob.r + 1.6f) { free = false; break; }
            if (!free) continue;

            float yD = Mathf.Max(wy + 0.5f, Hy(st) + 0.15f);
            Vector3 o3 = new Vector3(outD.x, 0f, outD.y);
            Quaternion q = Quaternion.LookRotation(o3, Vector3.up);
            Vector2 lat = new Vector2(-outD.y, outD.x);
            Func<Vector2, float, Vector3> P3 = (v2, hh) => new Vector3(v2.x, hh, v2.y);

            // настил
            int np = Mathf.Max(6, Mathf.RoundToInt(len / 0.36f));
            float pl = len / np;
            for (int k = 0; k < np; k++)
            {
                Vector2 pc = st + outD * ((k + 0.5f) * pl);
                int sub = (k % 3 == 2) ? RD_BARK : RD_PLANK;
                LkBox(mb, sub, P3(pc, yD + (Rf(rnd) - 0.5f) * 0.03f), new Vector3(0.68f, 0.035f, pl * 0.46f), q);
            }
            // балки под настилом
            for (int sg = -1; sg <= 1; sg += 2)
                LkBeam(mb, RD_BARK, P3(st + lat * (sg * 0.5f) - outD * 0.15f, yD - 0.1f), P3(en + lat * (sg * 0.5f) + outD * 0.1f, yD - 0.1f), 0.1f);
            // сваи и верёвочные перила
            float[] tp = { 0.04f, 0.5f, 0.96f };
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector3 prev = Vector3.zero;
                for (int j = 0; j < tp.Length; j++)
                {
                    Vector2 pp = st + outD * (len * tp[j]) + lat * (sg * 0.72f);
                    float top = yD + (j == 2 ? 0.8f : 0.6f);
                    float bot = Mathf.Min(Hy(pp), wy) - 0.3f;
                    LkBeam(mb, RD_BARK, P3(pp, bot), P3(pp, top), 0.13f);
                    LkBox(mb, RD_PLANK, P3(pp, top + 0.02f), new Vector3(0.09f, 0.03f, 0.09f), Quaternion.identity);
                    Vector3 cur = P3(pp, top - 0.1f);
                    if (j > 0)
                    {
                        Vector3 mid = (prev + cur) * 0.5f - new Vector3(0f, 0.1f, 0f);
                        LkBeam(mb, RD_ROPE, prev, mid, 0.035f);
                        LkBeam(mb, RD_ROPE, mid, cur, 0.035f);
                    }
                    prev = cur;
                }
            }
            // ведро у дальнего конца
            Vector2 bk = en - outD * 0.55f + lat * 0.38f;
            LkBox(mb, RD_BARK, P3(bk, yD + 0.18f), new Vector3(0.14f, 0.15f, 0.14f), q);
            LkBox(mb, RD_ROPE, P3(bk, yD + 0.34f), new Vector3(0.11f, 0.015f, 0.11f), q);
            // удочка: от настила над водой, леска и красный поплавок
            Vector3 rb = P3(en - outD * 0.7f - lat * 0.4f, yD + 0.1f);
            Vector3 rt = rb + o3 * 1.9f + new Vector3(0f, 1.1f, 0f);
            LkBeam(mb, RD_BARK, rb, rt, 0.03f);
            Vector3 bob = new Vector3(rt.x + o3.x * 0.2f, wy + 0.04f, rt.z + o3.z * 0.2f);
            LkBeam(mb, RD_ROPE, rt, bob, 0.012f);
            LkBox(mb, RD_RED, bob, new Vector3(0.05f, 0.05f, 0.05f), q);
            RvRipple(mb, new Vector2(bob.x, bob.z), wy + 0.025f, 0.2f, rnd);
            avoid.Add(s);
            return true;
        }
        return false;
    }

    // весь береговой декор одним мешем (подмеши RD_*)
    static Mesh BuildRiverDecor(Layout L, Field F)
    {
        var c = L.c; float ws = c.worldScale; var rv = L.river;
        var mb = new MB(RdSubs);
        var rnd = new System.Random(c.seed * 61 + 17);
        Func<Vector2, float> Hy = hp => F.g.At(F.H, hp.x, hp.y);
        var flowerPts = new List<Vector3>();   // где стоят цветы (над ними летают бабочки)
        var reedPts = new List<Vector3>();     // где растёт камыш (над ним летают стрекозы)
        var logS = new List<float>();          // s бревён и мостков, чтобы не лепить рядом

        if (rv != null && c.props)
        {
            // ---- дуги травы с цветами на пологих участках
            for (float s = 14f; s < rv.lipS - 18f; s += 9f + Rf(rnd) * 7f)
            {
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 10f) continue;
                if (Rf(rnd) < 0.35f) continue;
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                if (!RvGentle(L, F, s, side)) continue;
                float arcLen = 4.5f + Rf(rnd) * 3.5f;
                float kk = 0.45f + Rf(rnd) * 0.3f;
                kk = Mathf.Max(kk, RvShoreK(rv, Hy, s, side) + 0.2f);
                int cnt = Mathf.Max(4, Mathf.RoundToInt(arcLen / 0.7f));
                for (int n = 0; n < cnt; n++)
                {
                    float sv = s + n * (arcLen / cnt);
                    float kv = kk + 0.18f * Mathf.Sin(n * 0.9f) + (Rf(rnd) - 0.5f) * 0.1f;
                    Vector2 pt = RvBank(rv, sv, side, kv);
                    if (!RvLandOk(L, F, pt) || RiverDist(L, pt) < 0.3f) continue;
                    RvGrassTuft(mb, pt, Hy(pt), rnd);
                    if (Rf(rnd) < 0.5f)
                    {
                        RvFlower(mb, pt + new Vector2((Rf(rnd) - 0.5f) * 0.5f, (Rf(rnd) - 0.5f) * 0.5f), Hy(pt), rnd);
                        flowerPts.Add(new Vector3(pt.x, Hy(pt), pt.y));
                    }
                }
            }

            // ---- песчаные отмели с камешками
            int shoals = 0;
            for (float s = 22f; s < rv.lipS - 22f && shoals < 6; s += 24f + Rf(rnd) * 14f)
            {
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 12f) continue;
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                if (!RvGentle(L, F, s, side)) continue;
                float sk0 = RvShoreK(rv, Hy, s, side);
                Vector2 cen = RvBank(rv, s, side, sk0 - 0.05f);
                if (!RvLandOk(L, F, RvBank(rv, s, side, Mathf.Max(0.8f, sk0 + 0.3f))) || InIslet(L, cen, 2f)) continue;
                int bi = rv.Index(s);
                Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
                float rx = 1.8f + Rf(rnd) * 1.2f, rz = 1.0f + Rf(rnd) * 0.5f;
                RvSand(mb, Hy, cen, fl, nr, rx, rz, rnd);
                int pn = 4 + rnd.Next(0, 4);
                for (int k = 0; k < pn; k++)
                {
                    Vector2 pp = cen + fl * ((Rf(rnd) * 2f - 1f) * rx * 0.8f) + nr * ((Rf(rnd) * 2f - 1f) * rz * 0.8f);
                    float dia = 0.12f + Rf(rnd) * 0.18f;
                    AddBoulder(mb, RD_PEBBLE, pp, Hy(pp) + dia * 0.15f, dia, rnd, 1f);
                }
                shoals++;
            }
        }

        // ---- 2-3 упавших бревна у воды
        if (rv != null && c.riverLogs && rv.lipS > 50f)
        {
            int want = 3, got = 0;
            for (int tries = 0; tries < 120 && got < want; tries++)
            {
                float s = 18f + Rf(rnd) * (rv.lipS - 40f);
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 16f) continue;
                bool far = true;
                foreach (float ps in logS) if (Mathf.Abs(ps - s) < 24f) { far = false; break; }
                if (!far) continue;
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                Vector2 cen = RvBank(rv, s, side, RvShoreK(rv, Hy, s, side) + 0.1f);
                if (!RvLandOk(L, F, RvBank(rv, s, side, 1f)) || InIslet(L, cen, 1.5f)) continue;
                int bi = rv.Index(s);
                Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
                // ось: в основном вдоль берега, дальний конец уходит в воду
                Vector2 ax = (fl * ((Rf(rnd) < 0.5f ? 1f : -1f) * 0.8f) + nr * (-side * (0.3f + Rf(rnd) * 0.4f))).normalized;
                float len = 3.2f + Rf(rnd) * 1.0f, rad = 0.25f + Rf(rnd) * 0.09f;
                Vector2 pa = cen - ax * (len * 0.5f), pb = cen + ax * (len * 0.5f);
                if (!RvLandOk(L, F, pa)) continue;
                float wy = rv.WaterY(s);
                float ya = Mathf.Max(Hy(pa) + rad * 0.85f, wy - rad * 0.3f);
                float yb = Mathf.Max(Hy(pb) + rad * 0.85f, wy - rad * 0.3f);
                RvLog(mb, new Vector3(pa.x, ya, pa.y), new Vector3(pb.x, yb, pb.y), rad, rnd);
                logS.Add(s); got++;
            }
        }

        // ---- кувшинки в плёсах (где вода шире обычного)
        if (rv != null && c.props && c.buildWater)
        {
            int made = 0;
            for (float s = 10f; s < rv.lipS - 12f && made < 10; s += 2.3f)
            {
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 9f) continue;
                if (Rf(rnd) > 0.3f) continue;
                float hw = rv.HW(s);
                if (hw < c.riverWidth * 0.5f * 0.97f) continue;
                int bi = rv.Index(s);
                Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                Vector2 cen = rv.p[bi] + nr * (side * hw * (0.45f + 0.35f * Rf(rnd)));
                float wy = rv.WaterY(s);
                if (RiverDist(L, cen) > -0.8f || Hy(cen) > wy - 0.22f || InIslet(L, cen, 1.8f)) continue;
                bool free = true;
                foreach (var ob in L.obst) if (Vector2.Distance(ob.pos, cen) < ob.r + 1.4f) { free = false; break; }
                if (!free) continue;
                int pads = 1 + rnd.Next(0, 3);
                for (int k = 0; k < pads; k++)
                {
                    Vector2 p2 = cen + new Vector2((Rf(rnd) - 0.5f) * 1.6f, (Rf(rnd) - 0.5f) * 1.6f);
                    RvLily(mb, p2, wy + 0.03f, 0.35f + Rf(rnd) * 0.3f, Rf(rnd) < 0.6f, rnd);
                }
                made++;
            }
        }

        // ---- рыбы-силуэты под водой у островков (против течения, чуть ниже поверхности)
        if (rv != null && c.riverFish && c.buildWater)
        {
            for (int k = 0; k < L.islets.Count; k++)
            {
                var il = L.islets[k];
                Vector2 perp = new Vector2(-il.dir.y, il.dir.x);
                float wy = il.topY - 0.55f;
                for (int fi = 0; fi < 2; fi++)
                    for (int tries = 0; tries < 14; tries++)
                    {
                        float alng = (Rf(rnd) < 0.7f ? -1f : 0.7f) * (il.r * 1.4f + 0.8f + Rf(rnd) * 1.6f);
                        float lat = (Rf(rnd) < 0.5f ? -1f : 1f) * (il.r * 0.85f + 0.5f + Rf(rnd) * 1.2f);
                        Vector2 pos = il.pos + il.dir * alng + perp * lat;
                        if (RiverDist(L, pos) > -0.7f || Hy(pos) > wy - 0.2f || InIslet(L, pos, 0.7f)) continue;
                        Vector2 fw = RvRot(-il.dir, (Rf(rnd) - 0.5f) * 0.9f);
                        RvFish(mb, pos, wy - 0.07f, fw, 0.75f + Rf(rnd) * 0.35f);
                        break;
                    }
            }
        }

        // ---- камыш и рогоз у самой кромки воды
        if (rv != null && c.riverReeds && c.props)
        {
            for (float s = 9f; s < rv.lipS - 14f; s += 5f + Rf(rnd) * 6f)
            {
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 8f) continue;
                if (Rf(rnd) < 0.4f) continue;
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                float kk = RvShoreK(rv, Hy, s, side) - 0.05f + Rf(rnd) * 0.2f;
                int clusters = 3 + rnd.Next(0, 3);
                for (int n = 0; n < clusters; n++)
                {
                    float sv = s + n * (0.9f + Rf(rnd) * 0.6f);
                    Vector2 pt = RvBank(rv, sv, side, kk + (Rf(rnd) - 0.5f) * 0.12f);
                    float wy = rv.WaterY(sv), gy = Hy(pt);
                    if (gy > wy + 0.45f || gy < wy - 0.5f) continue;
                    if (!L.outline.Inside(pt.x, pt.y) || InIslet(L, pt, 0.8f) || RiverDist(L, pt) < -1.2f) continue;
                    bool free = true;
                    foreach (var ob in L.obst) if (Vector2.Distance(ob.pos, pt) < ob.r + 0.5f) { free = false; break; }
                    if (!free) continue;
                    RvReeds(mb, pt, gy, rnd, 4 + rnd.Next(0, 5));
                    reedPts.Add(new Vector3(pt.x, wy, pt.y));
                }
            }
        }

        // ---- мокрая галька вдоль уреза воды
        if (rv != null && c.props)
        {
            for (float s = 8f; s < rv.lipS - 10f; s += 3f + Rf(rnd) * 4f)
            {
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 7f) continue;
                float side = Rf(rnd) < 0.5f ? -1f : 1f;
                if (!RvGentle(L, F, s, side)) continue;
                float pk0 = RvShoreK(rv, Hy, s, side);
                int pn = 3 + rnd.Next(0, 4);
                for (int k = 0; k < pn; k++)
                {
                    Vector2 pp = RvBank(rv, s + (Rf(rnd) - 0.5f) * 2.2f, side, pk0 - 0.1f + Rf(rnd) * 0.22f);
                    if (!L.outline.Inside(pp.x, pp.y) || InIslet(L, pp, 0.4f)) continue;
                    float dia = 0.08f + Rf(rnd) * 0.16f;
                    AddBoulder(mb, RD_PEBBLE, pp, Hy(pp) + dia * 0.15f, dia, rnd, 1f);
                }
            }
        }

        // ---- галька на дне на перекатах (видна сквозь мелкую воду)
        if (rv != null && c.props && c.buildWater)
        {
            foreach (float fs in rv.FoamSpots())
                for (int k = 0; k < 16; k++)
                {
                    float s = fs + (Rf(rnd) - 0.5f) * 6f;
                    int bi = rv.Index(s);
                    Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
                    Vector2 pp = rv.p[bi] + nr * ((Rf(rnd) * 2f - 1f) * rv.HW(s) * 0.85f);
                    float wy = rv.WaterY(s), bed = Hy(pp);
                    if (bed > wy - 0.05f || bed < wy - 0.8f || InIslet(L, pp, 0.5f)) continue;
                    float dia = 0.15f + Rf(rnd) * 0.25f;
                    AddBoulder(mb, RD_PEBBLE, pp, bed + dia * 0.15f, dia, rnd, 1f);
                }
        }

        // ---- стрекозы над камышом и бабочки над цветами
        if (rv != null && c.riverLife)
        {
            int dn = Mathf.Min(5, reedPts.Count);
            for (int k = 0; k < dn; k++)
            {
                Vector3 rp = reedPts[rnd.Next(0, reedPts.Count)];
                RvDragonfly(mb, new Vector3(rp.x + (Rf(rnd) - 0.5f) * 1.2f, rp.y + 0.7f + Rf(rnd) * 0.8f, rp.z + (Rf(rnd) - 0.5f) * 1.2f), Rf(rnd) * 360f);
            }
            int bn = Mathf.Min(6, flowerPts.Count);
            for (int k = 0; k < bn; k++)
            {
                Vector3 fp = flowerPts[rnd.Next(0, flowerPts.Count)];
                float pick = Rf(rnd);
                int sub = pick < 0.4f ? RD_FPINK : (pick < 0.75f ? RD_FYEL : RD_FWHITE);
                RvButterfly(mb, new Vector3(fp.x + (Rf(rnd) - 0.5f) * 0.8f, fp.y + 0.7f + Rf(rnd) * 0.6f, fp.z + (Rf(rnd) - 0.5f) * 0.8f), Rf(rnd) * 360f, sub);
            }
        }

        // ---- утиные семьи в плёсах: селезень, утка и три утёнка цепочкой
        if (rv != null && c.riverLife && c.buildWater)
        {
            int fams = 0;
            var famS = new List<float>();
            for (int tries = 0; tries < 100 && fams < 2; tries++)
            {
                float s = 16f + Rf(rnd) * (rv.lipS - 45f);
                if (L.bridge != null && Mathf.Abs(s - rv.bridgeS) < 12f) continue;
                bool far = true;
                foreach (float ps in famS) if (Mathf.Abs(ps - s) < 18f) { far = false; break; }
                if (!far || rv.HW(s) < c.riverWidth * 0.5f * 0.95f) continue;
                int bi = rv.Index(s);
                Vector2 fl = rv.Dir(bi), nr = new Vector2(-fl.y, fl.x);
                Vector2 pos = rv.p[bi] + nr * ((Rf(rnd) * 2f - 1f) * rv.HW(s) * 0.35f);
                float wy = rv.WaterY(s);
                if (RiverDist(L, pos) > -2.5f || Hy(pos) > wy - 0.3f || InIslet(L, pos, 2.2f)) continue;
                bool free = true;
                foreach (var ob in L.obst) if (Vector2.Distance(ob.pos, pos) < ob.r + 2.2f) { free = false; break; }
                if (!free) continue;
                Vector2 fw = RvRot(-fl, (Rf(rnd) - 0.5f) * 0.5f);
                Vector2 sd = new Vector2(-fw.y, fw.x);
                RvDuck(mb, pos, wy, fw, 1f, 0, rnd);
                RvDuck(mb, pos + sd * 0.75f - fw * 0.25f, wy, fw, 0.92f, 1, rnd);
                for (int j = 0; j < 3; j++)
                {
                    Vector2 dp = pos + sd * 0.75f - fw * (0.95f + 0.45f * j) + sd * ((Rf(rnd) - 0.5f) * 0.25f);
                    if (RiverDist(L, dp) > -1f || Hy(dp) > wy - 0.15f) continue;
                    RvDuck(mb, dp, wy, RvRot(fw, (Rf(rnd) - 0.5f) * 0.4f), 0.42f, 2, rnd);
                }
                famS.Add(s); fams++;
            }
        }

        // ---- рыбацкие мостки с ведром и удочкой
        if (rv != null && c.riverJetty && rv.lipS > 60f)
            RvJetty(L, F, mb, Hy, rnd, logS);

        // ---- мокрые валуны по обе стороны от водопада с края острова
        if (rv != null && c.riverMist)
        {
            Vector2 ld = rv.lipDir, lnr = new Vector2(-ld.y, ld.x);
            float lhw = rv.HW(rv.s[rv.p.Count - 2]) + 0.3f;
            for (int sg = -1; sg <= 1; sg += 2)
                for (int j = 0; j < 3; j++)
                {
                    Vector2 pos = rv.lip - ld * (0.2f + j * 1.5f + Rf(rnd) * 0.6f) + lnr * (sg * (lhw + 0.5f + Rf(rnd) * 1.1f));
                    if (!L.outline.Inside(pos.x, pos.y)) continue;
                    float dia = 0.7f + Rf(rnd) * 0.8f;
                    AddBoulder(mb, RD_PEBBLE, pos, Hy(pos) + dia * 0.18f, dia, rnd, 1f);
                }
        }

        // ---- брызги: белые комки пены вдоль падающей с края воды
        if (rv != null && c.riverMist)
        {
            Vector2 d = rv.lipDir, nr = new Vector2(-d.y, d.x);
            float tl = rv.s[rv.p.Count - 2];
            float y0 = rv.WaterY(tl);
            float hwl = rv.HW(tl) + 0.3f;
            for (int k = 0; k < 16; k++)
            {
                float u = k < 5 ? 0.02f + Rf(rnd) * 0.1f : 0.12f + Rf(rnd) * 0.88f;
                // та же дуга, что у листа воды (BuildLipFallV2 в LocationBuilderTester.Falls.cs)
                float flen = Mathf.Max(4f, c.fallLength);
                float outD = -0.25f + (Mathf.Clamp(0.14f * flen, 1.8f, 4.2f) + 0.25f) * u;
                float drop = flen * u * u;
                float wk = hwl * (1f + 0.28f * Mathf.SmoothStep(0f, 1f, u));
                Vector2 pos = rv.lip + d * (outD + 0.3f + Rf(rnd) * 0.5f) + nr * ((Rf(rnd) * 2f - 1f) * wk);
                float dia = (0.18f + Rf(rnd) * 0.25f) * (1f + u);
                AddBoulder(mb, RD_FOAM, pos, y0 - drop + (k < 5 ? 0.1f : 0.1f + Rf(rnd) * 0.6f), dia, rnd, 1f);
            }
        }

        for (int vi = 0; vi < mb.v.Count; vi++) mb.v[vi] = mb.v[vi] * ws;
        return mb.Build("LocationTest_RiverDecor");
    }

    // материалы в порядке подмешей RD_*: кора, торец, трава, три цветка, песок, галька, лист кувшинки, цветок кувшинки, рыба, пена,
// камыш, рогоз, утка (коричневая), селезень (тело), голова селезня, утёнок, клюв, крыло, стрекоза, доски мостков, верёвка, красное (поплавок)
    static Material[] MakeRiverDecorMats(Cfg c, Texture2D[] tex)
    {
        if (c.cardboard)
        {
            float lt = c.tileM * 0.5f, pt = c.tileM * 0.6f;
            Color wd = c.lookoutWood;
            return new[]
            {
                MakeCardMat(c, "River_Bark", new Color(wd.r * 0.8f, wd.g * 0.68f, wd.b * 0.58f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "River_LogEnd", new Color(1.4f, 1.2f, 0.9f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "River_Grass", new Color(0.55f, 1.1f, 0.40f), tex[0], c.tileM * 0.5f, 0f, 0f, false),
                MakeCardMat(c, "River_FlowerPink", new Color(1.7f, 0.70f, 0.95f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_FlowerYellow", new Color(1.7f, 1.45f, 0.45f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_FlowerWhite", new Color(1.6f, 1.55f, 1.5f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Sand", new Color(1.1f, 1.0f, 0.88f), tex[6], c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "River_Pebble", new Color(0.95f, 0.92f, 1.0f), tex[4], c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "River_Lily", new Color(0.50f, 1.05f, 0.42f), tex[0], c.tileM * 0.5f, 0f, 0f, false),
                MakeCardMat(c, "River_LilyFlower", new Color(1.7f, 1.3f, 1.5f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Fish", new Color(0.12f, 0.45f, 0.60f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Foam", new Color(1.6f, 1.65f, 1.7f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Reed", new Color(0.62f, 1.0f, 0.38f), tex[0], c.tileM * 0.5f, 0f, 0f, false),
                MakeCardMat(c, "River_Cattail", new Color(0.62f, 0.36f, 0.22f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_DuckBrown", new Color(0.85f, 0.62f, 0.38f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_DuckBody", new Color(1.25f, 1.2f, 1.1f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_DuckHead", new Color(0.15f, 0.75f, 0.45f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Duckling", new Color(1.7f, 1.5f, 0.5f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Beak", new Color(1.7f, 0.95f, 0.35f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Wing", new Color(1.5f, 1.6f, 1.7f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Dragonfly", new Color(0.2f, 1.1f, 1.2f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Plank", new Color(wd.r * 1.05f, wd.g * 1.0f, wd.b * 0.9f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "River_Rope", new Color(1.3f, 1.15f, 0.8f), _paperTex, pt, 0f, 0f, false),
                MakeCardMat(c, "River_Red", new Color(1.8f, 0.3f, 0.25f), _paperTex, pt, 0f, 0f, false)
            };
        }
        return new[]
        {
            MakeMat("River_Bark", new Color(0.42f, 0.28f, 0.16f), false, 0f),
            MakeMat("River_LogEnd", new Color(0.78f, 0.62f, 0.40f), false, 0f),
            MakeMat("River_Grass", new Color(0.30f, 0.65f, 0.25f), false, 0f),
            MakeMat("River_FlowerPink", new Color(1f, 0.55f, 0.75f), false, 0f),
            MakeMat("River_FlowerYellow", new Color(1f, 0.85f, 0.30f), false, 0f),
            MakeMat("River_FlowerWhite", new Color(0.98f, 0.97f, 0.95f), false, 0f),
            MakeMat("River_Sand", new Color(0.95f, 0.85f, 0.60f), false, 0f),
            MakeMat("River_Pebble", new Color(0.62f, 0.58f, 0.72f), false, 0f),
            MakeMat("River_Lily", new Color(0.30f, 0.62f, 0.25f), false, 0f),
            MakeMat("River_LilyFlower", new Color(1f, 0.80f, 0.88f), false, 0f),
            MakeMat("River_Fish", new Color(0.10f, 0.38f, 0.50f), false, 0f),
            MakeMat("River_Foam", new Color(0.95f, 0.98f, 1f), false, 0f),
            MakeMat("River_Reed", new Color(0.40f, 0.62f, 0.25f), false, 0f),
            MakeMat("River_Cattail", new Color(0.45f, 0.27f, 0.16f), false, 0f),
            MakeMat("River_DuckBrown", new Color(0.55f, 0.40f, 0.25f), false, 0f),
            MakeMat("River_DuckBody", new Color(0.80f, 0.78f, 0.72f), false, 0f),
            MakeMat("River_DuckHead", new Color(0.10f, 0.50f, 0.30f), false, 0f),
            MakeMat("River_Duckling", new Color(1f, 0.85f, 0.30f), false, 0f),
            MakeMat("River_Beak", new Color(1f, 0.60f, 0.15f), false, 0f),
            MakeMat("River_Wing", new Color(0.85f, 0.92f, 0.98f), false, 0f),
            MakeMat("River_Dragonfly", new Color(0.10f, 0.70f, 0.80f), false, 0f),
            MakeMat("River_Plank", new Color(0.70f, 0.52f, 0.32f), false, 0f),
            MakeMat("River_Rope", new Color(0.78f, 0.68f, 0.45f), false, 0f),
            MakeMat("River_Red", new Color(0.90f, 0.18f, 0.15f), false, 0f)
        };
    }
}
#endif

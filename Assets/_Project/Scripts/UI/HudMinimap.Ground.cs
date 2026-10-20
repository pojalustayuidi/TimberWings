// Assets/_Project/Scripts/UI/HudMinimap.Ground.cs (НЕ в Editor)
// Земля карты. Раньше землю снимала камера карты: у Terrain подменялся materialTemplate на плоский Unlit. У террейна
// из LocationBuilder включён Draw Instanced, и с чужим шейдером он в кадр не попадает, а запасная «основа острова»
// (DrawMesh) лежала ниже воды и пропадала. В итоге остров на карте был голубым, как море.
//
// Теперь земля рисуется кодом, по данным TerrainData: цвета слоёв раскраски (трава, тропа, площадка, пляж, лес),
// плюс подсветка рельефа по карте высот (холмы, обрывы тёмнее/светлее). Всё один раз при старте, в фоновом потоке (без фриза).
// Камера карты рисует поверх только объекты (вода, мосты, здания, лес, камни) на прозрачном фоне, а MinimapPost
// подкладывает под них эту текстуру. Террейн на время печати просто выключается (drawHeightmap = false).
using System.Collections;
using UnityEngine;

public partial class HudMinimap
{
    [Header("Земля карты (из Terrain)")]
    [Tooltip("Рисовать землю кодом по данным Terrain. Нужен шейдер MinimapPost (подкладывает землю под объекты)")]
    public bool paintGround = true;
    [Tooltip("Размер текстуры земли в пикселях. Рисуется один раз в фоновом потоке")]
    public int groundResolution = 768;
    [Tooltip("Насколько заметен рельеф (холмы, обрывы). 0 = плоско")]
    [Range(0f, 1.5f)] public float reliefStrength = 0.7f;
    [Tooltip("Крутые склоны подкрашиваются под камень")]
    [Range(0f, 1f)] public float cliffRock = 0.45f;

    Texture2D groundTex;
    bool groundReady;
    Vector3 groundCenter;
    float groundSide = -1f;

    class TerrainSrc
    {
        public Vector3 pos, size;
        public float[,] h; public int hr;
        public float[,,] am; public int ar, layers;
        public Color[] colors;
    }

    public bool GroundReady { get { return groundReady && groundTex != null; } }

    static Color LayerColor(TerrainLayer l, Color fallback)
    {
        if (l == null) return fallback;
        var tex = l.diffuseTexture as Texture2D;
        if (tex != null)
        {
            try { return tex.GetPixel(tex.width / 2, tex.height / 2); }   // слои LocationBuilder: плоские цвета 4x4
            catch { }                                                     // текстура нечитаемая (импорт): берём цвет по имени
        }
        return fallback;
    }

    Color LayerFallback(string name)
    {
        string n = string.IsNullOrEmpty(name) ? "" : name.ToLowerInvariant();
        if (n.Contains("trail") || n.Contains("path")) return palette.sand;
        if (n.Contains("pad")) return Color.Lerp(palette.sand, palette.grass, 0.35f);
        if (n.Contains("beach") || n.Contains("sand")) return palette.sand;
        if (n.Contains("forest")) return Color.Lerp(palette.forestA, palette.grass, 0.35f);
        return palette.grass;
    }

    TerrainSrc[] CollectTerrains()
    {
        var list = new System.Collections.Generic.List<TerrainSrc>();
        foreach (var t in Terrain.activeTerrains)
        {
            if (t == null || t.terrainData == null) continue;
            var td = t.terrainData;
            var s = new TerrainSrc();
            s.pos = t.transform.position; s.size = td.size;
            s.hr = td.heightmapResolution;
            s.h = td.GetHeights(0, 0, s.hr, s.hr);
            var layers = td.terrainLayers;
            s.layers = layers != null ? layers.Length : 0;
            s.ar = td.alphamapResolution;
            if (s.layers > 0 && td.alphamapLayers == s.layers) s.am = td.GetAlphamaps(0, 0, s.ar, s.ar);
            s.colors = new Color[Mathf.Max(1, s.layers)];
            for (int i = 0; i < s.colors.Length; i++)
            {
                var l = i < s.layers ? layers[i] : null;
                s.colors[i] = LayerColor(l, LayerFallback(l != null ? l.name : null));
            }
            if (s.am == null) s.layers = 0;
            list.Add(s);
        }
        return list.ToArray();
    }

    static float Bil2(float[,] a, int n, float u, float v)
    {
        float fx = Mathf.Clamp01(u) * (n - 1), fy = Mathf.Clamp01(v) * (n - 1);
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Mathf.Min(x0 + 1, n - 1), y1 = Mathf.Min(y0 + 1, n - 1);
        float tx = fx - x0, ty = fy - y0;
        return Mathf.Lerp(Mathf.Lerp(a[y0, x0], a[y0, x1], tx), Mathf.Lerp(a[y1, x0], a[y1, x1], tx), ty);
    }

    static Color SampleSplat(TerrainSrc s, float u, float v, Color fallback)
    {
        if (s.layers == 0) return fallback;
        float gx = Mathf.Clamp(u * s.ar - 0.5f, 0f, s.ar - 1), gy = Mathf.Clamp(v * s.ar - 0.5f, 0f, s.ar - 1);
        int x0 = (int)gx, y0 = (int)gy;
        int x1 = Mathf.Min(x0 + 1, s.ar - 1), y1 = Mathf.Min(y0 + 1, s.ar - 1);
        float tx = gx - x0, ty = gy - y0;
        float r = 0f, g = 0f, b = 0f, ws = 0f;
        for (int k = 0; k < s.layers; k++)
        {
            float w = Mathf.Lerp(Mathf.Lerp(s.am[y0, x0, k], s.am[y0, x1, k], tx), Mathf.Lerp(s.am[y1, x0, k], s.am[y1, x1, k], tx), ty);
            if (w <= 0f) continue;
            Color c = s.colors[k];
            r += c.r * w; g += c.g * w; b += c.b * w; ws += w;
        }
        if (ws < 0.001f) return fallback;
        return new Color(r / ws, g / ws, b / ws, 1f);
    }

    static bool InPoly(Vector2[] poly, float x, float z)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    // Всё, что нужно для расчёта пикселей без обращения к объектам Unity (расчёт идёт в фоновом потоке)
    class GroundJob
    {
        public int n;
        public float cx, cz, side;
        public Vector3 axRight, axUp, light;
        public Vector2[] poly;
        public TerrainSrc[] src;
        public Color grass, rock;
        public Color32 ocean;
        public float relief, cliff;
        public Color32[] px;
    }

    // Строки y0..y1-1. Только математика и чтение массивов: можно из любого потока
    static void PaintRows(GroundJob j, int y0, int y1)
    {
        int n = j.n;
        float flatLit = j.light.y;
        for (int y = y0; y < y1; y++)
        {
            float my = ((y + 0.5f) / n - 0.5f) * j.side;
            for (int x = 0; x < n; x++)
            {
                float mx = ((x + 0.5f) / n - 0.5f) * j.side;
                float wx = j.cx + j.axRight.x * mx + j.axUp.x * my;
                float wz = j.cz + j.axRight.z * mx + j.axUp.z * my;
                if (j.poly != null && j.poly.Length >= 3 && !InPoly(j.poly, wx, wz)) { j.px[y * n + x] = j.ocean; continue; }

                Color col = j.grass;
                for (int si = 0; si < j.src.Length; si++)
                {
                    var s = j.src[si];
                    float u = (wx - s.pos.x) / s.size.x, v = (wz - s.pos.z) / s.size.z;
                    if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                    col = SampleSplat(s, u, v, j.grass);

                    float e = 1f / (s.hr - 1);
                    float hL = Bil2(s.h, s.hr, u - e, v), hR = Bil2(s.h, s.hr, u + e, v);
                    float hD = Bil2(s.h, s.hr, u, v - e), hU = Bil2(s.h, s.hr, u, v + e);
                    float dx = (hR - hL) * s.size.y / (2f * e * s.size.x);
                    float dz = (hU - hD) * s.size.y / (2f * e * s.size.z);
                    var nrm = new Vector3(-dx, 1f, -dz).normalized;
                    float k = Mathf.Clamp(1f + (Vector3.Dot(nrm, j.light) - flatLit) * j.relief * 1.6f, 0.72f, 1.3f);
                    float rockT = Mathf.SmoothStep(0.12f, 0.35f, 1f - nrm.y) * j.cliff;
                    if (rockT > 0f) col = Color.Lerp(col, j.rock, rockT);
                    col *= k;
                    break;
                }
                col *= 0.96f + 0.08f * VNoise(wx * 0.25f, wz * 0.25f);       // лёгкая неровность цвета, чтобы земля не была плакатом
                j.px[y * n + x] = new Color32((byte)(Mathf.Clamp01(col.r) * 255f), (byte)(Mathf.Clamp01(col.g) * 255f), (byte)(Mathf.Clamp01(col.b) * 255f), 255);
            }
        }
    }

    // Рисует текстуру земли в системе координат карты (center, axRight, axUp, side): тот же кадр, что у камеры карты.
    // Расчёт идёт в фоновом потоке (на WebGL потоков нет: там кусочками по кадрам), игра не тормозит.
    IEnumerator EnsureGround()
    {
        if (!paintGround || !stylized) yield break;
        if (groundReady && groundTex != null && Mathf.Abs(groundSide - side) < 0.01f && (groundCenter - center).sqrMagnitude < 0.0001f) yield break;
        groundReady = false;

        int n = Mathf.Clamp(groundResolution, 256, 2048);
        if (groundTex != null && groundTex.width != n) { Destroy(groundTex); groundTex = null; }
        if (groundTex == null)
            groundTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "MinimapGround", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };

        var job = new GroundJob();
        job.n = n; job.cx = center.x; job.cz = center.z; job.side = side;
        job.axRight = axRight; job.axUp = axUp;
        Vector3 L = axRight * -0.55f + axUp * 0.75f; L.y = 0.85f;                    // свет сверху-слева (на экране)
        job.light = L.normalized;
        job.poly = islandPoly;
        job.grass = palette.grass; job.rock = palette.rock;
        Color32 ocean = palette.waterDeep; ocean.a = 255; job.ocean = ocean;
        job.relief = reliefStrength; job.cliff = cliffRock;
        job.px = new Color32[n * n];
        job.src = CollectTerrains();
        float cSide = side; Vector3 cCenter = center;
        yield return null;                                               // GetHeights/GetAlphamaps уже отняли время: кадр отдыха

        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            var task = System.Threading.Tasks.Task.Run(() => PaintRows(job, 0, n));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); yield break; }
        }
        else
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int y = 0; y < n; y += 8)
            {
                PaintRows(job, y, Mathf.Min(n, y + 8));
                if (sw.Elapsed.TotalMilliseconds > sliceMs * 2f) { yield return null; sw.Reset(); sw.Start(); }
            }
        }

        if (groundTex == null) yield break;                              // HUD уничтожен, пока считали
        groundTex.SetPixels32(job.px);
        groundTex.Apply(false);
        groundCenter = cCenter; groundSide = cSide;
        groundReady = true;
    }
}

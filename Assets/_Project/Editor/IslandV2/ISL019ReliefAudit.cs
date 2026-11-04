// Assets/_Project/Editor/IslandV2/ISL019ReliefAudit.cs
// ISL-019: read-only relief and terrain-collider audit for the existing IslandLayout.
// Does NOT change IslandLayout, heights, TerrainData, meshes, scene objects or materials.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslandV2
{
    public static class ISL019ReliefAudit
    {
        private const float GridStep = 4f;
        private const float PathSampleStep = 2f;
        private const float ColliderTolerance = 0.35f; // Terrain heightmap interpolation differs from mesh sampling
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        private struct Cell
        {
            public float x, z, h, slope;
            public int kind; // 0 = unprotected dry land; 1 = route; 2 = anchor; 3 = river/pond/bank; 4 = edge
            public bool inside;
        }

        private struct Candidate
        {
            public float x, z, h, slope;
        }

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/ISL-019/1. Аудит высот (без изменений)", false, 187)]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("ISL-019: выйди из Play Mode перед аудитом.");
                return;
            }
            IslandLayout layout = AssetDatabase.LoadAssetAtPath<IslandLayout>(IslandLayout.AssetPath);
            if (layout == null)
            {
                Debug.LogError("ISL-019: не найден существующий " + IslandLayout.AssetPath + ". Ничего не создавалось.");
                return;
            }
            IslandField field = new IslandField(layout);
            string rootPath = Directory.GetParent(Application.dataPath).FullName;
            string txtPath = Path.Combine(rootPath, "ISL-019_ReliefAudit.txt");
            string csvPath = Path.Combine(rootPath, "ISL-019_ReliefGrid.csv");
            string pngPath = Path.Combine(rootPath, "ISL-019_ReliefPreview.png");
            StringBuilder report = new StringBuilder(12000);
            StringBuilder csv = new StringBuilder(200000);
            Scene scene = SceneManager.GetActiveScene();
            report.AppendLine("TIMBER WINGS / ISL-019 / RELIEF AUDIT (READ ONLY)");
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Unity: " + Application.unityVersion);
            report.AppendLine("Scene: " + scene.path + " (" + scene.name + ")");
            report.AppendLine("Layout asset: " + IslandLayout.AssetPath);
            report.AppendLine("All heights in world units; assumed meters, not independently calibrated.");
            report.AppendLine("WARNING: this report does not prove navigation, water physics, save-state safety or FPS.");
            if (scene.path.IndexOf("_Recovery", StringComparison.OrdinalIgnoreCase) >= 0)
                report.AppendLine("WARNING: active scene is under _Recovery. Verify that it is your actual playable scene.");

            report.AppendLine("\n[1] SAVED LAYOUT VALUES (NOT CODE DEFAULTS)");
            report.AppendLine("seed=" + layout.seed + "; hillAmp=" + F(layout.hillAmp) + "; hillScale=" + F(layout.hillScale)
                + "; pathFlatten=" + F(layout.pathFlatten) + "; flatBlend=" + F(layout.flatBlend));
            report.AppendLine("maxWalkSlopeDeg=" + F(layout.maxWalkSlopeDeg) + "; meshCell=" + F(layout.meshCell)
                + "; terrainResolution=" + layout.terrainResolution + "; terrainBaseY=" + F(layout.terrainBaseY)
                + "; terrainHeightRange=" + F(layout.terrainHeightRange));
            report.AppendLine("Paths=" + layout.paths.Count + "; anchors=" + layout.anchors.Count
                + "; meadows=" + layout.meadows.Count + "; crossings=" + layout.crossings.Count);
            report.AppendLine("Contour area=" + F(field.Area) + "; bounds X[" + F(field.Min.x) + "," + F(field.Max.x)
                + "] Z[" + F(field.Min.y) + "," + F(field.Max.y) + "]");

            int cols = Mathf.CeilToInt((field.Max.x - field.Min.x) / GridStep);
            int rows = Mathf.CeilToInt((field.Max.y - field.Min.y) / GridStep);
            cols = Mathf.Clamp(cols, 1, 512);
            rows = Mathf.Clamp(rows, 1, 512);
            Cell[] cells = new Cell[cols * rows];
            csv.AppendLine("x,z,height,slope_deg,category,near_path_edge_m,near_water_m,near_island_edge_m");
            int landCount = 0, openCount = 0, openSteep = 0, protectedCount = 0;
            float allMin = float.MaxValue, allMax = float.MinValue;
            float naturalMin = float.MaxValue, naturalMax = float.MinValue;
            float allSlopeMax = 0f, openSlopeMax = 0f;
            List<Candidate> candidates = new List<Candidate>();

            for (int j = 0; j < rows; j++)
            {
                float z = field.Min.y + (j + 0.5f) * (field.Max.y - field.Min.y) / rows;
                for (int i = 0; i < cols; i++)
                {
                    float x = field.Min.x + (i + 0.5f) * (field.Max.x - field.Min.x) / cols;
                    Cell cell = new Cell { x = x, z = z, inside = field.Inside(x, z) };
                    if (!cell.inside) { cells[j * cols + i] = cell; continue; }
                    cell.h = field.Height(x, z);
                    cell.slope = field.Slope(x, z);
                    float pd = field.PathDistance(x, z);
                    float wd = field.WaterDistance(x, z);
                    float ed = field.EdgeDistance(x, z);
                    cell.kind = Classify(field, x, z, pd, wd, ed);
                    cells[j * cols + i] = cell;
                    landCount++;
                    allMin = Mathf.Min(allMin, cell.h);
                    allMax = Mathf.Max(allMax, cell.h);
                    allSlopeMax = Mathf.Max(allSlopeMax, cell.slope);
                    if (cell.kind == 0)
                    {
                        openCount++;
                        naturalMin = Mathf.Min(naturalMin, cell.h);
                        naturalMax = Mathf.Max(naturalMax, cell.h);
                        openSlopeMax = Mathf.Max(openSlopeMax, cell.slope);
                        if (cell.slope > layout.maxWalkSlopeDeg) openSteep++;
                        if (cell.slope < Mathf.Min(18f, layout.maxWalkSlopeDeg * 0.75f))
                            candidates.Add(new Candidate { x = x, z = z, h = cell.h, slope = cell.slope });
                    }
                    else protectedCount++;
                    csv.Append(F(x)).Append(',').Append(F(z)).Append(',').Append(F(cell.h)).Append(',')
                        .Append(F(cell.slope)).Append(',').Append(KindName(cell.kind)).Append(',')
                        .Append(F(pd)).Append(',').Append(F(wd)).Append(',').Append(F(ed)).AppendLine();
                }
            }
            report.AppendLine("\n[2] HEIGHT GRID (sampling ~" + F(GridStep) + " m)");
            report.AppendLine("Inside cells=" + landCount + "; open natural=" + openCount + "; protected=" + protectedCount);
            report.AppendLine("Height all terrain min/max=" + Range(allMin, allMax)
                + "; maximum sampled slope=" + F(allSlopeMax) + " deg");
            report.AppendLine("Height OPEN LAND min/max=" + Range(naturalMin, naturalMax)
                + "; maximum sampled slope=" + F(openSlopeMax) + " deg");
            report.AppendLine("Open-land samples above maxWalkSlopeDeg=" + openSteep + " / " + openCount
                + " (not equivalent to navigation failures)");
            report.AppendLine("Protected means within 3 m of a path edge, 6 m of water, 5 m of island edge,");
            report.AppendLine("or within anchor.flatRadius + flatBlend + 2 m. Protected zones MUST NOT be reshaped casually.");

            report.AppendLine("\n[3] ANCHOR HEIGHTS AND PAD TRANSITIONS");
            Transform markers = null;
            GameObject islandRoot = IslandBlockout.FindRoot();
            if (islandRoot != null) markers = islandRoot.transform.Find(IslandBlockout.MarkersName);
            for (int a = 0; a < layout.anchors.Count; a++)
            {
                IslandLayout.Anchor an = layout.anchors[a];
                Vector2 p = field.AnchorWorld(an);
                float h = field.Height(p.x, p.y);
                float hMin = h, hMax = h, slopeMax = field.Slope(p.x, p.y);
                if (an.flatRadius > 0f)
                {
                    float inner = an.flatRadius * 0.75f;
                    float blend = an.flatRadius + Mathf.Max(0.5f, layout.flatBlend) * 0.5f;
                    for (int k = 0; k < 12; k++)
                    {
                        float ang = k * (Mathf.PI * 2f / 12);
                        foreach (float r in new[] { inner, blend })
                        {
                            float x = p.x + Mathf.Cos(ang) * r, z = p.y + Mathf.Sin(ang) * r;
                            if (!field.Inside(x, z) || field.IsWater(x, z)) continue;
                            float yh = field.Height(x, z);
                            hMin = Mathf.Min(hMin, yh); hMax = Mathf.Max(hMax, yh);
                            slopeMax = Mathf.Max(slopeMax, field.Slope(x, z));
                        }
                    }
                }
                string mark = "not found";
                if (markers != null)
                {
                    Transform tr = markers.Find(an.name);
                    if (tr != null)
                        mark = "XZ offset=" + F(Vector2.Distance(new Vector2(tr.position.x, tr.position.z), p))
                            + ", Y delta=" + F(tr.position.y - h);
                }
                report.AppendLine(an.name + " x=" + F(p.x) + " z=" + F(p.y) + " height=" + F(h)
                    + " nearby dry Y=" + Range(hMin, hMax) + " maxSlope=" + F(slopeMax)
                    + " marker: " + mark);
            }

            report.AppendLine("\n[4] ROUTE CENTRE-LINE SLOPES (SAMPLED)");
            for (int i = 0; i < layout.paths.Count; i++)
            {
                Vector2[] poly = field.PathPolyline(i);
                int n = 0, steep = 0, wet = 0;
                float lo = float.MaxValue, hi = float.MinValue, ms = 0f;
                Vector2 worst = Vector2.zero;
                for (int s = 0; s + 1 < poly.Length; s++)
                {
                    Vector2 a = poly[s], b = poly[s + 1];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / PathSampleStep));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, (k + 0.5f) / steps);
                        if (!field.Inside(p.x, p.y)) continue;
                        if (field.IsWater(p.x, p.y)) { wet++; continue; }
                        float h = field.Height(p.x, p.y), slope = field.Slope(p.x, p.y);
                        n++; lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                        if (slope > ms) { ms = slope; worst = p; }
                        if (slope > layout.maxWalkSlopeDeg) steep++;
                    }
                }
                report.AppendLine(layout.paths[i].name + ": drySamples=" + n + ", height=" + Range(lo, hi)
                    + ", maxSlope=" + F(ms) + " at (" + F(worst.x) + "," + F(worst.y) + ")"
                    + ", overLimit=" + steep + ", river/pondSamples=" + wet);
            }

            report.AppendLine("\n[5] SCENE TERRAIN COLLIDER SPOT-CHECK");
            TerrainCollider tc = null;
            if (islandRoot != null)
            {
                Transform generated = islandRoot.transform.Find(IslandBlockout.GeneratedName);
                if (generated != null)
                {
                    Transform t = generated.Find(IslandBlockout.TerrainName);
                    if (t != null) tc = t.GetComponent<TerrainCollider>();
                }
            }
            if (tc == null || tc.terrainData == null || !tc.enabled)
            {
                report.AppendLine("No usable IslandV2/_Generated/Ground_Terrain/TerrainCollider found. Scene geometry comparison skipped.");
            }
            else
            {
                int count = 0, missed = 0, bad = 0;
                float maxError = 0f;
                foreach (IslandLayout.Anchor a in layout.anchors)
                {
                    Vector2 p = field.AnchorWorld(a);
                    if (!field.Inside(p.x, p.y) || field.IsWater(p.x, p.y)) continue;
                    count++;
                    RaycastHit hit;
                    // Raycast on the specific collider, not Physics.Raycast, to ignore props and foliage.
                    if (!tc.Raycast(new Ray(new Vector3(p.x, 120f, p.y), Vector3.down), out hit, 300f))
                    { missed++; report.AppendLine("  MISS at " + a.name); continue; }
                    float err = Mathf.Abs(hit.point.y - field.Height(p.x, p.y));
                    maxError = Mathf.Max(maxError, err);
                    if (err > ColliderTolerance)
                    { bad++; report.AppendLine("  DELTA " + a.name + " = " + F(err) + " m"); }
                }
                report.AppendLine("anchorChecks=" + count + "; rayMisses=" + missed + "; above "
                    + F(ColliderTolerance) + " m=" + bad + "; maxAbsError=" + F(maxError) + " m");
                report.AppendLine("Errors may indicate stale TerrainData or sampling resolution; inspect before rebuilding anything.");
            }

            candidates.Sort((a, b) => b.h.CompareTo(a.h));
            report.AppendLine("\n[6] OPTIONAL SCENIC-VIEW CANDIDATES (HEIGHT ONLY, NOT LINE OF SIGHT)");
            List<Candidate> chosen = new List<Candidate>();
            foreach (Candidate c in candidates)
            {
                bool near = false;
                foreach (Candidate p in chosen)
                    if ((new Vector2(p.x, p.z) - new Vector2(c.x, c.z)).sqrMagnitude < 18f * 18f) { near = true; break; }
                if (near) continue;
                chosen.Add(c);
                report.AppendLine("  X=" + F(c.x) + ", Z=" + F(c.z) + ", groundY=" + F(c.h) + ", slope=" + F(c.slope) + " deg");
                if (chosen.Count == 8) break;
            }
            report.AppendLine("These spots are only candidates. Trees, buildings, camera angle and interaction areas are NOT evaluated.");
            report.AppendLine("\n[7] NEXT STEPS");
            report.AppendLine("1. Compare Game View from central camp, forest, river/bridge, and a distant edge viewpoint.");
            report.AppendLine("2. Run the existing IslandCheck separately; this report is NOT a passability test.");
            report.AppendLine("3. Do not run full IslandV2 rebuild or edit hillAmp until collider, pad, routes and placed objects are accounted for.");
            report.AppendLine("4. Any later geometry change must rebuild BOTH visual chunks and TerrainCollider and re-check gameplay.");
            report.AppendLine("Files written outside Assets: TXT, CSV, PNG. Scene/assets modified: NONE.");

            try
            {
                File.WriteAllText(txtPath, report.ToString(), new UTF8Encoding(false));
                File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
                CreatePreview(cells, cols, rows, naturalMin, naturalMax, pngPath);
                Debug.Log("ISL-019: аудит высот готов (без изменения сцены):\n" + txtPath + "\n" + csvPath + "\n" + pngPath
                    + "\nOpen-land samples: " + openCount + ", steep: " + openSteep);
            }
            catch (Exception ex)
            {
                Debug.LogError("ISL-019: не удалось сохранить файлы аудита: " + ex);
            }
        }

        private static int Classify(IslandField f, float x, float z, float path, float water, float edge)
        {
            if (f.IsWater(x, z) || water < 6f) return 3;
            if (path < 3f) return 1;
            for (int i = 0; i < f.L.anchors.Count; i++)
            {
                IslandLayout.Anchor a = f.L.anchors[i];
                if (a.flatRadius <= 0f) continue;
                if (Vector2.Distance(new Vector2(x, z), f.AnchorWorld(a))
                    < a.flatRadius + Mathf.Max(0.5f, f.L.flatBlend) + 2f) return 2;
            }
            if (edge < 5f) return 4;
            return 0;
        }

        private static string KindName(int kind)
        {
            switch (kind)
            {
                case 1: return "path_protected";
                case 2: return "anchor_protected";
                case 3: return "waterbank_protected";
                case 4: return "edge_protected";
                default: return "open_dry_land";
            }
        }

        private static void CreatePreview(Cell[] grid, int cols, int rows, float min, float max, string filepath)
        {
            const int pixelSize = 9;
            Texture2D png = new Texture2D(cols * pixelSize, rows * pixelSize, TextureFormat.RGBA32, false);
            try
            {
                Color[] pixels = new Color[cols * rows * pixelSize * pixelSize];
                Color grassLow = new Color(0.39f, 0.74f, 0.60f);
                Color grassHigh = new Color(0.95f, 0.72f, 0.42f);
                Color outside = new Color(0.13f, 0.16f, 0.23f);
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++)
                    {
                        Cell c = grid[j * cols + i];
                        Color color = outside;
                        if (c.inside)
                        {
                            float t = max > min ? Mathf.Clamp01((c.h - min) / (max - min)) : 0.5f;
                            color = Color.Lerp(grassLow, grassHigh, t);
                            switch (c.kind)
                            {
                                case 1: color = new Color(0.97f, 0.87f, 0.69f); break; // paths
                                case 2: color = new Color(0.94f, 0.65f, 0.72f); break; // anchors
                                case 3: color = new Color(0.33f, 0.68f, 0.89f); break; // water buffer
                                case 4: color = Color.Lerp(color, new Color(0.55f, 0.57f, 0.67f), 0.5f); break;
                            }
                            if (c.kind == 0 && c.slope > 30f) color = new Color(0.82f, 0.23f, 0.36f);
                        }
                        for (int py = 0; py < pixelSize; py++)
                            for (int px = 0; px < pixelSize; px++)
                            {
                                int imageWidth = cols * pixelSize;
                                int imageX = i * pixelSize + px, imageY = j * pixelSize + py;
                                pixels[imageY * imageWidth + imageX] = color;
                            }
                    }
                png.SetPixels(pixels); png.Apply(false, false);
                File.WriteAllBytes(filepath, png.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(png); }
        }

        private static string F(float x) { return x.ToString("0.###", Ci); }
        private static string Range(float lo, float hi)
        {
            return lo == float.MaxValue || hi == float.MinValue ? "n/a" : F(lo) + " .. " + F(hi) + " m";
        }
    }
}

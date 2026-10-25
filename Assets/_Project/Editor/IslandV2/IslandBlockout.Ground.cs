// Assets/_Project/Editor/IslandV2/IslandBlockout.Ground.cs
// Остров v2, этап 1: земля. Часть partial-класса IslandBlockout (остальные части: IslandBlockout.cs, .GroundTex.cs, .CandyGround.cs, .Props.cs ...).
// Файл восстановлен: в исходном архиве его не было, а на него ссылаются IslandBlockout.cs (BuildGround) и IslandBlockout.GroundTex.cs (GroundColorSrgb).
//
// Методы:
//   BuildGround(field, gen, mat, out chunks, out tris)   невидимый Terrain-коллайдер + видимые меш-чанки земли
//   BuildTerrain(field, gen)                             Ground_Terrain: Terrain с TerrainCollider, drawHeightmap = false, дыры вне контура острова
//   BuildChunks(field, gen, mat, out chunks, out tris)   Ground_Chunk_X_Z: меш земли клеткой meshCell, чанк = chunkCells x chunkCells клеток
//   GroundColorSrgb(field, x, z)                         старая раскраска земли по полям IslandLayout (нужна, если в IslandCandy снята галка candyOn)
//
// Контракт (его проверяет IslandCheck): ровно один Terrain под _Generated, drawHeightmap выключен, есть TerrainCollider;
// есть меши Ground_Chunk_*; на метках высота коллайдера = field.Height; за контуром на 1.5 м земли нет, внутри на 1.5 м есть.
// Альфа вершин земли = 0: шейдер берёт цвет из текстуры _GroundTex по мировым XZ (запекание: IslandBlockout.GroundTex.cs).
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static partial class IslandBlockout
    {
        public const string TerrainName = "Ground_Terrain";
        public const string ChunkPrefix = "Ground_Chunk_";

        static void BuildGround(IslandField f, Transform gen, Material mat, out int chunks, out int tris)
        {
            BuildTerrain(f, gen);
            BuildChunks(f, gen, mat, out chunks, out tris);
        }

        // ================================================================ невидимый Terrain-коллайдер
        static void BuildTerrain(IslandField f, Transform gen)
        {
            var L = f.L;
            float margin = Mathf.Max(0f, L.terrainMargin);
            float x0 = f.Min.x - margin, z0 = f.Min.y - margin;
            float sizeX = (f.Max.x - f.Min.x) + margin * 2f, sizeZ = (f.Max.y - f.Min.y) + margin * 2f;
            float range = Mathf.Max(1f, L.terrainHeightRange);
            float baseY = L.terrainBaseY;

            // разрешение карты высот: степень двойки плюс 1
            int res = Mathf.ClosestPowerOfTwo(Mathf.Clamp(L.terrainResolution, 33, 4097) - 1) + 1;

            IslandLayout.EnsureFolder(IslandLayout.AssetFolder + "/Generated");
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainDataPath);
            }
            td.heightmapResolution = res;   // сначала разрешение (оно сбрасывает карту высот), потом размер
            td.size = new Vector3(sizeX, range, sizeZ);

            var heights = new float[res, res];
            int hres = td.holesResolution;
            var holes = new bool[hres, hres];
            try
            {
                for (int j = 0; j < res; j++)
                {
                    if ((j & 15) == 0 && EditorUtility.DisplayCancelableProgressBar("Остров v2: Terrain-коллайдер", "Карта высот " + res + "x" + res, (float)j / res))
                        break;
                    float z = z0 + (float)j / (res - 1) * sizeZ;
                    for (int i = 0; i < res; i++)
                    {
                        float x = x0 + (float)i / (res - 1) * sizeX;
                        heights[j, i] = Mathf.Clamp01((f.Height(x, z) - baseY) / range);
                    }
                }
                // дыры: true = земля есть, false = дыра (по документации Unity). Клетка твёрдая, если её центр внутри контура
                for (int j = 0; j < hres; j++)
                {
                    float z = z0 + (j + 0.5f) / hres * sizeZ;
                    for (int i = 0; i < hres; i++)
                    {
                        float x = x0 + (i + 0.5f) / hres * sizeX;
                        bool solid = f.Inside(x, z);
                        holes[j, i] = L.terrainHolesInverted ? !solid : solid;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            td.SetHeights(0, 0, heights);
            td.SetHoles(0, 0, holes);
            EditorUtility.SetDirty(td);

            var tgo = Terrain.CreateTerrainGameObject(td);
            tgo.name = TerrainName;
            Undo.RegisterCreatedObjectUndo(tgo, "Create " + TerrainName);
            tgo.transform.SetParent(gen, false);
            tgo.transform.position = new Vector3(x0, baseY, z0);

            var t = tgo.GetComponent<Terrain>();
            t.drawHeightmap = false;            // невидим: землю рисуют меш-чанки
            t.drawInstanced = false;
            t.drawTreesAndFoliage = false;
            t.allowAutoConnect = false;
            t.shadowCastingMode = ShadowCastingMode.Off;
            var tc = tgo.GetComponent<TerrainCollider>();
            if (tc == null) tc = tgo.AddComponent<TerrainCollider>();
            tc.terrainData = td;
            AssetDatabase.SaveAssets();
        }

        // ================================================================ видимые меш-чанки
        static void BuildChunks(IslandField f, Transform gen, Material mat, out int chunks, out int tris)
        {
            chunks = 0; tris = 0;
            var L = f.L;
            float cell = Mathf.Max(0.5f, L.meshCell);
            int cc = Mathf.Clamp(L.chunkCells, 8, 64);

            // сетка вершин с запасом в одну клетку вокруг габарита контура
            float ox = f.Min.x - cell, oz = f.Min.y - cell;
            int nx = Mathf.CeilToInt((f.Max.x - ox) / cell) + 1;
            int nz = Mathf.CeilToInt((f.Max.y - oz) / cell) + 1;
            int vw = nx + 1, vh = nz + 1;

            var pos = new Vector3[vw * vh];
            var nrm = new Vector3[vw * vh];
            var ins = new bool[vw * vh];
            float e = cell * 0.5f;
            for (int j = 0; j < vh; j++)
            {
                for (int i = 0; i < vw; i++)
                {
                    float x = ox + i * cell, z = oz + j * cell;
                    bool inside = f.Inside(x, z);
                    if (!inside)
                    {
                        // вершины снаружи прижимаем к контуру: край меша ровно по контуру острова
                        float d;
                        Vector2 q = f.NearestOnContour(x, z, out d);
                        x = q.x; z = q.y;
                    }
                    int k = j * vw + i;
                    ins[k] = inside;
                    pos[k] = new Vector3(x, f.Height(x, z), z);
                    float dx = (f.Height(x + e, z) - f.Height(x - e, z)) / (2f * e);
                    float dz = (f.Height(x, z + e) - f.Height(x, z - e)) / (2f * e);
                    nrm[k] = new Vector3(-dx, 1f, -dz).normalized;
                }
            }

            int chunksX = Mathf.CeilToInt((float)nx / cc), chunksZ = Mathf.CeilToInt((float)nz / cc);
            var remap = new int[vw * vh];
            for (int cz = 0; cz < chunksZ; cz++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    for (int k = 0; k < remap.Length; k++) remap[k] = -1;
                    var verts = new System.Collections.Generic.List<Vector3>();
                    var norms = new System.Collections.Generic.List<Vector3>();
                    var cols = new System.Collections.Generic.List<Color>();
                    var idx = new System.Collections.Generic.List<int>();

                    int i0 = cx * cc, i1 = Mathf.Min(nx, i0 + cc);
                    int j0 = cz * cc, j1 = Mathf.Min(nz, j0 + cc);
                    for (int j = j0; j < j1; j++)
                    {
                        for (int i = i0; i < i1; i++)
                        {
                            int a = j * vw + i, b = (j + 1) * vw + i, c = (j + 1) * vw + i + 1, d = j * vw + i + 1;
                            if (!ins[a] && !ins[b] && !ins[c] && !ins[d]) continue;   // клетка целиком снаружи
                            // диагональ чередуем, чтобы не было перекоса рельефа в одну сторону
                            if (((i + j) & 1) == 0)
                            {
                                AddGroundTri(f, pos, nrm, remap, verts, norms, cols, idx, a, b, c);
                                AddGroundTri(f, pos, nrm, remap, verts, norms, cols, idx, a, c, d);
                            }
                            else
                            {
                                AddGroundTri(f, pos, nrm, remap, verts, norms, cols, idx, a, b, d);
                                AddGroundTri(f, pos, nrm, remap, verts, norms, cols, idx, b, c, d);
                            }
                        }
                    }
                    if (idx.Count == 0) continue;

                    string name = ChunkPrefix + cx + "_" + cz;
                    var mesh = new Mesh { name = name };
                    if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(verts);
                    mesh.SetNormals(norms);
                    mesh.SetColors(cols);
                    mesh.SetTriangles(idx, 0);
                    mesh.RecalculateBounds();
                    NewMeshObject(name, gen, mesh, mat, true);
                    chunks++;
                    tris += idx.Count / 3;
                }
            }
        }

        static void AddGroundTri(IslandField f, Vector3[] pos, Vector3[] nrm, int[] remap,
            System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> norms,
            System.Collections.Generic.List<Color> cols, System.Collections.Generic.List<int> idx, int a, int b, int c)
        {
            // вырожденные и перевёрнутые (после прижима к контуру) треугольники не нужны: лицо должно смотреть вверх
            Vector3 n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            if (n.y <= 1e-5f) return;
            idx.Add(GroundVert(f, pos, nrm, remap, verts, norms, cols, a));
            idx.Add(GroundVert(f, pos, nrm, remap, verts, norms, cols, b));
            idx.Add(GroundVert(f, pos, nrm, remap, verts, norms, cols, c));
        }

        static int GroundVert(IslandField f, Vector3[] pos, Vector3[] nrm, int[] remap,
            System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> norms,
            System.Collections.Generic.List<Color> cols, int k)
        {
            if (remap[k] >= 0) return remap[k];
            int n = verts.Count;
            verts.Add(pos[k]);
            norms.Add(nrm[k]);
            // альфа 0 = «земля»: шейдер берёт цвет из текстуры по мировым XZ. RGB запасной (линейный), на вид не влияет
            Color lin = GroundColorSrgb(f, pos[k].x, pos[k].z).linear;
            cols.Add(new Color(lin.r, lin.g, lin.b, 0f));
            remap[k] = n;
            return n;
        }

        // ================================================================ старая раскраска земли (без Candy)
        // Цвет в sRGB по полям IslandLayout: трава тремя пятнами, лес, тропа с каймой, площадки, берег, брод, стены русла, кромка, вода.
        static Color GroundColorSrgb(IslandField f, float x, float z)
        {
            var L = f.L;

            // трава: три зелёных пятнами по двум шумам
            float sc = Mathf.Max(6f, L.grassPatchScale);
            float n1 = Mathf.PerlinNoise(x / sc + 7.1f, z / sc + 3.3f);
            float n2 = Mathf.PerlinNoise(x / (sc * 0.4f) + 21.7f, z / (sc * 0.4f) + 9.4f);
            float n = Mathf.Clamp01(0.5f + (n1 * 0.75f + n2 * 0.25f - 0.5f) * L.grassPatchContrast);
            float soft = Mathf.Max(0.005f, L.grassPatchSoft);
            float wDeep = 1f - IslandField.Step(L.grassDeepBelow - soft, L.grassDeepBelow + soft, n);
            float wLight = IslandField.Step(L.grassLightAbove - soft, L.grassLightAbove + soft, n);
            Color c = Color.Lerp(L.grassMid, L.grassDeep, wDeep);
            c = Color.Lerp(c, L.grassLight, wLight);
            float ripple = 1f + (Mathf.PerlinNoise(x * 1.15f + 41.3f, z * 1.15f + 17.9f) - 0.5f) * 2f * L.groundVariation;
            c = CScale(c, ripple);

            // лес
            c = Color.Lerp(c, L.forestFloor, f.ForestWeight(x, z) * L.forestPaintStrength);

            // влажный берег у воды
            float wd = f.WaterDistance(x, z);
            if (wd > 0f && wd < L.shoreWidth)
                c = Color.Lerp(c, L.shoreWet, 1f - IslandField.Step(0f, L.shoreWidth, wd));

            // площадки зданий
            float pad = 0f;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.kind != IslandLayout.AnchorKind.Building) continue;
                if (a.name == "CAMPFIRE_Point") continue;
                pad = Mathf.Max(pad, 1f - IslandField.Step(0.3f, 2.4f, f.RectDistance(a, x, z)));
            }
            c = Color.Lerp(c, L.padDirt, pad * L.padStrength);

            // тропа: песок, снаружи кайма; на развилках расширяется
            float d1, d2;
            f.PathEdgeNearest(x, z, out d1, out d2);
            float junction = 1f - IslandField.Step(0f, Mathf.Max(0.1f, L.pathJunctionReach), Mathf.Max(0f, d2));
            float edge = d1 - L.pathJunctionWiden * junction;
            float cover = 1f - IslandField.Step(-0.15f, 0.15f, edge);
            float rimW = 1f - IslandField.Step(0f, Mathf.Max(0.1f, L.pathRimWidth), Mathf.Max(0f, edge));
            c = Color.Lerp(c, L.pathRim, rimW * L.pathRimStrength * (1f - cover));
            c = Color.Lerp(c, L.pathSand, cover);

            // круглая площадка у костра
            float pd = Vector2.Distance(new Vector2(x, z), f.PlazaCenter);
            float R = Mathf.Max(1f, L.plazaRadius);
            float ring = IslandField.Step(R - L.plazaRimWidth - 0.2f, R - L.plazaRimWidth + 0.15f, pd);
            Color stone = Color.Lerp(L.plazaStone, L.plazaRim, ring);
            c = Color.Lerp(c, stone, 1f - IslandField.Step(R - 0.15f, R + 0.2f, pd));

            // брод и стены русла
            float rdist = f.RiverDistance(x, z);
            float half = L.riverWidth * 0.5f;
            float fw = f.FordWeight(x, z);
            float inRiver = 1f - IslandField.Step(half - 0.5f, half + 0.5f, rdist);
            c = Color.Lerp(c, L.fordSand, inRiver * fw);
            if (rdist < half + 16f && rdist > half * 0.85f)
            {
                float wallW = IslandField.Step(24f, 42f, f.Slope(x, z));
                if (wallW > 0.001f)
                {
                    float dy = f.Height(x, z) - f.RiverSurfaceAt(x, z);
                    Color rock = Color.Lerp(L.riverWallColor, L.riverWallWet, 1f - IslandField.Step(0.4f, 1.8f, dy));
                    c = Color.Lerp(c, rock, wallW);
                }
            }

            // кромка обрыва чуть темнее
            float ed = f.EdgeDistance(x, z);
            if (ed < L.edgeDarkWidth)
            {
                float k = 1f - IslandField.Step(0f, Mathf.Max(0.5f, L.edgeDarkWidth), Mathf.Max(0f, ed));
                c = CScale(c, 1f - L.edgeDarken * k);
            }

            // дно под водой: русло без брода и пруд
            c = Color.Lerp(c, L.waterColor, inRiver * (1f - fw));
            float pondW = 1f - IslandField.Step(0.9f, 1.08f, f.PondValue(x, z));
            c = Color.Lerp(c, L.waterColor, pondW);

            return new Color(c.r, c.g, c.b, 1f);
        }
    }
}

// Assets/_Project/Editor/IslandV2/IslandCliff.cs
// Остров v2, этап 4 (часть 2): обрыв и подбрюшье. Один меш «Cliff» под _Generated, один материал на шейдере Project/IslandRock.
//
// Как устроен: контур острова режется на колонны шириной ~cliffColumnWidth (3 м). Вниз от кромки идут «кольца» (профиль ниже),
// каждое кольцо = по точке на колонну. Между кольцами плоские грани (у каждой своя нормаль и свой цвет), поэтому вид граненый, low-poly, как на референсе.
//   кольца 0-2   травяная кромка: земля, нависающий зелёный валик, тёмный грунт под ним
//   кольца 3-5   стенка из скалы, почти вертикальная (cliffWall метров), три оттенка лаванды сверху вниз
//   кольца 6-9   подбрюшье: сужение к острию (cliffTaper метров), рваные «сталактиты» (cliffSpikes)
// Верх следует за рельефом (IslandField.Height на контуре), низ выравнивается к средней высоте края.
// Меш без коллайдера (падение с края ловит IslandEdgeGuard), тени не отбрасывает. Около 2,5k tris.
// Заодно записывает в IslandV2Data цвета скалы, глубину и материал: их читают CloudSea и подбрюшье мини-островов, чтобы цвета совпадали.
// Вырез под реку (D): в колоннах у русла нет нависающего валика, скала ровнее и «мокрая» (cliffWet); русло у края углубляет IslandField.Height (notchDrop). Метки FALL_RiverIn/Out для этапа 6.
// Все числа в IslandLayout (блоки «Обрыв и подбрюшье» и «Вырез под реку»), здесь чисел-настроек нет.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class IslandCliff
    {
        public const string ObjectName = "Cliff";
        public const string RockShaderName = "Project/IslandRock";
        public const string MaterialPath = IslandLayout.AssetFolder + "/Mat_IslandRock.mat";

        // профиль колец: смещение вниз (в метрах, считается ниже), вынос наружу, доля сужения к острию
        const int Rings = 10;

        static float N(float x, float y, int s) { return Mathf.PerlinNoise(x + s * 3.17f, y + s * 1.93f); }

        static float SignedArea(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += p[j].x * p[i].y - p[i].x * p[j].y;
            return a * 0.5f;   // плюс = против часовой (x вправо, z вверх на плане)
        }

        public static Material GetMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m != null) return m;
            var sh = Shader.Find(RockShaderName);
            if (sh == null) return null;
            IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
            m = new Material(sh) { name = "Mat_IslandRock" };
            m.SetFloat("_UseCandyGround", 0f);   // цвет только из вершин, без шейдерной травы CandyGround
            m.SetFloat("_AllGround", 0f);
            m.SetFloat("_Wrap", 0.4f);
            m.SetFloat("_Rim", 0.22f);
            m.SetColor("_Shade", new Color(0.58f, 0.52f, 0.74f, 1f));
            AssetDatabase.CreateAsset(m, MaterialPath);
            return m;
        }

        // Возвращает число треугольников. mat = материал обрыва (null, если нет шейдера)
        public static int Build(IslandField f, Transform gen, IslandV2Data data, out Material mat)
        {
            var L = f.L;
            mat = GetMaterial();
            if (mat == null)
            {
                Debug.LogWarning("IslandV2: не найден шейдер " + RockShaderName + " (Scripts/Location/IslandRock.shader). Обрыв не построен.");
                return 0;
            }

            // ---------- контур против часовой, колонны по длине
            var c = new List<Vector2>(f.Contour);
            if (SignedArea(c) < 0f) c.Reverse();
            int n = c.Count;
            float perimeter = 0f;
            for (int i = 0; i < n; i++) perimeter += Vector2.Distance(c[i], c[(i + 1) % n]);
            int m = Mathf.Clamp(Mathf.RoundToInt(perimeter / Mathf.Max(1f, L.cliffColumnWidth)), 24, 400);
            int seed = L.seed;

            var P = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                float jitter = (N(j * 0.77f, 3.3f, seed + 5) - 0.5f) * 0.5f;   // колонны разной ширины
                float s = ((j + jitter) / m) * perimeter;
                s = Mathf.Repeat(s, perimeter);
                P[j] = PointAlong(c, s);
            }
            var Nrm = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                Vector2 t = P[(j + 1) % m] - P[(j - 1 + m) % m];
                Nrm[j] = t.sqrMagnitude > 1e-6f ? new Vector2(t.y, -t.x).normalized : Vector2.up;   // наружу
            }

            // высота верха на каждой колонне и средняя (низ обрыва выравнивается к ней)
            var top = new float[m];
            float avg = 0f;
            Vector2 centre = Vector2.zero;
            for (int j = 0; j < m; j++) { top[j] = f.Height(P[j].x, P[j].y); avg += top[j]; centre += P[j]; }
            avg /= m; centre /= m;

            // вырез под реку (D): вес по колоннам, 1 в русле, 0 дальше halfWidth + notchBlend
            var notch = new float[m];
            for (int j = 0; j < m; j++) notch[j] = f.NotchWeight(P[j].x, P[j].y);

            float W = Mathf.Max(2f, L.cliffWall), U = Mathf.Max(3f, L.cliffTaper), lip = L.lipOverhang;
            float totalDepth = 1f + W + U;
            Vector2 tipXZ = centre + new Vector2((N(1.3f, 2.1f, seed + 11) - 0.5f) * 5f, (N(4.1f, 0.7f, seed + 12) - 0.5f) * 5f);

            //               0       1       2       3            4            5          6            7           8           9
            float[] dy    = { -0.05f, -0.40f, -0.95f, -(1f + 0.30f * W), -(1f + 0.62f * W), -(1f + W), -(1f + W + 0.25f * U), -(1f + W + 0.50f * U), -(1f + W + 0.75f * U), -totalDepth };
            float[] push  = { -0.12f, lip, lip * 0.3f, 0.10f, 0.00f, -0.10f, 0f, 0f, 0f, 0f };
            float[] taper = { 0f, 0f, 0f, 0.02f, 0.04f, 0.07f, 0.28f, 0.55f, 0.82f, 1f };
            float[] rough = { 0f, 0.15f, 0.25f, 0.9f, 1f, 1f, 1f, 0.9f, 0.6f, 0f };

            var V = new Vector3[Rings, m];
            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    float cut = notch[j];
                    float rj = (N(j * 0.93f + i * 5.1f, i * 3.3f + 1.7f, seed + 31) - 0.5f) * 2f * L.cliffRough * rough[i] * (1f - 0.7f * cut);   // в вырезе скала ровнее
                    float pushI = i < 3 ? Mathf.Lerp(push[i], Mathf.Min(push[i], 0f), cut) : push[i];                                         // в вырезе нет нависающего валика
                    Vector2 b = P[j] + Nrm[j] * (pushI + rj);
                    Vector2 p2 = Vector2.Lerp(b, tipXZ, taper[i]);
                    float yBase = Mathf.Lerp(top[j], avg, Mathf.Clamp01((i - 2f) / 5f));
                    float y = yBase + dy[i];
                    if (i >= 3 && i < Rings - 1)
                    {
                        y += (N(j * 0.61f + i * 2.7f, i * 1.9f, seed + 41) - 0.5f) * 0.9f * rough[i];
                        // сталактиты: часть колонн свисает ниже соседних, сильнее в подбрюшье
                        float sp = Mathf.Clamp01((N(j * 0.21f, 7.7f, seed + 21) - 0.5f) * 2.2f);
                        if (i >= 6) y -= sp * sp * L.cliffSpikes * Mathf.Sin(Mathf.PI * (i - 5f) / (Rings - 5f));
                    }
                    V[i, j] = new Vector3(p2.x, y, p2.y);
                }
            }

            // ---------- цвета колец (sRGB, как ждёт IslandRock)
            Color[] ringCol =
            {
                L.lipTop, L.lipDark, L.cliffSoil,
                L.rockTop, Color.Lerp(L.rockTop, L.rockMid, 0.55f), L.rockMid,
                Color.Lerp(L.rockMid, L.rockDark, 0.4f), Color.Lerp(L.rockMid, L.rockDark, 0.75f), L.rockDark, L.rockDark
            };

            // ---------- грани: у каждой свои вершины, нормаль и цвет (граненый вид)
            var verts = new List<Vector3>(m * Rings * 6);
            var norms = new List<Vector3>(m * Rings * 6);
            var cols = new List<Color>(m * Rings * 6);
            var uvs = new List<Vector2>(m * Rings * 6);
            var idx = new List<int>(m * Rings * 6);

            for (int i = 0; i < Rings - 1; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    int j2 = (j + 1) % m;
                    Color a = ringCol[i], b = ringCol[i + 1];
                    float v = 1f + (N(j * 0.5f + i * 3f, i * 1.3f, seed + 9) - 0.5f) * 0.24f;   // оттенок блока
                    float warm = (N(j * 0.35f, i * 2.2f, seed + 15) - 0.5f) * 0.10f;           // чуть розовее или синее
                    Color col = Color.Lerp(a, b, 0.5f);
                    float wet = notch[j] * (i < 6 ? 1f : 0.4f);   // мокрая скала в русле: сверху сильнее, подбрюшье слабее
                    if (wet > 0f) col = Color.Lerp(col, L.cliffWet, wet * 0.85f);
                    col = new Color(Mathf.Clamp01(col.r * v + warm), Mathf.Clamp01(col.g * v), Mathf.Clamp01(col.b * v - warm * 0.5f), 1f);

                    // треугольник 1: (i,j) (i,j2) (i+1,j); треугольник 2: (i,j2) (i+1,j2) (i+1,j). Порядок подобран под контур против часовой: нормаль наружу
                    AddTri(V[i, j], V[i, j2], V[i + 1, j], col, verts, norms, cols, uvs, idx);
                    AddTri(V[i, j2], V[i + 1, j2], V[i + 1, j], col, verts, norms, cols, uvs, idx);
                }
            }

            var mesh = new Mesh { name = "IslandCliff" };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create Cliff");
            go.transform.SetParent(gen, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;

            PlaceFallMarkers(f, gen);

            // данные для рантайма (CloudSea рисует клубы и мини-острова теми же цветами)
            if (data != null)
            {
                data.depth = totalDepth;
                data.grassTop = L.lipTop; data.grassDark = L.lipDark; data.soil = L.cliffSoil;
                data.rockWarm = L.rockTop; data.rockMid = L.rockMid; data.rockDark = L.rockDark;
                data.rockMaterial = mat;
                EditorUtility.SetDirty(data);
            }
            return idx.Count / 3;
        }

        // Метки под водопады (этап 6): FALL_RiverOut там, где река уходит с острова, FALL_RiverIn там, где приходит. Смотрят наружу.
        // Лежат в Markers (создаются, если нет; если есть, переставляются). Лишние метки FALL_* от прошлых сборок удаляются.
        static void PlaceFallMarkers(IslandField f, Transform gen)
        {
            var edges = f.RiverEdges;
            Transform markers = gen.parent != null ? gen.parent.Find(IslandBlockout.MarkersName) : null;
            if (markers == null) return;
            var used = new HashSet<string>();
            int nIn = 0, nOut = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                string name = e.isOutlet ? (nOut++ == 0 ? "FALL_RiverOut" : "FALL_RiverOut_" + nOut) : (nIn++ == 0 ? "FALL_RiverIn" : "FALL_RiverIn_" + nIn);
                used.Add(name);
                Transform t = markers.Find(name);
                if (t == null)
                {
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                    go.transform.SetParent(markers, false);
                    t = go.transform;
                }
                t.position = new Vector3(e.pos.x, f.Height(e.pos.x, e.pos.y), e.pos.y);
                t.rotation = Quaternion.LookRotation(new Vector3(e.outward.x, 0f, e.outward.y), Vector3.up);
                EditorUtility.SetDirty(t);
            }
            for (int i = markers.childCount - 1; i >= 0; i--)
            {
                var ch = markers.GetChild(i);
                if (ch.name.StartsWith("FALL_River") && !used.Contains(ch.name)) Undo.DestroyObjectImmediate(ch.gameObject);
            }
            if (edges.Count == 0) Debug.LogWarning("IslandV2: река не пересекает край острова, выреза под водопад нет (проверь riverPoints: концы должны выходить за контур).");
            else Debug.Log("IslandV2: вырез под реку: входов " + nIn + ", выходов " + nOut + " (метки FALL_RiverIn, FALL_RiverOut в Markers, смотрят наружу).");
        }

        static void AddTri(Vector3 a, Vector3 b, Vector3 c, Color col, List<Vector3> v, List<Vector3> nr, List<Color> cl, List<Vector2> uv, List<int> t)
        {
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-8f) return;   // вырожденный (у острия все точки сходятся)
            Vector3 nn = cr.normalized;
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            nr.Add(nn); nr.Add(nn); nr.Add(nn);
            cl.Add(col); cl.Add(col); cl.Add(col);
            uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(0f, 1f));   // x = вес травы шейдера (0 = скала), y = яркость
            t.Add(k); t.Add(k + 1); t.Add(k + 2);
        }

        static Vector2 PointAlong(List<Vector2> poly, float s)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                if (s <= len || i == n - 1) return Vector2.Lerp(a, b, len > 1e-4f ? Mathf.Clamp01(s / len) : 0f);
                s -= len;
            }
            return poly[0];
        }
    }
}

// Assets/_Project/Editor/IslandV2/IslandRiverWater.cs
// Остров v2, этап 6 (задача E): вода реки, водопад и объём воды для плавания. Зовётся из IslandBlockout.Build после обрыва.
// Один меш «River_Water» под _Generated (поверхность воды и водопад вместе), один материал на шейдере Project/IslandV2/Water, один draw call, коллайдера нет.
//
// Что строит:
//   1. Поверхность воды: лента по оси реки внутри острова. Высота берётся из IslandField.RiverSurfaceAt, поэтому вода течёт вниз вместе с руслом
//      и у кромки падает на notchDrop. Лента чуть шире воды (на 0,25 м под откос берега), поэтому у берега нет щели между водой и землёй.
//   2. Водопад: полоса вниз от порога у каждого выхода реки (FALL_RiverOut). Загибается через кромку, дальше падает вертикально в облака
//      на fallOffset метров от скалы, внизу растворяется шумом (шейдер, без прозрачности и сортировки).
//   3. Объём воды (WaterVolume, он уже есть в Scripts/Location): по нему WaterSwim решает «бредёт, плывёт, тонет». Один на кусок реки.
// Вход реки (FALL_RiverIn) водопада пока не получает: выше по течению на острове ничего нет (задача I2).
// Все числа в IslandLayout (блоки «Русло и сток реки» и «Вода реки и водопад»), здесь чисел-настроек нет, кроме шага сетки меша и формы водопада (константы ниже).
// Мультяшная версия: цельное полотно водопада (дуга через порог без ступеньки по ширине), брызги на гребне и в облаках (CandyWaterFx), три цвета воды.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class IslandRiverWater
    {
        public const string ObjectName = "River_Water";
        public const string VolumeName = "River_WaterVolume";
        public const string WaterShaderName = "Project/IslandV2/Water";
        public const string MaterialPath = IslandLayout.AssetFolder + "/Mat_IslandWater.mat";

        const float RowStep = 1.0f;        // шаг рядов ленты воды вдоль реки, метры (мельче: гладкий поворот штрихов течения на меандре)
        const float ArcDrop = 1.6f;        // на каком падении (м) струя из горизонтальной становится вертикальной: плавная дуга через порог
        const int FallCols = 11;           // колонок в полотне водопада (было 5): ровный изгиб и струи без изломов
        public const string SprayMaterialPath = IslandLayout.AssetFolder + "/Mat_IslandSpray.mat";
        const float EdgeTuck = 0.25f;      // насколько лента шире воды (уходит под откос берега), метры
        const float Lift = 0.03f;          // лента выше расчётной поверхности: нет z-fight с откосом у самой воды

        // ================================================================ материал
        public static Material GetMaterial(IslandLayout L)
        {
            var sh = Shader.Find(WaterShaderName);
            if (sh == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
                m = new Material(sh) { name = "Mat_IslandWater" };
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            else if (m.shader != sh) m.shader = sh;
            m.SetColor("_ShallowColor", L.riverShallow);
            m.SetColor("_DeepColor", L.riverDeep);
            m.SetColor("_MidColor", Color.Lerp(L.riverShallow, L.riverDeep, 0.5f));   // средняя ступень глубины (мультяшные три цвета)
            m.SetColor("_FoamColor", L.riverFoam);
            m.SetFloat("_FlowSpeed", L.riverFlowSpeed);
            m.SetFloat("_HalfWidth", Mathf.Max(0.5f, L.riverWidth * 0.5f));
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================ сборка
        // Возвращает число треугольников. 0 = ничего не построено (нет шейдера или река не заходит на остров).
        public static int Build(IslandField f, Transform gen, IslandV2Data data)
        {
            var L = f.L;
            var mat = GetMaterial(L);
            if (mat == null)
            {
                Debug.LogWarning("IslandV2: не найден шейдер " + WaterShaderName + " (файл Shaders/IslandV2/IslandWater.shader). Вода реки не построена.");
                return 0;
            }
            var pieces = f.RiverInsidePieces();
            if (pieces.Count == 0)
            {
                Debug.LogWarning("IslandV2: ось реки не заходит на остров, воды нет (проверь riverPoints).");
                return 0;
            }

            float half = L.riverWidth * 0.5f;
            var v = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>();
            var col = new List<Color>(); var idx = new List<int>();

            // водопад живёт в своём меше (прозрачный шейдер): списки отдельные
            var fv = new List<Vector3>(); var fnr = new List<Vector3>(); var fuv = new List<Vector2>();
            var fcol = new List<Color>(); var fidx = new List<int>();

            int volumes = 0;
            for (int pi = 0; pi < pieces.Count; pi++)
            {
                var axis = Resample(pieces[pi], RowStep);
                if (axis.Count < 2) continue;
                AddSurface(f, axis, half, v, nr, uv, col, idx);
                AddVolume(f, gen, Resample(pieces[pi], 3f), half, pi);
                volumes++;
            }

            // водопады: по одному на каждый выход реки
            float totalDepth = TotalCliffDepth(L, data);
            int falls = 0;
            foreach (var e in f.RiverEdges)
            {
                if (!e.isOutlet) continue;
                AddFall(f, e.pos, half, totalDepth, 0f, 1f, 1f, fv, fnr, fuv, fcol, fidx);      // передний слой
                AddFall(f, e.pos, half, totalDepth, 1f, 0.8f, 0.55f, fv, fnr, fuv, fcol, fidx); // задний слой: уже и ближе к скале
                AddSpray(f, gen, e.pos, half, totalDepth, falls);
                falls++;
            }

            if (idx.Count == 0) return 0;
            var mesh = new Mesh { name = "IslandRiverWater" };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(nr);
            mesh.SetUVs(0, uv);
            mesh.SetColors(col);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create River_Water");
            go.transform.SetParent(gen, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            int fallTris = BuildFallObject(L, gen, fv, fnr, fuv, fcol, fidx);

            Debug.Log("IslandV2: вода реки: кусков " + volumes + ", водопадов " + falls + ", Tris реки " + (idx.Count / 3) + ", водопада " + fallTris + " (River_Water + River_Fall). Вода течёт вниз на " + L.riverDrop.ToString("0.0") + " м.");
            return idx.Count / 3 + fallTris;
        }

        // полная высота обрыва вниз от кромки: то же число, что считает IslandCliff (1 + стенка + подбрюшье)
        static float TotalCliffDepth(IslandLayout L, IslandV2Data data)
        {
            float calc = 1f + Mathf.Max(2f, L.cliffWall) + Mathf.Max(3f, L.cliffTaper);
            if (L.buildCliff && data != null && data.depth > 3f) return data.depth;
            return calc;
        }

        // ================================================================ поверхность
        static void AddSurface(IslandField f, List<Vector2> axis, float half, List<Vector3> v, List<Vector3> nr, List<Vector2> uv, List<Color> col, List<int> idx)
        {
            var L = f.L;
            float w = half + EdgeTuck;
            float along = 0f;
            int first = v.Count;
            for (int i = 0; i < axis.Count; i++)
            {
                Vector2 p = axis[i];
                if (i > 0) along += Vector2.Distance(axis[i - 1], p);
                Vector2 t = (axis[Mathf.Min(axis.Count - 1, i + 1)] - axis[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new Vector2(-t.y, t.x);

                float cut = f.NotchCut(p.x, p.y);
                float fw = f.FordWeight(p.x, p.y);
                float y = f.RiverSurfaceAt(p.x, p.y) + Lift;
                float depthShare = Mathf.Clamp01(f.RiverWaterDepthAt(fw, cut) / Mathf.Max(0.1f, L.riverWaterDepth));

                // «ущелье»: насколько высоки стены над водой (0 на броде, 1 при 3,5 м и выше). Шейдер темнит воду у стен и замедляет течение
                float wallOff = half + 3f;
                float bankTop = 0.5f * (f.Height(p.x + n.x * wallOff, p.y + n.y * wallOff) + f.Height(p.x - n.x * wallOff, p.y - n.y * wallOff));
                float canyon = Mathf.Clamp01((bankTop - (y - Lift)) / 3.5f) * (1f - fw);

                // r = доля глубины (цвет), g = 0 (режим «поверхность»), b = пена у порога, a = ущелье
                var c = new Color(depthShare, 0f, Mathf.Clamp01(cut), canyon);
                float edgeU = w / Mathf.Max(0.1f, half);
                v.Add(new Vector3(p.x - n.x * w, y, p.y - n.y * w)); nr.Add(Vector3.up); uv.Add(new Vector2(-edgeU, along)); col.Add(c);
                v.Add(new Vector3(p.x + n.x * w, y, p.y + n.y * w)); nr.Add(Vector3.up); uv.Add(new Vector2(edgeU, along)); col.Add(c);
            }
            for (int i = 0; i + 1 < axis.Count; i++)
            {
                int a = first + i * 2, b = a + 1, c2 = a + 2, d = a + 3;
                idx.Add(a); idx.Add(c2); idx.Add(b);
                idx.Add(b); idx.Add(c2); idx.Add(d);
            }
        }

        // ================================================================ водопад
        // Нормаль контура наружу в точке edge
        static Vector2 OutwardNormal(IslandField f, Vector2 edge)
        {
            var c = f.Contour;
            float best = float.MaxValue; Vector2 tan = Vector2.right;
            for (int i = 0, j = c.Count - 1; i < c.Count; j = i++)
            {
                Vector2 a = c[j], b = c[i], ab = b - a;
                float l2 = ab.sqrMagnitude;
                if (l2 < 1e-6f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(edge - a, ab) / l2);
                float d2 = (a + ab * t - edge).sqrMagnitude;
                if (d2 < best) { best = d2; tan = ab.normalized; }
            }
            Vector2 n1 = new Vector2(tan.y, -tan.x);
            return f.Inside(edge.x + n1.x * 0.6f, edge.y + n1.y * 0.6f) ? -n1 : n1;
        }

        // Профиль «через порог»: вынос наружу растёт по дуге (быстро в начале, к ArcDrop замирает), дальше водопад падает строго вертикально.
        static float FallOut(float drop, float off)
        {
            float k = Mathf.Clamp01(drop / ArcDrop);
            return off * (1f - (1f - k) * (1f - k));
        }

        static void AddFall(IslandField f, Vector2 edge, float half, float totalDepth, float layer, float widthMul, float outMul, List<Vector3> v, List<Vector3> nr, List<Vector2> uv, List<Color> col, List<int> idx)
        {
            var L = f.L;
            Vector2 n = OutwardNormal(f, edge);
            Vector2 side = new Vector2(-n.y, n.x);               // вдоль кромки
            float yLip = f.RiverSurfaceAt(edge.x, edge.y) + Lift;
            float yCliffTop = yLip - Lift - L.riverLip;            // земля на пороге
            float yBottom = yCliffTop - totalDepth - L.fallExtra;
            float total = Mathf.Max(2f, yLip - yBottom);
            float off = Mathf.Max(0.3f, L.fallOffset) * outMul;

            // ширина: сверху ровно как у ленты реки (half + EdgeTuck): нет ступеньки между рекой и водопадом, водопад «цельный».
            // Дальше мягко сужается до fallWidthShare и к низу чуть расходится
            float wTop = (half + EdgeTuck) * widthMul;
            float wMid = half * Mathf.Clamp(L.fallWidthShare, 0.4f, 1.2f) * widthMul;

            // ряды по падению: густо на дуге, реже на вертикали
            var dropRows = new List<float>();
            const int arcRows = 9;
            for (int k = 0; k < arcRows; k++)
            {
                float t = (float)k / (arcRows - 1);
                dropRows.Add(ArcDrop * t * t * (3f - 2f * t) * 0.35f + ArcDrop * t * 0.65f);   // немного гуще у самого порога
            }
            const int tail = 9;
            float tailStart = ArcDrop;
            for (int k = 1; k <= tail; k++)
                dropRows.Add(Mathf.Lerp(tailStart, total, (float)k / tail));
            int rows = dropRows.Count;
            int cols = FallCols;

            int first = v.Count;
            float pathLen = 0f;
            Vector3 prev = Vector3.zero;
            for (int r = 0; r < rows; r++)
            {
                float drop = dropRows[r];
                float out_ = FallOut(drop, off);
                float k01 = Mathf.Clamp01(drop / total);
                float narrow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(drop / (ArcDrop * 2.2f)));
                float wHere = Mathf.Lerp(wTop, wMid, narrow) * (1f + 0.18f * k01);   // струя чуть расходится к низу
                wHere *= 1f - 0.30f * Mathf.SmoothStep(0.70f, 1f, k01);               // и закругляется в самом низу, а не обрубается прямоугольником
                float y = yLip - drop;
                Vector2 centre = edge + n * out_;
                Vector3 cen3 = new Vector3(centre.x, y, centre.y);
                if (r > 0) pathLen += Vector3.Distance(prev, cen3);
                prev = cen3;

                float fade = 1f - Mathf.SmoothStep(0.30f, 1.0f, k01);                            // внизу плавно растворяется (альфа в шейдере водопада)
                float foam = Mathf.Clamp01(1f - drop / 0.45f);                                    // белая кромка гребня сверху
                var c = new Color(fade, 0f, foam * (1f - 0.4f * layer), layer);                  // a = номер слоя (шейдер Waterfall)

                // нормаль: у порога смотрит вверх, дальше постепенно от скалы наружу
                Vector3 nrm = new Vector3(n.x, 0.0f, n.y);
                float tilt = Mathf.Clamp01(drop / ArcDrop);
                nrm = Vector3.Lerp(Vector3.up, nrm, tilt * tilt * (3f - 2f * tilt));

                for (int cI = 0; cI < cols; cI++)
                {
                    float u = Mathf.Lerp(-1f, 1f, (float)cI / (cols - 1));
                    // полотно чуть выгнуто наружу по центру (на 6% ширины): вода выглядит объёмной, а не плоской лентой
                    float bow = (1f - u * u) * 0.06f * wHere * Mathf.Clamp01(drop / ArcDrop);
                    Vector2 pp = centre + side * (u * wHere) + n * bow;
                    v.Add(new Vector3(pp.x, y, pp.y));
                    nr.Add(nrm.normalized);
                    uv.Add(new Vector2(u, pathLen));
                    col.Add(c);
                }
            }
            for (int r = 0; r + 1 < rows; r++)
            {
                for (int cI = 0; cI + 1 < cols; cI++)
                {
                    int a = first + r * cols + cI, b = a + 1, c2 = a + cols, d = c2 + 1;
                    idx.Add(a); idx.Add(c2); idx.Add(b);
                    idx.Add(b); idx.Add(c2); idx.Add(d);
                }
            }
        }

        // ================================================================ объект водопада (прозрачный шейдер)
        public const string FallObjectName = "River_Fall";
        public const string FallShaderName = "Project/IslandV2/Waterfall";
        public const string FallMaterialPath = IslandLayout.AssetFolder + "/Mat_IslandWaterfall.mat";

        static Material GetFallMaterial(IslandLayout L)
        {
            var sh = Shader.Find(FallShaderName);
            if (sh == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(FallMaterialPath);
            if (m == null)
            {
                IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
                m = new Material(sh) { name = "Mat_IslandWaterfall" };
                AssetDatabase.CreateAsset(m, FallMaterialPath);
            }
            else if (m.shader != sh) m.shader = sh;
            m.SetColor("_ShallowColor", L.riverShallow);
            m.SetColor("_MidColor", Color.Lerp(L.riverShallow, L.riverDeep, 0.45f));
            m.SetColor("_FoamColor", L.riverFoam);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static int BuildFallObject(IslandLayout L, Transform gen, List<Vector3> v, List<Vector3> nr, List<Vector2> uv, List<Color> col, List<int> idx)
        {
            if (idx.Count == 0) return 0;
            var mat = GetFallMaterial(L);
            if (mat == null)
            {
                Debug.LogWarning("IslandV2: не найден шейдер " + FallShaderName + " (файл Shaders/IslandV2/IslandWaterfall.shader). Водопад не построен.");
                return 0;
            }
            var mesh = new Mesh { name = "IslandRiverFall" };
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(nr);
            mesh.SetUVs(0, uv);
            mesh.SetColors(col);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(FallObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create River_Fall");
            go.transform.SetParent(gen, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return idx.Count / 3;
        }

        // ================================================================ брызги водопада
        // Два эмиттера CandyWaterFx на каждый водопад: пенные клубки на гребне (вес 1) и туман в облаках на месте растворения полотна (вес 3).
        // Общий бюджет частиц острова делит CandyWaterFx (Low 16, Mid 28, High 40), лишних draw call-ов нет: один ParticleSystem на эмиттер.
        // Нет шейдера Project/CandyParticle = брызг нет, водопад строится как обычно.
        static Material sprayMat;
        static bool sprayWarned;

        static Material GetSprayMaterial()
        {
            var sh = Shader.Find("Project/CandyParticle");
            if (sh == null || !sh.isSupported)
            {
                if (!sprayWarned) { sprayWarned = true; Debug.LogWarning("IslandV2: не найден шейдер Project/CandyParticle (CandyParticle.shader). Брызг у водопада не будет."); }
                return null;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(SprayMaterialPath);
            if (m == null)
            {
                IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
                m = new Material(sh) { name = "Mat_IslandSpray" };
                AssetDatabase.CreateAsset(m, SprayMaterialPath);
            }
            else if (m.shader != sh) m.shader = sh;
            m.renderQueue = (int)RenderQueue.Transparent + 5;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AddSpray(IslandField f, Transform gen, Vector2 edge, float half, float totalDepth, int index)
        {
            var mat = GetSprayMaterial();
            if (mat == null) return;
            var L = f.L;
            Vector2 n = OutwardNormal(f, edge);
            float yLip = f.RiverSurfaceAt(edge.x, edge.y) + Lift;
            float yBottom = yLip - Lift - L.riverLip - totalDepth - L.fallExtra;
            float total = Mathf.Max(2f, yLip - yBottom);
            float off = Mathf.Max(0.3f, L.fallOffset);
            string suffix = index == 0 ? "" : "_" + (index + 1);

            // гребень: чуть над порогом и чуть за кромкой
            Vector2 lip = edge + n * (off * 0.5f);
            Vector3 lipLocal = gen.InverseTransformPoint(new Vector3(lip.x, yLip + 0.15f, lip.y));
            var a = CandyWaterFx.Create(gen, "Spray_FallCrest" + suffix, lipLocal, 0.6f, half * 0.7f, 1, mat);
            if (a != null) Undo.RegisterCreatedObjectUndo(a.gameObject, "Create Spray_FallCrest");

            // туман: там, где полотно растворяется (около 88% падения), по центру струи
            Vector2 mist = edge + n * off;
            Vector3 mistLocal = gen.InverseTransformPoint(new Vector3(mist.x, yLip - total * 0.88f, mist.y));
            var b = CandyWaterFx.Create(gen, "Spray_FallMist" + suffix, mistLocal, 1.0f, half * 0.9f, 3, mat);
            if (b != null) Undo.RegisterCreatedObjectUndo(b.gameObject, "Create Spray_FallMist");
        }

        // ================================================================ объём воды (плавание)
        static void AddVolume(IslandField f, Transform gen, List<Vector2> axis, float half, int index)
        {
            if (axis.Count < 2) return;
            var go = new GameObject(index == 0 ? VolumeName : VolumeName + "_" + (index + 1));
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            go.transform.SetParent(gen, false);
            var pts = new Vector3[axis.Count];
            var reach = new float[axis.Count];
            for (int i = 0; i < axis.Count; i++)
            {
                float y = f.RiverSurfaceAt(axis[i].x, axis[i].y);
                pts[i] = gen.InverseTransformPoint(new Vector3(axis[i].x, y, axis[i].y));
                reach[i] = half;
            }
            var wv = go.AddComponent<WaterVolume>();
            wv.SetData(pts, reach);
            EditorUtility.SetDirty(wv);
        }

        // ================================================================ меню 127: числа глубокого русла
        // Новые поля раскладки получают значения по умолчанию только в новом ассете, а в уже сохранённом лежат старые числа.
        // Пункт одним нажатием ставит рекомендуемые для глубокого русла с крутыми стенами (под будущий мост). Потом «2. Собрать блокаут».
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Река: глубокое русло (рекомендуемые числа)", false, 127)]
        static void MenuDeepRiver()
        {
            var L = IslandLayout.GetOrCreate();
            Undo.RecordObject(L, "Река: глубокое русло");
            L.riverWidth = 10f;
            L.riverWaterDepth = 3.0f;
            L.riverSurfaceBelow = 1.3f;
            L.riverDrop = 1.6f;
            L.riverLip = 0.45f;
            L.riverBank = 2.5f;
            L.riverBankRun = 0.9f;
            L.riverFordRun = 2.8f;
            L.riverMeshRefine = true;
            L.riverRefineMargin = 5.5f;
            L.terrainResolution = 513;     // стены крутые: коллайдер должен их повторять
            L.terrainBaseY = -8f;          // дно глубже -6 м
            L.terrainHeightRange = 18f;
            L.riverShallow = new Color(0.40f, 0.78f, 0.84f, 1f);
            L.riverDeep = new Color(0.07f, 0.32f, 0.60f, 1f);
            L.riverWallColor = new Color(0.64f, 0.58f, 0.74f, 1f);
            L.riverWallWet = new Color(0.38f, 0.42f, 0.62f, 1f);
            EditorUtility.SetDirty(L);
            AssetDatabase.SaveAssets();
            Debug.Log("IslandV2: числа глубокого русла проставлены (ширина 10 м, вода 3 м, стены около 60°, брод пологий). Дальше: «2. Собрать блокаут».");
        }

        // ================================================================ меню 128: мультяшный вид воды
        // Ставит цвета и числа воды под референс (яркая бирюза, цельный водопад на всю ширину реки). Потом «2. Собрать блокаут».
        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Река: мультяшный вид (цвета и водопад)", false, 128)]
        static void MenuToonRiver()
        {
            var L = IslandLayout.GetOrCreate();
            Undo.RecordObject(L, "Река: мультяшный вид");
            L.riverShallow = new Color(0.55f, 0.92f, 0.95f, 1f);
            L.riverDeep = new Color(0.12f, 0.58f, 0.92f, 1f);
            L.riverFoam = new Color(0.96f, 1f, 1f, 1f);
            L.riverFlowSpeed = 1.2f;
            L.fallWidthShare = 0.95f;     // водопад почти во всю ширину реки (сверху он и так равен ширине воды)
            L.fallOffset = 1.0f;          // запас от скалы на неровность граней
            L.fallExtra = 8f;             // водопад уходит ниже подбрюшья глубже, в облака
            L.notchDrop = 0.8f;           // вода чуть заметнее ускоряется перед краем
            EditorUtility.SetDirty(L);
            AssetDatabase.SaveAssets();
            Debug.Log("IslandV2: мультяшный вид воды проставлен (бирюзовые цвета, цельный водопад). Дальше: «2. Собрать блокаут».");
        }

        // ================================================================ помощники
        // ломаная с шагом не больше step (линейно между точками)
        static List<Vector2> Resample(List<Vector2> pl, float step)
        {
            var res = new List<Vector2>();
            if (pl == null || pl.Count == 0) return res;
            res.Add(pl[0]);
            for (int i = 1; i < pl.Count; i++)
            {
                Vector2 a = pl[i - 1], b = pl[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(0.3f, step)));
                for (int k = 1; k <= n; k++) res.Add(Vector2.Lerp(a, b, (float)k / n));
            }
            return res;
        }
    }
}

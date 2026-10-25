// Assets/_Project/Editor/IslandV2/IslandField.cs
// Остров v2, этап 1: «поле» острова. Чистая математика по числам из IslandLayout, без объектов сцены.
// Его читают и сборка блокаута (IslandBlockout), и проверка (IslandCheck), поэтому форма, высота и тропы в обоих местах одни и те же.
// Части этапа 1: IslandLayout.cs, IslandField.cs (этот файл), IslandBlockout.cs, IslandBlockout.Ground.cs, IslandCheck.cs.
//
// Что умеет:
//   Contour / Area / Min / Max            контур острова (многоугольник), площадь, рамка
//   Inside(x,z), EdgeDistance(x,z)        внутри ли контура, расстояние до края (внутри положительное)
//   Height(x,z)                           высота земли: мягкие холмы, ровные площадки у зданий, русло реки и пруд
//   IsWater / IsRiver / IsFord / IsPond   вода (река вне брода, пруд), брод
//   PathDistance(x,z)                     расстояние до оси ближайшей тропы; PathWeight(x,z) = 1 на тропе, 0 вдали
//   AnchorWorld(a)                        позиция метки на плане (для EdgePoint считается по контуру)
//   RectDistance(a,x,z)                   расстояние от точки до повёрнутого прямоугольника здания (0 внутри)
//   Slope(x,z)                            уклон в градусах
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    public partial class IslandField
    {
        public readonly IslandLayout L;
        public readonly List<Vector2> Contour = new List<Vector2>();
        public float Area { get; private set; }
        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }

        readonly List<Vector2[]> pathDense = new List<Vector2[]>();
        readonly List<float> pathHalf = new List<float>();
        Vector2[] riverDense = new Vector2[0];
        readonly float sx, sz;   // сдвиги шума от зерна

        public IslandField(IslandLayout layout)
        {
            L = layout;
            sx = L.seed * 17.31f + 3.7f;
            sz = L.seed * 9.77f + 11.3f;
            BuildContour();
            for (int i = 0; i < L.paths.Count; i++)
            {
                pathDense.Add(Dense(PathControlPoints(L.paths[i]), 6f));
                pathHalf.Add(Mathf.Max(0.5f, L.paths[i].width * 0.5f));
            }
            riverDense = Dense(L.riverPoints, 4f);
        }

        // опорные точки тропы; если задан endAnchor, конец тропы стоит на метке (последняя точка заменяется, если ближе 4 м)
        List<Vector2> PathControlPoints(IslandLayout.PathLine pl)
        {
            var pts = new List<Vector2>(pl.points);
            if (string.IsNullOrEmpty(pl.endAnchor)) return pts;
            var a = L.FindAnchor(pl.endAnchor);
            if (a == null) return pts;
            Vector2 end = AnchorWorld(a);
            if (pts.Count > 0 && Vector2.Distance(pts[pts.Count - 1], end) < 4f) pts.RemoveAt(pts.Count - 1);
            pts.Add(end);
            return pts;
        }

        // ================================================================ контур
        void BuildContour()
        {
            int n = Mathf.Max(32, L.contourPoints);
            var raw = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float rx = L.radiusX, rz = L.radiusZ;
                float r = rx * rz / Mathf.Sqrt(rz * rz * c * c + rx * rx * s * s);   // эллипс
                float big = Mathf.PerlinNoise(c * L.edgeFreqBig + sx, s * L.edgeFreqBig + sz) - 0.5f;
                float small = Mathf.PerlinNoise(c * L.edgeFreqSmall + sz, s * L.edgeFreqSmall + sx) - 0.5f;
                float k = 1f + big * 2f * L.edgeNoiseBig + small * 2f * L.edgeNoiseSmall;
                raw.Add(new Vector2(c * r * k, s * r * k));
            }
            // подгоняем площадь: равномерный масштаб вокруг центра
            float area0 = PolyArea(raw);
            float sc = area0 > 1f ? Mathf.Sqrt(L.targetArea / area0) : 1f;
            Contour.Clear();
            for (int i = 0; i < raw.Count; i++) Contour.Add(raw[i] * sc + L.centerOffset);
            Area = PolyArea(Contour);
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < Contour.Count; i++)
            {
                mn = Vector2.Min(mn, Contour[i]); mx = Vector2.Max(mx, Contour[i]);
            }
            Min = mn; Max = mx;
        }

        public static float PolyArea(List<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += p[j].x * p[i].y - p[i].x * p[j].y;
            return Mathf.Abs(a) * 0.5f;
        }

        public bool Inside(float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = Contour.Count - 1; i < Contour.Count; j = i++)
            {
                Vector2 a = Contour[i], b = Contour[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        // ближайшая точка контура и расстояние до него
        public Vector2 NearestOnContour(float x, float z, out float dist)
        {
            Vector2 p = new Vector2(x, z), best = Contour[0];
            float bd = float.MaxValue;
            for (int i = 0, j = Contour.Count - 1; i < Contour.Count; j = i++)
            {
                Vector2 q = NearestOnSeg(p, Contour[j], Contour[i]);
                float d = (q - p).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
            dist = Mathf.Sqrt(bd);
            return best;
        }

        public float EdgeDistance(float x, float z)
        {
            float d;
            NearestOnContour(x, z, out d);
            return Inside(x, z) ? d : -d;
        }

        // точка пересечения луча из центра (по углу) с контуром
        public Vector2 EdgePointAtAngle(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            Vector2 o = L.centerOffset;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float lo = 0f, hi = Mathf.Max(L.radiusX, L.radiusZ) * 2f;
            for (int it = 0; it < 40; it++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector2 p = o + dir * mid;
                if (Inside(p.x, p.y)) lo = mid; else hi = mid;
            }
            return o + dir * lo;
        }

        // ================================================================ кривые
        static Vector2 NearestOnSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return a + ab * t;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // плотная ломаная по опорным точкам: Catmull-Rom, шаг примерно step метров
        public static Vector2[] Dense(List<Vector2> pts, float step)
        {
            if (pts == null || pts.Count == 0) return new Vector2[0];
            if (pts.Count == 1) return new[] { pts[0] };
            var res = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int segs = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / Mathf.Max(0.5f, step)));
                for (int k = 0; k < segs; k++) res.Add(CatmullRom(p0, p1, p2, p3, (float)k / segs));
            }
            res.Add(pts[pts.Count - 1]);
            return res.ToArray();
        }

        public Vector2[] PathPolyline(int i) { return pathDense[i]; }
        public Vector2[] RiverPolyline { get { return riverDense; } }

        static float DistToPolyline(Vector2[] pl, Vector2 p, out int segIndex)
        {
            float best = float.MaxValue;
            segIndex = 0;
            for (int i = 0; i + 1 < pl.Length; i++)
            {
                float d = (NearestOnSeg(p, pl[i], pl[i + 1]) - p).sqrMagnitude;
                if (d < best) { best = d; segIndex = i; }
            }
            return Mathf.Sqrt(best);
        }

        // ================================================================ тропы
        public float PathDistance(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < pathDense.Count; i++)
            {
                int si;
                float d = DistToPolyline(pathDense[i], p, out si) - pathHalf[i];
                if (d < best) best = d;
            }
            return best;   // от КРАЯ тропы: меньше нуля = на тропе
        }

        // расстояние до оси ближайшей тропы, метры
        public float PathAxisDistance(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < pathDense.Count; i++)
            {
                int si;
                float d = DistToPolyline(pathDense[i], p, out si);
                if (d < best) best = d;
            }
            return best;
        }

        // 1 на тропе, 0 дальше мягкого края (1 м)
        public float PathWeight(float x, float z)
        {
            float d = PathDistance(x, z);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + 0.6f) / 1.6f));
        }

        // ================================================================ вода
        public float RiverDistance(float x, float z)
        {
            if (riverDense.Length < 2) return float.MaxValue;
            int si;
            return DistToPolyline(riverDense, new Vector2(x, z), out si);
        }

        // 0 = брода нет, 1 = брод полностью (плавно по длине брода)
        public float FordWeight(float x, float z)
        {
            float best = 0f;
            for (int i = 0; i < L.crossings.Count; i++)
            {
                var c = L.crossings[i];
                float d = Vector2.Distance(new Vector2(x, z), c.pos);
                float w = 1f - Step(c.halfLength * 0.7f, c.halfLength * 1.3f, d);
                if (w > best) best = w;
            }
            return best;
        }

        public bool IsFord(float x, float z) { return FordWeight(x, z) > 0.5f; }

        public bool IsRiver(float x, float z)
        {
            if (RiverDistance(x, z) > L.riverWidth * 0.5f) return false;
            return !IsFord(x, z);
        }

        // эллиптическое расстояние до пруда: меньше 1 = внутри воды
        public float PondValue(float x, float z)
        {
            float dx = (x - L.pondPos.x) / Mathf.Max(0.1f, L.pondRadius.x);
            float dz = (z - L.pondPos.y) / Mathf.Max(0.1f, L.pondRadius.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public bool IsPond(float x, float z) { return PondValue(x, z) < 1f; }
        public bool IsWater(float x, float z) { return IsPond(x, z) || IsRiver(x, z); }

        // расстояние до ближайшей воды (метры, 0 и меньше = в воде); для отступа леса от берега
        public float WaterDistance(float x, float z)
        {
            float riv = float.MaxValue;
            if (riverDense.Length >= 2 && !IsFord(x, z)) riv = RiverDistance(x, z) - L.riverWidth * 0.5f;
            float pr = (PondValue(x, z) - 1f) * Mathf.Min(L.pondRadius.x, L.pondRadius.y);
            return Mathf.Min(riv, pr);
        }

        // ================================================================ метки и здания
        public Vector2 AnchorWorld(IslandLayout.Anchor a)
        {
            if (a.kind == IslandLayout.AnchorKind.EdgePoint)
            {
                Vector2 e = EdgePointAtAngle(a.edgeAngleDeg);
                Vector2 toCenter = (L.centerOffset - e).normalized;
                return e + toCenter * a.edgeInset;
            }
            return a.pos;
        }

        public static Vector2 Forward(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        // расстояние от точки до повёрнутого прямоугольника здания (0 внутри)
        public float RectDistance(IslandLayout.Anchor a, float x, float z)
        {
            Vector2 c = AnchorWorld(a);
            Vector2 f = Forward(a.yawDeg), r = new Vector2(f.y, -f.x);
            Vector2 d = new Vector2(x, z) - c;
            float lx = Mathf.Abs(Vector2.Dot(d, r)) - a.footprint.x * 0.5f;
            float lz = Mathf.Abs(Vector2.Dot(d, f)) - a.footprint.y * 0.5f;
            float ox = Mathf.Max(lx, 0f), oz = Mathf.Max(lz, 0f);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        // ближе всего к зданию, у которого есть площадка (flatRadius): вес выравнивания 1 внутри, 0 за flatRadius + flatBlend
        float AnchorFlatWeight(float x, float z)
        {
            float best = 0f;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                if (a.flatRadius <= 0f) continue;
                Vector2 c = AnchorWorld(a);
                float d = Vector2.Distance(new Vector2(x, z), c);
                float w = 1f - Step(a.flatRadius, a.flatRadius + Mathf.Max(0.5f, L.flatBlend), d);
                if (w > best) best = w;
            }
            return best;
        }

        // ================================================================ высота
        float Hills(float x, float z)
        {
            float sc = Mathf.Max(5f, L.hillScale);
            float n1 = Mathf.PerlinNoise(x / sc + sx, z / sc + sz) - 0.5f;
            float n2 = Mathf.PerlinNoise(x / (sc * 0.4f) + sz, z / (sc * 0.4f) + sx) - 0.5f;
            return (n1 * 2f + n2 * 0.5f) * L.hillAmp;
        }

        // высота земли в точке (метры, Y=0 на земле у костра). Определена и за контуром (меш и Terrain сами обрезаются по контуру)
        public float Height(float x, float z)
        {
            float h = Hills(x, z);
            float flat = AnchorFlatWeight(x, z);
            float pw = PathWeight(x, z) * L.pathFlatten;
            float keep = (1f - flat) * (1f - pw);
            h *= keep;

            // русло реки (этап E): дно и вода текут вниз от истока к устью, откосы берега полого поднимаются до земли.
            // Профиль, поверхность воды и вырез у кромки (D) описаны в IslandField.River.cs (RiverProfile)
            if (riverDense.Length >= 2)
            {
                float rs;
                float rd = RiverNearest(x, z, out rs);
                float reachMax = L.riverWidth * 0.5f + Mathf.Max(0.5f, L.riverBank) + Mathf.Max(L.riverBankRun, L.riverFordRun) * (L.riverSurfaceBelow + L.riverDrop + L.notchDrop + 3f) * 1.5f;
                if (rd < reachMax) h = RiverProfile(h, rd, rs, FordWeight(x, z), NotchCut(x, z));
            }
            // пруд
            float pv = PondValue(x, z);
            float bankRel = Mathf.Max(0.5f, L.pondBank) / Mathf.Max(1f, Mathf.Min(L.pondRadius.x, L.pondRadius.y));
            float kp = 1f - Step(0.65f, 1f + bankRel, pv);
            h = Mathf.Lerp(h, -L.pondDepth, kp);
            return h;
        }

        // уклон в градусах (центральные разности на шаге 0.5 м)
        public float Slope(float x, float z)
        {
            const float e = 0.5f;
            float dx = (Height(x + e, z) - Height(x - e, z)) / (2f * e);
            float dz = (Height(x, z + e) - Height(x, z - e)) / (2f * e);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        // ================================================================ лес-заглушки
        // Узлы сетки со сдвигом: где стоят деревья. Не считает высоту, она берётся в сборке.
        public List<Vector2> TreePoints()
        {
            var res = new List<Vector2>();
            float sp = Mathf.Max(2.5f, L.treeSpacing);
            var rnd = new System.Random(L.seed * 31 + 5);
            for (float gx = Min.x; gx <= Max.x; gx += sp)
            {
                for (float gz = Min.y; gz <= Max.y; gz += sp)
                {
                    float jx = ((float)rnd.NextDouble() - 0.5f) * 2f * L.treeJitter * sp;
                    float jz = ((float)rnd.NextDouble() - 0.5f) * 2f * L.treeJitter * sp;
                    float x = gx + jx, z = gz + jz;
                    if (AllowedForTree(x, z)) res.Add(new Vector2(x, z));
                }
            }
            return res;
        }

        bool AllowedForTree(float x, float z)
        {
            if (EdgeDistance(x, z) < L.edgeClear) return false;
            if (WaterDistance(x, z) < L.waterClear) return false;
            if (PathAxisDistance(x, z) < L.pathClear) return false;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                var a = L.anchors[i];
                float clear = a.kind == IslandLayout.AnchorKind.Building ? L.buildingClear : Mathf.Max(2.5f, a.flatRadius * 0.5f);
                if (a.kind == IslandLayout.AnchorKind.Building)
                {
                    if (RectDistance(a, x, z) < clear) return false;
                }
                else if (Vector2.Distance(AnchorWorld(a), new Vector2(x, z)) < clear) return false;
            }
            for (int i = 0; i < L.meadows.Count; i++)
                if (Vector2.Distance(L.meadows[i].pos, new Vector2(x, z)) < L.meadows[i].radius) return false;
            return ForestNoise(x, z) > L.forestThreshold;
        }
    }
}

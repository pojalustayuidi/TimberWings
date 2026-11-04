// Assets/_Project/Editor/TWRiver/TWRiverBanks.cs
// Берега, пороги и обрамление водопада.
//  Берега: скальная «оболочка» лежит ровно по рельефу русла (IslandField.Height), поэтому прижата к земле. Толщина зависит от шума вдоль реки
//   и от стороны: у скалистого берега толще и непрерывнее, у мягкого только пятна. В местах, где оболочка истончается, она уходит под землю без шва.
//  Пороги: низкие камни поперёк русла с проходом для воды, россыпи, валун у берега. Ритм задан шагом 14-21 м, между ними тихая вода.
//  Водопад: ступенчатые выступы по бокам порога прижаты к настоящему мешу обрыва (зонд), камни-разделители струй у кромки.
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverBanks
    {
        const int K = 6;
        static readonly float[] Pat = { 0f, 0.9f, 0.5f, 1.0f, 0.55f, 0.95f, 0f };

        // доля скальной облицовки берега в точке реки s: 0 мягкий берег, 1 сплошной камень
        public static float Cover(TWRiverCtx x, float s, int side)
        {
            float n = TWNoise.Val1(s * 0.085f, x.seed + (side > 0 ? 100 : 137));
            float bias = side == x.rockSide ? 0.22f : -0.18f;
            float c = Mathf.SmoothStep(0.34f, 0.58f, n + bias);
            float len = x.ax.Length;
            if (x.hasSource) c = Mathf.Max(c, (1f - Mathf.SmoothStep(TWRiverSpec.MouthDist, TWRiverSpec.MouthDist + 10f, s)) * 0.92f);
            c = Mathf.Max(c, Mathf.SmoothStep(len - 15f, len - 5f, s) * (side == x.rockSide ? 0.95f : 0.8f));
            c *= 1f - x.ExclusionWeight(x.ax.At(s), 3f);
            return c;
        }

        static Vector3[] Ring(TWRiverCtx x, float s, int side, out float cover, out float yw)
        {
            var f = x.f;
            float half = x.half;
            Vector2 A = x.ax.At(s), C = x.ax.Side(s) * side;   // C смотрит в сторону этого берега
            yw = x.Yw(s);
            float prev = f.Height(A.x + C.x * half, A.y + C.y * half);
            float offTop = half + 4.5f;
            for (float o = half + 0.25f; o < half + 8f; o += 0.25f)
            {
                float h = f.Height(A.x + C.x * o, A.y + C.y * o);
                if (o > half + 1.0f && h - prev < 0.06f && h > yw + 0.4f) { offTop = o; break; }
                prev = h;
            }
            cover = Cover(x, s, side);
            bool rock = side == x.rockSide;
            float thick = (rock ? 0.85f : 0.48f) * (0.75f + 0.5f * TWNoise.Val1(s * 0.31f, x.seed + 9));
            var pts = new Vector3[K + 1];
            for (int k = 0; k <= K; k++)
            {
                float fk = (float)k / K;
                float off = Mathf.Lerp(half - 0.25f, offTop + 0.9f, fk);
                float px = A.x + C.x * off, pz = A.y + C.y * off;
                float g = f.Height(px, pz);
                float lift = Mathf.Lerp(-0.3f, thick * Pat[k], cover);
                float y = g + lift;
                if (k == 0) y = Mathf.Min(yw - 0.35f, y);     // низ под водой: стык с водой виден как линия камня, а не щель
                if (k == K) y = g - 0.25f;                    // верхний край уходит под траву
                pts[k] = new Vector3(px, y, pz);
            }
            return pts;
        }

        public static void Build(TWRiverCtx x)
        {
            float len = x.ax.Length;
            float s0 = x.hasSource ? TWRiverSpec.MouthDist - 1.0f : 0.5f;
            float s1 = len - 0.6f;
            for (int side = -1; side <= 1; side += 2)
            {
                var rings = new List<Vector3[]>();
                var covers = new List<float>();
                var ywl = new List<float>();
                float s = s0;
                int idx = 0;
                while (s < s1)
                {
                    float cv, yw;
                    rings.Add(Ring(x, s, side, out cv, out yw)); covers.Add(cv); ywl.Add(yw);
                    s += 1.7f + (TWNoise.Hash(idx++, side, x.seed + 5) - 0.5f) * 0.7f;
                }
                {
                    float cv, yw;
                    rings.Add(Ring(x, s1, side, out cv, out yw)); covers.Add(cv); ywl.Add(yw);
                }
                for (int i = 0; i + 1 < rings.Count; i++)
                {
                    if (covers[i] < 0.03f && covers[i + 1] < 0.03f) continue;
                    var r0 = rings[i]; var r1 = rings[i + 1];
                    float yw = (ywl[i] + ywl[i + 1]) * 0.5f;
                    for (int k = 0; k < K; k++)
                    {
                        Vector3 a = r0[k], b = r0[k + 1], c = r1[k + 1], d = r1[k];
                        Vector3 cen = (a + b + c + d) * 0.25f;
                        Vector3 nn = Vector3.Cross(b - a, d - a).normalized;
                        Vector2 cside = x.ax.Side(Mathf.Clamp(s0 + i * 1.7f, 0f, x.ax.Length)) * side;
                        Vector3 toward = cen + Vector3.up * 0.8f - new Vector3(cside.x, 0f, cside.y) * 0.8f;
                        if (Vector3.Dot(nn, toward - cen) < 0f) nn = -nn;
                        float h = TWNoise.Hash(i, k + side * 7, x.seed + 21);
                        float moss = k >= K - 2 ? 0.40f : (h > 0.8f ? 0.2f : 0f);
                        Color col = x.pal.Rock(cen.y - yw, nn, h, moss);
                        x.rocks.TriToward(a, b, c, col, toward);
                        x.rocks.TriToward(a, c, d, col, toward);
                    }
                    x.rocks.anchors.Add(r0[0]); x.rocks.anchors.Add(r0[K]);
                }
            }
        }

        // ------------------------------------------------------------------ пороги и камни в воде
        static TWStone.ColorFn WaterRock(TWRiverCtx x, float yw)
        {
            return (wy, n, h) => x.pal.Rock(wy - yw, n, h, 0.08f);
        }

        static void AddStone(TWRiverCtx x, float s, float off, float r, float flat, int seed, float topAboveWater)
        {
            float yw = x.Yw(s);
            float ry = r * flat;
            Vector3 c = x.P(s, off, yw + topAboveWater - ry * 0.80f);
            TWStone.Build(x.rocks, c, new Vector3(r * 1.1f, ry, r * 1.25f), TWNoise.Hash(seed, 2, x.seed) * 3.1f, seed, WaterRock(x, yw), 0.6f);
            x.stones.Add(new Vector4(s, off, yw, r));
        }

        static void Sill(TWRiverCtx x, float s, int idx)
        {
            float scale = x.half / 5f;
            float gap = Mathf.Lerp(-1.8f, 1.8f, TWNoise.Hash(idx, 1, x.seed + 70)) * scale;
            int n = 6;
            float skew = (TWNoise.Hash(idx, 2, x.seed + 71) - 0.5f) * 0.3f;
            for (int i = 0; i < n; i++)
            {
                float off = Mathf.Lerp(-3.9f, 3.9f, (i + 0.5f) / n) * scale + (TWNoise.Hash(i, idx, x.seed + 72) - 0.5f) * 0.4f;
                if (Mathf.Abs(off - gap) < 1.15f * scale) continue;
                float r = 0.55f + 0.35f * TWNoise.Hash(i, idx, x.seed + 73);
                AddStone(x, s + off * skew, off, r, 0.5f, x.seed + 700 + idx * 13 + i, 0.30f + 0.12f * TWNoise.Hash(i, 3, x.seed + idx));
            }
        }

        static void Scatter(TWRiverCtx x, float s, int idx)
        {
            float scale = x.half / 5f;
            float dir = TWNoise.Hash(idx, 5, x.seed + 74) > 0.5f ? 1f : -1f;
            AddStone(x, s, -1.6f * dir * scale, 0.70f, 0.75f, x.seed + 800 + idx * 11, 0.52f);
            AddStone(x, s + 1.9f, 0.9f * dir * scale, 0.95f, 0.8f, x.seed + 801 + idx * 11, 0.62f);
            AddStone(x, s + 3.5f, -0.5f * dir * scale, 0.60f, 0.7f, x.seed + 802 + idx * 11, 0.42f);
        }

        static void BankBoulder(TWRiverCtx x, float s, int idx)
        {
            float off = x.rockSide * (x.half - 1.4f);
            AddStone(x, s, off, 1.35f, 0.85f, x.seed + 900 + idx * 7, 0.95f);
            AddStone(x, s + 1.7f, off - x.rockSide * 0.9f, 0.62f, 0.7f, x.seed + 901 + idx * 7, 0.42f);
            Vector3 c = x.P(s, off, x.Yw(s) + 1.0f);
            x.mossSpots.Add(new Vector4(c.x, c.y, c.z, 0.9f));
        }

        public static void BuildRapids(TWRiverCtx x)
        {
            float len = x.ax.Length;
            float lm = x.hasSource ? TWRiverSpec.MouthDist : 0f;
            float s = lm + 12f + 4f * TWNoise.Hash(0, 0, x.seed + 66);
            int idx = 0;
            while (s < len - 18f)
            {
                float ex = x.ExclusionWeight(x.ax.At(s), 5f) + x.ExclusionWeight(x.ax.At(s + 3.5f), 5f);
                if (ex < 0.02f)
                {
                    int kind = idx % 3;
                    if (kind == 0) Sill(x, s, idx);
                    else if (kind == 1) Scatter(x, s, idx);
                    else BankBoulder(x, s, idx);
                }
                s += 14f + 7f * TWNoise.Hash(idx, 9, x.seed + 67);
                idx++;
            }
        }

        // ------------------------------------------------------------------ водопад: выступы по бокам и камни-разделители
        public static void BuildFallFrame(TWRiverCtx x)
        {
            var f = x.f;
            float len = x.ax.Length;
            Vector2 E = x.ax.At(len);
            Vector2 fd = x.outletTail.sqrMagnitude > 0.5f ? x.outletTail.normalized : x.ax.Tangent(len);
            Vector2 cr = new Vector2(-fd.y, fd.x);
            float yRib = f.RiverSurfaceAt(E.x, E.y);
            float yGround = yRib - x.L.riverLip;
            float uIn = -4.0f;
            int seed = x.seed + 1300;

            for (int side = -1; side <= 1; side += 2)
            {
                bool rock = side == x.rockSide;
                for (int k = 0; k < 4; k++)
                {
                    float w = 3.3f - 0.6f * k + (TWNoise.Hash(k, side, seed) - 0.5f) * 0.5f;
                    float v0 = side * (x.half + 0.3f), v1 = side * (x.half + 0.3f + w);
                    float vm = (v0 + v1) * 0.5f;
                    float topK = yGround + (rock ? 0.55f : 0.20f) - 1.05f * k;
                    float yMid = topK - 0.6f;

                    float uo0 = Outer(x, E, fd, cr, v0, uIn, yMid), uo1 = Outer(x, E, fd, cr, v1, uIn, yMid), uom = Outer(x, E, fd, cr, vm, uIn, yMid);
                    var plan = new List<Vector2>
                    {
                        new Vector2(uIn, v0),
                        new Vector2(uIn, v1),
                        new Vector2(uo1 - 0.45f, v1 + side * 0.18f * TWNoise.Hash(k, 1, seed)),
                        new Vector2(uo1, Mathf.Lerp(v1, v0, 0.35f)),
                        new Vector2(uom + 0.15f, vm),
                        new Vector2(uo0, v0)
                    };
                    PrismFromPlan(x, E, fd, cr, plan, topK, yGround, yRib, k, side, seed);
                }
            }

            // камни-разделители у самой кромки: струи падают между ними
            float scale = x.half / 5f;
            float[] offs = { -2.5f, 0.5f, 3.0f };
            float[] rs = { 0.45f, 0.58f, 0.42f };
            for (int i = 0; i < 3; i++)
            {
                float s = len - 0.9f - 0.3f * TWNoise.Hash(i, 3, seed + 5);
                AddStone(x, s, offs[i] * scale + (TWNoise.Hash(i, 4, seed + 6) - 0.5f) * 0.4f, rs[i], 0.6f, seed + 40 + i, 0.20f + 0.1f * TWNoise.Hash(i, 5, seed));
            }
        }

        // расстояние от начала луча (u = uIn) до обрыва в боковой точке v на высоте y, плюс запас 0.28 м наружу
        static float Outer(TWRiverCtx x, Vector2 E, Vector2 fd, Vector2 cr, float v, float uIn, float y)
        {
            Vector2 o = E + fd * uIn + cr * v;
            float d = x.cliff != null ? x.cliff.Ray(new Vector3(o.x, y, o.y), fd, 9f) : -1f;
            if (d < 0f) return 0.6f;
            return uIn + d + 0.28f;
        }

        static void PrismFromPlan(TWRiverCtx x, Vector2 E, Vector2 fd, Vector2 cr, List<Vector2> plan, float topK, float yGround, float yRib, int k, int side, int seed)
        {
            int n = plan.Count;
            var top = new Vector3[n];
            var bot = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = E + fd * plan[i].x + cr * plan[i].y;
                float g = x.f.Height(p.x, p.y);
                float yTop = topK + (TWNoise.Hash(i, k, seed + side) - 0.5f) * 0.18f;
                if (plan[i].x < -0.8f) yTop = Mathf.Min(yTop, g + 0.30f);    // внутренний конец уходит в землю
                top[i] = new Vector3(p.x, yTop, p.y);
                bot[i] = new Vector3(p.x, yTop - 1.5f, p.y);
                x.rocks.anchors.Add(bot[i]);
            }
            Vector3 cen = Vector3.zero;
            for (int i = 0; i < n; i++) cen += (top[i] + bot[i]) * 0.5f;
            cen /= n;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (top[i] + top[j] + bot[i] + bot[j]) * 0.25f;
                float h = TWNoise.Hash(i, k + 5, seed);
                Vector3 nn = Vector3.Cross(top[j] - top[i], bot[i] - top[i]).normalized;
                if (Vector3.Dot(nn, mid - cen) < 0f) nn = -nn;
                Color col = x.pal.Rock(mid.y - yRib, nn, h, 0f);
                x.rocks.TriAway(top[i], top[j], bot[j], col, cen);
                x.rocks.TriAway(top[i], bot[j], bot[i], col, cen);
            }
            var tri = TWGeo.EarClip(plan);
            for (int i = 0; i + 2 < tri.Count; i += 3)
            {
                Vector3 a = top[tri[i]], b = top[tri[i + 1]], c = top[tri[i + 2]];
                Vector3 ct = (a + b + c) / 3f;
                Color col = x.pal.Rock(ct.y - yRib, Vector3.up, TWNoise.Hash(i, k + 9, seed), 0.35f);
                x.rocks.TriToward(a, b, c, col, ct + Vector3.up);
            }
            Vector3 topMid = Vector3.zero;
            for (int i = 0; i < n; i++) topMid += top[i];
            topMid /= n;
            if (k == 0) x.mossSpots.Add(new Vector4(topMid.x, topMid.y, topMid.z, 1.1f));
        }
    }
}

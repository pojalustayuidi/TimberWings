// Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBuilder.Ground.cs (НЕ в Editor)
// Земля поляны: круглые площадки (с рваным краем) и тропы-ленты по сплайну.
// Цвет вершин: центр светлая сухая земля, к краю темнее, на самой кромке цвет травы, поэтому граница мягкая без текстур.
// Площадки и тропы лежат на разной высоте над лугом (groundLift + доли сантиметра), чтобы не мерцали друг на друге,
// а там, где тропа входит в площадку, её краевые цвета плавно меняются на цвет площадки (нет тёмных полос внутри площади).
using System.Collections.Generic;
using UnityEngine;

public partial class ChopGroveBuilder
{
    const int DiscSegs = 30;
    static readonly float[] DiscRing = { 0f, 0.5f, 0.82f, 1.0f, 1.25f };
    static readonly float[] PathOff = { -1.35f, -1.0f, -0.5f, 0f, 0.5f, 1.0f, 1.35f };

    void BuildGround()
    {
        if (discs != null)
            for (int i = 0; i < discs.Length; i++)
                if (discs[i] != null) BuildDisc(discs[i], i, groundLift * (i == 0 ? 0.8f : 1.1f));
        if (paths != null)
            for (int i = 0; i < paths.Length; i++)
                if (paths[i] != null) BuildPath(paths[i], i);
    }

    // насколько точка внутри площадок: 0 снаружи .. 1 глубоко
    float DiscInfluence(Vector2 p)
    {
        float best = 0f;
        if (discs == null) return 0f;
        for (int i = 0; i < discs.Length; i++)
        {
            var d = discs[i];
            if (d == null) continue;
            float t = Mathf.Clamp01((d.radius * 1.05f - (p - d.center).magnitude) / (d.radius * 0.45f));
            if (t > best) best = t;
        }
        return best;
    }

    float Patch(float x, float z, float scale, float salt) { return Mathf.PerlinNoise(x * scale + salt, z * scale + salt * 0.7f); }

    Color DirtAt(Vector2 p, int ring, float salt)
    {
        Color c;
        switch (ring)
        {
            case 0: case 1: c = dirtCenter; break;
            case 2: c = Color.Lerp(dirtCenter, dirtMid, 0.7f); break;
            case 3: c = dirtEdge; break;
            default: c = grassEdge; break;
        }
        if (ring <= 3)
        {
            float n = Patch(p.x, p.y, 0.55f, salt);                       // пятна потемнее и посветлее
            c = Color.Lerp(c, GrovePal.DirtPatch, Mathf.Clamp01((n - 0.58f) * 2.2f) * 0.55f);
            c = Color.Lerp(c, dirtCenter * 1.06f, Mathf.Clamp01((0.38f - n) * 2f) * 0.45f);
        }
        return c;
    }

    // ---------------------------------------------------------------- площадка
    void BuildDisc(GroveDisc d, int index, float lift)
    {
        float salt = 11f + index * 7.3f;
        int rings = DiscRing.Length;
        int[] idx = new int[rings * DiscSegs];
        int centerIdx;
        {
            Vector2 c = d.center;
            centerIdx = ground.Vert(new Vector3(c.x, GroundY(c.x, c.y) + lift, c.y), DirtAt(c, 0, salt));
        }
        for (int r = 1; r < rings; r++)
        {
            for (int s = 0; s < DiscSegs; s++)
            {
                float a = s / (float)DiscSegs * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float rough = 1f + (Mathf.PerlinNoise(ca * 1.4f + salt, sa * 1.4f + salt) - 0.5f) * 0.30f
                                 + (Mathf.PerlinNoise(ca * 3.7f + salt * 2f, sa * 3.7f - salt) - 0.5f) * 0.10f;
                float rad = d.radius * DiscRing[r] * rough;
                Vector2 p = d.center + new Vector2(ca, sa) * rad;
                float y = GroundY(p.x, p.y) + (r == rings - 1 ? lift * 0.4f : lift);
                idx[r * DiscSegs + s] = ground.Vert(new Vector3(p.x, y, p.y), DirtAt(p, r, salt));
            }
        }
        for (int s = 0; s < DiscSegs; s++)
        {
            int n = (s + 1) % DiscSegs;
            ground.TriUp(centerIdx, idx[1 * DiscSegs + s], idx[1 * DiscSegs + n]);
            for (int r = 1; r < rings - 1; r++)
            {
                int a = idx[r * DiscSegs + s], b = idx[r * DiscSegs + n], c = idx[(r + 1) * DiscSegs + n], e = idx[(r + 1) * DiscSegs + s];
                ground.TriUp(a, b, c);
                ground.TriUp(a, c, e);
            }
        }
    }

    // ---------------------------------------------------------------- тропа
    void BuildPath(GrovePath path, int index)
    {
        List<Vector2> s = path.Sample(0.45f);
        int n = s.Count;
        if (n < 2) return;
        float salt = 31f + index * 5.9f;
        int cols = PathOff.Length;
        int[,] idx = new int[n, cols];
        float acc = 0f, total = 0f;
        for (int i = 1; i < n; i++) total += (s[i] - s[i - 1]).magnitude;

        for (int i = 0; i < n; i++)
        {
            if (i > 0) acc += (s[i] - s[i - 1]).magnitude;
            Vector2 t = (s[Mathf.Min(i + 1, n - 1)] - s[Mathf.Max(i - 1, 0)]);
            t = t.sqrMagnitude > 1e-6f ? t.normalized : Vector2.up;
            Vector2 nrm = new Vector2(t.y, -t.x);

            float w = path.width * (1f + (Mathf.PerlinNoise(acc * 0.17f + salt, 3.1f) - 0.5f) * 0.28f);
            float taper = 1f;
            if (path.fadeStart) taper = Mathf.Min(taper, Mathf.SmoothStep(0.1f, 1f, acc / 3.2f));
            if (path.fadeEnd) taper = Mathf.Min(taper, Mathf.SmoothStep(0.1f, 1f, (total - acc) / 3.2f));
            float half = w * 0.5f * Mathf.Max(0.15f, taper);

            for (int c = 0; c < cols; c++)
            {
                float lateral = PathOff[c] * half;
                if (c == 0 || c == cols - 1) lateral += (Mathf.PerlinNoise(acc * 0.6f + salt, c * 3.3f) - 0.5f) * 0.45f;   // рваная кромка
                Vector2 pos = s[i] + nrm * lateral;
                bool outer = c == 0 || c == cols - 1;
                float y = GroundY(pos.x, pos.y) + (outer ? groundLift * 0.35f : groundLift * 1.5f);

                int ring = outer ? 4 : (c == 1 || c == cols - 2) ? 3 : (c == 2 || c == cols - 3) ? 2 : 0;
                Color col = DirtAt(pos, ring, salt);
                float inside = DiscInfluence(pos);
                if (inside > 0f) col = Color.Lerp(col, DirtAt(pos, ring == 4 ? 3 : Mathf.Min(ring, 1), salt), inside);
                if (taper < 1f) col = Color.Lerp(grassEdge, col, Mathf.Clamp01(taper * 1.4f));
                // колея по центру главной тропы: две едва заметные тёмные полосы
                if ((c == 2 || c == cols - 3) && path.width > 2.8f && inside < 0.5f) col = Color.Lerp(col, dirtEdge, 0.35f);
                idx[i, c] = ground.Vert(new Vector3(pos.x, y, pos.y), col);
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int c = 0; c < cols - 1; c++)
            {
                ground.TriUp(idx[i, c], idx[i + 1, c], idx[i + 1, c + 1]);
                ground.TriUp(idx[i, c], idx[i + 1, c + 1], idx[i, c + 1]);
            }
    }
}

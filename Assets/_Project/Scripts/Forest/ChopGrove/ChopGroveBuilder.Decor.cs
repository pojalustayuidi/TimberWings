// Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBuilder.Decor.cs (НЕ в Editor)
// Мелочь по свободным местам: фиолетовые камни, бирюзовые и зелёные кусты, цветы, пучки травы, камешки и трава по кромкам троп.
// Места выбираются случайно (seed), но только там, где Clearance() > 0: не на тропах, площадках и реквизите.
using System.Collections.Generic;
using UnityEngine;

public partial class ChopGroveBuilder
{
    bool Spot(float minR, float maxR, float clr, out float x, out float z)
    {
        for (int t = 0; t < 30; t++)
        {
            float a = props.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(props.Range(minR * minR, maxR * maxR));
            x = Mathf.Cos(a) * r;
            z = Mathf.Sin(a) * r + 0.8f;
            if (Clearance(x, z) >= clr) return true;
        }
        x = z = 0f;
        return false;
    }

    void BuildDecor()
    {
        float d = scatterDensity;
        if (d <= 0f) return;
        float outer = clearingRadius + 4.5f;

        // камни у ворот: тяжёлые, как на референсе
        Rock(-4.5f, -7.3f, 0.75f); Rock(4.7f, -7.4f, 0.60f); Rock(-6.0f, -6.6f, 0.45f);
        Bush(-3.6f, -8.0f, 0.62f); Bush(3.7f, -8.1f, 0.55f); Bush(5.3f, -6.6f, 0.5f);

        float x, z;
        for (int i = 0, n = Mathf.RoundToInt(8 * d); i < n; i++) if (Spot(4f, outer, 1.2f, out x, out z)) Rock(x, z, props.Range(0.35f, 0.85f));
        for (int i = 0, n = Mathf.RoundToInt(16 * d); i < n; i++) if (Spot(4.5f, outer, 1.0f, out x, out z)) Bush(x, z, props.Range(0.45f, 0.8f));
        for (int i = 0, n = Mathf.RoundToInt(26 * d); i < n; i++) if (Spot(3f, outer, 0.8f, out x, out z)) FlowerCluster(x, z);
        for (int i = 0, n = Mathf.RoundToInt(44 * d); i < n; i++) if (Spot(2.5f, outer, 0.6f, out x, out z)) Tuft(x, z);

        PathEdges(d);
    }

    void Rock(float x, float z, float r)
    {
        float y = Y(x, z);
        Color c = props.Rand() < 0.8f ? GrovePal.Rock : GrovePal.Stone;
        props.Blob(new Vector3(x, y + r * 0.22f, z), new Vector3(r, r * props.Range(0.55f, 0.8f), r * props.Range(0.75f, 1f)), c, 0.18f, 0.08f);
        if (r > 0.5f) props.Blob(new Vector3(x + r * 0.9f, y + r * 0.05f, z + r * 0.3f), new Vector3(r * 0.45f, r * 0.30f, r * 0.4f), GrovePal.StoneDark, 0.15f, 0.08f);
        foot.Add(new Vector3(x, z, r * 0.9f));
    }

    void Bush(float x, float z, float s)
    {
        float y = Y(x, z);
        float roll = props.Rand();
        Color c = roll < 0.45f ? GrovePal.Bush : roll < 0.85f ? GrovePal.BushTeal : GrovePal.Moss;
        props.Blob(new Vector3(x, y + s * 0.45f, z), new Vector3(s, s * 0.78f, s), c, 0.14f, 0.10f);
        props.Blob(new Vector3(x + s * 0.7f, y + s * 0.30f, z + s * 0.25f), new Vector3(s * 0.62f, s * 0.5f, s * 0.6f), c, 0.14f, 0.10f);
        props.Blob(new Vector3(x - s * 0.5f, y + s * 0.28f, z - s * 0.4f), new Vector3(s * 0.55f, s * 0.46f, s * 0.55f), c, 0.14f, 0.10f);
        foot.Add(new Vector3(x, z, s * 1.1f));
    }

    void FlowerCluster(float x, float z)
    {
        Color c = GrovePal.Flowers[props.rng.Next(GrovePal.Flowers.Length)];
        int n = props.rng.Next(3, 7);
        for (int i = 0; i < n; i++)
        {
            float fx = x + props.Range(-0.35f, 0.35f), fz = z + props.Range(-0.35f, 0.35f);
            float y = Y(fx, fz), h = props.Range(0.14f, 0.26f);
            props.Box(new Vector3(fx, y + h * 0.5f, fz), new Vector3(0.02f, h, 0.02f), GrovePal.GrassDark, 0.05f);
            props.Octa(new Vector3(fx, y + h + 0.03f, fz), new Vector3(0.075f, 0.055f, 0.075f), i == 0 && props.Rand() < 0.3f ? GrovePal.Flowers[0] : c, 0.08f);
        }
        foot.Add(new Vector3(x, z, 0.4f));
    }

    void Tuft(float x, float z)
    {
        float y = Y(x, z);
        Color c = props.Rand() < 0.6f ? GrovePal.Grass : GrovePal.GrassDark;
        for (int i = 0; i < 4; i++)
        {
            float ox = props.Range(-0.12f, 0.12f), oz = props.Range(-0.12f, 0.12f);
            props.Cone(new Vector3(x + ox, y - 0.02f, z + oz), props.Range(0.035f, 0.06f), props.Range(0.22f, 0.42f), 3, c, 0.10f);
        }
    }

    // трава, камешки и редкие цветы вдоль троп: кромка перестаёт быть «линией» и срастается с лугом
    void PathEdges(float d)
    {
        if (paths == null) return;
        for (int pi = 0; pi < paths.Length; pi++)
        {
            var path = paths[pi];
            if (path == null) continue;
            var s = path.Sample(0.45f);
            int step = Mathf.Max(1, Mathf.RoundToInt(1.1f / 0.45f / Mathf.Max(0.25f, d)));
            for (int i = 2; i < s.Count - 2; i += step)
            {
                Vector2 t = (s[i + 1] - s[i - 1]).normalized;
                Vector2 nrm = new Vector2(t.y, -t.x);
                float side = props.Rand() < 0.5f ? -1f : 1f;
                float off = path.width * 0.5f + props.Range(0.18f, 0.55f);
                Vector2 p = s[i] + nrm * side * off;
                if (path.fadeStart && i < 6) continue;
                if (path.fadeEnd && i > s.Count - 7) continue;
                if (Clearance(p.x, p.y) < 0.15f) continue;
                float roll = props.Rand();
                if (roll < 0.55f) Tuft(p.x, p.y);
                else if (roll < 0.80f) Pebble(p.x, p.y);
                else FlowerCluster(p.x, p.y);
            }
        }
    }

    void Pebble(float x, float z)
    {
        float y = Y(x, z), r = props.Range(0.07f, 0.15f);
        props.Octa(new Vector3(x, y + r * 0.35f, z), new Vector3(r, r * 0.6f, r * 0.85f), props.Rand() < 0.7f ? GrovePal.Stone : GrovePal.Metal, 0.08f);
    }
}

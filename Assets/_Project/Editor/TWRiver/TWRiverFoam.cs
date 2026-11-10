using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    // Small wakes attached to real boulders. The water shader handles the current
    // and crest, without another layer of white scribbles over the whole river.
    internal static class TWRiverFoam
    {
        public static void Build(TWRiverCtx x)
        {
            foreach (Vector4 stone in x.stones)
            {
                if (Mathf.Abs(stone.y) > x.half - 0.4f) continue;
                Vector3 centre = x.P(stone.x, stone.y, x.Yw(stone.x) + 0.055f);
                if (x.Protected(centre, 1f)) continue;
                Vector2 tangent = x.ax.Tangent(stone.x);
                x.foam.Ring(centre, new Vector3(tangent.x, 0f, tangent.y), stone.w * 1.04f,
                            stone.w * 0.9f, 0.13f, 0.45f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = new List<Vector3>(); var widths = new List<float>(); var alpha = new List<float>();
                    float length = 2.0f + stone.w;
                    for (int i = 0; i <= 6; i++)
                    {
                        float t = (float)i / 6;
                        float s = Mathf.Min(x.ax.Length - 0.25f, stone.x + stone.w + length * t);
                        float off = stone.y + side * (stone.w * 0.7f + length * t * 0.18f);
                        off = Mathf.Clamp(off, -x.half + 0.2f, x.half - 0.2f);
                        p.Add(x.P(s, off, x.Yw(s) + 0.055f));
                        widths.Add(Mathf.Lerp(0.07f, 0.14f, t));
                        alpha.Add(Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.42f);
                    }
                    x.foam.Strip(p, widths, alpha);
                }
            }
        }
    }
}

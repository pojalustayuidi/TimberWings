// Assets/_Project/Scripts/UI/Map/MapLineGraphic.cs (НЕ в Editor)
// Линия по точкам для карты, одним мешем UI (без спрайтов и без лишних объектов):
//   Dashed  пунктир с индиго-подложкой и «бегущими» штрихами (маршрут до цели). Штрихи могут быть разных цветов (dashColors:
//           по кругу, цвет штриха не меняется, пока он бежит) и с круглыми концами (roundCaps), как конфетки
//   Trail   тонкая сплошная линия, прозрачность растёт к концу (след игрока); alphas[i] = прозрачность точки i
// Точки в координатах RectTransform этого объекта. Рисуется внутри Canvas, поэтому ходит с картой и обрезается её маской.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapLineGraphic : MaskableGraphic
{
    public enum Mode { Dashed, Trail }

    const int MaxDashes = 600;       // больше штрихов на одну линию не строим
    const float MinStep = 0.05f;

    public Mode mode = Mode.Dashed;
    public float thickness = 4f;
    public float dash = 14f, gap = 10f;
    public float phase;                                  // сдвиг штрихов; растёт со временем, штрихи бегут к цели
    public Color outline = new Color(0.17f, 0.18f, 0.36f, 0.62f);   // индиго Candy, полупрозрачный
    [Tooltip("Цвета штрихов по кругу (пусто: все штрихи цветом color)")]
    public Color[] dashColors;
    [Tooltip("Круглые концы штрихов")]
    public bool roundCaps;
    public float outlinePad = 1.6f;                      // на сколько px подложка шире линии с каждой стороны

    public readonly List<Vector2> points = new List<Vector2>(64);
    public readonly List<float> alphas = new List<float>(64);

    public void Refresh() { SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = points.Count;
        if (n < 2) return;
        if (float.IsNaN(phase) || float.IsInfinity(phase)) phase = 0f;
        for (int i = 0; i < n; i++)                          // NaN и бесконечность в точках дали бы бесконечный цикл
            if (float.IsNaN(points[i].x) || float.IsNaN(points[i].y) || float.IsInfinity(points[i].x) || float.IsInfinity(points[i].y)) return;

        if (mode == Mode.Trail)
        {
            for (int i = 0; i < n - 1; i++)
            {
                float a0 = i < alphas.Count ? alphas[i] : 1f, a1 = i + 1 < alphas.Count ? alphas[i + 1] : 1f;
                Quad(vh, points[i], points[i + 1], thickness, color, a0, a1);
            }
            return;
        }

        // пунктир: сначала подложка, потом штрихи (в одном меше порядок индексов = порядок отрисовки)
        for (int pass = 0; pass < 2; pass++)
        {
            float th = pass == 0 ? thickness + outlinePad * 2f : thickness;
            float ext = pass == 0 ? outlinePad : 0f;                // подложка длиннее штриха на концах
            Color col = pass == 0 ? outline : color;
            float dashLen = Mathf.Max(0.5f, dash), gapLen = Mathf.Max(0.5f, gap);
            float period = dashLen + gapLen;
            float d = 0f;
            int quads = 0;                                   // предохранитель: штрихов не больше maxDashes на линию
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                Vector2 dir = (b - a) / len;
                float t = 0f;
                while (t < len)
                {
                    if (++quads > MaxDashes) { i = n; break; }       // выходим из обоих циклов, но подложка и штрихи остаются нарисованными
                    float m = Mathf.Repeat(d + t - phase, period);          // положение внутри «штрих + пробел»
                    if (m < dashLen)
                    {
                        float seg = Mathf.Max(MinStep, Mathf.Min(dashLen - m, len - t));
                        Color dc = col;
                        if (pass == 1 && dashColors != null && dashColors.Length > 0)
                        {
                            int k = Mathf.FloorToInt((d + t - phase) / period);        // номер штриха: «бежит» вместе с ним
                            int cn = dashColors.Length;
                            dc = dashColors[((k % cn) + cn) % cn];
                        }
                        if (roundCaps) Capsule(vh, a + dir * (t - ext), a + dir * (t + seg + ext), th, dc);
                        else Quad(vh, a + dir * (t - ext), a + dir * (t + seg + ext), th, dc, 1f, 1f);
                        t += seg;
                    }
                    else t += Mathf.Max(MinStep, Mathf.Min(period - m, len - t));   // шаг не меньше MinStep: иначе на длинной линии t не растёт
                    if (t >= len - 0.0001f) break;
                }
                d += len;
            }
        }
    }

    // штрих с круглыми концами: прямоугольник между центрами кружков и по кружку на каждом конце.
    // Длина штриха остаётся прежней (кружки входят в a..b), короткий штрих превращается в точку
    static void Capsule(VertexHelper vh, Vector2 a, Vector2 b, float th, Color col)
    {
        float r = th * 0.5f;
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.001f) { Disc(vh, a, r, col); return; }
        dir /= len;
        if (len <= th) { Disc(vh, (a + b) * 0.5f, Mathf.Min(r, len * 0.5f + r * 0.35f), col); return; }
        Vector2 ca = a + dir * r, cb = b - dir * r;
        Quad(vh, ca, cb, th, col, 1f, 1f);
        Disc(vh, ca, r, col);
        Disc(vh, cb, r, col);
    }

    // круг веером из 10 треугольников
    static void Disc(VertexHelper vh, Vector2 c, float r, Color col)
    {
        const int N = 10;
        int i = vh.currentVertCount;
        vh.AddVert(c, col, Vector2.zero);
        for (int k = 0; k < N; k++)
        {
            float ang = k * (Mathf.PI * 2f / N);
            vh.AddVert(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r, col, Vector2.zero);
        }
        for (int k = 0; k < N; k++) vh.AddTriangle(i, i + 1 + k, i + 1 + (k + 1) % N);
    }

    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, float th, Color col, float a0, float a1)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.001f) return;
        dir /= len;
        Vector2 nrm = new Vector2(-dir.y, dir.x) * (th * 0.5f);
        Color c0 = col, c1 = col;
        c0.a *= a0; c1.a *= a1;
        int i = vh.currentVertCount;
        vh.AddVert(a - nrm, c0, Vector2.zero);
        vh.AddVert(a + nrm, c0, Vector2.zero);
        vh.AddVert(b + nrm, c1, Vector2.zero);
        vh.AddVert(b - nrm, c1, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}

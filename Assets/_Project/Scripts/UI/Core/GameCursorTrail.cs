// Assets/_Project/Scripts/UI/Core/GameCursorTrail.cs (НЕ в Editor)
// Золотой след курсора одним мешем (один draw call): лента с мягким свечением, искры, кольцо клика и свечение за стрелкой.
// Данные ведёт GameCursor (AddSample, Spawn, Ring), здесь только хранение и построение меша. Координаты = пиксели экрана.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameCursorTrail : Graphic
{
    struct Sample { public Vector2 p; public float age, w; }
    struct Spark { public Vector2 p, v; public float age, life, size, rot, spin; public Color col; public bool ring; }

    const int MaxSamples = 64;
    const int MaxSparks = 32;

    static readonly Color HeadCol = new Color(1f, 0.80f, 0.26f, 1f);    // золото у головы
    static readonly Color TailCol = new Color(1f, 0.62f, 0.24f, 1f);    // тёплый янтарь к хвосту
    static readonly Color GlowCol = new Color(1f, 0.72f, 0.20f, 1f);
    static readonly Color CoreCol = new Color(1f, 0.96f, 0.78f, 1f);

    public Texture atlas;
    public float life = 0.22f;          // секунд жизни следа
    public float maxWidth = 7f;         // ширина у головы, px (уже с учётом масштаба экрана)
    public float minSpacing = 3f;       // минимальный шаг между точками, px

    public Vector2 glowPos;             // свечение за стрелкой
    public float glowAlpha, glowSize;

    readonly List<Sample> samples = new List<Sample>(MaxSamples);
    readonly List<Spark> sparks = new List<Spark>(MaxSparks);

    public override Texture mainTexture { get { return atlas; } }

    bool drawnEmpty;                    // последний построенный меш был пустым: в покое его не пересобираем

    public void Clear()
    {
        if (samples.Count == 0 && sparks.Count == 0) return;   // уже пусто: ничего не пересобираем
        samples.Clear(); sparks.Clear(); drawnEmpty = false; SetVerticesDirty();
    }

    /// <summary>Добавить точку следа. w = 0..1: сила следа от скорости (медленно = почти невидим).</summary>
    public void AddSample(Vector2 p, float w)
    {
        if (samples.Count > 0 && (p - samples[samples.Count - 1].p).sqrMagnitude < minSpacing * minSpacing) return;
        if (samples.Count >= MaxSamples) samples.RemoveAt(0);
        samples.Add(new Sample { p = p, age = 0f, w = w });
    }

    public void Spawn(Vector2 p, Vector2 v, float size, float sparkLife)
    {
        if (sparks.Count >= MaxSparks) sparks.RemoveAt(0);
        Color c = Random.value < 0.4f ? Color.white : Color.Lerp(HeadCol, CoreCol, Random.value);
        sparks.Add(new Spark { p = p, v = v, size = size, life = sparkLife, rot = 0f, spin = 0f, col = c });
    }

    public void Ring(Vector2 p, float size, float ringLife)
    {
        if (sparks.Count >= MaxSparks) sparks.RemoveAt(0);
        sparks.Add(new Spark { p = p, size = size, life = ringLife, col = Color.Lerp(HeadCol, CoreCol, 0.3f), ring = true });
    }

    public void Tick(float dt)
    {
        for (int i = samples.Count - 1; i >= 0; i--)
        {
            var s = samples[i];
            s.age += dt;
            if (s.age > life) { samples.RemoveRange(0, i + 1); break; }   // старые лежат в начале списка
            samples[i] = s;
        }
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i];
            s.age += dt;
            if (s.age >= s.life) { sparks.RemoveAt(i); continue; }
            s.p += s.v * dt;
            s.v *= Mathf.Exp(-3f * dt);
            s.rot += s.spin * dt;
            sparks[i] = s;
        }
        bool empty = samples.Count == 0 && sparks.Count == 0 && glowAlpha <= 0.01f;
        if (!empty || !drawnEmpty) SetVerticesDirty();
        drawnEmpty = empty;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (atlas == null) return;

        if (glowAlpha > 0.01f)
            Quad(vh, glowPos, glowSize, 0f, GameCursorArt.TileGlow, new Color(GlowCol.r, GlowCol.g, GlowCol.b, glowAlpha));

        Ribbon(vh, 2.4f, 0.20f, GlowCol, GlowCol);     // мягкое свечение вокруг
        Ribbon(vh, 1.0f, 0.85f, HeadCol, TailCol);     // основная золотая лента
        Ribbon(vh, 0.42f, 1.0f, CoreCol, HeadCol);     // светлая жилка по центру

        for (int i = 0; i < sparks.Count; i++)
        {
            var s = sparks[i];
            float k = s.age / s.life;
            if (s.ring)
            {
                float e = 1f - (1f - k) * (1f - k);
                Quad(vh, s.p, s.size * Mathf.Lerp(0.35f, 1f, e), 0f, GameCursorArt.TileRing, Fade(s.col, 0.8f * Mathf.Pow(1f - k, 1.5f)));
            }
            else
            {
                float a = (1f - k) * Mathf.Min(1f, k * 6f);
                Quad(vh, s.p, s.size * (1f - 0.4f * k), s.rot, GameCursorArt.TileStar, Fade(s.col, a));
            }
        }
    }

    void Ribbon(VertexHelper vh, float widthMul, float alphaMul, Color head, Color tail)
    {
        int n = samples.Count;
        if (n < 2) return;
        Rect uv = GameCursorArt.TileUv(GameCursorArt.TileRibbon);
        float vMid = uv.y + uv.height * 0.5f;
        int baseIdx = vh.currentVertCount;
        for (int i = 0; i < n; i++)
        {
            var s = samples[i];
            float t = Mathf.Clamp01(s.age / life);
            Vector2 d = samples[Mathf.Min(i + 1, n - 1)].p - samples[Mathf.Max(i - 1, 0)].p;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            Vector2 nrm = new Vector2(-d.y, d.x).normalized;
            float half = maxWidth * widthMul * s.w * Mathf.Pow(1f - t, 0.7f) * 0.5f;
            Color c = Color.Lerp(head, tail, t);
            c.a = alphaMul * Mathf.Clamp01(s.w * 1.4f) * Mathf.Pow(1f - t, 1.3f);
            Color32 c32 = c;
            vh.AddVert(s.p - nrm * half, c32, new Vector2(uv.xMin, vMid));
            vh.AddVert(s.p + nrm * half, c32, new Vector2(uv.xMax, vMid));
        }
        for (int i = 0; i < n - 1; i++)
        {
            int l = baseIdx + i * 2;
            vh.AddTriangle(l, l + 1, l + 2);
            vh.AddTriangle(l + 1, l + 3, l + 2);
        }
    }

    static void Quad(VertexHelper vh, Vector2 c, float size, float rotDeg, Vector2Int tile, Color col)
    {
        if (col.a <= 0.004f || size <= 0.1f) return;
        Rect uv = GameCursorArt.TileUv(tile);
        float r = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        float h = size * 0.5f;
        Vector2 ax = new Vector2(cs, sn) * h, ay = new Vector2(-sn, cs) * h;
        int b = vh.currentVertCount;
        Color32 c32 = col;
        vh.AddVert(c - ax - ay, c32, new Vector2(uv.xMin, uv.yMin));
        vh.AddVert(c - ax + ay, c32, new Vector2(uv.xMin, uv.yMax));
        vh.AddVert(c + ax + ay, c32, new Vector2(uv.xMax, uv.yMax));
        vh.AddVert(c + ax - ay, c32, new Vector2(uv.xMax, uv.yMin));
        vh.AddTriangle(b, b + 1, b + 2);
        vh.AddTriangle(b, b + 2, b + 3);
    }

    static Color Fade(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
}

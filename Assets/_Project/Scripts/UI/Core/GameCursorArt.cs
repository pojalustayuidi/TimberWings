// Assets/_Project/Scripts/UI/Core/GameCursorArt.cs (НЕ в Editor)
// Картинки курсора, нарисованные кодом при старте (внешних файлов нет): стрелка (аппаратный курсор, три вида), значок «недоступно»,
// атлас для следа (искра, кольцо, профиль ленты, мягкое свечение). Части курсора: GameCursor (поведение),
// GameCursorTrail (меш следа), этот файл (текстуры).
// Стиль: кремовая стрелка с индиго-контуром, золотым ободком внутри и мягкой тенью.
using UnityEngine;

public static class GameCursorArt
{
    // ---------------------------------------------------------------- стрелка (аппаратный курсор: без задержки, клик точный)
    // Размеры при k = 1 (экран 1080p): текстура 36x48, сама стрелка около 20x32 px, острие в (6,6) от левого верхнего угла.
    const float BaseU = 1.6f;                      // пикселей на одну единицу контура
    const float BaseOutline = 1.5f;                // индиго-контур
    const float BaseRim = 1.1f;                    // золотой ободок внутри контура
    const float BaseHot = 6f;
    static readonly Vector2[] ArrowUnits =
    {
        new Vector2(0f, 0f), new Vector2(0f, 17f), new Vector2(4.4f, 13.2f), new Vector2(7.3f, 20f),
        new Vector2(10.2f, 18.8f), new Vector2(7.4f, 12.1f), new Vector2(12.8f, 12.1f)
    };

    static readonly Color OutlineCol = new Color(0.20f, 0.13f, 0.45f, 1f);
    static readonly Color RimCol = new Color(1f, 0.79f, 0.30f, 1f);
    static readonly Color FillTop = new Color(1f, 1f, 0.97f, 1f);
    static readonly Color FillBottom = new Color(1f, 0.92f, 0.72f, 1f);
    static readonly Color ShadowCol = new Color(0.16f, 0.09f, 0.38f, 1f);

    public const int StateNormal = 0, StateHover = 1, StateDisabled = 2;

    /// <summary>Картинка курсора-стрелки. k = размер (1 на 1080p). state: обычная, наведение (тёплое золото), недоступно (серая). hotspot: пиксели от левого верхнего угла.</summary>
    public static Texture2D BuildCursor(float k, int state, out Vector2 hotspot)
    {
        int w = Mathf.CeilToInt(36f * k), h = Mathf.CeilToInt(48f * k);
        float u = BaseU * k, outline = BaseOutline * k, rim = BaseRim * k, hot = BaseHot * k;
        hotspot = new Vector2(hot, hot);

        var poly = new Vector2[ArrowUnits.Length];
        for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(hot + ArrowUnits[i].x * u, hot + ArrowUnits[i].y * u);
        Vector2 shadowOff = new Vector2(1f, 1.7f) * k;
        float blur = 2.6f * k;

        bool disabled = state == StateDisabled, hover = state == StateHover;
        Color outlineCol = disabled ? new Color(0.36f, 0.34f, 0.50f, 1f) : OutlineCol;
        Color rimCol = disabled ? new Color(0.62f, 0.60f, 0.70f, 1f) : hover ? new Color(1f, 0.66f, 0.16f, 1f) : RimCol;
        Color top = disabled ? new Color(0.80f, 0.80f, 0.86f, 1f) : hover ? new Color(1f, 0.97f, 0.80f, 1f) : FillTop;
        Color bottom = disabled ? new Color(0.66f, 0.66f, 0.74f, 1f) : hover ? new Color(1f, 0.82f, 0.38f, 1f) : FillBottom;
        float alpha = disabled ? 0.88f : 1f;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);   // y вниз: строка 0 = верх картинки

                float sdShadow = PolySd(p - shadowOff, poly) - outline;
                float sh = Mathf.Clamp01(1f - sdShadow / blur);
                var c = ShadowCol;
                c.a = sh * sh * 0.30f;

                float sd = PolySd(p, poly);
                float cover = Mathf.Clamp01(0.5f - (sd - outline));
                if (cover > 0f)
                {
                    float inside = Mathf.Clamp01(0.5f - sd);                 // внутри контура стрелки
                    float fillCover = Mathf.Clamp01(0.5f - (sd + rim));      // глубже золотого ободка
                    float t = Mathf.Clamp01((p.y - hot) / (20f * u));
                    Color fill = Color.Lerp(top, bottom, t);
                    Color body = Color.Lerp(outlineCol, Color.Lerp(rimCol, fill, fillCover), inside);
                    body.a = cover * alpha;
                    c = Over(c, body);
                }
                px[(h - 1 - y) * w + x] = c;     // в текстуре строка 0 = низ
            }
        }
        return MakeTexture(w, h, px);
    }

    // ---------------------------------------------------------------- значок «недоступно» (красный круг с чертой)
    public const int BadgeSize = 44;

    public static Sprite BuildBadge()
    {
        const float cx = BadgeSize * 0.5f, cy = BadgeSize * 0.5f;
        var px = new Color32[BadgeSize * BadgeSize];
        for (int y = 0; y < BadgeSize; y++)
        {
            for (int x = 0; x < BadgeSize; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float shadow = Mathf.Clamp01(1f - (Vector2.Distance(p, new Vector2(cx, cy + 1.6f)) - 17.5f) / 3.5f);
                var c = ShadowCol;
                c.a = shadow * shadow * 0.28f;

                float dc = Vector2.Distance(p, new Vector2(cx, cy));
                float ring = Mathf.Clamp01(0.5f - (dc - 17.5f));
                float red = Mathf.Clamp01(0.5f - (dc - 14.4f));
                Color body = Color.Lerp(Color.white, new Color(0.93f, 0.23f, 0.27f, 1f), red);
                body.a = ring;
                c = Over(c, body);

                float slash = Mathf.Clamp01(0.5f - (SegDist(p, new Vector2(cx - 7.5f, cy - 7.5f), new Vector2(cx + 7.5f, cy + 7.5f)) - 2.5f));
                c = Over(c, new Color(1f, 1f, 1f, slash));
                px[(BadgeSize - 1 - y) * BadgeSize + x] = c;
            }
        }
        var tex = MakeTexture(BadgeSize, BadgeSize, px);
        return Sprite.Create(tex, new Rect(0, 0, BadgeSize, BadgeSize), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---------------------------------------------------------------- атлас следа 128x128, четыре плитки по 64
    public const int AtlasSize = 128, TileSize = 64;
    public static readonly Vector2Int TileStar = new Vector2Int(0, 0);
    public static readonly Vector2Int TileRing = new Vector2Int(1, 0);
    public static readonly Vector2Int TileRibbon = new Vector2Int(0, 1);
    public static readonly Vector2Int TileGlow = new Vector2Int(1, 1);

    /// <summary>UV плитки с отступом в 1.5 px, чтобы соседние плитки не просвечивали при фильтрации.</summary>
    public static Rect TileUv(Vector2Int t)
    {
        const float pad = 1.5f;
        float s = TileSize - 2f * pad;
        return new Rect((t.x * TileSize + pad) / AtlasSize, (t.y * TileSize + pad) / AtlasSize, s / AtlasSize, s / AtlasSize);
    }

    public static Texture2D BuildAtlas()
    {
        var px = new Color32[AtlasSize * AtlasSize];
        for (int y = 0; y < AtlasSize; y++)
        {
            for (int x = 0; x < AtlasSize; x++)
            {
                int tx = x / TileSize, ty = y / TileSize;
                float u = ((x % TileSize) + 0.5f) / TileSize;       // 0..1 внутри плитки
                float v = ((y % TileSize) + 0.5f) / TileSize;
                float p = u * 2f - 1f, q = v * 2f - 1f;             // -1..1
                float r = Mathf.Sqrt(p * p + q * q);
                float a;
                if (tx == 0 && ty == 0)          // искра: мягкая четырёхлучевая звёздочка с яркой точкой в центре
                {
                    float star = Mathf.Pow(Mathf.Abs(p), 0.95f) + Mathf.Pow(Mathf.Abs(q), 0.95f);   // мягкий ромб, а не тонкий крестик
                    float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - star) / 0.55f));
                    float core = Mathf.Exp(-r * r * 7f) * 0.85f;
                    a = Mathf.Max(body, core);
                }
                else if (tx == 1 && ty == 0)     // кольцо для клика
                {
                    float k = (r - 0.72f) / 0.11f;
                    a = Mathf.Exp(-k * k) * Mathf.Clamp01((1f - r) / 0.15f);
                }
                else if (tx == 0 && ty == 1)     // профиль ленты поперёк: мягкие края, плотная середина
                {
                    a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(p)), 1.3f);
                }
                else                             // мягкое круглое свечение
                {
                    a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                }
                px[y * AtlasSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }
        return MakeTexture(AtlasSize, AtlasSize, px);
    }

    // ---------------------------------------------------------------- математика
    static Texture2D MakeTexture(int w, int h, Color32[] px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>Обычное наложение с прямой (не премультиплицированной) альфой.</summary>
    static Color Over(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
        float k = dst.a * (1f - src.a);
        return new Color((src.r * src.a + dst.r * k) / a, (src.g * src.a + dst.g * k) / a, (src.b * src.a + dst.b * k) / a, a);
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a, w = p - a;
        float t = Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
        return (w - e * t).magnitude;
    }

    /// <summary>Расстояние со знаком до многоугольника: минус внутри, плюс снаружи.</summary>
    static float PolySd(Vector2 p, Vector2[] poly)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            d = Mathf.Min(d, SegDist(p, a, b));
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside ? -d : d;
    }
}

// Assets/_Project/Scripts/UI/Progression/AchUi.cs (НЕ в Editor)
// Общие мелочи окна достижений и тоста: цвета рангов, кольцо прогресса, узор фона вкладок, искры и всплывающие надписи.
//   AchUi.TierColor(tier)         цвет ранга (бронза / серебро / золото)
//   AchUi.Ring                    белое кольцо (рисуется кодом), для круговой полоски прогресса вокруг узла
//   AchUi.Pattern(cat)            бесшовная текстура с узором под цвет вкладки (RawImage, wrap = Repeat)
//   AchSparks.Create(parent, якорь)  слой искр: .Burst(...) — россыпь, .FloatText(...) — всплывающее «+30»
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public static class AchUi
{
    public static readonly Color Bronze = new Color32(0xD9, 0x9A, 0x5B, 255);
    public static readonly Color Silver = new Color32(0xC8, 0xD2, 0xE0, 255);
    public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x3D, 255);
    public static readonly Color Outline = new Color32(0x0B, 0x10, 0x20, 255);

    public static Color TierColor(AchTier t)
    {
        switch (t)
        {
            case AchTier.Gold: return Gold;
            case AchTier.Silver: return Silver;
            default: return Bronze;
        }
    }

    public static string TierName(AchTier t)
    {
        switch (t)
        {
            case AchTier.Gold: return "Золото";
            case AchTier.Silver: return "Серебро";
            default: return "Бронза";
        }
    }

    // ------------------------------------------------------------ кольцо
    static Sprite ring;

    public static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[N * N];
            const float px1 = 2f / N;    // один пиксель в единицах -1..1
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((1f - r) / px1) * Mathf.Clamp01((r - 0.80f) / px1);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            ring = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            ring.name = "Ach_Ring";
            return ring;
        }
    }

    // ------------------------------------------------------------ узор фона
    static readonly Texture2D[] patterns = new Texture2D[AchCats.Count];

    // 128x128, бесшовная. Фигурки рассыпаны в стороне от краёв, поэтому стыки не видны.
    // Лагерь: искры-крестики. Лес: ёлочки. Торговля: монетки. Друзья: ромбики. Путь: точки.
    public static Texture2D Pattern(AchCat cat)
    {
        int ci = (int)cat;
        if (patterns[ci] != null) return patterns[ci];
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[N * N];

        var rng = new System.Random(ci * 977 + 13);
        for (int k = 0; k < 8; k++)
        {
            float cx = 16f + (float)rng.NextDouble() * 96f, cy = 16f + (float)rng.NextDouble() * 96f;
            float s = 4f + (float)rng.NextDouble() * 3.5f;
            float alpha = 0.10f + (float)rng.NextDouble() * 0.10f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - s - 2f)), x1 = Mathf.Min(N - 1, Mathf.CeilToInt(cx + s + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - s - 2f)), y1 = Mathf.Min(N - 1, Mathf.CeilToInt(cy + s + 2f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    bool on;
                    switch (ci)
                    {
                        case 0: on = (Mathf.Abs(dx) < 0.9f && Mathf.Abs(dy) < s) || (Mathf.Abs(dy) < 0.9f && Mathf.Abs(dx) < s); break;
                        case 1: on = dy > -s && dy < s && Mathf.Abs(dx) < (s - dy) * 0.42f; break;
                        case 2: on = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - s) < 1.0f; break;
                        case 3: on = Mathf.Abs(dx) + Mathf.Abs(dy) < s; break;
                        default: on = dx * dx + dy * dy < s * s * 0.25f; break;
                    }
                    if (!on) continue;
                    int i = y * N + x;
                    byte a = (byte)Mathf.Clamp(px[i].a + Mathf.RoundToInt(alpha * 255f), 0, 255);
                    px[i] = new Color32(255, 255, 255, a);
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        tex.name = "Ach_Pattern_" + cat;
        patterns[ci] = tex;
        return tex;
    }

    // ------------------------------------------------------------ виньетка: края фона темнеют, в центре светло (глубина)
    static Sprite vignette;

    public static Sprite Vignette
    {
        get
        {
            if (vignette != null) return vignette;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                    float k = Mathf.Clamp01((r - 0.45f) / 0.85f);
                    float a = k * k * (3f - 2f * k) * 0.78f;
                    px[y * N + x] = new Color32(2, 4, 12, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            vignette = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            vignette.name = "Ach_Vignette";
            return vignette;
        }
    }

    // ------------------------------------------------------------ мелкая сборка
    public static void Wrap2(TMP_Text t, float min, float max)
    {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    public static void Outlined(TMP_Text t, float width = 0.22f)
    {
        t.outlineWidth = width;
        t.outlineColor = Outline;
    }

    public static float Ph(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }
}

// ============================================================================================
//  Слой искр и всплывающих надписей. Нулевого размера, начало координат = якорь (anchor == pivot) в родителе.
// ============================================================================================
public class AchSparks : MonoBehaviour
{
    class P { public RectTransform rt; public Image img; public Vector2 pos, vel; public float age, life, size, spin, grav, rot; public Color col; public bool on; }
    class F { public RectTransform rt; public TMP_Text tx; public Vector2 pos; public float age, life; public Color col; public bool on; }

    const int MaxP = 110, MaxF = 8;
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    readonly List<P> ps = new List<P>();
    readonly List<F> fs = new List<F>();

    public static AchSparks Create(Transform parent, Vector2 anchor)
    {
        var rt = NewRT("Sparks", parent);
        Anch(rt, anchor, anchor, Vector2.zero, Vector2.zero);
        return rt.gameObject.AddComponent<AchSparks>();
    }

    // россыпь искр из точки pos. speed: пикселей в секунду, gravity: пикселей в секунду^2 (вниз)
    public void Burst(Vector2 pos, Color c, int count, float speed, float size = 24f, float gravity = 0f, float life = 0.9f)
    {
        for (int i = 0; i < count; i++)
        {
            var p = Spawn();
            if (p == null) return;
            float a = Random.value * Mathf.PI * 2f, sp = speed * Random.Range(0.3f, 1f);
            p.pos = pos;
            p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp;
            p.age = 0f;
            p.life = life * Random.Range(0.7f, 1.25f);
            p.size = size * Random.Range(0.45f, 1f);
            p.spin = Random.Range(-260f, 260f);
            p.rot = Random.Range(0f, 90f);
            p.grav = gravity;
            p.col = Color.Lerp(c, Color.white, Random.Range(0f, 0.65f));
            p.on = true;
            p.rt.gameObject.SetActive(true);
        }
    }

    public void FloatText(Vector2 pos, string text, Color c, float size = 36f)
    {
        F f = null;
        for (int i = 0; i < fs.Count; i++) if (!fs[i].on) { f = fs[i]; break; }
        if (f == null)
        {
            if (fs.Count >= MaxF) return;
            f = new F();
            f.tx = UiKit.MakeLabel(transform, "", size, c, TextAlignmentOptions.Center, true);
            f.rt = f.tx.rectTransform;
            Anch(f.rt, Mid, Mid, Vector2.zero, new Vector2(360f, 70f));
            AchUi.Outlined(f.tx);
            fs.Add(f);
        }
        f.tx.fontSize = size;
        f.tx.text = text;
        f.col = c;
        f.pos = pos;
        f.age = 0f;
        f.life = 1.25f;
        f.on = true;
        f.rt.gameObject.SetActive(true);
        f.rt.SetAsLastSibling();
    }

    P Spawn()
    {
        for (int i = 0; i < ps.Count; i++) if (!ps[i].on) return ps[i];
        if (ps.Count >= MaxP) return null;
        var p = new P();
        p.img = UiKit.MakeImage(transform, "Spark", PetUi.SparkSprite, false);
        p.img.raycastTarget = false;
        p.rt = p.img.rectTransform;
        Anch(p.rt, Mid, Mid, Vector2.zero, new Vector2(24f, 24f));
        p.rt.gameObject.SetActive(false);
        ps.Add(p);
        return p;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < ps.Count; i++)
        {
            var p = ps[i];
            if (!p.on) continue;
            p.age += dt;
            if (p.age >= p.life) { p.on = false; p.rt.gameObject.SetActive(false); continue; }
            p.vel.y -= p.grav * dt;
            p.vel *= Mathf.Max(0f, 1f - 1.6f * dt);
            p.pos += p.vel * dt;
            p.rot += p.spin * dt;
            float k = p.age / p.life;
            float s = p.size * (1f - 0.55f * k);
            p.rt.anchoredPosition = p.pos;
            p.rt.sizeDelta = new Vector2(s, s);
            p.rt.localEulerAngles = new Vector3(0f, 0f, p.rot);
            var c = p.col; c.a = 1f - k * k;
            p.img.color = c;
        }
        for (int i = 0; i < fs.Count; i++)
        {
            var f = fs[i];
            if (!f.on) continue;
            f.age += dt;
            if (f.age >= f.life) { f.on = false; f.rt.gameObject.SetActive(false); continue; }
            float k = f.age / f.life;
            f.rt.anchoredPosition = f.pos + new Vector2(0f, 90f * UiDraw.EaseOutCubic(k));
            f.rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, UiDraw.EaseOutBack(Mathf.Clamp01(f.age / 0.25f)));
            var c = f.col; c.a = k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f;
            f.tx.color = c;
        }
    }
}

// ============================================================================================
//  Парящие огоньки на фоне вкладки. Каждый на своей «глубине»: дальние движутся меньше (параллакс) и тусклее.
//  Стиль под вкладку: Лагерь — угольки вверх, Лес — светлячки, Торговля — блики, Друзья — тёплые пылинки, Путь — звёзды.
//  Положение камеры (сдвиг дерева) и мыши window передаёт сам в cam и mouse.
// ============================================================================================
public class AchMotes : MonoBehaviour
{
    const int N = 38;
    //                      подъём  качание  мерцание  размер
    static readonly Vector4[] Styles =
    {
        new Vector4(58f, 16f, 2.2f, 1.00f),   // Лагерь
        new Vector4(5f,  34f, 1.5f, 1.15f),   // Лес
        new Vector4(0f,  6f,  3.2f, 0.85f),   // Торговля
        new Vector4(18f, 24f, 1.2f, 1.10f),   // Друзья
        new Vector4(0f,  10f, 2.6f, 0.80f),   // Путь
    };

    struct M { public float x, y, depth, size, phase, speed; }

    readonly RectTransform[] rts = new RectTransform[N];
    readonly Image[] imgs = new Image[N];
    readonly M[] ms = new M[N];
    float w, h, t;
    Vector4 st = new Vector4(20f, 20f, 2f, 1f);
    Color col = Color.white;
    public Vector2 cam, mouse;     // сдвиг дерева и положение мыши (-1..1)

    public static AchMotes Create(Transform parent, float w, float h)
    {
        var rt = UiLayout.NewRT("Motes", parent);
        UiLayout.Stretch(rt);
        var m = rt.gameObject.AddComponent<AchMotes>();
        m.w = w; m.h = h;
        var rng = new System.Random(4242);
        for (int i = 0; i < N; i++)
        {
            var img = UiKit.MakeImage(rt, "Mote", PetUi.SparkSprite, false);
            img.raycastTarget = false;
            m.imgs[i] = img;
            m.rts[i] = img.rectTransform;
            UiLayout.Anch(m.rts[i], new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            m.ms[i] = new M
            {
                x = (float)rng.NextDouble() * (w + 60f), y = (float)rng.NextDouble() * (h + 60f),
                depth = 0.25f + (float)rng.NextDouble() * 0.85f,
                size = 9f + (float)rng.NextDouble() * 15f,
                phase = (float)rng.NextDouble() * 6.28f,
                speed = 0.6f + (float)rng.NextDouble() * 0.9f
            };
        }
        return m;
    }

    public void SetStyle(int cat) { st = Styles[Mathf.Clamp(cat, 0, Styles.Length - 1)]; }
    public void SetColor(Color c) { col = c; }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        for (int i = 0; i < N; i++)
        {
            var m = ms[i];
            float d = m.depth;
            float x = m.x + Mathf.Sin(t * 0.55f * m.speed + m.phase) * st.y * d + cam.x * d * 0.5f - mouse.x * 22f * d;
            float y = m.y - st.x * d * t - cam.y * d * 0.5f + mouse.y * 22f * d;
            x = Mathf.Repeat(x + 30f, w + 60f) - 30f;
            y = Mathf.Repeat(y + 30f, h + 60f) - 30f;
            rts[i].anchoredPosition = new Vector2(x, -y);
            float tw = 0.5f + 0.5f * Mathf.Sin(t * st.z * m.speed + m.phase * 3f);
            float s = m.size * d * st.w * (0.7f + 0.5f * tw);
            rts[i].sizeDelta = new Vector2(s, s);
            Color c = Color.Lerp(col, Color.white, 0.25f + 0.4f * tw);
            c.a = Mathf.Clamp01((0.18f + 0.55f * d) * (0.35f + 0.65f * tw));
            imgs[i].color = c;
        }
    }
}

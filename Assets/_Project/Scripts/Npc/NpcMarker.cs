// Assets/_Project/Scripts/Npc/NpcMarker.cs (НЕ в Editor)
// Кружок «!» над NPC: покачивается, чуть пульсирует, всегда повёрнут к камере.
// Картинку можно заменить на свою (SpriteRenderer.Sprite), а пока рисуется кодом.
// DialogueSequence сам прячет кружок, пока NPC говорит, и после разговора.
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class NpcMarker : MonoBehaviour
{
    public float size = 0.9f;          // множитель размера
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.2f;
    public bool billboard = true;

    SpriteRenderer sr;
    Vector3 basePos;
    float show = 1f;
    bool visible = true;
    static Sprite fallback;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            if (fallback == null)
                fallback = Sprite.Create(MakeTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            sr.sprite = fallback;
        }
        sr.sortingOrder = 50;
        basePos = transform.localPosition;
    }

    public void SetVisible(bool v) { visible = v; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        show = Mathf.MoveTowards(show, visible ? 1f : 0f, dt / 0.25f);
        sr.enabled = show > 0.001f;
        if (!sr.enabled) return;

        float t = Time.time;
        float pop = UiDraw.EaseOutBack(Mathf.Clamp01(show));
        float pulse = 1f + 0.06f * Mathf.Sin(t * 4f);
        transform.localScale = Vector3.one * (size * pop * pulse);
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);

        var c = sr.color;
        c.a = Mathf.Clamp01(show * 1.5f);
        sr.color = c;

        if (billboard)
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    // ---------------------------------------------------------------- рисование кружка (128x128)
    static readonly Color Ink = new Color(0.043f, 0.063f, 0.125f, 1f);
    static readonly Color Navy = new Color(0.086f, 0.125f, 0.227f, 1f);

    public static Texture2D MakeTexture()
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = Pix(x + 0.5f, y + 0.5f);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static float Cov(float d) { return Mathf.Clamp01(0.5f - d); }

    static float BoxD(float x, float y, float hw, float hh, float r)
    {
        float qx = Mathf.Abs(x) - (hw - r), qy = Mathf.Abs(y) - (hh - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static Color Over(Color d, Color s, float cov)
    {
        float a = s.a * cov;
        if (a <= 0f) return d;
        float oa = a + d.a * (1f - a);
        if (oa <= 1e-5f) return Color.clear;
        float k = d.a * (1f - a);
        return new Color((s.r * a + d.r * k) / oa, (s.g * a + d.g * k) / oa, (s.b * a + d.b * k) / oa, oa);
    }

    // круг-«облачко» с хвостиком вниз-влево и восклицательным знаком
    static Color Pix(float x, float y)
    {
        float dc = Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 70f) * (y - 70f)) - 50f;
        float u = ((x - 50f) + (y - 30f)) * 0.70711f;
        float v = ((y - 30f) - (x - 50f)) * 0.70711f;
        float dt = BoxD(u, v, 10f, 10f, 2.5f);
        float d = Mathf.Min(dc, dt);

        Color c = Color.clear;
        c = Over(c, Ink, Cov(d - 3.5f));

        float t = Mathf.Clamp01(y / 128f);
        Color fill = Color.Lerp(new Color(0.91f, 0.66f, 0.16f, 1f), new Color(1f, 0.89f, 0.48f, 1f), t);
        c = Over(c, fill, Cov(d));

        // блик по верхнему левому краю
        float hl = Mathf.Clamp01(((64f - x) + (y - 70f)) / 60f);
        c = Over(c, new Color(1f, 1f, 1f, 0.55f * hl), Cov(Mathf.Abs(dc + 8f) - 1.2f));

        // «!»
        float bar = BoxD(x - 64f, y - 78f, 7f, 20f, 6f);
        float dot = Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 44f) * (y - 44f)) - 8f;
        c = Over(c, Navy, Cov(Mathf.Min(bar, dot)));
        return c;
    }
}
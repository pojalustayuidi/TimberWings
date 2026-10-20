// Assets/_Project/Scripts/UI/PetInteract.cs (НЕ в Editor)
// Интерактив в окне «Питомцы»:
//   PetPetZone   прозрачная зона поверх живой модели питомца: клик = погладить, перетаскивание = покрутить рукой
//                (мышь и палец одинаково; клик не срабатывает, если мышь успела сдвинуться на порог перетаскивания).
//   PetHeartFx   сердечки, которые взлетают над питомцем при поглаживании (спрайт сердца рисуется кодом, картинок не нужно).
//                Координаты как у AchSparks: от центра родителя (окно считает их через InnerToFx).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PetPetZone : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public System.Action onPat;               // клик: погладить
    public System.Action<float> onDrag;       // смещение мыши по X в пикселях экрана: покрутить

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && onPat != null) onPat();
    }

    public void OnBeginDrag(PointerEventData e) { }

    public void OnDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && onDrag != null) onDrag(e.delta.x);
    }

    public void OnEndDrag(PointerEventData e) { }
}

public class PetHeartFx : MonoBehaviour
{
    class H
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos, vel;
        public float age, life, size, sway, phase;
        public Color col;
        public bool on;
    }

    public static readonly Color Pink = new Color32(0xFF, 0x6E, 0x9C, 255);
    const int Max = 28;
    const float Base = 24f;                         // размер картинки в сцене, дальше масштабом
    static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    static Sprite heart;

    readonly List<H> hs = new List<H>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { heart = null; }

    public static Sprite HeartSprite
    {
        get
        {
            if (heart == null) heart = MakeHeart();
            return heart;
        }
    }

    // классическая кривая сердца (x^2 + y^2 - 1)^3 - x^2 y^3 = 0, сглаживание 3x3 точки на пиксель
    static Sprite MakeHeart()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float cov = 0f;
                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 3f) / N * 2f - 1f;
                        float v = (y + (sy + 0.5f) / 3f) / N * 2f - 1f;
                        float hx = u * 1.25f, hy = v * 1.25f + 0.12f;
                        float a = hx * hx + hy * hy - 1f;
                        if (a * a * a - hx * hx * hy * hy * hy <= 0f) cov += 1f / 9f;
                    }
                }
                // лёгкий блик: верх-лево чуть светлее, низ-право чуть темнее (красится через Image.color)
                float shade = Mathf.Clamp01(0.88f + 0.12f * (((float)(N - y) / N) * 0.5f + ((float)(N - x) / N) * 0.5f));
                byte g = (byte)Mathf.RoundToInt(255f * shade);
                px[y * N + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(cov * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var s = Sprite.Create(tex, new Rect(0, 0, N, N), Mid, 100f);
        s.name = "Pet_Heart";
        return s;
    }

    public static PetHeartFx Create(Transform parent, Vector2 anchor)
    {
        var rt = UiLayout.NewRT("Hearts", parent);
        UiLayout.Anch(rt, anchor, anchor, Vector2.zero, Vector2.zero);
        return rt.gameObject.AddComponent<PetHeartFx>();
    }

    // count сердечек вокруг точки pos (по ширине spread в обе стороны); size: примерный размер в пикселях
    public void Burst(Vector2 pos, int count, float spread = 70f, float size = 30f)
    {
        for (int i = 0; i < count; i++)
        {
            H h = Spawn();
            if (h == null) return;
            h.pos = pos + new Vector2(Random.Range(-spread, spread), Random.Range(-spread * 0.4f, spread * 0.4f));
            h.vel = new Vector2(Random.Range(-24f, 24f), Random.Range(120f, 220f));
            h.age = -Random.Range(0f, 0.2f);                // небольшая задержка: взлетают не разом
            h.life = Random.Range(0.95f, 1.45f);
            h.size = size * Random.Range(0.6f, 1.15f);
            h.sway = Random.Range(8f, 24f);
            h.phase = Random.value * 6.2832f;
            h.col = Color.Lerp(Pink, Color.white, Random.Range(0f, 0.35f));
            h.on = true;
            h.img.enabled = false;
            h.rt.gameObject.SetActive(true);
        }
    }

    H Spawn()
    {
        for (int i = 0; i < hs.Count; i++) if (!hs[i].on) return hs[i];
        if (hs.Count >= Max) return null;
        var h = new H();
        h.img = UiKit.MakeImage(transform, "Heart", HeartSprite, false);
        h.img.raycastTarget = false;
        h.img.preserveAspect = true;
        h.rt = h.img.rectTransform;
        UiLayout.Anch(h.rt, Mid, Mid, Vector2.zero, new Vector2(Base, Base));
        h.rt.gameObject.SetActive(false);
        hs.Add(h);
        return h;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < hs.Count; i++)
        {
            H h = hs[i];
            if (!h.on) continue;
            h.age += dt;
            if (h.age < 0f) continue;                       // ещё ждёт своей очереди
            float t = h.age / h.life;
            if (t >= 1f)
            {
                h.on = false;
                h.rt.gameObject.SetActive(false);
                continue;
            }
            h.img.enabled = true;
            h.pos += h.vel * dt;
            h.vel.y = Mathf.Max(40f, h.vel.y - 90f * dt);   // подъём чуть замедляется
            float x = Mathf.Sin(h.age * 4f + h.phase) * h.sway;
            h.rt.anchoredPosition = h.pos + new Vector2(x, 0f);
            h.rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(h.age * 3f + h.phase) * 14f);
            float pop = UiDraw.EaseOutBack(Mathf.Clamp01(h.age / 0.22f));
            h.rt.localScale = Vector3.one * (h.size / Base * pop * (1f - 0.2f * t * t));
            float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
            h.img.color = new Color(h.col.r, h.col.g, h.col.b, a);
        }
    }
}

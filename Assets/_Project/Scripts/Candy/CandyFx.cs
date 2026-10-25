// Assets/_Project/Scripts/UI/Candy/CandyFx.cs (НЕ в Editor)
// Эффекты для Candy UI: искры, конфетти, кольцо-волна, сердечки. Всё рисуется обычными Image на прозрачном слое
// «CandyFxLayer», который сам создаётся поверх корневого Canvas. Время unscaled: работает и на паузе.
//
// Вызов из кода (kit = CandyFxKit из CandyKit/Fun):
//   CandyFx.Burst(kit, rect, new Color(1f, .92f, .55f), 8);     // искры из точки (кнопка нажата, награда получена)
//   CandyFx.Confetti(kit, rect, 28);                              // праздник: новый уровень, задание выполнено
//   CandyFx.Ring(kit, rect, Color.white);                         // волна от иконки
//   CandyFx.Hearts(kit, rect, 5);                                 // сердечки (питомец, лайк)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// пружина для «желейных» анимаций: x стремится к target, v хранит скорость (можно «пнуть», дав v импульс)
public struct CandySpring
{
    public float x, v;
    public CandySpring(float start) { x = start; v = 0f; }

    public void Step(float target, float stiffness, float damping, float dt)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.012f));
        float h = dt / n;
        for (int i = 0; i < n; i++)
        {
            v += (target - x) * stiffness * h;
            v *= Mathf.Exp(-damping * h);
            x += v * h;
        }
    }

    public bool Settled(float target) { return Mathf.Abs(x - target) < 0.0008f && Mathf.Abs(v) < 0.01f; }
}

// одна частица: летит, вращается, «выскакивает» и тает, потом сама удаляется
public class CandyFxParticle : MonoBehaviour
{
    public Vector2 vel;
    public float gravity;
    public float drag;
    public float spin;
    public float life = 0.7f;
    public float startScale = 1f;
    public float endScale = 0.4f;
    public float popTime = 0.12f;
    public Color color = Color.white;

    float age;
    RectTransform rt;
    Image img;

    void Awake()
    {
        rt = (RectTransform)transform;
        img = GetComponent<Image>();
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        age += dt;
        if (age >= life) { Destroy(gameObject); return; }

        vel.y -= gravity * dt;
        vel *= Mathf.Exp(-drag * dt);
        rt.anchoredPosition += vel * dt;
        rt.Rotate(0f, 0f, spin * dt);

        float k = age / life;
        float pop = age < popTime
            ? Mathf.SmoothStep(0f, 1.2f, age / popTime)
            : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((age - popTime) / popTime));
        rt.localScale = Vector3.one * (Mathf.Lerp(startScale, endScale, k) * pop);

        var c = color;
        c.a = color.a * Mathf.Clamp01((1f - k) / 0.35f);
        img.color = c;
    }
}

public static class CandyFx
{
    static readonly Color[] Party =
    {
        new Color32(0xFF, 0xC9, 0x3C, 255), new Color32(0xFF, 0x7A, 0x6B, 255), new Color32(0x6D, 0xD6, 0xA0, 255),
        new Color32(0x69, 0xB4, 0xF2, 255), new Color32(0xB7, 0x9C, 0xFF, 255)
    };

    // ------------------------------------------------------------ слой
    static RectTransform Layer(Canvas root)
    {
        var t = root.transform.Find("CandyFxLayer");
        if (t != null) return (RectTransform)t;

        var go = new GameObject("CandyFxLayer", typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var c = go.GetComponent<Canvas>();
        c.overrideSorting = true;       // поверх всех окон
        c.sortingOrder = 3000;
        return rt;
    }

    static CandyFxParticle Spawn(Canvas root, Vector3 world, Sprite sprite, Color color, float size)
    {
        if (root == null || sprite == null) return null;
        var layer = Layer(root);
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, world);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, cam, out local)) return null;

        var go = new GameObject("fx", typeof(RectTransform), typeof(Image), typeof(CandyFxParticle));
        var rt = (RectTransform)go.transform;
        rt.SetParent(layer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = local;
        rt.sizeDelta = new Vector2(size, size * sprite.rect.height / Mathf.Max(1f, sprite.rect.width));
        rt.localScale = Vector3.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;

        var p = go.GetComponent<CandyFxParticle>();
        p.color = color;
        return p;
    }

    static Canvas RootOf(RectTransform at)
    {
        if (at == null) return null;
        var c = at.GetComponentInParent<Canvas>(true);
        return c != null ? c.rootCanvas : null;
    }

    // точка внутри прямоугольника в долях (0..1 слева-направо и снизу-вверх) в мировых координатах
    public static Vector3 PointIn(RectTransform at, float nx, float ny)
    {
        Rect r = at.rect;
        return at.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, nx), Mathf.Lerp(r.yMin, r.yMax, ny), 0f));
    }

    // ------------------------------------------------------------ эффекты
    // искры во все стороны из центра
    public static void Burst(CandyFxKit kit, RectTransform at, Color color, int count = 8, float speed = 260f)
    {
        if (kit == null || at == null) return;
        var root = RootOf(at);
        Vector3 w = at.TransformPoint(at.rect.center);
        for (int i = 0; i < count; i++)
        {
            bool big = Random.value < 0.45f;
            var p = Spawn(root, w, big ? kit.sparkle : (kit.sparkleSmall != null ? kit.sparkleSmall : kit.sparkle), color, Random.Range(20f, 36f));
            if (p == null) return;
            float a = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
            float s = speed * Random.Range(0.5f, 1f);
            p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s;
            p.drag = 3.2f;
            p.gravity = 120f;
            p.spin = Random.Range(-220f, 220f);
            p.life = Random.Range(0.5f, 0.85f);
            p.startScale = 1f;
            p.endScale = 0.3f;
        }
    }

    // маленькая мерцающая искорка на глянцевом блике кнопки
    public static void Twinkle(CandyFxKit kit, RectTransform at, Color color)
    {
        if (kit == null || at == null) return;
        var p = Spawn(RootOf(at), PointIn(at, Random.Range(0.1f, 0.2f), 0.78f), kit.sparkle, color, 26f);
        if (p == null) return;
        p.vel = new Vector2(0f, 10f);
        p.spin = 90f;
        p.life = 0.55f;
        p.startScale = 0.9f;
        p.endScale = 0.2f;
    }

    // праздничное конфетти вверх и в стороны
    public static void Confetti(CandyFxKit kit, RectTransform at, int count = 26)
    {
        if (kit == null || at == null) return;
        var root = RootOf(at);
        Vector3 w = at.TransformPoint(at.rect.center);
        Sprite[] sp = { kit.confettiRect, kit.confettiCircle, kit.confettiTri };
        for (int i = 0; i < count; i++)
        {
            Sprite s = sp[Random.Range(0, sp.Length)];
            if (s == null) s = kit.sparkleSmall;
            var p = Spawn(root, w, s, Party[Random.Range(0, Party.Length)], Random.Range(14f, 24f));
            if (p == null) return;
            float a = Random.Range(25f, 155f) * Mathf.Deg2Rad;
            p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(320f, 640f);
            p.gravity = 900f;
            p.drag = 0.9f;
            p.spin = Random.Range(-520f, 520f);
            p.life = Random.Range(1.3f, 2f);
            p.startScale = 1f;
            p.endScale = 0.8f;
            p.popTime = 0.08f;
        }
    }

    // кольцо-волна: расходится и тает
    public static void Ring(CandyFxKit kit, RectTransform at, Color color)
    {
        if (kit == null || at == null) return;
        var p = Spawn(RootOf(at), at.TransformPoint(at.rect.center), kit.ring, color, 110f);
        if (p == null) return;
        p.life = 0.5f;
        p.startScale = 0.3f;
        p.endScale = 1.7f;
        p.popTime = 0.01f;
    }

    // сердечки вверх
    public static void Hearts(CandyFxKit kit, RectTransform at, int count = 5)
    {
        if (kit == null || at == null) return;
        var root = RootOf(at);
        for (int i = 0; i < count; i++)
        {
            var p = Spawn(root, PointIn(at, Random.value, 0.8f), kit.heart, Color.white, Random.Range(22f, 34f));
            if (p == null) return;
            p.vel = new Vector2(Random.Range(-40f, 40f), Random.Range(110f, 190f));
            p.drag = 0.8f;
            p.spin = Random.Range(-40f, 40f);
            p.life = Random.Range(0.9f, 1.4f);
            p.startScale = 1f;
            p.endScale = 0.7f;
        }
    }
}

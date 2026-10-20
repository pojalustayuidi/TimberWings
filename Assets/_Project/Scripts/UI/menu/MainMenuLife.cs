// Assets/_Project/Scripts/UI/menu/MainMenuLife.cs (НЕ в Editor)
// ШАГ 5: жизнь поверх картинки главного меню. Вешать вручную не нужно: MainMenuStub добавляет компонент сам.
// Всё делается кодом, без отдельных спрайтов и пакетов:
//   дым из трубы, мерцание костра / фонарей / окон, ореол солнца, искры, светлячки, падающие листья,
//   «дыхание» фона (масштаб) и параллакс от мыши, фоновые ветер и костёр (синтез, громкость = GameAudio.AmbientLevel).
// Эффекты лежат в слое MenuFx под доской: первым ребёнком Art, привязаны к точкам MainMenuBackground по долям картинки.
// Всё отключается при VibeSettings.ReduceEffects (звук остаётся). Параллакс выключен на мобильных.
// Свечение аддитивное, если в проекте есть шейдер TimberWings/UIAdditive (Assets/_Project/Resources/UiAdditive.shader), иначе обычное.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuLife : MonoBehaviour
{
    [Header("Фон: масштаб и параллакс")]
    [Tooltip("Фон чуть увеличен, чтобы дыхание и параллакс не открывали края")]
    [Range(1f, 1.1f)] public float baseZoom = 1.04f;
    [Range(0f, 0.03f)] public float breathAmount = 0.01f;
    public float breathPeriod = 9f;
    public bool parallax = true;
    [Range(0f, 30f)] public float parallaxPx = 10f;
    [Tooltip("Доска и плашки сдвигаются дополнительно: ближе к камере")]
    [Range(0f, 30f)] public float boardExtraPx = 14f;

    [Header("Свечение")]
    [Range(0f, 1.5f)] public float glowStrength = 1f;
    [Range(0f, 1.5f)] public float sunHalo = 1f;

    [Header("Частицы")]
    [Range(0, 16)] public int smokeMax = 9;
    public float smokeEvery = 0.9f;
    [Range(0, 24)] public int sparkMax = 14;
    public float sparkPerSec = 5f;
    [Range(0, 16)] public int fireflies = 9;
    [Range(0, 10)] public int leavesMax = 6;
    public float leafEvery = 2.6f;

    [Header("Звук")]
    [Range(0f, 1f)] public float windVolume = 0.35f;
    [Range(0f, 1f)] public float fireVolume = 0.5f;

    // ------------------------------------------------------------ состояние
    MainMenuBackground bg;
    RectTransform art, fx, board;
    Material addMat;
    Sprite sGlow, sSmoke, sSpark, sLeaf;
    float time, u = 1f;
    Vector2 par;
    bool built;

    class Glow
    {
        public RectTransform rt, anchor;
        public Image img;
        public float size, alpha, speed, seed, amp;
        public Color color;
        public bool flicker;
    }
    class Part
    {
        public RectTransform rt;
        public Image img;
        public bool alive;
        public float t, life, seed, s0, s1, rot, rotV, peak;
        public Vector2 pos, vel, nrm;
        public Color c;
    }

    readonly List<Glow> glows = new List<Glow>();
    Part[] smoke, sparks, flies, leaves;
    RectTransform chimney, campfire;
    float smokeT, sparkAcc, leafT;

    // звук
    AudioSource wind, fire;
    float startT, busyT;

    // ------------------------------------------------------------ жизненный цикл
    void Start()
    {
        bg = MainMenuBackground.Instance != null ? MainMenuBackground.Instance : FindFirstObjectByType<MainMenuBackground>();
        if (bg == null) { enabled = false; return; }
        bg.EnsureBuilt();
        art = bg.Art;
        var b = art.Find("MenuBoard");
        board = b != null ? (RectTransform)b : null;
        startT = Time.unscaledTime;

        Build();
        BuildAudio();
    }

    // ------------------------------------------------------------ построение
    void Build()
    {
        sGlow = MakeSprite(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Exp(-r * r * 4.2f) * Mathf.SmoothStep(1f, 0.82f, r); });
        sSmoke = MakeSprite(64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float n = 0.55f * Mathf.PerlinNoise(x * 2.2f + 5f, y * 2.2f + 5f) + 0.45f * Mathf.PerlinNoise(x * 5f + 11f, y * 5f + 3f);
            return Mathf.SmoothStep(1f, 0.12f, r) * (0.3f + 0.7f * n);
        });
        sSpark = MakeSprite(16, (x, y) => { float r2 = x * x + y * y; return Mathf.Clamp01(Mathf.Exp(-r2 * 14f) + 0.35f * Mathf.Exp(-r2 * 3f)); });
        sLeaf = MakeSprite(48, LeafAlpha);

        var sh = Shader.Find("TimberWings/UIAdditive");
        if (sh != null) addMat = new Material(sh);
        else Debug.Log("MainMenuLife: нет шейдера TimberWings/UIAdditive (Resources/UiAdditive.shader), свечение рисуется обычным смешиванием.");

        var go = new GameObject("MenuFx", typeof(RectTransform));
        go.transform.SetParent(art, false);
        fx = (RectTransform)go.transform;
        fx.anchorMin = Vector2.zero; fx.anchorMax = Vector2.one; fx.offsetMin = fx.offsetMax = Vector2.zero;
        fx.SetAsFirstSibling();                                   // под якорями, доской и плашками

        chimney = bg.GetAnchor("chimney");
        campfire = bg.GetAnchor("campfire");

        // свечение: (точка, размер px при 1920, цвет, прозрачность, скорость, размах мерцания, мерцает ли)
        AddGlow("sun", 760f, new Color(1f, 0.86f, 0.55f), 0.16f, 0.35f, 0f, false);
        AddGlow("campfire", 430f, new Color(1f, 0.52f, 0.16f), 0.34f, 0.9f, 0.45f, true);
        AddGlow("campfire", 170f, new Color(1f, 0.80f, 0.42f), 0.50f, 1.6f, 0.30f, true);
        AddGlow("lantern_left", 150f, new Color(1f, 0.78f, 0.38f), 0.50f, 0.55f, 0.16f, true);
        AddGlow("lantern_right", 130f, new Color(1f, 0.78f, 0.38f), 0.48f, 0.60f, 0.16f, true);
        AddGlow("lantern_hut", 110f, new Color(1f, 0.78f, 0.38f), 0.42f, 0.50f, 0.16f, true);
        AddGlow("window_1", 120f, new Color(1f, 0.82f, 0.45f), 0.30f, 0.45f, 0.14f, false);
        AddGlow("window_2", 110f, new Color(1f, 0.82f, 0.45f), 0.28f, 0.40f, 0.14f, false);
        AddGlow("window_3", 110f, new Color(1f, 0.82f, 0.45f), 0.28f, 0.52f, 0.14f, false);

        smoke = MakePool(smokeMax, "Smoke", sSmoke, null);
        sparks = MakePool(sparkMax, "Spark", sSpark, addMat);
        flies = MakePool(fireflies, "Firefly", sGlow, addMat);
        leaves = MakePool(leavesMax, "Leaf", sLeaf, null);
        InitFireflies();
        built = true;
    }

    void AddGlow(string id, float size, Color col, float alpha, float speed, float amp, bool flicker)
    {
        var img = NewImage("Glow_" + id, sGlow, addMat);
        glows.Add(new Glow
        {
            rt = img.rectTransform, img = img, anchor = bg.GetAnchor(id), size = size, color = col, alpha = alpha,
            speed = speed, amp = amp, flicker = flicker, seed = Random.Range(0f, 100f)
        });
    }

    Image NewImage(string name, Sprite s, Material m)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(fx, false);
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        if (m != null) img.material = m;
        var r = img.rectTransform;
        r.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    Part[] MakePool(int n, string name, Sprite s, Material m)
    {
        var a = new Part[n];
        for (int i = 0; i < n; i++)
        {
            var img = NewImage(name + "_" + i, s, m);
            img.enabled = false;
            a[i] = new Part { rt = img.rectTransform, img = img, seed = Random.Range(0f, 100f) };
        }
        return a;
    }

    // светлячки живут над травой острова, у кустов справа от доски и у левого края
    void InitFireflies()
    {
        var regions = new[] { new Rect(0.60f, 0.50f, 0.30f, 0.20f), new Rect(0.38f, 0.62f, 0.17f, 0.25f), new Rect(0f, 0.55f, 0.09f, 0.35f) };
        for (int i = 0; i < flies.Length; i++)
        {
            var r = regions[i % regions.Length];
            var p = flies[i];
            p.nrm = new Vector2(r.x + Random.value * r.width, r.y + Random.value * r.height);
            p.s0 = Random.Range(14f, 22f);
            p.rotV = Random.Range(0.5f, 1.2f);          // скорость мигания
            p.c = Random.value < 0.7f ? new Color(0.86f, 1f, 0.55f) : new Color(1f, 0.92f, 0.55f);
            p.alive = true;
            p.img.enabled = true;
        }
    }

    // ------------------------------------------------------------ обновление
    void Update()
    {
        if (!built) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        time += dt;
        u = art.rect.width / 1920f;

        UpdateAudio(dt);

        bool reduced = VibeSettings.ReduceEffects;
        if (reduced)
        {
            if (fx.gameObject.activeSelf)
            {
                fx.gameObject.SetActive(false);
                art.localScale = Vector3.one;
                art.anchoredPosition = Vector2.zero;
                if (board != null) board.anchoredPosition = Vector2.zero;
            }
            return;
        }
        if (!fx.gameObject.activeSelf) fx.gameObject.SetActive(true);

        UpdateBackground(dt);
        UpdateGlows();
        UpdateSmoke(dt);
        UpdateSparks(dt);
        UpdateFireflies();
        UpdateLeaves(dt);
    }

    void UpdateBackground(float dt)
    {
        float z = baseZoom + breathAmount * Mathf.Sin(time * Mathf.PI * 2f / Mathf.Max(1f, breathPeriod));
        art.localScale = new Vector3(z, z, 1f);

        Vector2 target = Vector2.zero;
        if (parallax && !Application.isMobilePlatform && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 m = Input.mousePosition;
            target = new Vector2(Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f));
        }
        par = Vector2.Lerp(par, target, 1f - Mathf.Exp(-3f * dt));
        art.anchoredPosition = new Vector2(-par.x * parallaxPx, -par.y * parallaxPx * 0.5f);
        if (board != null) board.anchoredPosition = new Vector2(-par.x * boardExtraPx, -par.y * boardExtraPx * 0.5f);
    }

    void UpdateGlows()
    {
        for (int i = 0; i < glows.Count; i++)
        {
            var g = glows[i];
            float f;
            if (g.flicker)
            {
                float n1 = Mathf.PerlinNoise(time * g.speed, g.seed);
                float n2 = Mathf.PerlinNoise(time * g.speed * 2.7f + 9f, g.seed + 4f);
                f = 1f - g.amp + g.amp * 2f * (0.65f * n1 + 0.35f * n2);
            }
            else f = 1f + g.amp * Mathf.Sin(time * g.speed + g.seed);
            float k = g.anchor == campfire || g.size > 700f ? 1f : 1f;
            float a = g.alpha * f * (g.size > 700f ? sunHalo : glowStrength);
            var c = g.color; c.a = Mathf.Clamp01(a);
            g.img.color = c;
            float sz = g.size * u * (g.flicker ? (0.94f + 0.12f * f * k) : 1f);
            g.rt.sizeDelta = new Vector2(sz, sz);
            g.rt.anchorMin = g.rt.anchorMax = g.anchor.anchorMin;
            g.rt.anchoredPosition = Vector2.zero;
        }
    }

    // дым: серо-белые облачка медленно уходят вправо вверх
    void UpdateSmoke(float dt)
    {
        smokeT -= dt;
        if (smokeT <= 0f)
        {
            smokeT = smokeEvery * Random.Range(0.8f, 1.25f);
            var p = Free(smoke);
            if (p != null)
            {
                p.t = 0f; p.life = Random.Range(6f, 8f);
                p.pos = new Vector2(Random.Range(-4f, 4f), 0f) * u;
                p.vel = new Vector2(Random.Range(16f, 26f), Random.Range(26f, 38f)) * u;
                p.s0 = Random.Range(30f, 44f); p.s1 = Random.Range(120f, 170f);
                p.rot = Random.Range(0f, 360f); p.rotV = Random.Range(-8f, 8f);
                p.peak = Random.Range(0.22f, 0.34f);
                p.seed = Random.Range(0f, 100f);
                Spawn(p);
            }
        }
        var anchor = chimney.anchorMin;
        for (int i = 0; i < smoke.Length; i++)
        {
            var p = smoke[i];
            if (!p.alive) continue;
            p.t += dt;
            float k = p.t / p.life;
            if (k >= 1f) { Kill(p); continue; }
            p.vel.x += 3f * u * dt;
            p.pos += p.vel * dt;
            p.pos.x += Mathf.Sin(p.t * 1.1f + p.seed) * 6f * u * dt;
            p.rot += p.rotV * dt;
            float a = p.peak * Mathf.Min(1f, p.t / 0.9f) * (1f - Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));
            float sz = Mathf.Lerp(p.s0, p.s1, Mathf.Sqrt(k)) * u;
            Apply(p, anchor, new Color(0.94f, 0.93f, 0.92f, a), sz);
        }
    }

    // искры над костром
    void UpdateSparks(float dt)
    {
        sparkAcc += sparkPerSec * dt;
        while (sparkAcc >= 1f)
        {
            sparkAcc -= 1f;
            var p = Free(sparks);
            if (p == null) break;
            p.t = 0f; p.life = Random.Range(1.4f, 2.4f);
            p.pos = new Vector2(Random.Range(-18f, 18f), Random.Range(4f, 14f)) * u;
            p.vel = new Vector2(Random.Range(-12f, 12f), Random.Range(70f, 140f)) * u;
            p.s0 = Random.Range(9f, 16f); p.s1 = 3f;
            p.seed = Random.Range(0f, 100f);
            Spawn(p);
        }
        var anchor = campfire.anchorMin;
        for (int i = 0; i < sparks.Length; i++)
        {
            var p = sparks[i];
            if (!p.alive) continue;
            p.t += dt;
            float k = p.t / p.life;
            if (k >= 1f) { Kill(p); continue; }
            p.vel.x += Mathf.Sin(p.t * 5f + p.seed) * 40f * u * dt;
            p.vel.y *= 1f - 0.35f * dt;
            p.pos += p.vel * dt;
            float tw = 0.75f + 0.25f * Mathf.Sin(p.t * 20f + p.seed);
            float a = Mathf.Pow(1f - k, 1.3f) * 0.9f * tw;
            var col = Color.Lerp(new Color(1f, 0.86f, 0.42f), new Color(1f, 0.38f, 0.10f), k);
            col.a = a * glowStrength;
            Apply(p, anchor, col, Mathf.Lerp(p.s0, p.s1, k) * u);
        }
    }

    // светлячки: плавают по шуму Перлина и мягко мигают
    void UpdateFireflies()
    {
        for (int i = 0; i < flies.Length; i++)
        {
            var p = flies[i];
            if (!p.alive) continue;
            float t = time * 0.18f + p.seed;
            Vector2 off = new Vector2(Mathf.PerlinNoise(t, p.seed) - 0.5f, Mathf.PerlinNoise(p.seed + 20f, t) - 0.5f) * new Vector2(240f, 170f) * u;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * p.rotV + p.seed * 3f);
            float a = 0.08f + 0.7f * pulse * pulse * pulse;
            var col = p.c; col.a = a * glowStrength;
            p.pos = off;
            Apply(p, p.nrm, col, p.s0 * u * (0.8f + 0.4f * pulse), true);
        }
    }

    // листья: падают сверху, качаются и кружатся
    void UpdateLeaves(float dt)
    {
        leafT -= dt;
        if (leafT <= 0f)
        {
            leafT = leafEvery * Random.Range(0.7f, 1.4f);
            var p = Free(leaves);
            if (p != null)
            {
                p.t = 0f; p.life = Random.Range(13f, 17f);
                p.nrm = new Vector2(Random.Range(0.35f, 1.0f), -0.05f);
                p.vel = new Vector2(Random.Range(-0.03f, -0.012f), 1.13f / p.life);   // норм. доли в секунду
                p.s0 = Random.Range(20f, 32f);
                p.rot = Random.Range(0f, 360f); p.rotV = Random.Range(-40f, 40f);
                p.seed = Random.Range(0f, 100f);
                var pal = new[] { new Color(0.78f, 0.86f, 0.36f), new Color(0.96f, 0.70f, 0.26f), new Color(0.62f, 0.80f, 0.32f), new Color(0.92f, 0.52f, 0.24f) };
                p.c = pal[Random.Range(0, pal.Length)];
                Spawn(p);
            }
        }
        for (int i = 0; i < leaves.Length; i++)
        {
            var p = leaves[i];
            if (!p.alive) continue;
            p.t += dt;
            if (p.t >= p.life) { Kill(p); continue; }
            p.nrm += p.vel * dt;
            p.pos = new Vector2(Mathf.Sin(p.t * 0.9f + p.seed) * 38f * u, 0f);
            float a = Mathf.Clamp01(p.t / 0.8f) * Mathf.Clamp01((p.life - p.t) / 1f) * 0.9f;
            p.rot += p.rotV * dt;
            var col = p.c; col.a = a;
            Apply(p, p.nrm, col, p.s0 * u, false, Mathf.Sin(p.t * 1.2f + p.seed) * 35f + p.rot);
        }
    }

    // ------------------------------------------------------------ частицы: помощники
    static Part Free(Part[] pool)
    {
        for (int i = 0; i < pool.Length; i++) if (!pool[i].alive) return pool[i];
        return null;
    }

    static void Spawn(Part p) { p.alive = true; p.img.enabled = true; }
    static void Kill(Part p) { p.alive = false; p.img.enabled = false; }

    void Apply(Part p, Vector2 anchorMin, Color c, float size) { Apply(p, anchorMin, c, size, false, p.rot); }

    // точка в долях картинки: anchorMin = (x, 1-y). Для светлячков и листьев переданы доли «y вниз», пересчитываем
    void Apply(Part p, Vector2 anchor, Color c, float size, bool fromTopLeft, float rot = 0f)
    {
        p.rt.anchorMin = p.rt.anchorMax = fromTopLeft ? new Vector2(anchor.x, 1f - anchor.y) : anchor;
        p.rt.anchoredPosition = p.pos;
        p.rt.sizeDelta = new Vector2(size, size);
        p.rt.localEulerAngles = new Vector3(0f, 0f, rot);
        p.img.color = c;
    }

    // ------------------------------------------------------------ процедурные спрайты
    static Sprite MakeSprite(int n, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = ((x + 0.5f) / n) * 2f - 1f, v = ((y + 0.5f) / n) * 2f - 1f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static float LeafAlpha(float u, float v)
    {
        u *= 1.1f; v *= 1.1f;
        float x = (u + v) * 0.7071f, y = (v - u) * 0.7071f;
        const float R = 1.15f, d = 0.68f;
        float m = Mathf.Min(R - Mathf.Sqrt(x * x + (y + d) * (y + d)), R - Mathf.Sqrt(x * x + (y - d) * (y - d)));
        float a = Mathf.Clamp01(m * 24f + 0.5f);
        if (x > -0.9f && x < 0.5f) a *= 1f - 0.45f * Mathf.Exp(-(y * y) / 0.0016f);   // прожилка
        return a;
    }

    // ------------------------------------------------------------ звук
    void BuildAudio()
    {
        wind = gameObject.AddComponent<AudioSource>();
        wind.clip = MakeWind(); wind.loop = true; wind.spatialBlend = 0f; wind.volume = 0f; wind.playOnAwake = false; wind.Play();
        fire = gameObject.AddComponent<AudioSource>();
        fire.clip = MakeFire(); fire.loop = true; fire.spatialBlend = 0f; fire.volume = 0f; fire.playOnAwake = false; fire.Play();
    }

    void UpdateAudio(float dt)
    {
        if (wind == null) return;
        if (MenuFlow.Busy) busyT += dt; else busyT = 0f;
        float k = Mathf.Clamp01((Time.unscaledTime - startT) / 2.5f) * (1f - Mathf.Clamp01(busyT / 0.4f));
        float amb = GameAudio.AmbientLevel * k;
        wind.volume = windVolume * amb;
        fire.volume = fireVolume * amb;
    }

    // ветер: фильтрованный шум с медленными порывами; фильтр прогнан по кругу, края сходятся без щелчка
    static AudioClip MakeWind()
    {
        const int rate = 22050, n = rate * 10;
        var rng = new System.Random(3);
        var w = new float[n];
        for (int i = 0; i < n; i++) w[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        var d = new float[n];
        float y1 = 0f, y2 = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                y1 += 0.05f * (w[i] - y1);
                y2 += 0.12f * (y1 - y2);
                d[i] = y2;
            }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float ph = (float)i / n * Mathf.PI * 2f;
            float swell = 0.55f + 0.30f * Mathf.Sin(ph * 3f + 0.7f) + 0.15f * Mathf.Sin(ph * 7f + 2.1f);   // целое число циклов: шов не слышен
            d[i] *= swell;
            peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        }
        for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.8f;
        var c = AudioClip.Create("menu_wind", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    // костёр: тихий гул и редкие щелчки углей
    static AudioClip MakeFire()
    {
        const int rate = 22050, n = rate * 8;
        var rng = new System.Random(11);
        var d = new float[n];
        float y = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                y += 0.03f * ((float)(rng.NextDouble() * 2.0 - 1.0) - y);
                if (pass == 1) d[i] = y * 1.6f;
            }
        for (int p = 0; p < 46; p++)
        {
            int pos = rng.Next(n);
            int len = rng.Next(250, 900);
            float amp = 0.15f + (float)rng.NextDouble() * (p % 9 == 0 ? 0.8f : 0.4f);
            for (int j = 0; j < len; j++)
            {
                float env = Mathf.Exp(-j / (len * 0.22f));
                d[(pos + j) % n] += ((float)(rng.NextDouble() * 2.0 - 1.0)) * amp * env;
            }
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        for (int i = 0; i < n; i++) d[i] = d[i] / peak * 0.75f;
        var c = AudioClip.Create("menu_fire", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}

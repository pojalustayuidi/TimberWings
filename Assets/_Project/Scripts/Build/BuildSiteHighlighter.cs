// Assets/_Project/Scripts/Build/BuildSiteHighlighter.cs
// Кольцо + световой «луч» (столб) + «призрак» над площадкой стройки. Один общий материал и MaterialPropertyBlock.
// Состояние берёт у BuildSiteTagView (если есть) или у BuildingController.
// Призрак показывается только у ОДНОЭТАПНЫХ построек: у ступенчатых (костёр) вид даёт HutStages.
//
// Что изменено:
//   * Когда здание достроено (BuildingController.IsFinished или событие Finished), луч, кольцо и призрак плавно гаснут
//     и УДАЛЯЮТСЯ насовсем. Раньше это зависело от метки, и если она запаздывала или не находилась, луч оставался.
//   * Луч и кольцо появляются/гаснут плавно, а не щёлкают.
//   * Луч стал по-настоящему прозрачным (раньше материал был непрозрачным, и альфа ничего не делала) и тает к верху.
//   * Для зоны в центре (костёр) можно подогнать вид прямо в Inspector: Pillar Width, Pillar Height, Ring Radius, Ring Height.
//     Значения по умолчанию такие же, как были, остальные зоны выглядят как раньше.
using UnityEngine;
using UnityEngine.Rendering;

public class BuildSiteHighlighter : MonoBehaviour
{
    [Header("Размеры")]
    public float ringRadius = 2.2f;
    public float pillarHeight = 6f;
    [Tooltip("Толщина луча (диаметр, м). Для костра в центре можно поставить 0.4-0.5, если луч закрывает пламя")]
    public float pillarWidth = 0.7f;
    [Tooltip("Высота кольца над землёй (м). Если кольцо тонет в камнях или земле, подними до 0.1-0.2")]
    public float ringHeight = 0.03f;
    public float pulseSpeed = 1.6f;

    [Header("Вид")]
    [Tooltip("Луч плавно тает кверху. Выключи, чтобы был ровного цвета, как раньше")]
    public bool softPillar = true;
    [Tooltip("Скорость появления и угасания (1 = секунда)")]
    public float fadeSpeed = 4f;
    public Color soft = new Color(0.42f, 0.8f, 0.35f, 0.35f), bright = new Color(0.42f, 0.8f, 0.35f, 0.95f);

    Transform ring, pillar, ghost;
    Renderer ringR, pillarR;
    Renderer[] ghostRs = new Renderer[0];
    MaterialPropertyBlock mpb;
    Material sharedMat, pillarMat;
    BuildSiteTagView tagView;
    PlayerInventory inv;
    BuildingController bc;
    float vis;            // 0..1: насколько видны кольцо, луч и призрак
    bool finished;

    static Material _shared, _pillarSoft;
    static Texture2D _fade;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { _shared = null; _pillarSoft = null; _fade = null; }

    void Awake()
    {
        tagView = GetComponent<BuildSiteTagView>();
        bc = GetComponent<BuildingController>();
        inv = PlayerInventory.Instance;
        mpb = new MaterialPropertyBlock();

        if (_shared == null)
        {
            _shared = NewTransparent("BuildSiteHighlight_Flat");
            _shared.enableInstancing = true;
        }
        sharedMat = _shared;

        // у луча свой материал с градиентом по высоте (кольцу и призраку градиент не нужен)
        pillarMat = sharedMat;
        if (softPillar)
        {
            if (_pillarSoft == null)
            {
                _pillarSoft = NewTransparent("BuildSiteHighlight_Pillar");
                _pillarSoft.enableInstancing = true;
                var tex = FadeTexture();
                if (_pillarSoft.HasProperty("_BaseMap")) _pillarSoft.SetTexture("_BaseMap", tex);
                if (_pillarSoft.HasProperty("_MainTex")) _pillarSoft.SetTexture("_MainTex", tex);
            }
            pillarMat = _pillarSoft;
        }

        CreateRing();
        CreatePillar();
    }

    // призрак создаём в Start: к этому моменту BuildingController уже понял, ступенчатая ли стройка
    void Start()
    {
        CreateGhost();
    }

    void OnEnable()
    {
        if (bc == null) bc = GetComponent<BuildingController>();
        if (bc != null) bc.Finished += OnFinished;
    }

    void OnDisable()
    {
        if (bc != null) bc.Finished -= OnFinished;
    }

    void OnFinished() { finished = true; }

    // ---------------------------------------------------------------- материал и текстура
    static Material NewTransparent(string matName)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh) { name = matName };

        // URP Unlit по умолчанию непрозрачный: без этого альфа цвета игнорировалась и луч был сплошным
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        return m;
    }

    // вертикальный градиент: внизу непрозрачно, наверху прозрачно (v = 0 у основания цилиндра)
    static Texture2D FadeTexture()
    {
        if (_fade != null) return _fade;
        const int h = 64;
        var t = new Texture2D(2, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float a = Mathf.Pow(1f - v, 1.6f);
            a = Mathf.Lerp(a, 1f, 0.06f * (1f - v));   // у самой земли чуть плотнее, чтобы основание читалось
            var c = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            for (int x = 0; x < 2; x++) t.SetPixel(x, y, c);
        }
        t.Apply(false, true);
        _fade = t;
        return _fade;
    }

    // ---------------------------------------------------------------- создание
    void CreateRing()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
        ring = go.transform;
        ring.SetParent(transform, false);
        ring.localPosition = new Vector3(0, ringHeight, 0);
        ring.localScale = new Vector3(ringRadius * 2f, 0.015f, ringRadius * 2f);
        ringR = go.GetComponent<Renderer>();
        ringR.sharedMaterial = sharedMat;
        ringR.shadowCastingMode = ShadowCastingMode.Off;
        ringR.receiveShadows = false;
        ring.gameObject.SetActive(false);
    }

    void CreatePillar()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
        pillar = go.transform;
        pillar.SetParent(transform, false);
        pillar.localPosition = new Vector3(0, pillarHeight * 0.5f, 0);
        pillar.localScale = new Vector3(pillarWidth, pillarHeight * 0.5f, pillarWidth);
        pillarR = go.GetComponent<Renderer>();
        pillarR.sharedMaterial = pillarMat;
        pillarR.shadowCastingMode = ShadowCastingMode.Off;
        pillarR.receiveShadows = false;
        pillar.gameObject.SetActive(false);
    }

    void CreateGhost()
    {
        if (bc == null || bc.finishedBuilding == null) return;
        if (bc.StageTotal > 1) return;      // у ступенчатой стройки призрак лишний: он налезал на настоящий костёр

        var go = Instantiate(bc.finishedBuilding, transform);
        go.name = "Ghost";
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var l in go.GetComponentsInChildren<Light>(true)) Destroy(l);                 // свет у призрака не нужен
        foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) Destroy(p);        // и частицы тоже

        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) { Destroy(r); continue; }
            r.sharedMaterial = sharedMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            list.Add(r);
        }
        ghostRs = list.ToArray();

        ghost = go.transform;
        ghost.localPosition = Vector3.up * 0.05f;
        go.SetActive(false);
    }

    // ---------------------------------------------------------------- каждый кадр
    void Update()
    {
        // здание достроено: ничего больше не показываем, не глядя на метку
        if (!finished && bc != null && bc.IsFinished) finished = true;

        if (inv == null) inv = PlayerInventory.Instance;

        bool isNext;
        if (finished) isNext = false;
        else if (tagView != null) isNext = tagView.state == BuildSiteTagView.State.Next;
        else isNext = bc != null && !bc.IsFinished;

        vis = Mathf.MoveTowards(vis, isNext ? 1f : 0f, Time.deltaTime * Mathf.Max(0.1f, fadeSpeed));

        if (vis <= 0.001f)
        {
            Show(false);
            if (finished) { Cleanup(); enabled = false; }   // луч больше не вернётся
            return;
        }

        // цену берём у стройки
        int need = bc != null ? bc.Cost : 0;
        bool hasEnough = !finished && inv != null && (bc != null ? bc.PlayerCanContribute(inv) : (need > 0 && inv.woodCount >= need));
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
        float a = (hasEnough ? bright.a : soft.a) * pulse * vis;

        if (ring != null)
        {
            ring.gameObject.SetActive(true);
            float s = 1f + (hasEnough ? 0.08f : 0.04f) * Mathf.Sin(Time.time * pulseSpeed);
            ring.localScale = new Vector3(ringRadius * 2f * s, 0.015f, ringRadius * 2f * s);
            Paint(ringR, new Color(bright.r, bright.g, bright.b, a * 0.9f));
        }
        if (pillar != null)
        {
            pillar.gameObject.SetActive(true);
            // с градиентом внизу плотнее, а у верха всё равно тает: общая плотность чуть выше, чем у плоского луча
            float k = softPillar ? 0.42f : 0.22f;
            Paint(pillarR, new Color(bright.r, bright.g, bright.b, a * k));
            pillar.Rotate(0, 15f * Time.deltaTime, 0, Space.Self);
        }
        if (ghost != null)
        {
            ghost.gameObject.SetActive(true);
            var gc = new Color(1f, 1f, 1f, (hasEnough ? 0.35f * pulse : 0.18f * pulse) * vis);
            for (int i = 0; i < ghostRs.Length; i++) Paint(ghostRs[i], gc);
        }
    }

    void Show(bool on)
    {
        if (ring != null) ring.gameObject.SetActive(on);
        if (pillar != null) pillar.gameObject.SetActive(on);
        if (ghost != null) ghost.gameObject.SetActive(on);
    }

    void Cleanup()
    {
        if (ring != null) Destroy(ring.gameObject);
        if (pillar != null) Destroy(pillar.gameObject);
        if (ghost != null) Destroy(ghost.gameObject);
        ring = pillar = ghost = null;
        ringR = pillarR = null;
        ghostRs = new Renderer[0];
    }

    void Paint(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(mpb);
        SetColor(mpb, c);
        r.SetPropertyBlock(mpb);
    }

    void SetColor(MaterialPropertyBlock b, Color c)
    {
        if (sharedMat.HasProperty(BaseColorId)) b.SetColor(BaseColorId, c);
        else if (sharedMat.HasProperty(ColorId)) b.SetColor(ColorId, c);
        else b.SetColor("_BaseColor", c);
    }

    void OnDestroy()
    {
        Cleanup();
    }
}

// Assets/_Project/Scripts/Location/IslandV2Data.cs (НЕ в Editor)
// Остров v2, этап 4: данные острова для рантайма. Вешается на корень IslandV2 (его добавляет и заполняет IslandBlockout, меню «Собрать блокаут»).
// Заменяет FloatingIsland для тех, кто его искал ради контура и высоты: IslandLook (дымка), CloudSea, IslandAbyss, IslandEdgeGuard, LightingRig, HudMinimap.
// Они ходят через IslandSource.Find(), который берёт FloatingIsland (старая локация) или этот компонент (остров v2).
// В Awake сам вешает IslandEdgeGuard на тот же объект (падение с края и возврат на сушу), как это делал FloatingIsland.
// Что хранит: контур (мировые XZ), GroundY (уровень земли у костра, 0), глубину подбрюшья, seed, цвета травы и скалы, ссылки на шейдер облаков и материал скалы.
// Цвета скалы по умолчанию сиренево-лавандовые (референс), а не коричневые.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class IslandV2Data : MonoBehaviour
{
    public static IslandV2Data Instance { get; private set; }

    [Header("Форма (заполняет IslandBlockout, руками не править)")]
    [HideInInspector] public List<Vector2> outline = new List<Vector2>();
    [Tooltip("Уровень земли у костра (метры). Обрыв и облака считаются от него")]
    public float groundY = 0f;
    public int seed = 7;

    [Header("Подбрюшье и облака")]
    [Tooltip("Глубина острова вниз от земли, метры (от неё зависят облака и низ скалы)")]
    [Range(8f, 40f)] public float depth = 22f;
    public Shader cloudShader;
    [Tooltip("Пусто = Project/IslandRock")]
    public Material rockMaterial;

    [Header("Цвета (sRGB)")]
    public Color grassTop = new Color(0.56f, 0.78f, 0.24f);
    public Color grassDark = new Color(0.36f, 0.62f, 0.22f);
    public Color soil = new Color(0.56f, 0.45f, 0.42f);
    public Color rockWarm = new Color(0.72f, 0.64f, 0.76f);
    public Color rockMid = new Color(0.61f, 0.56f, 0.72f);
    public Color rockDark = new Color(0.42f, 0.38f, 0.58f);

    // примерный радиус острова: наибольшее расстояние от центра рамки до вершины контура
    public float Radius
    {
        get
        {
            if (outline == null || outline.Count < 3) return 50f;
            Vector2 mn = outline[0], mx = outline[0];
            for (int i = 1; i < outline.Count; i++) { mn = Vector2.Min(mn, outline[i]); mx = Vector2.Max(mx, outline[i]); }
            Vector2 c = (mn + mx) * 0.5f;
            float r = 0f;
            for (int i = 0; i < outline.Count; i++) r = Mathf.Max(r, Vector2.Distance(c, outline[i]));
            return r;
        }
    }

    void Awake()
    {
        Instance = this;
        if (GetComponent<IslandEdgeGuard>() == null) gameObject.AddComponent<IslandEdgeGuard>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    void Reset() { Instance = this; }
#endif

    public void SetOutline(List<Vector2> contour, float ground, int seedValue)
    {
        outline = new List<Vector2>(contour);
        groundY = ground;
        seed = seedValue;
    }

    // копия контура (вызывающий может её менять)
    public List<Vector2> GetOutline()
    {
        return outline == null ? new List<Vector2>() : new List<Vector2>(outline);
    }

    // контур против часовой стрелки, точки не реже чем через 0,5 м (для IslandEdgeGuard)
    public List<Vector2> GetEdgeLoop()
    {
        var src = GetOutline();
        if (src.Count < 3) return src;
        float area = 0f;
        for (int i = 0, j = src.Count - 1; i < src.Count; j = i++) area += (src[j].x - src[i].x) * (src[j].y + src[i].y);
        if (area < 0f) src.Reverse();   // знак подобран так же, как у FloatingIsland.SignedArea: нужен CCW

        var dense = new List<Vector2>();
        for (int i = 0; i < src.Count; i++)
        {
            Vector2 a = src[i], b = src[(i + 1) % src.Count];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
            for (int s = 0; s < steps; s++) dense.Add(Vector2.Lerp(a, b, (float)s / steps));
        }
        return dense;
    }
}

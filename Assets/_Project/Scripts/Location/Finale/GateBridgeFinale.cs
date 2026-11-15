using UnityEngine;

// Только ссылки на объекты, уже сохранённые в сцене сборщиком GateBridgeFinaleBaker.
// В Play Mode не создаёт ни геометрию, ни коллайдеры, ни источники света.
[DisallowMultipleComponent]
public sealed class GateBridgeFinale : MonoBehaviour
{
    [SerializeField] GameObject constructionBarrier;
    [SerializeField] GameObject completedCrossing;
    [SerializeField] Light[] lanterns = new Light[0];
    [SerializeField] FinalIslandPortal portal;

    GateBridge ownerBridge;
    Color[] lanternOriginalColors;
    bool crossingKnown, crossingOpen;
    float openingGlow;
    static readonly Color MagicMint = new Color(0.45f, 0.93f, 0.84f);

    public FinalIslandPortal Portal => portal;
    public bool IsBaked => constructionBarrier != null && completedCrossing != null && portal != null;

#if UNITY_EDITOR
    public void Configure(GameObject barrier, GameObject crossing, Light[] bakedLanterns, FinalIslandPortal bakedPortal)
    {
        constructionBarrier = barrier;
        completedCrossing = crossing;
        lanterns = bakedLanterns;
        portal = bakedPortal;
    }
#endif

    void Awake()
    {
        ownerBridge = GetComponentInParent<GateBridge>();
        CacheLanternColors();
    }

    void CacheLanternColors()
    {
        if (lanterns == null || lanternOriginalColors != null) return;
        lanternOriginalColors = new Color[lanterns.Length];
        for (int i = 0; i < lanterns.Length; i++)
            lanternOriginalColors[i] = lanterns[i] != null ? lanterns[i].color : Color.white;
    }

    public void SetCrossingOpen(bool open)
    {
        if (crossingKnown && !crossingOpen && open && Application.isPlaying)
            openingGlow = 1f; // once on completion, never on a loaded save
        crossingKnown = true;
        crossingOpen = open;
        if (constructionBarrier != null) constructionBarrier.SetActive(!open);
        if (completedCrossing != null) completedCrossing.SetActive(open);
    }

    void Update()
    {
        if (lanterns == null) return;
        CacheLanternColors();
        float normalIntensity = Mathf.Lerp(.4f, 2.6f, DayNightCycle.Night01);
        bool reduced = VibeSettings.ReduceEffects;
        var player = PlayerMovement.Instance;
        bool nearby = player != null && (player.transform.position - transform.position).sqrMagnitude < 38f * 38f;
        openingGlow = Mathf.MoveTowards(openingGlow, 0f, Time.deltaTime / 3.1f);
        float idlePulse = !reduced && crossingOpen && nearby
            ? 0.045f + 0.035f * Mathf.Sin(Time.time * 0.83f) : 0f;
        // A brief soft wave travels through the EXISTING bridge lanterns.
        for (int i = 0; i < lanterns.Length; i++)
        {
            Light lantern = lanterns[i];
            if (lantern == null) continue;
            float wave = !reduced && nearby && crossingOpen
                ? openingGlow * Mathf.Max(0f, Mathf.Sin((1f - openingGlow) * 9f - i * 0.65f)) : 0f;
            float accent = Mathf.Clamp01(idlePulse + wave * 0.52f);
            lantern.intensity = normalIntensity * (1f + idlePulse + wave * 0.75f);
            lantern.color = Color.Lerp(lanternOriginalColors[i], MagicMint, accent);
        }
    }

    void OnDisable()
    {
        if (lanternOriginalColors == null || lanterns == null) return;
        for (int i = 0; i < lanterns.Length && i < lanternOriginalColors.Length; i++)
            if (lanterns[i] != null) lanterns[i].color = lanternOriginalColors[i];
    }
}

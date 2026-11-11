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

    public void SetCrossingOpen(bool open)
    {
        if (constructionBarrier != null) constructionBarrier.SetActive(!open);
        if (completedCrossing != null) completedCrossing.SetActive(open);
    }

    void Update()
    {
        float intensity = Mathf.Lerp(.4f, 2.6f, DayNightCycle.Night01);
        if (lanterns == null) return;
        foreach (var lantern in lanterns)
            if (lantern != null) lantern.intensity = intensity;
    }
}

using UnityEngine;

/// <summary>Мягкое покачивание деревьев на ветру. Сам не обновляется: регистрирует себя в WindManager (один общий Update).</summary>
public class ToonSway : MonoBehaviour
{
    public float angle = 1.2f;
    public float speed = 0.9f;

    float amplitudeMul = 1f;     // ResourceItem: старые деревья качаются меньше, молодые больше
    WindAgent agent;

    // агент нужен TreeGrowthVisual: росток качается в той же фазе, что и дерево, поэтому при смене нет «скачка»
    public WindAgent Agent { get { return agent; } }

    void Awake() { EnsureAgent(); }

    void EnsureAgent()
    {
        if (agent != null) return;
        agent = new WindAgent
        {
            t = transform,
            r0 = transform.localRotation,
            phase = WindManager.PhaseAt(transform.position),
            angle = angle * amplitudeMul,
            speed = speed,
            item = GetComponent<ResourceItem>()
        };
    }

    void Start()
    {
        if (agent.rend == null) agent.rend = FindRenderer();
    }

    void OnEnable()
    {
        EnsureAgent();
        WindManager.Add(agent);
    }

    void OnDisable()
    {
        WindManager.Remove(agent);
        // как и раньше: объект «стоит ровно», когда не качается (TreeHits делает то же в своём OnDisable)
        if (agent != null) transform.localRotation = agent.r0;
    }

    // ResourceItem наклонил дерево после Awake: качание должно идти от НОВОЙ стоячей позы, иначе первый кадр ветра выпрямит дерево
    public void Rebase()
    {
        EnsureAgent();
        agent.r0 = transform.localRotation;
    }

    public void SetAmplitude(float mul)
    {
        amplitudeMul = mul;
        if (agent != null) agent.angle = angle * amplitudeMul;
    }

    public void SetRenderer(Renderer r)
    {
        EnsureAgent();
        agent.rend = r;
    }

    Renderer FindRenderer()
    {
        var own = GetComponent<MeshRenderer>();
        if (own != null) return own;
        foreach (var r in GetComponentsInChildren<MeshRenderer>(false))
        {
            string n = r.gameObject.name;
            if (n == "Outline" || n == "Hull" || n == "HighlightRing") continue;
            return r;
        }
        return null;   // не нашли: качаем всегда
    }
}

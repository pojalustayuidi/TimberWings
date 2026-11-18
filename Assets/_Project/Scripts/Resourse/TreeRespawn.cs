using UnityEngine;

// Native regrowth, with a CHOP-01 gate: the timer begins after the trunk is cleared.
public class TreeRespawn : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 1.8f, 0f);
    GameObject tree, stump;
    float baseSeconds;
    float grown;
    WorldTag worldTag;
    ResourceItem item;
    TreeGrowthVisual growth;
    float age;
    bool sinkStarted;
    float stumpStartY, stumpDepth;

    public static TreeRespawn Begin(GameObject tree, GameObject stump, Vector3 position, float seconds, TreeGrowthVisual growth = null)
    {
        var go = new GameObject("TreeRespawn");
        go.transform.position = position;
        var r = go.AddComponent<TreeRespawn>();
        r.tree = tree;
        r.stump = stump;
        r.item = tree != null ? tree.GetComponent<ResourceItem>() : null;
        r.growth = growth;
        r.baseSeconds = Mathf.Max(0.1f, seconds);
        if (growth != null) growth.SetStumpBlend(stump != null ? 1f : 0f);
        return r;
    }

    void OnEnable() { SaveSystem.Loaded += OnSaveLoaded; }
    void OnDisable() { SaveSystem.Loaded -= OnSaveLoaded; }

    void OnSaveLoaded()
    {
        if (growth != null) growth.Finish();
        if (tree != null) tree.SetActive(true);
        if (item != null) item.BeginWave(false);
        if (stump != null) Destroy(stump);
        Destroy(gameObject);
    }

    void Start()
    {
        worldTag = WorldTag.Add(gameObject, true);
        worldTag.offset = offset;
        worldTag.SetFontSize(34f);
        Refresh(TotalSeconds());
    }

    float TotalSeconds() { return Mathf.Max(0.1f, baseSeconds * LumberSkills.GrowthMul); }

    void Update()
    {
        if (ChopPrototypeLog.Pending(item))
        {
            if (worldTag != null) worldTag.visible = false;
            return;
        }
        if (worldTag != null) worldTag.visible = true;
        float total = TotalSeconds();
        grown += Time.deltaTime / total;
        age += Time.deltaTime;
        if (stump != null && growth != null && GameBalance.StumpSinks) TickStump();

        if (grown >= 1f)
        {
            if (item != null) item.RestoreTransform();
            if (tree != null) tree.SetActive(true);
            if (item != null) item.PlaySpring();
            if (growth != null) growth.Finish();
            if (stump != null) Destroy(stump);
            Destroy(gameObject);
            return;
        }

        if (growth != null) growth.SetProgress(grown);
        Refresh((1f - grown) * total);
    }

    void TickStump()
    {
        float start = GameBalance.GrowFallSeconds + Mathf.Max(0f, GameBalance.StumpHoldSeconds);
        if (age < start) return;
        Transform st = stump.transform;
        if (!sinkStarted)
        {
            sinkStarted = true;
            foreach (var c in stump.GetComponentsInChildren<Collider>()) c.enabled = false;
            float h = 0.6f;
            var rs = stump.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                h = b.size.y;
            }
            stumpDepth = Mathf.Max(0.4f, h + 0.15f);
            stumpStartY = st.position.y;
            if (item != null) item.PlayDust();
        }

        float u = Mathf.Clamp01((age - start) / Mathf.Max(0.1f, GameBalance.StumpSinkSeconds));
        float e = u * u * (3f - 2f * u);
        Vector3 pos = st.position;
        pos.y = stumpStartY - stumpDepth * e;
        st.position = pos;
        growth.SetStumpBlend(1f - e);
        if (u >= 1f)
        {
            Destroy(stump);
            stump = null;
        }
    }

    void Refresh(float left)
    {
        if (worldTag == null) return;
        worldTag.SetText(Mathf.CeilToInt(left).ToString());
        worldTag.SetProgress(1f - grown);
    }
}

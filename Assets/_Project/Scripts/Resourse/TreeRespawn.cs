using UnityEngine;
using UnityEngine.SceneManagement;

// The native regrowth clock starts after the experimental trunk has been collected.
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
    ChopPrototypeLog trunk;
    ChopPrototypeSave.Tree saved;
    bool growthStarted;

    internal static TreeRespawn Begin(ResourceItem item, GameObject stump, Vector3 position, float seconds,
        TreeGrowthVisual growth, ChopPrototypeSave.Tree saved, ChopPrototypeLog trunk)
    {
        var go = new GameObject("TreeRespawn");
        SceneManager.MoveGameObjectToScene(go, item.gameObject.scene);
        if (stump != null && stump.scene != item.gameObject.scene)
            SceneManager.MoveGameObjectToScene(stump, item.gameObject.scene);
        go.transform.position = position;
        var r = go.AddComponent<TreeRespawn>();
        r.tree = item.gameObject;
        r.stump = stump;
        r.item = item;
        r.growth = growth;
        r.saved = saved;
        r.trunk = trunk;
        r.grown = saved != null ? saved.grown : 0f;
        r.age = saved != null ? saved.regrowthAge : 0f;
        r.baseSeconds = Mathf.Max(0.1f, seconds);
        if (growth != null) growth.SetStumpBlend(stump != null ? 1f : 0f);
        return r;
    }

    internal void DiscardForLoad()
    {
        if (trunk != null) trunk.Discard();
        if (growth != null) growth.Finish();
        if (stump != null)
        {
            stump.SetActive(false);
            Destroy(stump);
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    void Start()
    {
        worldTag = WorldTag.Add(gameObject, true);
        worldTag.offset = offset;
        worldTag.SetFontSize(34f);
        bool waiting = trunk != null && trunk.HasWood;
        worldTag.visible = !waiting;
        Refresh((1f - grown) * TotalSeconds());
    }

    float TotalSeconds() { return Mathf.Max(0.1f, baseSeconds * LumberSkills.GrowthMul); }

    void Update()
    {
        if (trunk != null && trunk.HasWood)
        {
            if (worldTag != null) worldTag.visible = false;
            return;
        }
        if (worldTag != null) worldTag.visible = true;
        if (saved != null && !growthStarted)
        {
            growthStarted = true;
            if (growth != null) growth.ResumeGrowth(grown, stump != null);
        }
        float total = TotalSeconds();
        grown += Time.deltaTime / total;
        age += Time.deltaTime;
        if (saved != null)
        {
            saved.grown = grown;
            saved.regrowthAge = age;
        }
        if (stump != null && growth != null && GameBalance.StumpSinks) TickStump();

        if (grown >= 1f)
        {
            ChopPrototypeSave.Finish(saved);
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

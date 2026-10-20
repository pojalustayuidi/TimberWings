using UnityEngine;

// Круглый таймер над пнём: показывает, через сколько секунд вырастет дерево.
// Создаётся сам из ResourceItem.ChopDown(), вешать вручную не нужно.
//
// Задача 3 («Лесоруб»): время роста = базовое время × LumberSkills.GrowthMul (Крепкая древесина ×1.2, Быстрый рост ×0.6).
// Рост хранится ДОЛЕЙ (grown 0..1), а множитель читается каждый кадр. Поэтому:
//   - купили или сбросили навык, пока деревья растут: у них сразу меняется остаток времени (доля роста сохраняется);
//   - ничего не нужно пересчитывать и сохранять.
//
// Задача 2a: базовое время теперь зависит от размера дерева (30 / 45 / 75 с, его передаёт ResourceItem).
//   Та же доля grown управляет визуалом отроста (TreeGrowthVisual): росток, молодое, зрелое.
//   В последнем кадре дерево включается и двойник выключается в одном Update, поэтому мерцания нет.
public class TreeRespawn : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 1.8f, 0f);

    GameObject tree, stump;   // имена полей нужны ResourceHighlighter (рефлексия по «stump»), не переименовывать
    float baseSeconds;        // время роста без навыков
    float grown;              // доля роста 0..1
    WorldTag worldTag;
    ResourceItem item;
    TreeGrowthVisual growth;

    // пень уходит в землю: свежий пень стоит StumpHoldSeconds после падения, потом погружается StumpSinkSeconds, и росток остаётся в земле
    float age;                // секунд с момента рубки (реальное время, навыки не влияют)
    bool sinkStarted;
    float stumpStartY, stumpDepth;

    // seconds: базовое время роста по размеру дерева (30 / 45 / 75 с), без навыков
    // growth: двойник для визуала отроста (null = как раньше, дерево просто появится в конце)
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
        if (growth != null) growth.SetStumpBlend(stump != null ? 1f : 0f);   // нет пня: росток сразу стоит на земле
        return r;
    }

    void OnEnable() { SaveSystem.Loaded += OnSaveLoaded; }
    void OnDisable() { SaveSystem.Loaded -= OnSaveLoaded; }

    // Load: деревья не сохраняются, срубленные считаются спелыми (дерево возвращается сразу, пень и таймер уходят)
    void OnSaveLoaded()
    {
        if (growth != null) growth.Finish();
        if (tree != null) tree.SetActive(true);
        if (item != null) item.BeginWave(false);   // загрузка: срубленное дерево тоже вырастает волной вместе с остальными
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

    // полное время роста с учётом навыков прямо сейчас
    float TotalSeconds() { return Mathf.Max(0.1f, baseSeconds * LumberSkills.GrowthMul); }

    void Update()
    {
        float total = TotalSeconds();
        grown += Time.deltaTime / total;
        age += Time.deltaTime;
        if (stump != null && growth != null && GameBalance.StumpSinks) TickStump();

        if (grown >= 1f)
        {
            // всё в одном кадре: двойник гаснет, дерево включается (оно уже стоит в позе двойника), пень уходит
            if (item != null) item.RestoreTransform();
            if (tree != null) tree.SetActive(true);   // дерево выросло
            if (item != null) item.PlaySpring();       // пружинка, облачко листьев и тихий звук (только рядом с игроком)
            if (growth != null) growth.Finish();
            if (stump != null) Destroy(stump);
            Destroy(gameObject);
            return;
        }

        if (growth != null) growth.SetProgress(grown);
        Refresh((1f - grown) * total);
    }

    // пень погружается в землю (с колайдерами off: игрок проходит), на старте облачко пыли. В конце пень уничтожается
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
            stumpDepth = Mathf.Max(0.4f, h + 0.15f);   // уходит целиком под землю
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
            stump = null;   // ResourceHighlighter читает это поле и умеет null
        }
    }

    void Refresh(float left)
    {
        if (worldTag == null) return;
        worldTag.SetText(Mathf.CeilToInt(left).ToString());
        worldTag.SetProgress(1f - grown);
    }
}

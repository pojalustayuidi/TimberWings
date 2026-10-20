// Assets/_Project/Scripts/Resource/TreeCollision.cs (НЕ в Editor)
// Сплошные деревья и нормальные хитбоксы.
//
// Раньше у дерева был ОДИН капсульный коллайдер на весь корень (r 0.55, h 3.8: шире кроны молодого дерева и тоньше кроны старого).
// Через него нельзя было ни пройти нормально, ни попасть. Теперь два разных коллайдера с разной работой:
//   СТВОЛ  (дочерний объект "TrunkSolid", НЕ триггер): тонкая капсула только по стволу. Игрок упирается в неё и огибает дерево.
//          Трения нет (PhysicsMaterial), поэтому игрок скользит вдоль ствола, а не прилипает.
//   ВЫБОР  (старый коллайдер на корне, ТРИГГЕР, тег Resource): по размеру кроны. Через него игрок проходит сквозь крону,
//          а PlayerInteraction находит дерево (OverlapSphere с QueryTriggerInteraction.Collide, триггеры он видит).
// Размеры капсул заданы для сгенерированных деревьев (TreeMeshFactory) в локальных единицах: корень уже масштабирован TreeScaleMul.
// Свой префаб (useGeneratedModel = false): радиус и высота ствола считаются по bounds рендереров.
//
// Остальные деревья-декорации (тег "Tree", без ResourceItem): толстая капсула на всё дерево заменяется стволом (FixDecor, один раз при старте).
using UnityEngine;

public static class TreeCollision
{
    public const string TrunkName = "TrunkSolid";

    //                                  молодое  зрелое  старое   (порядок = enum TreeSize)
    static readonly float[] PickR  = { 0.45f,   0.70f,  0.90f };
    static readonly float[] PickH  = { 2.7f,    4.3f,   4.8f  };
    static readonly float[] TrunkR = { 0.20f,   0.32f,  0.55f };   // чуть шире визуального ствола (0.13 / 0.24 / 0.62 у корня)
    static readonly float[] TrunkH = { 1.2f,    1.8f,   2.2f  };

#if UNITY_6000_0_OR_NEWER
    static PhysicsMaterial slip;
#else
    static PhysicMaterial slip;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { slip = null; }

    // ствол без трения: игрок скользит вдоль него, а не залипает
    public static
#if UNITY_6000_0_OR_NEWER
    PhysicsMaterial
#else
    PhysicMaterial
#endif
    SlipMat
    {
        get
        {
            if (slip == null)
            {
#if UNITY_6000_0_OR_NEWER
                slip = new PhysicsMaterial("TreeTrunk");
                slip.frictionCombine = PhysicsMaterialCombine.Minimum;
#else
                slip = new PhysicMaterial("TreeTrunk");
                slip.frictionCombine = PhysicMaterialCombine.Minimum;
#endif
                slip.dynamicFriction = 0f;
                slip.staticFriction = 0f;
                slip.bounciness = 0f;
            }
            return slip;
        }
    }

    // Зовёт ResourceItem.Awake после масштаба и смены модели. Возвращает радиус ствола в метрах (мир): по нему считается, где встать у дерева.
    public static float Setup(ResourceItem item)
    {
        if (item == null) return 0.4f;
        var root = item.transform;

        // уже делали (повторный Awake или дерево из пула): второй ствол не нужен
        var old = root.Find(TrunkName);
        if (old != null)
        {
            var oc = old.GetComponent<CapsuleCollider>();
            return oc != null ? oc.radius * Mathf.Max(Mathf.Abs(root.lossyScale.x), Mathf.Abs(root.lossyScale.z)) : 0.4f;
        }

        int si = Mathf.Clamp((int)item.Size, 0, 2);
        bool gen = item.useGeneratedModel;
        // дерево-вид (Tools → Forest → Trees → Build Species): габариты берём из TreeSpeciesInfo, а не по bounds рендереров
        var info = item.GetComponent<TreeSpeciesInfo>();
        bool custom = !gen && info != null;

        // 1) все прежние коллайдеры дерева становятся «зоной выбора»: сквозь них можно пройти, а луч и OverlapSphere их видят
        var cols = item.GetComponentsInChildren<Collider>(true);
        CapsuleCollider rootCap = null;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            c.isTrigger = true;
            if (rootCap == null && c.gameObject == item.gameObject) rootCap = c as CapsuleCollider;
        }

        // 2) зона выбора по размеру кроны (для сгенерированных деревьев и деревьев-видов: их габариты известны)
        if (gen || custom)
        {
            float pr = custom ? info.pickRadius : PickR[si];
            float ph = custom ? info.pickHeight : PickH[si];
            if (rootCap == null && cols.Length == 0) rootCap = item.gameObject.AddComponent<CapsuleCollider>();
            if (rootCap != null)
            {
                rootCap.isTrigger = true;
                rootCap.direction = 1;
                rootCap.radius = pr;
                rootCap.height = Mathf.Max(ph, pr * 2f + 0.01f);
                rootCap.center = new Vector3(0f, ph * 0.5f - 0.1f, 0f);
            }
        }

        // 3) сплошной ствол
        float r, h, cy;
        if (gen) { r = TrunkR[si]; h = TrunkH[si]; cy = h * 0.5f - 0.1f; }
        else if (custom) { r = info.trunkRadius; h = info.trunkHeight; cy = h * 0.5f - 0.1f; }
        else FromBounds(item.gameObject, out r, out h, out cy);

        var go = new GameObject(TrunkName);
        go.layer = item.gameObject.layer;     // тег НЕ ставим: PlayerInteraction ищет только Resource, ствол он не примет за второе дерево
        go.transform.SetParent(root, false);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = r;
        cap.height = Mathf.Max(h, r * 2f + 0.01f);
        cap.center = new Vector3(0f, cy, 0f);
        cap.isTrigger = false;
        cap.sharedMaterial = SlipMat;

        return r * Mathf.Max(Mathf.Abs(root.lossyScale.x), Mathf.Abs(root.lossyScale.z));
    }

    // Пень сплошной тоже: иначе на месте только что срубленного дерева можно пройти насквозь. scale уже выставлен на объекте
    public static void AddStumpCollider(GameObject stump)
    {
        if (stump == null || stump.GetComponent<Collider>() != null) return;
        var cap = stump.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = 0.5f;        // меш пня: радиус 0.52 у земли и 0.42 сверху
        cap.height = 0.9f;
        cap.center = new Vector3(0f, 0.2f, 0f);
        cap.sharedMaterial = SlipMat;
    }

    // Дерево-декорация (тег "Tree", не ResourceItem): толстая капсула (r 0.7, h 4.5) на всё дерево заменяется стволом
    public static void FixDecor(GameObject tree)
    {
        if (tree == null || tree.GetComponent<ResourceItem>() != null) return;
        if (tree.transform.Find(TrunkName) != null) return;

        var cap = tree.GetComponent<CapsuleCollider>();
        float r, h, cy;
        FromBounds(tree, out r, out h, out cy);
        if (cap == null)
        {
            if (tree.GetComponentInChildren<Collider>(true) != null) return;   // свои коллайдеры (меш и т.п.) не трогаем
            cap = tree.AddComponent<CapsuleCollider>();
        }
        cap.isTrigger = false;
        cap.direction = 1;
        cap.radius = r;
        cap.height = Mathf.Max(h, r * 2f + 0.01f);
        cap.center = new Vector3(0f, cy, 0f);
        cap.sharedMaterial = SlipMat;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootDecor()
    {
        GameObject[] trees;
        try { trees = GameObject.FindGameObjectsWithTag("Tree"); }
        catch (UnityException) { return; }   // тега "Tree" в проекте нет: декораций нечего чинить
        for (int i = 0; i < trees.Length; i++) FixDecor(trees[i]);
    }

    // Радиус, высота и центр ствола в ЛОКАЛЬНЫХ единицах по bounds рендереров: ствол ~12% от ширины кроны, не выше 40% дерева
    static void FromBounds(GameObject go, out float r, out float h, out float cy)
    {
        r = 0.3f; h = 1.8f; cy = 0.8f;
        var rs = go.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds b = new Bounds();
        for (int i = 0; i < rs.Length; i++)
        {
            var rr = rs[i];
            if (rr == null || rr is ParticleSystemRenderer || rr.gameObject.name == "Hull" || rr.gameObject.name == "HighlightRing") continue;
            if (!has) { b = rr.bounds; has = true; } else b.Encapsulate(rr.bounds);
        }
        if (!has) return;

        Vector3 ls = go.transform.lossyScale;
        float sxz = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));

        float rW = Mathf.Clamp(0.12f * Mathf.Max(b.extents.x, b.extents.z), 0.15f, 0.65f);
        float hW = Mathf.Clamp(b.size.y * 0.4f, rW * 2f + 0.05f, 2.4f);
        r = rW / sxz;
        h = hW / sy;
        float baseLocalY = (b.min.y - go.transform.position.y) / sy;
        cy = baseLocalY + h * 0.5f;
    }
}

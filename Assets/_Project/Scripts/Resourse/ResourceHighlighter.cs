// Assets/_Project/Scripts/Resource/ResourceHighlighter.cs (НЕ в Editor)
// Ближайшее дерево: контур + кольцо + «Зажми E». Остальные деревья рядом: слабое кольцо (только на шаге «руби»).
// Пеньки приглушены.
//
// Что изменено против старой версии (она и давала лаг):
//   - раньше КАЖДЫЙ кадр: FindObjectsByType<TreeRespawn>, GetComponentsInChildren на каждом пне, SetPropertyBlock на каждом
//     рендерере, рефлексия в TutorialManager, линейный поиск дерева по списку. Теперь поиск раз в полсекунды, пни красятся один раз;
//   - пень красился белым цветом поверх своего материала. Теперь цвет материала умножается на stumpDim (затемнение);
//   - контур-оболочка рисовалась с Cull Back и закрывала дерево целиком. Теперь Cull Front (получается обводка);
//   - кольцо было непрозрачным диском. Теперь прозрачный материал;
//   - подсказка и контур прячутся, когда E занята разговором (InteractionHub.OwnsInput);
//   - слабые кольца зажигает IntroFlow (шаг Chop), а не поле currentStep в TutorialManager.
// Задача 3: с навыком lumber_fastgrow («Быстрый рост») ближайшее спелое дерево подсвечено мятным кольцом и контуром,
//   даже если оно далеко. Считается раз в scanInterval, гаснет сразу при сбросе навыка.
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

public class ResourceHighlighter : MonoBehaviour
{
    [Header("Слабый блик")]
    public float glowRadius = 12f;
    public Color nearestRing = new Color32(0xF5, 0xC5, 0x42, 220);
    public Color weakRing = new Color32(0x6B, 0xCB, 0x5A, 90);
    [Tooltip("Подсветка «Быстрого роста» (lumber_fastgrow): ближайшее спелое дерево")]
    public Color growRing = new Color32(0x7B, 0xE0, 0xB0, 230);

    [Header("Пеньки")]
    [Range(0.3f, 1f)] public float stumpDim = 0.65f;

    [Header("Подсказка над деревом")]
    [Tooltip("Выключается сама, если у игрока есть InteractionHint (иначе над деревом висели две подсказки «Зажми E»)")]
    public bool showHint = true;

    [Header("Производительность")]
    public float scanInterval = 0.5f;

    class Extra
    {
        public ResourceItem item;
        public GameObject hull;
        public Transform ring;
        public Renderer ringR;
    }

    static ResourceHighlighter inst;

    PlayerInteraction inter;
    Transform nearest;
    ResourceItem nearItem;     // дерево-цель: map индексируется ResourceItem.GetInstanceID(), а не Transform коллайдера (раньше подсветка цели не находила Extra)
    WorldTag hintTag;
    Transform hintHolder;
    Material outlineMat, ringMat;
    MaterialPropertyBlock mpb;
    FieldInfo stumpField;
    float scanTimer;
    bool weakShown;
    ResourceItem growTarget;   // ближайшее спелое дерево (считается в Scan)
    ResourceItem glowItem;     // дерево, на котором сейчас горит подсветка «Быстрого роста»

    readonly Dictionary<int, Extra> map = new Dictionary<int, Extra>(64);
    readonly List<int> dead = new List<int>();
    readonly HashSet<int> dimmed = new HashSet<int>();

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(this); return; }
        inst = this;
        inter = FindFirstObjectByType<PlayerInteraction>();
        if (inter != null && inter.GetComponent<InteractionHint>() != null) showHint = false;
        mpb = new MaterialPropertyBlock();
        stumpField = typeof(TreeRespawn).GetField("stump", BindingFlags.NonPublic | BindingFlags.Instance);

        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        outlineMat = new Material(sh) { name = "ResHighlight_Outline" };
        outlineMat.enableInstancing = true;
        if (outlineMat.HasProperty("_BaseColor")) outlineMat.SetColor("_BaseColor", new Color32(0xF5, 0xC5, 0x42, 255));
        if (outlineMat.HasProperty("_Cull")) outlineMat.SetFloat("_Cull", 1f);   // 1 = Front: видна только «спина» оболочки

        ringMat = new Material(sh) { name = "ResHighlight_Ring" };
        ringMat.enableInstancing = true;
        if (ringMat.HasProperty("_Surface"))
        {
            ringMat.SetFloat("_Surface", 1f);
            ringMat.SetFloat("_Blend", 0f);
            ringMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            ringMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            ringMat.SetFloat("_ZWrite", 0f);
            ringMat.SetOverrideTag("RenderType", "Transparent");
            ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        ringMat.renderQueue = 3000;

        var go = new GameObject("ResHint_Tag");
        hintHolder = go.transform;
        var wt = go.AddComponent<WorldTag>();
        wt.offset = new Vector3(0f, 3.2f, 0f);
        wt.SetText("Зажми <color=#6BCB5A>E</color>");
        hintTag = wt;
        hintTag.visible = false;
    }

    void OnDestroy()
    {
        if (inst == this) inst = null;
        if (hintHolder != null) Destroy(hintHolder.gameObject);
    }

    void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            Scan();
        }

        if (inter == null) { hintTag.visible = false; return; }

        bool owned = InteractionHub.OwnsInput;   // E у разговора: про дерево молчим
        Transform target = (!owned && inter.CurrentTarget != null) ? inter.CurrentTarget.transform : null;
        ResourceItem tItem = target != null ? inter.TargetItem : null;
        bool hoverOnly = false;

        // идём к дереву (клик или E издалека): подсвечиваем его так же, как цель рубки, пока не дойдём
        if (target == null && !owned)
        {
            var wt = inter.WalkTarget;
            if (wt != null && wt.gameObject.activeInHierarchy) { target = wt.transform; tItem = wt; }
        }

        // курсор над далёким деревом: оно подсвечивается заранее («сюда кликнешь»). Подсказку «Зажми E» над ним не показываем
        if (target == null && !owned)
        {
            var hv = inter.HoverItem;
            if (hv != null && hv.gameObject.activeInHierarchy) { target = hv.transform; tItem = hv; hoverOnly = true; }
        }

        if (target != nearest)
        {
            if (nearest != null) SetNearest(nearItem, false);
            nearest = target;
            nearItem = target != null ? tItem : null;
            if (nearest != null) SetNearest(nearItem, true);
        }

        if (nearest != null)
        {
            // подсказка выше у больших деревьев (WorldTag.offset = 3.2 м для зрелого)
            float hintLift = nearItem != null ? 3.2f * (nearItem.ScaleMul - 1f) : 0f;
            hintHolder.position = nearest.position + Vector3.up * hintLift;
            hintTag.visible = showHint && !inter.IsChopping && !hoverOnly;
            Extra ex;
            if (nearItem != null && map.TryGetValue(nearItem.GetInstanceID(), out ex) && ex.ring != null)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.time * 3f);
                ex.ring.localScale = new Vector3(2.4f * s, 0.01f, 2.4f * s);
            }
        }
        else hintTag.visible = false;

        bool needWeak = IntroFlow.Instance != null && IntroFlow.Instance.Current == IntroFlow.Step.Chop;
        if (needWeak || weakShown) UpdateWeak(needWeak);

        UpdateGrowGlow();
    }

    // «Быстрый рост»: мятное кольцо на ближайшем спелом дереве. Навык проверяется каждый кадр, поэтому после сброса подсветка гаснет сразу.
    // Если это дерево и так цель рубки (у него обычная золотая подсветка), второе кольцо не рисуем.
    void UpdateGrowGlow()
    {
        ResourceItem want = null;
        if (inter != null && !InteractionHub.OwnsInput && PlayerSkills.Has(SkillIds.LumberFastGrow))
        {
            want = growTarget;
            if (want != null && (!want.gameObject.activeInHierarchy || inter.TargetItem == want)) want = null;
        }

        if (want != glowItem)
        {
            SetGlow(glowItem, false);
            glowItem = want;
            SetGlow(glowItem, true);
        }

        if (glowItem != null)
        {
            Extra ex;
            if (map.TryGetValue(glowItem.GetInstanceID(), out ex) && ex.ring != null)
            {
                float s = 1f + 0.12f * Mathf.Sin(Time.time * 3f);
                ex.ring.localScale = new Vector3(2.4f * s, 0.01f, 2.4f * s);
            }
        }
    }

    // map заполнен ключами ResourceItem.GetInstanceID() (см. Scan), поэтому здесь ищем по самому дереву
    void SetGlow(ResourceItem item, bool on)
    {
        Extra ex;
        if (item == null || !map.TryGetValue(item.GetInstanceID(), out ex)) return;
        if (ex.hull != null) ex.hull.SetActive(on);
        if (ex.ring == null) return;

        ex.ring.gameObject.SetActive(on);
        if (!on) return;
        ex.ringR.GetPropertyBlock(mpb);
        SetColor(mpb, ringMat, growRing);
        ex.ringR.SetPropertyBlock(mpb);
    }

    // слабые кольца: только пока нужно и только рядом с игроком
    void UpdateWeak(bool on)
    {
        weakShown = on;
        Vector3 me = inter != null ? inter.transform.position : Vector3.zero;
        int nearestId = nearItem != null ? nearItem.GetInstanceID() : 0;
        float lim = glowRadius * glowRadius;

        foreach (var kv in map)
        {
            var ex = kv.Value;
            if (ex.ring == null || kv.Key == nearestId) continue;

            bool show = false;
            if (on && ex.item != null && ex.item.gameObject.activeInHierarchy)
            {
                Vector3 d = ex.item.transform.position - me;
                d.y = 0f;
                show = d.sqrMagnitude <= lim;
            }

            if (ex.ring.gameObject.activeSelf != show) ex.ring.gameObject.SetActive(show);
            if (!show) continue;

            ex.ringR.GetPropertyBlock(mpb);
            float a = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.2f + kv.Key * 0.1f);
            var c = weakRing;
            c.a *= a;
            SetColor(mpb, ringMat, c);
            ex.ringR.SetPropertyBlock(mpb);
        }
    }

    static void SetColor(MaterialPropertyBlock b, Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) b.SetColor("_BaseColor", c);
        else b.SetColor("_Color", c);
    }

    // ------------------------------------------------------------ скан (раз в scanInterval)
    void Scan()
    {
        if (inter == null) inter = FindFirstObjectByType<PlayerInteraction>();

        foreach (var t in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None))
        {
            if (t == null) continue;
            int id = t.GetInstanceID();
            if (map.ContainsKey(id)) continue;
            map[id] = MakeExtra(t);
        }

        // деревья, которых уже нет в сцене
        dead.Clear();
        foreach (var kv in map) if (kv.Value.item == null) dead.Add(kv.Key);
        for (int i = 0; i < dead.Count; i++) map.Remove(dead[i]);

        // «Быстрый рост»: ближайшее спелое дерево к игроку (без ограничения расстояния)
        growTarget = (inter != null && PlayerSkills.Has(SkillIds.LumberFastGrow))
            ? LumberSkills.NearestRipe(inter.transform.position, 0f, null, false)
            : null;

        DimStumps();
    }

    Extra MakeExtra(ResourceItem t)
    {
        var ex = new Extra { item = t };

        var mf = t.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var hull = new GameObject("Hull");
            hull.transform.SetParent(mf.transform, false);   // оболочка повторяет трансформ самого меша
            hull.transform.localScale = Vector3.one * 1.06f;
            hull.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = hull.AddComponent<MeshRenderer>();
            // у меша дерева несколько сабмешей (кора и листва): контур нужен на каждом, иначе обводится только ствол
            int subs = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            var hullMats = new Material[subs];
            for (int i = 0; i < subs; i++) hullMats[i] = outlineMat;
            mr.sharedMaterials = hullMats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            hull.SetActive(false);
            ex.hull = hull;
        }

        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringGo.name = "HighlightRing";
        var col = ringGo.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        var rt = ringGo.transform;
        rt.SetParent(t.transform, false);
        rt.localPosition = new Vector3(0f, 0.10f, 0f);   // выше пятна земли (TreeGround), иначе кольцо тонет в нём
        rt.localScale = new Vector3(2.4f, 0.01f, 2.4f);
        var rr = ringGo.GetComponent<Renderer>();
        rr.sharedMaterial = ringMat;
        rr.shadowCastingMode = ShadowCastingMode.Off;
        rr.receiveShadows = false;
        ringGo.SetActive(false);
        ex.ring = rt;
        ex.ringR = rr;
        return ex;
    }

    // пень затемняется один раз: цвет материала * stumpDim
    void DimStumps()
    {
        if (stumpField == null) return;
        if (dimmed.Count > 512) dimmed.Clear();

        foreach (var rs in FindObjectsByType<TreeRespawn>(FindObjectsSortMode.None))
        {
            if (rs == null) continue;
            GameObject stump;
            try { stump = stumpField.GetValue(rs) as GameObject; }
            catch (System.Exception) { continue; }
            if (stump == null) continue;

            int id = stump.GetInstanceID();
            if (!dimmed.Add(id)) continue;

            foreach (var r in stump.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.sharedMaterial == null) continue;
                var m = r.sharedMaterial;
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : (m.HasProperty("_Color") ? "_Color" : null);
                if (prop == null) continue;
                Color c = m.GetColor(prop);
                c.r *= stumpDim; c.g *= stumpDim; c.b *= stumpDim;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(prop, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    void SetNearest(ResourceItem item, bool on)
    {
        Extra ex;
        if (item == null || !map.TryGetValue(item.GetInstanceID(), out ex)) return;
        if (ex.hull != null) ex.hull.SetActive(on);
        if (ex.ring == null) return;

        ex.ring.gameObject.SetActive(on);
        if (!on) return;
        ex.ringR.GetPropertyBlock(mpb);
        SetColor(mpb, ringMat, nearestRing);
        ex.ringR.SetPropertyBlock(mpb);
    }
}

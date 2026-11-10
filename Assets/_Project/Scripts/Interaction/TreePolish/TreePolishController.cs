using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-700), DisallowMultipleComponent]
public sealed partial class TreePolishController : MonoBehaviour
{
    [Serializable] public sealed class MeshPair { public Mesh source, shell; }
    [Serializable] public sealed class MeshBinding { public MeshFilter filter; public Mesh source, shell; }
    [Serializable] public sealed class Legacy { public Behaviour target; public bool enabled; }
    public PlayerInteraction interaction;
    public Canvas canvas;
    public RectTransform promptRect;
    public CanvasGroup card;
    public TMP_Text amount, detail;
    public Image cardIcon;
    public Material outline, ground;
    public Mesh ring, chevron;
    public MeshPair[] shells;
    public MeshBinding[] bindings;
    [Range(.5f, 4f)] public float outlinePixels = 1.8f;
    [Range(0f, 1f)] public float intensity = .85f;
    public bool showOutline = true, showDirection = true, showPreview = true;
    [Header("Дальность выделения")]
    [Tooltip("Выбранное для автоподхода дерево может оставаться подсвеченным до этой дистанции.")]
    [Min(1f)] public float walkingHighlightDistance = 32f;
    [Tooltip("Дальность особого кольца навыка «Быстрый рост».")]
    [Min(1f)] public float growMarkerDistance = 18f;
    public Color hoverColor = new Color(1f, .88f, .58f, .9f);
    public Color selectedColor = new Color(1f, .79f, .35f, .95f);
    public Color choppingColor = new Color(.65f, 1f, .81f, .95f);
    [HideInInspector] public List<Legacy> legacy = new List<Legacy>();
    [HideInInspector] public List<GameObject> ownedUI = new List<GameObject>();
    [HideInInspector] public string backup;
    [HideInInspector] public ChopRewards rewards;
    [HideInInspector] public RectTransform previousPoolRoot;
    [HideInInspector] public Image[] previousPool;
    [HideInInspector] public bool poolCaptured, ownsRewards;
    static TreePolishController instance;
    public static TreePolishController Active { get { return instance != null && instance.isActiveAndEnabled ? instance : null; } }
    public ResourceItem Focus { get; private set; }
    public TreeFocusRules.Kind State { get; private set; }
    BackpackTag pack;
    Rigidbody body;
    Camera cam;
    MaterialPropertyBlock groundBlock;
    readonly RaycastHit[] hits = new RaycastHit[24];
    readonly Vector3[] arrows = new Vector3[3];
    readonly bool[] arrowOn = new bool[3];
    readonly ResourceItem[] lessons = new ResourceItem[3];
    readonly float[] lessonBest = new float[3];
    readonly List<RaycastResult> uiHits = new List<RaycastResult>(12);
    PointerEventData pointer;
    EventSystem pointerSystem;
    bool overUI, groundOK;
    Vector3 ringPoint, walkDirection;
    float alpha, nextGround, nextUI, nextHint, nextLesson, switchedAt;
    ResourceItem grow;
    int lastBase = -1, lastFit = -1, lastDistance = -1;
    TreeFocusRules.Kind lastState;
    int lastResource;
    Vector2 lastCanvas;
    Rect lastSafe;
    bool safeRegistered;
    static readonly int Tint = Shader.PropertyToID("_FocusColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }
    // Native Unity objects cannot be created while the component is being constructed/deserialized.
    // Both callbacks run on the main thread; the null checks also cover re-enabling after a reload.
    void EnsureRenderBlocks()
    {
        if (groundBlock == null) groundBlock = new MaterialPropertyBlock();
        if (drawBlock == null) drawBlock = new MaterialPropertyBlock();
        if (sourceBlock == null) sourceBlock = new MaterialPropertyBlock();
        if (rendererBlock == null) rendererBlock = new MaterialPropertyBlock();
    }
    void Awake() { EnsureRenderBlocks(); instance = this; }
    void OnEnable() { EnsureRenderBlocks(); instance = this; }
    void Start()
    {
        if (interaction == null) interaction = FindFirstObjectByType<PlayerInteraction>();
        if (interaction != null) { pack = interaction.GetComponent<BackpackTag>(); body = interaction.GetComponent<Rigidbody>(); }
        if (card != null) { card.alpha = 0; card.blocksRaycasts = false; card.interactable = false; }
        Reflow();
    }
    void OnDisable()
    {
        if (instance == this) instance = null;
        if (card != null) card.alpha = 0;
        if (safeRegistered && card != null && HudSafeAreaRegistry.Instance != null) HudSafeAreaRegistry.Instance.Unregister(card.transform as RectTransform);
        safeRegistered = false; Focus = null; State = TreeFocusRules.Kind.None; alpha = 0;
        ClearOutline();
    }
    static bool Valid(ResourceItem item) { return item != null && item.isActiveAndEnabled && item.gameObject.activeInHierarchy; }
    void LateUpdate()
    {
        if (interaction == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float now = Time.unscaledTime, dt = Mathf.Min(.1f, Time.unscaledDeltaTime);
        bool busy = MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying || !PlayerInteraction.ChoppingEnabled;
        if (now >= nextUI)
        { nextUI = now + .08f; overUI = PointerOverUI(); }
        ResourceItem target = null; var kind = TreeFocusRules.Kind.None;
        if (!busy)
        {
            bool canPoint = !InteractionHub.OwnsInput && !overUI;
            Vector3 me = interaction.transform.position;
            bool walking = Valid(interaction.WalkTarget) && TreeFocusRules.InRange(me, interaction.WalkTarget.transform.position, walkingHighlightDistance);
            bool hovering = canPoint && Valid(interaction.HoverItem) && TreeFocusRules.InRange(me, interaction.HoverItem.transform.position, interaction.hoverHighlightDistance);
            bool nearby = canPoint && Valid(interaction.TargetItem) && TreeFocusRules.InRange(me, interaction.TargetItem.transform.position, interaction.radius + 1f);
            kind = TreeFocusRules.Priority(interaction.IsChopping && Valid(interaction.TargetItem), walking, hovering, nearby);
            if (kind == TreeFocusRules.Kind.Chopping || kind == TreeFocusRules.Kind.Nearby) target = interaction.TargetItem;
            else if (kind == TreeFocusRules.Kind.Walking) target = interaction.WalkTarget;
            else if (kind == TreeFocusRules.Kind.Hover) target = interaction.HoverItem;
        }
        if (target != Focus)
        {
            Focus = target; if (target != null) alpha = 0f; switchedAt = now; nextGround = 0; lastBase = -1; lastResource = 0;
            BindOutline(target);
        }
        State = kind;
        alpha = Mathf.MoveTowards(alpha, Focus != null ? 1f : 0f, dt / .14f);
        bool reduced = VibeSettings.ReduceEffects;
        Color color = kind == TreeFocusRules.Kind.Chopping ? choppingColor : kind == TreeFocusRules.Kind.Walking ? selectedColor : hoverColor;
        color.a *= alpha * intensity;
        if (showOutline && Focus != null && alpha > .01f) DrawOutline(cam, color, reduced ? outlinePixels * .8f : outlinePixels);
        if (now >= nextGround)
        {
            nextGround = now + .1f;
            groundOK = Focus != null && GroundAt(Focus.transform.position, out ringPoint);
            PrepareDirection();
        }
        if (Focus != null && groundOK)
        {
            float radius = Mathf.Clamp(Focus.TrunkRadius + .35f, .65f, 1.5f);
            float pop = reduced ? 1f : 1f + .05f * Mathf.Sin(Mathf.Clamp01((now - switchedAt) / .35f) * Mathf.PI);
            DrawGround(ring, Matrix4x4.TRS(ringPoint, Quaternion.identity, Vector3.one * radius * pop), color);
        }
        if (showDirection && State == TreeFocusRules.Kind.Walking && Focus != null)
        {
            for (int i = 0; i < 3; i++) if (arrowOn[i])
            {
                Color c = selectedColor; c.a = intensity * (reduced ? .62f : .48f + .22f * Mathf.Sin(now * 4f - i));
                DrawGround(chevron, Matrix4x4.TRS(arrows[i], Quaternion.LookRotation(walkDirection, Vector3.up), Vector3.one * .28f), c);
            }
        }
        if (now >= nextLesson) { nextLesson = now + .5f; FindLessons(); }
        DrawLessons(busy);
        if (now >= nextHint) { nextHint = now + .1f; UpdateCard(); }
        if (card != null)
        {
            bool visible = !busy && showPreview && Focus != null && State != TreeFocusRules.Kind.Chopping;
            var p = Focus != null ? cam.WorldToViewportPoint(Focus.transform.position + Vector3.up) : Vector3.zero;
            visible &= p.z > 0 && p.x > -.1f && p.x < 1.1f && p.y > -.1f && p.y < 1.1f;
            card.alpha = Mathf.MoveTowards(card.alpha, visible ? 1f : 0f, dt / .13f);
            if (canvas != null && (lastCanvas != ((RectTransform)canvas.transform).rect.size || lastSafe != Screen.safeArea)) Reflow();
            if (!safeRegistered && HudSafeAreaRegistry.Instance != null)
            { HudSafeAreaRegistry.Instance.Register(card.transform as RectTransform); safeRegistered = true; }
        }
    }
    bool PointerOverUI()
    {
        if (Application.isMobilePlatform || !Input.mousePresent) return false;
        var system = EventSystem.current; if (system == null) return false;
        if (pointer == null || pointerSystem != system) { pointerSystem = system; pointer = new PointerEventData(system); }
        pointer.Reset(); pointer.position = Input.mousePosition; uiHits.Clear(); system.RaycastAll(pointer, uiHits);
        for (int i = 0; i < uiHits.Count; i++) if (uiHits[i].gameObject.GetComponentInParent<Selectable>() != null) return true;
        return false;
    }
    bool GroundAt(Vector3 planned, out Vector3 point)
    {
        point = planned; int n = Physics.RaycastNonAlloc(planned + Vector3.up * 2f, Vector3.down, hits, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var hit = hits[i]; if (hit.collider == null || hit.normal.y < .65f) continue;
            var t = hit.collider.transform;
            if (t.GetComponentInParent<ResourceItem>() != null || t.GetComponentInParent<PlayerInteraction>() != null) continue;
            if (hit.point.y > planned.y + .65f || hit.point.y < planned.y - 1.8f) continue;
            if (hit.point.y > best) { best = hit.point.y; point = hit.point + hit.normal * .065f; }
        }
        return !float.IsNegativeInfinity(best);
    }
    void PrepareDirection()
    {
        for (int i = 0; i < 3; i++) arrowOn[i] = false;
        if (State != TreeFocusRules.Kind.Walking || Focus == null || interaction == null) return;
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero; velocity.y = 0;
        // A short heading cue follows actual steering. It is not a new route or movement controller.
        if (velocity.sqrMagnitude < .04f) return;
        walkDirection = velocity.normalized;
        float distance = Vector3.Distance(interaction.transform.position, Focus.transform.position);
        for (int i = 0; i < 3; i++)
        {
            float ahead = .6f + .38f * i;
            if (ahead > distance - .8f) continue;
            arrowOn[i] = GroundAt(interaction.transform.position + walkDirection * ahead, out arrows[i]);
        }
    }
    void UpdateCard()
    {
        if (Focus == null || amount == null || detail == null) return;
        int all = Mathf.Max(0, LumberSkills.BaseWood(Focus));
        int fit = TreeFocusRules.Fits(all, pack != null ? pack.Free : all);
        int meters = Mathf.Max(0, Mathf.RoundToInt(Vector3.Distance(interaction.transform.position, Focus.transform.position)));
        if (lastResource == Focus.GetInstanceID() && lastBase == all && lastFit == fit && lastState == State && lastDistance == meters) return;
        lastResource = Focus.GetInstanceID(); lastBase = all; lastFit = fit; lastState = State; lastDistance = meters;
        amount.text = "База: +" + all + " " + UiDraw.Plural(all, "бревно", "бревна", "бревен");
        amount.color = fit == 0 ? CandyTag.Danger : CandyTag.Ink;
        if (fit == 0) detail.text = "Рюкзак полон · сдай брёвна";
        else if (fit < all) detail.text = "Влезет " + fit + " из " + all + (State == TreeFocusRules.Kind.Walking ? " · идём" : "");
        else if (State == TreeFocusRules.Kind.Walking) detail.text = "Подход к дереву · " + meters + " м";
        else if (Focus != interaction.TargetItem) detail.text = meters > interaction.radius ? "ЛКМ · подойти к дереву" : "ЛКМ · рубить";
        else detail.text = Application.isMobilePlatform ? "Дерево выбрано" : interaction.chopKey + (interaction.mouseChop ? " / ЛКМ · рубить" : " · рубить");
    }
    void FindLessons()
    {
        grow = null; for (int i = 0; i < lessons.Length; i++) lessons[i] = null;
        if (interaction == null) return;
        if (PlayerSkills.Has(SkillIds.LumberFastGrow)) grow = LumberSkills.NearestRipe(interaction.transform.position, 0f, null, false);
        if (IntroFlow.Instance == null || IntroFlow.Instance.Current != IntroFlow.Step.Chop) return;
        var all = ResourceItem.Active; var best = lessonBest; for (int i = 0; i < 3; i++) best[i] = 144f;
        for (int i = 0; i < all.Count; i++)
        {
            var item = all[i]; if (!Valid(item) || item == Focus) continue;
            float d = (item.transform.position - interaction.transform.position).sqrMagnitude;
            for (int slot = 0; slot < 3; slot++) if (d < best[slot])
            { for (int k = 2; k > slot; k--) { best[k] = best[k - 1]; lessons[k] = lessons[k - 1]; } best[slot] = d; lessons[slot] = item; break; }
        }
    }
    void DrawLessons(bool busy)
    {
        if (busy || InteractionHub.OwnsInput) return;
        if (Valid(grow) && grow != Focus && PlayerSkills.Has(SkillIds.LumberFastGrow)
            && TreeFocusRules.InRange(interaction.transform.position,grow.transform.position,growMarkerDistance))
            DrawGround(ring, Matrix4x4.TRS(grow.transform.position + Vector3.up * .1f, Quaternion.identity, Vector3.one * 1.1f), new Color(.51f, .94f, .72f, .65f));
        if (IntroFlow.Instance == null || IntroFlow.Instance.Current != IntroFlow.Step.Chop) return;
        for (int i = 0; i < 3; i++) if (Valid(lessons[i]) && lessons[i] != Focus && lessons[i] != grow)
            DrawGround(ring, Matrix4x4.TRS(lessons[i].transform.position + Vector3.up * .1f, Quaternion.identity, Vector3.one * .7f), new Color(.63f, .91f, .71f, .22f));
    }
    void DrawGround(Mesh mesh, Matrix4x4 matrix, Color color)
    {
        if (mesh == null || ground == null || cam == null || color.a <= .005f) return;
        groundBlock.Clear(); groundBlock.SetColor(Tint, color);
        Graphics.DrawMesh(mesh, matrix, ground, 0, cam, 0, groundBlock, ShadowCastingMode.Off, false);
    }
    void Reflow()
    {
        if (card == null || canvas == null) return;
        lastCanvas = ((RectTransform)canvas.transform).rect.size; lastSafe = Screen.safeArea;
        float s = Mathf.Max(.01f, canvas.scaleFactor);
        var r = card.transform as RectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, 0);
        float bottom = lastSafe.yMin / s + 166f;
        if (promptRect != null && Mathf.Approximately(promptRect.anchorMin.y, 0f) && Mathf.Approximately(promptRect.anchorMax.y, 0f))
            bottom = Mathf.Max(bottom, promptRect.anchoredPosition.y + promptRect.rect.height * (1f - promptRect.pivot.y) + 16f);
        r.anchoredPosition = new Vector2(0, bottom);
        r.sizeDelta = new Vector2(Mathf.Min(386f, lastCanvas.x - 64f), 82f);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FinalIslandPortal : MonoBehaviour, IInteractable, IPriorityInteractable
{
    const float WalkSpeed = 1.48f;
    static readonly int Activation = Shader.PropertyToID("_Activation");
    static readonly int Motion = Shader.PropertyToID("_Motion");
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    readonly RaycastHit[] cameraHits = new RaycastHit[12];

    [SerializeField] GateBridge bridge;
    [SerializeField] Transform altar;
    [SerializeField] Transform walkPoint;
    [SerializeField] Light portalLight;
    [SerializeField] MeshRenderer field;
    MaterialPropertyBlock properties;

    PlayerMovement actor;
    Rigidbody body;
    Camera camera;
    CameraFollow follow;
    OrbitCamera orbit;
    bool playing, followEnabled, orbitEnabled, orthographic, balanceSpeed;
    float charge, elapsed, fieldOfView, originalSpeed;
    Vector3 startPosition, cameraPosition;
    Quaternion startFacing, cameraRotation;
    Image fade;
    RectTransform topBar, bottomBar;
    Canvas overlay;
    Action interrupted;

    public Transform Anchor => altar;
    public Vector3 PromptPoint => altar.position + Vector3.up * .78f;
    public string Prompt => "активировать портал";
    public float Radius => 1.75f;
    public bool Priority => true;
    public bool CanInteract
    {
        get
        {
            var player = PlayerMovement.Instance;
            var story = StoryFlow.Instance;
            if (playing || altar == null || player == null || player.ControlLocked || player.IsDashing
                || story == null || !story.CanActivatePortal(this)) return false;
            var pos = player.transform.position;
            var offset = pos - altar.position;
            return Mathf.Abs(offset.y) < 1.35f
                && offset.x * offset.x + offset.z * offset.z < Radius * Radius * 1.2f
                && transform.InverseTransformPoint(pos).z < -.68f;
        }
    }

#if UNITY_EDITOR
    public void Configure(GateBridge gate, Transform stone, MeshRenderer surface, Light light, Transform destination)
    {
        bridge = gate;
        altar = stone;
        field = surface;
        portalLight = light;
        walkPoint = destination;
    }
#endif

    public bool IsBaked => bridge != null && altar != null && walkPoint != null && portalLight != null && field != null;

    void Awake()
    {
        properties = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        Interactables.Register(this);
        UiModal.Register(this, () => playing);
        SaveSystem.Loaded += OnSaveChanged;
        SaveSystem.NewGameStarted += OnSaveChanged;
    }

    void OnDisable()
    {
        Interactables.Unregister(this);
        UiModal.Unregister(this);
        SaveSystem.Loaded -= OnSaveChanged;
        SaveSystem.NewGameStarted -= OnSaveChanged;
        if (playing)
        {
            StopAllCoroutines();
            Release(false);
        }
    }

    void OnSaveChanged()
    {
        if (!playing) return;
        StopAllCoroutines();
        Release(false);
    }

    public void Interact()
    {
        if (CanInteract) StoryFlow.Instance.ActivatePortal(this, PlayerMovement.Instance);
    }

    internal bool OnFarIsland(Vector3 position)
    {
        if (bridge == null || bridge.arrivalSpot == null) return false;
        Vector3 onIsland = bridge.arrivalSpot.parent.InverseTransformPoint(position);
        float radius = Mathf.Max(10f, bridge.islandRadius);
        return Mathf.Abs(onIsland.y) < 2.7f
            && onIsland.z >= bridge.arrivalSpot.localPosition.z - 1.1f
            && new Vector2(onIsland.x, onIsland.z).sqrMagnitude < radius * radius;
    }

    internal bool Play(PlayerMovement player, Action onShown, Action onInterrupted)
    {
        if (playing || player == null || EndingSequence.Active || Camera.main == null) return false;
        body = player.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic) return false;

        actor = player;
        interrupted = onInterrupted;
        startPosition = body.position;
        startFacing = player.model != null ? player.model.rotation : player.transform.rotation;
        originalSpeed = player.speed;
        balanceSpeed = player.useBalanceSpeed;
        float normalSpeed = balanceSpeed ? GameBalance.PlayerSpeed : originalSpeed;
        float multiplier = player.CurrentSpeed / Mathf.Max(.05f, normalSpeed);
        player.useBalanceSpeed = false;
        player.speed = WalkSpeed / Mathf.Max(.05f, multiplier);
        player.CancelWalk();
        body.linearVelocity = Vector3.zero;

        camera = Camera.main;
        cameraPosition = camera.transform.position;
        cameraRotation = camera.transform.rotation;
        orthographic = camera.orthographic;
        fieldOfView = camera.fieldOfView;
        follow = camera.GetComponent<CameraFollow>();
        followEnabled = follow != null && follow.enabled;
        orbit = FindFirstObjectByType<OrbitCamera>();
        orbitEnabled = orbit != null && orbit.enabled;
        if (orbitEnabled) orbit.enabled = false;
        if (followEnabled) follow.enabled = false;
        camera.orthographic = false;

        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.enabled || canvas.sortingOrder > UiLayers.OrbitCameraHud) continue;
            canvas.enabled = false;
            hiddenCanvases.Add(canvas);
        }
        CreateOverlay();
        elapsed = 0f;
        playing = true;
        StartCoroutine(Sequence(onShown));
        return true;
    }

    IEnumerator Sequence(Action onShown)
    {
        for (float time = 0f; time < 1.35f; time += Time.unscaledDeltaTime)
        {
            charge = Mathf.SmoothStep(.12f, 1f, time / 1.35f);
            yield return null;
        }
        charge = 1f;

        bool arrived = false;
        yield return WalkTo(-1.05f, () => arrived = true);
        if (!arrived)
        {
            Release(false);
            yield break;
        }

        arrived = false;
        yield return WalkTo(1.05f, () => arrived = true);
        if (!arrived)
        {
            Release(false);
            yield break;
        }

        for (float time = 0f; time < .68f; time += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, time / .68f));
            yield return null;
        }

        fade.color = Color.black;
        bool shown = EndingSequence.PlayPortal(onShown);
        if (shown) yield return null; // EndingCanvas starts opaque on the next frame.
        Release(shown);
    }

    IEnumerator WalkTo(float z, Action arrived)
    {
        bool reached = false;
        walkPoint.localPosition = new Vector3(0f, 0f, z);
        actor.WalkTo(walkPoint, .3f, () => reached = true, true);
        for (float time = 0f; actor != null && actor.AutoWalking && !reached && time < 4.75f; time += Time.unscaledDeltaTime)
            yield return null;
        if (reached) arrived();
    }

    void LateUpdate()
    {
        if (bridge == null) return;
        if (!playing)
            charge = Mathf.MoveTowards(charge, bridge.WalkwayActive ? .13f : .02f, Time.unscaledDeltaTime * .9f);
        if (field != null && properties != null)
        {
            properties.SetFloat(Activation, charge);
            properties.SetFloat(Motion, VibeSettings.ReduceEffects ? 0f : 1f);
            field.SetPropertyBlock(properties);
        }
        if (portalLight != null)
        {
            float idle = Mathf.Lerp(.36f, 1.12f, DayNightCycle.Night01);
            portalLight.intensity = idle + charge * 3.8f;
        }

        if (!playing || camera == null || actor == null) return;
        elapsed += Time.unscaledDeltaTime;
        float blend = Mathf.SmoothStep(0f, 1f, elapsed / .85f);
        float push = VibeSettings.ReduceEffects ? 0f : Mathf.SmoothStep(0f, 1f, (elapsed - 1.15f) / 4.2f);
        var wide = new Vector3(-3.35f, 2.85f, -6.55f);
        var close = new Vector3(-2.15f, 2.25f, -4.8f);
        var desired = transform.TransformPoint(Vector3.Lerp(wide, close, push));
        var focus = Vector3.Lerp(actor.transform.position + Vector3.up * .95f,
            transform.TransformPoint(new Vector3(0f, 1.75f, 0f)), .67f);
        desired = ClearShot(focus, desired);
        camera.transform.SetPositionAndRotation(Vector3.Lerp(cameraPosition, desired, blend),
            Quaternion.Slerp(cameraRotation, Quaternion.LookRotation(focus - desired), blend));
        camera.fieldOfView = Mathf.Lerp(fieldOfView, 47f, blend);

        float bar = .075f * blend;
        topBar.anchorMin = new Vector2(0f, 1f - bar);
        bottomBar.anchorMax = new Vector2(1f, bar);
    }

    Vector3 ClearShot(Vector3 focus, Vector3 desired)
    {
        Vector3 direction = desired - focus;
        float distance = direction.magnitude;
        direction /= distance;
        int hits = Physics.SphereCastNonAlloc(focus, .17f, direction, cameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            var hit = cameraHits[i];
            if (hit.distance <= .05f || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(actor.transform)) continue;
            distance = Mathf.Min(distance, Mathf.Max(.4f, hit.distance - .12f));
        }
        return focus + direction * distance;
    }

    void CreateOverlay()
    {
        var holder = new GameObject("PortalCinematicCanvas", typeof(RectTransform), typeof(Canvas));
        holder.transform.SetParent(transform, false);
        overlay = holder.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = UiLayers.Ending - 1;

        topBar = UiKit.MakeImage(holder.transform, "TopBar", null, false).rectTransform;
        bottomBar = UiKit.MakeImage(holder.transform, "BottomBar", null, false).rectTransform;
        topBar.GetComponent<Image>().color = Color.black;
        bottomBar.GetComponent<Image>().color = Color.black;
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = Vector2.one;
        bottomBar.anchorMin = Vector2.zero;
        bottomBar.anchorMax = Vector2.right;
        topBar.offsetMin = topBar.offsetMax = bottomBar.offsetMin = bottomBar.offsetMax = Vector2.zero;

        fade = UiKit.MakeImage(holder.transform, "Fade", null, false);
        UiLayout.Stretch(fade.rectTransform);
        fade.color = Color.clear;
    }

    void Release(bool crossed)
    {
        playing = false;
        if (actor != null)
        {
            actor.CancelWalk();
            actor.speed = originalSpeed;
            actor.useBalanceSpeed = balanceSpeed;
            if (crossed && body != null)
            {
                body.position = startPosition;
                body.linearVelocity = Vector3.zero;
                if (actor.model != null) actor.model.rotation = startFacing;
            }
        }
        if (camera != null)
        {
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            camera.fieldOfView = fieldOfView;
            camera.orthographic = orthographic;
        }
        if (follow != null) follow.enabled = followEnabled;
        if (orbit != null) orbit.enabled = orbitEnabled;
        CameraFollow.SnapToTarget();
        foreach (var canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
        hiddenCanvases.Clear();
        if (overlay != null) Destroy(overlay.gameObject);
        overlay = null;
        actor = null;
        var callback = interrupted;
        interrupted = null;
        if (!crossed) callback?.Invoke();
    }
}

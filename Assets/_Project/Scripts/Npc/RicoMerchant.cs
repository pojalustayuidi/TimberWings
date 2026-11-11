using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class RicoMerchant : MonoBehaviour
{
    public const string DisplayName = "Рико";
    public const string ModelPath = "Rico/Rico";
    const float GreetingRadius = 4.2f;
    const float MaxTurn = 28f;

    public TradingStall Stall { get; private set; }

    Speaker speaker;
    Transform visual;
    Animator animator;
    AnimationClip idle, greet, talk, trade, gesture;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable idleTrack, gestureTrack;
    float idleTime, gestureTime, greetAfter, yaw;
    bool wasNear, wasSpeaking;

    public static RicoMerchant Create(StoryFlow story)
    {
        var market = story.market;
        if (market == null) return null;

        var prefab = Resources.Load<GameObject>(ModelPath);
        if (prefab == null || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
        {
            Debug.LogWarning("Рико: проверь импорт модели Rico.fbx.", story);
            return null;
        }

        var legacy = FindLegacyMerchant(story);
        bool legacyIsSeparate = legacy != null && !market.transform.IsChildOf(legacy.transform)
            && !story.transform.IsChildOf(legacy.transform);
        var shop = market.GetComponentInParent<ShopLife>(true);
        var link = market.GetComponent<BuildStageLink>();
        if (shop == null && link != null && link.shop != null) shop = link.shop.GetComponent<ShopLife>();
        var anchor = story.ricoSpot;
        Vector3 position, forward;
        float scale = 1f;
        if (anchor != null)
        {
            position = anchor.position;
            forward = anchor.forward;
        }
        else if (shop != null && shop.counterPoint != null && shop.greetPoint != null)
        {
            var shopSpace = shop.transform;
            Vector3 counter = shopSpace.InverseTransformPoint(shop.counterPoint.position);
            Vector3 entrance = shopSpace.InverseTransformPoint(shop.greetPoint.position);
            position = shopSpace.TransformPoint(CounterSide(shop, counter, entrance.y));
            forward = shopSpace.forward;
            scale = Mathf.Clamp(shopSpace.TransformVector(Vector3.up).magnitude, .55f, 1.3f);
        }
        else
        {
            anchor = legacyIsSeparate ? legacy.transform : market.transform;
            position = anchor.position;
            forward = anchor.forward;
            if (anchor == market.transform) position += anchor.right * .85f;
        }

        var root = new GameObject("Rico_Merchant");
        root.SetActive(false);
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(HorizontalForward(forward, market.transform.forward)));
        root.transform.localScale = Vector3.one * scale;
        root.transform.SetParent(story.transform, true);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var alignment = new GameObject("ModelAlignment").transform;
        alignment.SetParent(visual.transform, false);
        var model = Instantiate(prefab, alignment, false);
        model.name = "Rico_Model";
        var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        AlignModel(alignment, renderer);
        var material = Resources.Load<Material>("Rico/RicoPalette");
        if (material != null) renderer.sharedMaterial = material;
        var actor = root.AddComponent<RicoMerchant>();
        actor.visual = visual.transform;
        actor.animator = model.GetComponent<Animator>();
        if (actor.animator == null) actor.animator = model.AddComponent<Animator>();
        actor.animator.applyRootMotion = false;
        actor.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actor.LoadClips();

        actor.speaker = root.AddComponent<Speaker>();
        actor.speaker.displayName = DisplayName;
        actor.speaker.nameColor = new Color32(224, 172, 91, 255);
        actor.speaker.bubbleOffset = Vector3.up * (1.69f * scale);
        actor.speaker.turnRoot = visual.transform;
        actor.speaker.externalFacing = true;

        var conversation = root.AddComponent<DialogueSequence>();
        conversation.trigger = DialogueSequence.Trigger.Manual;
        conversation.prompt = "поговорить с Рико";

        var existingStall = story.marketTrading;
        if (existingStall == null && story.tradeAtBuilding) existingStall = market.GetComponentInChildren<TradingStall>(true);
        var legacyStall = legacy != null ? legacy.GetComponentInChildren<TradingStall>(true) : null;
        if (existingStall != null && (!legacyIsSeparate || !existingStall.transform.IsChildOf(legacy.transform)))
        {
            actor.Stall = existingStall;
            existingStall.enabled = true;
        }
        else
        {
            actor.Stall = root.AddComponent<TradingStall>();
            if (legacyStall != null)
            {
                actor.Stall.radius = legacyStall.radius;
                actor.Stall.coinColor = legacyStall.coinColor;
            }
        }
        conversation.radius = actor.Stall.radius;
        actor.Stall.BindConversation(conversation);
        if (shop != null) shop.stall = actor.Stall;

        if (legacyIsSeparate) legacy.SetActive(false);
        else if (legacy != null)
        {
            foreach (var component in legacy.GetComponents<Behaviour>())
                if (component != actor.Stall && (component is Speaker || component is DialogueSequence ||
                    component is NpcBanter || component is TradingStall || component is NpcMarker)) component.enabled = false;
        }

        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = Vector3.up * .75f;
        collider.height = 1.5f;
        collider.radius = .25f;
        return actor;
    }

    static Vector3 CounterSide(ShopLife shop, Vector3 counter, float platformY)
    {
        var stages = shop.stages != null ? shop.stages : shop.GetComponent<ShopStages>();
        var frame = stages != null ? stages.stage2 : null;
        var colliderRoot = frame != null ? frame.transform.Find("CounterCollider") : null;
        var box = colliderRoot != null ? colliderRoot.GetComponent<BoxCollider>() : null;
        if (box == null) return new Vector3(counter.x + .95f, platformY, counter.z - .1f);

        // Read the baked collider dimensions even when the market is unbuilt.
        Vector3 corner = box.center + new Vector3(box.size.x * .5f, 0f, box.size.z * .5f);
        corner = shop.transform.InverseTransformPoint(box.transform.TransformPoint(corner));
        return new Vector3(corner.x + .47f, platformY, corner.z - .23f);
    }

    static void AlignModel(Transform alignment, SkinnedMeshRenderer renderer)
    {
        // The jaw pivot is in front of the head bone in Rico's rest pose.
        var bones = renderer.bones;
        var head = Array.Find(bones, bone => bone != null && bone.name == "Head");
        var jaw = Array.Find(bones, bone => bone != null && bone.name == "Jaw");
        if (head == null || jaw == null) return;

        Vector3 forward = alignment.InverseTransformVector(jaw.position - head.position);
        forward.y = 0f;
        if (forward.sqrMagnitude > .00001f)
            alignment.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward));
    }

    static GameObject FindLegacyMerchant(StoryFlow story)
    {
        if (story.merchant != null) return story.merchant;
        foreach (var candidate in FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.gameObject.scene == story.gameObject.scene &&
                string.Equals(candidate.displayName, story.merchantName, StringComparison.OrdinalIgnoreCase)) return candidate.gameObject;
        return null;
    }

    static Vector3 HorizontalForward(Vector3 forward, Vector3 fallback)
    {
        forward.y = 0;
        if (forward.sqrMagnitude > .0001f) return forward;
        fallback.y = 0;
        return fallback.sqrMagnitude > .0001f ? fallback : Vector3.forward;
    }

    void LoadClips()
    {
        foreach (var clip in Resources.LoadAll<AnimationClip>(ModelPath))
        {
            string name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
            switch (name)
            {
                case "Idle": idle = clip; break;
                case "Greet": greet = clip; break;
                case "Talk": talk = clip; break;
                case "Trade": trade = clip; break;
            }
        }
    }

    void OnEnable()
    {
        TradingStall.SaleMade += OnSale;
        if (idle == null || idle.length <= 0f || animator == null) return;
        graph = PlayableGraph.Create("Rico Motion");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        idleTrack = AnimationClipPlayable.Create(graph, idle);
        graph.Connect(idleTrack, 0, mixer, 0);
        mixer.SetInputWeight(0, 1f);
        AnimationPlayableOutput.Create(graph, "Rico", animator).SetSourcePlayable(mixer);
        idleTime = 0;
        graph.Play();
        graph.Evaluate(0);
    }

    void OnDisable()
    {
        TradingStall.SaleMade -= OnSale;
        if (graph.IsValid()) graph.Destroy();
        gesture = null;
        wasNear = wasSpeaking = false;
    }

    void OnSale(int coins, int logs, int planks)
    {
        if (SellWindow.IsOpenFor(Stall)) PlayGesture(trade);
    }

    void LateUpdate()
    {
        if (visual == null) return;
        var player = PlayerMovement.Instance;
        Vector3 offset = player != null ? player.transform.position - transform.position : Vector3.one * 100f;
        bool near = Mathf.Abs(offset.y) < 2.5f && new Vector2(offset.x, offset.z).sqrMagnitude < GreetingRadius * GreetingRadius;
        Vector3 localOffset = transform.InverseTransformDirection(offset);
        bool inFront = near && localOffset.z > 0f;
        if (Speaker.TryGetConversationPartner(speaker, out var partner))
            localOffset = transform.InverseTransformDirection(partner.transform.position - transform.position);
        else if (!inFront)
            localOffset = Vector3.zero;
        float targetYaw = localOffset.z > 0f
            ? Mathf.Clamp(Mathf.Atan2(localOffset.x, localOffset.z) * Mathf.Rad2Deg, -MaxTurn, MaxTurn)
            : 0f;
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-6f * Time.deltaTime));
        visual.localRotation = Quaternion.Euler(0f, yaw, 0f);

        if (!graph.IsValid()) return;
        bool speaking = DialogueManager.CurrentSpeaker == speaker;
        if (speaking && !wasSpeaking) PlayGesture(talk);
        else if (inFront && !wasNear && Time.time >= greetAfter && gesture == null && !DialogueManager.IsBusy && !SellWindow.IsOpenFor(Stall))
        {
            greetAfter = Time.time + 12f;
            PlayGesture(greet);
        }
        wasNear = inFront;
        wasSpeaking = speaking;

        float dt = Mathf.Min(Time.deltaTime, .1f);
        idleTime = Mathf.Repeat(idleTime + dt, idle.length);
        idleTrack.SetTime(idleTime);
        if (gesture != null)
        {
            gestureTime += dt;
            if (gesture == talk && speaking) gestureTime = Mathf.Repeat(gestureTime, gesture.length);
            if (gestureTime >= gesture.length) StopGesture();
            else
            {
                gestureTrack.SetTime(gestureTime);
                float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(gestureTime, gesture.length - gestureTime) / .16f);
                mixer.SetInputWeight(0, 1f - weight);
                mixer.SetInputWeight(1, weight);
            }
        }
        graph.Evaluate(0);
    }

    void PlayGesture(AnimationClip clip)
    {
        if (clip == null || clip.length <= 0f || !graph.IsValid()) return;
        if (gesture != null) StopGesture();
        gesture = clip;
        gestureTime = 0f;
        gestureTrack = AnimationClipPlayable.Create(graph, clip);
        graph.Connect(gestureTrack, 0, mixer, 1);
    }

    void StopGesture()
    {
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        graph.Disconnect(mixer, 1);
        graph.DestroyPlayable(gestureTrack);
        gesture = null;
    }
}

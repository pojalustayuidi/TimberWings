using UnityEngine;

public partial class PlayerInteraction
{
    readonly struct LogTarget
    {
        internal readonly ChopPrototypeLog log;
        internal readonly int index;
        internal readonly bool cut;

        internal LogTarget(ChopPrototypeLog log, int index, bool cut)
        {
            this.log = log;
            this.index = index;
            this.cut = cut;
        }

        internal bool Valid => log != null && log.isActiveAndEnabled &&
            (cut ? log.CanCut(index) : log.CanCollect(index));
        internal bool Same(LogTarget other) => log == other.log && index == other.index && cut == other.cut;
        internal Vector3 Point(Vector3 actor) =>
            cut ? log.CutAnchor(index).position : log.SectionPoint(index, actor);
    }

    const float CollectDistance = 1.15f;
    const float CutApproachRange = 6f;
    const float PickInterval = .05f;
    readonly RaycastHit[] prototypeHits = new RaycastHit[48];
    CapsuleCollider logActorCapsule;

    LogTarget processing, aimedLog, chop03Log, focusedCut;
    ResourceItem aimedTree, chop03Tree;
    float nextPrototypePick, nextPickupScan;
    LogTarget nearestPickup;
    bool mouseOnFallenTrunk, emittingPrototypeFall, chop03PointerBlocked;
    bool chop03Hold;
    int pickupLabelWood = -1;
    bool pickupLabelEnglish;
    string pickupLabel;

    public bool IsProcessingPrototypeLog => IsChopping && processing.log != null;
    internal bool PrototypeReticleActive { get; private set; }
    internal static bool PrototypeFellingEvent => inst != null && inst.emittingPrototypeFall;

    bool PrototypeInputAllowed => !MobileInput.ModalOpen && !DialogueBusy() && !StoryInspection.Active &&
        !ArrivalIntroSequence.Active && !SaveSystem.IsLoading && !EndingSequence.Active &&
        !LeoCelebration.IsPlaying && !BuildWorkPose.MovementLocked && !BuildWorkPose.HasWork(movement) &&
        !BuildingController.AnyRestorePending && !CameraVibe.RevealActive && CameraVibe.RevealShown <= .01f &&
        !WaterSwim.Swimming && !CampSleep.Sleeping &&
        (movement == null || !movement.ControlLocked && !movement.IsDashing) &&
        Application.isFocused && Time.timeScale > 0f;

    bool PrototypeStanding(ResourceItem item) => ChopPrototypeLog.Supports(item);
    bool PrototypeHitDrivenFelling(ResourceItem item) => chop03Standing && PrototypeStanding(item);

    void PrototypeToggle()
    {
        if (!Input.GetKeyDown(KeyCode.F6) || !PrototypeInputAllowed || IsChopping) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DayNightDebug.OwnsPhaseKey(KeyCode.F6)) return;
#endif
        if (WalkTarget != null) movement?.CancelWalkTo(WalkTarget.transform);
        ResetPrototypeInput();
        ClearChopTargets();
        savedItem = null;
        clickArmed = false;
        ChopPrototypeLog.Toggle();
    }

    void FellPrototype(ResourceItem item, ChopPrototypeSave.Layout layout, int wood, int xp, bool wasGolden)
    {
        ResourceItem echo = inventory != null && inventory.FreeSlots >= GameBalance.SkillEchoBonusWood
            ? LumberSkills.RollEcho(item) : null;
        if (leoImpactFrame != Time.frameCount) ReadLeoImpact(item);
        var state = ChopPrototypeSave.Tree.Create(item.ChopSaveId, layout, ChopPrototypeLog.CutsFor(item.Size), wood, xp);
        item.FellPrototype(state);
        LumberSkills.TrackProcessing(state);
        emittingPrototypeFall = true;
        try { GameEvents.RaiseTreeChopped(); }
        finally { emittingPrototypeFall = false; }
        if (wasGolden) SfxManager.Play("coin", .9f, .9f);
        if (echo != null && ChopPrototypeLog.TryPlan(echo, transform, out var echoLayout))
        {
            echo.FellPrototype(ChopPrototypeSave.Tree.Create(echo.ChopSaveId, echoLayout, 0,
                GameBalance.SkillEchoBonusWood, 0));
            SfxManager.Play("woodCrack", .32f, .84f);
        }
        StopChop();
        ClearChopTargets();
        nextPrototypePick = 0f;
    }

    Collider ClosestPrototypeHit(Ray ray, float distance, out Vector3 point) =>
        ClosestPrototypeHit(ray, distance, out point, out _);

    Collider ClosestPrototypeHit(Ray ray, float distance, out Vector3 point, out Vector3 normal)
    {
        point = normal = default;
        float closest = float.PositiveInfinity;
        Collider first = null;
        int count = Physics.RaycastNonAlloc(ray, prototypeHits, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count >= prototypeHits.Length) return null;
        for (int i = 0; i < count; i++)
        {
            var hit = prototypeHits[i];
            var c = hit.collider;
            if (c == null || c.transform.IsChildOf(transform) || hit.distance >= closest) continue;
            closest = hit.distance;
            point = hit.point;
            normal = hit.normal;
            first = c;
        }
        return first;
    }

    float TreeApproachRange => Mathf.Min(walkRangeClick, OrbitCamera.FirstPersonActive ? 11f : 18f);

    void PickChop03Targets(bool overUi)
    {
        aimedTree = null;
        aimedLog = default;
        mouseOnFallenTrunk = false;
        var cam = Camera.main;
        if (cam == null || !MouseActive() || overUi) return;

        bool firstPerson = OrbitCamera.FirstPersonActive;
        Vector2 pointer = OrbitCamera.PointerPos;
        if (!firstPerson && (pointer.x < 0f || pointer.y < 0f || pointer.x > Screen.width || pointer.y > Screen.height))
            return;
        Ray ray = firstPerson ? cam.ViewportPointToRay(new Vector3(.5f, .5f)) : cam.ScreenPointToRay(pointer);
        Collider hit = ClosestPrototypeHit(ray, firstPerson ? 12f : 44f, out var point);
        if (hit != null && hit.GetComponentInParent<ChopPrototypeLog>() is ChopPrototypeLog log)
        {
            mouseOnFallenTrunk = true;
            if (!log.Ready || PlanarDistance(point, transform.position) > CutApproachRange) return;
            int cut = log.FindCut(point);
            if (cut >= 0) aimedLog = new LogTarget(log, cut, true);
            else
            {
                int section = log.FindSection(hit);
                if (log.CanCollect(section)) aimedLog = new LogTarget(log, section, false);
            }
            return;
        }

        if (!ChopPrototypeLog.Enabled) return;
        ResourceItem item = hit != null ? hit.GetComponentInParent<ResourceItem>() : null;
        // The solid ray has priority. The top-down camera can also select through the existing crown trigger.
        if (item == null && !firstPerson)
        {
            item = PickThroughCrown(ray, hit != null ? Vector3.Distance(ray.origin, point) : 44f);
        }
        if (PrototypeStanding(item) && PlanarDistance(item.transform.position, transform.position) <= TreeApproachRange)
            aimedTree = item;
    }

    ResourceItem PickThroughCrown(Ray ray, float distance)
    {
        ResourceItem best = null;
        int count = Physics.RaycastNonAlloc(ray, rayBuf, distance + .01f, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var hit = rayBuf[i];
            if (hit.collider == null || !hit.collider.isTrigger || hit.distance > distance) continue;
            var tree = hit.collider.GetComponentInParent<ResourceItem>();
            if (!PrototypeStanding(tree)) continue;
            distance = hit.distance;
            best = tree;
        }
        return best;
    }

    Vector3 TreePromptPoint(ResourceItem tree)
    {
        if (tree.TryGetStandingContact(out var point)) return point;
        Vector3 towardActor = Vector3.ProjectOnPlane(transform.position - tree.transform.position, Vector3.up).normalized;
        return tree.transform.position + towardActor * tree.TrunkRadius + Vector3.up * (tree.Size == TreeSize.Young ? .75f : 1.03f);
    }

    float LogFeetHeight => logActorCapsule != null ? logActorCapsule.bounds.min.y : transform.position.y;
    float ActorRadius => logActorCapsule != null
        ? Mathf.Max(logActorCapsule.bounds.extents.x, logActorCapsule.bounds.extents.z) : .25f;

    static float PlanarDistance(Vector3 a, Vector3 b) =>
        Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

    bool CanSeeLog(LogTarget target)
    {
        Vector3 eye = transform.position;
        eye.y = LogFeetHeight + 1.02f;
        Vector3 delta = target.Point(transform.position) - eye;
        float distance = delta.magnitude;
        var hit = ClosestPrototypeHit(new Ray(eye, delta / Mathf.Max(.001f, distance)),
            distance + .018f, out _);
        return hit == null || hit.GetComponentInParent<ChopPrototypeLog>() == target.log;
    }

    bool InCutStance(LogTarget target)
    {
        Vector3 point = target.Point(transform.position);
        Vector3 toward = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        float distance = toward.magnitude;
        float height = point.y - LogFeetHeight;
        float logRadius = target.log.Layout.radius;
        Vector3 axis = Vector3.ProjectOnPlane(target.log.transform.forward, Vector3.up).normalized;
        float reach = Mathf.Clamp(logRadius * .92f + ActorRadius + .32f, .64f, 1.13f);
        return distance >= Mathf.Max(.28f, logRadius + .055f) && distance <= reach &&
            height >= -.02f && height <= 1.06f &&
            Mathf.Abs(Vector3.Dot(toward.normalized, axis)) < .70f && CanSeeLog(target);
    }

    bool AtPickup(LogTarget target) => target.Valid && !target.cut &&
        PlanarDistance(target.Point(transform.position), transform.position) <= CollectDistance &&
        Mathf.Abs(target.Point(transform.position).y - LogFeetHeight) <= 1.1f && CanSeeLog(target);

    LogTarget NearestPrototypePickup()
    {
        LogTarget best = default;
        float reach = CollectDistance;
        foreach (var log in ChopPrototypeLog.Active)
        {
            if (log == null || !log.Ready ||
                PlanarDistance(log.transform.position, transform.position) > reach + log.Layout.length * .65f) continue;
            for (int i = 0; i < log.SectionCount; i++)
            {
                if (!log.CanCollect(i)) continue;
                var target = new LogTarget(log, i, false);
                Vector3 point = target.Point(transform.position);
                float distance = PlanarDistance(point, transform.position);
                if (distance > reach || Mathf.Abs(point.y - LogFeetHeight) > 1.1f || !CanSeeLog(target)) continue;
                reach = distance;
                best = target;
            }
        }
        return best;
    }

    void SetFocusedCut(LogTarget cut)
    {
        if (focusedCut.Same(cut)) return;
        if (focusedCut.log != null) focusedCut.log.FocusCut(-1);
        focusedCut = cut;
        if (cut.Valid) cut.log.FocusCut(cut.index);
    }

    void ShowPickup(LogTarget target)
    {
        int amount = target.log.WoodIn(target.index);
        if (amount != pickupLabelWood || pickupLabelEnglish != GameLanguage.English)
        {
            pickupLabelWood = amount;
            pickupLabelEnglish = GameLanguage.English;
            pickupLabel = ChopPrototypeLog.Text("E — Забрать древесину", "E — Pick up wood") + " · " + amount;
        }
        string label = inventory != null && inventory.FreeSlots == 0
            ? ChopPrototypeLog.Text("Рюкзак полон", "Backpack full") : pickupLabel;
        Chop02Feedback.Show(target.Point(transform.position), label);
    }

    void ShowChop03Target(LogTarget pickup)
    {
        var cut = aimedLog.Valid && aimedLog.cut ? aimedLog : default;
        SetFocusedCut(cut);
        if (cut.Valid)
        {
            string label = InCutStance(cut) ? ChopPrototypeLog.Text("ЛКМ — Разделать", "LMB — Cut")
                : ChopPrototypeLog.Text("ЛКМ — Подойти к разрезу", "LMB — Approach the cut");
            Chop02Feedback.Show(cut.Point(transform.position), label, cut.log.CutProgress(cut.index));
        }
        else if (pickup.Valid) ShowPickup(pickup);
        else if (aimedTree != null)
        {
            string label = InTreeStance(aimedTree) ? ChopPrototypeLog.Text("ЛКМ — Рубить", "LMB — Chop")
                : ChopPrototypeLog.Text("ЛКМ — Подойти к дереву", "LMB — Approach the tree");
            float damage = aimedTree.StandingDamage;
            Chop02Feedback.Show(TreePromptPoint(aimedTree), label, damage > 0f ? damage : -1f);
        }
        else Chop02Feedback.Hide();
    }

    internal bool TryReadPrototypeContact(out Vector3 point, out Vector3 sectionAxis)
    {
        point = sectionAxis = default;
        if (!IsProcessingPrototypeLog) return false;
        point = processing.log.CutContact(processing.index, transform.position);
        sectionAxis = processing.log.transform.forward;
        return true;
    }

    void StopPrototypeWork()
    {
        if (!IsChop03Working && !chop03Standing && ReferenceEquals(processing.log, null)) return;
        StopChop();
        processing = default;
        chop03Standing = false;
        ClearChopTargets();
    }

    void ClearChopTargets()
    {
        CurrentTarget = null;
        TargetItem = lastItem = null;
        lastTarget = null;
        findTimer = 0f;
    }

    bool Chop03ContactInView(Vector3 contact)
    {
        if (!OrbitCamera.FirstPersonActive) return true;
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 view = cam.WorldToViewportPoint(contact);
        return view.z > .12f && view.x > -.08f && view.x < 1.08f && view.y > -.25f && view.y < 1.18f;
    }

    void ResetPrototypeInput()
    {
        CancelChop03Approach();
        chop03Hold = false;
        chop03Tree = aimedTree = HoverItem = null;
        chop03Log = aimedLog = nearestPickup = default;
        nextPrototypePick = nextPickupScan = 0f;
        PrototypeReticleActive = mouseOnFallenTrunk = chop03PointerBlocked = false;
        StopPrototypeWork();
        SetFocusedCut(default);
        Chop02Feedback.Hide(true);
    }

    bool TickChop03()
    {
        bool enabled = ChopPrototypeLog.Enabled;
        if (!enabled && ChopPrototypeLog.Active.Count == 0 && !IsChop03Working && !chop03Walking)
        {
            if (PrototypeReticleActive || mouseOnFallenTrunk || !ReferenceEquals(nearestPickup.log, null))
                ResetPrototypeInput();
            else Chop02Feedback.Hide();
            return false;
        }

        if (!PrototypeInputAllowed || !ChoppingEnabled)
        {
            ResetPrototypeInput();
            if (enabled)
            {
                StopChop();
                ClearChopTargets();
            }
            return enabled;
        }

        bool click = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        if (!held) chop03Hold = false;
        bool moving = Mathf.Abs(MobileInput.Horizontal) > .01f || Mathf.Abs(MobileInput.Vertical) > .01f;
        bool action = Input.GetKeyDown(KeyCode.E);
        if (click || Time.unscaledTime >= nextPrototypePick)
        {
            chop03PointerBlocked = OverUiButton(OrbitCamera.PointerPos);
            nextPrototypePick = Time.unscaledTime + PickInterval;
            PickChop03Targets(chop03PointerBlocked);
        }
        if (enabled) HoverItem = aimedTree;
        PrototypeReticleActive = aimedLog.Valid || aimedTree != null;
        if (moving || InteractionHub.ActionConsumedThisFrame)
        {
            CancelChop03Approach();
            chop03Hold = false;
            chop03Tree = null;
            chop03Log = default;
            StopPrototypeWork();
        }
        else if (click && !chop03PointerBlocked && (enabled || aimedLog.Valid && aimedLog.cut))
        {
            CancelChop03Approach();
            StopPrototypeWork();
            StopChop();
            ClearChopTargets();
            chop03Tree = aimedTree;
            chop03Log = aimedLog.Valid && aimedLog.cut ? aimedLog : default;
            chop03Hold = chop03Tree != null || chop03Log.Valid;
            if (chop03Hold) TryStartChop03Approach();
        }

        if (TickChop03Approach())
        {
            SetFocusedCut(chop03Log);
            Chop02Feedback.Hide();
            return true;
        }

        if (chop03Hold && !chop03PointerBlocked && MouseActive())
        {
            if (chop03Tree != null && chop03Tree.isActiveAndEnabled && InTreeStance(chop03Tree) &&
                Chop03ContactInView(TreePromptPoint(chop03Tree)))
            {
                SetFocusedCut(default);
                TickChop03Standing(chop03Tree);
                return true;
            }
            if (chop03Log.Valid && InCutStance(chop03Log) &&
                Chop03ContactInView(chop03Log.Point(transform.position)))
            {
                SetFocusedCut(chop03Log);
                TickGroundSwing(chop03Log);
                return true;
            }
        }
        StopPrototypeWork();

        LogTarget pickup = default;
        if (!InteractionHub.OwnsInput && !IsChopping && ChopPrototypeLog.Active.Count > 0)
        {
            if (action || Time.unscaledTime >= nextPickupScan)
            {
                nextPickupScan = Time.unscaledTime + PickInterval;
                nearestPickup = NearestPrototypePickup();
            }
            pickup = aimedLog.Valid && !aimedLog.cut && AtPickup(aimedLog) ? aimedLog : nearestPickup;
            if (pickup.Valid && !AtPickup(pickup)) pickup = default;
            if (action && pickup.Valid)
            {
                if (inventory != null && inventory.FreeSlots > 0)
                {
                    pickup.log.Collect(pickup.index, inventory, stats, pickup.Point(transform.position));
                    nextPickupScan = 0f;
                }
                else backpack?.Flash();
                ShowChop03Target(pickup.Valid ? pickup : default);
                return true;
            }
        }

        if (enabled)
        {
            StopChop();
            ClearChopTargets();
        }
        ShowChop03Target(pickup);
        return enabled;
    }
}

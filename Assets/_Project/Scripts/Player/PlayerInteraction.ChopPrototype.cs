using UnityEngine;

// CHOP-02: replaces only the CHOP-01 interaction partial. The tree, reward,
// inventory, save and geometry implementations remain owned by CHOP-01.
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

        internal bool Valid => log != null && log.isActiveAndEnabled && log.Ready &&
            (cut ? log.CanCut(index) : log.Harvestable && log.WoodIn(index) > 0);
        internal bool Same(LogTarget other) => log == other.log && index == other.index && cut == other.cut;
        internal Vector3 Point(Vector3 actor) => cut ? log.CutAnchor(index).position : log.SectionPoint(index, actor);
    }

    const float GroundSwingWindup = .42f;
    const float CollectDistance = .94f;
    readonly RaycastHit[] prototypeHits = new RaycastHit[48];
    LogTarget processing, mouseCut, keyboardCut, presented;
    WorldTag logTag;
    string logTagText;
    bool mouseOnFallenTrunk, capturedLogKey;
    CapsuleCollider logActorCapsule;
    bool emittingPrototypeFall;
    Vector3 chop02RayPoint;
    bool chop02RayPointValid;

    public bool IsProcessingPrototypeLog => IsChopping && processing.log != null;
    internal bool PrototypeReticleActive { get; private set; }
    internal static bool PrototypeFellingEvent => inst != null && inst.emittingPrototypeFall;

    bool PrototypeInputAllowed => !MobileInput.ModalOpen && !DialogueBusy() && !StoryInspection.Active &&
        !ArrivalIntroSequence.Active && !SaveSystem.IsLoading && !EndingSequence.Active &&
        !LeoCelebration.IsPlaying && !BuildWorkPose.MovementLocked && !WaterSwim.Swimming &&
        (movement == null || !movement.ControlLocked && !movement.IsDashing) &&
        Application.isFocused && Time.timeScale > 0f;

    bool PrototypeStanding(ResourceItem item) => ChopPrototypeLog.Supports(item);
    bool PrototypeHitDrivenFelling(ResourceItem item) => PrototypeStanding(item) && item.Size != TreeSize.Young;

    void PrototypeToggle()
    {
        if (!Input.GetKeyDown(KeyCode.F6) || !PrototypeInputAllowed || IsChopping) return;
        ChopPrototypeLog.Toggle();
        savedItem = null;
        clickArmed = false;
        Chop02ResetWork();
    }

    // Longer standing-tree setup: identical across all four log-processing modes.
    // Skills still use the existing CHOP-01 GatherTime/PowerMul flow.
    int PrototypeFellingHits(ResourceItem item) => item.Size == TreeSize.Old ? 10 : 7;

    void TickPrototypeFelling()
    {
        if (TargetItem == null) return;
        int need = PrototypeFellingHits(TargetItem);
        if (Time.time >= nextHitAt)
        {
            nextHitAt = Time.time + Mathf.Max(.15f, hitInterval);
            holdTime += GameBalance.ChopTime * TargetItem.ChopMul / need;
            RaiseChopContact(GetTreeHits(TargetItem), 1f);
        }
        Progress = Mathf.Clamp01(holdTime / Mathf.Max(.1f, GatherTime));
        hitsDone = Mathf.FloorToInt(Progress * need + .0001f);
        if (progressUI != null) progressUI.Show(Progress, hitsDone, need);
        if (Progress >= .9999f) { CompleteChop(); return; }
        TickChopBonus();
    }

    void FellPrototype(ResourceItem item, ChopPrototypeSave.Layout layout, int wood, int xp, bool wasGolden)
    {
        ResourceItem echo = inventory != null && inventory.FreeSlots >= GameBalance.SkillEchoBonusWood
            ? LumberSkills.RollEcho(item) : null;
        if (leoImpactFrame != Time.frameCount) ReadLeoImpact(item);
        var state = ChopPrototypeSave.Tree.Create(item.ChopSaveId, layout, ChopPrototypeLog.CutsFor(item.Size), wood, xp);
        item.FellPrototype(state);
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
        savedItem = null;
        StopChop();
        clickArmed = false;
        FindTarget(true);
    }

    Collider ClosestPrototypeHit(Ray ray, float distance, out Vector3 point)
    {
        point = default;
        float closest = float.PositiveInfinity;
        Collider first = null;
        int count = Physics.RaycastNonAlloc(ray, prototypeHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = prototypeHits[i];
            var c = hit.collider;
            if (c == null || c.transform.IsChildOf(transform) || hit.distance >= closest) continue;
            closest = hit.distance;
            point = hit.point;
            first = c;
        }
        return first;
    }

    LogTarget PickPrototypeLog()
    {
        mouseOnFallenTrunk = false;
        chop02RayPointValid = false;
        var cam = Camera.main;
        if (cam == null || !MouseActive() || OverUiButton(OrbitCamera.PointerPos)) return default;
        bool firstPerson = OrbitCamera.FirstPersonActive;
        Ray ray = firstPerson ? cam.ViewportPointToRay(new Vector3(.5f, .5f)) :
            cam.ScreenPointToRay(OrbitCamera.PointerPos);
        var hit = ClosestPrototypeHit(ray, firstPerson ? 6f : 40f, out Vector3 point);
        if (hit == null) return default;
        var log = hit.GetComponentInParent<ChopPrototypeLog>();
        if (log == null || !log.Ready) return default;
        mouseOnFallenTrunk = true;
        if (Vector3.Distance(transform.position, point) > 2.45f) return default;
        int index = log.FindCut(point);
        if (index >= 0)
        {
            chop02RayPoint = point;
            chop02RayPointValid = true;
            return new LogTarget(log, index, true);
        }
        index = log.FindSection(hit);
        return index >= 0 && log.Harvestable && log.WoodIn(index) > 0
            ? new LogTarget(log, index, false) : default;
    }

    bool CanSeeLog(LogTarget target)
    {
        Vector3 eye = transform.position;
        eye.y = LogFeetHeight + 1.05f;
        Vector3 delta = target.Point(transform.position) - eye;
        float distance = delta.magnitude;
        var hit = ClosestPrototypeHit(new Ray(eye, delta / Mathf.Max(.001f, distance)),
            distance + .018f, out _);
        return hit == null || hit.GetComponentInParent<ChopPrototypeLog>() == target.log;
    }

    float LogDistance(LogTarget target) =>
        Vector3.ProjectOnPlane(target.Point(transform.position) - transform.position, Vector3.up).magnitude;

    float LogFeetHeight => logActorCapsule != null ? logActorCapsule.bounds.min.y : transform.position.y;

    bool InCutStance(LogTarget target)
    {
        Vector3 point = target.Point(transform.position);
        Vector3 toCut = point - transform.position;
        float height = point.y - LogFeetHeight;
        Vector3 toward = Vector3.ProjectOnPlane(toCut, Vector3.up);
        float distance = toward.magnitude;
        float bodyRadius = logActorCapsule != null ? logActorCapsule.bounds.extents.x : .25f;
        float radius = target.log.Layout.radius;
        Vector3 axis = Vector3.ProjectOnPlane(target.log.transform.forward, Vector3.up).normalized;
        Transform log = target.log.transform;
        float joint = target.log.CutAnchor(target.index).localPosition.z;
        float boxTop = log.TransformPoint(new Vector3(0f, radius * .795f, joint)).y;
        float above = logActorCapsule != null
            ? Mathf.Max(0f, logActorCapsule.bounds.min.y + bodyRadius - boxTop) : 0f;
        float bodyWidthAtLog = Mathf.Sqrt(Mathf.Max(0f, bodyRadius * bodyRadius - above * above));
        float reach = Mathf.Clamp(bodyWidthAtLog + radius * .86f + .12f, .52f, 1.02f);
        return distance >= Mathf.Max(.32f, radius + .10f) && distance <= reach &&
            height >= .07f && height <= .92f &&
            Mathf.Abs(Vector3.Dot(toward.normalized, axis)) < .69f && CanSeeLog(target);
    }

    LogTarget NearestPrototypeTarget(bool cut, float reach)
    {
        LogTarget best = default;
        foreach (var log in ChopPrototypeLog.Active)
        {
            if (log == null || !log.Ready || (!cut && !log.Harvestable)) continue;
            int count = cut ? log.CutCount : log.SectionCount;
            for (int i = 0; i < count; i++)
            {
                if (cut ? !log.CanCut(i) : log.WoodIn(i) == 0) continue;
                var target = new LogTarget(log, i, cut);
                float distance = LogDistance(target);
                if (distance >= reach || Mathf.Abs(target.Point(transform.position).y - LogFeetHeight) > 1f ||
                    (cut && !InCutStance(target)) || (!cut && !CanSeeLog(target))) continue;
                reach = distance;
                best = target;
            }
        }
        return best;
    }

    void ShowPrototypeTarget(LogTarget target)
    {
        if (!target.Same(presented))
        {
            if (presented.log != null) presented.log.FocusCut(-1);
            presented = target;
            logTagText = null;
        }
        if (!target.Valid)
        {
            if (logTag != null) logTag.visible = false;
            return;
        }
        if (target.cut) target.log.FocusCut(target.index);
        if (logTag == null)
        {
            var holder = new GameObject("Log interaction hint");
            holder.transform.SetParent(transform, false);
            logTag = WorldTag.Add(holder, false);
            logTag.SetFontSize(22f);
            logTag.offset = Vector3.up * .14f;
        }
        string label;
        if (target.cut)
        {
            bool firstPerson = OrbitCamera.FirstPersonActive;
            label = !InCutStance(target)
                ? ChopPrototypeLog.Text("Встань сбоку у зарубки", "Stand beside the notch")
                : Chop02CutPrompt(firstPerson);
            float progress = target.log.CutProgress(target.index);
            if (progress > 0f) label += " · " + Mathf.RoundToInt(progress * 100f) + "%";
        }
        else
        {
            label = inventory != null && inventory.FreeSlots == 0
                ? ChopPrototypeLog.Text("Рюкзак полон", "Backpack full")
                : ChopPrototypeLog.Text("Подойди за брёвнами", "Step closer to collect");
            label += " · " + target.log.WoodIn(target.index);
        }
        if (label != logTagText)
        {
            logTag.SetText(label);
            logTagText = label;
        }
        logTag.transform.position = target.Point(transform.position);
        logTag.visible = true;
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
        Chop02ResetWork();
        if (ReferenceEquals(processing.log, null)) return;
        if (processing.log != null) processing.log.FocusCut(-1);
        processing = default;
        StopChop();
        CurrentTarget = null;
        TargetItem = null;
        findTimer = 0f;
        lastTarget = null;
        lastItem = null;
    }

    void ResetPrototypeInput()
    {
        mouseCut = keyboardCut = default;
        PrototypeReticleActive = mouseOnFallenTrunk = false;
        StopPrototypeWork();
        ShowPrototypeTarget(default);
        Chop02Feedback.Hide();
    }

    bool TickPrototypeLog()
    {
        if (!MobileInput.ActionHeld) capturedLogKey = false;
        if (PrototypeInputAllowed && Input.GetKeyDown(KeyCode.F7) && (!IsChopping || IsProcessingPrototypeLog))
        {
            StopPrototypeWork();
            mouseCut = keyboardCut = default;
            Chop02NextMode();
        }
        if (!PrototypeInputAllowed || !ChoppingEnabled || ChopPrototypeLog.Active.Count == 0)
        {
            ResetPrototypeInput();
            return false;
        }

        LogTarget aim = PickPrototypeLog();
        PrototypeReticleActive = aim.Valid && (!aim.cut || InCutStance(aim));
        bool releasedMouse = Input.GetMouseButtonUp(0);
        bool releasedKey = !MobileInput.ActionHeld && keyboardCut.Valid;
        bool moving = Mathf.Abs(MobileInput.Horizontal) > .14f || Mathf.Abs(MobileInput.Vertical) > .14f;
        // In B a short click still commits exactly one scheduled downward strike.
        // Sample its accuracy before clearing the held mouse target.
        if (chop02Mode == Chop02Mode.PreciseNotch && releasedMouse &&
            mouseCut.Valid && processing.Same(mouseCut) && mouseCut.Same(aim) &&
            !moving && InCutStance(mouseCut) && !chop02HitDuringHold)
        {
            chop02TapPrecision = Chop02Precision(mouseCut);
            chop02TapTarget = mouseCut;
            chop02TapQueued = true;
        }
        // D is resolved on release *before* CHOP-01 clears its held target.
        if (Chop02UsesCharge && (releasedMouse || releasedKey))
        {
            bool mouseStillAimed = mouseCut.Valid && mouseCut.Same(processing) && mouseCut.Same(aim);
            bool keyStillValid = keyboardCut.Valid && keyboardCut.Same(processing);
            bool same = processing.Valid && (mouseStillAimed || keyStillValid);
            if (same && !moving && InCutStance(processing)) Chop02ReleaseCharge(processing);
            else Chop02ResetWork();
        }
        if (Input.GetMouseButtonDown(0))
        {
            Chop02ClearTap();
            mouseCut = aim.Valid && aim.cut && InCutStance(aim) ? aim : default;
        }
        if (!Input.GetMouseButton(0) || !mouseCut.Same(aim)) mouseCut = default;

        // Precision mode B specifically requires the cursor/crosshair to hit the notch.
        // The nearest-target E shortcut must not bypass this condition.
        bool keyboardAvailable = !OrbitCamera.FirstPersonActive && !InteractionHub.OwnsInput &&
            !Chop02PrecisionRequiresAim;
        if (keyboardAvailable && MobileInput.ActionDown && (!IsChopping || IsProcessingPrototypeLog))
        {
            keyboardCut = NearestPrototypeTarget(true, 1.06f);
            capturedLogKey = keyboardCut.Valid;
        }
        if (!keyboardAvailable || !MobileInput.ActionHeld) keyboardCut = default;
        LogTarget wanted = mouseCut.Valid ? mouseCut :
            keyboardCut.Valid ? keyboardCut : chop02TapQueued ? chop02TapTarget :
            chop02ChargePending ? chop02PendingChargeTarget : default;
        if (wanted.Valid && !moving && InCutStance(wanted))
        {
            TickGroundSwing(wanted);
            ShowPrototypeTarget(wanted.Valid ? wanted : default);
            Chop02Present(wanted);
            return true;
        }
        if (moving || !IsChopping && processing.log != null) mouseCut = keyboardCut = default;
        StopPrototypeWork();
        LogTarget pickup = NearestPrototypeTarget(false, CollectDistance);
        if (!IsChopping && pickup.Valid && inventory != null && inventory.FreeSlots > 0)
        {
            if (pickup.log.Collect(pickup.index, inventory, stats, pickup.Point(transform.position)) > 0)
                pickup = NearestPrototypeTarget(false, CollectDistance);
        }
        ShowPrototypeTarget(aim.Valid ? aim : pickup);
        Chop02Present(aim.Valid && aim.cut ? aim : default);
        if (mouseOnFallenTrunk && Input.GetMouseButtonDown(0))
        {
            clickArmed = false;
            HoverItem = null;
        }
        return false;
    }

    void TickGroundSwing(LogTarget wanted)
    {
        if (!IsChopping || !processing.Same(wanted))
        {
            StopChop();
            movement?.CancelWalk();
            WalkTarget = leoWalkArrival = leoApproachAttempt = null;
            leoEnterRhythmOnArrival = false;
            clickArmed = false;
            HoverItem = TargetItem = lastItem = null;
            CurrentTarget = wanted.log.CutAnchor(wanted.index);
            lastTarget = null;
            processing = wanted;
            IsChopping = true;
            waitingForChopAxe = true;
            Progress = wanted.log.CutProgress(wanted.index);
            Chop02BeginCut(wanted);
        }
        if (!LeoAxeReady) return;
        if (waitingForChopAxe)
        {
            waitingForChopAxe = false;
            nextHitAt = Time.time + GroundSwingWindup;
            Chop02Ready();
        }
        Chop02TickSwing(wanted);
    }
}

using UnityEngine;

public sealed partial class LeoModelPresentation
{
    // Три удара с живым ритмом: короткий, средний, акцентированный («тук-тук-ТУК»).
    // Акцент мягкий: не блокируем игровой таймер и не привязываем физику к анимации.
    const float WorkContact = .53f;
    static readonly float[] WorkCycles = { .64f, .70f, .79f };
    float CurrentWorkCycle { get { return WorkCycles[workBeatIndex % WorkCycles.Length]; } }
    float CurrentWorkPhase { get { return Mathf.Clamp01(workStrokeTime / CurrentWorkCycle); } }
    static readonly Vector3 HammerGrip = new Vector3(0f, -.039f, 0f);
    static readonly Vector3 HammerFace = new Vector3(0f, .14448f, .10622f);

    float workStrokeTime, workWeight;
    int workBeatIndex;
    bool workHitSent;
    bool workRequested;
    bool workStrikePending;   // в этом кадре молоток коснулся здания: событие уйдёт, когда молоток встанет в позу удара

    void ResetWorkPose()
    {
        workStrokeTime = workWeight = 0f;
        workBeatIndex = 0;
        workHitSent = false;
        workRequested = false;
        workStrikePending = false;
    }

    void ReadWorkPose(float speed, float dt, bool allowed, bool chopping)
    {
        bool atWork = player != null && BuildWorkPose.TryGetFocus(player, out _);
        bool ready = atWork && BuildWorkPose.IsReady && !player.AutoWalking;
        // Микродвижение Rigidbody/поворот модели не должны переоткрывать позу удара.
        // Именно это сбивало цикл молотка и заставляло Лео дёргаться.
        bool moving = ready ? speed > 1.4f :
            speed > .26f || Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f;
        bool free = allowed && !chopping && !MobileInput.ModalOpen && player != null && !player.IsDashing;
        workRequested = free && ready && !moving && rig.hammerRoot != null && rig.handR != null && rig.HammerHolster != null;
        bool works = workRequested && holster >= .99f;
        workWeight = chopping ? 0f : Mathf.MoveTowards(workWeight, works ? 1f : 0f, dt * (works ? 7f : 9f));
        if (works)
        {
            workStrokeTime += dt;
            float cycle = CurrentWorkCycle;
            bool heavy = workBeatIndex % WorkCycles.Length == WorkCycles.Length - 1;
            if (!workHitSent && workStrokeTime >= cycle * WorkContact && workWeight > .85f)
            {
                // Sound is emitted in PlaceWorkHammer after IK has actually placed
                // the hammer face, in the same frame as BuildStrikes.Struck.
                workStrikePending = true;
                workHitSent = true;
            }
            if (workStrokeTime >= cycle)
            {
                workStrokeTime = Mathf.Repeat(workStrokeTime, cycle);
                workBeatIndex++;
                workHitSent = false;
            }
        }
        else if (workWeight <= 0f)
        {
            workStrokeTime = 0f;
            workBeatIndex = 0;
            workHitSent = false;
            workStrikePending = false; // не воспроизводим прошлый контакт после повторного входа в стройку
        }
    }

    void ApplyWorkBody()
    {
        if (workWeight <= .001f) return;
        float phase = CurrentWorkPhase;
        float accent = workBeatIndex % WorkCycles.Length == WorkCycles.Length - 1 ? 1.22f : 1f;
        float tap = Mathf.Max(0f, 1f - Mathf.Abs(phase - WorkContact) / .17f) * workWeight * accent;
        if (rig.hips != null)
            rig.hips.localPosition += new Vector3(-.015f * workWeight, -.043f * workWeight - .017f * tap, .044f * workWeight);
        rig.Rotate(rig.spine, new Vector3(6f * workWeight + tap * 3f, -3f * workWeight, 0f));
        rig.Rotate(rig.chest, new Vector3(3f * tap, 3.5f * workWeight, .6f * tap));
        rig.Rotate(rig.head, new Vector3((3f + 5f * tap) * workWeight, 0f, 0f));
        rig.Rotate(rig.upperArmL, new Vector3(-18f * workWeight, 0f, 0f));
        rig.Rotate(rig.elbowL, new Vector3(-24f * workWeight, 0f, 0f));
    }

    void PlaceWorkHammer()
    {
        if (rig.hammerRoot == null || rig.handR == null || rig.HammerHolster == null) return;
        // Раньше корпус вообще не анимировался: ApplyWorkBody существовал,
        // но не вызывался ни из одного LateUpdate/PlaceWorkHammer.
        // Сначала слегка поворачиваем плечи и корпус, затем решаем IK кисти.
        ApplyWorkBody();
        float phase = CurrentWorkPhase;
        bool heavy = workBeatIndex % WorkCycles.Length == WorkCycles.Length - 1;
        float raise = LeoMotionProfile.Smooth(phase / .36f);
        float strike = LeoMotionProfile.Smooth((phase - .36f) / (WorkContact - .36f));
        float recoil = LeoMotionProfile.Smooth((phase - WorkContact) / (1f - WorkContact));
        Vector3 ready = new Vector3(.24f, .998f, .24f);
        Vector3 high = new Vector3(.31f, heavy ? 1.37f : 1.27f, .21f);
        Vector3 tip = new Vector3(.12f, .895f, .497f);
        if (BuildWorkPose.TryGetHit(player, out Vector3 hitWorld))
        {
            // молоток целится в точку на стене здания, а не в воздух перед героем (в пределах длины руки)
            Vector3 l = transform.InverseTransformPoint(hitWorld);
            tip = new Vector3(Mathf.Clamp(l.x, -.12f, .32f), Mathf.Clamp(l.y, .62f, 1.12f), Mathf.Clamp(l.z, .40f, .80f));
        }
        Vector3 angles = Vector3.Lerp(new Vector3(30f, -3f, -18f), new Vector3(-28f, 15f, -39f), raise);
        Vector3 hitAngles = new Vector3(64f, -8f, -9f);
        Vector3 hit = tip - Quaternion.Euler(hitAngles) * (HammerFace - HammerGrip);
        angles = Vector3.Lerp(Vector3.Lerp(angles, hitAngles, strike), new Vector3(30f, -3f, -18f), recoil);
        Vector3 grip = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(ready, high, raise), hit, strike), ready, recoil);

        Quaternion tool = transform.rotation * Quaternion.Euler(angles);
        Quaternion wrist = LeoAxePose.RightWrist(tool);
        Vector3 pole = transform.TransformPoint(new Vector3(.69f, 1.00f, -.06f));
        LeoRig.SolveTwoBone(rig.upperArmR, rig.elbowR, rig.handR,
            LeoAxePose.WristGoal(rig.handR, transform.TransformPoint(grip), wrist), pole, wrist, workWeight);

        rig.PlaceAxe(holster);
        UpdateChopTrail(0f);

        // The small hammer shares the across-palm grip. Its own pocket
        // socket is the start and end of the draw.
        Quaternion held = LeoAxePose.ToolRotation(rig.handR);
        Vector3 shaft = Vector3.Scale(HammerGrip, rig.hammerRoot.lossyScale);
        Vector3 origin = LeoAxePose.Grip(rig.handR) - held * shaft;
        float draw = LeoMotionProfile.Smooth(workWeight);
        rig.hammerRoot.SetPositionAndRotation(Vector3.Lerp(rig.HammerHolster.position, origin, draw),
            Quaternion.Slerp(rig.HammerHolster.rotation, held, draw));

        // PlaceAxe opened the hand at the belt. Close it around the hammer during the draw.
        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(-42f * holster * draw, 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform joint in rig.fingerTipsR)
                rig.Rotate(joint, new Vector3(-35f * holster * draw, 0f, 0f));

        // кадр контакта: молоток уже стоит в позе удара, берём настоящую точку бойка
        if (workStrikePending)
        {
            workStrikePending = false;
            bool heavyImpact = workBeatIndex % WorkCycles.Length == WorkCycles.Length - 1;
            SfxManager.Play("build", heavyImpact ? .62f : .43f,
                heavyImpact ? .92f : (workBeatIndex % 2 == 0 ? 1.09f : 1.01f));
            Vector3 face = rig.hammerStrike != null ? rig.hammerStrike.position : rig.hammerRoot.TransformPoint(HammerFace);
            BuildWorkPose.NotifyStrike(player, face);
        }
    }
}

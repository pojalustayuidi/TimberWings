using UnityEngine;

// Стройка молотком для нового контроллера. Раньше молоток умел только старый LeoModelPresentation:
// установщик отключает его, и у Лео при стройке не было ни молотка, ни ударов (BuildWorkPose включал
// молоток только для старого компонента), а топор так и торчал из руки. Логика здесь та же, что в
// LeoModelPresentation.Life.cs: топор уходит на пояс тем же путём, что в радости, затем молоток
// достаётся из кармана и бьёт по стене. Каждый удар уходит в BuildWorkPose.NotifyStrike.
public sealed partial class LeoAnimationController
{
    const float WorkContact = .53f;
    static readonly float[] WorkCycles = { .64f, .70f, .79f };
    static readonly Vector3 HammerGrip = new Vector3(0f, -.039f, 0f);
    static readonly Vector3 HammerFace = new Vector3(0f, .14448f, .10622f);

    [Tooltip("Раз в секунду пишет в консоль, почему Лео бьёт или не бьёт молотком. Выключить, когда всё работает.")]
    public bool debugWork = false;

    float workWeight, workStrokeTime, workStowWait, nextWorkLog;
    int workBeatIndex;
    bool workHitSent, workRequested, workStrikePending;

    float CurrentWorkCycle => WorkCycles[workBeatIndex % WorkCycles.Length];
    float CurrentWorkPhase => Mathf.Clamp01(workStrokeTime / CurrentWorkCycle);
    bool WorkHeavy => workBeatIndex % WorkCycles.Length == WorkCycles.Length - 1;

    void ResetWork()
    {
        workWeight = workStrokeTime = workStowWait = 0f;
        workBeatIndex = 0;
        workHitSent = workRequested = workStrikePending = false;
    }

    // atWork: стройка уже началась (в том числе подход к стене). allowed: ничто не мешает работать.
    void ReadWork(float speed, float dt, bool atWork, bool allowed, bool chopping)
    {
        bool ready = atWork && BuildWorkPose.IsReady && player != null && !player.AutoWalking;
        // Микродвижение Rigidbody/поворот модели не должны переоткрывать позу удара.
        bool moving = ready ? speed > 1.4f :
            speed > .26f || Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f;
        bool free = allowed && !chopping && !MobileInput.ModalOpen && player != null && !player.IsDashing;
        workRequested = free && ready && !moving && rig.hammerRoot != null && rig.handR != null && rig.HammerHolster != null;

        // Молоток начинает работать, когда топор лёг на пояс. Если уборка по какой-то причине зависла,
        // через полсекунды кладём топор на пояс принудительно, чтобы стройка не встала.
        if (workRequested && holster < .99f) workStowWait += dt; else if (!workRequested) workStowWait = 0f;
        if (workStowWait >= .5f) holster = 1f;
        bool works = workRequested && holster >= .99f;

        if (debugWork && atWork && Time.time >= nextWorkLog)
        {
            nextWorkLog = Time.time + 1f;
            Debug.Log("[Leo work/controller] allowed=" + allowed + " chopping=" + chopping + " modal=" + MobileInput.ModalOpen +
                " ready=" + ready + " moving=" + moving + " speed=" + speed.ToString("F2") +
                " hammer=" + (rig.hammerRoot != null) + " hammerHolster=" + (rig.HammerHolster != null) +
                " holster=" + holster.ToString("F2") + " requested=" + workRequested + " works=" + works +
                " weight=" + workWeight.ToString("F2") + " strikes=" + BuildWorkPose.StrikeCount, this);
        }

        workWeight = chopping ? 0f : Mathf.MoveTowards(workWeight, works ? 1f : 0f, dt * (works ? 7f : 9f));
        if (works)
        {
            workStrokeTime += dt;
            float cycle = CurrentWorkCycle;
            if (!workHitSent && workStrokeTime >= cycle * WorkContact && workWeight > .85f)
            {
                // Звук и событие уходят в ApplyPose, когда молоток уже стоит в позе удара.
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
            workStrikePending = false;
        }
    }

    static void AddRotation(Transform t, Vector3 euler)
    {
        if (t != null) t.localRotation *= Quaternion.Euler(euler);
    }

    // Корпус: слегка наклоняется к стене. Вызывается до постановки стоп, чтобы ноги учли смещение таза.
    void ApplyWorkBody(PoseFrame f)
    {
        float w = f.work;
        float accent = f.workHeavy ? 1.22f : 1f;
        float tap = Mathf.Max(0f, 1f - Mathf.Abs(f.workPhase - WorkContact) / .17f) * w * accent;
        if (rig.hips != null)
            rig.hips.localPosition += new Vector3(-.015f * w, -.043f * w - .017f * tap, .044f * w);
        AddRotation(rig.spine, new Vector3(6f * w + tap * 3f, -3f * w, 0f));
        AddRotation(rig.chest, new Vector3(3f * tap, 3.5f * w, .6f * tap));
        AddRotation(rig.head, new Vector3((3f + 5f * tap) * w, 0f, 0f));
        AddRotation(rig.upperArmL, new Vector3(-18f * w, 0f, 0f));
        AddRotation(rig.elbowL, new Vector3(-24f * w, 0f, 0f));
    }

    void PlaceWorkHammer(PoseFrame f)
    {
        if (rig.hammerRoot == null || rig.handR == null || rig.HammerHolster == null) return;
        float phase = f.workPhase;
        bool heavy = f.workHeavy;
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
            LeoAxePose.WristGoal(rig.handR, transform.TransformPoint(grip), wrist), pole, wrist, f.work);

        rig.PlaceAxe(f.holster);

        // Молоток держится так же поперёк ладони. Его карман — начало и конец доставания.
        Quaternion held = LeoAxePose.ToolRotation(rig.handR);
        Vector3 shaft = Vector3.Scale(HammerGrip, rig.hammerRoot.lossyScale);
        Vector3 origin = LeoAxePose.Grip(rig.handR) - held * shaft;
        float draw = LeoMotionProfile.Smooth(f.work);
        rig.hammerRoot.SetPositionAndRotation(Vector3.Lerp(rig.HammerHolster.position, origin, draw),
            Quaternion.Slerp(rig.HammerHolster.rotation, held, draw));

        // PlaceAxe раскрыл ладонь у пояса: при доставании закрываем её на молотке.
        if (rig.fingersR != null)
            foreach (Transform finger in rig.fingersR)
                rig.Rotate(finger, new Vector3(-42f * f.holster * draw, 0f, 0f));
        if (rig.fingerTipsR != null)
            foreach (Transform joint in rig.fingerTipsR)
                rig.Rotate(joint, new Vector3(-35f * f.holster * draw, 0f, 0f));

        // кадр контакта: молоток уже стоит в позе удара, берём настоящую точку бойка
        if (workStrikePending)
        {
            workStrikePending = false;
            SfxManager.Play("build", heavy ? .62f : .43f, heavy ? .92f : (workBeatIndex % 2 == 0 ? 1.09f : 1.01f));
            Vector3 face = rig.hammerStrike != null ? rig.hammerStrike.position : rig.hammerRoot.TransformPoint(HammerFace);
            BuildWorkPose.NotifyStrike(player, face);
        }
    }
}

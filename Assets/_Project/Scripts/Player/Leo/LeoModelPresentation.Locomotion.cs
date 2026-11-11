using UnityEngine;

public sealed partial class LeoModelPresentation
{
    readonly LeoMotionProfile.Gait gait = new LeoMotionProfile.Gait();
    LeoMotionProfile.Book locomotion;
    LeoFootPlacement feet;
    Quaternion previousFacing;
    float cycle = .25f;
    float motionSpeed, jogWeight, moveWeight, turnWeight;

    void InitializeLocomotion()
    {
        locomotion = chopBook;
        if (locomotion == null)
        {
            locomotion = new LeoMotionProfile.Book();
            LeoMotionProfile.SetDefaultGaits(locomotion);
        }
        feet = new LeoFootPlacement(rig, player != null ? player.transform : transform);
        ResetLocomotion();
    }

    void ResetLocomotion()
    {
        previousFacing = transform.rotation;
        cycle = .25f;
        motionSpeed = jogWeight = moveWeight = turnWeight = 0f;
        feet?.Reset();
    }

    void ApplyLocomotion(float travel, float frameTime, float now, float carry, bool animate, bool probeGround,
        LeoCelebration.Pose cheer)
    {
        float dt = Mathf.Min(.1f, frameTime);
        if (travel > Mathf.Max(.8f, frameTime * 20f))
        {
            ResetLocomotion();
            travel = 0f;
        }

        float speed = animate ? travel / Mathf.Max(.001f, frameTime) : 0f;
        if (speed < .05f) speed = 0f;
        motionSpeed = Mathf.Lerp(motionSpeed, Mathf.Min(speed, 18f), 1f - Mathf.Exp(-12f * dt));
        float targetWeight = animate ? LeoMotionProfile.Smooth((motionSpeed - .055f) / .72f) : 0f;
        moveWeight = Mathf.MoveTowards(moveWeight, targetWeight, dt * (targetWeight > moveWeight ? 8f : 5.5f));
        jogWeight = Mathf.Lerp(jogWeight, LeoMotionProfile.RunBlend(motionSpeed), 1f - Mathf.Exp(-8f * dt));
        LeoMotionProfile.MixInto(gait, locomotion.walk, locomotion.run, jogWeight);

        float scale = Mathf.Max(.01f, transform.lossyScale.y);
        if (animate && speed > 0f && moveWeight > .001f)
        {
            float advance = Mathf.Min(travel / (gait.stride * scale), LeoMotionProfile.MaxCadence(jogWeight) * dt);
            cycle = Mathf.Repeat(cycle + advance, 1f);
        }
        else if (moveWeight <= .001f) cycle = .25f;

        float yaw = Mathf.DeltaAngle(previousFacing.eulerAngles.y, transform.eulerAngles.y);
        previousFacing = transform.rotation;
        float turn = Mathf.Clamp(yaw / Mathf.Max(.001f, frameTime) / 235f, -1f, 1f);
        turnWeight = Mathf.Lerp(turnWeight, turn, 1f - Mathf.Exp(-11f * dt));

        float angle = cycle * Mathf.PI * 2f;
        float swing = Mathf.Cos(angle);
        float sway = Mathf.Sin(angle);
        float breath = Mathf.Sin(now * 1.75f);
        float idle = 1f - moveWeight;
        if (rig.hips != null)
        {
            rig.hips.localPosition += new Vector3(.007f * breath * idle - .024f * sway * moveWeight,
                -.009f * idle + LeoMotionProfile.PelvisHeight(gait, cycle, jogWeight) * moveWeight, 0f);
            rig.Rotate(rig.hips, new Vector3(0f, (4.5f + 1.2f * jogWeight) * swing * moveWeight,
                (-2.6f * sway - 1.5f * turnWeight) * moveWeight));
        }
        float lean = (1.7f + 4.2f * jogWeight + 2.8f * carry) * moveWeight + 1.5f * carry;
        rig.Rotate(rig.spine, new Vector3(lean + breath * idle * .55f, -1.6f * swing * moveWeight, 1.2f * sway * moveWeight));
        rig.Rotate(rig.chest, new Vector3(0f, -(5.2f + 1.3f * jogWeight) * swing * moveWeight, .9f * turnWeight * moveWeight));
        float gaze = Mathf.Sin(now * .93f) * 1.8f * idle - turnWeight * 3f * moveWeight;
        rig.Rotate(rig.head, new Vector3(-lean * .70f, gaze, -sway * moveWeight * .72f));

        float arm = Mathf.Cos(angle - .24f);
        rig.Rotate(rig.upperArmL, new Vector3(arm * gait.arm * moveWeight * (1f - carry * .26f), 0f, -2.5f * moveWeight));
        rig.Rotate(rig.elbowL, new Vector3(-(5f + (1f - arm) * (6f + 7f * jogWeight)) * moveWeight, 0f, 0f));
        rig.Rotate(rig.upperArmR, new Vector3(-swing * (4f + 3f * jogWeight) * moveWeight, 0f, 3.5f * moveWeight));
        rig.Rotate(rig.elbowR, new Vector3(-5f - (2f + 5f * jogWeight) * moveWeight, 0f, 0f));
        rig.Rotate(rig.backpack, new Vector3(.9f * swing * moveWeight, 0f, -1.5f * sway * moveWeight));

        BraceForChop();
        ApplyWorkBody();
        cheer.ApplyBody(rig, carry);
        float release = Mathf.Max(Mathf.Abs(turn), Mathf.Abs(turnWeight));
        float brace = Mathf.Max(chopWeight, workWeight * .66f) * (1f - moveWeight);
        feet.Apply(gait, cycle, moveWeight, jogWeight, release, brace, probeGround, probeGround, ~0, dt, cheer);
    }
}

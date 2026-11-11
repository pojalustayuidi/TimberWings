using UnityEngine;

// Visual poses for the existing river and island respawn mechanics.
sealed class LeoAirWaterMotion
{
    readonly RaycastHit[] groundHits = new RaycastHit[8];
    PlayerMovement owner;
    WaterSwim water;
    Rigidbody body;
    CapsuleCollider capsule;
    float swim, fall, landing, stroke, effort, airTime, descent, waterY, sinking;

    internal struct Frame
    {
        internal float Swim, Fall, Landing;
        internal float Stroke, Effort, AirTime, Descent, WaterY, Sinking;
        internal bool MatchWaterline, InMotion;

        internal bool StowTool => Swim > .015f || Fall > .015f || Landing > .015f;
        internal string Label => Landing > .015f ? "Land" : Fall > .015f ? "Fall" : Effort > .3f ? "Swim" : "Swim idle";

        internal void Apply(LeoRig rig, float holster)
        {
            if (rig.hips == null) return;
            float freeHand = LeoMotionProfile.Smooth((holster - .66f) / .34f);
            if (Swim > 0f) ApplySwim(rig, freeHand);
            if (Fall > 0f) ApplyFall(rig, freeHand);
            if (Landing > 0f) ApplyLanding(rig, freeHand);
        }

        void ApplySwim(LeoRig rig, float freeHand)
        {
            float phase = Stroke * Mathf.PI * 2f;
            float sway = Mathf.Sin(phase);
            float kick = Mathf.Sin(phase * 2f - .24f);
            float pull = LeoMotionProfile.Smooth((Stroke - .07f) / .31f) *
                (1f - LeoMotionProfile.Smooth((Stroke - .47f) / .44f));
            float lean = Mathf.Lerp(12f, 51f, Effort);
            Blend(rig, rig.hips, lean + 2.8f * sway, 1.5f * sway, 2.2f * kick, Swim);
            Blend(rig, rig.spine, Mathf.Lerp(6f, 10f, Effort), -2f * sway, 1.4f * sway, Swim);
            Blend(rig, rig.chest, 3f, 2.5f * sway, 0f, Swim);
            Blend(rig, rig.neck, -lean * .34f, 0f, 0f, Swim);
            Blend(rig, rig.head, -lean * .64f - 4f + 4f * Sinking, -2f * sway, -2f * kick, Swim);
            Blend(rig, rig.jaw, 2f + 4f * pull, 0f, 0f, Swim);

            float reach = Mathf.Lerp(-49f + 14f * sway, -143f + 72f * pull, Effort);
            float spread = Mathf.Lerp(44f + 12f * pull, 10f + 51f * pull, Effort);
            float elbow = Mathf.Lerp(35f + 13f * pull, 12f + 60f * pull, Effort);
            Arms(rig, reach, spread, elbow, 5f * sway, Swim, freeHand);
            Legs(rig, -11f + 14f * kick, -11f - 14f * kick, 21f + 13f * kick, 21f - 13f * kick, Swim);

            if (rig.eyeL == null || rig.eyeR == null) return;
            float scale = Mathf.Max(.01f, rig.transform.lossyScale.y);
            float eyesY = (rig.eyeL.position.y + rig.eyeR.position.y) * .5f;
            float above = (.188f + .04f * Mathf.Cos(phase) - .37f * Sinking) * scale;
            float surface = MatchWaterline ? WaterY : eyesY - above;
            if (MatchWaterline)
            {
                float rise = Mathf.Clamp(surface + above - eyesY, -.46f * scale, 1.05f * scale);
                rig.hips.position += Vector3.up * (rise * Swim);
            }

            // The island's river is opaque. Bring each palm above the surface
            // on recovery so the stroke reads even from behind.
            SwimRecovery(rig, rig.upperArmL, rig.elbowL, rig.handL, -1f, Stroke, surface, scale, Swim);
            SwimRecovery(rig, rig.upperArmR, rig.elbowR, rig.handR, 1f,
                Mathf.Repeat(Stroke + .5f, 1f), surface, scale, Swim * freeHand);
        }

        void SwimRecovery(LeoRig rig, Transform shoulder, Transform elbow, Transform hand, float side,
            float phase, float surface, float scale, float weight)
        {
            if (shoulder == null || elbow == null || hand == null) return;
            float lift = LeoMotionProfile.Smooth((phase - .59f) / .18f) *
                (1f - LeoMotionProfile.Smooth((phase - .83f) / .17f));
            float reach = weight * lift * (1f - Sinking);
            if (reach <= 0f) return;

            Transform model = rig.transform;
            Vector3 goal = shoulder.position +
                model.TransformVector(new Vector3(side * Mathf.Lerp(.18f, .24f, Effort), 0f, Mathf.Lerp(.095f, .23f, Effort)));
            goal.y = surface + (.13f + .035f * lift) * scale;
            Vector3 pole = shoulder.position + model.TransformVector(new Vector3(.50f * side, -.25f, -.08f));
            LeoRig.SolveTwoBone(shoulder, elbow, hand, goal, pole, hand.rotation, reach);
        }

        void ApplyFall(LeoRig rig, float freeHand)
        {
            float beat = Mathf.Sin(13f * AirTime);
            float settle = LeoMotionProfile.Smooth(AirTime / .52f);
            Blend(rig, rig.hips, Mathf.Lerp(13f, -13f, settle), 3.5f * beat, 7f * beat, Fall);
            Blend(rig, rig.spine, 8f - 15f * settle, -6f * beat, -3f * beat, Fall);
            Blend(rig, rig.chest, 4f, 3f * beat, 2f * beat, Fall);
            Blend(rig, rig.head, 16f - 10f * settle, -6f * beat, -3f * beat, Fall);
            Blend(rig, rig.jaw, 16f - 5f * settle, 0f, 0f, Fall);
            Blend(rig, rig.browL, 0f, 0f, 7f, Fall);
            Blend(rig, rig.browR, 0f, 0f, -7f, Fall);
            Arms(rig, -42f - 16f * settle, 59f + 8f * settle, 29f + 7f * beat, 14f * beat, Fall, freeHand);
            Legs(rig, -22f + 13f * beat, -22f - 13f * beat, 41f - 10f * beat, 41f + 10f * beat, Fall);
        }

        void ApplyLanding(LeoRig rig, float freeHand)
        {
            float brace = LeoMotionProfile.Smooth((Descent - .56f) / .24f) *
                (1f - LeoMotionProfile.Smooth((Descent - .80f) / .20f));
            float air = 1f - LeoMotionProfile.Smooth((Descent - .46f) / .54f);
            Blend(rig, rig.hips, 3f * air, 0f, 0f, Landing);
            Blend(rig, rig.spine, 6f * air + 7f * brace, 0f, 0f, Landing);
            Blend(rig, rig.chest, -2f * air, 0f, 0f, Landing);
            Blend(rig, rig.head, 12f * air + 4f * brace, 0f, 0f, Landing);
            Arms(rig, -29f * air, 34f * air, 26f * air, 3f * air, Landing, freeHand);
            Legs(rig, -9f * air - 9f * brace, -9f * air - 9f * brace,
                19f * air + 19f * brace, 19f * air + 19f * brace, Landing);
        }

        static void Arms(LeoRig rig, float pitch, float spread, float elbow, float asymmetry, float weight, float freeHand)
        {
            float rightWeight = weight * freeHand;
            Blend(rig, rig.upperArmL, pitch + asymmetry, 0f, -spread, weight);
            Blend(rig, rig.upperArmR, pitch - asymmetry, 0f, spread, rightWeight);
            Blend(rig, rig.elbowL, -elbow, -4f, -6f, weight);
            Blend(rig, rig.elbowR, -elbow, 4f, 6f, rightWeight);
            Blend(rig, rig.handL, 9f, -12f, 6f, weight);
            Blend(rig, rig.handR, 9f, 12f, -6f, rightWeight);
        }

        static void Legs(LeoRig rig, float left, float right, float leftKnee, float rightKnee, float weight)
        {
            Blend(rig, rig.hipL, left, 0f, -5f * Mathf.Clamp01(leftKnee / 20f), weight);
            Blend(rig, rig.hipR, right, 0f, 5f * Mathf.Clamp01(rightKnee / 20f), weight);
            Blend(rig, rig.kneeL, leftKnee, 0f, 0f, weight);
            Blend(rig, rig.kneeR, rightKnee, 0f, 0f, weight);
            Blend(rig, rig.ankleL, -(.44f * leftKnee + .54f * left), 0f, 0f, weight);
            Blend(rig, rig.ankleR, -(.44f * rightKnee + .54f * right), 0f, 0f, weight);
        }

        static void Blend(LeoRig rig, Transform joint, float x, float y, float z, float weight)
        {
            if (joint == null || weight <= 0f) return;
            Quaternion goal = rig.RestRotation(joint) * Quaternion.Euler(x, y, z);
            joint.localRotation = Quaternion.Slerp(joint.localRotation, goal, weight);
        }
    }

    internal Frame Read(PlayerMovement player, float dt)
    {
        if (player == null || SaveSystem.IsLoading)
        {
            Reset();
            return default;
        }
        if (player != owner)
        {
            Reset();
            owner = player;
            body = player.GetComponent<Rigidbody>();
            capsule = player.GetComponent<CapsuleCollider>();
            water = player.GetComponent<WaterSwim>();
        }
        IslandEdgeGuard edge = IslandEdgeGuard.Instance;
        float landingProgress = 0f;
        IslandEdgeGuard.State state = edge != null ? edge.ReadPlayerMotion(player, out landingProgress) : IslandEdgeGuard.State.Idle;
        if (water == null && (WaterSwim.Swimming || state == IslandEdgeGuard.State.Falling))
            water = player.GetComponent<WaterSwim>();
        bool returning = state == IslandEdgeGuard.State.Returning;
        float surfaceY = 0f, sink = 0f;
        bool swimming = !returning && water != null && water.isActiveAndEnabled && water.TryGetPoseSurface(out surfaceY, out sink);
        bool falling = !returning && !swimming && (state == IslandEdgeGuard.State.Falling || IsDropping(player));

        if (returning)
        {
            // The teleport from the abyss happens under the cloud cover.
            swim = fall = 0f;
            airTime = 0f;
            landing = 1f;
            descent = landingProgress;
        }
        else
        {
            swim = Mathf.MoveTowards(swim, swimming ? 1f : 0f, dt / (swimming ? .24f : .17f));
            fall = Mathf.MoveTowards(fall, falling ? 1f : 0f, dt / (falling ? .16f : .15f));
            landing = Mathf.MoveTowards(landing, 0f, dt / .14f);
            airTime = fall > 0f ? airTime + dt : 0f;
        }

        bool paddling = swimming && (player.AutoWalking || Mathf.Abs(MobileInput.Horizontal) > .04f || Mathf.Abs(MobileInput.Vertical) > .04f);
        effort = Mathf.MoveTowards(effort, paddling ? 1f : 0f, dt * 4f);
        stroke = swim > 0f ? Mathf.Repeat(stroke + dt * Mathf.Lerp(.82f, 1.24f, effort), 1f) : 0f;
        if (swimming)
        {
            waterY = surfaceY;
            sinking = sink;
        }

        return new Frame
        {
            Swim = swim, Fall = fall, Landing = landing, Stroke = stroke, Effort = effort,
            AirTime = airTime, Descent = descent, WaterY = waterY, Sinking = sinking,
            MatchWaterline = swim > 0f, InMotion = swimming || falling || returning
        };
    }

    bool IsDropping(PlayerMovement player)
    {
        if (body == null || body.isKinematic || player.ControlLocked || body.linearVelocity.y > -1.25f) return false;
        Vector3 feet = player.transform.position;
        if (capsule != null && capsule.enabled)
        {
            Bounds bounds = capsule.bounds;
            feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        float distance = Mathf.Clamp(-body.linearVelocity.y * .105f, .42f, .80f);
        int count = Physics.RaycastNonAlloc(feet + Vector3.up * .15f, Vector3.down, groundHits,
            distance + .15f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(player.transform) && hit.normal.y >= .60f &&
                (hit.rigidbody == null || hit.rigidbody.isKinematic))
                return false;
        }
        return true;
    }

    internal void Reset()
    {
        owner = null;
        swim = fall = landing = stroke = effort = airTime = descent = waterY = sinking = 0f;
    }
}

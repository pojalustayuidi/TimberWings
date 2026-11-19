using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class OrbitCamera
{
    [Header("First Person")]
    public KeyCode firstPersonKey = KeyCode.F5;
    public KeyCode firstPersonAxeKey = KeyCode.R;
    [Range(55f, 95f)] public float firstPersonFov = 68f;
    [Tooltip("Fallback eye height when the player has no Leo rig.")]
    [Range(.7f, 2.4f)] public float firstPersonEyeHeight = 1.65f;
    [Tooltip("Adds height above Leo’s baked eye position without moving the collision body.")]
    [Range(0f, .35f)] public float firstPersonEyeLift = .21f;
    [Tooltip("Scale multiplier for Leo’s first-person arms and axe.")]
    [Range(.4f, 2f)] public float firstPersonToolScale = 1f;

    const string FpBobKey = "tw_cam_first_person_bob";
    const float FpTransition = .22f;
    static int fpBobSetting = -1;

    public static bool FirstPersonHeadBob
    {
        get
        {
            if (fpBobSetting < 0) fpBobSetting = PlayerPrefs.GetInt(FpBobKey, 1);
            return fpBobSetting == 1;
        }
        set
        {
            fpBobSetting = value ? 1 : 0;
            PlayerPrefs.SetInt(FpBobKey, fpBobSetting);
        }
    }

    bool fpRequested, fpLive, fpCarryAxe, fpShowView, fpExiting, fpKeepPitch;
    float fpPitch, fpPitchGoal, fpYawGoal, fpNear, fpTransitionAt, fpFromFov;
    float fpEyeHeight, fpEyeY, fpSpeed, fpStride, fpBobWeight;
    Vector3 fpFromPosition;
    Quaternion fpFromRotation;
    Camera fpCamera, fpRendering;
    bool fpRenderSubscribed;
    PlayerMovement fpBoundPlayer;
    LeoRig fpRig;
    LeoFirstPersonView fpView;
    readonly List<Renderer> fpBody = new List<Renderer>(240);
    readonly List<Renderer> fpMaskedBody = new List<Renderer>(240);

    public static bool FirstPersonActive => Active && instance != null && instance.fpLive;

    bool FpCinematic => player == null || player.ControlLocked ||
        SaveSystem.IsLoading || BuildingController.AnyRestorePending || BuildWorkPose.MovementLocked ||
        BuildWorkPose.HasWork(player) || BuildingController.AnyRevealing || CameraVibe.RevealActive ||
        CameraVibe.RevealShown > .01f || DialoguePreferences.ConversationOwnsCamera ||
        DialogueManager.IsBusy || DialogueSequence.AnyPlaying || StoryInspection.Active ||
        LeoCelebration.IsPlaying || ArrivalIntroSequence.Active || EndingSequence.Active || WaterSwim.Swimming;

    void FpToggle()
    {
        if (Application.isMobilePlatform || !Active || !Enabled || !Application.isFocused ||
            MobileInput.ModalOpen || FpCinematic || Time.timeScale <= 0f) return;
        if (Input.GetKeyDown(firstPersonKey) && (interaction == null || !interaction.IsChopping))
        {
            if (!fpRequested)
            {
                fpRequested = true;
                fpExiting = false;
                if (player.AutoWalking) player.CancelWalk();
            }
            else
            {
                bool wasLive = fpLive;
                if (wasLive) FpCaptureTransition();
                if (player.AutoWalking) player.CancelWalk();
                FpLeave(true);
                fpExiting = wasLive;
                if (fpExiting) FpSubscribeRender();
            }
        }
        if (fpLive && Input.GetKeyDown(firstPersonAxeKey) &&
            !Input.GetKey(freeCursorKey) && !Input.GetKey(KeyCode.RightAlt))
            fpCarryAxe = !fpCarryAxe;
    }

    void FpCaptureTransition()
    {
        fpFromPosition = cam.transform.position;
        fpFromRotation = cam.transform.rotation;
        fpFromFov = cam.fieldOfView;
        fpTransitionAt = Time.unscaledTime;
    }

    bool FpStep()
    {
        if (!fpRequested || FpCinematic)
        {
            FpLeave(false);
            if (fpExiting && FpCinematic) FpEndExit();
            return false;
        }

        if (!fpLive)
        {
            FpBind();
            FpCaptureTransition();
            fpLive = true;
            fpNear = cam.nearClipPlane;
            cam.nearClipPlane = .025f;
            if (!fpKeepPitch)
                fpPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x) * .3f, -9f, 9f);
            fpPitchGoal = fpPitch;
            fpKeepPitch = true;
            fpYawGoal = yaw;
            fpEyeY = player.transform.position.y + fpEyeHeight;
            fpSpeed = fpStride = fpBobWeight = 0f;
            ClearTreePeek();
            treePeekBlend = 0f;
            resumeLookAt = Time.unscaledTime + lookResumeDelay;
            FpSubscribeRender();
            GameEvents.ChopHit += FpOnImpact;
            GameEvents.TreeChopped += FpOnFelled;
        }

        bool modal = MobileInput.ModalOpen || !Application.isFocused || Time.timeScale <= 0f;
        if (wasModal && !modal) resumeLookAt = Time.unscaledTime + lookResumeDelay;
        wasModal = modal;
        bool alt = Input.GetKey(freeCursorKey) || Input.GetKey(KeyCode.RightAlt);
        bool lockMouse = !modal && !alt && Time.unscaledTime >= resumeLookAt &&
            (MouseLook || Input.GetMouseButton(1));
        bool changed = cursorHeld != lockMouse || (Cursor.lockState == CursorLockMode.Locked) != lockMouse;
        if (changed) SetCursorLocked(lockMouse);

        float dt = Mathf.Min(Time.deltaTime, .05f);
        float transition = LeoMotionProfile.Smooth((Time.unscaledTime - fpTransitionAt) / FpTransition);
        if (lockMouse && !changed)
        {
            Vector2 delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            delta = Vector2.ClampMagnitude(delta * (mouseSensitivity * PerformanceBoot.MouseMultiplier), 42f);
            fpYawGoal += delta.x;
            fpPitchGoal = Mathf.Clamp(fpPitchGoal - delta.y, -78f, 78f);
        }
        else
        {
            fpYawGoal = yaw;
            fpPitchGoal = fpPitch;
        }
        float look = 1f - Mathf.Exp(-38f * Mathf.Min(Time.unscaledDeltaTime, .05f));
        yaw += (fpYawGoal - yaw) * look;
        fpPitch = Mathf.Lerp(fpPitch, fpPitchGoal, look);

        Vector3 origin = player.transform.position;
        float wantedY = origin.y + fpEyeHeight;
        if (snapPending || (origin - lastPlayerPos).sqrMagnitude > 1f)
        {
            fpEyeY = wantedY;
            fpView?.Suspend();
            fpTransitionAt = Time.unscaledTime - FpTransition;
            transition = 1f;
        }
        else
            fpEyeY = Mathf.Lerp(fpEyeY, wantedY, 1f - Mathf.Exp(-25f * dt));
        fpEyeY = Mathf.Clamp(fpEyeY, wantedY - .07f, wantedY + .07f);
        crosshairOn = lockMouse && transition >= 1f;

        bool chopping = interaction != null && interaction.IsChopping;
        Vector3 velocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        fpSpeed = Mathf.Lerp(fpSpeed, modal || chopping ? 0f : velocity.magnitude, 1f - Mathf.Exp(-10f * dt));
        float run = LeoMotionProfile.RunBlend(fpSpeed);
        float move = Mathf.Clamp01(fpSpeed / 1.8f);
        fpStride = Mathf.Repeat(fpStride + dt * fpSpeed * Mathf.Lerp(3.7f, 2.65f, run), 2f * Mathf.PI);
        float wantedBob = !modal && !chopping && !VibeSettings.ReduceEffects && FirstPersonHeadBob ? move : 0f;
        fpBobWeight = Mathf.MoveTowards(fpBobWeight, wantedBob, dt * 9f);
        float bob = Mathf.Cos(fpStride * 2f) * Mathf.Lerp(.0035f, .010f, run) * fpBobWeight;
        Quaternion rotation = Quaternion.Euler(fpPitch, yaw, 0f);
        Vector3 eye = new Vector3(origin.x, fpEyeY + bob, origin.z);
        cam.orthographic = false;
        cam.fieldOfView = Mathf.Lerp(fpFromFov, firstPersonFov, transition);
        cam.transform.SetPositionAndRotation(Vector3.Lerp(fpFromPosition, eye, transition),
            Quaternion.Slerp(fpFromRotation, rotation, transition));
        player.cameraYaw = yaw;

        // Keep the original orbit controller ready to resume from the current player pose.
        focus = origin + focusOffset;
        focusVel = ahead = aheadVel = Vector3.zero;
        lastPlayerPos = origin;
        shakeT = 0f;
        arcT = -1f;
        arcOffset = arcOffsetVel = idleT = 0f;
        snapPending = false;

        fpShowView = !modal && transition > .94f;
        if (fpView != null && fpRig != null && fpShowView)
            fpView.Step(interaction, fpCarryAxe, dt, fpStride, move, run, firstPersonToolScale);
        else
            fpView?.Suspend();
        UpdateCrosshair(Mathf.Min(Time.unscaledDeltaTime, .05f));
        return true;
    }

    void FpBlendToOrbit()
    {
        if (!fpExiting) return;
        float t = LeoMotionProfile.Smooth((Time.unscaledTime - fpTransitionAt) / FpTransition);
        if (t >= 1f) { FpEndExit(); return; }
        cam.fieldOfView = Mathf.Lerp(fpFromFov, cam.fieldOfView, t);
        cam.transform.SetPositionAndRotation(Vector3.Lerp(fpFromPosition, cam.transform.position, t),
            Quaternion.Slerp(fpFromRotation, cam.transform.rotation, t));
    }

    void FpEndExit()
    {
        fpExiting = false;
        FpUnsubscribeRender();
    }

    void FpSubscribeRender()
    {
        if (fpRenderSubscribed) return;
        fpRenderSubscribed = true;
        RenderPipelineManager.beginCameraRendering += FpBeforeRender;
        RenderPipelineManager.endCameraRendering += FpAfterRender;
    }

    void FpUnsubscribeRender()
    {
        if (!fpRenderSubscribed) return;
        fpRenderSubscribed = false;
        RenderPipelineManager.beginCameraRendering -= FpBeforeRender;
        RenderPipelineManager.endCameraRendering -= FpAfterRender;
        FpRestoreRenderers();
    }

    void FpBind()
    {
        if (fpCamera != cam || fpBoundPlayer != player)
        {
            FpReleaseView();
            fpCamera = cam;
            fpBoundPlayer = player;
        }
        LeoRig rig = player.model != null ? player.model.GetComponentInChildren<LeoRig>(true) : null;
        if (fpRig != rig || fpRig == null && fpView != null)
        {
            fpView?.Dispose();
            fpView = null;
            fpRig = rig;
        }
        if (fpView == null && fpRig != null)
            fpView = LeoFirstPersonView.Create(cam, fpRig);
        player.GetComponentsInChildren(true, fpBody);
        fpBody.RemoveAll(r => !(r is MeshRenderer) && !(r is SkinnedMeshRenderer));

        var capsule = player.GetComponent<CapsuleCollider>();
        fpEyeHeight = capsule != null ? Mathf.Max(.55f,
            capsule.bounds.max.y - player.transform.position.y - .10f) : firstPersonEyeHeight;
        if (fpRig != null && fpRig.eyeL != null && fpRig.eyeR != null)
        {
            // Read the baked eyes in the rig's rest pose, independent of breathing and blinking.
            Vector3 eye = (FpRestPoint(fpRig.eyeL) + FpRestPoint(fpRig.eyeR)) * .5f;
            fpEyeHeight = Mathf.Clamp(fpRig.transform.TransformPoint(eye).y - player.transform.position.y + firstPersonEyeLift,
                .55f, firstPersonEyeHeight + .30f);
        }
        lastPlayerPos = player.transform.position;
    }

    Vector3 FpRestPoint(Transform joint)
    {
        Vector3 point = Vector3.zero;
        for (Transform bone = joint; bone != null && bone != fpRig.transform; bone = bone.parent)
            point = fpRig.RestPosition(bone) + fpRig.RestRotation(bone) * Vector3.Scale(fpRig.RestScale(bone), point);
        return point;
    }

    void FpBeforeRender(ScriptableRenderContext context, Camera rendering)
    {
        if ((!fpLive && !fpExiting) || rendering != fpCamera || FpCinematic) return;
        fpRendering = rendering;
        float progress = (Time.unscaledTime - fpTransitionAt) / FpTransition;
        if (fpLive ? progress > .78f : progress < .29f)
        {
            foreach (Renderer renderer in fpBody) FpMask(renderer);
            // Equipment can change while F5 is active, after the initial body snapshot.
            if (fpRig != null) FpMask(fpRig.EquippedAxeRenderer);
        }
        fpView?.SetRendered(fpShowView && !MobileInput.ModalOpen && Application.isFocused && Time.timeScale > 0f);
    }

    void FpMask(Renderer renderer)
    {
        if (renderer == null || renderer.forceRenderingOff) return;
        renderer.forceRenderingOff = true;
        fpMaskedBody.Add(renderer);
    }

    void FpAfterRender(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering == fpRendering) FpRestoreRenderers();
    }

    void FpRestoreRenderers()
    {
        fpRendering = null;
        foreach (var r in fpMaskedBody)
            if (r != null) r.forceRenderingOff = false;
        fpMaskedBody.Clear();
        fpView?.SetRendered(false);
    }

    internal static bool TryReadFirstPersonBark(ResourceItem item, out Vector3 point)
    {
        point = default;
        if (!FirstPersonActive || instance.fpView == null || instance.fpRig == null || !instance.fpShowView ||
            instance.FpCinematic) return false;
        Transform view = instance.fpCamera.transform;
        float slope = view.forward.y / Mathf.Max(.38f, Vector3.ProjectOnPlane(view.forward, Vector3.up).magnitude);
        float height = view.position.y + Mathf.Clamp(slope * .66f - .085f, -.49f, .19f);
        return LeoChopContact.TryPointAtHeight(item, instance.fpRig.transform, height, out point);
    }

    void FpOnImpact() => fpView?.Hit(interaction, false);
    void FpOnFelled() => fpView?.Hit(interaction, true);

    void FpLeave(bool resetRequest)
    {
        if (resetRequest)
        {
            fpRequested = fpCarryAxe = fpExiting = fpKeepPitch = false;
            FpUnsubscribeRender();
        }
        if (!fpLive) return;
        fpLive = fpShowView = false;
        FpUnsubscribeRender();
        GameEvents.ChopHit -= FpOnImpact;
        GameEvents.TreeChopped -= FpOnFelled;
        fpView?.Suspend();
        if (fpCamera != null) fpCamera.nearClipPlane = fpNear;
        snapPending = true;
        fovVelocity = 0f;
        resumeLookAt = Time.unscaledTime + lookResumeDelay;
        ReleaseCursor();
    }

    void FpReleaseView()
    {
        if (fpView != null) fpView.Dispose();
        fpView = null;
        fpCamera = null;
        fpBoundPlayer = null;
        fpRig = null;
        fpBody.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// CAM-01: reversible PC playtest. Uses the existing orbit controller, input,
// inventory, tree state, actor and quest logic. No scene edits or save migration.
public partial class OrbitCamera
{
    [Header("CAM-01 Experimental First Person")]
    public KeyCode firstPersonKey = KeyCode.F5;
    [Range(55f, 95f)] public float firstPersonFov = 75f;
    [Range(.7f, 2.4f)] public float firstPersonEyeHeight = 1.65f;
    [Range(.4f, 2f)] public float firstPersonToolScale = 1f;

    bool fpRequested, fpLive;
    float fpPitch, fpOldPitch, fpNear, fpClock, fpNextHide;
    GameObject fpView;
    Transform fpTool, fpArm;
    Material fpLeather, fpSleeve, fpWood, fpMetal;
    readonly Dictionary<Renderer, bool> fpHidden = new Dictionary<Renderer, bool>();
    readonly List<Renderer> fpMeshes = new List<Renderer>();
    Camera fpCamera;
    Transform fpPlayer;
    LeoRig fpRig;
    bool fpRenderSubscribed;

    public static bool FirstPersonActive => Active && instance != null && instance.fpLive;

    bool FpCinematic => player == null || player.ControlLocked ||
        SaveSystem.IsLoading || BuildWorkPose.MovementLocked || BuildingController.AnyRevealing ||
        CameraVibe.RevealShown > .01f || DialoguePreferences.ConversationOwnsCamera ||
        DialogueManager.IsBusy || DialogueSequence.AnyPlaying || StoryInspection.Active ||
        LeoCelebration.IsPlaying || ArrivalIntroSequence.Active || EndingSequence.Active || WaterSwim.Swimming;

    void FpToggle()
    {
        if (!Input.GetKeyDown(firstPersonKey) || Application.isMobilePlatform || !Active ||
            !Enabled || !Application.isFocused || MobileInput.ModalOpen || FpCinematic ||
            (interaction != null && interaction.IsChopping)) return;
        fpRequested = !fpRequested;
        if (fpRequested)
        {
            fpOldPitch = pitch;
            fpPitch = 0f;
            if (player.AutoWalking) player.CancelWalk();
        }
        else FpLeave(true);
    }

    // Returns true if we own this frame. The stock orbit Step handles every cinematic.
    bool FpStep()
    {
        if (!fpRequested || FpCinematic)
        {
            FpLeave(false);
            return false;
        }
        if (!fpLive)
        {
            fpCamera = cam;
            fpPlayer = player.transform;
            fpNear = cam.nearClipPlane;
            fpLive = true;
            fpClock = 0f;
            fpNextHide = 0f;
            cam.nearClipPlane = .035f;
            ClearTreePeek();
            treePeekBlend = 0f;
            if (player.AutoWalking) player.CancelWalk();
            FpEnsureView();
            FpHideActor();
            if (!fpRenderSubscribed)
            {
                RenderPipelineManager.beginCameraRendering += FpBeforeRender;
                RenderPipelineManager.endCameraRendering += FpAfterRender;
                fpRenderSubscribed = true;
            }
            resumeLookAt = Time.unscaledTime + lookResumeDelay;
        }
        bool modal = MobileInput.ModalOpen || !Application.isFocused;
        if (wasModal && !modal) resumeLookAt = Time.unscaledTime + lookResumeDelay;
        wasModal = modal;
        bool alt = Input.GetKey(freeCursorKey) || Input.GetKey(KeyCode.RightAlt);
        bool lockMouse = !modal && !alt && Time.unscaledTime >= resumeLookAt;
        bool changed = cursorHeld != lockMouse || (Cursor.lockState == CursorLockMode.Locked) != lockMouse;
        if (changed) SetCursorLocked(lockMouse);
        crosshairOn = lockMouse;
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (lockMouse && !changed)
        {
            var d = Vector2.ClampMagnitude(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) *
                mouseSensitivity * PerformanceBoot.MouseMultiplier, 45f);
            yaw += d.x;
            fpPitch = Mathf.Clamp(fpPitch - d.y, -80f, 80f);
        }
        var rot = Quaternion.Euler(fpPitch, yaw, 0f);
        // Use collision-body height, never the animated head bone (avoids pose jitter).
        var capsule = player.GetComponent<CapsuleCollider>();
        float h = capsule != null ? Mathf.Clamp(capsule.bounds.max.y - player.transform.position.y - .10f,
            .55f, firstPersonEyeHeight + .30f) : firstPersonEyeHeight;
        cam.orthographic = false;
        cam.fieldOfView = firstPersonFov;
        cam.transform.SetPositionAndRotation(player.transform.position + Vector3.up * h, rot);
        player.cameraYaw = yaw;
        // Prepare orbit return without a stale focus from before FPS.
        focus = player.transform.position + focusOffset;
        focusVel = ahead = aheadVel = Vector3.zero;
        lastPlayerPos = player.transform.position;
        shakeT = 0f;
        arcT = -1f; arcOffset = arcOffsetVel = 0f; idleT = 0f;
        snapPending = false;
        FpHideActor();
        FpEnsureView();
        FpPose(dt, !modal);
        UpdateCrosshair(Mathf.Min(Time.unscaledDeltaTime, .05f));
        return true;
    }

    void FpHideActor()
    {
        if (fpPlayer == null || Time.unscaledTime < fpNextHide) return;
        fpNextHide = Time.unscaledTime + .25f;
        // Include equipment and backpack, including renderers activated later.
        foreach (var r in fpPlayer.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))) continue;
            if (!fpHidden.ContainsKey(r)) fpHidden.Add(r, r.forceRenderingOff);
            r.forceRenderingOff = true;
        }
    }

    void FpBeforeRender(ScriptableRenderContext context, Camera rendering)
    {
        if (!fpLive) return;
        // Only the gameplay camera sees the viewmodel, not minimap/snapshot cameras.
        bool own = rendering == fpCamera;
        foreach (var r in fpMeshes) if (r != null) r.forceRenderingOff = !own;
    }

    void FpAfterRender(ScriptableRenderContext context, Camera rendering)
    {
        if (!fpLive) return;
        foreach (var r in fpMeshes) if (r != null) r.forceRenderingOff = false;
    }

    Material FpMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        var m = new Material(shader); m.color = color; return m;
    }

    Transform FpPart(string name, Transform parent, Vector3 p, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.layer = 2;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p; go.transform.localScale = scale;
        var col = go.GetComponent<Collider>(); if (col != null) { col.enabled = false; Destroy(col); }
        var r = go.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        fpMeshes.Add(r);
        return go.transform;
    }

    void FpEnsureView()
    {
        if (fpView != null || cam == null || player == null) return;
        fpRig = player.GetComponentInChildren<LeoRig>(true);
        fpView = new GameObject("CAM01_ViewModel_HideFromMap");
        fpView.layer = 2;
        fpView.transform.SetParent(cam.transform, false);
        fpArm = new GameObject("CAM01_Arm").transform;
        fpArm.SetParent(fpView.transform, false);
        fpTool = new GameObject("CAM01_Axe").transform;
        fpTool.SetParent(fpArm, false);
        fpLeather = FpMaterial(new Color(.46f, .28f, .16f));
        fpSleeve = FpMaterial(new Color(.25f, .37f, .32f));
        fpWood = FpMaterial(new Color(.42f, .25f, .13f));
        fpMetal = FpMaterial(new Color(.57f, .63f, .65f));
        FpPart("Sleeve", fpArm, new Vector3(.07f,-.11f,-.09f), new Vector3(.13f,.16f,.31f), fpSleeve).localRotation = Quaternion.Euler(-18f, 3f, -12f);
        FpPart("Glove", fpArm, Vector3.zero, new Vector3(.11f,.125f,.12f), fpLeather);
        bool copied = false;
        if (fpRig != null && fpRig.axeRoot != null)
        {
            var source = fpRig.axeRoot;
            foreach (var mf in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.GetComponent<MeshRenderer>();
                if (src == null || mf.sharedMesh == null || !src.enabled) continue;
                var g = new GameObject("AxeMesh_" + mf.name, typeof(MeshFilter), typeof(MeshRenderer));
                g.layer = 2;
                g.transform.SetParent(fpTool, false);
                g.transform.localPosition = source.InverseTransformPoint(mf.transform.position);
                g.transform.localRotation = Quaternion.Inverse(source.rotation) * mf.transform.rotation;
                Vector3 ss = source.lossyScale, ms = mf.transform.lossyScale;
                g.transform.localScale = new Vector3(ms.x / Mathf.Max(.001f, Mathf.Abs(ss.x)), ms.y / Mathf.Max(.001f, Mathf.Abs(ss.y)), ms.z / Mathf.Max(.001f, Mathf.Abs(ss.z)));
                g.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var rr = g.GetComponent<MeshRenderer>(); rr.sharedMaterials = src.sharedMaterials;
                rr.shadowCastingMode = ShadowCastingMode.Off; rr.receiveShadows = false;
                fpMeshes.Add(rr); copied = true;
            }
            if (copied) fpTool.localPosition = -LeoAxePose.MainGrip * firstPersonToolScale;
        }
        if (!copied)
        {
            FpPart("Handle", fpTool, new Vector3(0f,.10f,0f), new Vector3(.045f,.48f,.045f), fpWood);
            FpPart("Head", fpTool, new Vector3(0f,.33f,.07f), new Vector3(.07f,.19f,.20f), fpMetal);
        }
        fpTool.localScale = Vector3.one * firstPersonToolScale;
    }

    void FpPose(float dt, bool shown)
    {
        if (fpView == null) return;
        fpView.SetActive(shown);
        if (!shown || fpArm == null) return;
        bool chop = interaction != null && interaction.IsChopping;
        float speed = playerBody != null ? new Vector2(playerBody.linearVelocity.x, playerBody.linearVelocity.z).magnitude : 0f;
        fpClock += dt * (chop ? 1f : Mathf.Lerp(.3f, 1f, Mathf.Clamp01(speed / 4f)));
        float swing = 0f;
        if (chop && interaction.AnimationHasScheduledImpact)
        {
            float interval = Mathf.Max(.15f, interaction.hitInterval);
            float phase = Mathf.Clamp01(1f - (interaction.AnimationScheduledImpact - Time.time) / interval);
            swing = Mathf.Sin(phase * Mathf.PI);
        }
        else if (chop) swing = .65f * Mathf.Sin(fpClock * 9f) * Mathf.Sin(fpClock * 9f);
        float bob = VibeSettings.ReduceEffects || chop ? 0f : Mathf.Sin(fpClock * 9f) * .018f * Mathf.Clamp01(speed / 4f);
        fpArm.localPosition = new Vector3(.27f - .13f * swing, -.26f + bob + .12f * swing, .46f + .13f * swing);
        fpArm.localRotation = Quaternion.Euler(-18f - 65f * swing, -17f - 20f * swing, -26f + 12f * swing);
    }

    void FpLeave(bool resetRequest)
    {
        if (resetRequest) fpRequested = false;
        if (!fpLive && fpView == null && fpHidden.Count == 0) return;
        fpLive = false;
        foreach (var pair in fpHidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        fpHidden.Clear();
        if (fpCamera != null) fpCamera.nearClipPlane = fpNear;
        if (fpView != null) { fpView.SetActive(false); Destroy(fpView); }
        fpView = null; fpArm = fpTool = null; fpCamera = null; fpPlayer = null;
        fpMeshes.Clear();
        if (fpRenderSubscribed)
        {
            RenderPipelineManager.beginCameraRendering -= FpBeforeRender;
            RenderPipelineManager.endCameraRendering -= FpAfterRender;
            fpRenderSubscribed = false;
        }
        if (fpLeather != null) Destroy(fpLeather); if (fpSleeve != null) Destroy(fpSleeve);
        if (fpWood != null) Destroy(fpWood); if (fpMetal != null) Destroy(fpMetal);
        fpLeather = fpSleeve = fpWood = fpMetal = null;
        pitch = fpOldPitch;
        snapPending = true;
        resumeLookAt = Time.unscaledTime + lookResumeDelay;
        ReleaseCursor();
    }
}

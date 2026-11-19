using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Leo's close-up arm and equipped axe, rendered separately from the world when URP supports stacking.
internal sealed partial class LeoFirstPersonView
{
    // Layer 30 is unused by the shipped scenes. A named layer takes precedence when configured.
    const int DefaultViewLayer = 30;
    const float ViewFov = 62f;
    const string BaseColor = "_BaseColor";
    const string EmissionColor = "_EmissionColor";
    readonly List<Renderer> renderers = new List<Renderer>(72);
    readonly Dictionary<Material, Material> litMaterials = new Dictionary<Material, Material>();
    readonly List<Material> watchMaterials = new List<Material>(4);
    readonly List<Camera> stack;
    readonly Camera overlay;
    readonly UniversalAdditionalCameraData overlayData;
    readonly List<ScriptableRendererFeature> pausedOcclusion = new List<ScriptableRendererFeature>(1);
    readonly int layer;
    Material nailMaterial;
    Mesh watchMesh, sleeveMesh;
    readonly TrailRenderer stroke;
    float fill, refreshLightingAt;
    readonly Transform root, axe, bakedAxe, mainGrip, supportGrip;
    readonly Transform[] axeModels = new Transform[AxeCatalog.All.Length];
    Transform activeDisplay;
    AxeDefinition activeAxe;
    Vector3 cuttingEdge = new Vector3(0f, .23748f, .28564f);
    readonly Arm left, right;
    bool axeVisible = true;
    readonly Camera camera;
    readonly LeoRig source;
    readonly bool hasGeometry;
    bool visible, rendered;

    sealed class Arm
    {
        internal Transform upper, elbow, forearm, wrist, wristTwist;
        internal SkinnedMeshRenderer hand;
        internal Vector3 gripCenter = LeoFirstPersonHandMesh.GripCenter;
        internal Quaternion upperRest, elbowRest, wristRest;
        internal float reach;

        internal void Prepare(float scale)
        {
            upperRest = upper.localRotation;
            elbowRest = elbow.localRotation;
            wristRest = wrist.localRotation;
            reach = (Vector3.Distance(upper.position, elbow.position) +
                Vector3.Distance(elbow.position, wrist.position)) * .992f / scale;
        }

        internal Quaternion GripWrist(Vector3 grip, Vector3 pole, Vector3 shaft)
        {
            // The opening of the closed hand runs along its local X axis.
            // Roll around the shaft toward the elbow pole while keeping that axis fixed.
            Vector3 neutral = Vector3.ProjectOnPlane(Vector3.forward, shaft);
            if (neutral.sqrMagnitude < .03f) neutral = Vector3.ProjectOnPlane(Vector3.down, shaft);
            Vector3 fingers = Vector3.ProjectOnPlane(grip - pole, shaft);
            fingers += neutral.normalized * .055f;
            if (fingers.sqrMagnitude < .001f) fingers = neutral;
            float roll = Mathf.Clamp(Vector3.SignedAngle(neutral, fingers, shaft), -72f, 72f);
            fingers = Quaternion.AngleAxis(roll, shaft) * neutral;
            return Quaternion.LookRotation(Vector3.Cross(shaft, fingers), -fingers);
        }

        internal void Pose(Transform space, Vector3 goal, Vector3 pole, Quaternion rotation, float close)
        {
            PlaceWrist(space, goal, pole, rotation);
            hand.SetBlendShapeWeight(0, close * 100f);
        }

        void PlaceWrist(Transform space, Vector3 goal, Vector3 pole, Quaternion rotation)
        {
            upper.localRotation = upperRest;
            elbow.localRotation = elbowRest;
            wrist.localRotation = wristRest;
            LeoRig.SolveTwoBone(upper, elbow, wrist, space.TransformPoint(goal), space.TransformPoint(pole),
                space.rotation * rotation, 1f);
            wristTwist.rotation = Quaternion.Slerp(forearm.rotation * wristRest, wrist.rotation, .5f);
        }
    }

    internal static LeoFirstPersonView Create(Camera camera, LeoRig source)
    {
        if (source.upperArmR == null || source.elbowR == null || source.forearmTwistR == null ||
            source.handR == null || source.upperArmL == null || source.elbowL == null ||
            source.forearmTwistL == null || source.handL == null || source.axeRoot == null)
            return null;
        LeoFirstPersonHandMesh shape = LeoFirstPersonHandMesh.Load();
        if (shape == null) return null;
        var view = new LeoFirstPersonView(camera, source, shape);
        if (view.hasGeometry) return view;
        view.Dispose();
        return null;
    }

    LeoFirstPersonView(Camera camera, LeoRig source, LeoFirstPersonHandMesh shape)
    {
        this.camera = camera;
        this.source = source;
        int namedLayer = LayerMask.NameToLayer("Viewmodel");
        layer = namedLayer >= 0 ? namedLayer : DefaultViewLayer;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset &&
            camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData) &&
            cameraData.renderType == CameraRenderType.Base && cameraData.scriptableRenderer is UniversalRenderer)
            stack = cameraData.cameraStack;

        if (stack != null)
        {
            var cameraRoot = NewTransform("Leo Viewmodel Camera", camera.transform);
            cameraRoot.gameObject.SetActive(false);
            overlay = cameraRoot.gameObject.AddComponent<Camera>();
            overlay.enabled = false;
            overlay.fieldOfView = ViewFov;
            overlay.nearClipPlane = .025f;
            overlay.farClipPlane = 6f;
            overlay.useOcclusionCulling = false;
            overlay.cullingMask = 1 << layer;
            overlayData = overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay; // The overlay clears depth before drawing the arm.
            overlayData.renderShadows = false;
            overlayData.renderPostProcessing = false;
            overlayData.requiresDepthOption = CameraOverrideOption.Off;
            overlayData.requiresColorOption = CameraOverrideOption.Off;
            stack.Add(overlay);
            cameraRoot.gameObject.SetActive(true);
        }

        root = NewTransform("Leo First Person (HideFromMap)", camera.transform);
        left = CopyArm(source.upperArmL, source.elbowL, source.forearmTwistL, source.handL, shape, true);
        right = CopyArm(source.upperArmR, source.elbowR, source.forearmTwistR, source.handR, shape, false);
        int beforeAxe = renderers.Count;
        axe = NewTransform("Axe", root);
        mainGrip = NewTransform("Right grip", axe);
        mainGrip.localPosition = LeoAxePose.MainGrip;
        supportGrip = NewTransform("Left support grip", axe);
        supportGrip.localPosition = LeoAxePose.SupportGrip + new Vector3(0f, -.028f, -.005f);
        foreach (var definition in AxeCatalog.All)
        {
            var model = definition.Model;
            if (model == null || !model.TryGetComponent<MeshFilter>(out var filter) ||
                filter.sharedMesh == null || !model.TryGetComponent<MeshRenderer>(out var original)) continue;
            Transform display = NewTransform(definition.Id.ToString(), axe);
            var copy = display.gameObject.AddComponent<MeshRenderer>();
            display.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var materials = original.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = ViewMaterial(materials[i]);
            copy.sharedMaterials = materials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = overlay == null;
            copy.forceRenderingOff = true;
            renderers.Add(copy);
            display.gameObject.SetActive(false);
            axeModels[(int)definition.Id] = display;
        }
        if (axeModels[(int)AxeId.Old] == null)
        {
            bakedAxe = NewTransform("Original axe", axe);
            CopyMeshes(source.axeRoot, bakedAxe, true, includeDisabled: true);
            bakedAxe.gameObject.SetActive(false);
        }
        hasGeometry = renderers.Count > beforeAxe;
        stroke = CreateStroke();
        ReleaseMotion();
    }

    Arm CopyArm(Transform upper, Transform elbow, Transform twist, Transform wrist,
        LeoFirstPersonHandMesh shape, bool mirrored)
    {
        var arm = new Arm();
        arm.upper = CopyBone(upper, root, false);
        CopyMeshes(upper, arm.upper);
        arm.elbow = CopyBone(elbow, arm.upper, false);
        CopyMeshes(elbow, arm.elbow, suffix: "__SkinWarm");
        arm.forearm = CopyBone(twist, arm.elbow);
        arm.wrist = CopyBone(wrist, arm.forearm, false);
        arm.wristTwist = NewTransform("Wrist twist blend", arm.forearm);
        arm.wristTwist.SetLocalPositionAndRotation(arm.wrist.localPosition, arm.wrist.localRotation);
        arm.wristTwist.localScale = arm.wrist.localScale;
        arm.Prepare(root.TransformVector(Vector3.up).magnitude);

        var sourceSkin = wrist.Find("Geo_" + wrist.name + "__Skin");
        Material skin = sourceSkin != null ? sourceSkin.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        if (skin == null)
            skin = elbow.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
        skin = ViewMaterial(skin);
        if (nailMaterial == null && skin != null)
        {
            nailMaterial = new Material(skin) { name = "Leo fingernails (First Person)" };
            if (nailMaterial.HasProperty(BaseColor))
            {
                Color colour = nailMaterial.GetColor(BaseColor);
                nailMaterial.SetColor(BaseColor, Color.Lerp(colour, new Color(.90f, .67f, .56f, 1f), .17f));
            }
        }

        arm.hand = arm.wrist.gameObject.AddComponent<SkinnedMeshRenderer>();
        arm.hand.sharedMesh = shape.Create(arm.forearm.worldToLocalMatrix * arm.wrist.localToWorldMatrix, mirrored);
        arm.hand.bones = new[] { arm.wrist, arm.forearm, arm.wristTwist };
        arm.hand.rootBone = arm.wrist;
        arm.hand.localBounds = arm.hand.sharedMesh.bounds;
        arm.hand.sharedMaterials = new[] { skin, nailMaterial != null ? nailMaterial : skin };
        arm.hand.updateWhenOffscreen = true;
        arm.hand.shadowCastingMode = ShadowCastingMode.Off;
        arm.hand.receiveShadows = overlay == null;
        arm.hand.forceRenderingOff = true;
        renderers.Add(arm.hand);
        AddSleeve(arm, upper, shape);
        if (mirrored && skin != null) AddWatch(arm, shape, skin);
        return arm;
    }

    void AddSleeve(Arm arm, Transform originalUpper, LeoFirstPersonHandMesh shape)
    {
        if (sleeveMesh == null) sleeveMesh = shape.CreateSleeve();
        if (sleeveMesh == null) return;
        string prefix = "Geo_" + originalUpper.name + "__";
        var cloth = originalUpper.Find(prefix + "Jacket")?.GetComponent<MeshRenderer>()?.sharedMaterial;
        if (cloth == null) return;
        var dark = originalUpper.Find(prefix + "JacketDark")?.GetComponent<MeshRenderer>()?.sharedMaterial;
        var light = originalUpper.Find(prefix + "JacketLight")?.GetComponent<MeshRenderer>()?.sharedMaterial;
        AddForearmMesh(arm, "Rolled sleeve and stitching", sleeveMesh,
            new[] { ViewMaterial(cloth), ViewMaterial(dark != null ? dark : cloth), ViewMaterial(light != null ? light : cloth) });
    }

    void AddWatch(Arm arm, LeoFirstPersonHandMesh shape, Material skin)
    {
        watchMesh = shape.CreateWatch();
        if (watchMesh == null) return;
        AddForearmMesh(arm, "Leo trail watch", watchMesh, new[]
        {
            WatchMaterial(skin, "Oiled leather", new Color32(107, 73, 50, 255), .13f, 0f),
            WatchMaterial(skin, "Brushed brass", new Color32(176, 137, 76, 255), .32f, .22f),
            WatchMaterial(skin, "Watch face", new Color32(217, 194, 148, 255), .19f, 0f),
            WatchMaterial(skin, "Dial markings", new Color32(83, 60, 46, 255), .19f, 0f)
        });
    }

    void AddForearmMesh(Arm arm, string name, Mesh mesh, Material[] materials)
    {
        Transform accessory = NewTransform(name, arm.forearm);
        // These pieces sit on the skin bound to the forearm above the wrist pivot.
        accessory.SetLocalPositionAndRotation(arm.wrist.localPosition, arm.wrist.localRotation);
        accessory.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = accessory.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = overlay == null;
        renderer.forceRenderingOff = true;
        renderers.Add(renderer);
    }

    Material WatchMaterial(Material skin, string name, Color colour, float polish, float metal)
    {
        var material = new Material(skin) { name = name, color = colour };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", polish);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal);
        watchMaterials.Add(material);
        return material;
    }

    Transform CopyBone(Transform original, Transform parent, bool copyGeometry = true)
    {
        if (original == null || parent == null) return null;
        Transform bone = NewTransform(original.name, parent);
        bone.localPosition = source.RestPosition(original);
        bone.localRotation = source.RestRotation(original);
        bone.localScale = source.RestScale(original);
        if (copyGeometry) CopyMeshes(original, bone);
        return bone;
    }

    void CopyMeshes(Transform original, Transform parent, bool descendants = false, string suffix = null,
        bool includeDisabled = false)
    {
        foreach (Transform child in original)
        {
            if (!child.gameObject.activeSelf ||
                suffix != null && !child.name.EndsWith(suffix, System.StringComparison.Ordinal)) continue;
            var rr = child.GetComponent<MeshRenderer>();
            bool hasMesh = child.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null &&
                rr != null && (rr.enabled || includeDisabled);
            if (!hasMesh && (!descendants || child.childCount == 0)) continue;
            Transform copy = NewTransform(child.name, parent);
            copy.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
            copy.localScale = child.localScale;
            if (hasMesh)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var renderer = copy.gameObject.AddComponent<MeshRenderer>();
                var materials = rr.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = ViewMaterial(materials[i]);
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = overlay == null;
                renderer.forceRenderingOff = true;
                renderers.Add(renderer);
            }
            if (descendants) CopyMeshes(child, copy, true, includeDisabled: includeDisabled);
        }
    }

    Material ViewMaterial(Material original)
    {
        if (original == null || !original.HasProperty(BaseColor) || !original.HasProperty(EmissionColor))
            return original;
        if (litMaterials.TryGetValue(original, out var material)) return material;
        material = new Material(original) { name = original.name + " (First Person)" };
        material.EnableKeyword("_EMISSION");
        litMaterials.Add(original, material);
        return material;
    }

    void UpdateLighting(float now)
    {
        if (now < refreshLightingAt) return;
        refreshLightingAt = now + .2f;
        float wanted = Mathf.Lerp(.052f, .19f, DayNightCycle.Night01);
        if (Mathf.Abs(wanted - fill) < .003f) return;
        fill = wanted;
        foreach (var pair in litMaterials)
        {
            Color ambient = pair.Key.GetColor(BaseColor) * fill;
            Color original = pair.Key.GetColor(EmissionColor);
            pair.Value.SetColor(EmissionColor, new Color(ambient.r + original.r,
                ambient.g + original.g, ambient.b + original.b, original.a));
        }
        if (nailMaterial != null && nailMaterial.HasProperty(BaseColor) && nailMaterial.HasProperty(EmissionColor))
            nailMaterial.SetColor(EmissionColor, nailMaterial.GetColor(BaseColor) * fill);
        foreach (var material in watchMaterials)
            if (material.HasProperty(BaseColor) && material.HasProperty(EmissionColor))
                material.SetColor(EmissionColor, material.GetColor(BaseColor) * fill);
    }

    TrailRenderer CreateStroke()
    {
        var material = Resources.Load<Material>("LeoAnimation/Leo_AxeStroke");
        if (material == null || material.shader == null || !material.shader.isSupported) return null;
        Transform edge = NewTransform("Axe edge stroke", axe);
        edge.localPosition = cuttingEdge;
        var trail = edge.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.emitting = false;
        trail.forceRenderingOff = true;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.time = .047f;
        trail.minVertexDistance = .011f;
        trail.startWidth = .030f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, .88f, .64f, .24f);
        trail.endColor = new Color(1f, .76f, .48f, 0f);
        renderers.Add(trail);
        return trail;
    }

    void SetAxe(AxeDefinition definition)
    {
        if (activeAxe == definition) return;
        if (activeDisplay != null) activeDisplay.gameObject.SetActive(false);
        activeAxe = definition;

        var chosen = axeModels[(int)definition.Id] ?? axeModels[(int)AxeId.Old];
        activeDisplay = chosen != null ? chosen : bakedAxe;
        if (activeDisplay != null) activeDisplay.gameObject.SetActive(axeVisible);
        var model = chosen != null ? definition.Model ?? AxeCatalog.Get(AxeId.Old).Model : null;
        var edge = model != null ? model.transform.Find("CuttingEdge") : null;
        cuttingEdge = edge != null ? edge.localPosition : LeoAxePose.Edge;

        if (stroke != null)
        {
            stroke.transform.localPosition = cuttingEdge;
            stroke.Clear();
        }
    }

    Transform NewTransform(string name, Transform parent)
    {
        var part = new GameObject(name) { layer = layer };
        part.transform.SetParent(parent, false);
        return part.transform;
    }

    internal void SetAxeVisible(bool show)
    {
        if (axeVisible == show) return;
        axeVisible = show;
        if (activeDisplay != null) activeDisplay.gameObject.SetActive(show);
        if (stroke != null && !show)
        {
            stroke.emitting = false;
            stroke.Clear();
        }
    }

    internal void SetVisible(bool show)
    {
        if (visible == show) return;
        visible = show;
        if (overlay != null) overlay.enabled = visible;
        if (visible) return;
        SetRendered(false);
        if (stroke != null)
        {
            stroke.emitting = false;
            stroke.Clear();
        }
    }

    internal bool RendersIn(Camera rendering) => visible && rendering == (overlay != null ? overlay : camera);

    Vector3 ProjectContact(Vector3 point, bool ground)
    {
        if (overlay == null) return root.InverseTransformPoint(point);
        Vector3 viewport = camera.WorldToViewportPoint(point);
        if (viewport.z <= .075f) return Vector3.zero;
        // Keep the blade on the bark in screen space within the rig's comfortable travel.
        float scale = root.lossyScale.y;
        float depth = Mathf.Clamp(viewport.z / scale + .16f, ground ? .87f : .84f, .97f);
        viewport.z = depth * scale;
        return root.InverseTransformPoint(overlay.ViewportToWorldPoint(viewport));
    }

    internal void SetRendered(bool show)
    {
        if (rendered == show) return;
        rendered = show;
        if (show) PauseOverlayOcclusion();
        else
        {
            foreach (var feature in pausedOcclusion)
                if (feature != null) feature.SetActive(true);
            pausedOcclusion.Clear();
        }
        foreach (var renderer in renderers)
            if (renderer != null) renderer.forceRenderingOff = !show;
    }

    void PauseOverlayOcclusion()
    {
        // The world camera already ran SSAO. The overlay draws only the arm
        // and clears depth; another full-screen AO pass would use that new depth.
        if (overlayData == null || GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset pipeline) return;
        var data = pipeline.rendererDataList;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == null || pipeline.GetRenderer(i) != overlayData.scriptableRenderer) continue;
            foreach (var feature in data[i].rendererFeatures)
            {
                if (feature is not ScreenSpaceAmbientOcclusion || !feature.isActive) continue;
                feature.SetActive(false);
                pausedOcclusion.Add(feature);
            }
            break;
        }
    }

    internal void Dispose()
    {
        SetVisible(false);
        SetRendered(false);
        if (overlay != null)
        {
            stack.Remove(overlay);
            Object.Destroy(overlay.gameObject);
        }
        foreach (Material material in litMaterials.Values)
            if (material != null) Object.Destroy(material);
        litMaterials.Clear();
        foreach (var material in watchMaterials) Object.Destroy(material);
        if (watchMesh != null) Object.Destroy(watchMesh);
        if (sleeveMesh != null) Object.Destroy(sleeveMesh);
        if (nailMaterial != null) Object.Destroy(nailMaterial);
        if (left.hand != null) Object.Destroy(left.hand.sharedMesh);
        if (right.hand != null) Object.Destroy(right.hand.sharedMesh);
        if (root != null)
        {
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }
    }
}

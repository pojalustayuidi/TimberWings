using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// First-person rig with independent hands, Leo's sleeves and a separate equipped model.
internal sealed partial class LeoFirstPersonView
{
    const int ViewLayer = 2; // Ignore Raycast, as in CAM-01.
    const string BaseColor = "_BaseColor";
    const string EmissionColor = "_EmissionColor";
    readonly List<Renderer> renderers = new List<Renderer>(72);
    readonly Dictionary<Material, Material> litMaterials = new Dictionary<Material, Material>();
    readonly Transform compass, compassNeedle;
    Material nailMaterial;
    readonly TrailRenderer stroke;
    float fill, refreshLightingAt;
    readonly Transform root, axe, bakedAxe;
    readonly Transform[] axeModels = new Transform[AxeCatalog.All.Length];
    AxeDefinition activeAxe;
    Vector3 cuttingEdge = new Vector3(0f, .23748f, .28564f);
    readonly Arm right, left;
    readonly Camera camera;
    readonly LeoRig source;
    readonly bool hasGeometry;
    bool rendered;

    sealed class Arm
    {
        internal Transform upper, elbow, forearm, wrist;
        internal SkinnedMeshRenderer hand;
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

        internal Quaternion GripWrist(Transform space, Vector3 grip, Vector3 pole, Quaternion fallback, Vector3 shaft)
        {
            PlaceWrist(space, grip - fallback * LeoFirstPersonHandMesh.GripCenter, pole, fallback);
            Vector3 forearmDirection = space.InverseTransformDirection((wrist.position - elbow.position).normalized);
            Vector3 fingers = Vector3.ProjectOnPlane(forearmDirection, shaft);
            if (fingers.sqrMagnitude < .001f) return fallback;
            // Roll around the handle to follow the forearm while keeping the grip centered on the shaft.
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
        }
    }

    internal static LeoFirstPersonView Create(Camera camera, LeoRig source)
    {
        if (source.upperArmL == null || source.elbowL == null || source.forearmTwistL == null ||
            source.handL == null || source.upperArmR == null || source.elbowR == null ||
            source.forearmTwistR == null || source.handR == null || source.axeRoot == null)
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
        root = NewTransform("Leo First Person (HideFromMap)", camera.transform);
        right = CopyArm(source.upperArmR, source.elbowR, source.forearmTwistR, source.handR, shape, false);
        left = CopyArm(source.upperArmL, source.elbowL, source.forearmTwistL, source.handL, shape, true);
        compass = CopyWristCompass(out compassNeedle);
        int beforeAxe = renderers.Count;
        axe = NewTransform("Axe", root);
        foreach (var definition in AxeCatalog.All)
        {
            var model = definition.Model;
            if (model == null) continue;
            Transform display = NewTransform(definition.Id.ToString(), axe);
            var copy = display.gameObject.AddComponent<MeshRenderer>();
            display.gameObject.AddComponent<MeshFilter>().sharedMesh = model.GetComponent<MeshFilter>().sharedMesh;
            var materials = model.GetComponent<MeshRenderer>().sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = ViewMaterial(materials[i]);
            copy.sharedMaterials = materials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.forceRenderingOff = true;
            renderers.Add(copy);
            display.gameObject.SetActive(false);
            axeModels[(int)definition.Id] = display;
        }
        if (axeModels[(int)AxeId.Old] == null)
        {
            bakedAxe = NewTransform("Original axe", axe);
            CopyMeshes(source.axeRoot, bakedAxe, true);
        }
        hasGeometry = renderers.Count > beforeAxe;
        stroke = CreateStroke();
        ReleaseMotion();
    }

    Arm CopyArm(Transform upper, Transform elbow, Transform twist, Transform wrist,
        LeoFirstPersonHandMesh shape, bool leftHand)
    {
        var arm = new Arm();
        arm.upper = CopyBone(upper, root, false);
        CopyMeshes(upper, arm.upper, suffix: "__Jacket");
        arm.elbow = CopyBone(elbow, arm.upper, false);
        // Leo's sleeve and rolled cuff cover the elbow in the close camera.
        Transform sleeve = NewTransform("Rolled sleeve", arm.elbow);
        sleeve.localPosition = new Vector3(elbow.localPosition.x * -.31f, .024f, -.006f);
        sleeve.localScale = new Vector3(.98f, .56f, .98f);
        CopyMeshes(upper, sleeve);
        arm.forearm = CopyBone(twist, arm.elbow);
        arm.wrist = CopyBone(wrist, arm.forearm, false);
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
                nailMaterial.SetColor(BaseColor, Color.Lerp(colour, new Color(.90f, .67f, .56f, 1f), .27f));
            }
        }

        arm.hand = arm.wrist.gameObject.AddComponent<SkinnedMeshRenderer>();
        arm.hand.sharedMesh = shape.Create(leftHand, arm.forearm.worldToLocalMatrix * arm.wrist.localToWorldMatrix);
        arm.hand.bones = new[] { arm.wrist, arm.forearm };
        arm.hand.rootBone = arm.wrist;
        arm.hand.localBounds = arm.hand.sharedMesh.bounds;
        arm.hand.sharedMaterials = new[] { skin, nailMaterial != null ? nailMaterial : skin };
        arm.hand.updateWhenOffscreen = true;
        arm.hand.shadowCastingMode = ShadowCastingMode.Off;
        arm.hand.receiveShadows = true;
        arm.hand.forceRenderingOff = true;
        renderers.Add(arm.hand);
        return arm;
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

    void CopyMeshes(Transform original, Transform parent, bool descendants = false, string suffix = null)
    {
        foreach (Transform child in original)
        {
            if (!child.gameObject.activeSelf ||
                suffix != null && !child.name.EndsWith(suffix, System.StringComparison.Ordinal)) continue;
            var rr = child.GetComponent<MeshRenderer>();
            bool hasMesh = child.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null &&
                rr != null && rr.enabled;
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
                renderer.receiveShadows = true;
                renderer.forceRenderingOff = true;
                renderers.Add(renderer);
            }
            if (descendants) CopyMeshes(child, copy, true);
        }
    }

    Transform CopyWristCompass(out Transform needle)
    {
        needle = null;
        if (source.compass == null) return null;
        Transform model = NewTransform("Leo's wrist compass", root);
        CopyMeshes(source.compass, model, true);
        needle = source.compassNeedle != null ? model.Find(source.compassNeedle.name) : null;
        int parts = model.childCount;
        Transform face = NewTransform("Compass face", model);
        face.localPosition = Vector3.forward * .024f;
        face.localScale = Vector3.one * .55f;
        for (int i = parts - 1; i >= 0; i--)
        {
            Transform part = model.GetChild(i);
            if (part.name.EndsWith("__LeatherDark", System.StringComparison.Ordinal) ||
                part.name.EndsWith("__LeatherEdge", System.StringComparison.Ordinal))
            {
                part.localScale = new Vector3(1.12f, .70f, 1.12f);
                part.localPosition *= .70f;
            }
            else part.SetParent(face, false);
        }
        return model;
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
        float wanted = Mathf.Lerp(.028f, .20f, DayNightCycle.Night01);
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
        if (activeAxe != null && axeModels[(int)activeAxe.Id] != null)
            axeModels[(int)activeAxe.Id].gameObject.SetActive(false);
        activeAxe = definition;

        var chosen = axeModels[(int)definition.Id] ?? axeModels[(int)AxeId.Old];
        if (bakedAxe != null) bakedAxe.gameObject.SetActive(chosen == null);
        // Disable the fallback too when changing from a missing model to an imported one.
        var old = axeModels[(int)AxeId.Old];
        if (old != null && old != chosen) old.gameObject.SetActive(false);
        if (chosen != null)
        {
            chosen.gameObject.SetActive(true);
            cuttingEdge = (definition.Model ?? AxeCatalog.Get(AxeId.Old).Model)
                .transform.Find("CuttingEdge").localPosition;
        }
        else cuttingEdge = new Vector3(0f, .23748f, .28564f);

        if (stroke != null)
        {
            stroke.transform.localPosition = cuttingEdge;
            stroke.Clear();
        }
    }

    static Transform NewTransform(string name, Transform parent)
    {
        var part = new GameObject(name) { layer = ViewLayer };
        part.transform.SetParent(parent, false);
        return part.transform;
    }

    internal void SetRendered(bool show)
    {
        if (rendered == show) return;
        rendered = show;
        foreach (var renderer in renderers)
            if (renderer != null) renderer.forceRenderingOff = !show;
    }

    internal void Dispose()
    {
        SetRendered(false);
        foreach (Material material in litMaterials.Values)
            if (material != null) Object.Destroy(material);
        litMaterials.Clear();
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

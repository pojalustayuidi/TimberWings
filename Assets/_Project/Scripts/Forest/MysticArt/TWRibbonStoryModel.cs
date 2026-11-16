using UnityEngine;

// The scene-authored ribbon uses GPU vertex motion. Legacy scenes can still
// use the two wooden-marker pivots serialized by the older baker.
// The original MysticProp, DialogueSequence, StoryInspection and save identity stay untouched.
[DisallowMultipleComponent, DefaultExecutionOrder(500)]
public sealed class TWRibbonStoryModel : MonoBehaviour
{
    [SerializeField] Transform shortTail;
    [SerializeField] Transform longTail;
    [SerializeField] Renderer[] originalRenderers;
    [SerializeField] bool[] originalStates;
    [SerializeField] bool capturedOriginals;

    public const string BakedClothName = "Ribbon_Cloth";

    static readonly int WindAmount = Shader.PropertyToID("_ClothWindAmount");
    Renderer cloth;
    MaterialPropertyBlock wind;
    float appliedWind = -1f;

    public bool HasAuthoredCloth => longTail != null && longTail.name == BakedClothName;
    public bool HasVisual => longTail != null && (HasAuthoredCloth || shortTail != null);
    public bool HasCapturedOriginals => capturedOriginals;

    public void CaptureOriginals(Renderer[] renderers)
    {
        if (capturedOriginals) return;
        originalRenderers = renderers ?? new Renderer[0];
        originalStates = new bool[originalRenderers.Length];
        for (int i = 0; i < originalRenderers.Length; i++)
            originalStates[i] = originalRenderers[i] != null && originalRenderers[i].enabled;
        capturedOriginals = true;
    }

    public void Configure(Transform shorter, Transform longer)
    {
        shortTail = shorter;
        longTail = longer;
    }

    public void HideOriginals()
    {
        if (originalRenderers == null) return;
        foreach (var original in originalRenderers)
            if (original != null) original.enabled = false;
    }

    public void RestoreOriginals()
    {
        if (originalRenderers == null || originalStates == null) return;
        for (int i = 0; i < originalRenderers.Length && i < originalStates.Length; i++)
            if (originalRenderers[i] != null) originalRenderers[i].enabled = originalStates[i];
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        if (!HasVisual) return;
        float strength = VibeSettings.ReduceEffects ? .20f : 1f;
        if (HasAuthoredCloth)
        {
            if (appliedWind == strength) return;
            if (cloth == null) cloth = longTail.GetComponent<Renderer>();
            if (cloth == null) return;
            if (wind == null) wind = new MaterialPropertyBlock();
            cloth.GetPropertyBlock(wind);
            wind.SetFloat(WindAmount, strength);
            cloth.SetPropertyBlock(wind);
            appliedWind = strength;
            return;
        }

        float t = Time.time;
        shortTail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.32f + .7f) * .45f * strength,
            Mathf.Sin(t * .79f) * .60f * strength, Mathf.Sin(t * 1.09f) * .65f * strength);
        longTail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.07f) * .55f * strength,
            Mathf.Sin(t * .92f + .5f) * .75f * strength, Mathf.Sin(t * 1.24f + .2f) * .85f * strength);
    }

    void OnDisable()
    {
        if (shortTail != null) shortTail.localRotation = Quaternion.identity;
        if (longTail != null && !HasAuthoredCloth) longTail.localRotation = Quaternion.identity;
        appliedWind = -1f;
    }
}

using UnityEngine;

// Visual-only V5 story ribbon marker. Preserves the authored Ribbon_Memory encounter.
// The original MysticProp, DialogueSequence, StoryInspection and save identity stay untouched.
[DisallowMultipleComponent, DefaultExecutionOrder(500)]
public sealed class TWRibbonStoryModel : MonoBehaviour
{
    [SerializeField] Transform shortTail;
    [SerializeField] Transform longTail;
    [SerializeField] Renderer[] originalRenderers;
    [SerializeField] bool[] originalStates;
    [SerializeField] bool capturedOriginals;

    Transform obsoleteRuntimeRibbon;

    public bool HasVisual => shortTail != null && longTail != null;
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
        obsoleteRuntimeRibbon = null;
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

        // StoryInspectionProp in the current build creates its old flat ribbon at Start.
        // Hide that visual so the existing cutscene uses only our new authored geometry.
        // Other inspection types and their movable meshes are not affected.
        if (obsoleteRuntimeRibbon == null)
            obsoleteRuntimeRibbon = transform.Find("Inspection visual · Ribbon");
        if (obsoleteRuntimeRibbon == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Inspection visual") && child.name.EndsWith("Ribbon"))
                {
                    obsoleteRuntimeRibbon = child;
                    break;
                }
            }
        }
        if (obsoleteRuntimeRibbon != null && obsoleteRuntimeRibbon.gameObject.activeSelf)
            obsoleteRuntimeRibbon.gameObject.SetActive(false);

        if (!HasVisual) return;
        float t = Time.time;
        float strength = VibeSettings.ReduceEffects ? .20f : 1f;
        shortTail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.32f + .7f) * .45f * strength,
            Mathf.Sin(t * .79f) * .60f * strength, Mathf.Sin(t * 1.09f) * .65f * strength);
        longTail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.07f) * .55f * strength,
            Mathf.Sin(t * .92f + .5f) * .75f * strength, Mathf.Sin(t * 1.24f + .2f) * .85f * strength);
    }

    void OnDisable()
    {
        if (shortTail != null) shortTail.localRotation = Quaternion.identity;
        if (longTail != null) longTail.localRotation = Quaternion.identity;
        obsoleteRuntimeRibbon = null;
    }
}

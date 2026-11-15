using UnityEngine;

// Tiny ambient movement for book ornaments; respects Settings > ReduceEffects.
// Attach only to non-interactive decorations, never to buttons.
public sealed class TWJournalAmbientMotion : MonoBehaviour
{
    [Range(0f, 12f)] public float verticalPixels = 2f;
    [Range(0f, 6f)] public float rotationDegrees = 1f;
    [Range(0f, .05f)] public float scaleAmplitude = .007f;
    [Range(.05f, 4f)] public float cyclesPerSecond = .18f;
    public float phase;
    [Range(0f, 1f)] public float opacityBase = 1f;
    [Range(0f, .5f)] public float opacityAmplitude;
    CanvasGroup group;

    RectTransform rect;
    Vector2 initialPosition;
    Quaternion initialRotation;
    Vector3 initialScale;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = transform as RectTransform;
        if (rect != null) initialPosition = rect.anchoredPosition;
        initialRotation = transform.localRotation;
        initialScale = transform.localScale;
    }

    void OnDisable() { ResetToBase(); }

    void ResetToBase()
    {
        if (rect != null) rect.anchoredPosition = initialPosition;
        transform.localRotation = initialRotation;
        transform.localScale = initialScale;
        if (group != null) group.alpha = opacityBase;
    }

    // Used by the loading screen: it clones the menu canvas in the middle of an animation cycle.
    // Awake has just captured the already animated pose as "base", so subtract the current offset
    // to get the true base back. The phase stays continuous, the ornaments do not jump.
    public void RebaseFromAnimatedState()
    {
        if (VibeSettings.ReduceEffects) return;
        float p = Time.unscaledTime * Mathf.PI * 2f * cyclesPerSecond + phase;
        float wave = Mathf.Sin(p);
        if (rect != null) initialPosition = rect.anchoredPosition - Vector2.up * (verticalPixels * wave);
        initialRotation = transform.localRotation * Quaternion.Euler(0f, 0f, -rotationDegrees * Mathf.Sin(p + .7f));
        initialScale = transform.localScale / (1f + scaleAmplitude * wave);
    }

    void Update()
    {
        if (VibeSettings.ReduceEffects) { ResetToBase(); return; }
        float p = Time.unscaledTime * Mathf.PI * 2f * cyclesPerSecond + phase;
        float wave = Mathf.Sin(p);
        if (rect != null) rect.anchoredPosition = initialPosition + Vector2.up * (verticalPixels * wave);
        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, rotationDegrees * Mathf.Sin(p + .7f));
        transform.localScale = initialScale * (1f + scaleAmplitude * wave);
        if (group != null) group.alpha = Mathf.Clamp01(opacityBase + opacityAmplitude * wave);
    }
}

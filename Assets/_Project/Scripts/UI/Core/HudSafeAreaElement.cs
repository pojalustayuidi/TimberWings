using UnityEngine;
[RequireComponent(typeof(RectTransform))]
public class HudSafeAreaElement : MonoBehaviour
{
    void OnEnable() => HudSafeAreaRegistry.Instance?.Register((RectTransform)transform);
    void OnDisable() { if (HudSafeAreaRegistry.Instance != null) HudSafeAreaRegistry.Instance.Unregister((RectTransform)transform); }
}
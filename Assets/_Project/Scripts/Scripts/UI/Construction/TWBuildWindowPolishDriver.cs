// TimberWings — visual-only tick driver for the existing BuildWindow.
// Separate MonoBehaviour avoids CS0111 duplicate LateUpdate in partial BuildWindow.
using UnityEngine;

[DefaultExecutionOrder(950)]
public sealed class TWBuildWindowPolishDriver : MonoBehaviour
{
    static TWBuildWindowPolishDriver instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("TW BuildWindow Polish Driver");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TWBuildWindowPolishDriver>();
    }

    void LateUpdate()
    {
        var window = BuildWindow.Instance;
        if (window != null) window.TWV2VisualTick();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

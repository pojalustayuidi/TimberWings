// Assets/_Project/Scripts/Forest/ChopGrove/GroveChopBlock.cs (НЕ в Editor)
// Пень лесоруба с топором на поляне: E → разлетаются щепки и появляется реплика-тост.
// Создаётся ChopGroveBuilder (interactiveStump). Топор ничего не рубит и ресурсов не даёт, это живая деталь поляны.
using UnityEngine;

public class GroveChopBlock : MonoBehaviour, IInteractable
{
    public string title = "Пень лесоруба";
    public string prompt = "тряхнуть топор";
    public float radius = 2.8f;
    public Color toastColor = new Color(0.98f, 0.78f, 0.38f);
    public string[] lines =
    {
        "ТОПОР ЗАТОЧЕН",
        "ЛЕС ЖДЁТ РАБОТЫ",
        "ЗДЕСЬ РУБИЛИ ЕЩЁ ДЕДЫ",
        "ЩЕПКИ ПАХНУТ СМОЛОЙ",
        "РУКИ ПОМНЯТ ЛЮБОЕ ДЕЛО"
    };

    int next = -1;

    void OnEnable() { Interactables.Register(this); }
    void OnDisable() { Interactables.Unregister(this); }

    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + Vector3.up * 1.5f; } }
    public string Prompt { get { return prompt; } }
    public float Radius { get { return radius; } }
    public bool CanInteract { get { return true; } }

    public void Interact()
    {
        if (next < 0) next = Random.Range(0, lines.Length);
        string line = lines[next % lines.Length];
        next = (next + 1) % lines.Length;
        GroveChips.Burst(transform.position + Vector3.up * 0.1f);
        ZoneToast.Show(title, line, toastColor);
    }
}

// Горсть щепок: одна общая система частиц на всю игру, спрайт квадрат из VibeGfx.
public static class GroveChips
{
    static ParticleSystem ps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ps = null; }

    public static void Burst(Vector3 pos)
    {
        if (VibeSettings.ReduceEffects) return;
        if (ps == null && !Create()) return;
        ps.transform.position = pos;
        ps.Emit(16);
    }

    static bool Create()
    {
        Material mat = VibeGfx.ParticleMat(VibeGfx.Square);
        if (mat == null) return false;
        var go = new GameObject("GroveChips");
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.76f, 0.48f), new Color(0.76f, 0.52f, 0.28f));
        main.gravityModifier = 1.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38f; sh.radius = 0.15f;
        sh.rotation = new Vector3(-90f, 0f, 0f);   // вверх
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Object.DontDestroyOnLoad(go);
        return true;
    }
}

// Assets/_Project/Scripts/Build/Hut/BuildWorkPose.cs   (НЕ в папке Editor!)
// Пока идёт стройка, герой не стоит истуканом: включается рабочая анимация (та же «рубка/молоток», параметр isChopping)
// и он плавно поворачивается лицом к стройке. Вызывает BuildingController (BeginBuilding / StopBuilding / CompleteStep):
//   BuildWorkPose.Hold(inside, стройка.transform, true);   // начали
//   BuildWorkPose.Hold(null, null, false);                  // закончили / убежал
// Компонент сам вешается на героя при первом вызове.
// Порядок выполнения 900: PlayerInteraction.StopChop сбрасывает isChopping каждый кадр, когда рядом нет дерева,
// поэтому мы пишем флаг ПОСЛЕ него, иначе анимация мигала бы.
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(900)]
public class BuildWorkPose : MonoBehaviour
{
    static BuildWorkPose inst;

    [Tooltip("Градусов в секунду: как быстро герой поворачивается к стройке")]
    public float turnSpeed = 420f;

    Animator anim;
    string param = "isChopping";
    bool hasParam, hasMoving;
    Transform focus;
    Rigidbody rb;
    PlayerInteraction interaction;
    PlayerMovement movement;
    bool on, leoActive;

    public static bool TryGetFocus(PlayerMovement player, out Transform target)
    {
        target = null;
        if (inst == null || !inst.on || player == null || inst.gameObject != player.gameObject) return false;
        target = inst.focus;
        return target != null;
    }

    public static void Hold(Component player, Transform focus, bool on)
    {
        if (on)
        {
            if (player == null) return;
            var pi = player.GetComponentInParent<PlayerInteraction>();
            GameObject go = pi != null ? pi.gameObject : player.gameObject;
            var b = go.GetComponent<BuildWorkPose>();
            if (b == null) b = go.AddComponent<BuildWorkPose>();
            b.Begin(pi, focus);
            inst = b;
        }
        else if (inst != null)
        {
            inst.End();
        }
    }

    void Begin(PlayerInteraction pi, Transform f)
    {
        anim = (pi != null && pi.animator != null) ? pi.animator : GetComponentInChildren<Animator>();
        if (pi != null && !string.IsNullOrEmpty(pi.chopBool)) param = pi.chopBool;
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        interaction = pi;
        var presentation = GetComponentInChildren<LeoModelPresentation>();
        leoActive = presentation != null && presentation.isActiveAndEnabled;
        focus = f;
        hasParam = false;
        hasMoving = false;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Bool) continue;
                if (string.Equals(p.name, param, System.StringComparison.OrdinalIgnoreCase)) { param = p.name; hasParam = true; }
                if (p.name == "IsMoving") hasMoving = true;
            }
        }
        if (!hasParam && !leoActive) Debug.LogWarning("[BuildWorkPose] В аниматоре героя нет bool-параметра «" + param + "»: рабочая анимация не включится.", this);
        on = true;
    }

    void End()
    {
        if (!on) return;
        on = false;
        focus = null;
        if (!leoActive && anim != null && hasParam) anim.SetBool(param, false);
    }

    void Update()
    {
        if (!on) return;
        if (leoActive && (interaction != null && interaction.IsChopping ||
            DialogueSequence.FreezesPlayer || movement != null && movement.ControlLocked)) return;
        if (!leoActive && anim != null && hasParam) anim.SetBool(param, true);

        // лицом к стройке, но только пока герой стоит (если пошёл, не крутим)
        if (focus == null) return;
        if (hasMoving && anim != null && anim.GetBool("IsMoving")) return;
        Vector3 d = focus.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
        if (leoActive && movement != null && movement.model != null && movement.model != transform)
        {
            if (Mathf.Abs(MobileInput.Horizontal) > .03f || Mathf.Abs(MobileInput.Vertical) > .03f) return;
            Transform visual = movement.model;
            visual.rotation = Quaternion.RotateTowards(visual.rotation, want, turnSpeed * Time.deltaTime);
            return;
        }
        Quaternion q = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        if (rb != null && !rb.isKinematic) rb.MoveRotation(q); else transform.rotation = q;
    }

    void OnDisable() { End(); }
}

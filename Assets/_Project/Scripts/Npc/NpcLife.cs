// Assets/_Project/Scripts/Npc/NpcLife.cs (НЕ в Editor)
// «Оживляет» NPC (Миру и других): дыхание, взгляд головой на игрока, приветствие при подходе,
// кивки во время разговора, радость после разговора.
// Работает и без анимаций (процедурно), а если в Animator есть параметры Talking (bool), Wave (trigger), Cheer (trigger),
// то ещё и включает их. Лишних параметров не требует: чего нет в контроллере, то тихо пропускается.
// Вешается на корень NPC (рядом со Speaker / DialogueSequence). Меню: Tools > BuilderSim > Мир и персонаж > Оживить NPC.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcLife : MonoBehaviour
{
    [Header("Ссылки (пусто = найдутся сами)")]
    public Animator animator;
    [Tooltip("Модель, которую покачиваем (дыхание, подпрыгивание). Пусто = Animator или этот объект")]
    public Transform model;
    [Tooltip("Голова. Пусто = кость Head у гуманоида или объект с именем Head в иерархии")]
    public Transform head;
    public DialogueSequence dialogue;

    [Header("Дыхание")]
    public float breathAmount = 0.012f;
    public float breathSpeed = 1.6f;

    [Header("Взгляд на игрока")]
    public bool lookAtPlayer = true;
    public float lookRadius = 6f;
    public float maxHeadYaw = 55f;
    public float maxHeadPitch = 20f;
    public float lookSpeed = 3f;
    [Tooltip("На какой высоте над ногами игрока «глаза» (куда смотрит NPC)")]
    public float playerEyeHeight = 1.5f;

    [Header("Приветствие")]
    public bool greet = true;
    public float greetRadius = 5f;
    public float greetCooldown = 30f;

    [Header("Разговор")]
    public float talkNod = 0.03f;
    public float talkNodSpeed = 7f;
    public float hopHeight = 0.18f;
    public float hopTime = 0.45f;

    [Header("Имена параметров Animator")]
    public string talkingParam = "Talking";
    public string waveParam = "Wave";
    public string cheerParam = "Cheer";

    // события для NpcBanter и других скриптов
    public event System.Action Greeted;
    public event System.Action DialogueEnded;

    // ---- внутреннее
    readonly HashSet<string> boolParams = new HashSet<string>();
    readonly HashSet<string> triggerParams = new HashSet<string>();

    Transform player;
    float findTimer;
    Vector3 baseLocalPos;
    Vector3 baseLocalScale;
    float curYaw, curPitch;
    Quaternion headBaseLocal;       // поза головы без нашего поворота
    Quaternion headLastApplied;     // что мы сами выставили в прошлом кадре
    bool headHasApplied;
    float talkWeight;
    float hopT = -1f;
    float lastGreet = -999f;
    bool wasNear;
    bool wasPlaying;

    void Awake()
    {
        if (dialogue == null) dialogue = GetComponentInParent<DialogueSequence>();
        if (dialogue == null) dialogue = GetComponentInChildren<DialogueSequence>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (model == null) model = animator != null ? animator.transform : transform;
        if (model == transform && animator == null)
        {
            // новая модель Миры (MiraModelBuilder) стоит дочерним объектом MiraModel
            var mm = transform.Find("MiraModel");
            if (mm != null) model = mm;
            else if (transform.childCount > 0) model = transform.GetChild(0);
        }

        // без этого Animator может «утаскивать» NPC с места, пока играет анимация
        if (animator != null) animator.applyRootMotion = false;

        FindHead();
        CacheParams();

        baseLocalPos = model.localPosition;
        baseLocalScale = model.localScale;
    }

    void OnDisable()
    {
        if (head != null && headHasApplied && Quaternion.Angle(head.localRotation, headLastApplied) < 0.01f)
            head.localRotation = headBaseLocal;
        headHasApplied = false;
        // вернуть модель в исходную позу, чтобы не остаться «приподнятым» после выключения
        if (model != null)
        {
            model.localPosition = baseLocalPos;
            model.localScale = baseLocalScale;
        }
    }

    void FindHead()
    {
        if (head != null) return;
        if (animator != null && animator.isHuman)
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head != null) return;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == "head" || n.EndsWith(":head") || n.EndsWith("_head") || n.EndsWith(".head")) { head = t; break; }
        }
    }

    void CacheParams()
    {
        boolParams.Clear();
        triggerParams.Clear();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool) boolParams.Add(p.name);
            else if (p.type == AnimatorControllerParameterType.Trigger) triggerParams.Add(p.name);
        }
    }

    void SetBool(string name, bool v)
    {
        if (animator != null && !string.IsNullOrEmpty(name) && boolParams.Contains(name)) animator.SetBool(name, v);
    }

    void Fire(string name)
    {
        if (animator != null && !string.IsNullOrEmpty(name) && triggerParams.Contains(name)) animator.SetTrigger(name);
    }

    bool FindPlayer()
    {
        if (player != null) return true;
        findTimer -= Time.deltaTime;
        if (findTimer > 0f) return false;
        findTimer = 0.5f;
        var pm = PlayerMovement.Instance;
        if (pm != null) player = pm.transform;
        return player != null;
    }

    public void Hop() { hopT = 0f; }

    void Update()
    {
        bool playing = dialogue != null && dialogue.IsPlaying;

        // разговор начался / закончился
        if (playing != wasPlaying)
        {
            SetBool(talkingParam, playing);
            if (!playing) { Fire(cheerParam); Hop(); if (DialogueEnded != null) DialogueEnded(); }
            wasPlaying = playing;
        }

        // приветствие: один раз при входе в радиус, потом отдыхает greetCooldown секунд
        if (greet && FindPlayer())
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            bool near = d.sqrMagnitude <= greetRadius * greetRadius;
            if (near && !wasNear && !playing && Time.time - lastGreet >= greetCooldown)
            {
                lastGreet = Time.time;
                Fire(waveParam);
                Hop();
                if (Greeted != null) Greeted();
            }
            wasNear = near;
        }

        talkWeight = Mathf.MoveTowards(talkWeight, playing ? 1f : 0f, Time.deltaTime * 4f);

        // процедурное движение модели: дыхание + кивки при разговоре + подпрыгивание
        if (model != null)
        {
            float t = Time.time;
            float breath = Mathf.Sin(t * breathSpeed) * breathAmount;
            model.localScale = new Vector3(baseLocalScale.x, baseLocalScale.y * (1f + breath), baseLocalScale.z);

            float y = Mathf.Sin(t * talkNodSpeed) * talkNod * talkWeight;
            if (hopT >= 0f)
            {
                hopT += Time.deltaTime;
                float k = hopT / Mathf.Max(0.05f, hopTime);
                if (k >= 1f) hopT = -1f;
                else y += Mathf.Sin(k * Mathf.PI) * hopHeight;
            }
            model.localPosition = baseLocalPos + new Vector3(0f, y, 0f);
        }
    }

    // голову поворачиваем в LateUpdate: Animator к этому моменту уже выставил позу, мы добавляем поверх
    void LateUpdate()
    {
        if (!lookAtPlayer || head == null || model == null || !FindPlayer()) return;

        float targetYaw = 0f, targetPitch = 0f;
        Vector3 eye = player.position + Vector3.up * playerEyeHeight;
        Vector3 to = eye - head.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);

        if (flat.sqrMagnitude <= lookRadius * lookRadius && flat.sqrMagnitude > 0.01f)
        {
            Vector3 fwd = model.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                float yaw = Vector3.SignedAngle(fwd, flat, Vector3.up);
                // игрок сзади (за пределом поворота головы): смотрим прямо, а не выкручиваем шею
                if (Mathf.Abs(yaw) <= maxHeadYaw + 40f)
                {
                    targetYaw = Mathf.Clamp(yaw, -maxHeadYaw, maxHeadYaw);
                    float pitch = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                    targetPitch = Mathf.Clamp(pitch, -maxHeadPitch, maxHeadPitch);
                }
            }
        }

        float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);
        curYaw = Mathf.Lerp(curYaw, targetYaw, k);
        curPitch = Mathf.Lerp(curPitch, targetPitch, k);

        // Если поза головы осталась нашей (никто её не трогал), берём сохранённую базу, а не текущую:
        // иначе поворот накладывается каждый кадр заново и голова крутится без остановки.
        // Если позу выставил кто-то другой (Animator, MiraRig), берём его позу как новую базу.
        Quaternion cur = head.localRotation;
        if (!headHasApplied || Quaternion.Angle(cur, headLastApplied) > 0.01f) headBaseLocal = cur;

        Quaternion parentRot = head.parent != null ? head.parent.rotation : Quaternion.identity;
        Quaternion baseWorld = parentRot * headBaseLocal;

        // поворот в мировых осях: не зависит от того, как повёрнуты оси кости головы в конкретном риге
        Quaternion offset = Quaternion.AngleAxis(curYaw, Vector3.up) * Quaternion.AngleAxis(curPitch, model.right);
        head.rotation = offset * baseWorld;

        headLastApplied = head.localRotation;
        headHasApplied = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, greetRadius);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, lookRadius);
    }
}

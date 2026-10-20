// Assets/_Project/Scripts/Workers/WorkerCamp.cs (НЕ в Editor)
// «Лагерь рабочих»: точка у палатки, где по E открывается окно «Лагерь» (WorkersWindow: Бригада, Паки, Коллекция, склад).
// Вешается на объект палатки (где TentStages). Меню: Tools > BuilderSim > Настроить рабочих (WorkerManager + лагерь) сделает это само.
//   - открывается, когда палатка достроена (StoryFlow.tent.IsFinished; без StoryFlow смотрит TentStages.Stage >= 2;
//     если нет ни того ни другого, открыто всегда, чтобы можно было проверить на пустой сцене);
//   - сама ничего не нанимает: рабочих и доход ведёт WorkerManager, рисует окно WorkersWindow; табличку склада над палаткой даёт WorkerCampSign;
//   - requireTentBuilt = false: открыть лагерь без проверки палатки (для теста).
using UnityEngine;

public class WorkerCamp : MonoBehaviour, IInteractable, IPriorityInteractable
{
    [Header("Доступность")]
    public float radius = 3.5f;
    [Tooltip("true = лагерь открывается, когда палатка достроена")]
    public bool requireTentBuilt = true;
    public string prompt = "нанять рабочих";
    public Vector3 promptOffset = new Vector3(0f, 2.6f, 0f);

    [Header("Всплывающий текст")]
    public Color hireColor = new Color32(0x6B, 0xCB, 0x5A, 255);

    TentStages tentStages;
    Transform player;
    PlayerMovement pm;
    float findAt;

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return transform.position + promptOffset; } }
    public string Prompt { get { return prompt; } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }

    public bool CanInteract
    {
        get { return Unlocked && WorkerManager.Instance != null && FindPlayer(); }
    }

    public bool Unlocked
    {
        get
        {
            if (!requireTentBuilt) return true;
            var story = StoryFlow.Instance;
            if (story != null && story.tent != null) return story.tent.IsFinished;
            if (tentStages != null) return tentStages.Stage >= 2;
            return true;
        }
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake()
    {
        tentStages = GetComponentInParent<TentStages>();
        if (GetComponent<WorkerCampSign>() == null) gameObject.AddComponent<WorkerCampSign>();   // табличка с остатками склада над палаткой
    }
    void OnEnable() { Interactables.Register(this); }
    void OnDisable() { Interactables.Unregister(this); }

    bool FindPlayer()
    {
        if (pm != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;
        pm = PlayerMovement.Instance;
        if (pm == null) return false;
        player = pm.transform;
        return true;
    }

    // расстояние до игрока по земле (окно закрывается, если отойти дальше radius × 1,5)
    public float DistanceToPlayer()
    {
        if (!FindPlayer() || player == null) return float.MaxValue;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // ------------------------------------------------------------ действия
    public void Interact()
    {
        if (!CanInteract) return;
        WorkersWindow.Toggle(this);   // E открывает окно, а если оно открыто, закрывает
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.43f, 0.80f, 0.35f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, radius);
    }
}

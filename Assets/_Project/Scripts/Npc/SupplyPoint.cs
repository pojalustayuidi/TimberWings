// Assets/_Project/Scripts/Npc/SupplyPoint.cs (НЕ в Editor)
// «Отдай бревна NPC»: E у Миры забирает бревна из рюкзака, за каждое игрок получает опыт.
// Добавляется на Миру сам (StoryFlow, глава «Припасы»), вручную вешать не нужно.
//   Begin(нужно, опыт за бревно)  открывает приём
//   delivered                     сколько уже отдано
// Если в рюкзаке пусто, объект не мешает: E достаётся рубке и разговору.
using UnityEngine;

public class SupplyPoint : MonoBehaviour, IInteractable, IPriorityInteractable
{
    public float radius = 3f;
    public Color popupColor = new Color32(0x6B, 0xCB, 0x5A, 255);

    [Header("Только для просмотра")]
    public bool active;
    public int need;
    public int delivered;
    public int xpPerLog;

    Speaker own;
    PlayerInventory inv;
    PlayerStats stats;
    float findAt;

    public void Begin(int logsNeeded, int xpForEachLog)
    {
        need = Mathf.Max(1, logsNeeded);
        xpPerLog = Mathf.Max(0, xpForEachLog);
        delivered = 0;
        active = true;
    }

    public void End() { active = false; }

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return own != null ? own.BubblePoint + Vector3.up * 0.5f : transform.position + new Vector3(0f, 2.9f, 0f); } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }
    public string Prompt { get { return "отдать бревна"; } }

    public bool CanInteract
    {
        get { return active && delivered < need && FindPlayer() && inv.woodCount > 0; }
    }

    void Awake() { own = GetComponent<Speaker>(); }
    void OnEnable() { Interactables.Register(this); }
    void OnDisable() { Interactables.Unregister(this); }

    bool FindPlayer()
    {
        if (inv != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;
        var pm = PlayerMovement.Instance;
        if (pm == null) return false;
        inv = pm.GetComponent<PlayerInventory>();
        stats = pm.GetComponent<PlayerStats>();
        return inv != null;
    }

    public void Interact()
    {
        if (!CanInteract) return;

        int n = Mathf.Min(inv.woodCount, need - delivered);
        inv.RemoveWood(n);
        delivered += n;

        if (RewardPopups.Instance != null)
            RewardPopups.Instance.ShowText(PromptPoint, "Отдано " + n + " " + UiDraw.Plural(n, "бревно", "бревна", "бревен"), popupColor, 46f);
        if (stats != null && xpPerLog > 0) stats.AddXP(n * xpPerLog);
    }
}

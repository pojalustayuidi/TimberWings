// Assets/_Project/Scripts/Npc/TradingStall.cs (НЕ в Editor)
// Торговля у NPC-торговца (GDD: бревно 2 монеты, доска 6 монет, цена × «Торг» из прокачки).
// Вешается на NPC-торговца у прилавка (не на Миру). Сам ничего не продаёт: E открывает окно SellWindow.
//   - прилавок закрыт, пока не построен рынок (глава StoryFlow «Рынок»); без StoryFlow в сцене открывается после вступления;
//   - E открывает окно продажи, даже если рюкзак пуст (окно скажет «Нечего продавать»);
//   - сама продажа, резерв досок и монеты: UI/SellWindow.cs; цены: Core/Pricing.cs.
using System;
using UnityEngine;

public class TradingStall : MonoBehaviour, IInteractable, IPriorityInteractable
{
    [Header("Доступность")]
    public float radius = 3f;
    [Tooltip("true = прилавок открывается после вступления (Мира дала награду за костёр)")]
    public bool requireIntroDone = true;
    public string prompt = "торговать";
    public Vector3 promptOffset = new Vector3(0f, 2.9f, 0f);

    [Header("Всплывающий текст")]
    public Color coinColor = new Color32(0xF5, 0xC5, 0x42, 255);

    // продажа состоялась (квест «Первая продажа»). Событие можно вызвать только внутри класса, снаружи это делает ReportSold
    public static event Action Sold;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Sold = null; }

    Speaker own;
    DialogueSequence talk;
    PlayerInventory inv;
    Transform player;
    float findAt;

    // ------------------------------------------------------------ IInteractable
    public Transform Anchor { get { return transform; } }
    public Vector3 PromptPoint { get { return own != null ? own.BubblePoint + Vector3.up * 0.5f : transform.position + promptOffset; } }
    public string Prompt { get { return prompt; } }
    public float Radius { get { return radius; } }
    public bool Priority { get { return true; } }

    // рюкзак не проверяем: окно можно открыть и с пустым рюкзаком.
    // Пока у торговца есть несыгранный разговор (знакомство из StoryFlow), E принадлежит ему: иначе окно перехватило бы E,
    // потому что оба компонента стоят на одном объекте и одинаково близко
    public bool CanInteract
    {
        get
        {
            if (!Unlocked || !FindPlayer()) return false;
            if (talk != null && talk.CanInteract) return false;
            return true;
        }
    }

    public bool Unlocked
    {
        get
        {
            if (!requireIntroDone) return true;
            var story = StoryFlow.Instance;
            if (story != null) return story.MarketOpen;
            var intro = IntroFlow.Instance;
            return intro == null || intro.Current == IntroFlow.Step.Done;
        }
    }

    // ------------------------------------------------------------ жизненный цикл
    void Awake() { own = GetComponent<Speaker>(); talk = GetComponent<DialogueSequence>(); }
    void OnEnable() { Interactables.Register(this); }
    void OnDisable() { Interactables.Unregister(this); }

    bool FindPlayer()
    {
        if (inv != null) return true;
        if (Time.unscaledTime < findAt) return false;
        findAt = Time.unscaledTime + 0.5f;
        var pm = PlayerMovement.Instance;
        if (pm == null) return false;
        player = pm.transform;
        inv = pm.GetComponent<PlayerInventory>();
        return inv != null;
    }

    // рюкзак игрока (для окна продажи); null, если игрока ещё не нашли
    public PlayerInventory Inventory { get { return FindPlayer() ? inv : null; } }

    // расстояние до игрока по земле (окно продажи закрывается, если отойти дальше radius × 1,5)
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
        SellWindow.Toggle(this);   // E открывает окно, а если оно открыто, закрывает
    }

    // окно сообщает о продаже; событие уходит в StoryFlow (глава «Первая продажа»). Пустая продажа не считается
    public void ReportSold(int coins)
    {
        if (coins <= 0) return;
        if (Sold != null) Sold();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.96f, 0.77f, 0.26f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, radius);
    }
}

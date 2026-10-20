using UnityEngine;

// Вешается на игрока: хранит, сколько бревен в рюкзаке и какая вместимость, и отдаёт это над головой (PlayerHeadHud).
// Вместимость по GDD: 4 + 2 за каждый уровень прокачки «Вместимость рюкзака» (PlayerUpgrades), максимум 16.
// Сама табличка (иконка, «4/6», шкала, рамка: обычная, янтарная, красная) и таймер рубки живут в PlayerHeadHud:
// так рюкзак и таймер стоят один над другим и не перекрываются.
public class BackpackTag : MonoBehaviour
{
    [Header("Вид")]
    [Tooltip("Пусто = нарисованное бревно")]
    public Sprite icon;

    [Header("Больше не используются (положением управляет PlayerHeadHud, поля оставлены, чтобы сцена не ломалась)")]
    public Vector3 offset = new Vector3(0f, 2.8f, 0f);
    public bool hideWhileChopping = false;
    public float fontSize = 30f;

    PlayerInventory inventory;
    PlayerHeadHud hud;
    int lastWood = -1, lastCap = -1;

    public int Capacity => inventory != null ? inventory.Capacity : (PlayerUpgrades.Instance != null ? PlayerUpgrades.Instance.Capacity : GameBalance.StartBackpack);
    public int Carried => inventory != null ? inventory.woodCount : 0;
    public bool IsFull => Carried >= Capacity;                 // уже лежит в рюкзаке (так считают туториал, сюжет и замедление)

    // свободные слоты С УЧЁТОМ брёвен, что ещё летят в HUD. По ним решают, можно ли начать рубку и сколько влезет
    public int Free => inventory != null ? inventory.FreeSlots : Mathf.Max(0, Capacity - Carried);
    public bool NoRoom => Free <= 0;                           // рубить нельзя: места нет (с учётом летящих)

    void Start()
    {
        inventory = GetComponent<PlayerInventory>();
        hud = PlayerHeadHud.Get(gameObject);
        if (icon != null) hud.SetPackIcon(icon);
        Refresh();
    }

    void Update()
    {
        if (hud == null) return;
        if (Carried != lastWood || Capacity != lastCap) Refresh();
    }

    // Игрок пытается рубить, а рюкзак полон: табличка вздрагивает
    public void Flash()
    {
        if (hud != null) hud.PunchPack(0.3f);
    }

    void Refresh()
    {
        bool grew = lastWood >= 0 && Carried > lastWood;
        lastWood = Carried;
        lastCap = Capacity;
        hud.SetBackpack(lastWood, lastCap, grew);
    }
}

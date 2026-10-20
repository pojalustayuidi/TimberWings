using UnityEngine;

// Таймер рубки над игроком. Рисует его PlayerHeadHud: плашка с полосой, секундами до конца и «+2» появляется НАД рюкзаком,
// цвет полосы плавно идёт от янтарного к зелёному, в конце яркая вспышка и «Готово!».
// Вешается на игрока. API прежний (Show / Complete / Hide), PlayerInteraction менять не нужно.
public class ChopProgress : MonoBehaviour
{
    [Header("Анимация")]
    public float completeHold = 0.35f;   // сколько держится «Готово!» после последнего удара, с

    [Header("Больше не используются (оформлением управляет PlayerHeadHud, поля оставлены, чтобы сцена не ломалась)")]
    public Vector3 offset = new Vector3(0f, 2.8f, 0f);
    public Color startColor = new Color32(0xF2, 0xA9, 0x3B, 255);
    public Color endColor = new Color32(0x6B, 0xCB, 0x5A, 255);
    public float hitPunch = 0.25f;
    public float completePunch = 0.5f;

    PlayerHeadHud hud;
    PlayerInteraction interaction;
    bool active;

    void Start() { Init(); }

    void Init()
    {
        if (hud == null)
        {
            hud = PlayerHeadHud.Get(gameObject);
            hud.completeHold = completeHold;
        }
        if (interaction == null) interaction = GetComponent<PlayerInteraction>();
    }

    // progress: 0..1 на всё дерево, hitsDone / hitsTotal оставлены для совместимости
    public void Show(float progress, int hitsDone, int hitsTotal)
    {
        Init();
        active = true;
        float total = interaction != null ? interaction.GatherTime : GameBalance.ChopTime;
        int gain = interaction != null ? interaction.ExpectedWood : GameBalance.WoodPerTree;   // сколько влезет: «+3» с «Крепкой древесиной», «+6» у золотого
        int of = interaction != null ? interaction.WoodFull : gain;                            // сколько у дерева всего: если больше gain, плашка пишет «+2/4»
        hud.ShowChop(progress, (1f - progress) * total, gain, of);
    }

    // Дерево срублено: полоса полная, зелёная, «хлопок»
    public void Complete()
    {
        if (hud == null || !active) return;
        hud.CompleteChop();
    }

    public void Hide()
    {
        if (hud == null || !active) return;
        active = false;
        hud.HideChop();
    }
}

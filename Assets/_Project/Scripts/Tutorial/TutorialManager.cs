using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerInventory playerInventory;

    [Header("Задание «Собери дрова»")]
    public int woodRequired = 4;        // GDD: костёр стоит 4 бревна
    public float completeDelay = 1.6f;  // сколько показывать «6/4», прежде чем перейти дальше

    // 0 идти, 1 найти дерево, 2 собрать дрова, 3 отнести на фундамент, 4 готово
    private int currentStep = 0;
    private int collected = 0;          // сколько бревен собрано за всё время (не уменьшается при тратах)
    private int lastWood = 0;
    private float doneTimer = 0f;

    const string Green = "#6BCB5A";
    const string Gray = "#AEB9D6";

    void Start()
    {
        // IntroFlow ведёт обучение и сам пишет цель в HUD: старый менеджер молчит, иначе два скрипта перебивают друг друга
        if (FindFirstObjectByType<IntroFlow>() != null) { enabled = false; return; }
        lastWood = playerInventory != null ? playerInventory.woodCount : 0;
        SetStep(0);
    }

    void Update()
    {
        // Считаем только прирост, чтобы трата дров на постройку не откатывала задание
        if (playerInventory != null)
        {
            int wood = playerInventory.woodCount;
            if (wood > lastWood)
            {
                collected += wood - lastWood;
                if (currentStep == 2) ShowCollectStep();
            }
            lastWood = wood;
        }

        switch (currentStep)
        {
            case 0:
                if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f)
                    SetStep(1);
                break;

            case 1:
                if (collected > 0) SetStep(2);
                break;

            case 2:
                if (collected >= woodRequired)
                {
                    doneTimer += Time.deltaTime;
                    if (doneTimer >= completeDelay) SetStep(3);
                }
                break;
        }
    }

    // Вызови это из скрипта фундамента, когда постройка завершена
    public void OnFoundationBuilt()
    {
        if (!enabled) return;
        SetStep(4);
    }

    public void SetStep(int stepIndex)
    {
        currentStep = stepIndex;
        doneTimer = 0f;

        if (UIManager.Instance == null) return;

        switch (currentStep)
        {
            case 0: Say(1, "Используй W A S D для ходьбы"); break;
            case 1: Say(2, PlayerInteraction.RhythmChop ? "Подойди к дереву, нажми E и бей E, когда маркер в зелёной зоне" : "Подойди к дереву и зажми E, чтобы рубить"); break;
            case 2: ShowCollectStep(); break;
            case 3: Say(4, "Отнеси дрова на серый фундамент"); break;
            case 4: Say(0, "Отличная работа! Продолжай строить базу."); break;
        }
    }

    // «Собери 4 бревна  6/4»: счётчик идёт дальше нормы, а не упирается в максимум
    void ShowCollectStep()
    {
        if (UIManager.Instance == null) return;

        bool done = collected >= woodRequired;
        string counter = "<color=" + (done ? Green : "#FFFFFF") + ">" + collected + "/" + woodRequired + "</color>";
        string tail = done ? "  <color=" + Green + ">Готово!</color>" : "";

        UIManager.Instance.UpdateObjective(
            Label(3) + "Собери " + woodRequired + " " + UiDraw.Plural(woodRequired, "бревно", "бревна", "бревен") + "  " + counter + tail);
    }

    void Say(int number, string text)
    {
        UIManager.Instance.UpdateObjective(Label(number) + text);
    }

    // Маленькая серая подпись «ЗАДАНИЕ N» над основным текстом
    string Label(int number)
    {
        if (number <= 0) return "";
        return "<size=62%><color=" + Gray + ">ЗАДАНИЕ " + number + "</color></size>\n";
    }

}
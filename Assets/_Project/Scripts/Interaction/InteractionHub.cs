// Assets/_Project/Scripts/Interaction/InteractionHub.cs (НЕ в Editor)
// Вешается на игрока. Решает, кому сейчас принадлежит клавиша E:
//   1) идёт диалог              -> E пропускает реплику (это делает DialogueManager), всё остальное молчит;
//   2) рядом NPC/объект ближе,  -> E = «нажми» (разговор и т.п.), над ним подсказка «Нажми E, чтобы …»;
//      чем ближайшее дерево
//   3) иначе                    -> E свободна: рубка (PlayerInteraction) и её подсказка (InteractionHint).
// PlayerInteraction и InteractionHint читают InteractionHub.OwnsInput.
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class InteractionHub : MonoBehaviour
{
    // true, пока E занята хабом (идёт диалог или рядом доступный объект)
    public static bool OwnsInput { get; private set; }
    public static IInteractable Current { get; private set; }

    [Header("Ввод")]
    public KeyCode key = KeyCode.E;

    [Header("Подсказка")]
    public string promptFormat = "Нажми <color=#6BCB5A>{key}</color>, чтобы {prompt}";

    [Header("Выбор")]
    public float pickInterval = 0.1f;     // как часто искать ближайший объект
    public float keepMargin = 1.15f;      // запас радиуса, чтобы подсказка не мигала на границе

    PlayerInteraction trees;
    WorldTag worldTag;
    Transform holder;
    IInteractable shown;
    string shownPrompt;      // последний показанный текст: Prompt у объекта может меняться, пока подсказка висит («строить» -> «открыть стройку»)
    float timer;
    bool wasBusy, placed;

    void Start()
    {
        trees = GetComponent<PlayerInteraction>();

        var go = new GameObject("InteractionHub_Tag");
        holder = go.transform;
        worldTag = go.AddComponent<WorldTag>();
        worldTag.offset = Vector3.zero;
        worldTag.visible = false;
    }

    void OnDisable()
    {
        OwnsInput = false;
        Current = null;
    }

    void OnDestroy()
    {
        OwnsInput = false;
        Current = null;
        if (holder != null) Destroy(holder.gameObject);
    }

    string Fmt(string prompt)
    {
        string k = key.ToString();
        if (k.StartsWith("Alpha")) k = k.Substring(5);
        return promptFormat.Replace("{key}", k).Replace("{prompt}", prompt);
    }

    void Update()
    {
        bool busy = DialogueManager.IsBusy || DialogueSequence.AnyPlaying;
        // E, которой только что закончили диалог (пропуск последней реплики), второй раз не считаем
        bool lockInput = busy || wasBusy;
        wasBusy = busy;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = pickInterval;
            Pick();
        }

        if (!lockInput && Current != null && Current.CanInteract && MobileInput.ActionDown)
        {
            if (worldTag != null) worldTag.Punch(0.2f);
            Current.Interact();
        }

        OwnsInput = busy || Current != null || DialogueManager.BlocksUntilRelease;
        UpdatePrompt(busy);
    }

    // ------------------------------------------------------------ выбор
    void Pick()
    {
        Vector3 me = transform.position;
        IInteractable best = null;
        float bestSqr = float.MaxValue;

        var all = Interactables.All;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null || it.Anchor == null || !it.CanInteract) continue;

            Vector3 d = it.Anchor.position - me;
            d.y = 0f;
            float lim = it.Radius * (it == Current ? keepMargin : 1f);
            float sq = d.sqrMagnitude;
            if (sq > lim * lim || sq >= bestSqr) continue;
            bestSqr = sq;
            best = it;
        }

        // дерево ближе, чем объект: уступаем E рубке. Исключение: NPC и прилавок (Priority), их E не отдаём
        var prio = best as IPriorityInteractable;
        bool keep = prio != null && prio.Priority;
        if (best != null && !keep && trees != null && trees.CurrentTarget != null)
        {
            Vector3 td = trees.CurrentTarget.transform.position - me;
            td.y = 0f;
            if (td.sqrMagnitude < bestSqr) best = null;
        }

        Current = best;
    }

    // ------------------------------------------------------------ подсказка над объектом
    void UpdatePrompt(bool busy)
    {
        if (worldTag == null) return;

        if (Current != shown)
        {
            shown = Current;
            placed = false;
            shownPrompt = null;
        }

        // текст сверяем каждый кадр (строки из свойства без выделений памяти): раньше он ставился один раз и мог устареть
        if (shown != null)
        {
            string pr = shown.Prompt;
            if (pr != shownPrompt) { shownPrompt = pr; worldTag.SetText(Fmt(pr)); }
        }

        var hp = shown as IHidesPrompt;
        worldTag.visible = shown != null && !busy && (hp == null || !hp.PromptHidden);
        if (shown == null) return;

        Vector3 p = shown.PromptPoint;
        if (!placed) { holder.position = p; placed = true; }
        else holder.position = Vector3.Lerp(holder.position, p, 1f - Mathf.Exp(-14f * Time.deltaTime));
    }
}
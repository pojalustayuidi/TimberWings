// Assets/_Project/Scripts/Skills/LumberjackEffects.cs (НЕ в Editor)
// Живая часть ветки «Лесоруб»: золотое дерево (lumber_golden) и сброс состояния при смене навыков, загрузке и новой игре.
// Создаётся сам (BeforeSceneLoad), живёт всю игру, вешать на сцену ничего не нужно.
//
// Золотое дерево: раз в GameBalance.SkillGoldenInterval секунд одно спелое дерево рядом с игроком становится золотым
// (6 брёвен, свечение, столб света, табличка). Золотое дерево одно за раз: пока оно стоит, таймер не идёт.
// Срубил: следующее через 90 с. Не срубил: оно ждёт тебя (таймер стоит).
//
// Что НЕ сохраняется (по заданию): таймер золотого дерева, само золотое дерево и серия рубки.
// При Load и новой игре всё это сбрасывается, а срубленные деревья возвращает TreeRespawn.OnSaveLoaded (деревья спелые, как в 11-C).
// Наград здесь нет вообще, поэтому дублей при Load быть не может.
using UnityEngine;

public sealed class LumberjackEffects : MonoBehaviour
{
    static LumberjackEffects inst;

    float goldenTimer;          // секунд с момента, как золотого дерева нет (не сохраняется)
    float findPlayerAt;
    Transform player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { inst = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("LumberjackEffects");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<LumberjackEffects>();
    }

    void Awake()
    {
        if (inst != null && inst != this) { Destroy(gameObject); return; }
        inst = this;
    }

    void OnEnable()
    {
        // сначала отписка: при выключенном Domain Reload подписки не должны задваиваться
        PlayerSkills.Changed -= OnSkillsChanged; PlayerSkills.Changed += OnSkillsChanged;
        SaveSystem.Loaded -= ResetAll; SaveSystem.Loaded += ResetAll;
        SaveSystem.NewGameStarted -= ResetAll; SaveSystem.NewGameStarted += ResetAll;
    }

    void OnDisable()
    {
        PlayerSkills.Changed -= OnSkillsChanged;
        SaveSystem.Loaded -= ResetAll;
        SaveSystem.NewGameStarted -= ResetAll;
    }

    void OnDestroy() { if (inst == this) inst = null; }

    // купили или сбросили навык (или пришёл новый уровень): то, что без навыка не должно жить, убираем сразу
    void OnSkillsChanged()
    {
        if (!PlayerSkills.Has(SkillIds.LumberGolden))
        {
            ClearGolden();
            goldenTimer = 0f;
        }
        if (!PlayerSkills.Has(SkillIds.LumberChain)) LumberSkills.ResetChain();
    }

    // Load или новая игра: ни серии, ни золотого дерева, ни таймера
    void ResetAll()
    {
        ClearGolden();
        goldenTimer = 0f;
        LumberSkills.ResetChain();
    }

    void Update()
    {
        if (SaveSystem.IsLoading) return;
        if (!PlayerSkills.Has(SkillIds.LumberGolden)) return;

        if (HasGolden()) return;   // одно золотое дерево за раз, таймер стоит

        goldenTimer += Time.deltaTime;   // в паузе (timeScale 0) не идёт
        if (goldenTimer < GameBalance.SkillGoldenInterval) return;

        if (TrySpawnGolden()) goldenTimer = 0f;
        else goldenTimer = GameBalance.SkillGoldenInterval - 1f;   // нет подходящих деревьев: повтор через секунду
    }

    // золотое дерево сейчас стоит (спелое, не срубленное)
    static bool HasGolden()
    {
        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].IsGolden) return true;
        return false;
    }

    static void ClearGolden()
    {
        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && list[i].IsGolden) list[i].SetGolden(false);
    }

    bool TrySpawnGolden()
    {
        if (player == null && Time.unscaledTime >= findPlayerAt)
        {
            findPlayerAt = Time.unscaledTime + 1f;
            var pi = FindFirstObjectByType<PlayerInteraction>();
            if (pi != null) player = pi.transform;
        }
        if (player == null) return false;

        var tree = LumberSkills.PickGoldenCandidate(player.position);
        if (tree == null) return false;

        tree.SetGolden(true);
        SaveIndicator.ShowToast("Появилось золотое дерево! Оно даёт " + GameBalance.SkillGoldenWood + " брёвен");
        SfxManager.Play("hint", 0.9f, 1.15f);
        return true;
    }

    // ---------------------------------------------------------------- тест из Inspector (ПКМ по компоненту в Play Mode)
    [ContextMenu("Тест: золотое дерево прямо сейчас")]
    void DebugGoldenNow()
    {
        if (!PlayerSkills.Has(SkillIds.LumberGolden)) { Debug.Log("LumberjackEffects: навык lumber_golden не изучен."); return; }
        goldenTimer = GameBalance.SkillGoldenInterval;
    }

    [ContextMenu("Тест: сбросить серию рубки")]
    void DebugResetChain() { LumberSkills.ResetChain(); }
}

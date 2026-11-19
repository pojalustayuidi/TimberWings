// Promotes one eligible tree and restores its identity from the chopping save.
using UnityEngine;

public sealed class LumberjackEffects : MonoBehaviour
{
    static LumberjackEffects inst;

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
            if (!SaveSystem.IsLoading && !string.IsNullOrEmpty(ChopPrototypeSave.GoldenStanding))
                ChopPrototypeSave.SetGolden(null, ChopPrototypeSave.GoldenCooldown);
        }
        if (!PlayerSkills.Has(SkillIds.LumberChain)) LumberSkills.ResetChain();
    }

    // A saved golden tree is reapplied after its source becomes active.
    void ResetAll()
    {
        ClearGolden();
        LumberSkills.ResetChain();
    }

    void Update()
    {
        if (SaveSystem.IsLoading || SpecialTreeGroves.HasAuthoredGold) return;
        if (!PlayerSkills.Has(SkillIds.LumberGolden)) return;

        if (ChopPrototypeSave.HasUncollectedGolden()) return;

        var standingId = ChopPrototypeSave.GoldenStanding;
        if (!string.IsNullOrEmpty(standingId))
        {
            foreach (var tree in ResourceItem.Active)
            {
                if (tree == null || tree.ChopSaveId != standingId) continue;
                if (!tree.IsGolden) tree.SetGolden(true);
                return;
            }
            // Leave the identity intact while the playable island is loading.
            if (PlayerMovement.Instance == null || BuildingController.AnyRestorePending) return;
            ChopPrototypeSave.SetGolden(null, 1f);
        }

        float remaining = ChopPrototypeSave.GoldenCooldown - Time.deltaTime;
        ChopPrototypeSave.SetGolden(null, remaining);
        if (remaining <= 0 && !TrySpawnGolden()) ChopPrototypeSave.SetGolden(null, 1f);
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
        ChopPrototypeSave.SetGolden(tree.ChopSaveId, 0f);
        SaveIndicator.ShowToast("Появилось золотое дерево! Оно даёт " + GameBalance.SkillGoldenWood + " брёвен");
        SfxManager.Play("hint", 0.9f, 1.15f);
        return true;
    }

    // ---------------------------------------------------------------- тест из Inspector (ПКМ по компоненту в Play Mode)
    [ContextMenu("Тест: золотое дерево прямо сейчас")]
    void DebugGoldenNow()
    {
        if (!PlayerSkills.Has(SkillIds.LumberGolden)) { Debug.Log("LumberjackEffects: навык lumber_golden не изучен."); return; }
        ChopPrototypeSave.SetGolden(ChopPrototypeSave.GoldenStanding, 0f);
    }

    [ContextMenu("Тест: сбросить серию рубки")]
    void DebugResetChain() { LumberSkills.ResetChain(); }
}

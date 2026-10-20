// Assets/_Project/Scripts/Skills/SkillsDebug.cs (НЕ в Editor)
// Тестовое меню навыков. Только Editor и Development Build: в релиз не попадает.
// В Play Mode в Hierarchy появляется объект «[SkillsDebug]»: ПКМ по заголовку компонента в Inspector -> пункты «Тест: ...».
// Выбери узел в поле nodeId (например lumber_chain), нажми «Тест: купить узел».
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;

public sealed class SkillsDebug : MonoBehaviour
{
    public string nodeId = SkillIds.LumberChain;
    [Range(1, 10)] public int setLevel = 10;
    public int coinsToAdd = 500;

    static SkillsDebug instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[SkillsDebug]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SkillsDebug>();
    }

    [ContextMenu("Тест: купить узел (nodeId)")]
    void BuyNode()
    {
        string reason;
        if (PlayerSkills.CanBuy(nodeId, out reason))
        {
            PlayerSkills.TryBuy(nodeId);
            Debug.Log("Навык куплен: " + nodeId + ". Свободных очков: " + PlayerSkills.FreePoints);
        }
        else Debug.Log("Нельзя купить «" + nodeId + "»: " + reason);
    }

    [ContextMenu("Тест: купить всё доступное")]
    void BuyAllAvailable()
    {
        int bought = 0;
        bool progress = true;
        while (progress)
        {
            progress = false;
            var all = SkillTree.All;
            for (int i = 0; i < all.Count; i++)
            {
                string r;
                if (PlayerSkills.CanBuy(all[i].Id, out r) && PlayerSkills.TryBuy(all[i].Id)) { bought++; progress = true; }
            }
        }
        Debug.Log("Куплено узлов: " + bought + ". Свободных очков: " + PlayerSkills.FreePoints);
    }

    [ContextMenu("Тест: сбросить навыки")]
    void Reset()
    {
        string reason;
        int cost = PlayerSkills.ResetCost;
        if (PlayerSkills.CanReset(out reason) && PlayerSkills.TryReset()) Debug.Log("Навыки сброшены. Цена: " + cost + ". Следующий сброс: " + PlayerSkills.ResetCost + " монет.");
        else Debug.Log("Сброс невозможен: " + reason);
    }

    // меняет уровень в обход наград (только для проверки очков)
    [ContextMenu("Тест: поставить уровень (setLevel)")]
    void SetLevel()
    {
        var st = PlayerStats.Instance;
        if (st == null) { Debug.Log("PlayerStats не найден (запусти игровую сцену)."); return; }
        st.currentLevel = Mathf.Clamp(setLevel, 1, GameBalance.MaxLevel);
        st.currentXP = Mathf.Max(st.currentXP, GameBalance.XpForLevel(st.currentLevel));
        st.RefreshUI();
        PlayerSkills.Refresh();
        SaveSystem.RequestSave("stats");
        Debug.Log("Уровень " + st.currentLevel + ". Очков: всего " + PlayerSkills.TotalPoints + ", свободно " + PlayerSkills.FreePoints);
    }

    [ContextMenu("Тест: добавить монеты (coinsToAdd)")]
    void AddCoins()
    {
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.AddCoins(coinsToAdd);
    }

    [ContextMenu("Тест: вывести состояние")]
    void Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Очки: всего " + PlayerSkills.TotalPoints + ", потрачено " + PlayerSkills.SpentPoints + ", свободно " + PlayerSkills.FreePoints
                      + ". Цена сброса: " + PlayerSkills.ResetCost);
        var all = SkillTree.All;
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            string r;
            string state = PlayerSkills.Has(d.Id) ? "КУПЛЕН" : (PlayerSkills.CanBuy(d.Id, out r) ? "доступен" : r);
            sb.AppendLine("[" + SkillTree.BranchName(d.Branch) + " " + d.Tier + "] " + d.Id + " (" + d.Cost + " оч.): " + state);
        }
        Debug.Log(sb.ToString());
    }
}
#endif

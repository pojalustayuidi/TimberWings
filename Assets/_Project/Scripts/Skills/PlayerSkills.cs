// Assets/_Project/Scripts/Skills/PlayerSkills.cs (НЕ в Editor)
// Состояние дерева навыков: очки, покупка, сброс, сохранение. Эффектов здесь нет (их делают задачи 3 и 4).
// Эффекты читают состояние ТОЛЬКО через PlayerSkills.Has(id) и слушают PlayerSkills.Changed.
//
// Очки не хранятся: TotalPoints = min(Level - 1, SkillMaxPoints), считается из PlayerStats. Поэтому при загрузке
// двойных очков не бывает, а в файле лежат только купленные ID и флаг бесплатного сброса.
//
// Это static-класс, а ISaveable нельзя реализовать статикой, поэтому сохранение делает вложенный Saver.
// Он регистрируется сам (BeforeSceneLoad), живёт всю игру и не зависит от сцены. Порядок с SaveAutoRunner не важен:
// если Load() был раньше, секция применится прямо в Register(); если позже, её применит сам Load().
//
// Старые таланты за монеты удалены, их место занял этот класс. «Бесплатные очки улучшений» (PlayerUpgrades.freePoints на уровнях 3, 5, 7, 9) с очками навыков не связаны.
using System;
using System.Collections.Generic;
using UnityEngine;

// Состояние узла для UI (PlayerSkills.GetState): куплен, можно купить, не хватает очков, закрыт (уровень или корень ветки), занят выбором соседа
public enum SkillNodeState { Bought, Available, NoPoints, Locked, Blocked }

public static class PlayerSkills
{
    static readonly HashSet<string> owned = new HashSet<string>();
    static bool freeResetUsed;     // бесплатный сброс уже потрачен (сохраняется)
    static bool needsValidate;     // после Load нужно сверить навыки с уровнем, но PlayerStats ещё не готов
    static Saver saver;

    // после покупки, сброса и загрузки (один раз), а также при смене уровня (появилось очко)
    public static event Action Changed;

    // ------------------------------------------------------------ очки
    public static int TotalPoints { get { return Mathf.Clamp(PlayerStats.Level - 1, 0, GameBalance.SkillMaxPoints); } }

    public static int SpentPoints
    {
        get
        {
            int sum = 0;
            var all = SkillTree.All;
            for (int i = 0; i < all.Count; i++)
                if (owned.Contains(all[i].Id)) sum += all[i].Cost;
            return sum;
        }
    }

    public static int FreePoints { get { return Mathf.Max(0, TotalPoints - SpentPoints); } }

    public static int OwnedCount { get { return owned.Count; } }

    public static bool Has(string id) { return !string.IsNullOrEmpty(id) && owned.Contains(id); }

    // ------------------------------------------------------------ покупка
    // Проверки по порядку: известен ли узел, не идёт ли загрузка, не куплен ли, нет ли выбора в группе,
    // хватает ли уровня, изучен ли узел 1-го яруса ветки (для ярусов 2 и 3), хватает ли очков.
    // reason: готовая фраза для подсказки в UI (null, если купить можно).
    public static bool CanBuy(string id, out string reason)
    {
        reason = null;
        var def = SkillTree.Get(id);
        if (def == null) { reason = "Неизвестный навык"; return false; }
        if (SaveSystem.IsLoading) { reason = "Идёт загрузка"; return false; }
        if (owned.Contains(id)) { reason = "Уже изучено"; return false; }

        if (def.ExclusiveGroup != null)
        {
            var other = OwnedInGroup(owned, def.ExclusiveGroup);
            if (other != null) { reason = "Выбран другой навык: «" + other.Title + "»"; return false; }
        }

        int level = PlayerStats.Level;
        if (level < def.RequiredLevel) { reason = "Нужен " + def.RequiredLevel + " уровень"; return false; }

        if (def.Tier > 1)
        {
            var root = SkillTree.Tier1Of(def.Branch);
            if (root == null || !owned.Contains(root.Id))
            {
                reason = root != null ? "Сначала изучите «" + root.Title + "»" : "Не изучен навык 1-го яруса";
                return false;
            }
        }

        int free = FreePoints;
        if (free < def.Cost) { reason = "Не хватает очков: нужно " + def.Cost + ", свободно " + free; return false; }
        return true;
    }

    // Состояние узла для отрисовки. Причину текстом даёт CanBuy.
    public static SkillNodeState GetState(string id)
    {
        var def = SkillTree.Get(id);
        if (def == null) return SkillNodeState.Locked;
        if (owned.Contains(id)) return SkillNodeState.Bought;
        if (def.ExclusiveGroup != null && OwnedInGroup(owned, def.ExclusiveGroup) != null) return SkillNodeState.Blocked;
        if (PlayerStats.Level < def.RequiredLevel) return SkillNodeState.Locked;
        if (def.Tier > 1)
        {
            var root = SkillTree.Tier1Of(def.Branch);
            if (root == null || !owned.Contains(root.Id)) return SkillNodeState.Locked;
        }
        return FreePoints < def.Cost ? SkillNodeState.NoPoints : SkillNodeState.Available;
    }

    public static bool TryBuy(string id)
    {
        string reason;
        if (!CanBuy(id, out reason)) return false;
        owned.Add(id);
        SaveSystem.RequestSave("skills");
        Fire();
        return true;
    }

    // ------------------------------------------------------------ сброс
    // первый сброс бесплатный, дальше GameBalance.SkillResetCoins монет
    public static int ResetCost { get { return freeResetUsed ? GameBalance.SkillResetCoins : 0; } }

    // для кнопки в UI: можно ли сбросить сейчас (reason: что не так)
    public static bool CanReset(out string reason)
    {
        reason = null;
        if (SaveSystem.IsLoading) { reason = "Идёт загрузка"; return false; }
        if (owned.Count == 0) { reason = "Нечего сбрасывать"; return false; }
        int cost = ResetCost;
        if (cost > 0)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || inv.coinsCount < cost) { reason = "Нужно " + cost + " монет"; return false; }
        }
        return true;
    }

    // Сброс пустого дерева ничего не стоит и бесплатный сброс не тратит (возвращает false)
    public static bool TryReset()
    {
        string reason;
        if (!CanReset(out reason)) return false;

        int cost = ResetCost;
        if (cost > 0) PlayerInventory.Instance.AddCoins(-cost);   // монеты списываем через инвентарь (он сам просит сохранение)

        owned.Clear();
        freeResetUsed = true;
        SaveSystem.RequestSave("skills");
        Fire();   // эффекты (задачи 3 и 4) подписаны на Changed и снимаются сразу
        return true;
    }

    // ------------------------------------------------------------ внутреннее
    static SkillNodeDef OwnedInGroup(HashSet<string> set, string group)
    {
        var all = SkillTree.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].ExclusiveGroup == group && set.Contains(all[i].Id)) return all[i];
        return null;
    }

    static void Fire()
    {
        var a = Changed;
        if (a == null) return;
        foreach (Delegate d in a.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Debug.LogException(e); }   // один упавший слушатель не ломает остальных
        }
    }

    // PlayerStats.Start: уровень известен. Если после Load навыки ещё не сверены с уровнем, делаем это сейчас.
    public static void NotifyStatsReady()
    {
        if (needsValidate && Validate()) Fire();
    }

    // Пересчитать и оповестить слушателей (смена уровня в обход LevelUp, например из отладки)
    public static void Refresh()
    {
        Validate();
        Fire();
    }

    // Сверка с очками и правилами. Узлы идут по ярусам (сначала 1-й, потом 2-й, потом 3-й): если очков не хватает,
    // отбрасываются узлы старших ярусов. Также отбрасываются узлы без корня ветки, второй узел из группы и узлы выше уровня.
    // true: что-то убрали. Без PlayerStats (ещё не создан) отложено до NotifyStatsReady.
    static bool Validate()
    {
        if (PlayerStats.Instance == null) { needsValidate = true; return false; }
        needsValidate = false;

        int level = PlayerStats.Level, total = TotalPoints, spent = 0;
        var keep = new HashSet<string>();
        var removed = new List<string>();
        var all = SkillTree.All;

        for (int tier = 1; tier <= 3; tier++)
        {
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d.Tier != tier || !owned.Contains(d.Id)) continue;

                bool ok = level >= d.RequiredLevel && spent + d.Cost <= total;
                if (ok && d.ExclusiveGroup != null) ok = OwnedInGroup(keep, d.ExclusiveGroup) == null;
                if (ok && tier > 1)
                {
                    var root = SkillTree.Tier1Of(d.Branch);
                    ok = root != null && keep.Contains(root.Id);
                }

                if (ok) { keep.Add(d.Id); spent += d.Cost; }
                else removed.Add(d.Id);
            }
        }

        if (removed.Count == 0) return false;

        owned.Clear();
        foreach (var id in keep) owned.Add(id);
        Debug.LogWarning("PlayerSkills: в сохранении лишние навыки (не хватает очков или нарушены правила), они сброшены: " + string.Join(", ", removed.ToArray()));
        if (!SaveSystem.IsLoading) SaveSystem.RequestSave("skills");
        return true;
    }

    // ------------------------------------------------------------ жизненный цикл
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        owned.Clear();
        freeResetUsed = false;
        needsValidate = false;
        saver = null;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        // сначала отписка: при выключенном Domain Reload подписки не должны задваиваться
        GameEvents.LevelUp -= OnLevelUp; GameEvents.LevelUp += OnLevelUp;
        SaveSystem.Loaded -= OnLoaded; SaveSystem.Loaded += OnLoaded;
        SaveSystem.NewGameStarted -= OnNewGame; SaveSystem.NewGameStarted += OnNewGame;

        saver = new Saver();
        SaveSystem.Register(saver);   // если файл уже прочитан, секция «skills» применится прямо здесь
    }

    // новый уровень = новое очко: UI и подсветка вкладки обновляются. Награды тут не выдаются.
    static void OnLevelUp(int level)
    {
        if (!SaveSystem.IsLoading) Fire();
    }

    // Load() применил файл: один раз сверяем навыки с уровнем и сообщаем слушателям
    static void OnLoaded()
    {
        Validate();
        Fire();
    }

    static void OnNewGame() { Fire(); }

    // ------------------------------------------------------------ сохранение (ISaveable, секция «skills», LoadOrder 35)
    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ: старые файлы без них читаются нормально
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public string[] owned = new string[0];   // купленные ID
        public bool freeResetUsed;
    }

    sealed class Saver : ISaveable
    {
        public string SaveKey { get { return "skills"; } }
        public int LoadOrder { get { return 35; } }

        public string Save()
        {
            var ids = new string[owned.Count];
            owned.CopyTo(ids);
            Array.Sort(ids, StringComparer.Ordinal);   // стабильный порядок: файл не меняется без причины
            return JsonUtility.ToJson(new SaveData { owned = ids, freeResetUsed = freeResetUsed });
        }

        // Без событий и RequestSave (правило ISaveable). Changed один раз пошлёт OnLoaded.
        // Неизвестные ID игнорируются. Сверка с очками откладывается до готовности PlayerStats (Validate).
        public void Load(string json, int formatVersion)
        {
            SaveData d = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { d = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception e) { Debug.LogWarning("PlayerSkills: не удалось прочитать сохранение навыков: " + e.Message); }
            }
            if (d == null) { ResetToDefault(); return; }

            owned.Clear();
            if (d.owned != null)
                for (int i = 0; i < d.owned.Length; i++)
                    if (SkillTree.Get(d.owned[i]) != null) owned.Add(d.owned[i]);
            freeResetUsed = d.freeResetUsed;
            needsValidate = owned.Count > 0;
        }

        public void ResetToDefault()
        {
            owned.Clear();
            freeResetUsed = false;
            needsValidate = false;
        }
    }
}

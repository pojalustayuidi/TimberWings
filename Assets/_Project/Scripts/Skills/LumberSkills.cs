// Assets/_Project/Scripts/Skills/LumberSkills.cs (НЕ в Editor)
// Ветка «Лесоруб»: все формулы и состояние серии в одном месте. Эффекты читают навыки ТОЛЬКО через PlayerSkills.Has(id)
// на каждом вызове (ничего не кэшируется), поэтому после сброса навыков они пропадают сразу, без событий и перезапуска.
//
// ПОРЯДОК РАСЧЁТА (бонусы перемножаются, плоские добавки идут после умножения):
//
//   Время рубки = 3 с (GameBalance.ChopTime)
//                 × 0.9^L          «Сила удара» (PlayerUpgrades, L = уровень)
//                 × таланты и питомцы (PlayerUpgrades.Bonus)
//                 ÷ (1 + серия)    «Серия рубки» lumber_chain: +8% за дерево, максимум +24%
//   Пример: Сила удара 2 (×0.81) и серия +24%: 3 × 0.81 / 1.24 = 1.96 с.
//   Бонус «Пробел в зелёной зоне» срезает долю от уже посчитанного времени (PlayerInteraction.TickChopBonus).
//
//   Бревна с дерева = база                 2; «Крепкая древесина» lumber_sturdy делает 3; золотое дерево 6 (берётся большее, не сумма)
//                     × 2                  «Двойная добыча» (PlayerUpgrades.DoubleChance), умножает базу
//                     + 1                  «Чистый спил» ритм-режима, после умножения
//   «Эхо удара» lumber_echo: +1 бревно с УПАВШЕГО СОСЕДА отдельной награды, «Двойная добыча» на него не действует.
//
//   Время роста дерева = 45 с (GameBalance.TreeRespawnSeconds) × 1.2 (lumber_sturdy) × 0.6 (lumber_fastgrow).
//   Узлы sturdy и fastgrow из одной группы (взять можно один), формула всё равно перемножает, если когда-нибудь разрешат оба.
//
// Состояние серии НЕ сохраняется (static, сбрасывается при Load, новой игре и сбросе навыков).
using UnityEngine;

public static class LumberSkills
{
    static int chainCount;               // сколько деревьев подряд срублено (с потолком, см. ChainMaxCount)
    static float lastFellAt = -999f;     // Time.time, когда упало последнее дерево серии

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { ResetChain(); }

    static bool Has(string id) { return PlayerSkills.Has(id); }

    // ------------------------------------------------------------ «Серия рубки»
    public static bool ChainOn { get { return Has(SkillIds.LumberChain); } }

    // потолок счётчика: дальше бонус уже не растёт (24% / 8% = 3)
    public static int ChainMaxCount
    {
        get
        {
            if (GameBalance.SkillChainBonus <= 0f) return 0;
            return Mathf.CeilToInt(GameBalance.SkillChainMax / GameBalance.SkillChainBonus - 0.001f);
        }
    }

    // Серия на момент now: 0, если навык не изучен, серии нет или пауза между деревьями вышла.
    // Пауза считается от падения прошлого дерева до НАЧАЛА рубки следующего (PlayerInteraction берёт значение один раз в начале).
    public static int ChainCountAt(float now)
    {
        if (!ChainOn || chainCount <= 0) return 0;
        return now - lastFellAt <= GameBalance.SkillChainPause ? chainCount : 0;
    }

    public static int ChainNow { get { return ChainCountAt(Time.time); } }

    // прибавка к скорости рубки (0.16 = +16%) для серии из count деревьев; без навыка всегда 0
    public static float ChainBonus(int count)
    {
        if (!ChainOn || count <= 0) return 0f;
        return Mathf.Min(count * GameBalance.SkillChainBonus, GameBalance.SkillChainMax);
    }

    // дерево игрока упало. countAtStart: серия, зафиксированная в начале этой рубки. Возвращает новую длину серии.
    // Деревья, упавшие от «Эха», серию не продлевают: она про рубку руками.
    public static int RegisterFelled(int countAtStart)
    {
        if (!ChainOn) { ResetChain(); return 0; }
        chainCount = Mathf.Min(Mathf.Max(0, countAtStart) + 1, Mathf.Max(1, ChainMaxCount));
        lastFellAt = Time.time;
        return chainCount;
    }

    public static void ResetChain()
    {
        chainCount = 0;
        lastFellAt = -999f;
    }

    // ------------------------------------------------------------ рост и бревна
    // множитель времени роста дерева (меньше 1 = быстрее). Читает TreeRespawn каждый кадр, поэтому сброс навыка действует сразу
    public static float GrowthMul
    {
        get
        {
            float m = 1f;
            if (Has(SkillIds.LumberSturdy)) m *= GameBalance.SkillSturdyGrowth;
            if (Has(SkillIds.LumberFastGrow)) m *= GameBalance.SkillFastGrowth;
            return m;
        }
    }

    // база бревен с дерева (до «Двойной добычи»)
    public static int BaseWood(ResourceItem tree)
    {
        // база зависит от размера дерева (1 / 2 / 4), «Крепкая древесина» добавляет +1 (SkillSturdyWood − WoodPerTree), как раньше у зрелого: 2 -> 3
        int w = tree != null ? tree.Logs : GameBalance.WoodPerTree;
        if (Has(SkillIds.LumberSturdy)) w += GameBalance.SkillSturdyWood - GameBalance.WoodPerTree;
        if (tree != null && tree.IsGolden && Has(SkillIds.LumberGolden)) w = Mathf.Max(w, GameBalance.SkillGoldenWood);
        if (SilverTree.Is(tree)) w = Mathf.Max(w, GameBalance.SilverTreeLogs);   // ночное серебряное дерево (NightBonus)
        return w;
    }

    // ------------------------------------------------------------ поиск деревьев
    // ближайшее спелое дерево (из ResourceItem.Active). maxDist <= 0: без ограничения расстояния
    public static ResourceItem NearestRipe(Vector3 from, float maxDist, ResourceItem exclude, bool skipGolden)
    {
        ResourceItem best = null;
        float bestSqr = maxDist > 0f ? maxDist * maxDist : float.MaxValue;
        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t == null || t == exclude || !t.gameObject.activeInHierarchy) continue;
            if (skipGolden && t.IsGolden) continue;
            Vector3 d = t.transform.position - from;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bestSqr) { bestSqr = s; best = t; }
        }
        return best;
    }

    // «Эхо удара»: бросок шанса и поиск соседа. null: навык не изучен, не повезло или рядом нет спелых деревьев
    public static ResourceItem RollEcho(ResourceItem from)
    {
        if (from == null || !Has(SkillIds.LumberEcho)) return null;
        if (Random.value >= GameBalance.SkillEchoChance) return null;
        return NearestRipe(from.transform.position, GameBalance.SkillEchoRadius, from, true);
    }

    // кандидат на золотое дерево: случайное спелое в кольце [MinDist, MaxDist] вокруг игрока, иначе любое спелое
    public static ResourceItem PickGoldenCandidate(Vector3 playerPos)
    {
        var list = ResourceItem.Active;
        float minSqr = GameBalance.SkillGoldenMinDist * GameBalance.SkillGoldenMinDist;
        float maxSqr = GameBalance.SkillGoldenMaxDist * GameBalance.SkillGoldenMaxDist;

        ResourceItem pick = null;
        int ring = 0, any = 0;
        ResourceItem pickAny = null;

        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t == null || !t.gameObject.activeInHierarchy || t.IsGolden || t.IsChopping) continue;

            any++;
            if (Random.Range(0, any) == 0) pickAny = t;          // равномерный выбор без выделения памяти

            Vector3 d = t.transform.position - playerPos;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < minSqr || s > maxSqr) continue;
            ring++;
            if (Random.Range(0, ring) == 0) pick = t;
        }
        return pick != null ? pick : pickAny;
    }
}

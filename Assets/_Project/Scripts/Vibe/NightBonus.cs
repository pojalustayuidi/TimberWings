// Assets/_Project/Scripts/Vibe/NightBonus.cs (НЕ в Editor)
// Ночной бонус без давления: раз за ночь одно спелое дерево рядом с игроком становится серебряным (SilverTreeFx).
// Даёт GameBalance.SilverTreeLogs брёвен и GameBalance.SilverTreeXp опыта. Пропустил ночь: никаких штрафов, к рассвету серебро просто гаснет.
//
// Создаёт DayNightCycle сам, вешать ничего не нужно.
// «Раз за ночь» хранит TimeOfDaySaveData: nights (сколько ночей началось) и silverNight (в какую уже появлялось).
// Поэтому после загрузки в середине ночи второе дерево не появится, а если сохранились до появления, оно появится после загрузки.
using UnityEngine;

public sealed class NightBonus : MonoBehaviour
{
    ResourceItem current;
    Transform player;
    float nightTimer;          // секунд подряд в ночи (без сна и перемотки)
    float checkAt, findAt;

    void Update()
    {
        if (Time.unscaledTime < checkAt) return;
        checkAt = Time.unscaledTime + 1f;

        var sd = TimeOfDaySaveData.Instance;
        if (sd == null) return;

        bool night = DayNightCycle.Phase == DayPhase.Night && !DayNightCycle.Warping && !CampSleep.Sleeping;

        if (current != null)
        {
            // срублено / спрятано (компонент снял сам себя) или кончилась ночь
            if (!night || !current.gameObject.activeInHierarchy || !SilverTree.Is(current)) Clear();
            return;
        }

        if (!night) { nightTimer = 0f; return; }
        if (Time.timeScale <= 0f) return;
        if (sd.silverNight == sd.nights) return;   // в эту ночь уже было

        nightTimer += 1f;
        if (nightTimer < GameBalance.SilverTreeDelay) return;

        TrySpawn(sd);
    }

    // тесты: не ждать SilverTreeDelay, дерево появится на ближайшей проверке (если сейчас ночь и в эту ночь ещё не было)
    public void DebugSkipDelay() { nightTimer = GameBalance.SilverTreeDelay; checkAt = 0f; }

    void Clear()
    {
        if (current != null)
        {
            var fx = current.GetComponent<SilverTreeFx>();
            if (fx != null) Destroy(fx);
        }
        current = null;
    }

    void OnDestroy()
    {
        if (current != null)
        {
            var fx = current.GetComponent<SilverTreeFx>();
            if (fx != null) Destroy(fx);
        }
    }

    void TrySpawn(TimeOfDaySaveData sd)
    {
        if (player == null && Time.unscaledTime >= findAt)
        {
            findAt = Time.unscaledTime + 1f;
            var pm = PlayerMovement.Instance;
            if (pm != null) player = pm.transform;
        }
        if (player == null) return;

        ResourceItem tree = Pick(player.position, GameBalance.SilverTreeMinDist, GameBalance.SilverTreeMaxDist);
        if (tree == null) tree = Pick(player.position, 2f, 60f);   // рядом ничего: берём любое спелое подальше
        if (tree == null) return;                                  // нет спелых деревьев: повторим через секунду

        SilverTreeFx.Attach(tree);
        current = tree;
        sd.silverNight = sd.nights;
        SaveSystem.RequestSave("time");

        SfxManager.Play("silver", 0.8f, 1f);
        SaveIndicator.ShowToast("Появилось серебряное дерево: " + GameBalance.SilverTreeLogs + " брёвен и бонусный опыт");
    }

    // случайное спелое дерево в кольце [min, max] метров от игрока (равномерно, без списка)
    static ResourceItem Pick(Vector3 from, float min, float max)
    {
        float min2 = min * min, max2 = max * max;
        ResourceItem best = null;
        int seen = 0;
        var list = ResourceItem.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var tr = list[i];
            if (tr == null || !tr.gameObject.activeInHierarchy) continue;
            if (tr.IsGolden || tr.IsChopping || SilverTree.Is(tr)) continue;
            Vector3 d = tr.transform.position - from;
            d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < min2 || s > max2) continue;
            seen++;
            if (Random.Range(0, seen) == 0) best = tr;
        }
        return best;
    }
}

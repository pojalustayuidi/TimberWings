// Assets/_Project/Scripts/UI/Pets/PetDebugPanel.cs (НЕ в Editor)
// Тестовая панель для питомцев. Создаётся сама, класть на сцену ничего не нужно. Работает только в Editor и Development Build.
// F10 показывает и прячет панель (мышью). Кнопки:
//   Открыть окно клети: Лесная, Речная, Небесная (как с E у клети, с настоящим списанием монет)
//   Показать выпадение: Обычный, Необычный, Редкий, Легендарный (сразу экран результата, монеты не тратятся, питомец не выдаётся)
//   +2000 монет, Игнорировать уровень (открывает все клети), Бесплатная клеть (OfferFreeCrate)
//   Выдать случайного, Взять всех в слоты, Снять всех, Сбросить питомцев
// Для окна выпадения питомцы берутся из PetManager.database, поэтому в базе нужен хотя бы один питомец каждой редкости.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PetDebugPanel : MonoBehaviour
{
    static PetDebugPanel instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("PetDebugPanel");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PetDebugPanel>();
    }

    bool shown;
    string msg = "";
    float msgUntil;
    readonly List<PetData> tmp = new List<PetData>();
    Rect win = new Rect(16, 16, 300, 10);
    GUIStyle small;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10)) shown = !shown;
    }

    void Say(string s) { msg = s; msgUntil = Time.unscaledTime + 4f; }

    void OnGUI()
    {
        if (!shown) return;
        if (small == null) small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        win.height = 10;     // подгоняется под содержимое
        win = GUILayout.Window(94021, win, Draw, "Питомцы: тест (F10)");
    }

    void Draw(int id)
    {
        var pm = PetManager.Instance;
        if (pm == null)
        {
            GUILayout.Label("На сцене нет PetManager", small);
            GUI.DragWindow();
            return;
        }

        var inv = PlayerInventory.Instance;
        GUILayout.Label("Монеты: " + (inv != null ? inv.coinsCount.ToString() : "?")
            + "   Уровень: " + pm.PlayerLevel
            + "\nПитомцев: " + pm.Owned.Count + "   В слотах: " + pm.EquippedCount + " из " + pm.SlotCount
            + "\nДо гарантии: лес " + pm.OpensUntilGuarantee(CrateType.Forest)
            + ", река " + pm.OpensUntilGuarantee(CrateType.River)
            + ", небо " + pm.OpensUntilGuarantee(CrateType.Sky), small);

        GUILayout.Space(4);
        GUILayout.Label("Открыть окно клети", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Лесная")) PetCrateWindow.Open(CrateType.Forest);
        if (GUILayout.Button("Речная")) PetCrateWindow.Open(CrateType.River);
        if (GUILayout.Button("Небесная")) PetCrateWindow.Open(CrateType.Sky);
        GUILayout.EndHorizontal();

        GUILayout.Label("Показать выпадение (без монет)", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Обычный")) Preview(pm, PetRarity.Common);
        if (GUILayout.Button("Необычный")) Preview(pm, PetRarity.Uncommon);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Редкий")) Preview(pm, PetRarity.Rare);
        if (GUILayout.Button("Легендарный")) Preview(pm, PetRarity.Legendary);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Экономика", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+2000 монет") && inv != null) inv.AddCoins(2000);
        if (GUILayout.Button("Бесплатная клеть")) { pm.OfferFreeCrate(); Say(pm.FreeCratePending ? "Лесная клеть бесплатна" : "Бесплатная клеть уже была"); }
        GUILayout.EndHorizontal();
        pm.debugIgnoreLevelLock = GUILayout.Toggle(pm.debugIgnoreLevelLock, "Игнорировать уровень (все клети открыты)");

        GUILayout.Space(4);
        GUILayout.Label("Коллекция", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Выдать случайного")) GrantRandom(pm);
        if (GUILayout.Button("Выдать по одному")) GrantAll(pm);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Взять всех")) EquipAll(pm);
        if (GUILayout.Button("Снять всех")) UnequipAll(pm);
        if (GUILayout.Button("Сбросить")) Reset(pm);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Окно коллекции (P)")) PetCollectionWindow.OpenAnyway();

        if (Time.unscaledTime < msgUntil) GUILayout.Label(msg, small);
        GUI.DragWindow();
    }

    // ------------------------------------------------------------ действия
    void Preview(PetManager pm, PetRarity r)
    {
        if (pm.database == null) { Say("В PetManager не подключена база питомцев"); return; }
        pm.database.CollectByRarity(r, tmp);
        if (tmp.Count == 0) { Say("В базе нет питомцев этой редкости"); return; }
        PetCrateWindow.DebugPreview(CrateType.Forest, tmp[Random.Range(0, tmp.Count)]);
    }

    void GrantRandom(PetManager pm)
    {
        if (pm.database == null || pm.database.pets.Count == 0) { Say("База питомцев пуста"); return; }
        var d = pm.database.pets[Random.Range(0, pm.database.pets.Count)];
        if (d == null) return;
        pm.Grant(d);
        Say("Выдан: " + PetUi.NameOf(d) + " (" + PetText.Rarity(d.rarity) + ")");
    }

    void GrantAll(PetManager pm)
    {
        if (pm.database == null) { Say("База питомцев пуста"); return; }
        int n = 0;
        for (int i = 0; i < pm.database.pets.Count; i++)
            if (pm.database.pets[i] != null) { pm.Grant(pm.database.pets[i]); n++; }
        Say("Выдано видов: " + n);
    }

    void EquipAll(PetManager pm)
    {
        var list = new List<PetInstance>(pm.Owned);
        for (int i = 0; i < list.Count; i++) pm.Equip(list[i].uid);
    }

    void UnequipAll(PetManager pm)
    {
        for (int i = 0; i < PetManager.MaxSlots; i++) pm.Unequip(i);
    }

    // у PetManager сброс спрятан в ContextMenu (private), зовём его же
    void Reset(PetManager pm)
    {
        var m = typeof(PetManager).GetMethod("DebugReset", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null) { m.Invoke(pm, null); Say("Питомцы и сохранение сброшены"); }
        else Say("Метод DebugReset не найден");
    }
}
#endif

// Assets/_Project/Editor/CandyFunSetup.cs   (ДОЛЖЕН лежать в папке Editor)
// Меню: Tools > BuilderSim > UI > Candy Kit > Fun
//   1. Собрать CandyFxKit            собирает ассет со ссылками на спрайты из CandyKit/Fun (недостающие Fun-спрайты создаёт сам)
//   2. Оживить все кнопки            вешает CandyButtonJuice на все Button в открытых сценах и подставляет kit
//   3. Pop-in на выбранные           вешает CandyPopIn на выбранные окна (с галочкой «дети по очереди» для списков)
//   4. Покачивание на выбранные      вешает CandyIdleMotion на выбранные иконки/украшения
//   5. Проверка: праздник!           в Play Mode запускает конфетти в центре выбранного UI-объекта
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CandyFunSetup
{
    const string Root = "Tools/BuilderSim/UI/Candy Kit/Fun/";
    static string KitPath { get { return CandyUiKitGenerator.OutDir + "/Fun/CandyFxKit.asset"; } }

    static Sprite Load(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(CandyUiKitGenerator.OutDir + "/Fun/" + name + ".png");
        return s;
    }

    // ------------------------------------------------------------------ 1. kit
    [MenuItem(Root + "1. Собрать CandyFxKit", false, 300)]
    static void BuildKitMenu()
    {
        var kit = EnsureKit(true);
        if (kit != null) { Selection.activeObject = kit; EditorGUIUtility.PingObject(kit); }
    }

    public static CandyFxKit EnsureKit(bool report)
    {
        var kit = AssetDatabase.LoadAssetAtPath<CandyFxKit>(KitPath);
        bool created = false;
        if (kit == null)
        {
            if (Load("fx_sparkle") == null)
            {
                // Fun-спрайтов нет (в PNG v2 их нет): рисуем сами, дальше всё как обычно
                CandyUiKitGenerator.EnsureFunSprites(false);
                if (Load("fx_sparkle") == null)
                {
                    string msg = "Не удалось создать спрайты Fun в " + CandyUiKitGenerator.OutDir + "/Fun. " +
                                 "Запустите Tools > BuilderSim > UI > Candy Kit > Fun > 0. Пересоздать Fun-спрайты и посмотрите Console.";
                    Debug.LogWarning("[CandyFun] " + msg);
                    if (report) EditorUtility.DisplayDialog("Candy Fun", msg, "OK");
                    return null;
                }
            }
            kit = ScriptableObject.CreateInstance<CandyFxKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
            created = true;
        }

        kit.sparkle = Load("fx_sparkle");
        kit.sparkleSmall = Load("fx_sparkle_small");
        kit.glow = Load("fx_glow");
        kit.ring = Load("fx_ring");
        kit.heart = Load("fx_heart");
        kit.confettiRect = Load("fx_confetti_rect");
        kit.confettiCircle = Load("fx_confetti_circle");
        kit.confettiTri = Load("fx_confetti_tri");
        EditorUtility.SetDirty(kit);
        AssetDatabase.SaveAssets();
        if (report) Debug.Log("[CandyFun] CandyFxKit " + (created ? "создан" : "обновлён") + ": " + KitPath);
        return kit;
    }

    // ------------------------------------------------------------------ 2. кнопки
    [MenuItem(Root + "2. Оживить все кнопки в открытых сценах", false, 301)]
    static void JuiceAllButtons()
    {
        var kit = EnsureKit(false);
        var sb = new StringBuilder("=== Candy Fun: кнопки ===\n");
        if (kit == null) sb.AppendLine("! CandyFxKit не собран: кнопки оживут без искр. Запустите «1. Собрать CandyFxKit» и повторите.");

        int added = 0, skipped = 0;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.scene.IsValid()) continue;
            if (b.GetComponent<CandyButtonJuice>() != null || b.GetComponent<Animator>() != null) { skipped++; continue; }
            var j = Undo.AddComponent<CandyButtonJuice>(b.gameObject);
            j.kit = kit;
            added++;
        }
        sb.AppendLine("+ CandyButtonJuice на кнопках: " + added + (skipped > 0 ? " (пропущено: " + skipped + ", уже есть или у кнопки Animator)" : ""));
        if (added > 0)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));
            sb.AppendLine("Сохраните сцену (Ctrl+S). Откат: Ctrl+Z.");
        }
        Debug.Log(sb.ToString());
    }

    // ------------------------------------------------------------------ 3. pop-in
    [MenuItem(Root + "3. Pop-in на выбранные окна", false, 302)]
    static void PopInSelected()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<CandyPopIn>() != null) continue;
            Undo.AddComponent<CandyPopIn>(go);
            n++;
        }
        Debug.Log("[CandyFun] CandyPopIn добавлен: " + n + ". Для списков включите «Stagger Children» в Inspector.");
    }

    // ------------------------------------------------------------------ 4. покачивание
    [MenuItem(Root + "4. Покачивание на выбранные", false, 303)]
    static void IdleSelected()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<CandyIdleMotion>() != null) continue;
            var m = Undo.AddComponent<CandyIdleMotion>(go);
            m.wobbleAngle = 4f;
            n++;
        }
        Debug.Log("[CandyFun] CandyIdleMotion добавлен: " + n);
    }

    // ------------------------------------------------------------------ 5. проверка
    [MenuItem(Root + "5. Проверка: праздник!", false, 304)]
    static void Party()
    {
        if (!Application.isPlaying) { Debug.Log("[CandyFun] Нужен Play Mode: выберите UI-объект в иерархии и запустите игру."); return; }
        var kit = EnsureKit(false);
        var rt = Selection.activeGameObject != null ? Selection.activeGameObject.transform as RectTransform : null;
        if (kit == null || rt == null) { Debug.Log("[CandyFun] Выберите UI-объект (RectTransform) и соберите CandyFxKit."); return; }
        CandyFx.Confetti(kit, rt, 30);
        CandyFx.Ring(kit, rt, Color.white);
    }
}

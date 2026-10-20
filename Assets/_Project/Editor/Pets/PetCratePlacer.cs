// Assets/_Project/Editor/Pets/PetCratePlacer.cs (в папке Editor)
// Tools > BuilderSim > Питомцы > Поставить клети в локации   (строит модель процедурно, см. PetCrateBuilder)
// Tools > BuilderSim > Питомцы > Пересобрать модель клетей    (если поменял форму или цвета в PetCrateBuilder)
// Лесная клеть ставится у палатки (лагерь), Небесная у моста. Позиции потом можно двигать руками.
// Передняя сторона (клеймо, куда открывается крышка) смотрит от здания. Повторный запуск ничего не дублирует.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PetCratePlacer
{
    [MenuItem("Tools/BuilderSim/Питомцы/Поставить клети в локации")]
    public static void Place()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Питомцы] Останови Play и повтори: клети ставятся только в режиме редактирования.");
            return;
        }

        var all = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var existing = Object.FindObjectsByType<PetCrateSite>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int made = 0;

        made += PlaceOne(CrateType.Forest, StoryFlow.FindSite(StoryFlow.Site.Tent, all), new Vector3(4.5f, 0f, -1.5f), "Клеть: Лесная", existing);
        made += PlaceOne(CrateType.Sky, StoryFlow.FindSite(StoryFlow.Site.Bridge, all), new Vector3(5.5f, 0f, 2f), "Клеть: Небесная", existing);

        if (made == 0) Debug.Log("[Питомцы] Клети уже стоят (или не найдены палатка и мост), ничего не менял. Чтобы обновить модель, есть пункт «Пересобрать модель клетей».");
    }

    [MenuItem("Tools/BuilderSim/Питомцы/Пересобрать модель клетей")]
    public static void Rebuild()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Питомцы] Останови Play и повтори.");
            return;
        }

        var sites = Object.FindObjectsByType<PetCrateSite>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sites.Length == 0) { Debug.LogWarning("[Питомцы] На сцене нет клетей. Сначала «Поставить клети в локации»."); return; }

        for (int i = 0; i < sites.Length; i++)
        {
            PetCrateBuilder.Build(sites[i]);
            EditorSceneManager.MarkSceneDirty(sites[i].gameObject.scene);
        }
        Debug.Log("[Питомцы] Модель пересобрана у клетей: " + sites.Length);
    }

    static int PlaceOne(CrateType type, BuildingController anchor, Vector3 offset, string name, PetCrateSite[] existing)
    {
        for (int i = 0; i < existing.Length; i++)
            if (existing[i].crate == type) { Debug.Log("[Питомцы] " + name + " уже есть: «" + existing[i].name + "», пропускаю."); return 0; }

        if (anchor == null)
        {
            Debug.LogWarning("[Питомцы] Не нашёл зону стройки для «" + name + "» (нужна палатка или мост). Поставь клеть руками: пустой объект + PetCrateSite, потом «Пересобрать модель клетей».");
            return 0;
        }

        Vector3 pos = anchor.transform.TransformPoint(offset);
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out hit, 80f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point;

        // передняя сторона смотрит от здания
        Vector3 dir = pos - anchor.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = anchor.transform.forward;

        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Клеть питомцев");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        var site = root.AddComponent<PetCrateSite>();
        site.crate = type;
        if (type != CrateType.Forest) site.noteLines = new string[0];     // записка Гуннара только на первой, Лесной клети
        PetCrateBuilder.Build(site);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Питомцы] " + name + " поставлена в " + pos + ". Двигай как нужно: коллайдер и модель внутри Visual.");
        return 1;
    }
}

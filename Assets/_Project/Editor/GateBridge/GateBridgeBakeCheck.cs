#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class GateBridgeBakeCheck
{
    internal static string Validate(GateBridge bridge)
    {
        var result = new StringBuilder("=== Editor Bake GateBridge ===\n");
        if (bridge == null) return "ОШИБКА: в сцене нет GateBridge.";
        int errors = 0;
        void Error(string message) { errors++; result.AppendLine("ОШИБКА: " + message); }

        if (!bridge.HasFinalIsland) Error("Нет готовой геометрии/ссылок на дальний остров.");
        if (!bridge.HasBakedFinale)
        {
            var portal = bridge.GetComponentInChildren<FinalIslandPortal>(true);
            Error(portal != null ? portal.BakeIssue ?? "Портал не привязан к GateBridgeFinale."
                : "В сцене нет компонента FinalIslandPortal.");
        }
        if (bridge.walkway == null) Error("Нет прохода с коллайдерами.");
        else if (bridge.walkway.GetComponent<MeshCollider>() == null) Error("На Walkway нет MeshCollider.");
        if (bridge.source == null) Error("Нет RiverBridge: этапы не будут сохраняться.");
        if (bridge.stages == null || bridge.stages.Length != 3) Error("Нет трёх стадий стройки.");

        var zone = bridge.transform.Find("BuildZone");
        if (zone == null) Error("Нет запечённой зоны стройки GateBridge/BuildZone.");
        else
        {
            var build = zone.GetComponent<BuildingController>();
            var link = zone.GetComponent<BuildStageLink>();
            var trigger = zone.GetComponent<BoxCollider>();
            if (build == null || build.data == null || build.data.StepCount != 3) Error("BuildZone не привязана к трём этапам BuildSiteData.");
            if (link == null || link.bridge != bridge.source || link.build != build) Error("BuildStageLink привязан неверно.");
            if (trigger == null || !trigger.isTrigger) Error("BuildZone не имеет trigger-коллайдера.");
        }

        var root = bridge.transform.Find(GateBridgeBuilder.OwnedRootName);
        if (root == null) Error("Нет корня GB_Final.");
        else
        {
            if (root.Find("FinalIsland/Portal") == null) Error("В GB_Final отсутствует запечённый Portal.");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null || !EditorUtility.IsPersistent(filter.sharedMesh))
                    Error("Не сохранён mesh asset: " + filter.gameObject.name);
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                if (collider.sharedMesh == null || !EditorUtility.IsPersistent(collider.sharedMesh))
                    Error("Не сохранён mesh для коллайдера: " + collider.gameObject.name);
        }

        result.AppendLine(errors == 0
            ? "OK: объекты, коллайдеры, зона стройки, портал и ссылки сохранены. Теперь Ctrl+S и Play."
            : "Проверка обнаружила ошибок: " + errors + ". Пересобери мост в Edit Mode и повтори проверку.");
        return result.ToString();
    }

    [MenuItem("Tools/BuilderSim/Локация/Проверить запечённый финал", false, 105)]
    static void CheckMenu()
    {
        var bridge = Object.FindFirstObjectByType<GateBridge>(FindObjectsInactive.Include);
        string result = Validate(bridge);
        Debug.Log(result, bridge);
        EditorUtility.DisplayDialog("TimberWings - Editor Bake", result, "OK");
    }

    [MenuItem("Tools/BuilderSim/Локация/Проверить запечённый финал", true)]
    static bool ValidateMenu() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif

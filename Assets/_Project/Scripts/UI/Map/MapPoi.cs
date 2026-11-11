using System;
using UnityEngine;

// Метка сцены. Существующие метки показываются по ключу из HudMinimap.kinds.
public class MapPoi : MonoBehaviour
{
    public string kind = "PAD_Market";
    public string label = "";
    [Tooltip("Показывать вопрос поблизости, пока объект не осмотрен.")]
    public bool requiresInspection;
    [Tooltip("Уникальный постоянный ID. Если пусто, используется путь объекта в сцене.")]
    public string inspectionId;
    [Min(1f)] public float hintRadius = 12f;
    [Tooltip("ID главы StoryFlow, после которой доступна метка. Пусто — без требования квеста.")]
    public string afterChapter;

    internal static event Action<Transform, string> Inspected;

    // Можно подключить к UnityEvent существующего взаимодействия.
    public void Inspect()
    {
        if (requiresInspection) ReportInspection(transform, inspectionId);
    }

    internal static string GetInspectionId(Transform target, string explicitId = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId.Trim();
        return target.gameObject.scene.name + "/" + StoryFlow.PathOf(target);
    }

    internal static void ReportInspection(Transform target, string explicitId = null)
    {
        if (target == null) return;
        var poi = target.GetComponent<MapPoi>();
        if (string.IsNullOrWhiteSpace(explicitId) && poi != null) explicitId = poi.inspectionId;
        Inspected?.Invoke(target, GetInspectionId(target, explicitId));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEvents() { Inspected = null; }
}

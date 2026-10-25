// Assets/_Project/Scripts/UI/Map/HudMinimap.Layout.cs (НЕ в Editor)
// Стопка под кругом миникарты: плашка зоны, под ней табличка «День N», под ней подсказка «M · Карта».
// Раньше таблички стояли по фиксированным координатам (HudKitBuilder: DayTopY, HintGap), и на другом разрешении/масштабе
// плашка зоны перекрывала «День N», а «Карта» налезала на табличку. Теперь каждая следующая прижимается к нижней кромке
// предыдущей по её фактическому положению на экране (мировые углы RectTransform), при любом Canvas Scaler и аспекте.
using UnityEngine;

public partial class HudMinimap
{
    [Header("Раскладка HUD")]
    [Tooltip("Табличка дня и подсказка «Карта» прижимаются под плашку зоны, а не стоят по фиксированным координатам")]
    public bool autoStackHud = true;
    [Tooltip("Зазор между элементами стопки, px (минус = чуть заходят друг под друга)")]
    public float stackGap = -2f;
    [Tooltip("Имя объекта подсказки «M · Карта» рядом с табличкой дня")]
    public string mapHintName = "MapHint";

    RectTransform mapHint;
    readonly Vector3[] stackCorners = new Vector3[4];

    void LateUpdate()
    {
        if (!autoStackHud || zoneBanner == null) return;
        RectTransform prev = zoneBanner;
        if (dayBanner != null && dayBanner.gameObject.activeInHierarchy) { PlaceBelow(prev, dayBanner); prev = dayBanner; }
        if (mapHint == null)
        {
            var host = dayBanner != null ? dayBanner.parent : zoneBanner.parent;
            if (host != null) mapHint = host.Find(mapHintName) as RectTransform;
        }
        if (mapHint != null && mapHint.gameObject.activeInHierarchy) PlaceBelow(prev, mapHint);
    }

    // below.top-center ставится в above.bottom-center (+ зазор). Сдвиг в мировых координатах, поэтому pivot и якоря не важны
    void PlaceBelow(RectTransform above, RectTransform below)
    {
        above.GetWorldCorners(stackCorners);
        Vector3 bottomCenter = (stackCorners[0] + stackCorners[3]) * 0.5f;
        below.GetWorldCorners(stackCorners);
        Vector3 topCenter = (stackCorners[1] + stackCorners[2]) * 0.5f;
        float s = below.lossyScale.y;
        Vector3 target = bottomCenter + Vector3.down * (stackGap * s);
        Vector3 delta = target - topCenter;
        delta.z = 0f;
        if (delta.sqrMagnitude < 1e-8f) return;
        below.position += delta;
    }
}

// Assets/_Project/Scripts/UI/SafeAreaFitter.cs (НЕ в Editor)
// Прижимает RectTransform к Screen.safeArea: HUD не уходит под вырез камеры, скруглённые углы и жестовую полосу.
// Как подключить (2 минуты, один раз):
//   1. В Canvas с HUD создай пустой дочерний объект "SafeArea" (RectTransform), повесь на него этот компонент.
//   2. Перетащи в "SafeArea" панели HUD, которые стоят у краёв (ресурсы, уровень, кнопки, миникарта).
//      Полноэкранные фоны и затемнения оставь прямо в Canvas.
// Проверка без телефона: Window > General > Device Simulator, выбери iPhone с вырезом.
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;
    Rect applied;
    Vector2Int screen;

    void OnEnable() { Apply(true); }
    void Update() { Apply(false); }

    void Apply(bool force)
    {
        if (rt == null) rt = (RectTransform)transform;
        Rect s = Screen.safeArea;
        var size = new Vector2Int(Screen.width, Screen.height);
        if (!force && s == applied && size == screen) return;
        if (size.x <= 0 || size.y <= 0) return;
        applied = s; screen = size;

        rt.anchorMin = new Vector2(s.xMin / size.x, s.yMin / size.y);
        rt.anchorMax = new Vector2(s.xMax / size.x, s.yMax / size.y);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

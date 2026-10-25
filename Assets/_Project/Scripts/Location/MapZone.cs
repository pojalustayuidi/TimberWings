// Assets/_Project/Scripts/Map/MapZone.cs (НЕ в Editor)
// Именованная зона острова. Её читают HudMinimap (название на мини-карте, цветные области на большой карте)
// и ZoneToast (плашка при входе). Создаёт LocationSigns (под Location/Zones) или ставится руками.
//
// Из кода:
//   MapZone z = MapZone.At(player.position);   // в какой зоне стоит точка (или null)
//   MapZone.Find("Старый Стапель");
using System.Collections.Generic;
using UnityEngine;

// ExecuteAlways: зона живёт и в Edit Mode, MapZone.At / Contains / All работают без Play (для ForestZone и скриптов-инструментов)
[ExecuteAlways]
public class MapZone : MonoBehaviour
{
    public enum Shape { Circle, Rect }

    [Header("Название")]
    public string zoneName = "Зона";
    [Tooltip("Подпись на плашке при входе. Пусто = «НОВАЯ ЗОНА» при первом входе и «ЗОНА» потом")]
    public string caption = "";
    [TextArea(1, 3)] public string description;

    [Header("Форма (в метрах)")]
    public Shape shape = Shape.Circle;
    public float radius = 8f;                        // для круга
    public Vector2 size = new Vector2(16f, 16f);     // для прямоугольника: ширина (X) и глубина (Z), поворот берётся из объекта

    [Header("Внешний вид и поведение")]
    public Color color = Color.white;
    public bool showOnMap = true;
    public bool announce = true;                     // плашка при входе
    [Tooltip("Если зоны пересекаются, побеждает больший приоритет, при равенстве меньшая по площади")]
    public int priority = 0;

    public static readonly List<MapZone> All = new List<MapZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;   // окно префаба и превью: в общий список не попадаем
#endif
        if (!All.Contains(this)) All.Add(this);
    }
    void OnDisable() { All.Remove(this); }

    public float Area
    {
        get { return shape == Shape.Circle ? Mathf.PI * radius * radius : Mathf.Abs(size.x * size.y); }
    }

    public Vector2 MapPosition { get { return new Vector2(transform.position.x, transform.position.z); } }

    // Высота игнорируется: зона работает как плоская область на карте
    public bool Contains(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        if (shape == Shape.Circle) return d.sqrMagnitude <= radius * radius;

        Vector3 l = Quaternion.Euler(0f, -transform.eulerAngles.y, 0f) * d;
        return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.z) <= size.y * 0.5f;
    }

    public static MapZone At(Vector3 p)
    {
        MapZone best = null;
        for (int i = 0; i < All.Count; i++)
        {
            var c = All[i];
            if (c == null || !c.Contains(p)) continue;
            if (best == null || c.priority > best.priority || (c.priority == best.priority && c.Area < best.Area)) best = c;
        }
        return best;
    }

    public static MapZone Find(string name)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].zoneName == name) return All[i];
        return null;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (GetComponent<ForestZone>() != null) return;   // у лесных зон свои гизмо (заливка, зона перехода, подпись)
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, zoneName);
    }

    void OnDrawGizmosSelected()
    {
        if (GetComponent<ForestZone>() != null) return;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
        Vector3 p = transform.position + Vector3.up * 0.1f;
        if (shape == Shape.Circle)
        {
            const int n = 48;
            Vector3 prev = p + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 cur = p + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
        else
        {
            Quaternion q = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Vector3 a = p + q * new Vector3(-hx, 0f, -hz), b = p + q * new Vector3(hx, 0f, -hz);
            Vector3 c = p + q * new Vector3(hx, 0f, hz), d = p + q * new Vector3(-hx, 0f, hz);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
#endif
}
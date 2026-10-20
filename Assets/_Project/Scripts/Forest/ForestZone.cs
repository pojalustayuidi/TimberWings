// Assets/_Project/Scripts/Forest/ForestZone.cs
// Метка лесной зоны на объекте с MapZone: только биом и геометрия. Вся логика леса (создание зон, деревья) живёт в Editor/Forest.
// Форма, имя и цвет на карте берутся из MapZone на этом же объекте.
//
//   ForestZone z = ForestZone.At(point);   // главная лесная зона точки (или null)
//   float d = z.Depth(point);              // метров внутрь от границы
//   float w = z.Weight(point);             // 0 у границы .. 1 глубже feather
using System.Collections.Generic;
using UnityEngine;

public enum ForestBiome { Forest = 0, Cedar = 1, Birch = 2, Autumn = 3, Dev = 4 }

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MapZone))]
public class ForestZone : MonoBehaviour
{
    public ForestBiome biome = ForestBiome.Forest;
    [Tooltip("Ширина мягкой границы, м: на этой полосе внутри зоны плотность и размер деревьев плавно растут")]
    public float feather = 6f;

    public static readonly List<ForestZone> All = new List<ForestZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    MapZone map;
    public MapZone Map { get { if (map == null) map = GetComponent<MapZone>(); return map; } }

    // служебная зона: деревья в ней не генерируются
    public bool IsNeutral { get { return biome == ForestBiome.Dev; } }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
        if (!All.Contains(this)) All.Add(this);
    }
    void OnDisable() { All.Remove(this); }

    // метров внутрь от границы: >0 внутри, <0 снаружи (высота игнорируется, как в MapZone)
    public float Depth(Vector3 p)
    {
        MapZone m = Map;
        Vector3 d = p - m.transform.position;
        d.y = 0f;
        if (m.shape == MapZone.Shape.Circle) return m.radius - d.magnitude;
        Vector3 l = Quaternion.Euler(0f, -m.transform.eulerAngles.y, 0f) * d;
        return Mathf.Min(m.size.x * 0.5f - Mathf.Abs(l.x), m.size.y * 0.5f - Mathf.Abs(l.z));
    }

    public float Weight(Vector3 p)
    {
        float t = Mathf.Clamp01(Depth(p) / Mathf.Max(0.05f, feather));
        return t * t * (3f - 2f * t);
    }

    int Priority { get { return IsNeutral ? int.MaxValue : Map.priority; } }

    public static ForestZone At(Vector3 p)
    {
        ForestZone best = null;
        for (int i = 0; i < All.Count; i++)
        {
            var c = All[i];
            if (c == null || !c.isActiveAndEnabled || !c.Map.Contains(p)) continue;
            if (best == null || c.Priority > best.Priority || (c.Priority == best.Priority && c.Map.Area < best.Map.Area)) best = c;
        }
        return best;
    }
}

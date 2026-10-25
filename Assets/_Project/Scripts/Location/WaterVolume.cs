using System.Collections.Generic;
using UnityEngine;

// Assets/_Project/Scripts/Location/WaterVolume.cs (НЕ в Editor)
// Объём воды реки для игры. Сам ничего не рисует и не двигает: даёт ответ «игрок в воде или нет и насколько глубоко».
// Данные пишет Location Builder TESTER (ось русла, уровень воды, дальность берега), см. AddWaterVolume.
//
// Как пользоваться из PlayerMovement (плавание и прыжок делаются отдельным шагом):
//   if (WaterVolume.TrySample(transform.position, out var w)) {
//       if (w.submersion >= 1.0f) { /* плывём: плавучесть к w.surfaceY, скорость x0.6, w.flow = течение */ }
//       else if (w.submersion >= 0.25f) { /* бредём по мелководью */ }
//   }
// submersion: на сколько метров поверхность выше точки (ног). Мост и берег над водой дают значение <= 0.
[DisallowMultipleComponent]
public class WaterVolume : MonoBehaviour
{
    public static readonly List<WaterVolume> All = new List<WaterVolume>();

    public struct Hit
    {
        public bool inside;        // точка над руслом (по горизонтали)
        public float surfaceY;     // мировая высота поверхности воды
        public float submersion;   // surfaceY - y точки, м (больше 0 = точка под водой)
        public Vector3 flow;       // единичное направление течения (горизонтальное)
        public float lateral;      // расстояние от оси русла, м
    }

    [SerializeField] Vector3[] pts = new Vector3[0];   // точки вдоль русла в локальных координатах, y = уровень воды
    [SerializeField] float[] reach = new float[0];     // метры от оси русла, дальше воды заведомо нет

    [Tooltip("Глубже этого (м) считаем, что герой плывёт")]
    public float swimDepth = 1.0f;
    [Tooltip("Глубже этого (м) герой бредёт по мелководью")]
    public float wadeDepth = 0.25f;

    public int PointCount { get { return pts == null ? 0 : pts.Length; } }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void SetData(Vector3[] points, float[] reaches)
    {
        pts = points; reach = reaches;
    }

    // ближайший объём воды под точкой; false, если точка вне всех рек
    public static bool TrySample(Vector3 world, out Hit hit)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var v = All[i];
            if (v != null && v.Sample(world, out hit)) return true;
        }
        hit = default(Hit);
        return false;
    }

    public bool Sample(Vector3 world, out Hit hit)
    {
        hit = default(Hit);
        if (pts == null || reach == null || pts.Length < 2 || reach.Length != pts.Length) return false;

        Vector3 p = transform.InverseTransformPoint(world);
        Vector2 q = new Vector2(p.x, p.z);
        float best = float.MaxValue, bt = 0f; int bi = -1;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = new Vector2(pts[i].x, pts[i].z), ab = new Vector2(pts[i + 1].x, pts[i + 1].z) - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
            float d2 = (q - (a + ab * t)).sqrMagnitude;
            if (d2 < best) { best = d2; bi = i; bt = t; }
        }
        if (bi < 0) return false;

        float lat = Mathf.Sqrt(best);
        if (lat > Mathf.Lerp(reach[bi], reach[bi + 1], bt)) return false;

        float sy = Mathf.Lerp(pts[bi].y, pts[bi + 1].y, bt);
        float surfaceWorld = transform.TransformPoint(new Vector3(p.x, sy, p.z)).y;
        Vector3 lf = pts[bi + 1] - pts[bi]; lf.y = 0f;

        hit.inside = true;
        hit.surfaceY = surfaceWorld;
        hit.submersion = surfaceWorld - world.y;
        hit.flow = lf.sqrMagnitude < 1e-8f ? Vector3.zero : transform.TransformDirection(lf.normalized);
        hit.lateral = lat;
        return true;
    }

    public bool IsSwimming(Vector3 feet) { Hit h; return Sample(feet, out h) && h.submersion >= swimDepth; }
    public bool IsWading(Vector3 feet) { Hit h; return Sample(feet, out h) && h.submersion >= wadeDepth && h.submersion < swimDepth; }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (pts == null || pts.Length < 2) return;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        for (int i = 0; i < pts.Length - 1; i++)
            Gizmos.DrawLine(transform.TransformPoint(pts[i]), transform.TransformPoint(pts[i + 1]));
    }
#endif
}

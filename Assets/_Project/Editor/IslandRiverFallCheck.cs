// Assets/_Project/Editor/IslandV2/IslandRiverFallCheck.cs
// Проверка стыка River_Water и River_Fall без Game View. Только чтение: сцену не меняет.
// Что проверяет:
//   1. Полотно одно: число вершин River_Fall = (число выходов реки) x FallRows x FallCols. Лишний слой (старые «передний + задний», Waterfall_Veil) даст кратное больше.
//   2. Шов: оба конца верхнего ряда водопада совпадают с вершиной ленты River_Water (допуск 2 см по горизонтали и по высоте).
//   3. Излом на пороге: угол между лентой реки и первым звеном водопада (не больше 25 градусов; раньше было около 40).
// Что НЕ проверяет: внешний вид. Для этого нужен Game View (см. отчёт).
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandRiverFallCheck
    {
        const float Tol = 0.02f;
        const float MaxKinkDeg = 25f;

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/Река: проверить стык воды и водопада", false, 129)]
        public static void Run()
        {
            Transform water = null, fall = null;
            int markersOut = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid() || t.gameObject.hideFlags != HideFlags.None) continue;
                if (t.name == IslandRiverWater.ObjectName) water = t;
                else if (t.name == IslandRiverWater.FallObjectName) fall = t;
                else if (t.name.StartsWith("FALL_RiverOut")) markersOut++;
            }
            var sb = new StringBuilder("RIVER FALL CHECK\n");
            if (water == null || fall == null) { Debug.LogError("RIVER FALL CHECK: в открытой сцене нет River_Water или River_Fall (сначала «2. Собрать блокаут»)."); return; }
            var wm = water.GetComponent<MeshFilter>()?.sharedMesh;
            var fm = fall.GetComponent<MeshFilter>()?.sharedMesh;
            if (wm == null || fm == null) { Debug.LogError("RIVER FALL CHECK: у River_Water/River_Fall нет меша."); return; }

            int perFall = IslandRiverWater.FallRows * IslandRiverWater.FallCols;
            int blocks = fm.vertexCount / perFall;
            bool ok = true;
            if (fm.vertexCount % perFall != 0 || blocks != markersOut)
            {
                ok = false;
                sb.AppendLine("FAIL слои: вершин " + fm.vertexCount + ", блок водопада = " + perFall + ", блоков " + (fm.vertexCount / (float)perFall).ToString("0.##") +
                              ", выходов реки (FALL_RiverOut*) " + markersOut + ". Скорее всего старая сборка с двумя слоями: пересоберите блокаут.");
            }
            else sb.AppendLine("OK слои: полотен " + blocks + " = выходов реки " + markersOut + " (по одному).");

            var wv = wm.vertices; var fv = fm.vertices;
            for (int b = 0; b < blocks; b++)
            {
                int o = b * perFall, cols = IslandRiverWater.FallCols;
                Vector3 L0 = fall.TransformPoint(fv[o]), R0 = fall.TransformPoint(fv[o + cols - 1]);
                Vector3 mid0 = fall.TransformPoint(fv[o + cols / 2]), mid1 = fall.TransformPoint(fv[o + cols + cols / 2]);
                float dl = Nearest(water, wv, L0), dr = Nearest(water, wv, R0);
                bool seam = dl <= Tol && dr <= Tol;
                if (!seam) ok = false;
                sb.AppendLine((seam ? "OK" : "FAIL") + " шов #" + b + ": левый конец " + dl.ToString("0.000") + " м, правый " + dr.ToString("0.000") + " м от ленты реки (допуск " + Tol + ").");

                // излом: река идёт к порогу почти горизонтально; первое звено водопада - вниз и наружу
                Vector3 seg = mid1 - mid0;
                float ang = Mathf.Atan2(-seg.y, new Vector2(seg.x, seg.z).magnitude) * Mathf.Rad2Deg;   // 0 = горизонтально, 90 = вертикально вниз
                bool kink = ang <= MaxKinkDeg;
                if (!kink) ok = false;
                sb.AppendLine((kink ? "OK" : "FAIL") + " излом #" + b + ": первое звено под " + ang.ToString("0.0") + " градусов к горизонту (допуск " + MaxKinkDeg + ").");
            }
            sb.AppendLine(ok ? "ИТОГ: геометрический стык в порядке. Внешний вид проверьте в Game View." : "ИТОГ: есть замечания (см. выше).");
            if (ok) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
        }

        // расстояние (по XYZ) от точки до ближайшей вершины ленты
        static float Nearest(Transform water, Vector3[] wv, Vector3 world)
        {
            float best = float.MaxValue;
            for (int i = 0; i < wv.Length; i++)
            {
                float d = (water.TransformPoint(wv[i]) - world).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}

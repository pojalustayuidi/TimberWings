// Assets/_Project/Editor/IslandV2/IslandField.River.cs
// Остров v2, этап 4 (задача D): где река пересекает край острова. Часть partial-класса IslandField (основная часть: IslandField.cs).
// Нужно для выреза в обрыве под водопад: углубление русла у края (Height), убранный валик и мокрая скала (IslandCliff), метки FALL_* для этапа 6.
//
//   RiverEdges       список пересечений реки с контуром: точка на контуре, направление наружу, вход это (река приходит снаружи) или выход (уходит с острова)
//   NotchCut(x, z)   0..1: насколько здесь русло углублено вырезом (1 у самого края, 0 дальше notchLength метров)
//   NotchWeight(x,z) 0..1: близость к вырезу по ширине (для цвета скалы и кромки): 1 в русле, 0 дальше halfWidth + notchBlend
// Этап E (русло и сток): река течёт вниз. Поверхность воды опускается от истока к устью на riverDrop метров, русло глубиной riverWaterDepth,
// у кромки вода мелеет до riverLip (порог), откосы берега полого поднимаются до земли.
//   RiverNearest(x,z,out s)   расстояние до оси реки; s = 0..1 доля пути вдоль реки от истока (по длине ломаной)
//   RiverSurfaceY(s)          высота воды на оси реки без выреза (метры, Y=0 на земле у костра)
//   RiverSurfaceAt(x,z)       то же с вырезом у кромки: куда вода опускается перед краем
//   RiverBedY(x,z)            высота дна русла в точке оси (поверхность минус глубина воды), для проверки
//   RiverInsidePieces()       куски оси реки внутри острова (концы точно на контуре): по ним строится вода и объём для плавания
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    public partial class IslandField
    {
        public struct RiverEdge
        {
            public Vector2 pos;       // точка на контуре
            public Vector2 outward;   // единичное направление наружу острова
            public bool isOutlet;     // true: река уходит с острова (по течению: порядок riverPoints), false: приходит снаружи
        }

        List<RiverEdge> riverEdges;

        public List<RiverEdge> RiverEdges
        {
            get
            {
                if (riverEdges != null) return riverEdges;
                riverEdges = new List<RiverEdge>();
                var pl = riverDense;
                for (int k = 0; k + 1 < pl.Length; k++)
                {
                    bool ia = Inside(pl[k].x, pl[k].y), ib = Inside(pl[k + 1].x, pl[k + 1].y);
                    if (ia == ib) continue;
                    Vector2 a = pl[k], b = pl[k + 1];
                    Vector2 inP = ia ? a : b, outP = ia ? b : a;
                    for (int it = 0; it < 14; it++)
                    {
                        Vector2 mid = (inP + outP) * 0.5f;
                        if (Inside(mid.x, mid.y)) inP = mid; else outP = mid;
                    }
                    Vector2 edge = (inP + outP) * 0.5f;
                    Vector2 outward = (outP - inP);
                    // направление наружу лучше брать вдоль реки: так ложбина идёт по течению
                    Vector2 along = (b - a).normalized;
                    outward = ia ? along : -along;
                    riverEdges.Add(new RiverEdge { pos = edge, outward = outward.normalized, isOutlet = ia });
                }
                return riverEdges;
            }
        }

        // ---------------------------------------------------------------- этап E: сток реки
        float[] riverCum;     // длина ломаной до каждой точки, метры
        float riverTotal = 1f;

        void EnsureRiverLen()
        {
            if (riverCum != null && riverCum.Length == riverDense.Length) return;
            riverCum = new float[riverDense.Length];
            float acc = 0f;
            for (int i = 1; i < riverDense.Length; i++)
            {
                acc += Vector2.Distance(riverDense[i - 1], riverDense[i]);
                riverCum[i] = acc;
            }
            riverTotal = Mathf.Max(1f, acc);
        }

        // расстояние до оси реки и доля пути s (0 у истока, 1 у устья)
        public float RiverNearest(float x, float z, out float s)
        {
            s = 0f;
            if (riverDense.Length < 2) return float.MaxValue;
            EnsureRiverLen();
            Vector2 p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < riverDense.Length; i++)
            {
                Vector2 a = riverDense[i], ab = riverDense[i + 1] - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d2 = (a + ab * t - p).sqrMagnitude;
                if (d2 < best)
                {
                    best = d2;
                    s = (riverCum[i] + Mathf.Sqrt(l2) * t) / riverTotal;
                }
            }
            return Mathf.Sqrt(best);
        }

        // высота воды на оси без выреза: у истока riverSurfaceBelow ниже земли, к устью опускается ещё на riverDrop
        public float RiverSurfaceY(float s)
        {
            return -L.riverSurfaceBelow - L.riverDrop * Mathf.Clamp01(s);
        }

        // с вырезом: у кромки вода падает ещё на notchDrop (набирает скорость перед водопадом)
        public float RiverSurfaceAt(float x, float z)
        {
            float s;
            RiverNearest(x, z, out s);
            return RiverSurfaceY(s) - L.notchDrop * NotchCut(x, z);
        }

        // глубина воды в точке с учётом брода и порога водопада
        public float RiverWaterDepthAt(float fordW, float notchCut)
        {
            float w = Mathf.Lerp(L.riverWaterDepth, Mathf.Max(0.15f, L.riverWaterDepth * L.fordDepthShare), fordW);
            return Mathf.Lerp(w, Mathf.Min(w, L.riverLip), notchCut);
        }

        public float RiverBedY(float x, float z)
        {
            return RiverSurfaceAt(x, z) - RiverWaterDepthAt(FordWeight(x, z), NotchCut(x, z));
        }

        // Рельеф русла и берега в одной точке. h = высота земли без реки (холмы, площадки). Возвращает высоту с руслом.
        // Профиль поперёк реки (a = расстояние до оси / полуширина воды):
        //   a от 0 до 0,45  плоское дно на глубине воды
        //   a от 0,45 до 1  дно плавно поднимается до уровня воды (на a = 1 дно ровно на уровне воды: это и есть берег)
        //   дальше          стена поднимается от воды до земли; ширина растёт с высотой берега (riverBankRun, у брода riverFordRun)
        // Стена: ширина откоса = riverBankRun x высота берега над водой (не меньше riverBank). Крутизна у воды до ~2/riverBankRun (0,9: около 65°).
        // Берег всегда хотя бы на 0,15 м выше воды (ложбины холмов у реки подпираются).
        float RiverProfile(float h, float rd, float s, float fordW, float notchCut)
        {
            float half = L.riverWidth * 0.5f;
            float surf = RiverSurfaceY(s) - L.notchDrop * notchCut;
            if (rd <= half)
            {
                float wdepth = RiverWaterDepthAt(fordW, notchCut);
                float a = rd / half;
                return surf - wdepth * (1f - Step(0.45f, 1f, a));
            }
            h *= Step(half + 1f, half + 14f, rd);   // холмы у реки сглаживаются: вдоль русла берег ровный, и стены одинаково высокие
            float gTarget = Mathf.Max(h, surf + 0.15f);
            float run = Mathf.Lerp(L.riverBankRun, L.riverFordRun, fordW);   // стена русла крутая, у брода пологая
            float bankW = Mathf.Max(Mathf.Max(0.5f, L.riverBank), run * (gTarget - surf));
            float raise = 1f - Step(half, half + bankW * 1.5f, rd);
            float g = Mathf.Lerp(h, gTarget, raise);
            float x = Mathf.Clamp01((rd - half) / bankW);
            // стена: круто вверх от воды и мягкая кромка наверху (1-(1-x)^2); у брода обычный плавный подъём
            float wall = 1f - (1f - x) * (1f - x);
            float t = Mathf.Lerp(wall, Step(0f, 1f, x), fordW);
            return Mathf.Lerp(surf, g, t);
        }

        // куски оси реки внутри острова. Концы кусков лежат на контуре (бисекция). Обычно один кусок: от входа до выхода
        public List<List<Vector2>> RiverInsidePieces()
        {
            var res = new List<List<Vector2>>();
            var pl = riverDense;
            List<Vector2> cur = null;
            for (int k = 0; k < pl.Length; k++)
            {
                bool inK = Inside(pl[k].x, pl[k].y);
                if (k > 0)
                {
                    bool inP = Inside(pl[k - 1].x, pl[k - 1].y);
                    if (inP != inK)
                    {
                        Vector2 inPt = inP ? pl[k - 1] : pl[k], outPt = inP ? pl[k] : pl[k - 1];
                        for (int it = 0; it < 16; it++)
                        {
                            Vector2 mid = (inPt + outPt) * 0.5f;
                            if (Inside(mid.x, mid.y)) inPt = mid; else outPt = mid;
                        }
                        Vector2 edge = (inPt + outPt) * 0.5f;
                        if (inK) { cur = new List<Vector2> { edge }; }                   // вошли на остров
                        else { if (cur != null) { cur.Add(edge); res.Add(cur); cur = null; } }   // вышли
                    }
                }
                if (inK)
                {
                    if (cur == null) cur = new List<Vector2>();
                    cur.Add(pl[k]);
                }
            }
            if (cur != null && cur.Count >= 2) res.Add(cur);   // ось закончилась внутри острова
            return res;
        }

        public float NotchCut(float x, float z)
        {
            var list = RiverEdges;
            if (list.Count == 0 || L.notchDrop <= 0f) return 0f;
            var p = new Vector2(x, z);
            float best = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Vector2.Distance(p, list[i].pos);
                float w = 1f - Step(0f, Mathf.Max(1f, L.notchLength), d);
                if (w > best) best = w;
            }
            return best;
        }

        public float NotchWeight(float x, float z)
        {
            var list = RiverEdges;
            if (list.Count == 0) return 0f;
            var p = new Vector2(x, z);
            float half = L.riverWidth * 0.5f;
            float best = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Vector2.Distance(p, list[i].pos);
                float w = 1f - Step(half, half + Mathf.Max(0.5f, L.notchBlend), d);
                if (w > best) best = w;
            }
            return best;
        }
    }
}

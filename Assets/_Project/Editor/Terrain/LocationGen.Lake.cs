#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Terrain/LocationGen.Lake.cs
// Задача Р4: озеро (только рельеф). Чаша с плоским дном, стенкой и мелким пологим берегом; уровень воды задан заранее
// (Settings.LakeWaterY), чтобы будущий модуль воды лёг ровно на чашу: внутри водного радиуса земля везде ниже воды.
// Профиль от центра (dl = расстояние до lakePos, R = lakeRadius, Rw = R * lakeWaterFrac):
//   dl <= 0.3 Rw        плоское дно на -lakeDepth
//   0.3 Rw .. Rw        стенка чаши (smoothstep) до уровня воды, у кромки воды пологая
//   Rw .. R             песчаный берег от уровня воды до 0, уклон не больше ~0.14
//   R .. R + lakeShore  земля плавно возвращается (рельеф вокруг чаши не лезет к воде)
using UnityEngine;

public partial class LocationBuilder
{
    sealed partial class Gen
    {
        float LakeWaterY { get { return s.LakeWaterY; } }
        float LakeWaterR { get { return s.lakeRadius * Mathf.Clamp(s.lakeWaterFrac, 0.3f, 0.85f); } }

        // высота чаши на расстоянии dl от центра; на dl >= lakeRadius = 0
        float LakeBowl(float dl)
        {
            float R = s.lakeRadius, Rw = LakeWaterR, Rb = Rw * 0.3f, Yw = LakeWaterY;
            if (dl >= R) return 0f;
            if (dl <= Rb) return -s.lakeDepth;
            if (dl <= Rw) return Mathf.Lerp(-s.lakeDepth, Yw, SS(Rb, Rw, dl));
            float t = (dl - Rw) / Mathf.Max(0.5f, R - Rw);
            float e = Mathf.Lerp(t, t * t * (3f - 2f * t), 0.5f);   // у воды полого, у бровки берега плавный выход
            return Yw * (1f - e);
        }

        // 0 внутри водного радиуса (там площадка/тропа/долина не поднимают дно), 1 с запасом снаружи
        float LakeKeep(float x, float z)
        {
            if (!s.lakeEnabled) return 1f;
            float dl = Vector2.Distance(new Vector2(x, z), s.lakePos);
            return SS(LakeWaterR - 0.3f, LakeWaterR + 2.5f, dl);
        }

        // Проверки чаши (пишет предупреждения и одну строку итога в Console)
        void ValidateLake()
        {
            if (!s.lakeEnabled || hu == null) return;
            float R = s.lakeRadius, Rw = LakeWaterR, Yw = LakeWaterY;
            Vector2 lp = s.lakePos;

            // (1) земля над водой внутри водного радиуса («торчащая земля»)
            int stick = 0; float worst = -999f;
            for (float a = 0f; a < 360f; a += 10f)
                for (float r = 0f; r <= Rw; r += 0.5f)
                {
                    float x = lp.x + Mathf.Sin(a * Mathf.Deg2Rad) * r, z = lp.y + Mathf.Cos(a * Mathf.Deg2Rad) * r;
                    if (Mathf.Abs(x) > half - 1f || Mathf.Abs(z) > half - 1f) continue;
                    float over = HeightU(x, z) - (Yw + 0.03f);   // допуск 0.03 u: сглаживание heightmap
                    if (over > 0f) { stick++; worst = Mathf.Max(worst, over); }
                }
            if (stick > 0)
                warnings.Add("Озеро: внутри водного радиуса " + stick + " точек земли выше уровня воды (до +" + worst.ToString("0.00") + " u): вода будет рваной. Проверь площадки и тропы рядом с чашей.");

            // (2) Pond на берегу: пирс на ровной площадке, вода от неё на расстоянии (не на площадке и не вдали)
            var pond = pads.Find(q => q.def.name == "Pond");
            if (pond != null)
            {
                float sd = PadSD(pond, lp);   // от центра озера до края площадки
                if (sd < Rw + 1f) warnings.Add("Pond: край площадки в " + (sd - Rw).ToString("0.0") + " u от воды, нужно хотя бы 1 u ровной суши. Сдвинь озеро от площадки или уменьши водный радиус.");
                else if (sd > R + 2f) warnings.Add("Pond: площадка дальше берега озера на " + (sd - R).ToString("0.0") + " u, пирс не дойдёт до воды. Сдвинь озеро ближе.");
            }

            // (3) озеро и тропы: ни одна тропа не заходит в чашу (кроме 1 u запаса)
            foreach (var tr in trails)
                for (int i = 0; i < tr.p.Count; i++)
                    if (Vector2.Distance(tr.p[i], lp) - tr.half[i] < R + 1f)
                    {
                        warnings.Add("Озеро: тропа " + tr.name + " заходит в чашу озера. Подвинь озеро или тропу.");
                        break;
                    }

            // (4) озеро и река: чаша с берегом не касается русла с бровкой
            float need = R + s.lakeShore, minGap = 999f;
            for (float z = -half; z <= RiverSrcZ(); z += 1f)
            {
                float rw = s.newRiver ? RiverHalfTop(z) + s.riverBrowWidth : s.riverWidth * 0.5f + s.riverBank + s.riverSlope;
                minGap = Mathf.Min(minGap, Vector2.Distance(new Vector2(RiverX(z), z), lp) - rw);
            }
            if (minGap < need) warnings.Add("Озеро близко к реке: зазор " + minGap.ToString("0.0") + " u, нужно не меньше " + need.ToString("0.0") + " u (радиус чаши + берег).");

            // (5) край острова: чаша целиком на суше с запасом на ровную кромку
            float edge = EdgeIn(lp.x, lp.y);
            if (edge < R + 1f) warnings.Add("Озеро: до края острова " + edge.ToString("0.0") + " u, чаша радиусом " + R.ToString("0.0") + " выходит к кромке.");

            // (6) уклон берега
            float maxSl = 0f;
            for (float a = 0f; a < 360f; a += 15f)
                for (float r = Rw; r < R; r += 0.5f)
                {
                    float x0 = lp.x + Mathf.Sin(a * Mathf.Deg2Rad) * r, z0 = lp.y + Mathf.Cos(a * Mathf.Deg2Rad) * r;
                    float x1 = lp.x + Mathf.Sin(a * Mathf.Deg2Rad) * (r + 0.5f), z1 = lp.y + Mathf.Cos(a * Mathf.Deg2Rad) * (r + 0.5f);
                    if (Mathf.Abs(x1) > half - 1f || Mathf.Abs(z1) > half - 1f) continue;
                    maxSl = Mathf.Max(maxSl, Mathf.Abs(HeightU(x1, z1) - HeightU(x0, z0)) / 0.5f);
                }
            Debug.Log("[LocationBuilder] Озеро: центр (" + lp.x.ToString("0.#") + ", " + lp.y.ToString("0.#") + "), чаша R=" + R.ToString("0.#")
                + ", вода R=" + Rw.ToString("0.#") + " на Y=" + Yw.ToString("0.00") + " u, дно " + (-s.lakeDepth).ToString("0.00")
                + ", макс. уклон берега " + maxSl.ToString("0.00") + ", зазор до реки " + minGap.ToString("0.#") + " u.");
            if (maxSl > 0.3f) warnings.Add("Озеро: берег круче 0.3 (" + maxSl.ToString("0.00") + "): рядом площадка или тропа подтягивает землю. Для мелкого берега увеличь радиус чаши.");
        }
    }
}
#endif

#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Terrain/LocationGen.River2.cs
// Задача Р3 «Долина реки»: новая ось, ширина и профиль русла (Settings.newRiver). Старая река (CarveRiverCore/CarveGorge) не тронута.
// Ось: от скалы-истока Source (северо-запад) к мосту, у моста прямой участок (ровные берега), дальше меандр и выход за южную кромку.
// Ширина: узкая у истока (40%), монотонно шире к югу. Профиль поперёк: плоское дно, берег (smoothstep), ровная бровка, плавный выход в рельеф.
// Статические функции оси и ширины общие для генератора и контура острова (LocationIslandOutline), чтобы они не расходились.
using UnityEngine;

public partial class LocationBuilder
{
    static float Sm(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // множитель ширины русла по течению: 0.4 у истока, riverEndWidth у южной кромки (без шума плёсов)
    public static float RiverWidthK2(Settings s, float srcZ, float half, float z)
    {
        return Mathf.Lerp(0.4f, Mathf.Max(0.5f, s.riverEndWidth), Sm(srcZ, -half, z));
    }

    // полуширина русла у воды на z = 0 (там встанет мост), u
    static float RiverHalfW0(Settings s, float srcZ, float half)
    {
        return s.riverWidth * 0.5f * RiverWidthK2(s, srcZ, half, 0f);
    }

    // x оси на z = 0: ближняя бровка (с площадкой Bridge) чуть заходит на край площадки, берега по обе стороны ровные
    public static float RiverBaseX2(Settings s, float padX, float padHd, float srcZ, float half)
    {
        float hwT = RiverHalfW0(s, srcZ, half) + s.riverBankRun;
        return padX - padHd + 0.4f - hwT + s.riverOffsetX;
    }

    // ось русла: линия исток -> мост -> юг + меандр, который гасится на прямом участке у моста и у самого истока
    public static float RiverAxis2(Settings s, float baseX, float srcX, float srcZ, float half, float z)
    {
        float line = z >= 0f
            ? Mathf.Lerp(baseX, srcX, Sm(0f, srcZ, z))
            : baseX + s.riverSouthDrift * Sm(0f, half, -z);
        float win = Sm(s.riverBridgeReach, s.riverBridgeReach + 18f, Mathf.Abs(z)) * Sm(srcZ, srcZ - 10f, z);
        float mea = s.riverMeander * Mathf.Sin(z * 0.027f + 0.4f)
                  + s.riverCurve * (Mathf.Sin(z * 0.05f) + 0.4f * Mathf.Sin(z * 0.11f));
        // западные заходы меандра (к кромке острова, там мало места) сжаты: на юге дальний берег иначе упирается в край Terrain
        if (mea < 0f) mea *= Mathf.Lerp(1f, 0.35f, Sm(0f, 3f, -mea));
        return line + mea * win;
    }

    sealed partial class Gen
    {
        float RiverSrcX() { return ZonePos("Source").x; }

        float RiverHalfW(float z) { return s.riverWidth * 0.5f * RiverTaper(z) * RiverWidthVar(z); }

        // длина берега от воды до бровки: дальний берег (к кромке острова) короче, места там мало
        float RiverBankRunAt(float dx) { return dx < 0f ? s.riverBankRun * s.riverFarBankFrac : s.riverBankRun; }

        // полуширина до внешнего края бровки по обе стороны, для контекста модулей (максимум из двух сторон)
        float RiverHalfTop(float z) { return RiverHalfW(z) + s.riverBankRun; }

        // Поперечный профиль: дно (d < fh) -> берег (fh..hwT, smoothstep) -> ровная бровка (browWidth) -> плавный выход (browBlend).
        // Вода везде на waterLevel: дно всегда ниже воды минимум на 0.25 u (у порога у кромки тоже), бровка выше воды на bank-height.
        float CarveRiverNew(float x, float z, float h, float keepValley, float padCore)
        {
            float m = RiverMask(z);
            if (m <= 0f) return h;
            float dx = x - RiverX(z);
            float d = Mathf.Abs(dx);
            float hwW = RiverHalfW(z);
            float fh = hwW * Mathf.Clamp(s.riverFloorFrac, 0.1f, 0.9f);
            float hwT = hwW + RiverBankRunAt(dx);
            float browEdge = hwT + s.riverBrowWidth;
            float reach = browEdge + s.riverBrowBlend;
            if (d >= reach) return h;

            float srcZ = RiverSrcZ();
            // уровень бровки: на всём течении riverBrowLevel, у скалы-истока сходится к ровной площадке горы
            float browY = (s.riverHead && s.riverFullLength)
                ? Mathf.Lerp(s.riverBrowLevel, s.headFlatLevel, Sm(srcZ - 14f, srcZ - 2f, z))
                : s.riverBrowLevel;

            // дно плоское поперёк, медленно плывёт только вдоль течения; глубже к югу
            float t01 = Mathf.Clamp01((RiverTaper(z) - 0.4f) / Mathf.Max(0.1f, s.riverEndWidth - 0.4f));
            float depthAtBed = Mathf.Lerp(0.5f, 1.6f, t01);
            float lip = 1f - Sm(2.5f, s.gorgeLipLength, EdgeIn(x, z));
            float bed = waterLevel - depthAtBed + (Perlin(0.5f, z * 0.08f, 610f) - 0.5f) * 0.3f;
            bed = Mathf.Lerp(bed, waterLevel - 0.4f, lip * 0.9f);        // порог у южной кромки: дно поднимается, но остаётся под водой
            bed = Mathf.Min(bed, waterLevel - 0.25f);

            float target;
            if (d < hwT)
            {
                float tb = Mathf.Clamp01((d - fh) / Mathf.Max(0.5f, hwT - fh));
                target = Mathf.Lerp(bed, browY, Sm(0f, 1f, tb));
            }
            else target = browY;

            float k = (1f - Sm(browEdge, reach, d)) * m * (1f - padCore);
            return k > 0f ? Mathf.Lerp(h, target, k) : h;
        }

        // ---------------------------------------------------------- тропа за мостом к Dock

        // Dock стоит за краем Terrain (x = -80, Terrain кончается на -half), поэтому тропа идёт от дальней бровки моста до края Terrain в сторону Dock
        Vector2 DockApproachU()
        {
            Vector2 dk = s.dockPos;
            float lim = half - 1.5f;
            return new Vector2(Mathf.Clamp(dk.x, -lim, lim), Mathf.Clamp(dk.y, -lim, lim));
        }

        void BuildDockTrail()
        {
            if (!s.newRiver || bridge == null) return;
            float z0 = bridge.pos.y;
            float side = Mathf.Sign(s.dockPos.x - RiverX(z0));          // с какой стороны реки Dock
            float edgeX = RiverX(z0) + side * (RiverHalfW(z0) + RiverBankRunAt(side) + 0.6f);   // на ровной бровке за мостом
            Vector2 start = new Vector2(edgeX, z0);
            Vector2 end = DockApproachU();
            if (Vector2.Distance(start, end) < 1.2f) { warnings.Add("Тропа к Dock: от бровки до края Terrain меньше 1.2 u, тропа не построена."); return; }
            Vector2 dir = (end - start).normalized;
            var gp = new Pad { def = new PadDef("Dock", 0f, 0f, 1f, 1f, Color.white) { straight = true }, pos = end, hw = 0.5f, hd = 0.5f, rad = 0.5f };
            gp.Orient(-dir);
            var tr = MakeTrail(gp, start, dir, false, "Trail_Dock");
            trails.Add(tr);
        }

        // ---------------------------------------------------------- самопроверка (пишет в Console)

        void ValidateRiverNew()
        {
            if (!s.newRiver || hu == null) return;
            float srcZ = RiverSrcZ();
            int dry = 0, hang = 0; float worstFloor = -999f;
            for (float z = -half + 3f; z < srcZ - 6f; z += 2f)
            {
                float cx = RiverX(z);
                float hf = HeightU(cx, z);
                worstFloor = Mathf.Max(worstFloor, hf - waterLevel);
                if (hf > waterLevel - 0.05f) dry++;
                // «висящая» вода: на пути к бровке вода (земля ниже waterLevel) не должна появляться вне русла
                float hwT = RiverHalfW(z) + s.riverBankRun;
                for (int sd = -1; sd <= 1; sd += 2)
                    for (float o = hwT + 0.5f; o < hwT + s.riverBrowWidth + s.riverBrowBlend; o += 0.5f)
                    {
                        float px = cx + sd * o;
                        if (Mathf.Abs(px) < half - 1f && HeightU(px, z) < waterLevel + 0.02f) { hang++; break; }
                    }
            }
            if (dry > 0) warnings.Add("Река: на " + dry + " сечениях дно выше воды (наивысшая точка дна над водой " + worstFloor.ToString("0.00") + " u): воды там не будет.");
            if (hang > 0) warnings.Add("Река: рядом с руслом есть " + hang + " низких мест на уровне воды вне русла (возможна «висящая» вода): подними riverBrowLevel или уменьши понижение долины.");

            if (bridge != null)
            {
                float z0 = bridge.pos.y, cx = RiverX(z0);
                float mnH = 999f, mxH = -999f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float hwT = RiverHalfW(z0) + RiverBankRunAt(sd);
                    for (float o = hwT + 0.2f; o <= hwT + s.riverBrowWidth - 0.2f; o += 0.25f)
                        for (float dz = -s.bridgeWidth * 0.5f; dz <= s.bridgeWidth * 0.5f; dz += 0.5f)
                        {
                            float v = HeightU(cx + sd * o, z0 + dz);
                            mnH = Mathf.Min(mnH, v); mxH = Mathf.Max(mxH, v);
                        }
                }
                if (mxH - mnH > 0.15f)
                    warnings.Add("Мост: бровки у моста неровные, перепад " + (mxH - mnH).ToString("0.00") + " u (допуск 0.15). Подними riverBrowWidth или проверь площадку Bridge.");
            }

            // срезы для сверки со схемой: высоты через 1 u от оси, влево/вправо
            foreach (float zz in new[] { srcZ - 12f, srcZ * 0.5f, 0f, -half * 0.5f })
            {
                float cx = RiverX(zz);
                var sb = new System.Text.StringBuilder();
                for (int o = -14; o <= 14; o++) sb.Append(HeightU(cx + o, zz).ToString("0.0")).Append(' ');
                Debug.Log("[LocationBuilder] срез реки z=" + zz.ToString("0") + " (x от оси -14..+14, u): " + sb + "| вода " + waterLevel.ToString("0.00"));
            }
        }
    }
}
#endif

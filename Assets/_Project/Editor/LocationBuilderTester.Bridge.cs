#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilderTester.Bridge.cs
// Деревянный мост через реку для Location Builder TESTER (partial-часть LocationBuilderTester).
// Заменяет белый маркер Bridge_River. Вызывается из BuildMarkers: BuildBridge(L, F, root).
//
// Что строится (всё в u, меш в конце масштабируется на worldScale, как BuildLookout):
//   1. Настил из досок поперёк с лёгкой аркой (Cfg.bridgeArch, 0.4-0.6 u в середине), лаги вдоль и поперечные балки под ним.
//   2. Перила: стойки, верхняя и нижняя жерди; в среднем пролёте вместо нижней жерди провисающая верёвка.
//   3. Две каменные опоры в воде с деревянным оголовком; вокруг каждой пенные круги на уровне воды.
//   4. Входы: столбики (Cfg.bridgePosts) с фонарями (Cfg.bridgeLamps) и небольшая табличка на стороне площадки Bridge.
// Геометрия:
//   - Ось моста: от кромки площадки Bridge у реки (a) до центра Landing (b), как у старого маркера.
//   - Концы настила = Hy(a) и Hy(b) плюс 0.04 u (против мерцания на стыке с землёй): ступеньки нет.
//   - Толщина настила с лагами подбирается так, чтобы просвет над водой в середине был около 0.9 u (0.8-1).
//   - Мост целиком лежит в проходе RiverWall (|s - bridgeS| < BridgeWallGap): ширина при необходимости уменьшается.
// Коллайдеры: настил (4 бокса вдоль арки), перила невидимой стенкой, заделка щелей над водой по бокам от настила.
// Рельеф (Field), тропы и CheckBypass не меняются: мост отдельный объект, как суша он не считается.
// Карта файла: BridgePlan и PlanBridge (план), BuildBridgeMesh (меш), AddBridgeColliders / AddBridgeGapFill (коллайдеры),
//              MakeBridgeMats (материалы), BuildBridge (сборка в сцене).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilderTester
{
    // подмеши моста (индекс = номер материала в MakeBridgeMats)
    const int BR_A = 0, BR_B = 1, BR_DARK = 2, BR_STONE = 3, BR_GLOW = 4, BR_RED = 5, BR_FOAM = 6, BR_ROPE = 7;
    const int BrSubs = 8;

    // как в BuildBarriers: сегменты RiverWall с |s - bridgeS| < BridgeWallGap пропускаются (проход под мост)
    const float BridgeWallGap = 2.5f;
    const float BridgeRailH = 1.5f;      // высота перил над настилом, u
    const float BridgePlankT = 0.12f;    // толщина доски, u
    const float BridgeLip = 0.04f;       // насколько концы настила выше земли (против мерцания на стыке), u

    // план моста: все размеры в u
    sealed class BridgePlan
    {
        public Vector2 a, b, d, side;          // начало (кромка площадки у реки), конец (Landing), ось, поперёк оси
        public float len, w, arch;             // длина оси, ширина настила, подъём в середине
        public float yA, yB, thick, yW;        // верх настила на концах, толщина настила с лагами, уровень воды
        public float tc, ww;                   // центр воды на оси (t от a) и ширина воды на оси
        public float sC, sLo, sHi;             // s центра моста на реке и границы прохода в RiverWall по длине реки
        public float clearance;                // просвет между низом лаг и водой в середине
        public float[] piers;                  // t опор

        public Vector2 Pt(float t, float o) { return a + d * t + side * o; }

        // верх настила: прямая между концами плюс парабола подъёма (максимум arch в середине)
        public float Y(float t)
        {
            float u = Mathf.Clamp01(t / len);
            return Mathf.Lerp(yA, yB, u) + 4f * arch * u * (1f - u);
        }

        // единичное направление вдоль моста с уклоном
        public Vector3 Dir3(float t)
        {
            float u = Mathf.Clamp01(t / len);
            float dy = (yB - yA) / len + 4f * arch * (1f - 2f * u) / len;
            return new Vector3(d.x, dy, d.y).normalized;
        }

        // точка на расстоянии t вдоль оси, o поперёк, dy от верха настила
        public Vector3 P3(float t, float o, float dy)
        {
            Vector2 q = Pt(t, o);
            return new Vector3(q.x, Y(t) + dy, q.y);
        }
    }

    // =====================================================================================================
    //  ПЛАН
    // =====================================================================================================

    // s точки реки, ближайшей к q (проекция на ломаную русла)
    static float RiverS(River r, Vector2 q)
    {
        float best = float.MaxValue, bs = 0f;
        for (int i = 0; i + 1 < r.p.Count; i++)
        {
            Vector2 a0 = r.p[i], ab = r.p[i + 1] - a0;
            float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            float k = Mathf.Clamp01(Vector2.Dot(q - a0, ab) / l2);
            float dd = (a0 + ab * k - q).sqrMagnitude;
            if (dd < best) { best = dd; bs = Mathf.Lerp(r.s[i], r.s[i + 1], k); }
        }
        return bs;
    }

    // точка русла по длине дуги s
    static Vector2 RiverPt(River r, float s)
    {
        int i = Mathf.Clamp(r.Index(s), 0, r.p.Count - 2);
        float seg = r.s[i + 1] - r.s[i];
        float k = seg > 1e-4f ? Mathf.Clamp01((s - r.s[i]) / seg) : 0f;
        return Vector2.Lerp(r.p[i], r.p[i + 1], k);
    }

    // свободный участок реки без стенки RiverWall у моста: считается тем же циклом, что и в BuildBarriers.
    // Сегмент стенки i..i+2 длиннее на 0.4 (по 0.2 с каждой стороны), поэтому проход уже, чем кусок «пропущенных» сегментов.
    static void BridgeWallGapS(River r, out float lo, out float hi)
    {
        lo = r.bridgeS - BridgeWallGap; hi = r.bridgeS + BridgeWallGap;
        float a0 = float.MaxValue, b0 = float.MinValue;
        for (int i = 0; i + 2 < r.p.Count; i += 2)
        {
            if (Mathf.Abs(r.s[i + 1] - r.bridgeS) >= BridgeWallGap) continue;
            a0 = Mathf.Min(a0, r.s[i] + 0.2f);
            b0 = Mathf.Max(b0, r.s[i + 2] - 0.2f);
        }
        if (a0 < b0) { lo = a0; hi = b0; }
    }

    // план моста; null = нечего строить (нет реки или площадки, ось не пересекает воду)
    static BridgePlan PlanBridge(Layout L, Field F)
    {
        var c = L.c; var r = L.river;
        if (L.bridge == null || r == null) return null;
        Func<Vector2, float> Hy = q => F.g.At(F.H, q.x, q.y);
        var P = new BridgePlan();
        P.a = L.bridge.pos - L.bridge.fwd * L.bridge.hd;
        P.b = L.landing;
        Vector2 ab = P.b - P.a;
        P.len = ab.magnitude;
        if (P.len < 8f) return null;
        P.d = ab / P.len;
        P.side = new Vector2(-P.d.y, P.d.x);

        // вода на оси моста (RiverDist < 0 = в воде)
        float w0 = -1f, w1 = -1f;
        for (float t = 0f; t <= P.len; t += 0.1f)
        {
            if (RiverDist(L, P.a + P.d * t) >= 0f) continue;
            if (w0 < 0f) w0 = t;
            w1 = t;
        }
        if (w0 < 0f || w1 - w0 < 3f) return null;
        P.ww = w1 - w0;
        P.tc = (w0 + w1) * 0.5f;
        P.sC = RiverS(r, P.Pt(P.tc, 0f));
        P.yW = r.WaterY(P.sC);

        // высоты концов: земля под началом и под Landing
        P.yA = Hy(P.a) + BridgeLip;
        P.yB = Hy(P.b) + BridgeLip;
        P.arch = Mathf.Clamp(c.bridgeArch, 0.4f, 0.6f);
        float yMid = (P.yA + P.yB) * 0.5f + P.arch;
        P.thick = Mathf.Clamp(yMid - P.yW - 0.9f, 0.32f, 0.55f);
        P.clearance = yMid - P.thick - P.yW;
        if (P.clearance < 0.75f || P.clearance > 1.1f)
            L.warn.Add("Просвет под мостом " + P.clearance.ToString("0.00") + " u (цель 0.8-1): земля у концов моста на "
                + ((P.yA + P.yB) * 0.5f).ToString("0.00") + " u, вода на " + P.yW.ToString("0.00") + " u. Подправь «Арку моста».");

        // ширина: мост вместе с перилами должен лежать в проходе RiverWall
        BridgeWallGapS(r, out P.sLo, out P.sHi);
        float half = Mathf.Min(P.sC - P.sLo, P.sHi - P.sC);
        float want = Mathf.Clamp(c.bridgeWidth, 2.6f, 3f);
        float maxW = 2f * (half - 0.1f);                 // внешняя грань невидимых перил = w/2 + 0.05
        P.w = c.riverWall ? Mathf.Min(want, Mathf.Max(2.6f, maxW)) : want;   // без стенки над водой проход не нужен: мост любой ширины
        if (c.riverWall && P.w < want - 0.005f)
            L.warn.Add("Ширина моста уменьшена с " + want.ToString("0.0") + " до " + P.w.ToString("0.0") + " u: шире не помещается в проход RiverWall (полуширина прохода " + half.ToString("0.0") + " u).");
        if (c.riverWall && P.w * 0.5f + 0.05f > half + 0.01f)
            L.warn.Add("Проход в RiverWall узкий (полуширина " + half.ToString("0.0") + " u): перила моста шириной " + P.w.ToString("0.0")
                + " u заходят в стенку на " + (P.w * 0.5f + 0.05f - half).ToString("0.00") + " u. Проходу это не мешает; если важно, сдвинь площадку Bridge по z.");

        // две опоры по бокам от середины реки
        float po = Mathf.Clamp(P.ww * 0.27f, 1.2f, 3.2f);
        P.piers = new[] { P.tc - po, P.tc + po };
        return P;
    }

    // =====================================================================================================
    //  МЕШ
    // =====================================================================================================

    // брус прямоугольного сечения от a до b: hw полуширина, hh полувысота (для почти горизонтальных брусьев)
    static void BrBeam(MB mb, int sub, Vector3 a, Vector3 b, float hw, float hh)
    {
        Vector3 dv = b - a;
        float l = dv.magnitude;
        if (l < 0.01f) return;
        Quaternion q = Quaternion.LookRotation(dv / l, Vector3.up);
        LkBox(mb, sub, (a + b) * 0.5f, new Vector3(hw, hh, l * 0.5f + 0.02f), q);
    }

    // провисающая верёвка между двумя точками (sag вниз в середине)
    static void BrRope(MB mb, Vector3 a, Vector3 b, float sag)
    {
        const int n = 6;
        Vector3 prev = a;
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            Vector3 cur = Vector3.Lerp(a, b, u) - new Vector3(0f, sag * 4f * u * (1f - u), 0f);
            LkBeam(mb, BR_ROPE, prev, cur, 0.07f);
            prev = cur;
        }
    }

    // фонарь на верху столбика: пластина, светящийся корпус, четыре прута, крыша и навершие (base = верх столбика)
    static void BrLantern(MB mb, Vector3 b)
    {
        Quaternion qi = Quaternion.identity;
        LkBox(mb, BR_DARK, b + new Vector3(0f, 0.05f, 0f), new Vector3(0.24f, 0.05f, 0.24f), qi);
        LkBox(mb, BR_GLOW, b + new Vector3(0f, 0.38f, 0f), new Vector3(0.17f, 0.22f, 0.17f), qi);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                LkBox(mb, BR_DARK, b + new Vector3(sx * 0.2f, 0.38f, sz * 0.2f), new Vector3(0.03f, 0.24f, 0.03f), qi);
        LkBox(mb, BR_DARK, b + new Vector3(0f, 0.66f, 0f), new Vector3(0.26f, 0.05f, 0.26f), qi);
        LkBox(mb, BR_DARK, b + new Vector3(0f, 0.74f, 0f), new Vector3(0.12f, 0.04f, 0.12f), qi);
    }

    // пенное кольцо вокруг опоры: рваный эллипс (ra вдоль оси моста, rs поперёк), толщина th.
    // solid = сплошное кольцо, иначе отдельные дуги (рябь подальше от опоры)
    static void BrFoamRing(MB mb, Vector2 p, float y, Vector2 d, Vector2 side, float ra, float rs, float th, System.Random rnd, bool solid)
    {
        const int n = 16;
        float a0 = Rf(rnd) * 6.28f;
        for (int k = 0; k < n; k++)
        {
            if (!solid && Rf(rnd) < 0.45f) continue;
            float u0 = a0 + k * Mathf.PI * 2f / n, u1 = a0 + (k + 1) * Mathf.PI * 2f / n;
            float j0 = 1f + 0.3f * (Rf(rnd) - 0.5f), j1 = 1f + 0.3f * (Rf(rnd) - 0.5f);
            Vector2 i0 = p + d * (Mathf.Cos(u0) * ra) + side * (Mathf.Sin(u0) * rs);
            Vector2 i1 = p + d * (Mathf.Cos(u1) * ra) + side * (Mathf.Sin(u1) * rs);
            Vector2 o0 = p + d * (Mathf.Cos(u0) * (ra + th * j0)) + side * (Mathf.Sin(u0) * (rs + th * j0));
            Vector2 o1 = p + d * (Mathf.Cos(u1) * (ra + th * j1)) + side * (Mathf.Sin(u1) * (rs + th * j1));
            mb.Quad(BR_FOAM, new Vector3(i0.x, y, i0.y), new Vector3(o0.x, y, o0.y), new Vector3(o1.x, y, o1.y), new Vector3(i1.x, y, i1.y), Vector3.up);
        }
    }

    static Mesh BuildBridgeMesh(Layout L, Field F, BridgePlan P)
    {
        var c = L.c; float ws = c.worldScale;
        var mb = new MB(BrSubs);
        var rnd = new System.Random(c.seed * 47 + 11);
        Func<Vector2, float> Hy = q => F.g.At(F.H, q.x, q.y);
        float len = P.len, w = P.w;
        Vector3 d3 = new Vector3(P.d.x, 0f, P.d.y);
        Quaternion qd = Quaternion.LookRotation(d3, Vector3.up);   // local x = поперёк моста, local z = вдоль оси

        // ---- настил: доски поперёк, чуть разной длины, два тона, повторяют арку
        int np = Mathf.Max(8, Mathf.FloorToInt(len / 0.5f));
        float pstep = len / np;
        for (int i = 0; i < np; i++)
        {
            float t = (i + 0.5f) * pstep;
            AddPlank(mb, i % 2 == 0 ? BR_A : BR_B, P.P3(t, 0f, -BridgePlankT * 0.5f), P.Dir3(t), pstep - 0.05f, w + (Rf(rnd) - 0.5f) * 0.12f, BridgePlankT);
        }

        // ---- две лаги вдоль (по арке) и поперечные балки под настилом
        float depth = P.thick - BridgePlankT;           // высота лаги под настилом
        float so = w * 0.5f - 0.24f;                    // ось лаги от оси моста
        int ns = Mathf.Max(4, Mathf.CeilToInt(len / 1.0f));
        for (int sg = -1; sg <= 1; sg += 2)
            for (int k = 0; k < ns; k++)
            {
                float ta = len * k / ns, tb = len * (k + 1) / ns;
                float dy = -BridgePlankT - depth * 0.5f;
                BrBeam(mb, BR_DARK, P.P3(ta, sg * so, dy), P.P3(tb, sg * so, dy), 0.13f, depth * 0.5f);
            }
        for (float t = 1.0f; t < len - 0.5f; t += 2.0f)
            LkBox(mb, BR_DARK, P.P3(t, 0f, -BridgePlankT - 0.1f), new Vector3(w * 0.5f + 0.04f, 0.08f, 0.1f), Quaternion.LookRotation(P.Dir3(t), Vector3.up));

        // ---- перила: стойки каждые ~2.2 u, две жерди; в среднем пролёте над водой нижняя жердь заменена верёвкой
        int nb = Mathf.Max(3, Mathf.CeilToInt(len / 2.2f));
        float ro = w * 0.5f - 0.1f;                     // ось стоек от оси моста
        int kc = Mathf.Clamp(Mathf.FloorToInt(P.tc / len * nb), 1, nb - 2);
        for (int sg = -1; sg <= 1; sg += 2)
            for (int k = 0; k <= nb; k++)
            {
                float t = len * k / nb;
                float y = P.Y(t);
                AddPost(mb, BR_DARK, P.Pt(t, sg * ro), y - 0.3f, y + BridgeRailH, 0.2f, true);
                if (k == nb) continue;
                float t2 = len * (k + 1) / nb;
                LkBeam(mb, BR_DARK, P.P3(t, sg * ro, BridgeRailH - 0.12f), P.P3(t2, sg * ro, BridgeRailH - 0.12f), 0.14f);
                Vector3 la = P.P3(t, sg * ro, BridgeRailH * 0.45f), lb = P.P3(t2, sg * ro, BridgeRailH * 0.45f);
                if (k == kc) BrRope(mb, la, lb, 0.35f);
                else LkBeam(mb, BR_DARK, la, lb, 0.1f);
            }

        // ---- входы: столбики по бокам от прохода (на земле, чуть за концами настила) и фонари на них
        float eo = w * 0.5f + 0.25f;
        float[] et = { -0.15f, len + 0.15f };
        foreach (float t in et)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector2 pp = P.Pt(t, sg * eo);
                float g = Hy(pp);
                float top = (t < 0f ? P.yA : P.yB) + BridgeRailH + 0.6f;
                if (c.bridgePosts) AddPost(mb, BR_DARK, pp, g - 0.3f, top, 0.3f, true);
                else if (c.bridgeLamps) AddPost(mb, BR_DARK, pp, g - 0.3f, top, 0.17f, false);   // без столбиков фонарь стоит на тонкой мачте
                if (c.bridgeLamps) BrLantern(mb, new Vector3(pp.x, top + (c.bridgePosts ? 0.1f : 0f), pp.y));
            }

        // ---- табличка на стороне площадки Bridge: столбик у края пада, доска лицом к тропе, красный значок-арка
        {
            Vector2 sp = P.Pt(-1.3f, eo + 0.65f);
            float sTop = P.yA + 1.35f;
            AddPost(mb, BR_DARK, sp, Hy(sp) - 0.3f, sTop, 0.16f, false);
            Vector3 bc = new Vector3(sp.x, sTop - 0.38f, sp.y);
            LkBox(mb, BR_DARK, bc + d3 * 0.05f, new Vector3(0.64f, 0.36f, 0.03f), qd);   // рамка (позади)
            LkBox(mb, BR_A, bc, new Vector3(0.58f, 0.3f, 0.04f), qd);                  // доска
            Vector3 ic = bc - d3 * 0.06f;                                             // значок перед доской, к игроку
            Vector3 sd3 = new Vector3(P.side.x, 0f, P.side.y);
            const int arcN = 6;
            Vector3 prevA = ic - sd3 * 0.34f + Vector3.up * 0.04f;
            for (int i = 1; i <= arcN; i++)
            {
                float ang = Mathf.PI * i / arcN;
                Vector3 cur = ic - sd3 * (Mathf.Cos(ang) * 0.34f) + Vector3.up * (0.04f + Mathf.Sin(ang) * 0.17f);
                LkBeam(mb, BR_RED, prevA, cur, 0.05f);
                prevA = cur;
            }
            LkBeam(mb, BR_RED, ic - sd3 * 0.34f + Vector3.up * 0.04f, ic - sd3 * 0.34f - Vector3.up * 0.2f, 0.05f);
            LkBeam(mb, BR_RED, ic + sd3 * 0.34f + Vector3.up * 0.04f, ic + sd3 * 0.34f - Vector3.up * 0.2f, 0.05f);
            LkBeam(mb, BR_RED, ic - sd3 * 0.34f - Vector3.up * 0.08f, ic + sd3 * 0.34f - Vector3.up * 0.08f, 0.05f);
        }

        // ---- опоры в воде: три слоя тёсаного камня (слои чуть повёрнуты), деревянный оголовок под лагами
        var foam = new List<Vector2>();
        foreach (float tp in P.piers)
        {
            Vector2 pc = P.Pt(tp, 0f);
            float bed = Hy(pc) - 0.3f;
            float capH = 0.22f;
            float top = P.Y(tp) - P.thick - capH;
            if (top - bed < 0.3f) continue;
            const int courses = 3;
            float ch = (top - bed) / courses;
            for (int k = 0; k < courses; k++)
            {
                float f = k / (float)(courses - 1);
                float hs = Mathf.Lerp(1.2f, 0.95f, f), hd = Mathf.Lerp(0.62f, 0.48f, f);   // вытянуты вдоль течения
                Quaternion qk = Quaternion.AngleAxis((Rf(rnd) - 0.5f) * 10f, Vector3.up) * qd;
                LkBox(mb, BR_STONE, new Vector3(pc.x, bed + ch * (k + 0.5f), pc.y), new Vector3(hs, ch * 0.5f + 0.01f, hd), qk);
            }
            LkBox(mb, BR_DARK, new Vector3(pc.x, top + capH * 0.5f, pc.y), new Vector3(w * 0.5f, capH * 0.5f, 0.22f), Quaternion.LookRotation(P.Dir3(tp), Vector3.up));
            foam.Add(pc);
        }
        foreach (var fc in foam)
        {
            BrFoamRing(mb, fc, P.yW + 0.03f, P.d, P.side, 0.55f, 1.1f, 0.55f, rnd, true);
            BrFoamRing(mb, fc, P.yW + 0.035f, P.d, P.side, 1.2f, 1.85f, 0.3f, rnd, false);
        }

        // ---- короткие столбы под лагами на берегах, где земля ниже низа лаг (в воде лаги держат опоры)
        for (float t = 1.5f; t < len - 1.2f; t += 1.8f)
        {
            if (t > P.tc - P.ww * 0.5f - 0.4f && t < P.tc + P.ww * 0.5f + 0.4f) continue;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector2 pp = P.Pt(t, sg * so);
                float g = Hy(pp), ysb = P.Y(t) - P.thick;
                if (g < ysb - 0.12f) AddPost(mb, BR_DARK, pp, g - 0.25f, ysb, 0.22f, false);
            }
        }

        for (int vi = 0; vi < mb.v.Count; vi++) mb.v[vi] = mb.v[vi] * ws;
        return mb.Build("LocationTest_Bridge");
    }

    // =====================================================================================================
    //  КОЛЛАЙДЕРЫ
    // =====================================================================================================

    // настил: 4 бокса по хордам арки; перила: невидимая стенка с каждой стороны (тоже 4 бокса), выше визуальных перил
    static void AddBridgeColliders(Layout L, BridgePlan P, Transform parent)
    {
        float ws = L.c.worldScale;
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(parent, false);
        const int parts = 4;
        float rh = BridgeRailH + 1.2f;
        float ro = P.w * 0.5f - 0.1f;
        for (int k = 0; k < parts; k++)
        {
            Vector3 A = P.P3(P.len * k / parts, 0f, 0f), B = P.P3(P.len * (k + 1) / parts, 0f, 0f);
            Vector3 dv = B - A;
            float cl = dv.magnitude;
            Quaternion q = Quaternion.LookRotation(dv / cl, Vector3.up);
            Vector3 up = q * Vector3.up, rt = q * Vector3.right, mid = (A + B) * 0.5f;
            AddRotBoxCol(cg.transform, "Deck" + (k + 1), mid - up * 0.28f, new Vector3(P.w, 0.6f, cl + 0.16f), q, ws);
            for (int sg = -1; sg <= 1; sg += 2)
                AddRotBoxCol(cg.transform, "Rail" + (sg < 0 ? "L" : "R") + (k + 1), mid + rt * (sg * ro) + up * (rh * 0.5f - 0.3f), new Vector3(0.3f, rh, cl + 0.16f), q, ws);
        }
    }

    // щели над водой между перилами моста и краями прохода в RiverWall: закрываются такими же стенками,
    // иначе через них можно было бы зайти в воду рядом с мостом и перейти реку вброд
    static void AddBridgeGapFill(Layout L, BridgePlan P, Transform parent)
    {
        var r = L.river; float ws = L.c.worldScale;
        const float hM = 3.4f;   // высота стенки, м (как в BuildBarriers)
        int ib = r.Index(P.sC);
        float cosA = Mathf.Max(0.5f, Mathf.Abs(Vector2.Dot(r.Dir(ib), P.side)));
        float hDeck = (P.w * 0.5f + 0.05f) / cosA;
        float[][] strips = { new[] { P.sLo, P.sC - hDeck }, new[] { P.sC + hDeck, P.sHi } };
        foreach (var st in strips)
        {
            if (st[1] - st[0] < 0.1f) continue;
            Vector2 pa = RiverPt(r, st[0]), pb = RiverPt(r, st[1]);
            Vector2 dir = pb - pa;
            float l = dir.magnitude;
            if (l < 0.05f) continue;
            dir /= l;
            float sm = (st[0] + st[1]) * 0.5f;
            Vector2 mid = (pa + pb) * 0.5f;
            float width = (r.HW(sm) + 0.7f) * 2f;
            AddWall(parent, "BridgeGap", new Vector3(mid.x * ws, r.WaterY(sm) * ws + hM * 0.5f - 0.4f, mid.y * ws), dir, new Vector3(width * ws, hM, (l + 0.1f) * ws));
        }
    }

    // =====================================================================================================
    //  МАТЕРИАЛЫ И СБОРКА В СЦЕНЕ
    // =====================================================================================================

    // материалы в порядке подмешей BR_*: доска А, доска Б, тёмное дерево, камень, свет, красный, пена, верёвка
    static Material[] MakeBridgeMats(Cfg c)
    {
        Color wd = c.lookoutWood;
        if (c.cardboard)
        {
            var tex = MakeAllTextures(false);   // файлы уже есть: просто подхватываются
            float lt = c.tileM * 0.5f;
            return new[]
            {
                MakeCardMat(c, "Bridge_PlankA", wd, tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Bridge_PlankB", new Color(wd.r * 1.1f, wd.g * 1.05f, wd.b * 0.9f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Bridge_WoodDark", new Color(wd.r * 0.62f, wd.g * 0.52f, wd.b * 0.46f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Bridge_Stone", new Color(0.85f, 0.80f, 1.0f), tex[4], c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Bridge_Glow", new Color(2.2f, 1.7f, 0.75f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Bridge_Red", new Color(1.1f, 0.30f, 0.26f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Bridge_Foam", new Color(1.6f, 1.65f, 1.7f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Bridge_Rope", new Color(1.3f, 1.15f, 0.8f), _paperTex, c.tileM * 0.6f, 0f, 0f, false)
            };
        }
        return new[]
        {
            MakeMat("Bridge_PlankA", new Color(0.66f, 0.44f, 0.24f), false, 0f),
            MakeMat("Bridge_PlankB", new Color(0.72f, 0.50f, 0.28f), false, 0f),
            MakeMat("Bridge_WoodDark", new Color(0.40f, 0.26f, 0.14f), false, 0f),
            MakeMat("Bridge_Stone", new Color(0.62f, 0.58f, 0.72f), false, 0f),
            MakeMat("Bridge_Glow", new Color(1f, 0.88f, 0.45f), false, 0f),
            MakeMat("Bridge_Red", new Color(0.90f, 0.22f, 0.20f), false, 0f),
            MakeMat("Bridge_Foam", new Color(0.95f, 0.98f, 1f), false, 0f),
            MakeMat("Bridge_Rope", new Color(0.80f, 0.68f, 0.45f), false, 0f)
        };
    }

    // мост через реку: объект Bridge_River в корне (меш, материалы, коллайдеры). Вызывается из BuildMarkers.
    // Если ось моста не пересекает воду, остаётся старый маркер-куб, а в лог идёт предупреждение.
    void BuildBridge(Layout L, Field F, Transform root)
    {
        var c = L.c; float ws = c.worldScale;
        var P = PlanBridge(L, F);
        if (P == null)
        {
            L.warn.Add("Мост: ось от площадки Bridge до Landing не пересекает воду, оставлен маркер-куб.");
            Vector2 a0 = L.bridge.pos - L.bridge.fwd * L.bridge.hd, b0 = L.landing;
            Vector2 m0 = (a0 + b0) * 0.5f;
            Prim(root, "Bridge_River", PrimitiveType.Cube, new Vector3(m0.x * ws, (F.g.At(F.H, a0.x, a0.y) + 0.5f) * ws, m0.y * ws),
                new Vector3(Vector2.Distance(a0, b0) * ws, 0.25f * ws, 3f * ws), Color.white);
            return;
        }

        var mesh = BuildBridgeMesh(L, F, P);
        SaveAsset(mesh, AssetDir + "/Meshes/LocationTest_Bridge.asset");
        var go = new GameObject("Bridge_River");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = MakeBridgeMats(c);
        mr.shadowCastingMode = ShadowCastingMode.On;
        AddBridgeColliders(L, P, go.transform);
        if (c.riverBarrier && c.riverWall)
        {
            Transform rw = root.Find("Barriers/RiverWall");
            AddBridgeGapFill(L, P, rw != null ? rw : go.transform);
        }
        Debug.Log("[LocationTester] Мост: длина " + P.len.ToString("0.0") + " u, ширина " + P.w.ToString("0.0") + " u, арка " + P.arch.ToString("0.00")
            + " u, просвет над водой " + P.clearance.ToString("0.00") + " u, вода на оси " + P.ww.ToString("0.0") + " u.");
    }
}
#endif

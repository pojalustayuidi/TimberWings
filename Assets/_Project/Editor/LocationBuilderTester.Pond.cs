#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilderTester.Pond.cs
// Законченный пруд для Location Builder TESTER (partial-часть LocationBuilderTester).
// Включается галочкой Cfg.buildPond (по умолчанию выключена). При включении ВМЕСТЕ появляются:
//   1. Вода (BuilderSim/LocationToonWater): бирюзовая, мелкая, пенная кромка по глубине (uv.x), круги от острова (uv.y).
//   2. Остров радиуса pondIslandRadius посередине: поднимается в Compute функцией PondRaise (рельеф), трава по PondAlpha,
//      деревце, кусты, камыш, камни.
//   3. Деревянный мостик 1.4 u от западного берега к острову (сваи стоят на дне ямы).
//   4. Пирс на северном берегу (от площадки Pond) на pondPierLength u в воду: доски на сваях, лодка, ведро, удочка.
//   5. Кувшинки (pondLilies), камыш у воды, 8-12 валунов вдоль берега.
// Пока buildPond выключена, рельеф ямы (LakeBowl, ступенчатые берега) не меняется ни на один вертекс.
// Деревянные части строят общие помощники AddPlank / AddPost / AddRail (поверх LkBox/LkBeam смотровой).
// Размеры в u; меш собирается в u и в конце масштабируется на worldScale (как BuildLookout).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilderTester
{
    // подмеши PondProps (индекс 3 = камыш, его ждёт общий AddReeds)
    const int PW_A = 0, PW_B = 1, PW_DARK = 2, PW_REED = 3, PW_LEAF = 4, PW_FLOWER = 5, PW_YELLOW = 6,
              PW_RED = 7, PW_TEAL = 8, PW_METAL = 9, PW_CROWN = 10, PW_FOAM = 11, PW_ORANGE = 12, PW_STONE = 13, PW_PINK = 14, PW_GLOW = 15;
    const int PondSubs = 16;

    const float PondBridgeW = 1.4f;    // ширина мостика, u
    const float PondPierW = 2.0f;      // ширина пирса, u
    const float PondIsleTop = 0.5f;    // макушка острова над водой, u

    // источник света пруда (позиция в u, до масштабирования): фонари и свечение воды
    sealed class PondLamp { public Vector3 pos; public Color col; public float range, intensity; public bool flicker; }

    // план пруда: центр, остров, пирс, мостик, лодка. База (остров) считается в BuildLayout, геометрия после Compute.
    sealed class PondPlan
    {
        public float yW;
        public Vector2 center, isle, toPad, bridgeDir;
        public float isleR;
        public bool hasIsle;            // остров и мостик по желанию (Cfg.pondIsland / pondBridge), по умолчанию выключены
        public Pad pad;
        public bool pierOn, brOn, boatOn;
        public Vector2 pierS, pierW, pierE, pierIn, boatSide, boatC;
        public float pierYS, pierYE;
        public Vector2 brA, brB;
        public float brYA, brYB;
    }

    static bool PondOn(Cfg c) { return c.buildPond && c.buildWater && c.lakeRadius > 0.5f; }
    static float LakeWaterYOf(Cfg c) { return -0.4f * c.lakeDepth; }

    // =====================================================================================================
    //  ПЛАН
    // =====================================================================================================

    // до Compute: где остров. Остров чуть сдвинут от пирса, чтобы конец пирса не упирался в берег острова.
    static void PondPlanBase(Layout L)
    {
        var c = L.c;
        if (!PondOn(c)) return;
        var P = new PondPlan();
        P.yW = LakeWaterYOf(c);
        P.center = L.lakePos;
        P.hasIsle = c.pondIsland;
        P.isleR = P.hasIsle ? Mathf.Clamp(c.pondIslandRadius, 2f, 3.5f) : 0f;
        P.pad = L.pads.Find(q => q.name == "Pond");
        Vector2 toPad = P.pad != null ? P.pad.pos - P.center : Vector2.up;
        if (toPad.sqrMagnitude < 0.01f) toPad = Vector2.up;
        P.toPad = toPad.normalized;
        P.isle = P.hasIsle ? P.center - P.toPad * Mathf.Min(1f, c.lakeRadius * 0.1f) : P.center;
        Vector2 perp = new Vector2(-P.toPad.y, P.toPad.x);
        P.bridgeDir = Vector2.Dot(perp, -P.center) >= 0f ? perp : -perp;   // мостик со стороны лагеря
        L.pond = P;
    }

    // высота дна с островом (вызывается в Compute сразу после LakeBowl). Без пруда L.pond == null: ничего не меняется.
    // Над водой плоская макушка и пологий пляж, под водой крутой спад ко дну.
    static float PondRaise(Layout L, Vector2 q, float h, float wob)
    {
        var P = L.pond;
        if (P == null || !P.hasIsle) return h;
        float r = P.isleR;
        float d = Vector2.Distance(q, P.isle) + (wob - 0.5f) * 0.5f;
        if (d > r + 2.3f) return h;
        float top = P.yW + PondIsleTop;
        float v = d <= r ? Mathf.Lerp(top, P.yW, SS(r * 0.55f, r, d)) : Mathf.Lerp(P.yW, h, SS(r, r + 2.2f, d));
        return Mathf.Max(h, v);
    }

    // углубление: уровень воды и урез не меняются, дно под водой уходит глубже (крутые стенки, тёмный центр).
    // Вызывается в Compute после LakeBowl, до PondRaise. Без пруда ничего не меняет.
    static float PondDeepen(Layout L, float h)
    {
        var P = L.pond;
        if (P == null) return h;
        float k = Mathf.Clamp(L.c.pondDeepen, 1f, 3f);
        return h < P.yW ? P.yW + (h - P.yW) * k : h;
    }

    // раскраска: скала на крутых стенках чаши, трава на макушке острова (песок у воды остаётся от общего правила)
    static void PondAlpha(Layout L, Vector2 q, float[] w, float h, float slope)
    {
        var P = L.pond;
        if (P == null) return;
        float dc = Vector2.Distance(q, P.center);
        if (dc < L.c.lakeRadius + 0.6f)
            Mix(w, 6, SS(P.yW - 0.3f, P.yW - 0.05f, h) * (1f - SS(P.yW + 0.3f, P.yW + 0.9f, h)) * 0.92f);   // песчаная полоса у воды
        if (dc < L.c.lakeRadius + 0.6f)
            Mix(w, 4, SS(0.6f, 1.3f, slope) * SS(P.yW - 1.6f, P.yW + 0.2f, h) * 0.85f);
        if (!P.hasIsle) return;
        float d = Vector2.Distance(q, P.isle);
        if (d > P.isleR + 1f) return;
        Mix(w, 0, (1f - SS(P.isleR * 0.62f, P.isleR * 0.95f, d)) * 0.95f);
    }

    static float PondWaterR(Layout L, Field F, float ang)
    {
        var P = L.pond;
        Vector2 dv = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        for (float r = L.c.lakeRadius + 2f; r > 1f; r -= 0.1f)
            if (F.g.At(F.H, P.center.x + dv.x * r, P.center.y + dv.y * r) < P.yW - 0.02f) return r;
        return 0f;
    }

    // после Compute: точки пирса, мостика и лодки по реальному рельефу
    static void PondPlanGeometry(Layout L, Field F)
    {
        var P = L.pond;
        if (P == null) return;
        var c = L.c; var g = F.g;
        Func<Vector2, float> Hy = p => g.At(F.H, p.x, p.y);
        float yW = P.yW;
        P.pierOn = P.brOn = P.boatOn = false;

        // ---- пирс: от площадки Pond по прямой к центру чаши
        if (P.pad == null) L.warn.Add("Пруд: нет площадки Pond, пирса не будет.");
        else
        {
            Vector2 inDir = -P.toPad;
            Vector2 S = P.center + P.toPad * (c.lakeRadius + 1.6f);
            bool found = false; Vector2 W = S;
            for (float t = 0f; t < c.lakeRadius + 6f; t += 0.1f)
            {
                Vector2 p = S + inDir * t;
                if (Hy(p) < yW - 0.02f) { W = p; found = true; break; }
            }
            if (!found) L.warn.Add("Пруд: от площадки Pond по прямой нет воды, пирса не будет.");
            else
            {
                float len = Mathf.Clamp(c.pondPierLength, 3f, 8f);
                float maxLen = P.hasIsle ? Vector2.Distance(W, P.isle) - (P.isleR + 0.3f + 1.2f) : 99f;   // до берега острова не меньше 1.2 u воды
                if (len > maxLen)
                {
                    L.warn.Add("Пруд: пирс " + len.ToString("0.0") + " u упирается в остров, укорочен до " + Mathf.Max(2.5f, maxLen).ToString("0.0") + " u (уменьши остров или пирс).");
                    len = Mathf.Max(2.5f, maxLen);
                }
                P.pierS = S; P.pierW = W; P.pierIn = inDir; P.pierE = W + inDir * len;
                P.pierYS = Hy(S) + 0.14f;
                P.pierYE = Mathf.Min(yW + 0.8f, P.pierYS - 0.1f);
                P.pierOn = true;

                // лодка у конца пирса, со стороны, противоположной мостику
                Vector2 side = new Vector2(-inDir.y, inDir.x);
                P.boatSide = Vector2.Dot(side, -P.bridgeDir) >= 0f ? side : -side;
                P.boatC = P.pierE - inDir * 0.8f + P.boatSide * (PondPierW * 0.5f + 0.85f);
                P.boatOn = yW - Hy(P.boatC) > 0.4f;
                if (!P.boatOn) L.warn.Add("Пруд: у конца пирса слишком мелко для лодки, лодки не будет.");
            }
        }

        // ---- мостик: от верха берега ямы к макушке острова
        Vector2 bd = P.bridgeDir;
        float tA = -1f;
        bool wantBridge = P.hasIsle && c.pondBridge;
        if (wantBridge)
            for (float t = P.isleR + 2.5f; t < c.lakeRadius + 6f; t += 0.1f)
                if (Hy(P.isle + bd * t) >= -0.2f) { tA = t; break; }
        if (!wantBridge) { }
        else if (tA < 0f) L.warn.Add("Пруд: не нашёл берег для мостика, мостика не будет.");
        else
        {
            P.brA = P.isle + bd * (tA + 1.3f);
            P.brB = P.isle + bd * (P.isleR * 0.45f);
            P.brYA = Hy(P.brA) + 0.14f;
            P.brYB = P.yW + PondIsleTop + 0.14f;
            P.brOn = L.outline.Inside(P.brA.x, P.brA.y);
            if (!P.brOn) L.warn.Add("Пруд: берег мостика за краем острова.");
        }
    }

    // расстояние от точки до ближайшей постройки пруда (край, а не ось); > 0 = свободно
    static float PondClear(PondPlan P, Vector2 p)
    {
        float m = 999f;
        if (P.pierOn) m = Mathf.Min(m, PolySegDist(p, P.pierS, P.pierE) - PondPierW * 0.5f);
        if (P.brOn) m = Mathf.Min(m, PolySegDist(p, P.brA, P.brB) - PondBridgeW * 0.5f);
        if (P.boatOn) m = Mathf.Min(m, Vector2.Distance(p, P.boatC) - 1.4f);
        return m;
    }

    // проверки пруда: отдельный от реки, постройки внутри своей зоны, тропа не в воде
    static void CheckPond(Layout L, Field F)
    {
        var P = L.pond;
        if (P == null) return;
        var c = L.c;
        if (L.river != null)
        {
            float gap = RiverDist(L, P.center) - c.lakeRadius;
            if (gap < c.lakeShore + 1f) L.warn.Add("Пруд близко к реке: зазор " + gap.ToString("0.0") + " u, нужно не меньше " + (c.lakeShore + 1f).ToString("0.0") + " u. Подвинь пруд.");
            var pts = new List<Vector2>();
            if (P.hasIsle) pts.Add(P.isle);
            if (P.pierOn) { pts.Add(P.pierS); pts.Add(P.pierE); }
            if (P.brOn) { pts.Add(P.brA); pts.Add(P.brB); }
            foreach (var p in pts)
                if (RiverDist(L, p) < 8f) { L.warn.Add("Мостик или пирс подходят к реке ближе 8 u: пруд должен быть отдельным."); break; }
        }
        if (P.pierOn && P.brOn && PolySegDist(P.brA, P.pierS, P.pierE) < 4f)
            L.warn.Add("Мостик и пирс слишком близко друг к другу.");
        var tr = L.trails.Find(t => t.name == "Trail_Pond");
        if (tr != null)
            for (int i = 0; i < tr.p.Count; i++)
                if (Vector2.Distance(tr.p[i], P.center) - tr.half[i] < c.lakeRadius || F.g.At(F.H, tr.p[i].x, tr.p[i].y) < P.yW + 0.3f)
                { L.warn.Add("Trail_Pond заходит в яму пруда."); break; }
    }

    // =====================================================================================================
    //  ДЕРЕВЯННЫЕ ПОМОЩНИКИ (общие: пирс, мостик, при желании смотровая)
    // =====================================================================================================

    // доска: центр, направление движения dir (можно с уклоном), длина вдоль, ширина поперёк (горизонтально), толщина
    static void AddPlank(MB mb, int sub, Vector3 ctr, Vector3 dir, float along, float across, float thick)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion q = Quaternion.LookRotation(dir.normalized, Vector3.up);
        LkBox(mb, sub, ctr, new Vector3(across * 0.5f, thick * 0.5f, along * 0.5f), q);
    }

    // свая/стойка: вертикальный брус от yBottom до yTop, cap = плоская шляпка сверху
    static void AddPost(MB mb, int sub, Vector2 xz, float yBottom, float yTop, float thick, bool cap)
    {
        if (yTop - yBottom < 0.05f) return;
        float h = thick * 0.5f;
        LkBox(mb, sub, new Vector3(xz.x, (yBottom + yTop) * 0.5f, xz.y), new Vector3(h, (yTop - yBottom) * 0.5f, h), Quaternion.identity);
        if (cap) LkBox(mb, sub, new Vector3(xz.x, yTop + 0.05f, xz.y), new Vector3(h * 1.3f, 0.05f, h * 1.3f), Quaternion.identity);
    }

    static void AddRail(MB mb, int sub, Vector3 a, Vector3 b, float thick) { LkBeam(mb, sub, a, b, thick); }

    // настил с лагами и сваями от a до b (верх настила ya -> yb). rails = перила (мостик), bollards = пеньки (пирс).
    // Сваи уходят на дно (F.H); те, что стоят в воде, попадают в foam для пенных колец.
    static void PondDeck(MB mb, Field F, float yW, Vector2 a, Vector2 b, float ya, float yb, float w,
                         bool rails, bool bollards, System.Random rnd, List<Vector2> foam)
    {
        Vector2 d = b - a; float len = d.magnitude;
        if (len < 0.5f) return;
        d /= len;
        Vector2 side = new Vector2(-d.y, d.x);
        Vector3 dir3 = new Vector3(d.x, (yb - ya) / len, d.y);
        Func<float, float> Y = t => Mathf.Lerp(ya, yb, t / len);
        Func<Vector2, float> Hy = p => F.g.At(F.H, p.x, p.y);

        const float pl = 0.44f, gap = 0.05f;
        int n = Mathf.Max(3, Mathf.FloorToInt(len / (pl + gap)));
        float step = len / n;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) * step;
            Vector2 p = a + d * t;
            AddPlank(mb, i % 2 == 0 ? PW_A : PW_B, new Vector3(p.x, Y(t) - 0.06f, p.y), dir3, step - gap, w + (Rf(rnd) - 0.5f) * 0.14f, 0.12f);
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 off = side * (s * (w * 0.5f - 0.2f));
            LkBeam(mb, PW_DARK, new Vector3(a.x + off.x, ya - 0.28f, a.y + off.y), new Vector3(b.x + off.x, yb - 0.28f, b.y + off.y), 0.22f);
        }

        var ts = new List<float>();
        for (float t = 0.5f; t < len - 0.5f; t += 1.9f) ts.Add(t);
        ts.Add(len - 0.3f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 prevTop = Vector3.zero; bool hasPrev = false;
            foreach (float t in ts)
            {
                Vector2 pin = a + d * t + side * (s * (w * 0.5f - 0.2f));
                float bot = Hy(pin) - 0.25f;
                AddPost(mb, PW_DARK, pin, bot, Y(t) - 0.16f, 0.26f, false);
                if (bot < yW - 0.05f && foam != null) foam.Add(pin);
                if (rails)
                {
                    Vector2 pout = a + d * t + side * (s * (w * 0.5f - 0.05f));
                    float top = Y(t) + 0.95f;
                    AddPost(mb, PW_DARK, pout, Hy(pout) - 0.25f, top, 0.2f, true);
                    LkBox(mb, PW_YELLOW, new Vector3(pout.x, top + 0.26f, pout.y), new Vector3(0.08f, 0.11f, 0.08f), Quaternion.identity);   // фонарик
                    Vector3 cur = new Vector3(pout.x, top - 0.12f, pout.y);
                    if (hasPrev)
                    {
                        AddRail(mb, PW_DARK, prevTop, cur, 0.12f);
                        AddRail(mb, PW_DARK, prevTop - new Vector3(0f, 0.45f, 0f), cur - new Vector3(0f, 0.45f, 0f), 0.1f);
                    }
                    prevTop = cur; hasPrev = true;
                }
            }
        }
        if (bollards)
        {
            float[] bt = { len * 0.5f, len - 0.3f };
            for (int s = -1; s <= 1; s += 2)
                foreach (float t in bt)
                {
                    Vector2 pb = a + d * t + side * (s * (w * 0.5f + 0.08f));
                    float bot = Hy(pb) - 0.25f;
                    AddPost(mb, PW_DARK, pb, bot, Y(t) + 0.55f, 0.28f, true);
                    if (bot < yW - 0.05f && foam != null) foam.Add(pb);
                }
        }
    }

    // =====================================================================================================
    //  МЕЛОЧИ: пена, кувшинки, ведро, удочка, лодка, деревце
    // =====================================================================================================

    static void AddFoamRing(MB mb, Vector2 p, float y, float r0, float r1, System.Random rnd)
    {
        const int n = 8;
        float a0 = Rf(rnd) * 6.28f;
        for (int k = 0; k < n; k++)
        {
            float ra = a0 + k * Mathf.PI * 2f / n, rb = a0 + (k + 1) * Mathf.PI * 2f / n;
            float j0 = 1f + 0.25f * (Rf(rnd) - 0.5f), j1 = 1f + 0.25f * (Rf(rnd) - 0.5f);
            Vector3 i0 = new Vector3(p.x + Mathf.Cos(ra) * r0, y, p.y + Mathf.Sin(ra) * r0);
            Vector3 i1 = new Vector3(p.x + Mathf.Cos(rb) * r0, y, p.y + Mathf.Sin(rb) * r0);
            Vector3 o0 = new Vector3(p.x + Mathf.Cos(ra) * r1 * j0, y, p.y + Mathf.Sin(ra) * r1 * j0);
            Vector3 o1 = new Vector3(p.x + Mathf.Cos(rb) * r1 * j1, y, p.y + Mathf.Sin(rb) * r1 * j1);
            mb.Quad(PW_FOAM, i0, o0, o1, i1, Vector3.up);
        }
    }

    // кувшинка: плоский зелёный диск с вырезом-клином и белый цветок с жёлтой серединкой
    static void AddLily(MB mb, Vector2 p, float y, float r, bool flower, int leafSub, System.Random rnd)
    {
        const int n = 9;
        float notch = Rf(rnd) * 6.28f, sweep = Mathf.PI * 2f - 0.55f;
        Vector3 c0 = new Vector3(p.x, y, p.y);
        for (int k = 0; k < n; k++)
        {
            float a0 = notch + 0.55f + k * sweep / n, a1 = notch + 0.55f + (k + 1) * sweep / n;
            float j0 = 0.95f + 0.1f * Rf(rnd), j1 = 0.95f + 0.1f * Rf(rnd);
            Vector3 v0 = new Vector3(p.x + Mathf.Cos(a0) * r * j0, y, p.y + Mathf.Sin(a0) * r * j0);
            Vector3 v1 = new Vector3(p.x + Mathf.Cos(a1) * r * j1, y, p.y + Mathf.Sin(a1) * r * j1);
            mb.Tri(leafSub, c0, v0, v1, Vector3.up);
        }
        if (!flower) return;
        float s = r * 0.55f;
        int fsub = Rf(rnd) < 0.65f ? PW_PINK : PW_FLOWER;
        Vector3 fc = c0 + new Vector3(Mathf.Cos(notch + 3f) * r * 0.15f, 0.02f, Mathf.Sin(notch + 3f) * r * 0.15f);
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI * 2f / 6f + notch;
            Vector3 dv = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = new Vector3(-dv.z, 0f, dv.x);
            Vector3 b0 = fc + dv * 0.05f + side * 0.09f * s * 2f;
            Vector3 b1 = fc + dv * 0.05f - side * 0.09f * s * 2f;
            Vector3 tip = fc + dv * 0.34f * s * 1.3f + new Vector3(0f, 0.22f * s * 1.3f, 0f);
            mb.Tri(fsub, b0, b1, tip, dv * 0.5f + Vector3.up);
            mb.Tri(fsub, b0, b1, tip, -dv * 0.5f + Vector3.up * 0.2f);
        }
        LkBox(mb, PW_YELLOW, fc + new Vector3(0f, 0.07f, 0f), new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity);
    }

    // ведро: усечённый 8-угольный конус, ручка дугой; b = центр основания
    static void AddBucket(MB mb, Vector3 b)
    {
        const int n = 8;
        float r0 = 0.2f, r1 = 0.28f, h = 0.45f;
        for (int k = 0; k < n; k++)
        {
            float a0 = k * Mathf.PI * 2f / n, a1 = (k + 1) * Mathf.PI * 2f / n, am = (a0 + a1) * 0.5f;
            Vector3 b0 = b + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), b1 = b + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
            Vector3 t0 = b + new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1), t1 = b + new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
            mb.Quad(PW_METAL, b0, b1, t1, t0, new Vector3(Mathf.Cos(am), 0.2f, Mathf.Sin(am)));
            Vector3 c = b + new Vector3(0f, h * 0.9f, 0f);
            Vector3 i0 = c + new Vector3(Mathf.Cos(a0) * r1 * 0.95f, 0f, Mathf.Sin(a0) * r1 * 0.95f), i1 = c + new Vector3(Mathf.Cos(a1) * r1 * 0.95f, 0f, Mathf.Sin(a1) * r1 * 0.95f);
            mb.Tri(PW_DARK, c, i0, i1, Vector3.up);
        }
        Vector3 hl = b + new Vector3(-r1, h, 0f), hm = b + new Vector3(0f, h + 0.32f, 0f), hr = b + new Vector3(r1, h, 0f);
        LkBeam(mb, PW_DARK, hl, hm, 0.04f);
        LkBeam(mb, PW_DARK, hm, hr, 0.04f);
    }

    // лодка: дно, два борта с наклоном, нос из двух досок, корма, две лавки; c = центр на воде, dir = нос
    static void AddBoat(MB mb, Vector2 c, Vector2 dir, float yW)
    {
        Quaternion q = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
        Vector3 C = new Vector3(c.x, yW, c.y), fwd = q * Vector3.forward, right = q * Vector3.right, up = Vector3.up;
        LkBox(mb, PW_DARK, C - up * 0.18f, new Vector3(0.36f, 0.05f, 1.05f), q);
        for (int s = -1; s <= 1; s += 2)
            LkBox(mb, PW_TEAL, C + right * (s * 0.5f) + up * 0.04f - fwd * 0.27f, new Vector3(0.05f, 0.27f, 0.83f), q * Quaternion.Euler(0f, 0f, -s * 18f));
        LkBox(mb, PW_TEAL, C - fwd * 1.1f + up * 0.04f, new Vector3(0.55f, 0.27f, 0.05f), q);
        Vector3 fp = C + fwd * 1.3f + up * 0.04f;
        float c35 = Mathf.Cos(35f * Mathf.Deg2Rad), s35 = Mathf.Sin(35f * Mathf.Deg2Rad);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 v = -fwd * c35 + right * (s * s35);
            LkBox(mb, PW_TEAL, fp + v * 0.45f, new Vector3(0.05f, 0.27f, 0.45f), Quaternion.LookRotation(v, up));
        }
        LkBox(mb, PW_A, C - fwd * 0.2f + up * 0.1f, new Vector3(0.5f, 0.04f, 0.12f), q);
        LkBox(mb, PW_A, C + fwd * 0.55f + up * 0.1f, new Vector3(0.45f, 0.04f, 0.12f), q);
    }

    // цветок на стебле: голова из повёрнутого кубика
    static void AddFlowerTuft(MB mb, Vector2 pos, float y, int sub, System.Random rnd)
    {
        int n = 3 + rnd.Next(0, 3);
        for (int k = 0; k < n; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 0.35f, hgt = 0.35f + Rf(rnd) * 0.3f;
            Vector2 b = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            Vector3 top = new Vector3(b.x, y + hgt, b.y);
            LkBeam(mb, PW_LEAF, new Vector3(b.x, y - 0.02f, b.y), top, 0.03f);
            LkBox(mb, sub, top, new Vector3(0.07f, 0.06f, 0.07f), Quaternion.Euler(0f, Rf(rnd) * 90f, 0f));
        }
    }

    // пучок высокой травы: тонкие изогнутые лезвия
    static void AddGrass(MB mb, Vector2 pos, float y, System.Random rnd)
    {
        int n = 6 + rnd.Next(0, 4);
        for (int k = 0; k < n; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 0.3f, hgt = 0.5f + Rf(rnd) * 0.6f;
            Vector3 b = new Vector3(pos.x + Mathf.Cos(a) * rr, y - 0.03f, pos.y + Mathf.Sin(a) * rr);
            Vector3 tip = b + new Vector3(Mathf.Cos(a) * 0.2f, hgt, Mathf.Sin(a) * 0.2f);
            Vector3 side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.06f;
            Vector3 nrm = Vector3.Cross(tip - (b + side), tip - (b - side)).normalized;
            mb.Tri(PW_LEAF, b - side, b + side, tip, nrm);
            mb.Tri(PW_LEAF, b - side, b + side, tip, -nrm);
        }
    }

    // рыбка кои под поверхностью: тело, хвост и белое пятно; sub = цвет, size = масштаб
    static void AddFish(MB mb, Vector2 p, float y, float yaw, int sub, float size)
    {
        Vector3 c = new Vector3(p.x, y, p.y);
        Vector3 f = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), sd = new Vector3(-f.z, 0f, f.x);
        float Ln = 0.5f * size, Wd = 0.14f * size;
        Vector3 nose = c + f * Ln, tail = c - f * Ln * 0.7f;
        mb.Tri(sub, nose, c + sd * Wd, c - sd * Wd, Vector3.up);
        mb.Tri(sub, c + sd * Wd, tail, c - sd * Wd, Vector3.up);
        mb.Tri(sub, tail, tail - f * 0.25f * size + sd * 0.13f * size, tail - f * 0.25f * size - sd * 0.13f * size, Vector3.up);
        Vector3 u = new Vector3(0f, 0.004f, 0f);
        mb.Tri(PW_FLOWER, c + f * 0.08f * size + u, c - f * 0.18f * size + sd * 0.07f * size + u, c - f * 0.18f * size - sd * 0.07f * size + u, Vector3.up);
    }

    // фонарь: колпак, светящееся стекло, основание; top = верх колпака. Регистрирует точечный свет
    static void AddLantern(MB mb, List<PondLamp> lamps, Vector3 top, float range, float intensity)
    {
        LkBox(mb, PW_DARK, top, new Vector3(0.07f, 0.03f, 0.07f), Quaternion.identity);
        LkBox(mb, PW_GLOW, top - new Vector3(0f, 0.14f, 0f), new Vector3(0.06f, 0.1f, 0.06f), Quaternion.identity);
        LkBox(mb, PW_DARK, top - new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.02f, 0.07f), Quaternion.identity);
        if (lamps != null)
            lamps.Add(new PondLamp { pos = top - new Vector3(0f, 0.14f, 0f), col = new Color(1f, 0.72f, 0.38f), range = range, intensity = intensity, flicker = true });
    }

    // гирлянда: бусины со провисом между двумя точками
    static void AddGarland(MB mb, Vector3 a, Vector3 b, float sag, int beads)
    {
        Vector3 prev = a;
        for (int k = 1; k <= beads; k++)
        {
            float t = k / (float)beads;
            Vector3 q = Vector3.Lerp(a, b, t) - new Vector3(0f, sag * 4f * t * (1f - t), 0f);
            LkBeam(mb, PW_DARK, prev, q, 0.015f);
            if (k < beads) LkBox(mb, PW_GLOW, q, new Vector3(0.035f, 0.035f, 0.035f), Quaternion.identity);
            prev = q;
        }
    }

    // навес рыбака на конце пирса: четыре стойки, раскосы, двускатная крыша из рядов досок, коньковый брус,
    // фронтоны, два подвесных фонаря, гирлянды по свесам, ящики. Фонарные столбики на входе пирса.
    static void AddPavilion(MB mb, List<PondLamp> lamps, PondPlan P, Func<float, float> Y, float len, System.Random rnd)
    {
        Vector2 d = P.pierIn;
        Vector3 d3 = new Vector3(d.x, 0f, d.y);
        Quaternion qd = Quaternion.LookRotation(d3, Vector3.up);
        Vector3 rv = qd * Vector3.right, up = Vector3.up;
        float t0 = Mathf.Max(0.8f, len - 3.8f), t1 = len - 0.5f, tm = (t0 + t1) * 0.5f, Lr = t1 - t0 + 0.7f;
        float hw = PondPierW * 0.5f - 0.12f, H = 2.3f, ov = 0.55f, e = hw + ov, rise = 0.85f;
        float yb = Y(tm);
        Vector3 mid = new Vector3(P.pierS.x + d.x * tm, 0f, P.pierS.y + d.y * tm);
        Func<float, Vector3> At = t => new Vector3(P.pierS.x + d.x * t, Y(t), P.pierS.y + d.y * t);

        // стойки и раскосы
        for (int a = 0; a < 2; a++)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                float t = a == 0 ? t0 : t1;
                Vector3 pc = At(t) + rv * (sg * hw);
                AddPost(mb, PW_DARK, new Vector2(pc.x, pc.z), pc.y - 0.1f, yb + H, 0.16f, false);
                Vector3 along = d3 * (a == 0 ? 0.55f : -0.55f);
                LkBeam(mb, PW_DARK, new Vector3(pc.x, yb + H - 0.65f, pc.z), new Vector3(pc.x, yb + H, pc.z) + along, 0.07f);
                LkBeam(mb, PW_DARK, new Vector3(pc.x, yb + H - 0.65f, pc.z), new Vector3(pc.x, yb + H, pc.z) - rv * (sg * 0.45f), 0.07f);
            }
        // обвязка по верху
        for (int a = 0; a < 2; a++)
        {
            Vector3 c0 = At(a == 0 ? t0 : t1);
            LkBeam(mb, PW_DARK, new Vector3(c0.x, yb + H, c0.z) - rv * hw, new Vector3(c0.x, yb + H, c0.z) + rv * hw, 0.12f);
        }
        for (int sg = -1; sg <= 1; sg += 2)
            LkBeam(mb, PW_DARK, new Vector3(At(t0).x, yb + H, At(t0).z) + rv * (sg * hw), new Vector3(At(t1).x, yb + H, At(t1).z) + rv * (sg * hw), 0.12f);

        // крыша: ряды досок на каждом скате, наружный ряд ниже
        int rows = 4;
        float ang = Mathf.Atan2(rise, e) * Mathf.Rad2Deg;
        float rowW = Mathf.Sqrt(e * e + rise * rise) / rows * 1.18f;
        for (int sg = -1; sg <= 1; sg += 2)
            for (int r = 0; r < rows; r++)
            {
                float tt = (r + 0.5f) / rows;
                Vector3 ctr = mid + rv * (sg * e * (1f - tt)) + new Vector3(0f, yb + H + 0.08f + rise * tt, 0f);
                int sub = (r + (sg > 0 ? 0 : 1)) % 2 == 0 ? PW_B : PW_A;
                if (r == 0 && sg < 0) sub = PW_TEAL;   // цветная полоса по свесу
                LkBox(mb, sub, ctr, new Vector3(rowW * 0.5f, 0.035f, Lr * 0.5f), qd * Quaternion.Euler(0f, 0f, -sg * ang));
            }
        Vector3 ridgeC = mid + new Vector3(0f, yb + H + rise + 0.12f, 0f);
        LkBeam(mb, PW_DARK, ridgeC - d3 * (Lr * 0.5f + 0.05f), ridgeC + d3 * (Lr * 0.5f + 0.05f), 0.14f);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 eaveC = mid + rv * (sg * e) + new Vector3(0f, yb + H + 0.04f, 0f);
            LkBeam(mb, PW_DARK, eaveC - d3 * (Lr * 0.5f), eaveC + d3 * (Lr * 0.5f), 0.08f);
        }
        // фронтоны: треугольные щиты спереди и сзади
        for (int a = 0; a < 2; a++)
        {
            Vector3 g = mid + d3 * ((a == 0 ? -1f : 1f) * (Lr * 0.5f - 0.2f));
            Vector3 pa = g + new Vector3(0f, yb + H + rise + 0.05f, 0f);
            Vector3 pb = g + rv * (-e + 0.1f) + new Vector3(0f, yb + H + 0.1f, 0f);
            Vector3 pc2 = g + rv * (e - 0.1f) + new Vector3(0f, yb + H + 0.1f, 0f);
            Vector3 nrm = Vector3.Cross(pb - pa, pc2 - pa);
            mb.Tri(PW_B, pa, pb, pc2, nrm);
            mb.Tri(PW_B, pa, pb, pc2, -nrm);
        }
        // два подвесных фонаря под коньком
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 hp = mid + d3 * (k * 0.9f) + new Vector3(0f, yb + H + rise - 0.55f, 0f);
            LkBeam(mb, PW_DARK, hp + new Vector3(0f, 0.55f, 0f), hp, 0.02f);
            AddLantern(mb, lamps, hp, 6.5f, 1.7f);
        }
        // гирлянды по свесам
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 eaveC = mid + rv * (sg * e) + new Vector3(0f, yb + H - 0.02f, 0f);
            AddGarland(mb, eaveC - d3 * (Lr * 0.5f - 0.1f), eaveC + d3 * (Lr * 0.5f - 0.1f), 0.22f, 9);
        }
        // ящики у стойки
        Vector3 cr = At(t0 + 0.45f) + rv * (hw - 0.35f);
        LkBox(mb, PW_B, new Vector3(cr.x, cr.y + 0.2f, cr.z), new Vector3(0.24f, 0.2f, 0.24f), qd * Quaternion.Euler(0f, 12f, 0f));
        LkBox(mb, PW_A, new Vector3(cr.x, cr.y + 0.58f, cr.z), new Vector3(0.19f, 0.17f, 0.19f), qd * Quaternion.Euler(0f, -18f, 0f));

        // фонарные столбики на входе пирса
        for (int sg = -1; sg <= 1; sg += 2)
        {
            Vector3 lp = At(0.7f) + rv * (sg * (PondPierW * 0.5f + 0.12f));
            AddPost(mb, PW_DARK, new Vector2(lp.x, lp.z), lp.y - 0.25f, lp.y + 1.7f, 0.14f, true);
            LkBeam(mb, PW_DARK, new Vector3(lp.x, lp.y + 1.65f, lp.z), new Vector3(lp.x, lp.y + 1.65f, lp.z) - rv * (sg * 0.25f), 0.05f);
            AddLantern(mb, lamps, new Vector3(lp.x, lp.y + 1.58f, lp.z) - rv * (sg * 0.25f), 6f, 1.4f);
        }
    }

    static void MakePondLight(Transform parent, Vector3 localPos, Color col, float range, float intensity, bool flicker)
    {
        var g = new GameObject(flicker ? "PondLantern" : "PondGlow");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = col; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        if (flicker)
        {
            var lf = g.AddComponent<LightFlicker>();
            lf.Configure(FlickerPreset.Lantern, l, new Transform[0]);
        }
    }

    // верхняя кромка чаши по направлению ang: от уреза наружу до земли выше -0.15
    static float PondRimR(Layout L, Field F, float ang, float rw)
    {
        var P = L.pond;
        Vector2 dv = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        for (float r = rw; r < L.c.lakeRadius + 3f; r += 0.1f)
            if (F.g.At(F.H, P.center.x + dv.x * r, P.center.y + dv.y * r) >= -0.15f) return r;
        return rw;
    }

    // рогоз: тёмные «сосиски» на высоких стеблях рядом с камышом
    static void AddCattails(MB mb, Vector2 pos, float y, System.Random rnd)
    {
        for (int k = 0; k < 3; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = 0.15f + Rf(rnd) * 0.4f, hgt = 1.3f + Rf(rnd) * 0.7f;
            Vector2 b = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            Vector3 top = new Vector3(b.x + 0.06f, y + hgt, b.y);
            LkBeam(mb, PW_REED, new Vector3(b.x, y - 0.05f, b.y), top, 0.035f);
            LkBox(mb, PW_DARK, top - new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.15f, 0.05f), Quaternion.identity);
        }
    }

    // старая, но милая гребная лодка: плавный корпус из 4 поясов (тёмный, дерево, кремовая полоса, бирюзовая планка),
    // загнутый форштевень, рёбра, банки, уключины, два весла, фонарь на носу (свет), ящик с рыбой на корме, мох,
    // пара выпавших досок. Сидит на воде с лёгким креном.
    static void AddOldBoat(MB mb, List<PondLamp> lamps, Vector2 c, Vector2 dir, float yW, System.Random rnd)
    {
        Quaternion Q = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up) * Quaternion.Euler(-3f, 0f, 4f);
        Vector3 C = new Vector3(c.x, yW + 0.1f, c.y);
        Func<Vector3, Vector3> W = l => C + Q * l;
        const int N = 10;
        var zs = new float[N]; var hw = new float[N]; var gy = new float[N]; var by = new float[N];
        for (int i = 0; i < N; i++)
        {
            float u = i / (float)(N - 1);
            zs[i] = -1.35f + 3.1f * u;
            hw[i] = u < 0.45f ? Mathf.Lerp(0.5f, 0.68f, SS(0f, 0.45f, u)) : Mathf.Lerp(0.68f, 0.03f, Mathf.Pow((u - 0.45f) / 0.55f, 1.5f));
            gy[i] = 0.34f + 0.3f * u * u * u;
            by[i] = -0.2f + 0.38f * Mathf.Pow(u, 3.5f);
        }
        int[] layer = { PW_DARK, PW_B, PW_FLOWER, PW_TEAL };

        LkBeam(mb, PW_DARK, W(new Vector3(0f, by[0] - 0.04f, zs[0])), W(new Vector3(0f, by[N - 1], zs[N - 1])), 0.12f);
        // форштевень: загнутый нос с красным набалдашником
        Vector3 stem0 = W(new Vector3(0f, by[N - 1], zs[N - 1])), stem1 = W(new Vector3(0f, gy[N - 1] + 0.3f, zs[N - 1] + 0.2f));
        LkBeam(mb, PW_DARK, stem0, stem1, 0.1f);
        LkBox(mb, PW_RED, stem1, new Vector3(0.07f, 0.07f, 0.07f), Q);

        for (int sg = -1; sg <= 1; sg += 2)
            for (int i = 0; i < N - 1; i++)
            {
                if (i >= 1 && i <= N - 3)
                {
                    LkBeam(mb, PW_DARK, W(new Vector3(sg * 0.1f, by[i], zs[i])), W(new Vector3(sg * hw[i] * 0.92f, gy[i] - 0.02f, zs[i])), 0.06f);   // рёбра
                }
                for (int j = 0; j < 4; j++)
                {
                    if (j < 3 && Rf(rnd) < 0.05f) continue;   // выпавшая доска
                    float f0 = (j + 0.5f) / 4f;
                    Vector3 A = new Vector3(sg * hw[i] * (0.6f + 0.4f * f0), Mathf.Lerp(by[i], gy[i], f0), zs[i]);
                    Vector3 B = new Vector3(sg * hw[i + 1] * (0.6f + 0.4f * f0), Mathf.Lerp(by[i + 1], gy[i + 1], f0), zs[i + 1]);
                    LkBeam(mb, layer[j], W(A), W(B), 0.125f);
                }
                if (!(sg < 0 && i == 5))   // планшир, один кусок отломан
                    LkBeam(mb, PW_A, W(new Vector3(sg * hw[i] * 1.02f, gy[i] + 0.07f, zs[i])), W(new Vector3(sg * hw[i + 1] * 1.02f, gy[i + 1] + 0.07f, zs[i + 1])), 0.07f);
            }

        // корма, днище, банки, уключины
        LkBox(mb, PW_B, W(new Vector3(0f, 0.12f, zs[0] - 0.03f)), new Vector3(0.52f, 0.26f, 0.03f), Q);
        LkBox(mb, PW_FLOWER, W(new Vector3(0f, 0.17f, zs[0] - 0.05f)), new Vector3(0.5f, 0.05f, 0.02f), Q);
        for (int i = 0; i < N - 3; i++)
        {
            if (Rf(rnd) < 0.12f) continue;
            LkBox(mb, i % 2 == 0 ? PW_A : PW_DARK, W(new Vector3(0f, by[i] + 0.04f, (zs[i] + zs[i + 1]) * 0.5f)), new Vector3(hw[i] * 0.72f, 0.025f, 0.2f), Q);
        }
        LkBox(mb, PW_A, W(new Vector3(0f, 0.1f, -1.0f)), new Vector3(0.58f, 0.03f, 0.14f), Q);
        LkBox(mb, PW_A, W(new Vector3(0f, 0.12f, -0.15f)), new Vector3(0.64f, 0.03f, 0.12f), Q);
        LkBox(mb, PW_A, W(new Vector3(0f, 0.14f, 0.7f)), new Vector3(0.6f, 0.03f, 0.12f), Q);
        for (int sg = -1; sg <= 1; sg += 2)
        {
            LkBox(mb, PW_METAL, W(new Vector3(sg * 0.72f, 0.46f, 0.1f)), new Vector3(0.03f, 0.08f, 0.03f), Q);
            LkBox(mb, PW_METAL, W(new Vector3(sg * 0.72f, 0.56f, 0.1f)), new Vector3(0.06f, 0.015f, 0.06f), Q);
        }

        // вёсла: одно опущено в воду, второе лежит в лодке
        Vector3 oa = W(new Vector3(-0.35f, 0.62f, 0.18f)), ob = W(new Vector3(1.9f, -0.15f, 0.02f));
        LkBeam(mb, PW_A, oa, ob, 0.045f);
        LkBox(mb, PW_TEAL, W(new Vector3(2.0f, -0.17f, 0.02f)), new Vector3(0.2f, 0.02f, 0.09f), Q);
        LkBeam(mb, PW_A, W(new Vector3(-0.5f, 0.2f, -0.9f)), W(new Vector3(0.5f, 0.22f, 1.0f)), 0.045f);
        LkBox(mb, PW_TEAL, W(new Vector3(0.58f, 0.22f, 1.14f)), new Vector3(0.09f, 0.02f, 0.2f), Q);

        // фонарь на шесте у носа (даёт свет)
        Vector3 pole0 = W(new Vector3(0f, gy[N - 3], zs[N - 3])), pole1 = W(new Vector3(0f, gy[N - 3] + 0.95f, zs[N - 3]));
        LkBeam(mb, PW_DARK, pole0, pole1, 0.05f);
        LkBeam(mb, PW_DARK, pole1, pole1 + Q * new Vector3(0f, 0f, 0.22f), 0.04f);
        AddLantern(mb, lamps, pole1 + Q * new Vector3(0f, 0f, 0.22f), 4.5f, 1.3f);

        // ящик с уловом на корме
        Vector3 cr = W(new Vector3(0f, 0.04f, -0.7f));
        LkBox(mb, PW_B, cr + Q * new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.11f, 0.22f), Q);
        for (int k = 0; k < 3; k++)
        {
            Vector3 fp = cr + Q * new Vector3((k - 1) * 0.15f, 0.23f, (Rf(rnd) - 0.5f) * 0.1f);
            AddFish(mb, new Vector2(fp.x, fp.z), fp.y, 1.57f + (Rf(rnd) - 0.5f) * 0.5f + Mathf.Atan2(dir.y, dir.x) - 1.57f, k == 1 ? PW_YELLOW : PW_ORANGE, 0.45f);
        }
        // мох
        for (int k = 0; k < 6; k++)
        {
            int sg = Rf(rnd) < 0.5f ? -1 : 1, i = rnd.Next(0, N - 4);
            LkBox(mb, PW_LEAF, W(new Vector3(sg * hw[i] * 1.0f, gy[i] + 0.1f, zs[i])), new Vector3(0.1f, 0.025f, 0.13f), Q);
        }
    }

    static void AddIsleTree(MB mb, Vector2 pos, float y, System.Random rnd)
    {
        AddCone(mb, PW_DARK, new Vector3(pos.x, y - 0.1f, pos.y), 0.15f, 2.2f, 5, Rf(rnd) * 6.28f);
        AddBoulder(mb, PW_CROWN, pos, y + 2.5f, 2.1f, rnd, 1f);
        AddBoulder(mb, PW_CROWN, pos + new Vector2(0.45f, -0.2f), y + 3.2f, 1.3f, rnd, 1f);
    }

    // =====================================================================================================
    //  СБОРКА
    // =====================================================================================================

    // вода пруда: кольца вокруг центра чаши. uv.x = глубина воды по рельефу (бирюза и пена у берега, у острова, у свай),
    // uv.y = расстояние до острова (круги расходятся от него). Края с глубиной < 0 шейдер делает прозрачными.
    static Mesh BuildPond(Layout L, Field F, string name)
    {
        var c = L.c; float ws = c.worldScale;
        float yW = F.lakeWaterY;
        float rOut = c.lakeRadius * 0.9f;
        Vector2 isle = L.pond != null ? L.pond.isle : L.lakePos;
        const int seg = 72, rings = 36;
        var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int k = 0; k <= rings; k++)
        {
            float rr = rOut * k / rings;
            for (int s = 0; s < seg; s++)
            {
                float a = s * Mathf.PI * 2f / seg;
                float x = L.lakePos.x + Mathf.Cos(a) * rr, z = L.lakePos.y + Mathf.Sin(a) * rr;
                float depth = yW - F.g.At(F.H, x, z);
                vs.Add(new Vector3(x * ws, yW * ws, z * ws));
                uvs.Add(new Vector2(depth, Vector2.Distance(new Vector2(x, z), isle)));
            }
        }
        for (int k = 0; k < rings; k++)
            for (int s = 0; s < seg; s++)
            {
                int s2 = (s + 1) % seg;
                int a0 = k * seg + s, a1 = k * seg + s2, b0 = (k + 1) * seg + s, b1 = (k + 1) * seg + s2;
                tris.Add(a0); tris.Add(b1); tris.Add(b0);
                tris.Add(a0); tris.Add(a1); tris.Add(b1);
            }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // 8-12 валунов вдоль берега (часть торчит из воды у уреза) и 3 камня на острове. mb = меш Boulders (метры).
    static void PondRocks(Layout L, Field F, MB mb, System.Random rnd, float ws)
    {
        var P = L.pond; var c = L.c;
        if (P == null) return;
        int want = 8 + rnd.Next(0, 5);
        for (int k = 0, placed = 0; k < want * 3 && placed < want; k++)
        {
            float a = (placed + Rf(rnd) * 0.8f) / want * Mathf.PI * 2f;
            float rw = PondWaterR(L, F, a);
            if (rw < 1f) continue;
            bool inWater = placed >= want - 2;
            float rr = inWater ? rw - 0.3f - Rf(rnd) * 0.7f : rw + 0.3f + Rf(rnd) * 1.5f;
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 1.6f || F.g.At(F.dT, pos.x, pos.y) < 3.5f) continue;
            float dia = 0.7f + Rf(rnd) * 0.9f;
            float bed = F.g.At(F.H, pos.x, pos.y);
            float y = inWater ? Mathf.Max(bed + dia * 0.3f, P.yW + dia * 0.06f) : bed + dia * 0.18f;
            AddBoulder(mb, 0, pos, y, dia, rnd, ws);
            placed++;
        }
        // мелкие камни по самой кромке: каменный венчик чаши
        for (int k = 0, put = 0; k < 90 && put < 22; k++)
        {
            float a = Rf(rnd) * Mathf.PI * 2f;
            float rw = PondWaterR(L, F, a);
            if (rw < 1f) continue;
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rw + 0.1f + Rf(rnd) * 1.4f);
            if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 1.2f || F.g.At(F.dT, pos.x, pos.y) < 3f) continue;
            float size = 0.3f + Rf(rnd) * 0.4f;
            AddRock(mb, pos, F.g.At(F.H, pos.x, pos.y) + size * 0.2f, size, rnd, ws);
            put++;
        }
        for (int k = 0; k < (P.hasIsle ? 3 : 0); k++)
        {
            float a = (k + Rf(rnd) * 0.6f) / 3f * Mathf.PI * 2f + 0.8f;
            Vector2 pos = P.isle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * P.isleR * (0.7f + Rf(rnd) * 0.2f);
            if (PondClear(P, pos) < 0.9f) continue;
            float size = 0.5f + Rf(rnd) * 0.4f;
            AddRock(mb, pos, F.g.At(F.H, pos.x, pos.y) + size * 0.2f, size, rnd, ws);
        }
    }

    // всё остальное: деревянные постройки, кувшинки, камыш, деревце на острове; материалы, коллайдеры
    static void BuildPondStructures(Layout L, Field F, Transform root, Texture2D[] tex)
    {
        var P = L.pond; var c = L.c;
        if (P == null) return;
        float ws = c.worldScale, yW = P.yW;
        var mb = new MB(PondSubs);
        var rnd = new System.Random(c.seed * 61 + 19);
        Func<Vector2, float> Hy = p => F.g.At(F.H, p.x, p.y);
        var foam = new List<Vector2>();
        var lamps = new List<PondLamp>();

        // ---- мостик и пирс
        if (P.brOn) PondDeck(mb, F, yW, P.brA, P.brB, P.brYA, P.brYB, PondBridgeW, true, false, rnd, foam);
        if (P.pierOn)
        {
            PondDeck(mb, F, yW, P.pierS, P.pierE, P.pierYS, P.pierYE, PondPierW, false, true, rnd, foam);
            Vector2 d = P.pierIn, side = new Vector2(-d.y, d.x);
            float len = Vector2.Distance(P.pierS, P.pierE);
            Func<float, float> Y = t => Mathf.Lerp(P.pierYS, P.pierYE, t / len);
            Vector2 bs = P.boatSide;

            // ведро на настиле у конца
            Vector2 bp = P.pierE - d * 0.5f + bs * 0.4f;
            AddBucket(mb, new Vector3(bp.x, Y(len - 0.5f), bp.y));

            // навес с крышей, фонарями и гирляндами; фонарные столбики на входе пирса
            AddPavilion(mb, lamps, P, Y, len, rnd);

            // удочка на рогульке с поплавком, на стороне напротив лодки
            Vector2 rb = P.pierS + d * (len - 1.2f) - bs * (PondPierW * 0.5f - 0.35f);
            float yr = Y(len - 1.2f);
            Vector3 rBase = new Vector3(rb.x, yr + 0.25f, rb.y);
            Vector2 tipXZ = rb - bs * 2.0f + d * 0.6f;
            Vector3 tip = new Vector3(tipXZ.x, yr + 1.7f, tipXZ.y);
            LkBeam(mb, PW_DARK, rBase, tip, 0.07f);
            Vector3 fork = Vector3.Lerp(rBase, tip, 0.3f);
            LkBeam(mb, PW_DARK, new Vector3(fork.x, yr, fork.z), fork, 0.07f);
            LkBeam(mb, PW_DARK, tip, new Vector3(tip.x, yW + 0.08f, tip.z), 0.03f);
            LkBox(mb, PW_RED, new Vector3(tip.x, yW + 0.07f, tip.z), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.identity);

            // лодка и верёвка к пеньку
            if (P.boatOn)
            {
                AddOldBoat(mb, lamps, P.boatC, d, yW, rnd);
                Vector2 bo = P.pierS + d * (len - 0.3f) + bs * (PondPierW * 0.5f + 0.08f);
                Vector2 bow = P.boatC + d * 1.3f;
                LkBeam(mb, PW_DARK, new Vector3(bo.x, Y(len - 0.3f) + 0.5f, bo.y), new Vector3(bow.x, yW + 0.25f, bow.y), 0.04f);
            }
        }

        // ---- пенные кольца вокруг свай в воде
        foreach (var fp in foam) AddFoamRing(mb, fp, yW + 0.025f, 0.2f, 0.55f, rnd);

        // ---- кувшинки: пятнами в мелководье, мимо построек и острова
        int want = Mathf.Clamp(c.pondLilies, 0, 40);
        var lilies = new List<Vector2>();
        Func<Vector2, bool> lilyOk = p =>
        {
            if (Vector2.Distance(p, P.center) > c.lakeRadius * 0.78f) return false;
            float depth = yW - Hy(p);
            if (depth < 0.25f || depth > 2.2f) return false;
            if ((P.hasIsle && Vector2.Distance(p, P.isle) < P.isleR + 0.9f) || PondClear(P, p) < 0.9f) return false;
            foreach (var q in lilies) if ((q - p).sqrMagnitude < 1.2f) return false;
            return true;
        };
        var centers = new List<Vector2>();
        int clusters = Mathf.Clamp(want / 4, 1, 6);
        for (int tries = 0; tries < 300 && centers.Count < clusters && want > 0; tries++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * c.lakeRadius * 0.75f;
            Vector2 p = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (lilyOk(p)) centers.Add(p);
        }
        for (int tries = 0; tries < 800 && lilies.Count < want && centers.Count > 0; tries++)
        {
            Vector2 cc = centers[tries % centers.Count];
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * 1.8f;
            Vector2 p = cc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!lilyOk(p)) continue;
            lilies.Add(p);
            AddLily(mb, p, yW + 0.04f, 0.55f + Rf(rnd) * 0.4f, Rf(rnd) < 0.8f, lilies.Count % 2 == 0 ? PW_LEAF : PW_CROWN, rnd);
        }

        // ---- камыш у воды (по урезу) и на острове
        int reeds = 0;
        for (int tries = 0; tries < 400 && reeds < 22; tries++)
        {
            float a = Rf(rnd) * 6.28f, rw = PondWaterR(L, F, a);
            if (rw < 1f) continue;
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rw + Rf(rnd) * 0.4f - 0.15f);
            if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 1.4f || F.g.At(F.dT, pos.x, pos.y) < 3.5f) continue;
            AddReeds(mb, pos, Hy(pos), rnd, 1f);
            if (reeds % 2 == 0) AddCattails(mb, pos, Hy(pos), rnd);
            reeds++;
        }
        for (int k = 0; k < (P.hasIsle ? 2 : 0); k++)
        {
            float a = 2.2f + k * 2.6f + Rf(rnd) * 0.5f;
            Vector2 pos = P.isle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * P.isleR * 0.85f;
            if (PondClear(P, pos) < 0.9f) continue;
            AddReeds(mb, pos, Hy(pos), rnd, 1f);
        }

        // ---- бордюр из плоских камней по верхней кромке чаши
        {
            int nCurb = Mathf.CeilToInt(2f * Mathf.PI * c.lakeRadius / 0.65f);
            for (int k = 0; k < nCurb; k++)
            {
                float a = (k + (Rf(rnd) - 0.5f) * 0.3f) / nCurb * Mathf.PI * 2f;
                float rw = PondWaterR(L, F, a);
                if (rw < 1f) continue;
                Vector2 dv = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = P.center + dv * (PondRimR(L, F, a, rw) + 0.05f);
                if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 0.9f || F.g.At(F.dT, pos.x, pos.y) < 1.2f) continue;
                Quaternion cq = Quaternion.LookRotation(new Vector3(-dv.y, 0f, dv.x), Vector3.up);
                LkBox(mb, PW_STONE, new Vector3(pos.x, Hy(pos) + 0.02f, pos.y), new Vector3(0.17f, 0.12f, 0.35f + Rf(rnd) * 0.12f), cq);
            }
        }

        // ---- высокая трава по берегу: на песчаной полке и над кромкой
        for (int k = 0, put = 0; k < 400 && put < 55; k++)
        {
            float a = Rf(rnd) * 6.28f, rw = PondWaterR(L, F, a);
            if (rw < 1f) continue;
            float rim = PondRimR(L, F, a, rw);
            float rr = Rf(rnd) < 0.5f ? rw + 0.3f + Rf(rnd) * Mathf.Max(0.2f, rim - rw - 0.6f) : rim + 0.3f + Rf(rnd) * 1.2f;
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 1.2f || F.g.At(F.dT, pos.x, pos.y) < 2.5f) continue;
            AddGrass(mb, pos, Hy(pos), rnd);
            put++;
        }

        // ---- рыбки кои: две стайки по три, две большие одиночки, круги по воде впереди
        {
            int[] fsubs = { PW_ORANGE, PW_YELLOW, PW_RED, PW_ORANGE };
            int schools = 0;
            for (int k = 0; k < 300 && schools < 4; k++)
            {
                float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * c.lakeRadius * 0.7f;
                Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (yW - Hy(pos) < 1.4f || (P.hasIsle && Vector2.Distance(pos, P.isle) < P.isleR + 1.5f) || PondClear(P, pos) < 2.4f) continue;
                float yaw = Rf(rnd) * 6.28f;
                Vector2 fw = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw)), sdv = new Vector2(-fw.y, fw.x);
                int cnt = schools < 2 ? 3 : 1;
                for (int m = 0; m < cnt; m++)
                    AddFish(mb, pos + sdv * ((m - 1) * 0.5f) - fw * (m * 0.35f), yW - 0.07f - m * 0.04f, yaw + (Rf(rnd) - 0.5f) * 0.5f, fsubs[schools], cnt > 1 ? 0.8f : 1.3f);
                AddFoamRing(mb, pos + fw * 0.9f, yW + 0.025f, 0.18f, 0.42f, rnd);
                schools++;
            }
        }

        // ---- светлячки над водой и берегом
        for (int k = 0, put = 0; k < 300 && put < 28; k++)
        {
            float a = Rf(rnd) * 6.28f, rr = Rf(rnd) * (c.lakeRadius + 3f);
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!L.outline.Inside(pos.x, pos.y)) continue;
            float fy = Mathf.Max(yW, Hy(pos)) + 0.5f + Rf(rnd) * 2.2f;
            LkBox(mb, PW_GLOW, new Vector3(pos.x, fy, pos.y), new Vector3(0.04f, 0.04f, 0.04f), Quaternion.Euler(Rf(rnd) * 90f, Rf(rnd) * 90f, Rf(rnd) * 90f));
            put++;
        }

        // ---- кромка: кусты и цветные пятна (оранжевые, жёлтые, белые) у воды
        int bushes = 0, tufts = 0;
        for (int tries = 0; tries < 300 && (bushes < 10 || tufts < 26); tries++)
        {
            float a = Rf(rnd) * 6.28f, rw = PondWaterR(L, F, a);
            if (rw < 1f) continue;
            bool bush = bushes < 10 && (tufts >= 26 || Rf(rnd) < 0.35f);
            Vector2 pos = P.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (rw + (bush ? 1.0f + Rf(rnd) * 1.6f : 0.6f + Rf(rnd) * 2.4f));
            if (!L.outline.Inside(pos.x, pos.y) || PondClear(P, pos) < 1.6f || F.g.At(F.dT, pos.x, pos.y) < 3.5f) continue;
            if (bush) { AddBoulder(mb, PW_CROWN, pos, Hy(pos) + 0.15f, 0.9f + Rf(rnd) * 0.6f, rnd, 1f); bushes++; }
            else { AddFlowerTuft(mb, pos, Hy(pos), tufts % 3 == 0 ? PW_ORANGE : (tufts % 3 == 1 ? PW_YELLOW : PW_FLOWER), rnd); tufts++; }
        }

        // ---- остров: одно деревце и пара кустов
        Vector2 tp = P.isle - P.bridgeDir * 0.2f + P.toPad * 0.3f;
        if (P.hasIsle) AddIsleTree(mb, tp, Hy(tp), rnd);
        for (int k = 0; k < (P.hasIsle ? 2 : 0); k++)
        {
            float a = 0.6f + k * 2.4f + Rf(rnd) * 0.5f;
            Vector2 pos = P.isle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * P.isleR * 0.5f;
            if (PondClear(P, pos) < 0.8f) continue;
            AddBoulder(mb, PW_CROWN, pos, Hy(pos) + 0.2f, 0.8f + Rf(rnd) * 0.4f, rnd, 1f);
        }

        for (int vi = 0; vi < mb.v.Count; vi++) mb.v[vi] = mb.v[vi] * ws;
        var mesh = mb.Build("LocationTest_PondProps");
        SaveAsset(mesh, AssetDir + "/Meshes/LocationTest_PondProps.asset");

        // ---- материалы
        Color wd = c.lookoutWood;
        Material[] mats;
        if (c.cardboard)
        {
            float lt = c.tileM * 0.5f;
            mats = new[]
            {
                MakeCardMat(c, "Pond_PlankA", wd, tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Pond_PlankB", new Color(wd.r * 1.1f, wd.g * 1.05f, wd.b * 0.9f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Pond_WoodDark", new Color(wd.r * 0.62f, wd.g * 0.52f, wd.b * 0.46f, 1f), tex[3], lt, 0f, 0f, false),
                MakeCardMat(c, "Pond_Reed", new Color(1.25f, 1.15f, 0.60f), tex[0], c.tileM * 0.5f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Leaf", new Color(0.50f, 1.05f, 0.42f), tex[0], c.tileM * 0.5f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Flower", new Color(1.6f, 1.55f, 1.5f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Yellow", new Color(1.7f, 1.45f, 0.45f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Red", new Color(1.1f, 0.30f, 0.26f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Teal", new Color(0.30f, 0.85f, 0.90f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Metal", new Color(0.36f, 0.50f, 0.66f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Crown", new Color(0.95f, 1.1f, 0.75f), tex[0], c.tileM * 0.8f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Foam", new Color(1.6f, 1.65f, 1.7f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Orange", new Color(1.7f, 0.85f, 0.25f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Stone", new Color(0.85f, 0.80f, 1.0f), tex[4], c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Pink", new Color(1.7f, 0.70f, 0.95f), _paperTex, c.tileM * 0.6f, 0f, 0f, false),
                MakeCardMat(c, "Pond_Glow", new Color(2.2f, 1.7f, 0.75f), _paperTex, c.tileM * 0.6f, 0f, 0f, false)
            };
        }
        else
            mats = new[]
            {
                MakeMat("Pond_PlankA", new Color(0.66f, 0.44f, 0.24f), false, 0f), MakeMat("Pond_PlankB", new Color(0.72f, 0.50f, 0.28f), false, 0f),
                MakeMat("Pond_WoodDark", new Color(0.40f, 0.26f, 0.14f), false, 0f), MakeMat("Pond_Reed", new Color(0.70f, 0.65f, 0.30f), false, 0f),
                MakeMat("Pond_Leaf", new Color(0.30f, 0.62f, 0.25f), false, 0f), MakeMat("Pond_Flower", new Color(0.98f, 0.97f, 0.95f), false, 0f),
                MakeMat("Pond_Yellow", new Color(1f, 0.85f, 0.30f), false, 0f), MakeMat("Pond_Red", new Color(0.90f, 0.22f, 0.20f), false, 0f),
                MakeMat("Pond_Teal", new Color(0.20f, 0.70f, 0.75f), false, 0f), MakeMat("Pond_Metal", new Color(0.30f, 0.38f, 0.50f), false, 0f),
                MakeMat("Pond_Crown", new Color(0.34f, 0.65f, 0.24f), false, 0f), MakeMat("Pond_Foam", new Color(0.95f, 0.98f, 1f), false, 0f),
                MakeMat("Pond_Orange", new Color(1f, 0.55f, 0.15f), false, 0f),
                MakeMat("Pond_Stone", new Color(0.62f, 0.58f, 0.72f), false, 0f),
                MakeMat("Pond_Pink", new Color(1f, 0.55f, 0.75f), false, 0f),
                MakeMat("Pond_Glow", new Color(1f, 0.88f, 0.45f), false, 0f)
            };
        var go = new GameObject("PondStructures");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = ShadowCastingMode.On;

        // ---- освещение: тёплые мерцающие фонари и бирюзовое свечение воды
        foreach (var lp in lamps) MakePondLight(go.transform, lp.pos * ws, lp.col, lp.range * ws, lp.intensity, lp.flicker);
        MakePondLight(go.transform, new Vector3(P.center.x, yW - 0.4f, P.center.y) * ws, new Color(0.25f, 0.9f, 1f), c.lakeRadius * 0.85f * ws, 1.4f, false);

        // ---- коллайдеры: настил, перила мостика (игрок на Rigidbody не должен свалиться), пирс без перил
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(go.transform, false);
        if (P.brOn) PondDeckColliders(cg.transform, "Bridge", P.brA, P.brB, P.brYA, P.brYB, PondBridgeW, true, ws);
        if (P.pierOn) PondDeckColliders(cg.transform, "Pier", P.pierS, P.pierE, P.pierYS, P.pierYE, PondPierW, false, ws);
    }

    static void PondDeckColliders(Transform parent, string nm, Vector2 a, Vector2 b, float ya, float yb, float w, bool rails, float ws)
    {
        Vector2 d = b - a; float len = d.magnitude;
        if (len < 0.5f) return;
        d /= len;
        Vector3 dir3 = new Vector3(d.x, (yb - ya) / len, d.y);
        Quaternion q = Quaternion.LookRotation(dir3.normalized, Vector3.up);
        Vector3 mid = new Vector3((a.x + b.x) * 0.5f, (ya + yb) * 0.5f, (a.y + b.y) * 0.5f);
        float blen = dir3.magnitude * len;
        AddRotBoxCol(parent, nm + "Deck", mid - (q * Vector3.up) * 0.3f, new Vector3(w + 0.2f, 0.6f, blen + 0.2f), q, ws);
        if (!rails) return;
        for (int s = -1; s <= 1; s += 2)
            AddRotBoxCol(parent, nm + "Rail" + (s < 0 ? "L" : "R"), mid + (q * Vector3.right) * (s * (w * 0.5f - 0.05f)) + (q * Vector3.up) * 0.65f,
                new Vector3(0.2f, 1.3f, blen), q, ws);
    }

    static void AddRotBoxCol(Transform parent, string name, Vector3 centerU, Vector3 sizeU, Quaternion rot, float ws)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = centerU * ws;
        g.transform.localRotation = rot;
        g.AddComponent<BoxCollider>().size = sizeU * ws;
    }
}
#endif

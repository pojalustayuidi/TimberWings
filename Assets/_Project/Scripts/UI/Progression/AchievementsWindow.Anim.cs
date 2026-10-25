// Assets/_Project/Scripts/UI/Progression/AchievementsWindow.Anim.cs (НЕ в Editor)
// ЧАСТЬ класса AchievementsWindow (partial): камера, фон с параллаксом, анимация узлов, огоньки, карточка выбранного.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: AchievementsWindow.cs.
// Остальные части: AchievementsWindow.TabsData.cs, AchievementsWindow.Build.cs, AchievementsWindow.HudHelpers.cs.
// Методы этой части: TickWheel, TickFocus, FocusOn, TickBackground, ApplyTint, TickNodes, TickFlow, PlaceFlow, PointAt, TickDetail.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public partial class AchievementsWindow
{
    // ------------------------------------------------------------ колесо: если дерево шире окна и не выше, листаем вбок
    void TickWheel()
    {
        float w = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(w, 0f)) return;
        if (contentW > VpW + 1f && contentH <= VpH + 1f)
        {
            focusing = false;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition - w * 0.12f);
        }
    }

    // ------------------------------------------------------------ плавный «наезд» камеры на узел
    void TickFocus(float dt)
    {
        if (!focusing) return;
        if (Input.GetMouseButton(0) || Input.touchCount > 0) { focusing = false; return; }   // игрок взял дерево в руки
        scroll.StopMovement();
        Vector2 cur = contentsRt.anchoredPosition;
        Vector2 next = Vector2.Lerp(cur, focusTarget, 1f - Mathf.Exp(-9f * dt));
        if ((next - focusTarget).sqrMagnitude < 0.25f) { next = focusTarget; focusing = false; }
        contentsRt.anchoredPosition = next;
    }

    void FocusOn(Node n)
    {
        if (n == null) return;
        float maxX = Mathf.Max(0f, contentW - VpW), maxY = Mathf.Max(0f, contentH - VpH);
        float x = -Mathf.Clamp(n.pos.x - VpW * 0.5f, 0f, maxX);
        float y = Mathf.Clamp(n.pos.y - (VpH - DetH - 30f) * 0.5f, 0f, maxY);   // центр над карточкой
        focusTarget = new Vector2(x, y);
        focusing = true;
    }

    // ------------------------------------------------------------ фон: слои с параллаксом, реакция на мышь, дыхание свечения
    void TickBackground()
    {
        float dt = Time.unscaledDeltaTime;
        tintCur = Color.Lerp(tintCur, tintTarget, 1f - Mathf.Exp(-9f * dt));
        glowKick = Mathf.MoveTowards(glowKick, 0f, dt / 0.7f);
        ApplyTint();

        // мышь: фон чуть «отъезжает» от курсора, как у стекла с глубиной
        Vector2 target = Vector2.zero, loc;
        var vpRt = (RectTransform)patRaw.rectTransform.parent;
        if (!Application.isMobilePlatform && RectTransformUtility.ScreenPointToLocalPointInRectangle(vpRt, Input.mousePosition, null, out loc))
            target = new Vector2(Mathf.Clamp(loc.x / (VpW * 0.5f), -1f, 1f), Mathf.Clamp(loc.y / (VpH * 0.5f), -1f, 1f));
        mouseSm = Vector2.Lerp(mouseSm, target, 1f - Mathf.Exp(-5f * dt));

        Vector2 cam = contentsRt.anchoredPosition;
        Vector2 far = cam * 0.16f - mouseSm * 8f;       // дальний слой: медленный
        Vector2 near = cam * 0.42f - mouseSm * 18f;     // ближний слой: быстрее
        patFar.uvRect = new Rect(-far.x / PatFarTile + 0.37f, -far.y / PatFarTile + 0.21f, VpW / PatFarTile, VpH / PatFarTile);
        patRaw.uvRect = new Rect(-near.x / PatTile, -near.y / PatTile, VpW / PatTile, VpH / PatTile);

        // большое мягкое свечение цвета вкладки: плывёт и дышит, на смене вкладки вспыхивает
        Vector2 gp = new Vector2(VpW * 0.10f, -VpH * 0.04f) + cam * 0.28f - mouseSm * 26f;
        glowRt.anchoredPosition = gp;
        float breathe = 0.5f + 0.5f * Mathf.Sin(time * 0.8f);
        bgGlow.color = new Color(tintCur.r, tintCur.g, tintCur.b, 0.14f + 0.07f * breathe + 0.26f * glowKick);
        glowRt.localScale = Vector3.one * (1f + 0.04f * breathe + 0.12f * glowKick);

        motes.cam = cam;
        motes.mouse = mouseSm;
    }

    void ApplyTint()
    {
        Color t = tintCur;
        baseImg.color = Color.Lerp(UiKit.Ink, t, 0.16f);
        Color pc = Color.Lerp(Color.white, t, 0.6f);
        patRaw.color = pc;
        pc.a = 0.55f;
        patFar.color = pc;
        bgTitle.color = new Color(t.r, t.g, t.b, 0.30f);
        bgSub.color = new Color(t.r, t.g, t.b, 0.80f);
        motes.SetColor(t);
    }

    // ------------------------------------------------------------ анимация узлов
    void TickNodes(float dt)
    {
        tabT += dt;
        var list = byCat[(int)cat];
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            Color tc = TierColor(n.def.tier);

            // появление: узлы выпрыгивают по очереди слева направо
            float ap = Ph(tabT, n.delay, n.delay + 0.38f);
            float appear = ap <= 0f ? 0f : UiDraw.EaseOutBack(ap);

            n.hoverK = Mathf.MoveTowards(n.hoverK, (n == hov && n.vis) ? 1f : 0f, dt / 0.12f);
            n.selK = Mathf.MoveTowards(n.selK, n == sel ? 1f : 0f, dt / 0.15f);
            n.rt.localScale = Vector3.one * (appear * (1f + 0.10f * n.hoverK + 0.05f * n.selK));

            // лёгкое парение: у готовых к выдаче сильнее, у закрытых почти нет
            float bob = Mathf.Sin(time * 1.35f + n.phase) * (n.can ? 4.5f : (n.vis ? 2.2f : 0.8f));
            n.rt.anchoredPosition = new Vector2(n.pos.x, -n.pos.y + bob * appear);

            // выбранный: яркое кольцо
            n.sel.color = new Color(1f, 0.96f, 0.82f, n.selK);

            // готово к выдаче: свечение пульсирует, значок покачивается
            float haloA = 0f;
            if (n.can)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f + n.phase);
                haloA = Mathf.Lerp(0.35f, 0.95f, pulse);
                float wob = Mathf.Sin(time * 2.2f + n.phase);
                float kick = Mathf.Clamp01(wob * 3f - 1.2f);             // короткое покачивание раз в такт
                n.icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(time * 14f + n.phase) * 9f * kick);
                n.badge.rectTransform.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(time * 6f + n.phase));
            }
            else
            {
                haloA = n.hoverK * 0.35f + n.selK * 0.35f;
                n.icon.rectTransform.localEulerAngles = Vector3.zero;
            }
            n.halo.color = new Color(tc.r, tc.g, tc.b, haloA);
            n.halo.gameObject.SetActive(haloA > 0.01f);

            bool rays = n.can && n.def.tier == AchTier.Gold;
            n.rays.gameObject.SetActive(rays);
            if (rays)
            {
                n.rays.color = new Color(tc.r, tc.g, tc.b, 0.6f);
                n.rays.rectTransform.Rotate(0f, 0f, -22f * dt);
            }
        }
    }

    // ------------------------------------------------------------ огоньки бегут по открытым веткам: яркие по пройденным, бледные к следующей цели
    void TickFlow()
    {
        var edges = edgesByCat[(int)cat];
        Color tint = tintCur;
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            bool on = e.rank >= 1 && e.len > 1f && tabT > 0.45f;
            e.flowA.gameObject.SetActive(on);
            e.flowB.gameObject.SetActive(on && e.rank == 2);
            if (!on) continue;
            bool full = e.rank == 2;
            Color c = full ? Color.Lerp(UiKit.BrassHi, Color.white, 0.35f) : Color.Lerp(tint, Color.white, 0.4f);
            float baseA = full ? 0.95f : 0.45f;
            float sz = full ? 30f : 22f;
            float d = Mathf.Repeat(time * 150f + e.phase * e.len, e.len);
            PlaceFlow(e.flowA, e, d, c, baseA, sz);
            if (full) PlaceFlow(e.flowB, e, Mathf.Repeat(d + e.len * 0.5f, e.len), c, baseA, sz);
        }
    }

    static void PlaceFlow(Image img, Edge e, float d, Color c, float a, float size)
    {
        Vector2 p = PointAt(e, d);
        float k = Mathf.Sin(Mathf.Clamp01(d / e.len) * Mathf.PI);
        c.a = a * Mathf.Sqrt(k);
        img.color = c;
        img.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
        img.rectTransform.sizeDelta = new Vector2(size, size);
    }

    static Vector2 PointAt(Edge e, float d)
    {
        float l0 = (e.p1 - e.p0).magnitude, l1 = (e.p2 - e.p1).magnitude, l2 = (e.p3 - e.p2).magnitude;
        if (d <= l0) return l0 > 0f ? Vector2.Lerp(e.p0, e.p1, d / l0) : e.p0;
        d -= l0;
        if (d <= l1) return l1 > 0f ? Vector2.Lerp(e.p1, e.p2, d / l1) : e.p1;
        d -= l1;
        return l2 > 0f ? Vector2.Lerp(e.p2, e.p3, Mathf.Min(1f, d / l2)) : e.p3;
    }

    // ------------------------------------------------------------ карточка выбранного: выезжает снизу
    void TickDetail(float dt)
    {
        detK = Mathf.MoveTowards(detK, sel != null ? 1f : 0f, dt / 0.22f);
        detPunch = Mathf.MoveTowards(detPunch, 0f, dt / 0.25f);
        float y = Mathf.LerpUnclamped(-(DetH + 40f), 16f, UiDraw.EaseOutBack(detK));
        detRt.anchoredPosition = new Vector2(0f, y);
        detRt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(detPunch * Mathf.PI));
        detRt.gameObject.SetActive(detK > 0.001f);
    }
}

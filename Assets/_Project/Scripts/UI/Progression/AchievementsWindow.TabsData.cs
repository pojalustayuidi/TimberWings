// Assets/_Project/Scripts/UI/Progression/AchievementsWindow.TabsData.cs (НЕ в Editor)
// ЧАСТЬ класса AchievementsWindow (partial): вкладки, данные -> окно, действия.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: AchievementsWindow.cs.
// Остальные части: AchievementsWindow.Anim.cs, AchievementsWindow.Build.cs, AchievementsWindow.HudHelpers.cs.
// Методы этой части: StepCat, SetCat, ApplyCat, PinDefault, Select, Refresh, Skin, FillDetail, ProgText, Hours, OnNodeClick, OnNodeEnter, OnNodeExit, OnClaim, OnClaimAll, ClaimFx.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;
using static AchUi;

public partial class AchievementsWindow
{
    // ============================================================ вкладки
    void StepCat(int dir)
    {
        int n = AchCats.Count;
        SetCat(((int)cat + dir + n) % n);
    }

    void SetCat(int ci)
    {
        if ((int)cat == ci) return;
        cat = (AchCat)ci;
        ApplyCat();
        SfxManager.Play("ui", 0.3f, 1.1f);
    }

    // показать выбранную вкладку: раскладка, камера, выбор «следующей цели»
    void ApplyCat()
    {
        int ci = (int)cat;
        for (int i = 0; i < catRoots.Length; i++) catRoots[i].gameObject.SetActive(i == ci);
        LayoutCat(ci);

        tintTarget = AchCats.Colors[ci];
        if (!tintInit) { tintInit = true; tintCur = tintTarget; }
        patRaw.texture = Pattern(cat);
        patFar.texture = Pattern(cat);
        Set(bgTitle, AchCats.Names[ci].ToUpperInvariant());
        Set(bgSub, AchCats.Blurbs[ci]);
        motes.SetStyle(ci);
        glowKick = 1f;
        ApplyTint();

        contentsRt.anchoredPosition = Vector2.zero;
        scroll.StopMovement();
        tabT = 0f;
        hov = null;
        focusing = false;
        Refresh();
        PinDefault();
    }

    // ближайшая цель: сначала то, что можно забрать, потом самое «горячее» по прогрессу
    void PinDefault()
    {
        var list = byCat[(int)cat];
        Node best = null;
        float bestK = -1f;
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (!n.vis) continue;
            if (n.can) { best = n; break; }
            if (n.unl) continue;
            var m = AchievementManager.Instance;
            float k = (m != null && n.def.goal > 0) ? Mathf.Clamp01((float)m.Progress(n.def) / n.def.goal) : 0f;
            if (k > bestK) { bestK = k; best = n; }
        }
        if (best == null && list.Count > 0) best = list[0];
        Select(best, true, false);
    }

    void Select(Node n, bool focus, bool sound)
    {
        sel = n;
        dirty = true;
        if (n == null) return;
        if (focus) FocusOn(n);
        if (sound) SfxManager.Play("ui", 0.3f, n.vis ? 1.25f : 0.8f);
        FillDetail(n);
    }

    // ============================================================ данные -> окно
    void Refresh()
    {
        var m = AchievementManager.Instance;
        if (m == null) return;

        Set(countText, m.UnlockedCount + " / " + m.Total);

        // узлы
        for (int i = 0; i < nodes.Count; i++) Skin(nodes[i], m);

        // линии: ярко, если открыты оба конца; мягко, если открыт только родитель; тускло, если путь ещё закрыт
        for (int c = 0; c < edgesByCat.Length; c++)
        {
            Color tint = AchCats.Colors[c];
            var edges = edgesByCat[c];
            bool resort = false;
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                Color fill;
                int rank;
                if (e.a.unl && e.b.unl) { fill = Color.Lerp(UiKit.BrassHi, tint, 0.35f); rank = 2; }
                else if (e.a.unl) { fill = Color.Lerp(new Color(0.42f, 0.48f, 0.60f, 1f), tint, 0.45f); rank = 1; }
                else { fill = new Color(0.17f, 0.21f, 0.30f, 1f); rank = 0; }
                if (e.rank != rank) { e.rank = rank; resort = true; }
                for (int k = 0; k < 3; k++)
                {
                    e.line[k].color = new Color(AchUi.Outline.r, AchUi.Outline.g, AchUi.Outline.b, 0.9f);
                    e.line[3 + k].color = fill;
                }
            }
            // общий «ствол» нескольких веток: яркая линия всегда сверху тусклой
            if (resort)
                for (int r = 0; r <= 2; r++)
                    for (int i = 0; i < edges.Count; i++)
                        if (edges[i].rank == r)
                            for (int k = 3; k < 6; k++) edges[i].line[k].transform.SetAsLastSibling();
        }

        // вкладки: счётчик готовых наград красной точкой
        for (int c = 0; c < tabs.Length; c++)
        {
            int unl = 0, claim = 0, total = byCat[c].Count;
            for (int i = 0; i < byCat[c].Count; i++) { if (byCat[c][i].unl) unl++; if (byCat[c][i].can) claim++; }
            var tb = tabs[c];
            Set(tb.label, AchCats.Names[c] + "  " + unl + "/" + total);
            tb.img.color = c == (int)cat ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            tb.btn.transform.localScale = Vector3.one * (c == (int)cat ? 1.04f : 1f);
            tb.dot.gameObject.SetActive(claim > 0);
            if (claim > 0) Set(tb.dotLabel, claim > 9 ? "9+" : claim.ToString());
        }

        int claimable = m.ClaimableCount;
        claimAllBtn.interactable = claimable > 0;
        Set(claimAllLabel, claimable > 0 ? "Забрать все (" + claimable + ")" : "Забрать все");
        Set(hintText, "K или Esc: закрыть  ·  тяни дерево мышью  ·  стрелки: вкладки");

        if (sel != null) FillDetail(sel);
    }

    // внешний вид одного узла по состоянию
    void Skin(Node n, AchievementManager m)
    {
        var d = n.def;
        n.unl = m.IsUnlocked(d.id);
        n.vis = n.unl || n.parentNode == null || m.IsUnlocked(n.parentNode.def.id);   // выполненное видно всегда, даже если родитель ещё нет
        n.clm = m.IsClaimed(d.id);
        n.can = m.CanClaim(d.id);
        Color tc = TierColor(d.tier);

        n.lockImg.gameObject.SetActive(!n.vis);
        n.icon.gameObject.SetActive(n.vis);
        n.badge.gameObject.SetActive(n.vis && n.can);
        n.check.gameObject.SetActive(n.vis && n.clm);

        if (!n.vis)
        {
            n.medal.color = new Color(0.17f, 0.20f, 0.28f, 1f);
            n.ring.gameObject.SetActive(false);
            n.ringTrack.gameObject.SetActive(false);
            Set(n.label, "???");
            n.label.color = Dim;
            return;
        }

        Set(n.label, d.title);
        n.label.color = n.unl ? Color.white : new Color(0.82f, 0.86f, 0.92f, 1f);

        if (n.unl)
        {
            n.medal.color = n.clm ? Color.Lerp(tc, new Color(0.25f, 0.25f, 0.3f, 1f), 0.3f) : tc;
            n.icon.color = n.clm ? new Color(1f, 1f, 1f, 0.9f) : Color.white;
            n.ring.gameObject.SetActive(false);
            n.ringTrack.gameObject.SetActive(false);
        }
        else
        {
            n.medal.color = new Color(tc.r * 0.40f, tc.g * 0.40f, tc.b * 0.40f, 1f);
            n.icon.color = new Color(1f, 1f, 1f, 0.55f);
            float k = d.goal > 0 ? Mathf.Clamp01((float)m.Progress(d) / d.goal) : 0f;
            n.ringTrack.gameObject.SetActive(true);
            n.ring.gameObject.SetActive(k > 0.002f);
            n.ring.fillAmount = k;
            n.ring.color = tc;
        }
    }

    // карточка выбранного достижения
    void FillDetail(Node n)
    {
        var m = AchievementManager.Instance;
        if (m == null || n == null) return;
        var d = n.def;
        Color tc = TierColor(d.tier);
        dAccent.color = n.vis ? tc : new Color(tc.r * 0.45f, tc.g * 0.45f, tc.b * 0.45f, 1f);

        if (!n.vis)
        {
            dMedal.color = new Color(0.17f, 0.20f, 0.28f, 1f);
            dIcon.gameObject.SetActive(false);
            dLock.gameObject.SetActive(true);
            Set(dTitle, "???");
            string pt = n.parentNode != null ? n.parentNode.def.title : "";
            Set(dDesc, "Откроется после достижения «" + pt + "»");
            dBar.gameObject.SetActive(false);
            Set(dProg, TierName(d.tier));
            dProg.color = tc;
            dCoinIcon.gameObject.SetActive(false);
            dCoins.gameObject.SetActive(false);
            dXp.gameObject.SetActive(false);
            dClaim.gameObject.SetActive(false);
            dCheck.gameObject.SetActive(false);
            dDone.gameObject.SetActive(false);
            return;
        }

        dMedal.color = n.unl ? tc : new Color(tc.r * 0.45f, tc.g * 0.45f, tc.b * 0.45f, 1f);
        dIcon.gameObject.SetActive(true);
        dIcon.sprite = UiKit.Icon(d.icon);
        dIcon.color = n.unl ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        dLock.gameObject.SetActive(false);
        Set(dTitle, d.title);
        Set(dDesc, d.desc);

        int prog = Mathf.Min(m.Progress(d), d.goal);
        dBar.gameObject.SetActive(true);
        dBarFill.fillAmount = d.goal > 0 ? Mathf.Clamp01((float)prog / d.goal) : 0f;
        Set(dProg, ProgText(d, prog));
        dProg.color = Soft;

        dCoinIcon.gameObject.SetActive(d.coins > 0);
        dCoins.gameObject.SetActive(d.coins > 0);
        Set(dCoins, "+" + d.coins);
        dXp.gameObject.SetActive(d.xp > 0);
        Set(dXp, "+" + d.xp + " XP");

        dClaim.gameObject.SetActive(n.can);
        dCheck.gameObject.SetActive(n.clm);
        dDone.gameObject.SetActive(n.clm);
    }

    static string ProgText(AchievementDef d, int prog)
    {
        if (d.metric == AchMetric.PlaySeconds) return Hours(prog) + " / " + Hours(d.goal);
        if (d.metric == AchMetric.Chapter) return prog >= d.goal ? "Готово" : "Не пройдено";
        return prog + " / " + d.goal;
    }

    static string Hours(int sec)
    {
        if (sec >= 3600 * 10) return (sec / 3600) + " ч";
        if (sec >= 3600) return (sec / 3600f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " ч";
        return (sec / 60) + " мин";
    }

    // ============================================================ действия
    void OnNodeClick(Node n)
    {
        if (sel == n) { SfxManager.Play("ui", 0.2f, 1.0f); detPunch = 1f; return; }
        Select(n, false, true);
        detPunch = 1f;
    }

    void OnNodeEnter(Node n)
    {
        hov = n;
        if (n.vis) SfxManager.Play("ui", 0.10f, 1.5f);
    }

    void OnNodeExit(Node n) { if (hov == n) hov = null; }

    void OnClaim()
    {
        var m = AchievementManager.Instance;
        if (m == null || sel == null || !sel.can) return;
        var n = sel;
        if (m.Claim(n.def.id))
        {
            ClaimFx(n);
            SfxManager.Play("hint", 0.6f, 1.5f);
            detPunch = 1f;
        }
    }

    void OnClaimAll()
    {
        var m = AchievementManager.Instance;
        if (m == null) return;
        var list = byCat[(int)cat];
        var fx = new List<Node>();
        int coins = 0;
        for (int i = 0; i < list.Count; i++) if (list[i].can) { fx.Add(list[i]); coins += list[i].def.coins; }
        if (m.ClaimAll() > 0)
        {
            for (int i = 0; i < fx.Count; i++) ClaimFx(fx[i]);
            if (coins > 0 && fx.Count > 1) sparks.FloatText(new Vector2(contentW * 0.5f, -contentH * 0.2f), "+" + coins, UiKit.BrassHi, 54f);
            SfxManager.Play("hint", 0.7f, 1.6f);
            detPunch = 1f;
        }
    }

    // искры и «+30» над узлом
    void ClaimFx(Node n)
    {
        Color tc = TierColor(n.def.tier);
        Vector2 p = new Vector2(n.pos.x, -n.pos.y);
        int count = n.def.tier == AchTier.Gold ? 34 : (n.def.tier == AchTier.Silver ? 24 : 16);
        sparks.Burst(p, tc, count, 360f, 26f, 420f, 0.95f);
        if (n.def.coins > 0) sparks.FloatText(p + new Vector2(0f, n.size * 0.55f), "+" + n.def.coins, UiKit.BrassHi);
        if (n.def.xp > 0) sparks.FloatText(p + new Vector2(0f, n.size * 0.55f - 34f), "+" + n.def.xp + " XP", UiKit.Good, 26f);
    }
}

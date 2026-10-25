// Assets/_Project/Scripts/UI/Workers/WorkersWindow.PackOpen.cs (НЕ в Editor)
// ЧАСТЬ класса WorkersWindow (partial): экран открытия пака и окно шансов (логика и анимация).
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: WorkersWindow.cs.
// Остальные части: WorkersWindow.Tabs.cs, WorkersWindow.Build.cs.
// CANDY: тег «НОВЫЙ/ДУБЛИКАТ» и тексты в цветах Candy, у редкости тёмный цвет текста, конфетти CandyFx при показе карточки.
// Методы этой части: StartOpen, OnTableClosed, FillResult, GuaranteeName, RefreshResultButtons, OpenAgain, CloseResult, SkipAnim, TickOpening, AnimFrame, FinishAnim, Burst, SpawnSpark, TickSparks, OpenOdds, CloseOdds, RefreshOdds.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class WorkersWindow
{
    // ============================================================ открытие пака
    // Кнопка «Открыть»: раздаём три карты рубашкой вверх. Монеты спишутся, только когда игрок выберет карту (WorkerPackTable)
    void StartOpen(WorkerPackType type)
    {
        var m = WorkerManager.Instance;
        if (m == null || phase != Phase.None || table == null) return;
        var why = table.Begin(type, m);
        if (why != PackOpenResult.Ok) { Say(WhyText(why, type, m.PackPrice(type))); Refresh(); return; }
        phase = Phase.Table;
        Refresh();
    }

    void OnTableClosed()
    {
        phase = Phase.None;
        panel.anchoredPosition = Vector2.zero;
        dirty = true;
    }

    static readonly Vector2 CardPos = new Vector2(-300f, 30f);

    void FillResult(WorkerManager m)
    {
        var d = res.def;
        var st = res.worker;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        resCard.Set(d, st, false);

        rTag.gameObject.SetActive(true);
        if (res.isNew) { rTag.color = WorkerUi.Bad; rTagText.text = "НОВЫЙ!"; }
        else { rTag.color = WorkerUi.DupTag; rTagText.text = "ДУБЛИКАТ"; }
        if (res.wasFree) rTagText.text += "  ·  ПОДАРОК";

        rName.text = d.FullName + ", " + d.age;
        rGem.color = acc;
        rRarity.text = WorkerText.Rarity(res.rarity);
        rRarity.color = WorkerUi.RarityText(res.rarity);
        for (int i = 0; i < 5; i++) rStars[i].gameObject.SetActive(i <= (int)res.rarity);
        rRoleIcon.sprite = UiKit.CandyIcon(WorkerUi.RoleIcon(d.role));
        rRole.text = WorkerText.Role(d.role) + " · " + WorkerText.Output(d.role);
        rLevel.text = "Уровень " + st.level + (st.IsMax ? " (максимум)" : "");

        var sb = new System.Text.StringBuilder();
        if (res.isNew)
        {
            int slot = m.Sim.SlotOf(st);
            if (slot >= 0) sb.Append("Встал в слот ").Append(slot + 1).Append(" и уже работает.");
            else if (m.Sim.CountInSlots(d.role) >= WorkerBalance.MaxPerRole) sb.Append("В запасе: в лагере уже ").Append(WorkerBalance.MaxPerRole).Append(" рабочих этой роли.");
            else sb.Append("В запасе: все слоты заняты.");
        }
        else if (res.coinsGained > 0) sb.Append("Уже на максимальном уровне: вместо опыта +").Append(res.coinsGained).Append(" монет.");
        else
        {
            sb.Append("Уже в лагере: +").Append(res.xpGained).Append(" XP.");
            if (res.levelsGained > 0) sb.Append(" Новый уровень: ").Append(st.level).Append("!");
        }
        if (res.GuaranteeTriggered) sb.Append("\nСработала гарантия: ").Append(GuaranteeName(res.guarantee)).Append(".");
        rLine.text = sb.ToString();

        var pk = new System.Text.StringBuilder();
        for (int i = 0; i < d.perks.Count; i++) { if (i > 0) pk.Append("  ·  "); pk.Append(WorkerText.Perk(d.perks[i])); }
        if (d.HasSignature) pk.Append("  ·  Фирменный");
        rPerks.text = pk.Length > 0 ? "Перки: " + pk : "Перков нет";
        rBio.text = string.IsNullOrEmpty(d.bio) ? "" : d.bio;
        RefreshResultButtons(m);
    }

    static string GuaranteeName(PackGuarantee g)
    {
        switch (g)
        {
            case PackGuarantee.Mythic: return "Мифический";
            case PackGuarantee.Legendary: return "Легендарный или выше";
            default: return "Редкий или выше";
        }
    }

    void RefreshResultButtons(WorkerManager m)
    {
        PackOpenResult why;
        bool can = m.CanOpenPack(resType, out why);
        int price = m.PackPrice(resType);
        SetBtn(againBtn, can && phase == Phase.Result, "Ещё: " + (price == 0 ? "бесплатно" : price + " монет"));
        SetBtn(doneBtn, phase == Phase.Result, "Готово");
    }

    void OpenAgain()
    {
        if (phase != Phase.Result) return;
        var m = WorkerManager.Instance;
        PackOpenResult why;
        if (m == null || !m.CanOpenPack(resType, out why)) return;
        phase = Phase.None;
        StartOpen(resType);
    }

    void CloseResult()
    {
        phase = Phase.None;
        openRoot.SetActive(false);
        panel.anchoredPosition = Vector2.zero;
        dirty = true;
    }

    void SkipAnim()
    {
        if (phase != Phase.Anim) return;
        animT = AnimDur;
        FinishAnim();
    }

    void TickOpening(float dt)
    {
        if (phase == Phase.None) { return; }
        if (phase == Phase.Table) { table.Tick(dt); return; }
        if (phase == Phase.Anim)
        {
            animT += dt;
            if (animT >= AnimDur) FinishAnim(); else AnimFrame(animT);
        }
        else
        {
            if (raysRt.gameObject.activeSelf) raysRt.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 18f);
        }
        TickSparks(dt);
    }

    // 0–1.8 тряска и искры → 1.8–2.05 вспышка → 2.05–2.75 карточка выезжает → 2.8–3.3 появляется текст → 3.4 конец
    void AnimFrame(float t)
    {
        const float charge = 1.8f, flashEnd = 2.05f, revealEnd = 2.75f;
        Color pc = WorkerUi.PackColor(resType);
        Color acc = WorkerUi.RarityAccent(res.rarity);

        if (t < charge)
        {
            float k = t / charge;
            artRt.anchoredPosition = CardPos + new Vector2(Mathf.Sin(t * 55f) * (2f + 11f * k), Mathf.Sin(t * 41f) * 3f * k);
            artRt.localScale = Vector3.one * (1f + 0.2f * k);
            glowRt.localScale = Vector3.one * (0.7f + 0.6f * k);
            openGlow.color = new Color(pc.r, pc.g, pc.b, 0.15f + 0.45f * k);
            if (Random.value < 0.35f + 0.5f * k)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector2 p = CardPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(90f, 150f);
                Vector2 v = (CardPos - p).normalized * Random.Range(60f, 120f);
                SpawnSpark(p, v, Color.Lerp(pc, Color.white, 0.4f), Random.Range(0.5f, 0.9f), Random.Range(10f, 20f));
            }
            return;
        }

        if (t < flashEnd)
        {
            float k = (t - charge) / (flashEnd - charge);
            flash.color = new Color(acc.r, acc.g, acc.b, k);
            artRt.localScale = Vector3.one * (1.2f + 0.3f * k);
            return;
        }

        // показ карточки
        if (artRt.gameObject.activeSelf) { openArt.gameObject.SetActive(false); }
        if (!burstDone) Burst(true);
        float fk = Mathf.Clamp01((t - flashEnd) / 0.45f);
        flash.color = new Color(acc.r, acc.g, acc.b, 1f - fk);

        if (!resCard.rt.gameObject.activeSelf) resCard.rt.gameObject.SetActive(true);
        float rk = Mathf.Clamp01((t - flashEnd) / (revealEnd - flashEnd));
        float s = Mathf.Lerp(0.55f, 1f, UiDraw.EaseOutBack(rk));
        float flip = Mathf.Clamp01(rk / 0.6f);                         // переворот: ширина от 0 до полной
        resCard.rt.localScale = new Vector3(s * UiDraw.EaseOutCubic(flip), s, 1f);

        if (res.rarity >= WorkerRarity.Uncommon)
        {
            raysRt.gameObject.SetActive(true);
            raysRt.localRotation = Quaternion.Euler(0f, 0f, -t * 18f);
            openRays.color = new Color(acc.r, acc.g, acc.b, 0.55f * rk);
        }
        openGlow.color = new Color(acc.r, acc.g, acc.b, 0.35f * rk);

        float ik = Mathf.Clamp01((t - 2.8f) / 0.5f);
        infoCg.alpha = ik;
        if (ik > 0f) waitText.gameObject.SetActive(false);
    }

    // карточка в конечном состоянии (анимация закончилась сама или её пропустили)
    void FinishAnim()
    {
        phase = Phase.Result;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        openArt.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        resCard.rt.gameObject.SetActive(true);
        resCard.rt.localScale = Vector3.one;
        openGlow.color = new Color(acc.r, acc.g, acc.b, 0.35f);
        glowRt.localScale = Vector3.one;
        bool rays = res.rarity >= WorkerRarity.Uncommon;
        raysRt.gameObject.SetActive(rays);
        openRays.color = new Color(acc.r, acc.g, acc.b, 0.55f);
        waitText.gameObject.SetActive(false);
        infoCg.alpha = 1f;
        infoCg.interactable = true; infoCg.blocksRaycasts = true;
        openHint.text = "E или Esc: закрыть   ·   Enter: открыть ещё";
        if (!burstDone) Burst(false);
        var m = WorkerManager.Instance;
        if (m != null) RefreshResultButtons(m);
    }

    // взрыв искр в момент показа карточки; тряска окна у Редкого и выше (если анимацию не пропустили)
    void Burst(bool withShake)
    {
        burstDone = true;
        Color acc = WorkerUi.RarityAccent(res.rarity);
        int n = 10 + 6 * (int)res.rarity;
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f, sp = Random.Range(120f, 380f);
            SpawnSpark(CardPos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, Color.Lerp(acc, Color.white, Random.value * 0.5f), Random.Range(0.6f, 1.1f), Random.Range(12f, 26f));
        }
        CandyFx.Confetti(UiCandy.Kit, resCard.rt, 14 + 8 * (int)res.rarity);   // конфетти (нет CandyFxKit: просто без него)
        if (withShake && res.rarity >= WorkerRarity.Rare) shakeT = 0.45f;
        if (SfxManager.Instance != null) SfxManager.Play("ui", 0.8f, 0.7f + 0.15f * (int)res.rarity);
    }

    void SpawnSpark(Vector2 p, Vector2 v, Color c, float life, float size)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s.live) continue;
            s.live = true; s.p = p; s.v = v; s.life = s.max = life; s.size = size;
            s.img.color = c;
            s.img.gameObject.SetActive(true);
            s.rt.anchoredPosition = p;
            s.rt.sizeDelta = new Vector2(size, size);
            return;
        }
    }

    void TickSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (!s.live) continue;
            s.life -= dt;
            if (s.life <= 0f) { s.live = false; s.img.gameObject.SetActive(false); continue; }
            s.p += s.v * dt;
            s.v *= 1f - Mathf.Min(1f, 1.6f * dt);
            s.rt.anchoredPosition = s.p;
            var c = s.img.color; c.a = Mathf.Clamp01(s.life / s.max);
            s.img.color = c;
        }
    }

    // ============================================================ окно шансов
    void OpenOdds(WorkerPackType t)
    {
        oddsType = t;
        oddsOpen = true;
        oddsGo.SetActive(true);
        var m = WorkerManager.Instance;
        if (m != null) RefreshOdds(m);
    }

    void CloseOdds() { oddsOpen = false; oddsGo.SetActive(false); }

    void RefreshOdds(WorkerManager m)
    {
        var t = oddsType;
        var packs = m.Packs;
        oTitle.text = "Шансы: " + WorkerText.Pack(t);
        oTitle.color = WorkerUi.PackText(t);
        var odds = WorkerBalance.OddsOf(t);
        for (int r = 0; r < 5; r++)
        {
            bool zero = odds[r] <= 0f;
            oGem[r].color = zero ? new Color(1f, 1f, 1f, 0.25f) : WorkerUi.RarityAccent((WorkerRarity)r);
            oName[r].color = zero ? Dim : Ink;
            oPct[r].text = WorkerUi.OddsText(odds[r]);
            oPct[r].color = zero ? Dim : Ink;
            WorkerUi.SetBar(oFill[r], zero ? 0f : Mathf.Max(0.02f, odds[r] / 100f));
        }

        oGuarLine[0].text = "Редкий или выше: каждые " + WorkerBalance.GuaranteeRareEvery + " паков (" + UntilText(packs.OpensUntilRare(t)) + ")";
        bool leg = WorkerBalance.HasLegendaryGuarantee(t);
        oGuarLine[1].text = leg ? "Легендарный или выше: каждые " + WorkerBalance.GuaranteeLegendaryEvery + " паков (" + UntilText(packs.OpensUntilLegendary(t)) + ")"
                                : "Легендарный или выше: гарантии нет";
        oGuarLine[1].color = leg ? Soft : Dim;
        bool myth = WorkerBalance.HasMythicGuarantee(t);
        oGuarLine[2].text = myth ? "Мифический: каждые " + WorkerBalance.GuaranteeMythicEvery + " паков (до мифика: " + packs.MythicProgress(t) + "/" + WorkerBalance.GuaranteeMythicEvery + ")"
                                 : "Мифический: гарантии нет";
        oGuarLine[2].color = myth ? Soft : Dim;
        oMythFill.parent.gameObject.SetActive(myth);
        if (myth) WorkerUi.SetBar(oMythFill, packs.MythicProgress(t) / (float)WorkerBalance.GuaranteeMythicEvery);
    }
}

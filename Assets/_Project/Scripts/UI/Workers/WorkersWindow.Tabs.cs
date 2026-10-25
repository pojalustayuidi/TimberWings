// Assets/_Project/Scripts/UI/Workers/WorkersWindow.Tabs.cs (НЕ в Editor)
// ЧАСТЬ класса WorkersWindow (partial): вкладки «Бригада», «Паки», «Коллекция» и склад лагеря.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: WorkersWindow.cs.
// Остальные части: WorkersWindow.PackOpen.cs, WorkersWindow.Build.cs.
// CANDY: иконки во время игры берём через UiKit.CandyIcon (не зависят от скина окна), текст на светлом поле индиго, «хорошо» мятно-зелёное, «нельзя» золотисто-коричневое.
// Методы этой части: RefreshBrigade, MakeReserveCard, RefreshDetail, ToggleSlot, ClickSlot, AssignText, Say, RefreshStock, DoTake, DoSell, Tick, RefreshPacks, UntilText, WhyText, RefreshCollection.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class WorkersWindow
{
    // ------------------------------------------------------------ вкладка «Бригада»
    void RefreshBrigade(WorkerManager m)
    {
        slotsLabel.text = "Слоты " + m.WorkingCount + "/" + m.Slots + "     доход Приказчиков " + WorkerUi.Inv(m.TotalIncome, "0.00") + " монет/с";

        // выбранный рабочий: если пропал, берём первого в слоте, иначе первого из ростера
        if (m.Find(selId) == null)
        {
            selId = null;
            for (int i = 0; i < m.Slots && selId == null; i++) { var w = m.SlotAt(i); if (w != null) selId = w.id; }
            if (selId == null && m.Roster.Count > 0) selId = m.Roster[0].id;
        }

        for (int i = 0; i < m.Slots; i++)
        {
            var w = m.SlotAt(i);
            var card = slotCards[i];
            card.rt.gameObject.SetActive(w != null);
            slotEmpty[i].gameObject.SetActive(w == null);
            if (w != null) card.Set(w.def, w, false, w.id == selId);
            else
            {
                var sel = m.Find(selId);
                bool canPlace = sel != null && m.Sim.SlotOf(sel) < 0;
                slotEmptyText[i].text = canPlace ? "Свободный слот\nнажми, чтобы поставить" : "Свободный слот";
            }
        }

        // запас: рабочие, которых нет в слотах
        reserveIds.Clear();
        for (int i = 0; i < m.Roster.Count; i++) if (m.Sim.SlotOf(m.Roster[i]) < 0) reserveIds.Add(m.Roster[i].id);
        reserveLabel.text = "Запас (" + reserveIds.Count + ")" + (reserveIds.Count == 0 ? "   все рабочие стоят в слотах" : "");
        while (reserveCards.Count < reserveIds.Count) reserveCards.Add(MakeReserveCard(reserveCards.Count));
        for (int i = 0; i < reserveCards.Count; i++)
        {
            bool on = i < reserveIds.Count;
            reserveCards[i].rt.gameObject.SetActive(on);
            if (!on) continue;
            var w = m.Find(reserveIds[i]);
            if (w == null) continue;
            reserveCards[i].Set(w.def, w, false, w.id == selId);
            TL(reserveCards[i].rt, i * 120f, 4f, 112f, 150f);
        }
        reserveContent.sizeDelta = new Vector2(Mathf.Max(800f, reserveIds.Count * 120f), 156f);
        // Wheel navigation for large reserves (100+ workers), while keeping the compact horizontal strip.
        var reserveWheel = reserveContent.GetComponent<WorkerReserveWheel>();
        if (reserveWheel == null) reserveWheel = reserveContent.gameObject.AddComponent<WorkerReserveWheel>();
        reserveWheel.Target = reserveContent;

        RefreshDetail(m);
        RefreshStock(m);
    }

    WorkerCardView MakeReserveCard(int index)
    {
        var c = WorkerCardView.Create(reserveContent, 112f, 150f, "Reserve" + index);
        c.showRole = false;
        int idx = index;
        AddClick(c.frame, () => { if (idx < reserveIds.Count) { selId = reserveIds[idx]; dirty = true; } });
        return c;
    }

    void RefreshDetail(WorkerManager m)
    {
        var w = m.Find(selId);
        detailBody.SetActive(w != null);
        detailEmpty.SetActive(w == null);
        if (w == null) return;

        var d = w.def;
        detailCard.Set(d, w, false);
        dName.text = w.DisplayName;
        dAge.text = d != null ? "Возраст " + d.age : "";
        dGem.color = WorkerUi.RarityAccent(w.rarity);
        dRarity.text = WorkerText.Rarity(w.rarity);
        dRarity.color = WorkerUi.RarityText(w.rarity);
        for (int i = 0; i < 5; i++) dStars[i].gameObject.SetActive(i <= (int)w.rarity);
        dRoleIcon.sprite = UiKit.CandyIcon(WorkerUi.RoleIcon(w.role));
        dRole.text = WorkerText.Role(w.role);

        if (w.IsMax)
        {
            dLevel.text = "Уровень " + w.level + " (максимум, «Мастер» ×" + WorkerUi.Inv(WorkerBalance.MasterBonus, "0.0") + ")";
            dXpFill.fillAmount = 1f;
            dXp.text = "МАКС";
        }
        else
        {
            double into = w.totalXp - WorkerBalance.TotalXpForLevel(w.level);
            int need = WorkerBalance.XpToNext(w.level);
            dLevel.text = "Уровень " + w.level + " из " + WorkerBalance.MaxLevel;
            dXpFill.fillAmount = Mathf.Clamp01((float)(into / Mathf.Max(1, need)));
            dXp.text = (int)into + " / " + need + " XP";
        }

        var s = m.Sim;
        string prod;
        switch (w.role)
        {
            case WorkerRole.Clerk: prod = WorkerUi.Inv(s.ClerkRate(w), "0.00") + " монет/с"; break;
            case WorkerRole.Lumberjack: prod = WorkerUi.Inv(s.LumberRate(w), "0.00") + " бревен/с"; break;
            default: prod = "1 доска за " + WorkerUi.Inv(s.SawTime(w), "0.0") + " с"; break;
        }
        dProd.text = "Выработка: <b>" + prod + "</b>   ×" + WorkerUi.Inv(WorkerBalance.RarityMultiplier(w.rarity), "0.0#") + " редкость, ×" + WorkerUi.Inv(WorkerBalance.LevelMult(w.level), "0.00") + " уровень";

        int slot = s.SlotOf(w);
        string status; bool working = false;
        if (slot < 0) status = "В запасе: не работает";
        else if (w.role == WorkerRole.Lumberjack && m.Logs >= m.LogCap) status = "Стоит: склад бревен полон";
        else if (w.role == WorkerRole.Sawyer && m.Planks >= m.PlankCap) status = "Стоит: склад досок полон";
        else if (w.role == WorkerRole.Sawyer && !w.sawActive && m.Logs < WorkerBalance.SawyerLogsPerPlank) status = "Стоит: на складе нет бревен";
        else { status = "Работает в слоте " + (slot + 1) + ", копит опыт"; working = true; }
        dStatus.text = status;
        dStatusIcon.sprite = UiKit.CandyIcon(working ? UiIcon.Check : UiIcon.Clock);
        dStatus.color = working ? Good : Warn;

        // четыре перка роли: открытые и закрытые (закрытый = замок и серый текст, не только цвет)
        var perks = WorkerText.PerksOf(w.role);
        for (int i = 0; i < 4; i++)
        {
            bool has = w.Has(perks[i]);
            perkIcon[i].sprite = UiKit.CandyIcon(has ? UiIcon.Check : UiIcon.Lock);
            perkIcon[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            perkName[i].text = WorkerText.Perk(perks[i]);
            perkName[i].color = has ? Ink : Dim;
            perkDesc[i].text = WorkerText.PerkDesc(perks[i]);
            perkDesc[i].color = has ? Good : Dim;
        }
        bool sig = w.rarity == WorkerRarity.Mythic;
        perkIcon[4].gameObject.SetActive(sig);
        perkName[4].gameObject.SetActive(sig);
        perkDesc[4].gameObject.SetActive(sig);
        if (sig)
        {
            perkIcon[4].sprite = UiKit.CandyIcon(UiIcon.Star);
            perkName[4].text = "Фирменный";
            perkName[4].color = WorkerUi.RarityText(WorkerRarity.Mythic);
            perkDesc[4].text = WorkerText.Signature(w.role);
            perkDesc[4].color = Soft;
        }

        bool inSlot = slot >= 0;
        SetBtn(dAction, true, inSlot ? "Убрать из слота" : "Поставить в лагерь");
        dAction.b.onClick.RemoveAllListeners();
        string id = w.id;
        dAction.b.onClick.AddListener(() => ToggleSlot(id));
    }

    void ToggleSlot(string id)
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var w = m.Find(id);
        if (w == null) return;
        int slot = m.Sim.SlotOf(w);
        if (slot >= 0) { m.Unassign(slot); Say("Убрано в запас."); }
        else Say(AssignText(m.AutoAssign(id), "Поставлен в лагерь."));
        dirty = true;
    }

    void ClickSlot(int i)
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var w = m.SlotAt(i);
        if (w != null) { selId = w.id; dirty = true; return; }
        var sel = m.Find(selId);
        if (sel == null || m.Sim.SlotOf(sel) >= 0) return;
        Say(AssignText(m.Assign(selId, i), "Поставлен в слот " + (i + 1) + "."));
        dirty = true;
    }

    static string AssignText(AssignResult r, string ok)
    {
        switch (r)
        {
            case AssignResult.Ok: return ok;
            case AssignResult.RoleFull: return "Не больше " + WorkerBalance.MaxPerRole + " рабочих одной роли.";
            case AssignResult.NoFreeSlot: return "Все слоты заняты: убери кого-нибудь.";
            default: return "Не получилось.";
        }
    }

    void Say(string s) { message = s; messageUntil = Time.unscaledTime + 3.5f; }

    // ------------------------------------------------------------ склад лагеря
    void RefreshStock(WorkerManager m)
    {
        logFill.fillAmount = m.LogCap > 0 ? Mathf.Clamp01(m.Logs / (float)m.LogCap) : 0f;
        plankFill.fillAmount = m.PlankCap > 0 ? Mathf.Clamp01(m.Planks / (float)m.PlankCap) : 0f;
        logText.text = m.Logs + "/" + m.LogCap + (m.Logs >= m.LogCap ? " полон" : "");
        plankText.text = m.Planks + "/" + m.PlankCap + (m.Planks >= m.PlankCap ? " полон" : "");
        logText.color = m.Logs >= m.LogCap ? Warn : Ink;
        plankText.color = m.Planks >= m.PlankCap ? Warn : Ink;

        var take = m.PreviewTake();
        var sell = m.PreviewSell();
        string pre = "";
        if (take.Any) pre = "Заберёшь: бревна " + take.logs + ", доски " + take.planks;
        else if (m.Logs > 0) pre = "В рюкзаке нет места для бревен";
        else pre = "На складе пусто";
        if (sell.Any) pre += "   ·   продашь: +" + sell.coins + " монет";
        stockPreview.text = pre;
        SetBtn(takeBtn, take.Any, null);
        SetBtn(sellBtn, sell.Any, null);

        bool showMsg = message.Length > 0 && Time.unscaledTime < messageUntil;
        stockMsg.text = showMsg ? message : "";
    }

    void DoTake()
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var r = m.TakeAll();
        if (r.Any)
        {
            string s = "Забрано: бревна " + r.logs + ", доски " + r.planks;
            if (m.Logs > 0) s += " (рюкзак полон)";
            Say(s);
            Tick();
        }
        else Say(m.Logs > 0 ? "В рюкзаке нет места." : "На складе пусто.");
        dirty = true;
    }

    void DoSell()
    {
        var m = WorkerManager.Instance;
        if (m == null) return;
        var r = m.SellAll();
        Say(r.Any ? "Продано за " + r.coins + " монет" : "Продавать нечего.");
        if (r.Any) Tick();
        dirty = true;
    }

    static void Tick() { if (SfxManager.Instance != null) SfxManager.Play("ui"); }

    // ------------------------------------------------------------ вкладка «Паки»
    void RefreshPacks(WorkerManager m)
    {
        for (int t = 0; t < 3; t++)
        {
            var type = (WorkerPackType)t;
            var u = packUi[t];
            var packs = m.Packs;
            u.hl.color = t == packSel ? UiCandy.PeachDeep : new Color(1f, 1f, 1f, 0f);

            bool free = type == WorkerPackType.Artel && packs.FreePending;
            int price = m.PackPrice(type);
            bool unlocked = m.IsPackUnlocked(type);
            u.ribbon.gameObject.SetActive(free);

            if (!unlocked)
            {
                u.priceIcon.sprite = UiKit.CandyIcon(UiIcon.Lock);
                u.priceText.text = "С " + WorkerBalance.PackLevel(type) + " уровня (сейчас " + PlayerStats.Level + ")";
                u.priceText.color = Warn;
            }
            else
            {
                u.priceIcon.sprite = UiKit.CandyIcon(UiIcon.Coin);
                u.priceText.text = free ? "Бесплатно!" : price.ToString();
                u.priceText.color = free ? Good : Ink;
            }

            // шансы видны всегда, до покупки
            var odds = WorkerBalance.OddsOf(type);
            for (int r = 0; r < 5; r++)
            {
                bool zero = odds[r] <= 0f;
                u.oddName[r].color = zero ? Dim : Ink;
                u.oddPct[r].text = WorkerUi.OddsText(odds[r]);
                u.oddPct[r].color = zero ? Dim : Ink;
                u.oddIcon[r].color = zero ? new Color(1f, 1f, 1f, 0.25f) : WorkerUi.RarityAccent((WorkerRarity)r);
                WorkerUi.SetBar(u.oddFill[r], odds[r] <= 0f ? 0f : Mathf.Max(0.03f, odds[r] / 100f));
            }

            // счётчики гарантий
            u.guar[0].text = "Редкий или выше: " + UntilText(packs.OpensUntilRare(type));
            bool leg = WorkerBalance.HasLegendaryGuarantee(type);
            u.guar[1].gameObject.SetActive(leg);
            if (leg) u.guar[1].text = "Легендарный или выше: " + UntilText(packs.OpensUntilLegendary(type));
            bool myth = WorkerBalance.HasMythicGuarantee(type);
            u.guar[2].gameObject.SetActive(myth);
            u.mythBack.gameObject.SetActive(myth);
            if (myth)
            {
                u.guar[2].text = "до мифика: " + packs.MythicProgress(type) + "/" + WorkerBalance.GuaranteeMythicEvery;
                WorkerUi.SetBar(u.mythFill, packs.MythicProgress(type) / (float)WorkerBalance.GuaranteeMythicEvery);
            }

            PackOpenResult why;
            bool can = m.CanOpenPack(type, out why);
            u.reason.text = can ? "" : WhyText(why, type, price);
            SetBtn(u.open, can, free ? "Открыть бесплатно" : "Открыть");
        }
    }

    static string UntilText(int n)
    {
        if (n <= 1) return "гарантия в этом паке";
        return "через " + n + " " + UiDraw.Plural(n, "пак", "пака", "паков");
    }

    static string WhyText(PackOpenResult why, WorkerPackType t, int price)
    {
        switch (why)
        {
            case PackOpenResult.Locked: return "Откроется на " + WorkerBalance.PackLevel(t) + " уровне";
            case PackOpenResult.NoCoins: return "Не хватает монет: нужно " + price;
            case PackOpenResult.NoDatabase: return "Не назначена база рабочих";
            default: return "Нет игрока";
        }
    }

    // ------------------------------------------------------------ вкладка «Коллекция»
    void RefreshCollection(WorkerManager m)
    {
        if (!collBuilt) BuildCollection(m);
        if (!collBuilt) return;

        int have = 0;
        var perRoleHave = new int[3]; var perRoleAll = new int[3];
        for (int i = 0; i < collDefs.Count; i++)
        {
            var d = collDefs[i];
            var st = m.Find(d.id);
            bool owned = st != null;
            if (owned) { have++; perRoleHave[(int)d.role]++; }
            perRoleAll[(int)d.role]++;
            collCards[i].Set(d, st, !owned, d == selDef);
        }
        collCount.text = "Собрано " + have + "/" + collDefs.Count;
        for (int r = 0; r < 3; r++) collRoleCount[r].text = perRoleHave[r] + "/" + perRoleAll[r];

        if (selDef == null && collDefs.Count > 0) selDef = collDefs[0];
        cdBody.SetActive(selDef != null);
        if (selDef == null) return;

        var sd = selDef;
        var ss = m.Find(sd.id);
        bool own = ss != null;
        cdCard.Set(sd, ss, !own);
        Color acc = WorkerUi.RarityAccent(sd.rarity);
        cdGem.color = acc;
        for (int i = 0; i < 5; i++) cdStars[i].gameObject.SetActive(i <= (int)sd.rarity);
        cdRarity.text = WorkerText.Rarity(sd.rarity) + " · " + WorkerText.Role(sd.role);
        cdRarity.color = WorkerUi.RarityText(sd.rarity);

        if (own)
        {
            cdName.text = sd.FullName;
            cdLine.text = "Возраст " + sd.age;
            int slot = m.Sim.SlotOf(ss);
            cdState.text = "Уровень " + ss.level + (ss.IsMax ? " (максимум)" : "") + "  ·  " + (slot >= 0 ? "работает в слоте " + (slot + 1) : "в запасе");
            cdState.color = Ink;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sd.perks.Count; i++)
                sb.Append("<b>").Append(WorkerText.Perk(sd.perks[i])).Append("</b>: ").Append(WorkerText.PerkDesc(sd.perks[i])).Append('\n');
            if (sd.HasSignature) sb.Append("<b>Фирменный</b>: ").Append(WorkerText.Signature(sd.role));
            if (sb.Length == 0) sb.Append("Перков нет, зато надёжный работник.");
            cdPerks.text = sb.ToString();
            cdBio.text = string.IsNullOrEmpty(sd.bio) ? "" : sd.bio;
        }
        else
        {
            cdName.text = "???";
            cdLine.text = "Ещё не найден";
            cdState.text = "Выпадает из: " + WorkerUi.PacksWith(sd.rarity);
            cdState.color = Warn;
            cdPerks.text = sd.perks.Count > 0 ? "Перков: " + sd.perks.Count + (sd.HasSignature ? " и фирменный" : "") : "Перков нет";
            cdBio.text = "Открой пак, чтобы узнать этого человека.";
        }
    }
}

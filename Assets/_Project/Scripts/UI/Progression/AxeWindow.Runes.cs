using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

internal sealed partial class AxeWindow
{
    static readonly AxeId[] RuneAxes = { AxeId.Iron, AxeId.Steel, AxeId.Gunnar };
    GameObject runeView, rollView, chosenView, transferView;
    AxeId runeAxe, transferSource;
    ForgeTier forgeTier;
    bool transferring;
    readonly Button[] runeAxes = new Button[3], tiers = new Button[3], sources = new Button[3];
    readonly TMP_Text[] sourceLabels = new TMP_Text[3];
    Button rollTab, transferTab, pay, transfer;
    TMP_Text currentRune, currentEffect, runeWallet, oddsLabel, quoteLabel, unlockLabel, paidLabel, transferText;
    TMP_Text keepLabel;
    RuneCard[] offered;

    sealed class RuneCard
    {
        internal GameObject root;
        internal TMP_Text name, rarity, effect;
        internal Button choose;
    }

    static string ForgeText(string ru, string en) => GameLanguage.English ? en : ru;

    void ShowRunes()
    {
        if (table == null || !table.CanUse(equipment)) return;
        if (runeView == null)
            using (UiKit.UseSkin(UiSkin.Candy)) BuildRuneView();

        runeAxe = equipment.PendingRuneOffer?.axe ?? selected;
        if (!AxeRunes.Supports(runeAxe) || !equipment.Has(runeAxe))
            runeAxe = equipment.Has(AxeId.Iron) ? AxeId.Iron :
                equipment.Has(AxeId.Steel) ? AxeId.Steel : equipment.Has(AxeId.Gunnar) ? AxeId.Gunnar : AxeId.Iron;
        transferring = false;
        forgeTier = ForgeTier.Simple;
        transferSource = AxeId.Old;
        dragging = false;
        axeView.SetActive(false);
        previewStage.SetActive(false);
        runeView.SetActive(true);
        Refresh();
    }

    void ShowAxes()
    {
        runeView.SetActive(false);
        axeView.SetActive(true);
        previewStage.SetActive(true);
        Select(runeAxe);
    }

    void BuildRuneView()
    {
        var window = UiCandy.Window(panel.transform, new Vector2(1090, 646), ForgeText("РУННАЯ КУЗНИЦА", "RUNE FORGE"), Close);
        Prepare(window.close);
        runeView = window.root.gameObject;
        var content = window.content;

        for (int i = 0; i < RuneAxes.Length; i++)
        {
            AxeId axe = RuneAxes[i];
            runeAxes[i] = MakeButton(content, ShortAxe(axe), UiBtn.Gray, 10 + i * 245, 8, 234, 55, () =>
            {
                runeAxe = axe;
                transferSource = AxeId.Old;
                Refresh();
            });
        }
        MakeButton(content, ForgeText("К топорам", "Axes"), UiBtn.Gray, 755, 8, 278, 55, ShowAxes);

        RectTransform installed = RunicSurface(content, "Installed rune", UiTone.Lavender, 10, 75, 1023, 93);
        currentRune = ForgeLabel(installed, 25, 17, 9, 285, 73, true);
        currentEffect = ForgeLabel(installed, 23, 325, 9, 678, 73);
        rollTab = MakeButton(content, ForgeText("Подбор", "Draw"), UiBtn.Yellow, 10, 177, 508, 48, () =>
        {
            transferring = false;
            Refresh();
        });
        transferTab = MakeButton(content, ForgeText("Перенос", "Transfer"), UiBtn.Gray, 525, 177, 508, 48, () =>
        {
            transferring = true;
            Refresh();
        });

        var roll = Layer(content, "Draw tier");
        rollView = roll.gameObject;
        for (int i = 0; i < tiers.Length; i++)
        {
            var tier = (ForgeTier)i;
            tiers[i] = MakeButton(roll, TierName(tier), UiBtn.Gray, 10 + i * 343, 239, 333, 54, () =>
            {
                forgeTier = tier;
                Refresh();
            });
        }
        oddsLabel = ForgeLabel(roll, 21, 12, 305, 1020, 52, true);
        unlockLabel = ForgeLabel(roll, 21, 12, 362, 1020, 41);
        pay = MakeButton(roll, ForgeText("Оплатить подбор", "Pay for draw"), UiBtn.Green, 10, 424, 373, 56, () =>
        {
            equipment.TryRollRune(table, runeAxe, forgeTier);
            Refresh();
        });
        quoteLabel = ForgeLabel(roll, 22, 400, 411, 628, 81);

        var pending = Layer(content, "Paid draw");
        chosenView = pending.gameObject;
        paidLabel = ForgeLabel(pending, 23, 12, 178, 1015, 51, true);
        offered = new[] { MakeRuneCard(pending, 0), MakeRuneCard(pending, 1) };
        var keep = MakeButton(pending, "", UiBtn.Gray, 10, 443, 496, 54, () =>
        {
            equipment.TryChooseRune(table, equipment.PendingRuneOffer, AxeRuneId.None);
            Refresh();
        });
        keepLabel = keep.GetComponentInChildren<TMP_Text>();

        var moving = Layer(content, "Transfer rune");
        transferView = moving.gameObject;
        for (int i = 0; i < RuneAxes.Length; i++)
        {
            AxeId source = RuneAxes[i];
            sources[i] = MakeButton(moving, "", UiBtn.Gray, 10 + i * 343, 239, 333, 54, () =>
            {
                transferSource = source;
                Refresh();
            });
            sourceLabels[i] = sources[i].GetComponentInChildren<TMP_Text>();
        }
        transferText = ForgeLabel(moving, 25, 13, 306, 1005, 105);
        transfer = MakeButton(moving, ForgeText("Перенести бесплатно", "Transfer free"), UiBtn.Green, 10, 424, 373, 56, () =>
        {
            equipment.TryTransferRune(table, transferSource, runeAxe);
            transferSource = AxeId.Old;
            Refresh();
        });

        runeWallet = ForgeLabel(content, 22, 12, 508, 1017, 35, true);
        runeView.SetActive(false);
    }

    RuneCard MakeRuneCard(Transform parent, int index)
    {
        RectTransform card = RunicSurface(parent, "Rune choice", UiTone.Mint, 10 + index * 515, 234, 507, 198);
        var refs = new RuneCard
        {
            root = card.gameObject,
            name = ForgeLabel(card, 28, 17, 11, 472, 34, true),
            rarity = ForgeLabel(card, 19, 17, 46, 472, 28),
            effect = ForgeLabel(card, 22, 17, 78, 472, 55)
        };
        refs.choose = MakeButton(card, ForgeText("Установить", "Apply rune"), UiBtn.Green, 17, 141, 472, 45, () =>
        {
            var offer = equipment.PendingRuneOffer;
            if (offer != null)
            {
                AxeRuneId rune = index == 0 ? offer.first : offer.second;
                if (rune != AxeRuneId.None) equipment.TryChooseRune(table, offer, rune);
            }
            Refresh();
        });
        return refs;
    }

    void RefreshRunes()
    {
        ForgeRuneOffer offer = equipment.PendingRuneOffer;
        bool awaiting = offer != null;
        if (awaiting) runeAxe = offer.axe;
        for (int i = 0; i < RuneAxes.Length; i++)
            runeAxes[i].interactable = !awaiting && equipment.Has(RuneAxes[i]) && runeAxe != RuneAxes[i];

        AxeRune current = AxeRunes.Get(equipment.RuneFor(runeAxe));
        bool hasAxe = equipment.Has(runeAxe);
        Set(currentRune, current.Name);
        Set(currentEffect, hasAxe ? current.Effect : ForgeText(
            "Нужен железный, стальной или топор Гуннара.", "You need an iron, steel or Gunnar axe."));

        PlayerInventory stock = equipment.GetComponent<PlayerInventory>();
        Set(runeWallet, stock == null ? "" : ForgeText(
            $"{stock.coinsCount} монет  ·  {stock.RuneEssence} эссенции  ·  {stock.planksCount} досок",
            $"{stock.coinsCount} coins  ·  {stock.RuneEssence} essence  ·  {stock.planksCount} boards"));

        rollTab.gameObject.SetActive(!awaiting);
        transferTab.gameObject.SetActive(!awaiting);
        rollTab.interactable = transferring;
        transferTab.interactable = !transferring;
        rollView.SetActive(!awaiting && !transferring);
        transferView.SetActive(!awaiting && transferring);
        chosenView.SetActive(awaiting);

        if (awaiting)
        {
            Set(paidLabel, TierName(offer.tier) + ForgeText(" подбор оплачен · ", " draw paid · ") + CostText(offer.tier, offer.coins));
            Set(keepLabel, current.Id == AxeRuneId.None ? ForgeText("Оставить гнездо пустым", "Leave socket empty") :
                ForgeText("Оставить прежнюю", "Keep current rune"));
            ShowRuneCard(offered[0], offer.first);
            ShowRuneCard(offered[1], offer.second);
        }
        else if (transferring) RefreshTransfer();
        else RefreshQuote(stock);
    }

    void RefreshQuote(PlayerInventory stock)
    {
        for (int i = 0; i < tiers.Length; i++) tiers[i].interactable = i != (int)forgeTier;
        bool guaranteed = forgeTier == ForgeTier.Master && equipment.MasterGuaranteed;
        string[] odds = new string[5];
        for (int i = 0; i < odds.Length; i++)
        {
            var rarity = (RuneRarity)i;
            float percentage = AxeRunes.Weight(forgeTier, rarity, guaranteed) / 10f;
            string value = percentage.ToString("0.#", CultureInfo.InvariantCulture);
            odds[i] = RarityName(rarity) + " " + ForgeText(value.Replace('.', ','), value) + "%";
        }
        Set(oddsLabel, string.Join("   ·   ", odds));
        string note = "";
        if (!equipment.TierUnlocked(forgeTier))
            note = forgeTier == ForgeTier.Enhanced ? ForgeText("Откроется после покупки стального топора.", "Unlocks after buying the steel axe.")
                : ForgeText("Откроется после восстановления топора Гуннара.", "Unlocks after restoring Gunnar's axe.");
        else if (forgeTier == ForgeTier.Master)
            note = guaranteed ? ForgeText("Следующий подбор гарантирует мифическую или гуннарную руну.",
                "Your next draw guarantees a mythic or Gunnar rune.") : ForgeText(
                $"Гарантия мастерского подбора · {equipment.MasterMisses}/{AxeRunes.MasterLimit}",
                $"Master guarantee · {equipment.MasterMisses}/{AxeRunes.MasterLimit}");
        Set(unlockLabel, note);
        int coins = PlayerAxeEquipment.RuneCoinCost(forgeTier);
        int essence = AxeRunes.EssenceCost(forgeTier);
        bool affordable = stock != null && stock.coinsCount >= coins && stock.RuneEssence >= essence;
        string after = affordable ? ForgeText("Останется · ", "Remaining · ") + MoneyAndEssence(stock.coinsCount - coins, stock.RuneEssence - essence)
            : ForgeText("Не хватает ресурсов.", "More resources needed.");
        Set(quoteLabel, ForgeText("Цена · ", "Price · ") + CostText(forgeTier, coins) + "\n" + after);
        pay.interactable = equipment.CanRollRune(table, runeAxe, forgeTier);
    }

    void RefreshTransfer()
    {
        for (int i = 0; i < RuneAxes.Length; i++)
        {
            var source = RuneAxes[i];
            var rune = equipment.RuneFor(source);
            Set(sourceLabels[i], ShortAxe(source) + (rune != AxeRuneId.None ? " · " + AxeRunes.Get(rune).Name : ""));
            AutoSize(sourceLabels[i], 14, 25);
            sources[i].interactable = source != transferSource && equipment.CanTransferRune(table, source, runeAxe);
        }
        bool can = equipment.CanTransferRune(table, transferSource, runeAxe);
        if (can)
        {
            var rune = AxeRunes.Get(equipment.RuneFor(transferSource));
            Set(transferText, ShortAxe(transferSource) + " → " + ShortAxe(runeAxe) + "\n" + rune.Name + " · " + rune.Effect);
        }
        else Set(transferText, equipment.RuneFor(runeAxe) != AxeRuneId.None
            ? ForgeText("Выбери сверху топор с пустым гнездом.", "Choose an axe with an empty socket above.")
            : ForgeText("Выбери топор, с которого нужно снять руну.", "Select the axe that holds the rune."));
        transfer.interactable = can;
    }

    static void ShowRuneCard(RuneCard card, AxeRuneId id)
    {
        card.root.SetActive(id != AxeRuneId.None);
        if (id == AxeRuneId.None) return;
        var rune = AxeRunes.Get(id);
        Set(card.name, rune.Name);
        Set(card.rarity, RarityName(rune.Rarity));
        Set(card.effect, rune.Effect);
        card.rarity.color = rune.Rarity == RuneRarity.Gunnar ? (Color)new Color32(120, 65, 28, 255)
            : rune.Rarity >= RuneRarity.Legendary ? (Color)new Color32(88, 59, 130, 255) : UiCandy.Muted;
    }

    static RectTransform Layer(Transform parent, string name)
    {
        var rect = NewRT(name, parent);
        Stretch(rect);
        return rect;
    }

    static RectTransform RunicSurface(Transform parent, string name, UiTone tone, float x, float y, float w, float h)
    {
        var surface = UiKit.MakeImage(parent, name, UiKit.Slot, true);
        surface.color = UiCandy.TintOf(tone);
        surface.raycastTarget = false;
        TL(surface.rectTransform, x, y, w, h);
        return surface.rectTransform;
    }

    static TMP_Text ForgeLabel(Transform parent, float font, float x, float y, float w, float h, bool bold = false)
    {
        var label = Lbl(parent, "", font, UiCandy.Ink, TextAlignmentOptions.MidlineLeft, bold, x, y, w, h);
        AutoSize(label, 16, font);
        label.enableWordWrapping = true;
        return label;
    }

    static string ShortAxe(AxeId axe) => axe == AxeId.Iron ? ForgeText("Железный", "Iron") :
        axe == AxeId.Steel ? ForgeText("Стальной", "Steel") : ForgeText("Гуннара", "Gunnar");

    static string TierName(ForgeTier tier) => tier == ForgeTier.Simple ? ForgeText("Простой", "Simple") :
        tier == ForgeTier.Enhanced ? ForgeText("Усиленный", "Enhanced") : ForgeText("Мастерский", "Master");

    static string RarityName(RuneRarity rarity)
    {
        switch (rarity)
        {
            case RuneRarity.Rare: return ForgeText("Редкая", "Rare");
            case RuneRarity.Legendary: return ForgeText("Легендарная", "Legendary");
            case RuneRarity.Mythic: return ForgeText("Мифическая", "Mythic");
            case RuneRarity.Gunnar: return ForgeText("Гуннарная", "Gunnar");
            default: return ForgeText("Обычная", "Common");
        }
    }

    static string CostText(ForgeTier tier, int coins) => MoneyAndEssence(coins, AxeRunes.EssenceCost(tier));

    static string MoneyAndEssence(int coins, int essence) =>
        ForgeText($"{essence} эссенции  ·  {coins} монет", $"{essence} essence  ·  {coins} coins");
}

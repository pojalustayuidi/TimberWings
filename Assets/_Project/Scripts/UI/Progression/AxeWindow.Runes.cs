using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

internal sealed partial class AxeWindow
{
    const float ForgeWidth = 1280f, ForgeHeight = 748f;
    const float LeftWidth = 760f, RightX = 784f, RightWidth = 436f;
    const float HammerGap = .17f;
    static readonly AxeId[] RuneAxes = { AxeId.Iron, AxeId.Steel, AxeId.Gunnar };
    static readonly Color Selection = new Color32(255, 218, 133, 255);
    static readonly Color SelectedTab = new Color32(219, 192, 253, 255);

    enum ForgeMotion { None, Draw, Apply }
    ForgeMotion motion;
    float motionAt;
    int playedHits;
    bool catalogOpen, runeViewEnglish;
    AxeId runeAxe;
    ForgeTier forgeTier;
    ForgeRuneOffer inspectedOffer;
    AxeRuneId pickedRune, appliedRune, replacedRune;

    RectTransform runeHost;
    GameObject runeView, drawView, resultView, catalogView, motionView, receiptView, forgeAxeView;
    RawImage forgePreview;
    Button drawTab, catalogTab, previousAxe, nextAxe, pay, apply;
    TMP_Text coinWallet, essenceWallet, axeHeading, axeStatus, slotName, slotEffect, guidance, oddsTitle, quote, remainder;
    TMP_Text resultRarity, replaceHint, keepLabel, receiptName, receiptEffect, receiptChange, motionTitle, motionHint;
    Image socketStamp, receiptStamp, motionStamp, motionGlow;
    readonly Image[] sparks = new Image[5];
    readonly Image[] tierFrames = new Image[3], tierCards = new Image[3];
    readonly TMP_Text[] tierEssence = new TMP_Text[3], tierCoins = new TMP_Text[3], tierLabels = new TMP_Text[3], oddsValues = new TMP_Text[5];
    RuneOption[] options;

    sealed class RuneOption
    {
        internal Image face, border, stamp;
        internal TMP_Text name, effect;
    }

    static string ForgeText(string ru, string en) => GameLanguage.English ? en : ru;

    void ShowRunes()
    {
        if (table == null || !table.CanUse(equipment)) return;
        if (runeView == null || runeViewEnglish != GameLanguage.English)
        {
            if (runeHost != null)
            {
                runeHost.gameObject.SetActive(false);
                Destroy(runeHost.gameObject);
            }
            using (UiKit.UseSkin(UiSkin.Candy)) BuildRuneView();
        }
        ResetForgePresentation();
        var paid = equipment.PendingRuneOffer;
        runeAxe = paid?.axe ?? selected;
        if (!AxeRunes.Supports(runeAxe) || !equipment.Has(runeAxe))
        {
            runeAxe = equipment.Equipped.Id;
            if (!AxeRunes.Supports(runeAxe))
                foreach (AxeId axe in RuneAxes)
                    if (equipment.Has(axe)) { runeAxe = axe; break; }
        }
        catalogOpen = false;
        forgeTier = paid?.tier ?? ForgeTier.Simple;
        dragging = false;
        axeView.SetActive(false);
        FitRuneWindow();
        runeView.SetActive(true);
        Refresh();
    }

    void ShowAxes()
    {
        ResetForgePresentation();
        runeView.SetActive(false);
        axeView.SetActive(true);
        previewStage.SetActive(true);
        Select(ForgeOpeningAxe());
    }

    AxeId ForgeOpeningAxe()
    {
        if (forge != null && forge.GunnarBladeAvailable && !equipment.Has(AxeId.Gunnar)) return AxeId.Gunnar;
        return equipment.Has(AxeId.Steel) && AxeRunes.Supports(runeAxe) && equipment.Has(runeAxe) ? runeAxe : AxeId.Steel;
    }

    void ResetForgePresentation()
    {
        motion = ForgeMotion.None;
        inspectedOffer = null;
        pickedRune = appliedRune = replacedRune = AxeRuneId.None;
    }

    void FitRuneWindow()
    {
        var size = ((RectTransform)panel.transform).rect.size;
        float scale = Mathf.Min(1f, Mathf.Min((size.x - 34f) / ForgeWidth, (size.y - 38f) / ForgeHeight));
        if (scale > 0f && !Mathf.Approximately(runeHost.localScale.x, scale))
            runeHost.localScale = Vector3.one * scale;
    }

    void BuildRuneView()
    {
        runeViewEnglish = GameLanguage.English;
        runeHost = Layer(panel.transform, "Rune forge");
        var window = UiCandy.Window(runeHost, new Vector2(ForgeWidth, ForgeHeight), ForgeText("РУННАЯ КУЗНИЦА", "RUNE FORGE"), Close);
        Prepare(window.close);
        runeView = window.root.gameObject;
        var content = window.content;

        drawTab = ForgeTab(content, ForgeText("Зачарование", "Enchant"), UiIcon.Star, 10, 212, () => ShowRuneTab(false));
        catalogTab = ForgeTab(content, ForgeText("Справочник рун", "Rune guide"), UiIcon.Book, 234, 241, () => ShowRuneTab(true));
        ForgeTab(content, ForgeText("Топоры", "Axes"), UiIcon.Axe, 487, 159, ShowAxes);
        coinWallet = ForgeWallet(content, UiIcon.Coin, UiTone.Lemon, 794, 183);
        essenceWallet = ForgeWallet(content, UiIcon.RuneEssence, UiTone.Lavender, 987, 233);

        BuildDraw(content);
        BuildOptions(content);
        BuildCatalog(content);
        BuildMotion(content);
        BuildReceipt(content);
        BuildAxeCard(content);
        runeView.SetActive(false);
    }

    Button ForgeTab(Transform parent, string label, UiIcon icon, float x, float width, UnityEngine.Events.UnityAction action)
    {
        var tab = MakeButton(parent, label, UiBtn.Gray, x, 9, width, 52, action);
        var text = tab.GetComponentInChildren<TMP_Text>();
        text.rectTransform.offsetMin = new Vector2(39, 5);
        text.rectTransform.offsetMax = new Vector2(-7, -1);
        AutoSize(text, 17, 24);
        var glyph = UiKit.MakeIcon(tab.transform, icon, 31);
        TLC(glyph.rectTransform, 24, 24, 31, 31);
        return tab;
    }

    static TMP_Text ForgeWallet(Transform parent, UiIcon icon, UiTone tone, float x, float width)
    {
        var plate = ForgeSurface(parent, "Wallet", tone, x, 11, width, 48).rectTransform;
        var image = UiKit.MakeIcon(plate, icon, 31);
        TLC(image.rectTransform, 26, 22, 31, 31);
        return ForgeLabel(plate, "", 23, 49, 4, width - 60, 38, true);
    }

    void BuildDraw(Transform parent)
    {
        var draw = Layer(parent, "Choose a draw");
        drawView = draw.gameObject;
        ForgeLabel(draw, ForgeText("Подбор рун", "Rune draw"), 32, 12, 78, LeftWidth, 44, true);
        ForgeLabel(draw, ForgeText("После оплаты появятся до двух рун одной редкости.",
            "After payment, choose from up to two runes of the rolled rarity."), 21, 13, 122, LeftWidth, 31);

        for (int i = 0; i < tierCards.Length; i++)
        {
            var tier = (ForgeTier)i;
            float x = 10 + i * 256;
            tierFrames[i] = ForgeSurface(draw, "Tier highlight", UiTone.Lemon, x - 4, 162, 251, 153);
            var card = ForgeSurface(draw, "Draw tier", UiTone.Lavender, x, 166, 243, 145);
            tierCards[i] = card;
            AddClick(card, () => { forgeTier = tier; Refresh(); });
            Prepare(card.GetComponent<Button>());
            var gem = UiKit.MakeIcon(card.transform, UiIcon.Gem, 47);
            gem.color = RarityColor((RuneRarity)i);
            TLC(gem.rectTransform, 32, 32, 45, 45);
            ForgeLabel(card.transform, TierName(tier), 24, 60, 13, 176, 34, true);
            tierLabels[i] = ForgeLabel(card.transform, "", 19, 16, 54, 212, 43);
            var cost = ForgeSurface(card.transform, "Tier price", UiTone.Neutral, 10, 102, 223, 32).rectTransform;
            var crystal = UiKit.MakeIcon(cost, UiIcon.RuneEssence, 24);
            var coin = UiKit.MakeIcon(cost, UiIcon.Coin, 23);
            TLC(crystal.rectTransform, 47, 15, 24, 24);
            TLC(coin.rectTransform, 136, 15, 23, 23);
            tierEssence[i] = ForgeLabel(cost, "", 20, 62, 0, 49, 29, true);
            tierCoins[i] = ForgeLabel(cost, "", 20, 151, 0, 56, 29, true);
        }

        oddsTitle = ForgeLabel(draw, "", 26, 12, 324, LeftWidth, 41, true);
        for (int i = 0; i < oddsValues.Length; i++)
        {
            var rarity = (RuneRarity)i;
            var tile = ForgeSurface(draw, "Rarity odds", RarityTone(rarity), 10 + i * 153, 369, 148, 82).rectTransform;
            var gem = UiKit.MakeIcon(tile, UiIcon.Gem, 20);
            gem.color = RarityColor(rarity);
            TLC(gem.rectTransform, 20, 23, 20, 20);
            var name = ForgeLabel(tile, RarityName(rarity), 18, 34, 5, 109, 31, true);
            name.color = RarityColor(rarity);
            oddsValues[i] = ForgeLabel(tile, "", 28, 8, 34, 132, 42, true, TextAlignmentOptions.Center);
        }
        guidance = ForgeLabel(draw, "", 20, 12, 459, LeftWidth, 54);

        var buy = ForgeSurface(draw, "Draw checkout", UiTone.Lavender, 10, 522, LeftWidth, 121).rectTransform;
        quote = ForgeLabel(buy, "", 20, 16, 6, 281, 42, true);
        remainder = ForgeLabel(buy, "", 19, 312, 6, 427, 42, false, TextAlignmentOptions.MidlineRight);
        pay = MakeButton(buy, ForgeText("Подобрать руны", "Draw runes"), UiBtn.Green, 13, 50, 734, 57, PayForRune);
    }

    void BuildAxeCard(Transform parent)
    {
        var frame = ForgeSurface(parent, "Selected axe", UiTone.Lavender, RightX, 79, RightWidth, 565).rectTransform;
        forgeAxeView = frame.gameObject;
        ForgeLabel(frame, ForgeText("Выбранный топор", "Selected axe"), 28, 17, 11, 402, 38, true, TextAlignmentOptions.Center);
        previousAxe = MakeButton(frame, "‹", UiBtn.Gray, 15, 59, 52, 47, () => CycleAxe(-1));
        nextAxe = MakeButton(frame, "›", UiBtn.Gray, 369, 59, 52, 47, () => CycleAxe(1));
        var namePlate = ForgeSurface(frame, "Axe name", UiTone.Neutral, 74, 59, 288, 47).rectTransform;
        axeHeading = ForgeLabel(namePlate, "", 26, 4, 2, 280, 41, true, TextAlignmentOptions.Center);
        var stand = ForgeSurface(frame, "Axe stand", UiTone.Neutral, 15, 119, 406, 322).rectTransform;
        forgePreview = MakePreviewImage(stand, 23, 3, 358, 314);
        forgePreview.texture = previewTexture;
        axeStatus = ForgeLabel(frame, "", 17, 21, 445, 394, 22, true);
        socketStamp = UiKit.MakeIcon(frame, UiIcon.Gem, 58, "Socket");
        TLC(socketStamp.rectTransform, 51, 507, 58, 58);
        slotName = ForgeLabel(frame, "", 24, 88, 474, 329, 30, true);
        slotEffect = ForgeLabel(frame, "", 19, 89, 505, 326, 48);
    }

    void BuildOptions(Transform parent)
    {
        var result = Layer(parent, "Choose a rune");
        resultView = result.gameObject;
        ForgeLabel(result, ForgeText("Выпавшие руны", "Runes revealed"), 32, 12, 78, LeftWidth, 43, true);
        resultRarity = ForgeLabel(result, "", 21, 13, 123, LeftWidth, 37, true);
        options = new RuneOption[2];
        for (int i = 0; i < options.Length; i++)
        {
            int optionIndex = i;
            float y = 173 + i * 138;
            var border = ForgeSurface(result, "Rune selection", UiTone.Lemon, 6, y - 4, LeftWidth + 8, 135);
            var card = ForgeSurface(result, "Rune option", UiTone.Neutral, 10, y, LeftWidth, 127);
            var stamp = UiKit.MakeIcon(card.transform, UiIcon.Gem, 73);
            TLC(stamp.rectTransform, 53, 58, 73, 73);
            options[i] = new RuneOption
            {
                face = card, border = border, stamp = stamp,
                name = ForgeLabel(card.transform, "", 27, 101, 13, 643, 36, true),
                effect = ForgeLabel(card.transform, "", 23, 101, 54, 640, 57)
            };
            AddClick(card, () =>
            {
                var offer = equipment.PendingRuneOffer;
                if (offer == null || motion != ForgeMotion.None) return;
                pickedRune = optionIndex == 0 ? offer.first : offer.second;
                Refresh();
            });
            Prepare(card.GetComponent<Button>());
        }
        replaceHint = ForgeLabel(result, "", 21, 12, 454, LeftWidth, 62);
        apply = MakeButton(result, ForgeText("Нанести руну", "Apply rune"), UiBtn.Green, 10, 525, LeftWidth, 61, ApplyPickedRune);
        var keep = MakeButton(result, "", UiBtn.Gray, 10, 593, LeftWidth, 49, () => FinishRuneChoice(AxeRuneId.None));
        keepLabel = keep.GetComponentInChildren<TMP_Text>();
        AutoSize(keepLabel, 18, 25);
    }

    void BuildCatalog(Transform parent)
    {
        var catalog = Layer(parent, "Rune guide");
        catalogView = catalog.gameObject;
        int index = 0;
        for (int i = 0; i < oddsValues.Length; i++)
        {
            foreach (AxeRune rune in AxeRunes.AtRarity((RuneRarity)i))
            {
                var card = ForgeSurface(catalog, "Rune", RarityTone(rune.Rarity), 10 + index % 2 * 610, 80 + index / 2 * 112, 600, 103).rectTransform;
                var stamp = UiKit.MakeIcon(card, UiIcon.Gem, 56);
                stamp.sprite = UiKit.RuneStamp(rune.Id);
                TLC(stamp.rectTransform, 40, 49, 56, 56);
                ForgeLabel(card, rune.Name, 24, 77, 8, 342, 29, true);
                var rarity = ForgeLabel(card, RarityName(rune.Rarity), 18, 407, 9, 175, 24, true, TextAlignmentOptions.MidlineRight);
                rarity.color = RarityColor(rune.Rarity);
                ForgeLabel(card, rune.Effect, 21, 78, 41, 502, 54);
                index++;
            }
        }
    }

    void BuildMotion(Transform parent)
    {
        var plate = ForgeSurface(parent, "Stamping", UiTone.Lavender, 10, 79, LeftWidth, 565).rectTransform;
        motionView = plate.gameObject;
        motionTitle = ForgeLabel(plate, "", 34, 30, 27, 700, 57, true, TextAlignmentOptions.Center);
        motionGlow = UiKit.MakeIcon(plate, UiIcon.Gem, 207);
        TLC(motionGlow.rectTransform, 380, 244, 207, 207);
        motionStamp = UiKit.MakeIcon(plate, UiIcon.RuneEssence, 152);
        TLC(motionStamp.rectTransform, 380, 244, 152, 152);
        for (int i = 0; i < sparks.Length; i++) sparks[i] = UiKit.MakeIcon(plate, UiIcon.Star, 22);
        motionHint = ForgeLabel(plate, "", 23, 38, 367, 684, 65, false, TextAlignmentOptions.Center);
        MakeButton(plate, ForgeText("Показать результат", "Show result"), UiBtn.Gray, 171, 470, 418, 58, EndForgeMotion);
    }

    void BuildReceipt(Transform parent)
    {
        var plate = ForgeSurface(parent, "Engraved rune", UiTone.Lavender, 10, 79, LeftWidth, 565).rectTransform;
        receiptView = plate.gameObject;
        ForgeLabel(plate, ForgeText("Руна нанесена", "Rune applied"), 35, 19, 20, 722, 53, true, TextAlignmentOptions.Center);
        receiptStamp = UiKit.MakeIcon(plate, UiIcon.Gem, 134);
        TLC(receiptStamp.rectTransform, 380, 156, 134, 134);
        receiptName = ForgeLabel(plate, "", 33, 24, 229, 712, 45, true, TextAlignmentOptions.Center);
        receiptEffect = ForgeLabel(plate, "", 25, 53, 281, 654, 98, false, TextAlignmentOptions.Center);
        receiptChange = ForgeLabel(plate, "", 20, 24, 389, 712, 51, false, TextAlignmentOptions.Center);
        MakeButton(plate, ForgeText("Новый подбор", "New draw"), UiBtn.Green, 111, 466, 538, 61, () =>
        {
            appliedRune = replacedRune = AxeRuneId.None;
            Refresh();
        });
    }

    void ShowRuneTab(bool catalog)
    {
        if (catalogOpen == catalog) return;
        catalogOpen = catalog;
        dragging = false;
        motion = ForgeMotion.None;
        Refresh();
    }

    void CycleAxe(int direction)
    {
        if (equipment.PendingRuneOffer != null || motion != ForgeMotion.None || appliedRune != AxeRuneId.None) return;
        int currentIndex = System.Array.IndexOf(RuneAxes, runeAxe);
        for (int step = 1; step <= RuneAxes.Length; step++)
        {
            int index = (currentIndex + RuneAxes.Length + direction * step + RuneAxes.Length) % RuneAxes.Length;
            if (!equipment.Has(RuneAxes[index])) continue;
            runeAxe = RuneAxes[index];
            Refresh();
            return;
        }
    }

    void RefreshRunes()
    {
        var offer = equipment.PendingRuneOffer;
        if (offer != inspectedOffer)
        {
            inspectedOffer = offer;
            pickedRune = offer?.first ?? AxeRuneId.None;
        }
        if (offer != null) runeAxe = offer.axe;

        bool show = !catalogOpen;
        bool animating = motion != ForgeMotion.None;
        drawView.SetActive(show && !animating && offer == null && appliedRune == AxeRuneId.None);
        resultView.SetActive(show && !animating && offer != null);
        receiptView.SetActive(show && !animating && appliedRune != AxeRuneId.None);
        motionView.SetActive(show && animating);
        catalogView.SetActive(catalogOpen);
        forgeAxeView.SetActive(show);
        previewStage.SetActive(show);
        drawTab.targetGraphic.color = show ? SelectedTab : Color.white;
        catalogTab.targetGraphic.color = show ? Color.white : SelectedTab;

        var stock = equipment.GetComponent<PlayerInventory>();
        if (stock != null)
        {
            Set(coinWallet, ForgeText("Монеты  ", "Coins  ") + stock.coinsCount);
            Set(essenceWallet, ForgeText("Эссенция  ", "Essence  ") + stock.RuneEssence);
            essenceWallet.transform.parent.gameObject.SetActive(stock.RuneEssenceDiscovered);
        }
        if (!show) return;
        RefreshAxeCard(offer);
        if (animating) return;
        if (offer != null) RefreshOptions(offer);
        else if (appliedRune != AxeRuneId.None) RefreshReceipt();
        else RefreshDraw(stock);
    }

    void RefreshAxeCard(ForgeRuneOffer offer)
    {
        bool owns = equipment.Has(runeAxe);
        var current = equipment.RuneFor(runeAxe);
        bool preview = offer != null && motion == ForgeMotion.None && pickedRune != AxeRuneId.None && pickedRune != current;
        int ownedAxes = 0;
        foreach (AxeId axe in RuneAxes) if (equipment.Has(axe)) ownedAxes++;
        previousAxe.interactable = nextAxe.interactable =
            offer == null && motion == ForgeMotion.None && appliedRune == AxeRuneId.None && ownedAxes > 1;
        Set(axeHeading, ShortAxe(runeAxe));

        AxeRuneId display = preview ? pickedRune : current;
        if (motion == ForgeMotion.Apply)
        {
            float elapsed = Time.unscaledTime - motionAt;
            display = elapsed < .21f ? replacedRune : elapsed < .38f ? AxeRuneId.None : appliedRune;
        }
        DisplayAxe(runeAxe, display);
        previewCamera.backgroundColor = new Color32(232, 223, 248, 255);

        var shownRune = AxeRunes.Get(display);
        socketStamp.sprite = UiKit.RuneStamp(shownRune.Id);
        Set(axeStatus, preview ? ForgeText("ПРЕДПРОСМОТР РУНЫ", "RUNE PREVIEW")
            : ForgeText("ГНЕЗДО", "SOCKET"));
        bool hasSocket = owns && AxeRunes.Supports(runeAxe);
        Set(slotName, hasSocket ? shownRune.Name : ForgeText("Нет гнезда", "No socket"));
        Set(slotEffect, !hasSocket ? ForgeText(
            "Купи железный у Рико или стальной во вкладке «Топоры».", "Buy an iron axe from Rico or a steel axe in the Axes tab.")
            : shownRune.Effect);
    }

    void RefreshDraw(PlayerInventory stock)
    {
        for (int i = 0; i < tierCards.Length; i++)
        {
            var tier = (ForgeTier)i;
            bool available = equipment.TierUnlocked(tier);
            bool chosen = tier == forgeTier;
            tierFrames[i].gameObject.SetActive(chosen);
            if (chosen) tierFrames[i].color = Selection;
            tierCards[i].color = UiCandy.TintOf(available ? UiTone.Lavender : UiTone.Locked);
            Set(tierEssence[i], AxeRunes.EssenceCost(tier).ToString());
            Set(tierCoins[i], PlayerAxeEquipment.RuneCoinCost(tier).ToString());
            Set(tierLabels[i], available ? TierDescription(tier) : TierRequirement(tier));
        }

        bool guaranteed = forgeTier == ForgeTier.Master && equipment.MasterGuaranteed;
        Set(oddsTitle, ForgeText("Шансы выпадения · ", "Drop odds · ") + TierName(forgeTier) +
            (guaranteed ? ForgeText(" с гарантией", " guaranteed") : ""));
        for (int i = 0; i < oddsValues.Length; i++)
        {
            var rarity = (RuneRarity)i;
            float percent = AxeRunes.Weight(forgeTier, rarity, guaranteed) / 10f;
            string number = percent.ToString("0.#", CultureInfo.InvariantCulture);
            Set(oddsValues[i], ForgeText(number.Replace('.', ','), number) + "%");
            oddsValues[i].color = percent > 0f ? RarityColor(rarity) : UiCandy.Muted;
        }

        Set(guidance, DrawGuidance(guaranteed));
        int coins = PlayerAxeEquipment.RuneCoinCost(forgeTier), essence = AxeRunes.EssenceCost(forgeTier);
        Set(quote, CostText(forgeTier, coins));
        if (stock != null)
        {
            int afterCoins = stock.coinsCount - coins, afterEssence = stock.RuneEssence - essence;
            bool enough = afterCoins >= 0 && afterEssence >= 0;
            remainder.color = enough ? UiCandy.Ink : UiCandy.Danger;
            Set(remainder, enough ? ForgeText("Останется · ", "Remaining · ") + ResourcesText(afterCoins, afterEssence, true)
                : ForgeText("Не хватает · ", "Missing · ") + ResourcesText(Mathf.Max(0, -afterCoins), Mathf.Max(0, -afterEssence)));
        }
        pay.interactable = equipment.CanRollRune(table, runeAxe, forgeTier);
    }

    string DrawGuidance(bool guaranteed)
    {
        if (!AxeRunes.Supports(runeAxe) || !equipment.Has(runeAxe))
            return ForgeText("Для зачарования нужен топор с гнездом.", "Enchanting requires an axe with a socket.");
        if (!equipment.TierUnlocked(forgeTier)) return TierRequirement(forgeTier);
        if (forgeTier == ForgeTier.Master)
            return guaranteed ? ForgeText("В этот раз гарантирована мифическая или гуннарная руна.", "A mythic or Gunnar rune is guaranteed.")
                : ForgeText($"До гарантии · {equipment.MasterMisses}/{AxeRunes.MasterLimit} без мифической или гуннарной",
                    $"Guarantee · {equipment.MasterMisses}/{AxeRunes.MasterLimit} draws without a mythic or Gunnar rune");
        return ForgeText("Эссенцию дают рунные деревья рядом с кузницей.", "Gather essence from the rune trees beside the forge.");
    }

    void RefreshOptions(ForgeRuneOffer offer)
    {
        var installed = AxeRunes.Get(equipment.RuneFor(offer.axe));
        resultRarity.color = RarityColor(offer.Rarity);
        Set(resultRarity, RarityName(offer.Rarity) + ForgeText(" · списано ", " · paid ") + CostText(offer.tier, offer.coins));
        SizeFirstOption(offer.second == AxeRuneId.None);
        FillOption(options[0], offer.first);
        FillOption(options[1], offer.second);
        bool same = installed.Id == pickedRune;
        Set(replaceHint, same ? ForgeText("Такая руна уже нанесена на этот топор.", "This axe already has that rune.")
            : installed.Id == AxeRuneId.None ? ForgeText("Руна займёт пустое гнездо.", "This rune will fill the empty socket.")
            : ForgeText("Будет заменена · ", "Replaces · ") + installed.Name + "\n" + installed.Effect);
        Set(keepLabel, installed.Id == AxeRuneId.None ? ForgeText("Оставить гнездо пустым", "Leave socket empty")
            : ForgeText("Оставить прежнюю руну", "Keep current rune"));
        apply.interactable = !same && pickedRune != AxeRuneId.None && offer.Accepts(pickedRune);

        void FillOption(RuneOption option, AxeRuneId id)
        {
            bool visible = id != AxeRuneId.None;
            option.face.gameObject.SetActive(visible);
            option.border.gameObject.SetActive(visible && id == pickedRune);
            if (!visible) return;
            var rune = AxeRunes.Get(id);
            option.face.color = UiCandy.TintOf(RarityTone(rune.Rarity));
            option.stamp.sprite = UiKit.RuneStamp(id);
            Set(option.name, rune.Name);
            Set(option.effect, rune.Effect);
        }
    }

    void SizeFirstOption(bool single)
    {
        var option = options[0];
        float top = single ? 195 : 173, height = single ? 218 : 127;
        TL(option.face.rectTransform, 10, top, LeftWidth, height);
        TL(option.border.rectTransform, 6, top - 4, LeftWidth + 8, height + 8);
        TLC(option.stamp.rectTransform, single ? 88 : 53, single ? 103 : 58, single ? 119 : 73, single ? 119 : 73);
        TL(option.name.rectTransform, single ? 175 : 101, single ? 36 : 13, single ? 556 : 643, single ? 48 : 36);
        TL(option.effect.rectTransform, single ? 175 : 101, single ? 89 : 54, single ? 556 : 640, single ? 96 : 57);
    }

    void RefreshReceipt()
    {
        var rune = AxeRunes.Get(appliedRune);
        receiptStamp.sprite = UiKit.RuneStamp(appliedRune);
        receiptName.color = RarityColor(rune.Rarity);
        Set(receiptName, rune.Name + " · " + RarityName(rune.Rarity));
        Set(receiptEffect, rune.Effect);
        Set(receiptChange, replacedRune != AxeRuneId.None ? AxeRunes.Get(replacedRune).Name + "  →  " + rune.Name
            : ForgeText("Теперь руна действует с этим топором.", "The rune now works with this axe."));
    }

    void PayForRune()
    {
        if (motion != ForgeMotion.None || catalogOpen || !equipment.TryRollRune(table, runeAxe, forgeTier)) return;
        BeginForgeMotion(ForgeMotion.Draw);
    }

    void ApplyPickedRune()
    {
        if (pickedRune != AxeRuneId.None) FinishRuneChoice(pickedRune);
    }

    void FinishRuneChoice(AxeRuneId rune)
    {
        var offer = equipment.PendingRuneOffer;
        if (offer == null || motion != ForgeMotion.None) return;
        var old = equipment.RuneFor(offer.axe);
        if (!equipment.TryChooseRune(table, offer, rune)) return;
        if (rune == AxeRuneId.None) { Refresh(); return; }

        replacedRune = old;
        appliedRune = rune;
        BeginForgeMotion(ForgeMotion.Apply);
    }

    void BeginForgeMotion(ForgeMotion next)
    {
        if (!HudPolishRoot.Reduced)
        {
            motion = next;
            motionAt = Time.unscaledTime;
            playedHits = 0;
            bool draw = next == ForgeMotion.Draw;
            motionStamp.sprite = draw ? UiKit.CandyIcon(UiIcon.RuneEssence) : UiKit.RuneStamp(appliedRune);
            Set(motionTitle, draw ? ForgeText("Кузница подбирает руны", "Drawing runes")
                : ForgeText("Наносим руну", "Engraving the rune"));
            Set(motionHint, draw ? ForgeText("Горн разгорается…", "The hearth heats up…")
                : ForgeText("Знак проступает на лезвии", "The mark takes shape on the blade"));
        }
        Refresh();
        if (motion != ForgeMotion.None) TickRuneView();
    }

    void EndForgeMotion()
    {
        motion = ForgeMotion.None;
        Refresh();
    }

    void TickRuneView()
    {
        FitRuneWindow();
        if (forgeAxeView.activeSelf) RotatePreview(forgePreview);
        if (motion == ForgeMotion.None) return;

        bool draw = motion == ForgeMotion.Draw;
        int hits = draw ? (int)forgeTier + 1 : 1;
        float elapsed = Time.unscaledTime - motionAt;
        float lastHit = draw ? (hits - 1) * HammerGap : .38f;
        if (elapsed >= lastHit + (draw ? .31f : .32f) || HudPolishRoot.Reduced)
        {
            EndForgeMotion();
            return;
        }

        int count = draw ? Mathf.Min(hits, Mathf.FloorToInt(elapsed / HammerGap) + 1) : elapsed >= lastHit ? 1 : 0;
        if (playedHits < count)
        {
            playedHits = count;
            SfxManager.Play("build", .31f, .97f + .10f * count);
        }
        float t = count == 0 ? 0f : Mathf.Clamp01((elapsed - (draw ? (count - 1) * HammerGap : lastHit)) / .29f);
        float glow = Mathf.Sin(t * Mathf.PI);
        Color pulse = draw ? new Color(.77f, .57f, 1f) : AxeRuneArt.For(appliedRune).Accent;
        pulse.a = glow * .52f;
        motionGlow.color = pulse;
        motionStamp.rectTransform.localScale = Vector3.one * (1f + glow * .17f);
        motionStamp.color = new Color(1f, 1f, 1f, draw ? 1f : Mathf.Lerp(.44f, 1f, Mathf.Clamp01(elapsed / .45f)));
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = (i * 72f + 15f) * Mathf.Deg2Rad;
            float distance = 65f + 72f * t;
            var spark = sparks[i];
            TLC(spark.rectTransform, 380 + Mathf.Cos(angle) * distance, 244 - Mathf.Sin(angle) * distance, 22, 22);
            spark.color = new Color(1f, .88f, .58f, glow * .89f);
        }
        if (!draw) RefreshAxeCard(null);
    }

    static RectTransform Layer(Transform parent, string name)
    {
        var rect = NewRT(name, parent);
        Stretch(rect);
        return rect;
    }

    static Image ForgeSurface(Transform parent, string name, UiTone tone, float x, float y, float w, float h)
    {
        var surface = UiKit.MakeImage(parent, name, UiKit.Slot, true);
        surface.color = UiCandy.TintOf(tone);
        surface.raycastTarget = false;
        TL(surface.rectTransform, x, y, w, h);
        return surface;
    }

    static TMP_Text ForgeLabel(Transform parent, string text, float font, float x, float y, float w, float h,
        bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var label = Lbl(parent, text, font, UiCandy.Ink, alignment, bold, x, y, w, h);
        AutoSize(label, Mathf.Min(15f, font), font);
        label.enableWordWrapping = true;
        return label;
    }

    static string ShortAxe(AxeId axe)
    {
        switch (axe)
        {
            case AxeId.Iron: return ForgeText("Железный", "Iron");
            case AxeId.Steel: return ForgeText("Стальной", "Steel");
            case AxeId.Gunnar: return ForgeText("Топор Гуннара", "Gunnar’s axe");
            default: return ForgeText("Старый", "Old");
        }
    }

    static string TierName(ForgeTier tier) => tier == ForgeTier.Simple ? ForgeText("Простой", "Simple") :
        tier == ForgeTier.Enhanced ? ForgeText("Усиленный", "Enhanced") : ForgeText("Мастерский", "Master");

    static string TierDescription(ForgeTier tier) => tier == ForgeTier.Simple ? ForgeText("Базовый подбор рун", "Basic rune draw") :
        tier == ForgeTier.Enhanced ? ForgeText("Чаще выпадают редкие руны", "Rare runes are more likely") :
        ForgeText("Доступна гуннарная редкость", "Gunnar rarity is possible");

    static string TierRequirement(ForgeTier tier) => tier == ForgeTier.Enhanced ? ForgeText("Нужен стальной топор", "Requires the steel axe") :
        tier == ForgeTier.Master ? ForgeText("Нужен топор Гуннара", "Requires Gunnar’s axe") :
        ForgeText("Нужно восстановить кузницу", "Restore the forge first");

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

    static UiTone RarityTone(RuneRarity rarity)
    {
        switch (rarity)
        {
            case RuneRarity.Rare: return UiTone.Mint;
            case RuneRarity.Legendary: return UiTone.Lemon;
            case RuneRarity.Mythic: return UiTone.Lavender;
            case RuneRarity.Gunnar: return UiTone.Peach;
            default: return UiTone.Neutral;
        }
    }

    static Color RarityColor(RuneRarity rarity)
    {
        switch (rarity)
        {
            case RuneRarity.Rare: return new Color32(34, 104, 83, 255);
            case RuneRarity.Legendary: return new Color32(125, 79, 17, 255);
            case RuneRarity.Mythic: return new Color32(91, 62, 150, 255);
            case RuneRarity.Gunnar: return new Color32(139, 67, 43, 255);
            default: return UiCandy.Ink;
        }
    }

    static string CostText(ForgeTier tier, int coins) => ResourcesText(coins, AxeRunes.EssenceCost(tier));

    static string ResourcesText(int coins, int essence, bool showBoth = false)
    {
        var e = essence + " " + ForgeText(UiDraw.Plural(essence, "эссенция", "эссенции", "эссенций"), "essence");
        var c = coins + " " + ForgeText(UiDraw.Plural(coins, "монета", "монеты", "монет"), coins == 1 ? "coin" : "coins");
        if (showBoth) return e + " · " + c;
        if (coins == 0) return e;
        if (essence == 0) return c;
        return e + " + " + c;
    }
}

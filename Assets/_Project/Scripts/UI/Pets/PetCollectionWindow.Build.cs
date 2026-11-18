using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    void Build()
    {
        if (built) return;
        var cgo = new GameObject("PetCollectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = 5;
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UiLayers.Window;
        windowCanvas = (RectTransform)cgo.transform;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var ov = UiKit.MakeImage(cgo.transform, "Overlay", null, false);
        ov.color = new Color(0.08f, 0.06f, 0.16f, 0.72f);
        Stretch(ov.rectTransform);
        ov.raycastTarget = true;
        overlay = ov.gameObject.AddComponent<CanvasGroup>();
        var skin = TWPetCollectionSkin.Current;
        var win = UiKit.MakeImage(ov.transform, "Window", skin != null ? skin.window : UiKit.Panel, true, 0.65f);
        panel = win.rectTransform;
        Anch(panel, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(WinW, WinH));
        AddBtn(win, () => { });
        BuildHeader();
        var content = NewRT("Content", panel);
        TL(content, ContentX, ContentY, InW, InH);
        BuildTop(content);
        BuildGrid(content);
        BuildDetail(content);
        hintText = TWPetCollectionSkin.Label(panel, "", 19f, Dim, TextAlignmentOptions.Center, false);
        TL(hintText.rectTransform, 96f, WinH - 48f, WinW - 192f, 30f);
        PetUi.AutoSize(hintText, 15f, 19f);
        fx = AchSparks.Create(panel, Vector2.one * 0.5f);
        hearts = PetHeartFx.Create(panel, Vector2.one * 0.5f);
        BuildFilters();
        BuildPresets(panel);
        BuildActions();
        Canvas.ForceUpdateCanvases();
        FitWindow();
        ov.gameObject.SetActive(false);
        built = true;
    }

    void BuildHeader()
    {
        var paw = PetIcon(panel, UiIcon.Paw, 44f, "Paw");
        TLC(paw.rectTransform, 132f, 52f, 44f, 44f);
        titleText = TWPetCollectionSkin.Label(panel, "Питомцы", 40f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(titleText.rectTransform, 170f, 28f, 400f, 54f);
        countText = TWPetCollectionSkin.Label(panel, "", 21f, Dim, TextAlignmentOptions.Right, false);
        TL(countText.rectTransform, WinW - 510f, 38f, 300f, 34f);
        TMP_Text label;
        var close = MakeBtn(panel, "×", UiBtn.Gray, WinW - 158f, 28f, 54f, 54f, 32f, DoClose, out label);
        var skin = TWPetCollectionSkin.Current;
        if (skin != null && skin.close.Length >= 4 && skin.close[0] != null)
        {
            label.text = "";
            var image = close.GetComponent<Image>();
            image.sprite = skin.close[0]; image.type = Image.Type.Simple;
            close.transition = Selectable.Transition.SpriteSwap;
            close.spriteState = new SpriteState { highlightedSprite = skin.close[1], pressedSprite = skin.close[2], selectedSprite = skin.close[0], disabledSprite = skin.close[3] };
        }
    }

    void BuildTop(Transform parent)
    {
        var team = TWPetCollectionSkin.Surface(parent, "Team");
        TL(team.rectTransform, TeamX, TeamY, TeamW, TeamH);
        var tr = team.transform;
        slotsLabel = TWPetCollectionSkin.Label(tr, "Команда", 26f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(slotsLabel.rectTransform, 22f, 18f, TeamW - 44f, 34f);
        PetUi.AutoSize(slotsLabel, 22f, 26f);
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            var slot = new SlotView();
            slot.bg = TWPetCollectionSkin.Surface(tr, "Slot" + i);
            TL(slot.bg.rectTransform, 12f + i % 2 * (SlotW + SlotGapX), SlotY + i / 2 * (SlotH + SlotGapY), SlotW, SlotH);
            AddBtn(slot.bg, () => OnSlotClick(index));
            slot.hover = slot.bg.gameObject.AddComponent<PetHover>();
            slot.bg.gameObject.AddComponent<PetRightClick>().cb = () => OnSlotUnequip(index);
            slot.card = PetCardView.Create(slot.bg.transform, SlotW, SlotH, "Pet", false);
            slot.card.UseCloudStyle(TWPetCollectionSkin.Current);
            Anch(slot.card.rt, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(SlotW, SlotH));
            slot.icon = PetIcon(slot.bg.transform, UiIcon.Paw, 40f, "Empty");
            slot.icon.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            slot.label = TWPetCollectionSkin.Label(slot.bg.transform, "", 18f, Dim);
            Anch(slot.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(SlotW - 14f, 28f));
            TMP_Text xLabel;
            var remove = MakeBtn(slot.bg.transform, "×", UiBtn.Gray, SlotW - 34f, 5f, 28f, 28f, 20f, () => OnSlotUnequip(index), out xLabel);
            slot.xBtn = remove.gameObject;
            slot.xBtn.SetActive(false);
            Tip(remove, "Убрать из команды. Питомец останется в коллекции");
            slots[i] = slot;
        }
        teamHelp = TWPetCollectionSkin.Label(tr, "", 17f, Dim, TextAlignmentOptions.Center, false);
        TL(teamHelp.rectTransform, 16f, 224f, TeamW - 32f, 28f);
        bonusPanel = TWPetCollectionSkin.Surface(tr, "TeamBonuses");
        TL(bonusPanel.rectTransform, 12f, BonusY, TeamW - 24f, BonusH);
        var title = TWPetCollectionSkin.Label(bonusPanel.transform, "Бонусы команды", 23f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(title.rectTransform, 16f, 12f, TeamW - 56f, 32f);
        for (int i = 0; i < BonusRows; i++)
        {
            float y = BonusRowY + i * BonusPitch;
            bonusIcon[i] = PetIcon(bonusPanel.transform, UiIcon.Axe, 23f, "BonusIcon");
            TLC(bonusIcon[i].rectTransform, 28f, y + 14f, 23f, 23f);
            bonusText[i] = TWPetCollectionSkin.Label(bonusPanel.transform, "", 20f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left, false);
            TL(bonusText[i].rectTransform, 50f, y, TeamW - 92f, 28f);
            PetUi.AutoSize(bonusText[i], 17f, 20f);
            bonusBarTrack[i] = UiKit.MakeImage(bonusPanel.transform, "HiddenTrack", null, false);
            bonusBarFill[i] = UiKit.MakeImage(bonusBarTrack[i].transform, "Fill", null, false);
            bonusBarTrack[i].gameObject.SetActive(false);
        }
        bonusEmpty = TWPetCollectionSkin.Label(bonusPanel.transform, "Возьми питомца —\nон начнёт помогать.", 21f, Dim, TextAlignmentOptions.Center, false);
        TL(bonusEmpty.rectTransform, 16f, 64f, TeamW - 56f, 100f);
        bonusEmpty.enableWordWrapping = true;
        autoBtn = MakeBtn(tr, "Подобрать лучших", UiBtn.Gray, 12f, TeamH - 62f, TeamW - 24f, 46f, 21f, OnAutoClick, out autoLabel);
        Tip(autoBtn, "Подобрать команду с наибольшим суммарным бонусом. Ctrl+Z вернёт прежний состав");
    }
}

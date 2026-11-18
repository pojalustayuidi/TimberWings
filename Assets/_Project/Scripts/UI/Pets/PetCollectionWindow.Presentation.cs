using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    enum Popup { None, Filters, Presets, Actions }
    Popup popup;
    GameObject filtersPopup, presetsPopup, actionsPopup;
    RectTransform windowCanvas;
    Vector2 fittedCanvas;
    Rect fittedSafeArea;
    float fitScale = 1f;
    TMP_Text filtersCaption, teamHelp;
    Button filtersToggle, presetsToggle, moreActions;
    Image detailStatBox;
    readonly Button[] presetApply = new Button[PresetCount];
    readonly TMP_Text[] presetApplyText = new TMP_Text[PresetCount];

    static Image PetIcon(Transform parent, UiIcon icon, float size, string name = null)
    {
        var image = UiKit.MakeImage(parent, name ?? icon.ToString(), TWPetCollectionSkin.Icon(icon), false);
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        return image;
    }

    void FitWindow()
    {
        if (windowCanvas == null) return;
        var size = windowCanvas.rect.size;
        var safe = Screen.safeArea;
        if (fittedCanvas == size && fittedSafeArea == safe) return;
        fittedCanvas = size;
        fittedSafeArea = safe;
        Vector2 a, b;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(windowCanvas, safe.min, null, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(windowCanvas, safe.max, null, out b);
        fitScale = Mathf.Clamp(Mathf.Min((b.x - a.x - 32f) / WinW, (b.y - a.y - 32f) / WinH), 0.1f, 1f);
        panel.anchoredPosition = (a + b) * 0.5f;
    }

    void SetPopup(Popup value)
    {
        bool hadConfirmation = armedUntil > 0f;
        armedUntil = 0f;
        popup = value;
        if (filtersPopup != null) filtersPopup.SetActive(value == Popup.Filters);
        if (presetsPopup != null) presetsPopup.SetActive(value == Popup.Presets);
        if (actionsPopup != null) actionsPopup.SetActive(value == Popup.Actions);
        hoverPc = null;
        PetHover.ClearTip();
        if (value == Popup.Presets) RefreshPresets();
        if (hadConfirmation && built) RefreshDetail();
    }

    RectTransform PopupCard(string name, Vector2 size, Vector2 position, out GameObject root)
    {
        var shade = UiKit.MakeImage(panel, name, null, false);
        Stretch(shade.rectTransform);
        shade.color = new Color(0.13f, 0.10f, 0.24f, 0.25f);
        AddBtn(shade, () => SetPopup(Popup.None));
        root = shade.gameObject;
        var card = TWPetCollectionSkin.Surface(shade.transform, name + "Card");
        Anch(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        AddBtn(card, () => { });
        root.SetActive(false);
        return card.rectTransform;
    }

    void BuildFilters()
    {
        var card = PopupCard("FiltersPopup", new Vector2(690f, 340f), new Vector2(32f, 150f), out filtersPopup);
        var title = TWPetCollectionSkin.Label(card, "Фильтры и порядок", 28f, TWPetCollectionSkin.Ink, TextAlignmentOptions.Left);
        TL(title.rectTransform, 28f, 22f, 500f, 38f);
        TMP_Text done;
        MakeBtn(card, "Готово", UiBtn.Gray, 536f, 20f, 128f, 42f, 20f, () => SetPopup(Popup.None), out done);
        sortBtn = MakeBtn(card, "", UiBtn.Gray, 28f, 82f, 310f, 48f, 21f, OnSortClick, out sortLabel);
        filterBtn = MakeBtn(card, "", UiBtn.Gray, 350f, 82f, 312f, 48f, 21f, OnFilterClick, out filterLabel);
        var rarity = TWPetCollectionSkin.Label(card, "Редкость", 20f, Dim, TextAlignmentOptions.Left, false);
        TL(rarity.rectTransform, 28f, 150f, 300f, 28f);
        string[] names = { "Все", "Обычные", "Необычные", "Редкие", "Легенды" };
        for (int i = 0; i < rarBtn.Length; i++)
        {
            int index = i;
            TMP_Text label;
            rarBtn[i] = MakeBtn(card, names[i], UiBtn.Gray, 28f + i * 128f, 192f, 120f, 46f, 18f, () => OnRarClick(index), out label);
            rarImg[i] = rarBtn[i].GetComponent<Image>();
        }
        TMP_Text reset;
        MakeBtn(card, "Сбросить фильтры", UiBtn.Gray, 28f, 270f, 280f, 46f, 20f, OnResetFilters, out reset);
    }

    void BuildActions()
    {
        var card = PopupCard("PetActionsPopup", new Vector2(560f, 280f), Vector2.zero, out actionsPopup);
        var title = TWPetCollectionSkin.Label(card, "Действия с питомцем", 29f, TWPetCollectionSkin.Ink);
        TL(title.rectTransform, 24f, 24f, 512f, 40f);
        var note = TWPetCollectionSkin.Label(card, "Отпущенный питомец исчезнет из коллекции.\nДля удаления нужно нажать кнопку дважды.", 21f, Dim);
        TL(note.rectTransform, 30f, 82f, 500f, 65f);
        note.enableWordWrapping = true;
        releaseBtn = MakeBtn(card, "Отпустить", UiBtn.Gray, 30f, 178f, 300f, 54f, 23f, OnReleaseClick, out releaseLabel);
        releaseColor = TWPetCollectionSkin.Warning;
        releaseLabel.color = releaseColor;
        TMP_Text cancel;
        MakeBtn(card, "Назад", UiBtn.Gray, 346f, 178f, 184f, 54f, 22f, () => SetPopup(Popup.None), out cancel);
        armBar = UiKit.MakeImage(releaseBtn.transform, "ConfirmTime", null, false);
        armBar.color = TWPetCollectionSkin.Warning;
        var bar = armBar.rectTransform;
        bar.anchorMin = Vector2.zero; bar.anchorMax = new Vector2(1f, 0f); bar.pivot = Vector2.zero;
        bar.offsetMin = new Vector2(16f, 6f); bar.offsetMax = new Vector2(-16f, 10f);
        armBar.gameObject.SetActive(false);
    }

    void RefreshFilterCaption()
    {
        int active = (filterStat >= 0 ? 1 : 0) + (rarFilter >= 0 ? 1 : 0);
        Set(filtersCaption, active > 0 ? "Фильтры · " + active : "Фильтры");
        if (filtersToggle != null) filtersToggle.interactable = tab == Tab.Collection;
        for (int i = 0; i < rarImg.Length; i++)
            if (rarImg[i] != null)
                rarImg[i].color = rarFilter == i - 1 ? new Color(0.85f, 0.80f, 1f) : Color.white;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class ChapterOneJournal : MonoBehaviour
{
    static readonly (string key, string ru, string en)[] Conversations =
    {
        ("h01", "Мира · О лагере", "Mira · About the camp"),
        ("h02", "Оскар · Бумажные крылья", "Oskar · Paper wings"),
        ("h03", "Рико · Спросить о Гуннаре", "Rico · Ask about Gunnar"),
        ("s05", "Мира · Первые улучшения", "Mira · First upgrades"),
        ("s10", "Оскар · Профессии артели", "Oskar · Crew jobs"),
        ("s07", "Оскар · Как работает пила", "Oskar · How the saw works"),
        ("s12", "Оскар · Старая кузница", "Oskar · The old forge"),
        ("s13", "Оскар · Рунные деревья", "Oskar · Rune Trees"),
        ("s14", "Оскар · Осмотреть лезвие", "Oskar · Inspect the blade"),
        ("crates", "Оскар · Где найти клети", "Oskar · Where to find the crates")
    };

    StoryFlow story;
    Button openButton;
    TMP_Text buttonLabel;
    GameObject overlay;
    RectTransform contents;
    TMP_Text title;
    bool open;

    void Awake() => story = GetComponent<StoryFlow>();

    void OnEnable()
    {
        UiModal.Register(this, () => open, Close);
        GameLanguage.Changed += RefreshLanguage;
        SaveSystem.Loaded += Close;
    }

    void OnDisable()
    {
        UiModal.Unregister(this);
        GameLanguage.Changed -= RefreshLanguage;
        SaveSystem.Loaded -= Close;
        Close();
    }

    void Start()
    {
        var hud = NewCanvas("Chapter journal button", UiLayers.HudOverlay);
        using (UiKit.UseSkin(UiSkin.Candy))
            openButton = UiKit.MakeButton(hud.transform, "", UiBtn.Green, new Vector2(164f, 42f), 20f);
        var rt = (RectTransform)openButton.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(28f, 26f);
        buttonLabel = openButton.GetComponentInChildren<TMP_Text>(true);
        openButton.onClick.AddListener(Toggle);
        RefreshLanguage();
    }

    void Update()
    {
        if (openButton == null) return;
        bool canOpen = !SettingsMenu.IsOpen && !SaveSystem.IsLoading && !ArrivalIntroSequence.Active &&
            !EndingSequence.Active && !StoryInspection.Active && !DialogueSequence.AnyPlaying &&
            !DialogueManager.IsBusy && !BuildingController.AnyRevealing && !UiModal.AnyOpen(this);
        openButton.gameObject.SetActive(canOpen && !open);
        if (Input.GetKeyDown(KeyCode.N) && (open || canOpen)) Toggle();
    }

    void Toggle()
    {
        if (open) { Close(); return; }
        if (MobileInput.ModalOpen || DialogueManager.IsBusy || DialogueSequence.AnyPlaying) return;
        if (overlay == null) Build();
        open = true;
        overlay.SetActive(true);
        Populate();
    }

    void Close()
    {
        open = false;
        if (overlay != null) overlay.SetActive(false);
    }

    void RefreshLanguage()
    {
        if (buttonLabel != null) buttonLabel.text = ChapterOneText.Pick("Журнал [N]", "Journal [N]");
        if (open) Populate();
    }

    Canvas NewCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        return canvas;
    }

    void Build()
    {
        var canvas = NewCanvas("Chapter journal", UiLayers.Window);
        overlay = canvas.gameObject;

        var shade = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(canvas.transform, false);
        var shadeRect = (RectTransform)shade.transform;
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(.06f, .1f, .14f, .64f);
        var window = UiCandy.Window(canvas.transform, new Vector2(730f, 620f), "", Close, "LeoJournal");
        title = window.title;

        var viewport = new GameObject("Pages", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(window.content, false);
        var rect = (RectTransform)viewport.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, .015f);

        var page = new GameObject("Entries", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        page.transform.SetParent(rect, false);
        contents = (RectTransform)page.transform;
        contents.anchorMin = new Vector2(0f, 1f);
        contents.anchorMax = Vector2.one;
        contents.pivot = new Vector2(.5f, 1f);
        contents.sizeDelta = Vector2.zero;
        var layout = page.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 13f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        page.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = rect;
        scroll.content = contents;
        scroll.horizontal = false;
        scroll.scrollSensitivity = 24f;
    }

    void Populate()
    {
        title.text = ChapterOneText.Pick("Журнал Лео", "Leo's journal");
        for (int i = contents.childCount - 1; i >= 0; i--)
        {
            var child = contents.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        contents.anchoredPosition = Vector2.zero;
        var body = UiKit.MakeLabel(contents, story.ChapterJournalPage(), 24f, UiCandy.Ink,
            TextAlignmentOptions.TopLeft, false);
        body.enableWordWrapping = true;
        body.richText = true;

        foreach (var item in Conversations)
        {
            if (!story.CanChapterChat(item.key)) continue;
            var key = item.key;
            Button button;
            using (UiKit.UseSkin(UiSkin.Candy))
                button = UiKit.MakeButton(contents, ChapterOneText.Pick(item.ru, item.en), UiBtn.Green, new Vector2(600f, 42f), 21f);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            button.onClick.AddListener(() => { Close(); story.PlayChapterChat(key); });
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(250), DisallowMultipleComponent]
public sealed class TWCompactBackpackHud : MonoBehaviour
{
    const int QuarterCount = 4;
    const float Width = 204f, Height = 70f;
    static readonly Color Ink = new Color32(71, 50, 101, 255);
    static readonly Color Ochre = new Color32(145, 72, 32, 255);
    static readonly Color Lavender = new Color32(158, 134, 196, 255);
    static readonly Color Amber = new Color32(233, 168, 78, 255);

    static Sprite orangeBackpack;
    static bool spriteRequested;
    PlayerInventory inventory;
    PlayerHeadHud owner;
    Canvas canvas;
    RectTransform card;
    Image plate, icon;
    TMP_Text count, status;
    readonly Image[] quarters = new Image[QuarterCount];
    HudSafeAreaRegistry registry;
    int shownWood = -1, shownCapacity = -1;
    bool wasFull, wasNear, english;
    float shownRatio, flash;

    public bool IsReady => card != null;
    public int DisplayedCarried => shownWood;
    public int DisplayedCapacity => shownCapacity;
    public float DisplayedRatio => shownRatio;
    public string DisplayedText => count != null ? count.text : string.Empty;
    public bool UsesOrangeIcon => orangeBackpack != null;
    public int QuarterSlots => IsReady ? QuarterCount : 0;
    public float QuarterFill(int index) => IsReady && index >= 0 && index < QuarterCount ? quarters[index].fillAmount : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatic()
    {
        if (orangeBackpack != null) Destroy(orangeBackpack);
        orangeBackpack = null;
        spriteRequested = false;
    }

    static Sprite BackpackIcon()
    {
        if (!spriteRequested)
        {
            spriteRequested = true;
            var texture = Resources.Load<Texture2D>("TWBackpackOrange");
            if (texture != null)
                orangeBackpack = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect);
        }
        return orangeBackpack != null ? orangeBackpack : CandyTag.Icon(UiIcon.Backpack);
    }

    internal void Bind(PlayerHeadHud hud, PlayerInventory playerInventory, Canvas host, TMP_FontAsset font)
    {
        owner = hud;
        inventory = playerInventory;
        canvas = host;
        if (card != null) return;
        plate = UiKit.MakeImage(canvas.transform, "Backpack capacity", CandyTag.Panel, true, .55f);
        plate.raycastTarget = false;
        card = plate.rectTransform;
        card.sizeDelta = new Vector2(Width, Height);
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.23f, .14f, .33f, .20f);
        shadow.effectDistance = new Vector2(0f, -3f);

        var med = UiKit.MakeImage(card, "Backpack medallion", CandyTag.Medallion, false);
        Place(med.rectTransform, 10f, 9f, 52f, 52f);
        icon = UiKit.MakeImage(med.transform, "Backpack icon", BackpackIcon(), false);
        Place(icon.rectTransform, 3f, 3f, 46f, 46f);
        icon.preserveAspect = true;

        count = CandyTag.Text(card, "Carried logs", 30f, Ink, TextAlignmentOptions.MidlineLeft, true, font);
        count.enableAutoSizing = true;
        count.fontSizeMin = 19f;
        count.fontSizeMax = 30f;
        Place(count.rectTransform, 68f, 19f, 124f, 36f);
        status = CandyTag.Text(card, "Capacity warning", 13f, Ochre, TextAlignmentOptions.MidlineLeft, true, font);
        status.textWrappingMode = TextWrappingModes.NoWrap;
        Place(status.rectTransform, 68f, 46f, 126f, 17f);
        for (int i = 0; i < QuarterCount; i++)
        {
            var track = UiKit.MakeImage(card, "Capacity track", UiDraw.Pill, false, 3f);
            Place(track.rectTransform, 69f + i * 31f, 12f, 29f, 5f);
            track.color = new Color32(218, 204, 229, 255);
            var fill = UiKit.MakeImage(track.transform, "Capacity", UiDraw.Pill, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            quarters[i] = fill;
        }
    }

    internal void Hide()
    {
        if (card != null) card.gameObject.SetActive(false);
    }

    internal void Flash() => flash = 1f;

    internal void SetIcon(Sprite sprite)
    {
        if (icon != null && orangeBackpack == null && sprite != null) icon.sprite = sprite;
    }

    void LateUpdate()
    {
        if (card == null || inventory == null) return;
        var activeRegistry = HudSafeAreaRegistry.Instance;
        if (registry != activeRegistry)
        {
            if (registry != null) registry.Unregister(card);
            registry = activeRegistry;
            if (registry != null) registry.Register(card);
        }
        bool visible = owner != null && owner.isActiveAndEnabled && inventory == PlayerInventory.Instance &&
            !HudGameplayLayout.Blocked;
        if (card.gameObject.activeSelf != visible) card.gameObject.SetActive(visible);

        int wood = Mathf.Max(0, inventory.woodCount);
        int capacity = Mathf.Max(1, inventory.Capacity);
        float ratio = Mathf.Clamp01(wood / (float)capacity);
        bool full = inventory.FreeSlots <= 0;
        bool near = !full && ratio >= .75f;
        bool isEnglish = GameLanguage.English;
        if (shownWood != wood || shownCapacity != capacity || full != wasFull || near != wasNear || isEnglish != english)
        {
            if (shownWood < 0 || !visible) shownRatio = ratio;
            else if (!wasFull && full) Flash();
            shownWood = wood;
            shownCapacity = capacity;
            wasFull = full;
            wasNear = near;
            english = isEnglish;
            count.text = wood + "/" + capacity;
            count.color = full ? Ochre : Ink;
            status.text = full ? ChopPrototypeLog.Text("Рюкзак полон", "Backpack full")
                : near ? ChopPrototypeLog.Text("Почти полон", "Almost full") : string.Empty;
            count.rectTransform.anchoredPosition = new Vector2(68f, full || near ? 17f : 21f);
        }
        if (!visible) return;

        float dt = Time.unscaledDeltaTime;
        bool reduce = VibeSettings.ReduceEffects;
        shownRatio = reduce ? ratio : Mathf.MoveTowards(shownRatio, ratio, dt * 2.9f);
        flash = Mathf.MoveTowards(flash, 0f, dt * 2.7f);
        for (int i = 0; i < QuarterCount; i++)
        {
            quarters[i].fillAmount = Mathf.Clamp01(shownRatio * QuarterCount - i);
            quarters[i].color = full || near ? Amber : Lavender;
        }
        plate.color = full
            ? Color.Lerp(new Color32(255, 230, 191, 255), new Color32(255, 204, 120, 255), reduce ? 0f : flash)
            : Color.white;
        float scale = HudGameplayLayout.Fit(canvas, Width, .46f);
        card.localScale = Vector3.one * scale;
        HudGameplayLayout.Dock(card, canvas, false);
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void OnDisable() => Hide();

    void OnDestroy()
    {
        if (registry != null) registry.Unregister(card);
        if (card != null) Destroy(card.gameObject);
    }
}

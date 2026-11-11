// TimberWings - compact adaptive backpack HUD, polish v2.
// A self-contained visual skin for the existing PlayerHeadHud.
// No inventory, upgrade, save, or HUD builder replacement is required.
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(250)]
public sealed class TWCompactBackpackHud : MonoBehaviour
{
    private const int QuarterCount = 4;
    private const float Width = 230f;
    private const float Height = 71f;
    private const float TailHeight = 13f;
    private const float MedalSize = 69f;
    private const float TrackWidth = 137f;
    private const float TrackHeight = 12f;
    private const float TrackX = 76f;
    private const float TrackY = 8f;

    private static readonly Color Ink = new Color32(0x2B, 0x29, 0x58, 255);
    private static readonly Color FullInk = new Color32(0x9A, 0x5A, 0x0B, 255);
    private static readonly Color Amber = new Color32(0xF9, 0xA1, 0x35, 255);
    private static readonly Color AmberFull = new Color32(0xFF, 0xB4, 0x36, 255);
    private static readonly Color Lavender = new Color32(0x8B, 0x78, 0xCB, 255);
    private static readonly Color TrackEmpty = new Color32(0xD6, 0xCF, 0xE7, 255);

    private static bool bootstrapExists;
    private static Sprite orangeBackpack;
    private static bool spriteRequested;

    private PlayerHeadHud hud;
    private PlayerInventory inventory;
    private RectTransform card, tail, chop, rhythm, shadow;
    private Image frame, medal, twinkle;
    private TMP_Text count, gainText;
    private readonly Image[] quarterFills = new Image[QuarterCount];
    private float shownRatio, gainTime, celebrate, upgradeFlash, nextFindTime;
    private int displayedCarried, displayedCapacity;
    private bool hasInitialValue, ready;
    private string expectedText;

    // Public read-only values for the Editor QA menu.
    public bool IsReady { get { return ready; } }
    public int DisplayedCarried { get { return displayedCarried; } }
    public int DisplayedCapacity { get { return displayedCapacity; } }
    public float DisplayedRatio { get { return shownRatio; } }
    public string DisplayedText { get { return count != null ? count.text : ""; } }
    public bool UsesOrangeIcon { get { return orangeBackpack != null; } }
    public int QuarterSlots { get { return ready ? quarterFills.Length : 0; } }
    public float QuarterFill(int index)
    {
        return ready && index >= 0 && index < QuarterCount && quarterFills[index] != null
            ? quarterFills[index].fillAmount : 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        bootstrapExists = false;
        orangeBackpack = null;
        spriteRequested = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (bootstrapExists) return;
        bootstrapExists = true;
        GameObject go = new GameObject("TW_CompactBackpack_Bootstrap");
        DontDestroyOnLoad(go);
        go.AddComponent<TWCompactBackpackHud>();
    }

    private void Update()
    {
        // One lightweight bootstrap scanner. The skin itself lives on each player.
        if (hud != null || Time.unscaledTime < nextFindTime) return;
        nextFindTime = Time.unscaledTime + 0.5f;
        PlayerHeadHud[] all = FindObjectsOfType<PlayerHeadHud>();
        for (int i = 0; i < all.Length; i++)
        {
            PlayerHeadHud candidate = all[i];
            if (candidate == null || candidate.GetComponent<TWCompactBackpackHud>() != null) continue;
            if (candidate.GetComponent<PlayerInventory>() == null) continue;
            TWCompactBackpackHud skin = candidate.gameObject.AddComponent<TWCompactBackpackHud>();
            skin.hud = candidate;
            skin.inventory = candidate.GetComponent<PlayerInventory>();
        }
    }

    private void LateUpdate()
    {
        if (hud == null) return;
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (inventory == null) return;
        if (!ready && !TryBuild()) return;
        UpdateVisual();
    }

    private static RectTransform MakeRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void FixedRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Sprite GetBackpackSprite()
    {
        if (orangeBackpack != null || spriteRequested) return orangeBackpack;
        spriteRequested = true;
        Texture2D tex = Resources.Load<Texture2D>("TWBackpackOrange");
        if (tex == null)
        {
            Debug.LogWarning("[TW Backpack] TWBackpackOrange.png not found under Assets/_Project/Resources; old backpack icon remains.");
            return null;
        }
        // FullRect requires no mesh analysis. The imported PNG may remain a normal Texture2D.
        orangeBackpack = Sprite.Create(tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        orangeBackpack.name = "TWBackpackOrange_RuntimeSprite";
        return orangeBackpack;
    }

    private bool TryBuild()
    {
        // Bind to THIS actor, not to the first PlayerHeadHud under a shared canvas.
        var field = typeof(PlayerHeadHud).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic);
        RectTransform root = field != null ? field.GetValue(hud) as RectTransform : null;
        if (root == null) return false;
        card = root.Find("Pack") as RectTransform;
        tail = root.Find("Tail") as RectTransform;
        chop = root.Find("Chop") as RectTransform;
        rhythm = root.Find("Rhythm") as RectTransform;
        if (card == null || tail == null || chop == null || rhythm == null) return false;

        var oldSegments = card.Find("Segments");
        var status = card.Find("Status");
        RectTransform medRect = card.Find("Medallion") as RectTransform;
        Transform oldText = card.Find("Count");
        if (oldSegments == null || medRect == null || oldText == null) return false;
        if (card.Find("QuarterProgress") != null)
        {
            Debug.LogWarning("[TW Backpack] Duplicate quarter progress found; refusing to install twice.");
            enabled = false;
            return false;
        }
        if (status == null)
        {
            Debug.LogWarning("[TW Backpack] Base HUD was modified by another backpack patch. Restore base PlayerHeadHud before using this skin.");
            enabled = false;
            return false;
        }

        count = oldText.GetComponent<TMP_Text>();
        frame = card.Find("Frame") != null ? card.Find("Frame").GetComponent<Image>() : null;
        medal = medRect.GetComponent<Image>();
        if (count == null || frame == null || medal == null) return false;

        // Never destroy old objects: PlayerHeadHud keeps refs and animates those slots.
        status.gameObject.SetActive(false);
        oldSegments.gameObject.SetActive(false);

        // Subtle shadow as a sibling BEHIND the panel, not over its contents.
        shadow = MakeRect("CompactBackpackShadow", root);
        shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0f);
        shadow.pivot = new Vector2(0.5f, 0f);
        shadow.anchoredPosition = new Vector2(2f, TailHeight - 4f);
        shadow.sizeDelta = new Vector2(Width, Height);
        var shImage = shadow.gameObject.AddComponent<Image>();
        shImage.sprite = UiDraw.Pill;
        shImage.type = Image.Type.Sliced;
        shImage.color = new Color(0.16f, 0.12f, 0.36f, 0.19f);
        shImage.raycastTarget = false;
        shadow.SetSiblingIndex(card.GetSiblingIndex());

        card.sizeDelta = new Vector2(Width, Height);
        var panel = card.GetComponent<Image>();
        if (panel != null) panel.color = Color.white;
        tail.sizeDelta = new Vector2(24f, 24f * 22f / 36f);

        medRect.anchoredPosition = new Vector2(10f, 1f);
        medRect.sizeDelta = new Vector2(MedalSize, MedalSize);
        var iconRt = medRect.Find("Icon") as RectTransform;
        if (iconRt != null)
        {
            iconRt.sizeDelta = new Vector2(57f, 57f);
            var icon = iconRt.GetComponent<Image>();
            if (icon != null)
            {
                Sprite newSprite = GetBackpackSprite();
                if (newSprite != null) icon.sprite = newSprite;
                icon.color = Color.white;
                icon.preserveAspect = true;
            }
        }

        RectTransform number = count.rectTransform;
        FixedRect(number, TrackX, 24f, 136f, 42f);
        count.richText = false;
        count.enableAutoSizing = true;
        count.fontSize = 36f;
        count.fontSizeMin = 19f;
        count.fontSizeMax = 36f;
        count.alignment = TextAlignmentOptions.MidlineLeft;
        count.color = Ink;

        // 4 proportional quarters at any capacity (including 21, 54 or 1000).
        RectTransform track = MakeRect("QuarterProgress", card);
        FixedRect(track, TrackX, TrackY, TrackWidth, TrackHeight);
        const float gap = 4f;
        float each = (TrackWidth - gap * (QuarterCount - 1)) / QuarterCount;
        for (int i = 0; i < QuarterCount; i++)
        {
            RectTransform qr = MakeRect("Quarter" + (i + 1), track);
            FixedRect(qr, i * (each + gap), 0f, each, TrackHeight);
            var back = qr.gameObject.AddComponent<Image>();
            back.sprite = UiDraw.Pill;
            back.type = Image.Type.Sliced;
            back.color = TrackEmpty;
            back.raycastTarget = false;

            RectTransform inner = MakeRect("Fill", qr);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(1.3f, 1.3f);
            inner.offsetMax = new Vector2(-1.3f, -1.3f);
            var fill = inner.gameObject.AddComponent<Image>();
            fill.sprite = UiDraw.Pill;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.color = Amber;
            fill.raycastTarget = false;
            quarterFills[i] = fill;
        }

        // Tiny pickup / capacity gain floats near the right side of the card.
        RectTransform pop = MakeRect("CompactPickupGain", card);
        pop.anchorMin = pop.anchorMax = new Vector2(1f, 1f);
        pop.pivot = new Vector2(0.5f, 0f);
        pop.anchoredPosition = new Vector2(-10f, -1f);
        pop.sizeDelta = new Vector2(94f, 27f);
        gainText = pop.gameObject.AddComponent<TextMeshProUGUI>();
        gainText.font = count.font;
        gainText.fontStyle = FontStyles.Bold;
        gainText.alignment = TextAlignmentOptions.Center;
        gainText.fontSize = 21f;
        gainText.enableAutoSizing = true;
        gainText.fontSizeMin = 14f;
        gainText.fontSizeMax = 21f;
        gainText.raycastTarget = false;
        gainText.text = "";
        gainText.color = new Color(1f, 1f, 1f, 0f);

        RectTransform star = MakeRect("CompactFullSparkle", medRect);
        star.anchorMin = star.anchorMax = new Vector2(1f, 1f);
        star.pivot = new Vector2(0.5f, 0.5f);
        star.anchoredPosition = new Vector2(-2f, -6f);
        star.sizeDelta = new Vector2(25f, 25f);
        twinkle = star.gameObject.AddComponent<Image>();
        twinkle.sprite = CandyTag.Icon(UiIcon.Star);
        twinkle.preserveAspect = true;
        twinkle.raycastTarget = false;
        twinkle.color = new Color(1f, 0.86f, 0.4f, 0f);

        ready = true;
        return true;
    }

    private void UpdateVisual()
    {
        int carried = Mathf.Max(0, inventory.woodCount);
        int cap = Mathf.Max(1, inventory.Capacity);
        float ratio = Mathf.Clamp01(carried / (float)cap);
        bool full = carried >= cap;
        bool reduce = VibeSettings.ReduceEffects;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        if (!hasInitialValue)
        {
            shownRatio = ratio;   // no fake animation on loading a save
            displayedCarried = carried;
            displayedCapacity = cap;
            hasInitialValue = true;
        }
        else if (displayedCarried != carried || displayedCapacity != cap)
        {
            int gain = carried - displayedCarried;
            int upgrade = cap - displayedCapacity;
            if (gain > 0)
            {
                gainText.text = "+" + gain;
                gainTime = 0.85f;
                upgradeFlash = 0f;
            }
            else if (upgrade > 0)
            {
                gainText.text = "+" + upgrade + " мест";
                gainTime = 1.1f;
                upgradeFlash = 1f;
            }
            if (displayedCarried < displayedCapacity && full) celebrate = 1f;
            displayedCarried = carried;
            displayedCapacity = cap;
        }

        expectedText = carried + "/" + cap;
        // The original HUD may have just rewritten this TMP on SetBackpack.
        if (count.text != expectedText) count.text = expectedText;
        count.color = full ? FullInk : Ink;
        shownRatio = reduce ? ratio : Mathf.Lerp(shownRatio, ratio, 1f - Mathf.Exp(-10f * dt));
        if (Mathf.Abs(shownRatio - ratio) < 0.0005f) shownRatio = ratio;

        for (int i = 0; i < QuarterCount; i++)
        {
            quarterFills[i].fillAmount = Mathf.Clamp01(shownRatio * QuarterCount - i);
            quarterFills[i].color = full ? AmberFull : Amber;
        }

        celebrate = Mathf.MoveTowards(celebrate, 0f, dt / 0.85f);
        float flash = reduce ? 0f : celebrate;
        frame.color = full
            ? new Color(1f, 0.76f, 0.33f, 0.78f + 0.19f * flash)
            : (ratio >= 0.75f
                ? new Color(0.94f, 0.66f, 0.31f, 0.82f)
                : new Color(Lavender.r, Lavender.g, Lavender.b, 0.84f));
        medal.color = full ? new Color(1f, 0.96f, 0.82f, 1f) : Color.white;

        // Avoid strong pulses during sustained 4/4 and when Reduce Effects is on.
        twinkle.color = new Color(1f, 0.81f, 0.24f, reduce ? 0f : flash * 0.95f);
        twinkle.rectTransform.localScale = Vector3.one * (1f + 0.35f * flash);

        if (gainTime > 0f) gainTime = Mathf.Max(0f, gainTime - dt);
        float duration = upgradeFlash > 0f ? 1.1f : 0.85f;
        float t = 1f - gainTime / duration;
        float alpha = gainTime <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(t * 10f, gainTime * 5f));
        gainText.color = upgradeFlash > 0f
            ? new Color(0.51f, 0.42f, 0.79f, alpha)
            : new Color(0.97f, 0.60f, 0.18f, alpha);
        gainText.rectTransform.anchoredPosition = new Vector2(-10f,
            -1f + (reduce ? 0f : t * 18f));
        if (gainTime <= 0f) upgradeFlash = 0f;

        // Keep the existing chopping/rhythm windows anchored above the shorter card.
        // PlayerHeadHud rewrites card's animation position before this LateUpdate.
        card.anchoredPosition = new Vector2(0f, TailHeight +
            (reduce ? 0f : Mathf.Sin(Time.unscaledTime * 2.2f) * 0.6f));
        chop.anchoredPosition = new Vector2(0f, TailHeight + Height + 8f);
        rhythm.anchoredPosition = new Vector2(0f, TailHeight + Height + 8f + 72f + 8f);
        if (shadow != null)
        {
            shadow.anchoredPosition = new Vector2(2f, card.anchoredPosition.y - 3f);
            shadow.localScale = card.localScale;
        }
    }
}

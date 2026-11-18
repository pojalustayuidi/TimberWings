using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sprite references live in Resources/TWPetCollectionSkin; no texture generation is needed.
[CreateAssetMenu(menuName = "TimberWings/UI/Pet collection skin")]
public sealed class TWPetCollectionSkin : ScriptableObject
{
    public Sprite window, surface, chip, speech;
    public Sprite paw, padlock, check, star, backpack, axe, boot, log, coin, saw;
    public Sprite[] gold = new Sprite[4], mint = new Sprite[4], close = new Sprite[4];
    public TMP_FontAsset font;

    static TWPetCollectionSkin current;
    public static TWPetCollectionSkin Current
    {
        get { if (current == null) current = Resources.Load<TWPetCollectionSkin>("TWPetCollectionSkin"); return current; }
    }

    public static readonly Color Ink = new Color32(49, 39, 95, 255);
    public static readonly Color Muted = new Color32(112, 105, 144, 255);
    public static readonly Color Good = new Color32(31, 116, 83, 255);
    public static readonly Color Warning = new Color32(176, 55, 87, 255);
    public static readonly Color Accent = new Color32(135, 91, 30, 255);
    public static readonly Color Selection = new Color32(149, 136, 229, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { current = null; }

    public static Color RarityInk(PetRarity rarity)
    {
        switch (rarity)
        {
            case PetRarity.Uncommon: return Good;
            case PetRarity.Rare: return new Color32(48, 99, 162, 255);
            case PetRarity.Legendary: return Accent;
            default: return Muted;
        }
    }

    public static Sprite Icon(UiIcon icon)
    {
        var s = Current;
        Sprite value = null;
        if (s != null)
            switch (icon)
            {
                case UiIcon.Paw: value = s.paw; break;
                case UiIcon.Lock: value = s.padlock; break;
                case UiIcon.Check: value = s.check; break;
                case UiIcon.Star: value = s.star; break;
                case UiIcon.Backpack: value = s.backpack; break;
                case UiIcon.Axe: value = s.axe; break;
                case UiIcon.Boot: value = s.boot; break;
                case UiIcon.Log: value = s.log; break;
                case UiIcon.Coin: value = s.coin; break;
                case UiIcon.Saw: value = s.saw; break;
            }
        return value != null ? value : UiKit.CandyIcon(icon);
    }

    public static TMP_Text Label(Transform parent, string text, float size, Color color,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
    {
        var s = Current;
        var label = UiKit.MakeLabel(parent, text, size, color, align, bold, s != null ? s.font : null);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    public static Image Surface(Transform parent, string name)
    {
        var s = Current;
        return UiKit.MakeImage(parent, name, s != null ? s.surface : UiKit.Slot, true, 0.65f);
    }

    public static Button Button(Transform parent, string text, UiBtn style, Vector2 size, float fontSize)
    {
        var image = Surface(parent, "Button_" + text);
        image.rectTransform.sizeDelta = size;
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Style(button, style);
        var label = Label(image.transform, text, fontSize, Ink);
        label.rectTransform.offsetMin = new Vector2(12f, 5f);
        label.rectTransform.offsetMax = new Vector2(-12f, -3f);
        return button;
    }

    public static void Style(Button button, UiBtn style)
    {
        var s = Current;
        if (button == null || s == null) return;
        Sprite[] sprites = style == UiBtn.Green ? s.mint : style == UiBtn.Yellow || style == UiBtn.Amber ? s.gold : null;
        var image = button.targetGraphic as Image;
        if (sprites != null && sprites.Length >= 4 && sprites[0] != null)
        {
            image.sprite = sprites[0];
            image.pixelsPerUnitMultiplier = 56f / Mathf.Max(1f, image.rectTransform.sizeDelta.y);
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = sprites[1], pressedSprite = sprites[2],
                selectedSprite = sprites[0], disabledSprite = sprites[3] };
        }
        else
        {
            image.sprite = s.surface;
            image.pixelsPerUnitMultiplier = 48f / Mathf.Max(1f, image.rectTransform.sizeDelta.y);
            image.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(0.94f, 0.93f, 1f);
            colors.pressedColor = new Color(0.85f, 0.83f, 0.96f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.89f, 0.89f, 0.94f);
            button.colors = colors;
        }
    }
}

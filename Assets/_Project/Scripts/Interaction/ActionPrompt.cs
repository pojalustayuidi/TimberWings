// Assets/_Project/Scripts/Interaction/ActionPrompt.cs (НЕ в Editor)
// Плашка внизу по центру: [E] Рубить (Candy: облачное поле, клавиша-кейкап, индиго-текст).
// Появляется, когда рядом есть дерево.
// Если рюкзак полон, пишет «Рюкзак полон», а клавиша краснеет и дёргается.
// Молчит, пока клавишей E владеет InteractionHub (разговор или NPC рядом): тогда подсказка живёт над NPC.
// Поля назначает BuilderSimHud: group, keyRect, keyImage, keyColor, keyText, labelText.
// Объекты плашки собирает редакторский билдер (его нет в архиве), поэтому вид Candy накладывается здесь, один раз в Start (ApplyCandy):
// меняются только спрайты и цвета тех же объектов, раскладка и ссылки остаются прежними.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionPrompt : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform keyRect;
    public Image keyImage;
    public Color keyColor = Color.white;     // обычный цвет клавиши (Candy: белый, клавиша-кейкап уже лимонная)
    public TMP_Text keyText;
    public TMP_Text labelText;

    [Header("Тексты")]
    public string chopLabel = "Рубить";
    public string fullLabel = "Рюкзак полон";
    public Color fullKeyColor = new Color(1f, 0.6f, 0.55f, 1f);

    PlayerInteraction interaction;
    BackpackTag backpack;
    float findTimer;
    float shown;          // 0..1
    float punch = 1f;
    string lastLabel;
    bool wasFull;
    bool candyApplied;

    // можно вызвать из других систем: ручной показ с любой клавишей и текстом
    string manualKey, manualLabel;
    bool manual;

    public void ShowManual(string key, string label) { manual = true; manualKey = key; manualLabel = label; }
    public void HideManual() { manual = false; }

    void Start()
    {
        ApplyCandy();
        if (group != null) group.alpha = 0f;
        Find();
    }

    // Candy поверх готовой плашки: фон = облачная панель, клавиша = кейкап, текст индиго круглым шрифтом, старые рамки гасим
    void ApplyCandy()
    {
        if (candyApplied) return;
        candyApplied = true;

        keyColor = Color.white;
        fullKeyColor = new Color(1f, 0.70f, 0.66f, 1f);     // «полон»: клавиша розовеет

        if (group != null)
        {
            Image bg = group.GetComponent<Image>();
            var imgs = group.GetComponentsInChildren<Image>(true);
            if (bg == null)
            {
                for (int i = 0; i < imgs.Length; i++)
                {
                    var im = imgs[i];
                    if (IsKeyPart(im)) continue;
                    if (im.type == Image.Type.Sliced) { bg = im; break; }
                }
            }
            if (bg != null) CandyTag.StylePanel(bg, 0.45f);

            // остальные скруглённые рамки и «выемки» старого стиля больше не нужны: фон уже облачный
            for (int i = 0; i < imgs.Length; i++)
            {
                var im = imgs[i];
                if (im == bg || IsKeyPart(im)) continue;
                if (im.type == Image.Type.Sliced) im.enabled = false;
            }
        }

        if (keyImage != null)
        {
            keyImage.sprite = CandyTag.Keycap;
            keyImage.type = Image.Type.Simple;
            keyImage.preserveAspect = true;
            keyImage.color = keyColor;
        }
        if (keyText != null)
        {
            CandyTag.Font(keyText, keyText.font);
            keyText.color = CandyTag.Ink;
            keyText.outlineWidth = 0f;
            keyText.fontStyle = FontStyles.Bold;
        }
        if (labelText != null)
        {
            CandyTag.Font(labelText, labelText.font);
            labelText.color = CandyTag.Ink;
            labelText.outlineWidth = 0f;
            labelText.fontStyle = FontStyles.Bold;
        }
    }

    bool IsKeyPart(Image im)
    {
        if (im == keyImage) return true;
        return keyRect != null && im.transform.IsChildOf(keyRect);
    }

    void Find()
    {
        if (interaction == null) interaction = FindFirstObjectByType<PlayerInteraction>();
        if (interaction != null && backpack == null) backpack = interaction.GetComponent<BackpackTag>();
    }

    void Update()
    {
        if (interaction == null)
        {
            findTimer -= Time.unscaledDeltaTime;
            if (findTimer <= 0f) { findTimer = 0.5f; Find(); }
        }

        bool has = false, full = false;
        string key = "E", label = chopLabel;

        if (manual)
        {
            has = !DialogueManager.IsBusy;      // во время реплик ручная плашка тоже прячется
            key = manualKey;
            label = manualLabel;
        }
        else if (interaction != null)
        {
            // E занята разговором или NPC рядом: про дерево молчим
            bool owned = InteractionHub.OwnsInput;
            has = !owned && interaction.CurrentTarget != null && !interaction.IsChopping;
            full = backpack != null && backpack.NoRoom;   // места нет вообще (с учётом летящих брёвен). Если свободен хоть 1 слот, рубить можно
            key = KeyName(interaction.chopKey);
            label = full ? fullLabel : chopLabel;

            if (has && full && Input.GetKeyDown(interaction.chopKey)) punch = 0f;
        }

        if (full && !wasFull) punch = 0f;
        wasFull = full;

        if (keyText != null && keyText.text != key) keyText.text = key;
        if (labelText != null && label != lastLabel) { labelText.text = label; lastLabel = label; }
        if (keyImage != null) keyImage.color = full ? fullKeyColor : keyColor;

        float dt = Time.unscaledDeltaTime;
        shown = Mathf.MoveTowards(shown, has ? 1f : 0f, dt / 0.18f);
        if (group != null) group.alpha = shown;

        float s = Mathf.Lerp(0.88f, 1f, UiDraw.EaseOutBack(shown));
        transform.localScale = Vector3.one * s;

        if (keyRect != null)
        {
            punch = Mathf.Min(1f, punch + dt / 0.3f);
            float shake = Mathf.Sin(punch * Mathf.PI * 6f) * (1f - punch) * 6f;
            bool held = interaction != null && has && !full && Input.GetKey(interaction.chopKey);
            keyRect.localScale = Vector3.one * (held ? 0.9f : 1f);
            keyRect.localRotation = Quaternion.Euler(0f, 0f, shake);
        }
    }

    static string KeyName(KeyCode k)
    {
        string s = k.ToString();
        if (s.StartsWith("Alpha")) s = s.Substring(5);
        return s;
    }
}

// TimberWings: non-destructive Leo/Rico portrait compatibility layer.
// No scene rebuild and no modifications to the existing NpcDialoguePortraits component.
using System;
using UnityEngine;
using UnityEngine.UI;

// The bootstrap also handles a DialoguePolishView that is instantiated after scene load.
public sealed class TWPortraitAutoInstaller : MonoBehaviour
{
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<TWPortraitAutoInstaller>() != null) return;
        var obj = new GameObject("TW_DialoguePortraitAutoInstaller");
        DontDestroyOnLoad(obj);
        obj.AddComponent<TWPortraitAutoInstaller>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1.0f;
        foreach (var view in FindObjectsByType<DialoguePolishView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (view == null || view.gameObject.GetComponent<TWPortraitRuntimeFix>() != null) continue;
            view.gameObject.AddComponent<TWPortraitRuntimeFix>();
        }
    }
}

// Run after NpcDialoguePortraits.LateUpdate (execution order 410).
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class TWPortraitRuntimeFix : MonoBehaviour
{
    private DialoguePolishView view;
    private Image portrait;
    private Sprite leo;
    private Sprite rico;
    private bool initialized;
    private bool warnedLeo;
    private bool warnedRico;

    private void Awake()
    {
        view = GetComponent<DialoguePolishView>();
    }

    private void OnDisable()
    {
        if (portrait != null) portrait.enabled = false;
    }

    private void LateUpdate()
    {
        if (view == null || view.avatarBack == null || view.speakerName == null || view.group == null) return;
        if (view.group.alpha <= 0.01f)
        {
            if (portrait != null) portrait.enabled = false;
            return;
        }

        string name = (view.speakerName.text ?? "").Trim();
        bool isLeo = string.Equals(name, "Лео", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, "Leo", StringComparison.OrdinalIgnoreCase);
        bool isRico = string.Equals(name, "Рико", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(name, "Rico", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(name, "Торговец", StringComparison.OrdinalIgnoreCase);
        if (!isLeo && !isRico)
        {
            if (portrait != null) portrait.enabled = false;
            // Other characters are owned by the original portrait system.
            if (view.emblem != null) view.emblem.enabled = view.emblem.sprite != null;
            return;
        }

        if (!initialized)
        {
            initialized = true;
            leo = LoadPortrait("DialoguePortraits/Leo");
            rico = LoadPortrait("DialoguePortraits/Rico");
            if (rico == null) rico = LoadPortrait("Rico/RicoPortrait");
        }

        Sprite target = isLeo ? leo : rico;
        if (target == null)
        {
            if (isLeo && !warnedLeo)
            {
                warnedLeo = true;
                Debug.LogWarning("[TW Portrait Fix] Leo.png not loaded. Run Tools > TimberWings > Dialogue Portraits > Repair/Verify, and check Assets/_Project/Resources/DialoguePortraits/Leo.png.");
            }
            if (isRico && !warnedRico)
            {
                warnedRico = true;
                Debug.LogWarning("[TW Portrait Fix] Rico.png not loaded. Run Tools > TimberWings > Dialogue Portraits > Repair/Verify, and check Assets/_Project/Resources/DialoguePortraits/Rico.png.");
            }
            if (portrait != null) portrait.enabled = false;
            return;
        }

        EnsureImage();
        portrait.sprite = target;
        portrait.enabled = true;
        // This overlay replaces the small item-class emblem for Leo and Rico.
        if (view.emblem != null) view.emblem.enabled = false;
        if (view.monogram != null) view.monogram.enabled = false;
    }

    private void EnsureImage()
    {
        if (portrait != null) return;
        var badge = view.avatarBack.rectTransform;
        Transform existing = badge.Find("TW_CharacterPortrait");
        if (existing != null) portrait = existing.GetComponent<Image>();
        if (portrait == null)
        {
            var obj = new GameObject("TW_CharacterPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.layer = badge.gameObject.layer;
            obj.transform.SetParent(badge, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            portrait = obj.GetComponent<Image>();
        }
        portrait.type = Image.Type.Simple;
        portrait.color = Color.white;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.transform.SetAsLastSibling(); // On top of existing monogram/emblem.
    }

    private static Sprite LoadPortrait(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null) return sprite;
        // Recovery for PNG imported as Default rather than Sprite. Editor repair is preferred.
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return null;
        try
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TW Portrait Fix] Could not create sprite for " + resourcePath + ": " + exception.Message);
            return null;
        }
    }
}

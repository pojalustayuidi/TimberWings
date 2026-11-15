// TimberWings — BuildWindow visual polish (2026-11)
// Visual-only extension to the existing partial BuildWindow.
// NO edits to BuildWindow.cs, BuildWindow.Build.cs, BuildWindow.Fill.cs,
// BuildingController, BuildSiteData, sprites, saving, timers or interactions.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildWindow
{
    bool twPolishReady;
    RectTransform twPolishedTarget;

    // Apply after Build() has constructed the existing hierarchy.
    // LateUpdate is separate from the original Update(), so the original
    // update, modal control, progress animation, hotkeys and actions are intact.
    void LateUpdate()
    {
        if (!built || body == null) return;
        if (!twPolishReady)
        {
            ApplyTWPolish();
            twPolishReady = true;
        }
        // Nodes may be rebuilt when switching between construction sites.
        // This only adds an appearance accent to the active stage.
        if (targetNode != twPolishedTarget)
        {
            twPolishedTarget = targetNode;
            if (targetNode != null) AddTWStageGlow(targetNode);
        }
    }

    void ApplyTWPolish()
    {
        // Subtler, calmer cards with the same Candy palette and positions.
        var info = body.Find("Info") as RectTransform;
        var need = body.Find("Need") as RectTransform;
        if (info != null)
        {
            SetTWPanel(info, new Color(0.957f, 0.952f, 1f, 0.98f));
            AddTWTopRule(info, new Color(0.68f, 0.56f, 0.95f, 0.70f));
            // Keep the user's existing building icon and its slot.
            var iconSlot = info.Find("BuildingSlot") as RectTransform;
            if (iconSlot != null) AddTWIconAccent(iconSlot);
        }
        if (need != null)
        {
            SetTWPanel(need, new Color(0.955f, 0.970f, 1f, 0.98f));
            AddTWTopRule(need, new Color(0.44f, 0.70f, 0.94f, 0.72f));
            // Clear hierarchy under the title, still leaving the existing
            // resource icons, labels, values and progress bars untouched.
            AddTWRule(need, "TW_NeedHeaderRule", 24f, 67f, 574f,
                new Color(0.57f, 0.61f, 0.85f, 0.28f));
        }
        if (statusCard != null)
        {
            AddTWShadow(statusCard.gameObject, 0.10f, -3f);
            AddTWTopRule(statusCard.rectTransform, new Color(0.61f, 0.76f, 0.76f, 0.45f));
        }

        // Individual row surfaces emphasize resource progress without
        // inventing new resource icons or changing what the game displays.
        PolishTWRow(rowWood);
        PolishTWRow(rowPlank);
        PolishTWRow(rowCoin);

        // Improve legibility and whitespace without changing source strings.
        if (titleText != null)
        {
            titleText.fontSize = Mathf.Min(titleText.fontSize, 38f);
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 24f;
            titleText.fontSizeMax = 38f;
        }
        if (stageCaption != null) stageCaption.fontSize = 23f;
        if (stageName != null)
        {
            stageName.fontSize = 30f;
            stageName.enableAutoSizing = true;
            stageName.fontSizeMin = 21f;
            stageName.fontSizeMax = 30f;
        }
        if (stageDesc != null)
        {
            stageDesc.fontSize = 22f;
            stageDesc.fontSizeMax = 22f;
            stageDesc.lineSpacing = 2f;
            stageDesc.color = new Color(0.32f, 0.35f, 0.57f, 1f);
        }
        if (needTitle != null) needTitle.fontSize = 28f;
        if (backpackText != null) backpackText.fontSize = 20f;
        if (statusText != null) statusText.fontSize = 28f;

        AddTWButtonFinish(deposit != null ? deposit.btn : null, false);
        AddTWButtonFinish(play != null ? play.btn : null, true);
        if (stripRt != null)
        {
            var stripPanel = stripRt.Find("StripPanel");
            if (stripPanel != null) AddTWShadow(stripPanel.gameObject, 0.12f, -3f);
        }
    }

    static void SetTWPanel(RectTransform rect, Color tint)
    {
        var image = rect.GetComponent<Image>();
        if (image != null) image.color = tint;
        AddTWShadow(rect.gameObject, 0.12f, -4f);
    }

    static void AddTWShadow(GameObject go, float alpha, float y)
    {
        if (go == null || go.GetComponent<Shadow>() != null) return;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.18f, 0.16f, 0.42f, alpha);
        shadow.effectDistance = new Vector2(0f, y);
        shadow.useGraphicAlpha = true;
    }

    static Image TWImage(Transform parent, string name, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = UiDraw.Pill;
        image.type = image.sprite != null && image.sprite.border.sqrMagnitude > 0.01f
            ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    static void TWTopRect(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-left - right, height);
    }

    static void AddTWTopRule(RectTransform parent, Color tint)
    {
        if (parent.Find("TW_TopAccent") != null) return;
        var a = TWImage(parent, "TW_TopAccent", tint);
        TWTopRect(a.rectTransform, 24f, 24f, 9f, 5f);
        a.transform.SetSiblingIndex(0); // behind all existing controls
    }

    static void AddTWRule(Transform parent, string name, float left, float top,
        float width, Color tint)
    {
        if (parent.Find(name) != null) return;
        var image = TWImage(parent, name, tint);
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, 2f);
        image.transform.SetSiblingIndex(0);
    }

    static void PolishTWRow(Row row)
    {
        if (row == null || row.root == null || row.root.Find("TW_ResourceSurface") != null) return;
        var bg = TWImage(row.root, "TW_ResourceSurface",
            new Color(1f, 1f, 1f, 0.48f));
        var rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-10f, -3f);
        rt.offsetMax = new Vector2(10f, 3f);
        bg.transform.SetSiblingIndex(0);

        if (row.count != null)
        {
            row.count.enableAutoSizing = true;
            row.count.fontSizeMin = 16f;
            row.count.fontSizeMax = 26f;
        }
    }

    static void AddTWIconAccent(RectTransform slot)
    {
        if (slot.Find("TW_IconSoftGlow") != null) return;
        var image = TWImage(slot, "TW_IconSoftGlow",
            new Color(1f, 0.93f, 0.70f, 0.20f));
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(154f, 154f);
        image.transform.SetSiblingIndex(0);
    }

    static void AddTWButtonFinish(Button button, bool primary)
    {
        if (button == null) return;
        AddTWShadow(button.gameObject, primary ? 0.22f : 0.13f, -5f);
        if (button.transform.Find("TW_ButtonShine") != null) return;
        var image = TWImage(button.transform, "TW_ButtonShine",
            new Color(1f, 1f, 1f, primary ? 0.22f : 0.15f));
        var rt = image.rectTransform;
        rt.anchorMin = new Vector2(0.07f, 0.76f);
        rt.anchorMax = new Vector2(0.93f, 0.91f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        image.transform.SetSiblingIndex(0);
    }

    static void AddTWStageGlow(RectTransform target)
    {
        if (target == null || target.Find("TW_CurrentStageGlow") != null) return;
        var halo = TWImage(target, "TW_CurrentStageGlow",
            new Color(1f, 0.78f, 0.36f, 0.32f));
        var rt = halo.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(60f, 60f);
        halo.transform.SetSiblingIndex(0);
    }
}

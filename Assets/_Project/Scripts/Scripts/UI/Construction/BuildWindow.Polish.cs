// TimberWings — BuildWindow visual polish v2.
// Replace the previous BuildWindow.Polish.cs with this file.
// Visual changes only: no gameplay logic, inventory, cost, save, input or build code.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildWindow
{
    bool twV2Ready;
    RectTransform twV2Info;
    RectTransform twV2Need;
    Image twV2TipPanel;
    TMP_Text twV2TipTitle;
    TMP_Text twV2TipBody;
    TMP_Text twV2StatusSubtitle;
    RectTransform twV2EmphasizedStage;
    int twV2LastTipState = int.MinValue;

    // Called by the separate TWBuildWindowPolishDriver in LateUpdate.
    // BuildWindow is a partial class; declaring LateUpdate here would conflict
    // with another extension that also has a LateUpdate method.
    internal void TWV2VisualTick()
    {
        if (!built || body == null) return;
        if (!twV2Ready)
        {
            if (!TWV2Create()) return;
            twV2Ready = true;
        }
        if (!open || collapsed || bc == null) return;
        TWV2Refresh();
    }

    bool TWV2Create()
    {
        twV2Info = body.Find("Info") as RectTransform;
        twV2Need = body.Find("Need") as RectTransform;
        if (twV2Info == null || twV2Need == null) return false;

        // The existing Candy palette and existing building/resource sprites are kept.
        TWV2Panel(twV2Info, new Color(0.960f, 0.953f, 1f, 1f), 0.19f);
        TWV2Panel(twV2Need, new Color(0.948f, 0.963f, 1f, 1f), 0.19f);
        if (panel != null) TWV2Shadow(panel.gameObject, 0.29f, -9f);
        if (overlay != null)
        {
            var shade = overlay.GetComponent<Image>();
            if (shade != null) shade.color = new Color(0.14f, 0.15f, 0.32f, 0.47f);
            var hint = overlay.transform.Find("HintPill") as RectTransform;
            if (hint != null)
            {
                var hintImage = hint.GetComponent<Image>();
                if (hintImage != null) hintImage.color = new Color(0.98f, 0.97f, 1f, 0.97f);
                TWV2Shadow(hint.gameObject, 0.17f, -3f);
                hint.sizeDelta = new Vector2(860f, 48f);
                var hintLabel = hint.GetComponentInChildren<TMP_Text>(true);
                if (hintLabel != null)
                {
                    hintLabel.text = "Enter — начать   ·   Пробел — сдать ресурсы   ·   E / Esc — закрыть";
                    hintLabel.color = new Color(0.35f, 0.35f, 0.59f, 1f);
                    hintLabel.fontSize = 21f;
                    hintLabel.enableAutoSizing = true;
                    hintLabel.fontSizeMin = 18f;
                    hintLabel.fontSizeMax = 21f;
                    hintLabel.alignment = TextAlignmentOptions.Center;
                }
            }
        }
        if (titleText != null)
        {
            titleText.fontSize = 37f;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 25f;
            titleText.fontSizeMax = 37f;
        }

        var infoRule = TWV2Image(twV2Info, "TWV2_LeftTopAccent", new Color(0.63f, 0.54f, 0.89f, 0.90f));
        TWV2At(infoRule.rectTransform, 24f, 13f, 352f, 5f);
        infoRule.transform.SetSiblingIndex(0);

        // Raised illustration socket, but the original icon itself is not replaced.
        var iconSlot = twV2Info.Find("BuildingSlot") as RectTransform;
        if (iconSlot != null)
        {
            TWV2Shadow(iconSlot.gameObject, 0.16f, -4f);
            var light = TWV2Image(iconSlot, "TWV2_IconGlow", new Color(1f, 0.92f, 0.75f, 0.22f));
            TWV2Center(light.rectTransform, 165f, 146f);
            light.transform.SetSiblingIndex(0);
        }
        if (bigIcon != null) bigIcon.rectTransform.localScale = Vector3.one * 1.05f;

        // A distinct step-description card instead of free-floating text.
        var detail = TWV2Image(twV2Info, "TWV2_StepCard", new Color(1f, 1f, 1f, 0.83f));
        TWV2At(detail.rectTransform, 20f, 344f, 360f, 207f);
        TWV2Outline(detail.gameObject, new Color(0.71f, 0.68f, 0.88f, 0.55f), 1.2f);
        TWV2Shadow(detail.gameObject, 0.11f, -3f);
        // Render it behind the real step text, which remains fully functional.
        if (stageName != null) detail.transform.SetSiblingIndex(stageName.transform.GetSiblingIndex());

        var detailAccent = TWV2Image(detail.transform, "TWV2_StepAccent", new Color(0.96f, 0.77f, 0.33f, 0.90f));
        TWV2At(detailAccent.rectTransform, 17f, 16f, 5f, 35f);
        if (stageName != null)
        {
            stageName.fontSize = 30f;
            stageName.enableAutoSizing = true;
            stageName.fontSizeMin = 21f;
            stageName.fontSizeMax = 30f;
            stageName.color = new Color(0.20f, 0.19f, 0.39f, 1f);
            // Card now starts at 344; leave breathing room at its top.
            TWV2At(stageName.rectTransform, 46f, 357f, 309f, 44f);
            stageName.alignment = TextAlignmentOptions.Center;
        }
        if (stageDesc != null)
        {
            TWV2At(stageDesc.rectTransform, 42f, 412f, 315f, 119f);
            stageDesc.fontSize = 23f;
            stageDesc.enableAutoSizing = true;
            stageDesc.fontSizeMin = 17f;
            stageDesc.fontSizeMax = 23f;
            stageDesc.color = new Color(0.32f, 0.34f, 0.55f, 1f);
            stageDesc.alignment = TextAlignmentOptions.Top;
            stageDesc.lineSpacing = 2f;
        }
        if (stageCaption != null)
        {
            stageCaption.fontSize = 23f;
            stageCaption.color = new Color(0.36f, 0.35f, 0.62f, 1f);
        }

        // Original header label and original resource rows remain; they get raised surfaces.
        var needHeader = TWV2Image(twV2Need, "TWV2_NeedHeader", new Color(0.84f, 0.86f, 1f, 0.72f));
        TWV2At(needHeader.rectTransform, 15f, 11f, 592f, 54f);
        TWV2Outline(needHeader.gameObject, new Color(0.59f, 0.64f, 0.90f, 0.30f), 1f);
        needHeader.transform.SetSiblingIndex(0);
        if (needTitle != null)
        {
            TWV2At(needTitle.rectTransform, 31f, 18f, 545f, 41f);
            needTitle.fontSize = 29f;
            needTitle.color = new Color(0.20f, 0.21f, 0.43f, 1f);
        }
        TWV2ResourceRow(rowWood);
        TWV2ResourceRow(rowPlank);
        TWV2ResourceRow(rowCoin);

        // An adaptive helper fills the otherwise empty space when only
        // one or two materials are required; it disappears for three rows.
        twV2TipPanel = TWV2Image(twV2Need, "TWV2_TipPanel", new Color(0.995f, 0.993f, 1f, 0.93f));
        TWV2At(twV2TipPanel.rectTransform, 24f, 192f, 574f, 127f);
        TWV2Outline(twV2TipPanel.gameObject, new Color(0.71f, 0.72f, 0.91f, 0.48f), 1.1f);
        TWV2Shadow(twV2TipPanel.gameObject, 0.09f, -3f);
        var tipMark = TWV2Image(twV2TipPanel.transform, "TWV2_TipMark", new Color(0.98f, 0.77f, 0.42f, 1f));
        TWV2At(tipMark.rectTransform, 17f, 19f, 5f, 42f);
        twV2TipTitle = TWV2Label(twV2TipPanel.transform, "Как продолжить", 23f,
            new Color(0.26f, 0.25f, 0.49f, 1f), true);
        TWV2At(twV2TipTitle.rectTransform, 37f, 13f, 517f, 34f);
        twV2TipBody = TWV2Label(twV2TipPanel.transform, "", 20f,
            new Color(0.38f, 0.39f, 0.62f, 1f), false);
        TWV2At(twV2TipBody.rectTransform, 37f, 50f, 515f, 67f);
        twV2TipBody.enableWordWrapping = true;
        twV2TipBody.enableAutoSizing = true;
        twV2TipBody.fontSizeMin = 16f;
        twV2TipBody.fontSizeMax = 20f;

        if (backpackText != null)
        {
            backpackText.fontSize = 20f;
            backpackText.color = new Color(0.41f, 0.43f, 0.65f, 1f);
        }

        if (statusCard != null)
        {
            TWV2Shadow(statusCard.gameObject, 0.16f, -4f);
            TWV2Outline(statusCard.gameObject, new Color(0.78f, 0.72f, 0.51f, 0.32f), 1f);
            twV2StatusSubtitle = TWV2Label(statusCard.transform, "", 20f,
                new Color(0.43f, 0.42f, 0.62f, 1f), false);
            TWV2At(twV2StatusSubtitle.rectTransform, 24f, 73f, 574f, 37f);
            twV2StatusSubtitle.alignment = TextAlignmentOptions.Center;
            twV2StatusSubtitle.enableAutoSizing = true;
            twV2StatusSubtitle.fontSizeMin = 16f;
            twV2StatusSubtitle.fontSizeMax = 20f;
        }
        if (statusText != null)
        {
            statusText.fontSize = 29f;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 22f;
            statusText.fontSizeMax = 29f;
        }
        if (play != null) TWV2Button(play.btn, true);
        if (deposit != null) TWV2Button(deposit.btn, false);
        if (stripRt != null)
        {
            var bar = stripRt.Find("StripPanel");
            if (bar != null) TWV2Shadow(bar.gameObject, 0.18f, -4f);
        }
        return true;
    }

    void TWV2Refresh()
    {
        // A generic scene object name should not appear in player-facing UI.
        string currentTitle = bc.BuildingTitle;
        string shownTitle = currentTitle;
        bool generic = string.IsNullOrEmpty(shownTitle) || shownTitle == "BuildZone" ||
                       shownTitle == "BuildSite" || shownTitle.StartsWith("BuildZone (");
        if (generic)
        {
            if (bc.data != null && !string.IsNullOrEmpty(bc.data.buildingName))
                shownTitle = bc.data.buildingName;
            else
                shownTitle = "Строительство";
        }
        // Avoid geometry rebuilds, while retaining native existing title/icon logic.
        if (titleText != null && titleText.text != shownTitle)
        {
            titleText.text = shownTitle;
            if (headerIconRt != null)
            {
                float w = Mathf.Min(titleText.GetPreferredValues(shownTitle).x, 330f);
                headerIconRt.anchoredPosition = new Vector2(WinW * 0.5f - w * 0.5f - 34f, -27f);
            }
        }

        // Rename only the player-facing construction action, not DoPlay().
        if (play != null && play.txt != null && !bc.StageRunning && !bc.IsFinished && play.txt.text == "Играть")
            play.txt.text = "Начать";
        if (stageDesc != null && !string.IsNullOrEmpty(stageDesc.text) &&
            stageDesc.text.Contains("нажми «Играть»"))
            stageDesc.text = stageDesc.text.Replace("нажми «Играть»", "нажми «Начать»");
        if (statusText != null && !string.IsNullOrEmpty(statusText.text) &&
            statusText.text.Contains("«Играть»"))
            statusText.text = statusText.text.Replace("«Играть»", "«Начать»");

        // Focus decoration follows dynamically rebuilt nodes (1, 2, 3... stages).
        if (targetNode != twV2EmphasizedStage)
        {
            twV2EmphasizedStage = targetNode;
            if (targetNode != null && targetNode.Find("TWV2_Glow") == null)
            {
                var glow = TWV2Image(targetNode, "TWV2_Glow", new Color(1f, 0.78f, 0.39f, 0.35f));
                TWV2Center(glow.rectTransform, 61f, 61f);
                glow.transform.SetSiblingIndex(0);
            }
        }

        // Resource row visibility is already controlled by BuildWindow.Fill.cs.
        // Move the helper into the space that would otherwise be empty.
        int rows = 0;
        if (bc.CostTotal > 0) rows++;
        if (bc.PlankTotal > 0) rows++;
        if (bc.CoinTotal > 0) rows++;
        bool showTip = rows < 3 && !bc.StageRunning && !bc.IsFinished;
        if (twV2TipPanel != null)
        {
            if (twV2TipPanel.gameObject.activeSelf != showTip)
                twV2TipPanel.gameObject.SetActive(showTip);
            if (showTip)
            {
                TWV2At(twV2TipPanel.rectTransform, 24f, rows == 2 ? 276f : 192f, 574f,
                    rows == 2 ? 60f : 127f);
                twV2TipTitle.gameObject.SetActive(rows != 2);
                TWV2At(twV2TipBody.rectTransform, 37f, rows == 2 ? 10f : 50f,
                    515f, rows == 2 ? 43f : 67f);
                // Compact value state avoids TMP string allocation every frame.
                int key = bc.Deposited * 17 + bc.DepositedPlanks * 37 +
                    bc.DepositedCoins * 59 + bc.CostTotal * 71 + bc.PlankTotal * 83 +
                    bc.CoinTotal * 103 + (bc.CanDeposit(inv) ? 1 : 0) + (bc.AllDeposited ? 4096 : 0);
                if (key != twV2LastTipState)
                {
                    twV2LastTipState = key;
                    if (bc.AllDeposited)
                    {
                        twV2TipTitle.text = "Всё готово!";
                        twV2TipBody.text = "Материалы уже сданы. Нажми «Начать», чтобы перейти к строительству.";
                    }
                    else if (bc.CanDeposit(inv))
                    {
                        twV2TipTitle.text = "Материалы в рюкзаке";
                        twV2TipBody.text = "Часть ресурсов уже с тобой. Нажми «Сдать ресурсы» и продолжай собирать остальные.";
                    }
                    else if (rows == 0)
                    {
                        twV2TipTitle.text = "Можно начинать";
                        twV2TipBody.text = "Для этого этапа материалы не требуются — можно переходить к строительству.";
                    }
                    else
                    {
                        twV2TipTitle.text = "Как продолжить";
                        twV2TipBody.text = "Собери недостающие материалы в мире и принеси их сюда. Ресурсы можно сдавать по частям.";
                    }
                }
            }
        }

        if (twV2StatusSubtitle != null)
        {
            bool showSub = !bc.StageRunning && !bc.IsFinished;
            if (twV2StatusSubtitle.gameObject.activeSelf != showSub)
                twV2StatusSubtitle.gameObject.SetActive(showSub);
            if (showSub)
            {
                string subtitle = bc.AllDeposited ? "Все материалы готовы — можно строить"
                    : bc.CanDeposit(inv) ? "Материалы можно сдать прямо сейчас"
                    : "Материалы можно приносить несколькими заходами";
                if (twV2StatusSubtitle.text != subtitle) twV2StatusSubtitle.text = subtitle;
            }
        }
        // Keep disabled buttons legible while they remain genuinely non-interactable.
        if (play != null && play.txt != null && !play.btn.interactable)
            play.txt.color = new Color(0.37f, 0.39f, 0.57f, 1f);
        if (deposit != null && deposit.txt != null && !deposit.btn.interactable)
            deposit.txt.color = new Color(0.40f, 0.40f, 0.58f, 1f);
    }

    static void TWV2ResourceRow(Row row)
    {
        if (row == null || row.root == null || row.root.Find("TWV2_RowSurface") != null) return;
        var bg = TWV2Image(row.root, "TWV2_RowSurface", new Color(1f, 1f, 1f, 0.78f));
        TWV2Stretch(bg.rectTransform, -8f, -4f, 8f, 4f);
        bg.transform.SetSiblingIndex(0);
        TWV2Outline(bg.gameObject, new Color(0.74f, 0.75f, 0.92f, 0.48f), 1f);
        TWV2Shadow(bg.gameObject, 0.11f, -2f);
        if (row.count != null)
        {
            row.count.enableAutoSizing = true;
            row.count.fontSizeMin = 18f;
            row.count.fontSizeMax = 28f;
        }
    }

    static void TWV2Button(Button button, bool primary)
    {
        if (button == null) return;
        TWV2Shadow(button.gameObject, primary ? 0.24f : 0.16f, primary ? -6f : -4f);
        var shine = TWV2Image(button.transform, "TWV2_ButtonShine", new Color(1f, 1f, 1f, primary ? 0.18f : 0.12f));
        var rt = shine.rectTransform;
        rt.anchorMin = new Vector2(0.08f, 0.76f);
        rt.anchorMax = new Vector2(0.92f, 0.88f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        shine.transform.SetSiblingIndex(0);
        var colors = button.colors;
        colors.disabledColor = primary
            ? new Color(0.86f, 0.94f, 0.92f, 1f)
            : new Color(0.96f, 0.91f, 0.82f, 1f);
        button.colors = colors;
    }

    static void TWV2Panel(RectTransform rect, Color tint, float shadowStrength)
    {
        var image = rect.GetComponent<Image>();
        if (image != null) image.color = tint;
        TWV2Shadow(rect.gameObject, shadowStrength, -5f);
        TWV2Outline(rect.gameObject, new Color(0.67f, 0.66f, 0.88f, 0.45f), 1.5f);
    }

    static void TWV2Shadow(GameObject go, float alpha, float y)
    {
        if (go == null || go.GetComponent<Shadow>() != null) return;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.20f, 0.18f, 0.41f, alpha);
        shadow.effectDistance = new Vector2(0f, y);
        shadow.useGraphicAlpha = true;
    }

    static void TWV2Outline(GameObject go, Color color, float distance)
    {
        if (go == null || go.GetComponent<Outline>() != null) return;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    static Image TWV2Image(Transform parent, string objName, Color tint)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.sprite = UiDraw.Pill;
        im.type = im.sprite != null && im.sprite.border.sqrMagnitude > 0.01f
            ? Image.Type.Sliced : Image.Type.Simple;
        im.color = tint;
        im.raycastTarget = false;
        return im;
    }

    static TMP_Text TWV2Label(Transform parent, string value, float size, Color color, bool bold)
    {
        var text = UiKit.MakeLabel(parent, value, size, color, TextAlignmentOptions.Left, bold);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    static void TWV2At(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void TWV2Center(RectTransform rect, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    static void TWV2Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}

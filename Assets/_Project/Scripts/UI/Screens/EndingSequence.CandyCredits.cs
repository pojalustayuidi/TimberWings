using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UiLayout;

public partial class EndingSequence
{
    static readonly Color CreditWhite = new Color32(251, 247, 240, 255);
    static readonly Color CreditLilac = new Color32(206, 200, 248, 255);
    static readonly Color CreditMint = new Color32(174, 236, 220, 255);
    static readonly Color CreditPeach = new Color32(255, 212, 172, 255);

    TMP_FontAsset portalFont;
    CanvasGroup candyBackdrop, candyCard;
    Sprite cloudSprite;
    Texture2D cloudTexture;
    RectTransform[] backdropClouds;

    struct CreditBlock
    {
        internal CanvasGroup Group;
        internal float Center;
    }

    IEnumerator CandyCredits()
    {
        EnsureCandyBackdrop();
        yield return FadeCandyBackdrop(1f, .67f);
        if (skip) yield break;

        string priorHint = hint.text;
        hint.text = Fix("Пробел или клик ускоряет титры · удерживайте Esc для пропуска");
        var viewport = NewRT("CreditsViewport", root);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(0f, 84f);
        viewport.offsetMax = new Vector2(0f, -46f);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.SetSiblingIndex(hint.transform.GetSiblingIndex());

        const float height = 1028f;
        var roll = NewRT("CreditRoll", viewport);
        roll.anchorMin = new Vector2(.11f, 0f);
        roll.anchorMax = new Vector2(.89f, 0f);
        roll.pivot = new Vector2(.5f, 1f);
        roll.sizeDelta = new Vector2(0f, height);
        roll.anchoredPosition = Vector2.zero;

        var blocks = new CreditBlock[5];
        var block = CreditRow(roll, 0f, 224f, out blocks[0]);
        var icon = UiCandy.Icon(block, UiIcon.Axe, 59f);
        Anch(icon.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, .5f), new Vector2(0f, -43f), new Vector2(59f, 59f));
        CreditLine(block, GameTitle, 130f, 86f, 92f, CreditWhite, true);
        CreditLine(block, "ТИТРЫ", 202f, 26f, 41f, CreditMint, true, 5f);

        block = CreditRow(roll, 278f, 145f, out blocks[1]);
        CreditLine(block, "НАД ИГРОЙ РАБОТАЛ", 18f, 25f, 36f, CreditPeach, true, 3f);
        CreditLine(block, CreditAuthor, 68f, 68f, 80f, CreditWhite, true);
        CreditLine(block, CreditGroup, 122f, 29f, 38f, CreditLilac, false);

        block = CreditRow(roll, 492f, 139f, out blocks[2]);
        CreditLine(block, CreditSchool, 43f, 48f, 96f, CreditWhite, true);
        CreditLine(block, CreditProgram, 108f, 32f, 51f, CreditLilac, false);

        block = CreditRow(roll, 697f, 106f, out blocks[3]);
        CreditLine(block, "СДЕЛАНО НА", 22f, 25f, 36f, CreditMint, true, 3f);
        CreditLine(block, CreditEngine, 72f, 49f, 64f, CreditWhite, true);

        block = CreditRow(roll, 878f, 148f, out blocks[4]);
        CreditLine(block, CreditThanks, 46f, 49f, 88f, CreditWhite, true);
        CreditLine(block, CreditSignoff, 113f, 30f, 50f, CreditLilac, false);

        advance = false;
        float y = 0f, fast = 0f;
        while (y < viewport.rect.height + height && !skip)
        {
            if (advance)
            {
                fast = .72f;
                advance = false;
            }
            fast = Mathf.Max(0f, fast - Dt);
            bool held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetMouseButton(0);
            y += 146f * (held || fast > 0f ? 2.8f : 1f) * Dt;
            roll.anchoredPosition = new Vector2(0f, y);
            foreach (var row in blocks)
            {
                float center = y - row.Center;
                float fade = reduceFx ? 36f : 88f;
                row.Group.alpha = Mathf.SmoothStep(0f, 1f, center / fade) *
                    Mathf.SmoothStep(0f, 1f, (viewport.rect.height - center) / fade);
            }
            yield return null;
        }
        viewport.gameObject.SetActive(false);
        Destroy(viewport.gameObject);
        hint.text = priorHint;
        advance = false;
    }

    static RectTransform CreditRow(RectTransform parent, float top, float height, out CreditBlock row)
    {
        var rect = NewRT("Credit", parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        row = new CreditBlock { Group = group, Center = top + height * .5f };
        return rect;
    }

    void CreditLine(RectTransform parent, string text, float top, float size, float height, Color color, bool bold,
        float spacing = 0f)
    {
        var label = UiKit.MakeLabel(parent, text, size, color, TextAlignmentOptions.Center, bold, portalFont);
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
        label.characterSpacing = spacing;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .56f;
        label.fontSizeMax = size;
        label.textWrappingMode = TextWrappingModes.Normal;
    }

    IEnumerator CandyFinalCard()
    {
        ClearAll();
        emberRate = 0f;
        glowTarget = 0f;
        EnsureCandyBackdrop();
        finalShown = true;
        if (candyBackdrop.alpha < 1f) yield return FadeCandyBackdrop(1f, .55f);

        var card = NewRT("FinaleCard", root);
        Anch(card, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, -20f), new Vector2(1060f, 424f));
        candyCard = card.gameObject.AddComponent<CanvasGroup>();
        candyCard.alpha = 0f;
        candyCard.blocksRaycasts = false;

        using (UiKit.UseSkin(UiSkin.Candy))
        {
            var panel = UiKit.MakePanel(card, card.sizeDelta, false, 1.45f, "CloudCard");
            Stretch(panel.rectTransform);
            var badge = UiKit.MakeImage(card, "CrystalMedallion", UiDraw.Pill, true);
            badge.color = UiCandy.Lavender;
            Anch(badge.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, .5f),
                new Vector2(0f, -12f), new Vector2(98f, 98f));
            var gem = UiCandy.Icon(badge.transform, UiIcon.Gem, 59f);
            gem.color = UiCandy.MintLight;
            Anch(gem.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(57f, 57f));

            CreditLine(card, "ГЛАВА II · ПОД ЗЕМЛЁЙ", 68f, 24f, 34f, UiCandy.Muted, true, 3f);
            CreditLine(card, ToBeContinued + "…", 144f, 61f, 94f, UiCandy.Ink, true);
            CreditLine(card, GameTitle + "  ·  2026", 212f, 26f, 39f, UiCandy.Muted, false, 2f);

            var divider = UiKit.MakeImage(card, "Divider", UiDraw.Pill);
            divider.color = UiCandy.Lavender;
            Anch(divider.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, -51f), new Vector2(124f, 4f));

            var buttons = NewRT("Buttons", card);
            Stretch(buttons);
            btnGroup = buttons.gameObject.AddComponent<CanvasGroup>();
            btnGroup.interactable = false;
            btnGroup.blocksRaycasts = false;
            CandyEndButton(buttons, "Вернуться в игру", UiBtn.Green, -197f, 388f, false);
            CandyEndButton(buttons, "Главное меню", UiBtn.Gray, 208f, 358f, true);
        }

        for (float t = 0f; t < .62f; t += Dt)
        {
            candyCard.alpha = Mathf.SmoothStep(0f, 1f, t / .62f);
            FitCandyCard(candyCard.alpha);
            yield return null;
        }
        candyCard.alpha = 1f;
        FitCandyCard(1f);
        candyCard.blocksRaycasts = true;
        btnGroup.interactable = true;
        btnGroup.blocksRaycasts = true;
    }

    void CandyEndButton(RectTransform parent, string text, UiBtn style, float x, float width, bool menu)
    {
        var size = new Vector2(width, 76f);
        var button = UiKit.MakeButton(parent, text, style, size, 30f, 1.4f);
        Anch(button.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(x, -108f), size);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null && portalFont != null) label.font = portalFont;
        button.onClick.AddListener(() => Choose(menu));
    }

    void EnsureCandyBackdrop()
    {
        if (candyBackdrop != null) return;
        portalFont = Resources.Load<TMP_FontAsset>("Fonts/Comfortaa-SemiBold SDF");
        if (portalFont == null) portalFont = font;

        var sky = NewRT("FinaleCloudSky", root);
        Stretch(sky);
        sky.SetSiblingIndex(textRoot.GetSiblingIndex());
        candyBackdrop = sky.gameObject.AddComponent<CanvasGroup>();
        candyBackdrop.alpha = 0f;
        candyBackdrop.blocksRaycasts = false;

        var baseColor = UiKit.MakeImage(sky, "Night", null, false);
        baseColor.color = new Color32(13, 19, 42, 255);
        Stretch(baseColor.rectTransform);
        var horizon = UiKit.MakeImage(sky, "Dusk", GradientSprite(), false);
        horizon.color = new Color32(83, 69, 132, 255);
        Stretch(horizon.rectTransform);
        var haze = UiKit.MakeImage(sky, "PortalGlow", SoftCircle(), false);
        haze.color = new Color(.28f, .65f, .68f, .43f);
        haze.rectTransform.anchorMin = new Vector2(.17f, -.31f);
        haze.rectTransform.anchorMax = new Vector2(.83f, .82f);
        haze.rectTransform.offsetMin = haze.rectTransform.offsetMax = Vector2.zero;

        var random = new System.Random(193);
        for (int i = 0; i < 37; i++)
        {
            var star = UiKit.MakeImage(sky, "Star", i % 8 == 0 ? UiDraw.Star : SoftCircle(), false);
            float u = (float)random.NextDouble();
            float v = .24f + (float)random.NextDouble() * .73f;
            float alpha = (u < .25f || u > .75f ? .36f : .19f) + (float)random.NextDouble() * .15f;
            float size = i % 8 == 0 ? 12f : 3f + (float)random.NextDouble() * 4f;
            star.color = new Color(.89f, .91f, 1f, alpha);
            Anch(star.rectTransform, new Vector2(u, v), new Vector2(.5f, .5f), Vector2.zero, Vector2.one * size);
        }

        backdropClouds = new RectTransform[6];
        AddCloud(0, sky, new Vector2(.04f, .22f), new Vector2(770f, 210f), new Color32(89, 86, 139, 215));
        AddCloud(1, sky, new Vector2(.94f, .27f), new Vector2(710f, 184f), new Color32(90, 89, 144, 218));
        AddCloud(2, sky, new Vector2(.23f, .056f), new Vector2(1060f, 230f), new Color32(52, 59, 106, 248));
        AddCloud(3, sky, new Vector2(.81f, .08f), new Vector2(930f, 226f), new Color32(57, 58, 112, 248));
        AddCloud(4, sky, new Vector2(.14f, -.051f), new Vector2(1380f, 245f), new Color32(31, 40, 76, 255));
        AddCloud(5, sky, new Vector2(.91f, -.04f), new Vector2(1310f, 232f), new Color32(31, 41, 78, 255));
    }

    void AddCloud(int index, Transform parent, Vector2 anchor, Vector2 size, Color color)
    {
        var cloud = UiKit.MakeImage(parent, "Cloud", CloudSprite(), false);
        cloud.color = color;
        Anch(cloud.rectTransform, anchor, new Vector2(.5f, .5f), Vector2.zero, size);
        backdropClouds[index] = cloud.rectTransform;
    }

    Sprite CloudSprite()
    {
        if (cloudSprite != null) return cloudSprite;
        const int width = 216, height = 75;
        cloudTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Finale cloud silhouette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        var lobes = new[]
        {
            new Vector4(.50f, .31f, .49f, .28f), new Vector4(.21f, .47f, .16f, .25f),
            new Vector4(.43f, .55f, .23f, .35f), new Vector4(.66f, .52f, .20f, .30f),
            new Vector4(.82f, .39f, .15f, .23f)
        };
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float coverage = 0f;
                float u = (x + .5f) / width, v = (y + .5f) / height;
                foreach (var e in lobes)
                {
                    float dx = (u - e.x) / e.z, dy = (v - e.y) / e.w;
                    coverage = Mathf.Max(coverage, 1f - Mathf.SmoothStep(0f, 1f,
                        (dx * dx + dy * dy - .95f) / .1f));
                }
                byte shade = (byte)Mathf.Lerp(210f, 255f, v);
                pixels[y * width + x] = new Color32(shade, shade, 255, (byte)(coverage * 255f));
            }
        }
        cloudTexture.SetPixels32(pixels);
        cloudTexture.Apply(false);
        cloudSprite = Sprite.Create(cloudTexture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f));
        return cloudSprite;
    }

    void TickCandyBackdrop()
    {
        if (backdropClouds == null) return;
        if (!reduceFx)
        {
            for (int i = 0; i < backdropClouds.Length; i++)
                backdropClouds[i].anchoredPosition =
                    Vector2.right * (Mathf.Sin(age * (.10f + i * .013f) + i * .63f) * (22f + 3f * i));
        }
        if (candyCard != null) FitCandyCard(candyCard.alpha);
    }

    void FitCandyCard(float alpha)
    {
        float fit = Mathf.Min(1f, Mathf.Min(root.rect.width / 1140f, root.rect.height / 560f));
        candyCard.transform.localScale = Vector3.one * (fit * (reduceFx ? 1f : Mathf.Lerp(.965f, 1f, alpha)));
    }

    IEnumerator FadeCandyBackdrop(float to, float duration)
    {
        float from = candyBackdrop.alpha;
        for (float t = 0f; t < duration; t += Dt)
        {
            candyBackdrop.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            if (skip && !finalShown) break;
            yield return null;
        }
        candyBackdrop.alpha = to;
    }

    IEnumerator FadeCandyCard()
    {
        if (candyCard == null) yield break;
        candyCard.blocksRaycasts = false;
        for (float t = 0f; t < .42f; t += Dt)
        {
            candyCard.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / .42f);
            yield return null;
        }
        candyCard.alpha = 0f;
    }

    void DestroyCandyArt()
    {
        if (cloudSprite != null) Destroy(cloudSprite);
        if (cloudTexture != null) Destroy(cloudTexture);
    }
}

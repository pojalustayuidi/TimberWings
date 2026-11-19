using UnityEngine;

public partial class OrbitCamera
{
    enum FpGlyph { Dot, Tree, Far, Cut, Timber, Collect, Talk, Inspect }

    readonly Sprite[] fpGlyphs = new Sprite[8];
    bool fpGlyphsLoaded;
    float fpContactCueUntil;

    void ReadFirstPersonGlyphs()
    {
        if (fpGlyphsLoaded) return;
        const string folder = "LeoAnimation/Reticles/";
        fpGlyphs[(int)FpGlyph.Dot] = Resources.Load<Sprite>(folder + "Dot");
        fpGlyphs[(int)FpGlyph.Tree] = Resources.Load<Sprite>(folder + "Tree");
        fpGlyphs[(int)FpGlyph.Far] = Resources.Load<Sprite>(folder + "Far");
        fpGlyphs[(int)FpGlyph.Cut] = Resources.Load<Sprite>(folder + "Cut");
        fpGlyphs[(int)FpGlyph.Timber] = Resources.Load<Sprite>(folder + "Timber");
        fpGlyphs[(int)FpGlyph.Collect] = CandyTag.Icon(UiIcon.Backpack);
        fpGlyphs[(int)FpGlyph.Talk] = CandyTag.Icon(UiIcon.Person);
        fpGlyphs[(int)FpGlyph.Inspect] = Resources.Load<Sprite>(folder + "Interact");
        fpGlyphsLoaded = true;
    }

    FpGlyph ReadFirstPersonAim()
    {
        if (interaction == null) return FpGlyph.Dot;
        if (interaction.IsChopping) return FpGlyph.Timber;

        var target = PlayerInteraction.ChoppingEnabled ? interaction.FirstPersonFocus : FirstPersonAim.None;
        return target switch
        {
            FirstPersonAim.Tree => FpGlyph.Tree,
            FirstPersonAim.Cut => FpGlyph.Cut,
            FirstPersonAim.Collect => FpGlyph.Collect,
            FirstPersonAim.FarTree or FirstPersonAim.FarCut or FirstPersonAim.FarCollect => FpGlyph.Far,
            _ => interaction.ReadFirstPersonInteractable(cam) switch
            {
                DialogueSequence => FpGlyph.Talk,
                null => FpGlyph.Dot,
                _ => FpGlyph.Inspect
            }
        };
    }

    void UpdateFirstPersonReticle(float dt)
    {
        ReadFirstPersonGlyphs();
        var result = Chop02Feedback.RecentResult;
        bool contact = Time.unscaledTime < fpContactCueUntil || result != ChopTimingResult.None;
        var glyph = contact ? FpGlyph.Timber : ReadFirstPersonAim();
        var sprite = fpGlyphs[(int)glyph];
        chImg.sprite = sprite != null ? sprite : Reticle();
        chOutline.enabled = glyph == FpGlyph.Talk || glyph == FpGlyph.Collect;
        chImg.color = result switch
        {
            ChopTimingResult.Perfect => new Color32(255, 213, 117, 255),
            ChopTimingResult.Good => new Color32(194, 237, 165, 255),
            ChopTimingResult.Early or ChopTimingResult.Late or ChopTimingResult.Miss => new Color32(225, 195, 235, 255),
            _ when contact => new Color32(244, 218, 162, 255),
            _ => glyph == FpGlyph.Far ? new Color32(255, 210, 150, 255) : Color.white
        };
        float size = glyph switch
        {
            FpGlyph.Dot => 13f,
            FpGlyph.Tree or FpGlyph.Inspect => 22f,
            _ => 26f
        };
        chRt.position = cam.pixelRect.center;
        chRt.sizeDelta = Vector2.one * size * SettingsAppearance.HudScale;
        chT = Mathf.MoveTowards(chT, contact ? 1f : 0f, dt * 13f);
        chRt.localScale = Vector3.one * (VibeSettings.ReduceEffects ? 1f : 1f + chT * .12f);
    }
}

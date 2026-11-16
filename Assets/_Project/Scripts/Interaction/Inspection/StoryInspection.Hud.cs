using System.Collections.Generic;
using UnityEngine;

public sealed partial class StoryInspection
{
    readonly List<Canvas> mutedCanvases = new List<Canvas>();
    readonly List<Renderer> mutedBackpack = new List<Renderer>();

    void HideGameplayHud()
    {
        var legacyHud = UIManager.Instance;
        Canvas legacyCanvas = legacyHud != null ? legacyHud.GetComponentInParent<Canvas>() : null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Canvas root = canvas.rootCanvas;
            int layer = root.sortingOrder;
            bool gameplayHud = root == legacyCanvas || layer == UiLayers.WorldTag ||
                layer == UiLayers.QuestEdgeArrow || layer == UiLayers.QuestGuide ||
                layer == UiLayers.Hud || layer == UiLayers.Popups ||
                (layer == UiLayers.HudOverlay && root.name != "MobileControls_Canvas");
            if (!gameplayHud || !canvas.enabled) continue;
            mutedCanvases.Add(canvas);
        }
        foreach (Canvas canvas in mutedCanvases) canvas.enabled = false;

        if ((!AuthoredKnife && !AuthoredRibbon) || actorRig == null || actorRig.backpack == null) return;
        foreach (Renderer renderer in actorRig.backpack.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            mutedBackpack.Add(renderer);
            renderer.enabled = false;
        }
    }

    void RestoreGameplayHud()
    {
        foreach (Canvas canvas in mutedCanvases)
            if (canvas != null) canvas.enabled = true;
        mutedCanvases.Clear();

        foreach (Renderer renderer in mutedBackpack)
            if (renderer != null) renderer.enabled = true;
        mutedBackpack.Clear();
    }
}

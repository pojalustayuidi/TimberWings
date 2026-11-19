using UnityEngine;

public partial class OrbitCamera
{
    // Hooks the active viewmodel by reference, including the separate CAM03 render camera.
    internal static void SyncFirstPersonRune(LeoRig rig, AxeId axe, AxeRuneId rune)
    {
        if (instance != null && instance.fpRig == rig)
            instance.fpView?.SyncRuneInlay(axe, rune);
    }
}

internal sealed partial class LeoFirstPersonView
{
    readonly AxeRuneFinish viewRune = new AxeRuneFinish();
    Transform runeTarget;
    MeshRenderer runeRenderer;

    internal void SyncRuneInlay(AxeId axe, AxeRuneId rune)
    {
        Transform model = axeModels[(int)axe];
        if (model != runeTarget)
        {
            runeTarget = model;
            runeRenderer = model != null ? model.GetComponent<MeshRenderer>() : null;
        }
        viewRune.Set(runeRenderer, axe, rune);
    }
}

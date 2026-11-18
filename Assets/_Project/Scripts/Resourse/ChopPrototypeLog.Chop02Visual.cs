using UnityEngine;

// Extends CHOP-01's authored cut marker without changing its mesh/save logic.
public sealed partial class ChopPrototypeLog
{
    internal void Chop02StyleCut(int cut, bool boosted)
    {
        if (!CanCut(cut) || markerMaterial == null || cutMarks == null ||
            cut < 0 || cut >= cutMarks.Length || cutMarks[cut] == null) return;

        // Honey-coloured idle marker; muted moss-green when a power action is ready.
        // The pulse belongs to the seam, rather than a full-screen highlight.
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * (boosted ? 8.5f : 3.2f));
        Color idle = new Color(.84f, .68f, .40f);
        Color active = boosted ? new Color(.49f, .84f, .44f) : new Color(.92f, .76f, .48f);
        markerMaterial.color = Color.Lerp(idle, active, boosted ? .70f + pulse * .24f : pulse * .28f);
        Transform mark = cutMarks[cut].transform;
        float width = state.layout.radius * (.72f + .11f * pulse);
        mark.localScale = new Vector3(width, .013f, .033f + .008f * pulse);
    }
}

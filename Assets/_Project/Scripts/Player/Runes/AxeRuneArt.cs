using UnityEngine;

internal static class AxeRuneArt
{
    internal sealed class Sign
    {
        internal readonly Color Accent;
        internal readonly Vector2[][] Lines;

        internal Sign(Color accent, params Vector2[][] lines)
        {
            Accent = accent;
            Lines = lines;
        }
    }

    static readonly Sign[] Signs =
    {
        new Sign(new Color32(169, 153, 208, 255)),
        // Feather on a stem.
        new Sign(new Color32(222, 242, 250, 255),
            Line(-.58f, -.63f, .54f, .63f, .13f, .59f, -.40f, .10f, -.43f, -.40f),
            Line(-.24f, -.25f, .29f, -.06f, .54f, .63f)),
        // Three shoots on the same stem.
        new Sign(new Color32(168, 244, 210, 255),
            Line(0, -.70f, 0, .62f),
            Line(-.63f, .18f, 0, -.15f, .63f, .18f),
            Line(-.46f, .61f, 0, .24f, .46f, .61f)),
        // Two cuts and their edge.
        new Sign(new Color32(99, 239, 240, 255),
            Line(-.68f, -.40f, .39f, .67f, .63f, .43f, -.43f, -.63f),
            Line(-.62f, .08f, -.12f, .57f)),
        // Pack buckle.
        new Sign(new Color32(131, 196, 255, 255),
            Line(-.55f, .26f, -.55f, -.46f, .55f, -.46f, .55f, .26f, -.55f, .26f),
            Line(-.27f, .27f, -.27f, .58f, .27f, .58f, .27f, .27f),
            Line(-.18f, -.10f, .18f, -.10f)),
        // Heartwood.
        new Sign(new Color32(211, 238, 135, 255),
            Line(-.56f, -.43f, 0, -.65f, .56f, -.43f, .56f, .32f, 0, .65f, -.56f, .32f, -.56f, -.43f),
            Line(0, -.37f, 0, .37f, .25f, .19f),
            Line(-.26f, .11f, 0, -.09f)),
        // Sprout and its seed.
        new Sign(new Color32(86, 248, 167, 255),
            Line(-.46f, -.57f, -.02f, -.65f, .39f, -.40f, .39f, -.06f, .01f, .14f, -.45f, -.10f, -.46f, -.57f),
            Line(.01f, .14f, .15f, .59f, .57f, .69f),
            Line(.15f, .59f, -.31f, .48f)),
        // Sun.
        new Sign(new Color32(255, 208, 113, 255),
            Line(0, .38f, .37f, 0, 0, -.38f, -.37f, 0, 0, .38f),
            Line(0, .63f, 0, .79f), Line(.63f, 0, .79f, 0), Line(0, -.63f, 0, -.79f), Line(-.63f, 0, -.79f, 0)),
        // Saw teeth.
        new Sign(new Color32(186, 149, 255, 255),
            Line(-.65f, .37f, .65f, .37f, .65f, -.37f, .31f, .04f, 0, -.37f, -.32f, .04f, -.65f, -.37f, -.65f, .37f)),
        // Squared timber.
        new Sign(new Color32(235, 165, 237, 255),
            Line(-.55f, -.50f, -.55f, .50f, .55f, .50f, .55f, -.50f, -.55f, -.50f),
            Line(-.31f, .13f, -.06f, -.17f, .34f, .25f)),
        // Gunnar's lightning.
        new Sign(new Color32(255, 163, 107, 255),
            Line(-.43f, -.72f, -.04f, -.11f, -.50f, -.11f, .47f, .72f, .03f, .16f, .51f, .16f, -.43f, -.72f))
    };

    static Vector2[] Line(params float[] xy)
    {
        var line = new Vector2[xy.Length / 2];
        for (int i = 0; i < line.Length; i++) line[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
        return line;
    }

    internal static Sign For(AxeRuneId id) => Signs[AxeRunes.IsValid(id) ? (int)id : 0];
}

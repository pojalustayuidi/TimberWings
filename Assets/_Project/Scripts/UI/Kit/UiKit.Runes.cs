using UnityEngine;

public static partial class UiKit
{
    static readonly Sprite[] RuneStamps = new Sprite[(int)AxeRuneId.GunnarStrike + 1];

    internal static Sprite RuneStamp(AxeRuneId id)
    {
        int index = AxeRunes.IsValid(id) ? (int)id : 0;
        if (RuneStamps[index] != null) return RuneStamps[index];

        var painter = new Painter(64, 64);
        Vector2[] corners = { V(32, 4), V(56, 15), V(61, 32), V(54, 51), V(32, 60), V(9, 50), V(3, 31), V(10, 13) };
        var outline = Poly(corners);
        painter.Fill(outline, VGrad(4, 60, C(56, 48, 91), C(144, 127, 177)));
        painter.Fill(Ring(outline, 2.1f), C(223, 213, 252, 205));
        var design = AxeRuneArt.For(id);
        if (id == AxeRuneId.None)
        {
            painter.Fill(Ring(Poly(V(32, 46), V(45, 32), V(32, 18), V(19, 32)), 2f), C(205, 192, 233));
        }
        else
        {
            foreach (Vector2[] line in design.Lines)
                for (int i = 1; i < line.Length; i++)
                {
                    var a = V(32, 32) + line[i - 1] * 24f;
                    var b = V(32, 32) + line[i] * 24f;
                    painter.Fill(Capsule(a.x, a.y, b.x, b.y, 2.0f), design.Accent);
                }
        }
        painter.Outline(1.5f, CandyInk);
        return RuneStamps[index] = Make(painter, 0, 0, 0, 0);
    }
}

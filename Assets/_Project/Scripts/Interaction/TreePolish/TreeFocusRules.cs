using UnityEngine;

// Pure presentation rules: no inventory writes, rolls, timing changes or movement commands.
public static class TreeFocusRules
{
    public enum Kind { None, Nearby, Hover, Walking, Chopping }
    public static Kind Priority(bool chopping, bool walking, bool hover, bool nearby)
    { return chopping ? Kind.Chopping : walking ? Kind.Walking : hover ? Kind.Hover : nearby ? Kind.Nearby : Kind.None; }
    // Compare only ground-plane distance: the crown height should not change
    // whether a tree may be highlighted.
    public static bool InRange(Vector3 player, Vector3 tree, float meters)
    {
        if(meters<=0f)return false;
        float x=player.x-tree.x,z=player.z-tree.z;
        return x*x+z*z<=meters*meters;
    }
    public static int Fits(int baseLogs, int free) { return Mathf.Min(Mathf.Max(0, baseLogs), Mathf.Max(0, free)); }
    public static float Smooth(float value) { float t = Mathf.Clamp01(value); return t * t * (3f - 2f * t); }
    public static Vector3 Flight(Vector3 start, Vector3 control, Vector3 end, float time01)
    {
        float t = Smooth(time01), u = 1f - t;
        return u * u * start + 2f * u * t * control + t * t * end;
    }
}

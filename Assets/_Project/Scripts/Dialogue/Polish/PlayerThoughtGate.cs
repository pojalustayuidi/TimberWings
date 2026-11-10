using System.Collections.Generic;

// Pure timing policy; a thought never becomes an interaction or a quest transition.
public sealed class PlayerThoughtGate
{
    public const float GlobalGap = 60f, HintGap = 120f, StuckDelay = 75f, AfterModal = 12f;
    public float LastProgress { get; private set; }
    public float LastInput { get; private set; }
    float nextAny, nextHint;
    string stage;
    readonly Dictionary<string, int> hints = new Dictionary<string, int>();
    public void Reset(float now) { nextAny = now + AfterModal; nextHint = now + StuckDelay; LastProgress = LastInput = now; stage = ""; hints.Clear(); }
    public void Input(float now) { LastInput = now; }
    public void Progress(float now) { LastProgress = now; }
    public void Blocked(float now) { LastProgress = now; nextAny = System.Math.Max(nextAny, now + AfterModal); }
    public void Stage(string id, float now) { if (stage == id) return; stage = id; Progress(now); }
    public bool CanShow(float now, bool hint)
    {
        if (now < nextAny) return false;
        if (!hint) return true;
        int count; hints.TryGetValue(stage ?? "", out count);
        return now >= nextHint && now - LastProgress >= StuckDelay && now - LastInput <= 12f && count < 2;
    }
    public void Shown(float now, bool hint)
    {
        nextAny = now + GlobalGap;
        if (!hint) return;
        nextHint = now + HintGap; LastProgress = now;
        string key = stage ?? ""; int count; hints.TryGetValue(key, out count); hints[key] = count + 1;
    }
}

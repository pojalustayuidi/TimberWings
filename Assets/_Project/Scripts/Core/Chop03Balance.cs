using System;

internal static class Chop03Balance
{
    internal const int HitsPerCut = 8;
    internal const float PreciseMultiplier = 1.60f;
    internal const float GreenHalfWidth = .17f;
    internal const float ContactInterval = .56f;
    internal const float StandingWindup = .44f;
    internal const float GroundWindup = .50f;
    internal const float Recovery = .12f;
    internal const float ProcessingChainLimit = 18f;

    internal static int StandingHits(TreeSize size) => size == TreeSize.Young ? 3 : size == TreeSize.Old ? 10 : 7;

    // ChopTime already includes ChopPower, both chop talents and the active pet bonus.
    internal static float Power(float baseTime, float upgradedTime, float chainBonus)
    {
        float efficiency = Math.Max(1f, baseTime / Math.Max(.10f, upgradedTime));
        return Math.Min(2.5f, (1f + (efficiency - 1f) * 1.10f) * (1f + chainBonus));
    }

    internal static float AnimationSpeed(int chopLevel, int maxLevel) =>
        1f + .13f * Math.Min(1f, Math.Max(0, chopLevel) / (float)Math.Max(1, maxLevel));

    internal static float Damage(float power, int strikes, bool precise) =>
        power * (precise ? PreciseMultiplier : 1f) / strikes;

    internal static float Advance(float current, float damage)
    {
        float next = current + damage;
        return next >= 1f - .00001f ? 1f : next;
    }
}

internal struct Chop03Swing
{
    const float BeforeContact = .04f;
    float opensAt;
    bool attempted;

    internal float ImpactAt { get; private set; }
    internal bool Armed { get; private set; }
    internal bool Precise { get; private set; }
    internal bool Attempted => attempted;

    internal void Arm(float now, float delay, float recovery)
    {
        opensAt = now + recovery;
        ImpactAt = now + delay;
        Armed = true;
        attempted = Precise = false;
    }

    internal float Marker(float now) =>
        Math.Min(1f, Math.Max(0f, (now - opensAt) / (ImpactAt - opensAt - BeforeContact)));

    internal void TimePress(float now)
    {
        if (!Armed || attempted || now >= ImpactAt) return;
        attempted = true;
        Precise = Math.Abs(Marker(now) - .5f) <= Chop03Balance.GreenHalfWidth;
    }

    internal bool Due(float now) => Armed && now >= ImpactAt;
}

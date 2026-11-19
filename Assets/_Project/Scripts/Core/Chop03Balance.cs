using System;

internal static class Chop03Balance
{
    internal const int HitsPerCut = 8;
    internal const float PreciseMultiplier = 1.60f;
    internal static float GreenHalfWidth => PlayerSkills.Has(SkillIds.WideZone) ? .24f : .17f;
    internal const float ContactInterval = .56f;
    internal const float StandingWindup = .44f;
    internal const float GroundWindup = .50f;
    internal const float Recovery = .12f;
    internal const float ProcessingChainLimit = 18f;

    internal static int StandingHits(TreeSize size) => size == TreeSize.Young ? 3 : size == TreeSize.Old ? 10 : 7;

    internal static float Power(float axePower, int playerLevel, float talentAndPetTime, float chainBonus)
    {
        float playerPower = GameBalance.ChopPowerBonus[Math.Min(GameBalance.ChopPowerBonus.Length - 1, Math.Max(0, playerLevel))];
        float support = Math.Max(0f, 1f / Math.Max(.10f, talentAndPetTime) - 1f) * 1.10f;
        return Math.Min(2.5f, (axePower + playerPower + support) * (1f + chainBonus));
    }

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

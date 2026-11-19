using System;

internal static class Chop03Balance
{
    internal const int HitsPerCut = 8;
    internal const float PreciseMultiplier = 1.28f;
    internal const float OffBeatMultiplier = .90f;
    internal const float ContactInterval = .56f;
    internal const float StandingWindup = .44f;
    internal const float GroundWindup = .50f;
    internal const float Recovery = .23f;
    internal const float ProcessingChainLimit = 18f;

    internal static int StandingHits(TreeSize size) => size == TreeSize.Young ? 3 : size == TreeSize.Old ? 10 : 7;

    internal static float Power(float axePower, int playerLevel, float talentAndPetTime, float chainBonus)
    {
        float playerPower = GameBalance.ChopPowerBonus[Math.Min(GameBalance.ChopPowerBonus.Length - 1, Math.Max(0, playerLevel))];
        float support = Math.Max(0f, 1f / Math.Max(.10f, talentAndPetTime) - 1f) * 1.10f;
        return Math.Min(2.5f, (axePower + playerPower + support) * (1f + chainBonus));
    }

    internal static float Damage(float power, int strikes, ChopTimingResult timing)
    {
        float multiplier = timing switch
        {
            ChopTimingResult.Perfect => PreciseMultiplier,
            ChopTimingResult.Early or ChopTimingResult.Late => OffBeatMultiplier,
            ChopTimingResult.Miss => 0f,
            _ => 1f
        };
        return power * multiplier / strikes;
    }

    internal static float Advance(float current, float damage)
    {
        float next = current + damage;
        return next >= 1f - .00001f ? 1f : next;
    }
}

internal enum ChopTimingResult { None, Early, Good, Perfect, Late, Miss }

internal struct Chop03Swing
{
    internal float OpensAt { get; private set; }
    internal float ImpactAt { get; private set; }
    internal float GoodHalf { get; private set; }
    internal float PerfectHalf { get; private set; }
    internal bool Armed { get; private set; }
    internal ChopTimingResult Result { get; private set; }
    internal bool Attempted => Result != ChopTimingResult.None;

    internal void Arm(float now, float delay, float recovery, bool wideZone)
    {
        OpensAt = now + Math.Max(0f, recovery);
        ImpactAt = Math.Max(now + delay, OpensAt + .04f);
        GoodHalf = wideZone ? GameBalance.RhythmGoodHalf : .20f;
        PerfectHalf = wideZone ? GameBalance.RhythmPerfectHalf : .085f;
        Armed = true;
        Result = ChopTimingResult.None;
    }

    internal float Marker(float now) =>
        Armed ? Math.Min(1f, Math.Max(0f, (now - OpensAt) / (ImpactAt - OpensAt))) : 0f;

    internal bool InGoodZone(float marker) => Math.Abs(marker - .5f) <= GoodHalf;

    internal void TimePress(float now)
    {
        if (!Armed || Attempted || now < OpensAt) return;
        float marker = Marker(now);
        float offCenter = Math.Abs(marker - .5f);
        if (offCenter <= PerfectHalf) Result = ChopTimingResult.Perfect;
        else if (InGoodZone(marker)) Result = ChopTimingResult.Good;
        else Result = marker < .5f ? ChopTimingResult.Early : ChopTimingResult.Late;
    }

    internal bool Due(float now) => Armed && now >= ImpactAt;
}

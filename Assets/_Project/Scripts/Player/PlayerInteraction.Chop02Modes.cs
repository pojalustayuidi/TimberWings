using UnityEngine;

// The shared green-zone swing for standing and felled trees.
public partial class PlayerInteraction
{
    Chop03Swing chop03Swing;
    ChopTimingResult chop03Result;
    bool windupSoundPlayed, timingSoundPlayed;
    int chop03ImpactFrame = -1;
    bool chop03Standing;
    float chop03ReadyAt;

    internal bool IsChop03Working => IsChopping && (chop03Standing || IsProcessingPrototypeLog);
    internal bool Chop03ImpactThisFrame => chop03ImpactFrame == Time.frameCount;
    internal static bool PreciseChop03Impact => CurrentChop03Result == ChopTimingResult.Perfect;
    internal static ChopTimingResult CurrentChop03Result =>
        inst != null && inst.Chop03ImpactThisFrame ? inst.chop03Result : ChopTimingResult.None;
    internal static PlayerInteraction ActivePlayer => inst;
    internal float FirstPersonWindupStart => IsChop03Working && chop03Swing.Armed ? chop03Swing.OpensAt : -1f;
    static bool UsesChopTiming => ChopBonus && !Application.isMobilePlatform;

    internal float AxeSwingSpeed => Chop03Balance.ContactInterval /
        Mathf.Max(.1f, Axes != null ? Axes.EquippedSwingInterval : EquippedAxe.Interval);
    internal float EffectiveChopAnimSpeed => chopAnimSpeed * AxeSwingSpeed;

    float CurrentChopPower(int chain)
    {
        int rank = upgrades != null ? upgrades.Level(UpgradeType.ChopPower) : 0;
        float supportTime = upgrades != null ? upgrades.Bonus.chopMul : 1f;
        return Chop03Balance.Power(Axes != null ? Axes.EquippedPower : EquippedAxe.Power,
            rank, supportTime, LumberSkills.ChainBonus(chain));
    }

    float Chop03Damage(int strikes, ChopTimingResult result) =>
        Chop03Balance.Damage(CurrentChopPower(chainLock), strikes, result);

    void PrepareChop03(bool standing)
    {
        chainLock = LumberSkills.ChainNow;
        IsChopping = true;
        waitingForChopAxe = true;
        chop03Standing = standing;
        savedItem = null;
        chop03Swing = default;
    }

    void ArmChop03Swing(float interval, float recovery)
    {
        float speed = AxeSwingSpeed;
        float now = Time.time;
        float windup = Mathf.Max(.12f, (interval - recovery) / speed);
        float delay = Mathf.Max(windup + recovery / speed, chop03ReadyAt - now);
        chop03Swing.Arm(now, delay, delay - windup, PlayerSkills.Has(SkillIds.WideZone));
        windupSoundPlayed = timingSoundPlayed = false;
        nextHitAt = chop03Swing.ImpactAt;
    }

    bool TickChop03Swing(bool ground, out ChopTimingResult result)
    {
        result = ChopTimingResult.None;
        if (!LeoAxeReady) return false;
        if (waitingForChopAxe)
        {
            waitingForChopAxe = false;
            if (!ground && TargetItem != null)
            {
                TargetItem.SetChopping(true);
                ReadLeoImpact(TargetItem);
                TargetItem.PrepareStandingNotch(leoHasImpact ? leoImpact : TreePromptPoint(TargetItem),
                    transform.position);
                leoImpactFrame = -1;
            }

            ResolveChopParam();
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(chopBool)) animator.SetBool(chopBool, true);
                if (!string.IsNullOrEmpty(chopTrigger)) animator.SetTrigger(chopTrigger);
            }
            ArmChop03Swing(ground ? Chop03Balance.GroundWindup : Chop03Balance.StandingWindup, 0f);
        }

        float now = Time.time;
        float marker = chop03Swing.Marker(now);
        if (!windupSoundPlayed && marker >= .60f && marker < 1f)
        {
            windupSoundPlayed = true;
            bool heavy = ground || EquippedAxe.Id == AxeId.Iron || EquippedAxe.Id == AxeId.Gunnar;
            SfxManager.Timber(heavy ? "axeSwingHeavy" : "axeSwing", .57f,
                Mathf.Lerp(.96f, 1.08f, Mathf.InverseLerp(.85f, 1.28f, AxeSwingSpeed)));
        }
        if (UsesChopTiming)
        {
            if (!timingSoundPlayed && !chop03Swing.Attempted && chop03Swing.InGoodZone(marker))
            {
                timingSoundPlayed = true;
                if (!VibeSettings.ReduceEffects) SfxManager.Timber("timingWindow", .26f);
            }
            if (MobileInput.BonusDown) chop03Swing.TimePress(now);
        }
        if (!chop03Swing.Due(now)) return false;

        result = UsesChopTiming ? chop03Swing.Result : ChopTimingResult.None;
        chop03FirstHitPending = false;
        chop03Result = result;
        chop03ImpactFrame = Time.frameCount;
        chop03ReadyAt = now + (Axes != null ? Axes.EquippedSwingInterval : EquippedAxe.Interval);
        ArmChop03Swing(Chop03Balance.ContactInterval, ground ? .19f : Chop03Balance.Recovery);
        return true;
    }

    void TickChop03Standing(ResourceItem tree)
    {
        if (!IsChopping || !chop03Standing || TargetItem != tree)
        {
            StopChop();
            TargetItem = tree;
            CurrentTarget = tree.transform;
            Progress = tree.StandingDamage;
            PrepareChop03(true);
        }

        if (TickChop03Swing(false, out var result))
        {
            ReadLeoImpact(tree); // Snapshot the point the axe is reaching before the notch deepens.
            float previous = tree.StandingDamage;
            int strikes = tree.IsGolden ? 3 : Chop03Balance.StandingHits(tree.Size);
            float damage = Chop03Damage(strikes, result);
            if (ActiveRune == AxeRuneId.GunnarStrike && IsOrdinaryRuneTree(tree)) damage = 1f;
            bool precise = result == ChopTimingResult.Perfect;
            float summer = precise && ActiveRune == AxeRuneId.SummerSpark ? Chop03Damage(strikes, ChopTimingResult.None) : 0f;
            Progress = tree.DamageStanding(damage, summer);
            float strength = (precise ? 1.26f : .85f) + Progress * .26f;
            var hits = GetTreeHits(tree);
            if (hits != null) hits.Shake(strength, leoHasImpact ? leoImpact : (Vector3?)null);
            GameEvents.RaiseChopHit();
            FinishChop03Impact(result, leoHasImpact ? leoImpact : TreePromptPoint(tree));
            if (Progress >= 1f)
            {
                CompleteChop();
                chop03Tree = null;
                Chop02Feedback.Hide();
                return;
            }

            if (previous < .74f && Progress >= .74f)
                SfxManager.Timber("woodCrack", .46f, .90f, leoHasImpact ? leoImpact : TreePromptPoint(tree));
        }
        ShowChop03Swing(TreePromptPoint(tree), Progress);
    }

    void TickGroundSwing(LogTarget target)
    {
        if (!IsProcessingPrototypeLog || !processing.Same(target))
        {
            StopChop();
            TargetItem = lastItem = null;
            CurrentTarget = target.log.CutAnchor(target.index);
            lastTarget = null;
            processing = target;
            Progress = target.log.CutProgress(target.index);
            PrepareChop03(false);
        }

        if (TickChop03Swing(true, out var result))
        {
            leoImpact = target.log.CutContact(target.index, transform.position);
            leoImpactFrame = Time.frameCount;
            leoHasImpact = true;
            bool precise = result == ChopTimingResult.Perfect;
            bool opened = target.log.Hit(target.index,
                Chop03Damage(target.log.RequiredCutHits(target.index), result), precise);
            Progress = target.log.CutProgress(target.index);
            GameEvents.RaiseChopHit();
            FinishChop03Impact(result, leoImpact);
            if (opened)
            {
                StopPrototypeWork();
                chop03Log = default;
                Chop02Feedback.Hide();
                return;
            }
        }

        ShowChop03Swing(target.log.CutContact(target.index, transform.position), Progress);
    }

    void FinishChop03Impact(ChopTimingResult result, Vector3 contact)
    {
        if (result == ChopTimingResult.None) return;
        bool success = result == ChopTimingResult.Good || result == ChopTimingResult.Perfect;
        bool first = success && NeedsChopTimingHint && bonusHits == 0;
        if (success) RegisterBonusHit();
        GameEvents.RaiseChopQuality(result == ChopTimingResult.Perfect ? 2 : success ? 1 : 0);
        Chop02Feedback.Report(result, contact, first);
    }

    void ShowChop03Swing(Vector3 point, float progress)
    {
        bool meter = UsesChopTiming && chop03Swing.Armed && !waitingForChopAxe;
        string instruction = null;
        if (meter && Time.time >= chop03Swing.OpensAt && !chop03Swing.Attempted)
            instruction = NeedsChopTimingHint
                ? ChopPrototypeLog.Text("Пробел в зелёной зоне", "Space in the green zone")
                : ChopPrototypeLog.Text("Пробел · удар", "Space · strike");
        Chop02Feedback.Show(point, instruction, progress, meter ? chop03Swing : default, working: true);
    }
}

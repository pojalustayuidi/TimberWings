using UnityEngine;

// The shared green-zone swing for standing and felled trees.
public partial class PlayerInteraction
{
    Chop03Swing chop03Swing;
    bool chop03Accurate;
    int chop03ImpactFrame = -1;
    bool chop03Standing;
    float chop03ReadyAt;

    internal bool IsChop03Working => IsChopping && (chop03Standing || IsProcessingPrototypeLog);
    internal bool Chop03ImpactThisFrame => chop03ImpactFrame == Time.frameCount;
    internal static bool PreciseChop03Impact => inst != null && inst.Chop03ImpactThisFrame && inst.chop03Accurate;

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

    float Chop03Damage(int strikes, bool precise) =>
        Chop03Balance.Damage(CurrentChopPower(chainLock), strikes, precise);

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
        chop03Swing.Arm(now, Mathf.Max(interval / speed, chop03ReadyAt - now), recovery / speed);
        nextHitAt = chop03Swing.ImpactAt;
    }

    bool TickChop03Swing(bool ground, out bool precise)
    {
        precise = false;
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

        if (ChopBonus && MobileInput.BonusDown) chop03Swing.TimePress(Time.time);
        if (!chop03Swing.Due(Time.time)) return false;

        precise = ChopBonus && chop03Swing.Precise;
        chop03FirstHitPending = false;
        chop03Accurate = precise;
        chop03ImpactFrame = Time.frameCount;
        chop03ReadyAt = Time.time + (Axes != null ? Axes.EquippedSwingInterval : EquippedAxe.Interval);
        ArmChop03Swing(Chop03Balance.ContactInterval, Chop03Balance.Recovery);
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

        if (TickChop03Swing(false, out bool precise))
        {
            ReadLeoImpact(tree); // Snapshot the point the axe is reaching before the notch deepens.
            float previous = tree.StandingDamage;
            float damage = Chop03Damage(tree.IsGolden ? 3 : Chop03Balance.StandingHits(tree.Size), precise);
            if (ActiveRune == AxeRuneId.GunnarStrike && IsOrdinaryRuneTree(tree)) damage = 1f;
            float summer = precise && ActiveRune == AxeRuneId.SummerSpark
                ? Chop03Damage(tree.IsGolden ? 3 : Chop03Balance.StandingHits(tree.Size), false) : 0f;
            Progress = tree.DamageStanding(damage, summer);
            float strength = (precise ? 1.37f : .88f) + Progress * .34f;
            var hits = GetTreeHits(tree);
            if (hits != null) hits.Shake(strength, leoHasImpact ? leoImpact : (Vector3?)null);
            GameEvents.RaiseChopHit();
            FinishChop03Impact(precise);
            if (Progress >= 1f)
            {
                CompleteChop();
                chop03Tree = null;
                return;
            }

            if (previous < .74f && Progress >= .74f)
                SfxManager.Play("woodCrack", .29f, .83f);
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

        if (TickChop03Swing(true, out bool precise))
        {
            leoImpact = target.log.CutContact(target.index, transform.position);
            leoImpactFrame = Time.frameCount;
            leoHasImpact = true;
            bool opened = target.log.Hit(target.index, Chop03Damage(target.log.RequiredCutHits(target.index), precise), precise);
            Progress = target.log.CutProgress(target.index);
            GameEvents.RaiseChopHit();
            FinishChop03Impact(precise);
            if (opened)
            {
                StopPrototypeWork();
                chop03Log = default;
                return;
            }
        }

        ShowChop03Swing(target.log.CutContact(target.index, transform.position), Progress);
    }

    void FinishChop03Impact(bool precise)
    {
        if (!precise) return;
        RegisterBonusHit();
        GameEvents.RaiseChopQuality(2);
        Chop02Feedback.Pulse();
    }

    void ShowChop03Swing(Vector3 point, float progress)
    {
        bool meter = ChopBonus && chop03Swing.Armed && !waitingForChopAxe;
        string instruction = meter && BonusTeaching
            ? ChopPrototypeLog.Text("Пробел в зелёной зоне — сильнее", "Space in green — stronger hit")
            : null;
        Chop02Feedback.Show(point, instruction, progress, meter,
            meter ? chop03Swing.Marker(Time.time) : 0f, chop03Swing.Attempted, chop03Swing.Precise, working: true);
    }
}

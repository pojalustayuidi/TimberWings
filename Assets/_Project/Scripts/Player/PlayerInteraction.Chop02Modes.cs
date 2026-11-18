using UnityEngine;

// CHOP-02 is deliberately isolated from CHOP-01 save and reward accounting.
// The experimental A mode deliberately takes longer than B/C/D after player feedback.
// All modes still use the same CHOP-01 cuts, saves, and reward accounting.
public partial class PlayerInteraction
{
    enum Chop02Mode { GreenZone, PreciseNotch, Combo, Charged }
    const float Chop02BaseStrength = .42f; // B/C/D baseline, CHOP-01 advances by strength / 2.
    // A: 10 normal strikes or 6 perfectly timed strikes per notch at 1x strength.
    // A success speeds up the work, but no longer finishes a notch in 2-3 hits.
    const float Chop02GreenNormalStrength = .20f;
    const float Chop02GreenPerfectStrength = .36f;
    const float Chop02HitSpacing = .72f;
    const float Chop02MeterSweepSeconds = .72f;
    const float Chop02GreenHalf = .125f;
    const float Chop02ChargePeakAt = .82f;
    const float Chop02ChargePlateauAt = 1.10f;
    const float Chop02ComboWindow = 1.08f;

    Chop02Mode chop02Mode;
    float chop02Clock;
    float chop02BoostWindowEnd;
    float chop02ChargeBegan = -1f;
    int chop02NormalHits;
    bool chop02AQueued;
    bool chop02AWindowConsumed;
    bool chop02ChargeReady;
    bool chop02TapQueued;
    bool chop02HitDuringHold;
    LogTarget chop02TapTarget;
    float chop02TapPrecision;
    bool chop02ChargePending;
    LogTarget chop02PendingChargeTarget;
    float chop02HeldBeforeReady;
    string chop02Message;

    bool Chop02UsesCharge => chop02Mode == Chop02Mode.Charged;
    bool Chop02PrecisionRequiresAim => chop02Mode == Chop02Mode.PreciseNotch;
    internal bool Chop02ChargeActive => IsProcessingPrototypeLog && Chop02UsesCharge && chop02ChargeReady;

    static string Chop02Name(int mode)
    {
        switch (mode)
        {
            case 0: return ChopPrototypeLog.Text("A · Зелёная зона", "A · Green zone");
            case 1: return ChopPrototypeLog.Text("B · Точная зарубка", "B · Precise notch");
            case 2: return ChopPrototypeLog.Text("C · Мощный раскол", "C · Power split");
            default: return ChopPrototypeLog.Text("D · Заряд удара", "D · Charged strike");
        }
    }

    void Chop02NextMode()
    {
        chop02Mode = (Chop02Mode)(((int)chop02Mode + 1) % 4);
        Chop02Feedback.ModeChanged(Chop02Name((int)chop02Mode));
    }

    string Chop02CutPrompt(bool firstPerson)
    {
        string action = firstPerson || Chop02PrecisionRequiresAim ? "ЛКМ" : "ЛКМ / E";
        switch (chop02Mode)
        {
            case Chop02Mode.GreenZone:
                return ChopPrototypeLog.Text("Удерживай ", "Hold ") + action +
                       ChopPrototypeLog.Text(" · Пробел в зелёном", " · Space in green");
            case Chop02Mode.PreciseNotch:
                return ChopPrototypeLog.Text("Целься в центр зарубки · ", "Aim at notch centre · ") + action;
            case Chop02Mode.Combo:
                return ChopPrototypeLog.Text("Удерживай ", "Hold ") + action +
                       ChopPrototypeLog.Text(" · Пробел при вспышке", " · Space on cue");
            default:
                return ChopPrototypeLog.Text("Зажми и отпусти ", "Hold and release ") + action;
        }
    }

    void Chop02BeginCut(LogTarget target)
    {
        Chop02ResetWork();
        // Charge begins at button press, not after the holstered axe finishes drawing.
        if (Chop02UsesCharge)
        {
            chop02ChargeBegan = Time.time;
            chop02ChargeReady = true;
        }
        chop02Message = null;
    }

    void Chop02Ready()
    {
        if (Chop02UsesCharge && chop02ChargeBegan < 0f)
        {
            chop02ChargeReady = true;
            chop02ChargeBegan = Time.time;
        }
        chop02Clock = 0f;
        // The current Leo controllers forecast impact, reusing their ground swing.
        if (Chop02UsesCharge) nextHitAt = Time.time + Chop02ChargePeakAt;
    }

    void Chop02ResetWork()
    {
        chop02AQueued = false;
        chop02AWindowConsumed = false;
        chop02ChargeReady = false;
        chop02ChargeBegan = -1f;
        chop02ChargePending = false;
        chop02PendingChargeTarget = default;
        chop02HeldBeforeReady = 0f;
        chop02BoostWindowEnd = 0f;
        chop02Clock = 0f;
        chop02NormalHits = 0;
        Chop02ClearTap();
        chop02Message = null;
    }

    void Chop02ClearTap()
    {
        chop02TapQueued = false;
        chop02HitDuringHold = false;
        chop02TapTarget = default;
        chop02TapPrecision = 0f;
    }

    float Chop02PowerMultiplier => upgrades == null ? 1f :
        Mathf.Max(1f, GameBalance.ChopTime / Mathf.Max(.1f, upgrades.ChopTime));

    void Chop02TickSwing(LogTarget target)
    {
        if (!target.Valid) return;
        chop02Clock += Time.deltaTime;
        if (Chop02UsesCharge)
        {
            if (chop02ChargePending)
            {
                // A very quick tap can end before Leo has drawn his axe. Finish
                // the queued weak strike once the existing rig is ready.
                Chop02ReleaseCharge(target);
                StopPrototypeWork();
                return;
            }
            if (!chop02ChargeReady)
            {
                chop02ChargeReady = true;
                chop02ChargeBegan = Time.time;
            }
            // No hit is emitted before release. Forecast the expected sweet spot
            // so both existing Leo rigs prepare a downward swing, not an idle pose.
            // Re-arm the visual forecast when someone deliberately overcharges.
            if (Time.time > nextHitAt + .12f) nextHitAt = Time.time + .62f;
            return;
        }

        if (chop02Mode == Chop02Mode.GreenZone && Input.GetKeyDown(KeyCode.Space) && !chop02AWindowConsumed)
        {
            float marker = Chop02Marker;
            chop02AQueued = Mathf.Abs(marker - .5f) <= Chop02GreenHalf;
            chop02AWindowConsumed = true;
            chop02Message = chop02AQueued
                ? ChopPrototypeLog.Text("Отличный замах!", "Great timing!")
                : ChopPrototypeLog.Text("Обычный удар", "Regular hit");
            }

        if (chop02Mode == Chop02Mode.Combo && Time.time <= chop02BoostWindowEnd &&
            Input.GetKeyDown(KeyCode.Space))
        {
            chop02BoostWindowEnd = 0f;
            chop02NormalHits = 0;
            Chop02ApplyHit(target, 1.20f, true);
            nextHitAt = Time.time + Chop02HitSpacing;
            return;
        }
        if (Time.time < nextHitAt) return;
        nextHitAt = Time.time + Mathf.Max(hitInterval, Chop02HitSpacing);
        bool powerful = false;
        float strength = Chop02BaseStrength;
        if (chop02Mode == Chop02Mode.GreenZone)
        {
            powerful = chop02AQueued;
            strength = powerful ? Chop02GreenPerfectStrength : Chop02GreenNormalStrength;
            chop02AQueued = chop02AWindowConsumed = false;
        }
        else if (chop02Mode == Chop02Mode.PreciseNotch)
        {
            // The CHOP-01 cut zone is a generous longitudinal band, not a tiny pixel.
            // 0.45+ accuracy is the broad central sweet spot. If the pointer has moved
            // off the notch, PickPrototypeLog/held-target matching cancels the swing.
            float precision = Chop02Precision(target);
            powerful = precision >= .57f;
            if (powerful) strength = .92f;
        }
        else if (chop02Mode == Chop02Mode.Combo)
        {
            chop02NormalHits++;
            if (chop02NormalHits >= 2)
            {
                chop02NormalHits = 0;
                chop02BoostWindowEnd = Time.time + Chop02ComboWindow;
                        chop02Message = ChopPrototypeLog.Text("ПРОБЕЛ · Мощный раскол!", "SPACE · Power split!");
            }
        }
        Chop02ApplyHit(target, strength, powerful);
    }

    float Chop02Marker => Mathf.PingPong(chop02Clock / Chop02MeterSweepSeconds, 1f);

    float Chop02Precision(LogTarget target)
    {
        // A brief mouse click keeps its precision sampled on release until the
        // first scheduled impact. Holding instead reads fresh ray accuracy.
        if (chop02TapQueued && chop02TapTarget.Same(target)) return chop02TapPrecision;
        if (target.log == null || !chop02RayPointValid ||
            !mouseCut.Valid || !mouseCut.Same(target)) return 0f;
        Vector3 local = target.log.transform.InverseTransformPoint(chop02RayPoint);
        float z = target.log.CutAnchor(target.index).localPosition.z;
        float half = Mathf.Clamp(target.log.Layout.length * .135f, .16f, .29f);
        float axial = Mathf.Abs(local.z - z) / half;
        float across = Mathf.Abs(local.x) / Mathf.Max(.1f, target.log.Layout.radius * 1.15f);
        return Mathf.Clamp01(1f - axial * .75f - across * .25f);
    }

    void Chop02ReleaseCharge(LogTarget target)
    {
        if (!Chop02UsesCharge || !chop02ChargeReady || !target.Valid) return;
        float held = chop02ChargePending ? chop02HeldBeforeReady :
            Mathf.Max(0f, Time.time - chop02ChargeBegan);
        if (!LeoAxeReady)
        {
            chop02ChargePending = true;
            chop02PendingChargeTarget = target;
            chop02HeldBeforeReady = held;
            return;
        }
        chop02ChargePending = false;
        chop02PendingChargeTarget = default;
        // Early hits are useful, the optimum is rewarded, overcharging just
        // saturates. No stamina punishment or progress loss.
        float strength;
        bool powerful;
        if (held < .36f) { strength = .33f; powerful = false; }
        else if (held < Chop02ChargePeakAt)
        {
            strength = Mathf.Lerp(.38f, 1.00f, Mathf.InverseLerp(.36f, Chop02ChargePeakAt, held));
            powerful = held >= .64f;
        }
        else { strength = 1.06f; powerful = true; }
        if (held > Chop02ChargePlateauAt) strength = 1.06f;
        chop02ChargeReady = false;
        nextHitAt = Time.time;
        Chop02ApplyHit(target, strength, powerful);
        chop02Message = powerful
            ? ChopPrototypeLog.Text("Тяжёлый удар!", "Heavy strike!")
            : ChopPrototypeLog.Text("Обычный удар", "Regular strike");
        Chop02Feedback.Pulse(chop02Message);
    }

    void Chop02ApplyHit(LogTarget target, float strength, bool powerful)
    {
        if (!target.Valid || !IsProcessingPrototypeLog || !processing.Same(target)) return;
        leoImpactFrame = Time.frameCount;
        leoHasImpact = TryReadPrototypeContact(out leoImpact, out _);
        GameEvents.RaiseChopHit(); // one animation/contact event per counted strike
        if (powerful)
        {
            Vector3 point = target.log.CutContact(target.index, transform.position);
            if (!VibeSettings.ReduceEffects) ImpactFx.PlayLogDebris(point, true);
            SfxManager.Play("woodCrack", .48f, .90f);
            chop02Message = ChopPrototypeLog.Text("Сильный удар!", "Power hit!");
            Chop02Feedback.Pulse(chop02Message);
        }
        bool finished = target.log.Hit(target.index, strength * Chop02PowerMultiplier);
        Progress = target.log.CutProgress(target.index);
        if (chop02Mode == Chop02Mode.PreciseNotch)
        {
            chop02HitDuringHold = true;
            // Tap = one queued swing; hold = repeated swings.
            if (chop02TapQueued)
            {
                Chop02ClearTap();
                StopPrototypeWork();
                return;
            }
        }
        if (finished)
        {
            mouseCut = keyboardCut = default;
            StopPrototypeWork();
        }
    }

    void Chop02Present(LogTarget target)
    {
        if (!ChopPrototypeLog.Enabled || !target.Valid || !target.cut || !InCutStance(target))
        {
            Chop02Feedback.Hide();
            return;
        }
        float marker = 0f;
        bool meter = false;
        bool powerWindow = false;
        string cue = null;
        if (IsProcessingPrototypeLog && processing.Same(target))
        {
            if (chop02Mode == Chop02Mode.GreenZone)
            {
                meter = true;
                marker = Chop02Marker;
                cue = ChopPrototypeLog.Text("ПРОБЕЛ · зелёная зона", "SPACE · green zone");
            }
            else if (chop02Mode == Chop02Mode.PreciseNotch)
            {
                meter = true;
                marker = Chop02Precision(target);
                cue = ChopPrototypeLog.Text("Центр зарубки = сильнее", "Centre hit = stronger");
            }
            else if (chop02Mode == Chop02Mode.Combo)
            {
                powerWindow = Time.time <= chop02BoostWindowEnd;
                marker = powerWindow ? Mathf.Clamp01((chop02BoostWindowEnd - Time.time) / Chop02ComboWindow) : 0f;
                meter = powerWindow;
                cue = powerWindow ? ChopPrototypeLog.Text("ПРОБЕЛ · РАСКОЛ!", "SPACE · SPLIT!") :
                    ChopPrototypeLog.Text("Руби · раскол после 2 ударов", "Chop · split after 2 hits");
            }
            else
            {
                meter = true;
                float held = chop02ChargeReady ? Time.time - chop02ChargeBegan : 0f;
                marker = Mathf.Clamp01(held / Chop02ChargePeakAt);
                powerWindow = held >= Chop02ChargePeakAt;
                cue = powerWindow ? ChopPrototypeLog.Text("Отпускай · максимум", "Release · max power") :
                    ChopPrototypeLog.Text("Заряжай и отпускай ЛКМ", "Charge then release LMB");
            }
        }
        else if (chop02Mode == Chop02Mode.PreciseNotch)
        {
            meter = true;
            marker = Chop02Precision(target);
        }
        target.log.Chop02StyleCut(target.index, powerWindow || (chop02Mode == Chop02Mode.GreenZone && Mathf.Abs(marker - .5f) <= Chop02GreenHalf));
        Chop02Feedback.DrawNear(target.log.CutAnchor(target.index), Chop02Name((int)chop02Mode),
            target.log.CutProgress(target.index), meter, marker,
            chop02Mode == Chop02Mode.GreenZone ? Chop02GreenHalf : .14f,
            chop02Mode == Chop02Mode.GreenZone, powerWindow, cue);
    }
}

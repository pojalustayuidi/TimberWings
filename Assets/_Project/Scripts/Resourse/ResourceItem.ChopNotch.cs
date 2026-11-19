using UnityEngine;

public partial class ResourceItem
{
    ChopPrototypeSave.Standing standingNotch;
    TreeChopNotch standingVisual;

    internal float StandingDamage => standingNotch?.damage ?? 0f;
    internal int SummerStrikes => standingNotch?.summerStrikes ?? 0;

    internal void PrepareStandingNotch(Vector3 contact, Vector3 actor)
    {
        if (standingNotch != null || string.IsNullOrEmpty(ChopSaveId)) return;
        Vector3 from = Vector3.ProjectOnPlane(transform.InverseTransformPoint(actor), Vector3.up);
        Vector3 outward = from.sqrMagnitude > .001f ? from.normalized : Vector3.forward;
        float scale = Mathf.Max(.001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
        standingNotch = new ChopPrototypeSave.Standing
        {
            id = ChopSaveId,
            localPoint = transform.InverseTransformPoint(contact),
            localOutward = outward,
            radius = Mathf.Clamp(TrunkRadius / scale, .043f, 2.7f)
        };
        standingVisual = new TreeChopNotch(this);
        standingVisual.Seat(standingNotch);
    }

    internal bool TryGetStandingContact(out Vector3 point)
    {
        point = default;
        if (standingVisual == null || standingNotch == null) return false;
        point = standingVisual.Contact(standingNotch);
        return true;
    }

    internal bool TryGetStandingSide(out Vector3 side)
    {
        side = default;
        if (StandingDamage <= 0f) return false;
        side = Vector3.ProjectOnPlane(transform.TransformDirection(standingNotch.localOutward), Vector3.up).normalized;
        return true;
    }

    internal float DamageStanding(float damage, float summerDamage = 0f)
    {
        if (standingNotch == null) return 0f;
        if (summerDamage > 0f && AxeRunes.AdvanceCycle(ref standingNotch.summerStrikes, AxeRunes.SparkInterval))
            damage += summerDamage;
        float result = Chop03Balance.Advance(standingNotch.damage, damage);
        // A landing plan can fail after the last contact. Keep a real, nearly
        // finished notch available for another attempt in that case.
        standingNotch.damage = Mathf.Min(.999f, result);
        standingVisual.Draw(standingNotch);
        ChopPrototypeSave.RecordStanding(standingNotch);
        return result;
    }

    internal void RestoreStandingNotch(ChopPrototypeSave.Standing saved)
    {
        ReleaseStandingVisual();
        standingNotch = saved;
        if (saved == null || !isActiveAndEnabled) return;
        standingVisual = new TreeChopNotch(this);
        standingVisual.Draw(saved);
    }

    void ClearStandingDamage()
    {
        standingNotch = null;
        ChopPrototypeSave.RemoveStanding(ChopSaveId);
    }

    internal void ReleaseFallenNotch()
    {
        if (standingNotch == null) ReleaseStandingVisual();
    }

    void ReleaseStandingVisual()
    {
        standingVisual?.Dispose();
        standingVisual = null;
    }
}

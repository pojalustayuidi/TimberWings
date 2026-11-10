// Pure presentation rules. No quest, inventory, save or input mutations.
public static class MiraInteractionRules
{
    public enum Mode { Hidden, Talk, Report, Deliver, Reminder, Free }
    public static Mode Resolve(bool busy, bool deliveryPending, bool hasLogs, bool storyTalk, bool report, bool task)
    {
        if (busy) return Mode.Hidden;
        if (deliveryPending) return hasLogs ? Mode.Deliver : Mode.Reminder;
        if (storyTalk) return report ? Mode.Report : Mode.Talk;
        return task ? Mode.Reminder : Mode.Free;
    }
    public static bool OwnsInteraction(Mode mode) { return mode == Mode.Reminder || mode == Mode.Free; }
    public static float Blink(float elapsed)
    {
        if (elapsed < 0f || elapsed >= .19f) return 1f;
        float t = elapsed / .19f;
        return .05f + .95f * (t < .42f ? 1f - t / .42f : (t - .42f) / .58f);
    }
    // One greeting per approach; crossing the inner radius repeatedly cannot retrigger it.
    public sealed class Approach
    {
        public bool inside;
        float next;
        public void Reset(float now) { inside = true; next = now + 12f; }
        public bool Step(float distance, float radius, float now, bool allowed)
        {
            if (distance > radius + 1.8f) inside = false;
            if (distance > radius || inside) return false;
            inside = true;
            if (!allowed || now < next) return false;
            next = now + 90f;
            return true;
        }
    }
}
